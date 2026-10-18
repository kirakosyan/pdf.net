using System.Collections.ObjectModel;

namespace Fluent.Pdf;
/// <summary>Horizontal line alignment within the available flow interval.</summary>
public enum PdfFlowAlignment
{
    /// <summary>Align to the paragraph leading edge.</summary>
    Start,
    /// <summary>Align to the paragraph trailing edge.</summary>
    End,
    /// <summary>Center within the interval.</summary>
    Center,
    /// <summary>Expand ordinary spaces on wrapped nonfinal lines.</summary>
    Justify
}

/// <summary>Explicit handling of a word wider than an empty available interval.</summary>
public enum PdfFlowOverflow
{
    /// <summary>Reject overlong words without producing a layout.</summary>
    Error,
    /// <summary>Reshape emergency breaks at extended grapheme boundaries.</summary>
    BreakGrapheme,
    /// <summary>Clip complete overlong words with an explicit loss diagnostic.</summary>
    Clip
}

/// <summary>Physical side of a floating rectangle.</summary>
public enum PdfFlowFloatSide
{
    /// <summary>Use the physical left side.</summary>
    Left,
    /// <summary>Use the physical right side.</summary>
    Right
}

/// <summary>Which preceding floats a block must pass.</summary>
public enum PdfFlowClear
{
    /// <summary>Pass no additional floats.</summary>
    None,
    /// <summary>Use the physical left side.</summary>
    Left,
    /// <summary>Use the physical right side.</summary>
    Right,
    /// <summary>Pass floats on both physical sides.</summary>
    Both
}

/// <summary>Immutable shaping, font size and color for a logical text run.</summary>
public sealed class PdfFlowTextStyle
{
    /// <summary>Captures explicit fonts, physical size, horizontal shaping options and color.</summary>
    public PdfFlowTextStyle(PdfFontFallback fonts, double fontSize = 12, PdfTextOptions? textOptions = null, PdfColor? color = null, double baselineShift = 0)
    {
        Fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        PdfGraphicsNumbers.Finite(fontSize);
        if (fontSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        PdfGraphicsNumbers.Finite(baselineShift);
        BaselineShift = baselineShift;
        FontSize = fontSize;
        var o = textOptions ?? new PdfTextOptions();
        if (o.Vertical)
            throw new ArgumentException("Horizontal paragraph flow requires horizontal shaping options.", nameof(textOptions));
        TextOptions = new(o.ParagraphDirection, false, o.Language, o.Features.ToArray());
        Color = color ?? new PdfColor(PdfColorSpace.Gray, 0);
        if (Color.ColorSpace.IsPattern)
            throw new ArgumentException("A text style requires an explicit non-pattern color.", nameof(color));
    }

    /// <summary>Gets the explicit immutable font preference order.</summary>
    public PdfFontFallback Fonts { get; }
    /// <summary>Gets the typographic size in physical points.</summary>
    public double FontSize { get; }
    /// <summary>Gets a finite physical baseline raise, in points.</summary>
    public double BaselineShift { get; }
    /// <summary>Gets immutable language, direction and shaping feature choices.</summary>
    public PdfTextOptions TextOptions { get; }
    /// <summary>Gets the explicit text paint color.</summary>
    public PdfColor Color { get; }
}

/// <summary>A logical UTF-16 run. A style boundary may not split an extended grapheme.</summary>
public sealed class PdfFlowRun
{
    /// <summary>Captures logical UTF-16 text and its immutable style.</summary>
    public PdfFlowRun(string text, PdfFlowTextStyle style)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        if (text.Length > 1000000)
            throw new ArgumentException("A flow run is limited to one million UTF-16 units.", nameof(text));
        Style = style ?? throw new ArgumentNullException(nameof(style));
    }

    /// <summary>Gets unchanged logical Unicode.</summary>
    public string Text { get; }
    /// <summary>Gets the immutable formatting policy.</summary>
    public PdfFlowTextStyle Style { get; }
}

