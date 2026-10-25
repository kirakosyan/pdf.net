using System.Threading;
using System.Threading.Tasks;

namespace Fluent.Pdf;

/// <summary>A fixed-worker asynchronous boundary with bounded, nonblocking admission and privately owned documents.</summary>
/// <remarks>Callbacks execute synchronously on owned workers and must cooperate with their context. No arbitrary managed callback can be forcibly terminated safely. Provider instances and captured callback state remain caller-owned.</remarks>
public sealed class PdfProcessingPool : IDisposable
{
    private readonly object _gate = new();
    private readonly object _shutdownGate = new();
    private readonly LinkedList<Work> _queue = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly PdfOptions _documentOptions;
    private readonly PdfProviderRegistry _providers;
    private bool _accepting = true;
    private volatile bool _stopping;
    private int _running;
    private long _bytes, _succeeded, _failed, _canceled;

    /// <summary>Starts a fixed number of background workers. Registry providers must support independent concurrent operations or be separately instantiated per pool.</summary>
    public PdfProcessingPool(PdfProcessingOptions? options = null, PdfOptions? documentOptions = null, PdfProviderRegistry? providers = null)
    {
        Options = options ?? new PdfProcessingOptions();
        _documentOptions = documentOptions ?? PdfOptions.Default;
        _providers = providers ?? PdfProviderRegistry.Default;
        var workers = new List<Task>(Options.MaximumWorkers);
        try
        {
            for (int i = 0; i < Options.MaximumWorkers; i++)
                workers.Add(Task.Factory.StartNew(Worker, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default));
        }
        catch
        {
            lock (_gate) { _accepting = false; Monitor.PulseAll(_gate); }
            _ = Task.WhenAll(workers).ContinueWith(_ => _shutdown.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
        Completion = Task.WhenAll(workers);
        _ = Completion.ContinueWith(_ => { lock (_shutdownGate) _shutdown.Dispose(); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
    /// <summary>Gets immutable pool admission limits.</summary>
    public PdfProcessingOptions Options { get; }
    /// <summary>Gets completion of all owned workers after draining or shutdown. Completion never precedes active callback cleanup.</summary>
    public Task Completion { get; }
    /// <summary>Gets a synchronized bounded aggregate snapshot.</summary>
    public PdfProcessingProgress Progress { get { lock (_gate) return new(_queue.Count, _running, _bytes, _succeeded, _failed, _canceled); } }

    /// <summary>Copies input and admits work only when both queue and resident-byte capacity are available; otherwise returns false without copying or creating a waiter.</summary>
    /// <remarks>The caller must not mutate input during this call. On false, completion is null; retry after an admitted task completes. A stopped pool rejects admission. Exceptions from the callback are retained by its task.</remarks>
    public bool TrySubmit(byte[] input, Action<PdfProcessingJob> action, out Task<PdfOperationReport>? completion, CancellationToken cancellationToken = default)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (Attribute.IsDefined(action.Method, typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute)))
            throw new ArgumentException("The worker callback must be synchronous; async-void callbacks cannot own a document lifetime.", nameof(action));
        cancellationToken.ThrowIfCancellationRequested();
        if (input.LongLength > _documentOptions.OperationPolicy.Limits.MaximumInputBytes)
            throw new PdfException(new PdfDiagnostic("Limit.InputBytes", "The job input exceeds its operation policy before admission."));
        if (input.LongLength > _documentOptions.OperationPolicy.Limits.MaximumExpandedBytes)
            throw new PdfException(new PdfDiagnostic("Limit.ExpandedBytes", "The input copy exceeds its operation policy before admission."));
        Work work;
        lock (_gate)
        {
            if (!_accepting) throw new ObjectDisposedException(nameof(PdfProcessingPool));
            if (_queue.Count >= Options.MaximumQueuedJobs || input.LongLength > Options.MaximumResidentInputBytes - _bytes)
            { completion = null; return false; }
            var copy = new byte[input.Length];
            Buffer.BlockCopy(input, 0, copy, 0, input.Length);
            work = new Work(copy, action, cancellationToken);
            work.Node = _queue.AddLast(work);
            _bytes += copy.LongLength;
            completion = work.Source.Task;
            Monitor.Pulse(_gate);
        }
        var registration = cancellationToken.Register(() => CancelQueued(work));
        bool dispose;
        lock (_gate) { dispose = work.Finished; if (!dispose) work.Registration = registration; }
        if (dispose) registration.Dispose();
        return true;
    }
    /// <summary>Stops admission and drains existing work without canceling it.</summary>
    public Task CompleteAsync()
    {
        lock (_gate) { _accepting = false; Monitor.PulseAll(_gate); }
        return Completion;
    }
    /// <summary>Stops admission, cancels waiting and running jobs, and returns completion of all callback/provider cleanup.</summary>
    public Task ShutdownAsync() { Dispose(); return Completion; }
    /// <summary>Requests cancellation without blocking the calling thread. Await Completion to observe finished cleanup.</summary>
    public void Dispose()
    {
        Work[] queued;
        lock (_gate)
        {
            if (_stopping) return;
            _accepting = false; _stopping = true;
            queued = _queue.ToArray();
            foreach (var work in queued) FinishLocked(work, true, false);
            Monitor.PulseAll(_gate);
        }
        foreach (var work in queued) { work.Source.TrySetCanceled(); work.Registration.Dispose(); }
        lock (_shutdownGate) { if (!Completion.IsCompleted) _shutdown.Cancel(); }
    }
    private void CancelQueued(Work work)
    {
        lock (_gate)
        {
            if (work.Node?.List == null) return;
            FinishLocked(work, true, false);
        }
        work.Source.TrySetCanceled();
        work.Registration.Dispose();
    }
    private void Worker()
    {
        while (true)
        {
            Work work;
            lock (_gate)
            {
                while (_queue.Count == 0 && _accepting) Monitor.Wait(_gate);
                if (_queue.Count == 0) return;
                work = _queue.First!.Value;
                _queue.RemoveFirst(); work.Node = null;
                _running++;
            }
            PdfOperationReport? report = null;
            Exception? failure = null;
            bool canceled = false;
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(work.Token, _shutdown.Token);
                report = PdfFont.Run("ProcessDocument", "managed.processing", _documentOptions.OperationPolicy, linked.Token, context =>
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, work.Input.LongLength);
                    var job = new PdfProcessingJob(work.Input, _documentOptions, _providers, context);
                    try { work.Action!(job); context.Checkpoint(); }
                    finally { job.Close(); }
                }, PdfCapability.WriteDocument);
            }
            catch (OperationCanceledException ex) { canceled = work.Token.IsCancellationRequested || _stopping; if (!canceled) failure = ex; }
            catch (Exception ex) { failure = ex; }
            lock (_gate) { _running--; FinishLocked(work, canceled, failure != null); }
            work.Registration.Dispose();
            if (canceled) work.Source.TrySetCanceled();
            else if (failure != null) work.Source.TrySetException(failure);
            else work.Source.TrySetResult(report!);
        }
    }
    private void FinishLocked(Work work, bool canceled, bool failed)
    {
        if (work.Node?.List != null) _queue.Remove(work.Node);
        work.Node = null; work.Finished = true;
        _bytes -= work.Input.LongLength;
        work.Input = Array.Empty<byte>(); work.Action = null;
        if (canceled) _canceled++; else if (failed) _failed++; else _succeeded++;
    }
    private sealed class Work
    {
        internal Work(byte[] input, Action<PdfProcessingJob> action, CancellationToken token) { Input = input; Action = action; Token = token; }
        internal byte[] Input;
        internal Action<PdfProcessingJob>? Action;
        internal readonly CancellationToken Token;
        internal readonly TaskCompletionSource<PdfOperationReport> Source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal LinkedListNode<Work>? Node;
        internal CancellationTokenRegistration Registration;
        internal bool Finished;
    }
}
