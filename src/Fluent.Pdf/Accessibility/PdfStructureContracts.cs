using System.Collections.ObjectModel;

namespace Fluent.Pdf;
/// <summary>The requested accessibility validation standard; selection is not a conformance claim.</summary>
public enum PdfAccessibilityProfile
{
    /// <summary>PDF/UA-1.</summary>
    Ua1,
    /// <summary>PDF/UA-2.</summary>
    Ua2
}

/// <summary>A closed immutable logical-structure item; order is explicitly supplied by the author.</summary>
public abstract class PdfStructureItem
{
    private protected PdfStructureItem()
    {
    }
}

/// <summary>An explicit page/Form MCID or page annotation association. These references do not infer semantics from geometry.</summary>
public sealed class PdfStructureContent : PdfStructureItem
{
    private PdfStructureContent(int page, int mcid, PdfObjectId? stream, PdfObjectId? annotation)
    {
        if (page < 0 || mcid < -1 || mcid > 1000000)
            throw new ArgumentOutOfRangeException(nameof(page));
        PageIndex = page;
        Mcid = mcid;
        StreamId = stream;
        AnnotationId = annotation;
    }

    /// <summary>Identifies an existing marked-content sequence in a page or explicitly selected Form stream.</summary>
    public static PdfStructureContent MarkedContent(int pageIndex, int mcid, PdfObjectId? streamId = null)
    {
        if (mcid < 0)
            throw new ArgumentOutOfRangeException(nameof(mcid));
        return new(pageIndex, mcid, streamId, null);
    }

    /// <summary>Identifies an existing annotation on the selected page, including a Link or Widget.</summary>
    public static PdfStructureContent Annotation(int pageIndex, PdfObjectId annotationId) => new(pageIndex, -1, null, annotationId);
    /// <summary>Gets the source page index.</summary>
    public int PageIndex { get; }
    /// <summary>Gets the MCID or minus one for an annotation.</summary>
    public int Mcid { get; }
    /// <summary>Gets the explicit Form stream identity; null selects page content.</summary>
    public PdfObjectId? StreamId { get; }
    /// <summary>Gets the annotation identity, or null for marked content.</summary>
    public PdfObjectId? AnnotationId { get; }
}

/// <summary>Explicit standard table attributes. Header associations use bounded author-provided element IDs.</summary>
public sealed class PdfStructureTableAttributes
{
    /// <summary>Captures cell spans, optional Row/Column/Both header scope, header IDs and optional table summary.</summary>
    public PdfStructureTableAttributes(int rowSpan = 1, int columnSpan = 1, string? headerScope = null, string[]? headers = null, string? summary = null)
    {
        if (rowSpan < 1 || rowSpan > 100000 || columnSpan < 1 || columnSpan > 100000)
            throw new ArgumentOutOfRangeException(nameof(rowSpan));
        if (headerScope != null && headerScope != "Row" && headerScope != "Column" && headerScope != "Both")
            throw new ArgumentException("An explicit standard header scope is required.", nameof(headerScope));
        RowSpan = rowSpan;
        ColumnSpan = columnSpan;
        HeaderScope = headerScope;
        if (headers != null && headers.Length > 4096)
            throw new ArgumentException("A bounded header inventory is required.", nameof(headers));
        var copy = headers == null ? Array.Empty<string>() : (string[])headers.Clone();
        if (copy.Length > 4096 || copy.Any(s => !PdfStructureRules.Identifier(s)) || copy.Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("Unique bounded header IDs are required.", nameof(headers));
        Headers = Array.AsReadOnly(copy);
        Summary = PdfStructureRules.Text(summary, 8192, nameof(summary));
    }

    /// <summary>Gets the explicit cell row span.</summary>
    public int RowSpan { get; }
    /// <summary>Gets the explicit cell column span.</summary>
    public int ColumnSpan { get; }
    /// <summary>Gets optional standard header scope.</summary>
    public string? HeaderScope { get; }
    /// <summary>Gets immutable header element IDs.</summary>
    public IReadOnlyList<string> Headers { get; }
    /// <summary>Gets author-supplied table summary.</summary>
    public string? Summary { get; }
}

/// <summary>An immutable author-supplied semantic element with explicitly ordered children/content.</summary>
public sealed class PdfStructureElement : PdfStructureItem
{
    /// <summary>Captures an explicit standard role, ordered items and meaningful optional semantic text. No semantic text is guessed.</summary>
    public PdfStructureElement(string role, PdfStructureItem[] items, string? language = null, string? alternativeText = null, string? actualText = null, string? identifier = null, PdfStructureTableAttributes? table = null)
    {
        PdfStructureRules.Role(role);
        if (items == null || items.Length > 100000 || items.Any(i => i == null))
            throw new ArgumentException("Bounded nonnull ordered items are required.", nameof(items));
        Role = role;
        Items = Array.AsReadOnly((PdfStructureItem[])items.Clone());
        Language = PdfStructureRules.Language(language);
        AlternativeText = PdfStructureRules.Text(alternativeText, 1000000, nameof(alternativeText));
        ActualText = PdfStructureRules.Text(actualText, 1000000, nameof(actualText));
        if (identifier != null && !PdfStructureRules.Identifier(identifier))
            throw new ArgumentException("A bounded ASCII element ID is required.", nameof(identifier));
        Identifier = identifier;
        Table = table;
        PdfStructureRules.Table(role, table);
        if ((role == "Figure" || role == "Formula" || role == "Form") && string.IsNullOrWhiteSpace(alternativeText) && string.IsNullOrWhiteSpace(actualText))
            throw new ArgumentException("This role requires meaningful author-provided alternative or replacement text.", nameof(alternativeText));
    }

