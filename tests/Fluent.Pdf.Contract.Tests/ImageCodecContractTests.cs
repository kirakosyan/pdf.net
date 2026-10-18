using System.IO;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ImageCodecContractTests
{
    [Fact]
    public void GeometryUsesCheckedPackedRowArithmetic()
    {
        var info = new PdfImageInfo(9, 3, 1, 1);
        Assert.Equal(2, info.RowBytes);
        Assert.Equal(6, info.ByteLength);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfImageInfo(0, 1, 1, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfImageInfo(int.MaxValue, int.MaxValue, 4, 16));
    }

    [Fact]
    public void DecodingChecksPixelsBeforeEnteringDecoderAndExpiresProviderLeases()
    {
        var provider = new Probe();
        var registry = new PdfProviderRegistry(provider);
        Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => PolicyTests.Run(c => PdfFilters.Decode(Image(), c, registry), new PdfResourceLimits(maximumPixels: 1))).Diagnostic.Code);
        Assert.False(provider.Decoded);
        Stream? result = null;
        PolicyTests.Run(c => result = PdfFilters.Decode(Image(), c, registry));
        Assert.True(provider.Decoded);
        Assert.Throws<ObjectDisposedException>(() => provider.Input!.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => provider.Output!.WriteByte(1));
        using (result)
        {
            Assert.Equal(1, result!.ReadByte());
            Assert.Equal(2, result.ReadByte());
            Assert.Equal(-1, result.ReadByte());
        }
    }

    [Theory]
    [InlineData(0, "Codec.OutputLength")]
    [InlineData(3, "Codec.OutputLength")]
    public void IncorrectDecoderExtentsFailWithoutReturningPartialSamples(int count, string code)
    {
        var provider = new Probe
        {
            Count = count
        };
        Assert.Equal(code, Assert.Throws<PdfException>(() => PolicyTests.Run(c => PdfFilters.Decode(Image(), c, new PdfProviderRegistry(provider)))).Diagnostic.Code);
    }

    [Fact]
    public void MissingProvidersAndMetadataDisagreementFailPrecisely()
    {
        Assert.Equal("Provider.Unavailable", Assert.Throws<PdfException>(() => PolicyTests.Run(c => PdfFilters.Decode(Image(), c))).Diagnostic.Code);
        var provider = new Probe
        {
            Width = 3
        };
        Assert.Equal("Codec.MetadataMismatch", Assert.Throws<PdfException>(() => PolicyTests.Run(c => PdfFilters.Decode(Image(), c, new PdfProviderRegistry(provider)))).Diagnostic.Code);
        Assert.False(provider.Decoded);
        Assert.Throws<ArgumentException>(() => new PdfProviderRegistry(new ServiceProvider(new PdfProviderDescriptor("wrong", "1", PdfCapability.ImageCodecs))));
    }

    private static PdfValue Image()
    {
        using var fixture = new SyntaxFixtures();
        long offset = fixture.StreamObject(1, "/Filter /DCTDecode /Width 2 /Height 1", new byte[] { 1 });
        fixture.Classic(new[] { (1, 0, offset, false) });
        fixture.Bytes.Position = 0;
        return PdfSyntax.Read(fixture.Bytes).Resolve(new(1));
    }

    private sealed class Probe : IPdfImageCodecProvider
    {
        public PdfProviderDescriptor Descriptor { get; } = new("test.image", "1", PdfCapability.ImageCodecs);

        internal int Width = 2, Count = 2;
        internal bool Decoded;
        internal Stream? Input, Output;
        public bool Supports(PdfImageCodec codec, bool encoding) => codec == PdfImageCodec.Dct;
        public PdfImageInfo Inspect(PdfImageCodec codec, Stream encodedInput, PdfValue dictionary, PdfOperationContext context) => new(Width, 1, 1, 8);
        public void Decode(PdfImageCodec codec, PdfImageInfo info, PdfValue dictionary, PdfValue parameters, Stream input, Stream output, PdfOperationContext context)
        {
            Decoded = true;
            Input = input;
            Output = output;
            for (int i = 0; i < Count; i++)
                output.WriteByte((byte)(i + 1));
        }

        public void Encode(PdfImageCodec codec, PdfImageInfo info, PdfValue parameters, Stream input, Stream output, PdfOperationContext context) => throw new NotSupportedException();
    }
}
