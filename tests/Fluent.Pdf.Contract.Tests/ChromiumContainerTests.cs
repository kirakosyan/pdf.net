using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Fluent.Pdf.Html;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class ChromiumContainerTests
{
    private static readonly PdfResourceLimits Limits = new(maximumExpandedBytes: 1073741824, maximumElapsedTime: TimeSpan.FromSeconds(20));
    private sealed class Fixture
    {
        internal readonly string Root = Path.Combine(ArchivalFixture.Root(), "artifacts", "contracts", "browser-transport", Guid.NewGuid().ToString("N"));
        internal readonly string Executable, Policy, Config;
        internal Fixture(bool cleanupDenied = false)
        {
            string rid = PdfNativeDeploymentInspector.OperatingSystemRuntimeIdentifier ?? throw new InvalidOperationException("Unsupported source host.");
            Executable = Path.Combine(ArchivalFixture.Root(), "artifacts", "browser-cli-fixture", rid, rid.StartsWith("win-", StringComparison.Ordinal) ? "browser-cli-fixture.exe" : "browser-cli-fixture");
            Assert.True(File.Exists(Executable), "Build the original process peer with eng/build-browser-cli-fixture.ps1.");
            Directory.CreateDirectory(Root);
            Policy = Path.Combine(Root, "policy.json");
            File.WriteAllText(Policy, "{\"defaultAction\":\"SCMP_ACT_ERRNO\"}");
            Config = Path.Combine(Root, cleanupDenied ? "cleanup-denied" : "empty configuration");
            Directory.CreateDirectory(Config);
        }
        internal ChromiumContainerOptions Options(char mode = 'a', TimeSpan? timeout = null) => new(Executable, Hash(Executable), "unix:///explicit-source-fixture.sock", "sha256:" + new string(mode, 64), Config, Policy, Hash(Policy), 268435456, maximumElapsedTime: timeout);
        internal static string Hash(string path)
        {
            using var file = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }
    }

    [Fact]
    public void ExplicitPinsLocalEndpointsAndFiniteResourceBoundsAreRequired()
    {
        var f = new Fixture();
        Assert.Throws<ArgumentException>(() => new ChromiumContainerOptions(f.Executable, new string('0', 64), "tcp://remote.invalid:2375", "sha256:" + new string('a',64), f.Config, f.Policy, Fixture.Hash(f.Policy), 268435456));
        Assert.Throws<ArgumentException>(() => new ChromiumContainerOptions("relative", new string('0',64), "unix:///fixture", "sha256:" + new string('a',64), f.Config, f.Policy, Fixture.Hash(f.Policy), 268435456));
        Assert.Throws<ArgumentException>(() => new ChromiumContainerOptions(f.Executable, new string('0',64), "unix:///fixture", "latest", f.Config, f.Policy, Fixture.Hash(f.Policy), 268435456));
        Assert.Throws<ArgumentOutOfRangeException>(() => f.Options(timeout: TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() => new ChromiumContainerOptions("\\\\server\\share\\docker.exe", new string('0',64), "unix:///fixture", "sha256:" + new string('a',64), f.Config, f.Policy, Fixture.Hash(f.Policy), 268435456));
    }

    [Fact]
    public void OriginalPeerVerifiesOfflineFlagsAndEchoesUnicodeWithReleasedReservation()
    {
        var f = new Fixture();
        PolicyTests.Run(c =>
        {
            long before = c.Available(PdfResourceKind.ExpandedBytes);
            using (var connection = ChromiumContainerHost.Open(f.Options(), c))
            {
                connection.Send("{\"text\":\"Original Հայերեն 😀\"}", c);
                Assert.Equal("{\"text\":\"Original Հայերեն 😀\"}", connection.Receive(c));
            }
            Assert.True(before - c.Available(PdfResourceKind.ExpandedBytes) < 16777216);
        }, Limits);
    }

    [Theory]
    [InlineData('b')]
    [InlineData('d')]
    [InlineData('e')]
    public void InvalidBootstrapFramesAndExitedPeerFailWithoutPrivatePayloads(char mode)
    {
        var f = new Fixture();
        var failure = Assert.Throws<PdfException>(() => PolicyTests.Run(c =>
        {
            using var connection = ChromiumContainerHost.Open(f.Options(mode), c);
            connection.Send("{}", c);
            _ = connection.Receive(c);
        }, Limits));
        Assert.DoesNotContain("private-sentinel", failure.ToString());
        Assert.StartsWith("Html.", failure.Diagnostic.Code);
    }

    [Fact]
    public void BlockedReadCancellationTerminatesAndJoinsOwnedPeerWithinFiveSeconds()
    {
        var f = new Fixture();
        using var cancel = new CancellationTokenSource();
        var clock = Stopwatch.StartNew();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfFont.Run("ContainerFixture", "fixture", new(limits: Limits), cancel.Token, c =>
        {
            using var connection = ChromiumContainerHost.Open(f.Options('c'), c);
            connection.Send("{}", c);
            cancel.CancelAfter(100);
            _ = connection.Receive(c);
        }, PdfCapability.RenderHtml));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DeadlineAndFailedDaemonCleanupRemainFatalAndRetainOwnedIdentity()
    {
        var f = new Fixture();
        var deadline = Assert.Throws<PdfException>(() => PolicyTests.Run(c =>
        {
            using var connection = ChromiumContainerHost.Open(f.Options('c', TimeSpan.FromSeconds(1)), c);
            connection.Send("{}", c);
            _ = connection.Receive(c);
        }, Limits));
        Assert.Equal("Html.HostDeadline", deadline.Diagnostic.Code);
        var denied = new Fixture(cleanupDenied: true);
        var failure = Assert.Throws<PdfException>(() => PolicyTests.Run(c =>
        {
            using var connection = ChromiumContainerHost.Open(denied.Options(), c);
        }, Limits));
        Assert.Equal("Html.HostCleanup", failure.Diagnostic.Code);
        Assert.StartsWith("fluent-pdf-browser-", Assert.IsType<string>(failure.Data["Fluent.Pdf.Html.OwnedContainer"]));
    }

    [Fact]
    public void ChangedCliPolicyNonemptyConfigAndMemoryExhaustionRejectBeforeSendingData()
    {
        var f = new Fixture();
        File.WriteAllText(f.Policy, "{\"defaultAction\":\"SCMP_ACT_ALLOW\"}");
        Assert.Equal("Html.ContainerPolicy", Assert.Throws<PdfException>(() => PolicyTests.Run(c => ChromiumContainerHost.Open(f.Options(), c), Limits)).Diagnostic.Code);
        File.WriteAllText(f.Policy, "{\"defaultAction\":\"SCMP_ACT_ERRNO\"}");
        File.WriteAllText(Path.Combine(f.Config, "config.json"), "{}");
        Assert.Equal("Html.Configuration", Assert.Throws<PdfException>(() => PolicyTests.Run(c => ChromiumContainerHost.Open(f.Options(), c), Limits)).Diagnostic.Code);
        var empty = new Fixture();
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PolicyTests.Run(c => ChromiumContainerHost.Open(empty.Options(), c), new(maximumExpandedBytes: 16777216))).Diagnostic.Code);
        Assert.Equal("Html.PrerequisiteHash", Assert.Throws<PdfException>(() => PolicyTests.Run(c => ChromiumContainerHost.Open(new(empty.Executable, new string('0',64), "unix:///fixture", "sha256:"+new string('a',64), empty.Config, empty.Policy, Fixture.Hash(empty.Policy), 268435456), c), Limits)).Diagnostic.Code);
        Assert.Equal("Limit.Workers", Assert.Throws<PdfException>(() => PolicyTests.Run(c =>
        {
            using var occupied = c.EnterScope(PdfResourceKind.Workers);
            using var connection = ChromiumContainerHost.Open(empty.Options(), c);
        }, new(maximumWorkers: 1, maximumExpandedBytes: 1073741824))).Diagnostic.Code);
    }
}
