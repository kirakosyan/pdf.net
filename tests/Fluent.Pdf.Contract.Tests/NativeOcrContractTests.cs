using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using Fluent.Pdf.Ocr;
using Fluent.Pdf.Workers;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
// Original C-API protocol/ownership fixture only; these cases do not qualify recognition accuracy or any third-party engine/model.
public sealed class NativeOcrContractTests
{
    private static PdfOcrOptions Options(int characters = 1000) => new("eng", maximumCharacters: characters, maximumWords: 100);
    private static string Error(Action action) => Assert.ThrowsAny<PdfException>(action).Diagnostic.Code;
    [Fact]
    public void IsolatedCapiFixturePreservesGeometryConfidenceAndCheckedModelProvenance()
    {
        using var fixture = new Fixture();
        var result = PdfOcr.Recognize(OcrTests.Raster(), Options(), fixture.Providers);
        Assert.Equal("AB\n", result.Text);
        Assert.Equal("eng", result.Language);
        Assert.Equal("5.5.0-fixture", result.EngineVersion);
        Assert.Equal(fixture.ModelSha256, result.ModelIdentity);
        Assert.Equal(95, result.MeanConfidence);
        Assert.Single(result.Words);
        Assert.Equal(new PdfRect(1, 1, 2, 2), result.Words[0].Bounds);
        Assert.Equal(95, result.Words[0].Confidence);
        Assert.NotNull(result.OperationReport);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
        Assert.Contains(result.OperationReport!.Diagnostics, d => d.Code == "Ocr.RecognitionProvenance" && d.Severity == PdfDiagnosticSeverity.Information);
    }

    [Theory]
    [InlineData(1, "Ocr.Recognition")]
    [InlineData(2, "Ocr.NativeAllocation")]
    [InlineData(3, "Ocr.WordGeometry")]
    [InlineData(6, "Ocr.NativeUtf8")]
    public void NativeErrorsAndMalformedResultsCleanAllOwnedHandlesAndTheNextJobRecovers(byte mode, string expected)
    {
        using var fixture = new Fixture();
        Assert.Equal(expected, Error(() => PdfOcr.Recognize(OcrTests.Raster(mode), Options(), fixture.Providers)));
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
        Assert.Equal("AB\n", PdfOcr.Recognize(OcrTests.Raster(), Options(), fixture.Providers).Text);
    }

    [Fact]
    public void MissingSymbolsHavePreciseExportDiagnosticsBeforeRecognition()
    {
        using var fixture = new Fixture(missingSymbol: true);
        var error = Assert.Throws<PdfException>(() => PdfOcr.Recognize(OcrTests.Raster(), Options(), fixture.Providers));
        Assert.Equal("Ocr.MissingSymbol", error.Diagnostic.Code);
        Assert.Contains("TessBaseAPIGetTsvText", error.Diagnostic.Message);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Fact]
    public void ActualNativeVersionAndExplicitModelInitializationCannotSilentlyFallback()
    {
        using var wrongVersion = new Fixture(version: "5.5.9");
        Assert.Equal("Ocr.NativeVersion", Error(() => PdfOcr.Recognize(OcrTests.Raster(), Options(), wrongVersion.Providers)));
        using var wrongModel = new Fixture(corruptModel: true);
        Assert.Equal("Ocr.ModelInitialization", Error(() => PdfOcr.Recognize(OcrTests.Raster(), Options(), wrongModel.Providers)));
        Assert.Empty(Directory.GetFileSystemEntries(wrongModel.Jobs));
        Assert.Equal("Ocr.LanguageUnsupported", Error(() => PdfOcr.Recognize(OcrTests.Raster(), new("deu"), wrongVersion.Providers)));
    }

