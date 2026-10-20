namespace Fluent.Pdf;
/// <summary>Explicit or observed scan-region semantics, separate from prose column order.</summary>
public enum PdfScanRegionKind
{
    /// <summary>Geometrically inferred prose; not an accessibility assertion.</summary>
    Prose,
    /// <summary>A caller-declared table whose raw engine order is retained.</summary>
    Table,
    /// <summary>A caller-declared form whose raw engine order is retained.</summary>
    Form,
    /// <summary>Measured closed rulings; table-versus-form semantics remain unresolved and prose ordering is withheld.</summary>
    RuledTableOrForm
}

/// <summary>A finite caller declaration used to keep tables/forms out of prose-column inference.</summary>
public sealed class PdfScanRegionHint
{
    /// <summary>Captures explicit table/form bounds and caller-declared confidence; this is not an SDK correctness estimate.</summary>
    public PdfScanRegionHint(PdfScanRegionKind kind, PdfRect bounds, double confidence = 1)
    {
        if (kind != PdfScanRegionKind.Table && kind != PdfScanRegionKind.Form)
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (bounds.X < 0 || bounds.Y < 0 || bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(bounds));
        if (double.IsNaN(confidence) || double.IsInfinity(confidence) || confidence < 0 || confidence > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence));
        Kind = kind;
        Bounds = bounds;
        Confidence = confidence;
    }

    /// <summary>Gets caller-declared table/form kind.</summary>
    public PdfScanRegionKind Kind { get; }
    /// <summary>Gets processed-raster pixel bounds.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets caller-declared confidence, never inferred correctness.</summary>
    public double Confidence { get; }
}

/// <summary>Finite opt-in whitespace column/ruling analysis and explicit semantic-region authority.</summary>
public sealed class PdfScanLayoutOptions
{
    /// <summary>Captures explicit column/gap/line/work limits. Optional Gray8 raster analysis identifies closed grids without inventing table/form semantics.</summary>
    public PdfScanLayoutOptions(bool inferColumns = true, bool rightToLeftColumns = false, bool rightToLeftWords = false, double minimumColumnGapPixels = 12, double lineTolerancePixels = 4, double spanningHeightFraction = 0.1, int minimumWordsPerColumn = 2, int maximumColumns = 16, int minimumRuleLengthPixels = 20, int maximumWords = 100000, long maximumAnalysisWork = 100000000, params PdfScanRegionHint[] regions)
    {
        if (!Finite(minimumColumnGapPixels) || minimumColumnGapPixels <= 0 || minimumColumnGapPixels > 100000 || !Finite(lineTolerancePixels) || lineTolerancePixels <= 0 || lineTolerancePixels > 10000 || !Finite(spanningHeightFraction) || spanningHeightFraction < 0 || spanningHeightFraction > 0.5)
            throw new ArgumentOutOfRangeException(nameof(minimumColumnGapPixels));
        if (minimumWordsPerColumn < 1 || minimumWordsPerColumn > 100000 || maximumColumns < 1 || maximumColumns > 64 || minimumRuleLengthPixels < 3 || minimumRuleLengthPixels > 100000 || maximumWords < 1 || maximumWords > 1000000 || maximumAnalysisWork < 1 || maximumAnalysisWork > 1000000000)
            throw new ArgumentOutOfRangeException(nameof(maximumWords));
        if (regions == null || regions.Length > 64 || regions.Any(r => r == null))
            throw new ArgumentException("At most64 explicit nonnull region hints are supported.", nameof(regions));
        for (int i = 0; i < regions.Length; i++)
            for (int j = 0; j < i; j++)
            {
                var a = regions[i].Bounds;
                var b = regions[j].Bounds;
                if (Math.Min(a.Right, b.Right) > Math.Max(a.X, b.X) && Math.Min(a.Top, b.Top) > Math.Max(a.Y, b.Y))
                    throw new ArgumentException("Explicit semantic regions cannot overlap.", nameof(regions));
            }

        InferColumns = inferColumns;
        RightToLeftColumns = rightToLeftColumns;
        RightToLeftWords = rightToLeftWords;
        MinimumColumnGapPixels = minimumColumnGapPixels;
        LineTolerancePixels = lineTolerancePixels;
        SpanningHeightFraction = spanningHeightFraction;
        MinimumWordsPerColumn = minimumWordsPerColumn;
        MaximumColumns = maximumColumns;
        MinimumRuleLengthPixels = minimumRuleLengthPixels;
        MaximumWords = maximumWords;
        MaximumAnalysisWork = maximumAnalysisWork;
        Regions = Array.AsReadOnly((PdfScanRegionHint[])regions.Clone());
    }

