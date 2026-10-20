using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Fluent.Pdf.Html;

/// <summary>Explicit trusted application-view boundary. These names do not assert that a host/framework is installed.</summary>
public enum PdfViewKind
{
    /// <summary>A trusted compiled Razor/CSHTML view yielding UTF8 HTML.</summary>
    Razor,
    /// <summary>A trusted MVC view yielding UTF8 HTML.</summary>
    Mvc,
    /// <summary>A trusted Blazor static HTML render.</summary>
    Blazor,
    /// <summary>A trusted ASPX/WebForms host on Windows .NET Framework, yielding UTF8 HTML.</summary>
    WebForms,
    /// <summary>A trusted Windows XAML print host yielding a complete PDF.</summary>
    Xaml
}
/// <summary>Caller-owned application rendering host. Only trusted application templates/components/models belong here; this boundary does not sandbox arbitrary template code.</summary>
public interface IPdfViewHost
{
    /// <summary>Gets the exact view family; XAML emits PDF, other families emit well-formed UTF8 HTML.</summary>
    PdfViewKind Kind
    {
        get;
    }
    /// <summary>Reports whether the explicitly provisioned framework/dispatcher/compiled view is available.</summary>
    bool IsAvailable
    {
        get;
    }
    /// <summary>Renders once into an expiring bounded output lease. Forward the shared cancellation token to request/dispatcher work and finish every write before returning.</summary>
    Task RenderAsync(Stream output, PdfOperationContext context);
}
/// <summary>Optional framework-neutral bridge from explicit trusted application views into the SDK's bounded HTML/PDF import. Core imports no web or XAML types.</summary>
public static class PdfWebViews
{
    /// <summary>Captures one view under the same context as HTML rendering/PDF import. HTML resources are explicit, scripts default off, and Windows/legacy restrictions reject before host invocation. XAML accepts no HTML options/resources.</summary>
    public static async Task<PdfDocument> RenderAsync(IPdfViewHost host, PdfProviderRegistry providers, PdfHtmlOptions? htmlOptions = null, IEnumerable<PdfHtmlResource>? resources = null, Uri? baseAddress = null, int maximumPages = 1000, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (host == null || providers == null)
            throw new ArgumentNullException(host == null ? nameof(host) : nameof(providers));
        var kind = host.Kind;
        if (!Enum.IsDefined(typeof(PdfViewKind), kind))
            throw new ArgumentOutOfRangeException(nameof(host));
        if (maximumPages < 1 || maximumPages > 10000)
            throw new ArgumentOutOfRangeException(nameof(maximumPages));
        PdfDocument? document = null;
        PdfHtmlSource? captured = null;
        try
        {
            var report = await PdfFont.RunAsync("RenderApplicationView", "managed.view-adapter", policy, cancellationToken, async c =>
            {
                bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                bool framework = RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.Ordinal);
                if (kind == PdfViewKind.Xaml && !windows || kind == PdfViewKind.WebForms && (!windows || !framework))
                    throw PdfWriterCore.Error(c, "View.Platform", "This explicitly selected view requires Windows; WebForms additionally requires an actual .NET Framework host.");
                if (!host.IsAvailable)
                    throw PdfWriterCore.Error(c, "View.HostUnavailable", "The explicit framework/view/dispatcher host is not provisioned.");
                bool pdf = kind == PdfViewKind.Xaml;
                if (pdf && (htmlOptions != null || resources != null || baseAddress != null))
                    throw PdfWriterCore.Error(c, "View.OptionsUnsupported", "XAML PDF capture cannot apply HTML/browser resource or print options.");
                if (!pdf)
                    _ = providers.GetRequired<IPdfHtmlRenderer>(PdfCapability.RenderHtml);
                using var buffer = new PdfCodecBufferStream(c, pdf ? 67108864 : 8388608);
                try
                {
                    using var worker = c.EnterScope(PdfResourceKind.Workers);
                    using var output = new BorrowedStream(buffer, false, c);
                    await host.RenderAsync(output, c).ConfigureAwait(false);
                }
                catch (Exception error) when (!(error is PdfException) && !(error is OperationCanceledException))
                {
                    c.Checkpoint();
                    throw PdfWriterCore.Error(c, "View.HostFailure", "The explicit trusted view host failed; model, credentials and provider exception text are withheld.");
                }
                c.Checkpoint();
                var bytes = buffer.Finish();
                if (bytes.Length == 0)
                    throw PdfWriterCore.Error(c, "View.EmptyOutput", "A view cannot succeed with an empty substituted document.");
                if (pdf)
                    document = PdfPageOrganization.Read(bytes, maximumPages, c);
                else
                {
                    using var input = bytes.Open(0, bytes.Length);
                    captured = PdfHtmlSource.CreateCore(null, baseAddress ?? new Uri("https://fluent-pdf.invalid/view"), resources, input, c);
                    var options = htmlOptions ?? new PdfHtmlOptions(maximumPages: maximumPages);
                    if (options.MaximumPages > maximumPages)
                        throw PdfWriterCore.Error(c, "View.PageLimit", "Browser and adapter page ceilings must agree with the explicit maximum.");
                    document = PdfHtml.RenderCore(captured, providers, options, c);
                }
                c.Checkpoint();
            }, kind == PdfViewKind.Xaml ? PdfCapability.ReadDocument : PdfCapability.RenderHtml).ConfigureAwait(false);
            captured?.Complete(report);
            document!.SetCompositionReport(report);
            return document;
        }
        catch { document?.Dispose(); throw; }
    }
}
