namespace Fluent.Pdf;

/// <summary>Immutable limits on pool workers, waiting jobs and copied input bytes across waiting and running jobs.</summary>
public sealed class PdfProcessingOptions
{
    /// <summary>Creates bounded admission limits. Per-job operation budgets remain independently mandatory.</summary>
    public PdfProcessingOptions(int maximumWorkers = 4, int maximumQueuedJobs = 16, long maximumResidentInputBytes = 67108864)
    {
        if (maximumWorkers < 1 || maximumWorkers > 64) throw new ArgumentOutOfRangeException(nameof(maximumWorkers));
        if (maximumQueuedJobs < 1 || maximumQueuedJobs > 4096) throw new ArgumentOutOfRangeException(nameof(maximumQueuedJobs));
        if (maximumResidentInputBytes < 1) throw new ArgumentOutOfRangeException(nameof(maximumResidentInputBytes));
        MaximumWorkers = maximumWorkers;
        MaximumQueuedJobs = maximumQueuedJobs;
        MaximumResidentInputBytes = maximumResidentInputBytes;
    }
    /// <summary>Gets the fixed maximum number of concurrently executing job callbacks.</summary>
    public int MaximumWorkers { get; }
    /// <summary>Gets the maximum number of waiting jobs.</summary>
    public int MaximumQueuedJobs { get; }
    /// <summary>Gets the maximum copied input bytes retained by all admitted jobs.</summary>
    public long MaximumResidentInputBytes { get; }
}

/// <summary>A detached aggregate progress snapshot with no document data or unbounded event queue.</summary>
public sealed class PdfProcessingProgress
{
    internal PdfProcessingProgress(int queued, int running, long bytes, long succeeded, long failed, long canceled)
    { Queued = queued; Running = running; ResidentInputBytes = bytes; Succeeded = succeeded; Failed = failed; Canceled = canceled; }
    /// <summary>Gets the waiting job count.</summary>
    public int Queued { get; }
    /// <summary>Gets the executing job count.</summary>
    public int Running { get; }
    /// <summary>Gets copied input bytes retained by waiting and executing jobs.</summary>
    public long ResidentInputBytes { get; }
    /// <summary>Gets the successfully completed job count.</summary>
    public long Succeeded { get; }
    /// <summary>Gets the failed job count.</summary>
    public long Failed { get; }
    /// <summary>Gets the canceled job count.</summary>
    public long Canceled { get; }
}
