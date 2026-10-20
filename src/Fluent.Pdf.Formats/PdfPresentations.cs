using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml.Linq;

namespace Fluent.Pdf.Formats;
/// <summary>Explicit original image-based PPTX interchange. No editable text/vector reconstruction or automatic raster fallback is asserted.</summary>
public static class PdfPresentations
{
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main", A = "http://schemas.openxmlformats.org/drawingml/2006/main", R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships", Rel = "http://schemas.openxmlformats.org/package/2006/relationships", Ct = "http://schemas.openxmlformats.org/package/2006/content-types";
    /// <summary>Renders selected pages once in exact order into PNG pictures on an explicit common slide size. Aspect-preserving contain and opaque white margins are explicit; cropping and alpha backgrounds reject.</summary>
    public static PdfOperationReport WriteRasterSlides(PdfDocumentSnapshot source, IEnumerable<int> pages, PdfPageSize slideSize, PdfRenderOptions rendering, PdfProviderRegistry providers, Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || pages == null || rendering == null || providers == null)
            throw new ArgumentNullException(nameof(source));
        Guard.Size(slideSize, nameof(slideSize));
        if (slideSize.Width < 72 || slideSize.Height < 72 || slideSize.Width > 4032 || slideSize.Height > 4032)
            throw new ArgumentOutOfRangeException(nameof(slideSize));
        if (rendering.Region.HasValue || !rendering.BackgroundRgb.HasValue)
            throw new ArgumentException("Whole-page opaque rendering is required.", nameof(rendering));
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed output is required.", nameof(output));
        return PdfFont.Run("WriteRasterPresentation", "managed.pptx-raster", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            _ = providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages);
            c.Consume(PdfResourceKind.Objects, 256);
            c.Consume(PdfResourceKind.ExpandedBytes, 65536);
            var parts = new List<(string Name, byte[] Bytes, bool Stored)>();
            void Add(string name, XElement root) => parts.Add((name, FormatIo.Raw(FormatIo.XmlBytes(root, c), c), false));
            var slides = new XElement(P + "sldIdLst");
            var relationships = new XElement(Rel + "Relationships", Link("master1", "slideMaster", "slideMasters/master1.xml"));
            var types = new XElement(Ct + "Types", new XElement(Ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")), new XElement(Ct + "Default", new XAttribute("Extension", "png"), new XAttribute("ContentType", "image/png")), Override("/ppt/presentation.xml", "presentation.main+xml"));
            int count = 0;
            foreach (int page in pages)
            {
                c.Checkpoint();
                c.Consume(PdfResourceKind.Objects, 128);
                c.Consume(PdfResourceKind.ExpandedBytes, 32768);
                if (count == 512 || page < 0 || page >= source.Pages.Count)
                    throw Error(c, "PageSelection", "Select 1–512 existing pages in explicit order.");
                count++;
                c.Report(new("Presentation.RasterSlide", "This explicitly selected image-based slide contains rendered pixels; editable PDF objects are not reconstructed.", severity: PdfDiagnosticSeverity.Information, pageIndex: page));
                var bounds = source.Pages[page].Geometry.DisplayBounds();
                double scale = Math.Min(slideSize.Width / bounds.Width, slideSize.Height / bounds.Height), width = bounds.Width * scale, height = bounds.Height * scale, left = (slideSize.Width - width) / 2, top = (slideSize.Height - height) / 2;
                if (Emu(width) < 1 || Emu(height) < 1)
                    throw Error(c, "Geometry", "Rendered picture dimensions must fit positive EMU geometry.");
                var png = PdfRasterExport.Build(source, new[] { page }, new(PdfRasterFormat.Png, rendering), providers, c);
                string number = count.ToString(CultureInfo.InvariantCulture);
                parts.Add(("ppt/media/page" + number + ".png", FormatIo.Raw(png, c), false));
                slides.Add(new XElement(P + "sldId", new XAttribute("id", 255 + count), new XAttribute(R + "id", "r" + number)));
                relationships.Add(Link("r" + number, "slide", "slides/slide" + number + ".xml"));
                types.Add(Override("/ppt/slides/slide" + number + ".xml", "slide+xml"));
                var picture = new XElement(P + "pic", new XElement(P + "nvPicPr", new XElement(P + "cNvPr", new XAttribute("id", "2"), new XAttribute("name", "Raster page " + (page + 1).ToString(CultureInfo.InvariantCulture))), new XElement(P + "cNvPicPr", new XElement(A + "picLocks", new XAttribute("noChangeAspect", "1"))), new XElement(P + "nvPr")), new XElement(P + "blipFill", new XElement(A + "blip", new XAttribute(R + "embed", "image1")), new XElement(A + "stretch", new XElement(A + "fillRect"))), new XElement(P + "spPr", new XElement(A + "xfrm", new XElement(A + "off", new XAttribute("x", Emu(left)), new XAttribute("y", Emu(top))), new XElement(A + "ext", new XAttribute("cx", Emu(width)), new XAttribute("cy", Emu(height)))), new XElement(A + "prstGeom", new XAttribute("prst", "rect"), new XElement(A + "avLst"))));
                Add("ppt/slides/slide" + number + ".xml", new XElement(P + "sld", new XAttribute("showMasterSp", "0"), new XElement(P + "cSld", new XElement(P + "bg", new XElement(P + "bgPr", new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", "FFFFFF"))), new XElement(A + "effectLst"))), new XElement(P + "spTree", new XElement(P + "nvGrpSpPr", new XElement(P + "cNvPr", new XAttribute("id", "1"), new XAttribute("name", "")), new XElement(P + "cNvGrpSpPr"), new XElement(P + "nvPr")), new XElement(P + "grpSpPr"), picture)), new XElement(P + "clrMapOvr", new XElement(A + "masterClrMapping"))));
                Add("ppt/slides/_rels/slide" + number + ".xml.rels", new XElement(Rel + "Relationships", Link("image1", "image", "../media/page" + number + ".png"), Link("layout1", "slideLayout", "../slideLayouts/layout1.xml")));
            }

            if (count == 0)
                throw Error(c, "PageSelection", "A nonempty page selection is required.");
            types.Add(Override("/ppt/slideMasters/master1.xml", "slideMaster+xml"), Override("/ppt/slideLayouts/layout1.xml", "slideLayout+xml"));
            var mapping = new XElement(P + "clrMap");
            foreach (string color in new[]
            {
                "bg1",
                "tx1",
                "bg2",
                "tx2",
                "accent1",
                "accent2",
                "accent3",
                "accent4",
                "accent5",
                "accent6",
                "hlink",
                "folHlink"
            }

            )
                mapping.Add(new XAttribute(color, color == "bg1" ? "lt1" : color == "tx1" ? "dk1" : color == "bg2" ? "lt2" : color == "tx2" ? "dk2" : color));
            Add("ppt/slideMasters/master1.xml", new XElement(P + "sldMaster", new XElement(P + "cSld", EmptyTree()), mapping, new XElement(P + "sldLayoutIdLst", new XElement(P + "sldLayoutId", new XAttribute("id", "2147483649"), new XAttribute(R + "id", "layout1"))), new XElement(P + "txStyles", new XElement(P + "titleStyle"), new XElement(P + "bodyStyle"), new XElement(P + "otherStyle"))));
            Add("ppt/slideMasters/_rels/master1.xml.rels", new XElement(Rel + "Relationships", Link("layout1", "slideLayout", "../slideLayouts/layout1.xml")));
            Add("ppt/slideLayouts/layout1.xml", new XElement(P + "sldLayout", new XAttribute("type", "blank"), new XAttribute("preserve", "1"), new XElement(P + "cSld", EmptyTree()), new XElement(P + "clrMapOvr", new XElement(A + "masterClrMapping"))));
            Add("ppt/slideLayouts/_rels/layout1.xml.rels", new XElement(Rel + "Relationships", Link("master1", "slideMaster", "../slideMasters/master1.xml")));
            Add("[Content_Types].xml", types);
            Add("_rels/.rels", new XElement(Rel + "Relationships", Link("main", "officeDocument", "ppt/presentation.xml")));
            Add("ppt/presentation.xml", new XElement(P + "presentation", new XElement(P + "sldMasterIdLst", new XElement(P + "sldMasterId", new XAttribute("id", "2147483648"), new XAttribute(R + "id", "master1"))), slides, new XElement(P + "sldSz", new XAttribute("cx", Emu(slideSize.Width)), new XAttribute("cy", Emu(slideSize.Height)), new XAttribute("type", "custom")), new XElement(P + "notesSz", new XAttribute("cx", Emu(slideSize.Height)), new XAttribute("cy", Emu(slideSize.Width)))));
            Add("ppt/_rels/presentation.xml.rels", relationships);
            FormatIo.Copy(FormatPackage.Write(parts, c), output, c);
        }, PdfCapability.RenderPages);
    }

    /// <summary>Imports the documented explicit picture-only PPTX profile, preserving slide order and common geometry. Text, inherited master shapes, effects, animation, external media and picture cropping reject.</summary>
    public static PdfDocument ImportRasterSlides(Stream input, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfDocument? result = null;
        var finish = new List<Action<PdfOperationReport>>();
        try
        {
            var report = PdfFont.Run("ImportRasterPresentation", "managed.pptx-raster", policy, cancellationToken, c =>
            {
                using var package = new FormatPackage(input, c);
                package.RequireOpcMain("ppt/presentation.xml", "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml");
                if (package.Names.Any(n => n.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
                    throw Error(c, "ActiveContent", "Macro-enabled presentations reject.");
                var presentation = package.Xml("ppt/presentation.xml");
                if (presentation.Root?.Name != P + "presentation")
                    throw Error(c, "Root", "A standard presentation root is required.");
                var size = presentation.Root.Element(P + "sldSz") ?? throw Error(c, "Geometry", "Explicit slide size is required.");
                double width = Point(size, "cx", c), height = Point(size, "cy", c);
                var ids = presentation.Root.Element(P + "sldIdLst")?.Elements(P + "sldId").ToArray() ?? Array.Empty<XElement>();
                if (ids.Length < 1 || ids.Length > 512)
                    throw Error(c, "PageSelection", "A finite nonempty slide inventory is required.");
                result = Pdf.Create(new(operationPolicy: c.Policy));
                foreach (var id in ids)
                {
                    c.Checkpoint();
                    c.Consume(PdfResourceKind.Objects, 32);
                    c.Consume(PdfResourceKind.ExpandedBytes, 16384);
                    string slidePath = Target(package, "ppt/presentation.xml", (string? )id.Attribute(R + "id") ?? "", "slide", c);
                    ValidateLayout(package, slidePath, c);
                    var slide = package.Xml(slidePath).Root ?? throw Error(c, "Slide", "A complete slide is required.");
                    if (slide.Name != P + "sld" || (string? )slide.Attribute("showMasterSp") != "0" || slide.Elements().Any(e => e.Name != P + "cSld" && e.Name != P + "clrMapOvr"))
                        throw Error(c, "Unsupported", "Only explicit master-shape-free static raster slides are supported.");
                    var common = slide.Element(P + "cSld")!;
                    if (common == null || common.Elements().Any(e => e.Name != P + "bg" && e.Name != P + "spTree"))
                        throw Error(c, "Unsupported", "Unsupported common slide content rejects.");
                    var background = common.Element(P + "bg");
                    if (background == null || (string? )background.Element(P + "bgPr")?.Element(A + "solidFill")?.Element(A + "srgbClr")?.Attribute("val") != "FFFFFF" || background.Descendants().Any(e => e.Name != P + "bgPr" && e.Name != A + "solidFill" && e.Name != A + "srgbClr" && e.Name != A + "effectLst"))
                        throw Error(c, "Background", "An explicit opaque white slide background is required.");
                    var tree = common?.Element(P + "spTree") ?? throw Error(c, "Slide", "A shape tree is required.");
                    if (tree.Elements().Any(e => e.Name != P + "nvGrpSpPr" && e.Name != P + "grpSpPr" && e.Name != P + "pic") || tree.Element(P + "grpSpPr")?.HasElements == true)
                        throw Error(c, "Unsupported", "Text, grouped transforms and editable shapes require another profile.");
                    var pictures = new List<(PdfImageAsset Image, PdfRect Rect)>();
                    foreach (var pic in tree.Elements(P + "pic"))
                    {
                        ValidatePicture(pic, c);
                        var properties = pic.Element(P + "spPr");
                        var transform = properties?.Element(A + "xfrm");
                        if (pic.Descendants().Any(e => e.Name == A + "hlinkClick" || e.Name == A + "hlinkHover" || e.Name == A + "srcRect" || e.Name == A + "effectLst" || e.Name == A + "effectDag" || e.Name == A + "alphaModFix" || e.Name == P + "extLst") || transform == null || transform.Attributes().Any(a => !a.IsNamespaceDeclaration) || properties!.Elements().Any(e => e.Name != A + "xfrm" && e.Name != A + "prstGeom") || (string? )properties.Element(A + "prstGeom")?.Attribute("prst") != "rect")
                            throw Error(c, "Unsupported", "Cropped, rotated, linked or effect-bearing pictures reject.");
                        var blip = pic.Element(P + "blipFill")?.Element(A + "blip");
                        if (blip == null || blip.HasElements || blip.Attributes().Any(a => !a.IsNamespaceDeclaration && a.Name != R + "embed"))
                            throw Error(c, "Unsupported", "Only embedded unmodified raster pictures are supported.");
                        string imagePath = Target(package, slidePath, (string? )blip.Attribute(R + "embed") ?? "", "image", c);
                        var bytes = package.Read(imagePath);
                        var image = PdfImages.ReadCore(bytes, new(allowLosslessTranscode: true), c);
                        var offset = transform.Element(A + "off") ?? throw Error(c, "Geometry", "An explicit picture position is required.");
                        var extent = transform.Element(A + "ext") ?? throw Error(c, "Geometry", "An explicit picture extent is required.");
                        double x = Point(offset, "x", c, false), y = Point(offset, "y", c, false), w = Point(extent, "cx", c), h = Point(extent, "cy", c);
                        if (x < 0 || y < 0 || x + w > width + .001 || y + h > height + .001)
                            throw Error(c, "Geometry", "Pictures must lie inside the slide.");
                        pictures.Add((image, new(x, y, w, h)));
                    }

                    if (pictures.Count == 0)
                        throw Error(c, "EmptySlide", "At least one explicit raster picture is required.");
                    FormatIo.Page(result, new(width, height), canvas =>
                    {
                        canvas.DrawPath(PdfPath.RectangleCore(new(0, 0, width, height), c), new(fill: PdfPaint.Solid(new(PdfColorSpace.Rgb, 1, 1, 1))));
                        foreach (var picture in pictures)
                            canvas.DrawImageAsset(picture.Image, picture.Rect, PdfCanvasFit.Stretch);
                    }, c, finish);
                }
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

    private static void ValidateLayout(FormatPackage package, string slidePath, PdfOperationContext c)
    {
        int slash = slidePath.LastIndexOf('/');
        string linksPath = slidePath.Substring(0, slash + 1) + "_rels/" + slidePath.Substring(slash + 1) + ".rels";
        var links = package.Xml(linksPath).Root?.Elements(Rel + "Relationship").Where(e => (string? )e.Attribute("Type") == R.NamespaceName + "/slideLayout").ToArray() ?? Array.Empty<XElement>();
        if (links.Length != 1)
            throw Error(c, "Layout", "One explicit blank slide layout is required.");
        var layout = package.Xml(FormatPackage.Resolve(slidePath, (string? )links[0].Attribute("Target") ?? "", c)).Root;
        var tree = layout?.Element(P + "cSld")?.Element(P + "spTree");
        var nodes = new[]
        {
            P + "spTree",
            P + "nvGrpSpPr",
            P + "cNvPr",
            P + "cNvGrpSpPr",
            P + "nvPr",
            P + "grpSpPr"
        };
        if (layout?.Name != P + "sldLayout" || (string? )layout.Attribute("type") != "blank" || tree == null || tree.DescendantsAndSelf().Any(e => !nodes.Contains(e.Name)) || tree.Element(P + "grpSpPr")?.HasElements == true || layout.Elements().Any(e => e.Name != P + "cSld" && e.Name != P + "clrMapOvr") || layout.Element(P + "cSld")!.Elements().Any(e => e.Name != P + "spTree"))
            throw Error(c, "Layout", "Inherited layout shapes/backgrounds require another profile.");
    }

    private static XElement EmptyTree() => new(P + "spTree", new XElement(P + "nvGrpSpPr", new XElement(P + "cNvPr", new XAttribute("id", "1"), new XAttribute("name", "")), new XElement(P + "cNvGrpSpPr"), new XElement(P + "nvPr")), new XElement(P + "grpSpPr"));
    private static void ValidatePicture(XElement picture, PdfOperationContext c)
    {
        var allowed = new Dictionary<XName, XName[]>
        {
            [P + "pic"] = Array.Empty<XName>(),
            [P + "nvPicPr"] = Array.Empty<XName>(),
            [P + "cNvPr"] = new XName[]
            {
                "id",
                "name",
                "descr",
                "title"
            },
            [P + "cNvPicPr"] = Array.Empty<XName>(),
            [P + "nvPr"] = Array.Empty<XName>(),
            [A + "picLocks"] = new XName[]
            {
                "noChangeAspect"
            },
            [P + "blipFill"] = Array.Empty<XName>(),
            [A + "blip"] = new[]
            {
                R + "embed"
            },
            [A + "stretch"] = Array.Empty<XName>(),
            [A + "fillRect"] = Array.Empty<XName>(),
            [P + "spPr"] = Array.Empty<XName>(),
            [A + "xfrm"] = Array.Empty<XName>(),
            [A + "off"] = new XName[]
            {
                "x",
                "y"
            },
            [A + "ext"] = new XName[]
            {
                "cx",
                "cy"
            },
            [A + "prstGeom"] = new XName[]
            {
                "prst"
            },
            [A + "avLst"] = Array.Empty<XName>()
        };
        if (picture.DescendantsAndSelf().GroupBy(e => e.Name).Any(g => g.Count() > 1))
            throw Error(c, "Unsupported", "Duplicate picture structure rejects.");
        foreach (var element in picture.DescendantsAndSelf())
        {
            c.Checkpoint();
            if (!allowed.TryGetValue(element.Name, out var attributes) || element.Attributes().Any(a => !a.IsNamespaceDeclaration && !attributes.Contains(a.Name)) || element.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value)))
                throw Error(c, "Unsupported", "This picture contains unsupported styling, resources or effects.");
        }

        if (picture.Elements().Count() != 3 || picture.Elements(P + "nvPicPr").Count() != 1 || picture.Elements(P + "blipFill").Count() != 1 || picture.Elements(P + "spPr").Count() != 1 || picture.Element(P + "blipFill")!.Elements().Count() != 2 || picture.Element(P + "blipFill")!.Elements(A + "stretch").Count() != 1 || picture.Descendants(A + "avLst").Any(e => e.HasElements))
            throw Error(c, "Unsupported", "One explicit embedded rectangular picture is required.");
    }

    private static string Target(FormatPackage package, string owner, string id, string type, PdfOperationContext c)
    {
        int at = owner.LastIndexOf('/');
        string relPath = (at < 0 ? "" : owner.Substring(0, at + 1)) + "_rels/" + owner.Substring(at + 1) + ".rels";
        var root = package.Xml(relPath).Root;
        if (root?.Name != Rel + "Relationships")
            throw Error(c, "Relationship", "A relationships root is required.");
        var all = root.Elements(Rel + "Relationship").ToArray();
        if (all.Any(r => r.Attribute("TargetMode") != null && (string? )r.Attribute("TargetMode") != "Internal") || all.GroupBy(r => (string? )r.Attribute("Id")).Any(g => g.Count() != 1))
            throw Error(c, "Relationship", "External or ambiguous relationship identities reject.");
        var matches = all.Where(r => (string? )r.Attribute("Id") == id && (string? )r.Attribute("Type") == R.NamespaceName + "/" + type).ToArray();
        if (matches.Length != 1)
            throw Error(c, "Relationship", "One exact internal relationship is required.");
        return FormatPackage.Resolve(owner, (string? )matches[0].Attribute("Target") ?? "", c);
    }

    private static double Point(XElement element, string name, PdfOperationContext c, bool positive = true)
    {
        if (!long.TryParse((string? )element.Attribute(name), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value) || value < (positive ? 1 : 0) || value > 182880000)
            throw Error(c, "Geometry", "Bounded explicit EMU geometry is required.");
        return value / 12700d;
    }

    private static long Emu(double points) => checked((long)Math.Round(points * 12700, MidpointRounding.AwayFromZero));
    private static XElement Link(string id, string type, string target) => new(Rel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", R.NamespaceName + "/" + type), new XAttribute("Target", target));
    private static XElement Override(string name, string type) => new(Ct + "Override", new XAttribute("PartName", name), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.presentationml." + type));
    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Presentation." + code, message);
}
