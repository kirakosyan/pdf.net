using System.IO.Compression;
using System.Text;
using Fluent.Pdf.Fonts;
using Fluent.Pdf.Word;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class WordConversionTests
{
    private const string OriginalRtf = @"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fnil Noto Sans;}}\f0\fs20 AB\par BA}";
    private static readonly PdfOperationPolicy Permissive = new(PdfProcessingMode.Permissive);
    internal static PdfProviderRegistry Registry(params IPdfProvider[] extras)
    {
        var providers = new List<IPdfProvider>
        {
            ManagedPdfProvider.Instance,
            new PdfSimpleTextShaper(),
            new ManagedFontProvider(),
            new WordnetPdfConverter(true)
        };
        providers.AddRange(extras);
        return new(providers.ToArray());
    }

    private static PdfDocumentSnapshot Original()
    {
        var template = new PdfGenerationTemplate<string>("original-word", (c, value) => c.Paragraph(value, GenerationTests.Style()));
        using var doc = PdfDataGeneration.Generate(new[] { "AB" }, template, new(GenerationTests.Layout(), ""), new(ManagedPdfProvider.Instance, new PdfSimpleTextShaper()));
        return doc.CreateSnapshot();
    }

    private static string Text(PdfDocument document) => string.Concat(Enumerable.Range(0, document.Pages.Count).Select(p => PdfTextExtraction.ReadPage(document.CreateSnapshot(), p, providers: Registry()).Text));
    private static string Error(Action action) => Assert.ThrowsAny<PdfException>(action).Diagnostic.Code;
    [Fact]
    public void OriginalRtfImportsThroughTheActualPinnedWordPackage()
    {
        using var source = new MemoryStream(Encoding.ASCII.GetBytes(OriginalRtf));
        using var doc = PdfWordConversion.Import(source, PdfWordFormat.Rtf, new(PdfWordReconstruction.Editable), Registry());
        Assert.Contains("AB", Text(doc));
        Assert.Contains("BA", Text(doc));
        Assert.True(source.CanRead);
        Assert.True(doc.LastOperationReport!.Succeeded);
        Assert.Equal("1.1.0", WordnetPdfConverter.UpstreamVersion);
        Assert.Equal(40, WordnetPdfConverter.SourceRevision.Length);
        Assert.Equal(64, WordnetPdfConverter.PackageSha256.Length);
        Assert.Equal("Word.BundledFontPolicy", Error(() => new WordnetPdfConverter(false)));
    }

    [Theory]
    [InlineData(PdfWordFormat.Doc)]
    [InlineData(PdfWordFormat.Docx)]
    [InlineData(PdfWordFormat.Rtf)]
    public void EveryWordFormatContainsRealContentAndRoundTripsRecoveredUnicode(PdfWordFormat format)
    {
        var source = Original();
        using var output = new MemoryStream();
        var report = PdfWordConversion.Write(source, format, new(PdfWordReconstruction.Editable), Registry(), output, Permissive);
        Assert.True(report.Succeeded);
        Assert.True(output.CanWrite);
        Assert.Contains(report.Diagnostics, d => d.Code == "Word.EditableTextReconstruction" && d.Severity == PdfDiagnosticSeverity.Warning);
        Assert.Contains(report.Diagnostics, d => d.Code == "Word.RecoveredTextProvenance" && d.Severity == PdfDiagnosticSeverity.Information);
        byte[] bytes = output.ToArray();
        if (format == PdfWordFormat.Doc)
            Assert.Equal(new byte[] { 208, 207, 17, 224, 161, 177, 26, 225 }, bytes.Take(8));
        else if (format == PdfWordFormat.Docx)
        {
            using var zip = new ZipArchive(output, ZipArchiveMode.Read, true);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            Assert.Contains(">AB<", reader.ReadToEnd());
        }
        else
            Assert.StartsWith(@"{\rtf1", Encoding.ASCII.GetString(bytes));
        output.Position = 0;
        using var read = PdfWordConversion.Import(output, format, new(PdfWordReconstruction.Editable), Registry(), Permissive);
        Assert.Contains("AB", Text(read));
        Assert.True(output.CanRead);
    }

    [Theory]
    [InlineData(PdfWordFormat.Docx)]
    [InlineData(PdfWordFormat.Rtf)]
    public void FixedLayoutUsesOnlyTheExplicitRendererAndPreservesItsExactPngPixels(PdfWordFormat format)
    {
        var renderer = new OriginalRenderer();
        using var output = new MemoryStream();
        var report = PdfWordConversion.Write(Original(), format, new(PdfWordReconstruction.FixedLayout, rasterDpi: 72), Registry(renderer), output, Permissive);
        Assert.True(report.Succeeded);
        Assert.Equal(1, renderer.Calls);
        Assert.Contains(report.Diagnostics, d => d.Code == "Word.FixedLayoutRasterization");
        Assert.Equal(PdfCapability.ConvertWord, renderer.Context!.Capability);
        Assert.Equal("ExportWord", renderer.Context.Operation);
        if (format == PdfWordFormat.Docx)
        {
            output.Position = 0;
            using var zip = new ZipArchive(output, ZipArchiveMode.Read, true);
            using var png = zip.GetEntry("word/media/image1.png")!.Open();
            using var copy = new MemoryStream();
            png.CopyTo(copy);
            var bytes = copy.ToArray();
            Assert.True(bytes.Length < 5000, "The solid raster must be compressed before Word packaging.");
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.Take(8));
            Assert.Equal(200, (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19]);
            Assert.Equal(100, (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23]);
        }
        else
            Assert.Contains(@"\pngblip", Encoding.ASCII.GetString(output.ToArray()));
    }

    [Fact]
    public void UnsupportedLossesAndMissingRendererFailBeforePublication()
    {
        using var output = new MemoryStream();
        Assert.Equal("Word.EditableTextReconstruction", Error(() => PdfWordConversion.Write(Original(), PdfWordFormat.Docx, new(PdfWordReconstruction.Editable), Registry(), output)));
        Assert.Equal(0, output.Length);
        Assert.Equal("Provider.Unavailable", Error(() => PdfWordConversion.Write(Original(), PdfWordFormat.Docx, new(PdfWordReconstruction.FixedLayout), Registry(), output, Permissive)));
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(OriginalRtf));
        Assert.Equal("Provider.Unavailable", Error(() => PdfWordConversion.Import(input, PdfWordFormat.Rtf, new(PdfWordReconstruction.Editable), PdfProviderRegistry.Empty)));
        Assert.Equal(0, input.Position);
    }

    [Theory]
    [InlineData(@"{\rtf1\ansi{\field{\*\fldinst HYPERLINK file:///never/read}}}", "Word.RtfActiveContent")]
    [InlineData(@"{\rtf1\ansi{\object\objdata 00}}", "Word.RtfActiveContent")]
    [InlineData(@"{\rtf1\ansi\ansicpg65001 AB}", "Word.RtfCodepage")]
    [InlineData(@"{\rtf1\ansi\u65}", "Word.Rtf")]
    [InlineData(@"{\rtf1\ansi AB", "Word.Rtf")]
    [InlineData(@"{\rtf1\ansi\bin3 ABC}", "Word.RtfBinary")]
    public void MalformedAndActiveRtfFailsEvenUnderPermissivePolicy(string value, string expected)
    {
        using var source = new MemoryStream(Encoding.ASCII.GetBytes(value));
        Assert.Equal(expected, Error(() => PdfWordConversion.Import(source, PdfWordFormat.Rtf, new(PdfWordReconstruction.Editable), Registry(), Permissive)));
        Assert.True(source.CanRead);
    }

    [Fact]
    public void UnknownRtfControlsAreStrictFailuresAndPermissiveLosses()
    {
        const string value = @"{\rtf1\ansi\unknownfeature AB}";
        using var source = new MemoryStream(Encoding.ASCII.GetBytes(value));
        Assert.Equal("Word.RtfControl", Error(() => PdfWordConversion.Import(source, PdfWordFormat.Rtf, new(PdfWordReconstruction.Editable), Registry())));
        source.Position = 0;
        using var doc = PdfWordConversion.Import(source, PdfWordFormat.Rtf, new(PdfWordReconstruction.Editable), Registry(), Permissive);
        Assert.Contains("AB", Text(doc));
        Assert.Contains(doc.LastOperationReport!.Diagnostics, d => d.Code == "Word.RtfControl" && d.Severity == PdfDiagnosticSeverity.Warning);
    }

    [Fact]
    public void InputExpansionOutputAndAtomicBoundariesUseTheRootBudget()
    {
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(OriginalRtf));
        Assert.Equal("Word.InputLimit", Error(() => PdfWordConversion.Import(input, PdfWordFormat.Rtf, new(PdfWordReconstruction.Editable, maximumInputBytes: 2), Registry())));
        input.Position = 0;
        Assert.Equal("Limit.ExpandedBytes", Error(() => PdfWordConversion.Import(input, PdfWordFormat.Rtf, new(PdfWordReconstruction.Editable), Registry(), new(limits: new(maximumExpandedBytes: 1)))));
        var provider = new OverflowProvider();
        var dir = Path.Combine(Path.GetTempPath(), "pdf-word-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "retained.bin");
        File.WriteAllText(path, "retained");
        try
        {
            Assert.Equal("Codec.OutputLength", Error(() => PdfWordConversion.WriteFile(Original(), PdfWordFormat.Docx, new(PdfWordReconstruction.Editable, maximumOutputBytes: 1), new(provider), path)));
            Assert.Equal("retained", File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(dir));
            Assert.NotNull(provider.Context);
            Assert.False(provider.Output!.CanWrite);
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(dir);
        }
    }

    [Fact]
    public void RtfUnicodeFormattingAndRectangularTableCellsSurviveImport()
    {
        const string text = @"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fnil Noto Sans;}}\f0 A\u233?B\par {\b AB}\par \trowd\cellx1800\cellx3600\intbl AB\cell BA\cell\row \trowd\cellx1800\cellx3600\intbl BA\cell AB\cell\row}";
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(text));
        using var doc = PdfWordConversion.Import(input, PdfWordFormat.Rtf, new(PdfWordReconstruction.Editable), Registry(), Permissive);
        var recovered = Text(doc);
        Assert.Contains("AéB", recovered);
        Assert.True(recovered.Count(ch => ch == 'A') >= 6);
        Assert.True(recovered.Count(ch => ch == 'B') >= 6);
    }

    [Fact]
    public void CancellationAndPaintedPagesWithoutUnicodeDoNotInventRecoveredText()
    {
        using var cancellation = new System.Threading.CancellationTokenSource();
        cancellation.Cancel();
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(OriginalRtf));
        Assert.ThrowsAny<OperationCanceledException>(() => PdfWordConversion.Import(input, PdfWordFormat.Rtf, new(PdfWordReconstruction.Editable), Registry(), cancellationToken: cancellation.Token));
        Assert.Equal(0, input.Position);
        using var doc = Pdf.Create().AddPage(new PdfPageSize(100, 100));
        doc.Pages[0].Content = PdfContentProgram.Create(new[] { new PdfContentOperation(PdfOperator.Rectangle, PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(10), PdfValue.Integer(10)), new PdfContentOperation(PdfOperator.Fill) });
        using var output = new MemoryStream();
        Assert.Equal("Word.NoRecoverableText", Error(() => PdfWordConversion.Write(doc.CreateSnapshot(), PdfWordFormat.Docx, new(PdfWordReconstruction.Editable), Registry(), output, Permissive)));
        Assert.Equal(0, output.Length);
    }

    [Theory]
    [InlineData("word/_rels/document.xml.rels", "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='r1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/image' TargetMode='External' Target='file:///never/read'/></Relationships>", "Word.PackageExternalResource")]
    [InlineData("word/_rels/document.xml.rels", "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='r1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/image' Target='../../../../never/read'/></Relationships>", "Word.PackageRelationship")]
    [InlineData("word/document.xml", "<!DOCTYPE document [<!ENTITY x SYSTEM 'file:///never/read'>]><document>&x;</document>", "Word.PackageXml")]
    [InlineData("word/vbaProject.bin", "original", "Word.PackageActiveContent")]
    public void PackageEntitiesExternalResourcesMacrosAndTraversalFailBeforeTheDependency(string part, string content, string expected)
    {
        using var input = new MemoryStream();
        using (var zip = new ZipArchive(input, ZipArchiveMode.Create, true))
        {
            using var writer = new StreamWriter(zip.CreateEntry(part).Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        input.Position = 0;
        Assert.Equal(expected, Error(() => PdfWordConversion.Import(input, PdfWordFormat.Docx, new(PdfWordReconstruction.Editable), Registry(), Permissive)));
        Assert.True(input.CanRead);
    }

    [Fact]
    public void PublicCSharpAndVisualBasicWordConsumersExecute()
    {
        var options = new PdfWordConversionOptions(PdfWordReconstruction.Editable);
        using var cs = new MemoryStream();
        using var vb = new MemoryStream();
        Assert.True(ContractExamples.CSharp.ContractExample.ExportWord(Original(), PdfWordFormat.Docx, options, Registry(), cs).Succeeded);
        Assert.True(ContractExamples.VisualBasic.ContractExample.ExportWord(Original(), PdfWordFormat.Rtf, options, Registry(), vb).Succeeded);
        Assert.True(cs.Length > 0);
        Assert.True(vb.Length > 0);
    }

    private sealed class OverflowProvider : IPdfWordConverter
    {
        internal Stream? Output;
        internal PdfOperationContext? Context;
        public PdfProviderDescriptor Descriptor { get; } = new("original.word-overflow", "1.0", PdfCapability.ConvertWord);

        public bool Supports(PdfWordFormat format, bool import) => true;
        public void Import(Stream source, PdfWordFormat format, Stream pdfOutput, PdfWordConversionOptions options, PdfOperationContext context) => throw new InvalidOperationException();
        public void Export(PdfDocumentSnapshot source, PdfWordFormat format, Stream output, PdfWordConversionOptions options, PdfProviderRegistry providers, PdfOperationContext context)
        {
            Output = output;
            Context = context;
            try
            {
                output.Write(new byte[] { 1, 2 }, 0, 2);
            }
            catch (PdfException)
            {
            }
        }
    }

    private sealed class OriginalRenderer : IPdfPageRenderer
    {
        internal int Calls;
        internal PdfOperationContext? Context;
        internal readonly List<PdfOperationContext> Contexts = new();
        public PdfProviderDescriptor Descriptor { get; } = new("original.word-renderer", "1.0", PdfCapability.RenderPages);

        public void Render(PdfRasterRequest request, Stream output, PdfOperationContext context)
        {
            Context = context;
            Contexts.Add(context);
            Calls++;
            context.Consume(PdfResourceKind.ExpandedBytes, 8192);
            var row = new byte[request.Stride];
            for (int x = 0; x < request.Width; x++)
            {
                row[x * 4] = 30;
                row[x * 4 + 1] = 20;
                row[x * 4 + 2] = 10;
                row[x * 4 + 3] = 255;
            }

            for (int y = 0; y < request.Height; y++)
            {
                context.Checkpoint();
                output.Write(row, 0, row.Length);
            }
        }
    }
}
