using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>One actual glyph and its exact logical Unicode text, including multi-scalar ligatures.</summary>
public sealed class PdfGlyphMapping
{
    /// <summary>Captures a glyph identity and a well-formed Unicode sequence; an empty sequence explicitly carries no logical text.</summary>
    public PdfGlyphMapping(int glyphIndex, string unicodeText)
    {
        if (glyphIndex < 0 || glyphIndex > 65535)
            throw new ArgumentOutOfRangeException(nameof(glyphIndex));
        if (unicodeText == null)
            throw new ArgumentNullException(nameof(unicodeText));
        if (unicodeText.Length > 512)
            throw new ArgumentException("A glyph mapping is limited to 512 UTF-16 units.", nameof(unicodeText));
        for (int i = 0; i < unicodeText.Length; i++)
            if (char.IsSurrogate(unicodeText[i]))
            {
                if (!char.IsHighSurrogate(unicodeText[i]) || i + 1 >= unicodeText.Length || !char.IsLowSurrogate(unicodeText[++i]))
                    throw new ArgumentException("Unicode text contains an unpaired surrogate.", nameof(unicodeText));
            }

        GlyphIndex = glyphIndex;
        UnicodeText = unicodeText;
    }

    /// <summary>Gets the actual glyph identity.</summary>
    public int GlyphIndex { get; }
    /// <summary>Gets exact logical Unicode text for ToUnicode mapping.</summary>
    public string UnicodeText { get; }
}

/// <summary>An immutable eagerly embedded font resource and its encoded show-text operand.</summary>
public sealed class PdfFontResource
{
    private readonly IReadOnlyList<Entry> _entries;
    private readonly PdfValue _program, _toUnicode, _encoding;
    private readonly PdfFontProgramWriteResult _written;
    private readonly bool _simple;
    private readonly bool _identityCff;
    internal PdfValue EmbeddedProgram => _program;
    internal string EmittedName => _written.PostScriptName;
    internal string ResourceSubtype => Font.Info.Format == PdfFontFormat.Type3 ? "Type3" : _simple ? "Type1" : "Type0";
    internal PdfValue ToUnicodeStream => _toUnicode;

    private readonly struct Entry
    {
        internal Entry(PdfGlyphMapping mapping, double advance, string name, PdfFontVerticalMetrics? vertical)
        {
            Mapping = mapping;
            Advance = advance;
            Name = name;
            Vertical = vertical;
        }

        internal PdfGlyphMapping Mapping { get; }
        internal double Advance { get; }
        internal string Name { get; }
        internal PdfFontVerticalMetrics? Vertical { get; }
    }

    private PdfFontResource(PdfFont font, List<Entry> entries, PdfValue encoded, PdfValue program, PdfFontProgramWriteResult written, PdfValue unicode, PdfValue encoding, bool simple, bool vertical, bool identityCff)
    {
        Font = font;
        _entries = entries.AsReadOnly();
        EncodedText = encoded;
        _program = program;
        _written = written;
        _toUnicode = unicode;
        _encoding = encoding;
        _simple = simple;
        _identityCff = identityCff;
        Vertical = vertical;
        GlyphMappings = new ReadOnlyCollection<PdfGlyphMapping>(entries.Select(x => x.Mapping).ToArray());
    }

    /// <summary>Gets the exact selected font.</summary>
    public PdfFont Font { get; }
    /// <summary>Gets a direct immutable byte string ready for Tj/TJ operands, in the supplied mapping order.</summary>
    public PdfValue EncodedText { get; }
    /// <summary>Gets unique mappings in first-use order; EncodedText carries the exact selected byte codes.</summary>
    public IReadOnlyList<PdfGlyphMapping> GlyphMappings { get; }
    /// <summary>Gets whether vertical writing was explicitly selected with exact vertical metrics.</summary>
    public bool Vertical { get; }
    /// <summary>Gets the minimum required PDF version.</summary>
    public string MinimumVersion => Font.Program is PdfType3FontProgram type3 ? type3.MinimumVersion : _written.FontFileSubtype == "OpenType" ? "1.6" : "1.2";
    /// <summary>Gets the successful eager font-resource creation report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;

