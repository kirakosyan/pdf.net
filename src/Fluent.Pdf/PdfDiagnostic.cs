namespace Fluent.Pdf;

/// <summary>An immutable, provider-neutral failure description with a stable code.</summary>
public sealed class PdfDiagnostic
{
    /// <summary>Creates a diagnostic; code/message/provider identifiers are bounded configuration text.</summary>
    public PdfDiagnostic(string code, string message, PdfCapability? capability = null, string? providerId = null)
    {
        Code = Guard.Text(code, nameof(code), 128);
        Message = Guard.Text(message, nameof(message), 2048);
        if (capability.HasValue) Guard.Capability(capability.Value);
        Capability = capability;
        ProviderId = providerId is null ? null : Guard.Text(providerId, nameof(providerId), 128);
    }

    /// <summary>Gets a stable machine-readable code.</summary>
    public string Code { get; }
    /// <summary>Gets a description that does not include document contents or credentials.</summary>
    public string Message { get; }
    /// <summary>Gets the related capability, when applicable.</summary>
    public PdfCapability? Capability { get; }
    /// <summary>Gets the related registered provider identifier, when applicable.</summary>
    public string? ProviderId { get; }
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
