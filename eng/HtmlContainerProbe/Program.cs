using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Net;
using Fluent.Pdf;
using Fluent.Pdf.Html;

if (args.Length != 1) throw new ArgumentException("Supply an explicit source fixture manifest.");
using var settings = JsonDocument.Parse(File.ReadAllText(args[0]));
var root = settings.RootElement;
string Get(string key) => root.GetProperty(key).GetString()!;
var options = new ChromiumContainerOptions(Get("dockerExecutable"), Get("dockerSha256"), Get("daemonEndpoint"), Get("imageId"), Get("emptyConfigurationDirectory"), Get("seccompProfile"), Get("seccompSha256"), 536870912, 256, TimeSpan.FromSeconds(30));
string output = Get("outputDirectory");
Directory.CreateDirectory(output);
const string html = "<!doctype html><html><head><meta charset='utf-8'><style>body{margin:0;font:20px serif}.block{width:120px;height:40px;background:#00ff00}</style></head><body><div class='block'>ABC</div><p>Original browser source fixture</p></body></html>";
var observer = new VersionObserver(options, output);
var policy = new PdfOperationPolicy(limits: new(maximumExpandedBytes: 2147483648, maximumElapsedTime: TimeSpan.FromSeconds(30)));
using (var document = PdfHtml.Render(PdfHtmlSource.FromHtml(html), new(observer), policy: policy))
{
    document.SaveFile(Path.Combine(output, "original.pdf"));
    string text = string.Join("\n", Enumerable.Range(0, document.Pages.Count).Select(n => PdfTextExtraction.ReadPage(document.CreateSnapshot(), n).Text));
    if (!text.Contains("ABC", StringComparison.Ordinal) || !text.Contains("Original browser source fixture", StringComparison.Ordinal)) throw new InvalidDataException("Original source text was lost.");
    File.WriteAllText(Path.Combine(output, "text.txt"), text);
}
var captures = new List<object>();
ChromiumHtmlRenderer Renderer(bool mutate = false, HtmlResourceSessionFactory? resources = null) => new(c => mutate ? new MutationFixture(ChromiumContainerHost.Open(options, c)) : ChromiumContainerHost.Open(options, c), observer.Version!, observer.Revision!, resources);
const string prefix = "<!doctype html><html><head><meta charset='utf-8'><style>body{margin:0;background:white}</style></head><body>";
HtmlElementCapture Capture(string name, string markup, HtmlElementRequest selection, double x, double y, double width, double height, ChromiumHtmlRenderer? renderer = null, PdfHtmlSource? addressed = null)
{
    var result = (renderer ?? Renderer()).Capture(addressed ?? PdfHtmlSource.FromHtml(prefix + markup + "</body></html>"), selection, policy: policy);
    var b = result.DocumentBounds;
    if (Math.Abs(b.X-x)>.001 || Math.Abs(b.Y-y)>.001 || Math.Abs(b.Width-width)>.001 || Math.Abs(b.Height-height)>.001) throw new InvalidDataException("Original geometry differs: " + name);
    using (var png = result.OpenPng()) using (var destination = File.Create(Path.Combine(output, name + ".png"))) png.CopyTo(destination);
    captures.Add(new { name, result.ViewportBounds, result.DocumentBounds, result.CaptureBounds, markup = result.Markup, result.BaseAddress, outputSha256 = Hash(Path.Combine(output, name + ".png")) });
    return result;
}
Capture("nested-rotation", "<div style='position:absolute;left:30px;top:40px;transform:translate(10px,20px)'><div id='target' style='position:absolute;left:5px;top:7px;width:40px;height:20px;transform:rotate(90deg);transform-origin:0 0;background:#00ff00'></div></div>", new("#target"),25,67,20,40);
Capture("ancestor-overflow", "<div style='position:absolute;left:10px;top:20px;width:30px;height:30px;overflow:hidden'><div id='target' style='width:60px;height:40px;background:blue'></div></div>",new("#target"),10,20,60,40);
Capture("offscreen-vh", "<div id='target' style='position:absolute;left:5px;top:1200px;width:80px;height:10vh;background:red'></div>",new("#target"),5,1200,80,72);
var block = Capture("literal-block", "<div style='position:absolute;left:10px;top:10px;width:150px;height:30px'>Original literal block</div>",new(literalText:"literal block",maximumMarkupCharacters:4096),10,10,150,30);
if (block.Markup == null || !block.Markup.Contains("Original literal block",StringComparison.Ordinal)) throw new InvalidDataException("Explicit markup was lost.");
var errors = new List<object>();
void Reject(string name, string markup, HtmlElementRequest selection, string code, bool mutate = false, ChromiumHtmlRenderer? renderer = null)
{
    try { _ = (renderer ?? Renderer(mutate)).Capture(PdfHtmlSource.FromHtml(prefix+markup+"</body></html>"),selection,policy:policy); throw new InvalidDataException("Required failure was accepted: "+name); }
    catch(PdfException failure) { if(failure.Diagnostic.Code != code) throw; errors.Add(new { name, code }); }
}
Reject("missing","<div>Original</div>",new("#missing"),"Html.ElementMissing");
Reject("ambiguous","<div>A</div><div>B</div>",new("div"),"Html.ElementAmbiguous");
Reject("invalid-selector","<div>A</div>",new("["),"Html.ElementSelector");
Reject("search-extent","<div>A</div>",new("div",maximumNodes:1),"Html.ElementSearchExtent");
Reject("pixel-extent","<div style='width:20px;height:20px'>A</div>",new("div",maximumPixels:1),"Html.ElementPixels");
Reject("markup-extent","<div>A</div>",new("div",maximumMarkupCharacters:2),"Html.ElementMarkupExtent");
Reject("dom-replacement","<div id='target' style='width:20px;height:20px'>A</div>",new("#target"),"Html.ElementChanged",mutate:true);
var transport = new AuthenticationFixture();
var loader = new HtmlResourceLoader(new(new[] {new HtmlOriginRule(new("https://original.invalid/"), headers:new[] {new KeyValuePair<string,string>("Authorization","Bearer original-source-fixture")},initialCookies:new[] {"initial=original; Secure; Path=/"})}),new OriginalNames(),transport);
Capture("authenticated", "",new("#target"),0,0,30,20,Renderer(resources:loader.OpenSession),PdfHtmlSource.FromResource(new("https://original.invalid/main"),Array.Empty<PdfHtmlResource>()));
if(transport.MainRequests!=1 || transport.StyleRequests!=1) throw new InvalidDataException("Private authenticated managed resource fixture was not observed.");
Reject("origin-denied","<div id='target'>A</div><img src='https://denied.invalid/image.png'>",new("#target"),"Html.OriginDenied",renderer:Renderer(resources:loader.OpenSession));
File.WriteAllText(Path.Combine(output,"captures.json"),JsonSerializer.Serialize(new { productionQualified=false,captures,errors,authentication=new { transport.MainRequests,transport.StyleRequests, transport.SameContext } },new JsonSerializerOptions {WriteIndented=true}));
try
{
    _ = Renderer().Capture(PdfHtmlSource.FromHtml("<!doctype html><html><body>Original pending promise</body></html>"), new("body"),
        new(enableJavaScript:true,readiness:new(javaScriptPromise:"new Promise(()=>{})",timeout:TimeSpan.FromSeconds(1))),policy);
    throw new InvalidDataException("Pending element readiness did not time out.");
}
catch(PdfException failure) when(failure.Diagnostic.Code=="Limit.ElapsedTime") { }
var clock = Stopwatch.StartNew();
using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
{
    try
    {
        _ = Renderer().Capture(PdfHtmlSource.FromHtml("<!doctype html><html><body>Original pending promise</body></html>"), new("body"),
            new(enableJavaScript: true, readiness: new(javaScriptPromise: "new Promise(()=>{})", timeout: TimeSpan.FromSeconds(20))), policy, cancel.Token);
        throw new InvalidDataException("The explicit pending readiness promise did not cancel.");
    }
    catch (OperationCanceledException) { }
}
double cancellationMilliseconds = clock.Elapsed.TotalMilliseconds - 3000;
if (cancellationMilliseconds > 5000) throw new InvalidDataException("Private cancellation cleanup exceeded five seconds.");
var record = new { purpose = "actual-private-container-browser-original-source-observations", productionQualified = false, runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), imageId = options.ImageId, version = observer.Version, revision = observer.Revision, capturePromiseTimeoutObserved=true, capturePromiseCancellationObserved=true, cancellationMilliseconds, pdfSha256 = Hash(Path.Combine(output, "original.pdf")) };
File.WriteAllText(Path.Combine(output, "observations.json"), JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(record));
static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

