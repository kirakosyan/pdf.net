using System.Collections.ObjectModel;
using System.IO;

namespace Fluent.Pdf;
/// <summary>Exact standard editions. Selecting a profile is not a conformance claim.</summary>
public enum PdfConformanceProfile
{
    /// <summary>PDF/X-1a:2001.</summary>
    X1A2001,
    /// <summary>PDF/X-1a:2003.</summary>
    X1A2003,
    /// <summary>PDF/X-3:2002.</summary>
    X32002,
    /// <summary>PDF/X-3:2003.</summary>
    X32003,
    /// <summary>PDF/X-4:2010, not the external-output-intent variant.</summary>
    X42010,
    /// <summary>PDF/E-1:2008.</summary>
    E12008
}

/// <summary>Explicit profile, output intent and signature authority. Providers must report every color/flattening/content loss through the shared context.</summary>
public sealed class PdfProfileOptions
{
    /// <summary>Requires a complete caller-supplied ICC output space and literal print condition for PDF/X. PDF/X-1a requires a CMYK output intent. No profile/font/engine discovery occurs.</summary>
    public PdfProfileOptions(PdfConformanceProfile profile, PdfColorSpace? outputColorSpace = null, string? outputCondition = null, bool allowSignatureInvalidation = false, bool allowAdvancedContentLoss = false)
    {
        if (!Enum.IsDefined(typeof(PdfConformanceProfile), profile))
            throw new ArgumentOutOfRangeException(nameof(profile));
        if (profile != PdfConformanceProfile.E12008 && (outputColorSpace == null || outputColorSpace.Definition.Kind != PdfValueKind.Array || outputColorSpace.Definition.Items.Count != 2 || outputColorSpace.Definition.Items[0].Kind != PdfValueKind.Name || outputColorSpace.Definition.Items[0].NameValue != "ICCBased" || outputColorSpace.Definition.Items[1].Kind != PdfValueKind.Stream || outputColorSpace.Components != 3 && outputColorSpace.Components != 4))
            throw new ArgumentException("A complete explicit RGB/CMYK ICC output space is required.", nameof(outputColorSpace));
        if ((profile == PdfConformanceProfile.X1A2001 || profile == PdfConformanceProfile.X1A2003) && outputColorSpace!.Components != 4)
            throw new ArgumentException("PDF/X-1a needs an explicit CMYK print output intent.", nameof(outputColorSpace));
        if (profile == PdfConformanceProfile.E12008)
        {
            if (outputColorSpace != null || outputCondition != null)
                throw new ArgumentException("The PDF/E source gate does not accept unused print-intent settings.", nameof(outputColorSpace));
        }
        else
            Guard.Text(outputCondition!, nameof(outputCondition), 1024);
        Profile = profile;
        OutputColorSpace = outputColorSpace;
        OutputCondition = outputCondition ?? "";
        AllowSignatureInvalidation = allowSignatureInvalidation;
        AllowAdvancedContentLoss = allowAdvancedContentLoss;
    }

    /// <summary>Gets the exact requested profile.</summary>
    public PdfConformanceProfile Profile { get; }
    /// <summary>Gets the complete explicit PDF/X ICC output intent, or null for PDF/E.</summary>
    public PdfColorSpace? OutputColorSpace { get; }
    /// <summary>Gets the literal print condition identifier.</summary>
    public string OutputCondition { get; }
    /// <summary>Gets explicit signature rewrite authority; strict loss policy still applies.</summary>
    public bool AllowSignatureInvalidation { get; }
    /// <summary>Gets explicit authority for a rewrite whose advanced-content semantics cannot be proven retained by this source gate. Strict loss policy still rejects it.</summary>
    public bool AllowAdvancedContentLoss { get; }
}

/// <summary>Complete or incomplete independent observation bound to exact candidate bytes and request settings.</summary>
public sealed class PdfProfileValidation
{
    /// <summary>Captures bounded stable rule findings. A compliant observation must be complete with zero findings. Fixtures/providers alone do not qualify production support.</summary>
    public PdfProfileValidation(PdfConformanceProfile profile, string sourceSha256, string requestSha256, bool completed, bool isCompliant, IEnumerable<string> findings)
    {
        if (!Enum.IsDefined(typeof(PdfConformanceProfile), profile))
            throw new ArgumentOutOfRangeException(nameof(profile));
        CheckHash(sourceSha256, nameof(sourceSha256));
        CheckHash(requestSha256, nameof(requestSha256));
        if (findings == null)
            throw new ArgumentNullException(nameof(findings));
        var values = new List<string>();
        foreach (string value in findings)
        {
            if (values.Count == 10000)
                throw new ArgumentException("At most10000 findings are supported.", nameof(findings));
            Guard.Text(value, nameof(findings), 8192);
            values.Add(value);
        }

        if (isCompliant && (!completed || values.Count != 0))
            throw new ArgumentException("Compliance requires completed validation with zero violations.", nameof(isCompliant));
        Profile = profile;
        SourceSha256 = sourceSha256;
        RequestSha256 = requestSha256;
        Completed = completed;
        IsCompliant = isCompliant;
        Findings = new ReadOnlyCollection<string>(values);
    }

    /// <summary>Gets exact profile identity.</summary>
    public PdfConformanceProfile Profile { get; }
    /// <summary>Gets exact validated byte digest.</summary>
    public string SourceSha256 { get; }
    /// <summary>Gets the SDK-supplied exact request binding digest.</summary>
    public string RequestSha256 { get; }
    /// <summary>Gets whether complete profile validation finished.</summary>
    public bool Completed { get; }
    /// <summary>Gets the independent observation, not SDK production qualification.</summary>
    public bool IsCompliant { get; }
    /// <summary>Gets immutable rule findings.</summary>
    public IReadOnlyList<string> Findings { get; }
    /// <summary>Gets the completed public operation report; null on a provider-only result.</summary>
    public PdfOperationReport? OperationReport { get; internal set; }

    private static void CheckHash(string value, string name)
    {
        if (value == null || value.Length != 64 || value.Any(ch => !(ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f')))
            throw new ArgumentException("An exact lowercase SHA256 is required.", name);
    }
}

/// <summary>Explicit caller-owned profile conversion engine. No metadata-only implementation can claim complete conversion.</summary>
public interface IPdfProfileProducer
{
    /// <summary>Gets a nonsecret pinned ASCII engine/configuration identity.</summary>
    string EngineId { get; }

    /// <summary>Reports exact profile/print prerequisites before input capture.</summary>
    bool Supports(PdfProfileOptions options);
    /// <summary>Creates a candidate from exact borrowed PDF bytes using the shared context. Strict unsupported features and all color/flattening losses must be reported. Any native engine requires separately qualified bounded isolation and explicit offline assets.</summary>
    void Convert(Stream source, PdfProfileOptions options, Stream candidate, PdfOperationContext context);
}

/// <summary>Separately configured complete independent standard validator, not a metadata marker or SDK structural check.</summary>
public interface IPdfProfileValidator
{
    /// <summary>Gets a nonsecret pinned ASCII validator/configuration identity, distinct from the producer.</summary>
    string EngineId { get; }

    /// <summary>Reports exact profile validation availability before input capture.</summary>
    bool Supports(PdfConformanceProfile profile);
    /// <summary>Validates every applicable rule against exact borrowed bytes and requested output condition. Returns exact SHA256/profile/request binding; no implicit downloads/network. Leases expire at return.</summary>
    PdfProfileValidation Validate(Stream input, PdfProfileOptions options, string requestSha256, PdfOperationContext context);
}
