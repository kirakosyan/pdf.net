namespace Fluent.Pdf;
internal sealed class PdfSyntaxReader
{
    private readonly PdfByteStore _source;
    private readonly PdfOperationContext _context;
    private readonly List<PdfRevision> _revisions = new();
    private readonly HashSet<long> _xrefOffsets = new();
    private readonly HashSet<(int, int, int)> _resolving = new();
    private readonly Dictionary<(int, int, int), PdfValue> _resolved = new();
    private readonly Dictionary<long, List<(PdfObjectId Id, PdfValue Value)>> _offsetValues = new();
    private readonly Dictionary<PdfValue, List<EmbeddedObjects>> _objectStreams = new();
    private readonly PdfSyntaxCursor _errors;
    private readonly Func<PdfValue, PdfObjectId, PdfValue, PdfObjectCrypt>? _authenticate;
    private PdfObjectCrypt? _crypt;
    internal PdfSyntaxReader(PdfByteStore source, PdfOperationContext context, Func<PdfValue, PdfObjectId, PdfValue, PdfObjectCrypt>? authenticate = null)
    {
        _source = source;
        _context = context;
        _errors = new(source, context);
        _authenticate = authenticate;
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

            if (_authenticate != null)
                ConfigureCrypt();
            if (_crypt == null && _revisions.Any(revision => revision.Trailer.Entries.TryGetValue("Encrypt", out var encryption) && encryption.Kind != PdfValueKind.Null) && _revisions.Any(revision => revision.Entries.Any(entry => entry.Kind == PdfCrossReferenceKind.Compressed)))
                throw Error("Syntax.EncryptionProviderRequired", "Encrypted object streams require an explicitly configured encryption implementation.");
            for (int revision = 0; revision < _revisions.Count; revision++)
            {
                var seen = new HashSet<int>();
                for (int i = revision; i < _revisions.Count; i++)
                    foreach (var entry in _revisions[i].Entries)
                        if (seen.Add(entry.Id.Number) && entry.Kind != PdfCrossReferenceKind.Free)
                            _revisions[revision].Values.Add(entry.Id.Number, Resolve(entry.Id, revision));
            }

            return new PdfSyntaxDocument(version, _source, _revisions, _context.Policy, _crypt != null);
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

    private void ConfigureCrypt()
    {
        var trailer = _revisions[0].Trailer;
        if (!trailer.Entries.TryGetValue("Encrypt", out var encryption) || encryption.Kind == PdfValueKind.Null)
            throw SecurityError("NotEncrypted", "An explicitly encrypted input document is required.");
        bool indirect = encryption.Kind == PdfValueKind.Reference;
        if (!indirect && encryption.Kind != PdfValueKind.Dictionary)
            throw SecurityError("EncryptionDictionary", "The encryption declaration must be a direct dictionary or an uncompressed indirect dictionary.");
        var id = indirect ? encryption.ReferenceId : default;
        if (indirect)
            for (int revision = 0; revision < _revisions.Count; revision++)
            {
                PdfCrossReference? entry = null;
                for (int i = revision; i < _revisions.Count; i++)
                    if (_revisions[i].Index.TryGetValue(id.Number, out entry))
                        break;
                if (entry == null || entry.Kind != PdfCrossReferenceKind.Offset || entry.Id != id)
                    throw SecurityError("EncryptionDictionary", "The encryption dictionary must remain uncompressed and active throughout its history.");
            }

        var dictionary = indirect ? Resolve(id, 0) : encryption;
        ValidateDirect(dictionary);
        if (dictionary.Kind != PdfValueKind.Dictionary)
            throw SecurityError("EncryptionDictionary", "The encryption reference must identify a dictionary.");
        var identity = Identity(trailer);
        bool legacyIdentity = !dictionary.Entries.TryGetValue("V", out var version) || !version.TryGetInteger(out long v) || v < 5;
        for (int revision = 1; revision < _revisions.Count; revision++)
        {
            var prior = _revisions[revision].Trailer;
            if (!prior.Entries.TryGetValue("Encrypt", out var oldEncryption) || (indirect ? oldEncryption.Kind != PdfValueKind.Reference || oldEncryption.ReferenceId != id || !Same(dictionary, Resolve(id, revision)) : !Same(dictionary, oldEncryption)))
                throw SecurityError("EncryptionHistoryProfile", "Changing security handlers within incremental history requires a separately validated profile.");
            if (legacyIdentity)
            {
                var oldIdentity = Identity(prior);
                if (identity.Kind != PdfValueKind.Array || oldIdentity.Kind != PdfValueKind.Array || !Same(identity.Items[0], oldIdentity.Items[0]))
                    throw SecurityError("EncryptionHistoryProfile", "Legacy encrypted revisions must retain the permanent file identifier.");
            }
        }

        _crypt = _authenticate!(dictionary, id, identity) ?? throw SecurityError("SecurityHandler", "Authentication returned no content security handler.");
        _resolved.Clear(); // Bootstrap observations are raw; normal resolution now produces decrypted values.
        PdfValue Identity(PdfValue value)
        {
            if (!value.Entries.TryGetValue("ID", out var ids) || ids.Kind == PdfValueKind.Null)
                return PdfValue.Null;
            if (ids.Kind != PdfValueKind.Array || ids.Items.Count != 2 || ids.Items.Any(item => item.Kind != PdfValueKind.String))
                throw SecurityError("IdentifierProfile", "Encrypted input requires a direct pair of string file identifiers when ID is present.");
            return ids;
        }

        void ValidateDirect(PdfValue value)
        {
            _context.Checkpoint();
            _context.Consume(PdfResourceKind.Objects, 1);
            using var depth = _context.EnterScope(PdfResourceKind.Depth);
            if (value.Kind == PdfValueKind.Reference || value.Kind == PdfValueKind.Stream)
                throw SecurityError("ParameterReferenceProfile", "Indirect security parameters require a separately validated bootstrap profile.");
            if (value.Kind == PdfValueKind.Array)
                foreach (var child in value.Items)
                    ValidateDirect(child);
            if (value.Kind == PdfValueKind.Dictionary)
                foreach (var child in value.Entries.Values)
                    ValidateDirect(child);
        }

        bool Same(PdfValue left, PdfValue right)
        {
            _context.Checkpoint();
            _context.Consume(PdfResourceKind.Objects, 1);
            using var depth = _context.EnterScope(PdfResourceKind.Depth);
            if (left.Kind != right.Kind)
                return false;
            switch (left.Kind)
            {
                case PdfValueKind.Null:
                    return true;
                case PdfValueKind.Boolean:
                    return left.BooleanValue == right.BooleanValue;
                case PdfValueKind.Number:
                    return left.NumberText == right.NumberText;
                case PdfValueKind.Name:
                    return left.NameValue == right.NameValue;
                case PdfValueKind.String:
                    return left.StringBytes.SequenceEqual(right.StringBytes);
                case PdfValueKind.Array:
                    return left.Items.Count == right.Items.Count && left.Items.Select((value, i) => Same(value, right.Items[i])).All(equal => equal);
                case PdfValueKind.Dictionary:
                    if (left.Entries.Count != right.Entries.Count)
                        return false;
                    foreach (var pair in left.Entries)
                        if (!right.Entries.TryGetValue(pair.Key, out var value) || !Same(pair.Value, value))
                            return false;
                    return true;
                default:
                    throw SecurityError("ParameterReferenceProfile", "Indirect security parameters require a separately validated bootstrap profile.");
            }
        }

        PdfException SecurityError(string code, string message) => PdfWriterCore.Error(_context, "Security." + code, message);
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
        _context.Checkpoint();
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
            value = entry.Kind == PdfCrossReferenceKind.Compressed ? Compressed(entry, revision) : Indirect(entry.Offset, id, revision);
            if (_crypt != null && entry.Kind != PdfCrossReferenceKind.Compressed)
                value = _crypt.Transform(value, id, false, Follow);
            _resolved.Add(key, value);
            return value;
            PdfValue Follow(PdfValue item)
            {
                if (item.Kind != PdfValueKind.Reference)
                    return item;
                _context.Consume(PdfResourceKind.ExpandedBytes, 128);
                var seen = new HashSet<PdfObjectId>();
                while (item.Kind == PdfValueKind.Reference)
                {
                    _context.Checkpoint();
                    _context.Consume(PdfResourceKind.ExpandedBytes, 48);
                    if (!seen.Add(item.ReferenceId))
                        throw SecurityError();
                    item = Resolve(item.ReferenceId, revision);
                }

                return item;
                PdfException SecurityError() => PdfWriterCore.Error(_context, "Security.ReferenceCycle", "A cryptographic stream parameter contains a reference cycle.");
            }
        }
        finally
        {
            _resolving.Remove(key);
        }
    }

