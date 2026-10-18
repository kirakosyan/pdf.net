using System.Threading;

namespace Fluent.Pdf;
/// <summary>Standard authored comment/markup/media annotation kinds. Imported unknown kinds remain inspectable.</summary>
public enum PdfAnnotationKind
{
    /// <summary>A text comment.</summary>
    Text,
    /// <summary>A highlight.</summary>
    Highlight,
    /// <summary>An underline.</summary>
    Underline,
    /// <summary>A strikeout.</summary>
    StrikeOut,
    /// <summary>A squiggly underline.</summary>
    Squiggly,
    /// <summary>Freehand ink.</summary>
    Ink,
    /// <summary>A stamp.</summary>
    Stamp,
    /// <summary>A rectangular shape.</summary>
    Square,
    /// <summary>An elliptical shape.</summary>
    Circle,
    /// <summary>A line.</summary>
    Line,
    /// <summary>A polygon.</summary>
    Polygon,
    /// <summary>A polyline.</summary>
    PolyLine,
    /// <summary>Explicitly shaped free text.</summary>
    FreeText,
    /// <summary>A caret.</summary>
    Caret,
    /// <summary>An embedded file attachment.</summary>
    FileAttachment,
    /// <summary>Passive rich-media data with an explicit poster appearance; no media is executed.</summary>
    RichMedia
}

/// <summary>A detached immutable authored annotation with an explicit normal Form appearance.</summary>
public sealed partial class PdfAnnotation
{
    internal PdfAnnotation(PdfAnnotationKind kind, string id, PdfRect bounds, PdfValue properties, PdfGraphicsResource appearance, PdfAnnotationMetadata metadata, string version)
    {
        Kind = kind;
        Identifier = id;
        Bounds = bounds;
        Properties = properties;
        NormalAppearance = appearance;
        Metadata = metadata;
        MinimumVersion = version;
    }

    /// <summary>Gets its standard subtype.</summary>
    public PdfAnnotationKind Kind { get; }
    /// <summary>Gets its stable document-unique name.</summary>
    public string Identifier { get; }
    /// <summary>Gets its rectangle in native page coordinates, independent of page viewing rotation.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets explicit additional detached standard properties, without page/relationship/appearance identities.</summary>
    public PdfValue Properties { get; }
    /// <summary>Gets the explicit normal Form appearance and all its detached resources.</summary>
    public PdfGraphicsResource NormalAppearance { get; }
    /// <summary>Gets the immutable comment metadata.</summary>
    public PdfAnnotationMetadata Metadata { get; }
    /// <summary>Gets the aggregate feature version floor.</summary>
    public string MinimumVersion { get; }
    /// <summary>Gets the completed bounded construction report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;

