using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Fluent.Pdf.Contract.Tests;
public sealed class PhaseDeadlineTests
{
    private readonly ITestOutputHelper _output;
    public PhaseDeadlineTests(ITestOutputHelper output) => _output = output;
    private static PdfOperationReport Run(Action<PdfOperationContext> action, PdfResourceLimits? limits = null) => PdfFont.Run("OriginalPhaseDeadline", "original.fixture", new(limits: limits), default, action);
    private static void Mock(Action<PdfOperationContext, Action<TimeSpan>> action)
    {
        TimeSpan elapsed = TimeSpan.Zero;
        var context = new PdfOperationContext("OriginalPhaseDeadline", "original.fixture", PdfCapability.RenderHtml, new(limits: new(maximumElapsedTime: TimeSpan.FromMinutes(10))), default, () => elapsed);
        try
        {
            action(context, time => elapsed = time);
        }
        finally
        {
            context.Finish(false);
            context.DisposeSources();
        }
    }

    [Fact]
    public void NestedPhasesKeepTheEarliestDeadlineAndDisposalRestoresOnlyRemainingTime()
    {
        Mock((c, set) =>
        {
            using (c.EnterTimeLimit(TimeSpan.FromSeconds(10)))
            {
                set(TimeSpan.FromSeconds(2));
                using (c.EnterTimeLimit(TimeSpan.FromSeconds(2)))
                {
                    set(TimeSpan.FromSeconds(3));
                    c.Checkpoint();
                }

                set(TimeSpan.FromSeconds(9));
                c.Checkpoint();
            }

            set(TimeSpan.FromMinutes(9));
            c.Checkpoint();
            set(TimeSpan.FromMinutes(10));
            Assert.Equal("Limit.ElapsedTime", Assert.Throws<PdfException>(c.Checkpoint).Diagnostic.Code);
        });
    }

    [Fact]
    public void ALongInnerPhaseCannotExtendTheOuterPhase()
    {
        Mock((c, set) =>
        {
            using var outer = c.EnterTimeLimit(TimeSpan.FromSeconds(2));
            using var inner = c.EnterTimeLimit(TimeSpan.FromMinutes(1));
            set(TimeSpan.FromSeconds(2));
            Assert.Equal("Limit.ElapsedTime", Assert.Throws<PdfException>(c.Checkpoint).Diagnostic.Code);
        });
    }

    [Fact]
    public void DisposingAnExpiredPhaseLatchesItsFailureBeforeAnotherCheckpoint()
    {
        Mock((c, set) =>
        {
            var phase = c.EnterTimeLimit(TimeSpan.FromSeconds(2));
            set(TimeSpan.FromSeconds(2));
            phase.Dispose();
            var first = Assert.Throws<PdfException>(c.Checkpoint);
            Assert.Equal("Limit.ElapsedTime", first.Diagnostic.Code);
            phase.Dispose();
            Assert.Same(first, Assert.Throws<PdfException>(c.Checkpoint));
        });
    }

    [Fact]
    public void PhaseCleanupNeverReplacesAnExistingFatalFailure()
    {
        Mock((c, set) =>
        {
            var phase = c.EnterTimeLimit(TimeSpan.FromSeconds(2));
            var original = Assert.Throws<PdfException>(() => c.Report(new("Original.Failure", "Original bounded failure.")));
            set(TimeSpan.FromSeconds(3));
            phase.Dispose();
            Assert.Same(original, Assert.Throws<PdfException>(c.Checkpoint));
        });
    }

    [Fact]
    public async Task PhaseExpiryCancelsTheOriginalSharedTokenAndTerminatesItsAwaitedWork()
    {
        var context = new PdfOperationContext("OriginalPhaseDeadline", "original.fixture", PdfCapability.RenderHtml, new(limits: new(maximumElapsedTime: TimeSpan.FromSeconds(5))), default);
        var token = context.CancellationToken;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var signaled = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
            var canceledState = new TaskCompletionSource<(long Ticks, bool Canceled)>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task? work = null;
            // Cancellation callbacks run synchronously in LIFO order. Register this observer before
            // Task.Delay so it samples the promise state after the delay's cancellation callback.
            // A continuation can be queued despite ExecuteSynchronously when the promise forces async continuations.
            using var completion = token.Register(() => canceledState.TrySetResult((clock.ElapsedTicks, work != null && work.IsCanceled)));
            work = Task.Delay(Timeout.Infinite, token);
            var continuationObserved = work.ContinueWith(_ => clock.ElapsedTicks, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            using var signal = token.Register(() => signaled.TrySetResult(clock.ElapsedTicks));
            clock.Restart();
            using var phase = context.EnterTimeLimit(TimeSpan.FromMilliseconds(50));
            await Assert.ThrowsAsync<TaskCanceledException>(() => work);
            var actualCompletion = await canceledState.Task;
            long completedTicks = actualCompletion.Ticks;
            long continuationTicks = await continuationObserved;
            long signaledTicks = await signaled.Task;
            var signalElapsed = TimeSpan.FromSeconds(signaledTicks / (double)System.Diagnostics.Stopwatch.Frequency);
            var completionElapsed = TimeSpan.FromSeconds(completedTicks / (double)System.Diagnostics.Stopwatch.Frequency);
            _output.WriteLine("Phase signal={0:F3}ms; canceled-task state={1:F3}ms; task-continuation observation={2:F3}ms; test-continuation observation={3:F3}ms.", signalElapsed.TotalMilliseconds, completionElapsed.TotalMilliseconds, continuationTicks * 1000d / System.Diagnostics.Stopwatch.Frequency, clock.Elapsed.TotalMilliseconds);
            Assert.True(actualCompletion.Canceled);
            Assert.Equal(token, context.CancellationToken);
            Assert.True(signaledTicks >= 0 && signalElapsed < TimeSpan.FromSeconds(1));
            Assert.True(completedTicks >= 0 && completionElapsed < TimeSpan.FromSeconds(1));
            Assert.True(work.IsCanceled);
            Assert.Equal("Limit.ElapsedTime", Assert.Throws<PdfException>(context.Checkpoint).Diagnostic.Code);
        }
        finally
        {
            context.Finish(false);
            context.DisposeSources();
        }
    }

