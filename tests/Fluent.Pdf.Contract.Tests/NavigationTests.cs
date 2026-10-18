using System.IO;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class NavigationTests
{
    internal static PdfValue D(params (string, PdfValue)[] values) => PdfGraphicsNumbers.Dict(values);
    internal static PdfValue T(string value) => AnnotationTests.Text(value);
    internal static PdfDocument Fixture(Action<PdfTransaction, PdfObjectId, PdfObjectId[]> change, int count = 3)
    {
        using var edit = new PdfTransaction();
        var root = edit.Add(PdfValue.Null);
        var tree = edit.Add(PdfValue.Null);
        var pages = Enumerable.Range(0, count).Select(_ => edit.Add(D(("Type", PdfValue.Name("Page")), ("Parent", PdfValue.Reference(tree)), ("MediaBox", PdfGraphicsNumbers.Array(0, 0, 300, 400)), ("Resources", D())))).ToArray();
        edit.Set(tree, D(("Type", PdfValue.Name("Pages")), ("Kids", PdfValue.Array(pages.Select(PdfValue.Reference).ToArray())), ("Count", PdfValue.Integer(count))));
        edit.Set(root, D(("Type", PdfValue.Name("Catalog")), ("Pages", PdfValue.Reference(tree))));
        edit.Trailer = D(("Root", PdfValue.Reference(root)));
        change(edit, root, pages);
        using var bytes = new MemoryStream();
        edit.Save(bytes, new(minimumVersion: "1.7"));
        bytes.Position = 0;
        return Pdf.Open(bytes);
    }

    internal static void Put(PdfTransaction edit, PdfObjectId id, string key, PdfValue value)
    {
        var values = edit.Resolve(id).Entries.ToDictionary(p => p.Key, p => p.Value);
        values[key] = value;
        edit.Set(id, PdfValue.Dictionary(values));
    }

    [Fact]
    public void UnicodeNamedDestinationsAndLinksFollowPhysicalPageIdentityAfterMove()
    {
        using var blank = Pdf.Create().AddPage().AddPage().AddPage();
        using var named = PdfNavigation.SetNamedDestination(blank.CreateSnapshot(), "Նպատակ", new(2, PdfDestinationView.XYZ, 20, 350, null));
        using var linked = PdfNavigation.AddLink(named.CreateSnapshot(), 0, new(10, 20, 80, 30), PdfAction.GoToNamed("Նպատակ").Then(PdfAction.Uri("https://example.test/path?q=1")));
        linked.Pages.Move(2, 0);
        using var bytes = new MemoryStream();
        linked.Save(bytes);
        bytes.Position = 0;
        using var read = Pdf.Open(bytes);
        var inspected = PdfNavigation.Inspect(read.CreateSnapshot());
        Assert.Equal(0, Assert.Single(inspected.NamedDestinations).PageIndex);
        Assert.Contains(inspected.Actions, a => a.Subtype == "GoTo" && a.IsAllowed);
        Assert.Contains(inspected.Actions, a => a.Subtype == "URI" && a.IsAllowed);
        Assert.Equal("Նպատակ", inspected.NamedDestinations[0].Name);
        read.Pages.RemoveAt(0);
        Assert.Equal("Navigation.RemovedTarget", Assert.Throws<PdfException>(() => read.Save(new MemoryStream())).Diagnostic.Code);
    }

    [Theory]
    [InlineData(PdfDestinationView.Fit)]
    [InlineData(PdfDestinationView.FitB)]
    [InlineData(PdfDestinationView.FitH)]
    [InlineData(PdfDestinationView.FitV)]
    [InlineData(PdfDestinationView.FitBH)]
    [InlineData(PdfDestinationView.FitBV)]
    [InlineData(PdfDestinationView.XYZ)]
    [InlineData(PdfDestinationView.FitR)]
    public void EveryDestinationViewSerializesItsExactCoordinateArity(PdfDestinationView view)
    {
        double? [] coordinates = view == PdfDestinationView.FitR ? new double? []
        {
            1,
            2,
            50,
            80
        }

        : view == PdfDestinationView.XYZ ? new double? []
        {
            null,
            20,
            0
        }

        : view == PdfDestinationView.Fit || view == PdfDestinationView.FitB ? Array.Empty<double?>() : new double? []
        {
            null
        };
        using var blank = Pdf.Create().AddPage();
        using var named = PdfNavigation.SetNamedDestination(blank.CreateSnapshot(), "view", new(0, view, coordinates));
        var set = PdfNavigation.Inspect(named.CreateSnapshot());
        var graph = set.Source.SourceDocument!;
        var v = GraphicsTests.Resolve(graph, set.NamedDestinations[0].Definition);
        Assert.Equal(view.ToString(), v.Items[1].NameValue);
        Assert.Equal(coordinates.Length + 2, v.Items.Count);
    }

    [Fact]
    public void DocumentAndPageActionsRequireExplicitPermissionsAndRemainPassive()
    {
        using var blank = Pdf.Create().AddPage();
        Assert.Equal("Navigation.ActionPolicy", Assert.Throws<PdfException>(() => PdfNavigation.SetDocumentAction(blank.CreateSnapshot(), "OpenAction", PdfAction.Launch("never-run.bin"))).Diagnostic.Code);
        Assert.Equal("Navigation.ActionPolicy", Assert.Throws<PdfException>(() => PdfNavigation.AddLink(blank.CreateSnapshot(), 0, new(0, 0, 10, 10), PdfAction.Uri("file:///never/read"))).Diagnostic.Code);
        using var remote = PdfNavigation.SetDocumentAction(blank.CreateSnapshot(), "OpenAction", PdfAction.Remote("offline.pdf", "Chapter", true), new(allowRemote: true));
        using var launched = PdfNavigation.SetPageAction(remote.CreateSnapshot(), 0, "O", PdfAction.Launch("never-run.bin"), new(allowLaunch: true));
        var set = PdfNavigation.Inspect(launched.CreateSnapshot());
        Assert.Contains(set.Actions, a => a.Subtype == "Launch" && !a.IsAllowed && a.IsAttachment);
        Assert.Contains(set.Actions, a => a.Subtype == "GoToR" && !a.IsAllowed);
        using var sanitized = PdfNavigation.Sanitize(launched.CreateSnapshot());
        Assert.Empty(PdfNavigation.Inspect(sanitized.CreateSnapshot()).Actions);
    }

    [Fact]
    public void InventoryAndSanitizationCoverIndirectAdditionalActionsWidgetsOutlinesScriptsNextAndOrphans()
    {
        using var source = Fixture((edit, root, pages) =>
        {
            var script = edit.Add(D(("S", PdfValue.Name("JavaScript")), ("JS", T("throw 'must never run';"))));
            var unknown = edit.Add(D(("S", PdfValue.Name("FutureAction"))));
            var safe = edit.Add(D(("S", PdfValue.Name("URI")), ("URI", PdfValue.String(System.Text.Encoding.ASCII.GetBytes("https://example.test"))), ("Next", PdfValue.Array(PdfValue.Reference(script), PdfValue.Reference(unknown)))));
            Put(edit, root, "OpenAction", PdfValue.Reference(safe));
            Put(edit, root, "AA", PdfValue.Reference(edit.Add(D(("WC", PdfValue.Reference(unknown))))));
            Put(edit, root, "Names", D(("JavaScript", D(("Names", PdfValue.Array(T("onload"), PdfValue.Reference(script)))))));
            Put(edit, root, "Outlines", D(("First", PdfValue.Reference(edit.Add(D(("Title", T("entry")), ("A", PdfValue.Reference(script))))))));
            Put(edit, pages[0], "AA", D(("O", PdfValue.Reference(script)), ("C", PdfValue.Reference(safe))));
            Put(edit, pages[0], "Annots", PdfValue.Array(D(("Subtype", PdfValue.Name("Widget")), ("A", PdfValue.Reference(script)), ("AA", D(("Fo", PdfValue.Reference(unknown)))))));
            edit.Add(D(("S", PdfValue.Name("Launch")), ("F", T("orphan.bin"))));
            Put(edit, root, "AcroForm", D(("XFA", T("opaque script-bearing foreign content"))));
        });
        var inventory = PdfNavigation.Inspect(source.CreateSnapshot());
        Assert.Contains(inventory.Actions, a => a.Subtype == "FutureAction" && a.IsAttachment && !a.IsAllowed);
        Assert.Contains(inventory.Actions, a => a.Subtype == "ActiveContent:XFA" && !a.IsAllowed);
        Assert.True(inventory.Actions.Count(a => a.Subtype == "JavaScript") >= 5);
        using var sanitized = PdfNavigation.Sanitize(source.CreateSnapshot());
        var after = PdfNavigation.Inspect(sanitized.CreateSnapshot());
        Assert.NotEmpty(after.Actions);
        Assert.All(after.Actions, a =>
        {
            Assert.Equal("URI", a.Subtype);
            Assert.True(a.IsAllowed);
        });
        Assert.Contains(PdfNavigation.Inspect(source.CreateSnapshot()).Actions, a => a.Subtype == "Launch");
    }

    [Fact]
    public void ReferencedNextArraysKeepAllowedMembersAndScriptNameSlotsExposeUnknownTypes()
    {
        using var source = Fixture((edit, root, pages) =>
        {
            var unknown = edit.Add(D(("S", PdfValue.Name("FutureAction"))));
            var allowed = edit.Add(D(("S", PdfValue.Name("URI")), ("URI", PdfValue.String(System.Text.Encoding.ASCII.GetBytes("https://example.test")))));
            var array = edit.Add(PdfValue.Array(PdfValue.Reference(unknown), PdfValue.Reference(allowed)));
            Put(edit, root, "OpenAction", D(("S", PdfValue.Name("GoTo")), ("D", PdfValue.Array(PdfValue.Reference(pages[0]), PdfValue.Name("Fit"))), ("Next", PdfValue.Reference(array))));
            Put(edit, root, "Names", D(("JavaScript", D(("Names", PdfValue.Array(T("unknown"), PdfValue.Reference(unknown)))))));
        });
        Assert.Contains(PdfNavigation.Inspect(source.CreateSnapshot()).Actions, r => r.Subtype == "FutureAction" && r.Location.StartsWith("Catalog/Names/JavaScript", StringComparison.Ordinal));
        using var safe = PdfNavigation.Sanitize(source.CreateSnapshot());
        var set = PdfNavigation.Inspect(safe.CreateSnapshot());
        Assert.All(set.Actions, a => Assert.True(a.IsAllowed));
        var action = set.Actions.First(a => a.Subtype == "GoTo").Definition;
        Assert.Equal("URI", action.Entries["Next"].Items.Single().Kind == PdfValueKind.Reference ? set.Source.SourceDocument!.Resolve(action.Entries["Next"].Items.Single().ReferenceId).Entries["S"].NameValue : action.Entries["Next"].Items.Single().Entries["S"].NameValue);
    }

    [Fact]
    public void ActionCyclesFailWhileSharedDagNodesRemainValid()
    {
        using var cyclic = Fixture((edit, root, pages) =>
        {
            var id = edit.Add(D(("S", PdfValue.Name("Named"))));
            Put(edit, id, "Next", PdfValue.Reference(id));
            Put(edit, root, "OpenAction", PdfValue.Reference(id));
        });
        Assert.Equal("Navigation.ActionCycle", Assert.Throws<PdfException>(() => PdfNavigation.Inspect(cyclic.CreateSnapshot())).Diagnostic.Code);
        using var shared = Fixture((edit, root, pages) =>
        {
            var child = edit.Add(D(("S", PdfValue.Name("Named"))));
            Put(edit, root, "OpenAction", D(("S", PdfValue.Name("Named")), ("Next", PdfValue.Array(PdfValue.Reference(child), PdfValue.Reference(child)))));
        });
        Assert.NotEmpty(PdfNavigation.Inspect(shared.CreateSnapshot()).Actions);
    }

    [Fact]
    public void MalformedMissingAndCyclicDestinationsHaveStableFailures()
    {
        using var malformed = Fixture((edit, root, pages) => Put(edit, root, "OpenAction", D(("S", PdfValue.Name("GoTo")), ("D", PdfValue.Array(PdfValue.Reference(pages[0]), PdfValue.Name("XYZ"), PdfValue.Null)))));
        Assert.Equal("Navigation.Destination", Assert.Throws<PdfException>(() => malformed.Save(new MemoryStream())).Diagnostic.Code);
        using var blank = Pdf.Create().AddPage();
        Assert.Equal("Navigation.NamedDestination", Assert.Throws<PdfException>(() => PdfNavigation.AddLink(blank.CreateSnapshot(), 0, new(0, 0, 10, 10), PdfAction.GoToNamed("missing"))).Diagnostic.Code);
        using var cycle = Fixture((edit, root, pages) =>
        {
            var dest = edit.Add(D());
            Put(edit, dest, "D", PdfValue.Reference(dest));
            Put(edit, root, "Dests", D(("cycle", PdfValue.Reference(dest))));
        });
        Assert.Equal("Navigation.DestinationCycle", Assert.Throws<PdfException>(() => PdfNavigation.Inspect(cycle.CreateSnapshot())).Diagnostic.Code);
        Assert.Throws<ArgumentException>(() => new PdfDestination(0, PdfDestinationView.FitR, 10, 20, 0, 5));
    }

    [Fact]
    public void NamedDestinationsAndLocalActionsAreRemappedThroughMergeAndExtract()
    {
        using var blank = Pdf.Create().AddPage().AddPage();
        using var named = PdfNavigation.SetNamedDestination(blank.CreateSnapshot(), "chapter", new(1));
        using var linked = PdfNavigation.AddLink(named.CreateSnapshot(), 0, new(10, 10, 10, 10), PdfAction.GoToNamed("chapter"));
        using var selected = PdfPageOrganization.Extract(linked.CreateSnapshot(), PdfPageSelection.Parse("2,1", 2));
        Assert.Equal(0, Assert.Single(PdfNavigation.Inspect(selected.CreateSnapshot()).NamedDestinations).PageIndex);
        using var bytes = new MemoryStream();
        selected.Save(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothLanguageExamplesWriteNavigationAndPreserveOutputOwnership(bool vb)
    {
        using var bytes = new MemoryStream();
        if (vb)
            ContractExamples.VisualBasic.ContractExample.WriteNavigationPdf(bytes);
        else
            ContractExamples.CSharp.ContractExample.WriteNavigationPdf(bytes);
        Assert.True(bytes.CanWrite);
        bytes.Position = 0;
        using var document = Pdf.Open(bytes);
        Assert.Equal(1, Assert.Single(PdfNavigation.Inspect(document.CreateSnapshot()).NamedDestinations).PageIndex);
    }

    [Fact]
    public void NoActionPolicyRemovesEvenLocalAndUriActions()
    {
        using var blank = Pdf.Create().AddPage();
        using var linked = PdfNavigation.AddLink(blank.CreateSnapshot(), 0, new(0, 0, 10, 10), PdfAction.GoTo(new(0)).Then(PdfAction.Uri("https://example.test")));
        using var safe = PdfNavigation.Sanitize(linked.CreateSnapshot(), new(allowLocalGoTo: false, allowUri: false));
        Assert.Empty(PdfNavigation.Inspect(safe.CreateSnapshot()).Actions);
    }

    [Fact]
    public void LegacyByteKeysSurviveConsolidationAndMalformedAdditionalActionsFail()
    {
        using var source = Fixture((edit, root, pages) =>
        {
            Put(edit, root, "Dests", D(("\u0080", PdfValue.Array(PdfValue.Reference(pages[1]), PdfValue.Name("Fit")))));
            Put(edit, root, "OpenAction", D(("S", PdfValue.Name("GoTo")), ("D", PdfValue.Name("\u0080"))));
        });
        using var edited = PdfNavigation.SetNamedDestination(source.CreateSnapshot(), "new", new(0));
        var set = PdfNavigation.Inspect(edited.CreateSnapshot());
        Assert.Equal(1, set.NamedDestinations.Single(n => n.Name == "\u0080").PageIndex);
        using var linked = PdfNavigation.AddLink(edited.CreateSnapshot(), 0, new(0, 0, 10, 10), PdfAction.GoToNamed("\u0080"));
        using var malformed = Fixture((edit, root, pages) => Put(edit, root, "AA", PdfValue.Integer(1)));
        Assert.Equal("Navigation.AdditionalActions", Assert.Throws<PdfException>(() => PdfNavigation.Inspect(malformed.CreateSnapshot())).Diagnostic.Code);
    }

    [Fact]
    public void CancellationBudgetsAndDetachedOwnershipAreEnforced()
    {
        using var blank = Pdf.Create().AddPage();
        var snapshot = blank.CreateSnapshot();
        blank.Dispose();
        Assert.Throws<OperationCanceledException>(() => PdfNavigation.Inspect(snapshot, cancellationToken: new CancellationToken(true)));
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfNavigation.Inspect(snapshot, policy: new(limits: new(maximumObjects: 1)))).Diagnostic.Code);
        var advanced = PdfAction.Advanced(D(("S", PdfValue.Name("GoTo")), ("D", PdfValue.Reference(new(90)))));
        Assert.Equal("Graph.ForeignReference", Assert.Throws<PdfException>(() => PdfNavigation.SetDocumentAction(snapshot, "OpenAction", advanced)).Diagnostic.Code);
        using var output = PdfNavigation.SetNamedDestination(snapshot, "valid", new(0));
        using var bytes = new MemoryStream();
        output.Save(bytes);
        Assert.True(bytes.CanWrite);
    }
}
