using System.Text.RegularExpressions;

namespace Fluent.Pdf;
/// <summary>An immutable bounded literal/regex query over source-order semantic text, without inferred separators.</summary>
public sealed class PdfTextQuery
{
    /// <summary>Captures explicit matching, displayed-region and declared-font filters. Regex calls have a finite at-most-250ms timeout and shared operation checkpoints.</summary>
    public PdfTextQuery(string pattern, bool regex = false, bool ignoreCase = false, int maximumMatches = 10000, int regexTimeoutMilliseconds = 100, PdfRect? region = null, string? fontName = null, double? minimumFontSize = null, double? maximumFontSize = null)
    {
        if (string.IsNullOrEmpty(pattern) || pattern.Length > 4096)
            throw new ArgumentException("A nonempty pattern of at most 4096 UTF-16 units is required.", nameof(pattern));
        if (maximumMatches < 1 || maximumMatches > 1000000)
            throw new ArgumentOutOfRangeException(nameof(maximumMatches));
        if (regexTimeoutMilliseconds < 1 || regexTimeoutMilliseconds > 250)
            throw new ArgumentOutOfRangeException(nameof(regexTimeoutMilliseconds));
        if (fontName != null)
            Guard.Text(fontName, nameof(fontName), 1024);
        foreach (var size in new[]
        {
            minimumFontSize,
            maximumFontSize
        }

        )
            if (size.HasValue && (double.IsNaN(size.Value) || double.IsInfinity(size.Value) || size.Value <= 0))
                throw new ArgumentOutOfRangeException(nameof(minimumFontSize));
        if (minimumFontSize > maximumFontSize)
            throw new ArgumentException("Font size bounds must be ordered.");
        if (region.HasValue && (region.Value.Width <= 0 || region.Value.Height <= 0))
            throw new ArgumentException("A positive region is required.", nameof(region));
        Pattern = pattern;
        IsRegex = regex;
        IgnoreCase = ignoreCase;
        MaximumMatches = maximumMatches;
        RegexTimeoutMilliseconds = regexTimeoutMilliseconds;
        Region = region;
        FontName = fontName;
        MinimumFontSize = minimumFontSize;
        MaximumFontSize = maximumFontSize;
    }

    /// <summary>Gets the exact literal or regex pattern.</summary>
    public string Pattern { get; }
    /// <summary>Gets whether regex syntax is explicitly requested.</summary>
    public bool IsRegex { get; }
    /// <summary>Gets ordinal/invariant case-insensitive selection.</summary>
    public bool IgnoreCase { get; }
    /// <summary>Gets the maximum accepted matches; overflow fails rather than truncating.</summary>
    public int MaximumMatches { get; }
    /// <summary>Gets the finite timeout for each regex match call.</summary>
    public int RegexTimeoutMilliseconds { get; }
    /// <summary>Gets an optional displayed top-left region; all covered advance cells must be contained.</summary>
    public PdfRect? Region { get; }
    /// <summary>Gets the optional exact declared PDF font name, applied to all covered source characters.</summary>
    public string? FontName { get; }
    /// <summary>Gets the optional minimum displayed font size.</summary>
    public double? MinimumFontSize { get; }
    /// <summary>Gets the optional maximum displayed font size.</summary>
    public double? MaximumFontSize { get; }
}

/// <summary>A source-order semantic match retaining exact source characters and advance-cell bounds.</summary>
public sealed class PdfTextMatch
{
    internal PdfTextMatch(int start, string text, IReadOnlyList<PdfExtractedCharacter> characters, PdfRect? bounds, bool partial, bool actual)
    {
        Start = start;
        Text = text;
        Characters = characters;
        Bounds = bounds;
        IsPartialCluster = partial;
        UsesActualText = actual;
    }

    /// <summary>Gets the UTF-16 start in the result's semantic search text.</summary>
    public int Start { get; }
    /// <summary>Gets the exact matched Unicode.</summary>
    public string Text { get; }
    /// <summary>Gets covered source-code records, without inventing per-scalar ligature geometry.</summary>
    public IReadOnlyList<PdfExtractedCharacter> Characters { get; }
    /// <summary>Gets their displayed advance-cell union, or null for geometry-free logical text.</summary>
    public PdfRect? Bounds { get; }
    /// <summary>Gets whether a match cuts a Unicode cluster or ActualText replacement.</summary>
    public bool IsPartialCluster { get; }
    /// <summary>Gets whether explicit source ActualText contributes to the match.</summary>
    public bool UsesActualText { get; }
}

/// <summary>An immutable successful page search result.</summary>
public sealed class PdfTextSearchResult
{
    internal PdfTextSearchResult(int page, string text, IReadOnlyList<PdfTextMatch> matches, PdfOperationReport report)
    {
        PageIndex = page;
        Text = text;
        Matches = matches;
        OperationReport = report;
    }

    /// <summary>Gets the selected page index.</summary>
    public int PageIndex { get; }
    /// <summary>Gets source-order semantic text without inferred spaces or paragraph separators.</summary>
    public string Text { get; }
    /// <summary>Gets nonoverlapping selected matches, including explicit zero-length regex matches.</summary>
    public IReadOnlyList<PdfTextMatch> Matches { get; }
    /// <summary>Gets diagnostics and resource observations.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>Explicit overflow policy for replacement in a source advance-cell frame.</summary>
public enum PdfTextReplacementOverflow
{
    /// <summary>Reject text that exceeds the captured frame.</summary>
    Reject,
    /// <summary>Uniformly reduce the explicit font size to fit the captured frame.</summary>
    ShrinkToFit
}

/// <summary>Explicit single-line replacement policy; surrounding source advances remain unchanged.</summary>
public sealed class PdfTextReplacementOptions
{
    /// <summary>Captures maximum font size and explicit overflow behavior. Replacement uses explicit fonts/shaper and black fill; paragraph reflow is a separate required profile.</summary>
    public PdfTextReplacementOptions(double fontSize, PdfTextReplacementOverflow overflow = PdfTextReplacementOverflow.Reject)
    {
        if (double.IsNaN(fontSize) || double.IsInfinity(fontSize) || fontSize <= 0 || fontSize > 10000)
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        if (!Enum.IsDefined(typeof(PdfTextReplacementOverflow), overflow))
            throw new ArgumentOutOfRangeException(nameof(overflow));
        FontSize = fontSize;
        Overflow = overflow;
    }

    /// <summary>Gets the explicit maximum font size.</summary>
    public double FontSize { get; }
    /// <summary>Gets the explicit overflow behavior.</summary>
    public PdfTextReplacementOverflow Overflow { get; }
}
