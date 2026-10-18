using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit optional HTML fragment service. The core supplies no HTML/browser engine.</summary>
public interface IPdfTemplateHtmlProvider : IPdfProvider
{
    /// <summary>Renders an explicit HTML fragment under the shared context; no implicit browser is supplied.</summary>
    PdfDrawing Render(string html, PdfPageSize viewport, PdfOperationContext context);
}

/// <summary>Immutable explicit flow, vector/image or optional HTML decoration.</summary>
public sealed class PdfPageDecoration
{
    private PdfPageDecoration()
    {
    }

    internal IReadOnlyList<PdfFlowBlock>? Blocks { get; private set; }
    internal PdfDrawing? Drawing { get; private set; }
    internal PdfRect SourceBounds { get; private set; }
    internal string? HtmlText { get; private set; }
    internal IPdfTemplateHtmlProvider? HtmlProvider { get; private set; }

    /// <summary>Creates a decoration from bounded immutable flow blocks.</summary>
    public static PdfPageDecoration Flow(IEnumerable<PdfFlowBlock> content) => new()
    {
        Blocks = PdfFlowCapture.Blocks(content)
    };
    /// <summary>Creates a fitted vector or image drawing decoration with explicit source bounds.</summary>
    public static PdfPageDecoration Vector(PdfDrawing drawing, PdfRect sourceBounds)
    {
        if (drawing == null)
            throw new ArgumentNullException(nameof(drawing));
        if (sourceBounds.Width <= 0 || sourceBounds.Height <= 0)
            throw new ArgumentException("A positive source frame is required.", nameof(sourceBounds));
        return new()
        {
            Drawing = drawing,
            SourceBounds = sourceBounds
        };
    }

    /// <summary>Creates an HTML decoration requiring an explicit optional HTML provider.</summary>
    public static PdfPageDecoration Html(string html, IPdfTemplateHtmlProvider? provider = null)
    {
        if (html == null || html.Length > 1000000)
            throw new ArgumentException("A bounded explicit HTML fragment is required.", nameof(html));
        return new()
        {
            HtmlText = html,
            HtmlProvider = provider
        };
    }
}

/// <summary>Final stable page data for one synchronous header/footer callback.</summary>
public sealed class PdfPageTemplateContext
{
    internal PdfPageTemplateContext(int page, int count, string label, PdfFlowPage body, PdfOperationContext context)
    {
        PageIndex = page;
        TotalPages = count;
        PageLabel = label;
        Body = body;
        OperationContext = context;
    }

    /// <summary>Gets the zero-based physical page index.</summary>
    public int PageIndex { get; }
    /// <summary>Gets the one-based physical page number.</summary>
    public int PageNumber => PageIndex + 1;
    /// <summary>Gets the final converged physical page count.</summary>
    public int TotalPages { get; }
    /// <summary>Gets the final formatted page label.</summary>
    public string PageLabel { get; }
    /// <summary>Gets whether this is the first physical page.</summary>
    public bool IsFirstPage => PageIndex == 0;
    /// <summary>Gets whether the one-based physical page number is odd.</summary>
    public bool IsOddPage => (PageIndex & 1) == 0;
    /// <summary>Gets the final body layout.</summary>
    public PdfFlowPage Body { get; }
    /// <summary>Gets the shared context for synchronous callback work.</summary>
    public PdfOperationContext OperationContext { get; }
}

/// <summary>Finite reserved header/footer regions selected from final page context.</summary>
public sealed class PdfPageTemplate
{
    /// <summary>Creates fixed header/footer reservations and synchronous final-page callbacks.</summary>
    public PdfPageTemplate(double headerHeight = 0, double footerHeight = 0, double gap = 6, Func<PdfPageTemplateContext, PdfPageDecoration?>? header = null, Func<PdfPageTemplateContext, PdfPageDecoration?>? footer = null, PdfPageNumbering? numbering = null, int maximumPasses = 8)
    {
        foreach (double value in new[]
        {
            headerHeight,
            footerHeight,
            gap
        }

        )
            PdfGraphicsNumbers.Finite(value);
        if (headerHeight < 0 || footerHeight < 0 || gap < 0 || header != null && headerHeight == 0 || footer != null && footerHeight == 0)
            throw new ArgumentOutOfRangeException(nameof(headerHeight));
        if (maximumPasses < 2 || maximumPasses > 32)
            throw new ArgumentOutOfRangeException(nameof(maximumPasses));
        HeaderHeight = headerHeight;
        FooterHeight = footerHeight;
        Gap = gap;
        Header = header;
        Footer = footer;
        Numbering = numbering ?? new();
        MaximumPasses = maximumPasses;
    }

