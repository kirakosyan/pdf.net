using System.IO;
using System.Security.Cryptography;
using Fluent.Pdf.Codecs;
using Fluent.Pdf.Workers;
using Xunit;
using Xunit.Abstractions;

namespace Fluent.Pdf.Contract.Tests;
public sealed class NativeImageCodecTests
{
    private readonly ITestOutputHelper _output;
    public NativeImageCodecTests(ITestOutputHelper output) => _output = output;
    [Theory]
    [InlineData("l.jpg", PdfImageCodec.Dct, 1, 8)]
    [InlineData("l.j2k", PdfImageCodec.Jpx, 1, 8)]
    [InlineData("rgb.j2k", PdfImageCodec.Jpx, 3, 8)]
    [InlineData("mmr.jb2", PdfImageCodec.Jbig2, 1, 1)]
    public void IndependentEncodedFixturesDecodeToExactSamples(string file, PdfImageCodec codec, int components, int bits)
    {
        using var fixture = new CodecFixture(_output);
        byte[] source = File.ReadAllBytes(ImagePath(file)), expected = File.ReadAllBytes(ImagePath(file + ".samples"));
        Assert.Equal(expected, fixture.Decode(codec, source, new(64, 16, components, bits)));
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Theory]
    [InlineData(PdfImageCodec.Jpx, 1, 1)]
    [InlineData(PdfImageCodec.Jpx, 1, 8)]
    [InlineData(PdfImageCodec.Jpx, 3, 8)]
    [InlineData(PdfImageCodec.Jpx, 1, 16)]
    [InlineData(PdfImageCodec.Jbig2, 1, 1)]
    public void LosslessImageEncodersPreserveSamplesAndBitPolarity(PdfImageCodec codec, int components, int bits)
    {
        using var fixture = new CodecFixture(_output);
        var info = new PdfImageInfo(64, 16, components, bits);
        byte[] source = Enumerable.Range(0, (int)info.ByteLength).Select(i => (byte)((i * 17 + i / 11) & 255)).ToArray();
        byte[] encoded = fixture.Encode(codec, source, info);
        Assert.Equal(source, fixture.Decode(codec, encoded, info));
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Fact]
    public void DctEncodingIsExplicitAndProducesValidIndependentGeometry()
    {
        using var fixture = new CodecFixture(_output);
        var info = new PdfImageInfo(64, 16, 1, 8);
        byte[] original = Enumerable.Range(0, 1024).Select(i => (byte)(i % 64 * 4)).ToArray();
        byte[] encoded = fixture.Encode(PdfImageCodec.Dct, original, info), samples = fixture.Decode(PdfImageCodec.Dct, encoded, info);
        Assert.Equal(original.Length, samples.Length);
        Assert.InRange(original.Zip(samples, (a, b) => Math.Abs(a - b)).Max(), 0, 3);
    }

