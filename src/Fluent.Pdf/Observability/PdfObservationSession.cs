using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;

/// <summary>An explicitly owned bounded telemetry queue. Processing never invokes a sink or performs telemetry I/O.</summary>
/// <remarks>Drain on a caller-controlled thread. A slow sink blocks that caller only; throwing sinks are counted and discarded. Full/disposed queues drop observations without changing PDF results. Counters and generated IDs are metadata; the host must still apply its telemetry retention/access policy.</remarks>
public sealed class PdfObservationSession : IDisposable
{
    private readonly object _gate=new();
    private readonly PdfObservation?[] _queue;
    private int _head,_count,_draining;
    private long _sequence,_dropped,_sinkFailures;
    private bool _disposed;
    /// <summary>Creates a private queue of1–16384 events. No sink, network connection or background thread starts.</summary>
    public PdfObservationSession(int maximumEvents=1024)
    {
        if(maximumEvents<1||maximumEvents>16384)throw new ArgumentOutOfRangeException(nameof(maximumEvents));
        _queue=new PdfObservation[maximumEvents]; TraceId=Guid.NewGuid();
    }
    /// <summary>Gets the random trace correlation ID shared by contexts explicitly using this session.</summary>
    public Guid TraceId { get; }
    /// <summary>Gets the fixed queue capacity.</summary>
    public int MaximumEvents => _queue.Length;
    /// <summary>Gets the current pending event count.</summary>
    public int PendingEvents { get { lock(_gate)return _count; } }
    /// <summary>Gets events discarded because the queue was full/disposed or event construction failed.</summary>
    public long DroppedEvents { get { lock(_gate)return _dropped; } }
    /// <summary>Gets callbacks that threw; failed deliveries are not retried or exposed to PDF processing.</summary>
    public long SinkFailures { get { lock(_gate)return _sinkFailures; } }

