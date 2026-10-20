using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class FormWorkflowTests
{
    private static PdfValue D(params (string, PdfValue)[] pairs) => NavigationTests.D(pairs);
    private static PdfValue T(string text) => NavigationTests.T(text);
    private static PdfValue N(string text) => PdfValue.Name(text);
    private static string Text(PdfValue value) => Encoding.BigEndianUnicode.GetString(value.StringBytes.Skip(2).ToArray());
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    private static PdfFormTextStyle Style(string? mask = null) => new(new(TextTests.Read("business.ttf")), new(new PdfSimpleTextShaper()), passwordMask: mask);
    private static PdfFormWidgetDefinition Widget(int page, double y, string? export = null) => new(page, new(20, y, 160, 40), FormTests.Box(), export, export == null ? null : FormTests.Box());
    internal static PdfDocument Source()
    {
        using var blank = Pdf.Create().AddPage().AddPage();
        var font = TextTests.Read("business.ttf");
        var editing = PdfFontResource.CreateText(font, "officec –", subset: false);
        var shaped = PdfText.Shape("office", new(font), new(new PdfSimpleTextShaper()));
        var appearance = PdfForms.CreateTextAppearance(shaped, new(0, 0, 160, 40), 12);
        using var text = PdfForms.Add(blank.CreateSnapshot(), new("Text", PdfFormFieldKind.Text, new[] { new PdfFormWidgetDefinition(0, new(20, 300, 160, 40), appearance) }, T("office"), defaultValue: T("office"), editingFont: editing));
        using var check = PdfForms.Add(text.CreateSnapshot(), new("Agree", PdfFormFieldKind.CheckBox, new[] { Widget(0, 240, "Yes Exact") }, N("Off")));
        using var radio = PdfForms.Add(check.CreateSnapshot(), new("Radio", PdfFormFieldKind.RadioButton, new[] { Widget(0, 180, "A"), Widget(1, 180, "B") }, N("A")));
        return PdfForms.Add(radio.CreateSnapshot(), new("Sign", PdfFormFieldKind.Signature, new[] { Widget(1, 60) }));
    }

    [Fact]
    public void FillingGeneratesPrivateTextAndSynchronizesExactButtonStatesAcrossPages()
    {
        using var source = Source();
        var data = PdfFormData.Create(new[] { new PdfFormDataField("Text", T("officec –")), new PdfFormDataField("Agree", T("Yes Exact")), new PdfFormDataField("Radio", T("B")) });
        using var filled = PdfForms.FillData(source.CreateSnapshot(), data, Style());
        var fields = PdfForms.Inspect(filled.CreateSnapshot()).Fields.ToDictionary(x => x.Name);
        Assert.Equal("officec –", Text(fields["Text"].Value));
        Assert.Equal("Yes Exact", fields["Agree"].Value.NameValue);
        Assert.Equal(new[] { "Off", "B" }, fields["Radio"].Widgets.Select(w => w.Definition.Entries["AS"].NameValue));
        using var flat = PdfForms.Flatten(filled.CreateSnapshot(), new[] { "Text" });
        Assert.Equal(3, PdfForms.Inspect(flat.CreateSnapshot()).Fields.Count);
        Assert.Contains("officec –", PdfTextExtraction.ReadPage(flat.CreateSnapshot(), 0).Text);
        Assert.Equal("office", Text(PdfForms.Inspect(source.CreateSnapshot()).Fields.Single(f => f.Name == "Text").Value));
    }

    [Fact]
    public void StandardXfdfValuesRestoreButtonNamesUsingTargetFieldKinds()
    {
        using var source = Source();
        var values = PdfFormData.Create(new[] { new PdfFormDataField("Agree", N("Yes Exact")), new PdfFormDataField("Radio", N("B")) });
        using var bytes = new MemoryStream();
        values.Write(bytes, PdfFormDataFormat.Xfdf);
        bytes.Position = 0;
        var read = PdfFormData.Read(bytes, PdfFormDataFormat.Xfdf);
        Assert.All(read.Fields, f => Assert.Equal(PdfValueKind.String, f.Value.Kind));
        using var filled = PdfForms.FillData(source.CreateSnapshot(), read);
        Assert.Equal("B", PdfForms.Inspect(filled.CreateSnapshot()).Fields.Single(f => f.Name == "Radio").Value.NameValue);
        Assert.Equal("Forms.ButtonValue", Error(() => PdfForms.Fill(source.CreateSnapshot(), new[] { new PdfFormUpdate("Agree", T("yes exact")) })));
    }

    [Fact]
    public void MissingAppearancesForeignWidgetsDuplicateNamesAndLateErrorsRollbackTheWholeBatch()
    {
        using var source = Source();
        var snapshot = source.CreateSnapshot();
        Assert.Equal("Forms.AppearanceRequired", Error(() => PdfForms.Fill(snapshot, new[] { new PdfFormUpdate("Text", T("new")) })));
        Assert.Equal("Forms.AppearanceOwnership", Error(() => PdfForms.Fill(snapshot, new[] { new PdfFormUpdate("Text", T("new"), appearances: new Dictionary<PdfObjectId, PdfGraphicsResource> { { new(999999), FormTests.Box() } }) })));
        Assert.Equal("Forms.DuplicateUpdate", Error(() => PdfForms.Fill(snapshot, new[] { new PdfFormUpdate("Agree", N("Yes Exact")), new PdfFormUpdate("Agree", N("Off")) })));
        Assert.Equal("Forms.UnknownField", Error(() => PdfForms.Fill(snapshot, new[] { new PdfFormUpdate("Agree", N("Yes Exact")), new PdfFormUpdate("Missing", T("x")) })));
        Assert.Equal("Off", PdfForms.Inspect(snapshot).Fields.Single(f => f.Name == "Agree").Value.NameValue);
    }

    [Fact]
    public void MultiChoiceFillKeepsSortedIndicesAndExplicitUsableAppearances()
    {
        using var blank = Pdf.Create().AddPage();
        var font = PdfFontResource.CreateText(TextTests.Read("business.ttf"), "office", subset: false);
        using var source = PdfForms.Add(blank.CreateSnapshot(), new("Choice", PdfFormFieldKind.ListBox, new[] { Widget(0, 100) }, flags: PdfFormFieldFlags.MultiSelect, choices: new[] { new PdfFormChoice("a"), new PdfFormChoice("b") }, editingFont: font));
        var field = Assert.Single(PdfForms.Inspect(source.CreateSnapshot()).Fields);
        var id = Assert.Single(field.Widgets).Id;
        using var filled = PdfForms.Fill(field.Source, new[] { new PdfFormUpdate("Choice", PdfValue.Array(T("b"), T("a")), appearances: new Dictionary<PdfObjectId, PdfGraphicsResource> { { id, FormTests.Box() } }) });
        var current = Assert.Single(PdfForms.Inspect(filled.CreateSnapshot()).Fields);
        Assert.Equal(new[] { "a", "b" }, current.Value.Items.Select(Text));
        Assert.Equal(new[] { "0", "1" }, current.Inherited.Entries["I"].Items.Select(v => v.NumberText));
    }

    [Fact]
    public void NoExportAndNonDataFieldsHaveExplicitExtractionBehavior()
    {
        using var source = Source();
        var field = PdfForms.Inspect(source.CreateSnapshot()).Fields.Single(f => f.Name == "Text");
        using var privateValue = PdfForms.SetCommonFlags(field, PdfFormFieldFlags.NoExport);
        Assert.Equal(new[] { "Agree", "Radio" }, PdfForms.ReadValues(privateValue.CreateSnapshot()).Fields.Select(f => f.Name));
        Assert.Equal(3, PdfForms.ReadValues(privateValue.CreateSnapshot(), includeNoExport: true).Fields.Count);
    }

    [Fact]
    public void SameNameRepresentationsAndNamedOwnerUpdateTogetherIncludingNullClears()
    {
        PdfObjectId parent = default;
        using var source = FormTests.Source((edit, _, _, owner) =>
        {
            parent = owner;
            var a = edit.Add(D(("Parent", PdfValue.Reference(owner))));
            var b = edit.Add(D(("Parent", PdfValue.Reference(owner))));
            NavigationTests.Put(edit, owner, "Kids", PdfValue.Array(PdfValue.Reference(a), PdfValue.Reference(b)));
        });
        using var filled = PdfForms.Fill(source.CreateSnapshot(), new[] { new PdfFormUpdate("Group", T("new")) });
        Assert.Equal("new", Text(Assert.Single(PdfForms.Inspect(filled.CreateSnapshot()).Fields).Value));
        Assert.Equal("new", Text(filled.SourceDocument!.Resolve(parent).Entries["V"]));
        using var cleared = PdfForms.Fill(filled.CreateSnapshot(), new[] { new PdfFormUpdate("Group", PdfValue.Null) });
        Assert.Equal(PdfValueKind.Null, Assert.Single(PdfForms.Inspect(cleared.CreateSnapshot()).Fields).Value.Kind);
    }

    [Fact]
    public void NullDictionaryEntriesDoNotShadowInheritedValuesAndUnsafeInheritedClearsReject()
    {
        using var source = FormTests.Source((edit, _, _, parent) =>
        {
            var child = edit.Add(D(("T", T("Child")), ("Parent", PdfValue.Reference(parent)), ("V", PdfValue.Null), ("FT", PdfValue.Null)));
            NavigationTests.Put(edit, parent, "Kids", PdfValue.Array(PdfValue.Reference(child)));
        });
        var field = Assert.Single(PdfForms.Inspect(source.CreateSnapshot()).Fields);
        Assert.Equal("old", Text(field.Value));
        Assert.Equal(PdfFormFieldKind.Text, field.Kind);
        Assert.Equal("Forms.InheritedClearProfile", Error(() => PdfForms.Fill(field.Source, new[] { new PdfFormUpdate(field.Name, PdfValue.Null) })));
        using var empty = PdfForms.Fill(field.Source, new[] { new PdfFormUpdate(field.Name, T("")) });
        Assert.Equal("", Text(Assert.Single(PdfForms.Inspect(empty.CreateSnapshot()).Fields).Value));
        Assert.Equal("Forms.InheritedValueProfile", Error(() => PdfForms.Add(field.Source, new("Group.New", PdfFormFieldKind.PushButton, new[] { Widget(0, 20) }))));
    }

    [Fact]
    public void CopyingRebindsEditingFontsAndKeepsSourceOwnershipIndependent()
    {
        using var source = Source();
        using var target = Source();
        var field = PdfForms.Inspect(source.CreateSnapshot()).Fields.Single(f => f.Name == "Text");
        using var copied = PdfForms.CopyField(field, target.CreateSnapshot(), "Copied", new Dictionary<int, int> { { 0, 1 } });
        source.Dispose();
        target.Dispose();
        var copy = PdfForms.Inspect(copied.CreateSnapshot()).Fields.Single(f => f.Name == "Copied");
        Assert.Equal(1, Assert.Single(copy.Widgets).PageIndex);
        Assert.Equal("office", Text(copy.Value));
        var da = Encoding.ASCII.GetString(copy.Inherited.Entries["DA"].StringBytes.ToArray());
        Assert.Contains("CopiedFont", da);
        using var flat = PdfForms.Flatten(copy.Source, new[] { "Copied" });
        Assert.Contains("office", PdfTextExtraction.ReadPage(flat.CreateSnapshot(), 1).Text);
        Assert.Equal("Forms.DuplicateName", Error(() => PdfForms.CopyField(copy, copy.Source, "Copied", new Dictionary<int, int> { { 1, 0 } })));
    }

    [Fact]
    public void WidgetDecorationPreservesValueStateAndAllOwnedIdentities()
    {
        using var source = Source();
        var field = PdfForms.Inspect(source.CreateSnapshot()).Fields.Single(f => f.Name == "Agree");
        var widget = Assert.Single(field.Widgets);
        using var decorated = PdfForms.DecorateWidget(field, widget.Id, FormTests.Box());
        var current = PdfForms.Inspect(decorated.CreateSnapshot()).Fields.Single(f => f.Name == "Agree");
        var changed = Assert.Single(current.Widgets);
        Assert.Equal(widget.Id, changed.Id);
        Assert.Equal("Off", current.Value.NameValue);
        Assert.Equal("Off", changed.Definition.Entries["AS"].NameValue);
        var normal = changed.Definition.Entries["AP"].Entries["N"];
        foreach (var state in normal.Entries.Values)
        {
            var form = GraphicsTests.Resolve(decorated.SourceDocument!, state);
            Assert.Equal(2, form.Entries["Resources"].Entries["XObject"].Entries.Count);
        }
    }

    [Fact]
    public void SelectiveFlatteningKeepsRemainingFieldsWidgetsAndUnsignedSignatures()
    {
        using var source = Source();
        var before = PdfForms.Inspect(source.CreateSnapshot());
        using var flattened = PdfForms.Flatten(before.Source, new[] { "Agree", "Radio" });
        var after = PdfForms.Inspect(flattened.CreateSnapshot());
        Assert.Equal(new[] { "Text", "Sign" }, after.Fields.Select(f => f.Name));
        Assert.Equal(before.Fields.Single(f => f.Name == "Sign").Representations, after.Fields.Single(f => f.Name == "Sign").Representations);
        Assert.Single(flattened.Pages[0].Source!.Dictionary.Entries["Annots"].Items);
        Assert.Single(flattened.Pages[1].Source!.Dictionary.Entries["Annots"].Items);
        using var all = PdfForms.Flatten(after.Source);
        Assert.Empty(PdfForms.Inspect(all.CreateSnapshot()).Fields);
        Assert.Equal(PdfValueKind.Null, PdfForms.Inspect(all.CreateSnapshot()).Dictionary.Kind);
    }

    [Fact]
    public void IncrementalFlatteningRetainsOriginalSignatureBytesWithoutClaimingCryptographicValidity()
    {
        using var source = Source();
        var field = PdfForms.Inspect(source.CreateSnapshot()).Fields.Single(f => f.Name == "Sign");
        using var edit = new PdfTransaction(source.SourceDocument!);
        var value = edit.Add(D(("Type", N("Sig")), ("ByteRange", PdfGraphicsNumbers.Array(0, 0, 0, 0)), ("Contents", PdfValue.String())));
        NavigationTests.Put(edit, field.Representations[0], "V", PdfValue.Reference(value));
        using var original = new MemoryStream();
        edit.Save(original, new(PdfWriteMode.Incremental));
        var prefix = original.ToArray();
        original.Position = 0;
        using var signed = Pdf.Open(original);
        var snapshot = signed.CreateSnapshot();
        Assert.Equal("Writer.SignatureRewrite", Error(() => PdfForms.Flatten(snapshot, new[] { "Text" })));
        using var flattened = PdfForms.Flatten(snapshot, new[] { "Text" }, writeMode: PdfWriteMode.Incremental);
        using var output = new MemoryStream();
        flattened.Save(output, writeOptions: new(PdfWriteMode.Incremental));
        Assert.True(output.ToArray().Take(prefix.Length).SequenceEqual(prefix));
        var signature = PdfForms.Inspect(flattened.CreateSnapshot()).Fields.Single(f => f.Name == "Sign");
        Assert.Equal(PdfValueKind.Dictionary, signature.Value.Kind);
        Assert.True(flattened.SourceDocument!.Revisions.Count >= 3);
        Assert.Equal("Forms.SignedFieldFlattening", Error(() => PdfForms.Flatten(snapshot, new[] { "Sign" }, writeMode: PdfWriteMode.Incremental)));
    }

    [Fact]
    public void UnknownSurvivingOwnerReferencesBlockFieldRemoval()
    {
        using var source = FormTests.Source((edit, root, _, field) => NavigationTests.Put(edit, root, "CustomOwner", PdfValue.Reference(field)));
        Assert.Equal("Forms.FieldReferenceProfile", Error(() => PdfForms.Flatten(source.CreateSnapshot())));
        Assert.Single(PdfForms.Inspect(source.CreateSnapshot()).Fields);
    }

    [Fact]
    public void ViewerRegenerationAndResourceCancellationRemainExplicit()
    {
        using var source = FormTests.Source((edit, root, _, field) => NavigationTests.Put(edit, root, "AcroForm", D(("Fields", PdfValue.Array(PdfValue.Reference(field))), ("NeedAppearances", PdfValue.Boolean(true)))));
        Assert.Equal("Forms.RegenerationProfile", Error(() => PdfForms.Fill(source.CreateSnapshot(), new[] { new PdfFormUpdate("Group", T("new")) })));
        using var allowed = PdfForms.Fill(source.CreateSnapshot(), new[] { new PdfFormUpdate("Group", T("new")) }, retainSourceRegenerationRequest: true);
        Assert.Contains(allowed.LastOperationReport!.Diagnostics, d => d.Code == "Forms.RegenerationRequestRetained");
        Assert.Throws<OperationCanceledException>(() => PdfForms.Flatten(source.CreateSnapshot(), cancellationToken: new CancellationToken(true)));
        Assert.Equal("Limit.Objects", Error(() => PdfForms.Flatten(source.CreateSnapshot(), policy: new(limits: new(maximumObjects: 1)))));
    }

    [Fact]
    public void CSharpAndVbConsumersFillAndFlattenUsingOnlyPublicContracts()
    {
        using var source = Source();
        using var input = new MemoryStream();
        source.Save(input);
        var data = PdfFormData.Create(new[] { new PdfFormDataField("Text", T("officec")) });
        var font = TextTests.Read("business.ttf");
        using var cs = new MemoryStream();
        using var vb = new MemoryStream();
        input.Position = 0;
        ContractExamples.CSharp.ContractExample.FillAndFlattenForm(input, cs, data, font);
        Assert.True(input.CanRead);
        input.Position = 0;
        ContractExamples.VisualBasic.ContractExample.FillAndFlattenForm(input, vb, data, font);
        Assert.True(input.CanRead);
        cs.Position = 0;
        vb.Position = 0;
        using var a = Pdf.Open(cs);
        using var b = Pdf.Open(vb);
        Assert.Equal(3, PdfForms.Inspect(a.CreateSnapshot()).Fields.Count);
        Assert.Equal(3, PdfForms.Inspect(b.CreateSnapshot()).Fields.Count);
        Assert.Contains("officec", PdfTextExtraction.ReadPage(a.CreateSnapshot(), 0).Text);
        Assert.Contains("officec", PdfTextExtraction.ReadPage(b.CreateSnapshot(), 0).Text);
    }

    [Fact]
    public void ChoiceIndicesAndAuthoringFlagsFollowTheirFieldTypeRules()
    {
        using var blank = Pdf.Create().AddPage();
        var font = PdfFontResource.CreateText(TextTests.Read("business.ttf"), "office", subset: false);
        using var choice = PdfForms.Add(blank.CreateSnapshot(), new("Choice", PdfFormFieldKind.ComboBox, new[] { Widget(0, 100) }, T("a"), choices: new[] { new PdfFormChoice("a") }, editingFont: font));
        Assert.False(Assert.Single(PdfForms.Inspect(choice.CreateSnapshot()).Fields).Inherited.Entries.ContainsKey("I"));
        Assert.Equal("Forms.ChoiceFlags", Error(() => PdfForms.Add(blank.CreateSnapshot(), new("Bad", PdfFormFieldKind.ComboBox, new[] { Widget(0, 100) }, flags: PdfFormFieldFlags.DoNotSpellCheck, editingFont: font))));
        Assert.Equal("Forms.MultilineValue", Error(() => PdfForms.Add(blank.CreateSnapshot(), new("Bad", PdfFormFieldKind.Text, new[] { Widget(0, 100) }, T("line\nbreak"), editingFont: font))));
    }

    [Theory]
    [InlineData("Ch")]
    [InlineData("Btn")]
    public void NoninheritableChoiceAndRichTextPropertiesStayAtTheirOwningNode(string parentType)
    {
        using var source = FormTests.Source((edit, _, _, parent) =>
        {
            NavigationTests.Put(edit, parent, "FT", N(parentType));
            NavigationTests.Put(edit, parent, "V", PdfValue.Null);
            NavigationTests.Put(edit, parent, "Opt", PdfValue.Array(T("Parent-only")));
            NavigationTests.Put(edit, parent, "I", PdfValue.Array(PdfValue.Integer(0)));
            NavigationTests.Put(edit, parent, "RV", T("Parent-only rich text"));
            NavigationTests.Put(edit, parent, "DS", T("font-size:12pt"));
            var child = edit.Add(D(("T", T("Child")), ("FT", N("Ch")), ("Parent", PdfValue.Reference(parent))));
            NavigationTests.Put(edit, parent, "Kids", PdfValue.Array(PdfValue.Reference(child)));
        });
        var field = Assert.Single(PdfForms.Inspect(source.CreateSnapshot()).Fields);
        foreach (string key in new[]
        {
            "Opt",
            "I",
            "RV",
            "DS"
        }

        )
            Assert.False(field.Inherited.Entries.ContainsKey(key));
    }

    [Fact]
    public void PasswordGenerationRequiresAnExplicitMaskAndDoesNotPlaceTheValueInAppearanceText()
    {
        using var blank = Pdf.Create().AddPage();
        var font = PdfFontResource.CreateText(TextTests.Read("business.ttf"), "office", subset: false);
        using var source = PdfForms.Add(blank.CreateSnapshot(), new("Password", PdfFormFieldKind.Text, new[] { Widget(0, 100) }, flags: PdfFormFieldFlags.Password, editingFont: font));
        Assert.Equal("Forms.PasswordMaskRequired", Error(() => PdfForms.Fill(source.CreateSnapshot(), new[] { new PdfFormUpdate("Password", T("office"), Style()) })));
        using var filled = PdfForms.Fill(source.CreateSnapshot(), new[] { new PdfFormUpdate("Password", T("office"), Style("•")) });
        Assert.Equal("office", Text(Assert.Single(PdfForms.Inspect(filled.CreateSnapshot()).Fields).Value));
        using var flat = PdfForms.Flatten(filled.CreateSnapshot());
        var text = PdfTextExtraction.ReadPage(flat.CreateSnapshot(), 0).Text;
        Assert.Contains("••••••", text);
        Assert.DoesNotContain("office", text);
    }
}
