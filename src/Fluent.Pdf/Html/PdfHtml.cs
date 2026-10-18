using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit bounded browser layout/print choices. Javascript is disabled unless the caller enables it for this conversion.</summary>
public sealed class PdfHtmlOptions
{
    /// <summary>Captures page size, bounded viewport, script execution, background painting and imported page ceiling.</summary>
    public PdfHtmlOptions(PdfPageSize? pageSize = null, int viewportWidth = 1280, int viewportHeight = 720, bool enableJavaScript = false, bool printBackground = true, int maximumPages = 1000)
    {
        PageSize = pageSize ?? PdfPageSize.A4;
        Guard.Size(PageSize, nameof(pageSize));
        if (viewportWidth < 16 || viewportWidth > 16384 || viewportHeight < 16 || viewportHeight > 16384)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        if (maximumPages < 1 || maximumPages > 10000)
            throw new ArgumentOutOfRangeException(nameof(maximumPages));
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
        EnableJavaScript = enableJavaScript;
        PrintBackground = printBackground;
        MaximumPages = maximumPages;
    }

    /// <summary>Gets the requested physical print page size. The imported model retains the engine's actual page boxes and print precision.</summary>
    public PdfPageSize PageSize { get; }
    /// <summary>Gets the CSS viewport width in device-independent pixels.</summary>
    public int ViewportWidth { get; }
    /// <summary>Gets the CSS viewport height in device-independent pixels.</summary>
    public int ViewportHeight { get; }
    /// <summary>Gets whether source document JavaScript execution is explicitly enabled.</summary>
    public bool EnableJavaScript { get; }
    /// <summary>Gets whether CSS backgrounds are painted.</summary>
    public bool PrintBackground { get; }
    /// <summary>Gets the maximum accepted generated page count.</summary>
    public int MaximumPages { get; }
}

/// <summary>An explicitly registered optional HTML engine. Implementations must use a private job, supplied resources and the mandatory shared context.</summary>
public interface IPdfHtmlRenderer : IPdfProvider
{
    /// <summary>Renders HTML/CSS and explicitly enabled JavaScript to a borrowed PDF-output lease. No network, local file, engine or font discovery is implicit.</summary>
    void Render(PdfHtmlSource source, PdfHtmlOptions options, Stream pdfOutput, PdfOperationContext context);
}

/// <summary>Explicit optional HTML conversion importing the result through the ordinary managed reader into the editable document model.</summary>
public static class PdfHtml
{
    /// <summary>Renders privately, validates/imports the generated PDF under the same context and returns an independent editable document. Save/SaveFile retain their ordinary ownership/atomicity rules.</summary>
    public static PdfDocument Render(PdfHtmlSource source, PdfProviderRegistry providers, PdfHtmlOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        var renderer = providers.GetRequired<IPdfHtmlRenderer>(PdfCapability.RenderHtml);
        options ??= new();
        PdfDocument? document = null;
        try
        {
            var report = PdfFont.Run("RenderHtml", providers.GetIdentifier(PdfCapability.RenderHtml), policy, cancellationToken, context =>
            {
                context.Consume(PdfResourceKind.Objects, source.Resources.Count + 1L);
                context.Consume(PdfResourceKind.ExpandedBytes, (source.Markup?.Length ?? 0) * 2L + source.Resources.Count * 128L + 512);
                context.ConsumePixels(options.ViewportWidth, options.ViewportHeight);
                using var buffer = new PdfCodecBufferStream(context, context.Available(PdfResourceKind.ExpandedBytes));
                using (var lease = new BorrowedStream(buffer, false, context))
                    renderer.Render(source, options, lease, context);
                context.Checkpoint();
                document = PdfPageOrganization.Read(buffer.Finish(), options.MaximumPages, context, options.PageSize);
            }, PdfCapability.RenderHtml);
            document!.SetCompositionReport(report);
            return document;
        }
        catch
        {
            document?.Dispose();
            throw;
        }
    }
}