/// <summary>Immutable paragraph geometry and pagination constraints. Distances are physical points.</summary>
public sealed class PdfFlowParagraphStyle
{
    /// <summary>Captures finite paragraph geometry and explicit pagination constraints.</summary>
    public PdfFlowParagraphStyle(PdfFlowAlignment alignment = PdfFlowAlignment.Start, double lineHeight = 1.2, double spaceBefore = 0, double spaceAfter = 0, double leftIndent = 0, double rightIndent = 0, double firstLineIndent = 0, int orphanLines = 2, int widowLines = 2, bool keepTogether = false, bool keepWithNext = false, bool pageBreakBefore = false, PdfFlowClear clear = PdfFlowClear.None)
    {
        if (!Enum.IsDefined(typeof(PdfFlowAlignment), alignment))
            throw new ArgumentOutOfRangeException(nameof(alignment));
        if (!Enum.IsDefined(typeof(PdfFlowClear), clear))
            throw new ArgumentOutOfRangeException(nameof(clear));
        foreach (double value in new[]
        {
            lineHeight,
            spaceBefore,
            spaceAfter,
            leftIndent,
            rightIndent,
            firstLineIndent
        }

        )
            PdfGraphicsNumbers.Finite(value);
        if (lineHeight <= 0 || spaceBefore < 0 || spaceAfter < 0 || leftIndent < 0 || rightIndent < 0 || firstLineIndent < 0)
            throw new ArgumentOutOfRangeException(nameof(lineHeight));
        if (orphanLines < 1 || widowLines < 1 || orphanLines > 10000 || widowLines > 10000)
            throw new ArgumentOutOfRangeException(nameof(orphanLines));
        Alignment = alignment;
        LineHeight = lineHeight;
        SpaceBefore = spaceBefore;
        SpaceAfter = spaceAfter;
        LeftIndent = leftIndent;
        RightIndent = rightIndent;
        FirstLineIndent = firstLineIndent;
        OrphanLines = orphanLines;
        WidowLines = widowLines;
        KeepTogether = keepTogether;
        KeepWithNext = keepWithNext;
        PageBreakBefore = pageBreakBefore;
        Clear = clear;
    }

    /// <summary>Gets horizontal alignment within the available interval.</summary>
    public PdfFlowAlignment Alignment { get; }
    /// <summary>Gets the minimum line-height multiplier; font ink envelopes can require more space.</summary>
    public double LineHeight { get; }
    /// <summary>Gets nonnegative spacing before the paragraph in points.</summary>
    public double SpaceBefore { get; }
    /// <summary>Gets nonnegative spacing after the paragraph in points.</summary>
    public double SpaceAfter { get; }
    /// <summary>Gets physical left indentation in points.</summary>
    public double LeftIndent { get; }
    /// <summary>Gets physical right indentation in points.</summary>
    public double RightIndent { get; }
    /// <summary>Gets additional physical first-line indentation in points.</summary>
    public double FirstLineIndent { get; }
    /// <summary>Gets the minimum lines on the first page of a split paragraph.</summary>
    public int OrphanLines { get; }
    /// <summary>Gets the minimum lines on the last page of a split paragraph.</summary>
    public int WidowLines { get; }
    /// <summary>Gets whether the complete paragraph must fit one page.</summary>
    public bool KeepTogether { get; }
    /// <summary>Gets whether this paragraph must share a page with the next paragraph opening.</summary>
    public bool KeepWithNext { get; }
    /// <summary>Gets whether a nonempty current page ends before this paragraph.</summary>
    public bool PageBreakBefore { get; }
    /// <summary>Gets which preceding floats the block must pass.</summary>
    public PdfFlowClear Clear { get; }
}

/// <summary>Base of immutable managed flow blocks.</summary>
public abstract class PdfFlowBlock
{
    internal PdfFlowBlock()
    {
    }
}

/// <summary>A paragraph with logical run order and explicit pagination rules.</summary>
public sealed class PdfFlowParagraph : PdfFlowBlock
{
    /// <summary>Captures a nonempty style-bearing run collection or one styled text paragraph.</summary>
    public PdfFlowParagraph(IEnumerable<PdfFlowRun> runs, PdfFlowParagraphStyle? style)
    {
        if (runs == null)
            throw new ArgumentNullException(nameof(runs));
        var copy = new List<PdfFlowRun>();
        foreach (var run in runs)
        {
            if (run == null || copy.Count == 4096)
                throw new ArgumentException("A paragraph accepts up to 4096 nonnull runs.", nameof(runs));
            copy.Add(run);
        }

        if (copy.Count == 0)
            throw new ArgumentException("An empty paragraph still requires a text style.", nameof(runs));
        Runs = copy.AsReadOnly();
        Style = style ?? new PdfFlowParagraphStyle();
    }

    /// <summary>Captures a nonempty style-bearing run collection or one styled text paragraph.</summary>
    public PdfFlowParagraph(IEnumerable<PdfFlowRun> runs) : this(runs, null)
    {
    }

    /// <summary>Captures a nonempty style-bearing run collection or one styled text paragraph.</summary>
    public PdfFlowParagraph(string text, PdfFlowTextStyle textStyle, PdfFlowParagraphStyle? style = null) : this(new[] { new PdfFlowRun(text, textStyle) }, style)
    {
    }

    /// <summary>Gets immutable logical runs in source order.</summary>
    public IReadOnlyList<PdfFlowRun> Runs { get; }
    /// <summary>Gets the immutable formatting policy.</summary>
    public PdfFlowParagraphStyle Style { get; }
}

/// <summary>An explicit page boundary, including an intentional blank page when already at a page start.</summary>
public sealed class PdfFlowPageBreak : PdfFlowBlock
{
    /// <summary>Creates an intentional page boundary.</summary>
    public PdfFlowPageBreak()
    {
    }
}

