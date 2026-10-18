namespace Fluent.Pdf;
internal static class PdfImageJpeg
{
    internal static PdfImageAsset Read(PdfByteStore bytes, PdfImageReadOptions options, PdfOperationContext context)
    {
        long position = 0;
        int Byte()
        {
            if ((position & 4095) == 0)
                context.Checkpoint();
            int value = bytes.At(position++);
            if (value < 0)
                throw Error("Image.JpegTruncated", "The JPEG marker/scan data is truncated.");
            return value;
        }

        int U16() => Byte() * 256 + Byte();
        bool Prefix(long p, int length, string value)
        {
            if (length < value.Length)
                return false;
            for (int i = 0; i < value.Length; i++)
                if (bytes.At(p + i) != value[i])
                    return false;
            return true;
        }

        if (U16() != 0xffd8)
            throw Error("Image.JpegHeader", "The image has no JPEG SOI marker.");
        PdfImageInfo? info = null;
        PdfImageMetadata? exif = null;
        PdfImageMetadata density = new();
        var profiles = new Dictionary<int, (long Start, int Length)>();
        int profileCount = 0;
        int? adobe = null, renderingIntent = null;
        bool rgbIds = false, scan = false, sawScan = false;
        while (true)
        {
            context.Checkpoint();
            int b = Byte();
            if (b != 255)
            {
                if (scan)
                    continue;
                throw Error("Image.JpegMarker", "A JPEG marker prefix is missing.");
            }

            int marker;
            do
            {
                marker = Byte();
            }
            while (marker == 255);
            if (scan && (marker == 0 || marker >= 208 && marker <= 215))
                continue;
            if (marker == 217)
                break;
            if (marker == 0 || marker == 216 || marker >= 208 && marker <= 215)
                throw Error("Image.JpegMarker", "An unexpected standalone JPEG marker occurs.");
            if (marker == 1)
                continue;
            context.Consume(PdfResourceKind.Objects, 1);
            int length = U16() - 2;
            long start = position;
            if (length < 0 || length > bytes.Length - start)
                throw Error("Image.JpegSegment", "A JPEG segment exceeds the encoded file.");
            if (marker >= 0xc0 && marker <= 0xcf && marker != 0xc4 && marker != 0xc8 && marker != 0xcc)
            {
                if (marker != 0xc0 && marker != 0xc1 && marker != 0xc2 || info != null || length < 6)
                    throw Error("Image.JpegProcess", "This JPEG requires an additional frame/process profile.");
                int bits = Byte(), height = U16(), width = U16(), components = Byte();
                if (bits != 8 || width == 0 || height == 0 || components != 1 && components != 3 && components != 4 || length != 6 + components * 3)
                    throw Error("Image.JpegGeometry", "JPEG packaging requires positive 8-bit Gray, RGB or CMYK geometry.");
                context.ConsumePixels(width, height);
                info = new(width, height, components, bits);
                rgbIds = components == 3 && bytes.At(position) == 'R' && bytes.At(position + 3) == 'G' && bytes.At(position + 6) == 'B';
            }

            if (marker == 0xe0 && Prefix(start, length, "JFIF\0"))
            {
                if (length < 14)
                    throw Error("Image.JfifDensity", "The JFIF density header is truncated.");
                int unit = bytes.At(start + 7), x = bytes.At(start + 8) * 256 + bytes.At(start + 9), y = bytes.At(start + 10) * 256 + bytes.At(start + 11);
                if (unit > 2 || x == 0 || y == 0)
                    throw Error("Image.JfifDensity", "JFIF requires positive paired density and a standard unit.");
                density = unit == 0 ? new(pixelAspectRatio: (double)y / x) : new(horizontalDpi: x * (unit == 2 ? 2.54 : 1), verticalDpi: y * (unit == 2 ? 2.54 : 1));
            }

            if (marker == 0xe1 && Prefix(start, length, "Exif\0\0"))
            {
                if (exif != null)
                    throw Error("Image.ExifDuplicate", "The JPEG repeats Exif display metadata.");
                exif = PdfImageExif.Read(bytes, start + 6, length - 6, context);
            }

            if (marker == 0xe2 && Prefix(start, length, "ICC_PROFILE\0"))
            {
                if (length < 14)
                    throw Error("Image.IccSegments", "A JPEG ICC segment is truncated.");
                int sequence = bytes.At(start + 12), total = bytes.At(start + 13);
                if (total < 1 || sequence < 1 || sequence > total || profileCount != 0 && total != profileCount || profiles.ContainsKey(sequence))
                    throw Error("Image.IccSegments", "JPEG ICC segment numbering is inconsistent.");
                profileCount = total;
                context.Consume(PdfResourceKind.ExpandedBytes, 96);
                profiles.Add(sequence, (start + 14, length - 14));
            }

            if (marker == 0xee && Prefix(start, length, "Adobe"))
            {
                if (length < 12 || adobe.HasValue || bytes.At(start + 11) > 2)
                    throw Error("Image.JpegTransform", "JPEG color-transform metadata is invalid or repeated.");
                adobe = bytes.At(start + 11);
            }

            if (marker == 218)
            {
                if (info == null || length < 6)
                    throw Error("Image.JpegScan", "A JPEG scan requires a preceding frame and complete scan header.");
                sawScan = true;
            }

            position = start + length;
            scan = marker == 218 || scan && marker == 220;
        }

        if (info == null || !sawScan || position != bytes.Length)
            throw Error("Image.JpegExtent", "A JPEG requires one complete image with no trailing payload.");
        if (adobe == 1 && info.Components != 3 || adobe == 2 && info.Components != 4)
            throw Error("Image.JpegTransform", "JPEG Adobe color-transform metadata disagrees with its components.");
        var color = options.ColorSpaceOverride ?? (info.Components == 1 ? PdfColorSpace.Gray : info.Components == 3 ? PdfColorSpace.Rgb : PdfColorSpace.Cmyk);
        if (profileCount > 0)
        {
            if (profiles.Count != profileCount)
                throw Error("Image.IccSegments", "A JPEG ICC profile is missing segments.");
            if (options.ColorSpaceOverride != null)
                throw Error("Image.ColorOverrideConflict", "An embedded ICC profile must be preserved; explicit color conversion is a separate operation.");
            var profile = new PdfByteBuilder(context);
            for (int i = 1; i <= profileCount; i++)
            {
                var part = profiles[i];
                for (int j = 0; j < part.Length; j++)
                {
                    if ((j & 4095) == 0)
                        context.Checkpoint();
                    profile.Add((byte)bytes.At(part.Start + j));
                }
            }

            var profileBytes = profile.Build();
            color = PdfColorSpace.IccCore(profileBytes, null, context);
            renderingIntent = profileBytes.At(67);
        }

        var sourceMetadata = exif ?? density;
        var chosenDensity = exif != null && (exif.HorizontalDpi > 0 || exif.PixelAspectRatio != 1) ? exif : density;
        var metadata = new PdfImageMetadata(options.ApplyExifOrientation ? sourceMetadata.Orientation : PdfImageOrientation.Normal, chosenDensity.HorizontalDpi, chosenDensity.VerticalDpi, chosenDensity.PixelAspectRatio);
        PdfImages.Validate(info, color, null, null, context);
        int transform = info.Components == 3 ? adobe == 0 || rgbIds ? 0 : 1 : info.Components == 4 && adobe == 2 ? 1 : 0;
        double[]? decode = info.Components == 4 && adobe.HasValue ? new double[]
        {
            1,
            0,
            1,
            0,
            1,
            0,
            1,
            0
        }

        : null;
        return PdfImages.Build(bytes, info, color.Definition, null, decode, false, metadata, false, context, new[] { new PdfFilter("DCTDecode", PdfGraphicsNumbers.Dict(("ColorTransform", PdfValue.Integer(transform)))) }, bytes, "image/jpeg", color.MinimumVersion, renderingIntent.HasValue ? PdfImages.IntentName(renderingIntent.Value) : null, !options.ApplyExifOrientation && exif != null && exif.Orientation != PdfImageOrientation.Normal);
        PdfException Error(string code, string message) => PdfImages.Error(context, code, message);
    }
}
