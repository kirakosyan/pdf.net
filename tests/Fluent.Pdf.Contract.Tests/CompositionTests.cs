using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class CompositionTests
{
    private static PdfFlowTextStyle Style(double size = 12) => new(new(TextTests.Read("business.ttf")), size);
    private static PdfProviderRegistry Providers() => new(new PdfSimpleTextShaper());
    private static PdfFlowOptions Options(double width = 200, double height = 100) => new(new(new(new(0, 0, width, height))), PdfMargins.Uniform(0));
    private static PdfFlowListItem Item(string text, int? restart = null) => new(new[] { new PdfFlowParagraph(text, Style()) }, restart);
    [Fact]
    public void ListCountersRestartsAndNestedListsRemainStableAcrossPages()
    {
        var nested = PdfFlowList.Create(new[] { Item("nested"), Item("nested", 4), Item("nested") }, Style(), PdfFlowListKind.LowerAlpha);
        var list = PdfFlowList.Create(new[] { Item("first"), new PdfFlowListItem(new PdfFlowBlock[] { new PdfFlowParagraph("second", Style()), nested }), Item("third", 8), Item("last") }, Style());
        Assert.Equal(new[] { "1.", "2.", "8.", "9." }, list.Markers);
        Assert.Equal(new[] { "a.", "d.", "e." }, nested.Markers);
        PolicyTests.Run(context => Assert.Equal(4, list.ToTable(Options(), new PdfSimpleTextShaper(), context).Rows.Count));
        var layout = PdfFlow.Layout(new[] { list }, Options(200, 70), Providers());
        Assert.True(layout.Pages.Count > 1);
        Assert.All(layout.Pages.SelectMany(p => p.Tables), t => Assert.Same(list, t.Table.ListSource));
    }

    [Theory]
    [InlineData(PdfFlowListKind.UpperRoman, 14, "XIV.")]
    [InlineData(PdfFlowListKind.LowerRoman, 49, "xlix.")]
    [InlineData(PdfFlowListKind.UpperAlpha, 27, "AA.")]
    [InlineData(PdfFlowListKind.LowerAlpha, 703, "aaa.")]
    public void InvariantMarkerProfilesHaveKnownAnswers(PdfFlowListKind kind, int start, string expected) => Assert.Equal(expected, PdfFlowList.Create(new[] { Item("text") }, Style(), kind, start).Markers[0]);
    [Fact]
    public void MarkerRangesItemLimitsAndCancellationAreExplicit()
    {
        Assert.Equal("Composition.RomanRange", Assert.Throws<PdfException>(() => PdfFlowList.Create(new[] { Item("text") }, Style(), PdfFlowListKind.UpperRoman, 4000)).Diagnostic.Code);
        Assert.Equal("Composition.CounterOverflow", Assert.Throws<PdfException>(() => PdfFlowList.Create(new[] { Item("text"), Item("text") }, Style(), start: int.MaxValue)).Diagnostic.Code);
        Assert.Equal("Composition.ListLimit", Assert.Throws<PdfException>(() => PdfFlowList.Create(new[] { Item("text"), Item("text") }, Style(), maximumItems: 1)).Diagnostic.Code);
        Assert.Throws<OperationCanceledException>(() => PdfFlowList.Create(new[] { Item("text") }, Style(), cancellationToken: new CancellationToken(true)));
        var bullets = PdfFlowList.Create(new[] { Item("text") }, Style(), PdfFlowListKind.Bullet);
        Assert.Equal("\u2022", bullets.Markers[0]);
        Assert.Single(PdfFlow.Layout(new[] { bullets }, Options(), Providers()).Pages);
    }

    [Fact]
    public void ColumnsKeepLogicalOrderAndWriteRealParentTreeAssociations()
    {
        string text = string.Join(" ", Enumerable.Repeat("office", 40));
        var columns = new PdfFlowColumns(new[] { new PdfFlowParagraph(text, Style()) }, gap: 10, height: 72, order: PdfFlowColumnOrder.RightToLeft);
        var result = PdfFlow.Layout(new[] { columns }, Options(200, 100), Providers());
        Assert.True(result.Pages.Count > 1);
        Assert.Equal(text, string.Concat(result.Pages.SelectMany(p => p.Columns).SelectMany(c => c.Source.Lines).Select(l => l.Text)));
        foreach (var page in result.Pages)
            Assert.Equal(1, page.Columns[0].PhysicalColumnIndex);
        using var document = result.CreateDocument();
        var snapshot = document.CreateSnapshot();
        Assert.All(snapshot.Pages, p => Assert.Equal(p.GraphicsResources.Bindings.Count, p.StructureRoles.Count));
        using var output = new MemoryStream();
        document.Save(output);
        output.Position = 0;
        using var read = Pdf.Open(output);
        var source = read.SourceDocument!;
        var catalog = source.Resolve(source.Revisions[0].Trailer.Entries["Root"].ReferenceId);
        var root = source.Resolve(catalog.Entries["StructTreeRoot"].ReferenceId);
        var parents = source.Resolve(root.Entries["ParentTree"].ReferenceId).Entries["Nums"];
        Assert.Equal(read.Pages.Count * 2, parents.Items.Count);
        for (int i = 0; i < read.Pages.Count; i++)
        {
            Assert.Equal(i, read.Pages[i].Source!.Dictionary.Entries["StructParents"].IntegerValue);
            Assert.Equal(result.Pages[i].Columns.Count, parents.Items[i * 2 + 1].Items.Count);
            Assert.Equal(result.Pages[i].Columns.Count, read.Pages[i].ReadContent().Operations.Count(o => o.Operation == PdfOperator.BeginMarkedContentProperties));
        }

        using var roundtrip = new MemoryStream();
        read.Save(roundtrip);
    }

    [Fact]
    public void SourceStructureEditsRequireAnExplicitPreservationProfile()
    {
        var result = PdfFlow.Layout(new[] { new PdfFlowColumns(new[] { new PdfFlowParagraph("office office", Style()) }) }, Options(), Providers());
        using var document = result.CreateDocument();
        using var bytes = new MemoryStream();
        document.Save(bytes);
        bytes.Position = 0;
        using var read = Pdf.Open(bytes);
        read.Pages[0].Content = PdfContentProgram.Create(Array.Empty<PdfContentOperation>());
        Assert.Equal("Structure.SourceEditPolicy", Assert.Throws<PdfException>(() => read.Save(new MemoryStream())).Diagnostic.Code);
    }

    [Fact]
    public void FootnotesReserveTheirReferencePageAndRepeatedReferencesShareOneNote()
    {
        var note = new PdfFlowNote("n1", "1", new[] { new PdfFlowParagraph("supporting note", Style(8)) }, Style(8));
        var result = PdfFlow.Layout(new PdfFlowBlock[] { new PdfFlowNoteReference(new("reference", Style()), note), new PdfFlowNoteReference(new("again", Style()), note) }, Options(), Providers());
        var page = Assert.Single(result.Pages);
        var placement = Assert.Single(page.Notes);
        Assert.Equal(new[] { 0, 1 }, placement.ReferenceBlockIndices);
        Assert.Same(note, placement.Note);
        Assert.All(page.Lines, l => Assert.True(l.Bounds.Top <= placement.Bounds.Y));
        Assert.Equal("reference1again1", string.Concat(page.Lines.Select(l => l.Text)));
    }

    [Fact]
    public void FootnoteReservationMovesTheReferenceInsteadOfCoveringEarlierBodyText()
    {
        var note = new PdfFlowNote("n1", "1", new[] { new PdfFlowParagraph("note note note note", Style(10)) }, Style(10));
        var result = PdfFlow.Layout(new PdfFlowBlock[] { new PdfFlowParagraph(string.Join(" ", Enumerable.Repeat("office", 10)), Style()), new PdfFlowNoteReference(new("reference", Style()), note) }, Options(100, 90), Providers());
        var page = Assert.Single(result.Pages, p => p.Notes.Count > 0);
        Assert.Contains(page.Lines, l => l.BlockIndex == 1);
        Assert.All(page.Lines, l => Assert.True(l.Bounds.Top <= page.Notes[0].Bounds.Y));
        Assert.True(page.Index > 0);
    }

    [Fact]
    public void EndnotesAppendOnceInEncounterOrderWithReferenceAssociations()
    {
        var note = new PdfFlowNote("end", "A", new[] { new PdfFlowParagraph("endnote text", Style(10)) }, Style(10));
        var result = PdfFlow.Layout(new[] { new PdfFlowNoteReference(new("first", Style()), note, true), new PdfFlowNoteReference(new("second", Style()), note, true) }, Options(), Providers());
        Assert.Equal(2, result.Pages.Count);
        Assert.Empty(result.Pages[0].Notes);
        var placed = Assert.Single(result.Pages[1].Notes);
        Assert.True(placed.IsEndnote);
        Assert.Equal(new[] { 0, 1 }, placed.ReferenceBlockIndices);
    }

    [Fact]
    public void SectionParityAndReusableComponentsRemainDeterministic()
    {
        var component = new PdfFlowComponent(new[] { new PdfFlowParagraph("component", Style()) });
        var result = PdfFlow.Layout(new PdfFlowBlock[] { component, new PdfFlowSectionBreak(PdfFlowSectionStart.NextOddPage), component }, Options(), Providers());
        Assert.Equal(3, result.Pages.Count);
        Assert.Empty(result.Pages[1].Lines);
        Assert.Equal("component", result.Pages[2].Lines[0].Text);
        var kept = new PdfFlowComponent(new[] { new PdfFlowParagraph("office office office", Style()) }, true);
        Assert.Throws<PdfException>(() => PdfFlow.Layout(new[] { kept }, Options(50, 20), Providers()));
    }

    [Fact]
    public void ColumnGeometryNoteIdentityResourceAndLifetimeFailuresReturnNoPartialDocument()
    {
        Assert.Equal("Composition.ColumnFrame", Assert.Throws<PdfException>(() => PdfFlow.Layout(new[] { new PdfFlowColumns(Array.Empty<PdfFlowBlock>(), gap: 500) }, Options(), Providers())).Diagnostic.Code);
        var one = new PdfFlowNote("same", "1", Array.Empty<PdfFlowBlock>(), Style());
        var two = new PdfFlowNote("same", "2", Array.Empty<PdfFlowBlock>(), Style());
        Assert.Equal("Composition.NoteIdentity", Assert.Throws<PdfException>(() => PdfFlow.Layout(new[] { new PdfFlowNoteReference(new("first", Style()), one), new PdfFlowNoteReference(new("second", Style()), two) }, Options(), Providers())).Diagnostic.Code);
        var result = PdfFlow.Layout(new[] { new PdfFlowColumns(new[] { new PdfFlowParagraph("office", Style()) }) }, Options(), Providers());
        Assert.Throws<OperationCanceledException>(() => result.CreateDocument(cancellationToken: new CancellationToken(true)));
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => result.CreateDocument(new(operationPolicy: new(limits: new(maximumObjects: 1))))).Diagnostic.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothLanguageExamplesWriteTaggedCompositionAndRetainStreams(bool visualBasic)
    {
        using var font = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "text", "business.ttf"));
        using var output = new MemoryStream();
        if (visualBasic)
            ContractExamples.VisualBasic.ContractExample.WriteCompositionPdf(font, output, new PdfSimpleTextShaper());
        else
            ContractExamples.CSharp.ContractExample.WriteCompositionPdf(font, output, new PdfSimpleTextShaper());
        Assert.True(font.CanRead);
        Assert.True(output.CanWrite);
        output.Position = 0;
        using var read = Pdf.Open(output);
        var syntax = read.SourceDocument!;
        var catalog = syntax.Resolve(syntax.Revisions[0].Trailer.Entries["Root"].ReferenceId);
        Assert.True(catalog.Entries.ContainsKey("StructTreeRoot"));
    }

    [Fact]
    public void ImmutableCompositionCanBeLaidOutByIndependentJobs()
    {
        var list = PdfFlowList.Create(new[] { Item("first"), Item("second") }, Style());
        Parallel.For(0, 4, _ =>
        {
            var result = PdfFlow.Layout(new[] { list }, Options(), Providers());
            Assert.Single(result.Pages);
            Assert.Equal("1.", list.Markers[0]);
        });
    }
}
