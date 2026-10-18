using System.Globalization;
using System.IO;
using System.Text;

namespace Fluent.Pdf.Fonts;
internal sealed class Type1Font : IPdfFontProgram
{
    private readonly byte[] _header, _plain;
    private readonly List<(string Name, int Begin, int End, byte[] Program)> _glyphs = new();
    private readonly Dictionary<int, byte[]> _subrs = new();
    private readonly Dictionary<string, int> _glyphNames = new(StringComparer.Ordinal);
    private readonly Dictionary<int, int> _unicode = new();
    private readonly List<(int Begin, int End, bool Name)> _renames = new();
    private readonly int _countBegin, _countEnd, _entriesBegin, _entriesEnd, _plainEnd;
    private readonly int _lenIv;
    private readonly string[] _standardEncoding;
    public PdfFontInfo Info { get; }

    internal Type1Font(byte[] source, PdfOperationContext context)
    {
        var data = new FontData(source, context);
        byte[] cipher;
        if (source[0] == 128)
        {
            var prefix = new PdfByteBuilder(context);
            var encrypted = new PdfByteBuilder(context);
            bool binary = false, ended = false;
            int position = 0;
            while (position < source.Length)
            {
                context.Checkpoint();
                if (data.U8(position++) != 128)
                    throw data.Error("Font.Type1Pfb", "A PFB segment marker is missing.");
                int kind = data.U8(position++);
                if (kind == 3)
                {
                    ended = true;
                    break;
                }

                if (kind != 1 && kind != 2)
                    throw data.Error("Font.Type1Pfb", "A PFB segment type is invalid.");
                data.Range(position, 4);
                uint length = (uint)(source[position] | source[position + 1] << 8 | source[position + 2] << 16 | source[position + 3] << 24);
                position += 4;
                data.Range(position, length);
                if (kind == 2)
                    binary = true;
                if (kind == 2 || !binary)
                    for (uint i = 0; i < length; i++)
                    {
                        if ((i & 1023) == 0)
                            context.Checkpoint();
                        (kind == 2 ? encrypted : prefix).Add(source[position + (int)i]);
                    }

                position = checked(position + (int)length);
            }

            if (!ended || position != source.Length || !binary)
                throw data.Error("Font.Type1Pfb", "The PFB framing is incomplete or has trailing data.");
            _header = Array(prefix.Build(), context);
            cipher = Array(encrypted.Build(), context);
        }
        else
        {
            var lexer = new Type1Lexer(source, context);
            Type1Lexer.Token? token;
            int end = -1;
            while ((token = lexer.Next()) != null)
                if (!token.Value.Literal && !token.Value.Quoted && token.Value.Text == "eexec")
                {
                    end = token.Value.End;
                    break;
                }

            if (end < 0)
                throw data.Error("Font.Type1Eexec", "The Type 1 program has no explicit eexec boundary.");
            int start = end;
            while (start < source.Length && Type1Lexer.White(source[start]))
                start++;
            _header = data.Copy(0, start);
            int probe = start, hexDigits = 0;
            while (probe < source.Length && hexDigits < 4)
            {
                int c = source[probe++];
                if (Type1Lexer.White(c))
                    continue;
                if (PdfSyntaxCursor.Hex(c) < 0)
                    break;
                hexDigits++;
            }

            if (hexDigits == 4)
            {
                var decoded = new PdfByteBuilder(context);
                int high = -1;
                for (int i = start; i < source.Length; i++)
                {
                    if ((i & 1023) == 0)
                        context.Checkpoint();
                    int c = source[i];
                    if (Type1Lexer.White(c))
                        continue;
                    int digit = PdfSyntaxCursor.Hex(c);
                    if (digit < 0)
                        break;
                    if (high < 0)
                        high = digit;
                    else
                    {
                        decoded.Add((byte)(high * 16 + digit));
                        high = -1;
                    }
                }

                cipher = Array(decoded.Build(), context);
            }
            else
                cipher = data.Copy(start, source.Length - start);
        }

        _plain = Cipher(cipher, 55665, false, context);
        if (_plain.Length < 4)
            throw data.Error("Font.Type1Eexec", "The encrypted Type 1 private section is truncated.");
        string? name = null;
        double[]? bbox = null, matrix = null;
        PdfFontEmbeddingRights rights = PdfFontEmbeddingRights.Installable;
        var headerLexer = new Type1Lexer(_header, context);
        Type1Lexer.Token? item;
        while ((item = headerLexer.Next()) != null)
        {
            if (!item.Value.Literal)
                continue;
            string key = item.Value.Text;
            if (key == "FontName")
            {
                var value = headerLexer.Required();
                if (name != null || !value.Literal)
                    throw data.Error("Font.Type1Name", "One direct FontName declaration is required.");
                name = value.Text;
                _renames.Add((value.Start, value.End, true));
            }
            else if (key == "FullName" || key == "FamilyName")
            {
                var value = headerLexer.Required();
                if (value.Quoted)
                    _renames.Add((value.Start, value.End, false));
            }
            else if (key == "FontBBox")
                bbox = Numbers(headerLexer, 4, data);
            else if (key == "FontMatrix")
                matrix = Numbers(headerLexer, 6, data);
            else if (key == "FontType")
            {
                var value = headerLexer.Required();
                if (!value.Number(out double number) || number != 1)
                    throw data.Error("Font.Type1Format", "The PostScript font is not Type 1.");
            }
            else if (key == "FSType")
                rights = Rights(headerLexer.Required(), data);
        }

        if (name == null || bbox == null)
            throw data.Error("Font.Type1Metadata", "A Type 1 FontName and FontBBox are required.");
        matrix ??= new[]
        {
            .001,
            0,
            0,
            .001,
            0,
            0
        };
        if (matrix[0] <= 0 || matrix[0] != matrix[3] || matrix[1] != 0 || matrix[2] != 0 || matrix[4] != 0 || matrix[5] != 0)
            throw data.Error("Font.Type1Matrix", "This Type 1 font requires an explicit affine font-metric implementation.");
        double units = 1 / matrix[0];
        if (units < 1 || units > 16384 || Math.Abs(units - Math.Round(units)) > 0.000001 || bbox.Any(v => v < int.MinValue || v > int.MaxValue))
            throw data.Error("Font.Type1Metrics", "Type 1 design metrics are outside the supported range.");
        _countBegin = _countEnd = _entriesBegin = _entriesEnd = -1;
        int lenIv = 4, plainEnd = -1;
        var privateLexer = new Type1Lexer(_plain, context, 4);
        var history = new Queue<Type1Lexer.Token>();
        while ((item = privateLexer.Next()) != null)
        {
            var token = item.Value;
            if (!token.Literal && !token.Quoted && token.Text == "closefile")
            {
                plainEnd = token.End;
                break;
            }

            if (token.Literal && token.Text == "lenIV")
            {
                var value = privateLexer.Required();
                if (!value.Number(out double n) || n < -1 || n > 255 || n != Math.Floor(n))
                    throw data.Error("Font.Type1LenIv", "A Type 1 lenIV value is invalid.");
                lenIv = (int)n;
                history.Clear();
                continue;
            }

            if (token.Literal && token.Text == "FSType")
            {
                rights = Rights(privateLexer.Required(), data);
                history.Clear();
                continue;
            }

            if (token.Literal && token.Text == "CharStrings")
            {
                if (_countBegin >= 0)
                    throw data.Error("Font.Type1CharStrings", "The Type 1 program repeats CharStrings.");
                var capacity = privateLexer.Required();
                int count = Count(capacity, data);
                _countBegin = capacity.Start;
                _countEnd = capacity.End;
                bool begin = false;
                for (int i = 0; i < 8; i++)
                {
                    var next = privateLexer.Required();
                    if (!next.Literal && next.Text == "begin")
                    {
                        begin = true;
                        break;
                    }
                }

                if (!begin)
                    throw data.Error("Font.Type1CharStrings", "A direct CharStrings dictionary is required.");
                int entriesBegin = -1;
                while (true)
                {
                    var key = privateLexer.Required();
                    if (!key.Literal && key.Text == "end")
                    {
                        _entriesEnd = key.Start;
                        break;
                    }

                    if (!key.Literal || key.Text.Length == 0 || _glyphNames.ContainsKey(key.Text))
                        throw data.Error("Font.Type1CharStrings", "CharStrings glyph names must be direct and unique.");
                    if (entriesBegin < 0)
                        entriesBegin = key.Start;
                    int length = Count(privateLexer.Required(), data);
                    var rd = privateLexer.Required();
                    if (rd.Literal || rd.Text != "RD" && rd.Text != "-|")
                        throw data.Error("Font.Type1Binary", "A glyph charstring requires an explicit RD binary extent.");
                    int binary = privateLexer.Binary(length);
                    EndBinary(privateLexer, data);
                    context.Consume(PdfResourceKind.ExpandedBytes, 128);
                    _glyphNames.Add(key.Text, _glyphs.Count);
                    _glyphs.Add((key.Text, key.Start, privateLexer.Position, new FontData(_plain, context).Copy(binary, length)));
                    if (_glyphs.Count > count)
                        throw data.Error("Font.Type1CharStrings", "CharStrings entries exceed their declared dictionary capacity.");
                }

                _entriesBegin = entriesBegin < 0 ? _entriesEnd : entriesBegin;
                history.Clear();
                continue;
            }

            if (!token.Literal && (token.Text == "RD" || token.Text == "-|"))
            {
                var previous = history.ToArray();
                if (previous.Length < 2)
                    throw data.Error("Font.Type1Binary", "A Type 1 subroutine has no explicit index/length.");
                int length = Count(previous[previous.Length - 1], data), index = Count(previous[previous.Length - 2], data);
                if (index > 65535 || _subrs.ContainsKey(index))
                    throw data.Error("Font.Type1Subrs", "A Type 1 subroutine index is invalid or repeated.");
                int binary = privateLexer.Binary(length);
                _subrs.Add(index, new FontData(_plain, context).Copy(binary, length));
                EndBinary(privateLexer, data);
                history.Clear();
                continue;
            }

            history.Enqueue(token);
            while (history.Count > 3)
                history.Dequeue();
        }

        if (_countBegin < 0 || plainEnd < 0 || _glyphs.Count == 0 || !_glyphNames.TryGetValue(".notdef", out int missing))
            throw data.Error("Font.Type1CharStrings", "The Type 1 private section needs CharStrings, .notdef and an explicit closefile boundary.");
        if (missing != 0)
        {
            var first = _glyphs[0];
            _glyphs[0] = _glyphs[missing];
            _glyphs[missing] = first;
            _glyphNames[_glyphs[0].Name] = 0;
            _glyphNames[first.Name] = missing;
        }

        _plainEnd = plainEnd;
        _lenIv = lenIv;
        var glyphList = FontResources.GlyphList(context);
        for (int i = 0; i < _glyphs.Count; i++)
        {
            context.Checkpoint();
            string unicode = FontResources.Unicode(_glyphs[i].Name, glyphList);
            if (unicode.Length == 1 && !char.IsSurrogate(unicode[0]))
                _unicode[(int)unicode[0]] = i;
            else if (unicode.Length == 2 && char.IsSurrogatePair(unicode, 0))
                _unicode[char.ConvertToUtf32(unicode, 0)] = i;
        }

        _standardEncoding = FontResources.Text("StandardEncoding.txt", context).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var metrics = new PdfFontMetrics((int)Math.Round(units), (int)Math.Ceiling(bbox[3]), (int)Math.Floor(bbox[1]), (int)Math.Ceiling(bbox[3]), (int)Math.Floor(bbox[0]), (int)Math.Floor(bbox[1]), (int)Math.Ceiling(bbox[2]), (int)Math.Ceiling(bbox[3]));
        Info = new PdfFontInfo(name, PdfFontFormat.Type1, _glyphs.Count, metrics, FontData.Hash(source, context), rights);
        for (int i = 0; i < _glyphs.Count; i++)
            Width(i, context);
    }