    /// <summary>Creates an annotation with caller-defined appearance and standard subtype properties. Geometry, attachment/media requirements and detached ownership are validated; nothing executes.</summary>
    public static PdfAnnotation Create(PdfAnnotationKind kind, string identifier, PdfRect bounds, PdfGraphicsResource normalAppearance, PdfValue? properties = null, PdfAnnotationMetadata? metadata = null, string minimumVersion = "1.7", PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(typeof(PdfAnnotationKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.IsNullOrEmpty(identifier) || identifier.Length > 4096)
            throw new ArgumentException("A bounded nonempty identifier is required.", nameof(identifier));
        if (normalAppearance == null)
            throw new ArgumentNullException(nameof(normalAppearance));
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentException("An authored annotation requires a positive rectangle.", nameof(bounds));
        properties ??= PdfGraphicsNumbers.Dict();
        metadata ??= new();
        PdfAnnotation? result = null;
        var report = PdfFont.Run("CreateAnnotation", "managed.annotations", policy, cancellationToken, context =>
        {
            result = CreateCore(kind, identifier, bounds, normalAppearance, properties, metadata, context, minimumVersion);
        }, PdfCapability.WriteDocument);
        result!.OperationReport = report;
        return result;
    }

    internal static PdfAnnotation CreateCore(PdfAnnotationKind kind, string identifier, PdfRect bounds, PdfGraphicsResource normalAppearance, PdfValue properties, PdfAnnotationMetadata metadata, PdfOperationContext context, string minimumVersion = "1.3")
    {
        if (properties.Kind != PdfValueKind.Dictionary)
            throw PdfWriterCore.Error(context, "Annotation.Properties", "Annotation properties must be a direct dictionary.");
        ValidateProperties(properties, context);
        ValidateGeometry(kind.ToString(), properties, bounds, v => v, context);
        if (kind == PdfAnnotationKind.FileAttachment)
            PdfAnnotationMedia.ValidateFile(properties.Entries["FS"], context);
        if (kind == PdfAnnotationKind.RichMedia)
            PdfAnnotationMedia.Validate(properties, context);
        ValidateAppearance(normalAppearance, context);
        var strings = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
        metadata.Apply(strings, context);
        PdfTextStrings.Encode(identifier, context);
        int floor = Math.Max(PdfVersionNumber.Parse(minimumVersion), Math.Max(PdfGraphImport.ValidateDetached(properties, context), PdfVersionNumber.Parse(normalAppearance.MinimumVersion)));
        floor = Math.Max(floor, kind == PdfAnnotationKind.RichMedia ? 20 : kind == PdfAnnotationKind.Caret || kind == PdfAnnotationKind.Polygon || kind == PdfAnnotationKind.PolyLine ? 15 : kind == PdfAnnotationKind.Squiggly ? 14 : 13);
        if (properties.Entries.TryGetValue("F", out var declaredFlags) && declaredFlags.TryGetInteger(out long flags))
            floor = Math.Max(floor, (flags & 512) != 0 ? 17 : (flags & 256) != 0 ? 15 : (flags & 128) != 0 ? 14 : 13);
        if (metadata.ReplyTo != null || metadata.State != null)
            floor = Math.Max(floor, 15);
        return new(kind, identifier, bounds, properties, normalAppearance, metadata, PdfVersionNumber.Text(floor));
    }

    internal static void ValidateAppearance(PdfGraphicsResource appearance, PdfOperationContext context)
    {
        if (appearance.Category != PdfGraphicsResourceCategory.XObject || PdfPageImportSource.Name(appearance.Definition, "Subtype") != "Form")
            throw PdfWriterCore.Error(context, "Annotation.AppearanceForm", "A detached Form XObject is required for the normal appearance.");
        var bbox = appearance.Definition.Entries.TryGetValue("BBox", out var box) ? ReadRect(box, v => v, context) : throw PdfWriterCore.Error(context, "Annotation.AppearanceBounds", "The appearance requires a positive BBox.");
        var matrix = appearance.Definition.Entries.TryGetValue("Matrix", out var m) ? ReadMatrix(m, v => v, context) : PdfMatrix.Identity;
        var transformed = PdfGeometryMath.Transform(bbox, matrix).Bounds;
        if (transformed.Width <= 0 || transformed.Height <= 0)
            throw PdfWriterCore.Error(context, "Annotation.AppearanceBounds", "The transformed appearance bounds must be nondegenerate.");
    }

    internal static void ValidateProperties(PdfValue properties, PdfOperationContext context)
    {
        foreach (var pair in properties.Entries)
        {
            context.Checkpoint();
            if (new[]
            {
                "Type",
                "Subtype",
                "Rect",
                "AP",
                "P",
                "NM",
                "IRT",
                "Popup",
                "Parent",
                "StructParent"
            }.Contains(pair.Key))
                throw PdfWriterCore.Error(context, "Annotation.ReservedProperty", "Annotation identity, geometry, appearance and relationships use explicit contracts.");
        }
    }

    internal static void ValidateGeometry(string kind, PdfValue properties, PdfRect bounds, Func<PdfValue, PdfValue> resolve, PdfOperationContext context)
    {
        void Points(PdfValue value, int minimum, int multiple)
        {
            value = resolve(value);
            if (value.Kind != PdfValueKind.Array || value.Items.Count < minimum || value.Items.Count % multiple != 0)
                throw PdfWriterCore.Error(context, "Annotation.Points", "The annotation requires a complete bounded coordinate array.");
            for (int i = 0; i < value.Items.Count; i += 2)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                var xv = resolve(value.Items[i]);
                var yv = resolve(value.Items[i + 1]);
                if (xv.Kind != PdfValueKind.Number || yv.Kind != PdfValueKind.Number)
                    throw PdfWriterCore.Error(context, "Annotation.Points", "Annotation coordinates must be finite numbers.");
                double x = xv.NumberValue, y = yv.NumberValue;
                if (x < bounds.X || x > bounds.Right || y < bounds.Y || y > bounds.Top)
                    throw PdfWriterCore.Error(context, "Annotation.PointBounds", "Annotation coordinate geometry must lie inside its explicit rectangle.");
            }
        }

        PdfValue Need(string key) => properties.Entries.TryGetValue(key, out var value) ? resolve(value) : throw PdfWriterCore.Error(context, "Annotation.SubtypeProperties", "The annotation is missing required subtype properties.");
        if (kind == "Highlight" || kind == "Underline" || kind == "StrikeOut" || kind == "Squiggly")
            Points(Need("QuadPoints"), 8, 8);
        if (kind == "Line")
        {
            var line = Need("L");
            if (line.Kind != PdfValueKind.Array || line.Items.Count != 4)
                throw PdfWriterCore.Error(context, "Annotation.Points", "A line requires exactly two endpoints.");
            Points(line, 4, 4);
        }

        if (kind == "Polygon" || kind == "PolyLine")
            Points(Need("Vertices"), kind == "Polygon" ? 6 : 4, 2);
        if (kind == "Ink")
        {
            var ink = Need("InkList");
            if (ink.Kind != PdfValueKind.Array || ink.Items.Count == 0)
                throw PdfWriterCore.Error(context, "Annotation.Ink", "Ink requires at least one nonempty path.");
            foreach (var path in ink.Items)
                Points(path, 4, 2);
        }

        if (kind == "FreeText" && Need("DA").Kind != PdfValueKind.String)
            throw PdfWriterCore.Error(context, "Annotation.DefaultAppearance", "Free text requires an explicit default appearance string in addition to the authored appearance.");
        if (kind == "FileAttachment")
        {
            var fs = Need("FS");
            if (fs.Kind != PdfValueKind.Dictionary || !fs.Entries.TryGetValue("EF", out var ef) || resolve(ef).Kind != PdfValueKind.Dictionary)
                throw PdfWriterCore.Error(context, "Annotation.EmbeddedFile", "An authored file annotation requires an explicit embedded file specification.");
        }

        if (kind == "RichMedia")
        {
            if (Need("RichMediaContent").Kind != PdfValueKind.Dictionary || Need("RichMediaSettings").Kind != PdfValueKind.Dictionary)
                throw PdfWriterCore.Error(context, "Annotation.RichMedia", "Rich media requires explicit content and settings dictionaries and a poster appearance.");
        }

        if (properties.Entries.TryGetValue("F", out var f) && (!resolve(f).TryGetInteger(out long flags) || flags < 0 || flags > 1023))
            throw PdfWriterCore.Error(context, "Annotation.Flags", "Annotation flags must fit the standard ten-bit field.");
        if (properties.Entries.TryGetValue("CA", out var ca) && (resolve(ca).Kind != PdfValueKind.Number || resolve(ca).NumberValue < 0 || resolve(ca).NumberValue > 1))
            throw PdfWriterCore.Error(context, "Annotation.Opacity", "Annotation opacity must lie between zero and one.");
    }

