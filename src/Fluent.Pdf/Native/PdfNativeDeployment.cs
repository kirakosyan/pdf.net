using System.Collections.ObjectModel;
using System.IO;

namespace Fluent.Pdf;

/// <summary>A pinned local asset. The inspector never downloads or loads this file.</summary>
public sealed class PdfNativeAsset
{
    /// <summary>Captures a portable single file name, exact positive length, SHA-256 and binary/data role.</summary>
    public PdfNativeAsset(string fileName, long length, string sha256, bool isNativeBinary = true)
    {
        FileName = Guard.Text(fileName, nameof(fileName), 128);
        foreach (char c in fileName)
            if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '.' || c == '_' || c == '-'))
                throw new ArgumentException("Use a portable single file name.", nameof(fileName));
        if (fileName == "." || fileName == ".." || fileName.EndsWith(".", StringComparison.Ordinal))
            throw new ArgumentException("Use a portable single file name.", nameof(fileName));
        string stem = fileName.Split('.')[0].ToUpperInvariant();
        if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" ||
            stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
            stem[3] >= '1' && stem[3] <= '9')
            throw new ArgumentException("Device names are not assets.", nameof(fileName));
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
        Length = length;
        if (sha256 is null) throw new ArgumentNullException(nameof(sha256));
        if (sha256.Length != 64) throw new ArgumentException("Specify a SHA-256 hex digest.", nameof(sha256));
        foreach (char c in sha256)
            if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F'))
                throw new ArgumentException("Specify a SHA-256 hex digest.", nameof(sha256));
        Sha256 = sha256.ToLowerInvariant();
        IsNativeBinary = isNativeBinary;
    }

    /// <summary>Gets the single file name, relative to the explicitly configured directory.</summary>
    public string FileName { get; }
    /// <summary>Gets the expected positive byte length.</summary>
    public long Length { get; }
    /// <summary>Gets the expected lowercase SHA-256 hex digest.</summary>
    public string Sha256 { get; }
    /// <summary>Gets whether this asset requires a native binary header/architecture check.</summary>
    public bool IsNativeBinary { get; }
}

/// <summary>An immutable offline provisioning description; it grants no execution capability.</summary>
public sealed class PdfNativeDeployment
{
    /// <summary>Captures exact engine/ABI labels and 1–64 uniquely named assets under an explicit local absolute directory.</summary>
    /// <remarks>The caller must provision a trusted directory and pin the complete dependency set. ABI is a declaration, not a verified compatibility claim.</remarks>
    public PdfNativeDeployment(string rootDirectory, string runtimeIdentifier, string engineId, string engineVersion,
        string abi, long maximumTotalBytes, params PdfNativeAsset[] assets)
    {
        if (rootDirectory is null) throw new ArgumentNullException(nameof(rootDirectory));
        string? root = Path.GetPathRoot(rootDirectory);
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(rootDirectory) ||
            !(root!.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
              root.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)) ||
            rootDirectory.StartsWith(@"\\", StringComparison.Ordinal) || rootDirectory.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException("Specify a local, fully qualified directory.", nameof(rootDirectory));
        if (Path.DirectorySeparatorChar == '\\' && (root!.Length != 3 || root[1] != ':' || root[2] != '\\'))
            throw new ArgumentException("Specify a fully qualified local drive directory.", nameof(rootDirectory));
        string fullPath = Path.GetFullPath(rootDirectory);
        RootDirectory = fullPath.Length > root!.Length
            ? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : fullPath;
        RuntimeIdentifier = Guard.Text(runtimeIdentifier, nameof(runtimeIdentifier), 32);
        if (!IsSupportedRuntimeIdentifier(runtimeIdentifier))
            throw new ArgumentException("Specify win-x86, win-x64, win-arm64, linux-x64, linux-arm64, osx-x64 or osx-arm64.", nameof(runtimeIdentifier));
        EngineId = Guard.Text(engineId, nameof(engineId), 128);
        EngineVersion = Guard.Text(engineVersion, nameof(engineVersion), 128);
        Abi = Guard.Text(abi, nameof(abi), 128);
        if (maximumTotalBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTotalBytes));
        MaximumTotalBytes = maximumTotalBytes;
        if (assets is null) throw new ArgumentNullException(nameof(assets));
        if (assets.Length == 0 || assets.Length > 64) throw new ArgumentException("Specify 1–64 pinned assets.", nameof(assets));
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var copy = new PdfNativeAsset[assets.Length];
        long total = 0;
        bool binary = false;
        for (int i = 0; i < assets.Length; i++)
        {
            var asset = assets[i] ?? throw new ArgumentException("Assets cannot contain null.", nameof(assets));
            if (!names.Add(asset.FileName)) throw new ArgumentException("Asset names must be unique without case.", nameof(assets));
            if (asset.Length > maximumTotalBytes - total) throw new ArgumentException("Declared asset bytes exceed the provisioning budget.", nameof(assets));
            total += asset.Length;
            binary |= asset.IsNativeBinary;
            copy[i] = asset;
        }
        if (!binary) throw new ArgumentException("At least one native binary must be pinned.", nameof(assets));
        Assets = new ReadOnlyCollection<PdfNativeAsset>(copy);
    }

    /// <summary>Gets the normalized local directory; the inspector creates no files there.</summary>
    public string RootDirectory { get; }
    /// <summary>Gets the declared target RID.</summary>
    public string RuntimeIdentifier { get; }
    /// <summary>Gets the declared engine identity.</summary>
    public string EngineId { get; }
    /// <summary>Gets the exact caller-pinned engine version label.</summary>
    public string EngineVersion { get; }
    /// <summary>Gets the caller-declared ABI label; exports/libc/loader compatibility require a later isolated probe.</summary>
    public string Abi { get; }
    /// <summary>Gets the maximum cumulative declared asset bytes.</summary>
    public long MaximumTotalBytes { get; }
    /// <summary>Gets the defensive read-only asset inventory, in diagnostic order.</summary>
    public IReadOnlyList<PdfNativeAsset> Assets { get; }

    internal static bool IsSupportedRuntimeIdentifier(string value) =>
        value == "win-x86" || value == "win-x64" || value == "win-arm64" ||
        value == "linux-x64" || value == "linux-arm64" || value == "osx-x64" || value == "osx-arm64";
}
