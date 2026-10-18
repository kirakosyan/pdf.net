namespace Fluent.Pdf;
internal sealed class PdfExtractionFont
{
    private readonly PdfGraphEdit _graph;
    private readonly PdfProviderRegistry _providers;
    private readonly PdfValue _encoding;
    private PdfUnicodeMap? _unicode;
    private readonly PdfValue _unicodeSource;
    private bool _mappingReady;
    private readonly Dictionary<uint, double> _widths = new();
    private readonly Dictionary<uint, (double Advance, double X, double Y)> _vertical = new();
    private readonly string? [] _decoded = new string? [256];
    private readonly IReadOnlyList<double?>? _standardWidths;
    private readonly IPdfFontEncodingProvider? _simple;
    private readonly double _defaultWidth, _defaultVertical, _defaultOrigin;
    internal readonly string Name;
    internal readonly bool Vertical, Composite, Estimated;
    internal readonly double Ascent, Descent;
    internal bool WidthWasEstimated { get; private set; }

    private bool _heightReported;
    private PdfOperationContext Context => _graph.Context;

    internal PdfExtractionFont(PdfValue dictionary, PdfGraphEdit graph, PdfProviderRegistry providers)
    {
        _graph = graph;
        _providers = providers;
        dictionary = graph.Resolve(dictionary);
        if (dictionary.Kind != PdfValueKind.Dictionary)
            throw Error("FontDictionary", "A font resource must resolve to a dictionary.");
        Context.Consume(PdfResourceKind.ExpandedBytes, 4096);
        string subtype = TextName(graph.Get(dictionary, "Subtype"));
        Name = TextName(graph.Get(dictionary, "BaseFont"));
        if (subtype != "Type0" && subtype != "Type1" && subtype != "MMType1" && subtype != "TrueType" && subtype != "Type3")
            throw Error("FontSubtype", "The source font subtype requires an explicit extraction profile.");
        Composite = subtype == "Type0";
        _encoding = Deep(graph.Get(dictionary, "Encoding"));
        var metrics = dictionary;
        if (Composite)
        {
            if (_encoding.Kind != PdfValueKind.Name || _encoding.NameValue != "Identity-H" && _encoding.NameValue != "Identity-V")
                throw Error("CidEncodingProfile", "CID geometry requires a supported character-code-to-CID encoding profile.");
            Vertical = _encoding.NameValue == "Identity-V";
            var descendants = graph.Get(dictionary, "DescendantFonts");
            if (descendants.Kind != PdfValueKind.Array || descendants.Items.Count != 1)
                throw Error("DescendantFont", "A Type 0 font requires one descendant.");
            metrics = graph.Resolve(descendants.Items[0]);
            if (metrics.Kind != PdfValueKind.Dictionary)
                throw Error("DescendantFont", "The CID descendant must be a dictionary.");
            var width = graph.Get(metrics, "DW");
            _defaultWidth = width.Kind == PdfValueKind.Null ? 1000 : Number(width);
            CidWidths(graph.Get(metrics, "W"), false);
            var defaults = graph.Get(metrics, "DW2");
            _defaultOrigin = 880;
            _defaultVertical = -1000;
            if (defaults.Kind != PdfValueKind.Null)
            {
                if (defaults.Kind != PdfValueKind.Array || defaults.Items.Count != 2)
                    throw Error("VerticalMetrics", "DW2 requires origin and advance values.");
                _defaultOrigin = Number(graph.Resolve(defaults.Items[0]));
                _defaultVertical = Number(graph.Resolve(defaults.Items[1]));
            }

            CidWidths(graph.Get(metrics, "W2"), true);
        }
        else
        {
            if (subtype == "Type3")
            {
                var matrix = graph.Get(dictionary, "FontMatrix");
                if (matrix.Kind != PdfValueKind.Array || matrix.Items.Count != 6 || Number(graph.Resolve(matrix.Items[0])) != .001 || Number(graph.Resolve(matrix.Items[3])) != .001 || new[]
                {
                    1,
                    2,
                    4,
                    5
                }.Any(i => Number(graph.Resolve(matrix.Items[i])) != 0))
                    throw Error("Type3MatrixProfile", "Type 3 extraction requires an explicitly supported font-matrix geometry profile.");
            }

            var widths = graph.Get(dictionary, "Widths");
            if (widths.Kind != PdfValueKind.Null)
            {
                var first = graph.Get(dictionary, "FirstChar");
                var last = graph.Get(dictionary, "LastChar");
                if (!first.TryGetInteger(out long start) || !last.TryGetInteger(out long end) || start < 0 || end > 255 || end < start || widths.Kind != PdfValueKind.Array || widths.Items.Count != end - start + 1)
                    throw Error("SimpleWidths", "Simple Widths must exactly cover FirstChar through LastChar.");
                for (int i = 0; i < widths.Items.Count; i++)
                    AddWidth((uint)(start + i), Number(graph.Resolve(widths.Items[i])));
            }
        }

        var descriptor = graph.Get(metrics, "FontDescriptor");
        if (descriptor.Kind != PdfValueKind.Null && descriptor.Kind != PdfValueKind.Dictionary)
            throw Error("FontDescriptor", "A FontDescriptor must be a dictionary.");
        var missing = graph.Get(descriptor, "MissingWidth");
        if (!Composite)
            _defaultWidth = missing.Kind == PdfValueKind.Null ? 0 : Number(missing);
        string standard = Name.Length > 7 && Name[6] == '+' && Name.Take(6).All(c => c >= 'A' && c <= 'Z') ? Name.Substring(7) : Name;
        bool base14 = new[]
        {
            "Courier",
            "Courier-Bold",
            "Courier-Oblique",
            "Courier-BoldOblique",
            "Helvetica",
            "Helvetica-Bold",
            "Helvetica-Oblique",
            "Helvetica-BoldOblique",
            "Times-Roman",
            "Times-Bold",
            "Times-Italic",
            "Times-BoldItalic",
            "Symbol",
            "ZapfDingbats"
        }.Contains(standard);
        IPdfFontProvider? provider = providers.Capabilities.Contains(PdfCapability.Fonts) ? providers.GetRequired<IPdfFontProvider>(PdfCapability.Fonts) : null;
        if (!Composite && _widths.Count == 0 && base14)
        {
            if (provider is not IPdfFontEncodingMetricsProvider widthProvider)
                throw Error("StandardMetricsProvider", "A standard font without Widths requires an explicit encoding-metrics provider.");
            var supplied = widthProvider.GetSimpleAdvances(_encoding, Name, Context);
            if (supplied == null || supplied.Count != 256)
                throw Error("MetricsProviderResult", "A simple-font metrics provider must return exactly 256 entries.");
            Context.Consume(PdfResourceKind.ExpandedBytes, 8192);
            var captured = new double? [256];
            for (int i = 0; i < captured.Length; i++)
            {
                Context.Checkpoint();
                captured[i] = supplied[i];
                if (captured[i].HasValue && (double.IsInfinity(captured[i]!.Value) || double.IsNaN(captured[i]!.Value)))
                    throw Error("MetricsProviderResult", "A simple-font advance must be finite.");
            }

            _standardWidths = Array.AsReadOnly(captured);
        }
        else if (!Composite && _widths.Count == 0)
            throw Error("WidthsRequired", "A nonstandard simple font requires its declared Widths array.");
        var ascender = graph.Get(descriptor, "Ascent");
        var descender = graph.Get(descriptor, "Descent");
        var box = graph.Get(subtype == "Type3" ? dictionary : descriptor, "FontBBox");
        if (ascender.Kind != PdfValueKind.Null && descender.Kind != PdfValueKind.Null)
        {
            Ascent = Number(ascender);
            Descent = Number(descender);
        }
        else if (box.Kind == PdfValueKind.Array && box.Items.Count == 4)
        {
            Descent = Number(graph.Resolve(box.Items[1]));
            Ascent = Number(graph.Resolve(box.Items[3]));
        }
        else if (base14 && provider != null)
        {
            var face = provider.Standard(standard, Context);
            var m = face.Info.Metrics;
            Ascent = m.Ascender * 1000.0 / m.UnitsPerEm;
            Descent = m.Descender * 1000.0 / m.UnitsPerEm;
        }
        else
        {
            Ascent = 800;
            Descent = -200;
            Estimated = true;
        }

        if (Ascent < Descent)
            throw Error("FontHeight", "The source font ascent/descent interval is reversed.");
        _unicodeSource = graph.Get(dictionary, "ToUnicode");
        if (_unicodeSource.Kind != PdfValueKind.Stream && _unicodeSource.Kind != PdfValueKind.Null)
            throw Error("ToUnicode", "ToUnicode must resolve to a stream.");
        _simple = provider as IPdfFontEncodingProvider;
    }

