using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable bounded ToUnicode CMap. Its PostScript-style envelope is inspected without execution.</summary>
public sealed class PdfUnicodeMap
{
    private readonly Dictionary<ulong, string> _values;
    private readonly List<(uint First, uint Last, int Length)> _spaces;
    private PdfUnicodeMap(Dictionary<ulong, string> values, List<(uint First, uint Last, int Length)> spaces)
    {
        _values = values;
        _spaces = spaces;
    }

    /// <summary>Gets the number of explicit code-to-Unicode mappings.</summary>
    public int Count => _values.Count;
    /// <summary>Gets the successful bounded parsing report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;

    /// <summary>Parses borrowed decoded CMap bytes. Named inherited maps must be supplied explicitly; no external resource is loaded.</summary>
    public static PdfUnicodeMap Parse(Stream decodedInput, IReadOnlyDictionary<string, PdfUnicodeMap>? inheritedMaps = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (decodedInput == null)
            throw new ArgumentNullException(nameof(decodedInput));
        if (!decodedInput.CanRead)
            throw new ArgumentException("A readable CMap stream is required.", nameof(decodedInput));
        PdfUnicodeMap? result = null;
        var report = PdfFont.Run("ReadToUnicode", "core.fonts", policy, cancellationToken, context =>
        {
            using var lease = new BorrowedStream(decodedInput, true, context);
            result = ParseCore(PdfByteStore.Read(lease, context), inheritedMaps, context);
        });
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Decodes exact variable-length codes, rejecting missing/ambiguous codes under strict processing.</summary>
    public PdfDecodedText Decode(PdfValue encodedString, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (encodedString == null)
            throw new ArgumentNullException(nameof(encodedString));
        if (encodedString.Kind != PdfValueKind.String)
            throw new ArgumentException("An immutable byte string is required.", nameof(encodedString));
        string? text = null;
        var report = PdfFont.Run("DecodeFontText", "core.fonts", policy, cancellationToken, context =>
        {
            context.Consume(PdfResourceKind.InputBytes, encodedString.StringBytes.Count);
            text = DecodeCore(encodedString.StringBytes, context);
        });
        return new PdfDecodedText(text!, report);
    }

    internal string DecodeCore(IReadOnlyList<byte> bytes, PdfOperationContext context)
    {
        var text = new StringBuilder();
        int position = 0;
        while (position < bytes.Count)
        {
            context.Checkpoint();
            uint code = 0;
            int length = 0;
            for (int n = 1; n <= 4 && position + n <= bytes.Count; n++)
            {
                code = (code << 8) | bytes[position + n - 1];
                if (Overlaps(_spaces, code, code, n))
                {
                    length = n;
                    break;
                }
            }

            if (length == 0)
                throw PdfWriterCore.Error(context, "Font.CMapCodeSpace", "The encoded string contains a truncated or undeclared code.");
            position += length;
            if (!_values.TryGetValue(Key(code, length), out string? unicode))
            {
                context.Report(new PdfDiagnostic("Font.UnmappedCode", "An encoded font code has no Unicode mapping.", severity: PdfDiagnosticSeverity.Warning, lossCategory: PdfLossCategory.SubstitutedContent));
                unicode = "\ufffd";
            }

            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, unicode.Length * 4L + 16);
            text.Append(unicode);
        }

        return text.ToString();
    }

