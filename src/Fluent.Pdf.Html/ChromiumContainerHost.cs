using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Fluent.Pdf.Html;

/// <summary>Explicit bounded Docker CLI transport to one private offline Linux browser container. Complete daemon/kernel/image/security/RID qualification remains required.</summary>
public static class ChromiumContainerHost
{
    /// <summary>Verifies prerequisites, reserves memory, requires an immutable creation acknowledgement, then starts the unique offline read-only nonroot container and checks its bootstrap. Disposal targets the acknowledged ID and joins owned I/O; unacknowledged creation and cleanup failures retain owned identities.</summary>
    public static IChromiumConnection Open(ChromiumContainerOptions options, PdfOperationContext context)
    {
        if (options == null || context == null)
            throw new ArgumentNullException(options == null ? nameof(options) : nameof(context));
        context.Consume(PdfResourceKind.ExpandedBytes, 256);
        var worker = context.EnterScope(PdfResourceKind.Workers);
        try { return new WorkerConnection(new Connection(options, context), worker); }
        catch (PdfException failure)
        {
            worker.Dispose();
            context.Checkpoint();
            context.ReportFailure(failure.Diagnostic, "Fluent.Pdf.Html.HostFailure", failure.Diagnostic.Code);
            throw;
        }
        catch
        {
            worker.Dispose();
            context.Checkpoint();
            throw PdfWriterCore.Error(context, "Html.HostUnavailable", "The explicit private container prerequisite failed.");
        }
    }

    private sealed class WorkerConnection : IChromiumConnection
    {
        private readonly IChromiumConnection _inner;
        private IDisposable? _worker;
        internal WorkerConnection(IChromiumConnection inner, IDisposable worker) { _inner = inner; _worker = worker; }
        public void Send(string message, PdfOperationContext context) => _inner.Send(message, context);
        public string Receive(PdfOperationContext context) => _inner.Receive(context);
        public void Dispose()
        {
            var worker = Interlocked.Exchange(ref _worker, null);
            if (worker == null) return;
            try { _inner.Dispose(); } finally { worker.Dispose(); }
        }
    }

    private sealed class Connection : IChromiumConnection
    {
        private const int MaximumCharacters = 16777216;
        private readonly ChromiumContainerOptions _options;
        private readonly string _name = "fluent-pdf-browser-" + Guid.NewGuid().ToString("N");
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly object _lifecycle = new();
        private readonly IDisposable _reservation;
        private PdfOperationContext? _context;
        private Process? _process;
        private Task? _stderr;
        private Task? _pending;
        private StreamWriter? _input;
        private StreamReader? _output;
        private volatile bool _disposed;
        private bool _createIssued;
        private string? _containerId;

