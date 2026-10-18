using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable tiling or shading pattern preserving vector paint semantics.</summary>
public sealed class PdfPattern
{
    private PdfPattern(PdfGraphicsResource resource, bool colored)
    {
        Resource = resource;
        Colored = colored;
    }

    /// <summary>Gets its reusable Pattern resource.</summary>
    public PdfGraphicsResource Resource { get; }
    /// <summary>Gets whether the pattern supplies its own colors.</summary>
    public bool Colored { get; }

    /// <summary>Creates a type-1 tiling pattern with explicit cell bounds, steps, tiling policy and pattern-space transform.</summary>
    public static PdfPattern Tiling(PdfDrawing drawing, PdfRect bounds, double xStep, double yStep, bool colored = true, int tilingType = 1, PdfMatrix? matrix = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (drawing == null)
            throw new ArgumentNullException(nameof(drawing));
        PdfGraphicsNumbers.Finite(xStep);
        PdfGraphicsNumbers.Finite(yStep);
        if (bounds.Width <= 0 || bounds.Height <= 0 || xStep == 0 || yStep == 0 || tilingType < 1 || tilingType > 3)
            throw new ArgumentException("A tiling pattern requires positive cell bounds, nonzero steps and a standard tiling type.");
        PdfPattern? result = null;
        var report = PdfFont.Run("CreateTilingPattern", "managed.graphics", policy, cancellationToken, context =>
        {
            if (!colored)
                PdfUncoloredPattern.Validate(drawing, context);
            var stream = drawing.Content.ToStream(context);
            var entries = PdfWriterCore.CopyDictionary(stream.Entries, context);
            entries["Type"] = PdfValue.Name("Pattern");
            entries["PatternType"] = PdfValue.Integer(1);
            entries["PaintType"] = PdfValue.Integer(colored ? 1 : 2);
            entries["TilingType"] = PdfValue.Integer(tilingType);
            entries["BBox"] = bounds.Value;
            entries["XStep"] = PdfValues.Real(xStep);
            entries["YStep"] = PdfValues.Real(yStep);
            entries["Matrix"] = (matrix ?? PdfMatrix.Identity).Value;
            entries["Resources"] = PdfValue.Dictionary(System.Array.Empty<KeyValuePair<string, PdfValue>>());
            result = new PdfPattern(PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.Pattern, stream.WithStreamDictionary(PdfValue.OwnedDictionary(entries)), drawing.MinimumVersion, drawing.Resources, context), colored);
        }, PdfCapability.WriteDocument);
        result!.Resource.OperationReport = report;
        return result;
    }

    /// <summary>Creates a type-2 shading pattern with an optional exact compositing state and transform.</summary>
    public static PdfPattern Shading(PdfShading shading, PdfMatrix? matrix = null, PdfGraphicsState? state = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (shading == null)
            throw new ArgumentNullException(nameof(shading));
        PdfPattern? result = null;
        var report = PdfFont.Run("CreateShadingPattern", "managed.graphics", policy, cancellationToken, context =>
        {
            PdfValue Build(PdfValue value, PdfValue? graphics)
            {
                var entries = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
                {
                    {
                        "Type",
                        PdfValue.Name("Pattern")
                    },
                    {
                        "PatternType",
                        PdfValue.Integer(2)
                    },
                    {
                        "Shading",
                        value
                    },
                    {
                        "Matrix",
                        (matrix ?? PdfMatrix.Identity).Value
                    }
                };
                if (graphics != null)
                    entries["ExtGState"] = graphics;
                return PdfValue.OwnedDictionary(entries);
            }

            string version = PdfVersionNumber.Text(Math.Max(PdfVersionNumber.Parse(shading.Resource.MinimumVersion), state == null ? 13 : PdfVersionNumber.Parse(state.Resource.MinimumVersion)));
            var definition = Build(shading.Resource.Definition, state?.Resource.Definition);
            var validated = PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.Pattern, definition, version, PdfResourceSet.Empty, context);
            result = new PdfPattern(new PdfGraphicsResource(validated.Category, validated.Definition, validated.MinimumVersion, materialize: writer => Build(writer.Embed(shading.Resource), state == null ? null : writer.Embed(state.Resource))), true);
        }, PdfCapability.WriteDocument);
        result!.Resource.OperationReport = report;
        return result;
    }
}

internal static class PdfUncoloredPattern
{
    internal static void Validate(PdfDrawing drawing, PdfOperationContext context)
    {
        var visited = new HashSet<PdfGraphicsResource>();
        Visit(drawing.Content, drawing.Resources);
        PdfGraphicsResource Find(PdfResourceSet resources, PdfGraphicsResourceCategory category, string name)
        {
            var binding = resources.Bindings.FirstOrDefault(x => x.Resource.Category == category && x.Name == name);
            return binding?.Resource ?? throw PdfWriterCore.Error(context, "Graphics.UncoloredResourceProfile", "The nested uncolored form requires an explicit typed resource scope.");
        }

        void Reject() => throw PdfWriterCore.Error(context, "Graphics.UncoloredPatternContent", "Uncolored tiling content cannot select colors or paint an image that is not a stencil mask.");
        void Visit(PdfContentProgram content, PdfResourceSet resources)
        {
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            foreach (var operation in content.Operations)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                if (operation.Operation >= PdfOperator.StrokeColorSpace && operation.Operation <= PdfOperator.Shading)
                    Reject();
                if (operation.Operation == PdfOperator.InlineImage)
                {
                    var dictionary = operation.InlineImageDictionary!;
                    var mask = dictionary.Entries.TryGetValue("ImageMask", out var full) ? full : dictionary.Entries.TryGetValue("IM", out var abbreviated) ? abbreviated : PdfValue.Null;
                    if (mask.Kind != PdfValueKind.Boolean || !mask.BooleanValue)
                        Reject();
                }

                if (operation.Operation == PdfOperator.Font)
                {
                    var font = Find(resources, PdfGraphicsResourceCategory.Font, operation.Operands[0].NameValue).FontResource!;
                    if (font.Font.Info.Format == PdfFontFormat.Type3)
                        throw PdfWriterCore.Error(context, "Graphics.UncoloredType3Profile", "Authored Type 3 resources in uncolored tiling patterns require an explicit recursively validated glyph-resource profile.");
                }

                if (operation.Operation != PdfOperator.DrawObject)
                    continue;
                var resource = Find(resources, PdfGraphicsResourceCategory.XObject, operation.Operands[0].NameValue);
                context.Consume(PdfResourceKind.ExpandedBytes, 128);
                if (!visited.Add(resource))
                    continue;
                var value = resource.Definition;
                string subtype = value.Entries.TryGetValue("Subtype", out var kind) && kind.Kind == PdfValueKind.Name ? kind.NameValue : "";
                if (subtype == "Image")
                {
                    if (!value.Entries.TryGetValue("ImageMask", out var mask) || mask.Kind != PdfValueKind.Boolean || !mask.BooleanValue)
                        Reject();
                }
                else if (subtype == "Form")
                {
                    using var decoded = PdfFilters.Decode(value, context);
                    var bytes = PdfByteStore.Read(decoded, context);
                    var program = PdfContentProgram.ParseCore(bytes, PdfContentKind.Page, PdfUnknownOperatorPolicy.Reject, null, context);
                    Visit(program, resource.Dependencies);
                }
                else
                    Reject();
            }
        }
    }
}
