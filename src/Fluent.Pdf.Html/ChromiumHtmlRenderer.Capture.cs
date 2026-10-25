using System.IO;
using System.Text.Json;
using System.Threading;
using static Fluent.Pdf.Html.ChromiumProtocol;

namespace Fluent.Pdf.Html;

public sealed partial class ChromiumHtmlRenderer
{
    /// <summary>Captures one uniquely selected main-document element under the ordinary resource/readiness/host boundary. Missing, ambiguous, changing or oversized captures reject without retry or reflow.</summary>
    public HtmlElementCapture Capture(PdfHtmlSource source, HtmlElementRequest request, PdfHtmlOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || request == null) throw new ArgumentNullException(source == null ? nameof(source) : nameof(request));
        options ??= new(media: PdfHtmlMedia.Screen);
        var htmlOptions = options;
        HtmlElementCapture? captured = null;
        var report = PdfFont.Run("CaptureHtmlElement", Descriptor.Id, policy, cancellationToken, context =>
        {
            context.Consume(PdfResourceKind.Objects, source.Resources.Count + 1L);
            context.Consume(PdfResourceKind.ExpandedBytes, (source.Markup?.Length ?? 0) * 2L + source.Resources.Count * 128L + 512);
            context.ConsumePixels(htmlOptions.ViewportWidth, htmlOptions.ViewportHeight);
            Run(source, htmlOptions, null, context, (protocol, session, frame) => captured = CaptureCore(protocol, session, frame, request, context));
        }, PdfCapability.RenderHtml);
        captured!.OperationReport = report;
        return captured;
    }

    private static HtmlElementCapture CaptureCore(ChromiumProtocol protocol, string session, string frame, HtmlElementRequest request, PdfOperationContext context)
    {
        int world;
        using (var created = protocol.Call("Page.createIsolatedWorld", P(("frameId", frame), ("worldName", "fluent-pdf-element")), session))
        {
            if (!Result(created).TryGetProperty("executionContextId", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out world) || world <= 0) throw protocol.Error("ElementWorld", "The browser did not establish the private selection world.");
        }
        context.Consume(PdfResourceKind.Objects, request.MaximumNodes);
        context.Consume(PdfResourceKind.ExpandedBytes, request.MaximumNodes * 64L + request.MaximumMarkupCharacters * 12L + 32768);
        string settings = JsonSerializer.Serialize(P(("selector", request.Selector), ("text", request.LiteralText), ("nodes", request.MaximumNodes), ("markup", request.MaximumMarkupCharacters)));
        // Bounded traversal never materializes an unbounded selector result or textContent string. The state and observer live only in the isolated world.
        string select = "(()=>{const q=" + settings + ";let e=null,n=0,chars=0;const t=document.createTreeWalker(document,NodeFilter.SHOW_ELEMENT|NodeFilter.SHOW_TEXT);try{while(t.nextNode()){if(++n>q.nodes)return {error:'SearchExtent'};const a=t.currentNode;let match=false;if(q.selector!==null){if(a.nodeType===1)match=a.matches(q.selector)}else if(a.nodeType===3&&a.parentElement&&!['SCRIPT','STYLE','NOSCRIPT','TEMPLATE'].includes(a.parentElement.tagName)){chars+=a.length;if(a.length>65536||chars>1048576)return {error:'SearchExtent'};match=a.data.includes(q.text)}if(match){const x=a.nodeType===1?a:a.parentElement;if(e&&e!==x)return {error:'Ambiguous'};e=x}}}catch{return {error:'Selector'}}if(!e)return {error:'Missing'};if(!e.getClientRects().length)return {error:'Empty'};const d=document,r=e.getBoundingClientRect();let changes=0;const observer=new MutationObserver(()=>changes++);observer.observe(d,{subtree:true,childList:true,attributes:true,characterData:true});const geometry=()=>{const b=e.getBoundingClientRect();return [b.x,b.y,b.width,b.height,scrollX,scrollY,innerWidth,innerHeight,devicePixelRatio,visualViewport.scale]};const initial=geometry();let markup=null;if(q.markup){let count=0;const m=document.createTreeWalker(e,NodeFilter.SHOW_ALL);const check=a=>{if(a.nodeType!==1)count+=(a.nodeValue||'').length*6+7;else{count+=a.tagName.length*2+5;if(a.attributes.length>q.nodes)return false;for(const x of a.attributes)count+=x.name.length+x.value.length*6+4}return count<=q.markup};if(!check(e))return {error:'MarkupExtent'};let k=0;while(m.nextNode())if(++k>q.nodes||!check(m.currentNode))return {error:'MarkupExtent'};markup=e.outerHTML;if(markup.length>q.markup)return {error:'MarkupExtent'}}globalThis.__fluentElement={verify:()=>{const pending=observer.takeRecords().length;observer.disconnect();return d===document&&e.isConnected&&changes===0&&pending===0&&JSON.stringify(initial)===JSON.stringify(geometry())}};return {x:r.x,y:r.y,width:r.width,height:r.height,sx:scrollX,sy:scrollY,dpr:devicePixelRatio,scale:visualViewport.scale,address:document.URL,markup}})()";
        using var measured = Evaluate(select);
        var value = Value(measured);
        if (value.TryGetProperty("error", out var error))
        {
            string code = error.ValueKind == JsonValueKind.String ? error.GetString()! : "Selection";
            if (!new[] { "SearchExtent", "Ambiguous", "Selector", "Missing", "Empty", "MarkupExtent" }.Contains(code)) code = "Selection";
            throw protocol.Error("Element" + code, "The bounded unique element selection could not be captured; selector and document data are omitted.");
        }
        double x = Number(value, "x"), y = Number(value, "y"), width = Number(value, "width"), height = Number(value, "height"), sx = Number(value, "sx"), sy = Number(value, "sy");
        if (Number(value, "dpr") != 1 || Number(value, "scale") != 1 || width <= 0 || height <= 0) throw protocol.Error("ElementGeometry", "Element capture requires a positive border rectangle and CSS scale one.");
        string addressText = protocol.String(value, "address");
        if (addressText.Length > 8192 || !Uri.TryCreate(addressText, UriKind.Absolute, out var address) || (address.Scheme != "http" && address.Scheme != "https") || address.UserInfo.Length != 0) throw protocol.Error("ElementAddress", "The captured document has no supported credential-free resource address.");
        string? markup = value.TryGetProperty("markup", out var raw) && raw.ValueKind == JsonValueKind.String ? raw.GetString() : null;
        if (markup != null && (request.MaximumMarkupCharacters == 0 || markup.Length > request.MaximumMarkupCharacters)) throw protocol.Error("ElementMarkupExtent", "The extracted markup exceeds its explicit extent.");
        using var metrics = protocol.Call("Page.getLayoutMetrics", session: session);
        var surface = protocol.Object(Result(metrics), "cssContentSize");
        double left = Math.Max(Math.Floor(x + sx), Number(surface, "x")), top = Math.Max(Math.Floor(y + sy), Number(surface, "y"));
        double right = Math.Min(Math.Ceiling(x + sx + width), Number(surface, "x") + Number(surface, "width")), bottom = Math.Min(Math.Ceiling(y + sy + height), Number(surface, "y") + Number(surface, "height"));
        double w = right - left, h = bottom - top;
        if (w <= 0 || h <= 0 || w > 16384 || h > 16384 || w != Math.Floor(w) || h != Math.Floor(h) || w * h > request.MaximumPixels) throw protocol.Error("ElementPixels", "The document-clipped element exceeds its explicit integer capture area.");
        context.ConsumePixels((int)w, (int)h);
        // Admission precedes browser allocation. Native total memory is independently bounded by the supervised host.
        using var nativePixels = context.Reserve(PdfResourceKind.ExpandedBytes, checked((long)w * (long)h * 16L));
        using var encoded = context.Reserve(PdfResourceKind.ExpandedBytes, request.MaximumPngBytes * 12L + 4096);
        using var screenshot = protocol.Call("Page.captureScreenshot", P(("format", "png"), ("fromSurface", true), ("captureBeyondViewport", true), ("clip", P(("x", left), ("y", top), ("width", w), ("height", h), ("scale", 1)))), session);
        string data = protocol.String(Result(screenshot), "data");
        if (data.Length > ((request.MaximumPngBytes + 2L) / 3L) * 4L) throw protocol.Error("ElementOutputExtent", "The encoded element PNG exceeds its explicit ceiling.");
        byte[] png;
        try { png = Convert.FromBase64String(data); } catch (FormatException) { throw protocol.Error("ElementEncoding", "The browser element PNG encoding is invalid."); }
        if (png.Length > request.MaximumPngBytes || png.Length < 33 || !png.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || U32(16) != w || U32(20) != h) throw protocol.Error("ElementPng", "The element output is not a PNG with the admitted geometry.");
        context.Consume(PdfResourceKind.OutputBytes, png.Length);
        // Reuse the bounded original PNG reader for all chunk CRC/order/data checks, not just the browser's header claim.
        using var input = new MemoryStream(png, false);
        var bytes = PdfByteStore.Read(input, context);
        _ = PdfImages.ReadCore(bytes, new(), context);
        using var verified = Evaluate("({stable:!!globalThis.__fluentElement&&globalThis.__fluentElement.verify()})");
        var stable = Value(verified);
        if (!stable.TryGetProperty("stable", out var flag) || flag.ValueKind != JsonValueKind.True) throw protocol.Error("ElementChanged", "The document, selected node or geometry changed during capture; no result is exposed.");
        encoded.Complete(png.Length);
        return new(png, new(x, y, width, height), new(x + sx, y + sy, width, height), new(left, top, w, h), markup, address);

        uint U32(int p) => (uint)png[p] << 24 | (uint)png[p + 1] << 16 | (uint)png[p + 2] << 8 | png[p + 3];
        JsonDocument Evaluate(string expression) => protocol.Call("Runtime.evaluate", P(("expression", expression), ("contextId", world), ("returnByValue", true)), session);
        JsonElement Value(JsonDocument doc)
        {
            var result = Result(doc);
            if (result.TryGetProperty("exceptionDetails", out _)) throw protocol.Error("ElementInspection", "The browser rejected private element inspection; source data is omitted.");
            return protocol.Object(protocol.Object(result, "result"), "value");
        }
        double Number(JsonElement obj, string name)
        {
            if (!obj.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Number || !property.TryGetDouble(out double n) || double.IsNaN(n) || double.IsInfinity(n) || Math.Abs(n) > 1000000000) throw protocol.Error("ElementGeometry", "The browser returned unsupported element coordinates.");
            return n;
        }
    }
}
