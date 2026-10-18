using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Fluent.Pdf.Workers;
using Xunit;
using Xunit.Abstractions;

namespace Fluent.Pdf.Contract.Tests;

public sealed class NativeWorkerTests
{
    private readonly ITestOutputHelper _output;
    public NativeWorkerTests(ITestOutputHelper output)=>_output=output;
    [Fact]
    public void RealBridgeChecksAbiAndVersionAndReturnsDetachedBytes()
    {
        using var fixture=new WorkerFixture(_output);
        byte[] request={0,1,2,3,4};
        Assert.Equal(new byte[] {1,2,3,4},fixture.Run(request));
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
        Assert.Equal("1.0.0",fixture.Options.Deployment.EngineVersion);
    }

    [Fact]
    public void RepeatedShortLivedWorkersCompleteWithoutProcessInspectionRaces()
    {
        using var fixture=new WorkerFixture(_output);
        for(int iteration=0;iteration<40;iteration++)
        {
            Assert.Equal(new byte[]{1,2,3},fixture.Run(new byte[]{0,1,2,3}));
            Assert.Equal("Native.EngineFailure",Assert.Throws<PdfException>(()=>fixture.Run(new byte[]{3})).Diagnostic.Code);
        }
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Fact]
    public void SelfContainedManagedBridgeRunsNativeOsArchitectureAndRestrictsNewThreads()
    {
        using var fixture=new WorkerFixture(_output,"native-aot");
        Assert.Equal(new byte[]{1,2,3},fixture.Run(new byte[]{0,1,2,3}));
        string target=Path.Combine(fixture.Root,"private-thread-probe.txt");File.WriteAllText(target,"original");
        byte[] path=Encoding.UTF8.GetBytes(target);var request=new byte[path.Length+1];request[0]=7;Array.Copy(path,0,request,1,path.Length);
        Assert.Equal(new byte[]{42,0},fixture.Run(request));
        Assert.True(PdfNativeDeploymentInspector.InspectWorker(fixture.Options.Deployment).AssetsVerified);
        if(PdfNativeDeploymentInspector.CurrentRuntimeIdentifier!=fixture.Options.Deployment.RuntimeIdentifier)
            Assert.False(PdfNativeDeploymentInspector.Inspect(fixture.Options.Deployment).AssetsVerified);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Fact]
    public void WorkerDiagnosticsRequireExplicitDisclosureOptIn()
    {
        using var ordinary=new WorkerFixture(_output);
        var privateFailure=Assert.Throws<PdfException>(()=>ordinary.Run(new byte[]{8}));Assert.Null(privateFailure.Data["Native.WorkerDiagnostics"]);
        using var optedIn=new WorkerFixture(_output,captureDiagnostics:true);
        var failure=Assert.Throws<PdfException>(()=>optedIn.Run(new byte[]{8}));Assert.Equal("owned-fixture-diagnostic",failure.Data["Native.WorkerDiagnostics"]);
    }

    [Theory]
    [InlineData("wrong-abi","Native.AbiMismatch")]
    [InlineData("wrong-version","Native.EngineVersionMismatch")]
    [InlineData("missing-dependency","Native.DependencyLoadFailed")]
    public void ActualLoaderRejectsWrongAbiVersionOrMissingTransitiveDependency(string scenario,string code)
    {
        using var fixture=new WorkerFixture(_output,scenario);
        var error=Assert.Throws<PdfException>(()=>fixture.Run(new byte[] {0}));
        Assert.Equal(code,error.Diagnostic.Code);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Fact]
    public void CompletePinnedTransitiveDependencyLoadsFromThePrivateDirectory()
    {
        using var fixture=new WorkerFixture(_output,"dependency");
        Assert.Equal(new byte[] {7},fixture.Run(new byte[] {0,7}));
    }

