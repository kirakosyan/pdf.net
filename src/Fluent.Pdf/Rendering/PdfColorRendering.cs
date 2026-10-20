using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Standard ICC rendering intent.</summary>
public enum PdfColorRenderingIntent
{
    /// <summary>Perceptual gamut mapping.</summary>
    Perceptual,
    /// <summary>Media-relative colorimetric mapping.</summary>
    RelativeColorimetric,
    /// <summary>Saturation mapping.</summary>
    Saturation,
    /// <summary>ICC-absolute colorimetric mapping.</summary>
    AbsoluteColorimetric
}
/// <summary>Explicit spot-color handling; the provider must reject unhonored requests.</summary>
public enum PdfSpotColorPolicy
{
    /// <summary>Reject spot colors rather than substitute a process preview.</summary>
    Reject,
    /// <summary>Use declared alternates, reporting conversion loss.</summary>
    CompositeAlternate
}
/// <summary>Immutable color-management authority, distinct from ordinary device-color screen preview.</summary>
public sealed class PdfColorRenderOptions
{
    /// <summary>Captures a complete RGB ICC output profile and explicit intent, overprint and spot policy. Embedded source profiles/output intents must be honored by the selected provider.</summary>
    public PdfColorRenderOptions(PdfColorSpace outputProfile, PdfColorRenderingIntent intent = PdfColorRenderingIntent.RelativeColorimetric, bool simulateOverprint = false, PdfSpotColorPolicy spotColors = PdfSpotColorPolicy.Reject)
    {
        if (outputProfile == null || outputProfile.Components != 3 || outputProfile.Definition.Kind != PdfValueKind.Array || outputProfile.Definition.Items.Count != 2 || outputProfile.Definition.Items[0].Kind != PdfValueKind.Name || outputProfile.Definition.Items[0].NameValue != "ICCBased")
            throw new ArgumentException("An explicit complete RGB ICC output profile is required.", nameof(outputProfile));
        if (!Enum.IsDefined(typeof(PdfColorRenderingIntent), intent) || !Enum.IsDefined(typeof(PdfSpotColorPolicy), spotColors))
            throw new ArgumentOutOfRangeException(nameof(intent));
        OutputProfile = outputProfile;
        Intent = intent;
        SimulateOverprint = simulateOverprint;
        SpotColors = spotColors;
    }
    /// <summary>Gets exact RGB output profile.</summary>
    public PdfColorSpace OutputProfile
    {
        get;
    }
    /// <summary>Gets explicit rendering intent.</summary>
    public PdfColorRenderingIntent Intent
    {
        get;
    }
    /// <summary>Gets whether overprint simulation is required.</summary>
    public bool SimulateOverprint
    {
        get;
    }
    /// <summary>Gets explicit spot policy. Alternate conversion requires a diagnostic.</summary>
    public PdfSpotColorPolicy SpotColors
    {
        get;
    }
}
/// <summary>Optional ICC-aware renderer, independent of GUI/platform types. It must honor embedded profiles/output intents, requested output transform and print/screen policies, or reject the unsupported request.</summary>
public interface IPdfColorManagedPageRenderer : IPdfProvider
{
    /// <summary>Writes exact straight-alpha BGRA in the requested RGB output profile under shared render/pixel/worker limits.</summary>
    void Render(PdfRasterRequest request, PdfColorRenderOptions colorManagement, Stream bgraOutput, PdfOperationContext context);
}
/// <summary>Explicit color-managed rendering; ordinary page renderers are never silently substituted.</summary>
public static class PdfColorRendering
{
    /// <summary>Renders through the explicitly registered color-managed service. RGB ICC output is required; process separations and CMYK envelopes require separate profiles.</summary>
    public static PdfRaster RenderPage(PdfDocumentSnapshot source, int pageIndex, PdfProviderRegistry providers, PdfColorRenderOptions colorManagement, PdfRenderOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || providers == null || colorManagement == null)
            throw new ArgumentNullException(source == null ? nameof(source) : providers == null ? nameof(providers) : nameof(colorManagement));
        if (pageIndex < 0 || pageIndex >= source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        options ??= new();
        if (options.Color != PdfRasterColor.Rgb)
            throw new ArgumentException("RGB ICC output requires an RGB raster envelope.", nameof(options));
        _ = providers.GetRequired<IPdfColorManagedPageRenderer>(PdfCapability.RenderColorManaged);
        PdfRasterRequest? request = null;
        PdfByteStore? samples = null;
        var report = PdfFont.Run("RenderColorManagedPage", providers.GetIdentifier(PdfCapability.RenderColorManaged), policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            using var graph = new PdfGraphEdit(source, new(allowSignatureInvalidation: true), c);
            PdfGraphImport.ValidateDetached(colorManagement.OutputProfile.Definition, c);
            var result = PdfRendering.RenderCore(graph, pageIndex, providers, options, c, colorManagement);
            request = result.Request;
            samples = result.Samples;
        }, PdfCapability.RenderColorManaged);
        return new(samples!, request!, report);
    }
}