    internal static PdfRect ReadRect(PdfValue value, Func<PdfValue, PdfValue> resolve, PdfOperationContext context)
    {
        value = resolve(value);
        if (value.Kind != PdfValueKind.Array || value.Items.Count != 4 || value.Items.Any(v => resolve(v).Kind != PdfValueKind.Number))
            throw PdfWriterCore.Error(context, "Annotation.Rectangle", "Annotation rectangles require four finite numeric bounds.");
        double x = resolve(value.Items[0]).NumberValue, y = resolve(value.Items[1]).NumberValue, right = resolve(value.Items[2]).NumberValue, top = resolve(value.Items[3]).NumberValue;
        if (right < x || top < y)
            throw PdfWriterCore.Error(context, "Annotation.Rectangle", "Rectangle bounds must be ordered.");
        return new(x, y, right - x, top - y);
    }

    internal static PdfMatrix ReadMatrix(PdfValue value, Func<PdfValue, PdfValue> resolve, PdfOperationContext context)
    {
        value = resolve(value);
        if (value.Kind != PdfValueKind.Array || value.Items.Count != 6 || value.Items.Any(v => resolve(v).Kind != PdfValueKind.Number))
            throw PdfWriterCore.Error(context, "Annotation.AppearanceMatrix", "An appearance matrix requires six finite coefficients.");
        var n = value.Items.Select(v => resolve(v).NumberValue).ToArray();
        return new(n[0], n[1], n[2], n[3], n[4], n[5]);
    }
}
