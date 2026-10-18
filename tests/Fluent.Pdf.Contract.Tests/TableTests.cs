using System.Collections;
using System.Globalization;
using System.IO;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class TableTests
{
    private static PdfFlowTextStyle Style() => new(new(TextTests.Read("latin.ttf")));
    private static PdfProviderRegistry Providers() => new(new PdfSimpleTextShaper());
    private static PdfFlowOptions Options(double width = 200, double height = 100) => new(new(new(new(0, 0, width, height))), PdfMargins.Uniform(0));
    private static PdfTableCell Cell(string text = "office", int columns = 1, int rows = 1) => PdfTableCell.Text(text, Style(), columns, rows);
    private static PdfTableRow Row(params PdfTableCell[] cells) => new(cells);
    [Fact]
    public void RepeatedHeadersAndFixedColumnsPreserveGridIdentityAcrossPages()
    {
        var rows = Enumerable.Range(0, 6).Select(i => Row(Cell(i == 0 ? "A" : "office"), Cell()));
        var table = PdfTable.Create(new[] { PdfTableColumn.Fixed(100), PdfTableColumn.Fixed(100) }, rows, headerRows: 1);
        var result = table.Layout(Options(height: 72), Providers());
        Assert.Equal(3, result.Pages.Count);
        foreach (var page in result.Pages)
        {
            var fragment = Assert.Single(page.Tables);
            Assert.Equal(200, fragment.Bounds.Width);
            Assert.Equal(2, fragment.Cells.Count(c => c.Row == 0));
            Assert.All(fragment.Cells.Where(c => c.Row == 0), c => Assert.Equal(page.Index > 0, c.IsRepeatedHeader));
            Assert.All(fragment.Cells, c => Assert.Equal(100, c.Bounds.Width));
        }

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result.Pages.SelectMany(p => p.Tables).SelectMany(t => t.Cells).Where(c => c.Row > 0 && c.Column == 0).Select(c => c.Row));
        using var document = result.CreateDocument();
        using var output = new MemoryStream();
        document.Save(output);
        output.Position = 0;
        using var read = Pdf.Open(output);
        Assert.Equal(3, read.Pages.Count);
    }

    [Fact]
    public void RowspansContinueWithoutDuplicatingAlreadyPaintedTextPlacements()
    {
        var table = PdfTable.Create(new[] { PdfTableColumn.Fixed(100), PdfTableColumn.Fixed(100) }, new[] { Row(Cell("A"), Cell("A")), Row(Cell(string.Join(" ", Enumerable.Repeat("office", 12)), rows: 2), Cell("A")), Row(Cell("A")) }, headerRows: 1);
        var result = table.Layout(Options(), Providers());
        Assert.True(result.Pages.Count > 1);
        Assert.Contains(result.Pages.Skip(1).SelectMany(p => p.Tables).SelectMany(t => t.Cells), c => c.Row == 1 && c.Column == 0 && c.IsContinuation && c.Cell.RowSpan == 2);
        var source = result.Pages[0].Tables[0].Plan.Cells.First(c => c.Grid.Row == 1 && c.Grid.Column == 0);
        Assert.Equal(6, source.Content.Flow!.Pages.SelectMany(p => p.Lines).Count());
        // Every body text placement is copied into exactly one parent fragment; repeated headers are separate intentional instances.
        var originals = source.Content.Segments.Select(s => s.Placement.Resource).ToArray();
        Assert.All(originals, resource => Assert.Equal(1, result.Pages.Sum(p => p.Canvas.Placements.Count(s => ReferenceEquals(s.Resource, resource)))));
    }

    [Fact]
    public void AutomaticColumnsUseContentMinimaWeightsAndMergedSpanConstraints()
    {
        var table = PdfTable.Create(new[] { PdfTableColumn.Auto(weight: 1), PdfTableColumn.Auto(weight: 2) }, new[] { Row(Cell(), Cell()), Row(Cell("office office office", columns: 2)) });
        var result = table.Layout(Options(240), Providers());
        var fragment = result.Pages[0].Tables[0];
        var firstRow = fragment.Cells.Where(c => c.Row == 0).ToArray();
        Assert.True(firstRow[1].Bounds.Width > firstRow[0].Bounds.Width);
        Assert.Equal(240, firstRow.Sum(c => c.Bounds.Width), 8);
        Assert.Equal(240, fragment.Cells.Single(c => c.Row == 1).Bounds.Width, 8);
        Assert.Equal("Table.WidthOverflow", Assert.Throws<PdfException>(() => table.Layout(Options(20), Providers())).Diagnostic.Code);
    }

    [Fact]
    public void FiniteLargeWeightsNormalizeBeforeDistribution()
    {
        var table = PdfTable.Create(new[] { PdfTableColumn.Auto(weight: 1e308), PdfTableColumn.Auto(weight: 1e308) }, new[] { Row(Cell(), Cell()) });
        var result = table.Layout(Options(200), Providers());
        Assert.All(result.Pages[0].Tables[0].Cells, cell => Assert.Equal(100, cell.Bounds.Width, 8));
    }

    [Fact]
    public void NestedTablesRetainTheirCellsAndSafeIntervalsWithinTheParent()
    {
        var inner = PdfTable.Create(new[] { PdfTableColumn.Auto() }, new[] { Row(Cell("A")), Row(Cell()), Row(Cell()) }, headerRows: 1);
        var outer = PdfTable.Create(new[] { PdfTableColumn.Auto() }, new[] { Row(new PdfTableCell(new PdfFlowBlock[] { inner })) });
        var result = outer.Layout(Options(160, 120), Providers());
        var prepared = result.Pages[0].Tables[0].Plan.Cells[0];
        Assert.NotNull(prepared.Content.Flow);
        Assert.NotEmpty(prepared.Content.Flow!.Pages[0].Tables);
        Assert.True(prepared.Content.Height > 0);
        using var document = result.CreateDocument();
        using var output = new MemoryStream();
        document.Save(output);
        Assert.True(output.Length > 0);
    }

    [Fact]
    public void NestedHeadersRepeatInReservedBandsWhileEachBodyLinePaintsOnce()
    {
        var inner = PdfTable.Create(new[] { PdfTableColumn.Auto() }, Enumerable.Range(0, 9).Select(i => Row(Cell(i == 0 ? "A" : "office"))), headerRows: 1);
        var outer = PdfTable.Create(new[] { PdfTableColumn.Auto() }, new[] { Row(new PdfTableCell(new PdfFlowBlock[] { inner })) });
        var result = outer.Layout(Options(160, 70), Providers());
        Assert.True(result.Pages.Count > 1);
        Assert.All(result.Pages.Skip(1), page => Assert.Contains(page.Tables.SelectMany(t => t.NestedHeaders), h => ReferenceEquals(h.Table, inner)));
        var content = result.Pages[0].Tables[0].Plan.Cells[0].Content;
        var nestedPlan = content.Flow!.Pages[0].Tables[0].Plan;
        var headerResources = nestedPlan.Cells[0].Content.Segments.Select(s => s.Placement.Resource).ToArray();
        Assert.All(headerResources, resource => Assert.Equal(result.Pages.Count, result.Pages.Sum(p => p.Canvas.Placements.Count(s => ReferenceEquals(s.Resource, resource)))));
        foreach (var body in nestedPlan.Cells.Skip(1))
            Assert.All(body.Content.Segments, segment => Assert.Equal(1, result.Pages.Sum(p => p.Canvas.Placements.Count(s => ReferenceEquals(s.Resource, segment.Placement.Resource)))));
    }

    [Fact]
    public void MultipleNestedHeaderLevelsReserveOrderedBandsOnEveryContinuation()
    {
        var leaf = PdfTable.Create(new[] { PdfTableColumn.Auto() }, Enumerable.Range(0, 9).Select(i => Row(Cell(i == 0 ? "A" : "office"))), headerRows: 1);
        var middle = PdfTable.Create(new[] { PdfTableColumn.Auto() }, new[] { Row(Cell("A")), Row(new PdfTableCell(new PdfFlowBlock[] { leaf })) }, headerRows: 1);
        var outer = PdfTable.Create(new[] { PdfTableColumn.Auto() }, new[] { Row(new PdfTableCell(new PdfFlowBlock[] { middle })) });
        var result = outer.Layout(Options(200, 120), Providers());
        Assert.True(result.Pages.Count > 1);
        foreach (var page in result.Pages.Skip(1))
        {
            var headers = page.Tables.SelectMany(t => t.NestedHeaders).ToArray();
            var first = Assert.Single(headers, h => ReferenceEquals(h.Table, middle));
            var second = Assert.Single(headers, h => ReferenceEquals(h.Table, leaf));
            Assert.True(first.Bounds.Top <= second.Bounds.Y);
        }
    }

    [Fact]
    public void GridHolesOverlapBoundsAndHeaderCrossingsAreRejectedBeforeLayout()
    {
        var columns = new[]
        {
            PdfTableColumn.Fixed(100),
            PdfTableColumn.Fixed(100)
        };
        Assert.Equal("Table.GridHole", Assert.Throws<PdfException>(() => PdfTable.Create(columns, new[] { Row(Cell()) })).Diagnostic.Code);
        Assert.Equal("Table.SpanBounds", Assert.Throws<PdfException>(() => PdfTable.Create(columns, new[] { Row(Cell(columns: 3)) })).Diagnostic.Code);
        Assert.Equal("Table.HeaderSpan", Assert.Throws<PdfException>(() => PdfTable.Create(columns, new[] { Row(Cell(rows: 2), Cell()), Row(Cell()) }, headerRows: 1)).Diagnostic.Code);
        Assert.Equal("Table.SpanOverlap", Assert.Throws<PdfException>(() => PdfTable.Create(new[] { PdfTableColumn.Fixed(50), PdfTableColumn.Fixed(50), PdfTableColumn.Fixed(50) }, new[] { Row(Cell(), Cell(rows: 2), Cell()), Row(Cell(columns: 2)) })).Diagnostic.Code);
    }

    [Fact]
    public void UnsplitRowsAndOverlargeHeadersFailWithoutReturningClippedResults()
    {
        var table = PdfTable.Create(new[] { PdfTableColumn.Fixed(100) }, new[] { new PdfTableRow(new[] { Cell(string.Join(" ", Enumerable.Repeat("office", 30))) }, allowSplit: false) });
        Assert.Equal("Table.RowFragmentOverflow", Assert.Throws<PdfException>(() => table.Layout(Options(100, 40), Providers())).Diagnostic.Code);
        var header = PdfTable.Create(new[] { PdfTableColumn.Fixed(100) }, new[] { new PdfTableRow(new[] { Cell() }, minimumHeight: 200) }, headerRows: 1);
        Assert.Equal("Table.HeaderOverflow", Assert.Throws<PdfException>(() => header.Layout(Options(100, 100), Providers())).Diagnostic.Code);
    }

    [Fact]
    public void TableBlocksFollowParagraphsAndExposeOccupiedHeightForComposition()
    {
        var table = PdfTable.Create(new[] { PdfTableColumn.Auto() }, new[] { Row(Cell()) });
        var result = PdfFlow.Layout(new PdfFlowBlock[] { new PdfFlowParagraph("office", Style()), table, new PdfFlowParagraph("office", Style()) }, Options(), Providers());
        var page = Assert.Single(result.Pages);
        var fragment = Assert.Single(page.Tables);
        Assert.True(fragment.Bounds.Y >= page.Lines[0].Bounds.Top);
        Assert.True(page.Lines[1].Bounds.Y >= fragment.Bounds.Top);
        Assert.True(page.ContentHeight >= page.Lines[1].Bounds.Top);
    }

    private sealed class Record
    {
        internal Record(decimal? value) => Value = value;
        internal decimal? Value { get; }
    }

    [Fact]
    public void BindingUsesExplicitCultureNullTextAndTypedSelectors()
    {
        var style = Style();
        var column = new PdfTableDataColumn<Record?>(PdfTableColumn.Auto(), r => r!.Value, style, "N2", "empty");
        var table = PdfTableData.Bind(new Record? [] { new(1234.5m), new(null), null }, new[] { column }, CultureInfo.GetCultureInfo("fr-FR"));
        string Text(int row) => ((PdfFlowParagraph)table.Rows[row].Cells[0].Content[0]).Runs[0].Text;
        Assert.Equal(1234.5m.ToString("N2", CultureInfo.GetCultureInfo("fr-FR")), Text(0));
        Assert.Equal("empty", Text(1));
        Assert.Equal("empty", Text(2));
    }

    [Fact]
    public void InfiniteSourcesAreBoundedDisposedAndCyclesAreRejected()
    {
        var source = new InfiniteRows();
        var column = new PdfTableDataColumn<int>(PdfTableColumn.Auto(), x => x, Style());
        Assert.Equal("Table.RowLimit", Assert.Throws<PdfException>(() => PdfTableData.Bind(source, new[] { column }, CultureInfo.InvariantCulture, maximumRows: 2)).Diagnostic.Code);
        Assert.Equal(3, source.Moved);
        Assert.True(source.Disposed);
        var cycle = new List<object>();
        cycle.Add(cycle);
        Assert.Equal("Table.DataCycle", Assert.Throws<PdfException>(() => PdfTableData.Bind(cycle, new[] { new PdfTableDataColumn<object>(PdfTableColumn.Auto(), x => x, Style()) }, CultureInfo.InvariantCulture)).Diagnostic.Code);
    }

    [Fact]
    public void FormattingPrecisionCultureAndObjectProfilesAreBoundedBeforeExpansion()
    {
        var style = Style();
        Assert.Equal("Table.FormatPrecision", Assert.Throws<PdfException>(() => PdfTableData.Bind(new[] { 1d }, new[] { new PdfTableDataColumn<double>(PdfTableColumn.Auto(), x => x, style, "F999999999") }, CultureInfo.InvariantCulture)).Diagnostic.Code);
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.CurrencySymbol = new string ('x', 1000);
        Assert.Equal("Table.CultureLimit", Assert.Throws<PdfException>(() => PdfTableData.Bind(new[] { 1 }, new[] { new PdfTableDataColumn<int>(PdfTableColumn.Auto(), x => x, style) }, culture)).Diagnostic.Code);
        Assert.Equal("Table.ValueType", Assert.Throws<PdfException>(() => PdfTableData.Bind(new[] { 1 }, new[] { new PdfTableDataColumn<int>(PdfTableColumn.Auto(), _ => new object (), style) }, CultureInfo.InvariantCulture)).Diagnostic.Code);
    }

    [Fact]
    public void CancellationAndSharedBudgetsCoverBindingGridAndNestedLayout()
    {
        var columns = new[]
        {
            PdfTableColumn.Fixed(100)
        };
        var rows = new[]
        {
            Row(Cell())
        };
        Assert.Throws<OperationCanceledException>(() => PdfTable.Create(columns, rows, cancellationToken: new CancellationToken(true)));
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfTable.Create(columns, rows, policy: new(limits: new(maximumObjects: 1)))).Diagnostic.Code);
        var table = PdfTable.Create(columns, rows);
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => table.Layout(Options(), Providers(), new(limits: new(maximumExpandedBytes: 100)))).Diagnostic.Code);
        Assert.Throws<OperationCanceledException>(() => table.Layout(Options(), Providers(), cancellationToken: new CancellationToken(true)));
    }

    [Fact]
    public void AThousandLazyRowsBindAtTheExactLimitWithoutReadingASecondPass()
    {
        int enumerations = 0, count = 0;
        bool disposed = false;
        IEnumerable<string> Values()
        {
            enumerations++;
            try
            {
                for (int i = 0; i < 1000; i++)
                {
                    count++;
                    yield return "office";
                }
            }
            finally
            {
                disposed = true;
            }
        }

        var table = PdfTableData.Bind(Values(), new[] { new PdfTableDataColumn<string>(PdfTableColumn.Auto(), x => x, Style()) }, CultureInfo.InvariantCulture, maximumRows: 1000);
        Assert.Equal(1000, table.Rows.Count);
        Assert.Equal(1000, count);
        Assert.Equal(1, enumerations);
        Assert.True(disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothLanguageExamplesWriteRepeatedHeaderTablesAndRetainBorrowedStreams(bool visualBasic)
    {
        using var font = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "text", "latin.ttf"));
        using var output = new MemoryStream();
        if (visualBasic)
            ContractExamples.VisualBasic.ContractExample.WriteTablePdf(font, output, new PdfSimpleTextShaper());
        else
            ContractExamples.CSharp.ContractExample.WriteTablePdf(font, output, new PdfSimpleTextShaper());
        Assert.True(font.CanRead);
        Assert.True(output.CanWrite);
        output.Position = 0;
        using var document = Pdf.Open(output);
        Assert.True(document.Pages.Count >= 2);
    }

    private sealed class InfiniteRows : IEnumerable<int>, IEnumerator<int>
    {
        internal int Moved;
        internal bool Disposed;
        public int Current => Moved;

        object IEnumerator.Current => Current;

        public IEnumerator<int> GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool MoveNext()
        {
            Moved++;
            return true;
        }

        public void Reset() => throw new NotSupportedException();
        public void Dispose() => Disposed = true;
    }
}
