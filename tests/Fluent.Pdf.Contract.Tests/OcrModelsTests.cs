using System.Globalization;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class OcrModelsTests
{
    internal static PdfOcrLanguageModel Model(string alias = "eng", string version = "one", string? digest = null) => new(alias, version, digest ?? new string ('a', 64), 12, "original-fixture", "own-model", new[] { "Latn" }, new[] { alias == "deu" ? "de" : "en" });
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    [Fact]
    public void CatalogCapturesImmutableDeclarationsAndSelectsOnlyExplicitOrderedAliases()
    {
        var scripts = new[]
        {
            "Latn"
        };
        var languages = new[]
        {
            "en"
        };
        var model = new PdfOcrLanguageModel("eng", "v1", new string ('a', 64), 12, "fixture", "own", scripts, languages);
        scripts[0] = "Cyrl";
        languages[0] = "ru";
        var array = new[]
        {
            model,
            Model("deu")
        };
        var catalog = new PdfOcrModelCatalog(array);
        array[0] = Model(version: "changed");
        var selection = catalog.Select("deu+eng");
        Assert.Equal(new[] { "deu", "eng" }, selection.Models.Select(m => m.Identifier));
        Assert.Equal("Latn", selection.Models[1].Scripts[0]);
        Assert.Equal("en", selection.Models[1].Languages[0]);
        Assert.Equal("eng.traineddata", model.FileName);
        Assert.NotEqual(selection.Identity, catalog.Select("eng+deu").Identity);
        Assert.NotNull(selection.OperationReport);
        Assert.Throws<NotSupportedException>(() => ((IList<PdfOcrLanguageModel>)selection.Models).Clear());
        Assert.Equal("Ocr.ModelUnavailable", Error(() => catalog.Select("fra")));
        Assert.Equal("Ocr.ModelSelection", Error(() => catalog.Select("eng+eng")));
    }

    [Theory]
    [InlineData("../eng")]
    [InlineData("script/Latin")]
    [InlineData("ENG")]
    [InlineData("eng-deu")]
    [InlineData("")]
    public void NativeModelAliasesCannotImplyPathsOrDiscovery(string alias) => Assert.Throws<ArgumentException>(() => Model(alias));
    [Fact]
    public void ModelIdentitiesBindExactVersionContentAndOrderIndependentlyOfCulture()
    {
        var catalog = new PdfOcrModelCatalog(Model());
        string original = catalog.Select("eng").Identity;
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal(original, catalog.Select("eng").Identity);
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
        }

        Assert.NotEqual(original, new PdfOcrModelCatalog(Model(version: "two")).Select("eng").Identity);
        Assert.NotEqual(original, new PdfOcrModelCatalog(Model(digest: new string ('b', 64))).Select("eng").Identity);
        Assert.Throws<ArgumentException>(() => new PdfOcrModelCatalog(Model(), Model()));
        Assert.Throws<ArgumentException>(() => Model(version: "\ud800"));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => catalog.Select("eng", cancellationToken: cancel.Token));
    }

    [Theory]
    [InlineData("A😀B", "A😃B", 3, 1, 1, 1)]
    [InlineData("é", "e\u0301", 1, 2, 1, 1)]
    [InlineData("A\u00a0B", "A B", 3, 1, 2, 0)]
    [InlineData("AB CD", "AB XD", 5, 1, 2, 1)]
    public void AccuracyUsesUnicodeScalarsAndExplicitWhitespaceWithoutHiddenNormalization(string expected, string actual, int chars, int charErrors, int words, int wordErrors)
    {
        var group = Assert.Single(Evaluate(new PdfOcrAccuracySample("case", "eng", "model-v1", expected, actual)).Groups);
        Assert.Equal(chars, group.ReferenceCharacters);
        Assert.Equal(charErrors, group.CharacterErrors);
        Assert.Equal(words, group.ReferenceWords);
        Assert.Equal(wordErrors, group.WordErrors);
        Assert.Equal(charErrors / (double)chars, group.CharacterErrorRate);
    }

    [Fact]
    public void BlankReferenceInsertionsArePenalizedAndAllBlankGroupsCannotPass()
    {
        var blank = Assert.Single(Evaluate(new PdfOcrAccuracySample("blank", "eng", "v1", "", "AB")).Groups);
        Assert.Equal(2, blank.CharacterErrors);
        Assert.Null(blank.CharacterErrorRate);
        Assert.False(blank.ThresholdsMet);
        var mixed = Assert.Single(Evaluate(new PdfOcrAccuracySample("real", "eng", "v1", "A", "A"), new("blank", "eng", "v1", "", "AB")).Groups);
        Assert.Equal(2, mixed.CharacterErrorRate);
        Assert.False(mixed.ThresholdsMet);
    }

    [Fact]
    public void CorpusDigestsAndGroupsBindExactObservationsWithoutPoolingModelVersions()
    {
        var a = new PdfOcrAccuracySample("a", "eng", "v1", "AB", "AB");
        var b = new PdfOcrAccuracySample("b", "eng", "v2", "AB", "AX");
        var first = Evaluate(a, b);
        Assert.Equal(2, first.Groups.Count);
        var changed = Evaluate(a, new("b", "eng", "v2", "AB", "AY"));
        Assert.Equal(first.ReferenceSha256, changed.ReferenceSha256);
        Assert.NotEqual(first.RecognitionSha256, changed.RecognitionSha256);
        Assert.NotEqual(first.ReferenceSha256, Evaluate(b, a).ReferenceSha256);
        Assert.True(first.Groups[0].ThresholdsMet);
        Assert.False(first.Groups[1].ThresholdsMet);
        Assert.Throws<ArgumentException>(() => new PdfOcrAccuracySample("bad", "eng", "v1", "\ud800", ""));
    }

    [Fact]
    public void AccuracyBoundsComparisonWorkEnumerationAndCancellation()
    {
        var sample = new PdfOcrAccuracySample("a", "eng", "v1", "AB", "AB");
        Assert.Equal(13, PdfOcrAccuracy.Evaluate(new[] { sample }, "own", "1", Options(cells: 13)).ComparisonCells);
        Assert.Equal("Ocr.AccuracyComparisonLimit", Error(() => PdfOcrAccuracy.Evaluate(new[] { sample }, "own", "1", Options(cells: 12))));
        Assert.Equal("Ocr.AccuracySampleLimit", Error(() => PdfOcrAccuracy.Evaluate(new[] { sample, new PdfOcrAccuracySample("b", "eng", "v1", "A", "A") }, "own", "1", Options(samples: 1))));
        Assert.Equal("Ocr.AccuracyThresholdMissing", Error(() => Evaluate(new PdfOcrAccuracySample("a", "deu", "v1", "A", "A"))));
        Assert.Equal("Ocr.AccuracySampleIdentity", Error(() => Evaluate(sample, sample)));
        Assert.Equal("Ocr.AccuracyEmptyCorpus", Error(() => Evaluate()));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfOcrAccuracy.Evaluate(new[] { sample }, "own", "1", Options(), cancellationToken: cancel.Token));
        Assert.StartsWith("Limit.", Error(() => PdfOcrAccuracy.Evaluate(new[] { sample }, "own", "1", Options(), new(limits: new(maximumInputBytes: 1)))));
    }

    [Fact]
    public void ProviderModelDeclarationsAreDetachedAndRetainLegacyConstruction()
    {
        var models = new[]
        {
            Model()
        };
        var result = PdfOcrResult.WithModels("A", Array.Empty<PdfOcrWord>(), "eng", "own", "digest", null, models);
        models[0] = Model(version: "two");
        Assert.Equal("one", result.Models[0].Version);
        Assert.Empty(new PdfOcrResult("A", Array.Empty<PdfOcrWord>(), "eng", "own", "digest").Models);
    }

    [Fact]
    public void OptionalProviderRejectsModelMetadataThatDoesNotMatchThePinnedDataAsset()
    {
        string rid = PdfNativeDeploymentInspector.OperatingSystemRuntimeIdentifier!;
        string engine = rid.StartsWith("win-", StringComparison.Ordinal) ? "tesseract.dll" : rid.StartsWith("osx-", StringComparison.Ordinal) ? "libtesseract.dylib" : "libtesseract.so";
        var model = Model();
        var assets = new[]
        {
            new PdfNativeAsset("worker.bin", 1, new string ('a', 64)),
            new PdfNativeAsset("bridge.bin", 1, new string ('a', 64)),
            new PdfNativeAsset(engine, 1, new string ('a', 64)),
            new PdfNativeAsset("eng.traineddata", model.ByteLength, model.Sha256, false)
        };
        var deployment = new PdfNativeDeployment(Path.GetTempPath(), rid, "own", Fluent.Pdf.Ocr.IsolatedTesseractOcr.BridgeVersion, Fluent.Pdf.Ocr.IsolatedTesseractOcr.BridgeAbi, 4096, assets);
        var worker = new Fluent.Pdf.Workers.NativeWorkerOptions(deployment, "worker.bin", "bridge.bin", Path.GetTempPath());
        var catalog = new PdfOcrModelCatalog(model);
        Assert.Same(catalog, new Fluent.Pdf.Ocr.IsolatedTesseractOcr(worker, "5.5.0", catalog).ModelCatalog);
        Assert.Throws<ArgumentException>(() => new Fluent.Pdf.Ocr.IsolatedTesseractOcr(worker, "5.5.0", new PdfOcrModelCatalog(Model(digest: new string ('b', 64)))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExecutableLanguageConsumersSelectAndMeasure(bool visualBasic)
    {
        var catalog = new PdfOcrModelCatalog(Model());
        var samples = new[]
        {
            new PdfOcrAccuracySample("own", "eng", "v1", "A", "A")
        };
        var selection = visualBasic ? ContractExamples.VisualBasic.ContractExample.SelectOcrModels(catalog, "eng") : ContractExamples.CSharp.ContractExample.SelectOcrModels(catalog, "eng");
        var measured = visualBasic ? ContractExamples.VisualBasic.ContractExample.MeasureOcrAccuracy(samples, Options()) : ContractExamples.CSharp.ContractExample.MeasureOcrAccuracy(samples, Options());
        Assert.Equal(catalog.Select("eng").Identity, selection.Identity);
        Assert.True(Assert.Single(measured.Groups).ThresholdsMet);
    }

    private static PdfOcrAccuracyOptions Options(long cells = 16000000, int samples = 10000) => new(new Dictionary<string, PdfOcrAccuracyThresholds> { ["eng"] = new(0.1, 0.1) }, maximumSamples: samples, maximumComparisonCells: cells);
    private static PdfOcrAccuracyReport Evaluate(params PdfOcrAccuracySample[] samples) => PdfOcrAccuracy.Evaluate(samples, "original-labeled-corpus", "1", Options());
}
