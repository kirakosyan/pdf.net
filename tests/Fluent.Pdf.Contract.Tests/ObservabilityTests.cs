using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ObservabilityTests
{
    private static PdfOperationReport Run(PdfObservationSession session,Action<PdfOperationContext> action,PdfOperationPolicy? policy=null,CancellationToken token=default)
        =>PdfFont.Run("private-operation-name","private-provider-id",(policy??PdfOperationPolicy.Default).WithObservations(session),token,action);
    private static string Bundle(PdfObservationSession session)
    { using var stream=new MemoryStream();session.WriteBundle(stream);Assert.True(stream.CanWrite);return Encoding.UTF8.GetString(stream.ToArray()); }
    private static string Artifact(string name)
    { string directory=Path.Combine(AppContext.BaseDirectory,"artifacts","observability");Directory.CreateDirectory(directory);return Path.Combine(directory,name); }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompiledCSharpAndVisualBasicTelemetryDoesNotChangePdfBytes(bool visualBasic)
    {
        byte[] source=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures","Automation","original.pdf"));
        using var expected=new MemoryStream();using var plain=Pdf.Open(new MemoryStream(source));plain.Save(expected);
        using var output=new MemoryStream();using var support=new MemoryStream();
        if(visualBasic)ContractExamples.VisualBasic.ContractExample.ObservePdf(source,output,support);else ContractExamples.CSharp.ContractExample.ObservePdf(source,output,support);
        Assert.Equal(expected.ToArray(),output.ToArray());Assert.Contains("\"kind\":\"Finished\"",Encoding.UTF8.GetString(support.ToArray()));
        Assert.True(output.CanWrite);Assert.True(support.CanWrite);
        string language=visualBasic?"visual-basic":"csharp";
        File.WriteAllBytes(Artifact(language+".pdf"),output.ToArray());File.WriteAllBytes(Artifact(language+".json"),support.ToArray());
    }
    [Fact]
    public void DefaultPolicyHasNoTelemetryAndOptInDoesNotMutateIt()
    {
        using var session=new PdfObservationSession();
        var policy=PdfOperationPolicy.Default.WithObservations(session);
        Assert.Null(PdfOperationPolicy.Default.Observations);Assert.Same(session,policy.Observations);Assert.Same(PdfOperationPolicy.Default.Limits,policy.Limits);
        PdfFont.Run("private","private",null,default,c=>c.ReportProgress(1,1));Assert.Equal(0,session.PendingEvents);
    }
    [Fact]
    public void StructuredLifecycleProgressAndResourcesAreCorrelatedAndDetached()
    {
        using var session=new PdfObservationSession();
        Run(session,c=> { c.Consume(PdfResourceKind.Objects,3);c.ReportProgress(2,5); });
        File.WriteAllText(Artifact("lifecycle.json"),Bundle(session),new UTF8Encoding(false));
        var events=new List<PdfObservation>();Assert.Equal(3,session.Drain(events.Add));
        Assert.Equal(new[]{PdfObservationKind.Started,PdfObservationKind.Progress,PdfObservationKind.Finished},events.Select(x=>x.Kind));
        Assert.All(events,x=> {Assert.Equal(session.TraceId,x.TraceId);Assert.Equal(events[0].SpanId,x.SpanId);Assert.Equal(1,x.SchemaVersion);});
        Assert.Equal(new long[]{1,2,3},events.Select(x=>x.Sequence));Assert.Equal(2,events[1].Completed);Assert.Equal(5,events[1].Total);
        Assert.True(events[2].Succeeded);Assert.Equal(3,events[2].Resources.Single(x=>x.Resource==PdfResourceKind.Objects).Consumed);
        Assert.Equal(0,session.PendingEvents);Assert.Null(events[2].Code);
    }
    [Fact]
    public void ProviderFreeTextCodesPathsAndCredentialsNeverReachTheSupportBundle()
    {
        using var session=new PdfObservationSession();
        Assert.Throws<PdfException>(()=>Run(session,c=>c.Report(new("secret-code-token","password=secret-payload C:/private.pdf https://private.invalid/?token=secret",providerId:"secret-provider",operation:"secret-operation",pageIndex:42,objectId:123,remediation:"secret-remediation"))));
        string json=Bundle(session);
        File.WriteAllText(Artifact("privacy.json"),json,new UTF8Encoding(false));
        foreach(string secret in new[]{"private-operation-name","private-provider-id","secret","private.pdf","private.invalid","password"})Assert.DoesNotContain(secret,json);
        Assert.Contains("Provider.Diagnostic",json);Assert.Contains("trusted application scope",json);Assert.DoesNotContain("pageIndex",json);
    }
    [Fact]
    public void LimitAndCancellationFailuresHaveSafeCodesWithoutReplacingTheOriginalFailure()
    {
        using var session=new PdfObservationSession();
        var error=Assert.Throws<PdfException>(()=>Run(session,c=>c.Consume(PdfResourceKind.Objects,2),new(limits:new(maximumObjects:1))));
        Assert.Equal("Limit.Objects",error.Diagnostic.Code);Assert.Contains("Limit.Objects",Bundle(session));
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(()=>Run(session,_=>{},token:cancel.Token));
        Assert.Contains("Operation.Canceled",Bundle(session));
    }
    [Fact]
    public void SinksRunOnlyDuringExplicitDrainAndExceptionsCannotChangePdfProcessing()
    {
        using var session=new PdfObservationSession();
        Assert.True(Run(session,_=>{}).Succeeded);Assert.Equal(2,session.PendingEvents);
        int calls=0;Assert.Equal(0,session.Drain(_=> {calls++;throw new IOException("secret sink error");}));
        Assert.Equal(2,calls);Assert.Equal(2,session.SinkFailures);Assert.Equal(0,session.PendingEvents);
        Assert.True(Run(session,_=>{}).Succeeded);Assert.DoesNotContain("secret sink error",Bundle(session));
    }
    [Fact]
    public async Task BlockedSinkDoesNotHoldTheQueueOrProcessingContextLock()
    {
        using var session=new PdfObservationSession();Run(session,_=>{});
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var drain=Task.Run(()=>session.Drain(_=>{entered.Set();Assert.True(release.Wait(TimeSpan.FromSeconds(10)));},1));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(Run(session,c=>c.ReportProgress(1,1)).Succeeded);
            Assert.Throws<InvalidOperationException>(()=>session.Drain(_=>{}));
        }
        finally { release.Set(); }
        Assert.Equal(1,await drain);
    }
    [Fact]
    public void FullAndDisposedSessionsDropEventsWithoutFailingWork()
    {
        using var session=new PdfObservationSession(1);
        Assert.True(Run(session,c=>{for(int i=0;i<20;i++)c.ReportProgress(i,20);}).Succeeded);
        Assert.Equal(1,session.PendingEvents);Assert.Equal(21,session.DroppedEvents);
        session.Dispose();Assert.Equal(22,session.DroppedEvents);Assert.Equal(0,session.PendingEvents);
        Assert.True(Run(session,_=>{}).Succeeded);Assert.Equal(24,session.DroppedEvents);
    }
    [Fact]
    public void SupportExportRespectsLimitsCancellationAndBorrowedOwnership()
    {
        using var session=new PdfObservationSession();Run(session,_=>{});using var output=new MemoryStream();
        Assert.Equal("Limit.OutputBytes",Assert.Throws<PdfException>(()=>session.WriteBundle(output,new(limits:new(maximumOutputBytes:16)))).Diagnostic.Code);
        Assert.True(output.CanWrite);Assert.Equal(2,session.PendingEvents);
        Assert.Equal("Limit.ExpandedBytes",Assert.Throws<PdfException>(()=>session.WriteBundle(output,new(limits:new(maximumExpandedBytes:1)))).Diagnostic.Code);
        using var cancel=new CancellationTokenSource();cancel.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>session.WriteBundle(output,cancellationToken:cancel.Token));
    }
    [Fact]
    public void NumericEngineProbeAndBundleSchemaAreCultureIndependentAndDoNotSelfObserve()
    {
        using var session=new PdfObservationSession();var saved=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=new CultureInfo("fr-FR");
            session.RecordEngineProbe(PdfCapability.WriteDocument,typeof(Pdf).Assembly.GetName().Version!,true);
            using var output=new MemoryStream();session.WriteBundle(output,PdfOperationPolicy.Default.WithObservations(session));
            Assert.Equal(1,session.PendingEvents);string json=Encoding.UTF8.GetString(output.ToArray());
            Assert.Contains("\"schemaVersion\":1",json);Assert.Contains("\"kind\":\"EngineProbe\"",json);Assert.Contains("\"engineVersion\":\"1.0.0.0\"",json);
            string directory=Path.Combine(AppContext.BaseDirectory,"artifacts","observability");Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,"support.json"),output.ToArray());
        }
        finally { CultureInfo.CurrentCulture=saved; }
    }
    [Fact]
    public async Task ParallelWorkersShareTraceButNeverOperationIdentity()
    {
        using var session=new PdfObservationSession(256);using var pool=new PdfProcessingPool(new(64,64),new(operationPolicy:PdfOperationPolicy.Default.WithObservations(session)));
        var tasks=new List<Task<PdfOperationReport>>();
        for(int i=0;i<64;i++){Assert.True(pool.TrySubmit(Array.Empty<byte>(),job=>job.Context.ReportProgress(1,1),out var task));tasks.Add(task!);}
        await Task.WhenAll(tasks);await pool.CompleteAsync();
        var events=new List<PdfObservation>();session.Drain(events.Add);
        Assert.Equal(192,events.Count);Assert.Equal(64,events.Select(e=>e.SpanId).Distinct().Count());Assert.All(events,e=>Assert.Equal(session.TraceId,e.TraceId));
        Assert.All(events.GroupBy(e=>e.SpanId),group=>Assert.Equal(new[]{PdfObservationKind.Started,PdfObservationKind.Progress,PdfObservationKind.Finished},group.Select(e=>e.Kind)));
    }
    [Fact]
    public void ProgressChecksLimitsEvenWithoutTelemetryAndInvalidArgumentsReject()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>new PdfObservationSession(0));Assert.Throws<ArgumentOutOfRangeException>(()=>new PdfObservationSession(16385));
        using var session=new PdfObservationSession();
        Run(session,c=> {Assert.Throws<ArgumentOutOfRangeException>(()=>c.ReportProgress(2,1));Assert.Throws<ArgumentOutOfRangeException>(()=>c.ReportProgress(0,0));});
        using var cancel=new CancellationTokenSource();
        Assert.ThrowsAny<OperationCanceledException>(()=>PdfFont.Run("private","private",null,cancel.Token,c=>{cancel.Cancel();c.ReportProgress(0,1);}));
    }
}
