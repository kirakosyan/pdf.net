using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable detached stamp source with private resources. No browser, font, codec or file discovery is implicit.</summary>
public sealed class PdfStampContent
{
    private PdfStampContent(PdfGraphicsResource resource, PdfStampContentKind kind)
    {
        Resource = resource;
        Kind = kind;
    }

    internal PdfGraphicsResource Resource { get; }
    /// <summary>Gets the explicit source classification.</summary>
    public PdfStampContentKind Kind { get; }
    /// <summary>Gets the successful source-capture report.</summary>
    public PdfOperationReport OperationReport { get; private set; } = null!;

    /// <summary>Captures an original private drawing with positive source bounds.</summary>
    public static PdfStampContent FromDrawing(PdfDrawing drawing, PdfRect bounds, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (drawing == null)
            throw new ArgumentNullException(nameof(drawing));
        return Capture(PdfStampContentKind.Drawing, policy, cancellationToken, c => PdfGraphicsObjects.FormCore(drawing, bounds, null, null, c));
    }

    /// <summary>Embeds explicitly shaped text without selecting fonts, shaping providers or host resources.</summary>
    public static PdfStampContent FromText(PdfShapedText text, double fontSize, double lineHeight = 1.2, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        PdfGraphicsNumbers.Finite(fontSize);
        PdfGraphicsNumbers.Finite(lineHeight);
        if (fontSize <= 0 || lineHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        return Capture(PdfStampContentKind.Text, policy, cancellationToken, c =>
        {
            var bounds = PdfTextGeometry.Bounds(text, fontSize, lineHeight, c);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw PdfWriterCore.Error(c, "Stamp.EmptyText", "A text stamp requires a positive explicit shaped-text envelope.");
            var positioned = PdfTextDrawing.CreateCore(text, 0, 0, fontSize, lineHeight, "StampText", true, c);
            c.Consume(PdfResourceKind.ExpandedBytes, positioned.Fonts.Count * 192L + 256);
            var resources = new PdfResourceSet(positioned.Fonts.Select(f => new PdfResourceBinding(f.Key, PdfGraphicsResource.Font(f.Value))).ToArray());
            return PdfGraphicsObjects.FormCore(PdfDrawing.CreateCore(positioned.Content.Operations, resources, c), bounds, null, null, c);
        });
    }

    /// <summary>Captures immutable image samples, explicit physical aspect and Exif orientation without recompression.</summary>
    public static PdfStampContent FromImage(PdfImageAsset image, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (image == null)
            throw new ArgumentNullException(nameof(image));
        return Capture(PdfStampContentKind.Image, policy, cancellationToken, c =>
        {
            c.ConsumePixels(image.Info.Width, image.Info.Height);
            c.Consume(PdfResourceKind.ExpandedBytes, 1024);
            c.Consume(PdfResourceKind.Objects, 8);
            var matrix = new PdfMatrix(image.DisplayAspect, 0, 0, 1, 0, 0).Multiply(image.OrientationMatrix);
            var drawing = PdfDrawing.CreateCore(new[] { new PdfContentOperation(PdfOperator.Transform, matrix.Value.Items.ToArray()), new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("Image")) }, new(new PdfResourceBinding("Image", image.Resource)), c);
            return PdfGraphicsObjects.FormCore(drawing, new(0, 0, image.DisplayAspect, 1), null, null, c);
        });
    }

