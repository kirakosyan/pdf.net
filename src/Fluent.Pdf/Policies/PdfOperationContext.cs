using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Fluent.Pdf;

/// <summary>A synchronized per-operation policy, diagnostic and budget boundary, owned by the SDK.</summary>
/// <remarks>Providers must charge resources before allocating, poll checkpoints, and finish all workers/scopes before returning.</remarks>
public sealed class PdfOperationContext
{
    private readonly object _gate = new();
    private readonly long[] _used = new long[7];
    private readonly long[] _peaks = new long[7];
    private readonly List<PdfDiagnostic> _diagnostics = new();
    private readonly CancellationToken _callerToken;
    private readonly CancellationTokenSource _deadline = new();
    private readonly CancellationTokenSource _linked;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Func<TimeSpan> _elapsed;
    private PdfException? _fatal;
    private bool _active = true;

    internal PdfOperationContext(string operation, string providerId, PdfCapability capability, PdfOperationPolicy policy, CancellationToken token,
        Func<TimeSpan>? elapsedTime = null)
    {
        Operation = operation;
        ProviderId = providerId;
        Capability = capability;
        Policy = policy;
        _elapsed = elapsedTime ?? (() => _clock.Elapsed);
        _callerToken = token;
        _linked = CancellationTokenSource.CreateLinkedTokenSource(token, _deadline.Token);
        CancellationToken = _linked.Token;
        _deadline.CancelAfter(policy.Limits.MaximumElapsedTime);
    }

    /// <summary>Gets the operation name assigned by the SDK.</summary>
    public string Operation { get; }
    /// <summary>Gets the selected provider identity captured by the registry.</summary>
    public string ProviderId { get; }
    /// <summary>Gets the selected capability.</summary>
    public PdfCapability Capability { get; }
    /// <summary>Gets the immutable policy.</summary>
    public PdfOperationPolicy Policy { get; }
    /// <summary>Gets cancellation combining the caller token and the operation deadline.</summary>
    public CancellationToken CancellationToken { get; }

    internal bool IsActive { get { lock (_gate) return _active; } }

    /// <summary>Wraps a readable decoder output in a borrowed lease charging the shared expanded-byte budget.</summary>
    /// <remarks>The caller owns the decoder. The lease never closes it and expires with this operation. No codec is selected implicitly.</remarks>
    public Stream BorrowExpandedInput(Stream decodedInput)
    {
        if (decodedInput is null) throw new ArgumentNullException(nameof(decodedInput));
        if (!decodedInput.CanRead) throw new ArgumentException("A readable decoded stream is required.", nameof(decodedInput));
        Checkpoint();
        return new BorrowedStream(decodedInput, true, this, PdfResourceKind.ExpandedBytes);
    }

    /// <summary>Charges positive pixel dimensions before allocation, without allowing multiplication or cumulative overflow.</summary>
    public void ConsumePixels(long width, long height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        lock (_gate)
        {
            CheckCore();
            if (width > (Policy.Limits.MaximumPixels - _used[(int)PdfResourceKind.Pixels]) / height)
                Fail("Limit.Pixels", "The configured pixel limit was reached.");
            Charge(PdfResourceKind.Pixels, width * height);
        }
    }

    /// <summary>Checks cancellation, deadline and previously latched failures. A provider cannot suppress a fatal policy failure.</summary>
    public void Checkpoint() { lock (_gate) CheckCore(); }

    /// <summary>Charges a nonnegative cumulative resource amount before allocation; Depth/Workers require scoped acquisition.</summary>
    public void Consume(PdfResourceKind resource, long amount)
    {
        ValidateResource(resource);
        if (resource == PdfResourceKind.Depth || resource == PdfResourceKind.Workers) throw new ArgumentException("Use EnterScope for this resource.", nameof(resource));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        lock (_gate) { CheckCore(); Charge(resource, amount); }
    }

    /// <summary>Acquires one Depth or Workers unit until the returned idempotent lease is disposed.</summary>
    public IDisposable EnterScope(PdfResourceKind resource)
    {
        ValidateResource(resource);
        if (resource != PdfResourceKind.Depth && resource != PdfResourceKind.Workers) throw new ArgumentException("Only Depth and Workers are scoped.", nameof(resource));
        lock (_gate) { CheckCore(); Charge(resource, 1); return new Scope(this, resource); }
    }

