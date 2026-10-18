using System.IO;
using System.Security.Cryptography;
using System.Text;
using Fluent.Pdf.Rendering;
using Fluent.Pdf.Workers;
using Xunit;
using Xunit.Abstractions;

namespace Fluent.Pdf.Contract.Tests;
public sealed class NativeRenderingTests
{
    private readonly ITestOutputHelper _output;
    public NativeRenderingTests(ITestOutputHelper output) => _output = output;
    private static PdfDocument Drawing(PdfPageGeometry? geometry = null, string content = "1 0 0 rg 30 35 10 10 re f")
    {
        var document = Pdf.Create().AddPage(geometry ?? new(new(new(0, 0, 80, 60))));
        using var bytes = new MemoryStream(Encoding.ASCII.GetBytes(content));
        document.Pages[0].Content = PdfContentProgram.Parse(bytes);
        return document;
    }

    private static byte[] Samples(PdfRaster raster)
    {
        using var input = raster.OpenSamples();
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Pixel(byte[] samples, PdfRasterRequest request, int x, int y) => samples.Skip(y * request.Stride + x * 4).Take(4).ToArray();
    [Theory]
    [InlineData(PdfPageRotation.None)]
    [InlineData(PdfPageRotation.Clockwise90)]
    [InlineData(PdfPageRotation.Clockwise180)]
    [InlineData(PdfPageRotation.Clockwise270)]
    public void NativeCropOriginUserUnitAndAllRotationsPaintAtKnownDisplayCoordinates(PdfPageRotation rotation)
    {
        using var fixture = new Fixture(_output);
        var geometry = new PdfPageGeometry(new(new(10, 20, 100, 60), new(20, 30, 60, 20)), rotation, 2);
        using var document = Drawing(geometry);
        var raster = PdfRendering.RenderPage(document.CreateSnapshot(), 0, fixture.Providers, new(72, 72));
        var center = geometry.UserToDisplay().Transform(new(35, 40));
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(Samples(raster), raster.Request, (int)center.X, (int)center.Y));
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, Pixel(Samples(raster), raster.Request, 1, 1));
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Fact]
    public void RegionSamplesEqualTheSameIntegerWindowOfTheFullRaster()
    {
        using var fixture = new Fixture(_output);
        using var document = Drawing();
        var full = PdfRendering.RenderPage(document.CreateSnapshot(), 0, fixture.Providers, new(113, 157));
        var region = PdfRendering.RenderPage(document.CreateSnapshot(), 0, fixture.Providers, new(113, 157, new(25.25, 12.5, 20.75, 20.5)));
        var all = Samples(full);
        var window = Samples(region);
        var r = region.Request;
        for (int y = 0; y < r.Height; y++)
            Assert.Equal(all.Skip((r.Y + y) * full.Request.Stride + r.X * 4).Take(r.Stride), window.Skip(y * r.Stride).Take(r.Stride));
    }

    [Fact]
    public void TransparentAndOpaqueBackgroundsAndDeviceGrayHaveExplicitSamples()
    {
        using var fixture = new Fixture(_output);
        using var document = Drawing();
        var transparent = PdfRendering.RenderPage(document.CreateSnapshot(), 0, fixture.Providers, new(72, 72, backgroundRgb: null));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, Pixel(Samples(transparent), transparent.Request, 1, 1));
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(Samples(transparent), transparent.Request, 35, 20));
        var matte = PdfRendering.RenderPage(document.CreateSnapshot(), 0, fixture.Providers, new(72, 72, backgroundRgb: 0x123456));
        Assert.Equal(new byte[] { 0x56, 0x34, 0x12, 255 }, Pixel(Samples(matte), matte.Request, 1, 1));
        var gray = PdfRendering.RenderPage(document.CreateSnapshot(), 0, fixture.Providers, new(72, 72, color: PdfRasterColor.Gray));
        var pixel = Pixel(Samples(gray), gray.Request, 35, 20);
        Assert.Equal(pixel[0], pixel[1]);
        Assert.Equal(pixel[1], pixel[2]);
        Assert.InRange(pixel[0], (byte)1, (byte)254);
    }

    [Fact]
    public void ExistingWidgetAppearancesMatchStaticFlatteningAndCanBeExcluded()
    {
        using var fixture = new Fixture(_output);
        using var source = FormWorkflowTests.Source();
        var snapshot = source.CreateSnapshot();
        var original = Samples(PdfRendering.RenderPage(snapshot, 0, fixture.Providers, new(72, 72)));
        using var flat = PdfForms.Flatten(snapshot);
        var flattened = Samples(PdfRendering.RenderPage(flat.CreateSnapshot(), 0, fixture.Providers, new(72, 72)));
        Assert.Equal(original, flattened);
        var hidden = Samples(PdfRendering.RenderPage(snapshot, 0, fixture.Providers, new(72, 72, annotations: false)));
        Assert.NotEqual(original, hidden);
        Assert.All(hidden, b => Assert.Equal((byte)255, b));
    }

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(4, true, true)]
    [InlineData(36, false, true)]
    [InlineData(6, false, false)]
    [InlineData(5, true, true)]
    public void PrintIntentUsesAnnotationVisibilityWithoutExecutingActions(int flags, bool screenVisible, bool printVisible)
    {
        using var fixture = new Fixture(_output);
        using var blank = Drawing(content: "");
        var paint = PdfGraphicsObjects.Form(PdfDrawing.Create(new[] { new PdfContentOperation(PdfOperator.Rectangle, PdfValue.Integer(10), PdfValue.Integer(2), PdfValue.Integer(80), PdfValue.Integer(16)), new PdfContentOperation(PdfOperator.Fill) }), new(0, 0, 100, 20));
        using var forms = PdfForms.Add(blank.CreateSnapshot(), new("Visible", PdfFormFieldKind.CheckBox, new[] { new PdfFormWidgetDefinition(0, new(5, 5, 40, 20), paint, "On", paint) }, PdfValue.Name("On")));
        var field = Assert.Single(PdfForms.Inspect(forms.CreateSnapshot()).Fields);
        var widget = Assert.Single(field.Widgets);
        using var changed = PdfForms.EditWidget(field, widget.Id, new(0, new(5, 5, 40, 20), paint, "On", paint, annotationFlags: flags));
        var screen = PdfRendering.RenderPage(changed.CreateSnapshot(), 0, fixture.Providers, new(72, 72));
        var print = PdfRendering.RenderPage(changed.CreateSnapshot(), 0, fixture.Providers, new(72, 72, print: true));
        Assert.Equal(screenVisible ? new byte[] { 0, 0, 0, 255 } : new byte[] { 255, 255, 255, 255 }, Pixel(Samples(screen), screen.Request, 25, 45));
        Assert.Equal(printVisible ? new byte[] { 0, 0, 0, 255 } : new byte[] { 255, 255, 255, 255 }, Pixel(Samples(print), print.Request, 25, 45));
    }

    [Fact]
    public void UnsupportedExternalFontsAndViewerRegenerationFailBeforeAWorkerStarts()
    {
        using var fixture = new Fixture(_output);
        using var source = NavigationTests.Fixture((edit, root, pages) =>
        {
            var font = edit.Add(NavigationTests.D(("Type", PdfValue.Name("Font")), ("Subtype", PdfValue.Name("TrueType")), ("BaseFont", PdfValue.Name("Unavailable"))));
            NavigationTests.Put(edit, pages[0], "Resources", NavigationTests.D(("Font", NavigationTests.D(("F", PdfValue.Reference(font))))));
        });
        Assert.Equal("Render.FontRequired", Assert.Throws<PdfException>(() => PdfRendering.RenderPage(source.CreateSnapshot(), 0, fixture.Providers)).Diagnostic.Code);
        using var dynamic = FormTests.Source((edit, root, _, _) => NavigationTests.Put(edit, root, "AcroForm", NavigationTests.D(("Fields", PdfValue.Array()), ("NeedAppearances", PdfValue.Boolean(true)))));
        Assert.Equal("Render.AppearanceRequired", Assert.Throws<PdfException>(() => PdfRendering.RenderPage(dynamic.CreateSnapshot(), 0, fixture.Providers)).Diagnostic.Code);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Fact]
    public void NativeMemoryEnvelopeIsChargedBeforeLaunch()
    {
        using var fixture = new Fixture(_output);
        using var document = Drawing();
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PdfRendering.RenderPage(document.CreateSnapshot(), 0, fixture.Providers, policy: new(limits: new(maximumExpandedBytes: 16777216)))).Diagnostic.Code);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Fact]
    public void ImagesAndTransparencyRetainKnownColorsAndStraightAlpha()
    {
        using var fixture = new Fixture(_output);
        using var document = Drawing(content: "q 20 0 0 20 5 5 cm /Im Do Q q /Half gs 1 0 0 rg 35 5 20 20 re f Q");
        document.Pages[0].SetResource("Im", CanvasTests.Image());
        document.Pages[0].SetResource("Half", PdfGraphicsState.Create("Normal", .5).Resource);
        var raster = PdfRendering.RenderPage(document.CreateSnapshot(), 0, fixture.Providers, new(72, 72, backgroundRgb: null));
        var samples = Samples(raster);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(samples, raster.Request, 8, 45));
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, Pixel(samples, raster.Request, 22, 45));
        var half = Pixel(samples, raster.Request, 45, 45);
        Assert.Equal(new byte[] { 0, 0, 255 }, half.Take(3));
        Assert.InRange(half[3], (byte)127, (byte)128);
    }

    [Fact]
    public void DefaultOptionalContentControlsFormPaintingWithoutFlattening()
    {
        using var fixture = new Fixture(_output);
        using var document = Drawing(content: "/Art Do");
        var red = PdfDrawing.Create(new[] { new PdfContentOperation(PdfOperator.FillRgb, PdfValue.Integer(1), PdfValue.Integer(0), PdfValue.Integer(0)), new PdfContentOperation(PdfOperator.Rectangle, PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(80), PdfValue.Integer(60)), new PdfContentOperation(PdfOperator.Fill) });
        document.Pages[0].SetResource("Art", PdfGraphicsObjects.Form(red, new(0, 0, 80, 60)));
        using var group = PdfLayers.AddGroup(document.CreateSnapshot(), "Hidden", visible: false);
        var id = Assert.Single(PdfLayers.Inspect(group.CreateSnapshot()).Groups).Id;
        using var bound = PdfLayers.BindXObject(group.CreateSnapshot(), 0, "Art", PdfValue.Reference(id));
        var hidden = Samples(PdfRendering.RenderPage(bound.CreateSnapshot(), 0, fixture.Providers, new(72, 72)));
        Assert.All(hidden, b => Assert.Equal((byte)255, b));
        using var visible = PdfLayers.SetConfiguration(bound.CreateSnapshot(), NavigationTests.D(("BaseState", PdfValue.Name("ON"))));
        var raster = PdfRendering.RenderPage(visible.CreateSnapshot(), 0, fixture.Providers, new(72, 72));
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(Samples(raster), raster.Request, 40, 30));
    }

    private sealed class ReportingRenderer : IPdfPageRenderer
    {
        private readonly IPdfPageRenderer _inner;
        private readonly ITestOutputHelper _output;
        internal ReportingRenderer(IPdfPageRenderer inner, ITestOutputHelper output)
        {
            _inner = inner;
            _output = output;
        }

        public PdfProviderDescriptor Descriptor => _inner.Descriptor;

        public void Render(PdfRasterRequest request, Stream output, PdfOperationContext context)
        {
            try
            {
                _inner.Render(request, output, context);
            }
            catch (PdfException failure)
            {
                _output.WriteLine(failure.Diagnostic.Code);
                foreach (string key in new[]
                {
                    "Native.WorkerDiagnostics",
                    "Native.FailureType",
                    "Native.FailureErrorCode",
                    "Native.WorkerFailureDetail"
                }

                )
                    if (failure.Data[key] is object value)
                        _output.WriteLine(key + ": " + value);
                throw;
            }
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "fluent-pdf-render-" + Guid.NewGuid().ToString("N"));
        internal string Jobs { get; }
        internal PdfProviderRegistry Providers { get; }

        internal Fixture(ITestOutputHelper output)
        {
            Jobs = Path.Combine(_root, "jobs");
            Directory.CreateDirectory(Jobs);
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (repo != null && !File.Exists(Path.Combine(repo.FullName, "Fluent.Pdf.slnx")))
                repo = repo.Parent;
            string rid = PdfNativeDeploymentInspector.OperatingSystemRuntimeIdentifier ?? throw new InvalidOperationException();
            string root = Path.Combine(repo!.FullName, "artifacts", "native-fixtures", rid), extension = rid.StartsWith("win-", StringComparison.Ordinal) ? "dll" : rid.StartsWith("osx-", StringComparison.Ordinal) ? "dylib" : "so", exe = rid.StartsWith("win-", StringComparison.Ordinal) ? "worker.exe" : "worker", bridge = "fluent-pdf-render." + extension, engine = rid.StartsWith("win-", StringComparison.Ordinal) ? "pdfium.dll" : "libpdfium." + extension;
            var deployment = new PdfNativeDeployment(root, rid, "page.rendering", IsolatedPdfRenderer.BridgeVersion, IsolatedPdfRenderer.BridgeAbi, 67108864, Asset(root, exe), Asset(root, bridge), Asset(root, engine));
            var worker = new NativeWorkerOptions(deployment, exe, bridge, Jobs, 268435456, 67108864, 16777216, TimeSpan.FromSeconds(10), captureWorkerDiagnostics: true);
            Providers = new(new ReportingRenderer(new IsolatedPdfRenderer(worker), output));
            output.WriteLine("Candidate PDFium " + IsolatedPdfRenderer.BridgeVersion + "; isolated bridge; " + rid);
        }

        private static PdfNativeAsset Asset(string root, string name)
        {
            using var input = File.OpenRead(Path.Combine(root, name));
            using var sha = SHA256.Create();
            return new(name, input.Length, BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant());
        }

        public void Dispose()
        {
            Directory.Delete(Jobs);
            Directory.Delete(_root);
        }
    }
}
