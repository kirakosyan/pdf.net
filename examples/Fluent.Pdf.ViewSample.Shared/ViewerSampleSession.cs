using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Fluent.Pdf;
using Fluent.Pdf.Applications;
using Fluent.Pdf.Rendering;
using Fluent.Pdf.Workers;

namespace Fluent.Pdf.ViewSamples;
// Explicit developer sample bootstrap; the normal path never substitutes the fixture renderer.
internal sealed class ViewerSampleSession : IDisposable
{
    internal PdfDocument Document { get; }
    internal PdfApplicationViewer Viewer { get; }
    internal bool IsFixture { get; }
    internal PdfProviderRegistry Providers { get; }

    private ViewerSampleSession(PdfDocument document, PdfProviderRegistry providers, bool fixture)
    {
        Document = document;
        Providers = providers;
        Viewer = new(document.CreateSnapshot(), providers, maximumSurfacePixels: 4000000);
        IsFixture = fixture;
    }

    internal static ViewerSampleSession Open(string[] arguments)
    {
        bool fixture = arguments.Length == 1 && arguments[0] == "--self-test";
        if (!fixture && arguments.Length != 2)
            throw new ArgumentException("Supply an explicit PDF path and offline-rendering XML configuration, or --self-test for labeled UI boundary fixtures.");
        PdfProviderRegistry providers;
        string pdf;
        if (fixture)
        {
            providers = new(new SampleFrameRenderer());
            pdf = Path.Combine(AppContext.BaseDirectory, "original.pdf");
        }
        else
        {
            providers = Configure(arguments[1]);
            pdf = Path.GetFullPath(arguments[0]);
        }

        using var input = File.OpenRead(pdf);
        var document = Pdf.Open(input);
        try
        {
            return new(document, providers, fixture);
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static PdfProviderRegistry Configure(string configuration)
    {
        using var input = File.OpenRead(Path.GetFullPath(configuration));
        using var reader = XmlReader.Create(input, new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
        var root = XElement.Load(reader, LoadOptions.None);
        if (root.Name != "rendering" || root.Attributes().Any(a => !new[] { "root", "rid", "worker", "bridge", "jobs" }.Contains(a.Name.LocalName) || a.Name.NamespaceName != ""))
            throw new InvalidDataException("An explicit closed rendering configuration is required.");
        string Required(string name)
        {
            string? value = (string? )root.Attribute(name);
            if (string.IsNullOrWhiteSpace(value) || value!.Length > 32767)
                throw new InvalidDataException("A required bounded rendering setting is missing.");
            return value;
        }

        var assets = new List<PdfNativeAsset>();
        foreach (var item in root.Elements())
        {
            if (item.Name != "asset" || item.HasElements || assets.Count == 64 || item.Attributes().Any(a => !new[] { "file", "bytes", "sha256", "native" }.Contains(a.Name.LocalName) || a.Name.NamespaceName != ""))
                throw new InvalidDataException("Only bounded explicit asset records are accepted.");
            if (!long.TryParse((string? )item.Attribute("bytes"), NumberStyles.None, CultureInfo.InvariantCulture, out long length))
                throw new InvalidDataException("An exact asset byte length is required.");
            bool native = (string? )item.Attribute("native") != "false";
            if (item.Attribute("native") != null && (string? )item.Attribute("native") != "true" && (string? )item.Attribute("native") != "false")
                throw new InvalidDataException("Asset native flag must be explicit true/false.");
            assets.Add(new((string? )item.Attribute("file") ?? "", length, (string? )item.Attribute("sha256") ?? "", native));
        }

        var deployment = new PdfNativeDeployment(Required("root"), Required("rid"), "page.rendering", IsolatedPdfRenderer.BridgeVersion, IsolatedPdfRenderer.BridgeAbi, 536870912, assets.ToArray());
        return new(new IsolatedPdfRenderer(new NativeWorkerOptions(deployment, Required("worker"), Required("bridge"), Required("jobs"))));
    }

    public void Dispose()
    {
        Viewer.Dispose();
        Document.Dispose();
    }

    // Original constant-color contract fixture, enabled only by --self-test. No PDF fidelity claim.
    private sealed class SampleFrameRenderer : IPdfPageRenderer
    {
        public PdfProviderDescriptor Descriptor { get; } = new("application.ui-fixture", "1", PdfCapability.RenderPages);

        public void Render(PdfRasterRequest request, Stream output, PdfOperationContext context)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, request.Stride);
            var row = new byte[request.Stride];
            for (int x = 0; x < request.Width; x++)
            {
                row[x * 4] = 200;
                row[x * 4 + 1] = 70;
                row[x * 4 + 2] = 90;
                row[x * 4 + 3] = 255;
            }

            for (int y = 0; y < request.Height; y++)
            {
                context.Checkpoint();
                output.Write(row, 0, row.Length);
            }
        }
    }
}