    [Fact]
    public void DamagedImageRejectsWithPreciseDiagnosticsAndNextJobRecovers()
    {
        using var fixture = new CodecFixture(_output);
        byte[] valid = File.ReadAllBytes(ImagePath("l.jpg"));
        // Keep the complete SOF/header; damage entropy data beyond header preflight.
        byte[] truncated = valid.Take(valid.Length - 12).ToArray();
        Assert.Equal("Codec.Malformed", Assert.Throws<PdfException>(() => fixture.Decode(PdfImageCodec.Dct, truncated, new(64, 16, 1, 8))).Diagnostic.Code);
        Assert.Equal(File.ReadAllBytes(ImagePath("l.jpg.samples")), fixture.Decode(PdfImageCodec.Dct, valid, new(64, 16, 1, 8)));
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Fact]
    public void CodecEnvelopeIsChargedBeforeNativeExecution()
    {
        using var fixture = new CodecFixture(_output);
        byte[] valid = File.ReadAllBytes(ImagePath("l.jpg"));
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => fixture.Decode(PdfImageCodec.Dct, valid, new(64, 16, 1, 8), new PdfResourceLimits(maximumExpandedBytes: 1048576))).Diagnostic.Code);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    private static string ImagePath(string file) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "filters", "images", file);
    private sealed class CodecFixture : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _root = Path.Combine(Path.GetTempPath(), "fluent-pdf-image-codecs-" + Guid.NewGuid().ToString("N"));
        private readonly PdfProviderRegistry _registry;
        internal string Jobs { get; }

        internal CodecFixture(ITestOutputHelper output)
        {
            _output = output;
            Jobs = Path.Combine(_root, "jobs");
            Directory.CreateDirectory(Jobs);
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (repo != null && !File.Exists(Path.Combine(repo.FullName, "Fluent.Pdf.slnx")))
                repo = repo.Parent;
            string rid = PdfNativeDeploymentInspector.OperatingSystemRuntimeIdentifier ?? throw new InvalidOperationException();
            string root = Path.Combine(repo!.FullName, "artifacts", "native-fixtures", rid), exe = rid.StartsWith("win-", StringComparison.Ordinal) ? "worker.exe" : "worker", library = "fluent-pdf-codecs." + (rid.StartsWith("win-", StringComparison.Ordinal) ? "dll" : rid.StartsWith("osx-", StringComparison.Ordinal) ? "dylib" : "so");
            var deployment = new PdfNativeDeployment(root, rid, "image.codecs", IsolatedImageCodecProvider.BridgeVersion, IsolatedImageCodecProvider.BridgeAbi, 67108864, Asset(root, exe), Asset(root, library));
            var worker = new NativeWorkerOptions(deployment, exe, library, Jobs, 1073741824, 1048576, 1048576, TimeSpan.FromSeconds(10), allowInProcessThreads: true, captureWorkerDiagnostics: true);
            _registry = new PdfProviderRegistry(new IsolatedImageCodecProvider(worker));
        }

        internal byte[] Encode(PdfImageCodec codec, byte[] bytes, PdfImageInfo info) => Run(context =>
        {
            using var input = new MemoryStream(bytes);
            using var output = PdfFilters.Encode(input, new[] { new PdfFilter(Name(codec)) }, context, info, _registry);
            return Copy(output);
        });
        internal byte[] Decode(PdfImageCodec codec, byte[] bytes, PdfImageInfo info, PdfResourceLimits? limits = null)
        {
            using var fixture = new SyntaxFixtures();
            long offset = fixture.StreamObject(1, $"/Filter /{Name(codec)} /Width {info.Width} /Height {info.Height} /BitsPerComponent {info.BitsPerComponent}", bytes);
            fixture.Classic(new[] { (1, 0, offset, false) });
            fixture.Bytes.Position = 0;
            var syntax = PdfSyntax.Read(fixture.Bytes);
            return Run(context =>
            {
                using var output = PdfFilters.Decode(syntax.Resolve(new(1)), context, _registry);
                return Copy(output);
            }, limits);
        }

        private byte[] Run(Func<PdfOperationContext, byte[]> action, PdfResourceLimits? limits = null)
        {
            byte[]? result = null;
            try
            {
                PolicyTests.Run(c => result = action(c), limits ?? new PdfResourceLimits(maximumExpandedBytes: 2147483648));
            }
            catch (PdfException failure)
            {
                _output.WriteLine(failure.Diagnostic.Code);
                if (failure.Data["Native.WorkerDiagnostics"] is string detail)
                    _output.WriteLine(detail);
                throw;
            }

            return result!;
        }

        private static byte[] Copy(Stream input)
        {
            using var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        }

        private static string Name(PdfImageCodec codec) => codec == PdfImageCodec.Dct ? "DCTDecode" : codec == PdfImageCodec.Jpx ? "JPXDecode" : "JBIG2Decode";
        private static PdfNativeAsset Asset(string root, string name)
        {
            using var input = File.OpenRead(Path.Combine(root, name));
            using var hash = SHA256.Create();
            return new(name, input.Length, BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant());
        }

        public void Dispose()
        {
            Directory.Delete(Jobs);
            Directory.Delete(_root);
        }
    }
}
