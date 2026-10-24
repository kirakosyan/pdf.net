using System.IO;

namespace Fluent.Pdf;
internal sealed partial class PdfOptimizationEngine
{
    private void Downsample()
    {
        foreach (var id in Ids())
        {
            _c.Checkpoint();
            var value = _g.Edit.Resolve(id);
            if (value.Kind != PdfValueKind.Stream || Name(value, "Subtype") != "Image")
                continue;
            int width = Integer(value, "Width"), height = Integer(value, "Height");
            int limit = _options.MaximumImageDimension;
            if (width <= limit && height <= limit)
                continue;
            if (_g.Get(value, "ImageMask").Kind == PdfValueKind.Boolean && _g.Get(value, "ImageMask").BooleanValue || Integer(value, "BitsPerComponent") != 8)
                throw Error(_c, "ImageProfile", "Resampling requires explicit eight-bit non-stencil component planes; unsupported precision is never silently reduced.");
            var embeddedAlpha = _g.Get(value, "SMaskInData");
            if (_g.Get(value, "F").Kind != PdfValueKind.Null || embeddedAlpha.Kind != PdfValueKind.Null && (!embeddedAlpha.TryGetInteger(out long alphaMode) || alphaMode != 0) || _g.Get(value, "Alternates").Kind != PdfValueKind.Null)
                throw Error(_c, "ImageProfile", "External/embedded-alpha/alternate image profiles require a separate explicit resampling adapter.");
            var color = _g.Get(value, "ColorSpace");
            int components = PdfImageColorResolver.Components(color, PdfValue.Null, _g.Resolve, _c);
            if (components > 4)
                throw Error(_c, "ImageProfile", "This resampling profile supports at most four explicit component planes.");
            if (width > long.MaxValue / height / components)
                throw Error(_c, "ImageGeometry", "The decoded component extent exceeds the supported integer range.");
            int targetWidth, targetHeight;
            if (width >= height)
            {
                targetWidth = limit;
                targetHeight = Math.Max(1, (int)((long)height * limit / width));
            }
            else
            {
                targetHeight = limit;
                targetWidth = Math.Max(1, (int)((long)width * limit / height));
            }

            _c.Report(new("Optimization.ImageDownsampled", "An eight-bit image plane is explicitly resampled by nearest-neighbor sample selection; original resolution and pixel detail are reduced.", lossCategory: PdfLossCategory.FidelityReduction, objectId: id.Number));
            _c.ConsumePixels(targetWidth, targetHeight);
            var resolved = PdfFilterDeclarations.Resolve(value, reference => _g.Edit.Resolve(reference), _c);
            bool codec = PdfFilters.Inspect(resolved).Any(f => PdfStreamFilters.Canonical(f.Name) == "CCITTFaxDecode" || PdfImageCodecPipeline.TryKind(PdfStreamFilters.Canonical(f.Name), out _));
            if (!codec)
                _c.ConsumePixels(width, height);
            _c.Consume(PdfResourceKind.InputBytes, value.RawStreamLength);
            PdfByteStore samples;
            using (var decoded = PdfFilters.Decode(resolved, _c, _providers))
                samples = PdfImages.ReadExact(decoded, checked((long)width * height * components), _c, false);
            var resized = new PdfByteBuilder(_c);
            for (int row = 0; row < targetHeight; row++)
            {
                _c.Checkpoint();
                long sourceRow = Math.Min(height - 1L, ((2L * row + 1) * height) / (2L * targetHeight));
                for (int column = 0; column < targetWidth; column++)
                {
                    if ((column & 1023) == 0)
                        _c.Checkpoint();
                    long sourceColumn = Math.Min(width - 1L, ((2L * column + 1) * width) / (2L * targetWidth));
                    long offset = checked((sourceRow * width + sourceColumn) * components);
                    for (int channel = 0; channel < components; channel++)
                        resized.Add((byte)samples.At(offset + channel));
                }
            }

            var bytes = resized.Build();
            var entries = _g.Copy(value);
            entries["Width"] = PdfValue.Integer(targetWidth);
            entries["Height"] = PdfValue.Integer(targetHeight);
            entries.Remove("Length");
            entries.Remove("Filter");
            entries.Remove("DecodeParms");
            entries.Remove("DL");
            _g.Edit.Set(id, PdfValue.StreamValue(PdfValue.OwnedDictionary(entries), bytes, 0, bytes.Length));
            Downsampled++;
        }

        int Integer(PdfValue value, string key)
        {
            var number = _g.Get(value, key);
            if (!number.TryGetInteger(out long n) || n < 1 || n > int.MaxValue)
                throw Error(_c, "ImageGeometry", "Explicit positive bounded image dimensions and precision are required.");
            return (int)n;
        }
    }
}