    [Fact]
    public void MissingPinnedModelFailsPreflightWithItsExactAssetIdentity()
    {
        using var fixture = new Fixture();
        File.Delete(Path.Combine(fixture.Assets, "eng.traineddata"));
        var error = Assert.Throws<PdfException>(() => PdfOcr.Recognize(OcrTests.Raster(), Options(), fixture.Providers));
        Assert.Equal("Native.AssetMissing", error.Diagnostic.Code);
        Assert.Contains("eng.traineddata", error.Diagnostic.Message);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Fact]
    public void TotalCharacterCeilingRejectsBeforeReturningARecognitionResult()
    {
        using var fixture = new Fixture();
        Assert.Equal("Ocr.CharacterLimit", Error(() => PdfOcr.Recognize(OcrTests.Raster(), Options(4), fixture.Providers)));
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
        Assert.Equal("AB\n", PdfOcr.Recognize(OcrTests.Raster(), Options(5), fixture.Providers).Text);
    }

    [Fact]
    public void CancellationTerminatesThePrivateNativeRecognitionAndReleasesItsStage()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(200));
        var clock = Stopwatch.StartNew();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfOcr.Recognize(OcrTests.Raster(4), Options(), fixture.Providers, cancellationToken: cancellation.Token));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), "Private native recognition must terminate within the cancellation bound.");
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    internal sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "fluent-pdf-original-ocr-" + Guid.NewGuid().ToString("N"));
        internal string Assets { get; }
        internal string Jobs { get; }
        internal string ModelSha256 { get; }
        internal PdfProviderRegistry Providers { get; }

        internal Fixture(bool missingSymbol = false, bool corruptModel = false, string version = "5.5.0-fixture")
        {
            string rid = PdfNativeDeploymentInspector.OperatingSystemRuntimeIdentifier ?? throw new InvalidOperationException("The source native worker RID is unsupported.");
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (repo != null && !File.Exists(Path.Combine(repo.FullName, "Fluent.Pdf.slnx")))
                repo = repo.Parent;
            if (repo == null)
                throw new InvalidOperationException("The source fixture repository is unavailable.");
            string binaries = Path.Combine(repo.FullName, "artifacts", "native-fixtures", rid);
            string extension = rid.StartsWith("win-", StringComparison.Ordinal) ? "dll" : rid.StartsWith("osx-", StringComparison.Ordinal) ? "dylib" : "so";
            string executable = extension == "dll" ? "worker.exe" : "worker", bridge = "fluent-pdf-ocr." + extension, engine = extension == "dll" ? "tesseract.dll" : extension == "dylib" ? "libtesseract.dylib" : "libtesseract.so";
            Assets = Path.Combine(_root, "assets");
            Jobs = Path.Combine(_root, "jobs");
            Directory.CreateDirectory(Assets);
            Directory.CreateDirectory(Jobs);
            File.Copy(Path.Combine(binaries, executable), Path.Combine(Assets, executable));
            File.Copy(Path.Combine(binaries, bridge), Path.Combine(Assets, bridge));
            File.Copy(Path.Combine(binaries, missingSymbol ? "ocr-missing." + extension : engine), Path.Combine(Assets, engine));
            File.WriteAllText(Path.Combine(Assets, "eng.traineddata"), corruptModel ? "invalid-model" : "original-model-fixture");
            var names = new[]
            {
                executable,
                bridge,
                engine,
                "eng.traineddata"
            };
            var assets = names.Select(name => Pin(name, name != "eng.traineddata")).ToArray();
            ModelSha256 = assets[3].Sha256;
            var deployment = new PdfNativeDeployment(Assets, rid, "original.ocr-capi-fixture", IsolatedTesseractOcr.BridgeVersion, IsolatedTesseractOcr.BridgeAbi, 16777216, assets);
            Providers = new(new IsolatedTesseractOcr(new NativeWorkerOptions(deployment, executable, bridge, Jobs, 67108864, 1048576, 65536, TimeSpan.FromSeconds(10)), version));
        }

        private PdfNativeAsset Pin(string name, bool native)
        {
            using var input = File.OpenRead(Path.Combine(Assets, name));
            using var hash = SHA256.Create();
            return new(name, input.Length, BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant(), native);
        }

        public void Dispose() => Directory.Delete(_root, true);
    }
}
