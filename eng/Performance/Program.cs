using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Fluent.Pdf;

// Source measurement consumer. No source timing is promoted to a release baseline.
var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
string Hash(string path) { using var input = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant(); }
void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, json));
if (args.Length == 2 && args[0] == "create")
{
    var directory = Path.GetFullPath(args[1]);
    if (Directory.Exists(directory)) throw new IOException("Use a fresh corpus directory.");
    Directory.CreateDirectory(directory);
    var cases = new List<Case>();
    foreach (var (id, category, pages) in new[] { ("small", "small", 1), ("large", "large", 1000), ("scanned", "scanned", 4) })
    {
        using var document = Pdf.Create();
        PdfImageAsset? image = null;
        if (category == "scanned")
        {
            var pixels = Enumerable.Range(0, 512 * 512).Select(i => (byte)((i / 512 + i % 512) % 256)).ToArray();
            using var samples = new MemoryStream(pixels);
            image = PdfImages.FromSamples(samples, new(512, 512, 1, 8), PdfColorSpace.Gray);
        }
        for (int i = 0; i < pages; i++)
        {
            document.AddPage(new PdfPageSize(300 + i % 7, 400 + i % 11));
            if (image != null)
                PdfCanvas.Create(document.Pages[i].Geometry, c => c.DrawImageAsset(image, new(0, 0, 256, 256))).ApplyTo(document.Pages[i]);
        }
        string file = id + ".pdf", path = Path.Combine(directory, file);
        document.SaveFile(path);
        cases.Add(new(id, category, file, Hash(path), pages, 67108864, null));
    }
    var small = cases[0];
    cases.Add(small with { Id = "input-limit", Category = "adversarial", MaximumInputBytes = 1, ExpectedDiagnostic = "Limit.InputBytes" });
    Save(Path.Combine(directory, "corpus.json"), new { schemaVersion = 1, purpose = "original-source-performance-corpus", workflow = "managed-eager-open-first-page-model-and-atomic-rewrite", license = "MIT", cases, productionQualified = false });
    return;
}
if (args.Length != 5 || args[0] != "measure") throw new ArgumentException("Use create <fresh-directory>, or measure <case-json> <fresh-output-directory> <iterations> <warmups>.");
var specification = Path.GetFullPath(args[1]);
var selected = JsonSerializer.Deserialize<Case>(File.ReadAllText(specification), json) ?? throw new InvalidDataException("Missing case.");
var corpusRoot = Path.GetDirectoryName(specification)!;
var input = Path.GetFullPath(Path.Combine(corpusRoot, selected.File));
var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
if (Path.IsPathRooted(selected.File) || !input.StartsWith(corpusRoot + Path.DirectorySeparatorChar, comparison)) throw new InvalidDataException("Input escapes the corpus directory.");
if (new FileInfo(input).Length > 67108864 || Hash(input) != selected.Sha256) throw new InvalidDataException("Input size/hash mismatch.");
int iterations = int.Parse(args[3]), warmups = int.Parse(args[4]);
if (iterations is < 1 or > 10000 || warmups is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(args));
string destination = Path.GetFullPath(args[2]);
if (Directory.Exists(destination)) throw new IOException("Use a fresh measurement directory.");
Directory.CreateDirectory(destination);
var options = new PdfOptions(operationPolicy: new(limits: new(maximumInputBytes: selected.MaximumInputBytes, maximumOutputBytes: 67108864)));
var observations = new List<object>(iterations);
string output = Path.Combine(destination, "output.pdf");
using var process = Process.GetCurrentProcess();
for (int i = -warmups; i < iterations; i++)
{
    var timer = Stopwatch.StartNew();
    double? firstPageModelMs = null;
    string? diagnostic = null;
    PdfOperationReport? read = null, write = null;
    try
    {
        using var stream = File.OpenRead(input);
        using var document = Pdf.Open(stream, options);
        read = document.LastOperationReport;
        if (document.Pages.Count != selected.Pages || document.Pages[0].Size.Width <= 0) throw new InvalidDataException("Corpus page semantics differ.");
        firstPageModelMs = timer.Elapsed.TotalMilliseconds;
        write = document.SaveFile(output);
        if (selected.ExpectedDiagnostic != null) throw new InvalidDataException("Expected resource rejection did not occur.");
    }
    catch (PdfException error) when (error.Diagnostic.Code == selected.ExpectedDiagnostic)
    {
        diagnostic = error.Diagnostic.Code;
        if (File.Exists(output)) throw new InvalidDataException("Rejected input produced output.");
    }
    timer.Stop();
    process.Refresh();
    if (i >= 0) observations.Add(new { elapsedMs = timer.Elapsed.TotalMilliseconds, firstPageModelMs, diagnostic,
        peakWorkingSetBytes = process.PeakWorkingSet64, workingSetBytes = process.WorkingSet64,
        outputBytes = File.Exists(output) ? new FileInfo(output).Length : 0,
        resources = new { read = read?.Resources.Select(x => new { resource = x.Resource.ToString(), x.Consumed, x.Peak }), write = write?.Resources.Select(x => new { resource = x.Resource.ToString(), x.Consumed, x.Peak }) } });
}
Save(Path.Combine(destination, "measurements.json"), new { schemaVersion = 1, purpose = "source-performance-measurements", @case = selected,
    runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    processorCount = Environment.ProcessorCount, sdkSha256 = Hash(typeof(Pdf).Assembly.Location), consumerSha256 = Hash(typeof(Case).Assembly.Location),
    limits = options.OperationPolicy.Limits,
    warmups, iterations, state = warmups == 0 ? "process-cold-filesystem-cache-uncontrolled" : "process-warm-filesystem-cache-uncontrolled",
    firstPageMeaning = "Eager model availability; not selective range transfer, rendered pixels, browser layout or OCR.",
    memoryMeaning = "Process high-water working set including runtime/consumer/warmup; no child engines are used.",
    outputSha256 = File.Exists(output) ? Hash(output) : null, observations, productionQualified = false });

internal sealed record Case(string Id, string Category, string File, string Sha256, int Pages, long MaximumInputBytes, string? ExpectedDiagnostic);