// Original fixture-only intervention replaces the selected DOM node immediately before the production identity/geometry verification.
sealed class MutationFixture(IChromiumConnection inner) : IChromiumConnection
{
    public void Send(string message,PdfOperationContext context)
    {
        var json = JsonNode.Parse(message)!.AsObject();
        if(json["method"]!.GetValue<string>()=="Runtime.evaluate" && json["params"]?["expression"] is JsonValue expression && expression.GetValue<string>().StartsWith("({stable:",StringComparison.Ordinal))
            json["params"]!["expression"]="(()=>{const e=document.querySelector('#target');e.replaceWith(e.cloneNode(true));return {stable:!!globalThis.__fluentElement&&globalThis.__fluentElement.verify()}})()";
        inner.Send(json.ToJsonString(),context);
    }
    public string Receive(PdfOperationContext context)=>inner.Receive(context);
    public void Dispose()=>inner.Dispose();
}
sealed class OriginalNames : IHtmlNameResolver
{
    public IReadOnlyList<IPAddress> Resolve(string host,PdfOperationContext context)=>new[] { IPAddress.Parse("93.184.216.34") };
}
// Original in-memory authenticated responses: this observes browser interception plus the real managed jar/policy, not TLS or a remote network service.
sealed class AuthenticationFixture : IHtmlHttpTransport
{
    internal int MainRequests,StyleRequests;
    internal bool SameContext=true;
    private PdfOperationContext? _context;
    public HtmlHttpResponse Send(HtmlHttpRequest request,PdfOperationContext context)
    {
        _context??=context; SameContext &= ReferenceEquals(_context,context);
        if(request.Headers["Authorization"]!="Bearer original-source-fixture" || !request.Headers["Cookie"].Contains("initial=original",StringComparison.Ordinal)) throw new InvalidDataException("Original private authorization was lost.");
        if(request.Address.AbsolutePath=="/main")
        {
            MainRequests++;
            return new(200,"text/html",new MemoryStream(Encoding.UTF8.GetBytes("<!doctype html><html><head><link rel='stylesheet' href='/style.css'></head><body style='margin:0'><div id='target'></div></body></html>")),new[] {new KeyValuePair<string,string>("Set-Cookie","response=original; Secure; Path=/")},context);
        }
        if(request.Address.AbsolutePath!="/style.css" || !request.Headers["Cookie"].Contains("response=original",StringComparison.Ordinal)) throw new InvalidDataException("Original private resource cookie was lost.");
        StyleRequests++;
        return new(200,"text/css",new MemoryStream(Encoding.UTF8.GetBytes("#target{width:30px;height:20px;background:#ff00ff}")),null,context);
    }
}

