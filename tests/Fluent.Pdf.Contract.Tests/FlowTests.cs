using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class FlowTests
{
    private static PdfFlowTextStyle Style(double size = 12) => new(new(TextTests.Read("latin.ttf")), size);
    private static PdfProviderRegistry Providers() => new(new PdfSimpleTextShaper());
    private static PdfFlowOptions Options(double width = 100, double height = 100, PdfFlowOverflow overflow = PdfFlowOverflow.Error, int pages = 100) => new(new(new(new(0, 0, width, height))), PdfMargins.Uniform(0), overflow, pages);
    private static PdfFlowResult Layout(string text, PdfFlowOptions? options = null, PdfFlowParagraphStyle? paragraph = null) => PdfFlow.Layout(new[] { new PdfFlowParagraph(text, Style(), paragraph) }, options ?? Options(), Providers());
    [Fact]
    public void WrappingPaginationAndRepeatedGeometryPreserveEveryLogicalCodeUnit()
    {
        string text = string.Join(" ", Enumerable.Repeat("office", 20));
        var first = Layout(text, Options(75, 60));
        var second = Layout(text, Options(75, 60));
        Assert.True(first.Pages.Count > 1);
        Assert.Equal(text, string.Concat(first.Pages.SelectMany(p => p.Lines).Select(l => l.Text)));
        Assert.Equal(first.Pages.SelectMany(p => p.Lines).Select(l => (l.Utf16Offset, l.Utf16Length, l.Bounds.X, l.Bounds.Y, l.Bounds.Width, l.Bounds.Height)), second.Pages.SelectMany(p => p.Lines).Select(l => (l.Utf16Offset, l.Utf16Length, l.Bounds.X, l.Bounds.Y, l.Bounds.Width, l.Bounds.Height)));
        foreach (var page in first.Pages)
        {
            double bottom = 0;
            foreach (var line in page.Lines)
            {
                Assert.True(line.Bounds.Y >= bottom);
                Assert.True(line.Bounds.Top <= 60);
                bottom = line.Bounds.Top;
            }
        }

        using var document = first.CreateDocument();
        using var output = new MemoryStream();
        document.Save(output);
        output.Position = 0;
        using var read = Pdf.Open(output);
        Assert.Equal(first.Pages.Count, read.Pages.Count);
        Assert.True(output.CanRead);
    }

    [Fact]
    public void WidowAndOrphanRulesMoveLinesBeforeReturningAnyPage()
    {
        var result = Layout("office office office office", Options(50, 44));
        Assert.Equal(new[] { 2, 2 }, result.Pages.Select(p => p.Lines.Count));
        Assert.Equal("Layout.WidowConstraint", Assert.Throws<PdfException>(() => Layout("office office office", Options(50, 29))).Diagnostic.Code);
    }

    [Fact]
    public void KeepTogetherMovesAParagraphAndRejectsImpossibleEmptyFrames()
    {
        var style = Style();
        var result = PdfFlow.Layout(new PdfFlowBlock[] { new PdfFlowParagraph("office office", style), new PdfFlowParagraph("office office office", style, new(keepTogether: true)) }, Options(50, 60), Providers());
        Assert.Equal(new[] { 2, 3 }, result.Pages.Select(p => p.Lines.Count));
        Assert.Equal("Layout.KeepTogetherImpossible", Assert.Throws<PdfException>(() => Layout("office office office", Options(50, 29), new(keepTogether: true))).Diagnostic.Code);
    }

    [Fact]
    public void KeepWithNextRetainsTheFollowingParagraphOpening()
    {
        var style = Style();
        var result = PdfFlow.Layout(new PdfFlowBlock[] { new PdfFlowParagraph("office office office", style), new PdfFlowParagraph("office", style, new(keepWithNext: true)), new PdfFlowParagraph("office office", style) }, Options(50, 60), Providers());
        Assert.Equal(new[] { 3, 3 }, result.Pages.Select(p => p.Lines.Count));
        Assert.Equal("Layout.KeepWithNextTarget", Assert.Throws<PdfException>(() => Layout("office", paragraph: new(keepWithNext: true))).Diagnostic.Code);
    }

    [Fact]
    public void KeepChainsMoveTogetherAndLongParagraphsKeepOnlyTheirRequiredTail()
    {
        var style = Style();
        var chain = PdfFlow.Layout(new PdfFlowBlock[] { new PdfFlowParagraph("office office", style), new PdfFlowParagraph("office", style, new(keepWithNext: true)), new PdfFlowParagraph("office", style, new(keepWithNext: true)), new PdfFlowParagraph("office office", style) }, Options(50, 60), Providers());
        Assert.Equal(new[] { 2, 4 }, chain.Pages.Select(p => p.Lines.Count));
        var longParagraph = PdfFlow.Layout(new PdfFlowBlock[] { new PdfFlowParagraph("office office office office office office", style, new(keepWithNext: true)), new PdfFlowParagraph("office office", style) }, Options(50, 60), Providers());
        Assert.Equal(new[] { 4, 4 }, longParagraph.Pages.Select(p => p.Lines.Count));
        Assert.Equal(2, longParagraph.Pages[1].Lines.Count(l => l.BlockIndex == 0));
        Assert.Equal(2, longParagraph.Pages[1].Lines.Count(l => l.BlockIndex == 1));
    }

    [Fact]
    public void WrappedBidiLineLevelsRetainParagraphContextAcrossExplicitEmbeddings()
    {
        PolicyTests.Run(context =>
        {
            var paragraph = PdfUnicode.AnalyzeCore("A \u202Eoffice office\u202C", PdfParagraphDirection.Auto, context);
            var line = PdfFlowUnicode.Line(paragraph, 9, 6, context);
            Assert.Equal(0, line.Paragraphs[0].EmbeddingLevel);
            Assert.Contains(line.Scalars, s => s.EmbeddingLevel == 1);
            Assert.Equal(new[] { 5, 4, 3, 2, 1, 0 }, line.VisualScalarOrder);
        });
    }

    [Fact]
    public void RichRunsKeepStylesAndLogicalBoundariesWhileSharingOneLine()
    {
        var small = Style();
        var large = new PdfFlowTextStyle(small.Fonts, 24, color: new(PdfColorSpace.Rgb, 1, 0, 0));
        var paragraph = new PdfFlowParagraph(new[] { new PdfFlowRun("of", small), new PdfFlowRun("fice", large) });
        var result = PdfFlow.Layout(new[] { paragraph }, Options(120, 100), Providers());
        var line = Assert.Single(result.Pages[0].Lines);
        Assert.Equal(2, line.ShapedText.Runs.Count);
        Assert.Equal(28.8, line.Bounds.Height, 8);
        Assert.Equal("office", line.Text);
        Assert.NotEmpty(result.Pages[0].Canvas.Placements);
        Assert.Equal("Layout.StyleGraphemeBoundary", Assert.Throws<PdfException>(() => PdfFlow.Layout(new[] { new PdfFlowParagraph(new[] { new PdfFlowRun("A", small), new PdfFlowRun("\u0301", large) }) }, Options(), Providers())).Diagnostic.Code);
    }

    [Fact]
    public void HardBreaksTabsEmptyParagraphsAndExplicitPagesRemainDeterministic()
    {
        var style = Style();
        var result = PdfFlow.Layout(new PdfFlowBlock[] { new PdfFlowParagraph("of\tfice\r\noffice", style), new PdfFlowParagraph("", style), new PdfFlowPageBreak(), new PdfFlowParagraph("office", style) }, Options(), Providers());
        Assert.Equal(2, result.Pages.Count);
        Assert.Equal(3, result.Pages[0].Lines.Count);
        Assert.Equal("of\tfice\r\noffice", string.Concat(result.Pages[0].Lines.Where(l => l.BlockIndex == 0).Select(l => l.Text)));
        Assert.True(result.Pages[0].Lines[0].Advance > 36);
        Assert.True(result.Pages[0].Lines[0].EndsHardBreak);
        Assert.Empty(result.Pages[0].Lines[2].Text);
    }

    [Fact]
    public void FloatsExcludeTheirRectanglesAndClearAdvancesBelowThem()
    {
        var style = Style();
        var art = GraphicsTests.Rectangle();
        var result = PdfFlow.Layout(new PdfFlowBlock[] { new PdfFlowFloat(art, new(0, 0, 100, 80), 45, 35, gap: 5), new PdfFlowParagraph("office office office office", style, new(orphanLines: 1, widowLines: 1)), new PdfFlowParagraph("office", style, new(clear: PdfFlowClear.Both)) }, Options(100, 100), Providers());
        var page = Assert.Single(result.Pages);
        Assert.Single(page.Floats);
        foreach (var line in page.Lines)
            if (line.Bounds.Y < 35)
                Assert.True(line.Bounds.X >= 50);
        Assert.True(page.Lines.Last().Bounds.Y >= 35);
    }

    [Fact]
    public void WordOverflowIsExplicitAndClippingRequiresPermissiveLossReporting()
    {
        Assert.Equal("Layout.WordOverflow", Assert.Throws<PdfException>(() => Layout("office", Options(15, 100))).Diagnostic.Code);
        var split = Layout("office", Options(15, 100, PdfFlowOverflow.BreakGrapheme), new(orphanLines: 1, widowLines: 1));
        Assert.Equal("office", string.Concat(split.Pages.SelectMany(p => p.Lines).Select(l => l.Text)));
        Assert.All(split.Pages.SelectMany(p => p.Lines), l => Assert.True(l.Advance <= 15));
        Assert.Equal("Layout.ClippedText", Assert.Throws<PdfException>(() => Layout("office", Options(15, 100, PdfFlowOverflow.Clip))).Diagnostic.Code);
        var accepted = PdfFlow.Layout(new[] { new PdfFlowParagraph("office", Style()) }, Options(15, 100, PdfFlowOverflow.Clip), Providers(), new(PdfProcessingMode.Permissive));
        Assert.True(Assert.Single(accepted.Pages[0].Lines).IsClipped);
        Assert.Contains(accepted.OperationReport.Diagnostics, d => d.Code == "Layout.ClippedText" && d.LossCategory == PdfLossCategory.OmittedContent);
    }

    [Fact]
    public void ResourcePageCancellationAndUnsupportedShapingFailuresReturnNoPartialResult()
    {
        var blocks = new[]
        {
            new PdfFlowParagraph("office office office office", Style())
        };
        Assert.Throws<OperationCanceledException>(() => PdfFlow.Layout(blocks, Options(), Providers(), cancellationToken: new CancellationToken(true)));
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfFlow.Layout(blocks, Options(), Providers(), new(limits: new(maximumObjects: 1)))).Diagnostic.Code);
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PdfFlow.Layout(blocks, Options(), Providers(), new(limits: new(maximumExpandedBytes: 1048575)))).Diagnostic.Code);
        Assert.Equal("Limit.Pages", Assert.Throws<PdfException>(() => PdfFlow.Layout(blocks, Options(50, 29, pages: 1), Providers())).Diagnostic.Code);
        Assert.Equal("Text.ComplexShaperRequired", Assert.Throws<PdfException>(() => PdfFlow.Layout(new[] { new PdfFlowParagraph("مرحبا", new(new(TextTests.Read("arabic.ttf")))) }, Options(), Providers())).Diagnostic.Code);
        Assert.Equal("Layout.LineTooTall", Assert.Throws<PdfException>(() => Layout("office", Options(100, 1))).Diagnostic.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothLanguageExamplesWriteRealFlowPagesAndRetainBorrowedStreams(bool visualBasic)
    {
        using var font = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "text", "latin.ttf"));
        using var output = new MemoryStream();
        if (visualBasic)
            ContractExamples.VisualBasic.ContractExample.WriteFlowPdf(font, output, "office office", new PdfSimpleTextShaper());
        else
            ContractExamples.CSharp.ContractExample.WriteFlowPdf(font, output, "office office", new PdfSimpleTextShaper());
        Assert.True(font.CanRead);
        Assert.True(output.CanWrite);
        output.Position = 0;
        using var document = Pdf.Open(output);
        Assert.Single(document.Pages);
        Assert.NotEmpty(document.Pages[0].ReadContent().Operations);
    }

    [Fact]
    public void SharedImmutableBlocksAndFontsSupportIndependentLayoutJobs()
    {
        var blocks = new[]
        {
            new PdfFlowParagraph("office office", Style())
        };
        var options = Options();
        Parallel.For(0, 8, _ =>
        {
            var result = PdfFlow.Layout(blocks, options, Providers());
            Assert.Equal("office office", Assert.Single(result.Pages[0].Lines).Text);
        });
    }
}
