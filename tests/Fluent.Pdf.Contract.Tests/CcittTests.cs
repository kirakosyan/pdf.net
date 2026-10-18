using System.IO;
using BitMiracle.LibTiff.Classic;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class CcittTests
{
    [Theory]
    [InlineData("transitions", 65, 16, "g4", -1, false)]
    [InlineData("transitions", 65, 16, "g3-1d", 0, false)]
    [InlineData("transitions", 65, 16, "g3-mixed", 2, false)]
    [InlineData("transitions", 65, 16, "g3-mixed-fill", 2, true)]
    [InlineData("long-runs", 3456, 8, "g4", -1, false)]
    [InlineData("long-runs", 3456, 8, "g3-1d", 0, false)]
    [InlineData("long-runs", 3456, 8, "g3-mixed", 2, false)]
    [InlineData("long-runs", 3456, 8, "g3-mixed-fill", 2, true)]
    public void IndependentFaxCorpusIncludesVerticalPassHorizontalAndMakeupCodes(string label, int width, int height, string encoding, int k, bool align)
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "filters", "ccitt");
        byte[] expected = File.ReadAllBytes(Path.Combine(root, label + ".raw")), encoded = File.ReadAllBytes(Path.Combine(root, label + "-" + encoding + ".bin"));
        byte[] decoded = Decode(encoded, Parameters(width, height, k, true, k < 0, align, k >= 0));
        Assert.Equal(expected, decoded);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void EncodesBitonalRowsForEveryGroupAndExplicitPolarity(int k)
    {
        foreach (int width in new[]
        {
            1,
            7,
            8,
            9,
            65,
            1728,
            3456
        }

        )
            foreach (bool align in new[]
            {
                false,
                true
            }

            )
                foreach (bool eol in new[]
                {
                    false,
                    true
                }

                )
                {
                    const int height = 9;
                    int stride = (width + 7) / 8;
                    byte[] original = new byte[stride * height];
                    new Random(width + k).NextBytes(original);
                    if (width % 8 != 0)
                        for (int y = 0; y < height; y++)
                            original[(y + 1) * stride - 1] &= (byte)(255 << (8 - width % 8));
                    var parameters = Parameters(width, height, k, true, true, align, eol);
                    byte[]? encoded = null;
                    PolicyTests.Run(context =>
                    {
                        using var source = new MemoryStream(original);
                        using var output = PdfFilters.Encode(source, new[] { new PdfFilter("CCITTFaxDecode", parameters) }, context);
                        encoded = Copy(output);
                        Assert.True(source.CanRead);
                    });
                    Assert.Equal(original, Decode(encoded!, parameters));
                }
    }

    [Fact]
    public void KnownOnePixelCodesAndPolarityAreExact()
    {
        // White terminating code 1 is 000111; black 1 is 010 after white 0 (00110101).
        Assert.Equal(new byte[] { 0x00 }, Decode(new byte[] { 0x1C }, Parameters(1, 1, 0, true, false, false, false)));
        Assert.Equal(new byte[] { 0xFF }, Decode(new byte[] { 0x1C }, Parameters(1, 1, 0, false, false, false, false)));
        Assert.Equal(new byte[] { 0x80 }, Decode(new byte[] { 0x35, 0x40 }, Parameters(1, 1, 0, true, false, false, false)));
        Assert.Equal(new byte[] { 0x7F }, Decode(new byte[] { 0x35, 0x40 }, Parameters(1, 1, 0, false, false, false, false)));
    }

    [Fact]
    public void PixelLimitsRejectBeforeReadingTheEncoderInput()
    {
        using var source = new MemoryStream(new byte[1]);
        var failure = Assert.Throws<PdfException>(() => PolicyTests.Run(context => PdfFilters.Encode(source, new[] { new PdfFilter("CCF", Parameters(8, 1, -1, true, true, false, false)) }, context), new PdfResourceLimits(maximumPixels: 7)));
        Assert.Equal("Limit.Pixels", failure.Diagnostic.Code);
        Assert.Equal(0, source.Position);
        Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => Decode(new byte[0], Parameters(8, 1, -1, true, true, false, false), new PdfResourceLimits(maximumPixels: 7))).Diagnostic.Code);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void IndependentTiffDecoderAcceptsOurOutput(int k)
    {
        const int width = 65, height = 16;
        string root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "filters", "ccitt");
        byte[] original = File.ReadAllBytes(Path.Combine(root, "transitions.raw"));
        byte[]? encoded = null;
        PolicyTests.Run(context =>
        {
            using var source = new MemoryStream(original);
            using var output = PdfFilters.Encode(source, new[] { new PdfFilter("CCF", Parameters(width, height, k, true, false, false, true)) }, context);
            encoded = Copy(output);
        });
        if (k == 0)
            Assert.Equal(File.ReadAllBytes(Path.Combine(root, "transitions-g3-1d.bin")), encoded);
        using var container = new MemoryStream();
        using (var writer = Tiff.ClientOpen("independent-owned-fixture", "w", container, new BorrowedTiffStream()))
        {
            Assert.NotNull(writer);
            writer.SetField(TiffTag.IMAGEWIDTH, width);
            writer.SetField(TiffTag.IMAGELENGTH, height);
            writer.SetField(TiffTag.BITSPERSAMPLE, 1);
            writer.SetField(TiffTag.SAMPLESPERPIXEL, 1);
            writer.SetField(TiffTag.ROWSPERSTRIP, height);
            writer.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
            writer.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISWHITE);
            writer.SetField(TiffTag.COMPRESSION, k < 0 ? Compression.CCITTFAX4 : Compression.CCITTFAX3);
            if (k > 0)
                writer.SetField(TiffTag.GROUP3OPTIONS, Group3Opt.ENCODING2D);
            Assert.Equal(encoded!.Length, writer.WriteRawStrip(0, encoded, encoded.Length));
            Assert.True(writer.WriteDirectory());
        }

        container.Position = 0;
        using var reader = Tiff.ClientOpen("independent-owned-fixture", "r", container, new BorrowedTiffStream());
        Assert.NotNull(reader);
        byte[] decoded = new byte[original.Length];
        Assert.Equal(decoded.Length, reader.ReadEncodedStrip(0, decoded, 0, decoded.Length));
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void EndMarkersCanBoundUnknownRowsAndMalformedFaxNeverReturnsPartialSamples()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "filters", "ccitt");
        byte[] encoded = File.ReadAllBytes(Path.Combine(root, "transitions-g4.bin")), expected = File.ReadAllBytes(Path.Combine(root, "transitions.raw"));
        Assert.Equal(expected, Decode(encoded, Parameters(65, 0, -1, true, true, false, false)));
        for (int length = 0; length < encoded.Length - 1; length++)
            Assert.Throws<PdfException>(() => Decode(encoded.Take(length).ToArray(), Parameters(65, 16, -1, true, true, false, false)));
        Assert.Throws<PdfException>(() => Decode(new byte[] { 0, 0 }, Parameters(8, 1, 0, true, false, false, false)));
        // T.6 extension prefix is explicit, rather than being interpreted as raster data.
        Assert.Equal("Filter.CcittExtensionUnsupported", Assert.Throws<PdfException>(() => Decode(new byte[] { 2 }, Parameters(8, 1, -1, true, false, false, false))).Diagnostic.Code);
        var random = new Random(5528);
        for (int iteration = 0; iteration < 500; iteration++)
        {
            byte[] mutation = (byte[])encoded.Clone();
            mutation[random.Next(mutation.Length)] = (byte)random.Next(256);
            try
            {
                Decode(mutation, Parameters(65, 16, -1, true, true, false, false));
            }
            catch (PdfException)
            {
            }
        }
    }

    private sealed class BorrowedTiffStream : TiffStream
    {
        public override void Close(object clientData)
        {
        }
    }

    private static byte[] Decode(byte[] bytes, PdfValue parameters, PdfResourceLimits? limits = null)
    {
        byte[]? result = null;
        PolicyTests.Run(context =>
        {
            using var source = new MemoryStream(bytes);
            var output = PdfCcitt.Decode(source, parameters, PdfValue.Null, context);
            using var view = output.Open(0, output.Length);
            result = Copy(view);
        }, limits);
        return result!;
    }

    private static PdfValue Parameters(int width, int height, int k, bool black, bool end, bool align, bool eol) => PdfValue.Dictionary(new Dictionary<string, PdfValue> { ["Columns"] = PdfValue.Integer(width), ["Rows"] = PdfValue.Integer(height), ["K"] = PdfValue.Integer(k), ["BlackIs1"] = PdfValue.Boolean(black), ["EndOfBlock"] = PdfValue.Boolean(end), ["EncodedByteAlign"] = PdfValue.Boolean(align), ["EndOfLine"] = PdfValue.Boolean(eol) });
    private static byte[] Copy(Stream source)
    {
        using var target = new MemoryStream();
        source.CopyTo(target);
        return target.ToArray();
    }
}
