using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fluent.Pdf.Html;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class WebViewTests
{
    [Theory]
    [InlineData(PdfViewKind.Razor)]
    [InlineData(PdfViewKind.Mvc)]
    [InlineData(PdfViewKind.Blazor)]
    public async Task ViewCaptureAndBrowserImportShareOneContextAndExpiringLeases(PdfViewKind kind)
    {
        var host = new Host(kind);
        var renderer = new Renderer();
        using var result = await PdfWebViews.RenderAsync(host, new(renderer));
        Assert.Single(result.Pages);
        Assert.Equal(1, host.Calls);
        Assert.Same(host.Context, renderer.Context);
        Assert.Equal("<h1>Invoice &lt;25&gt;</h1>", renderer.Markup);
        Assert.Throws<ObjectDisposedException>(() => host.Output!.WriteByte(1));
        Assert.Throws<ObjectDisposedException>(() => host.Context!.Checkpoint());
        Assert.False(renderer.Scripts);
        await Assert.ThrowsAsync<PdfProviderUnavailableException>(() => ContractExamples.CSharp.ContractExample.RenderViewAsync(host, PdfProviderRegistry.Empty));
        await Assert.ThrowsAsync<PdfProviderUnavailableException>(() => ContractExamples.VisualBasic.ContractExample.RenderViewAsync(host, PdfProviderRegistry.Empty));
    }
    [Fact]
    public async Task ViewFailuresDoNotExposeModelSecretsOrInventEmptyOutput()
    {
        var renderer = new Renderer();
        var providers = new PdfProviderRegistry(renderer);
        var host = new Host(PdfViewKind.Mvc) { Available = false };
        Assert.Equal("View.HostUnavailable", (await Assert.ThrowsAsync<PdfException>(() => PdfWebViews.RenderAsync(host, providers))).Diagnostic.Code);
        Assert.Equal(0, host.Calls);
        host.Available = true;
        host.Fail = true;
        var failure = await Assert.ThrowsAsync<PdfException>(() => PdfWebViews.RenderAsync(host, providers));
        Assert.Equal("View.HostFailure", failure.Diagnostic.Code);
        Assert.DoesNotContain("secret-model", failure.ToString());
        host.Fail = false;
        host.Empty = true;
        Assert.Equal("View.EmptyOutput", (await Assert.ThrowsAsync<PdfException>(() => PdfWebViews.RenderAsync(host, providers))).Diagnostic.Code);
        host.Empty = false;
        await Assert.ThrowsAsync<PdfException>(() => PdfWebViews.RenderAsync(host, providers, policy: new(limits: new(maximumExpandedBytes: 100))));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfWebViews.RenderAsync(host, providers, cancellationToken: new CancellationToken(true)));
    }
    [Theory]
    [InlineData(PdfViewKind.WebForms)]
    [InlineData(PdfViewKind.Xaml)]
    public async Task PlatformSpecificViewsCannotInvokeHostsOnUnsupportedRuntimes(PdfViewKind kind)
    {
        var host = new Host(kind);
        var renderer = new Renderer();
        bool available = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && (kind == PdfViewKind.Xaml || RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.Ordinal));
        if (available)
        {
            using var result = await PdfWebViews.RenderAsync(host, new(renderer));
            Assert.Single(result.Pages);
            Assert.Equal(1, host.Calls);
        }
        else
        {
            Assert.Equal("View.Platform", (await Assert.ThrowsAsync<PdfException>(() => PdfWebViews.RenderAsync(host, new(renderer)))).Diagnostic.Code);
            Assert.Equal(0, host.Calls);
        }
    }
    private sealed class Host : IPdfViewHost
    {
        public PdfViewKind Kind
        {
            get;
        }
        internal int Calls; internal bool Available = true, Fail, Empty;
        internal PdfOperationContext? Context; internal Stream? Output;
        private readonly byte[] _pdf = Renderer.Bytes();
        internal Host(PdfViewKind kind) => Kind = kind;
        public bool IsAvailable => Available;
        public async Task RenderAsync(Stream output, PdfOperationContext context)
        {
            Calls++;
            Context = context;
            Output = output;
            await Task.Yield();
            context.Checkpoint();
            if (Fail)
                throw new IOException("secret-model");
            if (Empty)
                return;
            byte[] bytes = Kind == PdfViewKind.Xaml ? _pdf : Encoding.UTF8.GetBytes("<h1>Invoice &lt;25&gt;</h1>");
            output.Write(bytes, 0, bytes.Length);
        }
    }
    private sealed class Renderer : IPdfHtmlRenderer
    {
        internal PdfOperationContext? Context; internal string? Markup; internal bool Scripts;
        private readonly byte[] _pdf = Bytes();
        public PdfProviderDescriptor Descriptor { get; } = new("fixture.view-browser", "1", PdfCapability.RenderHtml);
        public void Render(PdfHtmlSource source, PdfHtmlOptions options, Stream output, PdfOperationContext context)
        {
            Context = context;
            Markup = source.Markup;
            Scripts = options.EnableJavaScript;
            output.Write(_pdf, 0, _pdf.Length);
        }
        internal static byte[] Bytes()
        {
            using var document = Pdf.Create().AddPage();
            using var output = new MemoryStream();
            document.Save(output);
            return output.ToArray();
        }
    }
}
