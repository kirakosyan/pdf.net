using Fluent.Pdf;
using Fluent.Pdf.Html;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

// Original trusted-fixture observer. This is not a deployable SDK isolation host or an untrusted-input runner.
if (args.Length != 2 || args[0] != "--trusted-original-fixtures")
    throw new ArgumentException("Supply --trusted-original-fixtures and an explicit manifest path. No asset or host discovery is performed.");
if (!OperatingSystem.IsLinux())
    throw new PlatformNotSupportedException("This source-fixture host has Linux applicability only. Other production lanes remain mandatory.");
var manifestPath = Path.GetFullPath(args[1]);
if (new FileInfo(manifestPath).Length > 65536)
    throw new InvalidDataException("The fixture manifest exceeds its bound.");
using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
var settings = manifest.RootElement;
string Text(string name) => settings.GetProperty(name).GetString() ?? throw new InvalidDataException("A fixture manifest string is absent.");
string Asset(string name)
{
    string path = Text(name);
    if (!Path.IsPathFullyQualified(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        throw new InvalidDataException("Fixture assets require absolute ordinary paths.");
    using var input = File.OpenRead(path);
    if (Convert.ToHexStringLower(SHA256.HashData(input)) != Text(name + "Sha256"))
        throw new InvalidDataException("A fixture asset differs from its explicit SHA-256 pin.");
    return path;
}

string browser = Asset("browserExecutable"), node = Asset("nodeExecutable"), fontPath = Asset("fontFile"), version = Text("browserVersion"), revision = Text("browserRevision");
string outputRoot = Text("outputDirectory"), temporaryParent = Text("temporaryParent");
if (!Path.IsPathFullyQualified(outputRoot) || !Path.IsPathFullyQualified(temporaryParent))
    throw new InvalidDataException("Explicit absolute output and temporary directories are required.");
Directory.CreateDirectory(outputRoot);
Directory.CreateDirectory(temporaryParent);
using var fontInput = File.OpenRead(fontPath);
var font = PdfHtmlResource.Read(new("https://fluent-pdf.invalid/font.ttf"), "font/ttf", fontInput);
const string html = """
<!doctype html><html><head><meta charset="utf-8"><style>
@font-face{font-family:Original;src:url('/font.ttf')}*{box-sizing:border-box}body{margin:0;font:24px Original}.grid{display:grid;grid-template-columns:100px 100px;gap:20px}.grid>div{height:40px;background:#f00}.flex{display:flex;gap:30px}.flex>div{width:100px;height:40px}svg,canvas{display:block}
</style></head><body><div class="grid"><div>A</div><div>B</div></div><div class="flex"><div>A</div><div>B</div></div><div>一😀</div><svg width="100" height="40"><rect width="100" height="40" fill="#00ff00"/></svg><canvas id="canvas" width="100" height="40"></canvas><div id="dynamic"></div><script>document.querySelector('#dynamic').textContent='B';const c=document.querySelector('#canvas').getContext('2d');c.fillStyle='#0000ff';c.fillRect(0,0,100,40)</script></body></html>
""";
File.WriteAllText(Path.Combine(outputRoot, "original.html"), html);
var observations = new List<object>();
foreach (bool enabled in new[]
{
    true,
    false
}

)
{
    var source = PdfHtmlSource.FromHtml(html, resources: new[] { font });
    var registry = new PdfProviderRegistry(new ChromiumHtmlRenderer(context => new FixtureConnection(node, browser, temporaryParent, context), version, revision));
    var policy = new PdfOperationPolicy(limits: new(maximumExpandedBytes: 2147483648, maximumElapsedTime: TimeSpan.FromSeconds(30)));
    using var document = PdfHtml.Render(source, registry, new(enableJavaScript: enabled), policy);
    string name = enabled ? "enabled" : "disabled";
    string rawPath = Path.Combine(outputRoot, name + ".pdf"), editedPath = Path.Combine(outputRoot, name + "-edited.pdf");
    document.SaveFile(rawPath);
    using var edited = PdfMetadata.Edit(document.CreateSnapshot(), e => e.SetInformation("Author", "Original HTML source fixture"));
    edited.SaveFile(editedPath);
    var text = PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0);
    var observation = new
    {
        javascript = enabled,
        pageCount = document.Pages.Count,
        width = document.Pages[0].Size.Width,
        height = document.Pages[0].Size.Height,
        text = text.Text,
        rawSha256 = Hash(rawPath),
        editedSha256 = Hash(editedPath)
    };
    observations.Add(observation);
    Console.WriteLine(JsonSerializer.Serialize(observation));
}

File.WriteAllText(Path.Combine(outputRoot, "observations.json"), JsonSerializer.Serialize(new { purpose = "trusted-original-source-fixtures-not-security-or-production-qualification", runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), version, revision, observations, productionQualified = false }, new JsonSerializerOptions { WriteIndented = true }));
if (settings.TryGetProperty("includeResourceFixtures", out var resourceFixtures) && resourceFixtures.GetBoolean())
    await ResourceFixture.ObserveAsync(outputRoot, context => new FixtureConnection(node, browser, temporaryParent, context), fontPath, version, revision);
