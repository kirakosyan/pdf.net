using System.IO;
using System.Text;
using System.Text.Json;
using static Fluent.Pdf.Html.ChromiumProtocol;

namespace Fluent.Pdf.Html;
/// <summary>Original DevTools HTML/CSS/SVG/canvas/explicit-script printing over a caller-owned supervised Chromium host. No executable or runtime is discovered or downloaded.</summary>
public sealed class ChromiumHtmlRenderer : IPdfHtmlRenderer
{
    private readonly ChromiumConnectionFactory _factory;
    private readonly string _version, _revision;
    /// <summary>Requires a private host factory and exact expected four-part Chromium version and DevTools source revision. The actual Browser.getVersion response must match before document input is sent.</summary>
    public ChromiumHtmlRenderer(ChromiumConnectionFactory factory, string exactVersion, string exactRevision)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        if (exactVersion == null || exactVersion.Length > 64 || exactVersion.Split('.').Length != 4 || exactVersion.Split('.').Any(part => part.Length == 0 || part.Length > 10 || part.Any(c => c < '0' || c > '9')))
            throw new ArgumentException("An exact four-part Chromium version is required.", nameof(exactVersion));
        if (string.IsNullOrEmpty(exactRevision) || exactRevision.Length > 128 || exactRevision.Any(c => c < 33 || c > 126))
            throw new ArgumentException("An exact bounded browser source revision is required.", nameof(exactRevision));
        _version = exactVersion;
        _revision = exactRevision;
        Descriptor = new("chromium.html", exactVersion, PdfCapability.RenderHtml);
    }

    /// <summary>Gets the pinned declared engine identity. This does not declare platform or production qualification.</summary>
    public PdfProviderDescriptor Descriptor { get; }
    /// <summary>Gets the exact required browser product version.</summary>
    public string ExpectedVersion => _version;
    /// <summary>Gets the exact required browser source revision.</summary>
    public string ExpectedRevision => _revision;

    /// <summary>Creates a fresh off-the-record browser context, intercepts all HTTP resources, waits for load/fonts and streams PDF into the borrowed SDK lease.</summary>
    public void Render(PdfHtmlSource source, PdfHtmlOptions options, Stream pdfOutput, PdfOperationContext context)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (pdfOutput == null || !pdfOutput.CanWrite)
            throw new ArgumentException("A writable borrowed PDF output is required.", nameof(pdfOutput));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
        context.Consume(PdfResourceKind.ExpandedBytes, 4096);
        context.Consume(PdfResourceKind.Objects, 8);
        using var worker = context.EnterScope(PdfResourceKind.Workers);
        using var connection = _factory(context) ?? throw PdfWriterCore.Error(context, "Html.HostUnavailable", "The explicit browser host returned no connection.");
        var protocol = new ChromiumProtocol(connection, context);
        using (var observed = protocol.Call("Browser.getVersion"))
        {
            var version = Result(observed);
            string product = protocol.String(version, "product");
            if (product != "Chrome/" + _version && product != "HeadlessChrome/" + _version || protocol.String(version, "revision") != _revision || protocol.String(version, "protocolVersion") != "1.3")
                throw protocol.Error("EngineVersion", "The actual browser version, source revision or protocol differs from the caller's exact pin.");
        }

        string browserContext, target, session;
        using (var created = protocol.Call("Target.createBrowserContext", P(("disposeOnDetach", true))))
            browserContext = protocol.String(Result(created), "browserContextId");
        using (protocol.Call("Browser.setDownloadBehavior", P(("behavior", "deny"), ("browserContextId", browserContext))))
        {
        }

        using (var created = protocol.Call("Target.createTarget", P(("url", "about:blank"), ("browserContextId", browserContext))))
            target = protocol.String(Result(created), "targetId");
        using (var attached = protocol.Call("Target.attachToTarget", P(("targetId", target), ("flatten", true))))
            session = protocol.String(Result(attached), "sessionId");
        context.Consume(PdfResourceKind.ExpandedBytes, source.Resources.Count * 256L + 2048);
        var resources = source.Resources.ToDictionary(r => r.Address.AbsoluteUri, r => r, StringComparer.Ordinal);
        var idleLoaders = new HashSet<string>(StringComparer.Ordinal);
        protocol.Notification = Notify;
        Command("Target.setAutoAttach", P(("autoAttach", true), ("waitForDebuggerOnStart", true), ("flatten", true)));
        Command("Page.enable");
        Command("Page.setLifecycleEventsEnabled", P(("enabled", true)));
        Command("Runtime.enable");
        Command("Network.enable");
        Command("Network.setCacheDisabled", P(("cacheDisabled", true)));
        Command("Network.setBypassServiceWorker", P(("bypass", true)));
        Command("Emulation.setDeviceMetricsOverride", P(("width", options.ViewportWidth), ("height", options.ViewportHeight), ("deviceScaleFactor", 1), ("mobile", false)));
        Command("Emulation.setLocaleOverride", P(("locale", "en-US")));
        Command("Emulation.setTimezoneOverride", P(("timezoneId", "UTC")));
        Command("Emulation.setScriptExecutionDisabled", P(("value", !options.EnableJavaScript)));
        Command("Fetch.enable", P(("patterns", new object[] { P(("urlPattern", "*"), ("requestStage", "Request")) })));
        string frame, loader;
        using (var navigated = protocol.Call("Page.navigate", P(("url", source.Address.AbsoluteUri)), session))
        {
            var navigation = Result(navigated);
            if (navigation.TryGetProperty("errorText", out _))
                throw protocol.Error("Navigation", "The explicitly supplied document could not be navigated.");
            frame = protocol.String(navigation, "frameId");
            loader = protocol.String(navigation, "loaderId");
        }

        protocol.PumpUntil(() => idleLoaders.Contains(frame + ":" + loader));
        int world;
        using (var isolated = protocol.Call("Page.createIsolatedWorld", P(("frameId", frame), ("worldName", "fluent-pdf-readiness")), session))
        {
            var result = Result(isolated);
            if (!result.TryGetProperty("executionContextId", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out world))
                throw protocol.Error("Readiness", "The browser did not create the private inspection world.");
        }

        using (var ready = protocol.Call("Runtime.evaluate", P(("expression", "({fontsReady:document.fonts.status==='loaded',failedFonts:Array.from(document.fonts).filter(f=>f.status==='error').length})"), ("contextId", world), ("returnByValue", true)), session))
        {
            var result = Result(ready);
            if (result.TryGetProperty("exceptionDetails", out _))
                throw protocol.Error("Readiness", "The browser did not complete the explicit document/font readiness boundary.");
            var value = protocol.Object(protocol.Object(result, "result"), "value");
            if (!value.TryGetProperty("fontsReady", out var complete) || complete.ValueKind != JsonValueKind.True || !value.TryGetProperty("failedFonts", out var failed) || failed.ValueKind != JsonValueKind.Number || !failed.TryGetInt32(out int count) || count < 0)
                throw protocol.Error("Readiness", "The browser did not complete the explicit document/font readiness boundary.");
            if (count != 0)
                context.Report(new("Html.FontUnavailable", "A declared web font failed to load; the browser would substitute its fallback.", lossCategory: PdfLossCategory.SubstitutedContent));
        }

        string handle;
        using (var printed = protocol.Call("Page.printToPDF", P(("transferMode", "ReturnAsStream"), ("printBackground", options.PrintBackground), ("paperWidth", options.PageSize.Width / 72d), ("paperHeight", options.PageSize.Height / 72d), ("marginTop", 0), ("marginBottom", 0), ("marginLeft", 0), ("marginRight", 0), ("displayHeaderFooter", false)), session))
            handle = protocol.String(Result(printed), "stream");
        while (true)
        {
            using var read = protocol.Call("IO.read", P(("handle", handle), ("size", 65536)), session);
            var result = Result(read);
            string data = protocol.String(result, "data");
            if (data.Length > 87384)
                throw protocol.Error("OutputExtent", "A browser PDF chunk exceeds its requested extent.");
            context.Consume(PdfResourceKind.ExpandedBytes, data.Length * 2L + 128);
            byte[] bytes;
            try
            {
                bool encoded = result.TryGetProperty("base64Encoded", out var flag) && flag.ValueKind == JsonValueKind.True;
                if (encoded)
                    bytes = Convert.FromBase64String(data);
                else
                {
                    if (data.Any(c => c > 127))
                        throw protocol.Error("OutputEncoding", "Unencoded browser PDF chunks must be exact ASCII bytes.");
                    bytes = Encoding.ASCII.GetBytes(data);
                }
            }
            catch (FormatException)
            {
                throw protocol.Error("OutputEncoding", "The browser PDF chunk is not valid base64.");
            }

            if (bytes.Length > 65536)
                throw protocol.Error("OutputExtent", "The decoded browser PDF chunk exceeds its requested extent.");
            pdfOutput.Write(bytes, 0, bytes.Length);
            if (!result.TryGetProperty("eof", out var eof) || eof.ValueKind != JsonValueKind.True && eof.ValueKind != JsonValueKind.False)
                throw protocol.Error("OutputExtent", "Every browser PDF chunk must state whether EOF was reached.");
            if (eof.ValueKind == JsonValueKind.True)
                break;
            if (bytes.Length == 0)
                throw protocol.Error("OutputExtent", "The browser returned an empty nonfinal PDF chunk.");
        }

        Command("IO.close", P(("handle", handle)));
        protocol.Notification = null; // Intentional target shutdown is not an unexpected renderer crash.
        using (protocol.Call("Target.closeTarget", P(("targetId", target))))
        {
        }

        using (protocol.Call("Target.disposeBrowserContext", P(("browserContextId", browserContext))))
        {
        }

        context.Checkpoint();
        void Command(string method, Dictionary<string, object?>? parameters = null)
        {
            using var result = protocol.Call(method, parameters, session);
        }

        void Notify(JsonElement message)
        {
            if (message.TryGetProperty("sessionId", out var observedSession) && (observedSession.ValueKind != JsonValueKind.String || observedSession.GetString() != session))
                throw protocol.Error("TargetIdentity", "A browser notification belongs to another target session.");
            string method = protocol.String(message, "method");
            if (method == "Page.lifecycleEvent")
            {
                var state = protocol.Object(message, "params");
                if (protocol.String(state, "name") == "networkIdle")
                {
                    if (idleLoaders.Count == 128)
                        throw protocol.Error("NavigationExtent", "The browser lifecycle inventory exceeded its source profile.");
                    context.Consume(PdfResourceKind.ExpandedBytes, 512);
                    idleLoaders.Add(protocol.String(state, "frameId") + ":" + protocol.String(state, "loaderId"));
                }

                return;
            }

            if (method == "Inspector.detached" || method == "Inspector.targetCrashed" || method == "Target.targetCrashed")
                throw protocol.Error("BrowserTerminated", "The private browser target detached or crashed.");
            if (method == "Fetch.authRequired")
                throw protocol.Error("AuthenticationUnsupported", "The local-resource source profile does not authorize browser authentication.");
            if (method == "Target.attachedToTarget")
                throw protocol.Error("SubtargetUnsupported", "Worker or out-of-process child targets require a separately supported browser lifecycle profile.");
            if (method == "Runtime.exceptionThrown")
                throw protocol.Error("ScriptException", "Explicit document JavaScript failed; source payload is omitted from diagnostics.");
            if (method == "Page.javascriptDialogOpening")
                throw protocol.Error("DialogUnsupported", "Interactive JavaScript dialogs require a separately supported readiness policy.");
            if (method != "Fetch.requestPaused")
                return;
            var parameters = protocol.Object(message, "params");
            string requestId = protocol.String(parameters, "requestId");
            var request = protocol.Object(parameters, "request");
            string url = protocol.String(request, "url"), requestMethod = protocol.String(request, "method");
            if (requestMethod != "GET")
                throw protocol.Error("RequestMethod", "This explicit local-resource profile supports GET requests only.");
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
            {
                protocol.Acknowledge("Fetch.continueRequest", P(("requestId", requestId)), session);
                return;
            }

            string contentType, body;
            if (url == source.Address.AbsoluteUri && source.Markup != null)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, source.Markup.Length * 16L + 256);
                contentType = "text/html; charset=utf-8";
                body = Convert.ToBase64String(new UTF8Encoding(false, true).GetBytes(source.Markup));
            }
            else if (resources.TryGetValue(url, out var resource))
            {
                if (url == source.Address.AbsoluteUri && !new[]
                {
                    "text/html",
                    "application/xhtml+xml"
                }.Contains(resource.MediaType.Split(';')[0].Trim(), StringComparer.OrdinalIgnoreCase))
                    throw protocol.Error("MainMediaType", "The main resource must explicitly contain HTML or XHTML; another named input format is not substituted.");
                context.Consume(PdfResourceKind.ExpandedBytes, resource.Length * 6L + 256);
                var bytes = new byte[checked((int)resource.Length)];
                using var raw = resource.OpenContent();
                using var lease = context.BorrowExpandedInput(raw);
                int at = 0, count;
                while (at < bytes.Length && (count = lease.Read(bytes, at, Math.Min(65536, bytes.Length - at))) > 0)
                    at += count;
                if (at != bytes.Length || lease.ReadByte() != -1)
                    throw protocol.Error("ResourceExtent", "An immutable browser resource has an inconsistent extent.");
                contentType = resource.MediaType;
                body = Convert.ToBase64String(bytes);
            }
            else
            {
                if (parameters.TryGetProperty("resourceType", out var resourceType) && resourceType.ValueKind == JsonValueKind.String && resourceType.GetString() == "Other" && Uri.TryCreate(url, UriKind.Absolute, out var icon) && icon.GetLeftPart(UriPartial.Authority) == source.Address.GetLeftPart(UriPartial.Authority) && icon.AbsolutePath == "/favicon.ico" && icon.Query.Length == 0)
                {
                    // The browser's own tab icon is outside printed document content. Explicit image/script requests still follow the resource policy.
                    protocol.Acknowledge("Fetch.fulfillRequest", P(("requestId", requestId), ("responseCode", 204), ("body", "")), session);
                    return;
                }

                if (url == source.Address.AbsoluteUri)
                    throw protocol.Error("MainResourceMissing", "The explicitly addressed main HTML resource was not supplied.");
                context.Report(new("Html.ResourceMissing", "An HTML subresource was not explicitly supplied; no URL was fetched.", lossCategory: PdfLossCategory.OmittedContent));
                protocol.Acknowledge("Fetch.failRequest", P(("requestId", requestId), ("errorReason", "Aborted")), session);
                return;
            }

            protocol.Acknowledge("Fetch.fulfillRequest", P(("requestId", requestId), ("responseCode", 200), ("responseHeaders", new object[] { P(("name", "Content-Type"), ("value", contentType)), P(("name", "Access-Control-Allow-Origin"), ("value", "*")) }), ("body", body)), session);
        }
    }
}