    /// <summary>Records a diagnostic with SDK-bound provenance. Strict losses and error severity latch a fatal failure.</summary>
    public void Report(PdfDiagnostic diagnostic)
    {
        if (diagnostic is null) throw new ArgumentNullException(nameof(diagnostic));
        lock (_gate)
        {
            CheckCore();
            if (_diagnostics.Count >= Policy.Limits.MaximumDiagnostics) Fail("Limit.Diagnostics", "The diagnostic retention limit was reached.");
            bool loss = diagnostic.LossCategory != PdfLossCategory.None;
            var severity = loss ? (Policy.Mode == PdfProcessingMode.Strict ? PdfDiagnosticSeverity.Error : PdfDiagnosticSeverity.Warning) : diagnostic.Severity;
            var bound = new PdfDiagnostic(diagnostic.Code, diagnostic.Message, Capability, ProviderId, severity,
                diagnostic.LossCategory, Operation, diagnostic.PageIndex, diagnostic.ObjectId, diagnostic.Remediation);
            _diagnostics.Add(bound);
            if (severity == PdfDiagnosticSeverity.Error) { _fatal = new PdfException(bound); throw _fatal; }
        }
    }

    internal long Remaining(PdfResourceKind resource)
    {
        lock (_gate) { CheckCore(); return Policy.Limits.GetLimit(resource) - _used[(int)resource]; }
    }

    internal void CheckOutputRange(long start, long position, long count)
    {
        lock (_gate)
        {
            CheckCore();
            long limit = Policy.Limits.MaximumOutputBytes;
            if (position < start || position - start > limit || count > limit - (position - start))
                Fail("Limit.OutputBytes", "The output extent would exceed the configured range.");
        }
    }

    internal void RejectLimit(PdfResourceKind resource)
    {
        lock (_gate) { CheckCore(); Fail("Limit." + resource, "The configured resource limit was reached."); }
    }

    internal void EnsureComplete()
    {
        lock (_gate)
        {
            CheckCore();
            if (_used[(int)PdfResourceKind.Depth] != 0 || _used[(int)PdfResourceKind.Workers] != 0)
                Fail("Operation.ScopeLeaked", "Provider scopes must be released before returning.");
        }
    }

    internal PdfOperationReport Finish(bool succeeded)
    {
        lock (_gate)
        {
            _active = false;
            _clock.Stop();
            var resources = new PdfResourceUsage[_used.Length];
            for (int i = 0; i < resources.Length; i++) resources[i] = new PdfResourceUsage((PdfResourceKind)i, _used[i], _peaks[i]);
            return new PdfOperationReport(Operation, ProviderId, succeeded, _elapsed(), _diagnostics.ToArray(), resources, _fatal?.Diagnostic);
        }
    }

    internal void DisposeSources() { _linked.Dispose(); _deadline.Dispose(); }

    private void CheckCore()
    {
        if (!_active) throw new ObjectDisposedException(nameof(PdfOperationContext));
        if (_fatal is not null) throw _fatal;
        _callerToken.ThrowIfCancellationRequested();
        if (_elapsed() >= Policy.Limits.MaximumElapsedTime || _deadline.IsCancellationRequested)
            Fail("Limit.ElapsedTime", "The operation deadline elapsed.");
    }

    private void Charge(PdfResourceKind resource, long amount)
    {
        int index = (int)resource;
        if (amount > Policy.Limits.GetLimit(resource) - _used[index]) Fail("Limit." + resource, "The configured resource limit was reached.");
        _used[index] += amount;
        _peaks[index] = Math.Max(_peaks[index], _used[index]);
    }

    private void Fail(string code, string message)
    {
        _fatal = new PdfException(new PdfDiagnostic(code, message, Capability, ProviderId, operation: Operation));
        throw _fatal;
    }

    private static void ValidateResource(PdfResourceKind resource)
    {
        if (!Enum.IsDefined(typeof(PdfResourceKind), resource)) throw new ArgumentOutOfRangeException(nameof(resource));
    }

    private sealed class Scope : IDisposable
    {
        private PdfOperationContext? _owner;
        private readonly PdfResourceKind _resource;
        internal Scope(PdfOperationContext owner, PdfResourceKind resource) { _owner = owner; _resource = resource; }
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner is not null) { lock (owner._gate) owner._used[(int)_resource]--; }
        }
    }
}
