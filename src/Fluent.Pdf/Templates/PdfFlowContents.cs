using System.Collections.ObjectModel;
using System.Globalization;

namespace Fluent.Pdf;
/// <summary>An explicit body heading and stable destination ID.</summary>
public sealed class PdfFlowHeading : PdfFlowBlock
{
    /// <summary>Creates an explicit heading with a stable unique destination ID.</summary>
    public PdfFlowHeading(string id, string title, PdfFlowTextStyle textStyle, int level = 1, PdfFlowParagraphStyle? paragraphStyle = null)
    {
        Id = Guard.Text(id, nameof(id), 128);
        Title = title ?? throw new ArgumentNullException(nameof(title));
        if (title.Length > 1000000 || level < 1 || level > 6)
            throw new ArgumentOutOfRangeException(nameof(level));
        Level = level;
        Paragraph = new(title, textStyle, paragraphStyle);
    }

    /// <summary>Gets the stable unique body destination ID.</summary>
    public string Id { get; }
    /// <summary>Gets the visible heading title.</summary>
    public string Title { get; }
    /// <summary>Gets the heading level from one through six.</summary>
    public int Level { get; }
    /// <summary>Gets the heading paragraph.</summary>
    public PdfFlowParagraph Paragraph { get; }
}

/// <summary>A contents placeholder resolved by the bounded multi-pass template planner.</summary>
public sealed class PdfFlowContents : PdfFlowBlock
{
    /// <summary>Creates a contents placeholder with an explicit heading range and point widths.</summary>
    public PdfFlowContents(PdfFlowTextStyle textStyle, int minimumLevel = 1, int maximumLevel = 6, double pageColumnWidth = 48, double levelIndent = 12)
    {
        TextStyle = textStyle ?? throw new ArgumentNullException(nameof(textStyle));
        if (minimumLevel < 1 || maximumLevel > 6 || minimumLevel > maximumLevel)
            throw new ArgumentOutOfRangeException(nameof(minimumLevel));
        PdfGraphicsNumbers.Finite(pageColumnWidth);
        PdfGraphicsNumbers.Finite(levelIndent);
        if (pageColumnWidth <= 0 || levelIndent < 0)
            throw new ArgumentOutOfRangeException(nameof(pageColumnWidth));
        MinimumLevel = minimumLevel;
        MaximumLevel = maximumLevel;
        PageColumnWidth = pageColumnWidth;
        LevelIndent = levelIndent;
    }

    /// <summary>Gets the explicit text style.</summary>
    public PdfFlowTextStyle TextStyle { get; }
    /// <summary>Gets the first included heading level.</summary>
    public int MinimumLevel { get; }
    /// <summary>Gets the last included heading level.</summary>
    public int MaximumLevel { get; }
    /// <summary>Gets the locator column width in points.</summary>
    public double PageColumnWidth { get; }
    /// <summary>Gets the title indentation per heading level in points.</summary>
    public double LevelIndent { get; }
}

/// <summary>An explicitly indexed term and its stable body destination IDs.</summary>
public sealed class PdfFlowIndexTerm
{
    /// <summary>Creates an indexed term with bounded explicit destination IDs.</summary>
    public PdfFlowIndexTerm(string text, params string[] destinationIds)
    {
        Text = Guard.Text(text, nameof(text), 2048);
        if (destinationIds == null || destinationIds.Length < 1 || destinationIds.Length > 4096)
            throw new ArgumentException("Supply a bounded nonempty destination list.", nameof(destinationIds));
        foreach (string id in destinationIds)
            Guard.Text(id, nameof(destinationIds), 128);
        DestinationIds = new ReadOnlyCollection<string>((string[])destinationIds.Clone());
    }

    /// <summary>Gets the indexed term.</summary>
    public string Text { get; }
    /// <summary>Gets the immutable body destination IDs.</summary>
    public IReadOnlyList<string> DestinationIds { get; }
}

/// <summary>Explicit index locators, sorted with the supplied culture; no implicit text search occurs.</summary>
public sealed class PdfFlowIndex : PdfFlowBlock
{
    /// <summary>Creates an index using explicit culture-sensitive term sorting.</summary>
    public PdfFlowIndex(IEnumerable<PdfFlowIndexTerm> terms, PdfFlowTextStyle textStyle, CultureInfo culture, double pageColumnWidth = 48)
    {
        if (terms == null)
            throw new ArgumentNullException(nameof(terms));
        if (culture == null)
            throw new ArgumentNullException(nameof(culture));
        TextStyle = textStyle ?? throw new ArgumentNullException(nameof(textStyle));
        PdfGraphicsNumbers.Finite(pageColumnWidth);
        if (pageColumnWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageColumnWidth));
        var copy = new List<PdfFlowIndexTerm>();
        foreach (var term in terms)
        {
            if (term == null || copy.Count == 4096)
                throw new ArgumentException("Supply up to 4096 nonnull terms.", nameof(terms));
            copy.Add(term);
        }

        Terms = copy.OrderBy(t => t.Text, StringComparer.Create(culture, false)).ToList().AsReadOnly();
        PageColumnWidth = pageColumnWidth;
    }

    /// <summary>Gets the terms sorted with the supplied culture.</summary>
    public IReadOnlyList<PdfFlowIndexTerm> Terms { get; }
    /// <summary>Gets the explicit text style.</summary>
    public PdfFlowTextStyle TextStyle { get; }
    /// <summary>Gets the locator column width in points.</summary>
    public double PageColumnWidth { get; }
}

/// <summary>A resolved physical heading destination.</summary>
public sealed class PdfFlowHeadingPlacement
{
    internal PdfFlowHeadingPlacement(PdfFlowHeading heading, int page, PdfRect bounds)
    {
        Heading = heading;
        PageIndex = page;
        Bounds = bounds;
    }

    /// <summary>Gets the originating body heading.</summary>
    public PdfFlowHeading Heading { get; }
    /// <summary>Gets the zero-based physical page index.</summary>
    public int PageIndex { get; }
    /// <summary>Gets bounds in the containing content coordinate system.</summary>
    public PdfRect Bounds { get; }
}

/// <summary>A contents/index link resolved to a final physical destination.</summary>
public sealed class PdfContentsEntry
{
    internal PdfContentsEntry(string id, string title, int page, PdfPoint point)
    {
        DestinationId = id;
        Title = title;
        PageIndex = page;
        Point = point;
    }

    /// <summary>Gets the stable body destination ID.</summary>
    public string DestinationId { get; }
    /// <summary>Gets the visible heading title.</summary>
    public string Title { get; }
    /// <summary>Gets the zero-based physical page index.</summary>
    public int PageIndex { get; }
    /// <summary>Gets the destination in native PDF page coordinates.</summary>
    public PdfPoint Point { get; }
}