    private static byte[] Array(PdfByteStore store, PdfOperationContext context)
    {
        if (store.Length > int.MaxValue)
            throw PdfWriterCore.Error(context, "Font.ProgramRange", "A font section exceeds the managed range.");
        context.Consume(PdfResourceKind.ExpandedBytes, store.Length);
        var bytes = new byte[(int)store.Length];
        using var input = store.Open(0, store.Length);
        int count = 0;
        while (count < bytes.Length)
        {
            context.Checkpoint();
            count += input.Read(bytes, count, bytes.Length - count);
        }

        return bytes;
    }

    private static int Count(Type1Lexer.Token token, FontData data)
    {
        if (!token.Number(out double value) || value < 0 || value > int.MaxValue || value != Math.Floor(value))
            throw data.Error("Font.Type1Count", "A Type 1 binary count must be a nonnegative bounded integer.");
        return (int)value;
    }

    private static void EndBinary(Type1Lexer lexer, FontData data)
    {
        for (int i = 0; i < 4; i++)
        {
            var token = lexer.Required();
            if (!token.Literal && (token.Text == "ND" || token.Text == "|-" || token.Text == "NP" || token.Text == "def" || token.Text == "put"))
                return;
        }

        throw data.Error("Font.Type1Binary", "A binary charstring/subroutine has no explicit definition terminator.");
    }