    private void Mapping()
    {
        if (_mappingReady)
            return;
        _mappingReady = true;
        if (_unicodeSource.Kind == PdfValueKind.Stream)
            _unicode = PdfUnicodeMap.ParseCore(PdfExtractionData.Read(_unicodeSource, _graph, _providers), null, Context);
        else if (Composite)
            Context.Report(new("Extraction.UnmappedCompositeText", "CID text without ToUnicode has no established Unicode mapping.", lossCategory: PdfLossCategory.SubstitutedContent));
        else
        {
            if (_simple == null)
                Context.Report(new("Extraction.EncodingProviderRequired", "Simple text without ToUnicode requires an explicit encoding provider.", lossCategory: PdfLossCategory.SubstitutedContent));
            else
                Context.Report(new("Font.UnicodeInferred", "Unicode is inferred from an explicit simple-font encoding provider.", severity: PdfDiagnosticSeverity.Information));
        }
    }

    internal (uint Code, int Length, string Text, PdfTextMappingConfidence Confidence) Decode(IReadOnlyList<byte> bytes, ref int position, bool mapText = true)
    {
        if (mapText)
            Mapping();
        if (mapText && _unicode != null)
        {
            var code = _unicode.DecodeCode(bytes, ref position, Context);
            if (code.Length != (Composite ? 2 : 1))
                throw Error("CodeSpaceProfile", "The ToUnicode code space does not match the supported font encoding.");
            return (code.Code, code.Length, code.Text, code.Mapped ? PdfTextMappingConfidence.ToUnicode : PdfTextMappingConfidence.Unmapped);
        }

        Context.Checkpoint();
        Context.Consume(PdfResourceKind.Objects, 1);
        int length = Composite ? 2 : 1;
        if (position > bytes.Count - length)
            throw Error("TruncatedCode", "A composite font code is truncated.");
        uint value = bytes[position++];
        if (Composite)
            value = (value << 8) | bytes[position++];
        if (!mapText || _simple == null)
            return (value, length, "\ufffd", PdfTextMappingConfidence.Unmapped);
        string? text = _decoded[value];
        if (text == null)
        {
            Context.Consume(PdfResourceKind.ExpandedBytes, 128);
            text = _simple.DecodeSimple(_encoding, PdfValue.String((byte)value), Name, Context);
            _decoded[value] = text;
        }

        return (value, length, text, text == "\ufffd" ? PdfTextMappingConfidence.Unmapped : PdfTextMappingConfidence.Inferred);
    }

