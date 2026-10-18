using System.Collections.ObjectModel;

namespace Fluent.Pdf;

/// <summary>An immutable, detached page-model snapshot passed to writers.</summary>
/// <remarks>Snapshots remain valid after the source document changes or is disposed. They are not PDF bytes.</remarks>
public sealed class PdfDocumentSnapshot
{
    internal PdfDocumentSnapshot(PdfOptions options, PdfPageSnapshot[] pages,PdfSyntaxDocument? source=null,IReadOnlyList<PdfObjectId>? originalOrder=null,PdfWriteOptions? writeOptions=null)
    {
        Options = options;
        Pages = new ReadOnlyCollection<PdfPageSnapshot>(pages);
        SourceDocument=source;OriginalOrder=originalOrder??System.Array.Empty<PdfObjectId>();WriteOptions=writeOptions??PdfWriteOptions.Default;
    }

    /// <summary>Gets the immutable options captured from the source session.</summary>
    public PdfOptions Options { get; }
    /// <summary>Gets immutable pages in document order.</summary>
    public IReadOnlyList<PdfPageSnapshot> Pages { get; }
    /// <summary>Gets the immutable original graph, or null for a newly authored document.</summary>
    public PdfSyntaxDocument? SourceDocument{get;}
    /// <summary>Gets the immutable save choices captured for this writer invocation.</summary>
    public PdfWriteOptions WriteOptions{get;}
    internal IReadOnlyList<PdfObjectId> OriginalOrder{get;}
}

/// <summary>A detached, immutable page geometry value.</summary>
public readonly struct PdfPageSnapshot
{
    internal PdfPageSnapshot(PdfPageSize size, PdfPageRotation rotation,PdfPageSource? source=null,PdfContentProgram? content=null,IReadOnlyDictionary<string,PdfFontResource>? fonts=null,PdfResourceSet? graphics=null,PdfPageGeometry? geometry=null,IReadOnlyList<PdfCanvasPageLayer>? canvasLayers=null) { CanvasLayers=canvasLayers??System.Array.Empty<PdfCanvasPageLayer>();Geometry=geometry??new PdfPageGeometry(new PdfPageBoxes(new PdfRect(0,0,size.Width,size.Height)),rotation);GraphicsResources=graphics??PdfResourceSet.Empty;Size = size; Rotation = rotation;Source=source;Content=content;FontResources=fonts??new System.Collections.ObjectModel.ReadOnlyDictionary<string,PdfFontResource>(new Dictionary<string,PdfFontResource>()); }
    /// <summary>Gets the unrotated physical size.</summary>
    public PdfPageSize Size { get; }
    /// <summary>Gets the clockwise display rotation.</summary>
    public PdfPageRotation Rotation { get; }
    /// <summary>Gets the immutable original page binding, or null for a newly authored page.</summary>
    public PdfPageSource? Source{get;}
    /// <summary>Gets immutable replacement content, or null to retain original bytes.</summary>
    public PdfContentProgram? Content{get;}
    /// <summary>Gets generated immutable page-content layers in application order.</summary>
    public IReadOnlyList<PdfCanvasPageLayer> CanvasLayers{get;}
    /// <summary>Gets immutable page boxes, UserUnit and rotation.</summary>
    public PdfPageGeometry Geometry{get;}
    /// <summary>Gets immutable named replacement graphics resources.</summary>
    public PdfResourceSet GraphicsResources{get;}
    /// <summary>Gets immutable named replacement font resources.</summary>
    public IReadOnlyDictionary<string,PdfFontResource> FontResources{get;}
}
