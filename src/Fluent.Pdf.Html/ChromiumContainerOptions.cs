using System.IO;

namespace Fluent.Pdf.Html;

/// <summary>Explicit local Linux-container browser prerequisites. No CLI, daemon, image, policy or credential discovery is permitted.</summary>
public sealed class ChromiumContainerOptions
{
    /// <summary>Captures exact CLI/policy hashes, a local Unix/named-pipe daemon endpoint, immutable local image ID and a protected empty CLI configuration directory. The caller provisions/reviews every prerequisite; none is downloaded.</summary>
    public ChromiumContainerOptions(string dockerExecutable, string dockerSha256, string daemonEndpoint, string imageId, string emptyConfigurationDirectory, string seccompProfile, string seccompSha256, long maximumMemoryBytes, int maximumProcesses = 64, TimeSpan? maximumElapsedTime = null)
    {
        DockerExecutable = Absolute(dockerExecutable, nameof(dockerExecutable));
        DockerSha256 = Hash(dockerSha256, nameof(dockerSha256));
        EmptyConfigurationDirectory = Absolute(emptyConfigurationDirectory, nameof(emptyConfigurationDirectory));
        SeccompProfile = Absolute(seccompProfile, nameof(seccompProfile));
        SeccompSha256 = Hash(seccompSha256, nameof(seccompSha256));
        if (daemonEndpoint == null || daemonEndpoint.Length > 512 || daemonEndpoint.Any(c => c <= 32 || c >= 127) || !(daemonEndpoint.StartsWith("unix:///", StringComparison.Ordinal) && !daemonEndpoint.Contains("..") || daemonEndpoint == "npipe:////./pipe/docker_engine" || daemonEndpoint == "npipe:////./pipe/dockerDesktopLinuxEngine"))
            throw new ArgumentException("An explicit local Unix socket or supported local Docker named pipe is required.", nameof(daemonEndpoint));
        DaemonEndpoint = daemonEndpoint;
        if (imageId == null || !imageId.StartsWith("sha256:", StringComparison.Ordinal))
            throw new ArgumentException("An immutable local SHA256 image ID is required.", nameof(imageId));
        ImageId = "sha256:" + Hash(imageId.Substring(7), nameof(imageId));
        if (maximumMemoryBytes < 268435456 || maximumMemoryBytes > 2147483648)
            throw new ArgumentOutOfRangeException(nameof(maximumMemoryBytes));
        MaximumMemoryBytes = maximumMemoryBytes;
        if (maximumProcesses < 8 || maximumProcesses > 512)
            throw new ArgumentOutOfRangeException(nameof(maximumProcesses));
        MaximumProcesses = maximumProcesses;
        MaximumElapsedTime = maximumElapsedTime ?? TimeSpan.FromSeconds(30);
        if (MaximumElapsedTime < TimeSpan.FromSeconds(1) || MaximumElapsedTime > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(nameof(maximumElapsedTime));
    }

    /// <summary>Gets the explicitly pinned Docker CLI path.</summary>
    public string DockerExecutable { get; }
    /// <summary>Gets its exact lowercase SHA256.</summary>
    public string DockerSha256 { get; }
    /// <summary>Gets the explicitly selected local daemon endpoint.</summary>
    public string DaemonEndpoint { get; }
    /// <summary>Gets the immutable local image ID; pulling is prohibited.</summary>
    public string ImageId { get; }
    /// <summary>Gets the caller-protected empty Docker configuration directory.</summary>
    public string EmptyConfigurationDirectory { get; }
    /// <summary>Gets the exact caller-reviewed default-deny seccomp profile path.</summary>
    public string SeccompProfile { get; }
    /// <summary>Gets its exact lowercase SHA256.</summary>
    public string SeccompSha256 { get; }
    /// <summary>Gets the hard browser-container memory/swap ceiling reserved against the operation budget.</summary>
    public long MaximumMemoryBytes { get; }
    /// <summary>Gets the container process/thread ceiling.</summary>
    public int MaximumProcesses { get; }
    /// <summary>Gets its finite wall deadline; operation/readiness cancellation may expire sooner.</summary>
    public TimeSpan MaximumElapsedTime { get; }

    private static string Absolute(string value, string parameter)
    {
        if (value == null || value.Length > 1024 || value.StartsWith("\\\\", StringComparison.Ordinal) || value.StartsWith("//", StringComparison.Ordinal) || !Path.IsPathRooted(value) || string.IsNullOrEmpty(Path.GetPathRoot(value)) || !Path.GetPathRoot(value)!.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
            throw new ArgumentException("An explicit bounded absolute local path is required.", parameter);
        return Path.GetFullPath(value);
    }
    private static string Hash(string value, string parameter)
    {
        if (value == null || value.Length != 64 || value.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')))
            throw new ArgumentException("An exact lowercase SHA256 is required.", parameter);
        return value;
    }
}
