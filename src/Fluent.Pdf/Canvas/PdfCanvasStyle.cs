using System.Collections.ObjectModel;

namespace Fluent.Pdf;
/// <summary>An explicit solid color or colored/uncolored pattern paint.</summary>
public sealed class PdfPaint
{
    private PdfPaint(PdfColor? color, PdfPattern? pattern)
    {
        Color = color;
        Pattern = pattern;
    }

    /// <summary>Gets the exact solid color or uncolored pattern base color.</summary>
    public PdfColor? Color { get; }
    /// <summary>Gets the optional exact pattern.</summary>
    public PdfPattern? Pattern { get; }

    /// <summary>Captures a non-pattern solid color.</summary>
    public static PdfPaint Solid(PdfColor color)
    {
        if (color == null)
            throw new ArgumentNullException(nameof(color));
        if (color.ColorSpace.IsPattern)
            throw new ArgumentException("Use the explicit pattern paint factory.", nameof(color));
        return new(color, null);
    }

    /// <summary>Captures a pattern and its required base color for an uncolored cell.</summary>
    public static PdfPaint PatternFill(PdfPattern pattern, PdfColor? baseColor = null)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));
        if (pattern.Colored ? baseColor != null : baseColor == null || baseColor.ColorSpace.IsPattern)
            throw new ArgumentException("A colored pattern supplies its colors; an uncolored pattern requires an exact non-pattern base color.", nameof(baseColor));
        return new(baseColor, pattern);
    }

    /// <summary>Gets immutable device black.</summary>
    public static PdfPaint Black { get; } = Solid(new(PdfColorSpace.Gray, 0));
}

/// <summary>Standard PDF line cap behavior.</summary>
public enum PdfLineCap
{
    /// <summary>Butt cap.</summary>
    Butt,
    /// <summary>Round cap.</summary>
    Round,
    /// <summary>Projecting square cap.</summary>
    Square
}

/// <summary>Standard PDF line join behavior.</summary>
public enum PdfLineJoin
{
    /// <summary>Miter join subject to its explicit limit.</summary>
    Miter,
    /// <summary>Round join.</summary>
    Round,
    /// <summary>Bevel join.</summary>
    Bevel
}

/// <summary>Immutable vector paint/stroke choices in canvas units.</summary>
public sealed class PdfCanvasStyle
{
    /// <summary>Captures at least one paint, finite line geometry and an optional exact compositing state.</summary>
    public PdfCanvasStyle(PdfPaint? fill = null, PdfPaint? stroke = null, double lineWidth = 1, PdfLineCap lineCap = PdfLineCap.Butt, PdfLineJoin lineJoin = PdfLineJoin.Miter, double miterLimit = 10, double[]? dash = null, double dashPhase = 0, bool evenOdd = false, PdfGraphicsState? state = null)
    {
        if (fill == null && stroke == null)
            throw new ArgumentException("At least one fill or stroke paint is required.");
        PdfGraphicsNumbers.Finite(lineWidth);
        PdfGraphicsNumbers.Finite(miterLimit);
        PdfGraphicsNumbers.Finite(dashPhase);
        if (lineWidth < 0 || miterLimit < 1)
            throw new ArgumentOutOfRangeException(nameof(lineWidth));
        if (!Enum.IsDefined(typeof(PdfLineCap), lineCap) || !Enum.IsDefined(typeof(PdfLineJoin), lineJoin))
            throw new ArgumentOutOfRangeException(nameof(lineCap));
        dash ??= System.Array.Empty<double>();
        if (dash.Length > 64)
            throw new ArgumentException("At most 64 dash entries are allowed.", nameof(dash));
        foreach (double value in dash)
        {
            PdfGraphicsNumbers.Finite(value);
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(dash));
        }

        if (dash.Length > 0 && dash.All(x => x == 0))
            throw new ArgumentException("A nonempty dash pattern must advance.", nameof(dash));
        Fill = fill;
        Stroke = stroke;
        LineWidth = lineWidth;
        LineCap = lineCap;
        LineJoin = lineJoin;
        MiterLimit = miterLimit;
        Dash = new ReadOnlyCollection<double>((double[])dash.Clone());
        DashPhase = dashPhase;
        EvenOdd = evenOdd;
        State = state;
    }

    /// <summary>Gets optional fill paint.</summary>
    public PdfPaint? Fill { get; }
    /// <summary>Gets optional stroke paint.</summary>
    public PdfPaint? Stroke { get; }
    /// <summary>Gets line width in canvas units; zero requests a device hairline.</summary>
    public double LineWidth { get; }
    /// <summary>Gets line cap behavior.</summary>
    public PdfLineCap LineCap { get; }
    /// <summary>Gets line join behavior.</summary>
    public PdfLineJoin LineJoin { get; }
    /// <summary>Gets the explicit miter ratio limit.</summary>
    public double MiterLimit { get; }
    /// <summary>Gets immutable dash lengths in canvas units.</summary>
    public IReadOnlyList<double> Dash { get; }
    /// <summary>Gets dash phase in canvas units.</summary>
    public double DashPhase { get; }
    /// <summary>Gets whether filling uses the even-odd rule.</summary>
    public bool EvenOdd { get; }
    /// <summary>Gets optional exact compositing state.</summary>
    public PdfGraphicsState? State { get; }
    /// <summary>Gets the default black-fill style.</summary>
    public static PdfCanvasStyle Default { get; } = new(PdfPaint.Black);
}

/// <summary>Explicit placement-box fitting policy.</summary>
public enum PdfCanvasFit
{
    /// <summary>Use the full destination dimensions.</summary>
    Stretch,
    /// <summary>Preserve aspect and fit completely inside the destination.</summary>
    Contain,
    /// <summary>Preserve aspect and cover the destination, clipping overflow.</summary>
    Cover
}

/// <summary>Displayed alignment of a fitted object inside its destination box.</summary>
public enum PdfCanvasAnchor
{
    /// <summary>Top left.</summary>
    TopLeft,
    /// <summary>Top center.</summary>
    Top,
    /// <summary>Top right.</summary>
    TopRight,
    /// <summary>Middle left.</summary>
    Left,
    /// <summary>Center.</summary>
    Center,
    /// <summary>Middle right.</summary>
    Right,
    /// <summary>Bottom left.</summary>
    BottomLeft,
    /// <summary>Bottom center.</summary>
    Bottom,
    /// <summary>Bottom right.</summary>
    BottomRight
}
