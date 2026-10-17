using System.IO;

namespace Fluent.Pdf.Workers;

/// <summary>Explicit offline deployment and limits for a fresh isolated native bridge process.</summary>
public sealed class NativeWorkerOptions
{
    /// <summary>Captures pinned executable/library names, an owned-job root and hard input/output ceilings.</summary>
    public NativeWorkerOptions(PdfNativeDeployment deployment, string executableFileName, string libraryFileName,
        string temporaryDirectory, long maximumMemoryBytes = 268435456, int maximumInputBytes = 67108864,
        int maximumOutputBytes = 67108864, TimeSpan? maximumElapsedTime = null)
    {
        Deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
        ExecutableFileName = RequireBinary(executableFileName);
        LibraryFileName = RequireBinary(libraryFileName);
        if (ExecutableFileName == LibraryFileName) throw new ArgumentException("The worker and engine must be distinct pinned binaries.");
        if (temporaryDirectory is null) throw new ArgumentNullException(nameof(temporaryDirectory));
        if (!Path.IsPathRooted(temporaryDirectory)) throw new ArgumentException("An absolute caller-owned job directory is required.", nameof(temporaryDirectory));
        TemporaryDirectory = Path.GetFullPath(temporaryDirectory);
        if (maximumMemoryBytes < 16777216 || maximumMemoryBytes > 1099511627776) throw new ArgumentOutOfRangeException(nameof(maximumMemoryBytes));
        if (IntPtr.Size == 4 && maximumMemoryBytes > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(maximumMemoryBytes));
        foreach (char c in deployment.Abi + deployment.EngineVersion)
            if (c < 33 || c > 126 || c == '"') throw new ArgumentException("Bridge ABI/version identifiers must be printable ASCII without quotes.", nameof(deployment));
        if (maximumInputBytes < 0) throw new ArgumentOutOfRangeException(nameof(maximumInputBytes));
        if (maximumOutputBytes < 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputBytes));
        var timeout = maximumElapsedTime ?? TimeSpan.FromSeconds(30);
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(maximumElapsedTime));
        MaximumMemoryBytes = maximumMemoryBytes;
        MaximumInputBytes = maximumInputBytes;
        MaximumOutputBytes = maximumOutputBytes;
        MaximumElapsedTime = timeout;
    }

    /// <summary>Gets the immutable pinned deployment, including the native worker executable.</summary>
    public PdfNativeDeployment Deployment { get; }
    /// <summary>Gets the pinned worker executable file name.</summary>
    public string ExecutableFileName { get; }
    /// <summary>Gets the pinned engine bridge library file name.</summary>
    public string LibraryFileName { get; }
    /// <summary>Gets the trusted local parent for private, single-job staging directories.</summary>
    public string TemporaryDirectory { get; }
    /// <summary>Gets the process memory ceiling in bytes.</summary>
    public long MaximumMemoryBytes { get; }
    /// <summary>Gets the maximum request bytes accepted by the bridge.</summary>
    public int MaximumInputBytes { get; }
    /// <summary>Gets the maximum response bytes accepted from the bridge.</summary>
    public int MaximumOutputBytes { get; }
    /// <summary>Gets the per-worker deadline, additionally constrained by the operation context.</summary>
    public TimeSpan MaximumElapsedTime { get; }

    private string RequireBinary(string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        foreach (var asset in Deployment.Assets)
            if (asset.FileName == name && asset.IsNativeBinary) return name;
        throw new ArgumentException("The exact binary name must occur in the pinned deployment.", nameof(name));
    }
}