    internal (double Width, double AdvanceY, double OriginX, double OriginY) Metrics(uint code)
    {
        WidthWasEstimated = false;
        double width;
        if (_standardWidths != null)
        {
            var known = _standardWidths[(int)code];
            if (!known.HasValue)
            {
                Context.Report(new("Extraction.UnmappedWidth", "The declared encoding names an unsupported standard-font glyph width.", lossCategory: PdfLossCategory.FidelityReduction));
                width = 0;
                WidthWasEstimated = true;
            }
            else
            {
                width = known.Value;
                if (double.IsNaN(width) || double.IsInfinity(width))
                    throw Error("MetricsProviderResult", "The font metrics provider returned a nonfinite advance.");
            }
        }
        else
            width = _widths.TryGetValue(code, out double declared) ? declared : _defaultWidth;
        if (!Vertical)
            return (width, 0, 0, 0);
        var vertical = _vertical.TryGetValue(code, out var specific) ? specific : (_defaultVertical, width / 2, _defaultOrigin);
        return (width, vertical.Item1, vertical.Item2, vertical.Item3);
    }

    internal void RequireGeometry()
    {
        if (Estimated && !_heightReported)
        {
            _heightReported = true;
            Context.Report(new("Extraction.EstimatedFontGeometry", "The source omits font height metrics; the requested geometry requires an explicit estimate.", lossCategory: PdfLossCategory.FidelityReduction));
        }
    }

