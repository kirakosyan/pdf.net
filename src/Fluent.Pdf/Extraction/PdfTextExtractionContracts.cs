namespace Fluent.Pdf;
/// <summary>The provenance of an extracted Unicode value.</summary>
public enum PdfTextMappingConfidence
{
    /// <summary>Exact source ToUnicode mapping, without a claim about the source mapping's linguistic accuracy.</summary>
    ToUnicode,
    /// <summary>Explicit source ActualText replacement.</summary>
    ActualText,
    /// <summary>Inferred from a declared simple-font encoding through an explicit provider.</summary>
    Inferred,
    /// <summary>No mapping was available; a visible replacement is accompanied by diagnostics.</summary>
    Unmapped
}

/// <summary>Explicit text/layer selection and finite geometric grouping tolerances.</summary>
public sealed class PdfTextExtractionOptions
{
    /// <summary>Captures selection and grouping. Tolerances use displayed physical points; no OCR or action execution occurs.</summary>
    public PdfTextExtractionOptions(bool includeHiddenLayers = false, bool includeInvisibleText = true, double lineTolerance = 2, double wordGapRatio = .25, PdfLayerUsage layerUsage = PdfLayerUsage.View, PdfValue? layerConfiguration = null)
    {
        if (double.IsNaN(lineTolerance) || double.IsInfinity(lineTolerance) || lineTolerance <= 0 || lineTolerance > 1000)
            throw new ArgumentOutOfRangeException(nameof(lineTolerance));
        if (double.IsNaN(wordGapRatio) || double.IsInfinity(wordGapRatio) || wordGapRatio <= 0 || wordGapRatio > 10)
            throw new ArgumentOutOfRangeException(nameof(wordGapRatio));
        if (!Enum.IsDefined(typeof(PdfLayerUsage), layerUsage))
            throw new ArgumentOutOfRangeException(nameof(layerUsage));
        IncludeHiddenLayers = includeHiddenLayers;
        IncludeInvisibleText = includeInvisibleText;
        LineTolerance = lineTolerance;
        WordGapRatio = wordGapRatio;
        LayerUsage = layerUsage;
        LayerConfiguration = layerConfiguration;
    }

    /// <summary>Gets whether optional-content-hidden characters are retained and flagged.</summary>
    public bool IncludeHiddenLayers { get; }
    /// <summary>Gets whether nonpainting text modes, including searchable OCR text, are retained.</summary>
    public bool IncludeInvisibleText { get; }
    /// <summary>Gets the displayed baseline grouping tolerance in points.</summary>
    public double LineTolerance { get; }
    /// <summary>Gets the font-size-relative gap used to infer word breaks.</summary>
    public double WordGapRatio { get; }
    /// <summary>Gets the explicit optional-content usage event.</summary>
    public PdfLayerUsage LayerUsage { get; }
    /// <summary>Gets the optional source configuration; null selects the default.</summary>
    public PdfValue? LayerConfiguration { get; }
}

/// <summary>A decoded source character code and its Unicode cluster. Ligatures may map to multiple scalars sharing one advance cell.</summary>
public sealed class PdfExtractedCharacter
{
    internal PdfExtractedCharacter(int index, int operation, uint code, int codeLength, string text, PdfTextMappingConfidence confidence, PdfQuad quad, PdfQuad userQuad, PdfPoint baselineStart, PdfPoint baselineEnd, string font, double size, int mode, bool layerVisible, bool approximate)
    {
        Index = index;
        OperationIndex = operation;
        Code = code;
        CodeLength = codeLength;
        Text = text;
        Confidence = confidence;
        Quad = quad;
        UserQuad = userQuad;
        BaselineStart = baselineStart;
        BaselineEnd = baselineEnd;
        FontName = font;
        FontSize = size;
        RenderingMode = mode;
        LayerVisible = layerVisible;
        GeometryEstimated = approximate;
    }

    internal PdfValue EncodedString { get; set; } = PdfValue.Null;
    internal int ByteOffset { get; set; }
    internal double AdvanceTJ { get; set; }
    internal bool Vertical { get; set; }
    /// <summary>Gets raw execution order within the selected page.</summary>
    public int Index { get; }
    /// <summary>Gets source operator execution order, including nested Forms.</summary>
    public int OperationIndex { get; }
    /// <summary>Gets the uninterpreted source character code.</summary>
    public uint Code { get; }
    /// <summary>Gets its encoded byte length.</summary>
    public int CodeLength { get; }
    /// <summary>Gets the decoded Unicode cluster; it may contain multiple scalars.</summary>
    public string Text { get; }
    /// <summary>Gets the mapping provenance, without inferred certainty.</summary>
    public PdfTextMappingConfidence Confidence { get; }
    /// <summary>Gets the advance-cell quad in displayed top-left physical points, not an ink outline.</summary>
    public PdfQuad Quad { get; }
    /// <summary>Gets the advance-cell quad in native unrotated PDF user coordinates.</summary>
    public PdfQuad UserQuad { get; }
    /// <summary>Gets the displayed baseline start.</summary>
    public PdfPoint BaselineStart { get; }
    /// <summary>Gets the displayed baseline end before character/word spacing.</summary>
    public PdfPoint BaselineEnd { get; }
    /// <summary>Gets the declared PDF font name.</summary>
    public string FontName { get; }
    /// <summary>Gets the effective displayed em height in physical points.</summary>
    public double FontSize { get; }
    /// <summary>Gets the PDF text rendering mode.</summary>
    public int RenderingMode { get; }
    /// <summary>Gets optional-content visibility separately from rendering mode and clipping.</summary>
    public bool LayerVisible { get; }
    /// <summary>Gets whether missing source metrics required an explicitly diagnosed estimate.</summary>
    public bool GeometryEstimated { get; }
}

