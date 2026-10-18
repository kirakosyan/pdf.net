namespace Fluent.Pdf;
internal static class PdfImageColorResolver
{
    internal static int Components(PdfValue color, PdfValue resources, Func<PdfValue, PdfValue> resolve, PdfOperationContext context)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        int Read(PdfValue value)
        {
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            value = resolve(value);
            if (value.Kind == PdfValueKind.Name)
            {
                string name = value.NameValue;
                if (name == "DeviceGray" || name == "G")
                    return 1;
                if (name == "DeviceRGB" || name == "RGB")
                    return 3;
                if (name == "DeviceCMYK" || name == "CMYK")
                    return 4;
                if (name == "Pattern")
                    throw Error("Image.PatternColor", "Images cannot use a Pattern color space.");
                context.Consume(PdfResourceKind.ExpandedBytes, 96);
                if (!names.Add(name))
                    throw Error("Image.ColorSpaceCycle", "Named image color spaces contain a cycle.");
                try
                {
                    var scope = resolve(resources);
                    if (scope.Kind != PdfValueKind.Dictionary || !scope.Entries.TryGetValue("ColorSpace", out var spaces))
                        throw Error("Image.NamedColorSpace", "A named image color space is absent from its resource scope.");
                    spaces = resolve(spaces);
                    if (spaces.Kind != PdfValueKind.Dictionary || !spaces.Entries.TryGetValue(name, out var definition))
                        throw Error("Image.NamedColorSpace", "A named image color space is absent from its resource scope.");
                    return Read(definition);
                }
                finally
                {
                    names.Remove(name);
                }
            }

            if (value.Kind != PdfValueKind.Array || value.Items.Count == 0 || resolve(value.Items[0]).Kind != PdfValueKind.Name)
                throw Error("Image.ColorSpace", "Image color space must resolve to a supported name or array.");
            string kind = resolve(value.Items[0]).NameValue;
            if (kind == "CalGray" || kind == "Separation" || kind == "Indexed" || kind == "I")
                return 1;
            if (kind == "CalRGB" || kind == "Lab")
                return 3;
            if (kind == "ICCBased" && value.Items.Count == 2)
            {
                var profile = resolve(value.Items[1]);
                if (profile.Kind != PdfValueKind.Stream || !profile.Entries.TryGetValue("N", out var n) || !resolve(n).TryGetInteger(out long count) || count != 1 && count != 3 && count != 4)
                    throw Error("Image.IccComponents", "An ICC image color space requires an explicit Gray, RGB or CMYK channel count.");
                return (int)count;
            }

            if (kind == "DeviceN" && value.Items.Count >= 4)
            {
                var colorants = resolve(value.Items[1]);
                if (colorants.Kind != PdfValueKind.Array || colorants.Items.Count < 1 || colorants.Items.Count > 32 || colorants.Items.Any(v => resolve(v).Kind != PdfValueKind.Name))
                    throw Error("Image.DeviceNComponents", "DeviceN image colorants must be a bounded nonempty name array.");
                return colorants.Items.Count;
            }

            throw Error("Image.ColorSpace", "This image color-space definition requires an additional explicit profile.");
        }

        return Read(color);
        PdfException Error(string code, string message) => PdfImages.Error(context, code, message);
    }
}
