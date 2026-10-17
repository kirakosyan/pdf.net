namespace Fluent.Pdf;
internal sealed class PdfSyntaxReader
{
    private readonly PdfByteStore _source;
    private readonly PdfOperationContext _context;
    private readonly List<PdfRevision> _revisions = new();
    private readonly HashSet<long> _xrefOffsets = new();
    private readonly HashSet<(int, int, int)> _resolving = new();
    private readonly Dictionary<(int, int, int), PdfValue> _resolved = new();
    private readonly PdfSyntaxCursor _errors;
    internal PdfSyntaxReader(PdfByteStore source, PdfOperationContext context)
    {
        _source = source;
        _context = context;
        _errors = new(source, context);
    }

    internal PdfSyntaxDocument Read()
    {
        try
        {
            if (_source.Length < 16 || !Match(0, "%PDF-"))
                throw Error("Syntax.Header", "A supported PDF header is required at the beginning of the input.");
            string version = _errors.Text(5, 3);
            if (version != "1.2" && version != "1.3" && version != "1.4" && version != "1.5" && version != "1.6" && version != "1.7" && version != "2.0")
                throw Error("Syntax.VersionUnsupported", "The PDF header version is outside 1.2–2.0.");
            if (!PdfSyntaxCursor.White(_source.At(8)))
                throw Error("Syntax.Header", "The PDF header version is not terminated.");
            long tail = _source.Length;
            while (tail > 0 && PdfSyntaxCursor.White(_source.At(tail - 1)))
                tail--;
            if (tail < 5 || !Match(tail - 5, "%%EOF"))
                throw Error("Syntax.EndOfFile", "The final EOF marker is missing or truncated.");
            long marker = -1;
            for (long i = tail - 14; i >= Math.Max(0, tail - 1024); i--)
            {
                if ((i & 255) == 0)
                    _context.Checkpoint();
                if (Match(i, "startxref") && (i == 0 || PdfSyntaxCursor.White(_source.At(i - 1))))
                {
                    marker = i;
                    break;
                }
            }

            if (marker < 0)
                throw Error("Syntax.StartXref", "The final startxref marker is missing.");
            var last = new PdfSyntaxCursor(_source, _context, marker + 9, tail - 5);
            long next = last.Integer();
            last.Skip();
            if (last.Position != last.End)
                throw Error("Syntax.StartXref", "Unexpected data follows the final cross-reference offset.");
            while (true)
            {
                _context.Checkpoint();
                if (_revisions.Count >= _context.Policy.Limits.MaximumDepth)
                    _context.RejectLimit(PdfResourceKind.Depth);
                var revision = ReadRevision(next);
                _revisions.Add(revision);
                if (!revision.Trailer.Entries.TryGetValue("Prev", out var previous))
                    break;
                next = DirectInteger(previous);
            }

            for (int revision = 0; revision < _revisions.Count; revision++)
            {
                var seen = new HashSet<int>();
                for (int i = revision; i < _revisions.Count; i++)
                    foreach (var entry in _revisions[i].Entries)
                        if (seen.Add(entry.Id.Number) && entry.Kind != PdfCrossReferenceKind.Free)
                            _revisions[revision].Values.Add(entry.Id.Number, Resolve(entry.Id, revision));
            }

            return new PdfSyntaxDocument(version, _source, _revisions);
        }
        catch (OverflowException)
        {
            throw Error("Syntax.IntegerRange", "A syntax count or offset overflowed its supported range.");
        }
        catch (System.IO.InvalidDataException)
        {
            throw Error("Syntax.IntegerRange", "An integer field is malformed or outside its supported range.");
        }
    }