static string Hash(string path)
{
    using var input = File.OpenRead(path);
    return Convert.ToHexStringLower(SHA256.HashData(input));
}

sealed class FixtureConnection : IChromiumConnection
{
    private readonly Process _process;
    private readonly Task _stderr;
    private readonly StreamWriter _input;
    private readonly StreamReader _output;
    private Task<string>? _reading;
    private bool _disposed;
    internal FixtureConnection(string node, string browser, string temporaryParent, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var start = new ProcessStartInfo(node)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "chromium-pipe-host.mjs"));
        start.ArgumentList.Add(browser);
        start.ArgumentList.Add(temporaryParent);
        _process = Process.Start(start) ?? throw new IOException("The explicit fixture host did not start.");
        _input = _process.StandardInput;
        _output = _process.StandardOutput;
        _stderr = Task.Run(() =>
        {
            var buffer = new char[1024];
            while (_process.StandardError.Read(buffer, 0, buffer.Length) > 0)
            {
            }
        });
    }

    public void Send(string message, PdfOperationContext context)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.ExpandedBytes, message.Length * 4L + 128);
        _input.WriteLine(message);
        _input.Flush();
        context.Checkpoint();
    }

    public string Receive(PdfOperationContext context)
    {
        _reading = Task.Run(() =>
        {
            context.Consume(PdfResourceKind.ExpandedBytes, 4096);
            var builder = new System.Text.StringBuilder();
            while (true)
            {
                int c = _output.Read();
                if (c < 0)
                    throw new EndOfStreamException("The private fixture browser reached EOF.");
                if (c == '\n')
                    return builder.ToString();
                if (builder.Length == 67108864)
                    throw new InvalidDataException("The private fixture message exceeds its bound.");
                context.Consume(PdfResourceKind.ExpandedBytes, 8);
                builder.Append((char)c);
            }
        });
        while (!_reading.IsCompleted)
        {
            context.CancellationToken.WaitHandle.WaitOne(25);
            context.Checkpoint();
        }

        context.Checkpoint();
        string result = _reading.GetAwaiter().GetResult();
        _reading = null;
        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _input.Close();
        if (!_process.WaitForExit(2000))
        {
            _process.Kill(entireProcessTree: true);
            if (!_process.WaitForExit(2000))
                throw new IOException("The private fixture process tree did not terminate.");
        }

        if (!_stderr.Wait(1000))
            throw new IOException("Fixture stderr work did not terminate.");
        if (_reading != null)
        {
            try
            {
                if (!_reading.Wait(1000))
                    throw new IOException("Fixture input work did not terminate.");
            }
            catch (AggregateException)
            { /* A prior read failure remains the enclosing operation's failure. All owned work is joined. */
            }
        }

        _output.Dispose();
        _process.Dispose();
    }
}
