namespace Fluent.Pdf;
internal static class PdfImagePng
{
    private static readonly uint[] CrcTable = BuildCrc();
    private static uint[] BuildCrc()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint v = i;
            for (int b = 0; b < 8; b++)
                v = (v & 1) == 0 ? v >> 1 : (v >> 1) ^ 0xedb88320;
            table[i] = v;
        }

        return table;
    }

    internal static PdfImageAsset Read(PdfByteStore bytes, PdfImageReadOptions options, PdfOperationContext context)
    {
        uint U32(long p)
        {
            if (p < 0 || p > bytes.Length - 4)
                throw Error("Image.PngBounds", "A PNG integer is truncated.");
            return (uint)bytes.At(p) << 24 | (uint)bytes.At(p + 1) << 16 | (uint)bytes.At(p + 2) << 8 | (uint)bytes.At(p + 3);
        }

        long position = 8;
        int width = 0, height = 0, depth = 0, type = -1, components = 0, interlace = 0, srgbIntent = 0;
        bool sawData = false, endedData = false, ended = false, srgb = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var data = new PdfByteBuilder(context);
        byte[]? palette = null, transparency = null;
        PdfByteStore? icc = null;
        double gamma = 0;
        uint[]? chroma = null;
        PdfImageMetadata? exif = null;
        PdfImageMetadata density = new();
        while (position < bytes.Length)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 128);
            long n = U32(position), start = position + 8;
            if (n > int.MaxValue || n > bytes.Length - start - 4)
                throw Error("Image.PngChunk", "A PNG chunk exceeds the encoded file.");
            string name = new(new[] { (char)bytes.At(position + 4), (char)bytes.At(position + 5), (char)bytes.At(position + 6), (char)bytes.At(position + 7) });
            if (name.Any(c => c < 'A' || c > 'Z' && c < 'a' || c > 'z') || (name[2] & 32) != 0)
                throw Error("Image.PngChunkType", "PNG chunk names require ASCII letters and a valid reserved bit.");
            uint crc = 0xffffffff;
            for (long p = position + 4; p < start + n; p++)
            {
                if ((p & 4095) == 0)
                    context.Checkpoint();
                crc = CrcTable[(crc ^ (byte)bytes.At(p)) & 255] ^ (crc >> 8);
            }

            if ((crc ^ 0xffffffff) != U32(start + n))
                throw Error("Image.PngCrc", "A PNG chunk has an invalid CRC.");
            if (type < 0 && name != "IHDR")
                throw Error("Image.PngOrder", "IHDR must be the first PNG chunk.");
            if (name == "acTL" || name == "fcTL" || name == "fdAT")
                throw Error("Image.AnimationSelection", "Animated PNG requires an explicit frame-selection service.");
            bool singleton = name != "IDAT" && new[]
            {
                "IHDR",
                "PLTE",
                "IEND",
                "tRNS",
                "iCCP",
                "sRGB",
                "gAMA",
                "cHRM",
                "pHYs",
                "eXIf"
            }.Contains(name);
            if (singleton && !seen.Add(name))
                throw Error("Image.PngDuplicate", "A PNG repeats a singleton image/display chunk.");
            byte[] Copy()
            {
                context.Consume(PdfResourceKind.ExpandedBytes, n * 2 + 32);
                var result = new byte[(int)n];
                for (int i = 0; i < result.Length; i++)
                    result[i] = (byte)bytes.At(start + i);
                return result;
            }

            if (name == "IHDR")
            {
                if (n != 13 || U32(start) == 0 || U32(start) > int.MaxValue || U32(start + 4) == 0 || U32(start + 4) > int.MaxValue)
                    throw Error("Image.PngHeader", "PNG requires a complete positive bounded IHDR.");
                width = (int)U32(start);
                height = (int)U32(start + 4);
                depth = bytes.At(start + 8);
                type = bytes.At(start + 9);
                interlace = bytes.At(start + 12);
                bool validDepth = type == 0 ? new[]
                {
                    1,
                    2,
                    4,
                    8,
                    16
                }.Contains(depth) : type == 3 ? new[]
                {
                    1,
                    2,
                    4,
                    8
                }.Contains(depth) : (type == 2 || type == 4 || type == 6) && (depth == 8 || depth == 16);
                if (!validDepth || bytes.At(start + 10) != 0 || bytes.At(start + 11) != 0 || interlace > 1)
                    throw Error("Image.PngProfile", "PNG color, precision, compression, filter or interlace declarations are invalid.");
                components = type == 2 ? 3 : type == 4 ? 2 : type == 6 ? 4 : 1;
                context.ConsumePixels(width, height);
            }
            else if (name == "IDAT")
            {
                if (endedData || type == 3 && palette == null)
                    throw Error("Image.PngOrder", "PNG IDAT chunks must be contiguous and follow the required palette.");
                sawData = true;
                for (long i = 0; i < n; i++)
                {
                    if ((i & 4095) == 0)
                        context.Checkpoint();
                    data.Add((byte)bytes.At(start + i));
                }
            }
            else if (name == "IEND")
            {
                if (n != 0 || !sawData)
                    throw Error("Image.PngEnd", "PNG requires a final empty IEND after image data.");
                ended = true;
            }
            else if (name == "PLTE")
            {
                if (sawData || n == 0 || n > 768 || n % 3 != 0 || type == 0 || type == 4 || type == 3 && n / 3 > 1 << depth)
                    throw Error("Image.PngPalette", "The PNG palette is invalid for its color type/precision.");
                palette = Copy();
            }
            else if (name == "tRNS")
            {
                if (sawData || type == 4 || type == 6 || type == 3 && (palette == null || n < 1 || n > palette.Length / 3) || type == 0 && n != 2 || type == 2 && n != 6)
                    throw Error("Image.PngTransparency", "PNG transparency must precede image data and cannot duplicate an alpha channel.");
                transparency = Copy();
            }
            else if (name == "iCCP")
            {
                if (sawData || palette != null || srgb)
                    throw Error("Image.PngColorOrder", "PNG ICC metadata must precede palette/data and cannot conflict with sRGB.");
                int zero = 0;
                while (zero < n && bytes.At(start + zero) != 0)
                    zero++;
                if (zero < 1 || zero > 79 || n < zero + 3 || bytes.At(start + zero + 1) != 0)
                    throw Error("Image.PngIcc", "The PNG ICC name/compression header is invalid.");
                using var profile = bytes.Open(start + zero + 2, n - zero - 2);
                icc = PdfInflate.Decode(profile, context);
            }
            else if (name == "sRGB")
            {
                if (sawData || palette != null || n != 1 || bytes.At(start) > 3 || icc != null)
                    throw Error("Image.PngColorOrder", "PNG sRGB metadata is malformed or conflicts with ICC metadata.");
                srgb = true;
                srgbIntent = bytes.At(start);
            }
            else if (name == "gAMA")
            {
                if (sawData || palette != null || n != 4 || U32(start) == 0)
                    throw Error("Image.PngGamma", "PNG gamma must be a positive value before palette/data.");
                gamma = 100000d / U32(start);
            }
            else if (name == "cHRM")
            {
                if (sawData || palette != null || n != 32)
                    throw Error("Image.PngChroma", "PNG chromaticities require eight values before palette/data.");
                context.Consume(PdfResourceKind.ExpandedBytes, 64);
                chroma = Enumerable.Range(0, 8).Select(i => U32(start + i * 4)).ToArray();
            }
            else if (name == "pHYs")
            {
                if (sawData || n != 9 || U32(start) == 0 || U32(start + 4) == 0 || bytes.At(start + 8) > 1)
                    throw Error("Image.PngDensity", "PNG density requires positive paired values and a standard unit before data.");
                double x = U32(start), y = U32(start + 4);
                density = bytes.At(start + 8) == 0 ? new(pixelAspectRatio: y / x) : new(horizontalDpi: x * .0254, verticalDpi: y * .0254);
            }
            else if (name == "eXIf")
                exif = PdfImageExif.Read(bytes, start, n, context);
            else if (name == "cICP" || name == "mDCV" || name == "cLLI")
            {
                if (options.ColorSpaceOverride == null)
                    throw Error("Image.PngColorProfile", "This PNG color/HDR metadata requires an explicit matching color-space profile.");
            }
            else if ((name[0] & 32) == 0)
                throw Error("Image.PngCriticalChunk", "The PNG declares an unsupported critical chunk.");
            if (sawData && name != "IDAT")
                endedData = true;
            position = start + n + 4;
            if (ended)
                break;
        }

        if (!ended || position != bytes.Length)
            throw Error("Image.PngExtent", "PNG requires one complete image with no trailing payload.");
        var info = new PdfImageInfo(width, height, components, depth);
        var encoded = data.Build();
        PdfColorSpace color = options.ColorSpaceOverride ?? (type == 0 || type == 4 ? PdfColorSpace.Gray : PdfColorSpace.Rgb);
        if (icc != null)
        {
            if (options.ColorSpaceOverride != null)
                throw Error("Image.ColorOverrideConflict", "An embedded ICC profile must be preserved; color conversion is a separate operation.");
            color = PdfColorSpace.IccCore(icc, null, context);
        }
        else if (options.ColorSpaceOverride == null)
        {
            if (srgb)
                color = PdfStandardImageProfiles.Srgb(type == 0 || type == 4, context);
            else if (gamma > 0 || chroma != null)
            {
                if (gamma == 0 || chroma == null)
                    throw Error("Image.PngColorProfile", "Incomplete PNG calibration requires an explicit matching color-space profile.");
                color = Calibrated(chroma, gamma, type == 0 || type == 4, context);
            }
        }

        if (color.Components != (type == 0 || type == 4 ? 1 : 3))
            throw Error("Image.ColorComponents", "PNG color metadata disagrees with its Gray/RGB sample interpretation.");
        if (type == 3)
            color = PdfColorSpace.IndexedCore(color, palette!.Length / 3 - 1, palette, context);
        var selectedDensity = !seen.Contains("pHYs") && exif != null ? exif : density;
        var metadata = new PdfImageMetadata(options.ApplyExifOrientation ? exif?.Orientation ?? PdfImageOrientation.Normal : PdfImageOrientation.Normal, selectedDensity.HorizontalDpi, selectedDensity.VerticalDpi, selectedDensity.PixelAspectRatio);
        if ((interlace != 0 || type == 4 || type == 6) && !options.AllowLosslessTranscode)
            throw Error("Image.LosslessTranscodeRequired", "PNG alpha separation or deinterlacing requires explicit lossless transcode permission.");
        var parameters = Parameters(width, components, depth);
        PdfByteStore inflated;
        using (var compressed = encoded.Open(0, encoded.Length))
            inflated = PdfInflate.Decode(compressed, context);
        var pixels = DecodeRows(inflated, info, interlace, parameters, context, out bool converted);
        if (type == 3)
            for (int y = 0; y < height; y++)
            {
                context.Checkpoint();
                for (int x = 0; x < width; x++)
                {
                    if ((x & 1023) == 0)
                        context.Checkpoint();
                    if (Sample(pixels, y * info.RowBytes, x, depth) >= palette!.Length / 3)
                        throw Error("Image.PngPaletteIndex", "PNG samples exceed the actual palette.");
                }
            }

        bool alpha = type == 4 || type == 6;
        PdfImageAsset? mask = null;
        double[]? colorKey = null;
        if (alpha)
        {
            if (!options.AllowLosslessTranscode)
                throw Error("Image.LosslessTranscodeRequired", "Separating PNG alpha requires explicit permission for a lossless channel transcode.");
            context.ConsumePixels(width, height);
            int sampleBytes = depth / 8, colorChannels = components - 1;
            var rgb = new PdfByteBuilder(context);
            var a = new PdfByteBuilder(context);
            for (long p = 0; p < pixels.Length; p += components * sampleBytes)
            {
                context.Checkpoint();
                for (int i = 0; i < colorChannels * sampleBytes; i++)
                    rgb.Add((byte)pixels.At(p + i));
                for (int i = 0; i < sampleBytes; i++)
                    a.Add((byte)pixels.At(p + colorChannels * sampleBytes + i));
            }

            mask = PdfImages.Build(a.Build(), new(width, height, 1, depth), PdfColorSpace.Gray.Definition, null, null, false, new(), false, context);
            pixels = rgb.Build();
            info = new(width, height, colorChannels, depth);
            converted = true;
        }

        if (transparency != null)
        {
            if (type == 3)
            {
                if (transparency.Length == 0 || transparency.Length > palette!.Length / 3)
                    throw Error("Image.PngTransparency", "The palette alpha table exceeds its palette.");
                var zeros = Enumerable.Range(0, transparency.Length).Where(i => transparency[i] == 0).ToArray();
                bool binary = transparency.All(v => v == 0 || v == 255), contiguous = zeros.Length == 0 || zeros[zeros.Length - 1] - zeros[0] + 1 == zeros.Length;
                if (binary && contiguous)
                {
                    if (zeros.Length > 0)
                        colorKey = new double[]
                        {
                            zeros[0],
                            zeros[zeros.Length - 1]
                        };
                }
                else
                {
                    if (!options.AllowLosslessTranscode)
                        throw Error("Image.LosslessTranscodeRequired", "A PNG palette alpha mask requires explicit lossless transcode permission.");
                    context.ConsumePixels(width, height);
                    var a = new PdfByteBuilder(context);
                    for (int y = 0; y < height; y++)
                    {
                        context.Checkpoint();
                        for (int x = 0; x < width; x++)
                        {
                            int index = Sample(pixels, y * info.RowBytes, x, depth);
                            if (index >= palette.Length / 3)
                                throw Error("Image.PngPaletteIndex", "PNG samples exceed the actual palette.");
                            a.Add(index < transparency.Length ? transparency[index] : (byte)255);
                        }
                    }

                    mask = PdfImages.Build(a.Build(), new(width, height, 1, 8), PdfColorSpace.Gray.Definition, null, null, false, new(), false, context);
                    context.Report(new("Image.PaletteMask", "The explicit policy expanded palette transparency into a separate alpha mask without changing color samples.", severity: PdfDiagnosticSeverity.Information));
                }
            }
            else
            {
                int channels = type == 0 ? 1 : 3;
                if (transparency.Length != channels * 2)
                    throw Error("Image.PngTransparency", "A PNG color key must declare one complete sample value per color component.");
                colorKey = new double[channels * 2];
                for (int c = 0; c < channels; c++)
                {
                    int n = transparency[c * 2] * 256 + transparency[c * 2 + 1];
                    if (n >= 1L << depth)
                        throw Error("Image.PngTransparency", "A PNG color key exceeds component precision.");
                    colorKey[c * 2] = colorKey[c * 2 + 1] = n;
                }
            }
        }

        if (interlace != 0 && !options.AllowLosslessTranscode)
            throw Error("Image.LosslessTranscodeRequired", "PNG deinterlacing requires explicit permission for a lossless layout transcode.");
        if (converted)
            context.Report(new("Image.LosslessTranscode", "The explicit policy authorized lossless alpha separation or deinterlacing; sample precision is retained.", severity: PdfDiagnosticSeverity.Information));
        var result = PdfImages.Build(converted ? pixels : encoded, info, color.Definition, mask, null, false, metadata, false, context, converted ? null : new[] { new PdfFilter("FlateDecode", parameters) }, bytes, "image/png", color.MinimumVersion, srgb ? PdfImages.IntentName(srgbIntent) : icc != null ? PdfImages.IntentName(icc.At(67)) : null, !options.ApplyExifOrientation && exif != null && exif.Orientation != PdfImageOrientation.Normal);
        if (colorKey != null)
        {
            var entries = PdfWriterCore.CopyDictionary(result.Definition.Entries, context);
            entries["Mask"] = PdfGraphicsNumbers.Array(colorKey);
            var resource = PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.XObject, result.Definition.WithStreamDictionary(PdfValue.OwnedDictionary(entries)), result.Resource.MinimumVersion, PdfResourceSet.Empty, context);
            result = new(resource, info, metadata, mask, bytes, "image/png");
        }

        return result;
        PdfException Error(string code, string message) => PdfImages.Error(context, code, message);
    }

    private static PdfValue Parameters(int width, int components, int depth) => PdfGraphicsNumbers.Dict(("Predictor", PdfValue.Integer(15)), ("Colors", PdfValue.Integer(components)), ("BitsPerComponent", PdfValue.Integer(depth)), ("Columns", PdfValue.Integer(width)));
    private static int Sample(PdfByteStore bytes, long row, int sample, int depth)
    {
        long bit = (long)sample * depth;
        int value = bytes.At(row + bit / 8);
        return depth == 16 ? value * 256 + bytes.At(row + bit / 8 + 1) : value >> (8 - depth - (int)(bit % 8)) & ((1 << depth) - 1);
    }

    private static PdfByteStore DecodeRows(PdfByteStore data, PdfImageInfo info, int interlace, PdfValue parameters, PdfOperationContext context, out bool converted)
    {
        converted = interlace != 0;
        if (interlace == 0)
        {
            if (data.Length != info.ByteLength + info.Height)
                throw PdfImages.Error(context, "Image.PngSampleLength", "PNG scanlines disagree with declared geometry.");
            return PdfStreamFilters.Predict(data, parameters, context);
        }

        if (info.ByteLength > int.MaxValue)
            throw PdfImages.Error(context, "Image.PngInterlaceExtent", "The requested PNG interlace layout exceeds the managed plane profile.");
        context.Consume(PdfResourceKind.ExpandedBytes, info.ByteLength + 256);
        var output = new byte[(int)info.ByteLength];
        int[] xs =
        {
            0,
            4,
            0,
            2,
            0,
            1,
            0
        }, ys =
        {
            0,
            0,
            4,
            0,
            2,
            0,
            1
        }, dx =
        {
            8,
            8,
            4,
            4,
            2,
            2,
            1
        }, dy =
        {
            8,
            8,
            8,
            4,
            4,
            2,
            2
        };
        long offset = 0;
        for (int pass = 0; pass < 7; pass++)
        {
            int width = info.Width <= xs[pass] ? 0 : (int)(((long)info.Width - xs[pass] + dx[pass] - 1) / dx[pass]), height = info.Height <= ys[pass] ? 0 : (int)(((long)info.Height - ys[pass] + dy[pass] - 1) / dy[pass]);
            if (width == 0 || height == 0)
                continue;
            var shape = new PdfImageInfo(width, height, info.Components, info.BitsPerComponent);
            long length = shape.ByteLength + height;
            if (length > data.Length - offset)
                throw PdfImages.Error(context, "Image.PngSampleLength", "An Adam7 pass is truncated.");
            var pixels = PdfStreamFilters.Predict(data, Parameters(width, info.Components, info.BitsPerComponent), context, offset, length);
            offset += length;
            for (int y = 0; y < height; y++)
            {
                context.Checkpoint();
                for (int x = 0; x < width; x++)
                    for (int c = 0; c < info.Components; c++)
                    {
                        if ((x & 1023) == 0)
                            context.Checkpoint();
                        int value = Sample(pixels, y * shape.RowBytes, x * info.Components + c, info.BitsPerComponent);
                        long bit = ((long)(ys[pass] + y * dy[pass]) * info.RowBytes * 8) + ((long)(xs[pass] + x * dx[pass]) * info.Components + c) * info.BitsPerComponent;
                        int p = (int)(bit / 8);
                        if (info.BitsPerComponent == 16)
                        {
                            output[p] = (byte)(value >> 8);
                            output[p + 1] = (byte)value;
                        }
                        else
                        {
                            int shift = 8 - info.BitsPerComponent - (int)(bit % 8);
                            output[p] |= (byte)(value << shift);
                        }
                    }
            }
        }

        if (offset != data.Length)
            throw PdfImages.Error(context, "Image.PngSampleLength", "Unexpected scanline data follows the Adam7 passes.");
        var builder = new PdfByteBuilder(context);
        foreach (byte b in output)
            builder.Add(b);
        return builder.Build();
    }

    private static PdfColorSpace Calibrated(uint[] values, double gamma, bool gray, PdfOperationContext context)
    {
        double X(int i) => values[i] / 100000d;
        double wx = X(0), wy = X(1);
        if (wy <= 0 || wx <= 0 || wx + wy >= 1)
            throw PdfImages.Error(context, "Image.PngChroma", "PNG white chromaticity must define a positive XYZ white point.");
        if (gray)
            return PdfColorSpace.CalGray(wx / wy, (1 - wx - wy) / wy, gamma);
        var p = new double[9];
        for (int c = 0; c < 3; c++)
        {
            double x = X(2 + c * 2), y = X(3 + c * 2);
            if (x < 0 || y <= 0 || x + y > 1)
                throw PdfImages.Error(context, "Image.PngChroma", "PNG primaries require finite valid chromaticities.");
            p[c] = x / y;
            p[3 + c] = 1;
            p[6 + c] = (1 - x - y) / y;
        }

        double determinant = p[0] * (p[4] * p[8] - p[5] * p[7]) - p[1] * (p[3] * p[8] - p[5] * p[6]) + p[2] * (p[3] * p[7] - p[4] * p[6]);
        if (determinant == 0 || double.IsInfinity(determinant))
            throw PdfImages.Error(context, "Image.PngChroma", "PNG primaries must form a nondegenerate calibration matrix.");
        double[] white =
        {
            wx / wy,
            1,
            (1 - wx - wy) / wy
        };
        var matrix = new double[9];
        for (int c = 0; c < 3; c++)
        {
            var replaced = (double[])p.Clone();
            for (int r = 0; r < 3; r++)
                replaced[r * 3 + c] = white[r];
            double d = replaced[0] * (replaced[4] * replaced[8] - replaced[5] * replaced[7]) - replaced[1] * (replaced[3] * replaced[8] - replaced[5] * replaced[6]) + replaced[2] * (replaced[3] * replaced[7] - replaced[4] * replaced[6]);
            double scale = d / determinant;
            if (scale <= 0 || double.IsInfinity(scale) || double.IsNaN(scale))
                throw PdfImages.Error(context, "Image.PngChroma", "PNG primaries do not enclose the declared white point.");
            for (int r = 0; r < 3; r++)
                matrix[c * 3 + r] = p[r * 3 + c] * scale;
        }

        return PdfColorSpace.CalRgb(white[0], white[2], new[] { gamma, gamma, gamma }, matrix);
    }
}
