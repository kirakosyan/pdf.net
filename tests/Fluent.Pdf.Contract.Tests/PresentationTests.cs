using System.IO;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class PresentationTests
{
    private static PdfValue D(params (string, PdfValue)[] pairs) => NavigationTests.D(pairs);
    [Fact]
    public void NestedBookmarksHaveCorrectLinksCountsUnicodeStyleAndPageIdentity()
    {
        using var blank = Pdf.Create().AddPage().AddPage().AddPage();
        var branch = new PdfBookmark("Փակ", isOpen: false, children: new[] { new PdfBookmark("First", new(0)), new PdfBookmark("Last", new(2)) });
        using var document = PdfPresentation.SetOutlines(blank.CreateSnapshot(), new[] { new PdfBookmark("Root", new(1), bold: true, italic: true, color: new(PdfColorSpace.Rgb, .2, .4, .6), children: new[] { branch, new PdfBookmark("External", action: PdfAction.Uri("https://example.test")) }) });
        var before = PdfPresentation.Inspect(document.CreateSnapshot());
        var root = Assert.Single(before.Outlines);
        Assert.Equal(1, root.TargetPageIndex);
        Assert.Equal("Փակ", root.Children[0].Title);
        Assert.False(root.Children[0].IsOpen);
        Assert.Equal(-2, root.Children[0].Definition.Entries["Count"].IntegerValue);
        Assert.Equal(2, root.Definition.Entries["Count"].IntegerValue);
        Assert.Equal(3, root.Definition.Entries["F"].IntegerValue);
        Assert.Equal(new[] { .2, .4, .6 }, root.Definition.Entries["C"].Items.Select(v => v.NumberValue));
        document.Pages.Move(2, 0);
        using var bytes = new MemoryStream();
        document.Save(bytes);
        bytes.Position = 0;
        using var moved = Pdf.Open(bytes);
        Assert.Equal(0, PdfPresentation.Inspect(moved.CreateSnapshot()).Outlines[0].Children[0].Children[1].TargetPageIndex);
        using var selected = PdfPageOrganization.Extract(moved.CreateSnapshot(), PdfPageSelection.Parse("3,2,1", 3));
        var imported = PdfPresentation.Inspect(selected.CreateSnapshot());
        Assert.Equal(2, imported.Outlines[0].Children[0].Children[1].TargetPageIndex);
        using var none = PdfPresentation.SetOutlines(selected.CreateSnapshot(), Array.Empty<PdfBookmark>());
        Assert.Empty(PdfPresentation.Inspect(none.CreateSnapshot()).Outlines);
    }

    [Fact]
    public void OutlineCyclesSharedNodesAndDepthAreRejectedExplicitly()
    {
        using var cyclic = NavigationTests.Fixture((edit, catalog, pages) =>
        {
            var root = edit.Add(D());
            var node = edit.Add(D(("Title", NavigationTests.T("cycle")), ("Parent", PdfValue.Reference(root))));
            NavigationTests.Put(edit, node, "Next", PdfValue.Reference(node));
            edit.Set(root, D(("First", PdfValue.Reference(node)), ("Last", PdfValue.Reference(node))));
            NavigationTests.Put(edit, catalog, "Outlines", PdfValue.Reference(root));
        });
        Assert.Equal("Presentation.OutlineCycle", Assert.Throws<PdfException>(() => PdfPresentation.Inspect(cyclic.CreateSnapshot())).Diagnostic.Code);
        using var blank = Pdf.Create().AddPage();
        PdfBookmark deep = new("leaf", new(0));
        for (int i = 0; i < 130; i++)
            deep = new("level " + i, children: new[] { deep });
        Assert.Equal("Limit.Depth", Assert.Throws<PdfException>(() => PdfPresentation.SetOutlines(blank.CreateSnapshot(), new[] { deep })).Diagnostic.Code);
        Assert.Throws<OperationCanceledException>(() => PdfPresentation.SetOutlines(blank.CreateSnapshot(), new[] { deep }, cancellationToken: new CancellationToken(true)));
    }

    [Fact]
    public void AllPageLabelStylesAndRestartsRoundTripAndClear()
    {
        using var blank = Pdf.Create();
        for (int i = 0; i < 6; i++)
            blank.AddPage();
        var numbering = new PdfPageNumbering(new(0, PdfPageLabelStyle.LowerRoman), new(2, PdfPageLabelStyle.UpperRoman, 4, "Part "), new(3, PdfPageLabelStyle.UpperLetters, 27), new(4, PdfPageLabelStyle.LowerLetters, 52), new(5, PdfPageLabelStyle.None, prefix: "Appendix"));
        using var document = PdfPresentation.SetPageLabels(blank.CreateSnapshot(), numbering);
        var set = PdfPresentation.Inspect(document.CreateSnapshot());
        Assert.NotNull(set.Numbering);
        Assert.Equal(new[] { "i", "ii", "Part IV", "AA", "zz", "Appendix" }, Enumerable.Range(0, 6).Select(set.Numbering!.GetLabel));
        Assert.Equal(5, set.PageLabels.Count);
        using var cleared = PdfPresentation.SetPageLabels(document.CreateSnapshot(), null);
        Assert.Empty(PdfPresentation.Inspect(cleared.CreateSnapshot()).PageLabels);
        Assert.Equal("Presentation.PageLabelRange", Assert.Throws<PdfException>(() => PdfPresentation.SetPageLabels(blank.CreateSnapshot(), new(new PdfPageNumberRange(0), new PdfPageNumberRange(6)))).Diagnostic.Code);
    }

    [Fact]
    public void UnknownLabelsPreferencesModesAndTransitionsArePreservedAndDiagnostic()
    {
        using var source = NavigationTests.Fixture((edit, root, pages) =>
        {
            NavigationTests.Put(edit, root, "PageLabels", D(("Nums", PdfValue.Array(PdfValue.Integer(0), D(("S", PdfValue.Name("FutureStyle")), ("P", NavigationTests.T("prefix")))))));
            NavigationTests.Put(edit, root, "ViewerPreferences", D(("FutureSetting", PdfValue.Boolean(true))));
            NavigationTests.Put(edit, root, "PageLayout", PdfValue.Name("FutureLayout"));
            NavigationTests.Put(edit, pages[0], "Trans", D(("S", PdfValue.Name("FutureTransition")), ("FutureValue", PdfValue.Integer(7))));
        });
        var set = PdfPresentation.Inspect(source.CreateSnapshot());
        Assert.Null(set.Numbering);
        Assert.Equal("FutureStyle", set.PageLabels[0].Entries["S"].NameValue);
        Assert.Contains(set.OperationReport.Diagnostics, d => d.Code == "Presentation.UnsupportedPreference");
        Assert.Contains(set.OperationReport.Diagnostics, d => d.Code == "Presentation.UnsupportedPageLayout");
        Assert.Contains(set.OperationReport.Diagnostics, d => d.Code == "Presentation.UnsupportedTransition");
        using var patch = PdfPresentation.SetViewerPreferences(source.CreateSnapshot(), D(("HideToolbar", PdfValue.Boolean(true))));
        var changed = PdfPresentation.Inspect(patch.CreateSnapshot());
        Assert.True(changed.Catalog.Entries["ViewerPreferences"].Entries.ContainsKey("FutureSetting"));
        Assert.Equal(7, changed.Pages[0].Transition.Entries["FutureValue"].IntegerValue);
        Assert.Equal("Presentation.UnsupportedPreference", Assert.Throws<PdfException>(() => PdfPresentation.SetViewerPreferences(source.CreateSnapshot(), D(("FutureSetting", PdfValue.Boolean(true))))).Diagnostic.Code);
    }

    [Fact]
    public void ViewerPreferencesInitialViewAndVersionFloorsAreSerialized()
    {
        using var blank = Pdf.Create().AddPage().AddPage();
        using var viewed = PdfPresentation.SetView(blank.CreateSnapshot(), PdfPageLayout.TwoPageRight, PdfPageMode.UseOutlines);
        using var initial = PdfPresentation.SetInitialView(viewed.CreateSnapshot(), new(1, PdfDestinationView.FitH, 200));
        var preferences = D(("HideToolbar", PdfValue.Boolean(true)), ("DisplayDocTitle", PdfValue.Boolean(true)), ("Direction", PdfValue.Name("R2L")), ("Duplex", PdfValue.Name("DuplexFlipLongEdge")), ("PrintPageRange", PdfValue.Array(PdfValue.Integer(1), PdfValue.Integer(2))), ("NumCopies", PdfValue.Integer(3)), ("PrintScaling", PdfValue.Name("None")), ("Enforce", PdfValue.Array(PdfValue.Name("PrintScaling"))));
        using var document = PdfPresentation.SetViewerPreferences(initial.CreateSnapshot(), preferences);
        var set = PdfPresentation.Inspect(document.CreateSnapshot());
        Assert.Equal("2.0", set.Source.SourceDocument!.Version);
        Assert.Equal("TwoPageRight", set.Catalog.Entries["PageLayout"].NameValue);
        Assert.Equal("UseOutlines", set.Catalog.Entries["PageMode"].NameValue);
        Assert.Equal("FitH", set.Catalog.Entries["OpenAction"].Items[1].NameValue);
        Assert.Empty(set.OperationReport.Diagnostics);
        using var clear = PdfPresentation.SetInitialView(document.CreateSnapshot(), null);
        Assert.False(PdfPresentation.Inspect(clear.CreateSnapshot()).Catalog.Entries.ContainsKey("OpenAction"));
        Assert.Equal("Presentation.PreferenceValue", Assert.Throws<PdfException>(() => PdfPresentation.SetViewerPreferences(blank.CreateSnapshot(), D(("Enforce", PdfValue.Array(PdfValue.Name("PrintScaling")))))).Diagnostic.Code);
        Assert.Equal("Presentation.PreferenceValue", Assert.Throws<PdfException>(() => PdfPresentation.SetViewerPreferences(blank.CreateSnapshot(), D(("PrintPageRange", PdfValue.Array(PdfValue.Integer(1), PdfValue.Integer(3)))))).Diagnostic.Code);
    }

    [Theory]
    [InlineData(PdfTransitionStyle.Replace)]
    [InlineData(PdfTransitionStyle.Split)]
    [InlineData(PdfTransitionStyle.Blinds)]
    [InlineData(PdfTransitionStyle.Box)]
    [InlineData(PdfTransitionStyle.Wipe)]
    [InlineData(PdfTransitionStyle.Dissolve)]
    [InlineData(PdfTransitionStyle.Glitter)]
    [InlineData(PdfTransitionStyle.Fly)]
    [InlineData(PdfTransitionStyle.Push)]
    [InlineData(PdfTransitionStyle.Cover)]
    [InlineData(PdfTransitionStyle.Uncover)]
    [InlineData(PdfTransitionStyle.Fade)]
    public void EveryStandardTransitionIsPassiveAndRoundTrips(PdfTransitionStyle style)
    {
        using var blank = Pdf.Create().AddPage();
        using var document = PdfPresentation.SetPageTransition(blank.CreateSnapshot(), 0, new(style, 1.5), 7.25);
        var page = PdfPresentation.Inspect(document.CreateSnapshot()).Pages[0];
        Assert.Equal(style == PdfTransitionStyle.Replace ? "R" : style.ToString(), page.Transition.Entries["S"].NameValue);
        Assert.Equal(1.5, page.Transition.Entries["D"].NumberValue);
        Assert.Equal(7.25, page.DisplayDuration.NumberValue);
        using var clear = PdfPresentation.SetPageTransition(document.CreateSnapshot(), 0, null);
        Assert.Equal(PdfValueKind.Null, PdfPresentation.Inspect(clear.CreateSnapshot()).Pages[0].Transition.Kind);
    }

    [Fact]
    public void TransitionParametersAndResourceBudgetsAreExplicit()
    {
        Assert.Throws<ArgumentException>(() => new PdfPageTransition(PdfTransitionStyle.Fade, direction: 90));
        Assert.Throws<ArgumentException>(() => new PdfPageTransition(PdfTransitionStyle.Wipe, dimension: PdfTransitionDimension.Vertical));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfPageTransition(PdfTransitionStyle.Fly, scale: -1));
        using var blank = Pdf.Create().AddPage();
        using var fly = PdfPresentation.SetPageTransition(blank.CreateSnapshot(), 0, new(PdfTransitionStyle.Fly, motion: PdfTransitionMotion.Outward, direction: -1, scale: .5, rectangular: true));
        var value = PdfPresentation.Inspect(fly.CreateSnapshot()).Pages[0].Transition;
        Assert.Equal("None", value.Entries["Di"].NameValue);
        Assert.Equal("O", value.Entries["M"].NameValue);
        Assert.Equal(.5, value.Entries["SS"].NumberValue);
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfPresentation.Inspect(blank.CreateSnapshot(), new(limits: new(maximumObjects: 1)))).Diagnostic.Code);
        Assert.Throws<OperationCanceledException>(() => PdfPresentation.Inspect(blank.CreateSnapshot(), cancellationToken: new CancellationToken(true)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothLanguageConsumersEmitBookmarksLabelsAndPassivePresentation(bool vb)
    {
        using var bytes = new MemoryStream();
        if (vb)
            ContractExamples.VisualBasic.ContractExample.WritePresentationPdf(bytes);
        else
            ContractExamples.CSharp.ContractExample.WritePresentationPdf(bytes);
        Assert.True(bytes.CanWrite);
        bytes.Position = 0;
        using var document = Pdf.Open(bytes);
        var set = PdfPresentation.Inspect(document.CreateSnapshot());
        Assert.Equal("Details", Assert.Single(Assert.Single(set.Outlines).Children).Title);
        Assert.Equal("Report-1", set.Numbering!.GetLabel(1));
        Assert.Equal("Fade", set.Pages[1].Transition.Entries["S"].NameValue);
    }

    [Fact]
    public void DeepFiniteOutlineTreesAndClosedRootCountsFollowTheStandard()
    {
        using var blank = Pdf.Create().AddPage();
        PdfBookmark branch = new("leaf", new(0));
        for (int i = 0; i < 16; i++)
            branch = new("level " + i, children: new[] { branch });
        using var document = PdfPresentation.SetOutlines(blank.CreateSnapshot(), new[] { branch });
        var root = PdfPresentation.Inspect(document.CreateSnapshot()).Outlines[0];
        int levels = 0;
        while (root.Children.Count > 0)
        {
            levels++;
            root = root.Children[0];
        }

        Assert.Equal(16, levels);
        using var closed = PdfPresentation.SetOutlines(blank.CreateSnapshot(), new[] { new PdfBookmark("closed", isOpen: false, children: new[] { new PdfBookmark("child", new(0)) }) });
        var snapshot = closed.CreateSnapshot();
        var graph = snapshot.SourceDocument!;
        var catalog = graph.Resolve(graph.Revisions[0].Trailer.Entries["Root"].ReferenceId);
        Assert.False(graph.Resolve(catalog.Entries["Outlines"].ReferenceId).Entries.ContainsKey("Count"));
        Assert.Throws<ArgumentException>(() => new PdfPageTransition(PdfTransitionStyle.Push, direction: 90));
        using var zero = PdfPresentation.SetPageTransition(blank.CreateSnapshot(), 0, new(PdfTransitionStyle.Fly, 0, direction: -1, scale: 0));
        Assert.Empty(PdfPresentation.Inspect(zero.CreateSnapshot()).OperationReport.Diagnostics);
    }

    [Fact]
    public void LabelTreeCyclesAndForeignPreferenceReferencesFail()
    {
        using var source = NavigationTests.Fixture((edit, root, pages) =>
        {
            var tree = edit.Add(D());
            edit.Set(tree, D(("Kids", PdfValue.Array(PdfValue.Reference(tree)))));
            NavigationTests.Put(edit, root, "PageLabels", PdfValue.Reference(tree));
        });
        Assert.Equal("Presentation.LabelCycle", Assert.Throws<PdfException>(() => PdfPresentation.Inspect(source.CreateSnapshot())).Diagnostic.Code);
        using var blank = Pdf.Create().AddPage();
        Assert.Equal("Graph.ForeignReference", Assert.Throws<PdfException>(() => PdfPresentation.SetViewerPreferences(blank.CreateSnapshot(), D(("HideToolbar", PdfValue.Reference(new(1)))))).Diagnostic.Code);
    }
}
