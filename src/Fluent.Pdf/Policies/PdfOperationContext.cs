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
    private readonly long[] _reserved = new long[7];
    private readonly bool[] _probing = new bool[7];
    private readonly List<PdfDiagnostic> _diagnostics = new();
    private readonly CancellationToken _callerToken;
    private readonly CancellationTokenSource _deadline = new();
    private readonly CancellationTokenSource _linked;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Func<TimeSpan> _elapsed;
    private PdfException? _fatal;
    private bool _active = true;
    private bool _unicodeResidentCharged;
    internal void ChargeUnicodeResident(long bytes)
    {
        lock (_gate)
        {
            CheckCore();
            if (_unicodeResidentCharged) return;
            Consume(PdfResourceKind.ExpandedBytes, bytes);
            _unicodeResidentCharged = true;
        }
    }

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
    internal long Available(PdfResourceKind resource)
    {ValidateResource(resource);lock(_gate){CheckCore();return Policy.Limits.GetLimit(resource)-_used[(int)resource]-_reserved[(int)resource];}}

    internal ResourceReservation ReserveRemainingObjects()
    {
        lock(_gate)
        {
            CheckCore();int i=(int)PdfResourceKind.Objects;long available=Policy.Limits.MaximumObjects-_used[i]-_reserved[i];
            if(available==0)Fail("Limit.Objects","No object budget remains for an isolated operation.");
            _reserved[i]+=available;return new ResourceReservation(this,available);
        }
    }

    internal sealed class ResourceReservation:IDisposable
    {
        private PdfOperationContext? _owner;
        internal ResourceReservation(PdfOperationContext owner,long amount){_owner=owner;Amount=amount;}
        internal long Amount{get;}
        internal void Complete(long consumed)
        {
            var owner=_owner??throw new ObjectDisposedException(nameof(ResourceReservation));
            lock(owner._gate)
            {
                if(!ReferenceEquals(_owner,owner))throw new ObjectDisposedException(nameof(ResourceReservation));
                owner.CheckCore();
                if(consumed<0||consumed>Amount)owner.Fail("Limit.Objects","An isolated operation exceeded its object reservation.");
                owner._reserved[(int)PdfResourceKind.Objects]-=Amount;_owner=null;owner.Charge(PdfResourceKind.Objects,consumed);
            }
        }
        public void Dispose()
        {
            var owner=_owner;
            if(owner!=null)lock(owner._gate)
            {
                if(!ReferenceEquals(_owner,owner))return;
                _owner=null;owner._reserved[(int)PdfResourceKind.Objects]-=Amount;Monitor.PulseAll(owner._gate);
            }
        }
    }

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

    internal int ReadBounded(Stream input, byte[] buffer, int offset, int count, PdfResourceKind resource)
    {
        int index = (int)resource;
        int request;
        bool probe;
        lock (_gate)
        {
            while (true)
            {
                CheckCore();
                if (count == 0) return 0;
                long available = Policy.Limits.GetLimit(resource) - _used[index] - _reserved[index];
                if (available > 0)
                {
                    request = (int)Math.Min(count, available);
                    _reserved[index] += request;
                    probe = false;
                    break;
                }
                if (_reserved[index] == 0 && !_probing[index])
                {
                    // Only one lookahead can consume a byte after the shared delivered-byte limit.
                    _probing[index] = true;
                    request = 1;
                    probe = true;
                    break;
                }
                // Release the lock while other I/O completes. Check cancel/deadline at bounded intervals.
                Monitor.Wait(_gate, 50);
            }
        }
        bool released = false;
        try
        {
            // Never hold the context lock across external stream I/O.
            int read = input.Read(buffer, offset, request);
            if (read < 0 || read > request) throw new IOException("The input stream returned an invalid byte count.");
            lock (_gate)
            {
                if (probe) _probing[index] = false;
                else _reserved[index] -= request;
                released = true;
                Monitor.PulseAll(_gate);
                CheckCore();
                Charge(resource, read);
            }
            return read;
        }
        finally
        {
            if (!released)
            {
                lock (_gate)
                {
                    if (probe) _probing[index] = false;
                    else _reserved[index] -= request;
                    Monitor.PulseAll(_gate);
                }
            }
        }
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
            if (_used[(int)PdfResourceKind.Depth] != 0 || _used[(int)PdfResourceKind.Workers] != 0 ||
                _reserved.Any(value=>value!=0) ||
                _probing[(int)PdfResourceKind.InputBytes] || _probing[(int)PdfResourceKind.ExpandedBytes])
                Fail("Operation.ScopeLeaked", "Provider scopes and reads must finish before returning.");
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
        if (amount > Policy.Limits.GetLimit(resource) - _used[index] - _reserved[index]) Fail("Limit." + resource, "The configured resource limit was reached.");
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
