using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace Fluent.Pdf.Formats;
/// <summary>Explicit EPUB layout intent.</summary>
public enum PdfEbookLayout
{
    /// <summary>Fixed-layout raster page XHTML with embedded PNG assets.</summary>
    FixedRaster,
    /// <summary>Declared-loss inferred text chapters.</summary>
    ReflowText
}

/// <summary>Explicit publication identity; identifiers and timestamps are never generated from ambient state.</summary>
public sealed class PdfEbookMetadata
{
    /// <summary>Captures literal identifier/title, a bounded modern BCP47 language tag and exact UTC modification time (yyyy-MM-ddTHH:mm:ssZ).</summary>
    public PdfEbookMetadata(string identifier, string title, string language, string modifiedUtc)
    {
        Guard.Text(identifier, nameof(identifier), 2048);
        Guard.Text(title, nameof(title), 4096);
        FormatIo.Language(language);
        Guard.Text(modifiedUtc, nameof(modifiedUtc), 20);
        XmlConvert.VerifyXmlChars(identifier);
        XmlConvert.VerifyXmlChars(title);
        if (!DateTime.TryParseExact(modifiedUtc, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _))
            throw new ArgumentException("An explicit UTC modification timestamp is required.", nameof(modifiedUtc));
        Identifier = identifier;
        Title = title;
        Language = language;
        ModifiedUtc = modifiedUtc;
    }

    /// <summary>Gets literal publication identifier.</summary>
    public string Identifier { get; }
    /// <summary>Gets title.</summary>
    public string Title { get; }
    /// <summary>Gets explicit language.</summary>
    public string Language { get; }
    /// <summary>Gets exact caller UTC modification timestamp.</summary>
    public string ModifiedUtc { get; }
}

/// <summary>Explicit EPUB layout, page progression and rendering/extraction policy.</summary>
public sealed class PdfEbookOptions
{
    /// <summary>Captures a finite layout profile. Rendering requires whole pages. Reflow text needs permissive loss policy.</summary>
    public PdfEbookOptions(PdfEbookLayout layout, bool rightToLeft = false, PdfRenderOptions? rendering = null, PdfTextExtractionOptions? text = null)
    {
        if (!Enum.IsDefined(typeof(PdfEbookLayout), layout))
            throw new ArgumentOutOfRangeException(nameof(layout));
        Layout = layout;
        RightToLeft = rightToLeft;
        Rendering = rendering ?? new();
        if (Rendering.Region.HasValue)
            throw new ArgumentException("Ebook page export requires whole-page rendering.", nameof(rendering));
        Text = text ?? new(includeInvisibleText: false);
    }

    /// <summary>Gets selected layout.</summary>
    public PdfEbookLayout Layout { get; }
    /// <summary>Gets explicit page progression and XHTML direction.</summary>
    public bool RightToLeft { get; }
    /// <summary>Gets whole-page rendering policy.</summary>
    public PdfRenderOptions Rendering { get; }
    /// <summary>Gets passive text selection/grouping policy.</summary>
    public PdfTextExtractionOptions Text { get; }
}