    private static double[] Numbers(Type1Lexer lexer, int count, FontData data)
    {
        var open = lexer.Required();
        if (open.Text != "[" && open.Text != "{")
            throw data.Error("Font.Type1Metrics", "Font geometry must be a direct numeric array.");
        data.Context.Consume(PdfResourceKind.ExpandedBytes, count * 8L);
        var values = new double[count];
        for (int i = 0; i < count; i++)
            if (!lexer.Required().Number(out values[i]))
                throw data.Error("Font.Type1Metrics", "Font geometry must contain finite numbers.");
        if (lexer.Required().Text != (open.Text == "[" ? "]" : "}"))
            throw data.Error("Font.Type1Metrics", "A font geometry array has the wrong extent.");
        return values;
    }

    private static PdfFontEmbeddingRights Rights(Type1Lexer.Token token, FontData data)
    {
        int value = Count(token, data);
        if ((value & ~782) != 0)
            throw data.Error("Font.EmbeddingRights", "The font declares unknown embedding flags.");
        return (PdfFontEmbeddingRights)value;
    }

    internal static byte[] Cipher(byte[] source, int seed, bool encrypt, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, source.Length);
        var result = new byte[source.Length];
        int state = seed;
        for (int i = 0; i < source.Length; i++)
        {
            if ((i & 1023) == 0)
                context.Checkpoint();
            byte transformed = (byte)(source[i] ^ (state >> 8));
            result[i] = transformed;
            int cipher = encrypt ? transformed : source[i];
            state = ((cipher + state) * 52845 + 22719) & 65535;
        }

