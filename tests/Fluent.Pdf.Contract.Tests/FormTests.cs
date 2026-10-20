using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class FormTests
{
    private static PdfValue D(params (string, PdfValue)[] values) => NavigationTests.D(values);
    private static PdfValue T(string value) => NavigationTests.T(value);
    private static PdfValue N(string value) => PdfValue.Name(value);
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    internal static PdfGraphicsResource Box() => PdfGraphicsObjects.Form(PdfDrawing.Create(new[] { new PdfContentOperation(PdfOperator.Rectangle, PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(100), PdfValue.Integer(20)), new PdfContentOperation(PdfOperator.Stroke) }), new(0, 0, 100, 20));
    private static PdfFormWidgetDefinition Widget(int page = 0, string? export = null) => new(page, new(20, 30, 100, 20), Box(), export, export == null ? null : Box());
    private static PdfFontResource Font() => PdfFontResource.CreateText(TextTests.Read("business.ttf"), "office –", subset: false);
    internal static PdfDocument Source(Action<PdfTransaction, PdfObjectId, PdfObjectId[], PdfObjectId>? change = null) => NavigationTests.Fixture((edit, root, pages) =>
    {
        var field = edit.Add(D(("T", T("Group")), ("FT", N("Tx")), ("V", T("old"))));
        NavigationTests.Put(edit, root, "AcroForm", D(("Fields", PdfValue.Array(PdfValue.Reference(field)))));
        change?.Invoke(edit, root, pages, field);
    });
    [Theory]
    [InlineData(PdfFormFieldKind.Text)]
    [InlineData(PdfFormFieldKind.CheckBox)]
    [InlineData(PdfFormFieldKind.RadioButton)]
    [InlineData(PdfFormFieldKind.ComboBox)]
    [InlineData(PdfFormFieldKind.ListBox)]
    [InlineData(PdfFormFieldKind.PushButton)]
    [InlineData(PdfFormFieldKind.Signature)]
    public void EveryStandardKindAuthorsOwnedWidgetsAndUsableAppearances(PdfFormFieldKind kind)
    {
        bool state = kind == PdfFormFieldKind.CheckBox || kind == PdfFormFieldKind.RadioButton;
        bool variable = kind == PdfFormFieldKind.Text || kind == PdfFormFieldKind.ComboBox || kind == PdfFormFieldKind.ListBox;
        using var blank = Pdf.Create().AddPage();
        using var document = PdfForms.Add(blank.CreateSnapshot(), new("Customer.Անուն", kind, new[] { Widget(export: state ? "Yes" : null) }, value: state ? N("Yes") : variable ? T("office –") : null, choices: variable && kind != PdfFormFieldKind.Text ? new[] { new PdfFormChoice("office –") } : null, editingFont: variable ? Font() : null));
        var set = PdfForms.Inspect(document.CreateSnapshot());
        var field = Assert.Single(set.Fields);
        var widget = Assert.Single(field.Widgets);
        Assert.Equal(kind, field.Kind);
        Assert.Equal("Customer.Անուն", field.Name);
        Assert.Equal(0, widget.PageIndex);
        var page = document.Pages[0].Source!;
        Assert.Equal(widget.Id, Assert.Single(page.Dictionary.Entries["Annots"].Items).ReferenceId);
        Assert.False(set.Dictionary.Entries["NeedAppearances"].BooleanValue);
        if (state)
            Assert.Equal("Yes", widget.Definition.Entries["AS"].NameValue);
        if (kind == PdfFormFieldKind.Signature)
            Assert.Equal("1", set.Dictionary.Entries["SigFlags"].NumberText);
        using var output = new MemoryStream();
        document.Save(output);
        output.Position = 0;
        using var reopened = Pdf.Open(output);
        Assert.Equal(kind, Assert.Single(PdfForms.Inspect(reopened.CreateSnapshot()).Fields).Kind);
    }

    [Fact]
    public void UnicodeShapedAppearanceEmbedsFontsAndRejectsOverflow()
    {
        var font = TextTests.Read("business.ttf");
        var shaped = PdfText.Shape("office –", new(font), new(new PdfSimpleTextShaper()));
        var appearance = PdfForms.CreateTextAppearance(shaped, new(0, 0, 160, 40), 12);
        using var blank = Pdf.Create().AddPage();
        using var document = PdfForms.Add(blank.CreateSnapshot(), new("Text", PdfFormFieldKind.Text, new[] { new PdfFormWidgetDefinition(0, new(20, 50, 160, 40), appearance) }, T("office –"), editingFont: Font()));
        var widget = Assert.Single(Assert.Single(PdfForms.Inspect(document.CreateSnapshot()).Fields).Widgets);
        var ap = GraphicsTests.Resolve(document.SourceDocument!, widget.Definition.Entries["AP"].Entries["N"]);
        Assert.NotEmpty(ap.Entries["Resources"].Entries["Font"].Entries);
        Assert.Equal("Forms.TextOverflow", Error(() => PdfForms.CreateTextAppearance(shaped, new(0, 0, 1, 1), 12)));
    }

    [Fact]
    public void RadioStateSelectionAndAdditionalWidgetsShareOneLogicalValue()
    {
        using var blank = Pdf.Create().AddPage().AddPage();
        using var first = PdfForms.Add(blank.CreateSnapshot(), new("Radio", PdfFormFieldKind.RadioButton, new[] { Widget(export: "A"), Widget(1, "B") }, N("B")));
        var field = Assert.Single(PdfForms.Inspect(first.CreateSnapshot()).Fields);
        Assert.Equal(new[] { "Off", "B" }, field.Widgets.Select(w => w.Definition.Entries["AS"].NameValue));
        using var more = PdfForms.AddWidget(field, Widget(export: "C"));
        Assert.Equal(3, Assert.Single(PdfForms.Inspect(more.CreateSnapshot()).Fields).Widgets.Count);
        Assert.Equal("Forms.RadioExports", Error(() => PdfForms.AddWidget(field, Widget(export: "A"))));
        Assert.Equal("Forms.DuplicateName", Error(() => PdfForms.Add(first.CreateSnapshot(), new("Radio", PdfFormFieldKind.PushButton, new[] { Widget() }))));
        Assert.Equal("Forms.TerminalNamePrefix", Error(() => PdfForms.Add(first.CreateSnapshot(), new("Radio.Child", PdfFormFieldKind.PushButton, new[] { Widget() }))));
    }

    [Fact]
    public void SortedMultiChoiceValuesAndIndicesFollowOptionOrder()
    {
        using var blank = Pdf.Create().AddPage();
        using var document = PdfForms.Add(blank.CreateSnapshot(), new("Choices", PdfFormFieldKind.ListBox, new[] { Widget() }, PdfValue.Array(T("c"), T("a")), flags: PdfFormFieldFlags.Sort | PdfFormFieldFlags.MultiSelect, choices: new[] { new PdfFormChoice("c", "Zulu"), new PdfFormChoice("b", "Middle"), new PdfFormChoice("a", "Alpha") }, editingFont: Font()));
        var field = Assert.Single(PdfForms.Inspect(document.CreateSnapshot()).Fields);
        Assert.Equal(new[] { "0", "2" }, field.Inherited.Entries["I"].Items.Select(v => v.NumberText));
        Assert.Equal(new[] { "a", "c" }, field.Value.Items.Select(v => Encoding.BigEndianUnicode.GetString(v.StringBytes.Skip(2).ToArray())));
    }

    [Fact]
    public void SameNameUnnamedTerminalsRequireACommonNamedAncestorAndConsistentValues()
    {
        using var valid = Source((edit, _, _, parent) =>
        {
            var a = edit.Add(D(("Parent", PdfValue.Reference(parent))));
            var b = edit.Add(D(("Parent", PdfValue.Reference(parent))));
            NavigationTests.Put(edit, parent, "Kids", PdfValue.Array(PdfValue.Reference(a), PdfValue.Reference(b)));
        });
        var field = Assert.Single(PdfForms.Inspect(valid.CreateSnapshot()).Fields);
        Assert.Equal(2, field.Representations.Count);
        using var renamed = PdfForms.Rename(field, "Renamed");
        Assert.Equal("Renamed", Assert.Single(PdfForms.Inspect(renamed.CreateSnapshot()).Fields).Name);
        using var bad = Source((edit, _, _, parent) =>
        {
            var a = edit.Add(D(("Parent", PdfValue.Reference(parent))));
            var b = edit.Add(D(("Parent", PdfValue.Reference(parent)), ("V", T("different"))));
            NavigationTests.Put(edit, parent, "Kids", PdfValue.Array(PdfValue.Reference(a), PdfValue.Reference(b)));
        });
        Assert.Equal("Forms.DuplicateName", Error(() => PdfForms.Inspect(bad.CreateSnapshot())));
    }

    [Theory]
    [InlineData("parent", "Forms.Parent")]
    [InlineData("cycle", "Forms.FieldCycle")]
    [InlineData("siblings", "Forms.DuplicateName")]
    public void InvalidFieldOwnershipCyclesAndNameCollisionsReject(string mutation, string code)
    {
        using var document = Source((edit, root, _, field) =>
        {
            if (mutation == "parent")
                NavigationTests.Put(edit, field, "Parent", PdfValue.Reference(root));
            if (mutation == "cycle")
                NavigationTests.Put(edit, field, "Kids", PdfValue.Array(PdfValue.Reference(field)));
            if (mutation == "siblings")
            {
                var other = edit.Add(D(("T", T("Group")), ("FT", N("Tx"))));
                NavigationTests.Put(edit, root, "AcroForm", D(("Fields", PdfValue.Array(PdfValue.Reference(field), PdfValue.Reference(other)))));
            }
        });
        Assert.Equal(code, Error(() => PdfForms.Inspect(document.CreateSnapshot())));
    }

    [Fact]
    public void CommonFlagsAndWidgetEditsPreserveTabAndTaggedObjectIdentities()
    {
        using var source = Source((edit, root, pages, field) =>
        {
            var widget = edit.Add(D(("Subtype", N("Widget")), ("Parent", PdfValue.Reference(field)), ("P", PdfValue.Reference(pages[0])), ("Rect", PdfGraphicsNumbers.Array(20, 30, 120, 50)), ("StructParent", PdfValue.Integer(8))));
            NavigationTests.Put(edit, field, "Kids", PdfValue.Array(PdfValue.Reference(widget)));
            NavigationTests.Put(edit, field, "Ff", PdfValue.Integer(4096));
            NavigationTests.Put(edit, pages[0], "Annots", PdfValue.Array(PdfValue.Reference(widget)));
            NavigationTests.Put(edit, pages[0], "Tabs", N("S"));
            var element = edit.Add(D(("Type", N("StructElem")), ("S", N("Form")), ("K", D(("Type", N("OBJR")), ("Obj", PdfValue.Reference(widget)), ("Pg", PdfValue.Reference(pages[0]))))));
            NavigationTests.Put(edit, root, "StructTreeRoot", D(("K", PdfValue.Reference(element)), ("ParentTree", D(("Nums", PdfValue.Array(PdfValue.Integer(8), PdfValue.Reference(element)))))));
        });
        var original = Assert.Single(PdfForms.Inspect(source.CreateSnapshot()).Fields);
        var id = Assert.Single(original.Widgets).Id;
        using var flags = PdfForms.SetCommonFlags(original, PdfFormFieldFlags.Required);
        var current = Assert.Single(PdfForms.Inspect(flags.CreateSnapshot()).Fields);
        Assert.Equal(4098u, current.Flags);
        using var edited = PdfForms.EditWidget(current, id, Widget());
        var widget = Assert.Single(Assert.Single(PdfForms.Inspect(edited.CreateSnapshot()).Fields).Widgets);
        Assert.Equal(id, widget.Id);
        Assert.Equal("8", widget.Definition.Entries["StructParent"].NumberText);
        Assert.Equal("S", edited.Pages[0].Source!.Dictionary.Entries["Tabs"].NameValue);
        using var tabs = PdfForms.SetTabOrder(edited.CreateSnapshot(), 0, PdfFormTabOrder.Columns);
        Assert.Equal("C", tabs.Pages[0].Source!.Dictionary.Entries["Tabs"].NameValue);
        Assert.Equal("Forms.StructureCreationProfile", Error(() => PdfForms.AddWidget(current, Widget())));
    }

    [Fact]
    public void CalculationActionsRemainPassiveAndRenamingRequiresAnExplicitOpaqueReferenceDecision()
    {
        using var source = Source((edit, root, _, field) =>
        {
            NavigationTests.Put(edit, field, "AA", D(("C", D(("S", N("JavaScript")), ("JS", T("throw 'MUST NOT EXECUTE';"))))));
            NavigationTests.Put(edit, root, "AcroForm", D(("Fields", PdfValue.Array(PdfValue.Reference(field))), ("CO", PdfValue.Array(PdfValue.Reference(field)))));
        });
        var field = Assert.Single(PdfForms.Inspect(source.CreateSnapshot()).Fields);
        Assert.Equal("Forms.OpaqueNameReferences", Error(() => PdfForms.Rename(field, "Other")));
        using var renamed = PdfForms.Rename(field, "Other", retainOpaqueNameReferences: true);
        Assert.Contains(renamed.LastOperationReport!.Diagnostics, d => d.Code == "Forms.OpaqueNameReferencesRetained");
        Assert.Equal("Forms.CalculationOrder", Error(() => PdfForms.SetCalculationOrder(source.CreateSnapshot(), Array.Empty<PdfObjectId>())));
        using var ordered = PdfForms.SetCalculationOrder(source.CreateSnapshot(), field.Representations);
        Assert.Equal(field.Representations[0], Assert.Single(PdfForms.Inspect(ordered.CreateSnapshot()).Dictionary.Entries["CO"].Items).ReferenceId);
    }

    [Fact]
    public void XfaInspectionIsPassiveAndMutationsRejectUnsynchronizedData()
    {
        using var source = Source((edit, root, _, field) => NavigationTests.Put(edit, root, "AcroForm", D(("Fields", PdfValue.Array(PdfValue.Reference(field))), ("XFA", T("opaque")))));
        var set = PdfForms.Inspect(source.CreateSnapshot());
        Assert.Contains(set.OperationReport.Diagnostics, d => d.Code == "Forms.XfaPresent");
        Assert.Equal("Forms.XfaProfile", Error(() => PdfForms.SetCommonFlags(set.Fields[0], PdfFormFieldFlags.Required)));
    }

    [Fact]
    public void CalculationAuthoringIsPolicyCheckedAndCSharpVbConsumersAgree()
    {
        using var source = Source();
        var field = Assert.Single(PdfForms.Inspect(source.CreateSnapshot()).Fields);
        var action = PdfAction.Advanced(D(("S", N("JavaScript")), ("JS", T("event.value = 2;"))));
        Assert.Equal("Navigation.ActionPolicy", Error(() => PdfForms.SetCalculationAction(field, action)));
        using var added = PdfForms.SetCalculationAction(field, action, new(allowJavaScript: true));
        var current = Assert.Single(PdfForms.Inspect(added.CreateSnapshot()).Fields);
        Assert.Single(PdfForms.Inspect(added.CreateSnapshot()).Dictionary.Entries["CO"].Items);
        using var removed = PdfForms.SetCalculationAction(current, null);
        Assert.False(PdfForms.Inspect(removed.CreateSnapshot()).Dictionary.Entries.ContainsKey("CO"));
        using var cs = new MemoryStream();
        using var vb = new MemoryStream();
        var font = TextTests.Read("business.ttf");
        ContractExamples.CSharp.ContractExample.WriteFormPdf(cs, font);
        ContractExamples.VisualBasic.ContractExample.WriteFormPdf(vb, font);
        cs.Position = 0;
        vb.Position = 0;
        using var a = Pdf.Open(cs);
        using var b = Pdf.Open(vb);
        Assert.Equal("Customer.Name", Assert.Single(PdfForms.Inspect(a.CreateSnapshot()).Fields).Name);
        Assert.Equal("Customer.Name", Assert.Single(PdfForms.Inspect(b.CreateSnapshot()).Fields).Name);
    }

    [Fact]
    public void AddingAnAppearanceDoesNotDisableOtherWidgetsRequestedRegeneration()
    {
        using var source = Source((edit, root, _, field) => NavigationTests.Put(edit, root, "AcroForm", D(("Fields", PdfValue.Array(PdfValue.Reference(field))), ("NeedAppearances", PdfValue.Boolean(true)))));
        using var added = PdfForms.Add(source.CreateSnapshot(), new("New", PdfFormFieldKind.PushButton, new[] { Widget() }));
        Assert.True(PdfForms.Inspect(added.CreateSnapshot()).Dictionary.Entries["NeedAppearances"].BooleanValue);
    }

    [Fact]
    public void CalculationOrderCannotAuthorReferencesToFieldsWithoutCalculations()
    {
        using var source = Source();
        var field = Assert.Single(PdfForms.Inspect(source.CreateSnapshot()).Fields);
        Assert.Equal("Forms.CalculationOrder", Error(() => PdfForms.SetCalculationOrder(source.CreateSnapshot(), field.Representations)));
        Assert.False(PdfForms.Inspect(source.CreateSnapshot()).Dictionary.Entries.ContainsKey("CO"));
    }

    [Fact]
    public void MalformedWidgetAppearanceHasATypedDiagnostic()
    {
        using var source = Source((edit, _, pages, field) =>
        {
            var widget = edit.Add(D(("Subtype", N("Widget")), ("Parent", PdfValue.Reference(field)), ("Rect", PdfGraphicsNumbers.Array(20, 30, 120, 50)), ("AP", PdfValue.Integer(1))));
            NavigationTests.Put(edit, field, "Kids", PdfValue.Array(PdfValue.Reference(widget)));
            NavigationTests.Put(edit, pages[0], "Annots", PdfValue.Array(PdfValue.Reference(widget)));
        });
        Assert.Equal("Forms.AppearanceDictionary", Error(() => PdfForms.Inspect(source.CreateSnapshot())));
    }

    [Fact]
    public void InvalidFlagsValuesAndCancellationLeaveSourceUnchanged()
    {
        using var blank = Pdf.Create().AddPage();
        var source = blank.CreateSnapshot();
        Assert.Equal("Forms.CombFlags", Error(() => PdfForms.Add(source, new("Bad", PdfFormFieldKind.Text, new[] { Widget() }, flags: PdfFormFieldFlags.Comb, editingFont: Font()))));
        Assert.Equal("Forms.ButtonValue", Error(() => PdfForms.Add(source, new("Bad", PdfFormFieldKind.CheckBox, new[] { Widget(export: "Yes") }, N("Unknown")))));
        Assert.Equal("Forms.MaxLen", Error(() => PdfForms.Add(source, new("Bad", PdfFormFieldKind.Text, new[] { Widget() }, T("Too long"), maximumLength: 2, editingFont: Font()))));
        Assert.Throws<OperationCanceledException>(() => PdfForms.Add(source, new("Bad", PdfFormFieldKind.PushButton, new[] { Widget() }), cancellationToken: new CancellationToken(true)));
        using var existing = Source();
        Assert.Equal("Limit.Objects", Error(() => PdfForms.Inspect(existing.CreateSnapshot(), new(limits: new(maximumObjects: 1)))));
        Assert.Empty(PdfForms.Inspect(source).Fields);
    }
}
