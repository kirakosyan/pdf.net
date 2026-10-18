using System.Collections.ObjectModel;

namespace Fluent.Pdf;
/// <summary>An immutable logical column instance in a physical point rectangle.</summary>
public sealed class PdfFlowColumnPlacement
{
    internal PdfFlowColumnPlacement(int block, int logical, int physical, PdfRect bounds, PdfFlowPage source)
    {
        BlockIndex = block;
        LogicalColumnIndex = logical;
        PhysicalColumnIndex = physical;
        Bounds = bounds;
        Source = source;
    }

    /// <summary>Gets the expanded zero-based logical source block index.</summary>
    public int BlockIndex { get; }
    /// <summary>Gets the sequential logical column index across physical pages.</summary>
    public int LogicalColumnIndex { get; }
    /// <summary>Gets the physical slot on its page.</summary>
    public int PhysicalColumnIndex { get; }
    /// <summary>Gets a content-relative rectangle in physical points.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets the immutable source column page, including its logical lines and local notes.</summary>
    public PdfFlowPage Source { get; }
    internal int PlacementIndex { get; set; }
}

/// <summary>A note's physical placement and source references on a flow page.</summary>
public sealed class PdfFlowNotePlacement
{
    internal PdfFlowNotePlacement(PdfFlowNote note, PdfRect bounds, bool endnote, int[] references)
    {
        Note = note;
        Bounds = bounds;
        IsEndnote = endnote;
        ReferenceBlockIndices = new ReadOnlyCollection<int>(references);
    }

    /// <summary>Gets its immutable note definition.</summary>
    public PdfFlowNote Note { get; }
    /// <summary>Gets a content-relative rectangle in physical points.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets whether the note is appended after body content.</summary>
    public bool IsEndnote { get; }
    /// <summary>Gets logical source references associated with this note instance.</summary>
    public IReadOnlyList<int> ReferenceBlockIndices { get; }
}