    /// <summary>Gets whether whitespace-based prose-column inference is requested.</summary>
    public bool InferColumns { get; }
    /// <summary>Gets explicit column direction.</summary>
    public bool RightToLeftColumns { get; }
    /// <summary>Gets explicit geometric word order within inferred lines.</summary>
    public bool RightToLeftWords { get; }
    /// <summary>Gets minimum low-density vertical gutter width.</summary>
    public double MinimumColumnGapPixels { get; }
    /// <summary>Gets geometric line-center tolerance.</summary>
    public double LineTolerancePixels { get; }
    /// <summary>Gets tolerated spanning-word vertical coverage when finding gutters.</summary>
    public double SpanningHeightFraction { get; }
    /// <summary>Gets minimum words required in each proposed column.</summary>
    public int MinimumWordsPerColumn { get; }
    /// <summary>Gets maximum inferred column count.</summary>
    public int MaximumColumns { get; }
    /// <summary>Gets minimum dark run used for optional literal closed-grid detection.</summary>
    public int MinimumRuleLengthPixels { get; }
    /// <summary>Gets finite recognized-word ceiling.</summary>
    public int MaximumWords { get; }
    /// <summary>Gets finite pixel/geometry analysis work ceiling.</summary>
    public long MaximumAnalysisWork { get; }
    /// <summary>Gets explicit semantic-region hints in caller order.</summary>
    public IReadOnlyList<PdfScanRegionHint> Regions { get; }

    private static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
}

/// <summary>A region observation retaining source word identities and explicit confidence provenance.</summary>
public sealed class PdfScanLayoutRegion
{
    internal PdfScanLayoutRegion(PdfScanRegionKind kind, PdfRect bounds, int[] words, int? column, double confidence, string provenance)
    {
        Kind = kind;
        Bounds = bounds;
        WordIndices = Array.AsReadOnly(words);
        ColumnIndex = column;
        Confidence = confidence;
        Provenance = provenance;
    }

    /// <summary>Gets declared or observed region kind.</summary>
    public PdfScanRegionKind Kind { get; }
    /// <summary>Gets processed-raster pixel bounds.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets original recognition word indices, without replacing raw engine order.</summary>
    public IReadOnlyList<int> WordIndices { get; }
    /// <summary>Gets left-to-right geometric column index, or null for spanning/nonprose regions.</summary>
    public int? ColumnIndex { get; }
    /// <summary>Gets a declared or heuristic evidence score, never calibrated correctness.</summary>
    public double Confidence { get; }
    /// <summary>Gets explicit authority: caller-declared, literal-rulings, or whitespace.</summary>
    public string Provenance { get; }
}

/// <summary>Prose reading order is separate from unresolved table/form order and immutable raw recognition data.</summary>
public sealed class PdfScanLayoutResult
{
    internal PdfScanLayoutResult(PdfOcrResult recognition, PdfScanLayoutRegion[] regions, int[] prose, int[] nonprose, long work)
    {
        Recognition = recognition;
        Regions = Array.AsReadOnly(regions);
        ProseReadingOrder = Array.AsReadOnly(prose);
        NonProseWordIndices = Array.AsReadOnly(nonprose);
        AnalysisWork = work;
    }

    /// <summary>Gets unchanged raw engine recognition order/provenance/confidence.</summary>
    public PdfOcrResult Recognition { get; }
    /// <summary>Gets declared/observed regions; collection order does not assert document reading order.</summary>
    public IReadOnlyList<PdfScanLayoutRegion> Regions { get; }
    /// <summary>Gets inferred prose word indices, including spanning headings. It does not order table/form cells.</summary>
    public IReadOnlyList<int> ProseReadingOrder { get; }
    /// <summary>Gets table/form/closed-grid word indices retained in raw engine order without a semantic ordering claim.</summary>
    public IReadOnlyList<int> NonProseWordIndices { get; }
    /// <summary>Gets finite analysis work consumed.</summary>
    public long AnalysisWork { get; }
    /// <summary>Gets root operation diagnostics.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;
}

/// <summary>One shared-context preprocessing, recognition and layout result, without automatic language/accuracy/semantic qualification.</summary>
public sealed class PdfScanRecognitionResult
{
    internal PdfScanRecognitionResult(PdfScanPreprocessingResult scan, PdfOcrResult recognition, PdfScanLayoutResult layout, PdfOperationReport report)
    {
        Scan = scan;
        Recognition = recognition;
        Layout = layout;
        OperationReport = report;
    }

    /// <summary>Gets preserved original, processed samples and inverse transforms.</summary>
    public PdfScanPreprocessingResult Scan { get; }
    /// <summary>Gets raw engine recognition in processed-raster coordinates.</summary>
    public PdfOcrResult Recognition { get; }
    /// <summary>Gets separate prose layout inference and unresolved nonprose order.</summary>
    public PdfScanLayoutResult Layout { get; }
    /// <summary>Gets the complete shared root report.</summary>
    public PdfOperationReport OperationReport { get; }
}
