using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Fluent.Pdf.Fonts;
internal sealed class SfntFont : IPdfFontProgram, IPdfVerticalFontProgram, IPdfFontShapingSource
{
    public int ShapingFaceIndex => _faceIndex;
    public void CopyShapingSource(System.IO.Stream output, PdfOperationContext context) { context.Checkpoint(); output.Write(_source,0,_source.Length); }
    private readonly byte[] _source;
    private readonly Dictionary<string, (int Offset, int Length)> _tables;
    private readonly FontCmap _cmap;
    private readonly int _horizontalMetrics, _verticalMetrics, _faceIndex;
    private readonly int[]? _locations;
    private readonly uint _flavor;
    private readonly CffFont? _cff;
    public PdfFontInfo Info { get; }

    internal SfntFont(byte[] source, int faceIndex, PdfOperationContext context)
    {
        _source = source;
        _faceIndex = faceIndex;
        var data = new FontData(source, context);
        int start = 0;
        if (data.Tag(0) == "ttcf")
        {
            uint version = data.U32(4), count = data.U32(8);
            if (version != 0x10000 && version != 0x20000 || count == 0 || count > int.MaxValue || faceIndex >= count)
                throw data.Error("Font.CollectionFace", "The font collection or selected face is invalid.");
            data.Range(12, count * 4L);
            start = Checked(data.U32(12 + faceIndex * 4), data);
        }
        else if (faceIndex != 0)
            throw data.Error("Font.CollectionFace", "A standalone font has only face zero.");
        _flavor = data.U32(start);
        if (_flavor != 0x10000 && _flavor != 0x4f54544f && _flavor != 0x74727565)
            throw data.Error("Font.SfntFormat", "The SFNT outline flavor is unsupported.");
        int countTables = data.U16(start + 4);
        if (countTables == 0)
            throw data.Error("Font.SfntDirectory", "A font must contain tables.");
        data.Range(start + 12, countTables * 16L);
        context.Consume(PdfResourceKind.Objects, countTables);
        context.Consume(PdfResourceKind.ExpandedBytes, countTables * 128L + 512);
        _tables = new Dictionary<string, (int, int)>(countTables, StringComparer.Ordinal);
        for (int i = 0; i < countTables; i++)
        {
            int record = start + 12 + i * 16;
            string tag = data.Tag(record);
            int offset = Checked(data.U32(record + 8), data), length = Checked(data.U32(record + 12), data);
            data.Range(offset, length);
            if (_tables.ContainsKey(tag) || tag.Any(c => c < 32 || c > 126))
                throw data.Error("Font.SfntDirectory", "The font table directory repeats a tag or uses an invalid tag.");
            _tables.Add(tag, (offset, length));
        }

        var head = Table("head", 54, data);
        if (data.U32(head.Offset + 12) != 0x5f0f3cf5)
            throw data.Error("Font.Head", "The font head magic is invalid.");
        int units = data.U16(head.Offset + 18);
        if (units < 16 || units > 16384)
            throw data.Error("Font.Head", "Units per em is outside the supported OpenType range.");
        var maxp = Table("maxp", 6, data);
        int glyphs = data.U16(maxp.Offset + 4);
        if (glyphs == 0)
            throw data.Error("Font.GlyphCount", "A font must contain a missing glyph.");
        var horizontal = Table("hhea", 36, data);
        _horizontalMetrics = data.U16(horizontal.Offset + 34);
        if (_horizontalMetrics == 0 || _horizontalMetrics > glyphs)
            throw data.Error("Font.Metrics", "The horizontal metric count is invalid.");
        Table("hmtx", _horizontalMetrics * 4 + (glyphs - _horizontalMetrics) * 2, data);
        _verticalMetrics = 0;
        if (_tables.TryGetValue("vhea", out var vertical))
        {
            if (vertical.Length < 36)
                throw data.Error("Font.Metrics", "The vertical metrics header is truncated.");
            _verticalMetrics = data.U16(vertical.Offset + 34);
            if (_verticalMetrics == 0 || _verticalMetrics > glyphs)
                throw data.Error("Font.Metrics", "The vertical metric count is invalid.");
            Table("vmtx", _verticalMetrics * 4 + (glyphs - _verticalMetrics) * 2, data);
        }

        int ascender = data.I16(horizontal.Offset + 4), descender = data.I16(horizontal.Offset + 6), capHeight = ascender;
        PdfFontEmbeddingRights rights = PdfFontEmbeddingRights.Installable;
        if (_tables.TryGetValue("OS/2", out var os2))
        {
            if (os2.Length < 10)
                throw data.Error("Font.EmbeddingRights", "The OS/2 embedding-rights field is truncated.");
            int flags = data.U16(os2.Offset + 8);
            if ((flags & ~782) != 0)
                throw data.Error("Font.EmbeddingRights", "Unknown embedding restriction flags require an explicit font implementation.");
            rights = (PdfFontEmbeddingRights)flags;
            if (os2.Length >= 72)
            {
                ascender = data.I16(os2.Offset + 68);
                descender = data.I16(os2.Offset + 70);
            }

            if (data.U16(os2.Offset) >= 2 && os2.Length >= 90)
                capHeight = data.I16(os2.Offset + 88);
        }

        bool fixedPitch = false;
        double italic = 0;
        if (_tables.TryGetValue("post", out var post))
        {
            if (post.Length < 16)
                throw data.Error("Font.Metrics", "The post metrics table is truncated.");
            italic = data.I32(post.Offset + 4) / 65536.0;
            fixedPitch = data.U32(post.Offset + 12) != 0;
        }

        var metrics = new PdfFontMetrics(units, ascender, descender, capHeight, data.I16(head.Offset + 36), data.I16(head.Offset + 38), data.I16(head.Offset + 40), data.I16(head.Offset + 42), fixedPitch, italic);
        var cmap = Table("cmap", 4, data);
        _cmap = new FontCmap(data, cmap.Offset, cmap.Length, glyphs);
        string name = PostScriptName(data);
        PdfFontFormat format;
        if (_tables.ContainsKey("glyf"))
        {
            format = PdfFontFormat.TrueType;
            int kind = data.I16(head.Offset + 50);
            if (kind != 0 && kind != 1)
                throw data.Error("Font.Loca", "The glyph-location representation is invalid.");
            var loca = Table("loca", checked((glyphs + 1) * (kind == 0 ? 2 : 4)), data);
            var glyf = Table("glyf", 0, data);
            context.Consume(PdfResourceKind.ExpandedBytes, (glyphs + 1L) * 4);
            _locations = new int[glyphs + 1];
            int previous = 0;
            for (int i = 0; i <= glyphs; i++)
            {
                int position = kind == 0 ? data.U16(loca.Offset + i * 2) * 2 : Checked(data.U32(loca.Offset + i * 4), data);
                if (position < previous || position > glyf.Length)
                    throw data.Error("Font.Loca", "Glyph offsets must be monotonic within the glyf table.");
                _locations[i] = position;
                previous = position;
            }

            for (int i = 0; i < glyphs; i++)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                GlyphDependencies(i, data, null);
            }
        }
        else if (_tables.ContainsKey("CFF "))
            format = PdfFontFormat.Cff;
        else if (_tables.ContainsKey("CFF2"))
            throw data.Error("Font.VariationProviderRequired", "CFF2 variable outlines require an explicit instance/variation provider.");
        else
            throw data.Error("Font.OutlineFormat", "The font contains no supported outline program.");
        if (_tables.ContainsKey("fvar") || _tables.ContainsKey("gvar"))
            throw data.Error("Font.VariationProviderRequired", "Variable fonts require explicit axis instancing before embedding.");
        string digest = FontData.Hash(source, context);
        if (faceIndex != 0)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, 256);
            digest = FontData.Hash(Encoding.ASCII.GetBytes(digest + ":" + faceIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        Info = new PdfFontInfo(name, format, glyphs, metrics, digest, rights);
        if (format == PdfFontFormat.Cff)
        {
            var cff = Table("CFF ", 4, data);
            _cff = new CffFont(data.Copy(cff.Offset, cff.Length), glyphs, context);
        }
    }

    private static int Checked(uint value, FontData data)
    {
        if (value > int.MaxValue)
            throw data.Error("Font.ProgramRange", "A font offset exceeds the managed program range.");
        return (int)value;
    }

    private (int Offset, int Length) Table(string tag, int minimum, FontData data)
    {
        if (!_tables.TryGetValue(tag, out var table) || table.Length < minimum)
            throw data.Error("Font.RequiredTable", "A required font table is absent or truncated: " + tag + ".");
        return table;
    }

    private string PostScriptName(FontData data)
    {
        var name = Table("name", 6, data);
        int count = data.U16(name.Offset + 2), storage = data.U16(name.Offset + 4);
        if (count > (name.Length - 6) / 12 || storage > name.Length)
            throw data.Error("Font.Name", "The font name directory is malformed.");
        string? selected = null;
        int score = -1;
        data.Context.Consume(PdfResourceKind.Objects, count);
        for (int i = 0; i < count; i++)
        {
            int record = name.Offset + 6 + i * 12, platform = data.U16(record), encoding = data.U16(record + 2), language = data.U16(record + 4), id = data.U16(record + 6), length = data.U16(record + 8), offset = data.U16(record + 10);
            if ((long)storage + offset + length > name.Length)
                throw data.Error("Font.Name", "A font name record exceeds its table.");
            if (id != 6 || platform != 0 && platform != 1 && platform != 3)
                continue;
            int rank = (platform == 3 && encoding == 1 ? 30 : platform == 0 ? 20 : platform == 1 ? 10 : 0) + (language == 0x409 ? 1 : 0);
            if (rank <= score)
                continue;
            data.Context.Consume(PdfResourceKind.ExpandedBytes, length * 2L + 64);
            string value;
            if (platform == 0 || platform == 3)
            {
                if (length % 2 != 0)
                    throw data.Error("Font.Name", "A Unicode font name has an odd byte extent.");
                value = Encoding.BigEndianUnicode.GetString(_source, name.Offset + storage + offset, length);
            }
            else
                value = FontData.ByteText(_source, name.Offset + storage + offset, length, data.Context);
            if (value.Length == 0 || value.Length > 127 || value.Any(c => c < 33 || c > 126 || "()<>[]{}/%#".IndexOf(c) >= 0))
                throw data.Error("Font.Name", "The PostScript name is not a regular ASCII font name.");
            selected = value;
            score = rank;
        }

        return selected ?? throw data.Error("Font.Name", "A deterministic PostScript name is required.");
    }

    public int MapGlyph(int unicodeScalar, PdfOperationContext context)
    {
        FontData.Scalar(unicodeScalar);
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        return _cmap.Map(unicodeScalar);
    }

    public double GetAdvance(int glyph, PdfOperationContext context) => Advance(glyph, false, context);
    public bool SupportsVerticalMetrics => _verticalMetrics > 0 && (_locations != null || _tables.ContainsKey("VORG"));

    public PdfFontVerticalMetrics GetVerticalMetrics(int glyph, PdfOperationContext context)
    {
        CheckGlyph(glyph, context);
        if (!SupportsVerticalMetrics)
            throw PdfWriterCore.Error(context, "Font.VerticalMetricsUnavailable", "The selected face has no complete vertical metrics/origins.");
        var data = new FontData(_source, context);
        double y;
        if (_locations != null)
        {
            var metrics = _tables["vmtx"];
            int bearing = glyph < _verticalMetrics ? data.I16(metrics.Offset + glyph * 4 + 2) : data.I16(metrics.Offset + _verticalMetrics * 4 + (glyph - _verticalMetrics) * 2);
            int maximum = _locations[glyph] == _locations[glyph + 1] ? 0 : data.I16(_tables["glyf"].Offset + _locations[glyph] + 8);
            y = maximum + bearing;
        }
        else
        {
            var vorg = Table("VORG", 8, data);
            if (data.U16(vorg.Offset) != 1 || data.U16(vorg.Offset + 2) != 0)
                throw data.Error("Font.VerticalMetrics", "The VORG version is unsupported.");
            int count = data.U16(vorg.Offset + 6);
            if (count > (vorg.Length - 8) / 4)
                throw data.Error("Font.VerticalMetrics", "The VORG records are truncated.");
            y = data.I16(vorg.Offset + 4);
            int previous = -1;
            for (int i = 0; i < count; i++)
            {
                context.Checkpoint();
                int record = vorg.Offset + 8 + i * 4, index = data.U16(record);
                if (index <= previous || index >= Info.GlyphCount)
                    throw data.Error("Font.VerticalMetrics", "VORG identities must increase within the glyph range.");
                if (index == glyph)
                    y = data.I16(record + 2);
                previous = index;
            }
        }

        return new PdfFontVerticalMetrics(Advance(glyph, true, context), Advance(glyph, false, context) / 2.0, y);
    }

    private int Advance(int glyph, bool vertical, PdfOperationContext context)
    {
        CheckGlyph(glyph, context);
        if (vertical && _verticalMetrics == 0)
            return Info.Metrics.UnitsPerEm;
        int metrics = vertical ? _verticalMetrics : _horizontalMetrics;
        var table = _tables[vertical ? "vmtx" : "hmtx"];
        int at = table.Offset + Math.Min(glyph, metrics - 1) * 4;
        return _source[at] * 256 + _source[at + 1];
    }

    public string GetGlyphName(int glyph, PdfOperationContext context)
    {
        CheckGlyph(glyph, context);
        return _cff?.GlyphName(glyph) ?? "gid" + glyph.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void CheckGlyph(int glyph, PdfOperationContext context)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        if (glyph < 0 || glyph >= Info.GlyphCount)
            throw new ArgumentOutOfRangeException(nameof(glyph));
    }

    private void GlyphDependencies(int glyph, FontData data, Action<int>? component)
    {
        int begin = _locations![glyph], end = _locations[glyph + 1];
        if (begin == end)
            return;
        var table = _tables["glyf"];
        int at = table.Offset + begin, limit = table.Offset + end;
        void Extent(int count)
        {
            if (count < 0 || at > limit - count)
                throw data.Error("Font.Glyph", "A glyph outline is truncated.");
        }

        Extent(10);
        int contours = data.I16(at);
        at += 10;
        if (contours >= 0)
        {
            Extent(contours * 2);
            int previous = -1, points = 0;
            for (int i = 0; i < contours; i++)
            {
                int last = data.U16(at);
                if (last <= previous)
                    throw data.Error("Font.Glyph", "Simple glyph contour endpoints must increase.");
                previous = last;
                points = last + 1;
                at += 2;
            }

            Extent(2);
            int instructions = data.U16(at);
            at += 2;
            Extent(instructions);
            at += instructions;
            int seen = 0, x = 0, y = 0;
            while (seen < points)
            {
                data.Context.Checkpoint();
                Extent(1);
                int flags = data.U8(at++), repeat = 1;
                if ((flags & 8) != 0)
                {
                    Extent(1);
                    repeat += data.U8(at++);
                }

                if (repeat > points - seen)
                    throw data.Error("Font.Glyph", "A glyph flag repeat exceeds the point count.");
                seen += repeat;
                x += repeat * ((flags & 2) != 0 ? 1 : (flags & 16) != 0 ? 0 : 2);
                y += repeat * ((flags & 4) != 0 ? 1 : (flags & 32) != 0 ? 0 : 2);
            }

            Extent(x + y);
            return;
        }

        if (contours != -1)
            throw data.Error("Font.Glyph", "An invalid negative contour count was encountered.");
        int compositeFlags;
        do
        {
            data.Context.Checkpoint();
            Extent(4);
            compositeFlags = data.U16(at);
            int child = data.U16(at + 2);
            at += 4;
            if (child >= _locations.Length - 1)
                throw data.Error("Font.Glyph", "A composite references a nonexistent glyph.");
            component?.Invoke(child);
            int count = (compositeFlags & 1) != 0 ? 4 : 2;
            int transform = ((compositeFlags & 8) != 0 ? 1 : 0) + ((compositeFlags & 64) != 0 ? 1 : 0) + ((compositeFlags & 128) != 0 ? 1 : 0);
            if (transform > 1)
                throw data.Error("Font.Glyph", "A composite declares incompatible transforms.");
            count += (compositeFlags & 8) != 0 ? 2 : (compositeFlags & 64) != 0 ? 4 : (compositeFlags & 128) != 0 ? 8 : 0;
            Extent(count);
            at += count;
        }
        while ((compositeFlags & 32) != 0);
        if ((compositeFlags & 256) != 0)
        {
            Extent(2);
            int count = data.U16(at);
            at += 2;
            Extent(count);
        }
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
        context.Consume(PdfResourceKind.ExpandedBytes, Info.GlyphCount * 16L + _tables.Count * 128L + 512);
        var retained = new HashSet<int>
        {
            0
        };
        foreach (int glyph in glyphs)
        {
            CheckGlyph(glyph, context);
            retained.Add(glyph);
        }

        var data = new FontData(_source, context);
        var visiting = new HashSet<int>();
        var visited = new HashSet<int>();
        if (_locations != null)
            foreach (int glyph in retained.ToArray())
                Visit(glyph);
        string name = subset ? SubsetName(retained, context) : _cff == null ? Info.PostScriptName : "FluentEmbedded-" + Info.Sha256.Substring(0, 16);
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var pair in _tables)
        {
            context.Checkpoint();
            if (pair.Key == "DSIG")
                continue;
            tables.Add(pair.Key, data.Copy(pair.Value.Offset, pair.Value.Length));
        }

        if (subset && Info.Format == PdfFontFormat.TrueType)
        {
            long glyfLength = 0;
            foreach (int glyph in retained)
            {
                context.Checkpoint();
                glyfLength = checked(glyfLength + ((_locations![glyph + 1] - _locations[glyph] + 3L) & ~3L));
            }

            if (glyfLength > int.MaxValue)
                throw PdfWriterCore.Error(context, "Font.ProgramRange", "The subset glyph extent exceeds the managed range.");
            context.Consume(PdfResourceKind.ExpandedBytes, glyfLength);
            using var glyf = new MemoryStream((int)glyfLength);
            context.Consume(PdfResourceKind.ExpandedBytes, (Info.GlyphCount + 1L) * 4);
            var loca = new byte[(Info.GlyphCount + 1) * 4];
            var original = _tables["glyf"];
            for (int i = 0; i < Info.GlyphCount; i++)
            {
                context.Checkpoint();
                FontData.Put32(loca, i * 4, (uint)glyf.Length);
                if (!retained.Contains(i))
                    continue;
                int length = _locations![i + 1] - _locations[i];
                glyf.Write(_source, original.Offset + _locations[i], length);
                while (glyf.Length % 4 != 0)
                    glyf.WriteByte(0);
            }

            FontData.Put32(loca, Info.GlyphCount * 4, (uint)glyf.Length);
            context.Consume(PdfResourceKind.ExpandedBytes, glyf.Length);
            tables["glyf"] = glyf.ToArray();
            tables["loca"] = loca;
            FontData.Put16(tables["head"], 50, 1);
        }

        if (_cff != null)
            tables["CFF "] = _cff.Write(name, retained, subset, context);
        if (subset)
            tables["cmap"] = _cmap.Subset(retained, context);
        if (subset || _cff != null)
            tables["name"] = Rename(tables["name"], name, context);
        FontData.Put32(tables["head"], 8, 0);
        byte[] program = Assemble(tables, _flavor, context);
        output.Write(program, 0, program.Length);
        context.Checkpoint();
        return new PdfFontProgramWriteResult(name, fontFileSubtype: _cff == null ? null : "OpenType", length1: program.Length, cidKeyed: _cff != null);
        void Visit(int glyph)
        {
            context.Checkpoint();
            using var scope = context.EnterScope(PdfResourceKind.Depth);
            if (visited.Contains(glyph))
                return;
            if (!visiting.Add(glyph))
                throw PdfWriterCore.Error(context, "Font.CompositeCycle", "The font has a cyclic composite glyph.");
            GlyphDependencies(glyph, data, child =>
            {
                retained.Add(child);
                Visit(child);
            });
            visiting.Remove(glyph);
            visited.Add(glyph);
        }
    }

    private string SubsetName(HashSet<int> retained, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, retained.Count * 4L + 512);
        var ids = retained.OrderBy(x => x).ToArray();
        using var hash = SHA256.Create();
        byte[] identity = Encoding.ASCII.GetBytes(Info.Sha256 + ":" + _faceIndex);
        hash.TransformBlock(identity, 0, identity.Length, identity, 0);
        var bytes = new byte[ids.Length * 2];
        for (int i = 0; i < ids.Length; i++)
            FontData.Put16(bytes, i * 2, ids[i]);
        hash.TransformFinalBlock(bytes, 0, bytes.Length);
        return new string (hash.Hash!.Take(6).Select(x => (char)('A' + x % 26)).ToArray()) + "+FluentSubset";
    }

    private static byte[] Rename(byte[] original, string name, PdfOperationContext context)
    {
        var data = new FontData(original, context);
        int count = data.U16(2), storage = data.U16(4);
        var records = new List<(int Platform, int Encoding, int Language, int Id, byte[] Bytes)>();
        foreach (int id in new[]
        {
            0,
            7,
            8,
            9,
            13,
            14
        }

        )
            for (int i = 0; i < count; i++)
            {
                int r = 6 + i * 12;
                if (data.U16(r + 6) != id)
                    continue;
                int length = data.U16(r + 8), offset = data.U16(r + 10);
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 64);
                records.Add((data.U16(r), data.U16(r + 2), data.U16(r + 4), id, data.Copy(storage + offset, length)));
            }

        foreach (int id in new[]
        {
            1,
            2,
            3,
            4,
            6,
            16,
            17
        }

        )
        {
            string text = id == 2 || id == 17 ? "Regular" : name.Replace('+', '-');
            if (id == 6)
                text = name;
            context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 2L + 64);
            records.Add((3, 1, 0x409, id, Encoding.BigEndianUnicode.GetBytes(text)));
        }

        records.Sort((a, b) => a.Platform != b.Platform ? a.Platform.CompareTo(b.Platform) : a.Encoding != b.Encoding ? a.Encoding.CompareTo(b.Encoding) : a.Language != b.Language ? a.Language.CompareTo(b.Language) : a.Id.CompareTo(b.Id));
        int header = checked(6 + records.Count * 12), lengthTotal = checked(header + records.Sum(x => x.Bytes.Length));
        if (header > 65535 || lengthTotal - header > 65535)
            throw PdfWriterCore.Error(context, "Font.NameRange", "Renamed font records exceed the SFNT name-table range.");
        context.Consume(PdfResourceKind.ExpandedBytes, lengthTotal);
        var result = new byte[lengthTotal];
        FontData.Put16(result, 2, records.Count);
        FontData.Put16(result, 4, header);
        int position = header;
        for (int i = 0; i < records.Count; i++)
        {
            var record = records[i];
            int at = 6 + i * 12;
            FontData.Put16(result, at, record.Platform);
            FontData.Put16(result, at + 2, record.Encoding);
            FontData.Put16(result, at + 4, record.Language);
            FontData.Put16(result, at + 6, record.Id);
            FontData.Put16(result, at + 8, record.Bytes.Length);
            FontData.Put16(result, at + 10, position - header);
            Buffer.BlockCopy(record.Bytes, 0, result, position, record.Bytes.Length);
            position += record.Bytes.Length;
        }

        return result;
    }

    internal static byte[] Assemble(SortedDictionary<string, byte[]> tables, uint flavor, PdfOperationContext context)
    {
        long extent = 12 + tables.Count * 16L;
        foreach (var table in tables.Values)
            extent = checked(extent + ((table.LongLength + 3) & ~3L));
        if (extent > int.MaxValue)
            throw PdfWriterCore.Error(context, "Font.ProgramRange", "The assembled font exceeds the managed program range.");
        context.Consume(PdfResourceKind.ExpandedBytes, extent);
        var result = new byte[(int)extent];
        FontData.Put32(result, 0, flavor);
        FontData.Put16(result, 4, tables.Count);
        int power = 1, selector = 0;
        while (power * 2 <= tables.Count)
        {
            power *= 2;
            selector++;
        }

        FontData.Put16(result, 6, power * 16);
        FontData.Put16(result, 8, selector);
        FontData.Put16(result, 10, tables.Count * 16 - power * 16);
        int index = 0, offset = 12 + tables.Count * 16, head = 0;
        foreach (var pair in tables)
        {
            context.Checkpoint();
            int record = 12 + index++ * 16;
            for (int i = 0; i < 4; i++)
                result[record + i] = (byte)pair.Key[i];
            FontData.Put32(result, record + 4, FontData.Checksum(pair.Value));
            FontData.Put32(result, record + 8, (uint)offset);
            FontData.Put32(result, record + 12, (uint)pair.Value.Length);
            Buffer.BlockCopy(pair.Value, 0, result, offset, pair.Value.Length);
            if (pair.Key == "head")
                head = offset;
            offset += (pair.Value.Length + 3) & ~3;
        }

        if (head != 0)
            FontData.Put32(result, head + 8, unchecked(0xb1b0afba - FontData.Checksum(result)));
        return result;
    }
}
