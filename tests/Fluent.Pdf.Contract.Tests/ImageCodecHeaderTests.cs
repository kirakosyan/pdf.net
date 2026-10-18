using System.IO;
using Fluent.Pdf.Codecs;
using Fluent.Pdf.Workers;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ImageCodecHeaderTests
{
    [Theory]
    [InlineData("l.jpg", PdfImageCodec.Dct, 1)]
    [InlineData("rgb.jpg", PdfImageCodec.Dct, 3)]
    [InlineData("l.j2k", PdfImageCodec.Jpx, 1)]
    [InlineData("rgb.j2k", PdfImageCodec.Jpx, 3)]
    public void MetadataPreflightDoesNotRequireImageAllocationOrStartAWorker(string file, PdfImageCodec codec, int components)
    {
        var provider = Provider();
        byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "filters", "images", file));
        var report = PolicyTests.Run(context =>
        {
            using var input = new MemoryStream(bytes);
            var info = provider.Inspect(codec, input, PdfValue.Dictionary(new Dictionary<string, PdfValue>()), context);
            Assert.Equal(64, info.Width);
            Assert.Equal(16, info.Height);
            Assert.Equal(components, info.Components);
            Assert.Equal(8, info.BitsPerComponent);
        }, new PdfResourceLimits(maximumPixels: 1, maximumExpandedBytes: 1048576));
        Assert.Equal(0, report.Resources.Single(x => x.Resource == PdfResourceKind.Pixels).Consumed);
        Assert.Equal(0, report.Resources.Single(x => x.Resource == PdfResourceKind.Workers).Peak);
    }

    [Fact]
    public void EveryTruncatedJpxPrefixAndDeterministicHeaderMutationsAreBounded()
    {
        var provider = Provider();
        byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "filters", "images", "l.j2k"));
        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] prefix = bytes.Take(length).ToArray();
            Assert.Throws<PdfException>(() => PolicyTests.Run(c =>
            {
                using var input = new MemoryStream(prefix);
                provider.Inspect(PdfImageCodec.Jpx, input, PdfValue.Null, c);
            }));
        }

        var random = new Random(9483);
        for (int i = 0; i < 500; i++)
        {
            byte[] mutation = (byte[])bytes.Clone();
            mutation[random.Next(Math.Min(50, bytes.Length))] ^= (byte)(1 << random.Next(8));
            try
            {
                PolicyTests.Run(c =>
                {
                    using var input = new MemoryStream(mutation);
                    provider.Inspect(PdfImageCodec.Jpx, input, PdfValue.Null, c);
                }, new PdfResourceLimits(maximumObjects: 10000));
            }
            catch (PdfException)
            {
            }
        }
    }

    [Fact]
    public void JpxTileBudgetIsRejectedBeforeDecoderAllocation()
    {
        var provider = Provider();
        byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "filters", "images", "l.j2k"));
        // SIZ XTsiz/YTsiz: one-pixel tiles multiply the declared allocation graph.
        for (int at = 24; at <= 28; at += 4)
        {
            bytes[at] = bytes[at + 1] = bytes[at + 2] = 0;
            bytes[at + 3] = 1;
        }

        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PolicyTests.Run(c =>
        {
            using var input = new MemoryStream(bytes);
            provider.Inspect(PdfImageCodec.Jpx, input, PdfValue.Null, c);
        }, new PdfResourceLimits(maximumObjects: 100))).Diagnostic.Code);
    }

    private static IsolatedImageCodecProvider Provider()
    {
        // Deliberately absent binaries prove header inspection performs no native execution.
        string root = Path.Combine(Path.GetTempPath(), "fluent-pdf-absent-header-fixture");
        var deployment = new PdfNativeDeployment(root, PdfNativeDeploymentInspector.OperatingSystemRuntimeIdentifier!, "header.test", IsolatedImageCodecProvider.BridgeVersion, IsolatedImageCodecProvider.BridgeAbi, 1024, new PdfNativeAsset("worker", 1, new string ('0', 64)), new PdfNativeAsset("codec", 1, new string ('0', 64)));
        return new(new NativeWorkerOptions(deployment, "worker", "codec", root, allowInProcessThreads: true));
    }
}
