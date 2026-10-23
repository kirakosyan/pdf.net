using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace Fluent.Pdf.Formats;
/// <summary>Explicit reconstruction intent; neither profile infers source accessibility structure.</summary>
public enum PdfHtmlExportMode
{
    /// <summary>Self-contained PNG page appearances at explicit rendering DPI.</summary>
    FixedRaster,
    /// <summary>Unstyled inferred text; shared permissive policy is required for declared reconstruction losses.</summary>
    InferredText
}

/// <summary>Explicit HTML document identity, rendering and passive extraction choices.</summary>
public sealed class PdfHtmlExportOptions
{
    /// <summary>Captures one profile, a literal title and explicit language. No asset, font, script or network discovery occurs.</summary>
    public PdfHtmlExportOptions(PdfHtmlExportMode mode, string title, string language, PdfRenderOptions? rendering = null, PdfTextExtractionOptions? text = null)
    {
        if (!Enum.IsDefined(typeof(PdfHtmlExportMode), mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        Guard.Text(title, nameof(title), 4096);
        FormatIo.Language(language);
        XmlConvert.VerifyXmlChars(title);
        Rendering = rendering ?? new();
        if (Rendering.Region.HasValue)
            throw new ArgumentException("HTML page export requires whole-page rendering.", nameof(rendering));
        Mode = mode;
        Title = title;
        Language = language;
        Text = text ?? new(includeInvisibleText: false);
    }

    /// <summary>Gets selected profile.</summary>
    public PdfHtmlExportMode Mode { get; }
    /// <summary>Gets literal caller title.</summary>
    public string Title { get; }
    /// <summary>Gets explicit language tag.</summary>
    public string Language { get; }
    /// <summary>Gets whole-page raster policy.</summary>
    public PdfRenderOptions Rendering { get; }
    /// <summary>Gets passive text/layer grouping policy.</summary>
    public PdfTextExtractionOptions Text { get; }
}

/// <summary>Bounded offline HTML export with static CSP, escaped source text, explicit rasterization and declared text reconstruction losses.</summary>
public static class PdfHtmlExport
{
    /// <summary>Stages self-contained HTML5 before copying to a borrowed stream. Fixed raster export requires an explicit renderer; inferred text requires permissive loss policy.</summary>
    public static PdfOperationReport Write(PdfDocumentSnapshot source, IEnumerable<int> pages, Stream output, PdfHtmlExportOptions options, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || pages == null || options == null)
            throw new ArgumentNullException(source == null ? nameof(source) : pages == null ? nameof(pages) : nameof(options));
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed HTML stream is required.", nameof(output));
        providers ??= PdfProviderRegistry.Empty;
        return PdfFont.Run("ExportHtml", "managed.html-export", policy ?? source.Options.OperationPolicy, cancellationToken, c => FormatIo.Copy(Build(source, pages, options, providers, c), output, c), PdfCapability.ReadDocument);
    }

    /// <summary>Stages complete HTML before atomic file replacement.</summary>
    public static PdfOperationReport WriteFile(PdfDocumentSnapshot source, IEnumerable<int> pages, string path, PdfHtmlExportOptions options, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || pages == null || options == null)
            throw new ArgumentNullException(source == null ? nameof(source) : pages == null ? nameof(pages) : nameof(options));
        Guard.Text(path, nameof(path), 32767);
        providers ??= PdfProviderRegistry.Empty;
        return PdfFont.Run("ExportHtmlFile", "managed.html-export", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            var bytes = Build(source, pages, options, providers, c);
            PdfAtomicFile.Write(path, c, target => FormatIo.Copy(bytes, target, c));
        }, PdfCapability.ReadDocument);
    }

    internal static PdfByteStore Build(PdfDocumentSnapshot source, IEnumerable<int> pages, PdfHtmlExportOptions options, PdfProviderRegistry providers, PdfOperationContext c)
    {
        if (options.Mode == PdfHtmlExportMode.FixedRaster)
            _ = providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages);
        c.Consume(PdfResourceKind.Objects, 32);
        c.Consume(PdfResourceKind.ExpandedBytes, 16384);
        var body = new XElement("body");
        var root = new XElement("html", new XAttribute("lang", options.Language), new XElement("head", new XElement("meta", new XAttribute("charset", "utf-8")), new XElement("meta", new XAttribute("http-equiv", "Content-Security-Policy"), new XAttribute("content", "default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'")), new XElement("title", options.Title), new XElement("style", "html,body{margin:0;background:white}section{page-break-after:always;overflow:hidden}img{display:block;width:100%;height:100%}pre{white-space:pre-wrap;overflow-wrap:anywhere;margin:1em}")), body);
        int count = 0;
        using var graph = options.Mode == PdfHtmlExportMode.InferredText ? new PdfGraphEdit(source, new(allowSignatureInvalidation: true), c) : null;
        foreach (int page in pages)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 8);
            c.Consume(PdfResourceKind.ExpandedBytes, 4096);
            if (count == 512 || page < 0 || page >= source.Pages.Count)
                throw Error(c, "PageSelection", "Select 1–512 existing pages in explicit order.");
            var section = new XElement("section", new XAttribute("id", "page-" + (++count).ToString(CultureInfo.InvariantCulture)), new XAttribute("data-source-page", page), new XAttribute("aria-label", "PDF page " + (page + 1).ToString(CultureInfo.InvariantCulture)));
            if (options.Mode == PdfHtmlExportMode.FixedRaster)
            {
                c.Report(new("HtmlExport.RasterPage", "The selected page is rendered as pixels. Original text, navigation and accessibility semantics are not reconstructed.", severity: PdfDiagnosticSeverity.Information, pageIndex: page));
                var bounds = source.Pages[page].Geometry.DisplayBounds();
                section.Add(new XAttribute("style", "width:" + FormatIo.N(bounds.Width) + "pt;height:" + FormatIo.N(bounds.Height) + "pt"));
                var png = PdfRasterExport.Build(source, new[] { page }, new(PdfRasterFormat.Png, options.Rendering), providers, c);
                if (png.Length > 16777216)
                    throw Error(c, "ImageExtent", "A self-contained page image exceeds16 MiB.");
                c.Consume(PdfResourceKind.ExpandedBytes, png.Length * 6 + 512);
                string encoded = Convert.ToBase64String(FormatIo.Raw(png, c));
                section.Add(new XElement("img", new XAttribute("src", "data:image/png;base64," + encoded), new XAttribute("alt", "Rendered PDF page " + (page + 1).ToString(CultureInfo.InvariantCulture))));
            }
            else
            {
                c.Report(new("HtmlExport.InferredText", "Page content is reconstructed as unstyled inferred text. Original geometry, fonts, graphics, navigation and semantic structure are not retained.", lossCategory: PdfLossCategory.FidelityReduction, pageIndex: page));
                string text = ReadText(graph!, page, options.Text, providers, c);
                section.Add(new XElement("pre", new XText(text)));
            }

            body.Add(section);
        }

        if (count == 0)
            throw Error(c, "PageSelection", "A nonempty page selection is required.");
        return HtmlBytes(root, c);
    }

    internal static string ReadText(PdfGraphEdit graph, int page, PdfTextExtractionOptions options, PdfProviderRegistry providers, PdfOperationContext c)
    {
        string text = new PdfTextExtractionEngine(graph, options, providers).Read(page).Text;
        try
        {
            XmlConvert.VerifyXmlChars(text);
        }
        catch (XmlException)
        {
            c.Checkpoint();
            throw Error(c, "Unicode", "Source text contains characters that cannot be represented by the selected markup profile.");
        }

        c.Consume(PdfResourceKind.ExpandedBytes, text.Length * 4L + 128);
        return text;
    }

    private static PdfByteStore HtmlBytes(XElement root, PdfOperationContext c)
    {
        using var output = new PdfCodecBufferStream(c, 67108864);
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false, true), OmitXmlDeclaration = true, CloseOutput = false }))
        {
            writer.WriteDocType("html", null, null, null);
            Element(root);
            writer.Flush();
            void Element(XElement element)
            {
                c.Checkpoint();
                writer.WriteStartElement(element.Name.LocalName);
                foreach (var attribute in element.Attributes())
                    writer.WriteAttributeString(attribute.Name.LocalName, attribute.Value);
                foreach (var node in element.Nodes())
                {
                    if (node is XElement child)
                        Element(child);
                    else if (node is XText text)
                        writer.WriteString(text.Value);
                }

                if (element.Name.LocalName == "img" || element.Name.LocalName == "meta")
                    writer.WriteEndElement();
                else
                    writer.WriteFullEndElement();
            }
        }

        return output.Finish();
    }

    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "HtmlExport." + code, message);
}
