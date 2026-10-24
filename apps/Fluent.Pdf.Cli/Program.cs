using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Fluent.Pdf;

namespace Fluent.Pdf.CommandLine;
internal static class Program
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += handler;
        try
        {
            if (args.Length == 1 && args[0] == "--help")
            {
                Console.Error.WriteLine("fluent-pdf <text|select|rewrite|preflight|detect|convert|decrypt> --input <path|-> [--output <path|->] [--pages <range>] [--view raw|semantic|layout] [--target Svg|StaticEps] [--password-file <protected-path>] [--overwrite] [--allow-signature-invalidation] [--max-input-bytes n] [--max-output-bytes n] [--timeout-ms n]\nfluent-pdf formats [--output <path|->]\nfluent-pdf <run|batch> --config <closed-schema-json>\nfluent-pdf --version");
                return 0;
            }

            if (args.Length == 1 && args[0] == "--version")
            {
                await JobRunner.WriteStdout(JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, application = "fluent-pdf", runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), sdkTarget = typeof(PdfDocument).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName, productionQualified = false }, Json), cancellation.Token);
                return 0;
            }

            using var admission = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
            admission.CancelAfter(5000);
            var configuration = Task.Run(() => Configuration.Parse(args));
            Settings settings;
            try
            {
                settings = await configuration.WaitAsync(admission.Token);
            }
            catch
            {
                _ = configuration.ContinueWith(t =>
                {
                    _ = t.Exception;
                }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                throw;
            }

            var results = new Outcome[settings.Jobs.Length];
            int next = -1;
            var workers = Enumerable.Range(0, settings.Parallelism).Select(_ => Task.Run(async () =>
            {
                while (true)
                {
                    int index = Interlocked.Increment(ref next);
                    if (index >= results.Length)
                        return;
                    results[index] = await JobRunner.Run(settings.Jobs[index], settings, cancellation.Token);
                }
            })).ToArray();
            await Task.WhenAll(workers);
            int exit = cancellation.IsCancellationRequested ? 130 : settings.Batch && results.Any(r => r.ExitCode != 0) ? 6 : results[0].ExitCode;
            if (settings.Report != null || settings.Batch)
            {
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, exitCode = exit, jobs = results, productionQualified = false }, Json);
                try
                {
                    if (settings.Report != null)
                    {
                        var publication = new Publication();
                        try
                        {
                            using var data = new MemoryStream(bytes, false);
                            FilePublication.Publish(settings.Report, data, settings.OverwriteReport, publication, CancellationToken.None);
                        }
                        finally
                        {
                            FilePublication.Cleanup(publication);
                            if (publication.Cleanup == "failed")
                                throw new CliFailure("CLI.CleanupFailed", 5);
                        }
                    }

                    // Batch has no binary stdout jobs. Its data channel is the same JSON result manifest.
                    if (settings.Batch)
                    {
                        using var outputDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
                        outputDeadline.CancelAfter(settings.TimeoutMilliseconds);
                        await JobRunner.WriteStdout((byte[])bytes.Clone(), outputDeadline.Token);
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(bytes);
                }
            }

            foreach (var result in results)
                Console.Error.WriteLine(JsonSerializer.Serialize(result, Json));
            return exit;
        }
        catch (Exception error)
        {
            var failure = JobRunner.Classify(error, cancellation.Token);
            Console.Error.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, exitCode = failure.ExitCode, code = failure.Code }, Json));
            return failure.ExitCode;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }
}
