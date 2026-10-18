using System.Text;

namespace Fluent.Pdf.Fonts;
// Original CFF1 structure reader and CID-keyed writer. Subroutine identities are retained conservatively.
internal sealed class CffFont
{
    private readonly byte[] _source;
    private readonly Dictionary<int, double[]> _top;
    private readonly CffData.Index _strings, _global, _charStrings;
    private readonly byte[] _fdSelect;
    private readonly string[] _glyphNames;
    private readonly List<(Dictionary<int, double[]> Dict, byte[] Private)> _fonts = new();
    internal int GlyphCount => _charStrings.Items.Length;
    internal bool CidKeyed => _top.ContainsKey(1230);
    internal string Name { get; }

    internal CffFont(byte[] source, int glyphCount, PdfOperationContext context)
    {
        _source = source;
        var cff = new CffData(source, context);
        var data = cff.Data;
        if (data.U8(0) != 1 || data.U8(2) < 4 || data.U8(3) < 1 || data.U8(3) > 4)
            throw data.Error("Font.CffHeader", "The CFF1 header is invalid.");
        var names = cff.ReadIndex(data.U8(2));
        var dictionaries = cff.ReadIndex(names.End);
        if (names.Items.Length != 1 || dictionaries.Items.Length != 1)
            throw data.Error("Font.CffFontSet", "A single selected CFF face is required.");
        var nameItem = names.Items[0];
        Name = FontData.ByteText(source, nameItem.Offset, nameItem.Length, context);
        _top = cff.Dictionary(dictionaries.Items[0].Offset, dictionaries.Items[0].Length);
        _strings = cff.ReadIndex(dictionaries.End);
        _global = cff.ReadIndex(_strings.End);
        int charStrings = cff.Integer(_top, 17);
        if (charStrings < 0)
            throw data.Error("Font.CffCharStrings", "A CFF CharStrings INDEX is required.");
        _charStrings = cff.ReadIndex(charStrings);
        if (glyphCount == 0)
            glyphCount = _charStrings.Items.Length;
        if (glyphCount == 0 || _charStrings.Items.Length != glyphCount)
            throw data.Error("Font.CffGlyphCount", "The CFF and SFNT glyph counts disagree or are empty.");
        context.Consume(PdfResourceKind.ExpandedBytes, glyphCount * 16L);
        _fdSelect = new byte[glyphCount];
        _glyphNames = new string[glyphCount];
        _glyphNames[0] = ".notdef";
        if (_top.ContainsKey(1230))
        {
            var ros = _top[1230];
            if (ros.Length != 3)
                throw data.Error("Font.CffRos", "A CID-keyed CFF requires a three-value ROS declaration.");
            int fdArray = cff.Integer(_top, 1236), fdSelect = cff.Integer(_top, 1237);
            if (fdArray < 0 || fdSelect < 0)
                throw data.Error("Font.CffCid", "CID-keyed CFF requires FDArray and FDSelect.");
            var fonts = cff.ReadIndex(fdArray);
            if (fonts.Items.Length == 0 || fonts.Items.Length > 256)
                throw data.Error("Font.CffCid", "The CFF font-dictionary count is outside range.");
            foreach (var item in fonts.Items)
            {
                context.Checkpoint();
                var dictionary = cff.Dictionary(item.Offset, item.Length);
                _fonts.Add((dictionary, Private(dictionary, cff)));
            }

            int format = data.U8(fdSelect++);
            if (format == 0)
            {
                data.Range(fdSelect, glyphCount);
                Buffer.BlockCopy(source, fdSelect, _fdSelect, 0, glyphCount);
            }
            else if (format == 3)
            {
                int ranges = data.U16(fdSelect);
                fdSelect += 2;
                data.Range(fdSelect, ranges * 3L + 2);
                if (ranges == 0)
                    throw data.Error("Font.CffFdSelect", "A format-three FDSelect requires ranges.");
                int previous = 0;
                for (int i = 0; i < ranges; i++)
                {
                    int first = data.U16(fdSelect + i * 3), font = data.U8(fdSelect + i * 3 + 2), end = i + 1 < ranges ? data.U16(fdSelect + (i + 1) * 3) : data.U16(fdSelect + ranges * 3);
                    if (first != previous || end <= first || end > glyphCount)
                        throw data.Error("Font.CffFdSelect", "FDSelect ranges must cover glyphs exactly in order.");
                    for (int glyph = first; glyph < end; glyph++)
                    {
                        if ((glyph & 255) == 0)
                            context.Checkpoint();
                        _fdSelect[glyph] = (byte)font;
                    }

                    previous = end;
                }

                if (previous != glyphCount)
                    throw data.Error("Font.CffFdSelect", "The FDSelect sentinel does not equal the glyph count.");
            }
            else
                throw data.Error("Font.CffFdSelect", "The CFF1 FDSelect format is unsupported.");
            foreach (byte index in _fdSelect)
                if (index >= _fonts.Count)
                    throw data.Error("Font.CffFdSelect", "FDSelect references an absent font dictionary.");
        }
        else
            _fonts.Add((new Dictionary<int, double[]>(), Private(_top, cff)));
        // Validate charset extents even though output uses an original, stable GID-to-CID charset.
        bool cid = _top.ContainsKey(1230);
        var standard = FontResources.Text("CffStandardStrings.txt", context).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string GlyphName(int sid)
        {
            if (cid)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, 64);
                return "cid" + sid.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (sid < standard.Length)
                return standard[sid];
            sid -= 391;
            if (sid < 0 || sid >= _strings.Items.Length)
                throw data.Error("Font.CffCharset", "A CFF glyph SID is outside the string INDEX.");
            var item = _strings.Items[sid];
            return FontData.ByteText(source, item.Offset, item.Length, context);
        }

