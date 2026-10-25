using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Fluent.Pdf.Applications;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ApplicationViewTests
{
    private static PdfDocument Document()
    {
        using var input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Automation", "original.pdf"));
        return Pdf.Open(input);
    }

    private sealed class PatternRenderer : IPdfPageRenderer, IDisposable
    {
        internal int Calls;
        internal bool BlockFirst;
        internal readonly ManualResetEventSlim Entered = new();
        internal readonly ManualResetEventSlim Release = new();
        public PdfProviderDescriptor Descriptor { get; } = new("application.fixture", "1", PdfCapability.RenderPages);

        public void Render(PdfRasterRequest request, Stream output, PdfOperationContext c)
        {
            int call = Interlocked.Increment(ref Calls);
            if (BlockFirst && call == 1)
            {
                Entered.Set();
                while (!Release.Wait(10))
                    c.Checkpoint();
            }

            c.Consume(PdfResourceKind.ExpandedBytes, request.Stride);
            byte[] row = new byte[request.Stride];
            for (int x = 0; x < request.Width; x++)
            {
                row[x * 4] = (byte)(request.PageIndex == 0 ? 200 : 50);
                row[x * 4 + 1] = 70;
                row[x * 4 + 2] = 90;
                row[x * 4 + 3] = 255;
            }

            for (int y = 0; y < request.Height; y++)
            {
                c.Checkpoint();
                output.Write(row, 0, row.Length);
            }
        }

        public void Dispose()
        {
            Entered.Dispose();
            Release.Dispose();
        }
    }

    [Fact]
    public async Task ExplicitViewCommandsRenderBoundedFramesAndCapturedUnicodeWithoutAnyUiDependency()
    {
        using var document = Document();
        using var renderer = new PatternRenderer();
        using var viewer = new PdfApplicationViewer(document.CreateSnapshot(), new(renderer));
        var frame = await viewer.RenderAsync();
        Assert.Equal("Original automation Ω", frame.CapturedText);
        Assert.Equal(0, frame.State.PageIndex);
        Assert.False(viewer.State.IsBusy);
        Assert.Equal(PdfApplicationViewCommand.NextPage, Enum.Parse(typeof(PdfApplicationViewCommand), "NextPage"));
        viewer.Execute(PdfApplicationViewCommand.NextPage);
        var second = await viewer.RenderAsync();
        Assert.Equal("Second automation page", second.CapturedText);
        Assert.Equal(1, viewer.Execute(PdfApplicationViewCommand.NextPage).PageIndex);
        Assert.Equal(0, viewer.Execute(PdfApplicationViewCommand.FirstPage).PageIndex);
        for (int i = 0; i < 20; i++)
            viewer.Execute(PdfApplicationViewCommand.ZoomIn);
        Assert.Equal(4, viewer.State.Zoom);
        for (int i = 0; i < 30; i++)
            viewer.Execute(PdfApplicationViewCommand.ZoomOut);
        Assert.Equal(.25, viewer.State.Zoom);
        Assert.Equal(1, viewer.Execute(PdfApplicationViewCommand.ActualSize).Zoom);
        Assert.True(viewer.CachedBytes > 0);
        string directory = Path.Combine(AppContext.BaseDirectory, "application-fixtures");
        Directory.CreateDirectory(directory);
        using var output = File.Create(Path.Combine(directory, "page-zero.png"));
        frame.WritePng(output);
        Assert.True(output.CanWrite);
    }

    [Fact]
    public async Task SupersededRequestsCannotPublishFramesOrCancelAnotherPrivateController()
    {
        using var document = Document();
        using var renderer = new PatternRenderer
        {
            BlockFirst = true
        };
        var providers = new PdfProviderRegistry(renderer);
        using var a = new PdfApplicationViewer(document.CreateSnapshot(), providers);
        using var b = new PdfApplicationViewer(document.CreateSnapshot(), providers);
        Task<PdfApplicationFrame> first = a.RenderAsync();
        Assert.True(renderer.Entered.Wait(TimeSpan.FromSeconds(10)));
        a.SetView(1, 1);
        var independent = await b.RenderAsync();
        Assert.Equal(0, independent.State.PageIndex);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        renderer.Release.Set();
        var current = await a.RenderAsync();
        Assert.Equal(1, current.State.PageIndex);
        Assert.Equal(current.State.Generation, a.State.Generation);
        Assert.Equal("", a.State.DiagnosticCode);
    }

    [Fact]
    public async Task SurfaceAndTextLimitsRejectWithoutImplicitRendererOrOcrFallback()
    {
        using var document = Document();
        using var renderer = new PatternRenderer();
        Assert.Throws<PdfProviderUnavailableException>(() => new PdfApplicationViewer(document.CreateSnapshot(), PdfProviderRegistry.Empty));
        using var tiny = new PdfApplicationViewer(document.CreateSnapshot(), new(renderer), maximumSurfacePixels: 1);
        Assert.Equal("ApplicationView.SurfaceLimit", (await Assert.ThrowsAsync<PdfException>(() => tiny.RenderAsync())).Diagnostic.Code);
        Assert.Equal(0, renderer.Calls);
        using var text = new PdfApplicationViewer(document.CreateSnapshot(), new(renderer), maximumTextCharacters: 1);
        Assert.Equal("ApplicationView.TextLimit", (await Assert.ThrowsAsync<PdfException>(() => text.RenderAsync())).Diagnostic.Code);
        Assert.Equal("ApplicationView.TextLimit", text.State.DiagnosticCode);
        using var pixelsOnly = new PdfApplicationViewer(document.CreateSnapshot(), new(renderer), captureText: false, maximumCachedBytes: 0);
        Assert.Equal("", (await pixelsOnly.RenderAsync()).CapturedText);
        Assert.Equal(0, pixelsOnly.CachedBytes);
    }

    [Fact]
    public async Task DisposalAndExplicitCancellationReleaseOnlyOwnedRequests()
    {
        using var document = Document();
        using var renderer = new PatternRenderer
        {
            BlockFirst = true
        };
        var viewer = new PdfApplicationViewer(document.CreateSnapshot(), new(renderer));
        var render = viewer.RenderAsync();
        Assert.True(renderer.Entered.Wait(TimeSpan.FromSeconds(10)));
        viewer.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => render);
        Assert.Equal("ApplicationView.Canceled", viewer.State.DiagnosticCode);
        viewer.Dispose();
        viewer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => viewer.State);
        Assert.Throws<ObjectDisposedException>(() => viewer.SetView(0, 1));
        using var newViewer = new PdfApplicationViewer(document.CreateSnapshot(), new(renderer));
        Assert.Equal("Original automation Ω", (await newViewer.RenderAsync()).CapturedText);
    }

    [Fact]
    public async Task DetachedPngEncodingPreservesBorrowedOutputAndHonorsItsOwnFiniteBudget()
    {
        using var document = Document();
        using var renderer = new PatternRenderer();
        using var viewer = new PdfApplicationViewer(document.CreateSnapshot(), new(renderer));
        var frame = await viewer.RenderAsync();
        using var output = new MemoryStream();
        Assert.Equal("Limit.OutputBytes", Assert.Throws<PdfException>(() => frame.WritePng(output, new(limits: new(maximumOutputBytes: 1)))).Diagnostic.Code);
        Assert.True(output.CanWrite);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => frame.WritePng(output, cancellationToken: canceled.Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => viewer.SetView(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => viewer.SetView(0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => viewer.Execute((PdfApplicationViewCommand)100));
    }
}
