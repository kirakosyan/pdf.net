using System.Collections.ObjectModel;

namespace Fluent.Pdf;
/// <summary>A visible cell rectangle on one table fragment, retaining its original merged-grid identity.</summary>
public sealed class PdfTableCellFragment
{
    internal PdfTableCellFragment(PdfTableGridCell grid, PdfRect bounds, bool repeated, bool continuation)
    {
        Row = grid.Row;
        Column = grid.Column;
        Cell = grid.Cell;
        Bounds = bounds;
        IsRepeatedHeader = repeated;
        IsContinuation = continuation;
    }

    /// <summary>Gets the original zero-based row origin.</summary>
    public int Row { get; }
    /// <summary>Gets the original zero-based column origin.</summary>
    public int Column { get; }
    /// <summary>Gets the immutable cell, including its row/column spans.</summary>
    public PdfTableCell Cell { get; }
    /// <summary>Gets the visible cell rectangle relative to the page content origin, in physical points.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets whether this is a repeated header instance.</summary>
    public bool IsRepeatedHeader { get; }
    /// <summary>Gets whether an earlier body fragment contains this merged cell's beginning.</summary>
    public bool IsContinuation { get; }
}

/// <summary>An explicit nested-table header repeated in a reserved continuation band.</summary>
public sealed class PdfTableHeaderRepeat
{
    internal PdfTableHeaderRepeat(PdfTableRepeatSource source, PdfRect bounds)
    {
        Source = source;
        Table = source.Table;
        Bounds = bounds;
    }

    /// <summary>Gets the original nested table whose complete header rows are repeated.</summary>
    public PdfTable Table { get; }
    /// <summary>Gets the reserved header rectangle relative to the page content origin, in physical points.</summary>
    public PdfRect Bounds { get; }
    internal PdfTableRepeatSource Source { get; }
}

/// <summary>An immutable repeated-header/body slice of a table on one flow page.</summary>
public sealed class PdfTableFragment
{
    internal PdfTableFragment(int block, PdfTablePlan plan, PdfRect bounds, double start, double end, bool repeated, PdfTableCellFragment[] cells, PdfTableInterval[] unsafeIntervals, PdfTableHeaderRepeat[] nestedHeaders, double nestedBandHeight)
    {
        BlockIndex = block;
        Table = plan.Table;
        Plan = plan;
        Bounds = bounds;
        BodyStart = start;
        BodyEnd = end;
        IsRepeatedHeader = repeated;
        Cells = new ReadOnlyCollection<PdfTableCellFragment>(cells);
        UnsafeIntervals = unsafeIntervals;
        NestedHeaders = new ReadOnlyCollection<PdfTableHeaderRepeat>(nestedHeaders);
        NestedHeaderBandHeight = nestedBandHeight;
    }

    /// <summary>Gets the zero-based source flow block index.</summary>
    public int BlockIndex { get; }
    /// <summary>Gets the original immutable table.</summary>
    public PdfTable Table { get; }
    /// <summary>Gets the displayed table rectangle relative to the page content origin, in physical points.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets the body slice start in the complete table's vertical sheet coordinates.</summary>
    public double BodyStart { get; }
    /// <summary>Gets the body slice end in the complete table's vertical sheet coordinates.</summary>
    public double BodyEnd { get; }
    /// <summary>Gets whether this fragment repeats the leading header rows after an earlier fragment.</summary>
    public bool IsRepeatedHeader { get; }
    /// <summary>Gets visible cell rectangles with original merged-grid identities.</summary>
    public IReadOnlyList<PdfTableCellFragment> Cells { get; }
    /// <summary>Gets explicit nested headers repeated after this table's own header rows.</summary>
    public IReadOnlyList<PdfTableHeaderRepeat> NestedHeaders { get; }
    /// <summary>Gets common physical band height reserved for nested continuation headers.</summary>
    public double NestedHeaderBandHeight { get; }
    internal PdfTablePlan Plan { get; }
    internal PdfTableInterval[] UnsafeIntervals { get; }
}

internal readonly struct PdfTableInterval
{
    internal PdfTableInterval(double start, double end)
    {
        Start = start;
        End = end;
    }

    internal double Start { get; }
    internal double End { get; }
}

internal sealed class PdfTableCellContent
{
    internal PdfTableCellContent(PdfFlowResult? flow, double width, double height, PdfTableContentSegment[] segments, PdfTableInterval[] unsafeIntervals, PdfTableRepeatSource[] repeats)
    {
        Flow = flow;
        Width = width;
        Height = height;
        Segments = segments;
        Repeats = repeats;
        UnsafeIntervals = unsafeIntervals;
    }

    internal PdfFlowResult? Flow { get; }
    internal double Width { get; }
    internal double Height { get; }
    internal PdfTableContentSegment[] Segments { get; }
    internal PdfTableRepeatSource[] Repeats { get; }
    internal PdfTableInterval[] UnsafeIntervals { get; }
}

internal sealed class PdfTableRepeatSource
{
    internal PdfTableRepeatSource(PdfTable table, PdfCanvasPlacement[] sources, PdfRect sourceBounds, double x, double start, double end, int depth)
    {
        Table = table;
        Sources = sources;
        SourceBounds = sourceBounds;
        X = x;
        Start = start;
        End = end;
        Depth = depth;
    }

    internal PdfTable Table { get; }
    internal PdfCanvasPlacement[] Sources { get; }
    internal PdfRect SourceBounds { get; }
    internal double X { get; }
    internal double Start { get; }
    internal double End { get; }
    internal int Depth { get; }
}

internal readonly struct PdfTableContentSegment
{
    internal PdfTableContentSegment(PdfCanvasPlacement placement, double y)
    {
        Placement = placement;
        Y = y;
    }

    internal PdfCanvasPlacement Placement { get; }
    internal double Y { get; }
}

internal sealed class PdfTablePreparedCell
{
    internal PdfTablePreparedCell(PdfTableGridCell grid, PdfTableCellContent content, PdfRect bounds, double contentY)
    {
        Grid = grid;
        Content = content;
        Bounds = bounds;
        ContentY = contentY;
    }

    internal PdfTableGridCell Grid { get; }
    internal PdfTableCellContent Content { get; }
    internal PdfRect Bounds { get; }
    internal double ContentY { get; }
}

// Detached geometry/resources only. A completed plan never retains an active operation context or builder.
internal sealed class PdfTablePlan
{
    internal PdfTablePlan(PdfTable table, double[] columns, double[] rows, PdfTablePreparedCell[] cells, PdfTableInterval[] unsafeIntervals)
    {
        Table = table;
        Columns = columns;
        Rows = rows;
        Cells = cells;
        UnsafeIntervals = unsafeIntervals;
    }

    internal PdfTable Table { get; }
    internal double[] Columns { get; }
    internal double[] Rows { get; }
    internal PdfTablePreparedCell[] Cells { get; }
    internal PdfTableInterval[] UnsafeIntervals { get; }
    internal double Width => Columns[Columns.Length - 1];
    internal double Height => Rows[Rows.Length - 1];
    internal double HeaderHeight => Rows[Table.HeaderRows];
}
