using System.Globalization;

namespace Fluent.Pdf;
// Original single-revision recovery scanner. Payloads are skipped by exact declared/caller lengths, never marker searches.
internal sealed class PdfRepairScanner
{
    internal readonly Dictionary<PdfObjectId, PdfValue> Objects = new();
    internal readonly Dictionary<PdfObjectId, long> Offsets = new();
    internal readonly Dictionary<int, (int Generation, bool Active)> Status = new();
    internal readonly List<PdfRepairRecord> Ledger = new();
    internal PdfValue Trailer = PdfGraphicsNumbers.Dict();
    internal string Version = "";
    private readonly PdfByteStore _source;
    private readonly PdfOperationContext _context;
    private readonly PdfRepairOptions _options;
    private readonly Dictionary<PdfObjectId, long> _lengths = new();
    private readonly HashSet<PdfObjectId> _usedLengths = new();
    private readonly HashSet<int> _numbers = new();
    private bool _xref, _trailer;
    internal PdfRepairScanner(PdfByteStore source, PdfRepairOptions options, PdfOperationContext context)
    {
        _source = source;
        _options = options;
        _context = context;
        context.Consume(PdfResourceKind.ExpandedBytes, options.StreamLengths.Count * 64L + 512);
        foreach (var length in options.StreamLengths)
            _lengths.Add(length.ObjectId, length.Length);
    }

    internal void Read()
    {
        if (_source.Length < 16 || !Match(0, "%PDF-") || !PdfSyntaxCursor.White(_source.At(8)))
            throw Error("Header", "A supported PDF header at byte zero is required for this recovery profile.");
        var cursor = new PdfSyntaxCursor(_source, _context, 9);
        Version = cursor.Text(5, 3);
        if (Version != "1.2" && Version != "1.3" && Version != "1.4" && Version != "1.5" && Version != "1.6" && Version != "1.7" && Version != "2.0")
            throw Error("Version", "The recovery header version is unsupported.");
        while (true)
        {
            cursor.Skip();
            if (cursor.Position >= cursor.End)
                break;
            long start = cursor.Position;
            if (Header(start, out var id))
            {
                ReadObject(cursor, id, start);
                continue;
            }

            if (cursor.TryKeyword("xref"))
            {
                ReadXref(cursor, start);
                continue;
            }

            if (cursor.TryKeyword("trailer"))
            {
                if (_trailer)
                    throw Error("HistoryProfile", "Multiple trailers require an index-aware incremental recovery profile; no revision is chosen by position.");
                Trailer = cursor.Value();
                if (Trailer.Kind != PdfValueKind.Dictionary)
                    throw Error("Trailer", "A trailer declaration must be a well-framed dictionary.");
                _trailer = true;
                continue;
            }

            if (cursor.TryKeyword("startxref"))
            {
                cursor.Skip();
                if (cursor.Position < cursor.End && !PdfSyntaxCursor.Delimiter(cursor.Peek()))
                    cursor.Token();
                continue;
            }

            throw Error("UnframedData", "Unframed data outside indirect objects/xref/trailer prevents safe recovery at byte " + start.ToString(CultureInfo.InvariantCulture) + ".");
        }

        if (Objects.Count == 0)
            throw Error("NoObjects", "No safely framed indirect objects were recovered.");
        if (Trailer.Entries.ContainsKey("Prev") || Trailer.Entries.ContainsKey("XRefStm"))
            throw Error("HistoryProfile", "Incremental/hybrid recovery requires another explicit profile; historical objects are not resurrected.");
        if (Trailer.Entries.TryGetValue("Encrypt", out var encryption) && encryption.Kind != PdfValueKind.Null)
            throw Error("EncryptionProfile", "Decrypt protection explicitly before repair; ciphertext is not rewritten as plaintext.");
        if (_usedLengths.Count != _lengths.Count)
            throw Error("UnusedStreamOverride", "A supplied exact stream-length override did not identify a recovered stream.");
        if (!_xref && _options.ActiveObjects == null)
            throw Error("ActiveInventoryRequired", "The source has no recoverable classic xref status rows. Supply a complete explicit active-object identity inventory; physical bodies cannot establish free/in-use status.");
        Add("Repair.XrefRebuilt", null, null, "Cross-reference offsets, subsections, free chains and footer are reconstructed in a full rewrite.");
        long tail = _source.Length;
        while (tail > 0 && PdfSyntaxCursor.White(_source.At(tail - 1)))
            tail--;
        if (tail < 5 || !Match(tail - 5, "%%EOF"))
            Add("Repair.EofRestored", null, null, "A missing final EOF marker is restored; original bytes remain available unchanged.");
    }

