using System.Threading;

namespace Fluent.Pdf;
/// <summary>Original form/pattern construction preserving vector operators and resource scopes.</summary>
public static class PdfGraphicsObjects
{
    /// <summary>Creates an immutable reusable Form XObject with explicit bounds and transform.</summary>
    public static PdfGraphicsResource Form(PdfDrawing drawing, PdfRect bounds, PdfMatrix? matrix = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        return FormCorePublic(drawing, bounds, matrix, null, policy, cancellationToken);
    }

    internal static PdfGraphicsResource FormCorePublic(PdfDrawing drawing, PdfRect bounds, PdfMatrix? matrix, PdfValue? group, PdfOperationPolicy? policy, CancellationToken token)
    {
        if (drawing == null)
            throw new ArgumentNullException(nameof(drawing));
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentException("A reusable form requires positive bounds.", nameof(bounds));
        PdfGraphicsResource? result = null;
        var report = PdfFont.Run("CreateForm", "managed.graphics", policy, token, context =>result=FormCore(drawing,bounds,matrix,group,context), PdfCapability.WriteDocument);
        result!.OperationReport = report;
        return result;
    }
    internal static PdfGraphicsResource FormCore(PdfDrawing drawing,PdfRect bounds,PdfMatrix? matrix,PdfValue? group,PdfOperationContext context)
    {
        if(bounds.Width<=0||bounds.Height<=0)throw new ArgumentException("A reusable form requires positive bounds.",nameof(bounds));
        var stream=drawing.Content.ToStream(context);var entries=PdfWriterCore.CopyDictionary(stream.Entries,context);entries["Type"]=PdfValue.Name("XObject");entries["Subtype"]=PdfValue.Name("Form");entries["FormType"]=PdfValue.Integer(1);entries["BBox"]=bounds.Value;entries["Matrix"]=(matrix??PdfMatrix.Identity).Value;entries["Resources"]=PdfValue.Dictionary(System.Array.Empty<KeyValuePair<string,PdfValue>>());if(group!=null)entries["Group"]=group;return PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.XObject,stream.WithStreamDictionary(PdfValue.OwnedDictionary(entries)),group==null?drawing.MinimumVersion:PdfVersionNumber.Text(Math.Max(14,PdfVersionNumber.Parse(drawing.MinimumVersion))),drawing.Resources,context);
    }

}

/// <summary>An explicit Form XObject transparency group.</summary>
public sealed class PdfTransparencyGroup
{
    private PdfTransparencyGroup(PdfGraphicsResource resource, PdfColorSpace colorSpace, bool isolated, bool knockout)
    {
        Resource = resource;
        ColorSpace = colorSpace;
        Isolated = isolated;
        Knockout = knockout;
    }

    /// <summary>Gets the reusable vector form resource.</summary>
    public PdfGraphicsResource Resource { get; }
    /// <summary>Gets the exact blending color space.</summary>
    public PdfColorSpace ColorSpace { get; }
    /// <summary>Gets the isolated-group choice.</summary>
    public bool Isolated { get; }
    /// <summary>Gets the knockout-group choice.</summary>
    public bool Knockout { get; }

    /// <summary>Creates a PDF 1.4 transparency group with an explicit nonspecial blending space.</summary>
    public static PdfTransparencyGroup Create(PdfDrawing drawing, PdfRect bounds, PdfColorSpace colorSpace, bool isolated = false, bool knockout = false, PdfMatrix? matrix = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (colorSpace == null)
            throw new ArgumentNullException(nameof(colorSpace));
        if (!colorSpace.SupportsBlending || colorSpace.IsPattern)
            throw new ArgumentException("A transparency group requires a nonspecial blending color space.", nameof(colorSpace));
        var group = PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Group")), ("S", PdfValue.Name("Transparency")), ("CS", colorSpace.Definition), ("I", PdfValue.Boolean(isolated)), ("K", PdfValue.Boolean(knockout)));
        var form = PdfGraphicsObjects.FormCorePublic(drawing, bounds, matrix, group, policy, cancellationToken);
        if (PdfVersionNumber.Parse(colorSpace.MinimumVersion) > PdfVersionNumber.Parse(form.MinimumVersion))
            form = new PdfGraphicsResource(form.Category, form.Definition, colorSpace.MinimumVersion, form.Dependencies)
            {
                OperationReport = form.OperationReport
            };
        return new(form, colorSpace, isolated, knockout);
    }
}

