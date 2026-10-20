namespace Fluent.Pdf;
/// <summary>An SDK-validated integer raster plan. Providers paint only the requested window of the full-page pixel grid.</summary>
public sealed class PdfRasterRequest
{
    internal PdfRasterRequest(int page, PdfPageSource source, PdfRenderOptions options, PdfOperationContext context)
    {
        PageIndex = page;
        Page = source;
        Options = options;
        var bounds = source.Geometry.DisplayBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw PdfWriterCore.Error(context, "Render.EmptyPage", "An empty effective CropBox cannot be rasterized.");
        FullWidth = Extent(bounds.Width, options.DpiX);
        FullHeight = Extent(bounds.Height, options.DpiY);
        EffectiveDpiX = FullWidth / bounds.Width * 72;
        EffectiveDpiY = FullHeight / bounds.Height * 72;
        var region = options.Region ?? bounds;
        if (!PdfPageBoxes.Contains(bounds, region))
            throw PdfWriterCore.Error(context, "Render.Region", "The requested displayed region must lie within the effective CropBox.");
        double sx = FullWidth / bounds.Width, sy = FullHeight / bounds.Height;
        X = (int)Math.Floor(region.X * sx);
        Y = (int)Math.Floor(region.Y * sy);
        int right = (int)Math.Min(FullWidth, Math.Ceiling(region.Right * sx)), bottom = (int)Math.Min(FullHeight, Math.Ceiling(region.Top * sy));
        if (options.PixelWindow is { } window)
        {
            X = window.X;
            Y = window.Y;
            right = checked(X + window.Width);
            bottom = checked(Y + window.Height);
            if (X < 0 || Y < 0 || right > FullWidth || bottom > FullHeight)
                throw PdfWriterCore.Error(context, "Render.TileGrid", "The planned tile is outside its exact page grid.");
        }
        Width = right - X;
        Height = bottom - Y;
        if (Width <= 0 || Height <= 0 || Width > int.MaxValue / 4 || (long)Width * Height > (int.MaxValue - 32L) / 4)
            throw PdfWriterCore.Error(context, "Render.Extent", "The requested raster exceeds the supported contiguous output extent.");
        Stride = Width * 4;
        ByteLength = (long)Stride * Height;
        Region = new(X / sx, Y / sy, Width / sx, Height / sy);
        int Extent(double points, double dpi)
        {
            double n = Math.Ceiling(points * dpi / 72);
            if (double.IsInfinity(n) || n < 1 || n > int.MaxValue)
                throw PdfWriterCore.Error(context, "Render.Extent", "The full-page pixel grid exceeds the supported integer extent.");
            return (int)n;
        }
    }

    /// <summary>Gets the zero-based page index in the prepared document.</summary>
    public int PageIndex
    {
        get;
    }
    /// <summary>Gets the exact immutable prepared page binding corresponding to PageIndex.</summary>
    public PdfPageSource Page
    {
        get;
    }
    /// <summary>Gets immutable original choices.</summary>
    public PdfRenderOptions Options
    {
        get;
    }
    /// <summary>Gets full rotated CropBox grid width.</summary>
    public int FullWidth
    {
        get;
    }
    /// <summary>Gets full rotated CropBox grid height.</summary>
    public int FullHeight
    {
        get;
    }
    /// <summary>Gets the window's left pixel on the full-page grid.</summary>
    public int X
    {
        get;
    }
    /// <summary>Gets the window's top pixel on the full-page grid.</summary>
    public int Y
    {
        get;
    }
    /// <summary>Gets output width.</summary>
    public int Width
    {
        get;
    }
    /// <summary>Gets output height.</summary>
    public int Height
    {
        get;
    }
    /// <summary>Gets tightly packed row size in bytes.</summary>
    public int Stride
    {
        get;
    }
    /// <summary>Gets exact BGRA sample extent.</summary>
    public long ByteLength
    {
        get;
    }
    /// <summary>Gets actual horizontal DPI after full-page integer ceiling.</summary>
    public double EffectiveDpiX
    {
        get;
    }
    /// <summary>Gets actual vertical DPI after full-page integer ceiling.</summary>
    public double EffectiveDpiY
    {
        get;
    }
    /// <summary>Gets the effective outward-snapped region in displayed physical points.</summary>
    public PdfRect Region
    {
        get;
    }
}
