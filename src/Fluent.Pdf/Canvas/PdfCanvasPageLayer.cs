using System.Threading;

namespace Fluent.Pdf;
/// <summary>How a completed canvas combines with a page's existing content.</summary>
public enum PdfCanvasApplyMode
{
    /// <summary>Paint in front of existing content and earlier overlays.</summary>
    Overlay,
    /// <summary>Paint behind existing content and earlier underlays.</summary>
    Underlay,
    /// <summary>Replace the visible content program and prior canvas layers.</summary>
    Replace
}

/// <summary>An immutable generated page-content layer, exposed in writer snapshots.</summary>
public sealed class PdfCanvasPageLayer
{
    internal PdfCanvasPageLayer(PdfCanvasApplyMode mode, string name, PdfContentProgram content)
    {
        Mode = mode;
        ResourceName = name;
        Content = content;
    }

    /// <summary>Gets its placement relative to original/replacement base content.</summary>
    public PdfCanvasApplyMode Mode { get; }
    /// <summary>Gets its generated XObject resource name.</summary>
    public string ResourceName { get; }
    /// <summary>Gets its isolated Do program.</summary>
    public PdfContentProgram Content { get; }
}

public sealed partial class PdfCanvasResult
{
    /// <summary>Atomically applies to a page with identical geometry. Original streams remain encoded and are isolated from new content.</summary>
    public PdfOperationReport ApplyTo(PdfPage page, PdfCanvasApplyMode mode = PdfCanvasApplyMode.Overlay, CancellationToken cancellationToken = default)
    {
        if (page == null)
            throw new ArgumentNullException(nameof(page));
        if (!Enum.IsDefined(typeof(PdfCanvasApplyMode), mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        return page.ApplyCanvas(this, mode, cancellationToken);
    }
}
