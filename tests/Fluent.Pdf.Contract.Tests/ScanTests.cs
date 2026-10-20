using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ScanTests
{
    internal static PdfOcrRaster Raster(byte[] bytes, int width, int height, int? stride = null, PdfOcrPixelFormat format = PdfOcrPixelFormat.Gray8)
    {
        using var input = new MemoryStream(bytes);
        return PdfOcrRaster.Read(input, width, height, stride ?? width, format);
    }

    internal static byte[] Pixels(PdfOcrRaster raster)
    {
        using var input = raster.Bytes.Open(0, raster.ByteLength);
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    private static PdfOcrWord Word(string text, double x, double y, double width = 20, double height = 5) => new(text, new(x, y, width, height), 90, 1, 1, 1, 1);
    private static PdfOcrResult Recognition(params PdfOcrWord[] words) => new(string.Join(" ", words.Select(w => w.Text)), words, "eng", "own-managed-fixture", "own-model", 90);
    [Fact]
    public void IdentityAndGrayConversionPreserveTheImmutableOriginalAndDeclaredPadding()
    {
        var original = Raster(new byte[] { 1, 2, 99, 3, 4, 99 }, 2, 2, 3);
        var result = PdfScanPreprocessing.Process(original, new());
        Assert.Same(original, result.Original);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, Pixels(result.Processed));
        Assert.Equal(new byte[] { 1, 2, 99, 3, 4, 99 }, Pixels(original));
        Assert.Equal(4, result.PixelVisits);
        Assert.Equal(2, result.Processed.Stride);
        Assert.Empty(result.OrientationScores);
        Assert.Equal(new PdfPoint(1, 1), result.Geometry.MapPoint(new(1, 1)));
        var rgb = Raster(new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255 }, 3, 1, 9, PdfOcrPixelFormat.Rgb24);
        Assert.Equal(new byte[] { 77, 149, 29 }, Pixels(PdfScanPreprocessing.Process(rgb, new()).Processed));
        var alpha = Raster(new byte[] { 0, 0, 0, 254 }, 1, 1, 4, PdfOcrPixelFormat.Bgra32);
        Assert.Equal("Ocr.ScanAlpha", Error(() => PdfScanPreprocessing.Process(alpha, new())));
    }

    [Fact]
    public void ExplicitMedianContrastAndThresholdHaveKnownReproducibleValues()
    {
        var impulse = Enumerable.Repeat((byte)255, 25).ToArray();
        impulse[12] = 0;
        var original = Raster(impulse, 5, 5);
        Assert.All(Pixels(PdfScanPreprocessing.Process(original, new(medianDenoise: true)).Processed), value => Assert.Equal(255, value));
        Assert.Equal(0, Pixels(original)[12]);
        var levels = Raster(new byte[] { 49, 50, 75, 100, 101 }, 5, 1);
        Assert.Equal(new byte[] { 0, 0, 128, 255, 255 }, Pixels(PdfScanPreprocessing.Process(levels, new(contrastLow: 50, contrastHigh: 100)).Processed));
        Assert.Equal(new byte[] { 0, 0, 255, 255, 255 }, Pixels(PdfScanPreprocessing.Process(levels, new(contrastLow: 50, contrastHigh: 100, binaryThreshold: 128)).Processed));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfScanPreprocessingOptions(contrastLow: 100, contrastHigh: 100));
    }

    [Theory]
    [InlineData(PdfScanOrientation.None, 2, 3, new byte[] { 1, 2, 3, 4, 5, 6 }, 0.5, 0.5)]
    [InlineData(PdfScanOrientation.Clockwise90, 3, 2, new byte[] { 5, 3, 1, 6, 4, 2 }, 0.5, 2.5)]
    [InlineData(PdfScanOrientation.Clockwise180, 2, 3, new byte[] { 6, 5, 4, 3, 2, 1 }, 1.5, 2.5)]
    [InlineData(PdfScanOrientation.Clockwise270, 3, 2, new byte[] { 2, 4, 6, 1, 3, 5 }, 1.5, 0.5)]
    public void QuarterTurnsHaveKnownPixelsAndInverseEdgeCoordinates(PdfScanOrientation orientation, int width, int height, byte[] expected, double x, double y)
    {
        var result = PdfScanPreprocessing.Process(Raster(new byte[] { 1, 2, 3, 4, 5, 6 }, 2, 3), new(orientation));
        Assert.Equal(width, result.Processed.Width);
        Assert.Equal(height, result.Processed.Height);
        Assert.Equal(expected, Pixels(result.Processed));
        Assert.Equal(new PdfPoint(x, y), result.Geometry.MapPoint(new(0.5, 0.5)));
        var boundary = result.Geometry.MapWord(Word("A", 0, 0, 1, 1));
        Assert.Equal(5, boundary.Points.Count);
        Assert.Equal(1, boundary.Bounds.Width, 8);
        Assert.Equal(1, boundary.Bounds.Height, 8);
        Assert.Equal(0, boundary.MaximumErrorPixels);
    }

    [Fact]
    public void DeskewExpandsTheCanvasAndRetainsAnExactInverseRatherThanClippingTheSource()
    {
        var original = Raster(Enumerable.Repeat((byte)255, 4000).ToArray(), 100, 40);
        var result = PdfScanPreprocessing.Process(original, new(skewDegrees: 10));
        Assert.Equal(106, result.Processed.Width);
        Assert.Equal(57, result.Processed.Height);
        var center = result.Geometry.MapPoint(new(53, 28.5));
        Assert.Equal(50, center.X, 9);
        Assert.Equal(20, center.Y, 9);
        foreach (var point in new[]
        {
            new PdfPoint(0, 0),
            new PdfPoint(100, 40),
            new PdfPoint(13, 27)
        }

        )
        {
            var processed = result.Geometry.ProcessedToDewarped.Invert().Transform(point);
            var mapped = result.Geometry.MapPoint(processed);
            Assert.Equal(point.X, mapped.X, 8);
            Assert.Equal(point.Y, mapped.Y, 8);
        }

        Assert.Equal("Ocr.ScanGeometry", Error(() => result.Geometry.MapWord(Word("A", 0, 0, 1, 1))));
    }

    [Fact]
    public void BoundedProjectionAnalysisFindsLabeledSkewAndReportsBlankAmbiguity()
    {
        var pixels = Enumerable.Repeat((byte)255, 20000).ToArray();
        foreach (int line in new[]
        {
            15,
            35,
            55,
            75
        }

        )
            for (int x = 10; x < 190; x++)
            {
                int y = (int)Math.Round(line + 0.05 * x, MidpointRounding.AwayFromZero);
                pixels[y * 200 + x] = 0;
            }

        var original = Raster(pixels, 200, 100);
        var result = PdfScanPreprocessing.Process(original, new(detectSkew: true, analysisStride: 1, minimumSkewMargin: 0));
        double expected = Math.Atan(0.05) * 180 / Math.PI;
        Assert.InRange(Math.Abs(result.SourceSkewDegrees - expected), 0, 0.5);
        Assert.NotNull(result.SkewMargin);
        var blank = Raster(Enumerable.Repeat((byte)255, 400).ToArray(), 20, 20);
        Assert.Equal("Ocr.ScanSkewAmbiguous", Error(() => PdfScanPreprocessing.Process(blank, new(detectSkew: true))));
        var accepted = PdfScanPreprocessing.Process(blank, new(detectSkew: true, minimumSkewMargin: 0));
        Assert.Equal(0, accepted.SourceSkewDegrees);
        Assert.Equal(0, accepted.SkewMargin);
        Assert.Equal(Pixels(blank), Pixels(accepted.Processed));
    }

    private sealed class OrientationProvider : IPdfOcrProvider
    {
        internal readonly List<PdfOperationContext> Contexts = new();
        internal bool Ambiguous;
        internal bool HideLimit;
        public PdfProviderDescriptor Descriptor { get; } = new("own.scan-orientation", "1", PdfCapability.RecognizeText);

        public PdfOcrResult Recognize(PdfOcrRaster raster, PdfOcrOptions options, PdfOperationContext context)
        {
            Contexts.Add(context);
            if (HideLimit)
            {
                try
                {
                    context.Consume(PdfResourceKind.Objects, 101);
                }
                catch (PdfException)
                {
                }
            }

            using var pixels = raster.OpenPixels(context);
            int value = pixels.ReadByte();
            return new("", Array.Empty<PdfOcrWord>(), options.Language, "own-orientation-observation", "own-model", Ambiguous ? 50 : value == 0 ? 98 : 20);
        }
    }

    [Fact]
    public void AutomaticOrientationUsesFourExplicitSameContextObservationsAndCannotHideAmbiguity()
    {
        var bytes = Enumerable.Repeat((byte)255, 12).ToArray();
        bytes[8] = 0;
        var original = Raster(bytes, 4, 3);
        var provider = new OrientationProvider();
        var result = PdfScanPreprocessing.Process(original, new(PdfScanOrientation.Automatic), new(provider), new("eng"));
        Assert.Equal(PdfScanOrientation.Clockwise90, result.AppliedOrientation);
        Assert.Equal(0.78, result.OrientationMargin);
        Assert.Equal(4, result.OrientationScores.Count);
        Assert.Equal(4, provider.Contexts.Count);
        Assert.All(provider.Contexts, c => Assert.Same(provider.Contexts[0], c));
        Assert.Throws<ObjectDisposedException>(() => provider.Contexts[0].Checkpoint());
        provider.Ambiguous = true;
        Assert.Equal("Ocr.ScanOrientationAmbiguous", Error(() => PdfScanPreprocessing.Process(original, new(PdfScanOrientation.Automatic), new(provider), new("eng"))));
        Assert.Throws<ArgumentException>(() => PdfScanPreprocessing.Process(original, new(PdfScanOrientation.Automatic)));
        provider.Ambiguous = false;
        provider.HideLimit = true;
        Assert.Equal("Limit.Objects", Error(() => PdfScanPreprocessing.Process(original, new(PdfScanOrientation.Automatic), new(provider), new("eng"), new(limits: new(maximumObjects: 100)))));
    }

    internal static PdfScanDewarpGrid CurvedGrid() => new(4, 4, 4, 4, 3, 2, new[] { new PdfPoint(0, 0), new PdfPoint(2, 1), new PdfPoint(4, 0), new PdfPoint(0, 3), new PdfPoint(2, 4), new PdfPoint(4, 3) });
    [Fact]
    public void ExplicitDewarpingHasKnownPixelsAndBoundaryMappingIncludesCurveExtrema()
    {
        var original = Raster(Enumerable.Range(0, 16).Select(n => (byte)n).ToArray(), 4, 4);
        var grid = CurvedGrid();
        var result = PdfScanPreprocessing.Process(original, new(dewarp: grid));
        Assert.Equal(new byte[] { 0, 5, 6, 3, 4, 5, 6, 7, 8, 9, 10, 11, 8, 13, 14, 11 }, Pixels(result.Processed));
        Assert.Equal(new PdfPoint(2, 1), grid.MapPoint(new(2, 0)));
        Assert.Equal(new PdfPoint(1, 2), grid.MapPoint(new(1, 2)));
        var boundary = result.Geometry.MapWord(Word("A", 0, 0, 4, 4));
        Assert.Contains(new PdfPoint(2, 4), boundary.Points);
        Assert.Equal(4, boundary.Bounds.Height, 8);
        Assert.InRange(boundary.MaximumErrorPixels, 0, 0.25);
        Assert.Equal("Ocr.ScanGeometryLimit", Error(() => result.Geometry.MapWord(Word("A", 0, 0, 4, 4), maximumPoints: 5)));
        Assert.Equal(Pixels(original), Enumerable.Range(0, 16).Select(n => (byte)n).ToArray());
        Assert.Throws<ArgumentException>(() => new PdfScanDewarpGrid(4, 4, 4, 4, 2, 2, new[] { new PdfPoint(0, 0), new PdfPoint(4, 0), new PdfPoint(1, 4), new PdfPoint(4, 4) }));
        Assert.Equal("Ocr.ScanDewarpGeometry", Error(() => PdfScanPreprocessing.Process(Raster(new byte[4], 2, 2), new(dewarp: grid))));
    }

    [Fact]
    public void NonlinearRotatedBoundarySubdivisionHonorsThePriorErrorEnvelopeAndFinitePointLimit()
    {
        var scan = PdfScanPreprocessing.Process(Raster(new byte[16], 4, 4), new(skewDegrees: 10, dewarp: CurvedGrid()));
        var word = Word("A", 2, 2, 0.5, 0.5);
        var boundary = scan.Geometry.MapWord(word, 0.00001);
        Assert.InRange(boundary.MaximumErrorPixels, 0, 0.00001);
        var corners = new[]
        {
            new PdfPoint(2, 2),
            new PdfPoint(2.5, 2),
            new PdfPoint(2.5, 2.5),
            new PdfPoint(2, 2.5)
        };
        for (int edge = 0; edge < 4; edge++)
            for (int n = 0; n <= 100; n++)
            {
                var a = corners[edge];
                var b = corners[(edge + 1) % 4];
                double t = n / 100d;
                var mapped = scan.Geometry.MapPoint(new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
                double nearest = double.MaxValue;
                for (int i = 1; i < boundary.Points.Count; i++)
                    nearest = Math.Min(nearest, Distance(mapped, boundary.Points[i - 1], boundary.Points[i]));
                Assert.True(nearest <= boundary.MaximumErrorPixels + 1e-8);
            }

        Assert.Equal("Ocr.ScanGeometryLimit", Error(() => scan.Geometry.MapWord(word, double.Epsilon)));
    }

    private static double Distance(PdfPoint point, PdfPoint a, PdfPoint b)
    {
        double x = b.X - a.X, y = b.Y - a.Y, length = x * x + y * y;
        double t = length == 0 ? 0 : Math.Max(0, Math.Min(1, ((point.X - a.X) * x + (point.Y - a.Y) * y) / length));
        return Math.Sqrt(Math.Pow(point.X - a.X - t * x, 2) + Math.Pow(point.Y - a.Y - t * y, 2));
    }

    [Fact]
    public void ScanWorkPixelAndCancellationBoundsRemainSharedAndFatal()
    {
        var original = Raster(new byte[6], 2, 3);
        Assert.Equal(6, PdfScanPreprocessing.Process(original, new(maximumPixelVisits: 6)).PixelVisits);
        Assert.Equal("Ocr.ScanWorkLimit", Error(() => PdfScanPreprocessing.Process(original, new(maximumPixelVisits: 5))));
        Assert.Equal("Ocr.ScanWorkLimit", Error(() => PdfScanPreprocessing.Process(original, new(PdfScanOrientation.Clockwise90, maximumPixelVisits: 6))));
        Assert.Equal("Limit.Pixels", Error(() => PdfScanPreprocessing.Process(original, new(), policy: new(limits: new(maximumPixels: 11)))));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfScanPreprocessing.Process(original, new(), cancellationToken: cancel.Token));
        Assert.Equal(new byte[6], Pixels(original));
    }

    private static PdfOcrResult Columns() => Recognition(Word("L1", 20, 20), Word("R1", 100, 20), Word("L2", 20, 40), Word("R2", 100, 40));
    [Theory]
    [InlineData(false, new int[] { 0, 2, 1, 3 })]
    [InlineData(true, new int[] { 1, 3, 0, 2 })]
    public void ProseColumnOrderIsExplicitAndDoesNotOverwriteRawEngineOrder(bool rtl, int[] expected)
    {
        var original = Columns();
        var result = PdfScanLayout.Analyze(original, 160, 100, new(rightToLeftColumns: rtl));
        Assert.Equal(expected, result.ProseReadingOrder);
        Assert.Empty(result.NonProseWordIndices);
        Assert.Same(original, result.Recognition);
        Assert.Equal(new[] { "L1", "R1", "L2", "R2" }, result.Recognition.Words.Select(w => w.Text));
        Assert.Equal(2, result.Regions.Count);
    }

    [Fact]
    public void SpanningHeadingsAndFootersBoundTheColumnReadingBands()
    {
        var input = Recognition(Word("heading", 10, 0, 120), Word("L1", 20, 20), Word("R1", 100, 20), Word("L2", 20, 40), Word("R2", 100, 40), Word("footer", 10, 60, 120));
        var result = PdfScanLayout.Analyze(input, 160, 100, new(spanningHeightFraction: 0.2));
        Assert.Equal(new[] { 0, 1, 3, 2, 4, 5 }, result.ProseReadingOrder);
        Assert.Contains(result.Regions, r => r.ColumnIndex == null && r.Kind == PdfScanRegionKind.Prose);
    }

    [Theory]
    [InlineData(PdfScanRegionKind.Table)]
    [InlineData(PdfScanRegionKind.Form)]
    public void ExplicitTableAndFormAuthorityWithholdsProseCellOrdering(PdfScanRegionKind kind)
    {
        var result = PdfScanLayout.Analyze(Columns(), 160, 100, new(regions: new[] { new PdfScanRegionHint(kind, new(10, 10, 130, 50), 0.9) }));
        Assert.Empty(result.ProseReadingOrder);
        Assert.Equal(new[] { 0, 1, 2, 3 }, result.NonProseWordIndices);
        var region = Assert.Single(result.Regions);
        Assert.Equal(kind, region.Kind);
        Assert.Equal("caller-declared", region.Provenance);
        Assert.Equal(0.9, region.Confidence);
    }

    [Fact]
    public void LiteralClosedGridsRemainUnresolvedTableOrFormRatherThanProseColumns()
    {
        var pixels = Enumerable.Repeat((byte)255, 16000).ToArray();
        foreach (int y in new[]
        {
            10,
            40,
            70
        }

        )
            for (int x = 10; x <= 140; x++)
                pixels[y * 160 + x] = 0;
        foreach (int x in new[]
        {
            10,
            70,
            140
        }

        )
            for (int y = 10; y <= 70; y++)
                pixels[y * 160 + x] = 0;
        var result = PdfScanLayout.Analyze(Columns(), 160, 100, raster: Raster(pixels, 160, 100));
        Assert.Empty(result.ProseReadingOrder);
        Assert.Equal(new[] { 0, 1, 2, 3 }, result.NonProseWordIndices);
        Assert.Equal(PdfScanRegionKind.RuledTableOrForm, Assert.Single(result.Regions).Kind);
    }

    [Fact]
    public void LayoutBoundariesAndAuthorityCannotSilentlyFallbackOrCrossRegions()
    {
        Assert.Equal("Ocr.LayoutWordLimit", Error(() => PdfScanLayout.Analyze(Columns(), 160, 100, new(maximumWords: 1))));
        Assert.Equal("Ocr.LayoutColumnLimit", Error(() => PdfScanLayout.Analyze(Columns(), 160, 100, new(maximumColumns: 1))));
        Assert.Equal("Ocr.LayoutWorkLimit", Error(() => PdfScanLayout.Analyze(Columns(), 160, 100, new(maximumAnalysisWork: 1))));
        Assert.Equal("Ocr.LayoutRegionCrossing", Error(() => PdfScanLayout.Analyze(Columns(), 160, 100, new(regions: new[] { new PdfScanRegionHint(PdfScanRegionKind.Form, new(22, 10, 100, 50)) }))));
        Assert.Throws<ArgumentException>(() => new PdfScanLayoutOptions(regions: new[] { new PdfScanRegionHint(PdfScanRegionKind.Table, new(0, 0, 100, 100)), new PdfScanRegionHint(PdfScanRegionKind.Form, new(50, 50, 100, 100)) }));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfScanLayout.Analyze(Columns(), 160, 100, cancellationToken: cancel.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExecutableLanguageConsumersPreserveScanSamplesAndInferExplicitColumnOrder(bool visualBasic)
    {
        var original = Raster(new byte[] { 1, 2, 3, 4, 5, 6 }, 2, 3);
        var scan = visualBasic ? ContractExamples.VisualBasic.ContractExample.PreprocessScan(original, new(PdfScanOrientation.Clockwise90)) : ContractExamples.CSharp.ContractExample.PreprocessScan(original, new(PdfScanOrientation.Clockwise90));
        Assert.Equal(new byte[] { 5, 3, 1, 6, 4, 2 }, Pixels(scan.Processed));
        Assert.Same(original, scan.Original);
        var layout = visualBasic ? ContractExamples.VisualBasic.ContractExample.AnalyzeScanLayout(Columns(), 160, 100, new()) : ContractExamples.CSharp.ContractExample.AnalyzeScanLayout(Columns(), 160, 100, new());
        Assert.Equal(new[] { 0, 2, 1, 3 }, layout.ProseReadingOrder);
    }

    [Fact]
    public async System.Threading.Tasks.Task IndependentScanJobsRetainSeparateSamplesOptionsAndReports()
    {
        var original = Raster(Enumerable.Range(0, 64).Select(n => (byte)n).ToArray(), 8, 8);
        var jobs = Enumerable.Range(0, 8).Select(n => System.Threading.Tasks.Task.Run(() => PdfScanPreprocessing.Process(original, new(n % 2 == 0 ? PdfScanOrientation.None : PdfScanOrientation.Clockwise180)))).ToArray();
        var results = await System.Threading.Tasks.Task.WhenAll(jobs);
        for (int i = 0; i < results.Length; i++)
        {
            Assert.Same(original, results[i].Original);
            Assert.NotSame(results[0].Processed, results[i == 0 ? 1 : i].Processed);
            Assert.Equal(i % 2 == 0 ? (byte)0 : (byte)63, Pixels(results[i].Processed)[0]);
        }

        Assert.Equal(Enumerable.Range(0, 64).Select(n => (byte)n).ToArray(), Pixels(original));
    }

    private sealed class PipelineProvider : IPdfOcrProvider
    {
        internal PdfOperationContext? Seen;
        public PdfProviderDescriptor Descriptor { get; } = new("own.scan-pipeline", "1", PdfCapability.RecognizeText);

        public PdfOcrResult Recognize(PdfOcrRaster raster, PdfOcrOptions options, PdfOperationContext c)
        {
            Seen = c;
            Assert.Equal("RecognizeScan", c.Operation);
            using var pixels = raster.OpenPixels(c);
            Assert.Equal(255, pixels.ReadByte());
            return Recognition(Word("A", 1, 1, 2, 2));
        }
    }

    [Fact]
    public void PreprocessRecognitionAndLayoutUseOneContextWithOriginalGeometryRetained()
    {
        var source = Raster(Enumerable.Repeat((byte)255, 200).ToArray(), 20, 10);
        var provider = new PipelineProvider();
        var result = PdfScanPreprocessing.Recognize(source, new(), new("eng"), new(provider));
        Assert.Same(source, result.Scan.Original);
        Assert.Same(result.OperationReport, result.Scan.OperationReport);
        Assert.Same(result.OperationReport, result.Recognition.OperationReport);
        Assert.Same(result.OperationReport, result.Layout.OperationReport);
        Assert.Equal(new[] { 0 }, result.Layout.ProseReadingOrder);
        Assert.Throws<ObjectDisposedException>(() => provider.Seen!.Checkpoint());
    }
}
