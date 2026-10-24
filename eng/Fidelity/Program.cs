using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Fluent.Pdf;

if (args.Length != 2) throw new ArgumentException("<corpus-directory> <fresh-output-directory>");
string corpus = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
if (Directory.Exists(output)) throw new IOException("Use a fresh output directory.");
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(corpus, "manifest.json")));
var cases = manifest.RootElement.GetProperty("fixtures").EnumerateArray().ToArray();
if (cases.Length == 0 || cases.Length > 1000) throw new InvalidDataException("A finite, nonempty corpus is required.");
Directory.CreateDirectory(output);
var records = new List<object>();
foreach (var item in cases)
{
    string name = item.GetProperty("file").GetString()!;
    if (Path.GetFileName(name) != name || !name.EndsWith(".pdf", StringComparison.Ordinal)) throw new InvalidDataException("Flat PDF fixture paths are required.");
    string source = Path.Combine(corpus, name), destination = Path.Combine(output, name);
    string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    if (new FileInfo(source).Length > 1048576 || Hash(source) != item.GetProperty("sha256").GetString()) throw new InvalidDataException("Fixture size/hash mismatch.");
    var policy = new PdfOperationPolicy(limits: new(maximumInputBytes: 1048576, maximumExpandedBytes: 16777216, maximumOutputBytes: 1048576, maximumObjects: 10000, maximumPixels: 1048576, maximumDepth: 64, maximumWorkers: 2, maximumElapsedTime: TimeSpan.FromSeconds(10)));
    using var input = File.OpenRead(source);
    using var document = Pdf.Open(input, new(operationPolicy: policy));
    document.SaveFile(destination);
    records.Add(new { file = name, inputSha256 = Hash(source), outputSha256 = Hash(destination), pages = document.Pages.Count });
}
File.WriteAllText(Path.Combine(output, "consumer.json"), JsonSerializer.Serialize(new { schemaVersion = 1, purpose = "source-fidelity-roundtrip", runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), sdkSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Pdf).Assembly.Location))).ToLowerInvariant(), records, productionQualified = false }, new JsonSerializerOptions { WriteIndented = true }));
