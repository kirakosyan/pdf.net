using System.Collections.ObjectModel;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable line with exact logical paragraph provenance and physical point geometry.</summary>
public sealed class PdfFlowLine
{
    internal PdfFlowLine(int block, int start, int length, string text, PdfRect bounds, double baseline, double advance, PdfShapedText shaped, bool hardBreak, bool clipped)
    {
        BlockIndex = block;
        Utf16Offset = start;
        Utf16Length = length;
        Text = text;
        Bounds = bounds;
        Baseline = baseline;
        Advance = advance;
        ShapedText = shaped;
        EndsHardBreak = hardBreak;
        IsClipped = clipped;
    }

    /// <summary>Gets the zero-based source block index.</summary>
    public int BlockIndex { get; }
    /// <summary>Gets the UTF-16 offset in the logical source paragraph.</summary>
    public int Utf16Offset { get; }
    /// <summary>Gets the logical UTF-16 extent.</summary>
    public int Utf16Length { get; }
    /// <summary>Gets unchanged logical Unicode.</summary>
    public string Text { get; }
    /// <summary>Gets the available rectangle relative to the page content origin, in physical points.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets the baseline below the page content top, in physical points.</summary>
    public double Baseline { get; }
    /// <summary>Gets the natural visible text advance before alignment or justification.</summary>
    public double Advance { get; }
    /// <summary>Gets immutable runs shaped with retained paragraph levels and line-specific reordering.</summary>
    public PdfShapedText ShapedText { get; }
    /// <summary>Gets whether the line ends at an explicit logical line separator.</summary>
    public bool EndsHardBreak { get; }
    /// <summary>Gets whether explicit diagnosed overflow clipping applies.</summary>
    public bool IsClipped { get; }
    /// <summary>Gets whether visual shaping appends a discretionary hyphen omitted from the unchanged logical Text and ActualText.</summary>
    public bool HasDiscretionaryHyphen => ShapedText.Unicode.Text.Length == Text.Length + 1 && ShapedText.Unicode.Text.EndsWith("-", StringComparison.Ordinal);
}

/// <summary>An immutable float placement in displayed physical point coordinates.</summary>
public sealed class PdfFlowFloatPlacement
{
    internal PdfFlowFloatPlacement(int block, PdfFlowFloat value, PdfRect bounds)
    {
        BlockIndex = block;
        Float = value;
        Bounds = bounds;
    }

    /// <summary>Gets the zero-based source block index.</summary>
    public int BlockIndex { get; }
    /// <summary>Gets the original immutable float description.</summary>
    public PdfFlowFloat Float { get; }
    /// <summary>Gets the available rectangle relative to the page content origin, in physical points.</summary>
    public PdfRect Bounds { get; }
}

/// <summary>An immutable page with line/float geometry and its complete reusable canvas.</summary>
public sealed class PdfFlowPage
{
    internal PdfFlowPage(int index, PdfCanvasResult canvas, PdfFlowLine[] lines, PdfFlowFloatPlacement[] floats, PdfTableFragment[] tables, double contentHeight, PdfFlowColumnPlacement[] columns, PdfFlowNotePlacement[] notes, PdfFlowHeadingPlacement[] headings, PdfTemplateLocator[] locators)
    {
        Index = index;
        Canvas = canvas;
        Lines = new ReadOnlyCollection<PdfFlowLine>(lines);
        Floats = new ReadOnlyCollection<PdfFlowFloatPlacement>(floats);
        Tables = new ReadOnlyCollection<PdfTableFragment>(tables);
        ContentHeight = contentHeight;
        Columns = new ReadOnlyCollection<PdfFlowColumnPlacement>(columns);
        Notes = new ReadOnlyCollection<PdfFlowNotePlacement>(notes);
        Headings = new ReadOnlyCollection<PdfFlowHeadingPlacement>(headings);
        TemplateLocators = locators;
    }

    internal PdfTemplateLocator[] TemplateLocators { get; }
    /// <summary>Gets the zero-based output page index.</summary>
    public int Index { get; }
    /// <summary>Gets the completed page canvas with content-relative point coordinates.</summary>
    public PdfCanvasResult Canvas { get; }
    /// <summary>Gets immutable lines in logical block order.</summary>
    public IReadOnlyList<PdfFlowLine> Lines { get; }
    /// <summary>Gets immutable floating rectangles.</summary>
    public IReadOnlyList<PdfFlowFloatPlacement> Floats { get; }
    /// <summary>Gets immutable table header/body slices on this page.</summary>
    public IReadOnlyList<PdfTableFragment> Tables { get; }
    /// <summary>Gets the occupied vertical extent below the page content top, in physical points.</summary>
    public double ContentHeight { get; }
    /// <summary>Gets logical columns in content-stream and structure order.</summary>
    public IReadOnlyList<PdfFlowColumnPlacement> Columns { get; }
    /// <summary>Gets footnotes/endnotes and their reference associations.</summary>
    public IReadOnlyList<PdfFlowNotePlacement> Notes { get; }
    /// <summary>Gets resolved explicit body headings on this page.</summary>
    public IReadOnlyList<PdfFlowHeadingPlacement> Headings { get; }
}

/// <summary>A detached deterministic layout, ready for independent page inspection and managed writing.</summary>
public sealed class PdfFlowResult
{
    internal PdfFlowResult(PdfFlowOptions options, PdfFlowPage[] pages)
    {
        Options = options;
        Pages = new ReadOnlyCollection<PdfFlowPage>(pages);
    }

    /// <summary>Gets captured immutable layout options.</summary>
    public PdfFlowOptions Options { get; }
    /// <summary>Gets immutable output pages.</summary>
    public IReadOnlyList<PdfFlowPage> Pages { get; }
    /// <summary>Gets the successful shared-context layout report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;

    /// <summary>Creates a caller-owned document from already laid-out pages. Save is a separate bounded operation.</summary>
    public PdfDocument CreateDocument(PdfOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= PdfOptions.Default;
        PdfDocument? document = null;
        var completed = new List<Action<PdfOperationReport>>();
        try
        {
            var report = PdfFont.Run("CreateFlowDocument", "managed.composition", options.OperationPolicy, cancellationToken, context => document = CreateDocumentCore(options, null, context, completed), PdfCapability.WriteDocument);
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
        var document = Pdf.Create(options, providers);
        try
        {
            using var modelScope = document.UseModelContext(context);
            bool tagged = Pages.Any(page => page.Columns.Count > 0);
            var finish = completed;
            foreach (var page in Pages)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 512);
                if (document.Pages.Count >= options.MaximumPages)
                    context.Report(new("Limit.Pages", "The document page limit was reached."));
                var target = document.Pages.Add(Options.Geometry);
                if (tagged)
                {
                    var value = PdfFlowTaggedPage.Build(page, Options, context, finish);
                    foreach (var binding in value.Resources.Bindings)
                        target.SetResource(binding.Name, binding.Resource);
                    target.Content = value.Content;
                    target.SetStructureRoles(value.Roles);
                }
                else
                {
                    foreach (var binding in page.Canvas.Drawing.Resources.Bindings)
                        target.SetResource(binding.Name, binding.Resource);
                    target.Content = page.Canvas.Drawing.Content;
                }
            }

            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }
}
