using System.Threading;

namespace Fluent.Pdf;
/// <summary>A self-contained vector drawing with a private graphics-state scope and explicit immutable resources.</summary>
public sealed class PdfDrawing
{
    private PdfDrawing(PdfContentProgram content, PdfResourceSet resources)
    {
        Content = content;
        Resources = resources;
        MinimumVersion = PdfVersionNumber.Text(Math.Max(PdfVersionNumber.Parse(content.MinimumVersion), PdfVersionNumber.Parse(resources.MinimumVersion)));
    }

    /// <summary>Gets the validated state-isolated program.</summary>
    public PdfContentProgram Content { get; }
    /// <summary>Gets its private named resources.</summary>
    public PdfResourceSet Resources { get; }
    /// <summary>Gets its aggregate version floor.</summary>
    public string MinimumVersion { get; }
    /// <summary>Gets the successful bounded composition report.</summary>
    public PdfOperationReport OperationReport { get; private set; } = null!;

    /// <summary>Validates and captures a drawing, adding q/Q around the complete program and rejecting missing named resources.</summary>
    public static PdfDrawing Create(IEnumerable<PdfContentOperation> operations, PdfResourceSet? resources = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (operations == null)
            throw new ArgumentNullException(nameof(operations));
        resources ??= PdfResourceSet.Empty;
        PdfDrawing? result = null;
        var report = PdfFont.Run("CreateDrawing", "managed.graphics", policy, cancellationToken, context => result = CreateCore(operations, resources, context), PdfCapability.WriteDocument);
        result!.OperationReport = report;
        result.Content.OperationReport = report;
        return result;
    }

    internal static PdfDrawing CreateCore(IEnumerable<PdfContentOperation> operations, PdfResourceSet resources, PdfOperationContext context)
    {
        IEnumerable<PdfContentOperation> Isolated()
        {
            yield return new(PdfOperator.SaveState);
            foreach (var operation in operations)
                yield return operation;
            yield return new(PdfOperator.RestoreState);
        }

        var content = PdfContentProgram.CreateCore(Isolated(), PdfContentKind.Page, context);
        context.Consume(PdfResourceKind.ExpandedBytes, resources.Bindings.Count * 128L + 128);
        var names = new HashSet<(PdfGraphicsResourceCategory, string)>(resources.Bindings.Select(x => (x.Resource.Category, x.Name)));
        void Need(PdfGraphicsResourceCategory category, PdfValue name)
        {
            if (!names.Contains((category, name.NameValue)))
                throw PdfWriterCore.Error(context, "Graphics.ResourceMissing", "A drawing references an absent named resource in its private scope.");
        }

        foreach (var operation in content.Operations)
        {
            context.Checkpoint();
            switch (operation.Operation)
            {
                case PdfOperator.Font:
                    Need(PdfGraphicsResourceCategory.Font, operation.Operands[0]);
                    break;
                case PdfOperator.ExtendedState:
                    Need(PdfGraphicsResourceCategory.ExtGState, operation.Operands[0]);
                    break;
                case PdfOperator.Shading:
                    Need(PdfGraphicsResourceCategory.Shading, operation.Operands[0]);
                    break;
                case PdfOperator.DrawObject:
                    Need(PdfGraphicsResourceCategory.XObject, operation.Operands[0]);
                    break;
                case PdfOperator.FillColorSpace:
                case PdfOperator.StrokeColorSpace:
                    string name = operation.Operands[0].NameValue;
                    if (name != "DeviceGray" && name != "DeviceRGB" && name != "DeviceCMYK" && name != "Pattern")
                        Need(PdfGraphicsResourceCategory.ColorSpace, operation.Operands[0]);
                    break;
                case PdfOperator.FillColorExtended:
                case PdfOperator.StrokeColorExtended:
                    if (operation.Operands.Count > 0 && operation.Operands[operation.Operands.Count - 1].Kind == PdfValueKind.Name)
                        Need(PdfGraphicsResourceCategory.Pattern, operation.Operands[operation.Operands.Count - 1]);
                    break;
            }
        }

        PdfDrawingColorState.Validate(content, resources, context);
        return new PdfDrawing(content, resources);
    }
}
