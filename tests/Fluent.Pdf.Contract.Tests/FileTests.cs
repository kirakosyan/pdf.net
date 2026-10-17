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
        var provider = new ContractProvider { OnWrite = (_, stream, token) => { Assert.Equal(cancellation.Token, token); stream.WriteByte(1); cancellation.Cancel(); } };
        using var document = Pdf.Create(providers: new PdfProviderRegistry(provider));
        Assert.Throws<OperationCanceledException>(() => document.SaveFile(path, cancellation.Token));
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(_directory, ".fluent-pdf-*.tmp"));
    }

    [Fact]
    public void MissingProviderCreatesNoTemporaryFileAndOpensNoInputFile()
    {
        var path = Path.Combine(_directory, "missing.bin");
        using var document = Pdf.Create();
        Assert.Throws<PdfProviderUnavailableException>(() => document.SaveFile(path));
        Assert.Throws<PdfProviderUnavailableException>(() => Pdf.OpenFile(path));
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

    public void Dispose()
    {
        // Only exact files in the unique directory created by this test are removed.
        foreach (var path in Directory.GetFiles(_directory)) File.Delete(path);
        Directory.Delete(_directory);
    }
}