        internal Connection(ChromiumContainerOptions options, PdfOperationContext context)
        {
            _options = options;
            _context = context;
            context.Checkpoint();
            VerifyPrerequisites(options, context);
            context.Consume(PdfResourceKind.ExpandedBytes, 131072);
            _reservation = context.Reserve(PdfResourceKind.ExpandedBytes, options.MaximumMemoryBytes);
            try
            {
                string memory = options.MaximumMemoryBytes.ToString(CultureInfo.InvariantCulture);
                var args = new List<string>
                {
                    "create", "--pull=never", "--rm", "--interactive", "--init", "--name=" + _name,
                    "--label=org.fluent-pdf.owner=" + _name, "--network=none", "--read-only", "--cap-drop=ALL",
                    "--security-opt=no-new-privileges", "--security-opt=seccomp=" + options.SeccompProfile,
                    "--user=65532:65532", "--memory=" + memory, "--memory-swap=" + memory,
                    "--pids-limit=" + options.MaximumProcesses.ToString(CultureInfo.InvariantCulture), "--cpus=1", "--shm-size=33554432",
                    "--tmpfs=/tmp:rw,nosuid,nodev,noexec,size=67108864,mode=1777", "--ulimit=core=0:0", "--stop-timeout=1",
                    "--env=FLUENT_PDF_MEMORY=" + memory, "--env=FLUENT_PDF_PIDS=" + options.MaximumProcesses.ToString(CultureInfo.InvariantCulture),
                    "--env=FLUENT_PDF_SECONDS=" + Math.Ceiling(options.MaximumElapsedTime.TotalSeconds).ToString(CultureInfo.InvariantCulture),
                    "--entrypoint=/usr/bin/python3", options.ImageId, "-I", "/opt/fluent-pdf/browser/host.py"
                };
                CreateContainer(args, context);
                // A late start can only refer to this acknowledged immutable ID; it cannot recreate a removed job.
                _process = Start(new[] { "start", "--attach", "--interactive", _containerId! });
                _input = new StreamWriter(_process.StandardInput.BaseStream, new UTF8Encoding(false, true), 8192, true) { NewLine = "\n" };
                _output = new StreamReader(_process.StandardOutput.BaseStream, new UTF8Encoding(false, true), false, 8192, true);
                _stderr = Task.Factory.StartNew(() => Drain(_process.StandardError), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                string bootstrap = Receive(context);
                using var json = JsonDocument.Parse(bootstrap);
                var root = json.RootElement;
                if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 4 || !root.TryGetProperty("fluentPdfBrowserHost", out var version) || version.GetInt32() != 1 || !root.TryGetProperty("memory", out var actualMemory) || actualMemory.GetInt64() != options.MaximumMemoryBytes || !root.TryGetProperty("pids", out var actualPids) || actualPids.GetInt32() != options.MaximumProcesses || !root.TryGetProperty("isolated", out var isolated) || isolated.ValueKind != JsonValueKind.True)
                    throw Error("Bootstrap", "The private container did not establish the required bounded host bootstrap.");
                context.Checkpoint();
            }
            catch
            {
                Dispose();
                context.Checkpoint();
                throw Error("HostUnavailable", "The explicitly provisioned private browser container could not start with its required isolation.");
            }
        }

