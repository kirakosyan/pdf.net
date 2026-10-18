using System.IO;
using System.Text;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class InlineImageContentTests
{
    [Fact]
    public void RawPixelsContainingAnEiSequenceCannotEndTheImage()
    {
        byte[] pixels = Encoding.ASCII.GetBytes(" EI q Q ");
        var program = Read("/W 8 /H 1 /BPC 8 /CS /G", pixels);
        var image = program.Operations.Single(x => x.Operation == PdfOperator.InlineImage);
        using var data = image.OpenInlineImageData();
        Assert.Equal(pixels, Copy(data));
        Assert.Equal(8, image.InlineImageLength);
        Assert.Equal(3, program.Operations.Count);
        using var output = new MemoryStream();
        program.Write(output);
        output.Position = 0;
        Assert.Equal(pixels, Copy(PdfContentProgram.Parse(output).Operations[1].OpenInlineImageData()));
    }

    [Theory]
    [InlineData("FlateDecode")]
    [InlineData("LZWDecode")]
    [InlineData("ASCIIHexDecode")]
    [InlineData("ASCII85Decode")]
    [InlineData("RunLengthDecode")]
    public void CompressedImageEndsAtTheOuterFilterBoundary(string filter)
    {
        byte[] pixels = Encoding.ASCII.GetBytes(" EI q Q ");
        var context = new PdfOperationContext("InlineFixture", "test", PdfCapability.WriteDocument, PdfOperationPolicy.Default, default);
        byte[] encoded;
        try
        {
            using var data = PdfFilters.Encode(new MemoryStream(pixels), new[] { new PdfFilter(filter) }, context);
            encoded = Copy(data);
            context.EnsureComplete();
            context.Finish(true);
        }
        finally
        {
            if (context.IsActive)
                context.Finish(false);
            context.DisposeSources();
        }

        var program = Read("/W 8 /H 1 /BPC 8 /CS /G /F /" + filter, encoded);
        Assert.Equal(encoded, Copy(program.Operations[1].OpenInlineImageData()));
        Assert.Equal(3, program.Operations.Count);
        var dictionary = PdfValue.Dictionary(new Dictionary<string, PdfValue> { { "Width", PdfValue.Integer(8) }, { "Height", PdfValue.Integer(1) }, { "BitsPerComponent", PdfValue.Integer(8) }, { "ColorSpace", PdfValue.Name("DeviceGray") }, { "Filter", PdfValue.Name(filter) } });
        using var input = new MemoryStream(encoded);
        Assert.Equal(encoded.Length, PdfContentOperation.InlineImage(dictionary, input).InlineImageLength);
        Assert.True(input.CanRead);
    }

    [Fact]
    public void IndependentJpegAndFaxBoundariesPreserveTheirEncodedBytes()
    {
        byte[] jpeg = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "filters", "images", "rgb.jpg"));
        Assert.Equal(jpeg, Copy(Read("/W 64 /H 16 /BPC 8 /CS /RGB /F /DCT", jpeg).Operations[1].OpenInlineImageData()));
        byte[] fax = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "filters", "ccitt", "transitions-g4.bin"));
        Assert.Equal(fax, Copy(Read("/W 65 /H 16 /BPC 1 /CS /G /F /CCF /DP << /K -1 /Columns 65 /Rows 16 >>", fax).Operations[1].OpenInlineImageData()));
    }

    [Theory]
    [InlineData("/W 10 /H 1 /BPC 8 /CS /G", "123", "Content.InlineTruncated")]
    [InlineData("/W 1 /Width 1 /H 1 /BPC 8 /CS /G", "x", "Content.InlineDuplicateKey")]
    [InlineData("/W 0 /H 1 /BPC 8 /CS /G", "x", "Content.InlineGeometry")]
    [InlineData("/W 1 /H 1 /BPC 3 /CS /G", "x", "Content.InlineBits")]
    [InlineData("/W 1 /H 1 /IM true /CS /G", "x", "Content.InlineMask")]
    [InlineData("/W 1 /H 1 /BPC 8 /CS /Named", "x", "Content.InlineColorSpaceRequired")]
    [InlineData("/W 1 /H 1 /BPC 8 /CS /G /F /Unknown", "x", "Content.InlineBoundaryUnsupported")]
    public void MalformedOrUnresolvedInlineImagesFailWithoutGuessing(string dictionary, string pixels, string code)
    {
        Assert.Equal(code, Assert.Throws<PdfException>(() => Read(dictionary, Encoding.ASCII.GetBytes(pixels))).Diagnostic.Code);
    }

    [Fact]
    public void PixelLimitIsEnforcedBeforeReadingCompressedBytes()
    {
        var bytes = Encoding.ASCII.GetBytes("BI /W 100 /H 100 /BPC 8 /CS /G /F /Fl ID malformed EI");
        var failure = Assert.Throws<PdfException>(() => PdfContentProgram.Parse(new MemoryStream(bytes), policy: new(limits: new(maximumPixels: 9999))));
        Assert.Contains("Pixels", failure.Diagnostic.Code);
    }

    internal static PdfContentProgram Read(string dictionary, byte[] pixels)
    {
        using var data = new MemoryStream();
        var prefix = Encoding.ASCII.GetBytes("q BI " + dictionary + " ID\n");
        data.Write(prefix, 0, prefix.Length);
        data.Write(pixels, 0, pixels.Length);
        var suffix = Encoding.ASCII.GetBytes("\nEI Q");
        data.Write(suffix, 0, suffix.Length);
        data.Position = 0;
        return PdfContentProgram.Parse(data);
    }

    private static byte[] Copy(Stream stream)
    {
        using (stream)
        {
            using var output = new MemoryStream();
            stream.CopyTo(output);
            return output.ToArray();
        }
    }
}
