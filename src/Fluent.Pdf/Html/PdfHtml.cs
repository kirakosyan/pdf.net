using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit bounded browser layout/print choices. Javascript is disabled unless the caller enables it for this conversion.</summary>
public sealed class PdfHtmlOptions
{
    /// <summary>Captures page size, bounded viewport, script execution, background painting and imported page ceiling.</summary>
    public PdfHtmlOptions(PdfPageSize? pageSize, int viewportWidth, int viewportHeight, bool enableJavaScript, bool printBackground, int maximumPages) : this(pageSize, viewportWidth, viewportHeight, enableJavaScript, printBackground, maximumPages, null, "en-US", "UTC", PdfHtmlMedia.Print)
    {
    }

    /// <summary>Captures explicit finite readiness, browser locale/timezone/media and the ordinary bounded layout/print options. Unknown engine locale/timezone values reject.</summary>
    public PdfHtmlOptions(PdfPageSize? pageSize = null, int viewportWidth = 1280, int viewportHeight = 720, bool enableJavaScript = false, bool printBackground = true, int maximumPages = 1000, PdfHtmlReadinessOptions? readiness = null, string locale = "en-US", string timeZone = "UTC", PdfHtmlMedia media = PdfHtmlMedia.Print)
    {
        Readiness = readiness ?? PdfHtmlReadinessOptions.Default;
        if (string.IsNullOrEmpty(locale) || locale.Length > 64 || locale.Split('-').Any(part => part.Length == 0 || part.Any(c => !(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9'))))
            throw new ArgumentException("An explicit bounded locale identifier is required.", nameof(locale));
        if (string.IsNullOrEmpty(timeZone) || timeZone.Length > 128 || timeZone.Any(c => !(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || "/_+-".IndexOf(c) >= 0)))
            throw new ArgumentException("An explicit bounded timezone identifier is required.", nameof(timeZone));
        if (!Enum.IsDefined(typeof(PdfHtmlMedia), media))
            throw new ArgumentOutOfRangeException(nameof(media));
        Locale = locale;
        TimeZone = timeZone;
        Media = media;
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
    /// <summary>Gets the immutable finite main-document readiness policy.</summary>
    public PdfHtmlReadinessOptions Readiness { get; }
    /// <summary>Gets the explicit browser locale, independent of the host account.</summary>
    public string Locale { get; }
    /// <summary>Gets the explicit browser timezone, independent of the host account.</summary>
    public string TimeZone { get; }
    /// <summary>Gets the explicit CSS media selected before navigation/readiness.</summary>
    public PdfHtmlMedia Media { get; }
    /// <summary>Gets the immutable explicit print policy.</summary>
    public PdfHtmlPrintOptions Print { get; } = PdfHtmlPrintOptions.Default;

    /// <summary>Returns an independent immutable options snapshot with the supplied print policy. Existing source/binary constructor signatures remain available.</summary>
    public PdfHtmlOptions WithPrint(PdfHtmlPrintOptions print) => new(this, print ?? throw new ArgumentNullException(nameof(print)));
    private PdfHtmlOptions(PdfHtmlOptions source, PdfHtmlPrintOptions print) : this(source.PageSize, source.ViewportWidth, source.ViewportHeight, source.EnableJavaScript, source.PrintBackground, source.MaximumPages, source.Readiness, source.Locale, source.TimeZone, source.Media)
    {
        double width = print.Landscape ? PageSize.Height : PageSize.Width, height = print.Landscape ? PageSize.Width : PageSize.Height;
        if (print.MarginLeft + print.MarginRight >= width || print.MarginTop + print.MarginBottom >= height)
            throw new ArgumentException("The requested fallback paper must retain a positive content rectangle.", nameof(print));
        Print = print;
    }
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
        providers.GetRequired<IPdfHtmlRenderer>(PdfCapability.RenderHtml);
        options ??= new();
        PdfDocument? document = null;
        try
        {
            var report = PdfFont.Run("RenderHtml", providers.GetIdentifier(PdfCapability.RenderHtml), policy, cancellationToken, context =>
            {
                document = RenderCore(source, providers, options, context);
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

    internal static PdfDocument RenderCore(PdfHtmlSource source, PdfProviderRegistry providers, PdfHtmlOptions options, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.Objects, source.Resources.Count + 1L);
        context.Consume(PdfResourceKind.ExpandedBytes, (source.Markup?.Length ?? 0) * 2L + source.Resources.Count * 128L + 512);
        context.ConsumePixels(options.ViewportWidth, options.ViewportHeight);
        using var buffer = new PdfCodecBufferStream(context, context.Available(PdfResourceKind.ExpandedBytes));
        using (var lease = new BorrowedStream(buffer, false, context))
            providers.GetRequired<IPdfHtmlRenderer>(PdfCapability.RenderHtml).Render(source, options, lease, context);
        context.Checkpoint();
        var document = PdfPageOrganization.Read(buffer.Finish(), options.MaximumPages, context, options.PageSize);
        try
        {
            if (options.Print.Tagged)
                PdfHtmlTagStructure.Validate(document.CreateSnapshot(), providers, context);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }
}