    private void ReadObject(PdfSyntaxCursor cursor, PdfObjectId id, long start)
    {
        _context.Consume(PdfResourceKind.Objects, 1);
        _context.Consume(PdfResourceKind.ExpandedBytes, 256);
        if (!_numbers.Add(id.Number))
            throw Error("AmbiguousObject", "Multiple physical definitions of an object number require explicit revision-aware recovery; latest-by-position selection is forbidden.");
        cursor.Integer();
        cursor.Integer();
        cursor.Require("obj");
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
                throw Error("StreamFraming", "A stream keyword requires its immediate end-of-line; payload start is not guessed.");
            long length;
            var declared = value.Entries.TryGetValue("Length", out var entry) ? entry : PdfValue.Null;
            if (_lengths.TryGetValue(id, out length))
            {
                _usedLengths.Add(id);
                Add("Repair.StreamLengthOverridden", id, start, "The caller-authorized exact raw payload length replaces the source declaration.");
            }
            else
            {
                var resolved = declared;
                var seen = new HashSet<PdfObjectId>();
                while (resolved.Kind == PdfValueKind.Reference)
                {
                    _context.Checkpoint();
                    _context.Consume(PdfResourceKind.ExpandedBytes, 48);
                    if (!seen.Add(resolved.ReferenceId) || !Objects.TryGetValue(resolved.ReferenceId, out resolved!))
                        throw Error("StreamLengthAuthority", "An unresolved/cyclic stream length requires an explicit exact length override; endstream is never searched inside payloads.");
                }

                if (!resolved.TryGetInteger(out length) || length < 0)
                    throw Error("StreamLengthAuthority", "A missing/invalid stream length requires an explicit exact length override.");
            }

            long data = cursor.Position;
            if (length < 0 || length > _source.Length - data)
                throw Error("StreamExtent", "The exact stream payload extends beyond the source.");
            cursor.Position = data + length;
            cursor.Require("endstream");
            var entries = PdfWriterCore.CopyDictionary(value.Entries, _context);
            entries["Length"] = PdfValue.Integer(length);
            value = PdfValue.StreamValue(PdfValue.OwnedDictionary(entries), _source, data, length);
            if (entries.TryGetValue("Filter", out var encoding) && encoding.Kind != PdfValueKind.Null)
                Add("Repair.EncodedPayloadRetained", id, data, "Exact encoded payload bytes are retained; structural recovery does not imply codec, image or content reconstruction.");
        }

        cursor.Require("endobj");
        if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
        {
            string type = Name(value, "Type"), filter = Name(value, "Filter");
            if (type == "ObjStm" || type == "XRef")
                throw Error("CompressedIndexProfile", "Compressed object/xref stream recovery requires a separately validated index profile; compressed members are never silently discarded.");
            if (filter == "Standard" || filter == "Adobe.PubSec")
                throw Error("EncryptionProfile", "A security handler was found; encrypted recovery requires explicit authentication first.");
        }

        Objects.Add(id, value);
        Offsets.Add(id, start);
        Add("Repair.ObjectFramed", id, start, "A unique indirect object was recovered using exact lexical and stream boundaries.");
    }

    private void ReadXref(PdfSyntaxCursor cursor, long start)
    {
        if (_xref)
            throw Error("HistoryProfile", "Multiple classic indexes require revision-aware recovery; no index is chosen silently.");
        _xref = true;
        while (true)
        {
            cursor.Skip();
            long saved = cursor.Position;
            if (cursor.TryKeyword("trailer") || cursor.TryKeyword("startxref") || cursor.Position >= cursor.End)
            {
                cursor.Position = saved;
                return;
            }

            long first = cursor.Integer(), count = cursor.Integer();
            if (first < 0 || first > int.MaxValue || count < 0 || count > int.MaxValue - first)
                throw Error("XrefIdentity", "A classic subsection identity range is invalid.");
            _context.Consume(PdfResourceKind.Objects, count);
            _context.Consume(PdfResourceKind.ExpandedBytes, count * 64L);
            for (long i = 0; i < count; i++)
            {
                _context.Checkpoint();
                cursor.Token();
                long generation = cursor.Integer();
                string state = cursor.Token();
                int number = checked((int)(first + i));
                if (generation < 0 || generation > 65535 || state != "n" && state != "f" || Status.ContainsKey(number))
                    throw Error("XrefIdentity", "Xref status rows contain duplicate identities, invalid generations or ambiguous active/free state.");
                Status.Add(number, ((int)generation, state == "n"));
            }
        }
    }

    private bool Header(long start, out PdfObjectId id)
    {
        id = default;
        long at = start;
        if (!Unsigned(ref at, int.MaxValue - 2, out long number) || number == 0 || !White(ref at) || !Unsigned(ref at, 65535, out long generation) || !White(ref at) || !Match(at, "obj") || !PdfSyntaxCursor.Delimiter(_source.At(at + 3)))
            return false;
        id = new((int)number, (int)generation);
        return true;
    }

    private bool Unsigned(ref long at, long maximum, out long value)
    {
        value = 0;
        long start = at;
        while (_source.At(at) >= '0' && _source.At(at) <= '9')
        {
            if ((at & 1023) == 0)
                _context.Checkpoint();
            int digit = _source.At(at++) - '0';
            if (value > (maximum - digit) / 10)
                return false;
            value = value * 10 + digit;
        }

        return at > start;
    }

    private bool White(ref long at)
    {
        long start = at;
        while (PdfSyntaxCursor.White(_source.At(at)))
        {
            if ((at & 1023) == 0)
                _context.Checkpoint();
            at++;
        }

        return at > start;
    }

    private bool Match(long at, string value)
    {
        for (int i = 0; i < value.Length; i++)
            if (_source.At(at + i) != value[i])
                return false;
        return true;
    }

    private static string Name(PdfValue value, string key) => value.Entries.TryGetValue(key, out var entry) && entry.Kind == PdfValueKind.Name ? entry.NameValue : "";
    internal void Add(string code, PdfObjectId? id, long? offset, string message)
    {
        _context.Checkpoint();
        _context.Consume(PdfResourceKind.Objects, 1);
        _context.Consume(PdfResourceKind.ExpandedBytes, (code.Length + message.Length) * 2L + 128);
        Ledger.Add(new(code, id, offset, message));
    }

    private PdfException Error(string code, string message) => PdfWriterCore.Error(_context, "Repair." + code, message);
}