        int charset = cff.Integer(_top, 15, 0);
        if (charset > 2)
        {
            int format = data.U8(charset++), glyph = 1;
            if (format == 0)
            {
                data.Range(charset, (glyphCount - 1L) * 2);
                for (int i = 1; i < glyphCount; i++)
                {
                    context.Checkpoint();
                    _glyphNames[i] = GlyphName(data.U16(charset + (i - 1) * 2));
                }

                glyph = glyphCount;
            }
            else if (format == 1 || format == 2)
            {
                while (glyph < glyphCount)
                {
                    context.Checkpoint();
                    int sid = data.U16(charset);
                    charset += 2;
                    int left = format == 1 ? data.U8(charset++) : data.U16(charset);
                    if (format == 2)
                        charset += 2;
                    if (left >= glyphCount - glyph || sid > 65535 - left)
                        throw data.Error("Font.CffCharset", "A CFF charset range exceeds its glyph count.");
                    for (int i = 0; i <= left; i++)
                        _glyphNames[glyph++] = GlyphName(sid + i);
                }
            }
            else
                throw data.Error("Font.CffCharset", "The CFF charset format is invalid.");
        }
        else
        {
            if (cid)
                throw data.Error("Font.CffCharset", "CID-keyed CFF requires an explicit charset.");
            var predefined = charset == 0 ? standard.Take(229).ToArray() : FontResources.Text(charset == 1 ? "CffExpertCharset.txt" : "CffExpertSubsetCharset.txt", context).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (glyphCount > predefined.Length)
                throw data.Error("Font.CffCharset", "The predefined CFF charset has too few glyphs.");
            for (int i = 0; i < glyphCount; i++)
                _glyphNames[i] = predefined[i];
        }
    }

    internal string GlyphName(int glyph) => _glyphNames[glyph];
    internal PdfFontMetrics Metrics(PdfOperationContext context)
    {
        var data = new FontData(_source, context);
        double[] matrix = _top.TryGetValue(1207, out var m) ? m : new[]
        {
            .001,
            0,
            0,
            .001,
            0,
            0
        }, box = _top.TryGetValue(5, out var b) ? b : new double[]
        {
            0,
            0,
            0,
            0
        };
        if (matrix.Length != 6 || matrix[0] <= 0 || matrix[0] != matrix[3] || matrix[1] != 0 || matrix[2] != 0 || matrix[4] != 0 || matrix[5] != 0)
            throw data.Error("Font.CffMatrix", "This CFF face requires an explicit affine metric implementation.");
        double units = 1 / matrix[0];
        if (units < 1 || units > 16384 || Math.Abs(units - Math.Round(units)) > 0.000001 || box.Length != 4 || box.Any(x => x < int.MinValue || x > int.MaxValue))
            throw data.Error("Font.CffMetrics", "The CFF metrics are outside the supported design range.");
        return new PdfFontMetrics((int)Math.Round(units), (int)Math.Ceiling(box[3]), (int)Math.Floor(box[1]), (int)Math.Ceiling(box[3]), (int)Math.Floor(box[0]), (int)Math.Floor(box[1]), (int)Math.Ceiling(box[2]), (int)Math.Ceiling(box[3]));
    }

    internal double Width(int glyph, PdfOperationContext context)
    {
        var font = _fonts[_fdSelect[glyph]];
        var original = font.Dict.TryGetValue(18, out var localPrivate) ? localPrivate : _top.TryGetValue(18, out var topPrivate) ? topPrivate : System.Array.Empty<double>();
        var privateData = new CffData(font.Private, context);
        var dictionary = font.Private.Length == 0 ? new Dictionary<int, double[]>() : privateData.Dictionary(0, (int)original[0]);
        double defaultWidth = 0, nominal = 0;
        if (dictionary.TryGetValue(20, out var dw))
        {
            if (dw.Length != 1)
                throw privateData.Data.Error("Font.CffWidth", "defaultWidthX must contain one value.");
            defaultWidth = dw[0];
        }

        if (dictionary.TryGetValue(21, out var nw))
        {
            if (nw.Length != 1)
                throw privateData.Data.Error("Font.CffWidth", "nominalWidthX must contain one value.");
            nominal = nw[0];
        }

        int localOffset = privateData.Integer(dictionary, 19);
        var locals = localOffset < 0 ? null : privateData.ReadIndex(localOffset);
        var stack = new List<double>();
        var active = new HashSet<(bool Global, int Index)>();
        double? width = null;
        Execute(_source, _charStrings.Items[glyph]);
        return width ?? throw PdfWriterCore.Error(context, "Font.CffWidth", "A Type 2 glyph has no width-defining instruction.");
        void Execute(byte[] bytes, (int Offset, int Length) item)
        {
            using var scope = context.EnterScope(PdfResourceKind.Depth);
            int position = item.Offset, end = checked(position + item.Length);
            while (position < end && !width.HasValue)
            {
                context.Checkpoint();
                int code = bytes[position++];
                if (code >= 32 || code == 28)
                {
                    double value;
                    if (code >= 32 && code <= 246)
                        value = code - 139;
                    else if (code >= 247 && code <= 254)
                    {
                        if (position >= end)
                            throw Error("A Type 2 number is truncated.");
                        value = code <= 250 ? (code - 247) * 256 + bytes[position++] + 108 : -(code - 251) * 256 - bytes[position++] - 108;
                    }
                    else if (code == 28)
                    {
                        if (position > end - 2)
                            throw Error("A Type 2 short number is truncated.");
                        value = (short)(bytes[position] * 256 + bytes[position + 1]);
                        position += 2;
                    }
                    else
                    {
                        if (position > end - 4)
                            throw Error("A Type 2 fixed number is truncated.");
                        value = (bytes[position] << 24 | bytes[position + 1] << 16 | bytes[position + 2] << 8 | bytes[position + 3]) / 65536.0;
                        position += 4;
                    }

                    context.Consume(PdfResourceKind.ExpandedBytes, 16);
                    stack.Add(value);
                    if (stack.Count > 48)
                        throw Error("The Type 2 operand stack exceeds 48 values.");
                    continue;
                }

                if (code == 10 || code == 29)
                {
                    if (stack.Count == 0)
                        throw Error("A Type 2 subroutine index is missing.");
                    double number = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    var index = code == 29 ? _global : locals;
                    if (index == null || number != Math.Floor(number) || number < int.MinValue || number > int.MaxValue)
                        throw Error("A Type 2 subroutine index is invalid.");
                    int bias = index.Items.Length < 1240 ? 107 : index.Items.Length < 33900 ? 1131 : 32768;
                    long id = (long)number + bias;
                    var key = (code == 29, (int)id);
                    if (id < 0 || id >= index.Items.Length || !active.Add(key))
                        throw Error("A Type 2 subroutine is absent or cyclic.");
                    try
                    {
                        Execute(code == 29 ? _source : font.Private, index.Items[(int)id]);
                    }
                    finally
                    {
                        active.Remove(key);
                    }

                    continue;
                }

                if (code == 11)
                    return;
                if (code == 1 || code == 3 || code == 18 || code == 23 || code == 19 || code == 20)
                {
                    width = stack.Count % 2 != 0 ? stack[0] + nominal : defaultWidth;
                    return;
                }

                if (code == 4 || code == 21 || code == 22)
                {
                    int count = code == 21 ? 2 : 1;
                    if (stack.Count != count && stack.Count != count + 1)
                        throw Error("A width-defining moveto has an invalid operand count.");
                    width = stack.Count == count + 1 ? stack[0] + nominal : defaultWidth;
                    return;
                }

                if (code == 14)
                {
                    if (stack.Count != 0 && stack.Count != 1 && stack.Count != 4 && stack.Count != 5)
                        throw Error("A Type 2 endchar has an invalid operand count.");
                    width = stack.Count == 1 || stack.Count == 5 ? stack[0] + nominal : defaultWidth;
                    return;
                }

                if (code == 12 && position < end)
                {
                    int escape = bytes[position++];
                    if (escape == 10 || escape == 11 || escape == 12 || escape == 24)
                    {
                        if (stack.Count < 2)
                            throw Error("A Type 2 arithmetic operation is missing operands.");
                        double right = stack[stack.Count - 1], left = stack[stack.Count - 2], value = escape == 10 ? left + right : escape == 11 ? left - right : escape == 12 ? left / right : left * right;
                        if (double.IsInfinity(value) || double.IsNaN(value))
                            throw Error("Type 2 arithmetic is not finite.");
                        stack.RemoveAt(stack.Count - 1);
                        stack[stack.Count - 1] = value;
                        continue;
                    }

                    if (escape == 9 || escape == 14 || escape == 26)
                    {
                        if (stack.Count == 0)
                            throw Error("A Type 2 unary operation is missing an operand.");
                        double value = stack[stack.Count - 1];
                        value = escape == 9 ? Math.Abs(value) : escape == 14 ? -value : Math.Sqrt(value);
                        if (double.IsNaN(value))
                            throw Error("Type 2 arithmetic is not finite.");
                        stack[stack.Count - 1] = value;
                        continue;
                    }
                }

                throw PdfWriterCore.Error(context, "Font.CffWidthUnsupported", "An unsupported Type 2 instruction occurs before its width is established.");
            }
        }

        PdfException Error(string message) => PdfWriterCore.Error(context, "Font.CffWidth", message);
    }

    private static byte[] Private(Dictionary<int, double[]> dictionary, CffData cff)
    {
        if (!dictionary.TryGetValue(18, out var values))
            return System.Array.Empty<byte>();
        if (values.Length != 2 || values.Any(x => x < 0 || x > int.MaxValue || x != Math.Floor(x)))
            throw cff.Data.Error("Font.CffPrivate", "A CFF Private declaration requires bounded size and offset integers.");
        int length = (int)values[0], offset = (int)values[1];
        var entries = cff.Dictionary(offset, length);
        int subrs = cff.Integer(entries, 19);
        if (subrs >= 0)
        {
            var index = cff.ReadIndex(checked(offset + subrs));
            length = Math.Max(length, index.End - offset);
        }

        return cff.Data.Copy(offset, length);
    }

    internal byte[] Write(string name, ISet<int> retained, bool subset, PdfOperationContext context)
    {
        var data = new FontData(_source, context);
        context.Consume(PdfResourceKind.ExpandedBytes, (_strings.Items.Length + _charStrings.Items.Length + 16L) * 16);
        var outlines = new Dictionary<int, CffGlyphMachine.Outline>();
        var activeGlyphs = new HashSet<int>();
        string[]? standardEncoding = null;
        var strings = _strings.Items.Select(item => data.Copy(item.Offset, item.Length)).ToList();
        int registry = 391 + strings.Count;
        strings.Add(Encoding.ASCII.GetBytes("Adobe"));
        int ordering = 391 + strings.Count;
        strings.Add(Encoding.ASCII.GetBytes("Identity"));
        var stringIndex = CffData.IndexBytes(strings, context);
        var global = CffData.IndexBytes(_global.Items.Select(item => data.Copy(item.Offset, item.Length)).ToArray(), context);
        var charStrings = new List<byte[]>(_charStrings.Items.Length);
        for (int i = 0; i < _charStrings.Items.Length; i++)
        {
            context.Checkpoint();
            var item = _charStrings.Items[i];
            if (subset && !retained.Contains(i))
            {
                context.Consume(PdfResourceKind.ExpandedBytes, 1);
                charStrings.Add(new byte[] { 14 });
            }
            else
            {
                CffGlyphMachine.Outline? outline = CidKeyed ? null : Outline(i);
                charStrings.Add(outline?.Composite == true ? CffGlyphMachine.Encode(outline, Nominal(i), context) : data.Copy(item.Offset, item.Length));
            }
        }

        byte[] glyphIndex = CffData.IndexBytes(charStrings, context);
        context.Consume(PdfResourceKind.ExpandedBytes, 1 + (_charStrings.Items.Length - 1L) * 2 + _fdSelect.Length + 1);
        var charset = new byte[1 + (_charStrings.Items.Length - 1) * 2];
        for (int i = 1; i < _charStrings.Items.Length; i++)
            FontData.Put16(charset, 1 + (i - 1) * 2, i);
        var fdSelect = new byte[_fdSelect.Length + 1];
        Buffer.BlockCopy(_fdSelect, 0, fdSelect, 1, _fdSelect.Length);
        context.Consume(PdfResourceKind.ExpandedBytes, name.Length + 64);
        var names = CffData.IndexBytes(new[] { Encoding.ASCII.GetBytes(name) }, context);
        List<(int Operation, double[] Values)> Top(int charsetOffset, int charsOffset, int fdArrayOffset, int fdSelectOffset)
        {
            var values = new List<(int, double[])>();
            foreach (int key in new[]
            {
                0,
                1,
                1200,
                5,
                1207,
                1201,
                1202,
                1203,
                1204
            }

            )
                if (_top.TryGetValue(key, out var original))
                    values.Add((key, original));
            values.Add((1230, new double[] { registry, ordering, 0 }));
            values.Add((1234, new double[] { _charStrings.Items.Length }));
            values.Add((15, new double[] { charsetOffset }));
            values.Add((17, new double[] { charsOffset }));
            values.Add((1236, new double[] { fdArrayOffset }));
            values.Add((1237, new double[] { fdSelectOffset }));
            return values;
        }

        List<(int Operation, double[] Values)> Font(int index, int privateOffset)
        {
            var values = new List<(int, double[])>();
            if (_fonts[index].Dict.TryGetValue(1207, out var matrix))
                values.Add((1207, matrix));
            var original = _fonts[index].Dict.TryGetValue(18, out var originalPrivate) ? originalPrivate : _top.TryGetValue(18, out var topPrivate) ? topPrivate : System.Array.Empty<double>();
            if (original.Length != 0 && original.Length != 2)
                throw PdfWriterCore.Error(context, "Font.CffPrivate", "A font private dictionary lost its original extent.");
            values.Add((18, new double[] { original.Length == 0 ? 0 : original[0], privateOffset }));
            return values;
        }

        // Fixed-width DICT integer encodings make the relocation pass independent of offset magnitudes.
        var topTemplate = CffData.IndexBytes(new[] { CffData.DictBytes(Top(0, 0, 0, 0), context) }, context);
        var fontTemplate = CffData.IndexBytes(Enumerable.Range(0, _fonts.Count).Select(i => CffData.DictBytes(Font(i, 0), context)).ToArray(), context);
        int header = 4 + names.Length + topTemplate.Length + stringIndex.Length + global.Length, charsetOffset = header, charsOffset = checked(charsetOffset + charset.Length), selectOffset = checked(charsOffset + glyphIndex.Length), arrayOffset = checked(selectOffset + fdSelect.Length), privateStart = checked(arrayOffset + fontTemplate.Length);
        var fontDictionaries = new List<byte[]>();
        int position = privateStart;
        for (int i = 0; i < _fonts.Count; i++)
        {
            fontDictionaries.Add(CffData.DictBytes(Font(i, position), context));
            position = checked(position + _fonts[i].Private.Length);
        }

        byte[] top = CffData.IndexBytes(new[] { CffData.DictBytes(Top(charsetOffset, charsOffset, arrayOffset, selectOffset), context) }, context), fontIndex = CffData.IndexBytes(fontDictionaries, context);
        if (top.Length != topTemplate.Length || fontIndex.Length != fontTemplate.Length)
            throw new InvalidOperationException("CFF relocation changed fixed-width structure lengths.");
        context.Consume(PdfResourceKind.ExpandedBytes, position);
        var result = new byte[position];
        result[0] = 1;
        result[1] = 0;
        result[2] = 4;
        result[3] = 4;
        position = 4;
        foreach (var block in new[]
        {
            names,
            top,
            stringIndex,
            global,
            charset,
            glyphIndex,
            fdSelect,
            fontIndex
        }.Concat(_fonts.Select(font => font.Private)))
        {
            context.Checkpoint();
            Buffer.BlockCopy(block, 0, result, position, block.Length);
            position += block.Length;
        }

        return result;
        Dictionary<int, double[]> Private(int glyph)
        {
            var font = _fonts[_fdSelect[glyph]];
            var declaration = font.Dict.TryGetValue(18, out var fd) ? fd : _top.TryGetValue(18, out var topPrivate) ? topPrivate : System.Array.Empty<double>();
            return font.Private.Length == 0 ? new Dictionary<int, double[]>() : new CffData(font.Private, context).Dictionary(0, (int)declaration[0]);
        }

        double Nominal(int glyph)
        {
            var dictionary = Private(glyph);
            return dictionary.TryGetValue(21, out var value) && value.Length == 1 ? value[0] : 0;
        }

        CffGlyphMachine.Outline Outline(int glyph)
        {
            context.Checkpoint();
            using var scope = context.EnterScope(PdfResourceKind.Depth);
            if (outlines.TryGetValue(glyph, out var ready))
                return ready;
            if (!activeGlyphs.Add(glyph))
                throw PdfWriterCore.Error(context, "Font.CffCompositeCycle", "Legacy composite glyph accents contain a cycle.");
            try
            {
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 256);
                var font = _fonts[_fdSelect[glyph]];
                var dictionary = Private(glyph);
                double defaultWidth = dictionary.TryGetValue(20, out var dw) && dw.Length == 1 ? dw[0] : 0, nominal = dictionary.TryGetValue(21, out var nw) && nw.Length == 1 ? nw[0] : 0;
                var privateData = new CffData(font.Private, context);
                int localOffset = privateData.Integer(dictionary, 19);
                var local = localOffset < 0 ? null : privateData.ReadIndex(localOffset);
                var machine = new CffGlyphMachine(defaultWidth, nominal, Subroutine, Accent, context);
                var item = _charStrings.Items[glyph];
                var outline = machine.Evaluate(_source, item.Offset, item.Length);
                outlines.Add(glyph, outline);
                return outline;
                (byte[] Bytes, int Offset, int Length) Subroutine(bool global, int encoded)
                {
                    var index = global ? _global : local;
                    if (index == null)
                        throw PdfWriterCore.Error(context, "Font.CffSubroutines", "The selected glyph has no local subroutine INDEX.");
                    int bias = index.Items.Length < 1240 ? 107 : index.Items.Length < 33900 ? 1131 : 32768;
                    long id = (long)encoded + bias;
                    if (id < 0 || id >= index.Items.Length)
                        throw PdfWriterCore.Error(context, "Font.CffSubroutines", "A Type 2 subroutine reference is outside its INDEX.");
                    var entry = index.Items[(int)id];
                    return (global ? _source : font.Private, entry.Offset, entry.Length);
                }

                CffGlyphMachine.Outline Accent(int code, int role)
                {
                    if (code < 0 || code > 255)
                        throw PdfWriterCore.Error(context, "Font.CffCompositeGlyph", "A composite accent requires a StandardEncoding byte.");
                    standardEncoding ??= FontResources.Text("StandardEncoding.txt", context).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    int component = System.Array.IndexOf(_glyphNames, standardEncoding[code]);
                    if (component < 0)
                        throw PdfWriterCore.Error(context, "Font.CffCompositeGlyph", "A legacy composite references an absent StandardEncoding glyph.");
                    return Outline(component);
                }
            }
            finally
            {
                activeGlyphs.Remove(glyph);
            }
        }
    }
}
