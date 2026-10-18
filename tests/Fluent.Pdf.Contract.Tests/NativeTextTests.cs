using System.IO;
using System.Security.Cryptography;
using Fluent.Pdf.Text;
using Fluent.Pdf.Workers;
using Xunit;
using Xunit.Abstractions;

namespace Fluent.Pdf.Contract.Tests;
public sealed class NativeTextTests
{
    [Fact]
    public void ManyShapingRunsReuseTheWorkerEnvelopeUnderTheDefaultOperationBudget()
    {
        using var fixture = new Fixture(_output);
        var font = TextTests.Read("latin.ttf");
        var request = new PdfShapingRequest("office", 0, 6, font, PdfTextDirection.LeftToRight, 0x4c61746e, "en", Array.Empty<PdfTextFeature>());
        var report = PolicyTests.Run(context =>
        {
            var shaper = fixture.Providers.GetRequired<IPdfTextShaper>(PdfCapability.ShapeText);
            for (int i = 0; i < 24; i++)
                Assert.NotEmpty(shaper.Shape(request, context));
        });
        Assert.True(report.Resources.Single(x => x.Resource == PdfResourceKind.ExpandedBytes).Consumed < 268435456);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    private readonly ITestOutputHelper _output;
    public NativeTextTests(ITestOutputHelper output) => _output = output;
    public static IEnumerable<object[]> KnownAnswers() => ShapingKnownAnswers.Vectors();
    [Theory]
    [MemberData(nameof(KnownAnswers))]
    public void ArabicIndicLigaturesMarksAndVerticalCjkMatchIndependentHarfBuzzAnswers(string file, string text, string script, string language, int direction, int[] expected)
    {
        using var fixture = new Fixture(_output);
        var font = TextTests.Read(file);
        var options = new PdfTextOptions(direction == 5 ? PdfParagraphDirection.RightToLeft : PdfParagraphDirection.LeftToRight, direction == 6, language);
        var result = PdfText.Shape(text, new(font), fixture.Providers, options, fixture.Policy);
        foreach (var run in result.Runs)
            Assert.Equal(script, new string (new[] { (char)(run.Request.ScriptTag >> 24), (char)((run.Request.ScriptTag >> 16) & 255), (char)((run.Request.ScriptTag >> 8) & 255), (char)(run.Request.ScriptTag & 255) }));
        int[] actual = result.Runs.SelectMany(x => x.Glyphs).SelectMany(x => new[] { x.GlyphIndex, x.Cluster, (int)x.AdvanceX, (int)x.AdvanceY, (int)x.OffsetX, (int)x.OffsetY }).ToArray();
        Assert.Equal(expected, actual);
        Assert.Equal(text, result.Unicode.Text);
        Assert.Equal(text, TextTests.Actual(PdfTextDrawing.Create(result, 30, 100, 24)));
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Fact]
    public void ExplicitFeatureOverrideDisablesLigatureWithoutChangingLogicalText()
    {
        using var fixture = new Fixture(_output);
        var font = TextTests.Read("latin.ttf");
        var enabled = PdfText.Shape("fi", new(font), fixture.Providers, policy: fixture.Policy);
        var disabled = PdfText.Shape("fi", new(font), fixture.Providers, new(features: new PdfTextFeature("liga", 0)), fixture.Policy);
        Assert.Single(enabled.Runs[0].Glyphs);
        Assert.Equal(2, disabled.Runs[0].Glyphs.Count);
        Assert.Equal("fi", disabled.Unicode.Text);
    }

    [Fact]
    public void MixedFallbackUsesBidiVisualRunsAndPreservesLogicalParagraph()
    {
        using var fixture = new Fixture(_output);
        var result = PdfText.Shape("office مرحبا", new(TextTests.Read("latin.ttf"), TextTests.Read("arabic.ttf")), fixture.Providers, policy: fixture.Policy);
        Assert.Equal(2, result.Runs.Count);
        Assert.Equal(PdfTextDirection.RightToLeft, result.Runs[1].Request.Direction);
        Assert.Equal("office مرحبا", TextTests.Actual(PdfTextDrawing.Create(result, 20, 80, 24)));
    }

    [Fact]
    public void NativeAllocationEnvelopeIsChargedBeforeLaunchingWorker()
    {
        using var fixture = new Fixture(_output);
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PdfText.Shape("fi", new(TextTests.Read("latin.ttf")), fixture.Providers, policy: new(limits: new(maximumExpandedBytes: 16777216)))).Diagnostic.Code);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Fact]
    public void WrappedFlowRetainsOriginalRtlParagraphDirectionOnLatinContinuationLines()
    {
        using var fixture = new Fixture(_output);
        var fonts = new PdfFontFallback(TextTests.Read("latin.ttf"), TextTests.Read("arabic.ttf"));
        var paragraph = new PdfFlowParagraph("مرحبا office office", new PdfFlowTextStyle(fonts), new PdfFlowParagraphStyle(orphanLines: 1, widowLines: 1));
        var options = new PdfFlowOptions(new PdfPageGeometry(new PdfPageBoxes(new PdfRect(0, 0, 42, 160))), PdfMargins.Uniform(0), PdfFlowOverflow.BreakGrapheme);
        var result = PdfFlow.Layout(new[] { paragraph }, options, fixture.Providers, new PdfOperationPolicy(limits: new PdfResourceLimits(maximumExpandedBytes: 8589934592)));
        var lines = result.Pages.SelectMany(p => p.Lines).ToArray();
        Assert.True(lines.Length >= 3);
        Assert.Equal("مرحبا office office", string.Concat(lines.Select(l => l.Text)));
        Assert.All(lines, line => Assert.Equal(1, line.ShapedText.Unicode.Paragraphs[0].EmbeddingLevel));
        Assert.Contains(lines.Skip(1).SelectMany(l => l.ShapedText.Runs), r => r.Request.Direction == PdfTextDirection.LeftToRight);
        using var document = result.CreateDocument();
        using var output = new MemoryStream();
        document.Save(output);
        Assert.True(output.Length > 0);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    private sealed class ReportingShaper : IPdfTextShaper
    {
        private readonly IPdfTextShaper _inner;
        private readonly ITestOutputHelper _output;
        internal ReportingShaper(IPdfTextShaper inner, ITestOutputHelper output)
        {
            _inner = inner;
            _output = output;
        }

        public PdfProviderDescriptor Descriptor => _inner.Descriptor;

        public IReadOnlyList<PdfShapedGlyph> Shape(PdfShapingRequest request, PdfOperationContext context)
        {
            try
            {
                return _inner.Shape(request, context);
            }
            catch (PdfException failure)
            {
                _output.WriteLine(failure.Diagnostic.Code);
                foreach (string key in new[]
                {
                    "Native.WorkerDiagnostics",
                    "Native.FailureType",
                    "Native.FailureErrorCode",
                    "Native.WorkerFailureDetail"
                }

                )
                    if (failure.Data[key] is object value)
                        _output.WriteLine(key + ": " + value);
                throw;
            }
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "fluent-pdf-text-" + Guid.NewGuid().ToString("N"));
        internal string Jobs { get; }
        internal PdfProviderRegistry Providers { get; }
        internal PdfOperationPolicy Policy { get; } = new(limits: new(maximumExpandedBytes: 1073741824));

        internal Fixture(ITestOutputHelper output)
        {
            Jobs = Path.Combine(_root, "jobs");
            Directory.CreateDirectory(Jobs);
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (repo != null && !File.Exists(Path.Combine(repo.FullName, "Fluent.Pdf.slnx")))
                repo = repo.Parent;
            string rid = PdfNativeDeploymentInspector.OperatingSystemRuntimeIdentifier ?? throw new InvalidOperationException();
            string root = Path.Combine(repo!.FullName, "artifacts", "native-fixtures", rid), extension = rid.StartsWith("win-", StringComparison.Ordinal) ? "dll" : rid.StartsWith("osx-", StringComparison.Ordinal) ? "dylib" : "so", exe = rid.StartsWith("win-", StringComparison.Ordinal) ? "worker.exe" : "worker", bridge = "fluent-pdf-text." + extension, dependency = rid.StartsWith("linux-", StringComparison.Ordinal) ? "libHarfBuzzSharp.so.0.61421.0" : "libHarfBuzzSharp." + extension;
            var deployment = new PdfNativeDeployment(root, rid, "text.shaping", IsolatedTextShaper.BridgeVersion, IsolatedTextShaper.BridgeAbi, 67108864, Asset(root, exe), Asset(root, bridge), Asset(root, dependency));
            var worker = new NativeWorkerOptions(deployment, exe, bridge, Jobs, 268435456, 4194304, 1048576, TimeSpan.FromSeconds(10), captureWorkerDiagnostics: true);
            Providers = new(new ReportingShaper(new IsolatedTextShaper(worker), output));
            output.WriteLine("HarfBuzz 14.2.1; isolated bridge; " + rid);
        }

        private static PdfNativeAsset Asset(string root, string name)
        {
            using var input = File.OpenRead(Path.Combine(root, name));
            using var sha = SHA256.Create();
            return new(name, input.Length, BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant());
        }

        public void Dispose()
        {
            Directory.Delete(Jobs);
            Directory.Delete(_root);
        }
    }
}
