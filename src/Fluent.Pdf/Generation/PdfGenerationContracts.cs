using System.Globalization;

namespace Fluent.Pdf;
/// <summary>Ordered synchronous preparation phases. Every hook runs before successful return or atomic file publication.</summary>
public enum PdfGenerationEventKind
{
    /// <summary>The bounded generation operation is starting.</summary>
    Starting,
    /// <summary>A selected source record is about to render.</summary>
    RecordStarting,
    /// <summary>A reusable template invocation is about to render.</summary>
    TemplateStarting,
    /// <summary>The invocation's blocks are captured; pagination has not necessarily occurred.</summary>
    TemplatePrepared,
    /// <summary>The selected source record's blocks are captured.</summary>
    RecordPrepared,
    /// <summary>Binding finished and final layout is starting.</summary>
    LayoutStarting,
    /// <summary>Layout/convergence finished; final page data is immutable.</summary>
    LayoutPrepared,
    /// <summary>One final physical page is available for inspection.</summary>
    PagePrepared,
    /// <summary>Private model writing is about to start.</summary>
    SaveStarting,
    /// <summary>Serialization finished; an atomic destination has not yet been published.</summary>
    SavePrepared,
    /// <summary>All generation work is prepared; final resource checks and file publication still follow.</summary>
    Prepared
}

/// <summary>An immutable hook view without a live editable document or output stream.</summary>
public sealed class PdfGenerationEvent
{
    internal PdfGenerationEvent(PdfGenerationEventKind kind, int ordinal, int recordIndex, int records, int blocks, string? template, int depth, IReadOnlyList<PdfGenerationPage> pages, int page, PdfOperationContext context)
    {
        Kind = kind;
        Ordinal = ordinal;
        RecordIndex = recordIndex;
        RecordsPrepared = records;
        BlockCount = blocks;
        TemplateIdentifier = template;
        TemplateDepth = depth;
        Pages = pages;
        PageIndex = page;
        OperationContext = context;
    }

    /// <summary>Gets the ordered phase.</summary>
    public PdfGenerationEventKind Kind { get; }
    /// <summary>Gets the zero-based dispatched hook ordinal.</summary>
    public int Ordinal { get; }
    /// <summary>Gets the current zero-based source record, or -1 before enumeration.</summary>
    public int RecordIndex { get; }
    /// <summary>Gets fully rendered source records.</summary>
    public int RecordsPrepared { get; }
    /// <summary>Gets captured top-level block count.</summary>
    public int BlockCount { get; }
    /// <summary>Gets the current explicit template identity, or null outside an invocation.</summary>
    public string? TemplateIdentifier { get; }
    /// <summary>Gets the active invocation depth.</summary>
    public int TemplateDepth { get; }
    /// <summary>Gets immutable final page observations after LayoutPrepared; no unfinished layout/model objects are exposed.</summary>
    public IReadOnlyList<PdfGenerationPage> Pages { get; }
    /// <summary>Gets the selected final page, or -1 for operation-wide phases.</summary>
    public int PageIndex { get; }
    /// <summary>Gets final page count when layout exists.</summary>
    public int PageCount => Pages.Count;
    /// <summary>Gets the immutable final page for PagePrepared, or null otherwise.</summary>
    public PdfGenerationPage? Page => PageIndex < 0 ? null : Pages[PageIndex];
    /// <summary>Gets the shared operation context for cooperative callback/provider work; no worker/scope may outlive its operation.</summary>
    public PdfOperationContext OperationContext { get; }
}

/// <summary>An immutable typed reusable renderer. Call nested templates through the generation context for cycle/depth accounting.</summary>
public sealed class PdfGenerationTemplate<T>
{
    internal Action<PdfGenerationContext, T> Render { get; }

