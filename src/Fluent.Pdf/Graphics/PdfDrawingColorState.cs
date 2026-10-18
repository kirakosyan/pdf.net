namespace Fluent.Pdf;
// Original color-state validation for authored self-contained drawings. Raw page content remains an inspection/preservation API.
internal static class PdfDrawingColorState
{
    internal static void Validate(PdfContentProgram content, PdfResourceSet resources, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, resources.Bindings.Count * 128L + 256);
        var bindings = resources.Bindings.ToDictionary(x => (x.Resource.Category, x.Name));
        var states = new Stack<((int Channels, bool Pattern) Fill, (int Channels, bool Pattern) Stroke)>();
        (int Channels, bool Pattern) fill = (1, false), stroke = (1, false);
        foreach (var operation in content.Operations)
        {
            context.Checkpoint();
            switch (operation.Operation)
            {
                case PdfOperator.SaveState:
                    context.Consume(PdfResourceKind.ExpandedBytes, 64);
                    states.Push((fill, stroke));
                    break;
                case PdfOperator.RestoreState:
                    var previous = states.Pop();
                    fill = previous.Fill;
                    stroke = previous.Stroke;
                    break;
                case PdfOperator.FillGray:
                    fill = (1, false);
                    break;
                case PdfOperator.StrokeGray:
                    stroke = (1, false);
                    break;
                case PdfOperator.FillRgb:
                    fill = (3, false);
                    break;
                case PdfOperator.StrokeRgb:
                    stroke = (3, false);
                    break;
                case PdfOperator.FillCmyk:
                    fill = (4, false);
                    break;
                case PdfOperator.StrokeCmyk:
                    stroke = (4, false);
                    break;
                case PdfOperator.FillColorSpace:
                    fill = Space(operation.Operands[0], new HashSet<string>(StringComparer.Ordinal));
                    break;
                case PdfOperator.StrokeColorSpace:
                    stroke = Space(operation.Operands[0], new HashSet<string>(StringComparer.Ordinal));
                    break;
                case PdfOperator.FillColor:
                case PdfOperator.FillColorExtended:
                    Check(operation, fill);
                    break;
                case PdfOperator.StrokeColor:
                case PdfOperator.StrokeColorExtended:
                    Check(operation, stroke);
                    break;
            }
        }

        (int Channels, bool Pattern) Space(PdfValue value, HashSet<string> names)
        {
            context.Checkpoint();
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 128);
            if (value.Kind == PdfValueKind.Name)
            {
                switch (value.NameValue)
                {
                    case "DeviceGray":
                        return (1, false);
                    case "DeviceRGB":
                        return (3, false);
                    case "DeviceCMYK":
                        return (4, false);
                    case "Pattern":
                        return (0, true);
                }

                if (!names.Add(value.NameValue))
                    throw PdfWriterCore.Error(context, "Graphics.ColorSpaceCycle", "Named color-space aliases contain a cycle.");
                if (!bindings.TryGetValue((PdfGraphicsResourceCategory.ColorSpace, value.NameValue), out var binding))
                    throw Error();
                return Space(binding.Resource.Definition, names);
            }

            if (value.Kind != PdfValueKind.Array || value.Items.Count < 2 || value.Items[0].Kind != PdfValueKind.Name)
                throw Error();
            switch (value.Items[0].NameValue)
            {
                case "CalGray":
                    return (1, false);
                case "CalRGB":
                case "Lab":
                    return (3, false);
                case "Separation":
                    return (1, false);
                case "DeviceN":
                    if (value.Items[1].Kind != PdfValueKind.Array || value.Items[1].Items.Count < 1 || value.Items[1].Items.Count > 32)
                        throw Error();
                    return (value.Items[1].Items.Count, false);
                case "Indexed":
                    return (1, false);
                case "ICCBased":
                    var profile = value.Items[1];
                    if (profile.Kind != PdfValueKind.Stream || !profile.Entries.TryGetValue("N", out var channels) || channels.Kind != PdfValueKind.Number)
                        throw Error();
                    long n = channels.IntegerValue;
                    if (n != 1 && n != 3 && n != 4)
                        throw Error();
                    return ((int)n, false);
                case "Pattern":
                    var basis = Space(value.Items[1], names);
                    if (basis.Pattern)
                        throw Error();
                    return (basis.Channels, true);
                default:
                    throw Error();
            }
        }

        void Check(PdfContentOperation operation, (int Channels, bool Pattern) state)
        {
            int count = operation.Operands.Count;
            if (!state.Pattern)
            {
                if (count != state.Channels || operation.Operands.Any(x => x.Kind != PdfValueKind.Number))
                    throw PdfWriterCore.Error(context, "Graphics.ColorChannels", "Color operands must match the current graphics-state color space.");
                return;
            }

            if (operation.Operation != PdfOperator.FillColorExtended && operation.Operation != PdfOperator.StrokeColorExtended || count != state.Channels + 1 || operation.Operands[count - 1].Kind != PdfValueKind.Name)
                throw PdfWriterCore.Error(context, "Graphics.PatternColorSpace", "Pattern painting requires matching base components and a named pattern operand.");
            string name = operation.Operands[count - 1].NameValue;
            if (!bindings.TryGetValue((PdfGraphicsResourceCategory.Pattern, name), out var binding))
                throw PdfWriterCore.Error(context, "Graphics.ResourceMissing", "The pattern resource is absent.");
            var definition = binding.Resource.Definition;
            bool uncolored = definition.Entries.TryGetValue("PaintType", out var paint) && paint.Kind == PdfValueKind.Number && paint.IntegerValue == 2;
            if (uncolored != (state.Channels > 0))
                throw PdfWriterCore.Error(context, "Graphics.PatternColorSpace", "Colored and uncolored patterns require their matching Pattern color spaces.");
        }

        PdfException Error() => PdfWriterCore.Error(context, "Graphics.ColorSpaceProfile", "The authored drawing uses an unsupported or malformed color-space definition.");
    }
}
