using System.Globalization;
using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An explicit table inference profile; neither mode asserts semantic structure or calibrated probability.</summary>
public enum PdfTableDetectionMode
{
    /// <summary>Infer rectangular grids/merged cells from literal axis-aligned source stroke segments.</summary>
    Ruled,
    /// <summary>Infer left-aligned columns from geometric text lines with explicit minimum gutters.</summary>
    Borderless
}

/// <summary>Explicit bounds, tolerances, continuation and cultural-number policy.</summary>
public sealed class PdfTableExtractionOptions
{
    /// <summary>Captures one explicit inference profile per selected page/region. Numeric typing occurs only when a culture is explicitly supplied.</summary>
    public PdfTableExtractionOptions(PdfTableDetectionMode mode, PdfRect? region = null, double tolerance = 1, double minimumColumnGap = 12, int minimumRows = 2, int minimumColumns = 2, int maximumCells = 100000, int headerRows = 0, bool joinConsecutivePages = false, string? numericCulture = null)
    {
        if (!Enum.IsDefined(typeof(PdfTableDetectionMode), mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (region.HasValue && (region.Value.Width <= 0 || region.Value.Height <= 0))
            throw new ArgumentException("A positive displayed region is required.", nameof(region));
        if (!Finite(tolerance) || tolerance <= 0 || tolerance > 10)
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (!Finite(minimumColumnGap) || minimumColumnGap <= 0 || minimumColumnGap > 10000)
            throw new ArgumentOutOfRangeException(nameof(minimumColumnGap));
        if (minimumRows < 1 || minimumRows > 10000 || minimumColumns < 1 || minimumColumns > 1000 || maximumCells < 1 || maximumCells > 1000000 || headerRows < 0 || headerRows > 1000)
            throw new ArgumentOutOfRangeException(nameof(minimumRows));
        Mode = mode;
        Region = region;
        Tolerance = tolerance;
        MinimumColumnGap = minimumColumnGap;
        MinimumRows = minimumRows;
        MinimumColumns = minimumColumns;
        MaximumCells = maximumCells;
        HeaderRows = headerRows;
        JoinConsecutivePages = joinConsecutivePages;
        if (numericCulture != null)
        {
            if (numericCulture.Length > 85)
                throw new ArgumentException("A bounded culture name is required.", nameof(numericCulture));
            Culture = CultureInfo.ReadOnly(new CultureInfo(numericCulture, false));
        }

        NumericCulture = numericCulture;
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    internal CultureInfo? Culture { get; }
    /// <summary>Gets the explicit inference profile.</summary>
    public PdfTableDetectionMode Mode { get; }
    /// <summary>Gets the optional displayed top-left table region, applied independently to selected pages.</summary>
    public PdfRect? Region { get; }
    /// <summary>Gets the displayed-point alignment/rule tolerance.</summary>
    public double Tolerance { get; }
    /// <summary>Gets the displayed-point gutter separating borderless cells.</summary>
    public double MinimumColumnGap { get; }
    /// <summary>Gets the minimum accepted rows.</summary>
    public int MinimumRows { get; }
    /// <summary>Gets the minimum accepted columns.</summary>
    public int MinimumColumns { get; }
    /// <summary>Gets the maximum primitive cells per candidate, checked before allocation.</summary>
    public int MaximumCells { get; }
    /// <summary>Gets the explicit leading header-row count used for exact repeated-header comparison.</summary>
    public int HeaderRows { get; }
    /// <summary>Gets whether consecutive selected pages with matching columns and explicit headers may form a sequence.</summary>
    public bool JoinConsecutivePages { get; }
    /// <summary>Gets the explicit numeric culture, or null for text-only cells. Empty explicitly selects invariant culture.</summary>
    public string? NumericCulture { get; }
}

/// <summary>An inferred rectangular cell with original source characters, rule provenance and optional explicit-culture decimal typing.</summary>
public sealed class PdfExtractedTableCell
{
    internal PdfExtractedTableCell(int page, int row, int column, int rows, int columns, PdfRect bounds, string text, decimal? number, IReadOnlyList<PdfExtractedCharacter> characters, IReadOnlyList<PdfResourceSource> rules, double confidence)
    {
        PageIndex = page;
        Row = row;
        Column = column;
        RowSpan = rows;
        ColumnSpan = columns;
        Bounds = bounds;
        Text = text;
        Number = number;
        Characters = characters;
        RuleSources = rules;
        Confidence = confidence;
    }

    /// <summary>Gets the zero-based source page.</summary>
    public int PageIndex { get; }
    /// <summary>Gets the zero-based row in its page table.</summary>
    public int Row { get; }
    /// <summary>Gets the zero-based column.</summary>
    public int Column { get; }
    /// <summary>Gets its inferred rectangular row span.</summary>
    public int RowSpan { get; }
    /// <summary>Gets its inferred rectangular column span.</summary>
    public int ColumnSpan { get; }
    /// <summary>Gets displayed top-left advance/rule bounds; these are inferred cell edges, not ink.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets semantic cell text with explicit ActualText and inferred geometric separators.</summary>
    public string Text { get; }
    /// <summary>Gets an explicitly culture-parsed decimal, or null for text/empty/unparsed cells.</summary>
    public decimal? Number { get; }
    /// <summary>Gets exact covered source-code records with operator execution provenance.</summary>
    public IReadOnlyList<PdfExtractedCharacter> Characters { get; }
    /// <summary>Gets literal rule source locations contributing to this candidate; borderless cells have none.</summary>
    public IReadOnlyList<PdfResourceSource> RuleSources { get; }
    /// <summary>Gets a documented heuristic score, not a calibrated probability or qualification metric.</summary>
    public double Confidence { get; }
}

/// <summary>One inferred table on one source page, retained independently of continuation decisions.</summary>
public sealed class PdfExtractedTable
{
    internal PdfExtractedTable(int page, PdfRect bounds, int rows, int columns, IReadOnlyList<PdfExtractedTableCell> cells, PdfTableDetectionMode mode, double confidence, IReadOnlyList<double> boundaries)
    {
        PageIndex = page;
        Bounds = bounds;
        RowCount = rows;
        ColumnCount = columns;
        Cells = cells;
        Mode = mode;
        Confidence = confidence;
        ColumnBoundaries = boundaries;
    }

    /// <summary>Gets its source page index.</summary>
    public int PageIndex { get; }
    /// <summary>Gets its inferred displayed top-left region.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets primitive grid rows, including merged/header rows.</summary>
    public int RowCount { get; }
    /// <summary>Gets primitive grid columns.</summary>
    public int ColumnCount { get; }
    /// <summary>Gets anchor cells with explicit row/column spans.</summary>
    public IReadOnlyList<PdfExtractedTableCell> Cells { get; }
    /// <summary>Gets the explicit detection profile.</summary>
    public PdfTableDetectionMode Mode { get; }
    /// <summary>Gets a heuristic score, not calibrated probability.</summary>
    public double Confidence { get; }
    /// <summary>Gets inferred displayed X boundaries used for explicit continuation comparison.</summary>
    public IReadOnlyList<double> ColumnBoundaries { get; }
}

/// <summary>An explicit heuristic continuation grouping, without deleting repeated source rows.</summary>
public sealed class PdfExtractedTableSequence
{
    internal PdfExtractedTableSequence(IReadOnlyList<int> tables, int headers)
    {
        TableIndices = tables;
        RepeatedHeaderRows = headers;
    }

    /// <summary>Gets ordered table indices in the owning result.</summary>
    public IReadOnlyList<int> TableIndices { get; }
    /// <summary>Gets the explicitly compared repeated header-row count; original source rows remain retained.</summary>
    public int RepeatedHeaderRows { get; }
}

/// <summary>An explicit lossy flattening decision for CSV merged cells.</summary>
public enum PdfMergedCellCsvPolicy
{
    /// <summary>Write the text at its anchor and empty covered cells.</summary>
    AnchorOnly,
    /// <summary>Repeat the anchor text in every covered primitive cell.</summary>
    RepeatText
}

/// <summary>Immutable inferred tables/sequences with original snapshot and operation diagnostics.</summary>
public sealed class PdfTableExtractionResult
{
    internal PdfTableExtractionResult(PdfDocumentSnapshot source, IReadOnlyList<PdfExtractedTable> tables, IReadOnlyList<PdfExtractedTableSequence> sequences, PdfOperationReport report)
    {
        Source = source;
        Tables = tables;
        Sequences = sequences;
        OperationReport = report;
    }

    /// <summary>Gets the immutable original captured snapshot for independent source inspection.</summary>
    public PdfDocumentSnapshot Source { get; }
    /// <summary>Gets per-page tables; zero candidates is explicit and diagnosed.</summary>
    public IReadOnlyList<PdfExtractedTable> Tables { get; }
    /// <summary>Gets explicit page-continuation groups.</summary>
    public IReadOnlyList<PdfExtractedTableSequence> Sequences { get; }
    /// <summary>Gets inference diagnostics and resource observations.</summary>
    public PdfOperationReport OperationReport { get; }

    /// <summary>Writes UTF-8 CSV with an explicit merged-cell policy. Header suppression and spreadsheet formula escaping require explicit opt-in; borrowed output stays open.</summary>
    public PdfOperationReport WriteCsv(Stream output, PdfMergedCellCsvPolicy mergedCells, int sequenceIndex = 0, bool omitRepeatedHeaders = false, bool escapeSpreadsheetFormulas = false, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfTableExport.Write(this, output, false, mergedCells, sequenceIndex, omitRepeatedHeaders, escapeSpreadsheetFormulas, policy, cancellationToken);
    /// <summary>Writes a versioned UTF-8 JSON schema retaining cell spans, page coordinates, source indices and typed values. Borrowed output stays open.</summary>
    public PdfOperationReport WriteJson(Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfTableExport.Write(this, output, true, PdfMergedCellCsvPolicy.AnchorOnly, 0, false, false, policy, cancellationToken);
    /// <summary>Atomically replaces a caller-selected UTF-8 JSON file after successful bounded serialization.</summary>
    public PdfOperationReport WriteJsonFile(string path, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfTableExport.File(this, path, policy, cancellationToken);
}