    /// <summary>Captures one passive PDF page's visible content, CropBox, rotation and UserUnit. Annotations, tags and optional-content owners require an explicit flatten/import profile.</summary>
    public static PdfStampContent FromPage(PdfDocumentSnapshot source, int pageIndex, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (pageIndex < 0 || pageIndex >= source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        return Capture(PdfStampContentKind.PdfPage, policy, cancellationToken, c => Page(source, pageIndex, c));
    }

    /// <summary>Renders an explicit HTML source and captures exactly one generated page under one shared context. Multi-page output requires an explicit caller selection through FromPage.</summary>
    public static PdfStampContent FromHtml(PdfHtmlSource source, PdfProviderRegistry providers, PdfHtmlOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        providers.GetRequired<IPdfHtmlRenderer>(PdfCapability.RenderHtml);
        options ??= new();
        return Capture(PdfStampContentKind.Html, policy, cancellationToken, c =>
        {
            using var document = PdfHtml.RenderCore(source, providers, options, c);
            if (document.Pages.Count != 1)
                throw PdfWriterCore.Error(c, "Stamp.HtmlPageSelection", "An HTML stamp requires exactly one generated page or an explicit separate page selection.");
            return Page(document.CreateSnapshot(), 0, c);
        }, providers.GetIdentifier(PdfCapability.RenderHtml), PdfCapability.RenderHtml);
    }

    private static PdfGraphicsResource Page(PdfDocumentSnapshot source, int index, PdfOperationContext context)
    {
        using var graph = new PdfGraphEdit(source, new(), context);
        graph.RequireUnstructuredContent();
        if (graph.Catalog.Entries.ContainsKey("OCProperties"))
            throw graph.Error("Stamp.OptionalContentProfile", "A source page with optional-content owners requires an explicit visibility flattening profile.");
        var page = graph.Snapshot.Pages[index].Source!;
        var annots = graph.Get(page.Dictionary, "Annots");
        if (annots.Kind != PdfValueKind.Null && (annots.Kind != PdfValueKind.Array || annots.Items.Count != 0))
            throw graph.Error("Stamp.PageAnnotations", "Page annotation appearances require an explicit flattening decision before stamp capture.");
        if (page.Dictionary.Entries.ContainsKey("OC"))
            throw graph.Error("Stamp.OptionalContentProfile", "A page optional-content association requires an explicit visibility flattening profile.");
        var stream = PdfPageContent.Read(page, PdfUnknownOperatorPolicy.Reject, graph.Graph.Version, new(), context).ToStream(context);
        context.Consume(PdfResourceKind.ExpandedBytes, 2048);
        context.Consume(PdfResourceKind.Objects, 16);
        var entries = graph.Copy(stream);
        var display = page.Geometry.DisplayBounds(PdfPageBox.Crop);
        entries["Type"] = PdfValue.Name("XObject");
        entries["Subtype"] = PdfValue.Name("Form");
        entries["BBox"] = page.Geometry.Boxes.Crop.Value;
        entries["Matrix"] = new PdfMatrix(1, 0, 0, -1, 0, display.Height).Multiply(page.Geometry.UserToDisplay(PdfPageBox.Crop)).Value;
        entries["Resources"] = page.EffectiveAttributes.Entries.TryGetValue("Resources", out var resources) ? resources : PdfValue.Dictionary(Array.Empty<KeyValuePair<string, PdfValue>>());
        if (page.Dictionary.Entries.TryGetValue("Group", out var rawGroup))
        {
            var group = graph.Resolve(rawGroup);
            if (group.Kind != PdfValueKind.Dictionary || PdfPageImportSource.Name(group, "S") != "Transparency")
                throw graph.Error("Stamp.PageGroup", "A page group requires an explicit supported transparency-group profile.");
            // A standalone page is effectively isolated even when its page-only I flag is false.
            // Capture that appearance as a valid Form group, including an explicitly declared CS.
            var capturedGroup = graph.Copy(group);
            capturedGroup["I"] = PdfValue.Boolean(true);
            entries["Group"] = PdfValue.OwnedDictionary(capturedGroup);
        }

        var form = stream.WithStreamDictionary(PdfValue.OwnedDictionary(entries));
        var detached = PdfPageGraphics.Detach(form, graph.Resolve(entries["Resources"]), graph);
        return PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.XObject, detached, graph.Graph.Version, PdfResourceSet.Empty, context);
    }

    private static PdfStampContent Capture(PdfStampContentKind kind, PdfOperationPolicy? policy, CancellationToken token, Func<PdfOperationContext, PdfGraphicsResource> create, string providerId = "managed.stamps", PdfCapability capability = PdfCapability.WriteDocument)
    {
        PdfStampContent? result = null;
        var report = PdfFont.Run("CaptureStamp", providerId, policy, token, c =>
        {
            var resource = create(c);
            c.Consume(PdfResourceKind.ExpandedBytes, 128);
            c.Consume(PdfResourceKind.Objects, 1);
            result = new(resource, kind);
        }, capability);
        result!.OperationReport = report;
        result.Resource.OperationReport = report;
        return result;
    }
}
