using System.IO;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class RenderingTests
{
    private sealed class Renderer : IPdfPageRenderer
    {
        internal PdfRasterRequest? Request;
        internal PdfOperationContext? Context;
        internal Stream? Output;
        internal Action<PdfRasterRequest, Stream, PdfOperationContext>? Action;
        public PdfProviderDescriptor Descriptor { get; } = new("test.renderer", "1", PdfCapability.RenderPages);

        public void Render(PdfRasterRequest request, Stream bgraOutput, PdfOperationContext context)
        {
            Request = request;
            Context = context;
            Output = bgraOutput;
            if (Action != null)
            {
                Action(request, bgraOutput, context);
                return;
            }

            context.Consume(PdfResourceKind.ExpandedBytes, 4);
            var pixel = new byte[]
            {
                40,
                40,
                40,
                255
            };
            for (long i = 0; i < request.ByteLength; i += 4)
            {
                context.Checkpoint();
                bgraOutput.Write(pixel, 0, 4);
            }
        }
    }

    [Fact]
    public void ExactPixelGridCropAndDetachedBorrowedOutputAreStable()
    {
        using var document = Pdf.Create().AddPage(new PdfPageSize(101, 79));
        var renderer = new Renderer();
        var result = PdfRendering.RenderPage(document.CreateSnapshot(), 0, new(renderer), new(96, 144, new(.25, .75, 20, 10)));
        var request = result.Request;
        Assert.Equal(135, request.FullWidth);
        Assert.Equal(158, request.FullHeight);
        Assert.Equal(0, request.X);
        Assert.Equal(1, request.Y);
        Assert.Equal(28, request.Width);
        Assert.Equal(21, request.Height);
        Assert.Equal(135d / 101 * 72, request.EffectiveDpiX, 10);
        Assert.Equal(144, request.EffectiveDpiY);
        Assert.True(request.Region.X <= .25 && request.Region.Y <= .75 && request.Region.Right >= 20.25 && request.Region.Top >= 10.75);
        Assert.Equal(request.Width * request.Height, result.Report.Resources.Single(x => x.Resource == PdfResourceKind.Pixels).Consumed);
        Assert.Throws<ObjectDisposedException>(() => renderer.Output!.WriteByte(0));
        Assert.Throws<ObjectDisposedException>(() => renderer.Context!.Checkpoint());
        document.Dispose();
        using var first = result.OpenSamples();
        using var second = result.OpenSamples();
        Assert.Equal(request.ByteLength, first.Length);
        Assert.Equal(40, first.ReadByte());
        Assert.Equal(0, second.Position);
        Assert.False(first.CanWrite);
    }

    [Theory]
    [InlineData(PdfPageRotation.None, 80, 40)]
    [InlineData(PdfPageRotation.Clockwise90, 40, 80)]
    [InlineData(PdfPageRotation.Clockwise180, 80, 40)]
    [InlineData(PdfPageRotation.Clockwise270, 40, 80)]
    public void CropOriginUserUnitAndRotationDefineTheDisplayedGrid(PdfPageRotation rotation, int width, int height)
    {
        using var doc = Pdf.Create().AddPage(new PdfPageGeometry(new(new(-20, 10, 100, 80), new(0, 30, 40, 20)), rotation, 2));
        var result = PdfRendering.RenderPage(doc.CreateSnapshot(), 0, new(new Renderer()), new(72, 72));
        Assert.Equal(width, result.Request.Width);
        Assert.Equal(height, result.Request.Height);
        Assert.Equal(0, result.Request.Region.X);
        Assert.Equal(0, result.Request.Region.Y);
    }

    [Fact]
    public void ChangedSnapshotIsPreparedOnceWithoutChangingOriginalSource()
    {
        using var doc = NavigationTests.Fixture((_, _, _) =>
        {
        });
        var original = doc.SourceDocument;
        doc.Pages[0].Size = new(40, 20);
        var renderer = new Renderer();
        var result = PdfRendering.RenderPage(doc.CreateSnapshot(), 0, new(renderer), new(72, 72));
        Assert.Equal(40, result.Request.Width);
        Assert.Equal(20, result.Request.Height);
        Assert.NotSame(original, renderer.Request!.Page.Document);
        Assert.Same(original, doc.SourceDocument);
    }

    [Fact]
    public void PixelAndOutputLimitsFailBeforeProviderAllocationOrPublication()
    {
        using var doc = Pdf.Create().AddPage(new PdfPageSize(20, 20));
        var renderer = new Renderer();
        Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => PdfRendering.RenderPage(doc.CreateSnapshot(), 0, new(renderer), new(72, 72), new(limits: new(maximumPixels: 399)))).Diagnostic.Code);
        Assert.Null(renderer.Request);
        Assert.Equal("Limit.OutputBytes", Assert.Throws<PdfException>(() => PdfRendering.RenderPage(doc.CreateSnapshot(), 0, new(renderer), new(72, 72), new(limits: new(maximumOutputBytes: 1599)))).Diagnostic.Code);
    }

    [Fact]
    public void ShortLongAndCaughtLatchedProviderFailuresNeverReturnARaster()
    {
        using var doc = Pdf.Create().AddPage(new PdfPageSize(1, 1));
        var renderer = new Renderer
        {
            Action = (_, _, _) =>
            {
            }
        };
        Assert.Equal("Render.OutputLength", Assert.Throws<PdfException>(() => PdfRendering.RenderPage(doc.CreateSnapshot(), 0, new(renderer), new(72, 72))).Diagnostic.Code);
        renderer.Action = (_, output, _) => output.Write(new byte[5], 0, 5);
        Assert.Equal("Codec.OutputLength", Assert.Throws<PdfException>(() => PdfRendering.RenderPage(doc.CreateSnapshot(), 0, new(renderer), new(72, 72))).Diagnostic.Code);
        renderer.Action = (_, _, context) =>
        {
            try
            {
                context.Consume(PdfResourceKind.Pixels, long.MaxValue);
            }
            catch (PdfException)
            {
            }
        };
        Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => PdfRendering.RenderPage(doc.CreateSnapshot(), 0, new(renderer), new(72, 72))).Diagnostic.Code);
    }

    [Fact]
    public void CancellationBeforeDispatchAndDuringProviderWorkIsObserved()
    {
        using var doc = Pdf.Create().AddPage(new PdfPageSize(1, 1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var renderer = new Renderer();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfRendering.RenderPage(doc.CreateSnapshot(), 0, new(renderer), cancellationToken: cancellation.Token));
        Assert.Null(renderer.Request);
        using var during = new CancellationTokenSource();
        renderer.Action = (_, _, context) =>
        {
            during.Cancel();
            context.Checkpoint();
        };
        Assert.ThrowsAny<OperationCanceledException>(() => PdfRendering.RenderPage(doc.CreateSnapshot(), 0, new(renderer), cancellationToken: during.Token));
    }

    [Fact]
    public void InvalidDpiRegionsAndHugeIntegerGridsFailWithNoDispatch()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfRenderOptions(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfRenderOptions(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfRenderOptions(backgroundRgb: 0x1000000));
        using var doc = Pdf.Create().AddPage(new PdfPageSize(20, 20));
        var renderer = new Renderer();
        Assert.Equal("Render.Region", Assert.Throws<PdfException>(() => PdfRendering.RenderPage(doc.CreateSnapshot(), 0, new(renderer), new(region: new(-1, 0, 10, 10)))).Diagnostic.Code);
        Assert.Null(renderer.Request);
        using var huge = Pdf.Create().AddPage(new PdfPageGeometry(new(new(0, 0, 14400, 14400)), userUnit: 75000));
        Assert.Equal("Render.Extent", Assert.Throws<PdfException>(() => PdfRendering.RenderPage(huge.CreateSnapshot(), 0, new(renderer), new(9600, 9600))).Diagnostic.Code);
    }

    [Fact]
    public void MissingProviderNeverTriggersDiscoveryOrFallback()
    {
        using var doc = Pdf.Create().AddPage();
        Assert.Throws<PdfProviderUnavailableException>(() => PdfRendering.RenderPage(doc.CreateSnapshot(), 0, PdfProviderRegistry.Default));
    }

    [Fact]
    public void InvalidAlphaAndGraySamplesAreRejectedAndBothLanguageConsumersUseTheContract()
    {
        using var doc = Pdf.Create().AddPage(new PdfPageSize(1, 1));
        var renderer = new Renderer();
        renderer.Action = (_, stream, _) => stream.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
        Assert.Equal("Render.OutputAlpha", Assert.Throws<PdfException>(() => PdfRendering.RenderPage(doc.CreateSnapshot(), 0, new(renderer), new(72, 72))).Diagnostic.Code);
        renderer.Action = (_, stream, _) => stream.Write(new byte[] { 1, 2, 3, 255 }, 0, 4);
        Assert.Equal("Render.OutputColor", Assert.Throws<PdfException>(() => PdfRendering.RenderPage(doc.CreateSnapshot(), 0, new(renderer), new(72, 72, color: PdfRasterColor.Gray))).Diagnostic.Code);
        Assert.Throws<ArgumentException>(() => new PdfRenderOptions(color: PdfRasterColor.Gray, backgroundRgb: 0xff0000));
        renderer.Action = null;
        using var bytes = new MemoryStream();
        doc.Save(bytes);
        bytes.Position = 0;
        var cs = ContractExamples.CSharp.ContractExample.RenderPagePreview(bytes, new(renderer), new(72, 72));
        bytes.Position = 0;
        var vb = ContractExamples.VisualBasic.ContractExample.RenderPagePreview(bytes, new(renderer), new(72, 72));
        using var a = cs.OpenSamples();
        using var b = vb.OpenSamples();
        Assert.Equal(a.ReadByte(), b.ReadByte());
        Assert.True(bytes.CanRead);
    }
}
