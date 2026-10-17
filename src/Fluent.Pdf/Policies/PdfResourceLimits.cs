namespace Fluent.Pdf;

/// <summary>Immutable positive resource ceilings for one operation. Byte and object counters use 64-bit arithmetic.</summary>
public sealed class PdfResourceLimits
{
    /// <summary>Creates finite limits. Elapsed time must be between one millisecond and one day.</summary>
    public PdfResourceLimits(long maximumInputBytes = 67108864, long maximumExpandedBytes = 268435456,
        long maximumOutputBytes = 268435456, long maximumObjects = 1000000, long maximumPixels = 100000000,
        int maximumDepth = 128, int maximumWorkers = 8, int maximumDiagnostics = 1024, TimeSpan? maximumElapsedTime = null)
    {
        MaximumInputBytes = Positive(maximumInputBytes, nameof(maximumInputBytes));
        MaximumExpandedBytes = Positive(maximumExpandedBytes, nameof(maximumExpandedBytes));
        MaximumOutputBytes = Positive(maximumOutputBytes, nameof(maximumOutputBytes));
        MaximumObjects = Positive(maximumObjects, nameof(maximumObjects));
        MaximumPixels = Positive(maximumPixels, nameof(maximumPixels));
        if (maximumDepth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        if (maximumWorkers <= 0) throw new ArgumentOutOfRangeException(nameof(maximumWorkers));
        if (maximumDiagnostics <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDiagnostics));
        MaximumDepth = maximumDepth;
        MaximumWorkers = maximumWorkers;
        MaximumDiagnostics = maximumDiagnostics;
        var elapsed = maximumElapsedTime ?? TimeSpan.FromSeconds(30);
        if (elapsed < TimeSpan.FromMilliseconds(1) || elapsed > TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(maximumElapsedTime));
        MaximumElapsedTime = elapsed;
    }

    /// <summary>Gets the maximum input bytes delivered, with at most one additional EOF-probe byte consumed on a rejected read.</summary>
    public long MaximumInputBytes { get; }
    /// <summary>Gets the cumulative expanded/decoded byte limit, charged before allocation by providers.</summary>
    public long MaximumExpandedBytes { get; }
    /// <summary>Gets the cumulative output-write/growth and relative output-extent limit.</summary>
    public long MaximumOutputBytes { get; }
    /// <summary>Gets the cumulative parsed/generated object limit.</summary>
    public long MaximumObjects { get; }
    /// <summary>Gets the cumulative decoded/rendered pixel limit.</summary>
    public long MaximumPixels { get; }
    /// <summary>Gets the maximum simultaneous recursion scopes.</summary>
    public int MaximumDepth { get; }
    /// <summary>Gets the maximum simultaneous worker scopes.</summary>
    public int MaximumWorkers { get; }
    /// <summary>Gets the maximum retained diagnostic count; exhaustion fails instead of dropping records.</summary>
    public int MaximumDiagnostics { get; }
    /// <summary>Gets the cooperative operation deadline duration.</summary>
    public TimeSpan MaximumElapsedTime { get; }

    internal long GetLimit(PdfResourceKind resource) => resource switch
    {
        PdfResourceKind.InputBytes => MaximumInputBytes,
        PdfResourceKind.ExpandedBytes => MaximumExpandedBytes,
        PdfResourceKind.OutputBytes => MaximumOutputBytes,
        PdfResourceKind.Objects => MaximumObjects,
        PdfResourceKind.Pixels => MaximumPixels,
        PdfResourceKind.Depth => MaximumDepth,
        PdfResourceKind.Workers => MaximumWorkers,
        _ => throw new ArgumentOutOfRangeException(nameof(resource))
    };

    private static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
}

/// <summary>A resource accounted by the operation context.</summary>
public enum PdfResourceKind
{
    /// <summary>Bytes delivered from input.</summary>
    InputBytes,
    /// <summary>Expanded or decoded bytes.</summary>
    ExpandedBytes,
    /// <summary>Written bytes and explicit output growth.</summary>
    OutputBytes,
    /// <summary>Parsed or generated objects.</summary>
    Objects,
    /// <summary>Decoded or rendered pixels.</summary>
    Pixels,
    /// <summary>Simultaneous recursion scopes.</summary>
    Depth,
    /// <summary>Simultaneous worker scopes.</summary>
    Workers
}
