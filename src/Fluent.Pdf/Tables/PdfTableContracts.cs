using System.Collections.ObjectModel;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable fixed or automatically sized table column.</summary>
public sealed class PdfTableColumn
{
    private PdfTableColumn(double minimum, double maximum, double weight, bool fixedWidth)
    {
        Minimum = minimum;
        Maximum = maximum;
        Weight = weight;
        IsFixed = fixedWidth;
    }

    /// <summary>Gets the minimum width in physical points.</summary>
    public double Minimum { get; }
    /// <summary>Gets the finite maximum width in physical points.</summary>
    public double Maximum { get; }
    /// <summary>Gets the positive distribution weight for remaining automatic width.</summary>
    public double Weight { get; }
    /// <summary>Gets whether the width is fixed.</summary>
    public bool IsFixed { get; }

    /// <summary>Creates a positive finite fixed column width.</summary>
    public static PdfTableColumn Fixed(double width)
    {
        PdfGraphicsNumbers.Finite(width);
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        return new(width, width, 1, true);
    }

    /// <summary>Creates an automatic column with finite physical limits and a positive growth weight.</summary>
    public static PdfTableColumn Auto(double minimum = 0, double maximum = 14400, double weight = 1)
    {
        foreach (double value in new[]
        {
            minimum,
            maximum,
            weight
        }

        )
            PdfGraphicsNumbers.Finite(value);
        if (minimum < 0 || maximum <= 0 || maximum < minimum || weight <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimum));
        return new(minimum, maximum, weight, false);
    }
}

/// <summary>One immutable table border in physical points.</summary>
public sealed class PdfTableBorder
{
    /// <summary>Captures a nonnegative width and explicit color. Zero disables painting the border.</summary>
    public PdfTableBorder(double width = .5, PdfColor? color = null)
    {
        PdfGraphicsNumbers.Finite(width);
        if (width < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        Width = width;
        Color = color ?? new(PdfColorSpace.Gray, 0);
        if (Color.ColorSpace.IsPattern)
            throw new ArgumentException("A border requires an explicit non-pattern color.", nameof(color));
    }

    /// <summary>Gets physical width.</summary>
    public double Width { get; }
    /// <summary>Gets explicit paint color.</summary>
    public PdfColor Color { get; }
    /// <summary>Gets an immutable disabled border.</summary>
    public static PdfTableBorder None { get; } = new(0);
}

/// <summary>Immutable cell padding, edge borders, background and vertical alignment.</summary>
public sealed class PdfTableCellStyle
{
    /// <summary>Captures physical padding and edge paint. An omitted edge uses the default half-point border.</summary>
    public PdfTableCellStyle(PdfMargins? padding = null, PdfColor? background = null, PdfTableBorder? left = null, PdfTableBorder? top = null, PdfTableBorder? right = null, PdfTableBorder? bottom = null, PdfTableVerticalAlignment verticalAlignment = PdfTableVerticalAlignment.Top)
    {
        if (!Enum.IsDefined(typeof(PdfTableVerticalAlignment), verticalAlignment))
            throw new ArgumentOutOfRangeException(nameof(verticalAlignment));
        if (background?.ColorSpace.IsPattern == true)
            throw new ArgumentException("A cell background requires an explicit non-pattern color.", nameof(background));
        Padding = padding ?? PdfMargins.Uniform(4);
        Background = background;
        Left = left ?? new();
        Top = top ?? new();
        Right = right ?? new();
        Bottom = bottom ?? new();
        VerticalAlignment = verticalAlignment;
    }

