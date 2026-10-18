using System.Globalization;
using System.IO;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class TemplateTests
{
    private static PdfFlowTextStyle Style(double size = 10) => new(new(TextTests.Read("business.ttf")), size);
    private static PdfProviderRegistry Providers() => new(new PdfSimpleTextShaper());
    private static PdfFlowOptions Options() => new(new(new(new(0, 0, 220, 160))), PdfMargins.Uniform(10));
    private static PdfFlowBlock[] Body(int sections = 6) => new PdfFlowBlock[]
    {
        new PdfFlowContents(Style())
    }.Concat(Enumerable.Range(1, sections).SelectMany(i => new PdfFlowBlock[] { new PdfFlowHeading("s" + i, "Section " + i, Style()), new PdfFlowParagraph(string.Join(" ", Enumerable.Repeat("office", 50)), Style()) })).ToArray();
    [Fact]
    public void ContentsConvergeAndCallbacksReceiveFinalTotalsAndVariants()
    {
        var calls = new List<(int Page, int Total, string Label, string Variant)>();
        var numbers = new PdfPageNumbering(new(0, PdfPageLabelStyle.LowerRoman), new(2, PdfPageLabelStyle.Decimal, prefix: "B-"));
        var template = new PdfPageTemplate(16, 16, header: c =>
        {
            string variant = c.IsFirstPage ? "first" : c.IsOddPage ? "odd" : "even";
            calls.Add((c.PageIndex, c.TotalPages, c.PageLabel, variant));
            return PdfPageDecoration.Flow(new[] { new PdfFlowParagraph(variant, Style(8)) });
        }, footer: c => PdfPageDecoration.Flow(new[] { new PdfFlowParagraph(c.PageLabel + " of " + c.TotalPages.ToString(CultureInfo.InvariantCulture), Style(8)) }), numbering: numbers);
        var result = PdfTemplateLayout.Layout(Body(), Options(), Providers(), template);
        Assert.InRange(result.Passes, 2, 8);
        Assert.Equal(result.Body.Pages.Count, calls.Count);
        Assert.All(calls, c => Assert.Equal(c.Total, calls.Count));
        Assert.Equal("first", calls[0].Variant);
        Assert.Equal("even", calls[1].Variant);
        Assert.Equal("B-1", calls[2].Label);
        Assert.Equal(6, result.Entries.Count);
        foreach (var entry in result.Entries)
            Assert.Contains(result.Body.Pages[entry.PageIndex].Headings, h => h.Heading.Id == entry.DestinationId);
        using var document = result.CreateDocument();
        using var bytes = new MemoryStream();
        document.Save(bytes);
        bytes.Position = 0;
        using var reopened = Pdf.Open(bytes);
        var source = reopened.SourceDocument!;
        var catalog = source.Resolve(source.Revisions[0].Trailer.Entries["Root"].ReferenceId);
        var labels = source.Resolve(catalog.Entries["PageLabels"].ReferenceId).Entries["Nums"];
        Assert.Equal(4, labels.Items.Count);
        Assert.Equal("r", labels.Items[1].Entries["S"].NameValue);
        Assert.Equal(2, labels.Items[2].IntegerValue);
        var links = reopened.Pages.SelectMany(p => p.Source!.Dictionary.Entries.TryGetValue("Annots", out var v) ? v.Items : Array.Empty<PdfValue>()).ToArray();
        Assert.Equal(6, links.Length);
        foreach (var reference in links)
        {
            var link = source.Resolve(reference.ReferenceId);
            var dest = link.Entries["Dest"];
            Assert.Contains(reopened.Pages, p => p.Source!.ObjectId == dest.Items[0].ReferenceId);
            Assert.Equal("XYZ", dest.Items[1].NameValue);
        }

        Assert.All(reopened.Pages, p => Assert.Contains(p.ReadContent().Operations, o => o.Operation == PdfOperator.BeginMarkedContent && o.Operands[0].NameValue == "Artifact"));
    }

    [Fact]
    public void ConvergenceReusesImmutableParagraphMeasurementsWithinOneOperationOnly()
    {
        var shaper = new CountedShaper();
        var providers = new PdfProviderRegistry(shaper);
        var blocks = Body(2);
        var first = PdfTemplateLayout.Layout(blocks, Options(), providers, new());
        Assert.True(first.Passes >= 2);
        Assert.Equal(2, shaper.FullParagraphCalls);
        var second = PdfTemplateLayout.Layout(blocks, Options(), providers, new());
        Assert.Equal(first.Body.Pages.Count, second.Body.Pages.Count);
        Assert.Equal(4, shaper.FullParagraphCalls);
        Assert.Equal(first.Entries.Select(e => e.PageIndex), second.Entries.Select(e => e.PageIndex));
    }

    private sealed class CountedShaper : IPdfTextShaper
    {
        private readonly PdfSimpleTextShaper _inner = new();
        public PdfProviderDescriptor Descriptor => _inner.Descriptor;
        public int FullParagraphCalls { get; private set; }

        public IReadOnlyList<PdfShapedGlyph> Shape(PdfShapingRequest request, PdfOperationContext context)
        {
            if (request.Text.Length == 349 && request.Start == 0 && request.Length == 349)
                FullParagraphCalls++;
            return _inner.Shape(request, context);
        }
    }

    [Theory]
    [InlineData(PdfPageLabelStyle.UpperLetters, 26, "AA")]
    [InlineData(PdfPageLabelStyle.LowerLetters, 51, "zz")]
    [InlineData(PdfPageLabelStyle.UpperRoman, 48, "XLIX")]
    [InlineData(PdfPageLabelStyle.None, 5, "")]
    public void LabelsUsePdfStandardCounters(PdfPageLabelStyle style, int index, string expected) => Assert.Equal(expected, new PdfPageNumbering(new PdfPageNumberRange(0, style)).GetLabel(index));
    [Fact]
    public void MissingDuplicateDestinationsAndPassLimitsFailExplicitly()
    {
        var duplicate = new PdfFlowHeading("same", "Title", Style());
        Assert.Equal("Template.DuplicateHeading", Assert.Throws<PdfException>(() => PdfTemplateLayout.Layout(new[] { duplicate, duplicate }, Options(), Providers(), new())).Diagnostic.Code);
        var index = new PdfFlowIndex(new[] { new PdfFlowIndexTerm("Term", "missing") }, Style(), CultureInfo.InvariantCulture);
        Assert.Equal("Template.MissingDestination", Assert.Throws<PdfException>(() => PdfTemplateLayout.Layout(new[] { index }, Options(), Providers(), new())).Diagnostic.Code);
        Assert.Throws<OperationCanceledException>(() => PdfTemplateLayout.Layout(Body(), Options(), Providers(), new(), cancellationToken: new CancellationToken(true)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfPageTemplate(maximumPasses: 1));
        Assert.Equal("Template.PaginationConvergence", Assert.Throws<PdfException>(() => PdfTemplateLayout.Layout(Body(12), Options(), Providers(), new(maximumPasses: 2, numbering: new PdfPageNumbering(new(0), new(1, prefix: "Section label "))))).Diagnostic.Code);
    }

    [Fact]
    public void ExplicitHtmlProviderIsRequiredAndDecorationOverflowFails()
    {
        Assert.Throws<PdfProviderUnavailableException>(() => PdfTemplateLayout.Layout(new[] { new PdfFlowParagraph("body", Style()) }, Options(), Providers(), new(20, header: _ => PdfPageDecoration.Html("<p>header</p>"))));
        Assert.Equal("Template.DecorationOverflow", Assert.Throws<PdfException>(() => PdfTemplateLayout.Layout(new[] { new PdfFlowParagraph("body", Style()) }, Options(), Providers(), new(20, header: _ => PdfPageDecoration.Flow(new[] { new PdfFlowParagraph(string.Join(" ", Enumerable.Repeat("office", 100)), Style(), new(orphanLines: 1, widowLines: 1)) })))).Diagnostic.Code);
    }

    [Fact]
    public void HeadingsInsideColumnsAndKeptComponentsResolvePhysicalPages()
    {
        var blocks = new PdfFlowBlock[]
        {
            new PdfFlowContents(Style()),
            new PdfFlowColumns(new[] { new PdfFlowHeading("col", "Column", Style()) }, height: 30),
            new PdfFlowComponent(new[] { new PdfFlowHeading("kept", "Kept", Style()) }, true)
        };
        var result = PdfTemplateLayout.Layout(blocks, Options(), Providers(), new());
        Assert.Equal(new[] { "col", "kept" }, result.Entries.Select(e => e.DestinationId));
        using var doc = result.CreateDocument();
        Assert.Equal(2, doc.CreateSnapshot().Pages.Sum(p => p.Links.Count));
    }

    [Fact]
    public void ExplicitIndexSortsWithCultureAndLinksEachLocator()
    {
        var index = new PdfFlowIndex(new[] { new PdfFlowIndexTerm("Zulu", "b"), new PdfFlowIndexTerm("Alpha", "a", "b") }, Style(), CultureInfo.InvariantCulture);
        var result = PdfTemplateLayout.Layout(new PdfFlowBlock[] { index, new PdfFlowHeading("a", "First", Style()), new PdfFlowHeading("b", "Second", Style()) }, Options(), Providers(), new());
        Assert.Equal("Alpha", index.Terms[0].Text);
        using var doc = result.CreateDocument();
        Assert.Equal(3, doc.CreateSnapshot().Pages.Sum(p => p.Links.Count));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothLanguageExamplesWriteFinalTotalsAndLinks(bool visualBasic)
    {
        using var font = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "text", "business.ttf"));
        using var bytes = new MemoryStream();
        if (visualBasic)
            ContractExamples.VisualBasic.ContractExample.WriteTemplatePdf(font, bytes, new PdfSimpleTextShaper());
        else
            ContractExamples.CSharp.ContractExample.WriteTemplatePdf(font, bytes, new PdfSimpleTextShaper());
        Assert.True(font.CanRead);
        Assert.True(bytes.CanWrite);
        bytes.Position = 0;
        using var document = Pdf.Open(bytes);
        Assert.Contains(document.Pages, p => p.Source!.Dictionary.Entries.ContainsKey("Annots"));
    }

    [Fact]
    public void NestedCellContentsLinksAndSharedBudgetsArePreserved()
    {
        var table = PdfTable.Create(new[] { PdfTableColumn.Fixed(200) }, new[] { new PdfTableRow(new[] { new PdfTableCell(new PdfFlowBlock[] { new PdfFlowContents(Style()), new PdfFlowHeading("nested", "Nested", Style()) }) }) });
        var result = PdfTemplateLayout.Layout(new PdfFlowBlock[] { table }, Options(), Providers(), new());
        Assert.Single(result.Entries);
        using var doc = result.CreateDocument();
        Assert.Single(doc.CreateSnapshot().Pages.SelectMany(p => p.Links));
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfTemplateLayout.Layout(Body(), Options(), Providers(), new(), new(limits: new(maximumObjects: 1)))).Diagnostic.Code);
        Assert.Throws<OperationCanceledException>(() => result.CreateDocument(cancellationToken: new CancellationToken(true)));
    }

    [Fact]
    public void ImageAndExplicitHtmlDecorationsUseReservedFramesAndSharedContext()
    {
        var image = PdfCanvas.Create(CanvasTests.SimpleGeometry(), c => c.DrawImage(CanvasTests.Image(), new(0, 0, 20, 10))).Drawing;
        var html = new FragmentProvider();
        var result = PdfTemplateLayout.Layout(new[] { new PdfFlowParagraph("body", Style()) }, Options(), Providers(), new(20, 20, header: _ => PdfPageDecoration.Vector(image, new(0, 0, 200, 100)), footer: _ => PdfPageDecoration.Html("<p>footer</p>", html)));
        Assert.Equal(result.Body.Pages.Count, html.Calls);
        Assert.All(result.Decorations, d => Assert.Equal(2, d.Placements.Count));
        using var doc = result.CreateDocument();
        using var output = new MemoryStream();
        doc.Save(output);
        Assert.Contains(doc.CreateSnapshot().Pages, p => p.GraphicsResources.Bindings.Count > 1);
    }

    private sealed class FragmentProvider : IPdfTemplateHtmlProvider
    {
        public PdfProviderDescriptor Descriptor { get; } = new("test.fragments", "1", PdfCapability.RenderHtml);
        public int Calls { get; private set; }

        private readonly PdfDrawing _drawing = PdfDrawing.Create(Array.Empty<PdfContentOperation>());
        public PdfDrawing Render(string html, PdfPageSize viewport, PdfOperationContext context)
        {
            Assert.Equal("<p>footer</p>", html);
            Assert.True(viewport.Width > 0);
            Assert.Equal(20, viewport.Height);
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            Calls++;
            return _drawing;
        }
    }

    [Fact]
    public void DeferredDestinationsFollowReorderedPagesAndRejectRemovedTargets()
    {
        var result = PdfTemplateLayout.Layout(new PdfFlowBlock[] { new PdfFlowContents(Style()), new PdfFlowPageBreak(), new PdfFlowHeading("target", "Target", Style()) }, Options(), Providers(), new());
        using var doc = result.CreateDocument();
        Assert.Equal(1, doc.CreateSnapshot().Pages[0].Links[0].TargetPageIndex);
        doc.Pages.Move(1, 0);
        Assert.Equal(0, doc.CreateSnapshot().Pages[1].Links[0].TargetPageIndex);
        doc.Pages.RemoveAt(0);
        Assert.Throws<InvalidOperationException>(() => doc.Save(new MemoryStream()));
    }
}
