using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Fluent.Pdf.Fonts;
using Fluent.Pdf.Word;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class MailMergeTests
{
    internal static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static PdfMergeRecord Record(params (string Key, PdfMergeValue Value)[] values) => new(values.ToDictionary(v => v.Key, v => v.Value));
    private static PdfProviderRegistry Providers() => new(ManagedPdfProvider.Instance, new PdfSimpleTextShaper(), new ManagedFontProvider(), new WordnetPdfConverter(true));
    private static PdfMailMergeOptions Options(PdfMergeMissingField missing = PdfMergeMissingField.Error, int visits = 100000, int records = 100000, string culture = "") => new(new(new(new PdfPageGeometry(new PdfPageBoxes(new PdfRect(0, 0, 300, 400))), PdfMargins.Uniform(10)), culture), new Dictionary<string, PdfFlowTextStyle> { ["body"] = new(new(PdfFont.Standard("Courier", new(new ManagedFontProvider()))), 10) }, missing, visits, records);
    private static string Text(PdfDocument doc) => string.Concat(Enumerable.Range(0, doc.Pages.Count).Select(p => PdfTextExtraction.ReadPage(doc.CreateSnapshot(), p, providers: Providers()).Text));
    private static string Error(Action action) => Assert.ThrowsAny<PdfException>(action).Diagnostic.Code;
    [Fact]
    public void NativeInvoiceSupportsExplicitCultureRowsConditionsAndTableHeaders()
    {
        var data = Record(("name", PdfMergeValue.Scalar("AB")), ("paid", PdfMergeValue.Scalar(true)), ("items", PdfMergeValue.Records(Record(("label", PdfMergeValue.Scalar("BA")), ("amount", PdfMergeValue.Scalar(1234.5m))), Record(("label", PdfMergeValue.Scalar("AB")), ("amount", PdfMergeValue.Scalar(-2.5m))))));
        var template = new PdfMergeTemplate("invoice", new PdfMergeParagraph(new[] { PdfMergeRun.Field("name") }), new PdfMergeCondition("paid", new PdfMergeBlock[] { new PdfMergeParagraph(new[] { PdfMergeRun.Text("PAID") }) }), new PdfMergeTable("items", new PdfMergeTableColumn(PdfTableColumn.Auto(), new[] { PdfMergeRun.Field("label") }, "ITEM"), new PdfMergeTableColumn(PdfTableColumn.Auto(), new[] { PdfMergeRun.Field("amount", format: "N2") }, "VALUE")));
        using var doc = PdfMailMerge.Generate(template, data, Options(culture: "de-DE"), Providers());
        string text = Text(doc);
        Assert.Contains("AB", text);
        Assert.Contains("PAID", text);
        Assert.Contains("ITEM", text);
        Assert.Contains("1.234,50", text);
        Assert.Contains("-2,50", text);
    }

    [Fact]
    public void NativeRepeatedScopeUsesRootAndIndexWithoutReflectionOrCodeEvaluation()
    {
        var rows = new[]
        {
            Record(("name", PdfMergeValue.Scalar("AB"))),
            Record(("name", PdfMergeValue.Scalar("BA")))
        };
        var data = Record(("title", PdfMergeValue.Scalar("X")), ("items", PdfMergeValue.Records(rows)));
        var template = new PdfMergeTemplate("scope", new PdfMergeRepeat("items", new PdfMergeParagraph(new[] { PdfMergeRun.Field("@index"), PdfMergeRun.Field("root.title"), PdfMergeRun.Field("name") })));
        using var doc = PdfMailMerge.Generate(template, data, Options(), Providers());
        Assert.Equal("0XAB1XBA", Text(doc));
        Assert.Throws<ArgumentException>(() => PdfMergeValue.Scalar(new object ()));
        Assert.Throws<ArgumentException>(() => PdfMergeRun.Field("System.IO.File.ReadAllText(...)"));
    }

    [Theory]
    [InlineData(PdfMergeMissingField.Error, "")]
    [InlineData(PdfMergeMissingField.Empty, "")]
    [InlineData(PdfMergeMissingField.PreserveScalar, "{{missing}}")]
    public void MissingFieldsFollowTheExplicitPolicy(PdfMergeMissingField missing, string expected)
    {
        var template = new PdfMergeTemplate("missing", new PdfMergeParagraph(new[] { PdfMergeRun.Text("AB"), PdfMergeRun.Field("missing") }));
        var data = Record();
        if (missing == PdfMergeMissingField.Error)
            Assert.Equal("Merge.MissingField", Error(() => PdfMailMerge.Generate(template, data, Options(missing), Providers())));
        else
        {
            using var doc = PdfMailMerge.Generate(template, data, Options(missing), Providers());
            Assert.Equal("AB" + expected, Text(doc));
            Assert.Contains(doc.LastOperationReport!.Diagnostics, d => d.Code.StartsWith("Merge.Missing", StringComparison.Ordinal) && d.Severity == PdfDiagnosticSeverity.Information);
        }
    }

    [Fact]
    public void EmptyRepeatedRecordsAndFalseConditionsDoNotProduceInventedContent()
    {
        var data = Record(("items", PdfMergeValue.Records()), ("show", PdfMergeValue.Scalar(false)));
        var template = new PdfMergeTemplate("empty", new PdfMergeParagraph(new[] { PdfMergeRun.Text("AB") }), new PdfMergeRepeat("items", new PdfMergeParagraph(new[] { PdfMergeRun.Text("NEVER") })), new PdfMergeCondition("show", new PdfMergeBlock[] { new PdfMergeParagraph(new[] { PdfMergeRun.Text("NEVER") }) }));
        using var doc = PdfMailMerge.Generate(template, data, Options(), Providers());
        Assert.Equal("AB", Text(doc));
        Assert.Equal("Merge.ConditionType", Error(() => PdfMailMerge.Generate(template, Record(("items", PdfMergeValue.Records()), ("show", PdfMergeValue.Scalar("true"))), Options(), Providers())));
    }

    [Fact]
    public void EvaluationRecordAndVisitCapsFailBeforeAtomicPublication()
    {
        var data = Record(("items", PdfMergeValue.Records(Record(), Record())));
        var template = new PdfMergeTemplate("limit", new PdfMergeRepeat("items", new PdfMergeParagraph(new[] { PdfMergeRun.Text("AB") })));
        Assert.Equal("Merge.RecordLimit", Error(() => PdfMailMerge.Generate(template, data, Options(records: 1), Providers())));
        Assert.Equal("Merge.VisitLimit", Error(() => PdfMailMerge.Generate(template, data, Options(visits: 1), Providers())));
        var dir = Path.Combine(Path.GetTempPath(), "pdf-merge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "retained.bin");
        File.WriteAllText(path, "retained");
        try
        {
            Assert.Equal("Merge.RecordLimit", Error(() => PdfMailMerge.WriteFile(template, data, Options(records: 1), Providers(), path)));
            Assert.Equal("retained", File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(dir));
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(dir);
        }
    }

    [Fact]
    public void NativeImagesUseOnlyCapturedPngAndDoNotDiscoverResources()
    {
        using var png = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures/images/rgb.png"));
        var image = PdfMergeImage.ReadPng(png, 10, 10);
        Assert.True(png.CanRead);
        var template = new PdfMergeTemplate("image", new PdfMergeImageBlock("logo"), new PdfMergeParagraph(new[] { PdfMergeRun.Text("AB") }));
        using var doc = PdfMailMerge.Generate(template, Record(("logo", PdfMergeValue.Image(image))), Options(), Providers());
        Assert.Equal("AB", Text(doc));
    }

    internal static XElement Paragraph(string text) => new(W + "p", new XElement(W + "r", new XElement(W + "rPr", new XElement(W + "rFonts", new XAttribute(W + "ascii", "Noto Sans"), new XAttribute(W + "hAnsi", "Noto Sans"))), new XElement(W + "t", text)));
    internal static XElement Control(string tag, params XElement[] content) => new(W + "sdt", new XElement(W + "sdtPr", new XElement(W + "tag", new XAttribute(W + "val", tag))), new XElement(W + "sdtContent", content));
    internal static MemoryStream Template(params XElement[] content)
    {
        var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Part(string name, string value)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(value);
            }

            Part("[Content_Types].xml", "<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Default Extension='bin' ContentType='application/octet-stream'/><Default Extension='rels' ContentType='application/vnd.openxmlformats-package.relationships+xml'/><Override PartName='/word/document.xml' ContentType='application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml'/></Types>");
            Part("_rels/.rels", "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rDocument' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument' Target='word/document.xml'/></Relationships>");
            Part("word/document.xml", new XDocument(new XElement(W + "document", new XAttribute(XNamespace.Xmlns + "w", W), new XElement(W + "body", content, new XElement(W + "sectPr", new XElement(W + "pgSz", new XAttribute(W + "w", 6000), new XAttribute(W + "h", 8000)))))).ToString(SaveOptions.DisableFormatting));
            Part("owned.bin", "original-retained");
        }

        output.Position = 0;
        return output;
    }

    private static PdfDocxMergeOptions DocxOptions(PdfMergeMissingField missing = PdfMergeMissingField.Error, string culture = "", int visits = 100000, int records = 100000) => new(new(culture, missing, visits, records), new(PdfWordReconstruction.Editable));
    private static XDocument Document(Stream output)
    {
        output.Position = 0;
        using var zip = new ZipArchive(output, ZipArchiveMode.Read, true);
        using var part = zip.GetEntry("word/document.xml")!.Open();
        return XDocument.Load(part);
    }

    [Fact]
    public void DocxScalarBindingsSpanRunsEscapeTextAndNeverReevaluateInsertedData()
    {
        var p = Paragraph("{{na");
        p.Add(new XElement(W + "r", new XElement(W + "t", "me}}")));
        using var input = Template(p);
        using var output = new MemoryStream();
        var data = Record(("name", PdfMergeValue.Scalar("<&>{{missing}}")));
        PdfDocxMailMerge.Write(input, data, DocxOptions(), output);
        Assert.Equal("<&>{{missing}}", string.Concat(Document(output).Descendants(W + "t").Select(t => t.Value)));
        Assert.True(input.CanRead);
        Assert.True(output.CanWrite);
        output.Position = 0;
        using var zip = new ZipArchive(output, ZipArchiveMode.Read, true);
        using var reader = new StreamReader(zip.GetEntry("owned.bin")!.Open());
        Assert.Equal("original-retained", reader.ReadToEnd());
    }

    [Fact]
    public void DocxRepeatsConditionsAndScalarControlsUseClosedDataThenTheActualWordProvider()
    {
        using var input = Template(Control("merge:if:show", Paragraph("AB")), Control("merge:each:rows", Paragraph("{{name}} {{amount:N2}}")), Control("merge:field:footer", Paragraph("old")));
        var data = Record(("show", PdfMergeValue.Scalar(true)), ("rows", PdfMergeValue.Records(Record(("name", PdfMergeValue.Scalar("AB")), ("amount", PdfMergeValue.Scalar(1234.5m))), Record(("name", PdfMergeValue.Scalar("BA")), ("amount", PdfMergeValue.Scalar(-2.5m))))), ("footer", PdfMergeValue.Scalar("AB")));
        using var output = new MemoryStream();
        PdfDocxMailMerge.Write(input, data, DocxOptions(culture: "de-DE"), output);
        string content = string.Concat(Document(output).Descendants(W + "t").Select(t => t.Value));
        Assert.Contains("1.234,50", content);
        Assert.Contains("-2,50", content);
        Assert.DoesNotContain("{{", content);
        Assert.Empty(Document(output).Descendants(W + "sdt"));
        input.Position = 0;
        PdfDocument pdf;
        try
        {
            pdf = PdfDocxMailMerge.GeneratePdf(input, data, DocxOptions(culture: "de-DE"), Providers(), new(PdfProcessingMode.Permissive));
        }
        catch (PdfException failure)
        {
            throw new InvalidDataException("Actual Word diagnostic: " + failure.Diagnostic.Code, failure);
        }

        using (pdf)
        {
            Assert.Contains("1.234,50", Text(pdf));
            Assert.Contains(pdf.LastOperationReport!.Diagnostics, d => d.Code == "Wordnet.Docx.AdditionalParts" && d.Severity == PdfDiagnosticSeverity.Warning);
        }
    }

    [Fact]
    public void DocxImageControlsAddOwnedPartsWithExplicitGeometry()
    {
        using var png = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures/images/rgb.png"));
        var data = Record(("logo", PdfMergeValue.Image(PdfMergeImage.ReadPng(png, 20, 10))));
        using var input = Template(Control("merge:image:logo", Paragraph("image")));
        using var output = new MemoryStream();
        PdfDocxMailMerge.Write(input, data, DocxOptions(), output);
        var xml = Document(output);
        Assert.Single(xml.Descendants(XName.Get("drawing", W.NamespaceName)));
        Assert.Empty(xml.Descendants(W + "sdt"));
        output.Position = 0;
        using var zip = new ZipArchive(output, ZipArchiveMode.Read, true);
        Assert.Single(zip.Entries, e => e.FullName.StartsWith("word/media/merge", StringComparison.Ordinal));
    }

    [Fact]
    public void DocxUnknownInstructionsMalformedFieldsAndMissingStructureReject()
    {
        using var unknown = Template(Control("merge:execute:anything", Paragraph("AB")));
        using var output = new MemoryStream();
        Assert.Equal("Merge.Instruction", Error(() => PdfDocxMailMerge.Write(unknown, Record(), DocxOptions(), output)));
        using var malformed = Template(Paragraph("{{name(unsafe)}}"));
        Assert.Equal("Merge.Path", Error(() => PdfDocxMailMerge.Write(malformed, Record(), DocxOptions(), output)));
        using var missing = Template(Control("merge:each:absent", Paragraph("AB")));
        Assert.Equal("Merge.MissingStructuralField", Error(() => PdfDocxMailMerge.Write(missing, Record(), DocxOptions(PdfMergeMissingField.PreserveScalar), output)));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void ForbiddenXmlDataRejectsBeforePublishingAnyDocxBytes()
    {
        using var input = Template(Paragraph("{{value}}"));
        using var output = new MemoryStream();
        Assert.Equal("Merge.XmlCharacter", Error(() => PdfDocxMailMerge.Write(input, Record(("value", PdfMergeValue.Scalar("A\0B"))), DocxOptions(), output)));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async System.Threading.Tasks.Task IndependentJobsLargeFiniteRecordsAndSourceArraysRemainIsolated()
    {
        var rows = Enumerable.Range(0, 100).Select(_ => Record(("name", PdfMergeValue.Scalar("AB")))).ToArray();
        var value = PdfMergeValue.Records(rows);
        rows[0] = Record(("name", PdfMergeValue.Scalar("CHANGED")));
        var data = Record(("items", value));
        var template = new PdfMergeTemplate("finite", new PdfMergeRepeat("items", new PdfMergeParagraph(new[] { PdfMergeRun.Field("name") })));
        using var document = PdfMailMerge.Generate(template, data, Options(), Providers());
        Assert.Equal(string.Concat(Enumerable.Repeat("AB", 100)), Text(document));
        var jobs = await System.Threading.Tasks.Task.WhenAll(Enumerable.Range(0, 3).Select(_ => System.Threading.Tasks.Task.Run(() =>
        {
            using var doc = PdfMailMerge.Generate(template, data, Options(), Providers());
            return Text(doc);
        })));
        Assert.All(jobs, text => Assert.Equal(string.Concat(Enumerable.Repeat("AB", 100)), text));
    }

    [Fact]
    public void DocxCancellationMissingFieldsAndFiniteRecordFailurePreserveDestination()
    {
        using var cancelled = Template(Paragraph("{{missing}}"));
        using var output = new MemoryStream();
        using var cancellation = new System.Threading.CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfDocxMailMerge.Write(cancelled, Record(), DocxOptions(), output, cancellationToken: cancellation.Token));
        Assert.Equal(0, cancelled.Position);
        var dir = Path.Combine(Path.GetTempPath(), "docx-merge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "retained.bin");
        File.WriteAllText(path, "retained");
        try
        {
            using var repeated = Template(Control("merge:each:rows", Paragraph("AB")));
            Assert.Equal("Merge.RecordLimit", Error(() => PdfDocxMailMerge.WriteFile(repeated, Record(("rows", PdfMergeValue.Records(Record(), Record()))), DocxOptions(records: 1), path)));
            Assert.Equal("retained", File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(dir));
            using var absent = Template(Paragraph("{{missing}}"));
            using var preserved = new MemoryStream();
            PdfDocxMailMerge.Write(absent, Record(), DocxOptions(PdfMergeMissingField.PreserveScalar), preserved);
            Assert.Equal("{{missing}}", string.Concat(Document(preserved).Descendants(W + "t").Select(t => t.Value)));
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(dir);
        }
    }

    [Fact]
    public void SharedGenerationContextAndBothPublicConsumersExecute()
    {
        var seen = new List<PdfOperationContext>();
        var original = Options();
        var options = new PdfMailMergeOptions(new(original.Generation.Layout, "", observer: ev => seen.Add(ev.OperationContext)), original.Styles);
        var template = new PdfMergeTemplate("consumers", new PdfMergeParagraph(new[] { PdfMergeRun.Field("value") }));
        var data = Record(("value", PdfMergeValue.Scalar("AB")));
        using var cs = new MemoryStream();
        using var vb = new MemoryStream();
        Assert.True(ContractExamples.CSharp.ContractExample.MergePdf(template, data, options, Providers(), cs).Succeeded);
        Assert.True(seen.Count > 0);
        Assert.All(seen, context => Assert.Same(seen[0], context));
        seen.Clear();
        Assert.True(ContractExamples.VisualBasic.ContractExample.MergePdf(template, data, options, Providers(), vb).Succeeded);
        Assert.All(seen, context => Assert.Same(seen[0], context));
    }
}