    private PdfValue Compressed(PdfCrossReference entry, int revision)
    {
        PdfCrossReference? container = null;
        for (int i = revision; i < _revisions.Count; i++)
            if (_revisions[i].Index.TryGetValue(entry.ContainerObjectNumber, out container))
                break;
        if (container is null || container.Kind != PdfCrossReferenceKind.Offset || container.Id.Generation != 0)
            throw Error("Syntax.ObjectStream", "An object stream must be an uncompressed generation-zero object.");
        var stream = Resolve(new PdfObjectId(entry.ContainerObjectNumber), revision);
        EmbeddedObjects? embedded = null;
        if (_objectStreams.TryGetValue(stream, out var candidates))
            foreach (var candidate in candidates)
                if (candidate.Dependencies.All(pair => ReferenceEquals(pair.Value, Resolve(pair.Key, revision))))
                {
                    embedded = candidate;
                    break;
                }

        if (embedded == null)
        {
            if (stream.Kind != PdfValueKind.Stream || !IsName(stream, "Type", "ObjStm"))
                throw Error("Syntax.ObjectStream", "A compressed entry does not identify an ObjStm stream.");
            long count = DirectInteger(Required(stream, "N")), first = DirectInteger(Required(stream, "First"));
            if (count <= 0 || count > int.MaxValue || first < 0)
                throw Error("Syntax.ObjectStream", "Object-stream counts or First offsets are outside range.");
            _context.Consume(PdfResourceKind.Objects, count);
            _context.Consume(PdfResourceKind.ExpandedBytes, checked(count * 12));
            var ids = new int[(int)count];
            var offsets = new long[(int)count];
            _context.Consume(PdfResourceKind.ExpandedBytes, 128);
            var dependencies = new Dictionary<PdfObjectId, PdfValue>();
            using var decoded = PdfStreamFilters.OpenBootstrap(PdfFilterDeclarations.Resolve(stream, id =>
            {
                var value = Resolve(id, revision);
                if (!dependencies.ContainsKey(id))
                    _context.Consume(PdfResourceKind.ExpandedBytes, 48);
                dependencies[id] = value;
                return value;
            }, _context), _context);
            var bytes = PdfByteStore.Read(decoded, _context);
            if (first > bytes.Length)
                throw Error("Syntax.ObjectStream", "The first object lies beyond the decoded object stream.");
            var header = new PdfSyntaxCursor(bytes, _context, 0, first);
            var seen = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                long number = header.Integer(), offset = header.Integer();
                if (number <= 0 || number > int.MaxValue || offset < 0 || offset > bytes.Length - first || i > 0 && offset <= offsets[i - 1] || !seen.Add((int)number))
                    throw Error("Syntax.ObjectStream", "An object-stream index has duplicate identities, non-increasing offsets or overflowing fields.");
                ids[i] = (int)number;
                offsets[i] = offset;
            }

            header.Skip();
            if (header.Position != first)
                throw Error("Syntax.ObjectStream", "The object-stream header disagrees with First.");
            embedded = new EmbeddedObjects(bytes, first, ids, offsets, dependencies);
            if (candidates == null)
                _objectStreams.Add(stream, candidates = new List<EmbeddedObjects>());
            candidates.Add(embedded);
        }