sealed class VersionObserver(ChromiumContainerOptions options, string output) : IPdfHtmlRenderer
{
    public PdfProviderDescriptor Descriptor { get; } = new("fixture.private-container-observer", "1", PdfCapability.RenderHtml);
    internal string? Version, Revision;
    public void Render(PdfHtmlSource source, PdfHtmlOptions htmlOptions, Stream destination, PdfOperationContext context)
    {
        using (var worker = context.EnterScope(PdfResourceKind.Workers))
        using (var connection = ChromiumContainerHost.Open(options, context))
        {
            connection.Send("{\"id\":1,\"method\":\"Browser.getVersion\"}", context);
            using var response = JsonDocument.Parse(connection.Receive(context));
            var result = response.RootElement.GetProperty("result");
            Version = result.GetProperty("product").GetString()!.Split('/')[1];
            Revision = result.GetProperty("revision").GetString()!;
            if (result.GetProperty("protocolVersion").GetString() != "1.3") throw new InvalidDataException("Unsupported source browser protocol.");
        }
        // The fixture operator explicitly observes pins for this exact local image before sending its original document.
        new ChromiumHtmlRenderer(c => new OriginalFixtureObservation(ChromiumContainerHost.Open(options, c), output), Version, Revision).Render(source, htmlOptions, destination, context);
    }
}

// Explicit original-fixture diagnostics only; the SDK never records browser error payloads by default.
sealed class OriginalFixtureObservation(IChromiumConnection inner, string output) : IChromiumConnection
{
    public void Send(string message, PdfOperationContext context) => inner.Send(message, context);
    public string Receive(PdfOperationContext context)
    {
        string message = inner.Receive(context);
        using var json = JsonDocument.Parse(message);
        if (json.RootElement.TryGetProperty("error", out var error)) File.WriteAllText(Path.Combine(output, "original-fixture-cdp-error.json"), error.GetRawText());
        return message;
    }
    public void Dispose() => inner.Dispose();
}