    /// <summary>Gets the explicit standard role.</summary>
    public string Role { get; }
    /// <summary>Gets author-defined reading order across elements and content.</summary>
    public IReadOnlyList<PdfStructureItem> Items { get; }
    /// <summary>Gets optional explicit element language.</summary>
    public string? Language { get; }
    /// <summary>Gets meaningful author-provided alternative text.</summary>
    public string? AlternativeText { get; }
    /// <summary>Gets optional exact logical replacement text.</summary>
    public string? ActualText { get; }
    /// <summary>Gets optional unique element ID.</summary>
    public string? Identifier { get; }
    /// <summary>Gets explicit table attributes.</summary>
    public PdfStructureTableAttributes? Table { get; }
}

/// <summary>A complete explicitly ordered replacement/creation plan. It does not assert PDF/UA conformance.</summary>
public sealed class PdfStructurePlan
{
    /// <summary>Captures language/root/profile. Replacing an existing tree requires an explicit reported-loss choice.</summary>
    public PdfStructurePlan(string language, PdfStructureElement root, PdfAccessibilityProfile profile = PdfAccessibilityProfile.Ua1, bool replaceExisting = false)
    {
        Language = PdfStructureRules.Language(language) ?? throw new ArgumentNullException(nameof(language));
        Root = root ?? throw new ArgumentNullException(nameof(root));
        if (root.Role != "Document")
            throw new ArgumentException("A Document root is required.", nameof(root));
        PdfStructureRules.Profile(profile);
        Profile = profile;
        ReplaceExisting = replaceExisting;
    }

    /// <summary>Gets the explicit document language.</summary>
    public string Language { get; }
    /// <summary>Gets the author-defined Document root.</summary>
    public PdfStructureElement Root { get; }
    /// <summary>Gets the requested validation profile.</summary>
    public PdfAccessibilityProfile Profile { get; }
    /// <summary>Gets whether existing structure replacement is explicitly allowed and diagnosed.</summary>
    public bool ReplaceExisting { get; }
}

/// <summary>A detached source element and exact child inventory, preserving identity and reading order.</summary>
public sealed class PdfStructureElementInfo
{
    internal PdfStructureElementInfo(PdfObjectId id, PdfValue dictionary, string role, string? language, string? alt, string? actual, PdfValue[] kids)
    {
        ObjectId = id;
        Dictionary = dictionary;
        Role = role;
        Language = language;
        AlternativeText = alt;
        ActualText = actual;
        Children = Array.AsReadOnly(kids);
    }

    /// <summary>Gets the exact element identity in the inspection source.</summary>
    public PdfObjectId ObjectId { get; }
    /// <summary>Gets the immutable raw element dictionary, including unknown semantic metadata.</summary>
    public PdfValue Dictionary { get; }
    /// <summary>Gets the raw declared role.</summary>
    public string Role { get; }
    /// <summary>Gets the declared element language.</summary>
    public string? Language { get; }
    /// <summary>Gets alternative text.</summary>
    public string? AlternativeText { get; }
    /// <summary>Gets logical replacement text.</summary>
    public string? ActualText { get; }
    /// <summary>Gets exact ordered child values; arrays are expanded one level.</summary>
    public IReadOnlyList<PdfValue> Children { get; }
}

/// <summary>A bounded automated or independent accessibility rule finding, without an implied conformance claim.</summary>
public sealed class PdfAccessibilityFinding
{
    /// <summary>Captures a stable rule identifier, location and neutral message.</summary>
    public PdfAccessibilityFinding(string rule, string location, string message)
    {
        Rule = Guard.Text(rule, nameof(rule), 256);
        Location = Guard.Text(location, nameof(location), 4096);
        Message = Guard.Text(message, nameof(message), 8192);
    }

