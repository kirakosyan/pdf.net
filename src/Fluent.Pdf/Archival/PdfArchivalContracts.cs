using System.Collections.ObjectModel;
using System.IO;

namespace Fluent.Pdf;
/// <summary>The explicitly requested ISO 19005 profile; selection alone makes no conformance claim.</summary>
public enum PdfArchivalProfile
{
    /// <summary>PDF/A-1a.</summary>
    A1A,
    /// <summary>PDF/A-1b.</summary>
    A1B,
    /// <summary>PDF/A-2a.</summary>
    A2A,
    /// <summary>PDF/A-2b.</summary>
    A2B,
    /// <summary>PDF/A-2u.</summary>
    A2U,
    /// <summary>PDF/A-3a.</summary>
    A3A,
    /// <summary>PDF/A-3b.</summary>
    A3B,
    /// <summary>PDF/A-3u.</summary>
    A3U,
    /// <summary>PDF/A-4.</summary>
    A4,
    /// <summary>PDF/A-4e.</summary>
    A4E,
    /// <summary>PDF/A-4f.</summary>
    A4F
}

/// <summary>Explicit full-rewrite archival choices. No fonts, color profiles or validation engines are discovered.</summary>
public sealed class PdfArchivalOptions
{
    /// <summary>Captures the requested profile and optional complete ICC RGB/CMYK output space. Metadata replacement is an explicitly reported loss; signatures require explicit invalidation permission.</summary>
    public PdfArchivalOptions(PdfArchivalProfile profile, PdfColorSpace? outputColorSpace = null, string outputCondition = "Explicit output condition", bool replaceMetadata = false, bool allowSignatureInvalidation = false)
    {
        if (!Enum.IsDefined(typeof(PdfArchivalProfile), profile))
            throw new ArgumentOutOfRangeException(nameof(profile));
        if (outputColorSpace != null && (outputColorSpace.Definition.Kind != PdfValueKind.Array || outputColorSpace.Definition.Items.Count != 2 || outputColorSpace.Definition.Items[0].Kind != PdfValueKind.Name || outputColorSpace.Definition.Items[0].NameValue != "ICCBased" || outputColorSpace.Components != 3 && outputColorSpace.Components != 4))
            throw new ArgumentException("An explicit complete RGB/CMYK ICC color space is required.", nameof(outputColorSpace));
        if (string.IsNullOrWhiteSpace(outputCondition) || outputCondition.Length > 1024)
            throw new ArgumentException("A bounded output-condition identifier is required.", nameof(outputCondition));
        Profile = profile;
        OutputColorSpace = outputColorSpace;
        OutputCondition = outputCondition;
        ReplaceMetadata = replaceMetadata;
        AllowSignatureInvalidation = allowSignatureInvalidation;
    }

    /// <summary>Gets the requested profile.</summary>
    public PdfArchivalProfile Profile { get; }
    /// <summary>Gets the caller-supplied complete ICC output space, or null to retain existing output intents.</summary>
    public PdfColorSpace? OutputColorSpace { get; }
    /// <summary>Gets the output-condition identifier.</summary>
    public string OutputCondition { get; }
    /// <summary>Gets whether existing XMP may be replaced with synchronized standard metadata.</summary>
    public bool ReplaceMetadata { get; }
    /// <summary>Gets whether an invalidating signature rewrite is authorized and reported.</summary>
    public bool AllowSignatureInvalidation { get; }
}

/// <summary>A detached validation/preflight rule finding, without an implied conformance claim.</summary>
public sealed class PdfArchivalFinding
{
    /// <summary>Captures bounded rule, location and description text.</summary>
    public PdfArchivalFinding(string rule, string location, string message)
    {
        Rule = Text(rule, 256, nameof(rule));
        Location = Text(location, 4096, nameof(location));
        Message = Text(message, 8192, nameof(message));
    }