/// <summary>The alpha/luminosity method used to derive a soft mask from its transparency group.</summary>
public enum PdfSoftMaskKind
{
    /// <summary>Use group alpha.</summary>
    Alpha,
    /// <summary>Use group luminosity in its explicit blending space.</summary>
    Luminosity
}

/// <summary>An immutable explicit vector soft mask; no rasterization occurs in the SDK.</summary>
public sealed class PdfSoftMask
{
    /// <summary>Captures a transparency group and optional luminosity backdrop/unary transfer function.</summary>
    public PdfSoftMask(PdfTransparencyGroup group, PdfSoftMaskKind kind = PdfSoftMaskKind.Alpha, PdfColor? backdrop = null, PdfFunction? transfer = null)
    {
        Group = group ?? throw new ArgumentNullException(nameof(group));
        if (!Enum.IsDefined(typeof(PdfSoftMaskKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (backdrop != null && (kind != PdfSoftMaskKind.Luminosity || !ReferenceEquals(backdrop.ColorSpace, group.ColorSpace)))
            throw new ArgumentException("A backdrop must use the exact luminosity group blending space.", nameof(backdrop));
        if (transfer != null && (transfer.InputComponents != 1 || transfer.OutputComponents != 1))
            throw new ArgumentException("A soft-mask transfer function must be unary.", nameof(transfer));
        Kind = kind;
        Backdrop = backdrop;
        Transfer = transfer;
    }

    /// <summary>Gets its exact vector transparency group.</summary>
    public PdfTransparencyGroup Group { get; }
    /// <summary>Gets the alpha or luminosity method.</summary>
    public PdfSoftMaskKind Kind { get; }
    /// <summary>Gets the explicitly selected luminosity backdrop.</summary>
    public PdfColor? Backdrop { get; }
    /// <summary>Gets the optional unary transfer function.</summary>
    public PdfFunction? Transfer { get; }

    internal PdfValue Definition(PdfValue group)
    {
        var entries = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
        {
            {
                "S",
                PdfValue.Name(Kind.ToString())
            },
            {
                "G",
                group
            }
        };
        if (Backdrop != null)
            entries["BC"] = PdfGraphicsNumbers.Array(Backdrop.Components.ToArray());
        if (Transfer != null)
            entries["TR"] = Transfer.Definition;
        return PdfValue.OwnedDictionary(entries);
    }
}

/// <summary>An immutable explicit extended graphics state with standard compositing behavior.</summary>
public sealed class PdfGraphicsState
{
    private PdfGraphicsState(PdfGraphicsResource resource, string blend, double fill, double stroke, PdfSoftMask? mask)
    {
        Resource = resource;
        BlendMode = blend;
        FillAlpha = fill;
        StrokeAlpha = stroke;
        SoftMask = mask;
    }

    internal static PdfGraphicsState StrokeAdjustmentCore(bool enabled, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.Objects, 4); context.Consume(PdfResourceKind.ExpandedBytes, 1024);
        var definition = PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("ExtGState")), ("SA", PdfValue.Boolean(enabled)));
        var resource = PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.ExtGState, definition, "1.2", PdfResourceSet.Empty, context);
        return new(resource, "Normal", 1, 1, null);
    }

    /// <summary>Gets its reusable ExtGState resource.</summary>
    public PdfGraphicsResource Resource { get; }
    /// <summary>Gets the actual standard blend mode.</summary>
    public string BlendMode { get; }
    /// <summary>Gets the fill opacity.</summary>
    public double FillAlpha { get; }
    /// <summary>Gets the stroke opacity.</summary>
    public double StrokeAlpha { get; }
    /// <summary>Gets its optional exact soft mask.</summary>
    public PdfSoftMask? SoftMask { get; }
    /// <summary>Gets all standard PDF separable and nonseparable blend mode names.</summary>
    public static IReadOnlyList<string> SupportedBlendModes { get; } = System.Array.AsReadOnly(new[] { "Normal", "Compatible", "Multiply", "Screen", "Overlay", "Darken", "Lighten", "ColorDodge", "ColorBurn", "HardLight", "SoftLight", "Difference", "Exclusion", "Hue", "Saturation", "Color", "Luminosity" });

    /// <summary>Creates explicit PDF 1.4 compositing state. Unknown blend modes fail strictly or report replacement by Normal under an explicit permissive policy.</summary>
    public static PdfGraphicsState Create(string blendMode = "Normal", double fillAlpha = 1, double strokeAlpha = 1, PdfSoftMask? softMask = null, bool alphaIsShape = false, bool textKnockout = true, bool strokeOverprint = false, bool fillOverprint = false, int overprintMode = 0, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(blendMode) || blendMode.Length > 64)
            throw new ArgumentException("A bounded blend mode is required.", nameof(blendMode));
        PdfGraphicsNumbers.Unit(fillAlpha);
        PdfGraphicsNumbers.Unit(strokeAlpha);
        if (overprintMode < 0 || overprintMode > 1)
            throw new ArgumentOutOfRangeException(nameof(overprintMode));
        PdfGraphicsState? result = null;
        var report = PdfFont.Run("CreateGraphicsState", "managed.graphics", policy, cancellationToken, context =>
        {
            string actual = blendMode;
            if (!SupportedBlendModes.Contains(actual, StringComparer.Ordinal))
            {
                context.Report(new PdfDiagnostic("Graphics.UnsupportedBlendMode", "The requested blend mode is unsupported; permissive processing substitutes Normal.", lossCategory: PdfLossCategory.SubstitutedContent));
                actual = "Normal";
            }

            PdfValue Build(PdfValue group)
            {
                var values = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
                {
                    {
                        "Type",
                        PdfValue.Name("ExtGState")
                    },
                    {
                        "BM",
                        PdfValue.Name(actual)
                    },
                    {
                        "ca",
                        PdfValues.Real(fillAlpha)
                    },
                    {
                        "CA",
                        PdfValues.Real(strokeAlpha)
                    },
                    {
                        "AIS",
                        PdfValue.Boolean(alphaIsShape)
                    },
                    {
                        "TK",
                        PdfValue.Boolean(textKnockout)
                    },
                    {
                        "OP",
                        PdfValue.Boolean(strokeOverprint)
                    },
                    {
                        "op",
                        PdfValue.Boolean(fillOverprint)
                    },
                    {
                        "OPM",
                        PdfValue.Integer(overprintMode)
                    },
                    {
                        "SMask",
                        softMask == null ? PdfValue.Name("None") : softMask.Definition(group)
                    }
                };
                return PdfValue.OwnedDictionary(values);
            }

            var definition = Build(softMask?.Group.Resource.Definition ?? PdfValue.Null);
            var validated = PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.ExtGState, definition, "1.4", PdfResourceSet.Empty, context);
            var resource = softMask == null ? validated : new PdfGraphicsResource(validated.Category, validated.Definition, PdfVersionNumber.Text(Math.Max(PdfVersionNumber.Parse(validated.MinimumVersion), PdfVersionNumber.Parse(softMask.Group.Resource.MinimumVersion))), materialize: writer => Build(writer.Embed(softMask.Group.Resource)));
            result = new PdfGraphicsState(resource, actual, fillAlpha, strokeAlpha, softMask);
        }, PdfCapability.WriteDocument);
        result!.Resource.OperationReport = report;
        return result;
    }
}
