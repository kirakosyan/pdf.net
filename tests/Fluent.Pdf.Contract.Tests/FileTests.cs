using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class FileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "fluent-pdf-contract-" + Guid.NewGuid().ToString("N"));
    public FileTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedFileSaveDoesNotPublishNewOrReplaceExistingOutput(bool existing)
    {
        var path = Path.Combine(_directory, "document.bin");
        if (existing) File.WriteAllText(path, "original");
        var provider = new ContractProvider { OnWrite = (_, stream, _) => { stream.WriteByte(1); throw new IOException("Injected failure."); } };
        using var document = Pdf.Create(providers: new PdfProviderRegistry(provider)).AddPage();
        Assert.Throws<IOException>(() => document.SaveFile(path));
        if (existing) Assert.Equal("original", File.ReadAllText(path));
        else Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFiles(_directory, ".fluent-pdf-*.tmp"));
    }

    [Fact]
    public void CompletedFileSaveCreatesAndAtomicallyReplacesTheDestination()
    {
        var path = Path.Combine(_directory, "document.bin");
        using var document = Pdf.Create(providers: new PdfProviderRegistry(new ContractProvider())).AddPage();
        document.SaveFile(path);
        Assert.Equal("contract:1", File.ReadAllText(path));
        document.AddPage();
        document.SaveFile(path);
        Assert.Equal("contract:2", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(_directory, ".fluent-pdf-*.tmp"));
    }

    [Fact]
    public void CancellationAfterProviderReturnPreservesExistingFile()
    {
        var path = Path.Combine(_directory, "document.bin");
        File.WriteAllText(path, "original");
        using var cancellation = new CancellationTokenSource();
        var provider = new ContractProvider { OnWrite = (_, stream, token) => { Assert.True(token.CanBeCanceled); stream.WriteByte(1); cancellation.Cancel(); Assert.True(token.IsCancellationRequested); } };
        using var document = Pdf.Create(providers: new PdfProviderRegistry(provider));
        Assert.Throws<OperationCanceledException>(() => document.SaveFile(path, cancellation.Token));
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(_directory, ".fluent-pdf-*.tmp"));
    }

    [Fact]
    public void MissingProviderCreatesNoTemporaryFileAndOpensNoInputFile()
    {
        var path = Path.Combine(_directory, "missing.bin");
        using var document = Pdf.Create(providers:PdfProviderRegistry.Empty);
        Assert.Throws<PdfProviderUnavailableException>(() => document.SaveFile(path));
        Assert.Throws<PdfProviderUnavailableException>(() => Pdf.OpenFile(path,providers:PdfProviderRegistry.Empty));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public void PathOpenClosesItsOwnedHandleBeforeReturning()
    {
        var path = Path.Combine(_directory, "input.bin");
        File.WriteAllBytes(path, new byte[] { 1 });
        using var document = Pdf.OpenFile(path, providers: new PdfProviderRegistry(new ContractProvider()));
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Single(document.Pages);
    }

    [Theory]
    [InlineData(PdfResourceKind.InputBytes)]
    [InlineData(PdfResourceKind.ExpandedBytes)]
    [InlineData(PdfResourceKind.OutputBytes)]
    [InlineData(PdfResourceKind.Objects)]
    [InlineData(PdfResourceKind.Pixels)]
    [InlineData(PdfResourceKind.Depth)]
    [InlineData(PdfResourceKind.Workers)]
    public void EveryBudgetFailureLeavesTheDestinationUnpublishedAndReleasesTemporaryHandles(PdfResourceKind resource)
    {
        var path = Path.Combine(_directory, "budget.bin");
        File.WriteAllText(path, "original");
        var provider = new ContractProvider { OnWriteContext = (_, stream, context) =>
        {
            stream.WriteByte(1);
            if (resource == PdfResourceKind.Depth || resource == PdfResourceKind.Workers)
            {
                using var first = context.EnterScope(resource);
                using var second = context.EnterScope(resource);
            }
            else context.Consume(resource, 2);
        } };
        var options = PolicyIoTests.Options(new PdfResourceLimits(1, 1, 1, 1, 1, 1, 1));
        using var document = Pdf.Create(options, new PdfProviderRegistry(provider));
        Assert.Equal("Limit." + resource, Assert.Throws<PdfException>(() => document.SaveFile(path)).Diagnostic.Code);
        Assert.False(document.LastOperationReport!.Succeeded);
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(_directory, ".fluent-pdf-*.tmp"));
        using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        // A fresh operation owns fresh counters; failure does not poison the document or a shared provider.
        provider.OnWriteContext = (_, stream, _) => stream.WriteByte(2);
        Assert.True(document.SaveFile(path).Succeeded);
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("strict")]
    [InlineData("diagnostics")]
    [InlineData("deadline")]
    [InlineData("cancellation")]
    public void PolicyFailuresPreventAtomicCommit(string reason)
    {
        var path = Path.Combine(_directory, "policy.bin");
        File.WriteAllText(path, "original");
        using var cancellation = new CancellationTokenSource();
        var provider = new ContractProvider { OnWriteContext = (_, stream, context) =>
        {
            stream.WriteByte(1);
            switch (reason)
            {
                case "strict": context.Report(PolicyTests.Loss(0, 1)); break;
                case "diagnostics":
                    context.Report(new PdfDiagnostic("Info", "First.", severity: PdfDiagnosticSeverity.Information));
                    context.Report(new PdfDiagnostic("Info", "Second.", severity: PdfDiagnosticSeverity.Information));
                    break;
                case "deadline": Assert.True(context.CancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(5))); break;
                case "cancellation": cancellation.Cancel(); break;
            }
        } };
        var elapsed = reason == "deadline" ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(30);
        using var document = Pdf.Create(PolicyIoTests.Options(new PdfResourceLimits(maximumDiagnostics: 1, maximumElapsedTime: elapsed)), new PdfProviderRegistry(provider));
        if (reason == "cancellation") Assert.Throws<OperationCanceledException>(() => document.SaveFile(path, cancellation.Token));
        else Assert.Throws<PdfException>(() => document.SaveFile(path, cancellation.Token));
        Assert.False(document.LastOperationReport!.Succeeded);
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(_directory, ".fluent-pdf-*.tmp"));
    }

    [Fact]
    public void InputBudgetFailureClosesTheSdkOwnedFileBeforeThrowing()
    {
        var path = Path.Combine(_directory, "oversized.bin");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        var options = PolicyIoTests.Options(new PdfResourceLimits(maximumInputBytes: 2));
        Assert.Equal("Limit.InputBytes", Assert.Throws<PdfException>(() => Pdf.OpenFile(path, options,
            new PdfProviderRegistry(new ContractProvider()))).Diagnostic.Code);
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(3, exclusive.Length);
    }

    public void Dispose()
    {
        // Only exact files in the unique directory created by this test are removed.
        foreach (var path in Directory.GetFiles(_directory)) File.Delete(path);
        Directory.Delete(_directory);
    }
}
