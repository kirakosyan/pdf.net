using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class BusinessDataTests
{
    private static readonly XNamespace Ns = "urn:fluent-pdf:business:1";
    private static byte[] Save(PdfDocument document) { using var output = new MemoryStream(); document.Save(output); return output.ToArray(); }
    private static byte[] Export(byte[] bytes, PdfBusinessDataFormat format, PdfBusinessDataOptions? options = null, PdfProviderRegistry? providers = null)
    { using var output = new MemoryStream(); PdfBusinessData.Write(new MemoryStream(bytes), output, format, options, providers); return output.ToArray(); }
    private static XElement Xml(byte[] bytes, PdfBusinessDataOptions? options = null, PdfProviderRegistry? providers = null) => XElement.Parse(Encoding.UTF8.GetString(Export(bytes, PdfBusinessDataFormat.Xml, options, providers)));
    private static IEnumerable<XElement> Records(XElement root, string kind) => root.Elements(Ns + "record").Where(e => (string?)e.Element(Ns + "kind") == kind);
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    private static string Artifact(string name) { string path = Path.Combine(AppContext.BaseDirectory, "artifacts", "business-data"); Directory.CreateDirectory(path); return Path.Combine(path, name); }

    [Fact]
    public void UnknownVersionsAndUnsupportedAggregationRejectBeforeReadingInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfBusinessDataOptions(schemaVersion: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfBusinessDataOptions(maximumRecords: 0));
        Assert.Throws<ArgumentException>(() => new PdfBusinessDataOptions(tables: new(PdfTableDetectionMode.Ruled, joinConsecutivePages: true)));
        Assert.Throws<ArgumentException>(() => new PdfBusinessDataOptions(recognition: new("eng")));
        Assert.Throws<ArgumentException>(() => new PdfBusinessDataOptions(recognition: new("eng"), rendering: new(96, 97)));
        using var input = new MemoryStream(new byte[] { 1 }); using var output = new MemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfBusinessData.Write(input, output, (PdfBusinessDataFormat)99)); Assert.Equal(0, input.Position);
    }

    [Fact]
    public void DigitalTextRetainsMappingSourceCodeGeometryAndSeparateInferredGroups()
    {
        using var document = TableExtractionTests.Fixture(); var bytes = Save(document); var xml = Xml(bytes);
        var page = Assert.Single(Records(xml, "page")); Assert.Equal("mixed", (string?)page.Element(Ns + "provenance"));
        var chars = Records(xml, "character").ToArray(); Assert.NotEmpty(chars);
        Assert.Equal("HeaderAB", string.Concat(chars.Select(c => (string?)c.Element(Ns + "text"))));
        Assert.All(chars, c => { Assert.NotNull(c.Element(Ns + "mapping")); Assert.NotNull(c.Element(Ns + "operationIndex")); Assert.NotNull(c.Element(Ns + "sourceCode")); Assert.NotNull(c.Element(Ns + "width")); });
        Assert.NotEmpty(Records(xml, "paragraph"));
        Assert.All(xml.Elements(), r => Assert.Equal("false", (string?)r.Element(Ns + "businessVerified")));
        using var hash = SHA256.Create(); string digest = string.Concat(hash.ComputeHash(bytes).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        Assert.Equal(digest, (string?)xml.Attribute("sourceSha256"));
    }

    [Fact]
    public void MergedTableCellsKeepHeuristicConfidenceSourceIndicesAndExplicitCulture()
    {
        using var document = TableExtractionTests.Fixture(left: "1.234,50", right: "-2,5"); byte[] bytes = Save(document);
        var options = new PdfBusinessDataOptions(tables: new(PdfTableDetectionMode.Ruled, numericCulture: "de-DE"));
        var xml = Xml(bytes, options); var table = Assert.Single(Records(xml, "table"));
        Assert.Equal("de-DE", (string?)table.Element(Ns + "numericCulture"));
        var cells = Records(xml, "cell").ToArray(); Assert.Equal(3, cells.Length);
        Assert.Equal("2", (string?)cells[0].Element(Ns + "columnSpan"));
        Assert.Contains(cells, c => (string?)c.Element(Ns + "number") == "1234.50");
        Assert.All(cells, c => { Assert.Equal("inference", (string?)c.Element(Ns + "provenance")); Assert.Equal("uncalibrated-table-heuristic", (string?)c.Element(Ns + "confidenceKind")); Assert.NotEmpty(c.Element(Ns + "sourceIndices")!.Elements()); });
        Assert.All(Records(Xml(bytes, new(tables: new(PdfTableDetectionMode.Ruled))), "cell"), c => Assert.Equal("null", (string?)c.Element(Ns + "number")!.Attribute("type")));
    }

    [Fact]
    public void FieldsRetainLiteralNamesAndValuesWithoutInterpretingBusinessMeaning()
    {
        const string text = "<total>\" & \r\n😀 001,20";
        using var document = FormTests.Source((edit, _, _, id) => NavigationTests.Put(edit, id, "V", NavigationTests.T(text)));
        byte[] bytes = Save(document); var xml = Xml(bytes); var field = Assert.Single(Records(xml, "field"));
        Assert.Equal("Group", (string?)field.Element(Ns + "label")); Assert.Equal(text, (string?)Assert.Single(field.Element(Ns + "values")!.Elements()));
        Assert.Equal("source", (string?)field.Element(Ns + "provenance")); Assert.Equal("String", (string?)field.Element(Ns + "valueKind"));
        Assert.NotEmpty(field.Element(Ns + "sourceObjects")!.Elements());
        Assert.Empty(Records(Xml(bytes, new(includeFields: false)), "field"));
        string json = Encoding.UTF8.GetString(Export(bytes, PdfBusinessDataFormat.Json)); Assert.Contains("\\ud83d\\ude00", json); Assert.Contains("\\u000d\\u000a", json);
    }

    [Fact]
    public void ExplicitStructureRetainsDeclaredRolesChildOrderAndFindingsWithoutConformanceClaim()
    {
        byte[] bytes = SemanticReviewTests.Tagged();
        var xml = Xml(bytes, new(structureProfile: PdfAccessibilityProfile.Ua1));
        Assert.NotEmpty(Records(xml, "structure"));
        Assert.Contains(Records(xml, "structure"), r => (string?)r.Element(Ns + "label") == "Document");
        Assert.All(Records(xml, "structure"), r => Assert.NotNull(r.Element(Ns + "children")));
        Assert.Empty(Records(Xml(bytes), "structure"));
    }

    private sealed class Renderer : IPdfPageRenderer
    {
        public PdfProviderDescriptor Descriptor { get; } = new("fixture.business-render", "1", PdfCapability.RenderPages);
        internal PdfOperationContext? Seen;
        public void Render(PdfRasterRequest request, Stream output, PdfOperationContext context)
        {
            Seen = context; context.Consume(PdfResourceKind.ExpandedBytes, 4096); var buffer = Enumerable.Repeat((byte)255, 4096).ToArray();
            long left = request.ByteLength;
            while (left > 0) { context.Checkpoint(); int n = (int)Math.Min(left, buffer.Length); output.Write(buffer, 0, n); left -= n; }
        }
    }
    private sealed class Recognition : IPdfOcrProvider
    {
        public PdfProviderDescriptor Descriptor { get; } = new("fixture.business-ocr", "1", PdfCapability.RecognizeText);
        internal PdfOperationContext? Seen;
        public PdfOcrResult Recognize(PdfOcrRaster raster, PdfOcrOptions options, PdfOperationContext context)
        {
            Seen = context; using var pixels = raster.OpenPixels(context); Assert.Equal(255, pixels.ReadByte());
            return new("Total 10", new[] { new PdfOcrWord("10", new(20, 20, 10, 10), 99.9, 1, 1, 1, 1) }, "eng", "original-contract-fixture", "original-not-a-trained-model", 99.9);
        }
    }
    [Fact]
    public void ExplicitOcrIsSourceBoundSharesTheRenderContextAndNeverVerifiesAValue()
    {
        using var document = TableExtractionTests.Fixture(); byte[] bytes = Save(document);
        var renderer = new Renderer(); var recognition = new Recognition(); var providers = new PdfProviderRegistry(renderer, recognition);
        var options = new PdfBusinessDataOptions(recognition: new("eng"), rendering: new(144, 144, region: new(10, 20, 100, 100)));
        var xml = Xml(bytes, options, providers); Assert.Same(renderer.Seen, recognition.Seen);
        Assert.NotEmpty(Records(xml, "character")); var ocr = Assert.Single(Records(xml, "ocr")); var word = Assert.Single(Records(xml, "ocr-word"));
        Assert.Equal("ocr", (string?)ocr.Element(Ns + "provenance")); Assert.Equal("engine-score-0-100", (string?)word.Element(Ns + "confidenceKind"));
        Assert.Equal("99.9", (string?)word.Element(Ns + "confidence")); Assert.Equal("false", (string?)word.Element(Ns + "businessVerified"));
        Assert.Equal("20", (string?)word.Element(Ns + "x")); Assert.Equal("30", (string?)word.Element(Ns + "y"));
        Assert.Equal("5", (string?)word.Element(Ns + "width")); Assert.Null(word.Element(Ns + "number"));
        Assert.Throws<PdfProviderUnavailableException>(() => Export(bytes, PdfBusinessDataFormat.Json, options));
    }

    [Theory]
    [InlineData(PdfBusinessDataFormat.Json)]
    [InlineData(PdfBusinessDataFormat.Xml)]
    public void InvalidUnicodeAndUnsupportedComplexFieldsRejectBothFormatsBeforeOutput(PdfBusinessDataFormat format)
    {
        foreach (bool complex in new[] { false, true })
        {
            using var document = FormTests.Source((edit, _, _, id) => NavigationTests.Put(edit, id, "V", complex ? NavigationTests.D(("Data", PdfValue.Integer(1))) : NavigationTests.T("a\u0001b")));
            using var input = new MemoryStream(Save(document)); using var output = new MemoryStream(); output.WriteByte(77);
            string code = Error(() => PdfBusinessData.Write(input, output, format));
            Assert.Equal(complex ? "Business.FieldValue" : "Business.Unicode", code); Assert.Equal(new byte[] { 77 }, output.ToArray()); Assert.True(input.CanRead); Assert.True(output.CanWrite);
        }
    }

    [Fact]
    public void LimitsCancellationAndInvalidSourcePreserveDestinationAndBorrowedStreams()
    {
        using var document = TableExtractionTests.Fixture(); var bytes = Save(document);
        using var input = new MemoryStream(bytes); using var output = new MemoryStream(); output.WriteByte(42);
        Assert.Equal("Business.RecordLimit", Error(() => PdfBusinessData.Write(input, output, PdfBusinessDataFormat.Json, new(maximumRecords: 1))));
        Assert.Equal(new byte[] { 42 }, output.ToArray()); Assert.True(input.CanRead); Assert.True(output.CanWrite);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfBusinessData.Write(new MemoryStream(bytes), output, PdfBusinessDataFormat.Xml, cancellationToken: cancel.Token));
        Assert.StartsWith("Limit.", Error(() => PdfBusinessData.Write(new MemoryStream(bytes), output, PdfBusinessDataFormat.Json, policy: new(limits: new(maximumExpandedBytes: 1)))));
        Assert.Equal("Codec.OutputLength", Error(() => PdfBusinessData.Write(new MemoryStream(bytes), output, PdfBusinessDataFormat.Xml, new(maximumOutputBytes: 1))));
        Assert.Throws<PdfException>(() => PdfBusinessData.Write(new MemoryStream(new byte[] { 1, 2, 3 }), output, PdfBusinessDataFormat.Json));
        string path = Artifact("atomic.json"); File.WriteAllBytes(path, new byte[] { 42 });
        Assert.Equal("Business.RecordLimit", Error(() => PdfBusinessData.WriteFile(new MemoryStream(bytes), path, PdfBusinessDataFormat.Json, new(maximumRecords: 1)))); Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void ConcurrentIndependentExportsAreDeterministicUnderDifferentAmbientCultures()
    {
        using var document = TableExtractionTests.Fixture(left: "1.234,50"); byte[] bytes = Save(document);
        var options = new PdfBusinessDataOptions(tables: new(PdfTableDetectionMode.Ruled, numericCulture: "de-DE")); var results = new byte[8][];
        Parallel.For(0, results.Length, i => { var prior = CultureInfo.CurrentCulture; try { CultureInfo.CurrentCulture = new CultureInfo(i % 2 == 0 ? "en-US" : "de-DE"); results[i] = Export(bytes, PdfBusinessDataFormat.Json, options); } finally { CultureInfo.CurrentCulture = prior; } });
        foreach (var result in results) Assert.Equal(results[0], result);
    }

    [Fact]
    public void OriginalDigitalFieldsStructureAndMixedOcrFixturesProducePairedSchemaArtifacts()
    {
        using var table = TableExtractionTests.Fixture(left: "1.234,50", right: "-2,5");
        using var fields = FormTests.Source((edit, _, _, id) => NavigationTests.Put(edit, id, "V", NavigationTests.T("<amount>\" &\r\n😀 001,20")));
        foreach (string name in new[] { "digital", "table", "fields", "structure", "mixed" })
        {
            byte[] bytes = name == "fields" ? Save(fields) : name == "structure" ? SemanticReviewTests.Tagged() : Save(table);
            var options = new PdfBusinessDataOptions(tables: name == "table" ? new(PdfTableDetectionMode.Ruled, numericCulture: "de-DE") : null, structureProfile: name == "structure" ? PdfAccessibilityProfile.Ua1 : null,
                recognition: name == "mixed" ? new("eng") : null, rendering: name == "mixed" ? new(72, 72) : null);
            var providers = new PdfProviderRegistry(new Renderer(), new Recognition());
            File.WriteAllBytes(Artifact(name + ".pdf"), bytes);
            File.WriteAllBytes(Artifact(name + ".json"), Export(bytes, PdfBusinessDataFormat.Json, options, providers));
            File.WriteAllBytes(Artifact(name + ".xml"), Export(bytes, PdfBusinessDataFormat.Xml, options, providers));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompiledLanguageConsumersExportBothFormats(bool visualBasic)
    {
        using var document = TableExtractionTests.Fixture(); byte[] bytes = Save(document);
        string name = visualBasic ? "visual-basic" : "csharp"; File.WriteAllBytes(Artifact(name + ".pdf"), bytes);
        foreach (var format in new[] { PdfBusinessDataFormat.Json, PdfBusinessDataFormat.Xml })
        {
            using var input = new MemoryStream(bytes); using var output = new MemoryStream();
            if (visualBasic) ContractExamples.VisualBasic.ContractExample.ExportBusinessData(input, output, format);
            else ContractExamples.CSharp.ContractExample.ExportBusinessData(input, output, format);
            Assert.True(input.CanRead); Assert.True(output.CanWrite); Assert.True(output.Length > 0);
            File.WriteAllBytes(Artifact(name + (format == PdfBusinessDataFormat.Json ? ".json" : ".xml")), output.ToArray());
        }
    }
}
