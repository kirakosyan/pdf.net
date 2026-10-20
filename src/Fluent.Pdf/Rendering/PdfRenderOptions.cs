namespace Fluent.Pdf;
/// <summary>Device-color output selection; grayscale retains three equal color channels.</summary>
public enum PdfRasterColor
{
    /// <summary>Device RGB output.</summary>
    Rgb,
    /// <summary>Device grayscale output in a BGRA envelope.</summary>
    Gray
}

/// <summary>Immutable headless rendering choices in displayed CropBox coordinates.</summary>
public sealed class PdfRenderOptions
{
    /// <summary>Captures requested DPI and an optional top-left physical-point region. Null background requests straight alpha; an RGB background is opaque.</summary>
    public PdfRenderOptions(double dpiX = 96, double dpiY = 96, PdfRect? region = null, uint? backgroundRgb = 0xffffff, PdfRasterColor color = PdfRasterColor.Rgb, bool annotations = true, bool print = false)
    {
        if (!Finite(dpiX) || dpiX <= 0 || dpiX > 9600)
            throw new ArgumentOutOfRangeException(nameof(dpiX));
        if (!Finite(dpiY) || dpiY <= 0 || dpiY > 9600)
            throw new ArgumentOutOfRangeException(nameof(dpiY));
        if (region.HasValue && (region.Value.Width <= 0 || region.Value.Height <= 0))
            throw new ArgumentException("A region must have positive extent.", nameof(region));
        if (backgroundRgb > 0xffffff)
            throw new ArgumentOutOfRangeException(nameof(backgroundRgb));
        if (!Enum.IsDefined(typeof(PdfRasterColor), color))
            throw new ArgumentOutOfRangeException(nameof(color));
        if (color == PdfRasterColor.Gray && backgroundRgb.HasValue && (((backgroundRgb.Value >> 16) & 255) != ((backgroundRgb.Value >> 8) & 255) || ((backgroundRgb.Value >> 8) & 255) != (backgroundRgb.Value & 255)))
            throw new ArgumentException("A device-gray raster requires a gray matte.", nameof(backgroundRgb));
        DpiX = dpiX;
        DpiY = dpiY;
        Region = region;
        BackgroundRgb = backgroundRgb;
        Color = color;
        Annotations = annotations;
        Print = print;
    }

    /// <summary>Gets requested horizontal dots per physical inch.</summary>
    public double DpiX { get; }
    /// <summary>Gets requested vertical dots per physical inch.</summary>
    public double DpiY { get; }
    /// <summary>Gets the optional region before outward pixel snapping.</summary>
    public PdfRect? Region { get; }
    /// <summary>Gets opaque 0xRRGGBB matte, or null for transparency.</summary>
    public uint? BackgroundRgb { get; }
    /// <summary>Gets device-color selection.</summary>
    public PdfRasterColor Color { get; }
    /// <summary>Gets whether annotation and existing widget appearances are painted.</summary>
    public bool Annotations { get; }
    /// <summary>Gets print intent, including print visibility and optional-content usage.</summary>
    public bool Print { get; }

    private static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
}