    /// <summary>Gets the stable rule identifier.</summary>
    public string Rule { get; }
    /// <summary>Gets the source or validator location.</summary>
    public string Location { get; }
    /// <summary>Gets a neutral bounded description.</summary>
    public string Message { get; }
}

/// <summary>Managed structural observations, with automated findings separate from always-required human semantic review.</summary>
public sealed class PdfAccessibilityInspection
{
    internal PdfAccessibilityInspection(PdfDocumentSnapshot source, PdfAccessibilityProfile profile, string? language, PdfStructureElementInfo[] elements, PdfStructureContent[] content, PdfAccessibilityFinding[] findings, PdfOperationReport report)
    {
        Source = source;
        Profile = profile;
        Language = language;
        Elements = Array.AsReadOnly(elements);
        Content = Array.AsReadOnly(content);
        Findings = Array.AsReadOnly(findings);
        OperationReport = report;
    }

    /// <summary>Gets normalized immutable source identities for subsequent remediation.</summary>
    public PdfDocumentSnapshot Source { get; }
    /// <summary>Gets requested profile, without conformance inference.</summary>
    public PdfAccessibilityProfile Profile { get; }
    /// <summary>Gets declared document language.</summary>
    public string? Language { get; }
    /// <summary>Gets elements in source tree traversal order.</summary>
    public IReadOnlyList<PdfStructureElementInfo> Elements { get; }
    /// <summary>Gets actual marked-content and annotation associations in logical tree order.</summary>
    public IReadOnlyList<PdfStructureContent> Content { get; }
    /// <summary>Gets automated source findings. Empty never establishes conformance.</summary>
    public IReadOnlyList<PdfAccessibilityFinding> Findings { get; }
    /// <summary>Gets true: reading order, descriptions and semantic correctness always require human review.</summary>
    public bool HumanReviewRequired => true;
    /// <summary>Gets root operation diagnostics and budgets.</summary>
    public PdfOperationReport OperationReport { get; }
}

internal static class PdfStructureRules
{
    internal const string Pdf2Namespace = "http://iso.org/pdf2/ssn";
    private static readonly HashSet<string> Roles = new("Document Part Art Sect Div BlockQuote Caption TOC TOCI Index NonStruct Private H H1 H2 H3 H4 H5 H6 P L LI Lbl LBody Table TR TH TD THead TBody TFoot Span Quote Note Reference BibEntry Code Link Annot Ruby RB RT RP Warichu WT WP Figure Formula Form".Split(' '), StringComparer.Ordinal);
    internal static void Profile(PdfAccessibilityProfile p)
    {
        if (!Enum.IsDefined(typeof(PdfAccessibilityProfile), p))
            throw new ArgumentOutOfRangeException(nameof(p));
    }

    internal static bool StandardRole(string role) => Roles.Contains(role);
    internal static void Role(string s)
    {
        if (s == null || !Roles.Contains(s))
            throw new ArgumentException("An explicit supported standard structure role is required.", nameof(s));
    }

    internal static bool Identifier(string? s) => s != null && s.Length > 0 && s.Length <= 256 && s.All(c => c >= 33 && c <= 126);
    internal static string? Text(string? s, int n, string name) => s == null || s.Length <= n ? s : throw new ArgumentException("Bounded semantic text is required.", name);
    internal static string? Language(string? s)
    {
        if (s == null)
            return null;
        if (s.Length < 2 || s.Length > 128 || s[0] == '-' || s[s.Length - 1] == '-' || s.Contains("--") || s.Any(c => !(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-')))
            throw new ArgumentException("An explicit bounded language tag is required.", nameof(s));
        return s;
    }

    internal static void Table(string role, PdfStructureTableAttributes? t)
    {
        if (t == null)
            return;
        if (role != "Table" && role != "TH" && role != "TD" || role != "TH" && t.HeaderScope != null || role != "Table" && t.Summary != null || role == "Table" && (t.RowSpan != 1 || t.ColumnSpan != 1 || t.Headers.Count > 0 || t.HeaderScope != null))
            throw new ArgumentException("Table attributes must match their explicit semantic role.", nameof(t));
    }
}
