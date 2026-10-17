using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fluent.Pdf.Workers;

/// <summary>Runs one pinned native bridge in a fresh platform-sandboxed process; never loads it into the caller.</summary>
public sealed class NativeWorkerHost
{
    /// <summary>Executes a bounded binary request and returns owned response bytes only after a valid reply and successful exit.</summary>
    /// <remarks>Every call stages/rechecks immutable assets, clears inherited environment, and destroys its own job directory.
    /// Request snapshots and response allocation charge the operation expanded-byte budget. Providers retain responsibility for PDF input/output leases.</remarks>
    public byte[] Execute(NativeWorkerOptions options, byte[] request, PdfOperationContext context)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (context is null) throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
        if (request.Length > options.MaximumInputBytes) throw Fail(context,"Native.InputLimit","The native request exceeds its configured ceiling.");
        using var scope = context.EnterScope(PdfResourceKind.Workers);
        PdfNativeDeploymentInspector.VerifyWorker(options.Deployment, context);
        context.Consume(PdfResourceKind.ExpandedBytes, request.Length);
        var input = (byte[])request.Clone();
        string job = Path.Combine(options.TemporaryDirectory,"fluent-pdf-worker-"+Guid.NewGuid().ToString("N"));
        string bootstrapDirectory = Path.Combine(job,"host");
        string? profile = null;
        Process? process = null;
        Task? inputTask = null;
        Task<Reply>? responseTask = null;
        Task<string?>? errorTask = null;
        bool created = false;
        bool started = false;
        try
        {
            if (!Directory.Exists(options.TemporaryDirectory)) throw Fail(context,"Native.TemporaryDirectory","The explicit native job parent is unavailable.");
            if ((File.GetAttributes(options.TemporaryDirectory) & FileAttributes.ReparsePoint) != 0)
                throw Fail(context,"Native.UnsafePath","The job parent cannot be a reparse point.");
            Directory.CreateDirectory(job);
            created = true;
            WorkerPlatform.ProtectDirectory(job);
            CopyAssets(options,job,context);
            var staged = new PdfNativeDeployment(job, options.Deployment.RuntimeIdentifier, options.Deployment.EngineId,
                options.Deployment.EngineVersion, options.Deployment.Abi, options.Deployment.MaximumTotalBytes, options.Deployment.Assets.ToArray());
            PdfNativeDeploymentInspector.VerifyWorker(staged,context);
            var nonce = new byte[16];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(nonce);
            string nonceText = BitConverter.ToString(nonce).Replace("-","").ToLowerInvariant();
            // Keep the trusted bootstrap's executable/current directory free of engine DLLs.
            // Its OS imports are resolved before main can restrict dynamic search paths.
            Directory.CreateDirectory(bootstrapDirectory);
            if (!WorkerPlatform.IsWindows) WorkerPlatform.ProtectDirectory(bootstrapDirectory);
            string executable = Path.Combine(bootstrapDirectory,options.ExecutableFileName);
            File.Copy(Path.Combine(job,options.ExecutableFileName),executable,false);
            WorkerPlatform.MakeExecutable(executable);
            var start = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = bootstrapDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.EnvironmentVariables.Clear();
            if (WorkerPlatform.IsWindows)
            {
                string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                start.EnvironmentVariables["SystemRoot"] = windows;
                start.EnvironmentVariables["WINDIR"] = windows;
                start.EnvironmentVariables["SystemDrive"] = Path.GetPathRoot(windows)!.TrimEnd('\\');
                // AppContainer creation expands these OS-owned profile locations.
                // Values are recomputed, never copied from the caller's environment.
                start.EnvironmentVariables["USERPROFILE"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                start.EnvironmentVariables["LOCALAPPDATA"] = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                start.EnvironmentVariables["APPDATA"] = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                start.EnvironmentVariables["ProgramData"] = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            }
            start.EnvironmentVariables["TMP"] = job;
            start.EnvironmentVariables["TEMP"] = job;
            start.EnvironmentVariables["TMPDIR"] = job;
            start.EnvironmentVariables["HOME"] = job;
            // Fixed per-process runtime bounds for self-contained managed native bridges.
            // These are recomputed from this job's envelope, never inherited from the caller.
            start.EnvironmentVariables["DOTNET_GCHeapHardLimit"]=(options.MaximumMemoryBytes/4).ToString("X",CultureInfo.InvariantCulture);
            start.EnvironmentVariables["DOTNET_GCConserveMemory"]="9";
            start.EnvironmentVariables["DOTNET_gcServer"]="0";
            start.EnvironmentVariables["DOTNET_gcConcurrent"]="0";
            start.EnvironmentVariables["DOTNET_EnableDiagnostics"]="0";
            start.EnvironmentVariables["DOTNET_PROCESSOR_COUNT"]="2";
            if (options.Deployment.RuntimeIdentifier.StartsWith("osx-",StringComparison.Ordinal))
                start.EnvironmentVariables["MallocNanoZone"] = "0";
            string[] arguments = { job,options.LibraryFileName,options.Deployment.Abi,options.Deployment.EngineVersion,
                options.MaximumInputBytes.ToString(CultureInfo.InvariantCulture),options.MaximumOutputBytes.ToString(CultureInfo.InvariantCulture),
                options.MaximumMemoryBytes.ToString(CultureInfo.InvariantCulture),
                ((long)Math.Ceiling(options.MaximumElapsedTime.TotalMilliseconds)).ToString(CultureInfo.InvariantCulture),nonceText,
                CallerProcessId().ToString(CultureInfo.InvariantCulture),options.AllowInProcessThreads?"1":"0" };
            start.Arguments = string.Join(" ",arguments.Select(Quote));
            context.Checkpoint();
            process = new Process { StartInfo = start };
            if (!process.Start()) throw Fail(context,"Native.StartFailed","The pinned native worker could not be started.");
            started = true;
            profile = "Fluent.Pdf.worker."+process.Id.ToString(CultureInfo.InvariantCulture)+"."+nonceText;
            var clock = Stopwatch.StartNew();
            var running = process;
            inputTask = Task.Factory.StartNew(() => WriteRequest(running.StandardInput.BaseStream,input),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
            responseTask = Task.Factory.StartNew(() => ReadResponse(running.StandardOutput.BaseStream,nonce,options,context),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
            errorTask = Task.Factory.StartNew(() => DrainErrors(running.StandardError.BaseStream,options.CaptureWorkerDiagnostics),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
            while (!process.WaitForExit(10))
            {
                context.Checkpoint();
                if (clock.Elapsed >= options.MaximumElapsedTime) throw Fail(context,"Native.ElapsedTime","The native worker deadline elapsed.");
                if (responseTask.IsFaulted || errorTask.IsFaulted)
                {
                    context.Checkpoint();
                    if (process.WaitForExit(50) && process.ExitCode != 0) throw Fail(context,"Native.WorkerCrashed","The isolated native worker exited abnormally.");
                    throw Fail(context,"Native.Protocol","The native worker violated its bounded transport contract.");
                }
                if (!WorkerPlatform.IsWindows)
                {
                    try
                    {
                        process.Refresh();
                        if (process.WorkingSet64 > options.MaximumMemoryBytes) throw Fail(context,"Native.MemoryLimit","The native worker exceeded its resident-memory ceiling.");
                    }
                    catch(InvalidOperationException) when(process.WaitForExit(0)) { }
                }
            }
            context.Checkpoint();
            if (!WaitTasks(new Task[] { responseTask,errorTask },1000)) throw Fail(context,"Native.TransportLeaked","A native transport did not close after process exit.");
            if (process.ExitCode != 0) throw Fail(context,"Native.WorkerCrashed","The isolated native worker exited abnormally.");
            var reply = responseTask.GetAwaiter().GetResult();
            if (reply.Status != 0)
            {
                string detail=reply.Status==5 ? " Host stage "+Read32(reply.Output,0).ToString(CultureInfo.InvariantCulture)+
                    ", native status "+Read32(reply.Output,4).ToString(CultureInfo.InvariantCulture)+"." : "";
                try { throw Fail(context,StatusCode(reply.Status),"The isolated native deployment or operation was rejected."+detail); }
                catch(PdfException failure)
                {
                    if(reply.Status==3)
                        failure.Data["Native.LoaderDetail"]=WorkerPlatform.IsWindows && reply.Output.Length==4
                            ? "Windows loader status "+Read32(reply.Output,0).ToString(CultureInfo.InvariantCulture)
                            : Encoding.UTF8.GetString(reply.Output);
                    throw;
                }
            }
            if (!WaitTasks(new[] { inputTask },1000) || inputTask.IsFaulted || errorTask.IsFaulted)
                throw Fail(context,"Native.Protocol","The native request transport did not complete.");
            context.Checkpoint();
            return reply.Output;
        }
        catch (PdfException failure)
        {
            if(options.CaptureWorkerDiagnostics && errorTask is not null && WaitTasks(new Task[]{errorTask},100) && errorTask.Status==TaskStatus.RanToCompletion && !string.IsNullOrEmpty(errorTask.Result))
                failure.Data["Native.WorkerDiagnostics"]=errorTask.Result;
            failure.Data["Native.EngineId"] = options.Deployment.EngineId;
            failure.Data["Native.ExpectedAbi"] = options.Deployment.Abi;
            failure.Data["Native.ExpectedEngineVersion"] = options.Deployment.EngineVersion;
            failure.Data["Native.RuntimeIdentifier"] = options.Deployment.RuntimeIdentifier;
            failure.Data["Native.LibraryFileName"] = options.LibraryFileName;
            throw;
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidDataException) { context.Checkpoint(); throw Fail(context,"Native.Protocol","The native worker violated its bounded transport contract."); }
        catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is System.ComponentModel.Win32Exception || failure is AggregateException)
        {
            context.Checkpoint();
            throw Fail(context,"Native.WorkerFailure","The native worker or its private transport failed.");
        }
        finally
        {
            var cleanupClock=Stopwatch.StartNew();
            int RemainingCleanup(int maximum)=>(int)Math.Max(0,Math.Min(maximum,4000-cleanupClock.ElapsedMilliseconds));
            void CleanupOwned(Action action)
            {
                while(true)
                {
                    try {action();return;}
                    catch(Exception cleanup) when(WorkerPlatform.IsWindows &&
                        (cleanup is IOException || cleanup is UnauthorizedAccessException) &&
                        ((cleanup.HResult&65535)==5||(cleanup.HResult&65535)==32||(cleanup.HResult&65535)==33||(cleanup.HResult&65535)==145) &&
                        RemainingCleanup(1000)>0)
                    {Thread.Sleep(Math.Min(10,RemainingCleanup(10)));}
                }
            }
            Exception? cleanupFailure = null;
            if (process is not null && started)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        WorkerPlatform.Terminate(process);
                        if (!process.WaitForExit(RemainingCleanup(3500))) cleanupFailure = new IOException("The native process did not terminate.");
                    }
                }
                catch (Exception cleanup) when (cleanup is IOException || cleanup is InvalidOperationException || cleanup is System.ComponentModel.Win32Exception)
                { cleanupFailure = cleanup; }
                try { CloseTransport(process); }
                catch (Exception cleanup) when (cleanup is IOException || cleanup is InvalidOperationException)
                { cleanupFailure ??= cleanup; }
                if (!WaitTasks(new Task?[] { inputTask,responseTask,errorTask }.Where(t => t is not null).Cast<Task>().ToArray(),RemainingCleanup(500)))
                    cleanupFailure ??= new IOException("A native transport did not terminate.");
                process.Dispose();
            }
            else process?.Dispose();
            try { if (WorkerPlatform.IsWindows && profile is not null) CleanupOwned(()=>WorkerPlatform.DeleteProfile(profile)); }
            catch (IOException cleanup) { cleanupFailure ??= cleanup; }
            if (created)
            {
                try
                {
                    CleanupOwned(()=>
                    {
                    if (Directory.Exists(bootstrapDirectory))
                    {
                        string bootstrap=Path.Combine(bootstrapDirectory,options.ExecutableFileName);
                        if (File.Exists(bootstrap)) { File.SetAttributes(bootstrap,FileAttributes.Normal); File.Delete(bootstrap); }
                        Directory.Delete(bootstrapDirectory,false);
                    }
                    foreach (string file in Directory.GetFiles(job))
                    {
                        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) File.SetAttributes(file,FileAttributes.Normal);
                        File.Delete(file);
                    }
                    Directory.Delete(job,false);
                    });
                }
                catch (Exception cleanup) when (cleanup is IOException || cleanup is UnauthorizedAccessException)
                { cleanupFailure ??= cleanup; }
            }
            Array.Clear(input,0,input.Length);
            if (cleanupFailure is not null)
            {
                try { throw Fail(context,"Native.CleanupFailed","The native job did not release every owned resource."); }
                catch (Exception failure)
                {
                    failure.Data["Native.CleanupFailure"] = cleanupFailure.GetType().Name;
                    if (created && Directory.Exists(job)) failure.Data["Native.JobDirectory"] = job;
                    if (profile is not null) failure.Data["Native.AppContainerProfile"] = profile;
                    throw;
                }
            }
        }
    }

    private static void CopyAssets(NativeWorkerOptions options, string job, PdfOperationContext context)
    {
        var buffer = new byte[65536];
        foreach (var asset in options.Deployment.Assets)
        {
            context.Checkpoint();
            using var source = new FileStream(Path.Combine(options.Deployment.RootDirectory,asset.FileName),FileMode.Open,FileAccess.Read,FileShare.Read);
            using var target = new FileStream(Path.Combine(job,asset.FileName),FileMode.CreateNew,FileAccess.Write,FileShare.None);
            using var hash = SHA256.Create();
            if (source.Length != asset.Length) throw Fail(context,"Native.SizeMismatch","A native asset changed during private staging.");
            long remaining = asset.Length;
            while (remaining > 0)
            {
                context.Checkpoint();
                int count = source.Read(buffer,0,(int)Math.Min(remaining,buffer.Length));
                context.Checkpoint();
                if (count == 0) throw Fail(context,"Native.SizeMismatch","A native asset changed during private staging.");
                hash.TransformBlock(buffer,0,count,buffer,0);
                target.Write(buffer,0,count);
                remaining -= count;
            }
            if (source.ReadByte() != -1) throw Fail(context,"Native.SizeMismatch","A native asset changed during private staging.");
            hash.TransformFinalBlock(Array.Empty<byte>(),0,0);
            if (BitConverter.ToString(hash.Hash!).Replace("-","").ToLowerInvariant() != asset.Sha256)
                throw Fail(context,"Native.HashMismatch","A native asset changed during private staging.");
        }
    }

    private static void WriteRequest(Stream stream, byte[] bytes)
    {
        try
        {
            Write32(stream,(uint)bytes.Length);
            stream.Write(bytes,0,bytes.Length);
            stream.Flush();
        }
        finally { stream.Dispose(); }
    }

    private static Reply ReadResponse(Stream stream, byte[] nonce, NativeWorkerOptions options, PdfOperationContext context)
    {
        var header = ReadExact(stream,32,context);
        byte[] magic = Encoding.ASCII.GetBytes("FPNW0001");
        for (int i=0;i<8;i++) if (header[i] != magic[i]) throw new InvalidDataException("Invalid worker response.");
        for (int i=0;i<16;i++) if (header[i+8] != nonce[i]) throw new InvalidDataException("Invalid worker authentication.");
        uint status = Read32(header,24),length = Read32(header,28);
        if (status > 9 || status == 0 && length > options.MaximumOutputBytes || status == 5 && length != 8 ||
            status == 3 && length > 2048 || status != 0 && status != 3 && status != 5 && length != 0)
            throw new InvalidDataException("Invalid worker response bounds.");
        if (status == 0) context.Consume(PdfResourceKind.ExpandedBytes,length);
        var result = ReadExact(stream,(int)length,context);
        if (stream.ReadByte() != -1) throw new InvalidDataException("Trailing worker response bytes.");
        context.Checkpoint();
        return new Reply(status,result);
    }

    private static byte[] ReadExact(Stream stream, int size, PdfOperationContext context)
    {
        var bytes = new byte[size];
        int offset=0;
        while (offset<size)
        {
            context.Checkpoint();
            int count=stream.Read(bytes,offset,size-offset);
            context.Checkpoint();
            if(count==0) throw new InvalidDataException("Truncated worker response.");
            offset+=count;
        }
        return bytes;
    }

    private static string? DrainErrors(Stream stream,bool capture)
    {
        var bytes=new byte[1024];
        byte[]? retained=capture?new byte[8192]:null;
        int total=0,count;
        while((count=stream.Read(bytes,0,bytes.Length))!=0)
        {
            total+=count;
            if(total>8192) throw new InvalidDataException("Worker diagnostic transport limit exceeded.");
            if(retained is not null)Array.Copy(bytes,0,retained,total-count,count);
        }
        return retained is null?null:Encoding.UTF8.GetString(retained,0,total);
    }

    private static bool WaitTasks(Task[] tasks, int milliseconds)
    {
        try { return Task.WaitAll(tasks,milliseconds); }
        catch (AggregateException) { return tasks.All(t=>t.IsCompleted); }
    }

    private static void CloseTransport(Process process)
    {
        process.StandardInput.BaseStream.Dispose();
        process.StandardOutput.BaseStream.Dispose();
        process.StandardError.BaseStream.Dispose();
    }

    private static string Quote(string value)
    {
        var result=new StringBuilder("\"");
        int slashes=0;
        foreach(char c in value)
        {
            if(c=='\\') { slashes++; continue; }
            if(c=='\"') result.Append('\\',slashes*2+1).Append(c);
            else result.Append('\\',slashes).Append(c);
            slashes=0;
        }
        return result.Append('\\',slashes*2).Append('"').ToString();
    }

    private static int CallerProcessId() { using var current=Process.GetCurrentProcess(); return current.Id; }

    private static uint Read32(byte[] b,int n) => (uint)(b[n]|b[n+1]<<8|b[n+2]<<16|b[n+3]<<24);
    private static void Write32(Stream stream,uint n)
    {
        var bytes=new byte[4];
        for(int i=0;i<4;i++) bytes[i]=(byte)(n>>(8*i));
        stream.Write(bytes,0,bytes.Length);
    }

    private static string StatusCode(uint status) => status switch
    {
        1=>"Native.AbiMismatch",2=>"Native.EngineVersionMismatch",3=>"Native.DependencyLoadFailed",
        4=>"Native.ExportMissing",5=>"Native.SandboxUnavailable",6=>"Native.InputLimit",
        7=>"Native.EngineFailure",8=>"Native.MemoryLimit",_=>"Native.Protocol"
    };

    private static PdfException Fail(PdfOperationContext context,string code,string message)
    {
        var diagnostic=new PdfDiagnostic(code,message,remediation:"Use the exact pinned bridge ABI/dependencies and a supported isolated worker host.");
        context.Report(diagnostic);
        return new PdfException(diagnostic);
    }

    private sealed class Reply
    {
        internal Reply(uint status,byte[] output) { Status=status;Output=output; }
        internal uint Status { get; }
        internal byte[] Output { get; }
    }
}
