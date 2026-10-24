namespace Fluent.Pdf;

/// <summary>The fixed kinds of privacy-filtered observation events.</summary>
public enum PdfObservationKind
{
    /// <summary>An operation entered its SDK context.</summary>
    Started,
    /// <summary>An explicit numeric progress checkpoint.</summary>
    Progress,
    /// <summary>An operation finished, including failure.</summary>
    Finished,
    /// <summary>A caller explicitly recorded a numeric engine-version probe.</summary>
    EngineProbe
}

/// <summary>An immutable schema1 event containing no caller/provider free-text fields or document payload.</summary>
public sealed class PdfObservation
{
    internal PdfObservation(Guid trace, Guid span, long sequence, PdfObservationKind kind, PdfCapability capability, TimeSpan elapsed, bool? succeeded, string? code, string? remediation, long? completed, long? total, int diagnostics, IReadOnlyList<PdfResourceUsage> resources, string? version)
    { TraceId=trace; SpanId=span; Sequence=sequence; Kind=kind; Capability=capability; ElapsedTime=elapsed; Succeeded=succeeded; Code=code; Remediation=remediation; Completed=completed; Total=total; DiagnosticCount=diagnostics; Resources=resources; EngineVersion=version; }
    /// <summary>Gets the fixed event schema version.</summary>
    public int SchemaVersion => 1;
    /// <summary>Gets the session's random correlation ID; it is unrelated to PDF IDs or content hashes.</summary>
    public Guid TraceId { get; }
    /// <summary>Gets a random per-operation/probe ID.</summary>
    public Guid SpanId { get; }
    /// <summary>Gets the session publication sequence; dropped events can leave incomplete traces.</summary>
    public long Sequence { get; }
    /// <summary>Gets the event kind.</summary>
    public PdfObservationKind Kind { get; }
    /// <summary>Gets the declared capability, without a provider identifier.</summary>
    public PdfCapability Capability { get; }
    /// <summary>Gets monotonic operation elapsed time.</summary>
    public TimeSpan ElapsedTime { get; }
    /// <summary>Gets success for completed operations/probes, otherwise null.</summary>
    public bool? Succeeded { get; }
    /// <summary>Gets a closed-catalog failure code, or null. Unrecognized provider codes become Provider.Diagnostic.</summary>
    public string? Code { get; }
    /// <summary>Gets SDK-authored safe remediation guidance, never provider-supplied free text.</summary>
    public string? Remediation { get; }
    /// <summary>Gets explicit completed work units for a progress event.</summary>
    public long? Completed { get; }
    /// <summary>Gets explicit total work units for a progress event.</summary>
    public long? Total { get; }
    /// <summary>Gets the number of accepted diagnostics without their messages, locations or identifiers.</summary>
    public int DiagnosticCount { get; }
    /// <summary>Gets detached numeric resource counters for a completed operation.</summary>
    public IReadOnlyList<PdfResourceUsage> Resources { get; }
    /// <summary>Gets an explicitly recorded numeric engine version, otherwise null; no version is inferred from a provider name.</summary>
    public string? EngineVersion { get; }
}
