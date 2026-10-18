using System.Collections.ObjectModel;

namespace Fluent.Pdf;

/// <summary>A detached immutable result containing every accepted diagnostic and resource usage.</summary>
[Serializable]
public sealed class PdfOperationReport
{
    internal PdfOperationReport(string operation, string providerId, bool succeeded, TimeSpan elapsed,
        PdfDiagnostic[] diagnostics, PdfResourceUsage[] resources, PdfDiagnostic? failure)
    {
        Operation = operation;
        ProviderId = providerId;
        Succeeded = succeeded;
        ElapsedTime = elapsed;
        Diagnostics = new ReadOnlyCollection<PdfDiagnostic>(diagnostics);
        Resources = new ReadOnlyCollection<PdfResourceUsage>(resources);
        Failure = failure;
    }
    /// <summary>Gets the SDK operation name.</summary>
    public string Operation { get; }
    /// <summary>Gets the captured registered provider ID.</summary>
    public string ProviderId { get; }
    /// <summary>Gets whether the operation returned successfully, including file commit where applicable.</summary>
    public bool Succeeded { get; }
    /// <summary>Gets the measured monotonic duration.</summary>
    public TimeSpan ElapsedTime { get; }
    /// <summary>Gets all accepted diagnostics, in reporting order.</summary>
    public IReadOnlyList<PdfDiagnostic> Diagnostics { get; }
    /// <summary>Gets final cumulative/current counters and high-water marks for all resource kinds.</summary>
    public IReadOnlyList<PdfResourceUsage> Resources { get; }
    /// <summary>Gets the latched policy failure, when one caused failure; original I/O/cancellation exceptions propagate separately.</summary>
    public PdfDiagnostic? Failure { get; }
}

/// <summary>An immutable resource counter snapshot.</summary>
[Serializable]
public sealed class PdfResourceUsage
{
    internal PdfResourceUsage(PdfResourceKind resource, long consumed, long peak) { Resource = resource; Consumed = consumed; Peak = peak; }
    /// <summary>Gets the resource kind.</summary>
    public PdfResourceKind Resource { get; }
    /// <summary>Gets cumulative use, or current outstanding scopes for Depth/Workers.</summary>
    public long Consumed { get; }
    /// <summary>Gets the highest counter value during the operation.</summary>
    public long Peak { get; }
}
