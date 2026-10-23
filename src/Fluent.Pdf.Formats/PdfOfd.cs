using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace Fluent.Pdf.Formats;
/// <summary>Explicit OFD document identity. No random identifier, ambient date or metadata invention occurs.</summary>
public sealed class PdfOfdIdentity
{
    /// <summary>Captures a32-hex-digit document ID, literal title and ISO calendar creation date.</summary>
    public PdfOfdIdentity(string documentId, string title, string creationDate)
    {
        Guard.Text(documentId, nameof(documentId), 32);
        if (documentId.Length != 32 || documentId.Any(ch => !(ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f' || ch >= 'A' && ch <= 'F')))
            throw new ArgumentException("A32-hex-digit OFD identifier is required.", nameof(documentId));
        Guard.Text(title, nameof(title), 4096);
        XmlConvert.VerifyXmlChars(title);
        if (!DateTime.TryParseExact(creationDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new ArgumentException("An explicit ISO calendar date is required.", nameof(creationDate));
        DocumentId = documentId;
        Title = title;
        CreationDate = creationDate;
    }

    /// <summary>Gets literal source identifier.</summary>
    public string DocumentId { get; }
    /// <summary>Gets literal title.</summary>
    public string Title { get; }
    /// <summary>Gets ISO calendar creation date.</summary>
    public string CreationDate { get; }
}

/// <summary>Original OFD1.0 raster-page package interchange. No font/vector/signature reconstruction or standard-wide conformance claim.</summary>
public static class PdfOfd
{
    private static readonly XNamespace Ns = "http://www.ofdspec.org/2016";
    /// <summary>Stages an OFD1.0 ZIP with ordered PNG pages, physical mm geometry, explicit metadata and document-owned resource identities. No source signature is copied or asserted valid.</summary>
    public static PdfOperationReport WriteRasterPages(PdfDocumentSnapshot source, IEnumerable<int> pages, Stream output, PdfOfdIdentity identity, PdfRenderOptions rendering, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || pages == null || identity == null || rendering == null || providers == null)
            throw new ArgumentNullException(source == null ? nameof(source) : pages == null ? nameof(pages) : identity == null ? nameof(identity) : rendering == null ? nameof(rendering) : nameof(providers));
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed OFD stream is required.", nameof(output));
        return PdfFont.Run("ExportRasterOfd", "managed.ofd-raster", policy ?? source.Options.OperationPolicy, cancellationToken, c => FormatIo.Copy(Build(source, pages, identity, rendering, providers, c), output, c), PdfCapability.RenderPages);
    }

    internal static PdfByteStore Build(PdfDocumentSnapshot source, IEnumerable<int> pages, PdfOfdIdentity identity, PdfRenderOptions rendering, PdfProviderRegistry providers, PdfOperationContext c)
    {
        _ = providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages);
        c.Consume(PdfResourceKind.Objects, 32);
        c.Consume(PdfResourceKind.ExpandedBytes, 16384);
        var parts = new List<(string Name, byte[] Bytes, bool Stored)>();
        string defaultBox = "";
        var pageList = E("Pages");
        var media = E("MultiMedias");
        int count = 0;
        foreach (int page in pages)
        {
            c.Checkpoint();
            if (count == 512 || page < 0 || page >= source.Pages.Count)
                throw Error(c, "PageSelection", "Select1–512 existing pages in explicit order.");
            c.Consume(PdfResourceKind.Objects, 32);
            c.Consume(PdfResourceKind.ExpandedBytes, 16384);
            var frame = FormatRaster.Capture(source, page, rendering, providers, c);
            string n = (++count).ToString(CultureInfo.InvariantCulture);
            int id = count * 4;
            string box = "0 0 " + FormatIo.N(frame.Size.Width * 25.4 / 72) + " " + FormatIo.N(frame.Size.Height * 25.4 / 72);
            pageList.Add(E("Page", A("ID", id), A("BaseLoc", "Pages/" + n + "/Content.xml")));
            media.Add(E("MultiMedia", A("ID", id + 1), A("Type", "Image"), A("Format", "PNG"), E("MediaFile", "Image-" + n + ".png")));
            Add("Doc_0/Pages/" + n + "/Content.xml", E("Page", E("Area", E("PhysicalBox", box)), E("Content", E("Layer", A("ID", id + 2), A("Type", "Body"), E("ImageObject", A("ID", id + 3), A("ResourceID", id + 1), A("Boundary", box), A("CTM", FormatIo.N(frame.Size.Width * 25.4 / 72) + " 0 0 " + FormatIo.N(frame.Size.Height * 25.4 / 72) + " 0 0"))))));
            parts.Add(("Doc_0/Res/Image-" + n + ".png", FormatIo.Raw(frame.Png, c), false));
            if (count == 1)
                defaultBox = box;
            c.Report(new("Ofd.RasterPage", "The selected page is represented as an explicit PNG; source editable text/fonts/vectors, annotations and signature validity are not reconstructed.", severity: PdfDiagnosticSeverity.Information, pageIndex: page));
        }

        if (count == 0)
            throw Error(c, "PageSelection", "A nonempty page selection is required.");
        Add("OFD.xml", E("OFD", A("Version", "1.0"), A("DocType", "OFD"), E("DocBody", E("DocInfo", E("DocID", identity.DocumentId), E("Title", identity.Title), E("CreationDate", identity.CreationDate)), E("DocRoot", "Doc_0/Document.xml"))));
        Add("Doc_0/Document.xml", E("Document", E("CommonData", E("MaxUnitID", count * 4 + 3), E("PageArea", E("PhysicalBox", defaultBox)), E("DocumentRes", "Res.xml")), pageList));
        Add("Doc_0/Res.xml", E("Res", A("BaseLoc", "Res"), media));
        return FormatPackage.Write(parts, c);
        void Add(string name, XElement value) => parts.Add((name, FormatIo.Raw(FormatIo.XmlBytes(value, c), c), false));
    }

    /// <summary>Reads the closed PNG/page/resource profile and returns the literal source identity separately. Signed OFD packages reject rather than asserting signature preservation on converted bytes.</summary>
    public static PdfDocument ImportRasterPages(Stream input, out PdfOfdIdentity identity, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("A readable borrowed OFD stream is required.", nameof(input));
        identity = null!;
        PdfOfdIdentity? captured = null;
        PdfDocument? result = null;
        var finish = new List<Action<PdfOperationReport>>();
        try
        {
            var report = PdfFont.Run("ImportRasterOfd", "managed.ofd-raster", policy, cancellationToken, c =>
            {
                using var package = new FormatPackage(input, c);
                var used = new HashSet<string>(StringComparer.Ordinal);
                var root = Read("OFD.xml");
                if (root.Descendants().Any(e => e.Name.LocalName == "Signatures" || e.Name.LocalName == "Signature") || package.Names.Any(n => n.IndexOf("/Signs/", StringComparison.OrdinalIgnoreCase) >= 0 || n.EndsWith("Signatures.xml", StringComparison.OrdinalIgnoreCase)))
                    throw Error(c, "SignatureUnsupported", "OFD signatures require explicit preservation/verification authority; conversion does not preserve their validity.");
                Check(root, "OFD", new[] { "Version", "DocType" }, new[] { "DocBody" }, c);
                if ((string? )root.Attribute("Version") != "1.0" || (string? )root.Attribute("DocType") != "OFD")
                    throw Error(c, "VersionUnsupported", "The explicit OFD1.0 document profile is required.");
                var body = One(root, "DocBody", c);
                Check(body, "DocBody", Array.Empty<string>(), new[] { "DocInfo", "DocRoot" }, c);
                var info = One(body, "DocInfo", c);
                Check(info, "DocInfo", Array.Empty<string>(), new[] { "DocID", "Title", "CreationDate" }, c);
                try
                {
                    captured = new(Text(info, "DocID", c), Text(info, "Title", c), Text(info, "CreationDate", c));
                }
                catch (ArgumentException)
                {
                    c.Checkpoint();
                    throw Error(c, "Identity", "The source identity is invalid for the explicit profile.");
                }

                string documentPath = FormatPackage.Resolve("OFD.xml", Text(body, "DocRoot", c), c);
                if (documentPath != "Doc_0/Document.xml")
                    throw Error(c, "Ownership", "The source profile requires one explicitly owned document root.");
                var document = Read(documentPath);
                Check(document, "Document", Array.Empty<string>(), new[] { "CommonData", "Pages" }, c);
                var common = One(document, "CommonData", c);
                Check(common, "CommonData", Array.Empty<string>(), new[] { "MaxUnitID", "PageArea", "DocumentRes" }, c);
                int max = Id(Text(common, "MaxUnitID", c), c);
                var area = One(common, "PageArea", c);
                Check(area, "PageArea", Array.Empty<string>(), new[] { "PhysicalBox" }, c);
                _ = Box(Text(area, "PhysicalBox", c), c);
                string resPath = FormatPackage.Resolve(documentPath, Text(common, "DocumentRes", c), c);
                if (resPath != "Doc_0/Res.xml")
                    throw Error(c, "Ownership", "A document-owned resource catalog is required.");
                var res = Read(resPath);
                Check(res, "Res", new[] { "BaseLoc" }, new[] { "MultiMedias" }, c);
                if ((string? )res.Attribute("BaseLoc") != "Res")
                    throw Error(c, "Ownership", "An explicit owned resource base is required.");
                var resources = new Dictionary<int, string>();
                var ids = new HashSet<int>();
                var multi = One(res, "MultiMedias", c);
                Check(multi, "MultiMedias", Array.Empty<string>(), new[] { "MultiMedia" }, c);
                foreach (var item in multi.Elements())
                {
                    Check(item, "MultiMedia", new[] { "ID", "Type", "Format" }, new[] { "MediaFile" }, c);
                    if ((string? )item.Attribute("Type") != "Image" || (string? )item.Attribute("Format") != "PNG")
                        throw Error(c, "ResourceUnsupported", "Only explicit PNG image resources are supported.");
                    int id = Unit(item);
                    string path = FormatPackage.Resolve(resPath, "Res/" + Text(item, "MediaFile", c), c);
                    if (!path.StartsWith("Doc_0/Res/", StringComparison.Ordinal) || !package.Has(path))
                        throw Error(c, "Ownership", "Resources must exist inside the owning document resource base.");
                    resources.Add(id, path);
                }

                var pages = One(document, "Pages", c);
                Check(pages, "Pages", Array.Empty<string>(), new[] { "Page" }, c);
                result = Pdf.Create(new(maximumPages: 512, operationPolicy: c.Policy));
                int count = 0;
                var referenced = new HashSet<int>();
                foreach (var reference in pages.Elements())
                {
                    c.Checkpoint();
                    c.Consume(PdfResourceKind.Objects, 32);
                    c.Consume(PdfResourceKind.ExpandedBytes, 16384);
                    if (++count > 512)
                        throw Error(c, "PageSelection", "At most512 fixed pages are supported.");
                    Check(reference, "Page", new[] { "ID", "BaseLoc" }, Array.Empty<string>(), c);
                    _ = Unit(reference);
                    string pagePath = FormatPackage.Resolve(documentPath, (string? )reference.Attribute("BaseLoc") ?? "", c);
                    if (!pagePath.StartsWith("Doc_0/Pages/", StringComparison.Ordinal))
                        throw Error(c, "Ownership", "Pages must belong to their document.");
                    var page = Read(pagePath);
                    Check(page, "Page", Array.Empty<string>(), new[] { "Area", "Content" }, c);
                    var pageArea = One(page, "Area", c);
                    Check(pageArea, "Area", Array.Empty<string>(), new[] { "PhysicalBox" }, c);
                    var size = Box(Text(pageArea, "PhysicalBox", c), c);
                    var content = One(page, "Content", c);
                    Check(content, "Content", Array.Empty<string>(), new[] { "Layer" }, c);
                    var layer = One(content, "Layer", c);
                    Check(layer, "Layer", new[] { "ID", "Type" }, new[] { "ImageObject" }, c);
                    _ = Unit(layer);
                    if ((string? )layer.Attribute("Type") != "Body")
                        throw Error(c, "LayerUnsupported", "Only an explicit body layer is supported.");
                    var image = One(layer, "ImageObject", c);
                    Check(image, "ImageObject", new[] { "ID", "ResourceID", "Boundary", "CTM" }, Array.Empty<string>(), c);
                    _ = Unit(image);
                    int resource = Id((string? )image.Attribute("ResourceID") ?? "", c);
                    if (!resources.TryGetValue(resource, out var imagePath))
                        throw Error(c, "Ownership", "The page image must reference its owning resource catalog.");
                    referenced.Add(resource);
                    var boundary = Box((string? )image.Attribute("Boundary") ?? "", c);
                    if (Math.Abs(size.Width - boundary.Width) > 1e-8 || Math.Abs(size.Height - boundary.Height) > 1e-8)
                        throw Error(c, "Geometry", "A full-page image boundary is required.");
                    string matrix = FormatIo.N(size.Width * 25.4 / 72) + " 0 0 " + FormatIo.N(size.Height * 25.4 / 72) + " 0 0";
                    var actual = Scalars((string? )image.Attribute("CTM") ?? "", 6, c);
                    var expected = Scalars(matrix, 6, c);
                    if (actual.Where((v, n) => Math.Abs(v - expected[n]) > 1e-7).Any())
                        throw Error(c, "Geometry", "The full unit-image page transform is required.");
                    used.Add(imagePath);
                    var asset = PdfImages.ReadCore(package.Read(imagePath), new(allowLosslessTranscode: true), c);
                    if (asset.MediaType != "image/png")
                        throw Error(c, "ResourceUnsupported", "The declared PNG resource must contain PNG data.");
                    finish.Add(r => PdfImages.Complete(asset, r));
                    FormatIo.Page(result, size, canvas => canvas.DrawImageAsset(asset, new(0, 0, size.Width, size.Height), PdfCanvasFit.Stretch), c, finish);
                }

                if (count == 0)
                    throw Error(c, "PageSelection", "A nonempty fixed-page package is required.");
                if (referenced.Count != resources.Count || package.Names.Any(n => !used.Contains(n)))
                    throw Error(c, "ExtensionUnsupported", "Unreferenced resources, attachments, annotations and unknown package extensions are not silently discarded.");
                XElement Read(string path)
                {
                    used.Add(path);
                    return package.Xml(path).Root ?? throw Error(c, "Structure", "An XML root is required.");
                }

                int Unit(XElement element)
                {
                    int value = Id((string? )element.Attribute("ID") ?? "", c);
                    if (value > max || !ids.Add(value))
                        throw Error(c, "Ownership", "Unit IDs must be unique and bounded by MaxUnitID.");
                    return value;
                }
            }, PdfCapability.WriteDocument);
            foreach (var complete in finish)
                complete(report);
            result!.SetCompositionReport(report);
            identity = captured!;
            return result;
        }
        catch
        {
            result?.Dispose();
            throw;
        }
    }

    private static XElement E(string name, params object[] values) => new(Ns + name, values);
    private static XAttribute A(string name, object value) => new(name, value);
    private static void Check(XElement e, string name, string[] attributes, string[] children, PdfOperationContext c)
    {
        c.Checkpoint();
        if (e.Name != Ns + name || e.Attributes().Any(a => !a.IsNamespaceDeclaration && !attributes.Contains(a.Name.ToString())) || e.Elements().Any(x => x.Name.Namespace != Ns || !children.Contains(x.Name.LocalName)) || e.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value)))
            throw Error(c, "ExtensionUnsupported", "Unknown OFD elements/properties or content require another explicit profile.");
    }

    private static XElement One(XElement e, string name, PdfOperationContext c)
    {
        var list = e.Elements(Ns + name).Take(2).ToArray();
        if (list.Length != 1)
            throw Error(c, "Structure", "An exact single OFD element is required.");
        return list[0];
    }

    private static string Text(XElement e, string name, PdfOperationContext c)
    {
        var value = One(e, name, c);
        if (value.HasElements || value.Attributes().Any(a => !a.IsNamespaceDeclaration))
            throw Error(c, "Structure", "A literal OFD scalar is required.");
        return value.Value;
    }

    private static int Id(string value, PdfOperationContext c)
    {
        if (value.Length > 10 || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id < 1)
            throw Error(c, "Identity", "A bounded positive unit ID is required.");
        return id;
    }

    private static double[] Scalars(string value, int count, PdfOperationContext c)
    {
        if (value.Length > 1024)
            throw Error(c, "Geometry", "A bounded OFD geometry tuple is required.");
        c.Consume(PdfResourceKind.ExpandedBytes, 4096);
        var parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != count)
            throw Error(c, "Geometry", "An exact physical geometry tuple is required.");
        return parts.Select(v => FormatIo.Number(v, c)).ToArray();
    }

    private static PdfPageSize Box(string value, PdfOperationContext c)
    {
        var n = Scalars(value, 4, c);
        if (n[0] != 0 || n[1] != 0 || n[2] <= 0 || n[3] <= 0 || n[2] > 5080 || n[3] > 5080)
            throw Error(c, "Geometry", "An origin-zero positive fixed page at most5080 mm is required.");
        return new(n[2] * 72 / 25.4, n[3] * 72 / 25.4);
    }

    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Ofd." + code, message);
}
