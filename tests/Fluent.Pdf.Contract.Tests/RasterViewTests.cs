using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BitMiracle.LibTiff.Classic;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class RasterViewTests
{
    internal sealed class PatternRenderer : IPdfPageRenderer, IPdfColorManagedPageRenderer
    {
        internal int Calls; internal PdfColorRenderOptions? Color; internal PdfOperationContext? Context;
        internal Action<PdfOperationContext>? BeforeRender;
        public PdfProviderDescriptor Descriptor { get; } = new("test.grid-color-renderer", "1", PdfCapability.RenderPages, PdfCapability.RenderColorManaged);
        public void Render(PdfRasterRequest r, Stream output, PdfOperationContext c)
        {
            Interlocked.Increment(ref Calls);
            Context = c;
            BeforeRender?.Invoke(c);
            c.Consume(PdfResourceKind.ExpandedBytes, 4);
            var pixel = new byte[4];
            for (int y = 0; y < r.Height; y++)
            for (int x = 0; x < r.Width; x++)
            {
                c.Checkpoint();
                pixel[0] = (byte)(x + r.X);
                pixel[1] = (byte)(y + r.Y);
                pixel[2] = (byte)(r.PageIndex * 40);
                pixel[3] = 255;
                output.Write(pixel, 0, 4);
            }
        }
        public void Render(PdfRasterRequest request, PdfColorRenderOptions options, Stream output, PdfOperationContext context)
        {
            Color = options;
            Render(request, output, context);
        }
    }
    [Theory]
    [InlineData(.1, .3, .8)]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 1)]
    public void MatrixIccRoundTripIsBoundedAndExplicit(double r, double g, double b)
    {
        using var input = File.OpenRead(GraphicsTests.Fixture("original-srgb.icc"));
        var profile = PdfColorSpace.Icc(input);
        var result = PdfMatrixIccTransform.Transform(new(profile, r, g, b), profile);
        Assert.Equal(r, result.Color.Components[0], 6);
        Assert.Equal(g, result.Color.Components[1], 6);
        Assert.Equal(b, result.Color.Components[2], 6);
        Assert.Throws<PdfException>(() => PdfMatrixIccTransform.Transform(new(PdfColorSpace.Rgb, r, g, b), profile));
        Assert.Throws<PdfException>(() => PdfMatrixIccTransform.Transform(new(profile, r, g, b), profile, new(limits: new(maximumExpandedBytes: 128))));
    }
    [Fact]
    public void MatrixIccConversionToLinearRgbMatchesTheDeclaredSrgbTransferFunction()
    {
        byte[] original = File.ReadAllBytes(GraphicsTests.Fixture("original-srgb.icc"));
        byte[] linear = (byte[])original.Clone();
        uint Read32(int at) => (uint)linear[at] << 24 | (uint)linear[at + 1] << 16 | (uint)linear[at + 2] << 8 | linear[at + 3];
        void Write32(int at, uint value) { for (int j = 3; j >= 0; j--) { linear[at + j] = (byte)value; value >>= 8; } }
        int tags = (int)Read32(128);
        for (int tag = 0; tag < tags; tag++)
        {
            int at = 132 + tag * 12;
            string name = System.Text.Encoding.ASCII.GetString(linear, at, 4);
            if (name != "rTRC" && name != "gTRC" && name != "bTRC") continue;
            int curve = (int)Read32(at + 4); Write32(at + 8, 16);
            linear[curve + 8] = 0; linear[curve + 9] = 0; Write32(curve + 12, 65536);
        }
        using var sourceInput = new MemoryStream(original); using var targetInput = new MemoryStream(linear);
        var sourceProfile = PdfColorSpace.Icc(sourceInput); var targetProfile = PdfColorSpace.Icc(targetInput);
        foreach (double component in new[] { .04, .5, 128d / 255 })
        {
            var transformed = PdfMatrixIccTransform.Transform(new(sourceProfile, component, component, component), targetProfile);
            double expected = component <= .04045 ? component / 12.92 : Math.Pow((component + .055) / 1.055, 2.4);
            Assert.All(transformed.Color.Components, actual => Assert.InRange(actual, expected - .0001, expected + .0001));
        }
    }

    [Fact]
    public void ColorRequestsPreserveExplicitProfileIntentOverprintAndPrintPolicies()
    {
        using var input = File.OpenRead(GraphicsTests.Fixture("original-srgb.icc"));
        var profile = PdfColorSpace.Icc(input);
        using var document = Pdf.Create().AddPage(new PdfPageSize(8, 6));
        var provider = new PatternRenderer();
        var color = new PdfColorRenderOptions(profile, PdfColorRenderingIntent.AbsoluteColorimetric, true, PdfSpotColorPolicy.CompositeAlternate);
        var raster = PdfColorRendering.RenderPage(document.CreateSnapshot(), 0, new(provider), color, new(72, 72, print: true));
        Assert.Same(color, provider.Color);
        Assert.True(raster.Request.Options.Print);
        Assert.Throws<ObjectDisposedException>(() => provider.Context!.Checkpoint());
        Assert.Throws<PdfProviderUnavailableException>(() => PdfColorRendering.RenderPage(document.CreateSnapshot(), 0, PdfProviderRegistry.Empty, color));
        Assert.Throws<PdfException>(() => PdfColorRendering.RenderPage(document.CreateSnapshot(), 0, new(provider), color, policy: new(limits: new(maximumPixels: 1))));
    }
    [Fact]
    public void ContainersCarryExactPixelsDpiAlphaAndMultipageOrder()
    {
        using var document = Pdf.Create().AddPage(new PdfPageSize(8, 6)).AddPage(new PdfPageSize(5, 4));
        var providers = new PdfProviderRegistry(new PatternRenderer());
        string directory = Path.Combine(AppContext.BaseDirectory, "raster-fixtures");
        Directory.CreateDirectory(directory);
        foreach (var format in new[] { PdfRasterFormat.Png, PdfRasterFormat.Bmp, PdfRasterFormat.Tiff })
        {
            using var output = new MemoryStream();
            PdfRasterExport.Write(document.CreateSnapshot(), format == PdfRasterFormat.Tiff ? new[] { 1, 0 } : new[] { 0 }, output, new(format, new(72, 72)), providers);
            Assert.True(output.CanWrite);
            File.WriteAllBytes(Path.Combine(directory, "grid." + format.ToString().ToLowerInvariant()), output.ToArray());
        }
        using var tiff = Tiff.Open(Path.Combine(directory, "grid.tiff"), "r");
        Assert.NotNull(tiff);
        Assert.Equal(2, tiff.NumberOfDirectories());
        Assert.Equal(5, tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt());
        Assert.Equal(4, tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt());
        Assert.Equal(72d, tiff.GetField(TiffTag.XRESOLUTION)[0].ToDouble(), 5);
        var row = new byte[tiff.ScanlineSize()];
        Assert.True(tiff.ReadScanline(row, 0));
        Assert.Equal(new byte[] { 40, 0, 0, 255 }, row.Take(4));
        Assert.True(tiff.ReadDirectory());
        Assert.Equal(8, tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt());
        Assert.True(tiff.ReadScanline(row = new byte[tiff.ScanlineSize()], 1));
        Assert.Equal(new byte[] { 0, 1, 0, 255 }, row.Take(4));
        Assert.Throws<ArgumentException>(() => new PdfRasterExportOptions(PdfRasterFormat.Bmp, new(backgroundRgb: null)));
        using var untouched = new MemoryStream();
        Assert.Throws<PdfException>(() => PdfRasterExport.Write(document.CreateSnapshot(), new[] { 0, 1 }, untouched, new(PdfRasterFormat.Png), providers));
        Assert.Equal(0, untouched.Length);
        Assert.Throws<PdfException>(() => PdfRasterExport.Write(document.CreateSnapshot(), new[] { 0 }, untouched, new(PdfRasterFormat.Png), providers, new(limits: new(maximumPixels: 1))));
        Assert.Equal(0, untouched.Length);
    }
    [Fact]
    public async Task ViewerTilesShareTheExactPageGridAndCacheEvictsWithoutInvalidatingReturnedRasters()
    {
        using var document = Pdf.Create().AddPage(new PdfPageGeometry(new(new(-20, 10, 101, 79)), PdfPageRotation.Clockwise90, 1.3));
        var renderer = new PatternRenderer();
        using var viewer = new PdfViewSession(document.CreateSnapshot(), new(renderer), maximumCachedBytes: 3000);
        var full = await viewer.RenderAsync(0, new(97, 97));
        var expected = SignatureTests.Bytes(full.OpenSamples());
        var coverage = new int[full.Request.FullWidth * full.Request.FullHeight];
        foreach (var tile in viewer.PlanTiles(0, 97, 17))
        {
            var raster = await viewer.RenderAsync(0, tile);
            var part = SignatureTests.Bytes(raster.OpenSamples());
            var r = raster.Request;
            for (int y = 0; y < r.Height; y++)
            for (int x = 0; x < r.Width; x++)
            {
                int index = (y + r.Y) * r.FullWidth + x + r.X;
                coverage[index]++;
                Assert.Equal(expected.Skip(index * 4).Take(4), part.Skip((y * r.Width + x) * 4).Take(4));
            }
            Assert.True(viewer.CachedBytes <= 3000);
        }
        Assert.All(coverage, n => Assert.Equal(1, n));
        int before = renderer.Calls;
        var thumbnail = await viewer.RenderThumbnailAsync(0, 12, 12);
        Assert.True(thumbnail.Request.Width <= 12 && thumbnail.Request.Height <= 12);
        Assert.Same(thumbnail, await viewer.RenderThumbnailAsync(0, 12, 12));
        Assert.Equal(before + 1, renderer.Calls);
        viewer.ClearCache();
        Assert.Equal(0, viewer.CachedBytes);
        using var retained = thumbnail.OpenSamples();
        Assert.True(retained.Length > 0);
    }
    [Fact]
    public async Task StaleRequestsCancelWithinTheRequestBoundAndNewRequestsRemainUsable()
    {
        using var document = Pdf.Create().AddPage(new PdfPageSize(10, 10));
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderer = new PatternRenderer { BeforeRender = c => { started.TrySetResult(true); c.CancellationToken.WaitHandle.WaitOne(); c.Checkpoint(); } };
        using var viewer = new PdfViewSession(document.CreateSnapshot(), new(renderer), maximumOutstandingRequests: 1);
        var task = viewer.RenderAsync(0, new(72, 72));
        await started.Task;
        Assert.Equal("View.RequestLimit", Assert.Throws<PdfException>(() => { _ = viewer.RenderAsync(0, new(96, 96)); }).Diagnostic.Code);
        viewer.CancelPending();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        renderer.BeforeRender = null;
        Assert.NotNull(await viewer.RenderAsync(0, new(72, 72)));
        viewer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => { _ = viewer.RenderAsync(0); });
    }
    [Fact]
    public void SelectionAndHitTestingUseDisplayedGeometryAndKeepCharacterProvenance()
    {
        using var document = ExtractionTests.Fixture("BT /F1 10 Tf 1 0 0 1 20 100 Tm (ABC) Tj ET");
        using var viewer = new PdfViewSession(document.CreateSnapshot(), new(new PatternRenderer()));
        var selection = viewer.SelectText(0, new(19, 290, 8, 20));
        Assert.Contains(selection, c => c.Text == "A");
        Assert.Equal("A", viewer.HitTest(0, new(23, 298))!.Text);
        Assert.Null(viewer.HitTest(0, new(250, 10)));
        var mapped = viewer.DisplayToUser(0, new(20, 300));
        Assert.Equal(20, mapped.X);
        Assert.Equal(100, mapped.Y);
    }
}