    private static ulong Key(uint code, int length) => ((ulong)length << 32) | code;
    internal Dictionary<int, int> ScalarMappings(Func<uint, int, int> glyphForCode, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, _values.Count * 128L + 256);
        var result = new Dictionary<int, int>();
        foreach (var pair in _values.OrderBy(x => x.Key))
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            string text = pair.Value;
            int scalar;
            if (text.Length == 1 && !char.IsSurrogate(text[0]))
                scalar = text[0];
            else if (text.Length == 2 && char.IsSurrogatePair(text, 0))
                scalar = char.ConvertToUtf32(text, 0);
            else
                continue;
            int glyph = glyphForCode((uint)pair.Key, (int)(pair.Key >> 32));
            if (glyph > 0 && !result.ContainsKey(scalar))
                result.Add(scalar, glyph);
        }

        return result;
    }

    internal static PdfUnicodeMap ParseCore(PdfByteStore bytes, IReadOnlyDictionary<string, PdfUnicodeMap>? inherited, PdfOperationContext context)
    {
        var cursor = new PdfSyntaxCursor(bytes, context);
        var values = new Dictionary<ulong, string>();
        var local = new HashSet<ulong>();
        var spaces = new List<(uint First, uint Last, int Length)>();
        PdfValue? previous = null;
        bool began = false, ended = false;
        while (true)
        {
            cursor.Skip();
            if (cursor.Peek() < 0)
                break;
            context.Checkpoint();
            int next = cursor.Peek();
            if (next == '/' || next == '(' || next == '<' || next == '[' || next == '+' || next == '-' || next == '.' || next >= '0' && next <= '9')
            {
                previous = cursor.Value();
                PdfContentProgram.Inspect(previous, context);
                continue;
            }

            string token = cursor.Token();
            if (token == "begincmap")
            {
                if (began)
                    throw Error("The CMap repeats its begin marker.");
                began = true;
                previous = null;
                continue;
            }

            if (token == "endcmap")
            {
                if (!began || ended)
                    throw Error("The CMap end marker is unmatched.");
                ended = true;
                previous = null;
                continue;
            }

            if (token == "usecmap")
            {
                if (previous == null || previous.Kind != PdfValueKind.Name || inherited == null || !inherited.TryGetValue(previous.NameValue, out var parent) || parent == null)
                    throw PdfWriterCore.Error(context, "Font.CMapProviderRequired", "The named inherited CMap must be supplied explicitly.");
                context.Consume(PdfResourceKind.Objects, parent._values.Count + parent._spaces.Count);
                context.Consume(PdfResourceKind.ExpandedBytes, parent._values.Count * 128L + parent._spaces.Count * 32L);
                foreach (var pair in parent._values)
                    if (!values.ContainsKey(pair.Key))
                        values.Add(pair.Key, pair.Value);
                spaces.AddRange(parent._spaces);
                previous = null;
                continue;
            }

            if (token == "begincodespacerange" || token == "beginbfchar" || token == "beginbfrange")
            {
                if (!began || ended || previous == null || !previous.TryGetInteger(out long count) || count < 0 || count > int.MaxValue)
                    throw Error("A mapping block requires a nonnegative bounded count within begincmap/endcmap.");
                context.Consume(PdfResourceKind.Objects, count);
                previous = null;
                for (long i = 0; i < count; i++)
                {
                    context.Checkpoint();
                    var first = Code(cursor.Value());
                    if (token == "beginbfchar")
                    {
                        Add(first.Code, first.Length, Unicode(cursor.Value()));
                        continue;
                    }

                    var last = Code(cursor.Value());
                    if (last.Length != first.Length || last.Code < first.Code)
                        throw Error("A CMap range has inconsistent code widths or descending bounds.");
                    if (token == "begincodespacerange")
                    {
                        context.Consume(PdfResourceKind.ExpandedBytes, 32);
                        spaces.Add((first.Code, last.Code, first.Length));
                        continue;
                    }

                    long extent = (long)last.Code - first.Code + 1;
                    context.Consume(PdfResourceKind.Objects, extent);
                    var target = cursor.Value();
                    if (target.Kind == PdfValueKind.Array)
                    {
                        if (target.Items.Count != extent)
                            throw Error("A bfrange Unicode array must match its code extent.");
                        for (int n = 0; n < target.Items.Count; n++)
                            Add(first.Code + (uint)n, first.Length, Unicode(target.Items[n]));
                    }
                    else
                    {
                        if (target.Kind != PdfValueKind.String || target.StringBytes.Count == 0 || target.StringBytes.Count > 1024)
                            throw Error("A bfrange target must be a bounded UTF-16BE byte string.");
                        context.Consume(PdfResourceKind.ExpandedBytes, target.StringBytes.Count);
                        byte[] current = target.StringBytes.ToArray();
                        for (long n = 0; n < extent; n++)
                        {
                            context.Checkpoint();
                            context.Consume(PdfResourceKind.ExpandedBytes, current.Length);
                            Add(first.Code + (uint)n, first.Length, Unicode(PdfValue.OwnedString((byte[])current.Clone())));
                            if (n + 1 == extent)
                                break;
                            int at = current.Length - 1;
                            while (at >= 0 && current[at] == 255)
                            {
                                current[at--] = 0;
                            }

                            if (at < 0)
                                throw Error("A bfrange target overflows its byte width.");
                            current[at]++;
                        }
                    }
                }

                cursor.Require(token == "begincodespacerange" ? "endcodespacerange" : token == "beginbfchar" ? "endbfchar" : "endbfrange");
                continue;
            }

            if (token != "findresource" && token != "begin" && token != "dict" && token != "def" && token != "currentdict" && token != "defineresource" && token != "pop" && token != "end" && token != "CMapName")
                throw PdfWriterCore.Error(context, "Font.CMapOperatorUnsupported", "The CMap contains an unsupported executable construct; no PostScript is executed.");
            previous = null;
        }

        if (!began || !ended || spaces.Count == 0)
            throw Error("A complete CMap and explicit/inherited code spaces are required.");
        context.Consume(PdfResourceKind.ExpandedBytes, spaces.Count * 64L);
        spaces.Sort((a, b) => a.Length != b.Length ? a.Length.CompareTo(b.Length) : a.First.CompareTo(b.First));
        var merged = new List<(uint First, uint Last, int Length)>();
        foreach (var space in spaces)
        {
            if (merged.Count > 0)
            {
                var last = merged[merged.Count - 1];
                if (last.Length == space.Length && (ulong)space.First <= (ulong)last.Last + 1)
                {
                    merged[merged.Count - 1] = (last.First, Math.Max(last.Last, space.Last), last.Length);
                    continue;
                }
            }

            merged.Add(space);
        }

        foreach (var longer in merged)
            for (int shorter = 1; shorter < longer.Length; shorter++)
            {
                context.Checkpoint();
                int shift = (longer.Length - shorter) * 8;
                if (Overlaps(merged, longer.First >> shift, longer.Last >> shift, shorter))
                    throw Error("Code spaces have ambiguous variable-length prefixes.");
            }

        foreach (ulong key in values.Keys)
        {
            context.Checkpoint();
            int length = (int)(key >> 32);
            uint code = (uint)key;
            if (!Overlaps(merged, code, code, length))
                throw Error("A Unicode mapping lies outside its code space.");
        }

        return new PdfUnicodeMap(values, merged);
        PdfException Error(string message) => PdfWriterCore.Error(context, "Font.CMapMalformed", message);
        (uint Code, int Length) Code(PdfValue value)
        {
            if (value.Kind != PdfValueKind.String || value.StringBytes.Count < 1 || value.StringBytes.Count > 4)
                throw Error("A CMap code must contain one through four bytes.");
            uint code = 0;
            foreach (byte b in value.StringBytes)
                code = (code << 8) | b;
            return (code, value.StringBytes.Count);
        }

        string Unicode(PdfValue value)
        {
            if (value.Kind != PdfValueKind.String || value.StringBytes.Count % 2 != 0 || value.StringBytes.Count > 1024)
                throw Error("Unicode targets must contain bounded UTF-16BE sequences.");
            context.Consume(PdfResourceKind.ExpandedBytes, value.StringBytes.Count * 4L + 64);
            var chars = new char[value.StringBytes.Count / 2];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = (char)(value.StringBytes[i * 2] * 256 + value.StringBytes[i * 2 + 1]);
            for (int i = 0; i < chars.Length; i++)
                if (char.IsSurrogate(chars[i]))
                {
                    if (!char.IsHighSurrogate(chars[i]) || i + 1 >= chars.Length || !char.IsLowSurrogate(chars[++i]))
                        throw Error("A Unicode target contains an unpaired surrogate.");
                }

            return new string (chars);
        }

        void Add(uint code, int length, string unicode)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, 192);
            ulong key = Key(code, length);
            if (local.Contains(key) && values.TryGetValue(key, out string? previousValue) && previousValue != unicode)
                throw Error("A CMap contains conflicting local Unicode mappings.");
            local.Add(key);
            values[key] = unicode;
        }
    }

    private static bool Overlaps(List<(uint First, uint Last, int Length)> spaces, uint first, uint last, int length)
    {
        int low = 0, high = spaces.Count - 1;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            var space = spaces[middle];
            if (space.Length < length || space.Length == length && space.Last < first)
                low = middle + 1;
            else if (space.Length > length || space.First > last)
                high = middle - 1;
            else
                return true;
        }

        return false;
    }
}

/// <summary>Exact decoded logical text and its operation report, including any explicitly permitted substitutions.</summary>
public sealed class PdfDecodedText
{
    internal PdfDecodedText(string text, PdfOperationReport report)
    {
        Text = text;
        OperationReport = report;
    }

    /// <summary>Gets decoded logical Unicode.</summary>
    public string Text { get; }
    /// <summary>Gets the complete operation report.</summary>
    public PdfOperationReport OperationReport { get; }
}
