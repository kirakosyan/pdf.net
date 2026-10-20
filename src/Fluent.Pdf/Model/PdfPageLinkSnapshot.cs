namespace Fluent.Pdf;
/// <summary>An immutable passive internal or URI page link in native PDF user coordinates.</summary>
public readonly struct PdfPageLinkSnapshot
{
    internal PdfPageLinkSnapshot(PdfRect bounds, int target, PdfPoint point)
    {
        Bounds = bounds;
        TargetPageIndex = target;
        TargetPoint = point;
        Uri = null;
    }

    internal PdfPageLinkSnapshot(PdfRect bounds, string uri)
    {
        Bounds = bounds;
        TargetPageIndex = -1;
        TargetPoint = default;
        Uri = uri;
    }

    /// <summary>Gets a passive external URI, or null for an internal destination. No request is made by the SDK.</summary>
    public string? Uri { get; }
    /// <summary>Gets bounds in the containing content coordinate system.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets the zero-based physical destination page index, or -1 for a URI.</summary>
    public int TargetPageIndex { get; }
    /// <summary>Gets the destination in native PDF page coordinates.</summary>
    public PdfPoint TargetPoint { get; }
}