/// <summary>A reusable vector float excluding subsequent text from its rectangle.</summary>
public sealed class PdfFlowFloat : PdfFlowBlock
{
    /// <summary>Captures a finite vector float and physical exclusion rectangle.</summary>
    public PdfFlowFloat(PdfDrawing drawing, PdfRect sourceBounds, double width, double height, PdfFlowFloatSide side = PdfFlowFloatSide.Left, double gap = 6, PdfFlowClear clear = PdfFlowClear.None)
    {
        Drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
        foreach (double value in new[]
        {
            width,
            height,
            gap
        }

        )
            PdfGraphicsNumbers.Finite(value);
        if (width <= 0 || height <= 0 || gap < 0 || sourceBounds.Width <= 0 || sourceBounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (!Enum.IsDefined(typeof(PdfFlowFloatSide), side) || !Enum.IsDefined(typeof(PdfFlowClear), clear))
            throw new ArgumentOutOfRangeException(nameof(side));
        SourceBounds = sourceBounds;
        Width = width;
        Height = height;
        Side = side;
        Gap = gap;
        Clear = clear;
    }

    /// <summary>Gets the immutable reusable vector drawing.</summary>
    public PdfDrawing Drawing { get; }
    /// <summary>Gets its source coordinate frame.</summary>
    public PdfRect SourceBounds { get; }
    /// <summary>Gets the displayed width in physical points.</summary>
    public double Width { get; }
    /// <summary>Gets the displayed height in physical points.</summary>
    public double Height { get; }
    /// <summary>Gets its physical exclusion side.</summary>
    public PdfFlowFloatSide Side { get; }
    /// <summary>Gets the text exclusion gap in points.</summary>
    public double Gap { get; }
    /// <summary>Gets which preceding floats the block must pass.</summary>
    public PdfFlowClear Clear { get; }
}

/// <summary>Immutable page frame and explicit overflow/tab settings for a complete layout operation.</summary>
public sealed class PdfFlowOptions
{
    /// <summary>Captures a positive displayed page frame, overflow policy and explicit tab geometry.</summary>
    public PdfFlowOptions(PdfPageGeometry geometry, PdfMargins? margins = null, PdfFlowOverflow overflow = PdfFlowOverflow.Error, int maximumPages = 10000, double tabInterval = 36, double[]? tabStops = null)
    {
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        Margins = margins ?? PdfMargins.Uniform(36);
        if (!Enum.IsDefined(typeof(PdfFlowOverflow), overflow))
            throw new ArgumentOutOfRangeException(nameof(overflow));
        if (maximumPages < 1 || maximumPages > 1000000)
            throw new ArgumentOutOfRangeException(nameof(maximumPages));
        PdfGraphicsNumbers.Finite(tabInterval);
        if (tabInterval <= 0)
            throw new ArgumentOutOfRangeException(nameof(tabInterval));
        var stops = tabStops ?? Array.Empty<double>();
        if (stops.Length > 4096)
            throw new ArgumentException("At most 4096 explicit tab stops are allowed.", nameof(tabStops));
        double previous = 0;
        foreach (double stop in stops)
        {
            PdfGraphicsNumbers.Finite(stop);
            if (stop <= previous)
                throw new ArgumentException("Tab stops must be positive and increasing.", nameof(tabStops));
            previous = stop;
        }

        TabStops = new ReadOnlyCollection<double>((double[])stops.Clone());
        Overflow = overflow;
        MaximumPages = maximumPages;
        TabInterval = tabInterval;
        var display = geometry.DisplayBounds();
        Frame = new(Margins.Left, Margins.Top, display.Width - Margins.Left - Margins.Right, display.Height - Margins.Top - Margins.Bottom);
        if (Frame.Width <= 0 || Frame.Height <= 0)
            throw new ArgumentException("Flow margins require a positive page frame.", nameof(margins));
    }

    /// <summary>Gets immutable page boxes, user scale and viewing rotation.</summary>
    public PdfPageGeometry Geometry { get; }
    /// <summary>Gets displayed margins in physical points.</summary>
    public PdfMargins Margins { get; }
    /// <summary>Gets the explicit overlong-word policy.</summary>
    public PdfFlowOverflow Overflow { get; }
    /// <summary>Gets the layout page-count ceiling.</summary>
    public int MaximumPages { get; }
    /// <summary>Gets the repeating physical tab interval after the final explicit stop.</summary>
    public double TabInterval { get; }
    /// <summary>Gets ordered explicit left-to-right physical tab stops relative to the line start.</summary>
    public IReadOnlyList<double> TabStops { get; }
    /// <summary>Gets the displayed page content frame in physical points.</summary>
    public PdfRect Frame { get; }

    internal double NextTab(double x)
    {
        foreach (double stop in TabStops)
            if (stop > x + 1e-9)
                return stop;
        double origin = TabStops.Count == 0 ? 0 : TabStops[TabStops.Count - 1];
        return origin + (Math.Floor((x - origin) / TabInterval) + 1) * TabInterval;
    }
}