    /// <summary>Gets physical cell padding.</summary>
    public PdfMargins Padding { get; }
    /// <summary>Gets the optional explicit background.</summary>
    public PdfColor? Background { get; }
    /// <summary>Gets the left border.</summary>
    public PdfTableBorder Left { get; }
    /// <summary>Gets the top border.</summary>
    public PdfTableBorder Top { get; }
    /// <summary>Gets the right border.</summary>
    public PdfTableBorder Right { get; }
    /// <summary>Gets the bottom border.</summary>
    public PdfTableBorder Bottom { get; }
    /// <summary>Gets vertical content placement inside the completed merged rectangle.</summary>
    public PdfTableVerticalAlignment VerticalAlignment { get; }
}

/// <summary>Vertical placement inside the padded cell rectangle.</summary>
public enum PdfTableVerticalAlignment
{
    /// <summary>Place at the top.</summary>
    Top,
    /// <summary>Center vertically.</summary>
    Center,
    /// <summary>Place at the bottom.</summary>
    Bottom
}

/// <summary>Immutable content and spans for one grid-origin cell. Empty content is allowed.</summary>
public sealed class PdfTableCell
{
    /// <summary>Captures up to 4096 nonnull flow blocks, finite style and positive bounded spans.</summary>
    public PdfTableCell(IEnumerable<PdfFlowBlock> content, int columnSpan = 1, int rowSpan = 1, PdfTableCellStyle? style = null)
    {
        if (content == null)
            throw new ArgumentNullException(nameof(content));
        if (columnSpan < 1 || columnSpan > 1024 || rowSpan < 1 || rowSpan > 1000000)
            throw new ArgumentOutOfRangeException(nameof(columnSpan));
        var copy = new List<PdfFlowBlock>();
        foreach (var block in content)
        {
            if (block == null || copy.Count == 4096)
                throw new ArgumentException("Cells accept up to 4096 nonnull flow blocks.", nameof(content));
            copy.Add(block);
        }

        Content = copy.AsReadOnly();
        ColumnSpan = columnSpan;
        RowSpan = rowSpan;
        Style = style ?? new();
    }

    /// <summary>Gets logical content blocks, including nested immutable tables.</summary>
    public IReadOnlyList<PdfFlowBlock> Content { get; }
    /// <summary>Gets the horizontal grid span.</summary>
    public int ColumnSpan { get; }
    /// <summary>Gets the vertical grid span.</summary>
    public int RowSpan { get; }
    /// <summary>Gets immutable cell style.</summary>
    public PdfTableCellStyle Style { get; }

    /// <summary>Creates a one-cell styled text paragraph with optional spans.</summary>
    public static PdfTableCell Text(string text, PdfFlowTextStyle textStyle, int columnSpan = 1, int rowSpan = 1, PdfTableCellStyle? style = null) => new(new[] { new PdfFlowParagraph(text, textStyle) }, columnSpan, rowSpan, style);
}

/// <summary>Immutable grid-origin cells and row constraints.</summary>
public sealed class PdfTableRow
{
    /// <summary>Captures up to 1024 nonnull cells, a finite minimum height and explicit split permission.</summary>
    public PdfTableRow(IEnumerable<PdfTableCell> cells, double minimumHeight = 0, bool allowSplit = true)
    {
        if (cells == null)
            throw new ArgumentNullException(nameof(cells));
        PdfGraphicsNumbers.Finite(minimumHeight);
        if (minimumHeight < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumHeight));
        var copy = new List<PdfTableCell>();
        foreach (var cell in cells)
        {
            if (cell == null || copy.Count == 1024)
                throw new ArgumentException("Rows accept up to 1024 nonnull cells.", nameof(cells));
            copy.Add(cell);
        }

        Cells = copy.AsReadOnly();
        MinimumHeight = minimumHeight;
        AllowSplit = allowSplit;
    }

    /// <summary>Gets cells whose origins occur on this row; spans from earlier rows occupy their grid slots.</summary>
    public IReadOnlyList<PdfTableCell> Cells { get; }
    /// <summary>Gets physical minimum height.</summary>
    public double MinimumHeight { get; }
    /// <summary>Gets whether safe text-line boundaries may split this row.</summary>
    public bool AllowSplit { get; }
}

/// <summary>An immutable validated grid, also usable as a flow block.</summary>
public sealed class PdfTable : PdfFlowBlock
{
    internal PdfTable(PdfTableColumn[] columns, PdfTableRow[] rows, int headers, bool split, PdfTableGridCell[] grid)
    {
        Columns = new ReadOnlyCollection<PdfTableColumn>(columns);
        Rows = new ReadOnlyCollection<PdfTableRow>(rows);
        HeaderRows = headers;
        AllowRowSplit = split;
        Grid = grid;
    }

    /// <summary>Gets explicit immutable column definitions.</summary>
    public IReadOnlyList<PdfTableColumn> Columns { get; }
    /// <summary>Gets immutable rows in logical source order.</summary>
    public IReadOnlyList<PdfTableRow> Rows { get; }
    /// <summary>Gets the count of complete leading rows repeated on every fragment page.</summary>
    public int HeaderRows { get; }
    /// <summary>Gets whether explicitly splittable rows may cross page boundaries.</summary>
    public bool AllowRowSplit { get; }
    /// <summary>Gets the successful bounded grid-construction or data-binding report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;
    internal PdfTableGridCell[] Grid { get; }

