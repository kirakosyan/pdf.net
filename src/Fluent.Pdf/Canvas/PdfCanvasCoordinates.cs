namespace Fluent.Pdf;
/// <summary>Explicit lengths used in a canvas coordinate frame.</summary>
public enum PdfLengthUnit
{
    /// <summary>One seventy-second of an inch.</summary>
    Point,
    /// <summary>One inch.</summary>
    Inch,
    /// <summary>One millimeter.</summary>
    Millimeter,
    /// <summary>One centimeter.</summary>
    Centimeter,
    /// <summary>Twelve points.</summary>
    Pica,
    /// <summary>A pixel at the caller's explicit DPI setting.</summary>
    Pixel
}

/// <summary>Explicit physical length conversion; no display/device resolution is queried.</summary>
public static class PdfUnits
{
    /// <summary>Gets points per selected unit. Pixel DPI defaults explicitly to 96 and must lie between 1 and 1000000.</summary>
    public static double PointsPerUnit(PdfLengthUnit unit, double dpi = 96)
    {
        PdfGraphicsNumbers.Finite(dpi);
        if (dpi < 1 || dpi > 1000000)
            throw new ArgumentOutOfRangeException(nameof(dpi));
        return unit switch
        {
            PdfLengthUnit.Point => 1,
            PdfLengthUnit.Inch => 72,
            PdfLengthUnit.Millimeter => 72 / 25.4,
            PdfLengthUnit.Centimeter => 72 / 2.54,
            PdfLengthUnit.Pica => 12,
            PdfLengthUnit.Pixel => 72 / dpi,
            _ => throw new ArgumentOutOfRangeException(nameof(unit))};
    }

    /// <summary>Converts a finite length and rejects nonfinite arithmetic.</summary>
    public static double Convert(double value, PdfLengthUnit from, PdfLengthUnit to, double dpi = 96)
    {
        PdfGraphicsNumbers.Finite(value);
        double result = value * PointsPerUnit(from, dpi) / PointsPerUnit(to, dpi);
        PdfGraphicsNumbers.Finite(result);
        return result;
    }
}

/// <summary>Canvas axes relative to the displayed selected box.</summary>
public enum PdfCanvasOrigin
{
    /// <summary>Origin at the top-left content corner, with x rightward and y downward.</summary>
    TopLeft,
    /// <summary>Origin at the bottom-left content corner, with x rightward and y upward.</summary>
    BottomLeft
}

/// <summary>Finite nonnegative content margins, expressed in the canvas unit.</summary>
public readonly struct PdfMargins
{
    /// <summary>Captures left/top/right/bottom margins.</summary>
    public PdfMargins(double left, double top, double right, double bottom)
    {
        foreach (double value in new[]
        {
            left,
            top,
            right,
            bottom
        }

        )
        {
            PdfGraphicsNumbers.Finite(value);
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(left));
        }

        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>Gets the left margin.</summary>
    public double Left { get; }
    /// <summary>Gets the top margin.</summary>
    public double Top { get; }
    /// <summary>Gets the right margin.</summary>
    public double Right { get; }
    /// <summary>Gets the bottom margin.</summary>
    public double Bottom { get; }

    /// <summary>Creates equal margins.</summary>
    public static PdfMargins Uniform(double value) => new(value, value, value, value);
}

/// <summary>Immutable explicit canvas coordinate and clipping settings.</summary>
public sealed class PdfCanvasOptions
{
    /// <summary>Captures units, viewing-box/origin selection, margins and the default content-frame clip.</summary>
    public PdfCanvasOptions(PdfLengthUnit unit = PdfLengthUnit.Point, PdfCanvasOrigin origin = PdfCanvasOrigin.TopLeft, PdfPageBox box = PdfPageBox.Crop, PdfMargins margins = default, bool clipToContent = true, double dpi = 96)
    {
        PointsPerUnit = PdfUnits.PointsPerUnit(unit, dpi);
        if (!Enum.IsDefined(typeof(PdfCanvasOrigin), origin))
            throw new ArgumentOutOfRangeException(nameof(origin));
        if (!Enum.IsDefined(typeof(PdfPageBox), box))
            throw new ArgumentOutOfRangeException(nameof(box));
        Unit = unit;
        Origin = origin;
        Box = box;
        Margins = margins;
        ClipToContent = clipToContent;
        Dpi = dpi;
    }

    /// <summary>Gets the physical coordinate unit.</summary>
    public PdfLengthUnit Unit { get; }
    /// <summary>Gets the explicit origin/axis orientation.</summary>
    public PdfCanvasOrigin Origin { get; }
    /// <summary>Gets the selected standard viewing box.</summary>
    public PdfPageBox Box { get; }
    /// <summary>Gets margins in canvas units.</summary>
    public PdfMargins Margins { get; }
    /// <summary>Gets whether painting is clipped to the margin content frame.</summary>
    public bool ClipToContent { get; }
    /// <summary>Gets the explicit pixel conversion DPI.</summary>
    public double Dpi { get; }
    internal double PointsPerUnit { get; }
}
