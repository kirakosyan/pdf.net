using System.Collections.ObjectModel;

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
    internal PdfFlowPage(int index, PdfCanvasResult canvas, PdfFlowLine[] lines, PdfFlowFloatPlacement[] floats)
    {
        Index = index;
        Canvas = canvas;
        Lines = new ReadOnlyCollection<PdfFlowLine>(lines);
        Floats = new ReadOnlyCollection<PdfFlowFloatPlacement>(floats);
    }

    /// <summary>Gets the zero-based output page index.</summary>
    public int Index { get; }
    /// <summary>Gets the completed page canvas with content-relative point coordinates.</summary>
    public PdfCanvasResult Canvas { get; }
    /// <summary>Gets immutable lines in logical block order.</summary>
    public IReadOnlyList<PdfFlowLine> Lines { get; }
    /// <summary>Gets immutable floating rectangles.</summary>
    public IReadOnlyList<PdfFlowFloatPlacement> Floats { get; }
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
    public PdfDocument CreateDocument(PdfOptions? options = null)
    {
        var document = Pdf.Create(options);
        try
        {
            foreach (var page in Pages)
            {
                var target = document.Pages.Add(Options.Geometry);
                foreach (var binding in page.Canvas.Drawing.Resources.Bindings)
                    target.SetResource(binding.Name, binding.Resource);
                target.Content = page.Canvas.Drawing.Content;
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
