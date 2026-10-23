using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Fluent.Pdf;
using Fluent.Pdf.Formats;

namespace Fluent.Pdf.CommandLine;
internal sealed record Outcome(int SchemaVersion, string Id, string Operation, int ExitCode, string Code, bool Published, string Cleanup, string? TemporaryName, string[] Diagnostics);
internal static class JobRunner
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    internal static async Task<Outcome> Run(Job job, Settings settings, CancellationToken global)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(global);
        deadline.CancelAfter(settings.TimeoutMilliseconds);
        var token = deadline.Token;
        var publication = new Publication();
        var diagnostics = new List<string>();
        int exit = 0;
        string code = "CLI.Success";
        try
        {
            token.ThrowIfCancellationRequested();
            var policy = new PdfOperationPolicy(limits: new(maximumInputBytes: settings.MaximumInputBytes, maximumOutputBytes: settings.MaximumOutputBytes, maximumExpandedBytes: 134217728, maximumObjects: 250000, maximumPixels: 16000000, maximumDiagnostics: 64, maximumElapsedTime: TimeSpan.FromMilliseconds(settings.TimeoutMilliseconds)));
            using var input = job.Input == null ? null : await Admission(() => Capture(job.Input, settings.MaximumInputBytes, publication, token), publication, token);
            using var output = new BoundedMemory(settings.MaximumOutputBytes);
            using var password = job.PasswordFile == null ? null : await Admission(() => FilePublication.ReadPassword(job.PasswordFile, token), publication, token);
            Execute(job, input, output, password, policy, diagnostics, token);
            token.ThrowIfCancellationRequested();
            if (job.Output == "-")
            {
                publication.StdoutStarted = true;
                await WriteStdout(output.ToArray(), token);
                token.ThrowIfCancellationRequested();
                publication.Published = true;
            }
            else
                FilePublication.Publish(job.Output, output, job.Overwrite, publication, token);
        }
        catch (Exception error)
        {
            (exit, code) = Classify(error, global);
        }
        finally
        {
            FilePublication.Cleanup(publication);
        }

        if (publication.Cleanup == "failed")
        {
            exit = 5;
            code = "CLI.CleanupFailed";
        }

        string cleanup = publication.StdoutStarted && !publication.Published ? "stdout-may-be-partial" : publication.PendingAdmission ? "input-released-on-process-exit" : publication.Cleanup;
        return new(1, job.Id, job.Operation, exit, code, publication.Published, cleanup, publication.TemporaryName, diagnostics.ToArray());
    }

    private static void Execute(Job job, Stream? input, BoundedMemory output, PdfPassword? password, PdfOperationPolicy policy, List<string> diagnostics, CancellationToken token)
    {
        void Report(PdfOperationReport report)
        {
            foreach (var diagnostic in report.Diagnostics)
            {
                if (diagnostics.Count == 64)
                    throw new CliFailure("CLI.DiagnosticLimit", 4);
                diagnostics.Add(SafeCode(diagnostic.Code));
            }
        }

        if (job.Operation == "formats")
        {
            using var matrix = new BoundedMemory(1048576);
            Report(PdfConversions.WriteMatrix(matrix, policy, token));
            matrix.Position = 0;
            using var json = JsonDocument.Parse(matrix);
            JsonSerializer.Serialize(output, new { schemaVersion = 1, cliRendererConfigured = false, cliTargets = new[] { "Svg", "StaticEps" }, sdkMatrix = json.RootElement }, Program.Json);
            return;
        }

        if (job.Operation == "detect")
        {
            var detection = PdfConversions.Detect(input!, job.Input == "-" ? null : Path.GetExtension(job.Input), policy, token);
            Report(detection.OperationReport);
            JsonSerializer.Serialize(output, new { schemaVersion = 1, format = detection.Format.ToString(), conformanceValidated = false }, Program.Json);
            return;
        }

        if (job.Operation == "preflight")
        {
            var report = PdfPreflight.Inspect(input!, policy: policy, cancellationToken: token);
            Report(report.OperationReport);
            Report(PdfPreflight.WriteReport(report, output, policy, token));
            return;
        }

        if (job.Operation == "decrypt")
        {
            Report(PdfSecurity.DecryptWithPassword(input!, output, password!, allowSignatureInvalidation: job.AllowSignatureInvalidation, policy: policy, cancellationToken: token).Report);
            return;
        }

        using var document = Pdf.Open(input!, new(operationPolicy: policy), cancellationToken: token);
        if (document.LastOperationReport is { } reading)
            Report(reading);
        var source = document.CreateSnapshot();
        if (job.Operation == "rewrite")
        {
            Report(document.Save(output, cancellationToken: token, writeOptions: new(allowSignatureInvalidation: job.AllowSignatureInvalidation)));
            return;
        }

        var selection = PdfPageSelection.Parse(job.Pages, source.Pages.Count, 10000, policy, token);
        if (job.Operation == "select")
        {
            using var selected = PdfPageOrganization.Extract(source, selection, new(allowSignatureInvalidation: job.AllowSignatureInvalidation, maximumPages: 10000), policy, token);
            if (selected.LastOperationReport is { } organization)
                Report(organization);
            Report(selected.Save(output, cancellationToken: token, writeOptions: new(allowSignatureInvalidation: job.AllowSignatureInvalidation)));
            return;
        }

        if (job.Operation == "convert")
        {
            if (!Enum.TryParse<PdfConversionTarget>(job.Target, false, out var target) || !Enum.IsDefined(target) || target.ToString() != job.Target)
                throw new CliFailure("CLI.TargetUnavailable", 3);
            if (target is not (PdfConversionTarget.Svg or PdfConversionTarget.StaticEps))
                throw new CliFailure("CLI.RendererRequired", 3);
            Report(PdfConversions.Export(source, selection.Indices, output, new(target), PdfProviderRegistry.Empty, policy: policy, cancellationToken: token));
            return;
        }

        bool first = true;
        foreach (int page in selection.Indices)
        {
            token.ThrowIfCancellationRequested();
            var text = PdfTextExtraction.ReadPage(source, page, policy: policy, cancellationToken: token);
            Report(text.OperationReport);
            if (!first)
                WriteText("\f", output);
            first = false;
            WriteText(job.View == "raw" ? text.RawText : job.View == "layout" ? text.LayoutText : text.Text, output);
        }
    }

    private static void WriteText(string text, BoundedMemory output)
    {
        if (Utf8.GetByteCount(text) > output.Remaining)
            throw new CliFailure("CLI.OutputLimit", 4);
        byte[] bytes = Utf8.GetBytes(text);
        try
        {
            output.Write(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static async Task<T> Admission<T>(Func<Task<T>> read, Publication state, CancellationToken token)
        where T : IDisposable
    {
        // Supervision applies only to process-owned input admission. SDK work is
        // never abandoned and starts only after admission completes successfully.
        var pending = Task.Run(read);
        try
        {
            return await pending.WaitAsync(token);
        }
        catch
        {
            if (!pending.IsCompleted)
                state.PendingAdmission = true;
            _ = pending.ContinueWith(completed =>
            {
                if (completed.Status == TaskStatus.RanToCompletion)
                    completed.Result.Dispose();
                else
                    _ = completed.Exception;
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
    }

    private static async Task<BoundedMemory> Capture(string path, int maximum, Publication state, CancellationToken token)
    {
        var result = new BoundedMemory(maximum);
        try
        {
            if (path == "-")
            {
                if (!Console.IsInputRedirected)
                    throw new CliFailure("CLI.InputRedirectionRequired", 2);
                var input = Console.OpenStandardInput();
                while (true)
                {
                    // The sole process-owned stdin read may block in the platform console.
                    // Cancellation ends the CLI process before any SDK operation starts.
                    // Its private chunk remains with the read task until completion/exit.
                    int size = (int)Math.Min(65536, maximum - result.Length + 1);
                    var pending = Task.Run(() =>
                    {
                        var bytes = new byte[size];
                        try
                        {
                            int count = input.Read(bytes, 0, bytes.Length);
                            return (bytes, count);
                        }
                        catch
                        {
                            CryptographicOperations.ZeroMemory(bytes);
                            throw;
                        }
                    });
                    (byte[] bytes, int count) chunk;
                    try
                    {
                        chunk = await pending.WaitAsync(token);
                    }
                    catch
                    {
                        if (!pending.IsCompleted)
                            state.PendingAdmission = true;
                        _ = pending.ContinueWith(completed =>
                        {
                            if (completed.Status == TaskStatus.RanToCompletion)
                                CryptographicOperations.ZeroMemory(completed.Result.bytes);
                            else
                                _ = completed.Exception;
                        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                        throw;
                    }

                    try
                    {
                        if (chunk.count == 0)
                            break;
                        if (result.Length > maximum - chunk.count)
                            throw new CliFailure("CLI.InputLimit", 4);
                        result.Write(chunk.bytes, 0, chunk.count);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(chunk.bytes);
                    }
                }
            }
            else
            {
                await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var bytes = new byte[65536];
                try
                {
                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        int count = await input.ReadAsync(bytes.AsMemory(0, (int)Math.Min(bytes.Length, maximum - result.Length + 1)), token);
                        if (count == 0)
                            break;
                        if (result.Length > maximum - count)
                            throw new CliFailure("CLI.InputLimit", 4);
                        result.Write(bytes, 0, count);
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(bytes);
                }
            }

            result.Position = 0;
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    internal static async Task WriteStdout(byte[] payload, CancellationToken token)
    {
        var task = Task.Run(() =>
        {
            try
            {
                using var stream = StandardOutput();
                stream.Write(payload);
                stream.Flush();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(payload);
            }
        });
        try
        {
            await task.WaitAsync(token);
        }
        catch
        {
            _ = task.ContinueWith(t =>
            {
                _ = t.Exception;
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
    }

    private static Stream StandardOutput()
    {
        // Console's text-oriented stream can suppress broken-pipe errors. The
        // borrowed raw descriptor preserves write/flush failures for pipelines.
        if (!Console.IsOutputRedirected)
            return Console.OpenStandardOutput();
        IntPtr value = OperatingSystem.IsWindows() ? GetStdHandle(-11) : new IntPtr(1);
        var handle = new SafeFileHandle(value, ownsHandle: false);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new IOException();
        }

        try
        {
            return new FileStream(handle, FileAccess.Write, 4096, isAsync: false);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);
    internal static (int ExitCode, string Code) Classify(Exception error, CancellationToken global) => error switch
    {
        CliFailure failure => (failure.ExitCode, failure.Code),
        OperationCanceledException => global.IsCancellationRequested ? (130, "CLI.Canceled") : (4, "CLI.Timeout"),
        PdfException pdf => (pdf.Diagnostic.Code.StartsWith("Limit.", StringComparison.Ordinal) ? 4 : 3, SafeCode(pdf.Diagnostic.Code)),
        JsonException or ArgumentException => (2, "CLI.Configuration"),
        IOException or UnauthorizedAccessException or System.Security.SecurityException => (5, "CLI.IO"),
        _ => (1, "CLI.Failed")};
    private static string SafeCode(string value) => value.Length <= 128 && value.All(ch => ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '_') ? value : "SDK.Failed";
}
