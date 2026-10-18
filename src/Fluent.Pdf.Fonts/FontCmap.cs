namespace Fluent.Pdf.Fonts;
// Original bounded OpenType cmap reader. A selected Unicode subtable never falls back to a machine encoding.
internal sealed class FontCmap
{
    private readonly byte[] _source;
    private readonly int _start, _length, _format, _glyphCount;
    internal FontCmap(FontData data, int offset, int length, int glyphCount)
    {
        _source = data.Bytes;
        _glyphCount = glyphCount;
        data.Range(offset, length);
        if (length < 4 || data.U16(offset) != 0)
            throw data.Error("Font.Cmap", "The cmap header is malformed.");
        int count = data.U16(offset + 2);
        if (count > (length - 4) / 8)
            throw data.Error("Font.Cmap", "The cmap record array is truncated.");
        data.Context.Consume(PdfResourceKind.Objects, count);
        int best = -1, start = 0, format = 0, extent = 0;
        for (int i = 0; i < count; i++)
        {
            int record = offset + 4 + i * 8, platform = data.U16(record), encoding = data.U16(record + 2);
            uint relative = data.U32(record + 4);
            if (relative > (uint)(length - 2))
                throw data.Error("Font.Cmap", "A cmap subtable offset is invalid.");
            int candidate = offset + (int)relative, kind = data.U16(candidate);
            bool unicode = platform == 0 || platform == 3 && (encoding == 1 || encoding == 10);
            if (!unicode || kind != 0 && kind != 4 && kind != 6 && kind != 10 && kind != 12 && kind != 13)
                continue;
            int score = (kind == 12 ? 100 : kind == 13 ? 90 : kind == 10 ? 80 : kind == 4 ? 70 : kind == 6 ? 60 : 50) + (platform == 0 ? 2 : 1);
            uint declared = kind >= 10 ? data.U32(candidate + 4) : (uint)data.U16(candidate + 2);
            if (declared > int.MaxValue || declared > length - relative)
                throw data.Error("Font.Cmap", "A Unicode cmap subtable exceeds its table.");
            if (score > best)
            {
                best = score;
                start = candidate;
                format = kind;
                extent = (int)declared;
            }
        }

        if (best < 0)
            throw data.Error("Font.UnicodeCmapRequired", "The font has no supported explicit Unicode cmap.");
        _start = start;
        _format = format;
        _length = extent;
        Validate(data);
    }

    private int U16(int offset) => _source[offset] * 256 + _source[offset + 1];
    private uint U32(int offset) => (uint)(_source[offset] << 24 | _source[offset + 1] << 16 | _source[offset + 2] << 8 | _source[offset + 3]);
    private void Validate(FontData data)
    {
        int at = _start;
        data.Range(at, _length);
        void Require(bool condition)
        {
            if (!condition)
                throw data.Error("Font.Cmap", "The selected Unicode cmap is malformed or maps outside the glyph range.");
        }

        void Glyph(uint glyph)
        {
            Require(glyph < (uint)_glyphCount);
        }

        if (_format == 0)
        {
            Require(_length >= 262);
            for (int i = 0; i < 256; i++)
                Glyph(_source[at + 6 + i]);
        }
        else if (_format == 6 || _format == 10)
        {
            int header = _format == 6 ? 10 : 20;
            Require(_length >= header);
            uint first = _format == 6 ? (uint)U16(at + 6) : U32(at + 12), count = _format == 6 ? (uint)U16(at + 8) : U32(at + 16);
            Require(count <= (uint)(_length - header) / 2 && first <= 0x10ffff && count <= 0x110000 - first);
            data.Context.Consume(PdfResourceKind.Objects, count);
            for (uint i = 0; i < count; i++)
            {
                data.Context.Checkpoint();
                Glyph((uint)U16(at + header + (int)i * 2));
            }
        }
        else if (_format == 12 || _format == 13)
        {
            Require(_length >= 16);
            uint count = U32(at + 12);
            Require(count <= (uint)(_length - 16) / 12);
            data.Context.Consume(PdfResourceKind.Objects, count);
            uint previous = 0;
            for (uint i = 0; i < count; i++)
            {
                data.Context.Checkpoint();
                int record = at + 16 + (int)i * 12;
                uint first = U32(record), last = U32(record + 4), glyph = U32(record + 8);
                Require(first <= last && last <= 0x10ffff && (i == 0 || first > previous));
                Glyph(glyph);
                if (_format == 12)
                    Require(last - first < (uint)_glyphCount - glyph);
                previous = last;
            }
        }
        else
        {
            Require(_length >= 16);
            int doubled = U16(at + 6);
            Require(doubled > 0 && doubled % 2 == 0);
            int count = doubled / 2;
            Require(count <= (_length - 16) / 8);
            data.Context.Consume(PdfResourceKind.Objects, count);
            int ends = at + 14, starts = ends + count * 2 + 2, deltas = starts + count * 2, ranges = deltas + count * 2;
            int previous = -1;
            for (int i = 0; i < count; i++)
            {
                data.Context.Checkpoint();
                int first = U16(starts + i * 2), last = U16(ends + i * 2);
                Require(first <= last && first > previous);
                previous = last;
                int range = U16(ranges + i * 2);
                if (range != 0)
                    Require(range % 2 == 0 && (long)(ranges + i * 2) + range + (last - first) * 2 + 2 <= at + _length);
                for (int scalar = first; scalar <= last; scalar++)
                {
                    if ((scalar & 255) == 0)
                        data.Context.Checkpoint();
                    Glyph((uint)Map(scalar));
                }
            }

            Require(previous == 65535);
        }
    }