/// <summary>Original bounded EPUB3 generation, passive reflow-text import and documented MobiXML source output. No DRM circumvention or book compilation occurs.</summary>
public static class PdfEbooks
{
    private static readonly XNamespace Opf = "http://www.idpf.org/2007/opf", Dc = "http://purl.org/dc/elements/1.1/", Html = "http://www.w3.org/1999/xhtml", Epub = "http://www.idpf.org/2007/ops", Container = "urn:oasis:names:tc:opendocument:xmlns:container";
    /// <summary>Stages an EPUB3 package with original manifest/spine/navigation, an uncompressed first mimetype entry, explicit metadata and PNG or escaped text chapters in exact caller page order.</summary>
    public static PdfOperationReport WriteEpub(PdfDocumentSnapshot source, IEnumerable<int> pages, Stream output, PdfEbookMetadata metadata, PdfEbookOptions options, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || pages == null || metadata == null || options == null)
            throw new ArgumentNullException(nameof(source));
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed ebook stream is required.", nameof(output));
        providers ??= PdfProviderRegistry.Empty;
        return PdfFont.Run("ExportEpub", "managed.epub3", policy ?? source.Options.OperationPolicy, cancellationToken, c => FormatIo.Copy(Build(source, pages, metadata, options, providers, c), output, c), PdfCapability.ReadDocument);
    }

    internal static PdfByteStore Build(PdfDocumentSnapshot source, IEnumerable<int> pages, PdfEbookMetadata metadata, PdfEbookOptions options, PdfProviderRegistry providers, PdfOperationContext c)
    {
        if (options.Layout == PdfEbookLayout.FixedRaster)
            _ = providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages);
        c.Consume(PdfResourceKind.Objects, 256);
        c.Consume(PdfResourceKind.ExpandedBytes, 65536);
        var parts = new List<(string Name, byte[] Bytes, bool Stored)>
        {
            ("mimetype", Encoding.ASCII.GetBytes("application/epub+zip"), true)
        };
        void Add(string name, XElement root) => parts.Add((name, FormatIo.Raw(FormatIo.XmlBytes(root, c), c), false));
        Add("META-INF/container.xml", new XElement(Container + "container", new XAttribute("version", "1.0"), new XElement(Container + "rootfiles", new XElement(Container + "rootfile", new XAttribute("full-path", "EPUB/package.opf"), new XAttribute("media-type", "application/oebps-package+xml")))));
        var manifest = new XElement(Opf + "manifest", new XElement(Opf + "item", new XAttribute("id", "navigation"), new XAttribute("href", "nav.xhtml"), new XAttribute("media-type", "application/xhtml+xml"), new XAttribute("properties", "nav")));
        var spine = new XElement(Opf + "spine", new XAttribute("page-progression-direction", options.RightToLeft ? "rtl" : "ltr"));
        var navigation = new XElement(Html + "ol");
        int count = 0;
        using var graph = options.Layout == PdfEbookLayout.ReflowText ? new PdfGraphEdit(source, new(allowSignatureInvalidation: true), c) : null;
        foreach (int page in pages)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 64);
            c.Consume(PdfResourceKind.ExpandedBytes, 16384);
            if (count == 512 || page < 0 || page >= source.Pages.Count)
                throw Error(c, "PageSelection", "Select 1–512 existing pages in explicit order.");
            string number = (++count).ToString(CultureInfo.InvariantCulture), path = "page" + number + ".xhtml";
            var head = new XElement(Html + "head", new XElement(Html + "title", metadata.Title + " — " + number));
            var body = new XElement(Html + "body");
            if (options.Layout == PdfEbookLayout.FixedRaster)
            {
                c.Report(new("Ebook.RasterPage", "This explicit fixed-layout chapter is a rendered page image; original objects/accessibility/navigation are not reconstructed. Viewport dimensions are rounded to CSS pixels.", severity: PdfDiagnosticSeverity.Information, pageIndex: page));
                var bounds = source.Pages[page].Geometry.DisplayBounds();
                double cssWidth = Math.Ceiling(bounds.Width * 4 / 3), cssHeight = Math.Ceiling(bounds.Height * 4 / 3);
                if (cssWidth < 1 || cssHeight < 1 || cssWidth > 19200 || cssHeight > 19200)
                    throw Error(c, "Viewport", "A finite positive fixed page viewport is required.");
                int width = (int)cssWidth, height = (int)cssHeight;
                head.Add(new XElement(Html + "meta", new XAttribute("name", "viewport"), new XAttribute("content", "width=" + width.ToString(CultureInfo.InvariantCulture) + ",height=" + height.ToString(CultureInfo.InvariantCulture))), new XElement(Html + "style", "html,body{margin:0;padding:0;width:100%;height:100%}img{display:block;width:100%;height:100%}"));
                var png = PdfRasterExport.Build(source, new[] { page }, new(PdfRasterFormat.Png, options.Rendering), providers, c);
                string image = "images/page" + number + ".png";
                parts.Add(("EPUB/" + image, FormatIo.Raw(png, c), false));
                manifest.Add(new XElement(Opf + "item", new XAttribute("id", "image" + number), new XAttribute("href", image), new XAttribute("media-type", "image/png")));
                body.Add(new XElement(Html + "img", new XAttribute("src", image), new XAttribute("alt", "Rendered PDF page " + (page + 1).ToString(CultureInfo.InvariantCulture))));
            }
            else
            {
                TextLoss(c, page);
                string text = PdfHtmlExport.ReadText(graph!, page, options.Text, providers, c);
                head.Add(new XElement(Html + "style", "pre{white-space:pre-wrap}"));
                body.Add(new XElement(Html + "pre", new XText(text)));
            }

            Add("EPUB/" + path, new XElement(Html + "html", new XAttribute("lang", metadata.Language), new XAttribute(XNamespace.Xml + "lang", metadata.Language), new XAttribute("dir", options.RightToLeft ? "rtl" : "ltr"), head, body));
            manifest.Add(new XElement(Opf + "item", new XAttribute("id", "page" + number), new XAttribute("href", path), new XAttribute("media-type", "application/xhtml+xml")));
            spine.Add(new XElement(Opf + "itemref", new XAttribute("idref", "page" + number)));
            navigation.Add(new XElement(Html + "li", new XElement(Html + "a", new XAttribute("href", path), "Page " + (page + 1).ToString(CultureInfo.InvariantCulture))));
        }

        if (count == 0)
            throw Error(c, "PageSelection", "A nonempty page selection is required.");
        Add("EPUB/nav.xhtml", new XElement(Html + "html", new XAttribute("lang", metadata.Language), new XAttribute(XNamespace.Xml + "lang", metadata.Language), new XElement(Html + "head", new XElement(Html + "title", metadata.Title)), new XElement(Html + "body", new XElement(Html + "nav", new XAttribute(Epub + "type", "toc"), new XAttribute("id", "toc"), new XElement(Html + "h1", "Contents"), navigation))));
        Add("EPUB/package.opf", new XElement(Opf + "package", new XAttribute("version", "3.0"), new XAttribute("unique-identifier", "book-id"), new XAttribute("prefix", "rendition: http://www.idpf.org/vocab/rendition/#"), new XElement(Opf + "metadata", new XElement(Dc + "identifier", new XAttribute("id", "book-id"), metadata.Identifier), new XElement(Dc + "title", metadata.Title), new XElement(Dc + "language", metadata.Language), new XElement(Opf + "meta", new XAttribute("property", "dcterms:modified"), metadata.ModifiedUtc), new XElement(Opf + "meta", new XAttribute("property", "rendition:layout"), options.Layout == PdfEbookLayout.FixedRaster ? "pre-paginated" : "reflowable"), new XElement(Opf + "meta", new XAttribute("property", "rendition:spread"), "none")), manifest, spine));
        return FormatPackage.Write(parts, c);
    }

    /// <summary>Reconstructs EPUB3 text-only spine content with explicit caller typography. Encrypted/obfuscated resources, fixed layouts, active markup, external resources and unsupported elements reject; declared reconstruction loss requires permissive policy.</summary>
    public static PdfDocument ImportReflowText(Stream input, PdfFormatTextOptions text, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (text == null || providers == null)
            throw new ArgumentNullException(nameof(text));
        PdfDocument? result = null;
        var finish = new List<Action<PdfOperationReport>>();
        try
        {
            var report = PdfFont.Run("ImportEpubText", "managed.epub3", policy, cancellationToken, c =>
            {
                using var package = new FormatPackage(input, c);
                if (package.Text("mimetype") != "application/epub+zip")
                    throw Error(c, "Mimetype", "An EPUB package is required.");
                if (package.Has("META-INF/encryption.xml"))
                    throw Error(c, "EncryptedResources", "Encrypted or obfuscated resources require separate qualified handling; no DRM bypass occurs.");
                var container = package.Xml("META-INF/container.xml");
                var roots = container.Root?.Name == Container + "container" ? container.Root.Element(Container + "rootfiles")?.Elements(Container + "rootfile").Take(2).ToArray() ?? Array.Empty<XElement>() : Array.Empty<XElement>();
                if (roots.Length != 1 || (string? )roots[0].Attribute("media-type") != "application/oebps-package+xml")
                    throw Error(c, "Container", "One explicit EPUB package root is required.");
                string path = FormatPackage.Resolve("", (string? )roots[0].Attribute("full-path") ?? "", c);
                var book = package.Xml(path).Root;
                if (book?.Name != Opf + "package" || (string? )book.Attribute("version") != "3.0")
                    throw Error(c, "Version", "This source profile requires an EPUB3 package.");
                if (book.Element(Opf + "metadata")?.Elements(Opf + "meta").Any(e => ((string? )e.Attribute("property"))?.EndsWith(":layout", StringComparison.Ordinal) == true && e.Value != "reflowable") == true)
                    throw Error(c, "Layout", "Fixed-layout EPUB import requires another profile.");
                var items = new Dictionary<string, (string Path, string Type)>(StringComparer.Ordinal);
                var paths = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in book.Element(Opf + "manifest")?.Elements(Opf + "item") ?? Enumerable.Empty<XElement>())
                {
                    c.Checkpoint();
                    c.Consume(PdfResourceKind.Objects, 1);
                    c.Consume(PdfResourceKind.ExpandedBytes, 2048);
                    string id = (string? )item.Attribute("id") ?? "";
                    if (id.Length == 0 || id.Length > 256 || items.Count == 4096 || items.ContainsKey(id))
                        throw Error(c, "Identity", "Manifest IDs must be bounded and unique.");
                    string target = FormatPackage.Resolve(path, (string? )item.Attribute("href") ?? "", c);
                    if (!paths.Add(target))
                        throw Error(c, "Identity", "Manifest resource paths must be unique.");
                    items.Add(id, (target, (string? )item.Attribute("media-type") ?? ""));
                }

                var references = new List<XElement>();
                foreach (var reference in book.Element(Opf + "spine")?.Elements(Opf + "itemref") ?? Enumerable.Empty<XElement>())
                {
                    c.Checkpoint();
                    c.Consume(PdfResourceKind.Objects, 1);
                    c.Consume(PdfResourceKind.ExpandedBytes, 64);
                    if (references.Count == 512)
                        throw Error(c, "Spine", "The spine exceeds512 items.");
                    references.Add(reference);
                }

                if (references.Count < 1)
                    throw Error(c, "Spine", "A finite nonempty spine is required.");
                var blocks = new List<PdfFlowBlock>();
                foreach (var reference in references)
                {
                    c.Checkpoint();
                    if ((string? )reference.Attribute("linear") == "no" || ((string? )reference.Attribute("properties"))?.IndexOf("layout-pre-paginated", StringComparison.Ordinal) >= 0)
                        throw Error(c, "Spine", "Nonlinear spine reconstruction requires explicit selection in another profile.");
                    string id = (string? )reference.Attribute("idref") ?? "";
                    if (!items.TryGetValue(id, out var item) || item.Type != "application/xhtml+xml")
                        throw Error(c, "Spine", "Every selected spine item must resolve to XHTML.");
                    var chapter = package.Xml(item.Path).Root;
                    if (chapter?.Name != Html + "html")
                        throw Error(c, "Xhtml", "A well-formed XHTML chapter is required.");
                    ValidateChapter(chapter, item.Path, c);
                    TextLoss(c, null);
                    var body = chapter.Element(Html + "body") ?? throw Error(c, "Xhtml", "A chapter body is required.");
                    long units = 0, nodes = 0;
                    foreach (var node in body.DescendantNodes())
                    {
                        c.Checkpoint();
                        nodes++;
                        if (node is XText value)
                            units += value.Value.Length;
                    }

                    c.Consume(PdfResourceKind.ExpandedBytes, units * 8 + nodes * 16 + 4096);
                    var flat = new StringBuilder();
                    Flatten(body, flat, c);
                    var paragraphs = flat.ToString().Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string paragraph in paragraphs)
                        if (paragraph.Length > 1000000)
                            throw Error(c, "TextExtent", "A text paragraph exceeds the finite flow profile.");
                    blocks.AddRange(FormatText.Paragraphs(paragraphs.Where(p => !string.IsNullOrWhiteSpace(p)), text, c));
                }

                if (blocks.Count == 0)
                    throw Error(c, "EmptyText", "A successful text import must contain supported text; no empty substitute is returned.");
                result = FormatText.Build(blocks, text, providers, c, finish);
            }, PdfCapability.WriteDocument);
            foreach (var complete in finish)
                complete(report);
            result!.SetCompositionReport(report);
            return result;
        }
        catch
        {
            result?.Dispose();
            throw;
        }
    }

    /// <summary>Writes escaped text-only MobiXML source with literal metadata and explicit page breaks. This does not compile a Mobi/KF8 book, bypass DRM or assert fixed-layout/typographic fidelity.</summary>
    public static PdfOperationReport WriteMobiSource(PdfDocumentSnapshot source, IEnumerable<int> pages, Stream output, PdfEbookMetadata metadata, PdfTextExtractionOptions? text = null, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || pages == null || metadata == null)
            throw new ArgumentNullException(nameof(source));
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed source stream is required.", nameof(output));
        text ??= new(includeInvisibleText: false);
        providers ??= PdfProviderRegistry.Empty;
        return PdfFont.Run("ExportMobiSource", "managed.mobixml", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            c.Consume(PdfResourceKind.Objects, 32);
            c.Consume(PdfResourceKind.ExpandedBytes, 16384);
            using var graph = new PdfGraphEdit(source, new(allowSignatureInvalidation: true), c);
            var body = new XElement("body");
            var root = new XElement("html", new XAttribute(XNamespace.Xmlns + "mbp", "http://mobipocket.com/ns/mbp"), new XAttribute("lang", metadata.Language), new XElement("head", new XElement("title", metadata.Title), new XElement("meta", new XAttribute("name", "identifier"), new XAttribute("content", metadata.Identifier))), body);
            int count = 0;
            XNamespace mbp = "http://mobipocket.com/ns/mbp";
            foreach (int page in pages)
            {
                c.Checkpoint();
                c.Consume(PdfResourceKind.Objects, 4);
                c.Consume(PdfResourceKind.ExpandedBytes, 2048);
                if (count++ == 512 || page < 0 || page >= source.Pages.Count)
                    throw Error(c, "PageSelection", "Select 1–512 existing pages in explicit order.");
                TextLoss(c, page);
                if (count > 1)
                    body.Add(new XElement(mbp + "pagebreak"));
                body.Add(new XElement("pre", new XText(PdfHtmlExport.ReadText(graph, page, text, providers, c))));
            }

            if (count == 0)
                throw Error(c, "PageSelection", "A nonempty selection is required.");
            FormatIo.Copy(FormatIo.XmlBytes(root, c), output, c);
        }, PdfCapability.ReadDocument);
    }

    private static void ValidateChapter(XElement chapter, string owner, PdfOperationContext c)
    {
        var names = new HashSet<string>(new[] { "html", "head", "title", "meta", "style", "body", "p", "pre", "div", "section", "article", "span", "a", "br", "h1", "h2", "h3", "h4", "h5", "h6", "strong", "em", "b", "i" }, StringComparer.Ordinal);
        foreach (var element in chapter.DescendantsAndSelf())
        {
            c.Checkpoint();
            if (element.Name.Namespace != Html || !names.Contains(element.Name.LocalName) || element.Name.LocalName == "style" && (element.Parent?.Name != Html + "head" || element.Value != "pre{white-space:pre-wrap}"))
                throw Error(c, "UnsupportedMarkup", "Only the documented passive text chapter elements/styles are supported.");
            foreach (var attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration)
                    continue;
                string name = attribute.Name.LocalName;
                bool allowed = attribute.Name == XNamespace.Xml + "lang" || attribute.Name.NamespaceName == "" && (name == "lang" || name == "dir" || name == "id" || element.Name.LocalName == "a" && name == "href" || element.Name.LocalName == "meta" && (name == "name" || name == "content" || name == "charset"));
                if (!allowed)
                    throw Error(c, "UnsupportedMarkup", "Active, styled or unsupported chapter attributes reject.");
                if (element.Name.LocalName == "a" && name == "href" && !attribute.Value.StartsWith("#", StringComparison.Ordinal))
                {
                    string target = attribute.Value.Split('#')[0];
                    if (target.Length > 0)
                        _ = FormatPackage.Resolve(owner, target, c);
                }
            }
        }
    }

    private static void Flatten(XElement element, StringBuilder output, PdfOperationContext c)
    {
        using var depth = c.EnterScope(PdfResourceKind.Depth);
        c.Checkpoint();
        bool block = TextBlocks.Contains(element.Name.LocalName);
        if (block)
            output.Append("\n\n");
        foreach (var node in element.Nodes())
        {
            c.Checkpoint();
            if (node is XText text)
                output.Append(text.Value);
            else if (node is XElement child)
            {
                if (child.Name == Html + "br")
                    output.Append('\n');
                else
                    Flatten(child, output, c);
            }
        }

        if (block)
            output.Append("\n\n");
    }

    private static readonly HashSet<string> TextBlocks = new(new[] { "p", "pre", "div", "section", "article", "h1", "h2", "h3", "h4", "h5", "h6" }, StringComparer.Ordinal);
    private static void TextLoss(PdfOperationContext c, int? page) => c.Report(new("Ebook.TextReconstruction", "Content is reconstructed as inferred unstyled text; original layout, graphics, fonts, CSS/visibility, navigation, publication metadata and source semantics are not retained.", lossCategory: PdfLossCategory.FidelityReduction, pageIndex: page));
    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Ebook." + code, message);
}
