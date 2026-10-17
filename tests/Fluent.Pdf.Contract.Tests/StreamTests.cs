using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class StreamTests
{
    [Fact]
    public void MissingProvidersFailBeforeChangingCallerStreams()
    {
        using var input = new MemoryStream(new byte[] { 2 });
        using var output = new MemoryStream();
        using var document = Pdf.Create().AddPage();
        Assert.Throws<PdfProviderUnavailableException>(() => Pdf.Open(input));
        Assert.Throws<PdfProviderUnavailableException>(() => document.Save(output));
        Assert.Equal(0, input.Position);
        Assert.Equal(0, output.Length);
        Assert.True(input.CanRead);
        Assert.True(output.CanWrite);
    }

    [Fact]
    public void PreCancellationDoesNotInvokeProvidersOrTouchStreams()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        var provider = new ContractProvider
        {
            OnRead = (_, _, _) => throw new Exception("Should not run."),
            OnWrite = (_, _, _) => throw new Exception("Should not run.")
        };
        var registry = new PdfProviderRegistry(provider);
        using var input = new MemoryStream(new byte[] { 1 });
        using var output = new MemoryStream();
        using var document = Pdf.Create(providers: registry);
        Assert.Throws<OperationCanceledException>(() => Pdf.Open(input, providers: registry, cancellationToken: cancel.Token));
        Assert.Throws<OperationCanceledException>(() => document.Save(output, cancel.Token));
        Assert.Throws<OperationCanceledException>(() => document.SaveFile("not-created.pdf", cancel.Token));
        Assert.Equal(0, input.Position);
        Assert.Empty(output.ToArray());
    }

    [Fact]
    public void ProviderLeasesAreDirectionalExpireAndNeverCloseCallerStreams()
    {
        Stream? readLease = null;
        Stream? writeLease = null;
        var provider = new ContractProvider
        {
            OnRead = (stream, target, _) =>
            {
                readLease = stream;
                Assert.False(stream.CanWrite);
                Assert.Throws<NotSupportedException>(() => stream.WriteByte(1));
                Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
                Assert.Equal(1, stream.ReadByte());
                target.AddPage();
                stream.Dispose();
            },
            OnWrite = (snapshot, stream, _) =>
            {
                writeLease = stream;
                Assert.Single(snapshot.Pages);
                Assert.False(stream.CanRead);
                Assert.Throws<NotSupportedException>(() => stream.ReadByte());
                stream.WriteByte(9);
                stream.Dispose();
            }
        };
        using var input = new MemoryStream(new byte[] { 1 });
        using var output = new MemoryStream();
        using var document = Pdf.Open(input, providers: new PdfProviderRegistry(provider));
        document.Save(output);
        Assert.True(input.CanRead);
        Assert.True(output.CanWrite);
        Assert.Equal(new byte[] { 9 }, output.ToArray());
        Assert.NotNull(readLease);
        Assert.NotNull(writeLease);
        Assert.Throws<ObjectDisposedException>(() => readLease!.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => writeLease!.WriteByte(3));
        document.Dispose();
        Assert.True(input.CanRead);
        Assert.True(output.CanWrite);
    }

    [Fact]
    public void ReadFailureDisposesPartialModelAndPropagatesOriginalFailure()
    {
        PdfDocument? partial = null;
        var original = new InvalidDataException("Fixture failure.");
        var provider = new ContractProvider { OnRead = (_, document, _) => { partial = document; document.AddPage(); throw original; } };
        using var input = new MemoryStream(new byte[] { 2 });
        var failure = Assert.Throws<InvalidDataException>(() => Pdf.Open(input, providers: new PdfProviderRegistry(provider)));
        Assert.Same(original, failure);
        Assert.True(partial!.IsDisposed);
        Assert.True(input.CanRead);
    }

    [Fact]
    public void ReadCannotExceedTheConfiguredPageBudget()
    {
        using var input = new MemoryStream(new byte[] { 3 });
        var failure = Assert.Throws<PdfException>(() => Pdf.Open(input, new PdfOptions(maximumPages: 2), new PdfProviderRegistry(new ContractProvider())));
        Assert.Equal("Limit.Pages", failure.Diagnostic.Code);
        Assert.True(input.CanRead);
    }

    [Fact]
    public void PostCallbackCancellationIsObservedAndOwnedReadStateIsCleaned()
    {
        using var cancel = new CancellationTokenSource();
        PdfDocument? partial = null;
        var provider = new ContractProvider { OnRead = (_, document, token) => { Assert.Equal(cancel.Token, token); partial = document; document.AddPage(); cancel.Cancel(); } };
        using var input = new MemoryStream(new byte[] { 1 });
        Assert.Throws<OperationCanceledException>(() => Pdf.Open(input, providers: new PdfProviderRegistry(provider), cancellationToken: cancel.Token));
        Assert.True(partial!.IsDisposed);
        Assert.True(input.CanRead);
    }

    [Fact]
    public void NonSeekableStreamsAreSupportedOnlyWhenTheProviderDeclaresSupport()
    {
        var provider = new ContractProvider();
        using var input = new NonSeekableStream(new byte[] { 2 });
        using var output = new NonSeekableStream();
        using var document = Pdf.Open(input, providers: new PdfProviderRegistry(provider));
        document.Save(output);
        Assert.Equal("contract:2", Encoding.UTF8.GetString(output.Bytes));
        provider.RequiresSeekableStream = true;
        var writeFailure = Assert.Throws<PdfException>(() => document.Save(output));
        Assert.Equal("Stream.SeekRequired", writeFailure.Diagnostic.Code);
        using var rejected = new NonSeekableStream(new byte[] { 1 });
        var readFailure = Assert.Throws<PdfException>(() => Pdf.Open(rejected, providers: new PdfProviderRegistry(provider)));
        Assert.Equal("Stream.SeekRequired", readFailure.Diagnostic.Code);
        Assert.Equal(1, rejected.ReadByte());
    }

    [Fact]
    public void StreamSavesPreservePositionOwnershipAndPartialFailureSemantics()
    {
        var provider = new ContractProvider { OnWrite = (_, stream, _) => { stream.WriteByte(7); throw new InvalidDataException("Write fixture failure."); } };
        using var output = new MemoryStream();
        output.WriteByte(1);
        using var document = Pdf.Create(providers: new PdfProviderRegistry(provider)).AddPage();
        Assert.Throws<InvalidDataException>(() => document.Save(output));
        Assert.Equal(new byte[] { 1, 7 }, output.ToArray());
        Assert.True(output.CanWrite);
        provider.OnWrite = null;
        document.Save(output);
        Assert.Equal("contract:1", Encoding.UTF8.GetString(output.ToArray(), 2, (int)output.Length - 2));
    }

    [Fact]
    public void WriterReceivesAnImmutableSnapshotAndCannotReenterOrDisposeTheSession()
    {
        PdfDocument? document = null;
        PdfDocumentSnapshot? retained = null;
        var provider = new ContractProvider
        {
            OnWrite = (snapshot, stream, _) =>
            {
                retained = snapshot;
                Assert.Throws<InvalidOperationException>(() => document!.Pages.Add());
                Assert.Throws<InvalidOperationException>(() => document!.Pages[0].Size = PdfPageSize.A4);
                Assert.Throws<InvalidOperationException>(() => document!.Dispose());
                var failure = Assert.Throws<PdfException>(() => document!.Save(stream));
                Assert.Equal("Operation.Reentrant", failure.Diagnostic.Code);
                var missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "nested.bin");
                var fileFailure = Assert.Throws<PdfException>(() => document!.SaveFile(missingDirectory));
                Assert.Equal("Operation.Reentrant", fileFailure.Diagnostic.Code);
            }
        };
        document = Pdf.Create(providers: new PdfProviderRegistry(provider)).AddPage(PdfPageSize.Letter);
        using var output = new MemoryStream();
        document.Save(output);
        document.Dispose();
        Assert.Equal(PdfPageSize.Letter, retained!.Pages[0].Size);
    }

    [Fact]
    public void IndependentDocumentsDoNotShareMutableStateAcross64Workers()
    {
        var registry = new PdfProviderRegistry(new ContractProvider());
        Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 64 }, worker =>
        {
            for (int iteration = 0; iteration < 200; iteration++)
            {
                using var input = new MemoryStream(new byte[] { 2 });
                using var document = Pdf.Open(input, providers: registry);
                document.Pages[0].Size = new PdfPageSize(worker + 1, iteration + 1);
                using var output = new MemoryStream();
                document.Save(output);
                Assert.Equal("contract:2", Encoding.UTF8.GetString(output.ToArray()));
                Assert.Equal(worker + 1, document.Pages[0].Size.Width);
            }
        });
    }
}