    [Fact]
    public void NativeCrashDoesNotCrashTheCallerOrCommitPartialFileOutput()
    {
        using var fixture=new WorkerFixture(_output);
        string output=Path.Combine(fixture.Root,"persisted.bin");
        File.WriteAllText(output,"original");
        var error=Assert.Throws<PdfException>(()=>fixture.SaveFile(new byte[] {1},output));
        Assert.Equal("Native.WorkerCrashed",error.Diagnostic.Code);
        Assert.Equal("original",File.ReadAllText(output));
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
        Assert.Equal(new byte[] {42},fixture.Run(new byte[] {0,42}));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeHangIsTerminatedOnDeadlineOrCallerCancellation(bool cancel)
    {
        using var fixture=new WorkerFixture(_output,timeout:cancel?TimeSpan.FromSeconds(5):TimeSpan.FromMilliseconds(300));
        using var cancellation=new CancellationTokenSource();
        Thread? canceller=null;
        if(cancel)
        {
            canceller=new Thread(()=>{Thread.Sleep(150);cancellation.Cancel();}) {IsBackground=true};
            canceller.Start();
        }
        var watch=Stopwatch.StartNew();
        try
        {
            if(cancel) Assert.ThrowsAny<OperationCanceledException>(()=>fixture.Run(new byte[] {2},cancellation.Token));
            else Assert.Equal("Native.ElapsedTime",Assert.Throws<PdfException>(()=>fixture.Run(new byte[] {2})).Diagnostic.Code);
        }
        finally { canceller?.Join(1000); }
        Assert.True(watch.Elapsed<TimeSpan.FromSeconds(5),"Native termination exceeded five seconds.");
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Fact]
    public void CancellationDuringSetupAndExecutionReleasesEveryOwnedStage()
    {
        using var fixture=new WorkerFixture(_output);
        foreach(int delay in new[]{5,20,50,150,250})for(int repeat=0;repeat<2;repeat++)
        {
            using var cancellation=new CancellationTokenSource();
            var canceller=new Thread(()=>{Thread.Sleep(delay);cancellation.Cancel();}){IsBackground=true};canceller.Start();
            var clock=Stopwatch.StartNew();
            try {Assert.ThrowsAny<OperationCanceledException>(()=>fixture.Run(new byte[]{2},cancellation.Token));}
            finally {canceller.Join(1000);}
            Assert.True(clock.Elapsed<TimeSpan.FromSeconds(5));Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
        }
        Assert.Equal(new byte[]{42},fixture.Run(new byte[]{0,42}));
    }

    [Fact]
    public void NativeSandboxRejectsCallerFileWritesAndNetworkAccess()
    {
        using var fixture=new WorkerFixture(_output);
        string target=Path.Combine(fixture.Root,"protected.txt");
        File.WriteAllText(target,"original");
        byte[] name=Encoding.UTF8.GetBytes(target+"\0");
        var request=new byte[name.Length+1];
        request[0]=4;Array.Copy(name,0,request,1,name.Length);
        Assert.Equal(new byte[] {0},fixture.Run(request));
        Assert.Equal("original",File.ReadAllText(target));
        request[0]=7;
        Assert.Equal(new byte[] {0,0},fixture.Run(request));
        var listener=new TcpListener(IPAddress.Loopback,0);
        listener.Start();
        try
        {
            int port=((IPEndPoint)listener.LocalEndpoint).Port;
            using(var control=new TcpClient())
            {
                control.Connect(IPAddress.Loopback,port);
                using(var accepted=listener.AcceptTcpClient()) { }
            }
            Assert.Equal(new byte[] {0},fixture.Run(new byte[] {5,(byte)(port>>8),(byte)port}));
            Assert.False(listener.Pending(),"The isolated worker reached the live caller endpoint.");
        }
        finally {listener.Stop();}
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Fact]
    public void NativeMemoryAndOutputBoundariesCannotReturnUnboundedResults()
    {
        using var fixture=new WorkerFixture(_output);
        try { Assert.Equal(new byte[] {0},fixture.Run(new byte[] {6})); }
        catch(PdfException error) { Assert.Contains(error.Diagnostic.Code,new[] {"Native.MemoryLimit","Native.WorkerCrashed"}); }
        Assert.Equal("Native.EngineFailure",Assert.Throws<PdfException>(()=>fixture.Run(new byte[] {3})).Diagnostic.Code);
        Assert.Equal(new byte[1024],fixture.Run(new byte[1025]));
        Assert.Equal("Native.EngineFailure",Assert.Throws<PdfException>(()=>fixture.Run(new byte[1026])).Diagnostic.Code);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
    }

    [Fact]
    public void HostValidatesRequestBeforeStartingAProcessAndCannotHideFailures()
    {
        using var fixture=new WorkerFixture(_output);
        Assert.Equal("Native.InputLimit",Assert.Throws<PdfException>(()=>fixture.Run(new byte[4097])).Diagnostic.Code);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Jobs));
        Assert.Throws<ArgumentException>(()=>new NativeWorkerOptions(fixture.Options.Deployment,"missing",fixture.Library,fixture.Jobs));
        Assert.Throws<ArgumentException>(()=>new NativeWorkerOptions(fixture.Options.Deployment,fixture.Executable,fixture.Executable,fixture.Jobs));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new NativeWorkerOptions(fixture.Options.Deployment,fixture.Executable,fixture.Library,fixture.Jobs,1));
    }

    private sealed class WorkerFixture : IDisposable
    {
        private readonly ITestOutputHelper _output;
        internal string Root {get;}=Path.Combine(Path.GetTempPath(),"fluent-pdf-worker-tests-"+Guid.NewGuid().ToString("N"));
        internal string Jobs {get;}
        internal string Executable {get;}
        internal string Library {get;}
        internal NativeWorkerOptions Options {get;}
        internal WorkerFixture(ITestOutputHelper output,string scenario="normal",TimeSpan? timeout=null,bool captureDiagnostics=false)
        {
            _output=output;
            string rid=(scenario=="native-aot"?PdfNativeDeploymentInspector.OperatingSystemRuntimeIdentifier:PdfNativeDeploymentInspector.CurrentRuntimeIdentifier)??throw new InvalidOperationException("Unsupported source host.");
            string repo=FindRepository();
            string binaries=Path.Combine(repo,"artifacts","native-fixtures",rid);
            Executable=rid.StartsWith("win-",StringComparison.Ordinal)?"worker.exe":"worker";
            string extension=rid.StartsWith("win-",StringComparison.Ordinal)?"dll":rid.StartsWith("osx-",StringComparison.Ordinal)?"dylib":"so";
            Library=(scenario=="native-aot"?"native-aot-fixture":scenario=="wrong-abi"?"wrong-abi":scenario=="dependency"||scenario=="missing-dependency"?"dependent":"bridge")+"."+extension;
            Directory.CreateDirectory(Root);Jobs=Path.Combine(Root,"jobs");Directory.CreateDirectory(Jobs);
            var assets=new List<PdfNativeAsset> {Asset(binaries,Executable),Asset(binaries,Library)};
            if(scenario=="dependency") assets.Add(Asset(binaries,"required."+extension));
            var deployment=new PdfNativeDeployment(binaries,rid,"native.fixture",scenario=="wrong-version"?"2.0.0":"1.0.0",
                "fluent-pdf-fixture-1",16777216,assets.ToArray());
            Options=new NativeWorkerOptions(deployment,Executable,Library,Jobs,scenario=="native-aot"?536870912:67108864,4096,1024,timeout??TimeSpan.FromSeconds(5),allowInProcessThreads:scenario=="native-aot",captureWorkerDiagnostics:scenario=="native-aot"||captureDiagnostics);
        }
        internal byte[] Run(byte[] request,CancellationToken cancellation=default)
        {
            using var provider=Provider(request);
            using var document=Pdf.Create(providers:new PdfProviderRegistry(provider)).AddPage();
            using var output=new MemoryStream();
            try { document.Save(output,cancellation); }
            catch(PdfException error)
            {
                _output.WriteLine(error.Diagnostic.Code+": "+error.Diagnostic.Message);
                foreach(string key in new[]{"Native.FailureType","Native.FailureHResult","Native.FailureErrorCode","Native.WorkerFailureDetail"})if(error.Data[key] is object value)_output.WriteLine(key+": "+value);
                if(error.Data["Native.LoaderDetail"] is string detail) _output.WriteLine(detail);
                if(error.Data["Native.WorkerDiagnostics"] is string diagnostics) _output.WriteLine(diagnostics);
                throw;
            }
            return output.ToArray();
        }
        internal void SaveFile(byte[] request,string path)
        {
            using var provider=Provider(request);
            using var document=Pdf.Create(providers:new PdfProviderRegistry(provider)).AddPage();
            document.SaveFile(path);
        }
        private ContractProvider Provider(byte[] request)=>new() {OnWriteContext=(_,output,context)=>
        {
            var bytes=new NativeWorkerHost().Execute(Options,request,context);
            output.Write(bytes,0,bytes.Length);
        }};
        private static PdfNativeAsset Asset(string root,string name)
        {
            string path=Path.Combine(root,name);
            if(!File.Exists(path)) throw new InvalidOperationException("Build the actual native worker fixtures before this source suite.");
            using var hash=SHA256.Create();using var file=File.OpenRead(path);
            return new PdfNativeAsset(name,file.Length,BitConverter.ToString(hash.ComputeHash(file)).Replace("-","").ToLowerInvariant());
        }
        private static string FindRepository()
        {
            var directory=new DirectoryInfo(AppContext.BaseDirectory);
            while(directory is not null)
            {
                if(File.Exists(Path.Combine(directory.FullName,"Fluent.Pdf.slnx"))) return directory.FullName;
                directory=directory.Parent;
            }
            throw new InvalidOperationException("The native source fixture repository is unavailable.");
        }
        public void Dispose()=>Directory.Delete(Root,true);
    }
}
