using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Fluent.Pdf.Compliance;

namespace Fluent.Pdf.Contract.Tests;

// Explicit original-fixture host; real independent validators, not a claim of qualified OS isolation or redistribution.
internal static class InvoiceValidationFixture
{
    internal static FacturX107MinimumReportValidator Validator(Action<string>? changeInputs = null, Action<string>? changeReport = null) => new((pdf, xml, model, report, context) => Run(pdf, xml, model, report, context, changeInputs, changeReport));
    internal static string Hash(string path)
    {
        using var input = File.OpenRead(path); using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
    }
    private static void Run(Stream pdf, Stream xml, Stream model, Stream report, PdfOperationContext c, Action<string>? changeInputs, Action<string>? changeReport)
    {
        string assets = Path.Combine(ArchivalFixture.Root(), "artifacts", "invoice-fixtures");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(assets, "manifest.json")));
        foreach (var file in manifest.RootElement.GetProperty("files").EnumerateArray())
        {
            c.Checkpoint(); c.Consume(PdfResourceKind.ExpandedBytes, 131072);
            string name = file.GetProperty("path").GetString()!;
            if (name.Contains("..") || Path.IsPathRooted(name) || Hash(Path.Combine(assets, name)) != file.GetProperty("sha256").GetString()) throw new InvalidDataException("Pinned source invoice host input changed.");
        }
        string directory = Path.Combine(ArchivalFixture.Root(), "artifacts", "contracts", "invoice-validation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        foreach (var pair in new[] { ("invoice.pdf", pdf), ("invoice.xml", xml), ("model.xml", model) })
        {
            using var output = File.Create(Path.Combine(directory, pair.Item1)); Copy(pair.Item2, output, c, pair.Item1.EndsWith(".pdf", StringComparison.Ordinal) ? 67108864 : 16777216);
        }
        changeInputs?.Invoke(directory);
        using var worker = c.EnterScope(PdfResourceKind.Workers);
        using var transport1 = c.EnterScope(PdfResourceKind.Workers);
        using var transport2 = c.EnterScope(PdfResourceKind.Workers);
        using var memory = c.Reserve(PdfResourceKind.ExpandedBytes, 536870912);
        string classpath = Path.Combine(assets, "classes") + Path.PathSeparator + Path.Combine(assets, "Mustang-CLI-2.26.0.jar");
        var arguments = new[] { "-Xmx384m", "-XX:MaxDirectMemorySize=64m", "-Djava.awt.headless=true", "-Dfluent.pdf.invoice.originalFixtureDiagnostics=true", "-Djava.net.useSystemProxies=false", "-cp", classpath, "InvoiceValidationHost", Path.Combine(directory, "invoice.pdf"), Path.Combine(directory, "invoice.xml"), Path.Combine(directory, "model.xml"), Path.Combine(directory, "report.xml"), Path.Combine(assets, "rules") };
        var start = new ProcessStartInfo("java", string.Join(" ", arguments.Select(Quote))) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = directory };
        using var process = Process.Start(start) ?? throw new IOException("Explicit source invoice host unavailable.");
        var stdout = Task.Run(() => { using var retained = File.Create(Path.Combine(directory, "stdout.log")); Copy(process.StandardOutput.BaseStream, retained, c, 1048576); });
        var stderr = Task.Run(() => { using var retained = File.Create(Path.Combine(directory, "stderr.log")); Copy(process.StandardError.BaseStream, retained, c, 1048576); });
        var clock = Stopwatch.StartNew();
        try
        {
            while (!process.WaitForExit(10)) { c.Checkpoint(); if (clock.Elapsed > TimeSpan.FromSeconds(45)) throw new TimeoutException("Independent source invoice deadline."); }
            while (!Task.WaitAll(new[] { stdout, stderr }, 10)) { c.Checkpoint(); if (clock.Elapsed > TimeSpan.FromSeconds(45)) throw new TimeoutException("Independent source invoice transport deadline."); }
            c.Checkpoint();
            File.WriteAllText(Path.Combine(directory, "host-status.json"), "{\"exitCode\":" + process.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"hostSourceSha256\":\"" + manifest.RootElement.GetProperty("hostSourceSha256").GetString() + "\",\"productionQualified\":false}");
            if (process.ExitCode != 0) throw new InvalidDataException("Independent original invoice fixture failed; inspect retained original-fixture diagnostics at " + directory);
            changeReport?.Invoke(directory);
            using var source = File.OpenRead(Path.Combine(directory, "report.xml")); Copy(source, report, c, 4194304);
        }
        finally
        {
            if (!process.HasExited) process.Kill();
            if (!process.WaitForExit(5000)) throw new IOException("Independent invoice host did not terminate.");
            try { if (!Task.WaitAll(new[] { stdout, stderr }, 5000)) throw new IOException("Independent invoice transports did not join."); }
            catch (AggregateException) { c.Checkpoint(); throw; }
        }
    }
    private static void Copy(Stream input, Stream output, PdfOperationContext c, long maximum)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 8192); var part = new byte[8192]; long total = 0; int count;
        while ((count = input.Read(part, 0, part.Length)) > 0) { c.Checkpoint(); if (count > maximum - total) throw new IOException("Source invoice fixture stream extent."); c.Consume(PdfResourceKind.ExpandedBytes, count); total += count; output.Write(part, 0, count); }
    }
    private static string Quote(string text) => "\"" + text.Replace("\"", "\\\"") + "\"";
}