    private void CidWidths(PdfValue value, bool vertical)
    {
        if (value.Kind == PdfValueKind.Null)
            return;
        if (value.Kind != PdfValueKind.Array)
            throw Error("CidWidths", "CID metric declarations must be arrays.");
        int at = 0;
        while (at < value.Items.Count)
        {
            Context.Checkpoint();
            uint first = Cid(_graph.Resolve(value.Items[at++]));
            if (at == value.Items.Count)
                throw Error("CidWidths", "A CID metric interval is incomplete.");
            var next = _graph.Resolve(value.Items[at++]);
            int fields = vertical ? 3 : 1;
            if (next.Kind == PdfValueKind.Array)
            {
                if (next.Items.Count % fields != 0 || next.Items.Count / fields > 65536 - first)
                    throw Error("CidWidths", "A CID metric array exceeds its code range.");
                for (int i = 0; i < next.Items.Count; i += fields)
                    Set(first++, Number(_graph.Resolve(next.Items[i])), vertical ? Number(_graph.Resolve(next.Items[i + 1])) : 0, vertical ? Number(_graph.Resolve(next.Items[i + 2])) : 0);
            }
            else
            {
                uint last = Cid(next);
                if (last < first || at > value.Items.Count - fields)
                    throw Error("CidWidths", "A CID metric interval is incomplete or reversed.");
                double advance = Number(_graph.Resolve(value.Items[at++])), x = vertical ? Number(_graph.Resolve(value.Items[at++])) : 0, y = vertical ? Number(_graph.Resolve(value.Items[at++])) : 0;
                for (uint code = first; code <= last; code++)
                    Set(code, advance, x, y);
            }
        }

        void Set(uint code, double advance, double x, double y)
        {
            if (vertical)
            {
                Context.Consume(PdfResourceKind.Objects, 1);
                Context.Consume(PdfResourceKind.ExpandedBytes, 96);
                if (_vertical.ContainsKey(code))
                    throw Error("CidMetricOverlap", "CID metric intervals overlap.");
                _vertical.Add(code, (advance, x, y));
            }
            else
                AddWidth(code, advance);
        }
    }

    private void AddWidth(uint code, double width)
    {
        Context.Consume(PdfResourceKind.Objects, 1);
        Context.Consume(PdfResourceKind.ExpandedBytes, 64);
        if (_widths.ContainsKey(code))
            throw Error("CidMetricOverlap", "Font width intervals overlap.");
        _widths.Add(code, width);
    }

    private uint Cid(PdfValue value)
    {
        if (!value.TryGetInteger(out long code) || code < 0 || code > 65535)
            throw Error("CidRange", "A CID must be in 0 through 65535.");
        return (uint)code;
    }

    private string TextName(PdfValue value)
    {
        if (value.Kind == PdfValueKind.Null)
            return "";
        if (value.Kind != PdfValueKind.Name)
            throw Error("FontName", "Font subtype/base names must be names.");
        return value.NameValue;
    }

    private double Number(PdfValue value) => PdfExtractionData.Number(value, _graph);
    private PdfException Error(string code, string message) => _graph.Error("Extraction." + code, message);
    private PdfValue Deep(PdfValue value)
    {
        Context.Checkpoint();
        using var depth = Context.EnterScope(PdfResourceKind.Depth);
        Context.Consume(PdfResourceKind.Objects, 1);
        value = _graph.Resolve(value);
        if (value.Kind == PdfValueKind.Array)
        {
            Context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 32L + 64);
            return PdfValue.OwnedArray(value.Items.Select(Deep).ToList());
        }

        if (value.Kind == PdfValueKind.Dictionary)
        {
            var entries = _graph.Copy(value);
            foreach (var pair in value.Entries)
                entries[pair.Key] = Deep(pair.Value);
            return PdfValue.OwnedDictionary(entries);
        }

        return value;
    }
}

internal static class PdfExtractionData
{
    internal static PdfByteStore Read(PdfValue stream, PdfGraphEdit graph, PdfProviderRegistry providers)
    {
        graph.Context.Consume(PdfResourceKind.InputBytes, stream.RawStreamLength);
        using var input = PdfFilters.Decode(PdfFilterDeclarations.Resolve(stream, id => graph.Resolve(PdfValue.Reference(id)), graph.Context), graph.Context, providers);
        return PdfByteStore.Read(input, graph.Context);
    }

    internal static double Number(PdfValue value, PdfGraphEdit graph)
    {
        if (value.Kind != PdfValueKind.Number)
            throw graph.Error("Extraction.NumberRequired", "A numeric content/font value is required.");
        try
        {
            double number = value.NumberValue;
            if (double.IsInfinity(number) || double.IsNaN(number))
                throw graph.Error("Extraction.NumericRange", "A content/font number exceeds finite geometry.");
            return number;
        }
        catch (InvalidOperationException)
        {
            throw graph.Error("Extraction.NumericRange", "A content/font number exceeds finite geometry.");
        }
    }
}