    /// <summary>Captures a bounded ASCII identifier and synchronous cooperative renderer; records are never reflected over or implicitly converted.</summary>
    public PdfGenerationTemplate(string identifier, Action<PdfGenerationContext, T> render)
    {
        if (string.IsNullOrEmpty(identifier) || identifier.Length > 128 || identifier.Any(c => !(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_' || c == '.')))
            throw new ArgumentException("A bounded ASCII template identifier is required.", nameof(identifier));
        Identifier = identifier;
        Render = render ?? throw new ArgumentNullException(nameof(render));
    }

    /// <summary>Gets the explicit trace identity. Cycle detection also retains actual template/record reference identity.</summary>
    public string Identifier { get; }
}

/// <summary>Immutable explicit layout/culture/template/hook and finite data-generation ceilings.</summary>
public sealed class PdfGenerationOptions
{
    /// <summary>Captures an explicit standard culture name (empty for invariant), layout, optional page decorations and a synchronous cooperative observer. No external resources are resolved implicitly.</summary>
    public PdfGenerationOptions(PdfFlowOptions layout, string cultureName, PdfPageTemplate? pageTemplate = null, PdfOptions? documentOptions = null, Action<PdfGenerationEvent>? observer = null, int maximumRecords = 100000, int maximumBlocks = 100000, int maximumEvents = 1000000)
    {
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        if (cultureName == null)
            throw new ArgumentNullException(nameof(cultureName));
        if (cultureName.Length > 85)
            throw new ArgumentException("A bounded culture name is required.", nameof(cultureName));
        if (maximumRecords < 1 || maximumRecords > 1000000 || maximumBlocks < 1 || maximumBlocks > 1000000 || maximumEvents < 1 || maximumEvents > 10000000)
            throw new ArgumentOutOfRangeException(nameof(maximumRecords));
        Culture = CultureInfo.ReadOnly(new CultureInfo(cultureName, false));
        PageTemplate = pageTemplate;
        DocumentOptions = documentOptions ?? PdfOptions.Default;
        Observer = observer;
        MaximumRecords = maximumRecords;
        MaximumBlocks = maximumBlocks;
        MaximumEvents = maximumEvents;
    }

    /// <summary>Gets immutable explicit layout settings.</summary>
    public PdfFlowOptions Layout { get; }
    /// <summary>Gets a captured read-only standard culture without user overrides.</summary>
    public CultureInfo Culture { get; }
    /// <summary>Gets optional final page decorations/numbering/contents convergence settings.</summary>
    public PdfPageTemplate? PageTemplate { get; }
    /// <summary>Gets the generated document's options and default operation policy.</summary>
    public PdfOptions DocumentOptions { get; }
    /// <summary>Gets the caller-owned synchronous cooperative observer; it receives immutable views.</summary>
    public Action<PdfGenerationEvent>? Observer { get; }
    /// <summary>Gets the finite source record ceiling, with at most one additional lookahead.</summary>
    public int MaximumRecords { get; }
    /// <summary>Gets the finite captured top-level block ceiling.</summary>
    public int MaximumBlocks { get; }
    /// <summary>Gets the finite dispatched observer-invocation ceiling.</summary>
    public int MaximumEvents { get; }
}

/// <summary>Immutable final-page observations without a live canvas, mutable model or unfinished operation report.</summary>
public sealed class PdfGenerationPage
{
    internal PdfGenerationPage(int index, PdfPageGeometry geometry, PdfFlowPage page)
    {
        Index = index;
        Geometry = geometry;
        ContentHeight = page.ContentHeight;
        LineCount = page.Lines.Count;
        TableFragmentCount = page.Tables.Count;
        ColumnCount = page.Columns.Count;
        NoteCount = page.Notes.Count;
    }

    /// <summary>Gets the zero-based physical page index.</summary>
    public int Index { get; }
    /// <summary>Gets explicit immutable boxes, rotation and UserUnit.</summary>
    public PdfPageGeometry Geometry { get; }
    /// <summary>Gets the occupied body extent in physical points.</summary>
    public double ContentHeight { get; }
    /// <summary>Gets final body line count.</summary>
    public int LineCount { get; }
    /// <summary>Gets final table-fragment count.</summary>
    public int TableFragmentCount { get; }
    /// <summary>Gets final logical-column count.</summary>
    public int ColumnCount { get; }
    /// <summary>Gets final note-placement count.</summary>
    public int NoteCount { get; }
}