    /// <summary>Maps unshaped Unicode scalars through one exact font. Complex shaping belongs to the text-shaping service.</summary>
    public static PdfFontResource CreateText(PdfFont font, string text, bool subset = true, bool vertical = false, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (font == null)
            throw new ArgumentNullException(nameof(font));
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        PdfFontResource? resource = null;
        var report = PdfFont.Run("CreateTextFontResource", font.ProviderId, policy, cancellationToken, context => resource = CreateCore(font, Mappings(context), subset, vertical, context));
        resource!.OperationReport = report;
        return resource;
        IEnumerable<PdfGlyphMapping> Mappings(PdfOperationContext context)
        {
            for (int i = 0; i < text.Length; i++)
            {
                context.Checkpoint();
                int begin = i, scalar = text[i];
                if (char.IsSurrogate(text[i]))
                {
                    if (!char.IsHighSurrogate(text[i]) || i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                        throw new ArgumentException("Text contains an unpaired surrogate.", nameof(text));
                    scalar = char.ConvertToUtf32(text[i], text[++i]);
                }

                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 128);
                yield return new PdfGlyphMapping(font.MapCore(scalar, context), text.Substring(begin, i - begin + 1));
            }
        }
    }

    /// <summary>Embeds exact glyph/Unicode mappings eagerly, retaining duplicate logical mappings as distinct encoded codes where needed.</summary>
    public static PdfFontResource Create(PdfFont font, IEnumerable<PdfGlyphMapping> mappings, bool subset = true, bool vertical = false, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (font == null)
            throw new ArgumentNullException(nameof(font));
        if (mappings == null)
            throw new ArgumentNullException(nameof(mappings));
        PdfFontResource? resource = null;
        var report = PdfFont.Run("CreateFontResource", font.ProviderId, policy, cancellationToken, context => resource = CreateCore(font, mappings, subset, vertical, context));
        resource!.OperationReport = report;
        return resource;
    }

    internal static PdfFontResource CreateCore(PdfFont font, IEnumerable<PdfGlyphMapping> mappings, bool subset, bool vertical, PdfOperationContext context)
    {
        font.EmbeddingAllowed(subset, context);
        var entries = new List<Entry>();
        var codes = new Dictionary<(int Glyph, string Text), int>();
        var sequence = new List<int>();
        IPdfVerticalFontProgram? verticalFont = font.Program as IPdfVerticalFontProgram;
        if (vertical && (verticalFont == null || !verticalFont.SupportsVerticalMetrics))
            throw PdfWriterCore.Error(context, "Font.VerticalMetricsUnavailable", "Vertical writing requires exact vertical advances and origins from the selected face.");
        foreach (var mapping in mappings)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 32);
            if (mapping == null)
                throw new ArgumentException("Glyph mappings cannot be null.", nameof(mappings));
            if (mapping.GlyphIndex == 0)
                throw PdfWriterCore.Error(context, "Font.MissingGlyph", "A requested glyph is unavailable in the explicitly selected font.");
            if (mapping.GlyphIndex >= font.Info.GlyphCount)
                throw new ArgumentOutOfRangeException(nameof(mappings));
            var key = (mapping.GlyphIndex, mapping.UnicodeText);
            if (!codes.TryGetValue(key, out int code))
            {
                if (entries.Count == 65535)
                    throw PdfWriterCore.Error(context, "Font.EncodingCapacity", "A font resource cannot exceed 65,535 distinct encoded mappings.");
                context.Consume(PdfResourceKind.ExpandedBytes, mapping.UnicodeText.Length * 4L + 256);
                code = entries.Count + 1;
                codes.Add(key, code);
                double advance = font.Program.GetAdvance(mapping.GlyphIndex, context);
                if (double.IsInfinity(advance) || double.IsNaN(advance))
                    throw PdfWriterCore.Error(context, "Font.ProviderMetrics", "A font provider returned a nonfinite advance.");
                string name = font.Program.GetGlyphName(mapping.GlyphIndex, context);
                PdfValue.Name(name);
                entries.Add(new Entry(mapping, advance, name, vertical ? verticalFont!.GetVerticalMetrics(mapping.GlyphIndex, context) : (PdfFontVerticalMetrics? )null));
            }

