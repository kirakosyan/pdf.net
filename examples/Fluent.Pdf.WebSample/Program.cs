using System.Text;
using Fluent.Pdf;
using Fluent.Pdf.Html;
using Fluent.Pdf.Workers;
using Fluent.Pdf.WebSample.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

var selfTest = args.Contains("--self-test", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(a => a != "--self-test").ToArray());
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 8388864;
    options.Limits.MaxConcurrentConnections = 16;
});
builder.Services.AddControllersWithViews();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
await using var app = builder.Build();
if (selfTest)
{
    // The view engines are real. This self-test checks their adapter boundary, not browser fidelity.
    var renderer = new BoundaryRenderer();
    foreach (var kind in new[]
    {
        PdfViewKind.Razor,
        PdfViewKind.Mvc,
        PdfViewKind.Blazor
    }

    )
    {
        using var scope = app.Services.CreateScope();
        using var document = await PdfWebViews.RenderAsync(new ApplicationView(scope.ServiceProvider, kind), new(renderer));
        if (document.Pages.Count != 1 || renderer.Markup == null || !renderer.Markup.Contains("Invoice &lt;25&gt;", StringComparison.Ordinal))
            throw new InvalidOperationException("Compiled view rendering/escaping failed.");
    }

    Console.WriteLine("Compiled Razor/MVC and Blazor static HTML adapter boundaries passed; browser/legacy/XAML qualification remains pending.");
    return;
}

app.MapGet("/pdf/{kind}", async (HttpContext request, string kind) =>
{
    if (!Enum.TryParse<PdfViewKind>(kind, true, out var selected) || selected != PdfViewKind.Razor && selected != PdfViewKind.Mvc && selected != PdfViewKind.Blazor)
        return Results.BadRequest("Select razor, mvc or blazor.");
    // The application must explicitly register its provisioned browser provider/host.
    var providers = request.RequestServices.GetService<PdfProviderRegistry>();
    if (providers == null || !providers.Supports(PdfCapability.RenderHtml))
        return Results.Problem("An explicit provisioned HTML renderer is required.", statusCode: 503);
    var policy = new PdfOperationPolicy(limits: new(maximumOutputBytes: 16777216));
    using var document = await PdfWebViews.RenderAsync(new ApplicationView(request.RequestServices, selected), providers, policy: policy, cancellationToken: request.RequestAborted);
    using var output = new MemoryStream();
    document.Save(output, request.RequestAborted);
    return Results.File(output.ToArray(), "application/pdf", "report.pdf");
});
app.MapPost("/worker/static-html", async (HttpContext request) =>
{
    // Explicit application provisioning supplies tenants/credentials and a real isolated renderer.
    var service = request.RequestServices.GetService<PdfRenderWorkerService>();
    if (service == null)
        return Results.Problem("An explicitly provisioned authenticated worker service is required.", statusCode: 503);
    try
    {
        using var input = new MemoryStream();
        var chunk = new byte[16384];
        int n;
        while ((n = await request.Request.Body.ReadAsync(chunk, request.RequestAborted)) > 0)
        {
            if (input.Length + n > 8388864)
                return Results.StatusCode(413);
            await input.WriteAsync(chunk.AsMemory(0, n), request.RequestAborted);
        }

        input.Position = 0;
        using var response = new MemoryStream();
        // The service has its own authenticated tenant processing context after bounded HTTP ingress.
        await Task.Run(() => service.Process(input, response, request.RequestAborted), request.RequestAborted);
        return Results.Bytes(response.ToArray(), "application/vnd.fluent-pdf.worker");
    }
    catch (PdfException error)
    {
        return Results.Problem(title: error.Diagnostic.Code, statusCode: error.Diagnostic.Code == "Worker.Authentication" ? 403 : error.Diagnostic.Code == "Worker.TenantQuota" ? 429 : 400);
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(408);
    }
    catch (Exception)
    {
        return Results.Problem(title: "Worker.HostFailed", statusCode: 500);
    }
});
await app.RunAsync();
internal sealed class ApplicationView(IServiceProvider services, PdfViewKind kind) : IPdfViewHost
{
    public PdfViewKind Kind => kind;
    public bool IsAvailable => true;

    public async Task RenderAsync(Stream output, PdfOperationContext context)
    {
        context.Checkpoint();
        if (kind == PdfViewKind.Blazor)
        {
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var markup = await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var component = await renderer.RenderComponentAsync<Report>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Text"] = "Invoice <25>" }));
                return component.ToHtmlString();
            });
            context.Consume(PdfResourceKind.ExpandedBytes, markup.Length * 4L + 64);
            var bytes = Encoding.UTF8.GetBytes(markup);
            await output.WriteAsync(bytes, context.CancellationToken);
        }
        else
        {
            var http = new DefaultHttpContext
            {
                RequestServices = services,
                RequestAborted = context.CancellationToken
            };
            var action = new ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(), new ActionDescriptor());
            var view = services.GetRequiredService<ICompositeViewEngine>().GetView(null, "/Views/Report.cshtml", true);
            if (!view.Success)
                throw new InvalidOperationException("The explicit compiled view is unavailable.");
            await using var writer = new StreamWriter(output, new UTF8Encoding(false), 4096, leaveOpen: true);
            var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = "Invoice <25>"
            };
            var temp = new TempDataDictionary(http, services.GetRequiredService<ITempDataProvider>());
            await view.View.RenderAsync(new ViewContext(action, view.View, data, temp, writer, new HtmlHelperOptions()));
            await writer.FlushAsync(context.CancellationToken);
        }

        context.Checkpoint();
    }
}

internal sealed class BoundaryRenderer : IPdfHtmlRenderer
{
    public PdfProviderDescriptor Descriptor { get; } = new("sample.view-boundary", "1", PdfCapability.RenderHtml);

    internal string? Markup;
    private readonly byte[] _pdf = Create();
    private static byte[] Create()
    {
        using var document = Pdf.Create().AddPage();
        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }

    public void Render(PdfHtmlSource source, PdfHtmlOptions options, Stream output, PdfOperationContext context)
    {
        Markup = source.Markup;
        output.Write(_pdf, 0, _pdf.Length);
    }
}
