namespace Fluent.Pdf;
/// <summary>An explicit source-raster pixel frame bound to exact PDF crop, rotation and UserUnit geometry.</summary>
public sealed class PdfOcrPageFrame
{
    /// <summary>Maps top-left raster pixels onto an explicit displayed CropBox region, in physical points. Omitted region selects the entire rotated crop.</summary>
    public PdfOcrPageFrame(PdfPageGeometry geometry, int rasterWidth, int rasterHeight, PdfRect? displayedRegion = null)
    {
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        if (rasterWidth < 1 || rasterHeight < 1 || rasterWidth > 100000 || rasterHeight > 100000)
            throw new ArgumentOutOfRangeException(nameof(rasterWidth));
        var bounds = geometry.DisplayBounds();
        var region = displayedRegion ?? bounds;
        if (region.Width <= 0 || region.Height <= 0 || !PdfPageBoxes.Contains(bounds, region))
            throw new ArgumentException("The explicit raster region must lie within the displayed crop.", nameof(displayedRegion));
        RasterWidth = rasterWidth;
        RasterHeight = rasterHeight;
        DisplayedRegion = region;
        PixelToUser = geometry.DisplayToUser().Multiply(new(region.Width / rasterWidth, 0, 0, region.Height / rasterHeight, region.X, region.Y));
    }

    /// <summary>Gets exact source page geometry; applying to another geometry rejects.</summary>
    public PdfPageGeometry Geometry { get; }
    /// <summary>Gets exact raster width.</summary>
    public int RasterWidth { get; }
    /// <summary>Gets exact raster height.</summary>
    public int RasterHeight { get; }
    /// <summary>Gets explicit top-left displayed physical-point region.</summary>
    public PdfRect DisplayedRegion { get; }
    /// <summary>Gets the exact pixel-to-native-PDF affine transform.</summary>
    public PdfMatrix PixelToUser { get; }
}

/// <summary>Explicit handling of sufficiently overlapping, mapped existing digital words.</summary>
public enum PdfOcrExistingTextPolicy
{
    /// <summary>Skip OCR words covered by existing mapped digital words, retaining the original layer.</summary>
    SkipOverlapping,
    /// <summary>Reject a sufficiently overlapping existing mapped word.</summary>
    RejectOverlap,
    /// <summary>Explicitly permit duplicate text layers.</summary>
    AllowDuplicates
}

/// <summary>Finite invisible-text placement and explicit existing-layer policy. No OCR confidence establishes digital-text adequacy.</summary>
public sealed class PdfOcrOverlayOptions
{
    /// <summary>Captures overlap coverage required on both word rectangles, finite overlay count and total overlap-comparison work.</summary>
    public PdfOcrOverlayOptions(PdfOcrExistingTextPolicy existingTextPolicy = PdfOcrExistingTextPolicy.SkipOverlapping, double overlapCoverage = 0.8, int maximumWords = 10000, long maximumOverlapComparisons = 1000000)
    {
        if (!Enum.IsDefined(typeof(PdfOcrExistingTextPolicy), existingTextPolicy))
            throw new ArgumentOutOfRangeException(nameof(existingTextPolicy));
        if (double.IsNaN(overlapCoverage) || double.IsInfinity(overlapCoverage) || overlapCoverage <= 0 || overlapCoverage > 1)
            throw new ArgumentOutOfRangeException(nameof(overlapCoverage));
        if (maximumWords < 1 || maximumWords > 100000 || maximumOverlapComparisons < 1 || maximumOverlapComparisons > 1000000000)
            throw new ArgumentOutOfRangeException(nameof(maximumWords));
        ExistingTextPolicy = existingTextPolicy;
        OverlapCoverage = overlapCoverage;
        MaximumWords = maximumWords;
        MaximumOverlapComparisons = maximumOverlapComparisons;
    }

    /// <summary>Gets the explicitly selected duplicate-layer policy.</summary>
    public PdfOcrExistingTextPolicy ExistingTextPolicy { get; }
    /// <summary>Gets minimum area coverage on both existing/OCR word rectangles.</summary>
    public double OverlapCoverage { get; }
    /// <summary>Gets maximum recognized words considered.</summary>
    public int MaximumWords { get; }
    /// <summary>Gets total finite digital/OCR word comparison ceiling.</summary>
    public long MaximumOverlapComparisons { get; }
}
