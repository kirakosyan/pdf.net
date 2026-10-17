using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace Fluent.Pdf;

/// <summary>A detached integrity preflight result, not permission to execute or proof of ABI/sandbox compatibility.</summary>
public sealed class PdfNativeDeploymentReport
{
    internal PdfNativeDeploymentReport(PdfNativeDeployment deployment, string? currentRuntimeIdentifier,
        int verifiedAssetCount, List<PdfDiagnostic> diagnostics)
    {
        Deployment = deployment;
        CurrentRuntimeIdentifier = currentRuntimeIdentifier;
        VerifiedAssetCount = verifiedAssetCount;
        Diagnostics = new ReadOnlyCollection<PdfDiagnostic>(diagnostics.ToArray());
        AssetsVerified = diagnostics.Count == 0 && verifiedAssetCount == deployment.Assets.Count;
    }

    /// <summary>Gets the immutable deployment description inspected.</summary>
    public PdfNativeDeployment Deployment { get; }
    /// <summary>Gets the actual supported OS/process-architecture RID, or null for an unsupported host.</summary>
    public string? CurrentRuntimeIdentifier { get; }
    /// <summary>Gets how many pinned files passed size, hash and applicable header/architecture checks.</summary>
    public int VerifiedAssetCount { get; }
    /// <summary>Gets whether every asset passed on the declared host; this never asserts dependency loading or execution safety.</summary>
    public bool AssetsVerified { get; }
    /// <summary>Gets stable failures in asset inventory order, including portable asset names but excluding directory paths and file contents.</summary>
    public IReadOnlyList<PdfDiagnostic> Diagnostics { get; }
}

/// <summary>Inspects explicitly provisioned local files without loading libraries, executing code or searching PATH.</summary>
public static class PdfNativeDeploymentInspector
{
    /// <summary>Gets a supported RID for the current OS and process architecture, or null.</summary>
    public static string? CurrentRuntimeIdentifier
    {
        get
        {
            string? os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
                RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : null;
            string rid = os + "-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            return PdfNativeDeployment.IsSupportedRuntimeIdentifier(rid) ? rid : null;
        }
    }

    /// <summary>Performs a read-only preflight with bounded buffers and cooperative cancellation between file reads.</summary>
    /// <remarks>Root/file reparse points are rejected. Caller-controlled immutable directory ancestry is required; this is not a race-free secure loader or a native worker sandbox.</remarks>
    public static PdfNativeDeploymentReport Inspect(PdfNativeDeployment deployment, CancellationToken cancellationToken = default) =>
        InspectCore(deployment, cancellationToken.ThrowIfCancellationRequested);

    /// <summary>Preflights within an SDK operation; any integrity failure is reported as a latched fatal diagnostic.</summary>
    /// <remarks>The deployment byte ceiling bounds this configuration I/O; operation checkpoints enforce cancellation/deadline. No native execution occurs.</remarks>
    public static PdfNativeDeploymentReport Verify(PdfNativeDeployment deployment, PdfOperationContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        var report = InspectCore(deployment, context.Checkpoint);
        foreach (var diagnostic in report.Diagnostics) context.Report(diagnostic);
        return report;
    }

