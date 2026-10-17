using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class NativeDeploymentTests
{
    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData("../engine.so")]
    [InlineData("sub/engine.so")]
    [InlineData(@"sub\engine.dll")]
    [InlineData("engine:stream")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("engine.")]
    [InlineData("engine ")]
    [InlineData("CON.dll")]
    [InlineData("nul")]
    [InlineData("COM1.bin")]
    [InlineData("lpt9.bin")]
    [InlineData("file\nname")]
    public void RejectsNonportableAndDeviceAssetNames(string name) =>
        Assert.Throws<ArgumentException>(() => new PdfNativeAsset(name, 1, Digest));

    [Fact]
    public void ValidatesLengthsDigestsAndMetadataBeforeOpeningAnything()
    {
        Assert.Throws<ArgumentNullException>(() => new PdfNativeAsset(null!, 1, Digest));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfNativeAsset("engine.bin", 0, Digest));
        Assert.Throws<ArgumentNullException>(() => new PdfNativeAsset("engine.bin", 1, null!));
        Assert.Throws<ArgumentException>(() => new PdfNativeAsset("engine.bin", 1, "abcd"));
        Assert.Throws<ArgumentException>(() => new PdfNativeAsset("engine.bin", 1, new string('z', 64)));
        var binary = new PdfNativeAsset("engine.bin", 10, Digest.ToUpperInvariant());
        Assert.Equal(Digest, binary.Sha256);
        string root = Path.GetTempPath();
        Assert.Throws<ArgumentException>(() => new PdfNativeDeployment("relative", Rid, "engine", "1", "abi", 10, binary));
        Assert.Throws<ArgumentException>(() => new PdfNativeDeployment(@"\\server\share", Rid, "engine", "1", "abi", 10, binary));
        Assert.Throws<ArgumentException>(() => new PdfNativeDeployment(root, "linux-mips", "engine", "1", "abi", 10, binary));
        Assert.Throws<ArgumentException>(() => new PdfNativeDeployment(root, Rid, " ", "1", "abi", 10, binary));
        Assert.Throws<ArgumentException>(() => new PdfNativeDeployment(root, Rid, "engine", "", "abi", 10, binary));
        Assert.Throws<ArgumentException>(() => new PdfNativeDeployment(root, Rid, "engine", "1", "", 10, binary));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfNativeDeployment(root, Rid, "engine", "1", "abi", 0, binary));
        Assert.Throws<ArgumentNullException>(() => new PdfNativeDeployment(root, Rid, "engine", "1", "abi", 10, null!));
        Assert.Throws<ArgumentException>(() => new PdfNativeDeployment(root, Rid, "engine", "1", "abi", 10));
        Assert.Throws<ArgumentException>(() => new PdfNativeDeployment(root, Rid, "engine", "1", "abi", 10, new PdfNativeAsset[65]));
        Assert.Throws<ArgumentException>(() => new PdfNativeDeployment(root, Rid, "engine", "1", "abi", 10, new PdfNativeAsset[] { null! }));
        Assert.Throws<ArgumentException>(() => new PdfNativeDeployment(root, Rid, "engine", "1", "abi", 20, binary,
            new PdfNativeAsset("ENGINE.BIN", 10, Digest)));
        Assert.Throws<ArgumentException>(() => new PdfNativeDeployment(root, Rid, "engine", "1", "abi", 10,
            new PdfNativeAsset("data.bin", 10, Digest, false)));
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    public void EnforcesProvisioningByteBoundary(long maximum, bool succeeds)
    {
        var binary = new PdfNativeAsset("engine.bin", 10, Digest);
        if (succeeds) Assert.Equal(maximum, new PdfNativeDeployment(Path.GetTempPath(), Rid, "engine", "1", "abi", maximum, binary).MaximumTotalBytes);
        else Assert.Throws<ArgumentException>(() => new PdfNativeDeployment(Path.GetTempPath(), Rid, "engine", "1", "abi", maximum, binary));
    }

    [Fact]
    public void RejectsCumulativeOverflowAndDefensivelyCapturesInventory()
    {
        var huge = new PdfNativeAsset("huge.bin", long.MaxValue, Digest);
        Assert.Throws<ArgumentException>(() => new PdfNativeDeployment(Path.GetTempPath(), Rid, "engine", "1", "abi", long.MaxValue,
            huge, new PdfNativeAsset("extra.bin", 1, Digest)));
        var binary = new PdfNativeAsset("engine.bin", 1, Digest);
        var assets = new[] { binary };
        var deployment = new PdfNativeDeployment(Path.GetTempPath(), Rid, "engine", "1.2.3", "declared-abi", 1, assets);
        assets[0] = huge;
        Assert.Same(binary, Assert.Single(deployment.Assets));
        Assert.Equal("1.2.3", deployment.EngineVersion);
        Assert.Equal("declared-abi", deployment.Abi);
        Assert.Throws<NotSupportedException>(() => ((IList<PdfNativeAsset>)deployment.Assets).Clear());
        var maximumInventory = new PdfNativeAsset[64];
        for (int i = 0; i < maximumInventory.Length; i++) maximumInventory[i] = new PdfNativeAsset("asset-" + i + ".bin", 1, Digest);
        Assert.Equal(64, new PdfNativeDeployment(Path.GetTempPath(), Rid, "engine", "1", "abi", 64, maximumInventory).Assets.Count);
    }

    [Theory]
    [InlineData("win-x86")]
    [InlineData("win-x64")]
    [InlineData("win-arm64")]
    [InlineData("linux-x64")]
    [InlineData("linux-arm64")]
    [InlineData("osx-x64")]
    [InlineData("osx-arm64")]
    public void RecognizesExplicitHeaderArchitectureWithoutExecutingIt(string rid)
    {
        using var input = new MemoryStream(Header(rid));
        Assert.Null(PdfNativeDeploymentInspector.InspectBinary(input, rid, () => { }));
    }

    [Theory]
    [InlineData("win-x64", "win-arm64")]
    [InlineData("win-arm64", "win-x86")]
    [InlineData("win-x86", "win-x64")]
    [InlineData("linux-x64", "linux-arm64")]
    [InlineData("linux-arm64", "linux-x64")]
    [InlineData("osx-x64", "osx-arm64")]
    [InlineData("osx-arm64", "osx-x64")]
    public void RejectsWrongBinaryArchitecture(string actual, string declared)
    {
        using var input = new MemoryStream(Header(actual));
        Assert.Equal("Native.ArchitectureMismatch", PdfNativeDeploymentInspector.InspectBinary(input, declared, () => { }));
    }

    [Theory]
    [InlineData("win-x64")]
    [InlineData("linux-x64")]
    [InlineData("osx-x64")]
    public void RejectsDamagedOrTruncatedHeaders(string rid)
    {
        using var damaged = new MemoryStream(new byte[256]);
        Assert.Equal("Native.BinaryFormatUnsupported", PdfNativeDeploymentInspector.InspectBinary(damaged, rid, () => { }));
        using var truncated = new MemoryStream(new byte[10]);
        Assert.Equal("Native.BinaryFormatUnsupported", PdfNativeDeploymentInspector.InspectBinary(truncated, rid, () => { }));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(63u)]
    [InlineData(200u)]
    [InlineData(1048577u)]
    [InlineData(uint.MaxValue)]
    public void RejectsUnboundedOrTruncatedPeOffsets(uint offset)
    {
        var bytes = Header("win-x64");
        Write32(bytes, 60, offset);
        using var input = new MemoryStream(bytes);
        Assert.Equal("Native.BinaryFormatUnsupported", PdfNativeDeploymentInspector.InspectBinary(input, "win-x64", () => { }));
    }

    [Fact]
    public void DoesNotAcceptWrongElfClassOrUnsupportedMachContainer()
    {
        var elf = Header("linux-x64");
        elf[4] = 1;
        using var elfInput = new MemoryStream(elf);
        Assert.Equal("Native.ArchitectureMismatch", PdfNativeDeploymentInspector.InspectBinary(elfInput, "linux-x64", () => { }));
        var mach = Header("osx-x64");
        Write32(mach, 0, 0xcafebabe);
        using var machInput = new MemoryStream(mach);
        Assert.Equal("Native.BinaryFormatUnsupported", PdfNativeDeploymentInspector.InspectBinary(machInput, "osx-x64", () => { }));
    }

    [Fact]
    public void InspectsPinnedBinaryAndDataReadOnlyWithDetachedResults()
    {
        using var directory = new DeploymentDirectory();
        var binary = directory.Write("engine.bin", Header(Rid));
        var data = directory.Write("data.bin", new byte[] { 1, 2, 3 }, false);
        var deployment = directory.Deployment(binary, data);
        var report = PdfNativeDeploymentInspector.Inspect(deployment);
        Assert.True(report.AssetsVerified);
        Assert.Equal(Rid, report.CurrentRuntimeIdentifier);
        Assert.Equal(2, report.VerifiedAssetCount);
        Assert.Empty(report.Diagnostics);
        Assert.Same(deployment, report.Deployment);
        Assert.Throws<NotSupportedException>(() => ((IList<PdfDiagnostic>)report.Diagnostics).Clear());
        File.Delete(Path.Combine(directory.Root, binary.FileName));
        Assert.True(report.AssetsVerified);
        Assert.Equal("data.bin", Path.GetFileName(Assert.Single(Directory.GetFileSystemEntries(directory.Root))));
    }

    [Fact]
    public void MissingDependencySizeAndHashFailuresHaveStableAssetDiagnostics()
    {
        using var directory = new DeploymentDirectory();
        var binary = directory.Write("engine.bin", Header(Rid));
        var absent = new PdfNativeAsset("missing.dat", 1, Digest, false);
        var size = new PdfNativeAsset("engine.bin", binary.Length + 1, binary.Sha256);
        AssertCode("Native.AssetMissing", PdfNativeDeploymentInspector.Inspect(directory.Deployment(binary, absent)), "missing.dat");
        AssertCode("Native.SizeMismatch", PdfNativeDeploymentInspector.Inspect(directory.Deployment(size)), "engine.bin");
        var hash = new PdfNativeAsset(binary.FileName, binary.Length, Digest);
        AssertCode("Native.HashMismatch", PdfNativeDeploymentInspector.Inspect(directory.Deployment(hash)), "engine.bin");
        var damaged = directory.Write("damaged.bin", new byte[256]);
        AssertCode("Native.BinaryFormatUnsupported", PdfNativeDeploymentInspector.Inspect(directory.Deployment(damaged)), "damaged.bin");
        string wrongArchitecture = Rid.Substring(0, Rid.LastIndexOf('-') + 1) + (Rid.EndsWith("x64", StringComparison.Ordinal) ? "arm64" : "x64");
        var wrongBinary = directory.Write("wrong-machine.bin", Header(wrongArchitecture));
        AssertCode("Native.ArchitectureMismatch", PdfNativeDeploymentInspector.Inspect(directory.Deployment(wrongBinary)), "wrong-machine.bin");
    }

    [Fact]
    public void RuntimeMismatchAndUnsafeFileTypeFailWithoutGrantingCapabilities()
    {
        using var directory = new DeploymentDirectory();
        var binary = directory.Write("engine.bin", Header(Rid));
        string other = Rid == "linux-arm64" ? "win-x64" : "linux-arm64";
        var wrong = new PdfNativeDeployment(directory.Root, other, "fixture", "1", "declared-abi", 4096, binary);
        AssertCode("Native.RuntimeMismatch", PdfNativeDeploymentInspector.Inspect(wrong));
        Directory.CreateDirectory(Path.Combine(directory.Root, "folder.bin"));
        var folder = new PdfNativeAsset("folder.bin", 1, Digest);
        AssertCode("Native.UnsafePath", PdfNativeDeploymentInspector.Inspect(directory.Deployment(folder)), "folder.bin");
        Assert.Empty(PdfProviderRegistry.Empty.Capabilities);
    }

    [Theory]
    [InlineData(PdfProcessingMode.Strict)]
    [InlineData(PdfProcessingMode.Permissive)]
    public void FailedPreflightCannotBeHiddenInsideAnSdkOperation(PdfProcessingMode mode)
    {
        using var directory = new DeploymentDirectory();
        var absent = new PdfNativeAsset("missing.bin", 1, Digest);
        var deployment = directory.Deployment(absent);
        using var provider = new ContractProvider();
        provider.OnWriteContext = (_, _, context) =>
        {
            Assert.Throws<PdfException>(() => PdfNativeDeploymentInspector.Verify(deployment, context));
        };
        using var document = Pdf.Create(new PdfOptions(operationPolicy: new PdfOperationPolicy(mode)), new PdfProviderRegistry(provider)).AddPage();
        using var output = new MemoryStream();
        var failure = Assert.Throws<PdfException>(() => document.Save(output));
        Assert.Equal("Native.AssetMissing", failure.Diagnostic.Code);
        Assert.False(document.LastOperationReport!.Succeeded);
        string destination = Path.Combine(directory.Root, "saved-output.txt");
        File.WriteAllText(destination, "original");
        Assert.Equal("Native.AssetMissing", Assert.Throws<PdfException>(() => document.SaveFile(destination)).Diagnostic.Code);
        Assert.Equal("original", File.ReadAllText(destination));
        Assert.Single(Directory.GetFileSystemEntries(directory.Root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExecutableLanguageExamplesInspectPinnedAssets(bool visualBasic)
    {
        using var directory = new DeploymentDirectory();
        var binary = directory.Write("engine.bin", Header(Rid));
        var report = visualBasic
            ? ContractExamples.VisualBasic.ContractExample.InspectDeployment(directory.Root, Rid, "1", "declared-abi", 4096, binary)
            : ContractExamples.CSharp.ContractExample.InspectDeployment(directory.Root, Rid, "1", "declared-abi", 4096, binary);
        Assert.True(report.AssetsVerified);
        Assert.Equal("example.native", report.Deployment.EngineId);
        Assert.Equal("declared-abi", report.Deployment.Abi);
    }

    [Fact]
    public void CancellationNullArgumentsAndMissingRootAreExplicit()
    {
        using var directory = new DeploymentDirectory();
        var absent = new PdfNativeAsset("missing.bin", 1, Digest);
        var deployment = directory.Deployment(absent);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => PdfNativeDeploymentInspector.Inspect(deployment, canceled.Token));
        Assert.Throws<ArgumentNullException>(() => PdfNativeDeploymentInspector.Inspect(null!));
        Assert.Throws<ArgumentNullException>(() => PdfNativeDeploymentInspector.Verify(deployment, null!));
        string missing = Path.Combine(directory.Root, "absent");
        var missingRoot = new PdfNativeDeployment(missing, Rid, "fixture", "1", "declared-abi", 1, absent);
        AssertCode("Native.AssetMissing", PdfNativeDeploymentInspector.Inspect(missingRoot));
    }

    [Fact]
    public void CancellationAfterAHashReadStopsBeforeAcceptingBytesAndKeepsTheStreamOpen()
    {
        using var canceled = new CancellationTokenSource();
        using var input = new CancelOnReadStream(Header(Rid), canceled);
        var asset = new PdfNativeAsset("engine.bin", input.Length, Digest);
        Assert.Throws<OperationCanceledException>(() => PdfNativeDeploymentInspector.InspectAsset(
            input, asset, Rid, new byte[32], canceled.Token.ThrowIfCancellationRequested));
        Assert.True(input.CanRead);
        Assert.Equal(32, input.Position);
    }

    [Fact]
    public void InspectsReadOnlyFilesAndRejectsLinkedFilesAndRootsWhereSupported()
    {
        using var directory = new DeploymentDirectory();
        var binary = directory.Write("engine.bin", Header(Rid));
        string path = Path.Combine(directory.Root, binary.FileName);
        var original = File.GetAttributes(path);
        try
        {
            File.SetAttributes(path, original | FileAttributes.ReadOnly);
            Assert.True(PdfNativeDeploymentInspector.Inspect(directory.Deployment(binary)).AssetsVerified);
            Assert.True((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0);
        }
        finally { File.SetAttributes(path, original); }
#if NET8_0_OR_GREATER
        // Source applicability: Unix modern runtimes expose unprivileged link creation and directory mode APIs.
        // Windows link privileges and minimum Framework behavior remain separate qualification obligations.
        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(directory.Root);
            try
            {
                File.SetUnixFileMode(directory.Root, UnixFileMode.UserRead | UnixFileMode.UserExecute);
                Assert.True(PdfNativeDeploymentInspector.Inspect(directory.Deployment(binary)).AssetsVerified);
            }
            finally { File.SetUnixFileMode(directory.Root, mode); }
            string link = Path.Combine(directory.Root, "linked.bin");
            File.CreateSymbolicLink(link, path);
            var linked = new PdfNativeAsset("linked.bin", binary.Length, binary.Sha256);
            AssertCode("Native.UnsafePath", PdfNativeDeploymentInspector.Inspect(directory.Deployment(linked)), "linked.bin");
            string rootLink = directory.Root + "-link";
            Directory.CreateSymbolicLink(rootLink, directory.Root);
            try
            {
                var linkedRoot = new PdfNativeDeployment(rootLink + Path.DirectorySeparatorChar, Rid, "fixture", "1", "abi", 4096, binary);
                AssertCode("Native.UnsafePath", PdfNativeDeploymentInspector.Inspect(linkedRoot));
            }
            finally { Directory.Delete(rootLink); }
        }
#endif
    }

    private static string Rid => PdfNativeDeploymentInspector.CurrentRuntimeIdentifier ??
        throw new InvalidOperationException("This source lane requires a supported native host.");

    private static void AssertCode(string code, PdfNativeDeploymentReport report, string? fileName = null)
    {
        Assert.False(report.AssetsVerified);
        var diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(code, diagnostic.Code);
        Assert.Equal("fixture", diagnostic.ProviderId);
        Assert.DoesNotContain(report.Deployment.RootDirectory, diagnostic.Message);
        if (fileName is not null) Assert.Contains(fileName, diagnostic.Message);
    }

    // Original inert header fixtures: these are not engines and are never loaded or executed.
    private static byte[] Header(string rid)
    {
        var bytes = new byte[256];
        if (rid.StartsWith("win-", StringComparison.Ordinal))
        {
            bytes[0] = (byte)'M'; bytes[1] = (byte)'Z'; Write32(bytes, 60, 64);
            bytes[64] = (byte)'P'; bytes[65] = (byte)'E';
            int machine = rid == "win-x86" ? 0x14c : rid == "win-x64" ? 0x8664 : 0xaa64;
            bytes[68] = (byte)machine; bytes[69] = (byte)(machine >> 8);
        }
        else if (rid.StartsWith("linux-", StringComparison.Ordinal))
        {
            bytes[0] = 0x7f; bytes[1] = (byte)'E'; bytes[2] = (byte)'L'; bytes[3] = (byte)'F';
            bytes[4] = 2; bytes[5] = 1; bytes[6] = 1; bytes[16] = 3;
            bytes[18] = (byte)(rid == "linux-x64" ? 62 : 183); Write32(bytes, 20, 1);
        }
        else
        {
            Write32(bytes, 0, 0xfeedfacf);
            Write32(bytes, 4, rid == "osx-x64" ? 0x01000007u : 0x0100000cu);
            Write32(bytes, 12, 6);
        }
        return bytes;
    }

    private static void Write32(byte[] bytes, int offset, uint value)
    {
        for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (8 * i));
    }

    private sealed class DeploymentDirectory : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "fluent-pdf-probe-" + Guid.NewGuid().ToString("N"));
        internal DeploymentDirectory() => Directory.CreateDirectory(Root);
        internal PdfNativeAsset Write(string name, byte[] bytes, bool binary = true)
        {
            File.WriteAllBytes(Path.Combine(Root, name), bytes);
            using var hash = SHA256.Create();
            string digest = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            return new PdfNativeAsset(name, bytes.Length, digest, binary);
        }
        internal PdfNativeDeployment Deployment(params PdfNativeAsset[] assets) =>
            new(Root, Rid, "fixture", "1", "declared-abi", 4096, assets);
        public void Dispose() => Directory.Delete(Root, true);
    }

    private sealed class CancelOnReadStream : MemoryStream
    {
        private readonly CancellationTokenSource _cancellation;
        internal CancelOnReadStream(byte[] bytes, CancellationTokenSource cancellation) : base(bytes) => _cancellation = cancellation;
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = base.Read(buffer, offset, count);
            _cancellation.Cancel();
            return read;
        }
    }
}