        return result;
    }

    private byte[] CharString(byte[] source, PdfOperationContext context)
    {
        if (_lenIv < 0)
            return source;
        var decoded = Cipher(source, 4330, false, context);
        if (decoded.Length < _lenIv)
            throw PdfWriterCore.Error(context, "Font.Type1CharString", "A charstring is shorter than lenIV.");
        return new FontData(decoded, context).Copy(_lenIv, decoded.Length - _lenIv);
    }

    public int MapGlyph(int unicodeScalar, PdfOperationContext context)
    {
        FontData.Scalar(unicodeScalar);
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        return _unicode.TryGetValue(unicodeScalar, out int glyph) ? glyph : 0;
    }

    public double GetAdvance(int glyph, PdfOperationContext context)
    {
        Check(glyph, context);
        return Width(glyph, context);
    }

    public double GetVerticalAdvance(int glyph, PdfOperationContext context)
    {
        Check(glyph, context);
        return Info.Metrics.UnitsPerEm;
    }

    public string GetGlyphName(int glyph, PdfOperationContext context)
    {
        Check(glyph, context);
        return _glyphs[glyph].Name;
    }

    private void Check(int glyph, PdfOperationContext context)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        if (glyph < 0 || glyph >= _glyphs.Count)
            throw new ArgumentOutOfRangeException(nameof(glyph));
    }

    private double Width(int glyph, PdfOperationContext context)
    {
        var operands = new List<double>();
        double? width = null;
        var active = new HashSet<int>();
        Execute(CharString(_glyphs[glyph].Program, context));
        if (!width.HasValue)
            throw PdfWriterCore.Error(context, "Font.Type1Width", "A glyph has no explicit horizontal width.");
        return width.Value;
        void Execute(byte[] bytes)
        {
            using var scope = context.EnterScope(PdfResourceKind.Depth);
            int position = 0;
            while (position < bytes.Length && !width.HasValue)
            {
                context.Checkpoint();
                int code = bytes[position++];
                if (code >= 32)
                {
                    operands.Add(Number(bytes, ref position, code, context));
                    if (operands.Count > 24)
                        throw PdfWriterCore.Error(context, "Font.Type1Stack", "The Type 1 operand stack exceeds 24 values.");
                    continue;
                }

                if (code == 13)
                {
                    if (operands.Count != 2)
                        throw PdfWriterCore.Error(context, "Font.Type1Width", "hsbw requires sidebearing and width.");
                    width = operands[1];
                    return;
                }

                if (code == 10)
                {
                    int subr = PopIndex(operands, context);
                    if (!_subrs.TryGetValue(subr, out var program) || !active.Add(subr))
                        throw PdfWriterCore.Error(context, "Font.Type1Subrs", "A width subroutine is missing or cyclic.");
                    try
                    {
                        Execute(CharString(program, context));
                    }
                    finally
                    {
                        active.Remove(subr);
                    }

                    continue;
                }

                if (code == 11)
                    return;
                if (code == 12 && position < bytes.Length)
                {
                    int escape = bytes[position++];
                    if (escape == 7)
                    {
                        if (operands.Count != 4)
                            throw PdfWriterCore.Error(context, "Font.Type1Width", "sbw requires two sidebearings and two widths.");
                        width = operands[2];
                        return;
                    }

                    if (escape == 12)
                    {
                        Divide(operands, context);
                        continue;
                    }
                }

                throw PdfWriterCore.Error(context, "Font.Type1WidthUnsupported", "An unsupported instruction occurs before a glyph's explicit width.");
            }
        }
    }

    private static double Number(byte[] bytes, ref int position, int code, PdfOperationContext context)
    {
        int needed = code == 255 ? 4 : code >= 247 ? 1 : 0;
        if (position > bytes.Length - needed)
            throw PdfWriterCore.Error(context, "Font.Type1CharString", "A Type 1 numeric operand is truncated.");
        if (code <= 246)
            return code - 139;
        if (code <= 250)
            return (code - 247) * 256 + bytes[position++] + 108;
        if (code <= 254)
            return -(code - 251) * 256 - bytes[position++] - 108;
        int value = bytes[position] << 24 | bytes[position + 1] << 16 | bytes[position + 2] << 8 | bytes[position + 3];
        position += 4;
        return value;
    }

    private static int PopIndex(List<double> operands, PdfOperationContext context)
    {
        if (operands.Count == 0)
            throw PdfWriterCore.Error(context, "Font.Type1Stack", "A subroutine index is missing.");
        double value = operands[operands.Count - 1];
        operands.RemoveAt(operands.Count - 1);
        if (value < 0 || value > 65535 || value != Math.Floor(value))
            throw PdfWriterCore.Error(context, "Font.Type1Subrs", "A subroutine index is outside range.");
        return (int)value;
    }

    private static void Divide(List<double> operands, PdfOperationContext context)
    {
        if (operands.Count < 2)
            throw PdfWriterCore.Error(context, "Font.Type1Stack", "div requires two operands.");
        double value = operands[operands.Count - 2] / operands[operands.Count - 1];
        if (double.IsInfinity(value) || double.IsNaN(value))
            throw PdfWriterCore.Error(context, "Font.Type1Number", "A Type 1 division is not finite.");
        operands.RemoveAt(operands.Count - 1);
        operands[operands.Count - 1] = value;
    }

    public PdfFontProgramWriteResult Write(Stream output, IReadOnlyList<int> glyphs, bool subset, PdfOperationContext context)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (glyphs == null)
            throw new ArgumentNullException(nameof(glyphs));
        context.Checkpoint();
        if ((Info.EmbeddingRights & (PdfFontEmbeddingRights.Restricted | PdfFontEmbeddingRights.BitmapOnly)) != 0)
            throw PdfWriterCore.Error(context, "Font.EmbeddingRestricted", "The font's declared rights forbid outline embedding.");
        if (subset && (Info.EmbeddingRights & PdfFontEmbeddingRights.NoSubsetting) != 0)
            throw PdfWriterCore.Error(context, "Font.SubsettingRestricted", "The font's declared rights forbid subsetting.");
        context.Consume(PdfResourceKind.ExpandedBytes, _glyphs.Count * 32L + 512);
        var retained = new HashSet<int>
        {
            0
        };
        foreach (int glyph in glyphs)
        {
            Check(glyph, context);
            retained.Add(glyph);
        }

        if (subset)
            Dependencies(retained, context);
        context.Consume(PdfResourceKind.ExpandedBytes, retained.Count * 64L + 1024);
        string identity = Info.Sha256 + ":" + string.Join(",", retained.OrderBy(x => x));
        using var subsetHash = System.Security.Cryptography.SHA256.Create();
        byte[] digest = subsetHash.ComputeHash(Encoding.ASCII.GetBytes(identity));
        string name = subset ? new string (digest.Take(6).Select(x => (char)('A' + x % 26)).ToArray()) + "+FluentType1" : Info.PostScriptName;
        var header = new PdfByteBuilder(context);
        int previous = 0;
        foreach (var span in (subset ? _renames : Enumerable.Empty<(int Begin, int End, bool Name)>()).OrderBy(x => x.Begin))
        {
            for (int i = previous; i < span.Begin; i++)
                header.Add(_header[i]);
            string value = span.Name ? "/" + name : "(" + name + ")";
            foreach (byte b in Encoding.ASCII.GetBytes(value))
                header.Add(b);
            previous = span.End;
        }

        for (int i = previous; i < _header.Length; i++)
            header.Add(_header[i]);
        var plain = new PdfByteBuilder(context);
        int count = subset ? retained.Count : _glyphs.Count;
        for (int i = 0; i < _countBegin; i++)
            plain.Add(_plain[i]);
        foreach (byte b in Encoding.ASCII.GetBytes(count.ToString(CultureInfo.InvariantCulture)))
            plain.Add(b);
        for (int i = _countEnd; i < _entriesBegin; i++)
            plain.Add(_plain[i]);
        foreach (var pair in _glyphs.Select((glyph, index) => (Glyph: glyph, Index: index)).OrderBy(x => x.Glyph.Begin))
        {
            context.Checkpoint();
            if (subset && !retained.Contains(pair.Index))
                continue;
            for (int i = pair.Glyph.Begin; i < pair.Glyph.End; i++)
                plain.Add(_plain[i]);
            plain.Add(10);
        }

        for (int i = _entriesEnd; i < _plainEnd; i++)
            plain.Add(_plain[i]);
        plain.Add(10);
        byte[] encrypted = Cipher(Array(plain.Build(), context), 55665, true, context), prefix = Array(header.Build(), context);
        output.Write(prefix, 0, prefix.Length);
        output.Write(encrypted, 0, encrypted.Length);
        context.Consume(PdfResourceKind.ExpandedBytes, 545);
        byte[] trailer = Encoding.ASCII.GetBytes("\n" + new string ('0', 512) + "\ncleartomark\n");
        output.Write(trailer, 0, trailer.Length);
        context.Checkpoint();
        return new PdfFontProgramWriteResult(name, length1: prefix.Length, length2: encrypted.Length, length3: trailer.Length);
    }

    private void Dependencies(HashSet<int> retained, PdfOperationContext context)
    {
        // Seac dependencies use StandardEncoding identities, independent of the font's external encoding.
        var pending = new Queue<int>(retained);
        var visited = new HashSet<int>();
        bool conservative = false;
        while (pending.Count > 0 && !conservative)
        {
            int glyph = pending.Dequeue();
            if (!visited.Add(glyph))
                continue;
            var stack = new List<double>();
            var active = new HashSet<int>();
            Execute(CharString(_glyphs[glyph].Program, context));
            void Execute(byte[] bytes)
            {
                using var scope = context.EnterScope(PdfResourceKind.Depth);
                int position = 0;
                while (position < bytes.Length && !conservative)
                {
                    context.Checkpoint();
                    int code = bytes[position++];
                    if (code >= 32)
                    {
                        stack.Add(Number(bytes, ref position, code, context));
                        if (stack.Count > 24)
                            throw PdfWriterCore.Error(context, "Font.Type1Stack", "The Type 1 operand stack exceeds 24 values.");
                        continue;
                    }

                    if (code == 10)
                    {
                        int subr = PopIndex(stack, context);
                        if (!_subrs.TryGetValue(subr, out var program) || !active.Add(subr))
                            throw PdfWriterCore.Error(context, "Font.Type1Subrs", "A glyph subroutine is absent or cyclic.");
                        try
                        {
                            Execute(CharString(program, context));
                        }
                        finally
                        {
                            active.Remove(subr);
                        }

                        continue;
                    }

                    if (code == 11 || code == 14)
                        return;
                    if (code == 12)
                    {
                        if (position >= bytes.Length)
                            throw PdfWriterCore.Error(context, "Font.Type1CharString", "A Type 1 escape is truncated.");
                        int escape = bytes[position++];
                        if (escape == 12)
                        {
                            Divide(stack, context);
                            continue;
                        }

                        if (escape == 6)
                        {
                            if (stack.Count != 5)
                                throw PdfWriterCore.Error(context, "Font.Type1Seac", "seac requires five operands.");
                            foreach (double value in stack.Skip(3))
                            {
                                if (value < 0 || value >= 256 || value != Math.Floor(value) || !_glyphNames.TryGetValue(_standardEncoding[(int)value], out int component))
                                    throw PdfWriterCore.Error(context, "Font.Type1Seac", "A composite accent references an absent StandardEncoding glyph.");
                                if (retained.Add(component))
                                    pending.Enqueue(component);
                            }

                            return;
                        }

                        if (escape == 16 || escape == 17)
                        {
                            conservative = true;
                            return;
                        }

                        if (escape != 0 && escape != 1 && escape != 2 && escape != 7 && escape != 33)
                        {
                            conservative = true;
                            return;
                        }
                    }
                    else if (code != 1 && code != 3 && code != 4 && code != 5 && code != 6 && code != 7 && code != 8 && code != 9 && code != 13 && code != 21 && code != 22 && code != 30 && code != 31)
                    {
                        conservative = true;
                        return;
                    }

                    stack.Clear();
                }
            }
        }

        if (conservative)
        {
            for (int i = 0; i < _glyphs.Count; i++)
            {
                context.Checkpoint();
                retained.Add(i);
            }

            context.Report(new PdfDiagnostic("Font.ConservativeSubset", "Type 1 OtherSubrs require conservatively retaining all glyph charstrings; no PostScript is executed.", severity: PdfDiagnosticSeverity.Information));
        }
    }
}