    private static PdfNativeDeploymentReport InspectCore(PdfNativeDeployment deployment, Action checkpoint)
    {
        if (deployment is null) throw new ArgumentNullException(nameof(deployment));
        checkpoint();
        var diagnostics = new List<PdfDiagnostic>();
        string? current = CurrentRuntimeIdentifier;
        int verified = 0;
        if (current != deployment.RuntimeIdentifier)
        {
            diagnostics.Add(Failure("Native.RuntimeMismatch", "The deployment RID does not match a supported current OS/process architecture.", deployment));
            return new PdfNativeDeploymentReport(deployment, current, verified, diagnostics);
        }
        try
        {
            var attributes = File.GetAttributes(deployment.RootDirectory);
            if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
            {
                diagnostics.Add(Failure("Native.UnsafePath", "The deployment root must be an ordinary directory.", deployment));
                return new PdfNativeDeploymentReport(deployment, current, verified, diagnostics);
            }
        }
        catch (FileNotFoundException) { diagnostics.Add(Failure("Native.AssetMissing", "The deployment directory is unavailable.", deployment)); }
        catch (DirectoryNotFoundException) { diagnostics.Add(Failure("Native.AssetMissing", "The deployment directory is unavailable.", deployment)); }
        catch (IOException) { diagnostics.Add(Failure("Native.AssetInaccessible", "The deployment directory cannot be inspected.", deployment)); }
        catch (UnauthorizedAccessException) { diagnostics.Add(Failure("Native.AssetInaccessible", "The deployment directory cannot be inspected.", deployment)); }
        checkpoint();
        if (diagnostics.Count != 0) return new PdfNativeDeploymentReport(deployment, current, verified, diagnostics);

        // One reusable buffer; declared lengths and cumulative totals were checked before any file is opened.
        var buffer = new byte[65536];
        foreach (var asset in deployment.Assets)
        {
            checkpoint();
            string? code;
            try
            {
                string path = Path.Combine(deployment.RootDirectory, asset.FileName);
                var attributes = File.GetAttributes(path);
                if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.Device)) != 0)
                    code = "Native.UnsafePath";
                else
                {
                    // Read sharing does not promise race-free loading. Do not hand this path to an in-process loader.
                    using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                    code = InspectAsset(input, asset, deployment.RuntimeIdentifier, buffer, checkpoint);
                }
            }
            catch (FileNotFoundException) { code = "Native.AssetMissing"; }
            catch (DirectoryNotFoundException) { code = "Native.AssetMissing"; }
            catch (UnauthorizedAccessException) { code = "Native.AssetInaccessible"; }
            catch (IOException) { code = "Native.AssetInaccessible"; }
            checkpoint();
            if (code is null) verified++;
            else diagnostics.Add(Failure(code, "The pinned asset '" + asset.FileName + "' failed preflight.", deployment));
        }
        checkpoint();
        return new PdfNativeDeploymentReport(deployment, current, verified, diagnostics);
    }

    internal static string? InspectAsset(Stream input, PdfNativeAsset asset, string rid, byte[] buffer, Action checkpoint)
    {
        if (input.Length != asset.Length) return "Native.SizeMismatch";
        using var hash = SHA256.Create();
        long remaining = asset.Length;
        while (remaining > 0)
        {
            checkpoint();
            int read = input.Read(buffer, 0, (int)Math.Min(remaining, buffer.Length));
            checkpoint();
            if (read == 0) return "Native.SizeMismatch";
            hash.TransformBlock(buffer, 0, read, buffer, 0);
            remaining -= read;
        }
        checkpoint();
        if (input.ReadByte() != -1 || input.Length != asset.Length) return "Native.SizeMismatch";
        hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        checkpoint();
        string digest = BitConverter.ToString(hash.Hash!).Replace("-", "").ToLowerInvariant();
        if (digest != asset.Sha256) return "Native.HashMismatch";
        return asset.IsNativeBinary ? InspectBinary(input, rid, checkpoint) : null;
    }

    internal static string? InspectBinary(Stream input, string rid, Action checkpoint)
    {
        var header = new byte[64];
        input.Position = 0;
        if (!ReadHeader(input, header, checkpoint)) return "Native.BinaryFormatUnsupported";
        if (rid.StartsWith("win-", StringComparison.Ordinal))
        {
            if (header[0] != 'M' || header[1] != 'Z') return "Native.BinaryFormatUnsupported";
            uint offset = UInt32(header, 60);
            if (offset < 64 || offset > 1048576 || offset > input.Length - 64) return "Native.BinaryFormatUnsupported";
            input.Position = offset;
            if (!ReadHeader(input, header, checkpoint) || header[0] != 'P' || header[1] != 'E' || header[2] != 0 || header[3] != 0)
                return "Native.BinaryFormatUnsupported";
            int machine = header[4] | header[5] << 8;
            int expected = rid == "win-x86" ? 0x14c : rid == "win-x64" ? 0x8664 : 0xaa64;
            return machine == expected ? null : "Native.ArchitectureMismatch";
        }
        if (rid.StartsWith("linux-", StringComparison.Ordinal))
        {
            if (header[0] != 0x7f || header[1] != 'E' || header[2] != 'L' || header[3] != 'F' ||
                header[5] != 1 || header[6] != 1 || UInt32(header, 20) != 1 ||
                (header[16] | header[17] << 8) is not (2 or 3))
                return "Native.BinaryFormatUnsupported";
            int machine = header[18] | header[19] << 8;
            int expected = rid == "linux-x64" ? 62 : 183;
            return header[4] == 2 && machine == expected ? null : "Native.ArchitectureMismatch";
        }
        if (UInt32(header, 0) != 0xfeedfacf || UInt32(header, 12) is not (2 or 6 or 8))
            return "Native.BinaryFormatUnsupported";
        uint cpu = UInt32(header, 4);
        uint expectedCpu = rid == "osx-x64" ? 0x01000007u : 0x0100000cu;
        return cpu == expectedCpu ? null : "Native.ArchitectureMismatch";
    }

    private static bool ReadHeader(Stream input, byte[] header, Action checkpoint)
    {
        int filled = 0;
        while (filled < header.Length)
        {
            checkpoint();
            int read = input.Read(header, filled, header.Length - filled);
            checkpoint();
            if (read == 0) return false;
            filled += read;
        }
        return true;
    }

    private static uint UInt32(byte[] bytes, int offset) =>
        (uint)(bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24);

    private static PdfDiagnostic Failure(string code, string message, PdfNativeDeployment deployment) =>
        new(code, message, providerId: deployment.EngineId, operation: "NativeDeployment.Inspect",
            remediation: "Provision the exact pinned assets in a trusted local directory; use a separately qualified isolated worker before execution.");
}
