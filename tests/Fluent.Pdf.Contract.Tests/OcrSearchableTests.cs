using System.Globalization;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Fluent.Pdf.Fonts;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class OcrSearchableTests
{
    private static PdfProviderRegistry Providers() => new(new PdfSimpleTextShaper(), ManagedFontProvider.Instance);
    private static PdfFontFallback Fonts() => new(FontTests.Read("original.ttf"));
    private static PdfOcrResult Result(string text = "AB", PdfRect? box = null) => new(text + "\n", new[] { new PdfOcrWord(text, box ?? new(20, 20, 40, 20), 94.5, 1, 1, 1, 1) }, "eng", "original-managed-fixture", "own-model", 94);
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    private static PdfPageGeometry Geometry(PdfPageRotation rotation = PdfPageRotation.None) => new(new(new(-20, -10, 200, 150), new(10, 20, 100, 60)), rotation, 2);
    [Theory]
    [InlineData(PdfPageRotation.None, 200, 120, 10, 80, 110, 20)]
    [InlineData(PdfPageRotation.Clockwise90, 120, 200, 10, 20, 110, 80)]
    [InlineData(PdfPageRotation.Clockwise180, 200, 120, 110, 20, 10, 80)]
    [InlineData(PdfPageRotation.Clockwise270, 120, 200, 110, 80, 10, 20)]
    public void PixelFramesHaveKnownAnswersAcrossCropOriginsUnitsAndRotations(PdfPageRotation rotation, int width, int height, double x0, double y0, double x1, double y1)
    {
        var frame = new PdfOcrPageFrame(Geometry(rotation), width, height);
        Assert.Equal(new PdfPoint(x0, y0), frame.PixelToUser.Transform(new(0, 0)));
        Assert.Equal(new PdfPoint(x1, y1), frame.PixelToUser.Transform(new(width, height)));
        Assert.Throws<ArgumentException>(() => new PdfOcrPageFrame(Geometry(), 100, 100, new(-1, 0, 10, 10)));
    }

    [Theory]
    [InlineData(PdfPageRotation.None)]
    [InlineData(PdfPageRotation.Clockwise90)]
    [InlineData(PdfPageRotation.Clockwise180)]
    [InlineData(PdfPageRotation.Clockwise270)]
    public void InvisibleOverlayHasExactDisplayedWordFrameAndLeavesOriginalUnchanged(PdfPageRotation rotation)
    {
        using var source = Pdf.Create().AddPage(Geometry(rotation));
        using var imageInput = new MemoryStream(ImageAssetTests.Fixture("rgb.png"));
        var image = PdfImages.Read(imageInput);
        PdfCanvas.Create(source.Pages[0].Geometry, canvas => canvas.DrawImageAsset(image, source.Pages[0].Geometry.DisplayBounds(), PdfCanvasFit.Stretch)).ApplyTo(source.Pages[0]);
        var snapshot = source.CreateSnapshot();
        var frame = new PdfOcrPageFrame(snapshot.Pages[0].Geometry, 200, 120, new(5, 8, 80, 50));
        using var searchable = PdfOcrSearchable.Apply(snapshot, 0, Result(), frame, Fonts(), Providers());
        Assert.Null(source.Pages[0].Content);
        Assert.Single(source.CreateSnapshot().Pages[0].CanvasLayers);
        var extracted = PdfTextExtraction.ReadPage(searchable.CreateSnapshot(), 0, providers: Providers());
        Assert.Equal("AB", extracted.Text.Trim());
        Assert.All(extracted.Characters, ch => Assert.Equal(3, ch.RenderingMode));
        var box = Assert.Single(extracted.Words).Bounds;
        Assert.Equal(13, box.X, 7);
        Assert.Equal(8 + 20 * 50d / 120, box.Y, 7);
        Assert.Equal(16, box.Width, 7);
        Assert.Equal(20 * 50d / 120, box.Height, 7);
        Assert.Empty(PdfTextExtraction.ReadPage(searchable.CreateSnapshot(), 0, new(includeInvisibleText: false), Providers()).Characters);
        using var output = new MemoryStream();
        searchable.Save(output);
        Assert.True(output.CanWrite);
        string directory = Path.Combine(AppContext.BaseDirectory, "ocr-searchable-original");
        Directory.CreateDirectory(directory);
        using var before = new MemoryStream();
        source.Save(before);
        File.WriteAllBytes(Path.Combine(directory, "source-rotation-" + ((int)rotation).ToString(CultureInfo.InvariantCulture) + ".pdf"), before.ToArray());
        File.WriteAllBytes(Path.Combine(directory, "rotation-" + ((int)rotation).ToString(CultureInfo.InvariantCulture) + ".pdf"), output.ToArray());
    }

    [Fact]
    public void ExistingMappedWordsAreSkippedOrRejectedAndExplicitDuplicatePolicyIsAvailable()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(100, 60));
        var frame = new PdfOcrPageFrame(source.Pages[0].Geometry, 100, 60);
        using var first = PdfOcrSearchable.Apply(source.CreateSnapshot(), 0, Result(), frame, Fonts(), Providers());
        using var skipped = PdfOcrSearchable.Apply(first.CreateSnapshot(), 0, Result(), frame, Fonts(), Providers());
        Assert.Equal(2, PdfTextExtraction.ReadPage(skipped.CreateSnapshot(), 0, providers: Providers()).Characters.Count);
        Assert.Contains(skipped.LastOperationReport!.Diagnostics, d => d.Code == "Ocr.ExistingTextRetained");
        Assert.Equal("Ocr.OverlayExistingText", Error(() => PdfOcrSearchable.Apply(first.CreateSnapshot(), 0, Result(), frame, Fonts(), Providers(), new(PdfOcrExistingTextPolicy.RejectOverlap))));
        using var doubled = PdfOcrSearchable.Apply(first.CreateSnapshot(), 0, Result(), frame, Fonts(), Providers(), new(PdfOcrExistingTextPolicy.AllowDuplicates));
        Assert.Equal(4, PdfTextExtraction.ReadPage(doubled.CreateSnapshot(), 0, providers: Providers()).Characters.Count);
        var mixed = new PdfOcrResult("AB AB", new[] { Result().Words[0], new PdfOcrWord("AB", new(65, 20, 30, 20), 90, 1, 1, 1, 2) }, "eng", "own", "own");
        using var partial = PdfOcrSearchable.Apply(first.CreateSnapshot(), 0, mixed, frame, Fonts(), Providers());
        Assert.Equal(4, PdfTextExtraction.ReadPage(partial.CreateSnapshot(), 0, providers: Providers()).Characters.Count);
    }

    [Fact]
    public void OverlayRejectsMismatchedGeometryOutOfBoundsWorkLimitsAndCancellation()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(100, 60));
        var frame = new PdfOcrPageFrame(source.Pages[0].Geometry, 100, 60);
        Assert.Equal("Ocr.OverlayFrameMismatch", Error(() => PdfOcrSearchable.Apply(source.CreateSnapshot(), 0, Result(), new(Geometry(), 100, 60), Fonts(), Providers())));
        Assert.Equal("Ocr.WordGeometry", Error(() => PdfOcrSearchable.Apply(source.CreateSnapshot(), 0, Result(box: new(90, 20, 20, 10)), frame, Fonts(), Providers())));
        Assert.Equal("Ocr.OverlayWordLayout", Error(() => PdfOcrSearchable.Apply(source.CreateSnapshot(), 0, Result("A\nB"), frame, Fonts(), Providers())));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfOcrSearchable.Apply(source.CreateSnapshot(), 0, Result(), frame, Fonts(), Providers(), cancellationToken: cancellation.Token));
        Assert.StartsWith("Limit.", Error(() => PdfOcrSearchable.Apply(source.CreateSnapshot(), 0, Result(), frame, Fonts(), Providers(), policy: new(limits: new(maximumExpandedBytes: 1)))));
        using var first = PdfOcrSearchable.Apply(source.CreateSnapshot(), 0, Result(), frame, Fonts(), Providers());
        var many = new PdfOcrResult("AB", new[] { Result(box: new(65, 1, 10, 10)).Words[0], Result(box: new(80, 1, 10, 10)).Words[0] }, "eng", "own", "own");
        Assert.Equal("Ocr.OverlayOverlapLimit", Error(() => PdfOcrSearchable.Apply(first.CreateSnapshot(), 0, many, frame, Fonts(), Providers(), new(maximumOverlapComparisons: 1))));
    }

    [Fact]
    public void TaggedSourceRequiresExplicitStructureAssociationInsteadOfInventingAccessibility()
    {
        using var source = NavigationTests.Fixture((edit, root, pages) => NavigationTests.Put(edit, root, "StructTreeRoot", NavigationTests.D(("Type", PdfValue.Name("StructTreeRoot")))));
        var frame = new PdfOcrPageFrame(source.Pages[0].Geometry, 100, 60);
        Assert.Equal("Structure.SourceEditPolicy", Error(() => PdfOcrSearchable.Apply(source.CreateSnapshot(), 0, Result(), frame, Fonts(), Providers())));
    }

    [Theory]
    [InlineData(PdfOcrOutputFormat.PlainText)]
    [InlineData(PdfOcrOutputFormat.Json)]
    [InlineData(PdfOcrOutputFormat.HOcr)]
    [InlineData(PdfOcrOutputFormat.AltoXml)]
    public void InterchangeEscapesUnicodeAndUsesExplicitPixelGeometry(PdfOcrOutputFormat format)
    {
        var result = Result("A<&\"😀", new(1.2, 2.3, 20.4, 10.2));
        using var output = new MemoryStream();
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            PdfOcrOutput.Write(result, 100, 60, format, output);
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
        }

        Assert.True(output.CanWrite);
        var bytes = output.ToArray();
        Assert.False(bytes.Take(3).SequenceEqual(new byte[] { 239, 187, 191 }));
        string text = Encoding.UTF8.GetString(bytes);
        if (format == PdfOcrOutputFormat.PlainText)
            Assert.Equal(result.Text, text);
        else if (format == PdfOcrOutputFormat.Json)
        {
            Assert.Contains("\"coordinateFrame\":\"top-left-pixels\"", text);
            Assert.Contains("94.5", text);
            Assert.Contains("1.2", text);
        }
        else
        {
            var xml = XDocument.Parse(text);
            Assert.Contains(result.Words[0].Text, xml.Descendants().Where(e => e.Name.LocalName == "span").Select(e => e.Value).Concat(xml.Descendants().Attributes("CONTENT").Select(a => a.Value)));
            if (format == PdfOcrOutputFormat.HOcr)
                Assert.Contains("bbox 1 2 22 13; x_wconf 95", text);
            else
            {
                Assert.Contains("WC=\"0.945\"", text);
                Assert.Contains("MeasurementUnit", text);
            }
        }

        string directory = Path.Combine(AppContext.BaseDirectory, "ocr-searchable-original");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "interchange-" + format + ".out"), bytes);
    }

    [Fact]
    public void XmlForbiddenControlsAndOutputBudgetsCannotCommitPartialFiles()
    {
        using var stream = new MemoryStream();
        Assert.Equal("Ocr.ExportXmlCharacter", Error(() => PdfOcrOutput.Write(Result("A\u0001"), 100, 60, PdfOcrOutputFormat.AltoXml, stream)));
        Assert.Equal(0, stream.Length);
        Assert.True(stream.CanWrite);
        string directory = Path.Combine(Path.GetTempPath(), "fluent-pdf-ocr-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "owned.json");
        File.WriteAllText(path, "retained");
        try
        {
            Assert.StartsWith("Limit.", Error(() => PdfOcrOutput.WriteFile(Result(), 100, 60, PdfOcrOutputFormat.Json, path, new(limits: new(maximumOutputBytes: 1)))));
            Assert.Equal("retained", File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(directory));
            PdfOcrOutput.WriteFile(Result(), 100, 60, PdfOcrOutputFormat.Json, path);
            Assert.Contains("\"version\":1", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExecutableLanguageConsumersApplyInvisibleTextAndExportJson(bool visualBasic)
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(100, 60));
        var frame = new PdfOcrPageFrame(source.Pages[0].Geometry, 100, 60);
        var recognized = Result();
        using var result = visualBasic ? ContractExamples.VisualBasic.ContractExample.ApplyOcrLayer(source.CreateSnapshot(), recognized, frame, Fonts(), Providers()) : ContractExamples.CSharp.ContractExample.ApplyOcrLayer(source.CreateSnapshot(), recognized, frame, Fonts(), Providers());
        Assert.Equal("AB", PdfTextExtraction.ReadPage(result.CreateSnapshot(), 0, providers: Providers()).Text.Trim());
        using var output = new MemoryStream();
        var report = visualBasic ? ContractExamples.VisualBasic.ContractExample.WriteOcrJson(recognized, 100, 60, output) : ContractExamples.CSharp.ContractExample.WriteOcrJson(recognized, 100, 60, output);
        Assert.True(report.Succeeded);
        Assert.True(output.CanWrite);
        Assert.Contains("AB", Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public void NewWorkflowsInheritTheSourceOperationPolicyWithoutResettingBudgets()
    {
        using var source = Pdf.Create(new PdfOptions(operationPolicy: new(limits: new(maximumInputBytes: 1)))).AddPage(new PdfPageSize(100, 60));
        var frame = new PdfOcrPageFrame(source.Pages[0].Geometry, 100, 60);
        Assert.StartsWith("Limit.", Error(() => PdfOcrSearchable.Apply(source.CreateSnapshot(), 0, Result(), frame, Fonts(), Providers())));
        using var rasterSource = Pdf.Create(new PdfOptions(operationPolicy: new(limits: new(maximumPixels: 1)))).AddPage(new PdfPageSize(100, 60));
        var provider = new RasterProvider();
        var providers = new PdfProviderRegistry(provider, new PdfSimpleTextShaper(), ManagedFontProvider.Instance);
        Assert.Equal("Limit.Pixels", Error(() => PdfOcrSearchable.RecognizePage(rasterSource.CreateSnapshot(), 0, new("eng"), Fonts(), providers, new(72, 72))));
        Assert.Null(provider.Seen);
    }

    [Fact]
    public void MissingRecognitionProviderRejectsBeforeCallingAnAvailableRenderer()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(100, 60));
        var renderer = new RendererOnly();
        var providers = new PdfProviderRegistry(renderer, new PdfSimpleTextShaper());
        Assert.Throws<PdfProviderUnavailableException>(() => PdfOcrSearchable.RecognizePage(source.CreateSnapshot(), 0, new("eng"), Fonts(), providers));
        Assert.False(renderer.Called);
    }

    private sealed class RendererOnly : IPdfPageRenderer
    {
        internal bool Called;
        public PdfProviderDescriptor Descriptor { get; } = new("own.renderer-only", "1", PdfCapability.RenderPages);

        public void Render(PdfRasterRequest request, Stream output, PdfOperationContext context) => Called = true;
    }

    private sealed class RasterProvider : IPdfPageRenderer, IPdfOcrProvider
    {
        internal PdfOperationContext? Seen;
        public PdfProviderDescriptor Descriptor { get; } = new("own.workflow-ocr", "1", PdfCapability.RenderPages, PdfCapability.RecognizeText);

        public void Render(PdfRasterRequest request, Stream output, PdfOperationContext context)
        {
            Seen = context;
            context.Consume(PdfResourceKind.ExpandedBytes, request.ByteLength);
            var pixels = new byte[(int)request.ByteLength];
            for (int i = 3; i < pixels.Length; i += 4)
                pixels[i] = 255;
            output.Write(pixels, 0, pixels.Length);
        }

        public PdfOcrResult Recognize(PdfOcrRaster raster, PdfOcrOptions options, PdfOperationContext context)
        {
            Assert.Same(Seen, context);
            Assert.Equal("RecognizeSearchablePage", context.Operation);
            using var pixels = raster.OpenPixels(context);
            Assert.Equal(0, pixels.ReadByte());
            return Result();
        }
    }

    [Fact]
    public void RenderRecognizeAndOverlayShareTheRootContextAndKeepOpaqueRasterCaptureBounded()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(100, 60));
        var provider = new RasterProvider();
        var providers = new PdfProviderRegistry(provider, new PdfSimpleTextShaper(), ManagedFontProvider.Instance);
        using var result = PdfOcrSearchable.RecognizePage(source.CreateSnapshot(), 0, new("eng"), Fonts(), providers, new(72, 72));
        Assert.Equal("AB", PdfTextExtraction.ReadPage(result.CreateSnapshot(), 0, providers: Providers()).Text.Trim());
        Assert.Throws<ObjectDisposedException>(() => provider.Seen!.Checkpoint());
        Assert.Throws<ArgumentException>(() => PdfOcrSearchable.RecognizePage(source.CreateSnapshot(), 0, new("eng"), Fonts(), providers, new(72, 72, backgroundRgb: null)));
    }
}