    /// <summary>Gets the stable rule identifier.</summary>
    public string Rule { get; }
    /// <summary>Gets the graph/validator location.</summary>
    public string Location { get; }
    /// <summary>Gets the rule description.</summary>
    public string Message { get; }

    private static string Text(string value, int maximum, string parameter) => value != null && value.Length <= maximum ? value : throw new ArgumentException("Bounded nonnull finding text is required.", parameter);
}

/// <summary>A complete or incomplete independent validation observation bound to exact bytes and profile.</summary>
public sealed class PdfArchivalValidation
{
    /// <summary>Captures a validator result. Compliance requires a complete result with zero violations; sourceSha256 is the lowercase exact-input digest.</summary>
    public PdfArchivalValidation(PdfArchivalProfile profile, string sourceSha256, bool completed, bool isCompliant, PdfArchivalFinding[] findings)
    {
        if (!Enum.IsDefined(typeof(PdfArchivalProfile), profile))
            throw new ArgumentOutOfRangeException(nameof(profile));
        if (sourceSha256 == null || sourceSha256.Length != 64 || sourceSha256.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')))
            throw new ArgumentException("An exact lowercase SHA-256 is required.", nameof(sourceSha256));
        if (findings == null || findings.Length > 10000 || findings.Any(f => f == null))
            throw new ArgumentException("A bounded nonnull finding inventory is required.", nameof(findings));
        if (isCompliant && (!completed || findings.Length != 0))
            throw new ArgumentException("Compliance requires completed validation without violations.", nameof(isCompliant));
        Profile = profile;
        SourceSha256 = sourceSha256;
        Completed = completed;
        IsCompliant = isCompliant;
        Findings = new ReadOnlyCollection<PdfArchivalFinding>((PdfArchivalFinding[])findings.Clone());
    }

    /// <summary>Gets the exact validated profile.</summary>
    public PdfArchivalProfile Profile { get; }
    /// <summary>Gets the exact validated input digest.</summary>
    public string SourceSha256 { get; }
    /// <summary>Gets whether full validation completed.</summary>
    public bool Completed { get; }
    /// <summary>Gets the independent validator's conformance observation, never an SDK production qualification.</summary>
    public bool IsCompliant { get; }
    /// <summary>Gets the complete SDK operation report after a public validation/conversion call; null on a provider-only observation or failed operation.</summary>
    public PdfOperationReport? OperationReport { get; internal set; }
    /// <summary>Gets all reported violations.</summary>
    public IReadOnlyList<PdfArchivalFinding> Findings { get; }
}

/// <summary>Optional complete independent ISO 19005 validation of exact borrowed input. No metadata-only implementation may declare compliance.</summary>
public interface IPdfArchivalValidator : IPdfProvider
{
    /// <summary>Validates the requested complete profile with the mandatory context and returns exact input/profile binding. No network, engine or font discovery is implicit.</summary>
    PdfArchivalValidation Validate(PdfArchivalProfile profile, Stream input, PdfOperationContext context);
}

/// <summary>A managed structural preflight inventory; zero findings is not a conformance result.</summary>
public sealed class PdfArchivalInspection
{
    internal PdfArchivalInspection(PdfArchivalFinding[] findings, PdfOperationReport report)
    {
        Findings = Array.AsReadOnly(findings);
        OperationReport = report;
    }

    /// <summary>Gets structural/profile blockers discovered by this bounded preflight.</summary>
    public IReadOnlyList<PdfArchivalFinding> Findings { get; }
    /// <summary>Gets the operation diagnostics and shared resource usage.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>A completed archival rewrite and independent exact-byte observation.</summary>
public sealed class PdfArchivalConversion
{
    internal PdfArchivalConversion(PdfArchivalValidation validation, PdfOperationReport report)
    {
        Validation = validation;
        OperationReport = report;
    }

    /// <summary>Gets the completed independent observation on the bytes copied to caller output.</summary>
    public PdfArchivalValidation Validation { get; }
    /// <summary>Gets full operation diagnostics including explicit losses.</summary>
    public PdfOperationReport OperationReport { get; }
}