    internal int Map(int scalar)
    {
        int at = _start;
        if (_format == 0)
            return scalar < 256 ? _source[at + 6 + scalar] : 0;
        if (_format == 6 || _format == 10)
        {
            int header = _format == 6 ? 10 : 20;
            uint first = _format == 6 ? (uint)U16(at + 6) : U32(at + 12), count = _format == 6 ? (uint)U16(at + 8) : U32(at + 16);
            return scalar >= first && (uint)scalar - first < count ? U16(at + header + (int)((uint)scalar - first) * 2) : 0;
        }

        int low = 0, high = _format == 4 ? U16(at + 6) / 2 - 1 : (int)U32(at + 12) - 1;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            if (_format == 4)
            {
                int count = U16(at + 6) / 2, ends = at + 14, starts = ends + count * 2 + 2, deltas = starts + count * 2, ranges = deltas + count * 2;
                int first = U16(starts + middle * 2), last = U16(ends + middle * 2);
                if (scalar < first)
                {
                    high = middle - 1;
                    continue;
                }

                if (scalar > last)
                {
                    low = middle + 1;
                    continue;
                }

                int range = U16(ranges + middle * 2), delta = U16(deltas + middle * 2);
                if (range == 0)
                    return (scalar + delta) & 65535;
                int glyph = U16(ranges + middle * 2 + range + (scalar - first) * 2);
                return glyph == 0 ? 0 : (glyph + delta) & 65535;
            }

            int record = at + 16 + middle * 12;
            uint begin = U32(record), end = U32(record + 4);
            if (scalar < begin)
                high = middle - 1;
            else if (scalar > end)
                low = middle + 1;
            else
                return (int)(_format == 13 ? U32(record + 8) : U32(record + 8) + (uint)scalar - begin);
        }

        return 0;
    }

    internal byte[] Subset(ISet<int> glyphs, PdfOperationContext context)
    {
        var groups = new List<(int First, int Last, int Glyph)>();
        int first = -1, last = -1, initial = 0, lastGlyph = -1;
        for (int scalar = 0; scalar <= 0x10ffff; scalar++)
        {
            if ((scalar & 255) == 0)
                context.Checkpoint();
            if (scalar >= 0xd800 && scalar <= 0xdfff)
                continue;
            int glyph = Map(scalar);
            if (glyph == 0 || !glyphs.Contains(glyph))
                continue;
            if (first >= 0 && scalar == last + 1 && glyph == lastGlyph + 1)
            {
                last = scalar;
                lastGlyph = glyph;
                continue;
            }

            Add();
            first = last = scalar;
            initial = lastGlyph = glyph;
        }

        Add();
        int length = checked(28 + groups.Count * 12);
        context.Consume(PdfResourceKind.ExpandedBytes, length);
        var result = new byte[length];
        FontData.Put16(result, 2, 1);
        FontData.Put16(result, 4, 3);
        FontData.Put16(result, 6, 10);
        FontData.Put32(result, 8, 12);
        FontData.Put16(result, 12, 12);
        FontData.Put32(result, 16, (uint)(length - 12));
        FontData.Put32(result, 24, (uint)groups.Count);
        for (int i = 0; i < groups.Count; i++)
        {
            int record = 28 + i * 12;
            FontData.Put32(result, record, (uint)groups[i].First);
            FontData.Put32(result, record + 4, (uint)groups[i].Last);
            FontData.Put32(result, record + 8, (uint)groups[i].Glyph);
        }

        return result;
        void Add()
        {
            if (first < 0)
                return;
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 32);
            groups.Add((first, last, initial));
        }
    }
}
