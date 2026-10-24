using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class ProcessingTests
{
    private static async Task Finish(Task task)
    {
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(20))));
        await task;
    }
    private static Task<PdfOperationReport> Submit(PdfProcessingPool pool, byte[] input, Action<PdfProcessingJob> callback, CancellationToken token = default)
    { Assert.True(pool.TrySubmit(input, callback, out var result, token)); return result!; }

    [Fact]
    public async Task BothLanguageConsumersExecuteRealAsyncReadEditWrite()
    {
        using var source = new MemoryStream();
        using (var document = Pdf.Create().AddPage()) document.Save(source);
        foreach (bool visualBasic in new[] { false, true })
        {
            using var output = new MemoryStream();
            var report = visualBasic ? await ContractExamples.VisualBasic.ContractExample.ProcessPdfAsync(source.ToArray(), output)
                : await ContractExamples.CSharp.ContractExample.ProcessPdfAsync(source.ToArray(), output);
            Assert.True(report.Succeeded);
            output.Position = 0;
            using var read = Pdf.Open(output);
            Assert.Equal(2, read.Pages.Count);
            var repository = new DirectoryInfo(AppContext.BaseDirectory);
            while (repository != null && !File.Exists(Path.Combine(repository.FullName, "Fluent.Pdf.slnx"))) repository = repository.Parent;
            var directory = Path.Combine(repository!.FullName, "artifacts", "contracts", "processing");
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, visualBasic ? "vb.pdf" : "csharp.pdf"), output.ToArray());
        }
    }

    [Fact]
    public async Task QueueAndResidentBytesBoundAdmissionAndCaptureInput()
    {
        using var pool = new PdfProcessingPool(new(1, 1, 8));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var first = Submit(pool, new byte[4], job => { entered.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(10))); });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        var bytes = new byte[] { 1, 2, 3, 4 };
        var second = Submit(pool, bytes, job => Assert.Equal(1, job.Input.ReadByte()));
        bytes[0] = 9;
        Assert.Equal(1, pool.Progress.Running);
        Assert.Equal(1, pool.Progress.Queued);
        Assert.Equal(8, pool.Progress.ResidentInputBytes);
        Assert.False(pool.TrySubmit(Array.Empty<byte>(), _ => { }, out var rejected));
        Assert.Null(rejected);
        release.Set();
        await Finish(Task.WhenAll(first, second));
        await Finish(pool.CompleteAsync());
        Assert.Equal(0, pool.Progress.ResidentInputBytes);
        Assert.Equal(2, pool.Progress.Succeeded);
        Assert.Throws<ObjectDisposedException>(() => pool.TrySubmit(Array.Empty<byte>(), _ => { }, out _));
    }

    [Fact]
    public async Task ByteCapacityRejectsBelowQueueLimitAndQueuedCancellationReleasesIt()
    {
        using var pool = new PdfProcessingPool(new(1, 4, 6));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var first = Submit(pool, new byte[3], _ => { entered.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(10))); });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        var second = Submit(pool, new byte[3], _ => throw new InvalidOperationException("Canceled queued callback ran."), cancellation.Token);
        Assert.False(pool.TrySubmit(new byte[1], _ => { }, out _));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        Assert.Equal(0, pool.Progress.Queued);
        Assert.Equal(3, pool.Progress.ResidentInputBytes);
        var third = Submit(pool, new byte[3], _ => { });
        release.Set();
        await Finish(Task.WhenAll(first, third));
        await Finish(pool.CompleteAsync());
        Assert.Equal(1, pool.Progress.Canceled);
    }

    [Fact]
    public async Task ShutdownCancelsQueuedAndActiveJobsButNeverReturnsBeforeCleanup()
    {
        using var pool = new PdfProcessingPool(new(1, 1));
        using var entered = new ManualResetEventSlim();
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cleanup = new ManualResetEventSlim();
        var first = Submit(pool, Array.Empty<byte>(), job =>
        {
            entered.Set();
            Assert.True(job.Context.CancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)));
            canceled.TrySetResult(true);
            Assert.True(cleanup.Wait(TimeSpan.FromSeconds(10)));
            job.Context.Checkpoint();
        });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        var queued = Submit(pool, Array.Empty<byte>(), _ => throw new InvalidOperationException());
        var completion = pool.ShutdownAsync();
        try
        {
            await Finish(canceled.Task);
            Assert.False(completion.IsCompleted);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        }
        finally { cleanup.Set(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Finish(completion);
        pool.Dispose();
        Assert.Equal(2, pool.Progress.Canceled);
        Assert.Equal(0, pool.Progress.Running);
    }

    [Fact]
    public async Task PrivateDocumentsRejectCrossThreadAccessAndExpireAfterCallback()
    {
        using var pool = new PdfProcessingPool(new(1));
        PdfDocument? escaped = null;
        Stream? input = null;
        PdfProcessingJob? escapedJob = null;
        var result = Submit(pool, new byte[] { 7 }, job =>
        {
            escaped = job.Document; input = job.Input; escapedJob = job;
            Exception? failure = null;
            var thread = new Thread(() => { try { escaped.AddPage(); } catch (Exception ex) { failure = ex; } });
            thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
            Assert.Equal("Processing.DocumentThread", Assert.IsType<PdfException>(failure).Diagnostic.Code);
            job.Document.AddPage();
        });
        await Finish(result);
        await Finish(pool.CompleteAsync());
        Assert.True(escaped!.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => input!.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => escapedJob!.ReadDocument());
    }

    [Fact]
    public async Task ExceptionsAndLatchedBudgetFailuresDoNotPoisonOtherJobs()
    {
        using var pool = new PdfProcessingPool(new(1), new(operationPolicy: new(limits: new(maximumObjects: 1))));
        var failure = Submit(pool, Array.Empty<byte>(), _ => throw new InvalidDataException("original"));
        Assert.Equal("original", (await Assert.ThrowsAsync<InvalidDataException>(() => failure)).Message);
        var budget = Submit(pool, Array.Empty<byte>(), job =>
        {
            job.Document.AddPage();
            Assert.Throws<PdfException>(() => job.Document.AddPage());
        });
        Assert.Equal("Limit.Objects", (await Assert.ThrowsAsync<PdfException>(() => budget)).Diagnostic.Code);
        await Finish(Submit(pool, Array.Empty<byte>(), job => job.Document.AddPage()));
        await Finish(pool.CompleteAsync());
        Assert.Equal(2, pool.Progress.Failed);
        Assert.Equal(1, pool.Progress.Succeeded);
        Assert.Equal(0, pool.Progress.ResidentInputBytes);
    }

    [Fact]
    public async Task ReadEditWriteUsesOneSharedContextAndPreservesStreams()
    {
        using var source = new MemoryStream();
        using (var doc = Pdf.Create()) { doc.AddPage(new PdfPageSize(123, 234)); doc.Save(source); }
        using var pool = new PdfProcessingPool(new(1));
        using var output = new MemoryStream();
        var task = Submit(pool, source.ToArray(), job =>
        {
            job.ReadDocument();
            Assert.Throws<InvalidOperationException>(job.ReadDocument);
            Assert.Equal(123, job.Document.Pages[0].Size.Width);
            job.Document.AddPage(new PdfPageSize(345, 456));
            job.Save(output);
            Assert.True(output.CanWrite);
        });
        var report = await task;
        await Finish(pool.CompleteAsync());
        Assert.True(report.Succeeded);
        Assert.True(report.Resources.Single(r => r.Resource == PdfResourceKind.InputBytes).Consumed > 0);
        Assert.True(report.Resources.Single(r => r.Resource == PdfResourceKind.OutputBytes).Consumed > 0);
        output.Position = 0;
        using var reopened = Pdf.Open(output);
        Assert.Equal(2, reopened.Pages.Count);
        Assert.Equal(345, reopened.Pages[1].Size.Width);
    }

    [Fact]
    public async Task FailedAtomicSavePreservesDestinationAndRemovesStagingFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "fluent-processing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "preserved.pdf");
        File.WriteAllText(path, "original");
        try
        {
            using var pool = new PdfProcessingPool(new(1), new(operationPolicy: new(limits: new(maximumOutputBytes: 1))));
            var work = Submit(pool, Array.Empty<byte>(), job => { job.Document.AddPage(); job.SaveFile(path); });
            Assert.Equal("Limit.OutputBytes", (await Assert.ThrowsAsync<PdfException>(() => work)).Diagnostic.Code);
            await Finish(pool.CompleteAsync());
            Assert.Equal("original", File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { File.Delete(path); Directory.Delete(directory); }
    }

    [Fact]
    public async Task CancellationRacesDoNotLeakQueueEntriesOrCompleteWorkTwice()
    {
        using var pool = new PdfProcessingPool(new(4, 32, 32));
        for (int batch = 0; batch < 10; batch++)
        {
            var pending = new List<Task>();
            using var source = new CancellationTokenSource();
            for (int i = 0; i < 32; i++)
                if (pool.TrySubmit(new byte[1], job => { job.Context.Checkpoint(); job.Document.AddPage(); }, out var task, source.Token)) pending.Add(task!);
            source.Cancel();
            try { await Task.WhenAll(pending); } catch (OperationCanceledException) { }
            Assert.All(pending, t => Assert.True(t.IsCompleted));
        }
        await Finish(pool.CompleteAsync());
        Assert.Equal(0, pool.Progress.Queued);
        Assert.Equal(0, pool.Progress.Running);
        Assert.Equal(0, pool.Progress.ResidentInputBytes);
        Assert.Equal(0, pool.Progress.Failed);
    }

    [Fact]
    public async Task SixtyFourIndependentWorkersExecuteTwoHundredMixedIterations()
    {
        using var pool = new PdfProcessingPool(new(64, 64), new(operationPolicy: new(limits: new(maximumElapsedTime: TimeSpan.FromMinutes(2)))));
        using var allEntered = new CountdownEvent(64);
        var identities = new ConcurrentDictionary<PdfDocument, byte>();
        var tasks = new List<Task>();
        for (int worker = 0; worker < 64; worker++)
        {
            int identity = worker;
            tasks.Add(Submit(pool, Array.Empty<byte>(), job =>
            {
                Assert.True(identities.TryAdd(job.Document, 0));
                allEntered.Signal();
                Assert.True(allEntered.Wait(TimeSpan.FromSeconds(20)));
                for (int iteration = 0; iteration < 200; iteration++)
                {
                    job.Context.Checkpoint();
                    job.Document.Pages.Clear();
                    job.Document.AddPage(new PdfPageSize(100 + identity, 200 + iteration));
                    using var output = new MemoryStream();
                    job.Save(output);
                    output.Position = 0;
                    using var read = Pdf.Open(output);
                    Assert.Equal(100 + identity, read.Pages[0].Size.Width);
                    Assert.Equal(200 + iteration, read.Pages[0].Size.Height);
                }
            }));
        }
        await Task.WhenAll(tasks);
        await Finish(pool.CompleteAsync());
        Assert.Equal(64, identities.Count);
        Assert.All(identities.Keys, doc => Assert.True(doc.IsDisposed));
        Assert.Equal(64, pool.Progress.Succeeded);
    }

    [Fact]
    public async Task InvalidLimitsAndAsyncVoidCallbacksRejectBeforeAdmission()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfProcessingOptions(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfProcessingOptions(65));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfProcessingOptions(maximumQueuedJobs: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfProcessingOptions(maximumResidentInputBytes: 0));
        using var pool = new PdfProcessingPool(new(1));
        Assert.Throws<ArgumentException>(() => pool.TrySubmit(Array.Empty<byte>(), async _ => await Task.Yield(), out _));
        Assert.Equal(0, pool.Progress.Queued);
        await Finish(pool.CompleteAsync());
    }

    [Fact]
    public async Task PolicyPreflightRejectsOversizeInputWithoutAdmission()
    {
        using var pool = new PdfProcessingPool(new(1), new(operationPolicy: new(limits: new(maximumInputBytes: 2))));
        Assert.Equal("Limit.InputBytes", Assert.Throws<PdfException>(() => pool.TrySubmit(new byte[3], _ => { }, out _)).Diagnostic.Code);
        Assert.Equal(0, pool.Progress.ResidentInputBytes);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => pool.TrySubmit(Array.Empty<byte>(), _ => { }, out _, canceled.Token));
        await Finish(pool.CompleteAsync());
    }

    [Fact]
    public async Task ScopedWorkerLeaksAndForeignCancellationCannotReportSuccess()
    {
        using var pool = new PdfProcessingPool(new(1));
        var leak = Submit(pool, Array.Empty<byte>(), job => job.Context.EnterScope(PdfResourceKind.Workers));
        Assert.Equal("Operation.ScopeLeaked", (await Assert.ThrowsAsync<PdfException>(() => leak)).Diagnostic.Code);
        var foreign = Submit(pool, Array.Empty<byte>(), _ => throw new OperationCanceledException("foreign"));
        await Assert.ThrowsAsync<OperationCanceledException>(() => foreign);
        Assert.True(foreign.IsFaulted);
        await Finish(pool.CompleteAsync());
        Assert.Equal(2, pool.Progress.Failed);
        Assert.Equal(0, pool.Progress.Canceled);
    }

    [Theory]
    [InlineData(PdfProcessingMode.Strict)]
    [InlineData(PdfProcessingMode.Permissive)]
    public async Task UnsupportedContentRetainsTheSharedStrictOrPermissivePolicy(PdfProcessingMode mode)
    {
        using var pool = new PdfProcessingPool(new(1), new(operationPolicy: new(mode)));
        var task = Submit(pool, Array.Empty<byte>(), job => job.Context.Report(new("Fixture.Unsupported", "Requested fixture content is unavailable.", lossCategory: PdfLossCategory.UnsupportedContent)));
        if (mode == PdfProcessingMode.Strict)
            Assert.Equal("Fixture.Unsupported", (await Assert.ThrowsAsync<PdfException>(() => task)).Diagnostic.Code);
        else
            Assert.Equal("Fixture.Unsupported", Assert.Single((await task).Diagnostics).Code);
        await Finish(pool.CompleteAsync());
    }
}
