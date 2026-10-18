namespace Fluent.Pdf;
/// <summary>Explicit opt-in scan orientation. Automatic compares four explicit OCR observations; it does not discover an orientation model.</summary>
public enum PdfScanOrientation
{
    /// <summary>Retain the original orientation.</summary>
    None,
    /// <summary>Rotate clockwise90 degrees.</summary>
    Clockwise90,
    /// <summary>Rotate180 degrees.</summary>
    Clockwise180,
    /// <summary>Rotate clockwise270 degrees.</summary>
    Clockwise270,
    /// <summary>Compare four explicitly configured OCR observations and require a prior confidence margin.</summary>
    Automatic
}

/// <summary>Immutable reproducible scan parameters. Every correction is opt-in; original samples remain available.</summary>
public sealed class PdfScanPreprocessingOptions
{
    /// <summary>Captures orientation, explicit or measured skew,3x3 median denoise, linear gray contrast, optional threshold/dewarp and finite pixel visits. Automatic orientation requires explicit OCR providers/options.</summary>
    public PdfScanPreprocessingOptions(PdfScanOrientation orientation = PdfScanOrientation.None, double skewDegrees = 0, bool detectSkew = false, double maximumSkewDegrees = 5, double skewStepDegrees = 0.5, int analysisStride = 2, bool medianDenoise = false, byte contrastLow = 0, byte contrastHigh = 255, byte? binaryThreshold = null, PdfScanDewarpGrid? dewarp = null, double minimumOrientationMargin = 0.05, double minimumSkewMargin = 0.01, long maximumPixelVisits = 100000000)
    {
        if (!Enum.IsDefined(typeof(PdfScanOrientation), orientation))
            throw new ArgumentOutOfRangeException(nameof(orientation));
        if (!Finite(skewDegrees) || Math.Abs(skewDegrees) > 15 || !Finite(maximumSkewDegrees) || maximumSkewDegrees <= 0 || maximumSkewDegrees > 15 || !Finite(skewStepDegrees) || skewStepDegrees < 0.1 || skewStepDegrees > maximumSkewDegrees || detectSkew && skewDegrees != 0)
            throw new ArgumentException("Explicit finite skew parameters are required; measured and explicit skew are mutually exclusive.");
        if (analysisStride < 1 || analysisStride > 64 || contrastLow >= contrastHigh)
            throw new ArgumentOutOfRangeException(nameof(analysisStride));
        if (!Finite(minimumOrientationMargin) || minimumOrientationMargin < 0 || minimumOrientationMargin > 1 || !Finite(minimumSkewMargin) || minimumSkewMargin < 0 || minimumSkewMargin > 1 || maximumPixelVisits < 1 || maximumPixelVisits > 1000000000)
            throw new ArgumentOutOfRangeException(nameof(maximumPixelVisits));
        Orientation = orientation;
        SkewDegrees = skewDegrees;
        DetectSkew = detectSkew;
        MaximumSkewDegrees = maximumSkewDegrees;
        SkewStepDegrees = skewStepDegrees;
        AnalysisStride = analysisStride;
        MedianDenoise = medianDenoise;
        ContrastLow = contrastLow;
        ContrastHigh = contrastHigh;
        BinaryThreshold = binaryThreshold;
        Dewarp = dewarp;
        MinimumOrientationMargin = minimumOrientationMargin;
        MinimumSkewMargin = minimumSkewMargin;
        MaximumPixelVisits = maximumPixelVisits;
    }

