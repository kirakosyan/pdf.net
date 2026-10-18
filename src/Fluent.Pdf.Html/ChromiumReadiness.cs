using System.Text.Json;
using static Fluent.Pdf.Html.ChromiumProtocol;

namespace Fluent.Pdf.Html;
internal static class ChromiumReadiness
{
    internal static void Wait(ChromiumProtocol protocol, string session, string frame, PdfHtmlReadinessOptions options, PdfOperationContext context, Func<bool> networkReady)
    {
        if (options.JavaScriptPromise != null)
        {
            using (var promised = protocol.Call("Runtime.evaluate", P(("expression", options.JavaScriptPromise), ("awaitPromise", true), ("returnByValue", false), ("generatePreview", false), ("objectGroup", "fluent-readiness")), session))
                if (Result(promised).TryGetProperty("exceptionDetails", out _))
                    throw protocol.Error("PromiseRejected", "The explicitly supplied readiness promise rejected; expression and document data are omitted.");
            using (protocol.Call("Runtime.releaseObjectGroup", P(("objectGroup", "fluent-readiness")), session))
            {
            }
        }

        int world;
        using (var isolated = protocol.Call("Page.createIsolatedWorld", P(("frameId", frame), ("worldName", "fluent-pdf-readiness")), session))
        {
            if (!Result(isolated).TryGetProperty("executionContextId", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out world) || world <= 0)
                throw protocol.Error("Readiness", "The browser did not create the private inspection world.");
        }

        context.Consume(PdfResourceKind.ExpandedBytes, options.Selectors.Sum(s => s.Length * 32L) + 8192);
        string selectors = JsonSerializer.Serialize(options.Selectors);
        string expression = "(()=>{const images=Array.from(document.images),fonts=Array.from(document.fonts);if(images.length>10000||fonts.length>10000)throw new RangeError();return {documentReady:document.readyState==='complete',selectorsReady:" + selectors + ".every(s=>document.querySelector(s)!==null),fontsReady:document.fonts.status==='loaded',failedFonts:fonts.filter(f=>f.status==='error').length,imagesReady:images.every(i=>i.complete),failedImages:images.filter(i=>i.complete&&i.naturalWidth===0).length,pendingImages:images.filter(i=>!i.complete).length}})()";
        for (int check = 0; check < 65536; check++)
        {
            context.Checkpoint();
            using var observed = protocol.Call("Runtime.evaluate", P(("expression", expression), ("contextId", world), ("returnByValue", true)), session);
            var result = Result(observed);
            if (result.TryGetProperty("exceptionDetails", out _))
                throw protocol.Error("Readiness", "The browser rejected bounded main-document readiness inspection; selectors and source data are omitted.");
            var value = protocol.Object(protocol.Object(result, "result"), "value");
            bool document = Flag(value, "documentReady"), matched = Flag(value, "selectorsReady"), fonts = Flag(value, "fontsReady"), images = Flag(value, "imagesReady");
            int failedFonts = Count(value, "failedFonts"), failedImages = Count(value, "failedImages"), pendingImages = Count(value, "pendingImages");
            if ((!options.WaitForDocumentComplete || document) && matched && (!options.WaitForFonts || fonts) && (!options.WaitForImages || images) && networkReady())
            {
                if (failedFonts != 0)
                    context.Report(new("Html.FontUnavailable", "A declared web font failed; the browser would substitute its fallback.", lossCategory: PdfLossCategory.SubstitutedContent));
                if (!fonts)
                    context.Report(new("Html.FontsPending", "Fonts were still loading at the explicitly selected readiness boundary.", lossCategory: PdfLossCategory.SubstitutedContent));
                if (failedImages != 0)
                    context.Report(new("Html.ImageUnavailable", "A declared image failed or has unsupported empty intrinsic geometry.", lossCategory: PdfLossCategory.OmittedContent));
                if (pendingImages != 0)
                    context.Report(new("Html.ImagesPending", "Images were still loading at the explicitly selected readiness boundary.", lossCategory: PdfLossCategory.OmittedContent));
                context.Checkpoint();
                return;
            }

            context.CancellationToken.WaitHandle.WaitOne(options.PollingInterval);
            context.Checkpoint();
        }

        throw protocol.Error("ReadinessExtent", "The bounded readiness inspection inventory was exhausted.");
        bool Flag(JsonElement value, string name)
        {
            if (!value.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False)
                throw protocol.Error("Readiness", "A browser readiness result omitted a required boolean state.");
            return property.ValueKind == JsonValueKind.True;
        }

        int Count(JsonElement value, string name)
        {
            if (!value.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out int count) || count < 0 || count > 10000)
                throw protocol.Error("Readiness", "A browser asset inventory exceeded its supported extent.");
            return count;
        }
    }
}