    private PdfRevision ReadRevision(long offset)
    {
        CheckOffset(offset);
        if (!_xrefOffsets.Add(offset))
            throw Error("Syntax.XrefCycle", "Cross-reference offsets form a cycle or alias an already used section.");
        _context.Consume(PdfResourceKind.Objects, 1);
        var cursor = new PdfSyntaxCursor(_source, _context, offset);
        var entries = new Dictionary<int, PdfCrossReference>();
        PdfValue trailer;
        bool hybrid = false;
        if (cursor.TryKeyword("xref"))
        {
            while (!cursor.TryKeyword("trailer"))
            {
                long first = cursor.Integer(), count = cursor.Integer();
                CheckRange(first, count);
                for (long i = 0; i < count; i++)
                {
                    _context.Checkpoint();
                    _context.Consume(PdfResourceKind.Objects, 1);
                    long position = cursor.Integer(), generation = cursor.Integer();
                    string kind = cursor.Token();
                    if (position < 0 || generation < 0 || generation > 65535 || (kind != "n" && kind != "f"))
                        throw Error("Syntax.XrefEntry", "A classic cross-reference entry is malformed.");
                    int number = checked((int)(first + i));
                    if (kind == "n")
                        CheckOffset(position);
                    else if (position > int.MaxValue)
                        throw Error("Syntax.XrefEntry", "A free-list object number is outside range.");
                    Add(entries, new(new(number, (int)generation), kind == "n" ? PdfCrossReferenceKind.Offset : PdfCrossReferenceKind.Free, position));
                }
            }

            trailer = cursor.Value();
            if (trailer.Kind != PdfValueKind.Dictionary)
                throw Error("Syntax.Trailer", "A trailer dictionary is required.");
            if (trailer.Entries.TryGetValue("XRefStm", out var supplement))
            {
                long streamOffset = DirectInteger(supplement);
                CheckOffset(streamOffset);
                if (!_xrefOffsets.Add(streamOffset))
                    throw Error("Syntax.XrefCycle", "A hybrid section repeats a cross-reference offset.");
                var additional = ReadXrefStream(streamOffset);
                foreach (var pair in additional.entries)
                    entries[pair.Key] = pair.Value; // ISO hybrid stream entries take precedence
                hybrid = true;
            }
        }
        else
            (trailer, entries) = ReadXrefStream(offset);
        if (!trailer.Entries.TryGetValue("Size", out var size) || DirectInteger(size) <= 0 || DirectInteger(size) > ((long)int.MaxValue + 1))
            throw Error("Syntax.Trailer", "The trailer Size is missing or outside range.");
        if (entries.Keys.Any(n => n >= DirectInteger(size)))
            throw Error("Syntax.XrefEntry", "An indexed object is outside the trailer Size.");
        return new(offset, trailer, entries, hybrid);
    }

    private (PdfValue trailer, Dictionary<int, PdfCrossReference> entries) ReadXrefStream(long offset)
    {
        var stream = Indirect(offset, null, -1);
        if (stream.Kind != PdfValueKind.Stream || !IsName(stream, "Type", "XRef"))
            throw Error("Syntax.XrefStream", "The cross-reference offset does not identify an XRef stream.");
        var widths = Integers(Required(stream, "W"));
        if (widths.Length != 3 || widths.Any(n => n < 0 || n > 8) || widths.Sum() == 0)
            throw Error("Syntax.XrefStream", "Cross-reference field widths must contain three bounded values.");
        long size = DirectInteger(Required(stream, "Size"));
        if (size <= 0 || size > ((long)int.MaxValue + 1))
            throw Error("Syntax.XrefStream", "The cross-reference Size is outside range.");
        long[] ranges = stream.Entries.TryGetValue("Index", out var index) ? Integers(index) : new long[]
        {
            0,
            size
        };
        if (ranges.Length == 0 || ranges.Length % 2 != 0)
            throw Error("Syntax.XrefStream", "Cross-reference Index must contain start/count pairs.");
        var entries = new Dictionary<int, PdfCrossReference>();
        using var decoded = PdfStreamFilters.OpenBootstrap(stream, _context);
        for (int range = 0; range < ranges.Length; range += 2)
        {
            long first = ranges[range], count = ranges[range + 1];
            CheckRange(first, count);
            if (first + count > size)
                throw Error("Syntax.XrefStream", "A cross-reference range exceeds Size.");
            for (long i = 0; i < count; i++)
            {
                _context.Checkpoint();
                _context.Consume(PdfResourceKind.Objects, 1);
                ulong type = widths[0] == 0 ? 1 : Field(decoded, (int)widths[0]), a = Field(decoded, (int)widths[1]), b = Field(decoded, (int)widths[2]);
                if (a > long.MaxValue || b > int.MaxValue)
                    throw Error("Syntax.XrefEntry", "A cross-reference field overflows its supported range.");
                int number = checked((int)(first + i));
                if (type <= 1)
                {
                    if (b > 65535)
                        throw Error("Syntax.XrefEntry", "A generation exceeds 65535.");
                    if (type == 1)
                        CheckOffset((long)a);
                    else if (a > int.MaxValue)
                        throw Error("Syntax.XrefEntry", "A free-list object number is outside range.");
                    Add(entries, new(new(number, (int)b), (PdfCrossReferenceKind)type, (long)a));
                }
                else if (type == 2)
                {
                    if (a == 0 || a > int.MaxValue)
                        throw Error("Syntax.XrefEntry", "An object-stream reference is outside range.");
                    Add(entries, new(new(number), PdfCrossReferenceKind.Compressed, 0, (int)a, (int)b));
                }
                else
                    throw Error("Syntax.XrefTypeUnsupported", "An unknown cross-reference entry type cannot be resolved safely.");
            }
        }

        if (decoded.ReadByte() != -1)
            throw Error("Syntax.XrefStream", "The cross-reference stream contains unexpected trailing bytes.");
        return (stream, entries);
    }