    /// <summary>Invokes at most1–16384 callbacks on this caller's thread and returns successful deliveries. Only one drain may run at a time; callbacks execute outside the queue lock. Ordinary callback exceptions are counted and discarded.</summary>
    public int Drain(Action<PdfObservation> sink,int maximumEvents=1024)
    {
        if(sink==null)throw new ArgumentNullException(nameof(sink));
        if(maximumEvents<1||maximumEvents>16384)throw new ArgumentOutOfRangeException(nameof(maximumEvents));
        if(Interlocked.CompareExchange(ref _draining,1,0)!=0)throw new InvalidOperationException("Only one observation drain may run at a time.");
        int delivered=0;
        try
        {
            for(int i=0;i<maximumEvents;i++)
            {
                PdfObservation item;
                lock(_gate)
                {
                    if(_count==0)break;
                    item=_queue[_head]!;_queue[_head]=null;_head=(_head+1)%_queue.Length;_count--;
                }
                try { sink(item);delivered++; }
                catch(Exception) { lock(_gate)Increment(ref _sinkFailures); }
            }
            return delivered;
        }
        finally { Volatile.Write(ref _draining,0); }
    }
    /// <summary>Explicitly records a numeric engine version and probe result. The caller must use an actual probe result; no engine is loaded and no path/name/message is captured.</summary>
    public void RecordEngineProbe(PdfCapability capability,Version version,bool succeeded)
    {
        Guard.Capability(capability);
        if(version==null)throw new ArgumentNullException(nameof(version));
        Publish(Guid.NewGuid(),PdfObservationKind.EngineProbe,capability,TimeSpan.Zero,succeeded,succeeded?null:"Provider.ProbeFailed",null,null,0,Array.Empty<PdfResourceUsage>(),version.ToString());
    }
    /// <summary>Writes schema1 JSON for a snapshot of pending sanitized events and loss counters, leaving the output open. This explicit export uses finite budgets, may leave partial stream output on failure, does not drain events and does not observe itself.</summary>
    public PdfOperationReport WriteBundle(Stream output,PdfOperationPolicy? policy=null,CancellationToken cancellationToken=default)
    {
        if(output==null)throw new ArgumentNullException(nameof(output));
        if(!output.CanWrite)throw new ArgumentException("A writable borrowed stream is required.",nameof(output));
        var selected=policy??PdfOperationPolicy.Default;
        return PdfFont.Run("WriteSupportBundle","managed.observation",new(selected.Mode,selected.Limits),cancellationToken,c=>
        {
            PdfObservation[] events;long dropped,failures;
            lock(_gate)
            {
                c.Consume(PdfResourceKind.Objects,_count+1);
                c.Consume(PdfResourceKind.ExpandedBytes,4096L+_count*4096L);
                events=new PdfObservation[_count];
                for(int i=0;i<_count;i++)events[i]=_queue[(_head+i)%_queue.Length]!;
                dropped=_dropped;failures=_sinkFailures;
            }
            using var lease=new BorrowedStream(output,false,c);
            using var writer=new StreamWriter(lease,new UTF8Encoding(false),1024,true);
            string N(long number)=>number.ToString(CultureInfo.InvariantCulture);
            void Q(string? text) { if(text==null)writer.Write("null");else { writer.Write('"');writer.Write(text);writer.Write('"'); } }
            writer.Write("{\"schemaVersion\":1,\"traceId\":");Q(TraceId.ToString("N"));writer.Write(",\"droppedEvents\":"+N(dropped)+",\"sinkFailures\":"+N(failures)+",\"events\":[");
            for(int i=0;i<events.Length;i++)
            {
                c.Checkpoint();if(i!=0)writer.Write(',');var e=events[i];
                writer.Write("{\"sequence\":"+N(e.Sequence)+",\"spanId\":");Q(e.SpanId.ToString("N"));writer.Write(",\"kind\":");Q(e.Kind.ToString());writer.Write(",\"capability\":");Q(e.Capability.ToString());
                writer.Write(",\"elapsedTicks\":"+N(e.ElapsedTime.Ticks)+",\"succeeded\":"+(e.Succeeded.HasValue?(e.Succeeded.Value?"true":"false"):"null")+",\"code\":");Q(e.Code);writer.Write(",\"remediation\":");Q(e.Remediation);
                writer.Write(",\"completed\":"+(e.Completed.HasValue?N(e.Completed.Value):"null")+",\"total\":"+(e.Total.HasValue?N(e.Total.Value):"null")+",\"diagnosticCount\":"+N(e.DiagnosticCount)+",\"engineVersion\":");Q(e.EngineVersion);
                writer.Write(",\"resources\":[");
                for(int j=0;j<e.Resources.Count;j++) { if(j!=0)writer.Write(',');var r=e.Resources[j];writer.Write("{\"resource\":");Q(r.Resource.ToString());writer.Write(",\"consumed\":"+N(r.Consumed)+",\"peak\":"+N(r.Peak)+"}"); }
                writer.Write("]}");
            }
            writer.Write("]}");writer.Flush();
        },PdfCapability.WriteDocument);
    }
    /// <summary>Closes the session and discards pending events. Further processing drops telemetry; it remains usable independently of the disposed session.</summary>
    public void Dispose() { lock(_gate) { if(_disposed)return;_disposed=true;for(int i=0;i<_count;i++)Increment(ref _dropped);Array.Clear(_queue,0,_queue.Length);_count=0;_head=0; } }
    internal Guid Start(PdfCapability capability)
    {
        try { var span=Guid.NewGuid();Publish(span,PdfObservationKind.Started,capability,TimeSpan.Zero,null,null,null,null,0,Array.Empty<PdfResourceUsage>(),null);return span; }
        catch(Exception) { lock(_gate)Increment(ref _dropped);return Guid.Empty; }
    }
    internal void Progress(Guid span,PdfCapability capability,TimeSpan elapsed,long completed,long total) => Publish(span,PdfObservationKind.Progress,capability,elapsed,null,null,completed,total,0,Array.Empty<PdfResourceUsage>(),null);
    internal void Finish(Guid span,PdfCapability capability,PdfOperationReport report,bool canceled)
    {
        string? code=report.Succeeded?null:canceled?"Operation.Canceled":SafeCode(report.Failure?.Code);
        Publish(span,PdfObservationKind.Finished,capability,report.ElapsedTime,report.Succeeded,code,null,null,report.Diagnostics.Count,report.Resources,null);
    }
    private void Publish(Guid span,PdfObservationKind kind,PdfCapability capability,TimeSpan elapsed,bool? success,string? code,long? completed,long? total,int diagnostics,IReadOnlyList<PdfResourceUsage> resources,string? version)
    {
        if(span==Guid.Empty)return;
        lock(_gate)
        {
            if(_disposed||_count==_queue.Length) { Increment(ref _dropped);return; }
            try
            {
                string? remediation=code==null?null:code.StartsWith("Limit.",StringComparison.Ordinal)||code=="Cache.EntryLimit"?"Review explicit budgets and reduce requested work.":code=="Operation.Canceled"?"Review caller cancellation and retry only when authorized.":"Inspect the input and configured provider in trusted application scope.";
                Increment(ref _sequence);
                _queue[(_head+_count)%_queue.Length]=new(TraceId,span,_sequence,kind,capability,elapsed,success,code,remediation,completed,total,diagnostics,resources,version);_count++;
            }
            catch(Exception) { Increment(ref _dropped); }
        }
    }
    // A free-form diagnostic code can itself contain a credential. Only this
    // closed SDK-authored catalog is exported; no message/path/name is retained.
    private static string SafeCode(string? code) => code switch
    {
        null=>"Operation.Failed",
        "Limit.InputBytes" or "Limit.OutputBytes" or "Limit.ExpandedBytes" or "Limit.Objects" or "Limit.Pixels" or "Limit.Depth" or "Limit.Workers" or "Limit.Diagnostics" or "Limit.ElapsedTime" or "Limit.TimeScopes" or "Operation.ScopeLeaked" or "Cache.EntryLimit" or "Cache.HashCollision" or "Provider.Unavailable" or "Native.SandboxUnavailable" or "Native.Timeout" or "Native.Crash" =>code,
        _=>"Provider.Diagnostic"
    };
    private static void Increment(ref long value) { if(value<long.MaxValue)value++; }
}