        private void CreateContainer(IEnumerable<string> arguments, PdfOperationContext context)
        {
            _process = Start(arguments);
            _createIssued = true;
            _stderr = Task.Factory.StartNew(() => Drain(_process.StandardError), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            var reading = Task.Factory.StartNew(() =>
            {
                using var reader = new StreamReader(_process.StandardOutput.BaseStream, new UTF8Encoding(false, true), false, 1024, true);
                var value = new StringBuilder(128);
                int next;
                while ((next = reader.Read()) >= 0)
                {
                    context.Checkpoint();
                    if (value.Length == 128) throw new IOException("Container creation identity extent.");
                    context.Consume(PdfResourceKind.ExpandedBytes, 8);
                    value.Append((char)next);
                }
                return value.ToString();
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            _pending = reading;
            while (!_process.WaitForExit(10) || !reading.IsCompleted || !_stderr.IsCompleted)
            {
                context.Checkpoint();
                if (reading.IsFaulted || _stderr.IsFaulted) throw Error("CreateIdentity", "The private container creation acknowledgement failed.");
                Check(context);
            }
            string response;
            try { response = reading.GetAwaiter().GetResult(); _stderr.GetAwaiter().GetResult(); }
            catch { context.Checkpoint(); throw Error("CreateIdentity", "The private container creation acknowledgement failed."); }
            string id = response.EndsWith("\r\n", StringComparison.Ordinal) ? response.Substring(0, response.Length - 2) : response.EndsWith("\n", StringComparison.Ordinal) ? response.Substring(0, response.Length - 1) : response;
            if (_process.ExitCode != 0 || id.Length != 64 || id.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')))
                throw Error("CreateIdentity", "An exact successful immutable container creation acknowledgement is required.");
            _containerId = id;
            reading.Dispose(); _stderr.Dispose(); _process.Dispose();
            _pending = null; _stderr = null; _process = null;
            Check(context);
        }

        public void Send(string message, PdfOperationContext context)
        {
            Check(context);
            if (message == null || message.Length == 0 || message.Length > MaximumCharacters || message.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw PdfWriterCore.Error(context, "Html.TransportExtent", "One bounded complete JSON transport message is required.");
            if (new UTF8Encoding(false, true).GetByteCount(message) > MaximumCharacters)
                throw PdfWriterCore.Error(context, "Html.TransportExtent", "The UTF8 browser transport message exceeds its byte ceiling.");
            context.Consume(PdfResourceKind.ExpandedBytes, message.Length * 4L + 32768);
            Task writing;
            lock (_lifecycle)
            {
                Check(context);
                if (_pending != null) throw PdfWriterCore.Error(context, "Html.ConcurrentTransport", "A private browser connection requires serialized transport calls.");
                writing = _pending = Task.Factory.StartNew(() => { _input!.WriteLine(message); _input.Flush(); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            Wait(writing, context);
        }

        public string Receive(PdfOperationContext context)
        {
            Check(context);
            context.Consume(PdfResourceKind.ExpandedBytes, 16384);
            Task<string> reading;
            lock (_lifecycle)
            {
            Check(context);
            if (_pending != null) throw PdfWriterCore.Error(context, "Html.ConcurrentTransport", "A private browser connection requires serialized transport calls.");
            reading = Task.Factory.StartNew(() =>
            {
                var value = new StringBuilder(2048);
                var block = new char[4096];
                while (true)
                {
                    context.Checkpoint();
                    int count = 0;
                    bool complete = false;
                    // StreamReader already buffers the pipe. Preserve newline framing
                    // without a block read waiting for bytes from the next response.
                    while (count < block.Length)
                    {
                        int next = _output!.Read();
                        if (next < 0) throw Error("HostExited", "The private browser transport ended before a complete message.");
                        if (next == '\n') { complete = true; break; }
                        if (next == '\r' || next == 0 || value.Length + count == MaximumCharacters)
                            throw Error("TransportExtent", "The browser transport exceeded its complete-message profile.");
                        block[count++] = (char)next;
                    }
                    context.Consume(PdfResourceKind.ExpandedBytes, count * 8L);
                    value.Append(block, 0, count);
                    if (!complete) continue;
                    if (value.Length == 0) throw Error("TransportExtent", "An empty browser transport message is invalid.");
                    return value.ToString();
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            _pending = reading;
            }
            Wait(reading, context);
            return reading.GetAwaiter().GetResult();
        }

        private void Wait(Task task, PdfOperationContext context)
        {
            while (!((IAsyncResult)task).AsyncWaitHandle.WaitOne(10)) Check(context);
            Check(context);
            try { task.GetAwaiter().GetResult(); }
            catch { context.Checkpoint(); throw PdfWriterCore.Error(context, "Html.TransportFailed", "The private browser transport failed without disclosing payloads."); }
            lock (_lifecycle)
            {
                if (ReferenceEquals(_pending, task)) _pending = null;
                task.Dispose();
            }
        }

        private void Check(PdfOperationContext context)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(IChromiumConnection));
            if (!ReferenceEquals(context, _context)) throw Error("TransportContext", "A private connection must retain its original operation context.");
            context.Checkpoint();
            if (_clock.Elapsed > _options.MaximumElapsedTime)
                throw PdfWriterCore.Error(context, "Html.HostDeadline", "The private browser host deadline elapsed.");
        }

        private Process Start(IEnumerable<string> arguments)
        {
            var start = new ProcessStartInfo
            {
                FileName = _options.DockerExecutable,
                Arguments = string.Join(" ", new[] { "--config", _options.EmptyConfigurationDirectory, "--host", _options.DaemonEndpoint }.Concat(arguments).Select(Quote)),
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = _options.EmptyConfigurationDirectory
            };
            start.EnvironmentVariables.Clear();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                start.EnvironmentVariables["SystemRoot"] = windows;
                start.EnvironmentVariables["WINDIR"] = windows;
            }
            var process = new Process { StartInfo = start };
            try
            {
                if (!process.Start()) throw Error("HostUnavailable", "The pinned private container CLI did not start.");
                return process;
            }
            catch { process.Dispose(); throw; }
        }

        public void Dispose()
        {
            lock (_lifecycle)
            {
                if (_disposed) return;
                _disposed = true;
            }
            var clock = Stopwatch.StartNew();
            var operation = _context;
            bool unexpectedExit = _process != null && _process.HasExited && _process.ExitCode != 0;
            Exception? cleanup = null;
            if (_createIssued) try
            {
                // Removal is out of band: a blocked stdin/renderer cannot prevent cancellation cleanup.
                using var remove = Start(new[] { "rm", "--force", "--volumes", _containerId ?? _name });
                var output = Task.Factory.StartNew(() => Drain(remove.StandardOutput), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                var error = Task.Factory.StartNew(() => Drain(remove.StandardError), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                if (!remove.WaitForExit(3000))
                {
                    remove.Kill();
                    if (!remove.WaitForExit(500)) throw new IOException("The private container cleanup command did not terminate.");
                    throw new IOException("The private container cleanup deadline elapsed.");
                }
                if (!Task.WaitAll(new[] { output, error }, 500)) throw new IOException("The private cleanup transports did not terminate.");
                // --rm can remove an already exited job first. Confirm absence with the same explicit daemon.
                if (remove.ExitCode != 0 && !ConfirmAbsent()) throw new IOException("The owned private container could not be removed.");
            }
            catch (Exception failure) { cleanup = failure; }
            try
            {
                if (_process != null)
                {
                    if (!_process.HasExited) _process.Kill();
                    int left = Math.Max(1, 4800 - (int)clock.ElapsedMilliseconds);
                    if (!_process.WaitForExit(left)) throw new IOException("The private browser CLI did not terminate.");
                    _process.StandardInput.Dispose();
                    _process.StandardOutput.Dispose();
                    _process.StandardError.Dispose();
                }
                var tasks = new[] { _pending, _stderr }.Where(t => t != null).Cast<Task>().ToArray();
                try
                {
                    if (!Task.WaitAll(tasks, Math.Max(1, 4900 - (int)clock.ElapsedMilliseconds))) throw new IOException("Owned browser I/O did not terminate.");
                }
                catch (AggregateException) { if (tasks.Any(t => !t.IsCompleted)) throw; }
                try { _input?.Dispose(); } catch (IOException) { } catch (ObjectDisposedException) { }
                _output?.Dispose();
                foreach (var task in tasks) task.Dispose();
            }
            catch (Exception failure) { cleanup ??= failure; }
            finally
            {
                _process?.Dispose();
                _reservation.Dispose();
                _context = null;
            }
            if (cleanup != null || clock.Elapsed > TimeSpan.FromSeconds(5))
            {
                var failure = Error("HostCleanup", "Private browser cleanup did not complete within its required boundary.");
                failure.Data["Fluent.Pdf.Html.OwnedContainer"] = _name;
                if (_containerId != null) failure.Data["Fluent.Pdf.Html.OwnedContainerId"] = _containerId;
                if (operation?.IsActive == true)
                {
                    operation.PreserveFailureDetail("Fluent.Pdf.Html.OwnedContainer", _name);
                    if (_containerId != null) operation.PreserveFailureDetail("Fluent.Pdf.Html.OwnedContainerId", _containerId);
                    operation.PreserveFailureDetail("Fluent.Pdf.Html.CleanupFailure", failure.Diagnostic.Code);
                    operation.ReportFailure(failure.Diagnostic, "Fluent.Pdf.Html.OwnedContainer", _name);
                }
                throw failure;
            }
            if (_createIssued && _containerId == null)
            {
                // A canceled daemon request can complete after its CLI is killed. A single absence query cannot prove future absence.
                // Preserve the owned identity for explicit daemon reconciliation; never advertise successful startup cleanup.
                var failure = Error("HostStartupUnconfirmed", "The private daemon did not acknowledge creation; later owned-container reconciliation remains required.");
                failure.Data["Fluent.Pdf.Html.OwnedContainer"] = _name;
                if (operation?.IsActive == true)
                {
                    operation.PreserveFailureDetail("Fluent.Pdf.Html.OwnedContainer", _name);
                    operation.ReportFailure(failure.Diagnostic, "Fluent.Pdf.Html.OwnedContainer", _name);
                }
                throw failure;
            }
            if (unexpectedExit)
            {
                var failure = Error("HostExited", "The private browser container exited unsuccessfully before cleanup.");
                if (operation?.IsActive == true) operation.ReportFailure(failure.Diagnostic, "Fluent.Pdf.Html.OwnedContainer", _name);
                throw failure;
            }
        }

        private bool ConfirmAbsent()
        {
            using var inspect = Start(new[] { "container", "ls", "--all", _containerId == null ? "--filter=name=^/" + _name + "$" : "--filter=id=" + _containerId, "--format={{.ID}}" });
            var output = Task.Factory.StartNew(() =>
            {
                int count = 0;
                var buffer = new char[1024];
                int read;
                while ((read = inspect.StandardOutput.Read(buffer, 0, buffer.Length)) > 0)
                {
                    count += read;
                    if (count > 4096) throw new IOException("The private container absence observation exceeded its bound.");
                }
                return count;
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            var error = Task.Factory.StartNew(() => Drain(inspect.StandardError), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            if (!inspect.WaitForExit(500)) { inspect.Kill(); inspect.WaitForExit(250); return false; }
            if (!Task.WaitAll(new[] { output, error }, 250)) return false;
            // Only a successful daemon query with no matching owned container establishes absence.
            return inspect.ExitCode == 0 && output.GetAwaiter().GetResult() == 0;
        }

        private static void Drain(StreamReader reader)
        {
            var buffer = new char[1024];
            while (reader.Read(buffer, 0, buffer.Length) > 0) { }
        }
    }

    private static void VerifyPrerequisites(ChromiumContainerOptions options, PdfOperationContext context)
    {
        VerifyFile(options.DockerExecutable, options.DockerSha256, 268435456, context);
        VerifyFile(options.SeccompProfile, options.SeccompSha256, 1048576, context);
        CheckPath(options.EmptyConfigurationDirectory);
        if (!Directory.Exists(options.EmptyConfigurationDirectory) || Directory.EnumerateFileSystemEntries(options.EmptyConfigurationDirectory).Any())
            throw Error("Configuration", "A caller-protected empty CLI configuration directory is required.");
        context.Consume(PdfResourceKind.ExpandedBytes, 8388608);
        using var file = File.OpenRead(options.SeccompProfile);
        var captured = PdfProfileIo.Capture(file, context, 1048576);
        using var view = captured.Open(0, captured.Length);
        using var policy = JsonDocument.Parse(view, new JsonDocumentOptions { MaxDepth = 32 });
        if (!policy.RootElement.TryGetProperty("defaultAction", out var action) || action.GetString() != "SCMP_ACT_ERRNO")
            throw Error("ContainerPolicy", "An explicitly pinned default-deny container syscall policy is required.");
    }

    private static void VerifyFile(string path, string expected, long maximum, PdfOperationContext context)
    {
        CheckPath(path);
        context.Consume(PdfResourceKind.ExpandedBytes, 65536);
        var buffer = new byte[65536];
        using var input = File.OpenRead(path);
        using var hash = SHA256.Create();
        long total = 0;
        int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            context.Checkpoint();
            total += count;
            if (total > maximum) throw Error("PrerequisiteExtent", "A pinned private browser prerequisite exceeds its extent.");
            hash.TransformBlock(buffer, 0, count, buffer, 0);
        }
        hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        if (total == 0 || BitConverter.ToString(hash.Hash!).Replace("-", "").ToLowerInvariant() != expected)
            throw Error("PrerequisiteHash", "A private browser prerequisite differs from its exact pin.");
    }

    private static void CheckPath(string path)
    {
        for (string? current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw Error("PrerequisitePath", "A private browser prerequisite cannot contain linked paths.");
    }

    private static string Quote(string value)
    {
        var text = new StringBuilder("\"");
        int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            text.Append('\\', slashes * (c == '"' ? 2 : 1));
            slashes = 0;
            if (c == '"') text.Append('\\');
            text.Append(c);
        }
        text.Append('\\', slashes * 2);
        return text.Append('"').ToString();
    }
    private static PdfException Error(string code, string message) => new(new PdfDiagnostic("Html." + code, message));
}