    /// <summary>Gets the reserved header height in points.</summary>
    public double HeaderHeight { get; }
    /// <summary>Gets the reserved footer height in points.</summary>
    public double FooterHeight { get; }
    /// <summary>Gets the gap between decoration and body in points.</summary>
    public double Gap { get; }
    /// <summary>Gets the final-page header callback.</summary>
    public Func<PdfPageTemplateContext, PdfPageDecoration?>? Header { get; }
    /// <summary>Gets the final-page footer callback.</summary>
    public Func<PdfPageTemplateContext, PdfPageDecoration?>? Footer { get; }
    /// <summary>Gets the immutable page numbering configuration.</summary>
    public PdfPageNumbering Numbering { get; }
    /// <summary>Gets the finite convergence pass limit.</summary>
    public int MaximumPasses { get; }
}

/// <summary>Final body/decoration output with resolved page labels and contents/index destinations.</summary>
public sealed class PdfPageTemplateResult
{
    internal PdfPageTemplateResult(PdfFlowResult body, PdfCanvasResult[] decorations, PdfPageTemplate template, PdfContentsEntry[] entries, PdfTemplateLink[] links, int passes)
    {
        Body = body;
        Decorations = System.Array.AsReadOnly(decorations);
        Template = template;
        Entries = System.Array.AsReadOnly(entries);
        Links = links;
        Passes = passes;
    }

    /// <summary>Gets the final body layout.</summary>
    public PdfFlowResult Body { get; }
    /// <summary>Gets the final artifact decorations in physical page order.</summary>
    public IReadOnlyList<PdfCanvasResult> Decorations { get; }
    /// <summary>Gets the template configuration.</summary>
    public PdfPageTemplate Template { get; }
    /// <summary>Gets the resolved body heading destinations.</summary>
    public IReadOnlyList<PdfContentsEntry> Entries { get; }
    /// <summary>Gets the number of body layout passes.</summary>
    public int Passes { get; }
    /// <summary>Gets the completed bounded layout report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;
    internal PdfTemplateLink[] Links { get; }

    /// <summary>Creates a document with final decorations, page labels and internal locator links under a new bounded operation.</summary>
    public PdfDocument CreateDocument(PdfOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= PdfOptions.Default;
        PdfDocument? document = null;
        var completed = new List<Action<PdfOperationReport>>();
        try
        {
            var report = PdfFont.Run("CreateTemplateDocument", "managed.templates", options.OperationPolicy, cancellationToken, context => document = CreateDocumentCore(options, null, context, completed), PdfCapability.WriteDocument);
            foreach (var complete in completed)
                complete(report);
            document!.SetCompositionReport(report);
            return document;
        }
        catch
        {
            document?.Dispose();
            throw;
        }
    }

    internal PdfDocument CreateDocumentCore(PdfOptions options, PdfProviderRegistry? providers, PdfOperationContext context, List<Action<PdfOperationReport>> completed)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 512);
        var document = Body.CreateDocumentCore(options, providers, context, completed);
        try
        {
            using var modelScope = document.UseModelContext(context);
            for (int i = 0; i < document.Pages.Count; i++)
            {
                context.Checkpoint();
                var page = document.Pages[i];
                var decoration = Decorations[i];
                if (decoration.Placements.Count == 0)
                    continue;
                var form = PdfGraphicsObjects.FormCore(decoration.Drawing, page.Geometry.Boxes.Media, null, null, context);
                string name = "Decoration";
                int suffix = 0;
                while (page.GraphicsResources.Bindings.Any(b => b.Name == name))
                    name = "Decoration" + (++suffix).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var roles = page.StructureRoles;
                context.Consume(PdfResourceKind.ExpandedBytes, page.Content!.Operations.Count * 32L + 512);
                var operations = new List<PdfContentOperation>(page.Content.Operations);
                operations.Add(new(PdfOperator.SaveState));
                operations.Add(new(PdfOperator.BeginMarkedContent, PdfValue.Name("Artifact")));
                operations.Add(new(PdfOperator.DrawObject, PdfValue.Name(name)));
                operations.Add(new(PdfOperator.EndMarkedContent));
                operations.Add(new(PdfOperator.RestoreState));
                var content = PdfContentProgram.CreateCore(operations, PdfContentKind.Page, context);
                completed.Add(r =>
                {
                    form.OperationReport = r;
                    content.OperationReport = r;
                });
                page.SetResource(name, form);
                page.Content = content;
                page.SetStructureRoles(roles);
            }

            foreach (var link in Links)
                document.Pages[link.SourcePage].AddDeferredLink(link.Bounds, document.Pages[link.TargetPage], link.Point, context);
            document.SetPageNumbering(Template.Numbering);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }
}

internal readonly struct PdfTemplateLink
{
    internal PdfTemplateLink(int source, PdfRect bounds, int target, PdfPoint point)
    {
        SourcePage = source;
        Bounds = bounds;
        TargetPage = target;
        Point = point;
    }

    internal int SourcePage { get; }
    internal PdfRect Bounds { get; }
    internal int TargetPage { get; }
    internal PdfPoint Point { get; }
}