/// <summary>A source ActualText replacement and its covered raw character range.</summary>
public sealed class PdfExtractedSpan
{
    internal PdfExtractedSpan(int first, int count, string text, PdfRect? bounds)
    {
        FirstCharacter = first;
        CharacterCount = count;
        Text = text;
        Bounds = bounds;
    }

    /// <summary>Gets the first covered selected character index.</summary>
    public int FirstCharacter { get; }
    /// <summary>Gets the number of covered selected characters.</summary>
    public int CharacterCount { get; }
    /// <summary>Gets the exact declared logical replacement.</summary>
    public string Text { get; }
    /// <summary>Gets the union of covered displayed advance cells, or null when the replacement has no glyph geometry.</summary>
    public PdfRect? Bounds { get; }
}

/// <summary>An inferred geometric word retaining its raw character records.</summary>
public sealed class PdfExtractedWord
{
    internal PdfExtractedWord(int index, List<PdfExtractedCharacter> characters, string text, PdfRect bounds)
    {
        Index = index;
        Characters = characters.AsReadOnly();
        Text = text;
        Bounds = bounds;
    }

    /// <summary>Gets its stable raw word order.</summary>
    public int Index { get; }
    /// <summary>Gets exact underlying records in source execution order.</summary>
    public IReadOnlyList<PdfExtractedCharacter> Characters { get; }
    /// <summary>Gets source-order Unicode; logical replacements are exposed separately.</summary>
    public string Text { get; }
    /// <summary>Gets the union of displayed advance cells.</summary>
    public PdfRect Bounds { get; }
}

/// <summary>A geometrically inferred line, separate from source execution order.</summary>
public sealed class PdfExtractedLine
{
    internal PdfExtractedLine(List<PdfExtractedWord> words, PdfRect bounds)
    {
        Words = words.AsReadOnly();
        Bounds = bounds;
    }

    /// <summary>Gets words in an explicitly inferred geometric order.</summary>
    public IReadOnlyList<PdfExtractedWord> Words { get; }
    /// <summary>Gets the displayed advance-cell union.</summary>
    public PdfRect Bounds { get; }
}

/// <summary>A geometric paragraph inference; it is not an accessibility/structure-tree assertion.</summary>
public sealed class PdfExtractedParagraph
{
    internal PdfExtractedParagraph(List<PdfExtractedLine> lines, PdfRect bounds)
    {
        Lines = lines.AsReadOnly();
        Bounds = bounds;
    }

    /// <summary>Gets inferred lines.</summary>
    public IReadOnlyList<PdfExtractedLine> Lines { get; }
    /// <summary>Gets their displayed advance-cell union.</summary>
    public PdfRect Bounds { get; }
}

/// <summary>Detached page text with raw, semantic and layout views and explicit mapping/geometry provenance.</summary>
public sealed class PdfTextExtractionResult
{
    internal PdfTextExtractionResult(int page, PdfPageGeometry geometry, List<PdfExtractedCharacter> characters, List<PdfExtractedSpan> spans, List<PdfExtractedWord> words, List<PdfExtractedLine> lines, List<PdfExtractedParagraph> paragraphs, string raw, string text, string layout, PdfOperationReport report)
    {
        PageIndex = page;
        Geometry = geometry;
        Characters = characters.AsReadOnly();
        ActualTextSpans = spans.AsReadOnly();
        Words = words.AsReadOnly();
        Lines = lines.AsReadOnly();
        Paragraphs = paragraphs.AsReadOnly();
        RawText = raw;
        Text = text;
        LayoutText = layout;
        OperationReport = report;
    }

    /// <summary>Gets the zero-based physical page index.</summary>
    public int PageIndex { get; }
    /// <summary>Gets immutable boxes, viewing rotation and UserUnit.</summary>
    public PdfPageGeometry Geometry { get; }
    /// <summary>Gets raw character-code execution order.</summary>
    public IReadOnlyList<PdfExtractedCharacter> Characters { get; }
    /// <summary>Gets source-declared logical replacements without inventing per-character geometry.</summary>
    public IReadOnlyList<PdfExtractedSpan> ActualTextSpans { get; }
    /// <summary>Gets inferred word segmentation in raw order.</summary>
    public IReadOnlyList<PdfExtractedWord> Words { get; }
    /// <summary>Gets inferred geometric line order.</summary>
    public IReadOnlyList<PdfExtractedLine> Lines { get; }
    /// <summary>Gets inferred paragraph grouping.</summary>
    public IReadOnlyList<PdfExtractedParagraph> Paragraphs { get; }
    /// <summary>Gets decoded cluster text in source order without synthetic spacing or ActualText substitution.</summary>
    public string RawText { get; }
    /// <summary>Gets source-order text with ActualText replacements and explicitly inferred positional separators.</summary>
    public string Text { get; }
    /// <summary>Gets a separate geometric layout inference; it is not a claim of original logical reading order.</summary>
    public string LayoutText { get; }
    /// <summary>Gets diagnostics and resource usage.</summary>
    public PdfOperationReport OperationReport { get; }
}
