using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class ComparisonXfaScriptTests
{
    [Fact]
    public void ComparisonSeparatesTextGeometryMetadataAndEscapesReports()
    {
        using var original = ExtractionTests.Fixture("BT /F1 10 Tf 1 0 0 1 20 100 Tm (Alpha) Tj ET");
        using var changed = ExtractionTests.Fixture("BT /F1 10 Tf 1 0 0 1 30 100 Tm (Bravo) Tj ET", (edit, root, page, font) => edit.Trailer = NavigationTests.D(("Root", PdfValue.Reference(root)), ("Info", NavigationTests.D(("Title", NavigationTests.T("<script>unsafe</script>")), ("ModDate", NavigationTests.T("volatile"))))));
        Assert.Empty(PdfComparison.Compare(original.CreateSnapshot(), original.CreateSnapshot()).Differences);
        var report = PdfComparison.Compare(original.CreateSnapshot(), changed.CreateSnapshot(), new(ignoredInformationKeys: new[] { "ModDate" }));
        Assert.Contains(report.Differences, d => d.Kind == PdfDifferenceKind.Text && d.PageIndex == 0 && d.BeforeObject.HasValue && d.AfterObject.HasValue);
        Assert.Contains(report.Differences, d => d.Kind == PdfDifferenceKind.Geometry);
        Assert.Single(report.Differences, d => d.Kind == PdfDifferenceKind.Metadata);
        using var annotated = report.CreateAnnotatedDocument();
        Assert.Equal(report.Differences.Count, PdfAnnotationSet.Inspect(annotated.CreateSnapshot()).Annotations.Count);
        using var html = new MemoryStream();
        report.WriteReport(html, true);
        string text = Encoding.UTF8.GetString(html.ToArray());
        Assert.Contains("&lt;script&gt;", text);
        Assert.DoesNotContain("<script>", text);
        using var json = new MemoryStream();
        report.WriteReport(json);
        using var parsed = System.Text.Json.JsonDocument.Parse(json.ToArray());
        Assert.Equal(report.Differences.Count, parsed.RootElement.GetProperty("differences").GetArrayLength());
        Assert.True(json.CanWrite);
        Assert.Throws<PdfException>(() => PdfComparison.Compare(original.CreateSnapshot(), changed.CreateSnapshot(), policy: new(limits: new(maximumObjects: 1))));
    }
    internal static PdfDocument XfaFixture(string? template = null, string? datasets = null)
        => NavigationTests.Fixture((edit, root, pages) =>
        {
            PdfValue Packet(string xml)
            {
                using var input = new MemoryStream(Encoding.UTF8.GetBytes(xml));
                return PdfValue.Reference(edit.Add(PdfValues.Stream(NavigationTests.D(), input)));
            }
            var packets = PdfValue.Array(NavigationTests.T("template"), Packet(template ?? "<template xmlns='http://www.xfa.org/schema/xfa-template/3.3/'><subform name='form' layout='position'><field name='name'><ui><textEdit/></ui></field></subform></template>"), NavigationTests.T("datasets"), Packet(datasets ?? "<xfa:datasets xmlns:xfa='http://www.xfa.org/schema/xfa-data/1.0/'><xfa:data><form><name>Արամ</name></form></xfa:data></xfa:datasets>"), NavigationTests.T("config"), Packet("<config>retained</config>"));
            NavigationTests.Put(edit, root, "AcroForm", PdfValue.Reference(edit.Add(NavigationTests.D(("Fields", PdfValue.Array()), ("XFA", packets)))));
        }, 1);
    [Fact]
    public void XfaScalarFillPreservesOtherPacketsAndConversionRequiresExactIdentityCoverage()
    {
        using var source = XfaFixture();
        var original = PdfXfa.Inspect(source.CreateSnapshot());
        Assert.Equal("Արամ", original.Data["form.name"]);
        using var filled = PdfXfa.Fill(source.CreateSnapshot(), new Dictionary<string, string> { { "form.name", "Նոր <value>" } });
        var current = PdfXfa.Inspect(filled.CreateSnapshot());
        Assert.Equal("Նոր <value>", current.Data["form.name"]);
        Assert.Equal(original.Packets[0].Xml, current.Packets[0].Xml);
        Assert.Equal(original.Packets[0].ObjectId, current.Packets[0].ObjectId);
        Assert.Equal(original.Packets[2].Xml, current.Packets[2].Xml);
        var definition = new PdfFormFieldDefinition("form.name", PdfFormFieldKind.Text, new[] { new PdfFormWidgetDefinition(0, new(10, 20, 80, 20), AnnotationTests.Appearance(new(0, 0, 80, 20))) }, editingFont: PdfFontResource.CreateText(TextTests.Read("business.ttf"), "office", subset: false));
        using var converted = PdfXfa.ConvertToAcroForm(filled.CreateSnapshot(), new[] { definition });
        var field = Assert.Single(PdfForms.Inspect(converted.CreateSnapshot()).Fields);
        Assert.Equal("form.name", field.Name);
        PolicyTests.Run(c => Assert.Equal("Նոր <value>", PdfTextStrings.Decode(field.Value, c)));
        Assert.Equal("Արամ", PdfXfa.Inspect(source.CreateSnapshot()).Data["form.name"]);
        Assert.Equal("Xfa.FieldCoverage", Assert.Throws<PdfException>(() => PdfXfa.ConvertToAcroForm(source.CreateSnapshot(), Array.Empty<PdfFormFieldDefinition>())).Diagnostic.Code);
        Assert.Equal("Xfa.DataPath", Assert.Throws<PdfException>(() => PdfXfa.Fill(source.CreateSnapshot(), new Dictionary<string, string> { { "missing", "value" } })).Diagnostic.Code);
    }
    [Fact]
    public void XfaRejectsEntitiesDynamicBehaviorAmbiguousDataAndXmlDepth()
    {
        using var dynamic = XfaFixture("<template><subform name='form' layout='tb'><field name='name'/></subform></template>");
        Assert.Equal("Xfa.DynamicLayout", Assert.Throws<PdfException>(() => PdfXfa.ConvertToAcroForm(dynamic.CreateSnapshot(), Array.Empty<PdfFormFieldDefinition>())).Diagnostic.Code);
        using var entity = XfaFixture(datasets: "<!DOCTYPE data [<!ENTITY x SYSTEM 'file:///not-opened'>]><datasets><data><form><name>&x;</name></form></data></datasets>");
        Assert.Equal("Xfa.Xml", Assert.Throws<PdfException>(() => PdfXfa.Inspect(entity.CreateSnapshot())).Diagnostic.Code);
        using var repeated = XfaFixture(datasets: "<datasets><data><form><name>a</name><name>b</name></form></data></datasets>");
        Assert.Equal("Xfa.DataPath", Assert.Throws<PdfException>(() => PdfXfa.Inspect(repeated.CreateSnapshot())).Diagnostic.Code);
        using var source = XfaFixture();
        Assert.Throws<PdfException>(() => PdfXfa.Inspect(source.CreateSnapshot(), new(limits: new(maximumDepth: 2))));
    }
    [Fact]
    public void ScriptsArePassiveUntilExplicitOptInAndClosedCalculationsHaveNoHostCapabilities()
    {
        using var source = Pdf.Create().AddPage();
        const string code = "event.value = this.getField(\"price\").value * (2 + 3);";
        using var scripted = PdfScripts.SetDocumentScript(source.CreateSnapshot(), "calc", code);
        var records = PdfScripts.Inspect(scripted.CreateSnapshot());
        Assert.Contains(records, r => r.Source == code);
        Assert.Contains(records, r => r.IsAttachment);
        var values = new Dictionary<string, decimal> { { "price", 4m } };
        Assert.Equal("Script.ExecutionDisabled", Assert.Throws<PdfException>(() => PdfScripts.EvaluateCalculation(code, values)).Diagnostic.Code);
        Assert.Equal(20m, PdfScripts.EvaluateCalculation(code, values, true).Value);
        Assert.Equal(20m, ContractExamples.CSharp.ContractExample.Calculate(code, values));
        Assert.Equal(20m, ContractExamples.VisualBasic.ContractExample.Calculate(code, values));
        using var removed = PdfScripts.SetDocumentScript(scripted.CreateSnapshot(), "calc", null);
        Assert.DoesNotContain(PdfNavigation.Inspect(removed.CreateSnapshot()).Actions, a => a.IsAttachment && a.Location.StartsWith("Catalog/Names/JavaScript", StringComparison.Ordinal));
        foreach (string malicious in new[] { "event.value = eval(\"1\");", "while(true){}", "event.value = System.IO.File.ReadAllText(\"x\");", "event.value = this.getField(\"price\").value.constructor;", "event.value = (function(){return 1;})();" })
            Assert.Equal("Script.UnsupportedSubset", Assert.Throws<PdfException>(() => PdfScripts.EvaluateCalculation(malicious, values, true)).Diagnostic.Code);
        Assert.Equal("Script.DivisionByZero", Assert.Throws<PdfException>(() => PdfScripts.EvaluateCalculation("event.value = 1/0;", values, true)).Diagnostic.Code);
        Assert.Equal("Script.FieldMissing", Assert.Throws<PdfException>(() => PdfScripts.EvaluateCalculation("event.value = this.getField(\"missing\").value;", values, true)).Diagnostic.Code);
        Assert.Equal("Limit.Depth", Assert.Throws<PdfException>(() => PdfScripts.EvaluateCalculation("event.value = (((((1)))));", values, true, new(limits: new(maximumDepth: 3)))).Diagnostic.Code);
        Assert.ThrowsAny<OperationCanceledException>(() => PdfScripts.EvaluateCalculation(code, values, true, cancellationToken: new CancellationToken(true)));
    }
}
