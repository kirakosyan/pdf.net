namespace Fluent.Pdf;

/// <summary>Explicit full-resolution blank-page classifier parameters. No page is removed.</summary>
public sealed class PdfBlankPageOptions
{
    /// <summary>Captures an opaque RGB background, maximum per-channel deviation and allowed foreground fraction. Confidence is a measured background fraction, not a correctness probability.</summary>
    public PdfBlankPageOptions(int backgroundRgb = 0xffffff, byte channelTolerance = 12, double maximumForegroundFraction = 0.001)
    {
        if (backgroundRgb < 0 || backgroundRgb > 0xffffff) throw new ArgumentOutOfRangeException(nameof(backgroundRgb));
        if (double.IsNaN(maximumForegroundFraction) || maximumForegroundFraction < 0 || maximumForegroundFraction > 0.25) throw new ArgumentOutOfRangeException(nameof(maximumForegroundFraction));
        BackgroundRgb = backgroundRgb; ChannelTolerance = channelTolerance; MaximumForegroundFraction = maximumForegroundFraction;
    }
    /// <summary>Gets the explicit expected opaque RGB background.</summary>
    public int BackgroundRgb { get; }
    /// <summary>Gets the inclusive per-channel background tolerance.</summary>
    public byte ChannelTolerance { get; }
    /// <summary>Gets the largest measured foreground fraction classified as blank.</summary>
    public double MaximumForegroundFraction { get; }
}

/// <summary>Measured evidence from every active source pixel; padding is excluded and alpha is never guessed.</summary>
public sealed class PdfBlankPageObservation
{
    internal PdfBlankPageObservation(long count, long foreground, PdfBlankPageOptions options)
    { PixelCount = count; ForegroundPixels = foreground; Options = options; }
    /// <summary>Gets the exact classifier parameters.</summary>
    public PdfBlankPageOptions Options { get; }
    /// <summary>Gets the number of examined active pixels.</summary>
    public long PixelCount { get; }
    /// <summary>Gets pixels whose maximum RGB distance from the background exceeds tolerance.</summary>
    public long ForegroundPixels { get; }
    /// <summary>Gets the measured foreground fraction.</summary>
    public double ForegroundFraction => (double)ForegroundPixels / PixelCount;
    /// <summary>Gets one minus foreground fraction; this heuristic confidence is not calibrated correctness.</summary>
    public double Confidence => 1 - ForegroundFraction;
    /// <summary>Gets whether the caller's foreground threshold is met; this never authorizes deletion.</summary>
    public bool IsBlank => ForegroundFraction <= Options.MaximumForegroundFraction;
}

/// <summary>Immutable opt-in image cleanup. Defaults preserve the original raster exactly.</summary>
public sealed class PdfImageCleanupOptions
{
    /// <summary>Captures explicit loss authority, device-gray/filter choices, source border crop, nearest-neighbor output size and optional blank analysis. Resize dimensions must both be supplied. Median and threshold on color require explicit grayscale conversion.</summary>
    public PdfImageCleanupOptions(bool allowLossy = false, bool grayscale = false, bool medianDenoise = false, byte? binaryThreshold = null, int cropLeft = 0, int cropTop = 0, int cropRight = 0, int cropBottom = 0, int? outputWidth = null, int? outputHeight = null, PdfBlankPageOptions? blankDetection = null, long maximumPixelVisits = 100000000)
    {
        if (cropLeft < 0 || cropTop < 0 || cropRight < 0 || cropBottom < 0 || cropLeft > 100000 || cropTop > 100000 || cropRight > 100000 || cropBottom > 100000) throw new ArgumentOutOfRangeException(nameof(cropLeft));
        if (outputWidth.HasValue != outputHeight.HasValue || outputWidth.HasValue && (outputWidth < 1 || outputHeight < 1 || outputWidth > 100000 || outputHeight > 100000)) throw new ArgumentOutOfRangeException(nameof(outputWidth));
        if (maximumPixelVisits < 1 || maximumPixelVisits > 1000000000) throw new ArgumentOutOfRangeException(nameof(maximumPixelVisits));
        if (!allowLossy && (grayscale || medianDenoise || binaryThreshold.HasValue || cropLeft + cropTop + cropRight + cropBottom != 0 || outputWidth.HasValue)) throw new ArgumentException("Cleanup transforms require explicit lossy authority.", nameof(allowLossy));
        AllowLossy = allowLossy; Grayscale = grayscale; MedianDenoise = medianDenoise; BinaryThreshold = binaryThreshold;
        CropLeft = cropLeft; CropTop = cropTop; CropRight = cropRight; CropBottom = cropBottom;
        OutputWidth = outputWidth; OutputHeight = outputHeight; BlankDetection = blankDetection; MaximumPixelVisits = maximumPixelVisits;
    }
    /// <summary>Gets explicit authority for the selected lossy transforms.</summary>
    public bool AllowLossy { get; }
    /// <summary>Gets explicit integer device-luma conversion; no ICC transform is implied.</summary>
    public bool Grayscale { get; }
    /// <summary>Gets replicated-edge3x3 gray median filtering.</summary>
    public bool MedianDenoise { get; }
    /// <summary>Gets the gray threshold; values below it become zero, others255.</summary>
    public byte? BinaryThreshold { get; }
    /// <summary>Gets removed source columns from the left.</summary>
    public int CropLeft { get; }
    /// <summary>Gets removed source rows from the top.</summary>
    public int CropTop { get; }
    /// <summary>Gets removed source columns from the right.</summary>
    public int CropRight { get; }
    /// <summary>Gets removed source rows from the bottom.</summary>
    public int CropBottom { get; }
    /// <summary>Gets the optional final width.</summary>
    public int? OutputWidth { get; }
    /// <summary>Gets the optional final height.</summary>
    public int? OutputHeight { get; }
    /// <summary>Gets explicit blank analysis, performed before and after cleanup, or null.</summary>
    public PdfBlankPageOptions? BlankDetection { get; }
    /// <summary>Gets the cumulative pixel-visit ceiling across analysis and all transforms.</summary>
    public long MaximumPixelVisits { get; }
}

/// <summary>Preserved source, separate processed pixels and reproducible cleanup evidence.</summary>
public sealed class PdfImageCleanupResult
{
    internal PdfImageCleanupResult(PdfOcrRaster original, PdfOcrRaster processed, PdfImageCleanupOptions options, PdfMatrix inverse, PdfBlankPageObservation? before, PdfBlankPageObservation? after, long visits)
    { Original = original; Processed = processed; Options = options; OutputToOriginal = inverse; Before = before; After = after; PixelVisits = visits; }
    /// <summary>Gets exact immutable source including padding; reverting means using this raster.</summary>
    public PdfOcrRaster Original { get; }
    /// <summary>Gets processed pixels, or the original instance for an identity operation. DPI remains a nominal OCR hint; use OutputToOriginal for physical geometry.</summary>
    public PdfOcrRaster Processed { get; }
    /// <summary>Gets captured explicit parameters.</summary>
    public PdfImageCleanupOptions Options { get; }
    /// <summary>Gets processed pixel-edge coordinates mapped into original pixels.</summary>
    public PdfMatrix OutputToOriginal { get; }
    /// <summary>Gets full source blank analysis, never inferred from reduced output.</summary>
    public PdfBlankPageObservation? Before { get; }
    /// <summary>Gets separate output blank analysis; changes do not authorize deletion.</summary>
    public PdfBlankPageObservation? After { get; }
    /// <summary>Gets total managed pixel visits across all stages.</summary>
    public long PixelVisits { get; }
    /// <summary>Gets resource accounting and explicit loss diagnostics.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;
}
