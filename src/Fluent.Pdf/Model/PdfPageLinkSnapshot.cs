namespace Fluent.Pdf;
/// <summary>An immutable resolved internal page link in native PDF user coordinates.</summary>
public readonly struct PdfPageLinkSnapshot
{
    internal PdfPageLinkSnapshot(PdfRect bounds, int target, PdfPoint point)
    {
        Bounds = bounds;
        TargetPageIndex = target;
        TargetPoint = point;
    }

    /// <summary>Gets bounds in the containing content coordinate system.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets the zero-based physical destination page index.</summary>
    public int TargetPageIndex { get; }
    /// <summary>Gets the destination in native PDF page coordinates.</summary>
    public PdfPoint TargetPoint { get; }
}
