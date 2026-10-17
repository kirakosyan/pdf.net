using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class PolicyIoTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    public void InputBudgetHandlesBelowAtAndAboveLimitIncludingExactLimitEof(bool seekable, int length)
    {
        using Stream input = seekable ? new MemoryStream(new byte[length]) : new NonSeekableStream(new byte[length]);
        PdfDocument? partial = null;
        var provider = new ContractProvider { OnReadContext = (stream, target, _) =>
        {
            partial = target;
            var buffer = new byte[16];
            while (stream.Read(buffer, 0, buffer.Length) != 0) { }
            target.AddPage();
        } };
        var options = Options(new PdfResourceLimits(maximumInputBytes: 2));
        if (length > 2)
        {
            var error = Assert.Throws<PdfException>(() => Pdf.Open(input, options, new PdfProviderRegistry(provider)));
            Assert.Equal("Limit.InputBytes", error.Diagnostic.Code);
            if (seekable) { Assert.Null(partial); Assert.Equal(0, input.Position); }
            else { Assert.True(partial!.IsDisposed); Assert.False(partial.LastOperationReport!.Succeeded); }
        }
        else
        {
            using var document = Pdf.Open(input, options, new PdfProviderRegistry(provider));
            Assert.Single(document.Pages);
            Assert.Equal(length, document.LastOperationReport!.Resources.Single(item => item.Resource == PdfResourceKind.InputBytes).Consumed);
        }
        Assert.True(input.CanRead);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void OutputBudgetRejectsExcessBeforeWriting(int length)
    {
        var provider = new ContractProvider { OnWriteContext = (_, stream, _) => stream.Write(new byte[length], 0, length) };
        using var document = Pdf.Create(Options(new PdfResourceLimits(maximumOutputBytes: 2)), new PdfProviderRegistry(provider));
        using var output = new MemoryStream();
        if (length > 2)
        {
            var error = Assert.Throws<PdfException>(() => document.Save(output));
            Assert.Equal("Limit.OutputBytes", error.Diagnostic.Code);
            Assert.Equal(0, output.Length);
            Assert.Equal(error.Diagnostic.Code, document.LastOperationReport!.Failure!.Code);
            Assert.False(document.LastOperationReport.Succeeded);
        }
        else
        {
            var report = document.Save(output);
            Assert.Equal(length, output.Length);
            Assert.Equal(length, report.Resources.Single(item => item.Resource == PdfResourceKind.OutputBytes).Consumed);
        }
        Assert.True(output.CanWrite);
    }

    [Theory]
    [InlineData("position")]
    [InlineData("seek")]
    [InlineData("length")]
    [InlineData("rewrites")]
    [InlineData("overflow")]
    [InlineData("prefix")]
    public void SeekingGrowingAndRepeatedWritesCannotBypassOutputLimits(string operation)
    {
        var provider = new ContractProvider { OnWriteContext = (_, stream, _) =>
        {
            try
            {
                switch (operation)
                {
                    case "position": stream.Position = 4; break;
                    case "seek": stream.Seek(4, SeekOrigin.Begin); break;
                    case "length": stream.SetLength(4); break;
                    case "rewrites": stream.Write(new byte[2], 0, 2); stream.Seek(1, SeekOrigin.Begin); stream.WriteByte(1); break;
                    case "overflow": stream.Seek(long.MaxValue, SeekOrigin.Current); break;
                    case "prefix": stream.Position = 0; break;
                }
            }
            catch (PdfException) { }
        } };
        using var document = Pdf.Create(Options(new PdfResourceLimits(maximumOutputBytes: 2)), new PdfProviderRegistry(provider));
        using var output = new MemoryStream();
        output.WriteByte(7);
        Assert.Equal("Limit.OutputBytes", Assert.Throws<PdfException>(() => document.Save(output)).Diagnostic.Code);
        Assert.Equal(7, output.ToArray()[0]);
        Assert.InRange(output.Length, 1, 3);
    }

    [Fact]
    public void SetLengthChargesGrowthAndDoesNotExpandPastTheLimit()
    {
        var provider = new ContractProvider { OnWriteContext = (_, stream, _) => stream.SetLength(2) };
        using var document = Pdf.Create(Options(new PdfResourceLimits(maximumOutputBytes: 2)), new PdfProviderRegistry(provider));
        using var output = new MemoryStream();
        var report = document.Save(output);
        Assert.Equal(2, output.Length);
        Assert.Equal(2, report.Resources.Single(item => item.Resource == PdfResourceKind.OutputBytes).Consumed);
    }

    [Fact]
    public void NonSeekableInputLookaheadIsBoundedAndNeverDeliveredOnFailure()
    {
        using var input = new CountingInput(new byte[100]);
        var provider = new ContractProvider { OnReadContext = (stream, _, _) =>
        {
            Assert.Equal(2, stream.Read(new byte[1024], 0, 1024));
            Assert.Throws<PdfException>(() => stream.ReadByte());
            Assert.Throws<PdfException>(() => stream.ReadByte());
        } };
        Assert.Equal("Limit.InputBytes", Assert.Throws<PdfException>(() => Pdf.Open(input,
            Options(new PdfResourceLimits(maximumInputBytes: 2)), new PdfProviderRegistry(provider))).Diagnostic.Code);
        Assert.Equal(3, input.BytesRead);
        Assert.True(input.CanRead);
    }

    [Fact]
    public void SwallowingThePageLimitDuringReadCannotReturnATruncatedSuccess()
    {
        PdfDocument? partial = null;
        var provider = new ContractProvider { OnReadContext = (_, target, _) =>
        {
            partial = target;
            target.AddPage();
            try { target.AddPage(); }
            catch (PdfException) { }
        } };
        using var input = new MemoryStream(new byte[1]);
        Assert.Equal("Limit.Pages", Assert.Throws<PdfException>(() => Pdf.Open(input, new PdfOptions(maximumPages: 1), new PdfProviderRegistry(provider))).Diagnostic.Code);
        Assert.True(partial!.IsDisposed);
    }

    [Fact]
    public void ReadCancellationCannotBeSuppressedByTheProvider()
    {
        using var cancellation = new CancellationTokenSource();
        PdfDocument? partial = null;
        var provider = new ContractProvider { OnReadContext = (_, target, context) =>
        {
            partial = target;
            target.AddPage();
            cancellation.Cancel();
            var response = System.Diagnostics.Stopwatch.StartNew();
            Assert.Throws<OperationCanceledException>(context.Checkpoint);
            Assert.True(response.Elapsed < TimeSpan.FromSeconds(1), "A cooperative checkpoint must respond within one second.");
        } };
        using var input = new MemoryStream(new byte[1]);
        var error = Assert.Throws<OperationCanceledException>(() => Pdf.Open(input, providers: new PdfProviderRegistry(provider), cancellationToken: cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.True(partial!.IsDisposed);
        Assert.True(input.CanRead);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(1048576)]
    public void RealDecompressionUsesTheExpandedBudgetAndPreservesDecoderOwnership(int expandedLength)
    {
        using var encoded = new MemoryStream();
        using (var compressor = new DeflateStream(encoded, CompressionMode.Compress, true))
        {
            var payload = new byte[expandedLength];
            compressor.Write(payload, 0, payload.Length);
        }
        encoded.Position = 0;
        Stream? retained = null;
        var provider = new ContractProvider { OnReadContext = (input, _, context) =>
        {
            using var decoder = new DeflateStream(input, CompressionMode.Decompress, true);
            using (var expanded = context.BorrowExpandedInput(decoder))
            {
                retained = expanded;
                expanded.CopyTo(Stream.Null);
            }
            Assert.True(decoder.CanRead);
        } };
        var options = Options(new PdfResourceLimits(maximumExpandedBytes: 2));
        if (expandedLength > 2)
            Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => Pdf.Open(encoded, options, new PdfProviderRegistry(provider))).Diagnostic.Code);
        else
        {
            using var document = Pdf.Open(encoded, options, new PdfProviderRegistry(provider));
            Assert.Equal(expandedLength, document.LastOperationReport!.Resources.Single(item => item.Resource == PdfResourceKind.ExpandedBytes).Consumed);
        }
        Assert.True(encoded.CanRead);
        Assert.False(retained!.CanRead);
        Assert.Throws<ObjectDisposedException>(() => retained.ReadByte());
    }

    [Fact]
    public void ADecoderLeaseExpiresAtOperationEndWithoutTakingTheDecoderOwnership()
    {
        using var decoder = new MemoryStream(new byte[1]);
        Stream? lease = null;
        PolicyTests.Run(context => { lease = context.BorrowExpandedInput(decoder); Assert.Equal(0, lease.ReadByte()); });
        Assert.True(decoder.CanRead);
        Assert.False(lease!.CanRead);
        Assert.Throws<ObjectDisposedException>(() => lease.ReadByte());
        lease.Dispose();
        Assert.True(decoder.CanRead);
    }

    [Fact]
    public void ParallelDecoderReadsShareReservationsAndRefundUnusedCapacity()
    {
        var report = PolicyTests.Run(context =>
        {
            System.Threading.Tasks.Parallel.For(0, 64, _ =>
            {
                using var worker = context.EnterScope(PdfResourceKind.Workers);
                using var decoder = new MemoryStream(new byte[1]);
                using var view = context.BorrowExpandedInput(decoder);
                var buffer = new byte[16];
                Assert.Equal(1, view.Read(buffer, 0, buffer.Length));
                Assert.Equal(0, view.Read(buffer, 0, buffer.Length));
            });
        }, new PdfResourceLimits(maximumExpandedBytes: 64, maximumWorkers: 64));
        Assert.Equal(64, report.Resources.Single(item => item.Resource == PdfResourceKind.ExpandedBytes).Consumed);
    }

    [Fact]
    public void ParallelDecodersCannotEachConsumeTheirOwnOverBudgetProbe()
    {
        int bytesRead = 0;
        var error = Assert.Throws<PdfException>(() => PolicyTests.Run(context =>
        {
            using var start = new ManualResetEventSlim();
            var workers = Enumerable.Range(0, 8).Select(_ => System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                using var worker = context.EnterScope(PdfResourceKind.Workers);
                using var decoder = new SlowInput(count => Interlocked.Add(ref bytesRead, count));
                using var view = context.BorrowExpandedInput(decoder);
                start.Wait();
                var buffer = new byte[16];
                while (view.Read(buffer, 0, buffer.Length) != 0) { }
            }, CancellationToken.None, System.Threading.Tasks.TaskCreationOptions.LongRunning,
                System.Threading.Tasks.TaskScheduler.Default)).ToArray();
            start.Set();
            System.Threading.Tasks.Task.WaitAll(workers);
        }, new PdfResourceLimits(maximumExpandedBytes: 1, maximumWorkers: 8)));
        Assert.Equal("Limit.ExpandedBytes", error.Diagnostic.Code);
        Assert.Equal(2, bytesRead); // One accepted byte and one shared rejection probe, across all eight decoders.
    }

    internal static PdfOptions Options(PdfResourceLimits limits, PdfProcessingMode mode = PdfProcessingMode.Strict) =>
        new(operationPolicy: new PdfOperationPolicy(mode, limits));

    private sealed class CountingInput : MemoryStream
    {
        internal CountingInput(byte[] bytes) : base(bytes) { }
        internal int BytesRead { get; private set; }
        public override bool CanSeek => false;
        public override int Read(byte[] buffer, int offset, int count)
        {
            int result = base.Read(buffer, offset, count);
            BytesRead += result;
            return result;
        }
    }

    private sealed class SlowInput : MemoryStream
    {
        private readonly Action<int> _observed;
        internal SlowInput(Action<int> observed) : base(new byte[100]) => _observed = observed;
        public override bool CanSeek => false;
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = base.Read(buffer, offset, count);
            _observed(read);
            Thread.Sleep(10); // Hold real I/O in flight long enough to exercise shared reservations.
            return read;
        }
    }
}
