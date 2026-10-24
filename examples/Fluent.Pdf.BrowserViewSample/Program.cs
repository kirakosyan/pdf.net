using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fluent.Pdf;
using Fluent.Pdf.Applications;
using Fluent.Pdf.ViewSamples;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

bool selfTest = args.Length == 1 && args[0] == "--self-test";
bool fixtureHost = args.Length == 1 && args[0] == "--self-test-host";
using var session = ViewerSampleSession.Open(selfTest || fixtureHost ? new[] { "--self-test" } : args);
var snapshot = session.Document.CreateSnapshot();
var builder = WebApplication.CreateBuilder(Array.Empty<string>());
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 16;
    options.Limits.MaxRequestBodySize = 0;
});
builder.Logging.ClearProviders();
await using var app = builder.Build();
app.Urls.Clear();
app.Urls.Add(selfTest || fixtureHost ? "http://127.0.0.1:0" : "http://127.0.0.1:5088");
string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
byte[] expectedToken = Encoding.ASCII.GetBytes(token);
using var requests = new SemaphoreSlim(4, 4);
var encodingPolicy = new PdfOperationPolicy(limits: new(maximumInputBytes: 16777216, maximumOutputBytes: 16777216));
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' blob:; connect-src 'self'; base-uri 'none'; frame-ancestors 'none'";
    context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    string host = context.Request.Host.Host;
    if (context.Connection.RemoteIpAddress == null || !IPAddress.IsLoopback(context.Connection.RemoteIpAddress) || host != "127.0.0.1" && host != "localhost" && host != "[::1]")
    {
        context.Response.StatusCode = 403;
        return;
    }

    string fetchSite = context.Request.Headers["Sec-Fetch-Site"].ToString();
    if (fetchSite.Length != 0 && fetchSite != "same-origin" && fetchSite != "none")
    {
        context.Response.StatusCode = 403;
        return;
    }

    if (context.Request.Path.StartsWithSegments("/api"))
    {
        string supplied = context.Request.Headers["X-Fluent-View-Token"].ToString();
        if (supplied.Length != 64 || supplied.Any(ch => !(ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f')) || !CryptographicOperations.FixedTimeEquals(expectedToken, Encoding.ASCII.GetBytes(supplied)))
        {
            context.Response.StatusCode = 403;
            return;
        }
    }

    await next(context);
});
app.MapGet("/", () => Results.File(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"), "text/html; charset=utf-8"));
app.MapGet("/viewer.js", () => Results.File(Path.Combine(AppContext.BaseDirectory, "wwwroot", "viewer.js"), "text/javascript; charset=utf-8"));
app.MapGet("/viewer.css", () => Results.File(Path.Combine(AppContext.BaseDirectory, "wwwroot", "viewer.css"), "text/css; charset=utf-8"));
app.MapGet("/api/page/{page:int}", async (HttpContext context, int page, double? zoom) =>
{
    if (page < 0 || page >= snapshot.Pages.Count || zoom.HasValue && (double.IsNaN(zoom.Value) || zoom.Value < .25 || zoom.Value > 4))
        return Results.BadRequest(new { code = "ApplicationView.InvalidView" });
    if (!await requests.WaitAsync(0, context.RequestAborted))
        return Results.StatusCode(429);
    try
    {
        // Independent private controller per authenticated local request; no shared payload cache.
        using var viewer = new PdfApplicationViewer(snapshot, session.Providers, maximumCachedBytes: 0, maximumSurfacePixels: 4000000);
        viewer.SetView(page, zoom ?? 1);
        var frame = await viewer.RenderAsync(context.RequestAborted);
        using var png = new MemoryStream();
        frame.WritePng(png, encodingPolicy, context.RequestAborted);
        return Results.Json(new { page, pageCount = snapshot.Pages.Count, zoom = frame.State.Zoom, width = frame.Raster.Request.Width, height = frame.Raster.Request.Height, capturedText = frame.CapturedText, png = Convert.ToBase64String(png.GetBuffer(), 0, checked((int)png.Length)) });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (PdfException error)
    {
        return Results.BadRequest(new { code = error.Diagnostic.Code });
    }
    catch (Exception)
    {
        return Results.Problem(title: "ApplicationView.Failed", statusCode: 500);
    }
    finally
    {
        requests.Release();
    }
});
await app.StartAsync();
string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
if (selfTest)
{
    using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
    {
        BaseAddress = new Uri(address),
        Timeout = TimeSpan.FromSeconds(20)
    };
    using var denied = await client.GetAsync("/api/page/0");
    if (denied.StatusCode != HttpStatusCode.Forbidden)
        throw new InvalidOperationException("Local document authority was not enforced.");
    client.DefaultRequestHeaders.Add("X-Fluent-View-Token", token);
    foreach (var item in new[]
    {
        (0, "Original automation Ω"),
        (1, "Second automation page")
    }

    )
    {
        using var response = await client.GetAsync("/api/page/" + item.Item1);
        response.EnsureSuccessStatusCode();
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var value = result.RootElement;
        if (value.GetProperty("capturedText").GetString() != item.Item2 || value.GetProperty("page").GetInt32() != item.Item1 || value.GetProperty("pageCount").GetInt32() != 2)
            throw new InvalidOperationException("Hosted viewer page/text boundary failed.");
        byte[] png = Convert.FromBase64String(value.GetProperty("png").GetString()!);
        if (png.Length < 32 || png[0] != 137 || png[1] != 80 || png[2] != 78 || png[3] != 71)
            throw new InvalidOperationException("Hosted viewer PNG boundary failed.");
        string directory = Path.Combine(AppContext.BaseDirectory, "application-browser-fixtures");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "page-" + item.Item1 + ".png"), png);
    }

    using var forbiddenOrigin = new HttpRequestMessage(HttpMethod.Get, "/api/page/0");
    forbiddenOrigin.Headers.Add("Sec-Fetch-Site", "cross-site");
    using var originResponse = await client.SendAsync(forbiddenOrigin);
    if (originResponse.StatusCode != HttpStatusCode.Forbidden)
        throw new InvalidOperationException("Cross-origin source read was not rejected.");
    Console.WriteLine("BROWSER_VIEW_HTTP_PASS;" + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + ";fixture-renderer;authority-and-pages");
    await app.StopAsync();
    return;
}

// Capability is in the fragment, never in the server request path/query or normal HTTP logs.
Console.WriteLine((fixtureHost ? "APPLICATION_VIEW_FIXTURE_HOST;" : "APPLICATION_VIEW_LOCAL_HOST;") + address + "/#" + token);
await app.WaitForShutdownAsync();
