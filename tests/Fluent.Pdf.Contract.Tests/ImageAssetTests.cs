using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ImageAssetTests
{
    internal static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "images", name));
    private static PdfImageAsset Read(string file, bool convert = false)
    {
        using var input = new MemoryStream(Fixture(file));
        return PdfImages.Read(input, new(convert));
    }

    private static byte[] Samples(PdfImageAsset image)
    {
        using var input = image.OpenSamples();
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    private static readonly byte[] Colors =
    {
        255,
        0,
        0,
        0,
        255,
        0,
        0,
        0,
        255,
        255,
        255,
        255,
        0,
        0,
        0,
        255,
        255,
        0
    };
    [Fact]
    public void PngKeepsOriginalIdatAndOriginalFileWhileReturningExactSamples()
    {
        var image = Read("rgb.png");
        Assert.Equal(Colors, Samples(image));
        Assert.Equal("FlateDecode", image.Definition.Entries["Filter"].NameValue);
        Assert.Equal(15, image.Definition.Entries["DecodeParms"].Entries["Predictor"].IntegerValue);
        using var original = new MemoryStream();
        image.WriteOriginalFile(original);
        Assert.Equal(Fixture("rgb.png"), original.ToArray());
        using var encoded = new MemoryStream();
        image.WriteEncoded(encoded);
        Assert.Equal(Idat(Fixture("rgb.png")), encoded.ToArray());
        Assert.True(original.CanWrite);
        Assert.True(encoded.CanWrite);
    }

    [Fact]
    public void RgbaAndPaletteAlphaRetainEveryColorAndOpacitySample()
    {
        Assert.Equal("Image.LosslessTranscodeRequired", Assert.Throws<PdfException>(() => Read("rgba.png")).Diagnostic.Code);
        var rgba = Read("rgba.png", true);
        Assert.Equal(Colors, Samples(rgba));
        Assert.Equal(new byte[] { 255, 128, 0, 64, 255, 192 }, Samples(rgba.SoftMask!));
        Assert.Equal("1.4", rgba.Resource.MinimumVersion);
        var palette = Read("palette-alpha.png", true);
        Assert.Equal(1, palette.Info.Components);
        Assert.Equal(new byte[] { 24, 208 }, Samples(palette));
        Assert.Equal(new byte[] { 0, 128, 255, 64, 128, 0 }, Samples(palette.SoftMask!));
        using var encoded = new MemoryStream();
        palette.WriteEncoded(encoded);
        Assert.Equal(Idat(Fixture("palette-alpha.png")), encoded.ToArray());
        Assert.Contains(palette.OperationReport.Diagnostics, d => d.Code == "Image.PaletteMask");
    }

    [Fact]
    public void Adam7AndSixteenBitPrecisionHaveExactKnownSamples()
    {
        Assert.Equal("Image.LosslessTranscodeRequired", Assert.Throws<PdfException>(() => Read("adam7-rgb.png")).Diagnostic.Code);
        Assert.Equal(Colors, Samples(Read("adam7-rgb.png", true)));
        var image = Read("rgb16.png");
        Assert.Equal(16, image.Info.BitsPerComponent);
        Assert.Equal("1.5", image.Resource.MinimumVersion);
        Assert.Equal(new byte[] { 0, 0, 17, 17, 34, 34, 51, 51, 68, 68, 85, 85, 102, 102, 119, 119, 136, 136 }, Samples(image));
        Assert.Equal(new byte[] { 85, 170 }, Samples(Read("mono.png")));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void JpegPreservesExactPacketsAndAppliesExifAndPhysicalPixelAspect(int orientation)
    {
        var image = Read("orientation-" + orientation + ".jpg");
        Assert.Equal((PdfImageOrientation)orientation, image.Metadata.Orientation);
        Assert.Equal(144, image.Metadata.HorizontalDpi);
        Assert.Equal(72, image.Metadata.VerticalDpi);
        Assert.Equal(orientation >= 5 ? new PdfPageSize(2, 1.5) : new PdfPageSize(1.5, 2), image.NaturalSize!.Value);
        using var encoded = new MemoryStream();
        image.WriteEncoded(encoded);
        Assert.Equal(Fixture("orientation-" + orientation + ".jpg"), encoded.ToArray());
        var canvas = PdfCanvas.Create(CanvasTests.SimpleGeometry(), c => c.DrawImageAsset(image, new(0, 0, 60, 40)));
        Assert.Equal(image.DisplayAspect, canvas.Placements[0].Bounds.Width / canvas.Placements[0].Bounds.Height, 10);
    }

    [Fact]
    public void CmykProfilesAndStandardRgbDataRemainExplicitAndExact()
    {
        var cmyk = Read("cmyk.jpg");
        Assert.Equal(4, cmyk.Info.Components);
        Assert.Equal("DeviceCMYK", cmyk.Definition.Entries["ColorSpace"].NameValue);
        Assert.Equal(new double[] { 1, 0, 1, 0, 1, 0, 1, 0 }, cmyk.Definition.Entries["Decode"].Items.Select(v => v.NumberValue));
        foreach (string file in new[]
        {
            "icc.png",
            "icc.jpg",
            "srgb.png"
        }

        )
        {
            var image = Read(file);
            var space = image.Definition.Entries["ColorSpace"];
            Assert.Equal("ICCBased", space.Items[0].NameValue);
            using var profile = space.Items[1].OpenRawStream();
            using var sha = SHA256.Create();
            Assert.Equal("1d6dc237618bcc3814610377d7a1abb3b4e7df3f1f6dccb4dc6abd7dd94b1f25", string.Concat(sha.ComputeHash(profile).Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture))));
        }

        Assert.Equal("RelativeColorimetric", Read("srgb.png").Definition.Entries["Intent"].NameValue);
    }

    [Fact]
    public void RawRgbaStencilsHardMasksAndColorKeysKeepSampleBytes()
    {
        using var input = new MemoryStream(new byte[] { 1, 2, 3, 255, 4, 5, 6, 128 });
        var image = PdfImages.FromRgba(input, 2, 1);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, Samples(image));
        Assert.Equal(new byte[] { 255, 128 }, Samples(image.SoftMask!));
        Assert.True(input.CanRead);
        using var mono = new MemoryStream(new byte[] { 64 });
        var mask = PdfImages.FromStencil(mono, 2, 1);
        using var rgb = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6 });
        var plain = PdfImages.FromSamples(rgb, new(2, 1, 3, 8), PdfColorSpace.Rgb);
        var explicitMask = PdfImages.WithHardMask(plain, mask);
        Assert.Equal("1.3", explicitMask.Resource.MinimumVersion);
        Assert.Same(mask.Definition, explicitMask.Definition.Entries["Mask"]);
        var key = PdfImages.WithColorKey(plain, new[] { 1, 1, 2, 2, 3, 3 });
        Assert.Equal(6, key.Definition.Entries["Mask"].Items.Count);
        Assert.Equal(Samples(plain), Samples(key));
        Assert.Equal("Image.HardMask", Assert.Throws<PdfException>(() => PdfImages.WithHardMask(image, mask)).Diagnostic.Code);
    }

    [Fact]
    public void BadGeometryCrcSampleLengthsPixelsAndCancellationFailBeforeUnsafeWork()
    {
        byte[] corrupt = Fixture("rgb.png");
        corrupt[corrupt.Length - 1] ^= 1;
        using var broken = new MemoryStream(corrupt);
        Assert.Equal("Image.PngCrc", Assert.Throws<PdfException>(() => PdfImages.Read(broken)).Diagnostic.Code);
        using var pixels = new MemoryStream(Fixture("rgb.png"));
        Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => PdfImages.Read(pixels, policy: new(limits: new(maximumPixels: 1)))).Diagnostic.Code);
        using var shortInput = new MemoryStream(new byte[] { 1, 2 });
        Assert.Equal("Image.SampleLength", Assert.Throws<PdfException>(() => PdfImages.FromSamples(shortInput, new(1, 1, 3, 8), PdfColorSpace.Rgb)).Diagnostic.Code);
        using var longInput = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        Assert.Equal("Image.SampleLength", Assert.Throws<PdfException>(() => PdfImages.FromSamples(longInput, new(1, 1, 3, 8), PdfColorSpace.Rgb)).Diagnostic.Code);
        using var cancelled = new MemoryStream(Fixture("rgb.png"));
        Assert.Throws<OperationCanceledException>(() => PdfImages.Read(cancelled, cancellationToken: new CancellationToken(true)));
        Assert.Equal(0, cancelled.Position);
        Assert.Equal(32, new PdfImageInfo(1, 1, 32, 8).ByteLength);
    }

    private static byte[] Idat(byte[] bytes)
    {
        using var output = new MemoryStream();
        int offset = 8;
        while (offset < bytes.Length)
        {
            int size = bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];
            if (bytes[offset + 4] == 'I' && bytes[offset + 5] == 'D' && bytes[offset + 6] == 'A' && bytes[offset + 7] == 'T')
                output.Write(bytes, offset + 8, size);
            offset += size + 12;
        }

        return output.ToArray();
    }
}
