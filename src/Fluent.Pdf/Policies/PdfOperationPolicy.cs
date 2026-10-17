namespace Fluent.Pdf;

/// <summary>Immutable operation behavior and resource ceilings. There is no global mutable policy.</summary>
public sealed class PdfOperationPolicy
{
    /// <summary>Creates a policy; strict processing is the default.</summary>
    public PdfOperationPolicy(PdfProcessingMode mode = PdfProcessingMode.Strict, PdfResourceLimits? limits = null)
    {
        if (!Enum.IsDefined(typeof(PdfProcessingMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        Mode = mode;
        Limits = limits ?? new PdfResourceLimits();
    }
    /// <summary>Gets the immutable shared default policy.</summary>
    public static PdfOperationPolicy Default { get; } = new();
    /// <summary>Gets how declared fidelity losses are handled.</summary>
    public PdfProcessingMode Mode { get; }
    /// <summary>Gets the immutable limits applied to each new operation.</summary>
    public PdfResourceLimits Limits { get; }
}

/// <summary>Controls whether explicitly reported losses can produce successful output.</summary>
public enum PdfProcessingMode
{
    /// <summary>Reject every reported loss.</summary>
    Strict,
    /// <summary>Allow reported losses and retain every record in the operation report.</summary>
    Permissive
}

/// <summary>Diagnostic severity.</summary>
public enum PdfDiagnosticSeverity
{
    /// <summary>Informational observation.</summary>
    Information,
    /// <summary>Nonfatal observation or explicitly accepted loss.</summary>
    Warning,
    /// <summary>Fatal operation failure.</summary>
    Error
}

/// <summary>The kind of loss explicitly reported by a provider.</summary>
public enum PdfLossCategory
{
    /// <summary>No content or fidelity loss.</summary>
    None,
    /// <summary>A requested content behavior cannot be supported.</summary>
    UnsupportedContent,
    /// <summary>Content was omitted.</summary>
    OmittedContent,
    /// <summary>Content was substituted.</summary>
    SubstitutedContent,
    /// <summary>Fidelity was reduced.</summary>
    FidelityReduction
}