        int member = entry.MemberIndex;
        if (member >= embedded.Ids.Length || embedded.Ids[member] != entry.Id.Number)
            throw Error("Syntax.ObjectStream", "A compressed entry disagrees with its member index or object number.");
        if (embedded.Values.TryGetValue(member, out var cached))
            return cached;
        long start = embedded.First + embedded.Offsets[member], end = member + 1 < embedded.Ids.Length ? embedded.First + embedded.Offsets[member + 1] : embedded.Bytes.Length;
        var cursor = new PdfSyntaxCursor(embedded.Bytes, _context, start, end);
        var result = cursor.Value();
        cursor.Skip();
        if (cursor.Position != end)
            throw Error("Syntax.ObjectStream", "A compressed member contains trailing data or a forbidden stream object.");
        _context.Consume(PdfResourceKind.ExpandedBytes, 48);
        embedded.Values.Add(member, result);
        return result;
    }

    private sealed class EmbeddedObjects
    {
        internal readonly PdfByteStore Bytes;
        internal readonly long First;
        internal readonly int[] Ids;
        internal readonly long[] Offsets;
        internal readonly Dictionary<PdfObjectId, PdfValue> Dependencies;
        internal readonly Dictionary<int, PdfValue> Values = new();
        internal EmbeddedObjects(PdfByteStore bytes, long first, int[] ids, long[] offsets, Dictionary<PdfObjectId, PdfValue> dependencies)
        {
            Bytes = bytes;
            First = first;
            Ids = ids;
            Offsets = offsets;
            Dependencies = dependencies;
        }
    }

    private PdfValue Indirect(long offset, PdfObjectId? expected, int revision)
    {
        CheckOffset(offset);
        if (_offsetValues.TryGetValue(offset, out var candidates))
            foreach (var cached in candidates)
            {
                if (expected.HasValue && cached.Id != expected.Value)
                    throw Error("Syntax.ObjectIdentity", "An indirect object header disagrees with its cross-reference entry.");
                if (cached.Value.Kind != PdfValueKind.Stream || !cached.Value.Entries.TryGetValue("Length", out var length) || length.Kind != PdfValueKind.Reference || revision >= 0 && DirectInteger(Resolve(length.ReferenceId, revision)) == cached.Value.RawStreamLength)
                    return cached.Value;
            }

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
        _context.Consume(PdfResourceKind.ExpandedBytes, 96);
        if (candidates == null)
            _offsetValues.Add(offset, candidates = new List<(PdfObjectId, PdfValue)>());
        candidates.Add((new PdfObjectId((int)number, (int)generation), value));
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