    /// <summary>Snapshots a bounded row source, validates merged slots and rejects header/body span crossings.</summary>
    public static PdfTable Create(PdfTableColumn[] columns, IEnumerable<PdfTableRow> rows, int headerRows = 0, bool allowRowSplit = true, int maximumRows = 100000, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (columns == null)
            throw new ArgumentNullException(nameof(columns));
        if (rows == null)
            throw new ArgumentNullException(nameof(rows));
        if (columns.Length < 1 || columns.Length > 1024 || columns.Any(c => c == null))
            throw new ArgumentException("Supply one through 1024 nonnull columns.", nameof(columns));
        if (maximumRows < 1 || maximumRows > 1000000 || headerRows < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumRows));
        PdfTable? result = null;
        var report = PdfFont.Run("CreateTable", "managed.table", policy, cancellationToken, c => result = CreateCore(columns, rows, headerRows, allowRowSplit, maximumRows, c), PdfCapability.WriteDocument);
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Lays out this table through the same bounded flow pipeline as paragraphs and nested content.</summary>
    public PdfFlowResult Layout(PdfFlowOptions options, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfFlow.Layout(new PdfFlowBlock[] { this }, options, providers, policy, cancellationToken);
    internal static PdfTable CreateCore(PdfTableColumn[] columns, IEnumerable<PdfTableRow> rows, int headers, bool split, int maximumRows, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, columns.Length * 32L + 256);
        var copy = new List<PdfTableRow>();
        using (var enumerator = rows.GetEnumerator())
        {
            while (true)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                if (!enumerator.MoveNext())
                    break;
                if (copy.Count == maximumRows)
                    throw PdfWriterCore.Error(context, "Table.RowLimit", "The row source exceeds its explicit finite limit.");
                var row = enumerator.Current ?? throw new ArgumentException("Rows cannot contain null.", nameof(rows));
                context.Consume(PdfResourceKind.ExpandedBytes, row.Cells.Count * 64L + 128);
                copy.Add(row);
            }
        }

        if (headers > copy.Count)
            throw new ArgumentOutOfRangeException(nameof(headers));
        var grid = PdfTableGrid.Validate(columns.Length, copy, headers, context);
        return new((PdfTableColumn[])columns.Clone(), copy.ToArray(), headers, split, grid);
    }
}

internal readonly struct PdfTableGridCell
{
    internal PdfTableGridCell(int row, int column, PdfTableCell cell)
    {
        Row = row;
        Column = column;
        Cell = cell;
    }

    internal int Row { get; }
    internal int Column { get; }
    internal PdfTableCell Cell { get; }
}

internal static class PdfTableGrid
{
    internal static PdfTableGridCell[] Validate(int columns, IReadOnlyList<PdfTableRow> rows, int headers, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, columns * 8L + 256);
        var occupiedUntil = new int[columns];
        var cells = new List<PdfTableGridCell>();
        for (int row = 0; row < rows.Count; row++)
        {
            context.Checkpoint();
            int column = 0;
            foreach (var cell in rows[row].Cells)
            {
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 96);
                while (column < columns && occupiedUntil[column] > row)
                    column++;
                if (column + cell.ColumnSpan > columns || cell.RowSpan > rows.Count - row)
                    throw Error("Table.SpanBounds", "A cell span exceeds the declared grid.");
                if (row < headers && row + cell.RowSpan > headers)
                    throw Error("Table.HeaderSpan", "A merged cell cannot cross the repeated-header/body boundary.");
                for (int c = column; c < column + cell.ColumnSpan; c++)
                {
                    if (occupiedUntil[c] > row)
                        throw Error("Table.SpanOverlap", "Merged cell slots overlap.");
                    occupiedUntil[c] = row + cell.RowSpan;
                }

                cells.Add(new(row, column, cell));
                column += cell.ColumnSpan;
            }

            for (int c = 0; c < columns; c++)
                if (occupiedUntil[c] <= row)
                    throw Error("Table.GridHole", "Every grid slot must be occupied by an explicit cell or an earlier span.");
        }

        return cells.ToArray();
        PdfException Error(string code, string message) => PdfWriterCore.Error(context, code, message);
    }
}
