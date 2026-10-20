using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class RasterImportTests
{
    private static PdfImageAsset Image(PdfDocument document, int page)
    {
        var graphics = PdfPageGraphics.Inspect(document.CreateSnapshot(), page);
        int index = Enumerable.Range(0, graphics.Occurrences.Count).Single(i => graphics.Occurrences[i].Kind == PdfGraphicsOccurrenceKind.Image);
        return graphics.ExtractImage(index);
    }
    [Fact]
    public void ManagedPngAndJpegComposeExplicitPagesWithoutLosingAlphaOrOrientation()
    {
        using var png = new MemoryStream(ImageAssetTests.Fixture("rgba.png")); using var jpeg = new MemoryStream(ImageAssetTests.Fixture("orientation-6.jpg"));
        using var result = PdfRasterDocuments.Create(new[] { new PdfRasterInput(png, PdfRasterFileFormat.Png, new[] { 0 }), new PdfRasterInput(jpeg, PdfRasterFileFormat.Jpeg, new[] { 0 }) }, new(imageOptions: new(allowLosslessTranscode: true)));
        Assert.Equal(2, result.Pages.Count); Assert.True(png.CanRead); Assert.True(jpeg.CanRead);
        var alpha = Image(result, 0).SoftMask; Assert.NotNull(alpha); Assert.Equal(new byte[] { 255, 128, 0, 64, 255, 192 }, SignatureTests.Bytes(alpha!.OpenSamples()));
        using var original = new MemoryStream(ImageAssetTests.Fixture("orientation-6.jpg")); var image = PdfImages.Read(original);
        Assert.Equal(image.NaturalSize!.Value, result.Pages[1].Size);
    }
    [Fact]
    public void OptionalFramesPreserveExactOrderDensityOrientationColorAndAlpha()
    {
        using var input = new MemoryStream(Encoding.ASCII.GetBytes("original frame fixture")); var decoder = new Decoder();
        using var result = PdfRasterDocuments.Create(new[] { new PdfRasterInput(input, PdfRasterFileFormat.Tiff, new[] { 1, 0, 1 }) }, codecs: new(decoder));
        Assert.Equal(3, result.Pages.Count); Assert.Equal(new PdfPageSize(1, 2), result.Pages[0].Size); Assert.Equal(new PdfPageSize(2, 3), result.Pages[1].Size);
        Assert.Equal(new[] { 1, 0, 1 }, decoder.Frames);
        Assert.Equal(80, SignatureTests.Bytes(Image(result, 0).OpenSamples())[0]); Assert.Equal(40, SignatureTests.Bytes(Image(result, 1).OpenSamples())[0]);
        Assert.All(SignatureTests.Bytes(Image(result, 0).SoftMask!.OpenSamples()), value => Assert.Equal(128, value));
        Assert.Throws<ObjectDisposedException>(() => decoder.Context!.Checkpoint()); Assert.True(input.CanRead);
    }
    [Fact]
    public void MissingCodecIsRejectedBeforeAnyInputIsReadAndAnimationIsExplicit()
    {
        using var png = new MemoryStream(ImageAssetTests.Fixture("rgb.png")); using var gif = new MemoryStream(new byte[] { 1 });
        Assert.Equal("RasterImport.CodecUnavailable", Assert.Throws<PdfException>(() => PdfRasterDocuments.Create(new[] { new PdfRasterInput(png, PdfRasterFileFormat.Png, new[] { 0 }), new PdfRasterInput(gif, PdfRasterFileFormat.Gif, new[] { 0 }) })).Diagnostic.Code); Assert.Equal(0, png.Position); Assert.Equal(0, gif.Position);
        var decoder = new Decoder();
        Assert.Equal("RasterImport.AnimationSelection", Assert.Throws<PdfException>(() => PdfRasterDocuments.Create(new[] { new PdfRasterInput(gif, PdfRasterFileFormat.Gif, new[] { 0 }) }, codecs: new(decoder))).Diagnostic.Code);
        gif.Position = 0; using var selected = PdfRasterDocuments.Create(new[] { new PdfRasterInput(gif, PdfRasterFileFormat.Gif, new[] { 1 }, allowAnimationFrames: true) }, codecs: new(decoder)); Assert.Single(selected.Pages);
    }
    [Fact]
    public void FrameLengthFailurePreservesStreamAndFileDestinations()
    {
        using var input = new MemoryStream(new byte[] { 1 }); var decoder = new Decoder { Short = true }; var sources = new[] { new PdfRasterInput(input, PdfRasterFileFormat.Tiff, new[] { 0 }) };
        using var output = new MemoryStream(); output.WriteByte(42);
        Assert.Equal("RasterImport.FrameLength", Assert.Throws<PdfException>(() => PdfRasterDocuments.Write(sources, output, codecs: new(decoder))).Diagnostic.Code); Assert.Equal(new byte[] { 42 }, output.ToArray());
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf"); File.WriteAllText(path, "retained");
        try { input.Position = 0; Assert.Throws<PdfException>(() => PdfRasterDocuments.WriteFile(sources, path, codecs: new(decoder))); Assert.Equal("retained", File.ReadAllText(path)); }
        finally { File.Delete(path); }
    }
    [Fact]
    public void ContentMismatchFrameIndicesAndResourceCancellationFailBeforeOutput()
    {
        using var input = new MemoryStream(ImageAssetTests.Fixture("rgb.png"));
        Assert.Equal("RasterImport.FormatMismatch", Assert.Throws<PdfException>(() => PdfRasterDocuments.Create(new[] { new PdfRasterInput(input, PdfRasterFileFormat.Jpeg, new[] { 0 }) })).Diagnostic.Code);
        input.Position = 0; Assert.Equal("RasterImport.FrameIndex", Assert.Throws<PdfException>(() => PdfRasterDocuments.Create(new[] { new PdfRasterInput(input, PdfRasterFileFormat.Png, new[] { 1 }) })).Diagnostic.Code);
        input.Position = 0; Assert.Throws<PdfException>(() => PdfRasterDocuments.Create(new[] { new PdfRasterInput(input, PdfRasterFileFormat.Png, new[] { 0 }) }, policy: new(limits: new(maximumPixels: 1))));
        input.Position = 0; Assert.ThrowsAny<OperationCanceledException>(() => PdfRasterDocuments.Create(new[] { new PdfRasterInput(input, PdfRasterFileFormat.Png, new[] { 0 }) }, cancellationToken: new CancellationToken(true)));
    }
    [Fact]
    public void StaticPngProfileRejectsApngAndExecutableConsumersRetainOwnership()
    {
        var png = ImageAssetTests.Fixture("rgb.png");
        // Original APNG mutation: acTL declares two frames; CRC computed independently.
        var animated = png.Take(33).Concat(Convert.FromBase64String("AAAACGFjVEwAAAACAAAAAPONk3A=")).Concat(png.Skip(33)).ToArray();
        using var apng = new MemoryStream(animated);
        Assert.Equal("Image.PngAnimation", Assert.Throws<PdfException>(() => PdfRasterDocuments.Create(new[] { new PdfRasterInput(apng, PdfRasterFileFormat.Png, new[] { 0 }, allowAnimationFrames: true) })).Diagnostic.Code);
        using var input = new MemoryStream(png);
        using var csharp = ContractExamples.CSharp.ContractExample.ImportRaster(new[] { new PdfRasterInput(input, PdfRasterFileFormat.Png, new[] { 0 }) }); Assert.Single(csharp.Pages);
        input.Position = 0; using var vb = ContractExamples.VisualBasic.ContractExample.ImportRaster(new[] { new PdfRasterInput(input, PdfRasterFileFormat.Png, new[] { 0 }) }); Assert.Single(vb.Pages); Assert.True(input.CanRead);
    }
    private sealed class Decoder : IPdfRasterDocumentDecoder
    {
        internal bool Short; internal PdfOperationContext? Context; internal readonly List<int> Frames = new();
        public PdfProviderDescriptor Descriptor { get; } = new("fixture.raster-file", "1", PdfCapability.DecodeRasterDocuments);
        public bool Supports(PdfRasterFileFormat format) => format == PdfRasterFileFormat.Tiff || format == PdfRasterFileFormat.Gif;
        public PdfRasterFileInfo Inspect(Stream file, PdfRasterFileFormat format, PdfOperationContext context) { Context = context; Assert.True(file.ReadByte() >= 0); return new(new[] { new PdfRasterFrameInfo(2, 3, new(horizontalDpi: 72, verticalDpi: 72)), new PdfRasterFrameInfo(4, 2, new(PdfImageOrientation.Rotate90, 144, 144)) }, format == PdfRasterFileFormat.Gif); }
        public void DecodeFrame(Stream file, PdfRasterFileFormat format, int frame, PdfRasterFrameInfo info, Stream rgbaOutput, PdfOperationContext context)
        {
            Assert.Same(Context, context); Frames.Add(frame); var bytes = new byte[(int)info.Samples.ByteLength];
            for (int i = 0; i < bytes.Length; i += 4) { bytes[i] = (byte)((frame + 1) * 40); bytes[i + 1] = 20; bytes[i + 2] = 10; bytes[i + 3] = 128; }
            rgbaOutput.Write(bytes, 0, bytes.Length - (Short ? 1 : 0));
        }
    }
}
