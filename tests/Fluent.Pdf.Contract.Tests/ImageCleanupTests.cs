using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ImageCleanupTests
{
    private static readonly PdfOperationPolicy Lossy = new(PdfProcessingMode.Permissive);
    private static PdfImageCleanupResult Process(PdfOcrRaster raster, PdfImageCleanupOptions options) => PdfImageCleanup.Process(raster, options, Lossy);
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;

    [Fact]
    public void DefaultsPreserveTheExactSourceIncludingPaddingAndReport()
    {
        var raster = ScanTests.Raster(new byte[] { 255, 17, 255, 18 }, 1, 2, 2);
        var capture = raster.OperationReport;
        var result = PdfImageCleanup.Process(raster, new());
        Assert.Same(raster, result.Original); Assert.Same(raster, result.Processed);
        Assert.Same(capture, raster.OperationReport); Assert.Null(result.Before); Assert.Null(result.After);
        Assert.Equal(0, result.PixelVisits); Assert.Equal(PdfMatrix.Identity, result.OutputToOriginal);
        Assert.Equal(new byte[] { 255, 17, 255, 18 }, ScanTests.Pixels(raster));
    }

    [Fact]
    public void AllLossyOptionsRequireExplicitAuthorityAndStrictPolicyStillRejects()
    {
        Assert.Throws<ArgumentException>(() => new PdfImageCleanupOptions(grayscale: true));
        Assert.Throws<ArgumentException>(() => new PdfImageCleanupOptions(medianDenoise: true));
        Assert.Throws<ArgumentException>(() => new PdfImageCleanupOptions(binaryThreshold: 128));
        Assert.Throws<ArgumentException>(() => new PdfImageCleanupOptions(cropLeft: 1));
        Assert.Throws<ArgumentException>(() => new PdfImageCleanupOptions(outputWidth: 2, outputHeight: 2));
        var raster = ScanTests.Raster(new byte[] { 127, 128 }, 2, 1);
        Assert.Equal("Cleanup.ExplicitLoss", Error(() => PdfImageCleanup.Process(raster, new(true, binaryThreshold: 128))));
        var result = Process(raster, new(true, binaryThreshold: 128));
        Assert.Equal(new byte[] { 0, 255 }, ScanTests.Pixels(result.Processed));
        Assert.Contains(result.OperationReport.Diagnostics, d => d.Code == "Cleanup.ExplicitLoss" && d.LossCategory == PdfLossCategory.FidelityReduction);
    }

    [Fact]
    public void InvalidParametersAndEmptyCropsReject()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfBlankPageOptions(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfBlankPageOptions(maximumForegroundFraction: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfBlankPageOptions(maximumForegroundFraction: double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfImageCleanupOptions(true, outputWidth: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfImageCleanupOptions(true, cropRight: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfImageCleanupOptions(maximumPixelVisits: 0));
        Assert.Equal("Cleanup.Crop", Error(() => Process(ScanTests.Raster(new byte[] { 1 }, 1, 1), new(true, cropLeft: 1))));
    }

    [Fact]
    public void CropAndNearestNeighborUsePixelCentersAndRetainInverseGeometry()
    {
        var original = ScanTests.Raster(Enumerable.Range(0, 20).Select(x => (byte)x).ToArray(), 5, 4);
        var result = Process(original, new(true, cropLeft: 1, cropTop: 1, cropRight: 1, cropBottom: 1, outputWidth: 2, outputHeight: 1));
        Assert.Equal(new byte[] { 11, 13 }, ScanTests.Pixels(result.Processed));
        Assert.Equal(new PdfMatrix(1.5, 0, 0, 2, 1, 1), result.OutputToOriginal);
        Assert.Equal(original.Dpi, result.Processed.Dpi); Assert.Same(original, result.Original);
        Assert.Equal(Enumerable.Range(0, 20).Select(x => (byte)x).ToArray(), ScanTests.Pixels(original));
        var up = Process(ScanTests.Raster(new byte[] { 10, 20 }, 2, 1), new(true, outputWidth: 4, outputHeight: 1));
        Assert.Equal(new byte[] { 10, 10, 20, 20 }, ScanTests.Pixels(up.Processed));
    }

    [Theory]
    [InlineData(PdfOcrPixelFormat.Rgb24, new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255 })]
    [InlineData(PdfOcrPixelFormat.Bgra32, new byte[] { 0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255 })]
    public void ColorConversionIsExplicitAndColorResamplingPreservesChannels(PdfOcrPixelFormat format, byte[] bytes)
    {
        var raster = ScanTests.Raster(bytes, 3, 1, bytes.Length, format);
        Assert.Equal("Cleanup.ColorAuthority", Error(() => Process(raster, new(true, medianDenoise: true))));
        Assert.Equal(new byte[] { 77, 149, 29 }, ScanTests.Pixels(Process(raster, new(true, grayscale: true)).Processed));
        int channels = bytes.Length / 3;
        var resized = Process(raster, new(true, outputWidth: 1, outputHeight: 1)).Processed;
        Assert.Equal(format, resized.PixelFormat); Assert.Equal(bytes.Skip(channels).Take(channels), ScanTests.Pixels(resized));
    }

    [Fact]
    public void AlphaIsPreservedByGeometryButNeverGuessedByBlankOrGrayAnalysis()
    {
        var original = ScanTests.Raster(new byte[] { 1, 2, 3, 4 }, 1, 1, 4, PdfOcrPixelFormat.Bgra32);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 1, 2, 3, 4 }, ScanTests.Pixels(Process(original, new(true, outputWidth: 2, outputHeight: 1)).Processed));
        Assert.Equal("Cleanup.Alpha", Error(() => PdfImageCleanup.Process(original, new(blankDetection: new()))));
        Assert.Equal("Ocr.ScanAlpha", Error(() => Process(original, new(true, grayscale: true))));
    }

    [Fact]
    public void BlankClassifierUsesEveryActivePixelAndReportsUncalibratedConfidence()
    {
        var bytes = Enumerable.Repeat((byte)255, 100).ToArray(); bytes[99] = 0;
        var original = ScanTests.Raster(bytes, 10, 10);
        var options = new PdfBlankPageOptions(maximumForegroundFraction: 0.01);
        var result = PdfImageCleanup.Process(original, new(blankDetection: options));
        Assert.Same(result.Before, result.After); Assert.True(result.Before!.IsBlank);
        Assert.Equal(1, result.Before.ForegroundPixels); Assert.Equal(100, result.Before.PixelCount);
        Assert.Equal(0.99, result.Before.Confidence, 10); Assert.Equal(100, result.PixelVisits);
        Assert.False(PdfImageCleanup.Process(original, new(blankDetection: new(maximumForegroundFraction: 0))).Before!.IsBlank);
        var padded = ScanTests.Raster(new byte[] { 255, 0, 255, 0 }, 1, 2, 2);
        Assert.True(PdfImageCleanup.Process(padded, new(blankDetection: new())).Before!.IsBlank);
    }

    [Fact]
    public void ColoredAndNearWhitePagesNeedExplicitBackgroundAndTolerance()
    {
        var colored = ScanTests.Raster(new byte[] { 10, 20, 30, 10, 20, 43 }, 2, 1, 6, PdfOcrPixelFormat.Rgb24);
        Assert.False(PdfImageCleanup.Process(colored, new(blankDetection: new())).Before!.IsBlank);
        Assert.False(PdfImageCleanup.Process(colored, new(blankDetection: new(0x0a141e))).Before!.IsBlank);
        Assert.True(PdfImageCleanup.Process(colored, new(blankDetection: new(0x0a141e, 13))).Before!.IsBlank);
        Assert.True(PdfImageCleanup.Process(ScanTests.Raster(new byte[] { 243 }, 1, 1), new(blankDetection: new())).Before!.IsBlank);
        Assert.False(PdfImageCleanup.Process(ScanTests.Raster(new byte[] { 242 }, 1, 1), new(blankDetection: new())).Before!.IsBlank);
    }

    [Fact]
    public void LossThatErasesFaintContentDoesNotOverwriteTheSourceObservation()
    {
        var raster = ScanTests.Raster(new byte[] { 220, 255, 255, 255 }, 2, 2);
        var result = Process(raster, new(true, binaryThreshold: 200, blankDetection: new()));
        Assert.False(result.Before!.IsBlank); Assert.True(result.After!.IsBlank);
        Assert.Equal(1, result.Before.ForegroundPixels); Assert.Equal(0, result.After.ForegroundPixels);
        Assert.Equal(16, result.PixelVisits); Assert.Equal(220, ScanTests.Pixels(result.Original)[0]);
    }

    [Fact]
    public void MedianRemovesIsolatedNoiseWithExplicitLossAndNoSourceMutation()
    {
        var bytes = Enumerable.Repeat((byte)255, 25).ToArray(); bytes[12] = 0;
        var result = Process(ScanTests.Raster(bytes, 5, 5), new(true, medianDenoise: true, blankDetection: new()));
        Assert.False(result.Before!.IsBlank); Assert.True(result.After!.IsBlank);
        Assert.Equal(300, result.PixelVisits); Assert.All(ScanTests.Pixels(result.Processed), b => Assert.Equal(255, b));
        Assert.Equal(bytes, ScanTests.Pixels(result.Original));
    }

    [Fact]
    public void PixelByteVisitAndCancellationLimitsRejectWithoutChangingSource()
    {
        var raster = ScanTests.Raster(new byte[] { 1, 2, 3, 4 }, 2, 2);
        Assert.Equal("Cleanup.WorkLimit", Error(() => Process(raster, new(blankDetection: new(), maximumPixelVisits: 3))));
        Assert.Equal("Cleanup.WorkLimit", Error(() => Process(raster, new(true, grayscale: true, blankDetection: new(), maximumPixelVisits: 4))));
        Assert.Equal("Ocr.ScanWorkLimit", Error(() => Process(raster, new(true, medianDenoise: true, maximumPixelVisits: 5))));
        Assert.Equal("Limit.Pixels", Error(() => PdfImageCleanup.Process(raster, new(blankDetection: new()), new(limits: new(maximumPixels: 3)))));
        Assert.Equal("Limit.ExpandedBytes", Error(() => PdfImageCleanup.Process(raster, new(true, outputWidth: 4, outputHeight: 4), new(PdfProcessingMode.Permissive, limits: new(maximumExpandedBytes: 1)))));
        Assert.Equal("Cleanup.Extent", Error(() => Process(raster, new(true, outputWidth: 10000, outputHeight: 10000, maximumPixelVisits: 100000001))));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => PdfImageCleanup.Process(raster, new(), cancellationToken: cancellation.Token));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, ScanTests.Pixels(raster));
    }

    [Fact]
    public void IndependentConcurrentJobsDoNotChangeSharedImmutableSourceOrOptions()
    {
        var original = ScanTests.Raster(Enumerable.Range(0, 100).Select(x => (byte)x).ToArray(), 10, 10);
        var options = new PdfImageCleanupOptions(true, cropLeft: 1, outputWidth: 3, outputHeight: 3, blankDetection: new());
        var results = new PdfImageCleanupResult[12];
        Parallel.For(0, results.Length, i => results[i] = Process(original, options));
        foreach (var result in results) { Assert.Equal(ScanTests.Pixels(results[0].Processed), ScanTests.Pixels(result.Processed)); Assert.Same(original, result.Original); }
        Assert.Equal(Enumerable.Range(0, 100).Select(x => (byte)x), ScanTests.Pixels(original));
    }

    [Fact]
    public void OriginalLabeledCorpusRetainsIndependentPixelInputsOutputsAndMeasurements()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "artifacts", "image-cleanup"); Directory.CreateDirectory(directory);
        var records = new List<string>();
        foreach (string name in new[] { "white", "near-white", "noise", "faint-text", "border", "color", "resize" })
        {
            int channels = name == "color" ? 3 : 1;
            byte[] bytes = Enumerable.Repeat((byte)255, 16 * 12 * channels).ToArray();
            if (name == "near-white") for (int i = 0; i < bytes.Length; i++) bytes[i] = 248;
            if (name == "noise") bytes[6 * 16 + 8] = 0;
            if (name == "faint-text") for (int y = 3; y < 9; y++) bytes[y * 16 + 8] = 220;
            if (name == "border") for (int y = 0; y < 12; y++) for (int x = 0; x < 16; x++) if (y == 0 || y == 11 || x == 0 || x == 15) bytes[y * 16 + x] = 0;
            if (name == "color") for (int i = 0; i < bytes.Length; i += 3) { bytes[i] = 20; bytes[i + 1] = 80; bytes[i + 2] = 160; }
            if (name == "resize") for (int y = 0; y < 12; y++) for (int x = 0; x < 16; x++) bytes[y * 16 + x] = (byte)((x * 11 + y * 7) % 256);
            var original = ScanTests.Raster(bytes, 16, 12, 16 * channels, channels == 1 ? PdfOcrPixelFormat.Gray8 : PdfOcrPixelFormat.Rgb24);
            var options = new PdfImageCleanupOptions(true, medianDenoise: name == "noise", binaryThreshold: name == "faint-text" ? (byte?)200 : null,
                cropLeft: name == "border" ? 1 : 0, cropTop: name == "border" ? 1 : 0, cropRight: name == "border" ? 1 : 0, cropBottom: name == "border" ? 1 : 0,
                outputWidth: name == "resize" ? (int?)8 : null, outputHeight: name == "resize" ? (int?)6 : null, blankDetection: new(name == "color" ? 0x1450a0 : 0xffffff));
            var result = Process(original, options);
            File.WriteAllBytes(Path.Combine(directory, name + "-before.raw"), bytes);
            File.WriteAllBytes(Path.Combine(directory, name + "-after.raw"), ScanTests.Pixels(result.Processed));
            records.Add("{\"name\":\"" + name + "\",\"width\":" + result.Processed.Width + ",\"height\":" + result.Processed.Height + ",\"channels\":" + channels + ",\"beforeForeground\":" + result.Before!.ForegroundPixels + ",\"afterForeground\":" + result.After!.ForegroundPixels + ",\"confidence\":" + result.Before.Confidence.ToString("R", CultureInfo.InvariantCulture) + "}");
        }
        File.WriteAllText(Path.Combine(directory, "observations.json"), "[" + string.Join(",", records) + "]", new UTF8Encoding(false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompiledLanguageConsumersRequireExplicitLossOptions(bool visualBasic)
    {
        var original = ScanTests.Raster(new byte[] { 0, 127, 128, 255 }, 2, 2);
        var options = new PdfImageCleanupOptions(true, binaryThreshold: 128, blankDetection: new());
        var result = visualBasic ? ContractExamples.VisualBasic.ContractExample.CleanScan(original, options) : ContractExamples.CSharp.ContractExample.CleanScan(original, options);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, ScanTests.Pixels(result.Processed)); Assert.Same(original, result.Original);
    }
}