            sequence.Add(code);
        }

        context.Consume(PdfResourceKind.ExpandedBytes, entries.Count * 16L + 256);
        var glyphs = entries.Select(x => x.Mapping.GlyphIndex).Distinct().OrderBy(x => x).ToArray();
        font.EmbeddingAllowed(subset, context);
        using var buffer = new PdfCodecBufferStream(context, context.Available(PdfResourceKind.ExpandedBytes));
        PdfFontProgramWriteResult written;
        using (var lease = new BorrowedStream(buffer, false, context))
            written = font.Program.Write(lease, new ReadOnlyCollection<int>(glyphs), subset, context) ?? throw PdfWriterCore.Error(context, "Font.ProviderResult", "The provider returned no embedded-program details.");
        var bytes = buffer.Finish();
        bool simple = font.Info.Format == PdfFontFormat.Standard || font.Info.Format == PdfFontFormat.Type1 || font.Info.Format == PdfFontFormat.Type3 || font.Info.Format == PdfFontFormat.Cff && !written.CidKeyed;
        if (simple && entries.Count > 255)
            throw PdfWriterCore.Error(context, "Font.EncodingCapacity", "A simple font resource supports at most 255 distinct glyph/Unicode mappings; split text into explicit resources.");
        if (simple && vertical)
            throw PdfWriterCore.Error(context, "Font.VerticalEncoding", "Simple fonts do not support vertical CID writing.");
        if (font.Info.Format != PdfFontFormat.Standard && font.Info.Format != PdfFontFormat.Type3 && bytes.Length == 0)
            throw PdfWriterCore.Error(context, "Font.ProviderResult", "An embedded font program cannot be empty.");
        if (font.Info.Format == PdfFontFormat.Type1 && (written.Length1 > bytes.Length || written.Length2 > bytes.Length - written.Length1 || written.Length3 != bytes.Length - written.Length1 - written.Length2))
            throw PdfWriterCore.Error(context, "Font.ProviderResult", "Type 1 section extents do not exactly cover the emitted font.");
        bool identityCff = !simple && font.Info.Format == PdfFontFormat.Cff && entries.Select(x => x.Mapping.GlyphIndex).Distinct().Count() == entries.Count;
        context.Consume(PdfResourceKind.ExpandedBytes, sequence.Count * (simple ? 1L : 2L));
        var encoded = new byte[checked(sequence.Count * (simple ? 1 : 2))];
        for (int i = 0; i < sequence.Count; i++)
        {
            context.Checkpoint();
            int code = identityCff ? entries[sequence[i] - 1].Mapping.GlyphIndex : sequence[i];
            if (simple)
                encoded[i] = (byte)code;
            else
            {
                encoded[i * 2] = (byte)(code >> 8);
                encoded[i * 2 + 1] = (byte)code;
            }
        }

        string cmapIdentity = MappingIdentity(entries, context);
        var program = PdfValue.StreamValue(Dict(), bytes, 0, bytes.Length);
        PdfValue unicode = Cmap(entries, simple, false, vertical, identityCff, "FluentUnicode" + cmapIdentity, context), encoding = PdfValue.Null;
        if (!simple && !identityCff && font.Info.Format == PdfFontFormat.Cff)
            encoding = Cmap(entries, false, true, vertical, false, "FluentEncoding" + cmapIdentity, context);
        else if (!simple && font.Info.Format == PdfFontFormat.TrueType)
        {
            var map = new PdfByteBuilder(context);
            map.Add(0);
            map.Add(0);
            foreach (var entry in entries)
            {
                map.Add((byte)(entry.Mapping.GlyphIndex >> 8));
                map.Add((byte)entry.Mapping.GlyphIndex);
            }

            var mapBytes = map.Build();
            encoding = PdfValue.StreamValue(Dict(), mapBytes, 0, mapBytes.Length);
        }

        return new PdfFontResource(font, entries, PdfValue.OwnedString(encoded), program, written, unicode, encoding, simple, vertical, identityCff);
    }

    private static string MappingIdentity(List<Entry> entries, PdfOperationContext context)
    {
        using var hash = SHA256.Create();
        foreach (var entry in entries)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.ExpandedBytes, entry.Mapping.UnicodeText.Length * 2L + 64);
            byte[] text = Encoding.BigEndianUnicode.GetBytes(entry.Mapping.UnicodeText);
            byte[] glyph =
            {
                (byte)(entry.Mapping.GlyphIndex >> 8),
                (byte)entry.Mapping.GlyphIndex,
                (byte)(text.Length >> 8),
                (byte)text.Length
            };
            hash.TransformBlock(glyph, 0, glyph.Length, glyph, 0);
            hash.TransformBlock(text, 0, text.Length, text, 0);
        }

        hash.TransformFinalBlock(System.Array.Empty<byte>(), 0, 0);
        context.Consume(PdfResourceKind.ExpandedBytes, 256);
        return string.Concat(hash.Hash!.Select(x => x.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static PdfValue Cmap(List<Entry> entries, bool simple, bool cid, bool vertical, bool glyphCodes, string cmapName, PdfOperationContext context)
    {
        using var buffer = new PdfCodecBufferStream(context, context.Available(PdfResourceKind.ExpandedBytes));
        var sink = new PdfWriterCore.Sink(buffer, context);
        sink.Text("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CIDSystemInfo << /Registry (Adobe) /Ordering (" + (cid ? "Identity" : "UCS") + ") /Supplement 0 >> def\n/CMapName /" + cmapName + " def\n/CMapType " + (cid ? "1" : "2") + " def\n");
        if (cid)
            sink.Text("/WMode " + (vertical ? "1" : "0") + " def\n");
        sink.Text("1 begincodespacerange\n" + (simple ? "<00> <FF>" : "<0000> <FFFF>") + "\nendcodespacerange\n");
        for (int first = 0; first < entries.Count; first += 100)
        {
            int count = Math.Min(100, entries.Count - first);
            sink.Text(count.ToString(CultureInfo.InvariantCulture) + (cid ? " begincidchar\n" : " beginbfchar\n"));
            for (int i = first; i < first + count; i++)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.ExpandedBytes, entries[i].Mapping.UnicodeText.Length * 8L + 128);
                sink.Text("<" + (glyphCodes ? entries[i].Mapping.GlyphIndex : i + 1).ToString(simple ? "X2" : "X4", CultureInfo.InvariantCulture) + "> ");
                if (cid)
                    sink.Text(entries[i].Mapping.GlyphIndex.ToString(CultureInfo.InvariantCulture));
                else
                {
                    sink.Text("<");
                    foreach (char c in entries[i].Mapping.UnicodeText)
                        sink.Text(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    sink.Text(">");
                }

                sink.Text("\n");
            }

            sink.Text(cid ? "endcidchar\n" : "endbfchar\n");
        }

        sink.Text("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n");
        sink.Flush();
        var bytes = buffer.Finish();
        var dictionary = cid ? Dict(("Type", PdfValue.Name("CMap")), ("CMapName", PdfValue.Name(cmapName)), ("CIDSystemInfo", Dict(("Registry", PdfValue.String(Encoding.ASCII.GetBytes("Adobe"))), ("Ordering", PdfValue.String(Encoding.ASCII.GetBytes("Identity"))), ("Supplement", PdfValue.Integer(0)))), ("WMode", PdfValue.Integer(vertical ? 1 : 0))) : Dict();
        return PdfValue.StreamValue(dictionary, bytes, 0, bytes.Length);
    }

    internal PdfObjectId AddTo(PdfTransaction transaction, PdfOperationContext context)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, _entries.Count * 8L + 32);
        context.Consume(PdfResourceKind.ExpandedBytes, _entries.Count * 256L + 4096);
        var unicode = transaction.Add(_toUnicode);
        var common = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
        {
            {
                "Type",
                PdfValue.Name("Font")
            },
            {
                "BaseFont",
                PdfValue.Name(_written.PostScriptName)
            },
            {
                "ToUnicode",
                PdfValue.Reference(unicode)
            }
        };
        if (Font.Program is PdfType3FontProgram type3)
        {
            var charProcs = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
            var differences = new List<PdfValue>
            {
                PdfValue.Integer(1)
            };
            var widths = new List<PdfValue>();
            foreach (var entry in _entries)
            {
                context.Checkpoint();
                differences.Add(PdfValue.Name(entry.Name));
                widths.Add(PdfValues.Real(entry.Advance));
                if (!charProcs.ContainsKey(entry.Name))
                    charProcs.Add(entry.Name, PdfValue.Reference(transaction.Add(type3.GlyphProgram(entry.Mapping.GlyphIndex, context).ToStream(context))));
            }

            if (_entries.Count == 0)
            {
                differences.Add(PdfValue.Name(".notdef"));
                widths.Add(PdfValue.Integer(0));
                charProcs.Add(".notdef", PdfValue.Reference(transaction.Add(type3.GlyphProgram(0, context).ToStream(context))));
            }

            var metrics = Font.Info.Metrics;
            common["Subtype"] = PdfValue.Name("Type3");
            common["FontBBox"] = PdfValue.Array(PdfValue.Integer(metrics.Left), PdfValue.Integer(metrics.Bottom), PdfValue.Integer(metrics.Right), PdfValue.Integer(metrics.Top));
            common["FontMatrix"] = PdfValue.Array(PdfValues.Real(1.0 / metrics.UnitsPerEm), PdfValue.Integer(0), PdfValue.Integer(0), PdfValues.Real(1.0 / metrics.UnitsPerEm), PdfValue.Integer(0), PdfValue.Integer(0));
            common["CharProcs"] = PdfValue.OwnedDictionary(charProcs);
            common["Resources"] = type3.Resources;
            common["Encoding"] = Dict(("Type", PdfValue.Name("Encoding")), ("Differences", PdfValue.OwnedArray(differences)));
            common["FirstChar"] = PdfValue.Integer(1);
            common["LastChar"] = PdfValue.Integer(Math.Max(1, _entries.Count));
            common["Widths"] = PdfValue.OwnedArray(widths);
            return transaction.Add(PdfValue.OwnedDictionary(common));
        }

        PdfObjectId? descriptor = null;
        if (Font.Info.Format != PdfFontFormat.Standard)
        {
            var program = PdfWriterCore.CopyDictionary(_program.Entries, context);
            if (_written.FontFileSubtype != null)
                program["Subtype"] = PdfValue.Name(_written.FontFileSubtype);
            if (_written.Length1 > 0)
                program["Length1"] = PdfValue.Integer(_written.Length1);
            if (_written.Length2 > 0)
                program["Length2"] = PdfValue.Integer(_written.Length2);
            if (Font.Info.Format == PdfFontFormat.Type1)
                program["Length3"] = PdfValue.Integer(_written.Length3);
            var file = transaction.Add(_program.WithDictionary(PdfValue.OwnedDictionary(program)));
            var m = Font.Info.Metrics;
            double scale = 1000.0 / m.UnitsPerEm;
            int flags = (m.FixedPitch ? 1 : 0) | (Font.Info.Symbolic ? 4 : 32) | (m.ItalicAngle != 0 ? 64 : 0);
            descriptor = transaction.Add(Dict(("Type", PdfValue.Name("FontDescriptor")), ("FontName", PdfValue.Name(_written.PostScriptName)), ("Flags", PdfValue.Integer(flags)), ("FontBBox", PdfValue.Array(PdfValues.Real(m.Left * scale), PdfValues.Real(m.Bottom * scale), PdfValues.Real(m.Right * scale), PdfValues.Real(m.Top * scale))), ("ItalicAngle", PdfValues.Real(m.ItalicAngle)), ("Ascent", PdfValues.Real(m.Ascender * scale)), ("Descent", PdfValues.Real(m.Descender * scale)), ("CapHeight", PdfValues.Real(m.CapHeight * scale)), ("StemV", PdfValue.Integer(0)), (Font.Info.Format == PdfFontFormat.TrueType ? "FontFile2" : _written.FontFileSubtype == null ? "FontFile" : "FontFile3", PdfValue.Reference(file))));
        }

        double unit = 1000.0 / Font.Info.Metrics.UnitsPerEm;
        if (_simple)
        {
            var differences = new List<PdfValue>
            {
                PdfValue.Integer(1)
            };
            var widths = new List<PdfValue>();
            foreach (var entry in _entries)
            {
                context.Checkpoint();
                differences.Add(PdfValue.Name(entry.Name));
                widths.Add(PdfValues.Real(entry.Advance * unit));
            }

            if (_entries.Count == 0)
            {
                differences.Add(PdfValue.Name(".notdef"));
                widths.Add(PdfValue.Integer(0));
            }

            common["Subtype"] = PdfValue.Name("Type1");
            if (descriptor.HasValue)
                common["FontDescriptor"] = PdfValue.Reference(descriptor.Value);
            common["Encoding"] = Dict(("Type", PdfValue.Name("Encoding")), ("Differences", PdfValue.OwnedArray(differences)));
            common["FirstChar"] = PdfValue.Integer(1);
            common["LastChar"] = PdfValue.Integer(Math.Max(1, _entries.Count));
            common["Widths"] = PdfValue.OwnedArray(widths);
            return transaction.Add(PdfValue.OwnedDictionary(common));
        }

        bool trueType = Font.Info.Format == PdfFontFormat.TrueType;
        var widthEntries = new SortedDictionary<int, Entry>();
        for (int i = 0; i < _entries.Count; i++)
            widthEntries[trueType ? i + 1 : _entries[i].Mapping.GlyphIndex] = _entries[i];
        var w = new List<PdfValue>();
        var w2 = new List<PdfValue>();
        foreach (var pair in widthEntries)
        {
            context.Checkpoint();
            w.Add(PdfValue.Integer(pair.Key));
            w.Add(PdfValue.Array(PdfValues.Real(pair.Value.Advance * unit)));
            if (Vertical)
            {
                var metric = pair.Value.Vertical!.Value;
                w2.Add(PdfValue.Integer(pair.Key));
                w2.Add(PdfValue.Array(PdfValues.Real(-metric.Advance * unit), PdfValues.Real(metric.OriginX * unit), PdfValues.Real(metric.OriginY * unit)));
            }
        }

        var descendant = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
        {
            {
                "Type",
                PdfValue.Name("Font")
            },
            {
                "Subtype",
                PdfValue.Name(Font.Info.Format == PdfFontFormat.TrueType ? "CIDFontType2" : "CIDFontType0")
            },
            {
                "BaseFont",
                PdfValue.Name(_written.PostScriptName)
            },
            {
                "CIDSystemInfo",
                Dict(("Registry", PdfValue.String(Encoding.ASCII.GetBytes("Adobe"))), ("Ordering", PdfValue.String(Encoding.ASCII.GetBytes("Identity"))), ("Supplement", PdfValue.Integer(0)))
            },
            {
                "FontDescriptor",
                PdfValue.Reference(descriptor!.Value)
            },
            {
                "W",
                PdfValue.OwnedArray(w)
            }
        };
        if (trueType)
            descendant["CIDToGIDMap"] = PdfValue.Reference(transaction.Add(_encoding));
        if (Vertical)
            descendant["W2"] = PdfValue.OwnedArray(w2);
        common["Subtype"] = PdfValue.Name("Type0");
        common["Encoding"] = trueType || _identityCff ? PdfValue.Name(Vertical ? "Identity-V" : "Identity-H") : PdfValue.Reference(transaction.Add(_encoding));
        common["DescendantFonts"] = PdfValue.Array(PdfValue.Reference(transaction.Add(PdfValue.OwnedDictionary(descendant))));
        return transaction.Add(PdfValue.OwnedDictionary(common));
    }

    private static PdfValue Dict(params (string Key, PdfValue Value)[] entries)
    {
        var result = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
        foreach (var entry in entries)
            result.Add(entry.Key, entry.Value);
        return PdfValue.OwnedDictionary(result);
    }
}