    [Fact]
    public async Task DisposingAnUnexpiredPhaseRemovesItsTimerWithoutExtendingTheOriginalDeadline()
    {
        var context = new PdfOperationContext("OriginalPhaseDeadline", "original.fixture", PdfCapability.RenderHtml, new(limits: new(maximumElapsedTime: TimeSpan.FromSeconds(5))), default);
        try
        {
            var phase = context.EnterTimeLimit(TimeSpan.FromSeconds(1));
            phase.Dispose();
            phase.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(1100));
            Assert.False(context.CancellationToken.IsCancellationRequested);
            context.Checkpoint();
            context.EnsureComplete();
        }
        finally
        {
            context.Finish(false);
            context.DisposeSources();
        }
    }

    [Fact]
    public void TimeScopeLeaksAndInventoryOverflowRejectWhileDisposalIsIdempotent()
    {
        Assert.Equal("Operation.ScopeLeaked", Assert.Throws<PdfException>(() => Run(c => c.EnterTimeLimit(TimeSpan.FromSeconds(10)))).Diagnostic.Code);
        var failure = Assert.Throws<PdfException>(() => Run(c =>
        {
            var scopes = new List<IDisposable>();
            try
            {
                for (int i = 0; i < 129; i++)
                    scopes.Add(c.EnterTimeLimit(TimeSpan.FromSeconds(10)));
            }
            finally
            {
                foreach (var scope in scopes)
                {
                    scope.Dispose();
                    scope.Dispose();
                }
            }
        }));
        Assert.Equal("Limit.TimeScopes", failure.Diagnostic.Code);
    }

    [Fact]
    public void ScopeAllocationHonorsTheSharedMemoryAndObjectBudgets()
    {
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => Run(c => c.EnterTimeLimit(TimeSpan.FromSeconds(1)), new(maximumExpandedBytes: 255))).Diagnostic.Code);
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => Run(c =>
        {
            using var first = c.EnterTimeLimit(TimeSpan.FromSeconds(1));
            c.EnterTimeLimit(TimeSpan.FromSeconds(1));
        }, new(maximumObjects: 1))).Diagnostic.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(86400001)]
    public void InvalidDurationsRejectWithoutAllocatingAScope(double milliseconds)
    {
        Assert.True(Run(c =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => c.EnterTimeLimit(TimeSpan.FromMilliseconds(milliseconds)));
        }).Succeeded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RootAndPhaseCancellationRunOutsideThePoolAndTheObserverIsJoined(bool phaseDeadline)
    {
        var context = new PdfOperationContext("IndependentDeadlineObserver", "original.fixture", PdfCapability.RenderHtml, new(limits: new(maximumElapsedTime: phaseDeadline ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(50))), default);
        using var signal = new ManualResetEventSlim();
        Thread? observer = null;
        bool pooled = true;
        IDisposable? phase = null;
        try
        {
            var token = context.CancellationToken;
            using var registration = token.Register(() =>
            {
                observer = Thread.CurrentThread;
                pooled = observer.IsThreadPoolThread;
                signal.Set();
            });
            if (phaseDeadline)
                phase = context.EnterTimeLimit(TimeSpan.FromMilliseconds(50));
            Assert.True(signal.Wait(TimeSpan.FromSeconds(1)));
            Assert.False(pooled);
            Assert.True(token.IsCancellationRequested);
            Assert.Equal(token, context.CancellationToken);
            Assert.Equal("Limit.ElapsedTime", Assert.Throws<PdfException>(context.Checkpoint).Diagnostic.Code);
        }
        finally
        {
            phase?.Dispose();
            context.Finish(false);
            context.DisposeSources();
        }

        Assert.NotNull(observer);
        Assert.False(observer!.IsAlive);
    }
}