    private PdfValue Resolve(PdfObjectId id, int revision)
    {
        if (id.Number == 0)
            return PdfValue.Null;
        var key = (revision, id.Number, id.Generation);
        if (_resolved.TryGetValue(key, out var value))
            return value;
        PdfCrossReference? entry = null;
        for (int i = revision; i < _revisions.Count; i++)
            if (_revisions[i].Index.TryGetValue(id.Number, out entry))
                break;
        if (entry is null || entry.Kind == PdfCrossReferenceKind.Free || entry.Id.Generation != id.Generation)
            return PdfValue.Null;
        if (!_resolving.Add(key))
            throw Error("Syntax.ObjectCycle", "Indirect stream-length dependencies form a cycle.");
        using var depth = _context.EnterScope(PdfResourceKind.Depth);
        try
        {
            if (entry.Kind == PdfCrossReferenceKind.Compressed)
                throw Error("Syntax.ObjectStreamUnsupported", "Compressed object resolution requires the object-stream decoder.");
            value = Indirect(entry.Offset, id, revision);
            _resolved.Add(key, value);
            return value;
        }
        finally
        {
            _resolving.Remove(key);
        }
    }

    private PdfValue Indirect(long offset, PdfObjectId? expected, int revision)
    {
        CheckOffset(offset);
        var cursor = new PdfSyntaxCursor(_source, _context, offset);
        long number = cursor.Integer(), generation = cursor.Integer();
        cursor.Require("obj");
        if (number <= 0 || number > int.MaxValue || generation < 0 || generation > 65535 || expected.HasValue && (number != expected.Value.Number || generation != expected.Value.Generation))
            throw Error("Syntax.ObjectIdentity", "An indirect object header disagrees with its cross-reference entry.");
        var value = cursor.Value();
        if (value.Kind == PdfValueKind.Dictionary && cursor.TryKeyword("stream"))
        {
            if (cursor.Peek() == 13)
            {
                cursor.Position++;
                if (cursor.Peek() == 10)
                    cursor.Position++;
            }
            else if (cursor.Peek() == 10)
                cursor.Position++;
            else
                throw Error("Syntax.Stream", "The stream keyword must be followed immediately by an end-of-line.");
            var lengthValue = Required(value, "Length");
            if (lengthValue.Kind == PdfValueKind.Reference)
            {
                if (revision < 0)
                    throw Error("Syntax.XrefStream", "A bootstrap cross-reference stream requires a direct Length.");
                lengthValue = Resolve(lengthValue.ReferenceId, revision);
            }

            long length = DirectInteger(lengthValue), start = cursor.Position;
            if (length < 0 || start > _source.Length - length)
                throw Error("Syntax.StreamLength", "A stream length extends beyond the input.");
            cursor.Position = start + length;
            cursor.Require("endstream");
            value = PdfValue.StreamValue(value, _source, start, length);
        }

        cursor.Require("endobj");
        return value;
    }

    private ulong Field(System.IO.Stream stream, int width)
    {
        ulong result = 0;
        for (int i = 0; i < width; i++)
        {
            int b = stream.ReadByte();
            if (b < 0)
                throw Error("Syntax.Truncated", "A cross-reference stream is truncated.");
            result = (result << 8) | (byte)b;
        }

        return result;
    }

    private long[] Integers(PdfValue value)
    {
        if (value.Kind != PdfValueKind.Array)
            throw Error("Syntax.XrefStream", "An integer array is required.");
        _context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 8L);
        return value.Items.Select(DirectInteger).ToArray();
    }

    private PdfValue Required(PdfValue dictionary, string name) => dictionary.Entries.TryGetValue(name, out var value) ? value : throw Error("Syntax.Dictionary", "A required dictionary entry is missing.");
    private static bool IsName(PdfValue dictionary, string key, string name) => dictionary.Entries.TryGetValue(key, out var value) && value.Kind == PdfValueKind.Name && value.NameValue == name;
    private long DirectInteger(PdfValue value)
    {
        if (value.Kind != PdfValueKind.Number)
            throw Error("Syntax.IntegerRange", "A direct integer is required.");
        return value.IntegerValue;
    }

    private void CheckOffset(long offset)
    {
        if (offset <= 0 || offset >= _source.Length)
            throw Error("Syntax.OffsetRange", "A cross-reference offset is outside the input.");
    }

    private void CheckRange(long first, long count)
    {
        if (first < 0 || count < 0 || first > int.MaxValue || count > ((long)int.MaxValue + 1) - first)
            throw Error("Syntax.XrefEntry", "An object-number range overflows its supported range.");
    }

    private void Add(Dictionary<int, PdfCrossReference> index, PdfCrossReference entry)
    {
        if (index.ContainsKey(entry.Id.Number))
            throw Error("Syntax.DuplicateObject", "A cross-reference section repeats an object number.");
        index.Add(entry.Id.Number, entry);
    }

    private bool Match(long offset, string text)
    {
        for (int i = 0; i < text.Length; i++)
            if (_source.At(offset + i) != text[i])
                return false;
        return true;
    }

    private PdfException Error(string code, string message) => _errors.Error(code, message);
}