    /// <summary>Gets explicit orientation policy.</summary>
    public PdfScanOrientation Orientation { get; }
    /// <summary>Gets explicitly measured source skew; correction rotates by its negative.</summary>
    public double SkewDegrees { get; }
    /// <summary>Gets whether bounded row-projection skew analysis is requested.</summary>
    public bool DetectSkew { get; }
    /// <summary>Gets absolute skew search extent.</summary>
    public double MaximumSkewDegrees { get; }
    /// <summary>Gets reproducible search increment.</summary>
    public double SkewStepDegrees { get; }
    /// <summary>Gets analysis pixel stride; output resampling remains full resolution.</summary>
    public int AnalysisStride { get; }
    /// <summary>Gets whether a replicated-edge3x3 median filter is requested.</summary>
    public bool MedianDenoise { get; }
    /// <summary>Gets the gray value mapped to zero.</summary>
    public byte ContrastLow { get; }
    /// <summary>Gets the gray value mapped to255.</summary>
    public byte ContrastHigh { get; }
    /// <summary>Gets optional final binary threshold, with values below it mapped to zero.</summary>
    public byte? BinaryThreshold { get; }
    /// <summary>Gets explicit vertical-curvature dewarping control data, applied before orientation/skew analysis.</summary>
    public PdfScanDewarpGrid? Dewarp { get; }
    /// <summary>Gets minimum best-versus-second engine-confidence gap divided by100. It is not correctness probability.</summary>
    public double MinimumOrientationMargin { get; }
    /// <summary>Gets minimum relative best-versus-second projection-score gap. It is not correctness probability.</summary>
    public double MinimumSkewMargin { get; }
    /// <summary>Gets cumulative finite managed pixel-visit ceiling.</summary>
    public long MaximumPixelVisits { get; }

    private static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
}

/// <summary>One exact requested orientation observation, distinct from recognition correctness.</summary>
public sealed class PdfScanOrientationScore
{
    internal PdfScanOrientationScore(PdfScanOrientation orientation, double? confidence)
    {
        Orientation = orientation;
        EngineConfidence = confidence;
    }

    /// <summary>Gets the tested clockwise quarter-turn.</summary>
    public PdfScanOrientation Orientation { get; }
    /// <summary>Gets nullable engine mean confidence0–100.</summary>
    public double? EngineConfidence { get; }
}

/// <summary>Original immutable scan, corrected raster and complete inverse geometry/provenance.</summary>
public sealed class PdfScanPreprocessingResult
{
    internal PdfScanPreprocessingResult(PdfOcrRaster original, PdfOcrRaster processed, PdfScanPreprocessingOptions options, PdfScanGeometry geometry, PdfScanOrientation orientation, double skew, double? orientationMargin, double? skewMargin, PdfScanOrientationScore[] scores, long visits)
    {
        Original = original;
        Processed = processed;
        Options = options;
        Geometry = geometry;
        AppliedOrientation = orientation;
        SourceSkewDegrees = skew;
        OrientationMargin = orientationMargin;
        SkewMargin = skewMargin;
        OrientationScores = Array.AsReadOnly(scores);
        PixelVisits = visits;
    }

    /// <summary>Gets the exact original immutable raster; it is never overwritten.</summary>
    public PdfOcrRaster Original { get; }
    /// <summary>Gets corrected top-left Gray8 pixels, including explicit white padding after deskew.</summary>
    public PdfOcrRaster Processed { get; }
    /// <summary>Gets exact captured parameters.</summary>
    public PdfScanPreprocessingOptions Options { get; }
    /// <summary>Gets the processed-to-original geometry map.</summary>
    public PdfScanGeometry Geometry { get; }
    /// <summary>Gets selected explicit or observed orientation.</summary>
    public PdfScanOrientation AppliedOrientation { get; }
    /// <summary>Gets explicit or measured source skew, corrected by its negative.</summary>
    public double SourceSkewDegrees { get; }
    /// <summary>Gets nullable orientation score margin, never an accuracy probability.</summary>
    public double? OrientationMargin { get; }
    /// <summary>Gets nullable skew score margin, never an accuracy probability.</summary>
    public double? SkewMargin { get; }
    /// <summary>Gets four orientation observations when requested, otherwise empty.</summary>
    public IReadOnlyList<PdfScanOrientationScore> OrientationScores { get; }
    /// <summary>Gets managed pixel visits consumed by this operation.</summary>
    public long PixelVisits { get; }
    /// <summary>Gets the complete root operation report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;
}
