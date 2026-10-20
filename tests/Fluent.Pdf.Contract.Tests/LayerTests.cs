using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class LayerTests
{
    private static PdfValue D(params (string, PdfValue)[] pairs) => NavigationTests.D(pairs);
    private static PdfValue N(string value) => PdfValue.Name(value);
    private static PdfValue T(string value) => NavigationTests.T(value);
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    internal static PdfDocument Fixture(string content, Action<PdfTransaction, PdfObjectId, PdfObjectId[], PdfObjectId[]>? extra = null) => NavigationTests.Fixture((edit, root, pages) =>
    {
        var a = edit.Add(D(("Type", N("OCG")), ("Name", T("Visible")), ("Custom", T("preserve"))));
        var b = edit.Add(D(("Type", N("OCG")), ("Name", T("Hidden"))));
        var resources = D(("Properties", D(("A", PdfValue.Reference(a)), ("B", PdfValue.Reference(b)))), ("Font", D(("F1", D(("Type", N("Font")), ("Subtype", N("Type1")), ("BaseFont", N("Helvetica")))))));
        NavigationTests.Put(edit, pages[0], "Resources", resources);
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(content));
        NavigationTests.Put(edit, pages[0], "Contents", PdfValue.Reference(edit.Add(PdfValues.Stream(D(), input))));
        NavigationTests.Put(edit, root, "OCProperties", D(("OCGs", PdfValue.Array(PdfValue.Reference(a), PdfValue.Reference(b))), ("D", D(("BaseState", N("ON")), ("OFF", PdfValue.Array(PdfValue.Reference(b)))))));
        extra?.Invoke(edit, root, pages, new[] { a, b });
    }, 1);
    private static PdfValue Member(PdfLayerSet set, string policy) => D(("Type", N("OCMD")), ("P", N(policy)), ("OCGs", PdfValue.Array(set.Groups.Select(x => PdfValue.Reference(x.Id)).ToArray())));
    [Fact]
    public void GroupsHaveUnicodeNamesExplicitUsageAndDetachedSourceOwnership()
    {
        using var blank = Pdf.Create().AddPage();
        using var document = PdfLayers.AddGroup(blank.CreateSnapshot(), "Հայերեն العربية", visible: false);
        var set = PdfLayers.Inspect(document.CreateSnapshot());
        var group = Assert.Single(set.Groups);
        Assert.Equal("Հայերեն العربية", group.Name);
        Assert.False(PdfLayers.IsVisible(set.Source, PdfValue.Reference(group.Id)).Visible);
        Assert.Equal("1.7", document.SourceDocument!.Version);
        document.Dispose();
        using var edited = PdfLayers.EditGroup(group, "Renamed", D(("View", D(("ViewState", N("ON"))))));
        Assert.Equal("Renamed", Assert.Single(PdfLayers.Inspect(edited.CreateSnapshot()).Groups).Name);
        Assert.Equal("Հայերեն العربية", group.Name);
        using var source = Fixture("");
        var original = PdfLayers.Inspect(source.CreateSnapshot()).Groups[0];
        using var renamed = PdfLayers.EditGroup(original, "Changed");
        Assert.Equal("preserve", Encoding.BigEndianUnicode.GetString(PdfLayers.Inspect(renamed.CreateSnapshot()).Groups[0].Definition.Entries["Custom"].StringBytes.Skip(2).ToArray()));
    }

    [Theory]
    [InlineData("AllOn", false)]
    [InlineData("AnyOn", true)]
    [InlineData("AllOff", false)]
    [InlineData("AnyOff", true)]
    public void MembershipPoliciesAndEmptyMembershipFollowTheConfiguredStates(string policy, bool expected)
    {
        using var source = Fixture("");
        var set = PdfLayers.Inspect(source.CreateSnapshot());
        Assert.Equal(expected, PdfLayers.IsVisible(set.Source, Member(set, policy)).Visible);
        Assert.True(PdfLayers.IsVisible(set.Source, D(("Type", N("OCMD")), ("P", N(policy)), ("OCGs", PdfValue.Array(PdfValue.Null, PdfValue.Reference(new PdfObjectId(999999, 0)))))).Visible);
    }

    [Fact]
    public void NestedExpressionsIntentsAndNullGroupsUseBoundedExplicitSemantics()
    {
        using var source = Fixture("");
        var set = PdfLayers.Inspect(source.CreateSnapshot());
        var a = PdfValue.Reference(set.Groups[0].Id);
        var b = PdfValue.Reference(set.Groups[1].Id);
        var membership = D(("Type", N("OCMD")), ("VE", PdfValue.Array(N("And"), a, PdfValue.Array(N("Not"), b))));
        Assert.True(PdfLayers.IsVisible(set.Source, membership).Visible);
        var alternate = D(("BaseState", N("OFF")), ("ON", PdfValue.Array(b)));
        Assert.False(PdfLayers.IsVisible(set.Source, membership, alternate).Visible);
        Assert.True(PdfLayers.IsVisible(set.Source, D(("Type", N("OCMD")), ("VE", PdfValue.Array(N("Not"), a))), D(("Intent", PdfValue.Array()))).Visible);
        PdfValue nested = a;
        for (int i = 0; i < 12; i++)
            nested = PdfValue.Array(N("Not"), nested);
        Assert.Equal("Limit.Depth", Error(() => PdfLayers.IsVisible(set.Source, D(("Type", N("OCMD")), ("VE", nested)), policy: new(limits: new(maximumDepth: 8)))));
    }

    [Fact]
    public void SelectingAnAlternateConfigurationMaterializesItsStatesIntoTheInitialView()
    {
        using var source = Fixture("");
        var set = PdfLayers.Inspect(source.CreateSnapshot());
        var a = PdfValue.Reference(set.Groups[0].Id);
        var b = PdfValue.Reference(set.Groups[1].Id);
        using var configured = PdfLayers.SetConfiguration(set.Source, D(("Name", T("Alternate")), ("BaseState", N("Unchanged")), ("ON", PdfValue.Array(b)), ("OFF", PdfValue.Array(a))), makeDefault: false);
        Assert.Single(PdfLayers.Inspect(configured.CreateSnapshot()).Configurations);
        using var selected = PdfLayers.SelectConfiguration(configured.CreateSnapshot(), 0);
        var selectedSet = PdfLayers.Inspect(selected.CreateSnapshot());
        Assert.False(PdfLayers.IsVisible(selectedSet.Source, a).Visible);
        Assert.True(PdfLayers.IsVisible(selectedSet.Source, b).Visible);
        Assert.Equal("ON", selectedSet.Properties.Entries["D"].Entries["BaseState"].NameValue);
        Assert.False(PdfLayers.IsVisible(set.Source, b).Visible);
        Assert.Equal("Layers.DefaultBaseState", Error(() => PdfLayers.SetConfiguration(set.Source, D(("BaseState", N("OFF"))))));
    }

    [Fact]
    public void UsageApplicationsCombineRecommendationsAndAnOmittedGroupArrayAffectsNone()
    {
        using var source = Fixture("", (edit, root, _, groups) => NavigationTests.Put(edit, groups[0], "Usage", D(("View", D(("ViewState", N("ON")))), ("Print", D(("PrintState", N("OFF")))))));
        var set = PdfLayers.Inspect(source.CreateSnapshot());
        var a = PdfValue.Reference(set.Groups[0].Id);
        var print = D(("AS", PdfValue.Array(D(("Event", N("Print")), ("Category", PdfValue.Array(N("Print"))), ("OCGs", PdfValue.Array(a))), D(("Event", N("Print")), ("Category", PdfValue.Array(N("View"))), ("OCGs", PdfValue.Array(a))))));
        Assert.False(PdfLayers.IsVisible(set.Source, a, print, PdfLayerUsage.Print).Visible);
        Assert.True(PdfLayers.IsVisible(set.Source, a, print, PdfLayerUsage.View).Visible);
        var omitted = D(("AS", PdfValue.Array(D(("Event", N("Print")), ("Category", PdfValue.Array(N("Print")))))));
        Assert.True(PdfLayers.IsVisible(set.Source, a, omitted, PdfLayerUsage.Print).Visible);
        var unknown = D(("AS", PdfValue.Array(D(("Event", N("View")), ("Category", PdfValue.Array(N("Zoom"))), ("OCGs", PdfValue.Array(a))))));
        Assert.Equal("Layers.UsageContextRequired", Error(() => PdfLayers.IsVisible(set.Source, a, unknown)));
    }

    [Fact]
    public void FlatteningDropsHiddenTextAndOptionalMarkersButRetainsGraphicsStateChanges()
    {
        using var source = Fixture("/OC /B BDC 1 0 0 rg BT /F1 12 Tf 1 0 0 1 20 300 Tm (HIDDEN-LAYER-SECRET) Tj ET 10 10 20 20 re F EMC 40 40 30 30 re f BT /F1 12 Tf 1 0 0 1 20 270 Tm (visible) Tj ET");
        using var flattened = PdfLayers.Flatten(source.CreateSnapshot());
        var program = flattened.Pages[0].ReadContent();
        Assert.Contains(program.Operations, x => x.Operation == PdfOperator.FillRgb);
        Assert.Contains(program.Operations, x => x.Operation == PdfOperator.EndPath);
        Assert.DoesNotContain(program.Operations, x => x.Operation == PdfOperator.BeginMarkedContentProperties);
        Assert.Single(program.Operations, x => x.Operation == PdfOperator.ShowText);
        Assert.Empty(PdfLayers.Inspect(flattened.CreateSnapshot()).Groups);
        using var output = new MemoryStream();
        flattened.Save(output);
        Assert.DoesNotContain("HIDDEN-LAYER-SECRET", Encoding.ASCII.GetString(output.ToArray()));
        Assert.Single(flattened.SourceDocument!.Revisions);
        Assert.Contains(source.Pages[0].ReadContent().Operations, x => x.Operation == PdfOperator.BeginMarkedContentProperties);
    }

    [Theory]
    [InlineData("BT /F1 12 Tf /OC /B BDC (hidden) Tj EMC (visible) Tj ET", "Layers.TextAdvanceProfile")]
    [InlineData("BT /F1 12 Tf 4 Tr /OC /B BDC (hidden) Tj EMC ET", "Layers.TextClippingProfile")]
    [InlineData("/Span << /ActualText (semantic hidden copy) >> BDC /OC /B BDC 0 0 10 10 re f EMC EMC", "Layers.SemanticMarkedContent")]
    public void UnsupportedStateOrSemanticDependenciesRejectTheWholeRewrite(string content, string expected)
    {
        using var source = Fixture(content);
        Assert.Equal(expected, Error(() => PdfLayers.Flatten(source.CreateSnapshot())));
        Assert.Equal(2, PdfLayers.Inspect(source.CreateSnapshot()).Groups.Count);
    }

    [Fact]
    public void AnExplicitTextPositionResetAllowsVisibleTextAfterAHiddenAdvance()
    {
        using var source = Fixture("BT /F1 12 Tf /OC /B BDC (hidden) Tj EMC 1 0 0 1 20 30 Tm (visible) Tj ET");
        using var flattened = PdfLayers.Flatten(source.CreateSnapshot());
        Assert.Single(flattened.Pages[0].ReadContent().Operations, x => x.Operation == PdfOperator.ShowText);
    }

    [Fact]
    public void HiddenXObjectsAndAnnotationsAreRemovedAndPageExtractionRetainsGroupReferences()
    {
        using var source = Fixture("/Secret Do /Visible Do", (edit, root, pages, groups) =>
        {
            using var secret = new MemoryStream(Encoding.ASCII.GetBytes("% HIDDEN-FORM-PAYLOAD\n0 0 10 10 re f"));
            var form = edit.Add(PdfValues.Stream(D(("Type", N("XObject")), ("Subtype", N("Form")), ("BBox", PdfGraphicsNumbers.Array(0, 0, 100, 100)), ("OC", PdfValue.Reference(groups[1]))), secret));
            using var visible = new MemoryStream(Encoding.ASCII.GetBytes("20 20 20 20 re f"));
            var visibleForm = edit.Add(PdfValues.Stream(D(("Type", N("XObject")), ("Subtype", N("Form")), ("BBox", PdfGraphicsNumbers.Array(0, 0, 100, 100))), visible));
            var resources = edit.Resolve(pages[0]).Entries["Resources"].Entries.ToDictionary(p => p.Key, p => p.Value);
            resources["XObject"] = D(("Secret", PdfValue.Reference(form)), ("Visible", PdfValue.Reference(visibleForm)));
            NavigationTests.Put(edit, pages[0], "Resources", PdfValue.Dictionary(resources));
            var note = edit.Add(D(("Type", N("Annot")), ("Subtype", N("Text")), ("Rect", PdfGraphicsNumbers.Array(0, 0, 20, 20)), ("OC", PdfValue.Reference(groups[1])), ("Contents", T("hidden annotation"))));
            NavigationTests.Put(edit, pages[0], "Annots", PdfValue.Array(PdfValue.Reference(note)));
        });
        using var extracted = PdfPageOrganization.Extract(source.CreateSnapshot(), PdfPageSelection.Parse("1", 1));
        var inventory = PdfLayers.Inspect(extracted.CreateSnapshot());
        Assert.Equal(2, inventory.Groups.Count);
        using var flattened = PdfLayers.Flatten(source.CreateSnapshot());
        Assert.Single(flattened.Pages[0].ReadContent().Operations, x => x.Operation == PdfOperator.DrawObject);
        Assert.Empty(flattened.Pages[0].Source!.Dictionary.Entries["Annots"].Items);
        using var output = new MemoryStream();
        flattened.Save(output);
        Assert.DoesNotContain("HIDDEN-FORM-PAYLOAD", Encoding.ASCII.GetString(output.ToArray()));
    }

    [Fact]
    public void NonOptionalUseOfTheOcTagIsRetainedAndStateActionsRequireAnExplicitDecision()
    {
        using var source = Fixture("/OC << /Custom 42 >> BDC 0 0 10 10 re f EMC /OC MP /OC << /Custom 17 >> DP");
        using var flattened = PdfLayers.Flatten(source.CreateSnapshot());
        Assert.Contains(flattened.Pages[0].ReadContent().Operations, x => x.Operation == PdfOperator.BeginMarkedContentProperties);
        Assert.Contains(flattened.Pages[0].ReadContent().Operations, x => x.Operation == PdfOperator.MarkPoint);
        using var action = Fixture("", (edit, root, _, groups) => NavigationTests.Put(edit, root, "OpenAction", D(("S", N("SetOCGState")), ("State", PdfValue.Array(N("OFF"), PdfValue.Reference(groups[0]))))));
        Assert.Equal("Layers.StateActionPolicy", Error(() => PdfLayers.Flatten(action.CreateSnapshot())));
    }

    [Fact]
    public void CSharpAndVisualBasicConsumersBindAHiddenCanvasWithPrivateResources()
    {
        foreach (var write in new Func<Stream, PdfOperationReport>[]
        {
            ContractExamples.CSharp.ContractExample.WriteLayerPdf,
            ContractExamples.VisualBasic.ContractExample.WriteLayerPdf
        }

        )
        {
            using var output = new MemoryStream();
            Assert.True(write(output).Succeeded);
            Assert.True(output.CanWrite);
            output.Position = 0;
            using var document = Pdf.Open(output);
            var layers = PdfLayers.Inspect(document.CreateSnapshot());
            var group = Assert.Single(layers.Groups);
            Assert.False(PdfLayers.IsVisible(layers.Source, PdfValue.Reference(group.Id)).Visible);
            using var flattened = PdfLayers.Flatten(layers.Source);
            Assert.DoesNotContain(flattened.Pages[0].ReadContent().Operations, x => x.Operation == PdfOperator.DrawObject);
        }
    }

    [Fact]
    public void ConfiguredStatesAndViewerPrintUsageAreExplicitlyDifferent()
    {
        using var source = Fixture("", (edit, root, _, groups) =>
        {
            NavigationTests.Put(edit, groups[0], "Usage", D(("View", D(("ViewState", N("OFF"))))));
            NavigationTests.Put(edit, root, "OCProperties", D(("OCGs", PdfValue.Array(groups.Select(PdfValue.Reference).ToArray())), ("D", D(("AS", PdfValue.Array(D(("Event", N("View")), ("Category", PdfValue.Array(N("View"))), ("OCGs", PdfValue.Array(PdfValue.Reference(groups[0]))))))))));
        });
        var set = PdfLayers.Inspect(source.CreateSnapshot());
        var group = PdfValue.Reference(set.Groups[0].Id);
        Assert.True(PdfLayers.IsVisible(set.Source, group, usage: PdfLayerUsage.Configured).Visible);
        Assert.False(PdfLayers.IsVisible(set.Source, group, usage: PdfLayerUsage.View).Visible);
        Assert.False(PdfLayers.IsVisible(set.Source, group, usage: PdfLayerUsage.Print).Visible);
    }

    [Fact]
    public void LayerOperationsRespectCancellationObjectsAndMalformedMembership()
    {
        using var source = Fixture("");
        var snapshot = source.CreateSnapshot();
        Assert.Throws<OperationCanceledException>(() => PdfLayers.Flatten(snapshot, cancellationToken: new CancellationToken(true)));
        Assert.Equal("Limit.Objects", Error(() => PdfLayers.Inspect(snapshot, new(limits: new(maximumObjects: 1)))));
        Assert.Equal("Layers.MembershipPolicy", Error(() => PdfLayers.IsVisible(snapshot, D(("Type", N("OCMD")), ("P", N("Unknown"))))));
        Assert.Equal("Layers.NameRequired", Error(() => PdfLayers.IsVisible(snapshot, D(("Type", N("OCMD")), ("P", PdfValue.Integer(1))))));
        Assert.Equal("Layers.VisibilityExpression", Error(() => PdfLayers.IsVisible(snapshot, D(("Type", N("OCMD")), ("VE", PdfValue.Array(N("Not"), PdfValue.Null, PdfValue.Null))))));
    }
}
