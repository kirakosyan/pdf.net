namespace Fluent.Pdf;

/// <summary>An immutable, provider-neutral failure description with a stable code.</summary>
[Serializable]
public sealed class PdfDiagnostic
{
    /// <summary>Creates a diagnostic; code/message/provider identifiers are bounded configuration text.</summary>
    public PdfDiagnostic(string code, string message, PdfCapability? capability = null, string? providerId = null,
        PdfDiagnosticSeverity severity = PdfDiagnosticSeverity.Error, PdfLossCategory lossCategory = PdfLossCategory.None,
        string? operation = null, int? pageIndex = null, long? objectId = null, string? remediation = null)
    {
        Code = Guard.Text(code, nameof(code), 128);
        Message = Guard.Text(message, nameof(message), 2048);
        if (capability.HasValue) Guard.Capability(capability.Value);
        Capability = capability;
        ProviderId = providerId is null ? null : Guard.Text(providerId, nameof(providerId), 128);
        if (!Enum.IsDefined(typeof(PdfDiagnosticSeverity), severity)) throw new ArgumentOutOfRangeException(nameof(severity));
        if (!Enum.IsDefined(typeof(PdfLossCategory), lossCategory)) throw new ArgumentOutOfRangeException(nameof(lossCategory));
        if (pageIndex < 0) throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (objectId < 0) throw new ArgumentOutOfRangeException(nameof(objectId));
        Severity = severity;
        LossCategory = lossCategory;
        Operation = operation is null ? null : Guard.Text(operation, nameof(operation), 128);
        PageIndex = pageIndex;
        ObjectId = objectId;
        Remediation = remediation is null ? null : Guard.Text(remediation, nameof(remediation), 2048);
    }

    /// <summary>Gets a stable machine-readable code.</summary>
    public string Code { get; }
    /// <summary>Gets the supplied description. Callers/providers must exclude document payloads and credentials.</summary>
    public string Message { get; }
    /// <summary>Gets the related capability, when applicable.</summary>
    public PdfCapability? Capability { get; }
    /// <summary>Gets the related registered provider identifier, when applicable.</summary>
    public string? ProviderId { get; }
    /// <summary>Gets diagnostic severity; permissive loss reports normalize losses to Warning.</summary>
    public PdfDiagnosticSeverity Severity { get; }
    /// <summary>Gets the explicitly reported loss category.</summary>
    public PdfLossCategory LossCategory { get; }
    /// <summary>Gets operation provenance when available.</summary>
    public string? Operation { get; }
    /// <summary>Gets the zero-based affected page, when known.</summary>
    public int? PageIndex { get; }
    /// <summary>Gets the affected nonnegative object identifier, when known.</summary>
    public long? ObjectId { get; }
    /// <summary>Gets bounded remediation guidance, when supplied.</summary>
    public string? Remediation { get; }
}

/// <summary>A failure with a typed, provider-neutral diagnostic.</summary>
public class PdfException : Exception
{
    /// <summary>Creates a failure retaining its diagnostic and optional underlying exception.</summary>
    public PdfException(PdfDiagnostic diagnostic, Exception? innerException = null)
        : base((diagnostic ?? throw new ArgumentNullException(nameof(diagnostic))).Message, innerException)
    {
        Diagnostic = diagnostic;
    }

    /// <summary>Gets the structured diagnostic.</summary>
    public PdfDiagnostic Diagnostic { get; }
}

/// <summary>A requested optional capability has no registered provider.</summary>
public sealed class PdfProviderUnavailableException : PdfException
{
    /// <summary>Creates the stable Provider.Unavailable failure for a missing capability.</summary>
    public PdfProviderUnavailableException(PdfCapability capability)
        : base(CreateDiagnostic(capability))
    {
    }

    private static PdfDiagnostic CreateDiagnostic(PdfCapability capability)
    {
        Guard.Capability(capability);
        return new PdfDiagnostic("Provider.Unavailable", $"No provider is registered for {capability}.", capability);
    }
}
