using System.IO;
using Fluent.Pdf;
using BitMiracle.LibJpeg.Classic;
using CoreJ2K;
using CoreJ2K.Configuration;
using CoreJ2K.Util;
using UglyToad.PdfPig.Filters;
using UglyToad.PdfPig.Filters.Jbig2.PdfboxJbig2;
using UglyToad.PdfPig.Tokens;

internal static class CodecAlgorithms
{
    internal static void Run(bool encode, PdfImageCodec codec, PdfImageInfo info, int quality, int transform, Stream input, byte[] globals, Stream output, PdfOperationContext context)
    {
        if (codec == PdfImageCodec.Dct)
            Jpeg(encode, info, quality, transform, input, output, context);
        else if (codec == PdfImageCodec.Jpx)
            Jpx(encode, info, input, output, context);
        else if (encode)
            WriteJbig2(info, input, output, context);
        else
            ReadJbig2(info, input, globals, output, context);
    }

    private static void Jpeg(bool encode, PdfImageInfo info, int quality, int transform, Stream input, Stream output, PdfOperationContext context)
    {
        if (info.BitsPerComponent != 8 || (info.Components != 1 && info.Components != 3 && info.Components != 4))
            throw new NotSupportedException();
        var row = new byte[checked((int)info.RowBytes)];
        var rows = new[]
        {
            row
        };
        J_COLOR_SPACE space = info.Components == 1 ? J_COLOR_SPACE.JCS_GRAYSCALE : info.Components == 3 ? J_COLOR_SPACE.JCS_RGB : J_COLOR_SPACE.JCS_CMYK;
        if (encode)
        {
            var encoder = new jpeg_compress_struct(new StrictJpegErrors());
            encoder.Image_width = info.Width;
            encoder.Image_height = info.Height;
            encoder.Input_components = info.Components;
            encoder.In_color_space = space;
            encoder.jpeg_set_defaults();
            if (transform == 0)
                encoder.jpeg_set_colorspace(space);
            else if (transform == 1)
            {
                if (info.Components == 1)
                    throw new NotSupportedException();
                encoder.jpeg_set_colorspace(info.Components == 3 ? J_COLOR_SPACE.JCS_YCbCr : J_COLOR_SPACE.JCS_YCCK);
            }

            encoder.jpeg_set_quality(quality, true);
            encoder.jpeg_stdio_dest(output);
            encoder.jpeg_start_compress(true);
            while (encoder.Next_scanline < info.Height)
            {
                context.Checkpoint();
                ReadExactly(input, row);
                if (encoder.jpeg_write_scanlines(rows, 1) != 1)
                    throw new InvalidDataException();
            }

            encoder.jpeg_finish_compress();
        }
        else
        {
            var decoder = new jpeg_decompress_struct(new StrictJpegErrors());
            decoder.jpeg_stdio_src(input);
            if (decoder.jpeg_read_header(true) != ReadResult.JPEG_HEADER_OK || decoder.Image_width != info.Width || decoder.Image_height != info.Height || decoder.Num_components != info.Components || decoder.Data_precision != 8)
                throw new InvalidDataException();
            if (transform == 0)
                decoder.Jpeg_color_space = space;
            else if (transform == 1)
            {
                if (info.Components == 1)
                    throw new NotSupportedException();
                decoder.Jpeg_color_space = info.Components == 3 ? J_COLOR_SPACE.JCS_YCbCr : J_COLOR_SPACE.JCS_YCCK;
            }

            decoder.Out_color_space = space;
            decoder.Do_fancy_upsampling = true;
            if (!decoder.jpeg_start_decompress() || decoder.Output_components != info.Components)
                throw new InvalidDataException();
            while (decoder.Output_scanline < info.Height)
            {
                context.Checkpoint();
                if (decoder.jpeg_read_scanlines(rows, 1) != 1)
                    throw new InvalidDataException();
                output.Write(row);
            }

            if (!decoder.jpeg_finish_decompress())
                throw new InvalidDataException();
        }
    }

    private static void Jpx(bool encode, PdfImageInfo info, Stream input, Stream output, PdfOperationContext context)
    {
        if (info.BitsPerComponent > 16)
            throw new NotSupportedException();
        if (encode)
        {
            int pixels = checked(info.Width * info.Height);
            var planes = new int[info.Components][];
            for (int c = 0; c < planes.Length; c++)
                planes[c] = new int[pixels];
            var row = new byte[checked((int)info.RowBytes)];
            for (int y = 0; y < info.Height; y++)
            {
                context.Checkpoint();
                ReadExactly(input, row);
                int bit = 0;
                for (int x = 0; x < info.Width; x++)
                    for (int c = 0; c < info.Components; c++)
                    {
                        int value = 0;
                        for (int b = 0; b < info.BitsPerComponent; b++, bit++)
                            value = (value << 1) | ((row[bit / 8] >> (7 - bit % 8)) & 1);
                        planes[c][y * info.Width + x] = value - (1 << (info.BitsPerComponent - 1));
                    }
            }

            var source = new InterleavedImageSource(info.Width, info.Height, info.Components, info.BitsPerComponent, new bool[info.Components], planes);
            var config = new J2KEncoderConfiguration().WithLossless().WithFileFormat(false).WithMaxDegreeOfParallelism(1).WithCancellationToken(context.CancellationToken);
            J2kImage.WriteTo(output, source, config);
        }
        else
        {
            var limits = DecoderLimits.Strict.WithMaxPixels((long)info.Width * info.Height).WithMaxMemoryBytes(context.Policy.Limits.MaximumExpandedBytes).WithMaxTileComponents(context.Policy.Limits.MaximumObjects);
            using var image = J2kImage.FromStream(input, new J2KDecoderConfiguration().WithHighestResolution().WithColorSpace(false).WithLimits(limits).WithMaxDegreeOfParallelism(1).WithCancellationToken(context.CancellationToken));
            if (image.Width != info.Width || image.Height != info.Height || image.NumberOfComponents != info.Components || image.BitDepths.Any(x => x != info.BitsPerComponent))
                throw new NotSupportedException();
            var row = new byte[checked((int)info.RowBytes)];
            int maximum = (1 << info.BitsPerComponent) - 1;
            for (int y = 0; y < info.Height; y++)
            {
                context.Checkpoint();
                Array.Clear(row);
                int bit = 0;
                for (int x = 0; x < info.Width; x++)
                    for (int c = 0; c < info.Components; c++)
                    {
                        int value = image.GetSample(x, y, c);
                        if (value < 0 || value > maximum)
                            throw new InvalidDataException();
                        for (int b = info.BitsPerComponent - 1; b >= 0; b--, bit++)
                            row[bit / 8] |= (byte)(((value >> b) & 1) << (7 - bit % 8));
                    }

                output.Write(row);
            }
        }
    }

    private static void ReadJbig2(PdfImageInfo info, Stream input, byte[] globals, Stream output, PdfOperationContext context)
    {
        if (info.Components != 1 || info.BitsPerComponent != 1)
            throw new NotSupportedException();
        var empty = new DictionaryToken(new Dictionary<NameToken, IToken>());
        var parms = new Dictionary<NameToken, IToken>();
        if (globals.Length > 0)
            parms.Add(NameToken.Create("JBIG2Globals"), new StreamToken(empty, globals));
        var dictionary = new DictionaryToken(new Dictionary<NameToken, IToken> { { NameToken.Create("DecodeParms"), new DictionaryToken(parms) } });
        byte[] encoded = new byte[checked((int)input.Length)];
        ReadExactly(input, encoded);
        context.Checkpoint();
        Memory<byte> decoded = new PdfboxJbig2DecodeFilter().Decode(encoded, dictionary, new RawGlobalsFilters(), 0);
        context.Checkpoint();
        if (decoded.Length != info.ByteLength)
            throw new InvalidDataException();
        output.Write(decoded.Span);
    }

    private static void WriteJbig2(PdfImageInfo info, Stream input, Stream output, PdfOperationContext context)
    {
        if (info.Components != 1 || info.BitsPerComponent != 1)
            throw new NotSupportedException();
        var parameters = PdfValue.Dictionary(new Dictionary<string, PdfValue> { { "K", PdfValue.Integer(-1) }, { "Columns", PdfValue.Integer(info.Width) }, { "Rows", PdfValue.Integer(info.Height) }, { "BlackIs1", PdfValue.Boolean(false) } });
        using var fax = PdfFilters.Encode(input, new[] { new PdfFilter("CCITTFaxDecode", parameters) }, context);
        Segment(output, 1, 48, 19);
        Big(output, (uint)info.Width);
        Big(output, (uint)info.Height);
        Big(output, 0);
        Big(output, 0);
        output.WriteByte(0);
        output.WriteByte(0);
        output.WriteByte(0);
        Segment(output, 2, 38, checked((uint)fax.Length + 18));
        Big(output, (uint)info.Width);
        Big(output, (uint)info.Height);
        Big(output, 0);
        Big(output, 0);
        output.WriteByte(0);
        output.WriteByte(1);
        fax.CopyTo(output);
    }

    private static void Segment(Stream output, uint number, byte type, uint length)
    {
        Big(output, number);
        output.WriteByte(type);
        output.WriteByte(0);
        output.WriteByte(1);
        Big(output, length);
    }

    private static void Big(Stream output, uint value)
    {
        output.WriteByte((byte)(value >> 24));
        output.WriteByte((byte)(value >> 16));
        output.WriteByte((byte)(value >> 8));
        output.WriteByte((byte)value);
    }

    private static void ReadExactly(Stream input, byte[] bytes)
    {
        int at = 0, n;
        while (at < bytes.Length && (n = input.Read(bytes, at, bytes.Length - at)) > 0)
            at += n;
        if (at != bytes.Length)
            throw new InvalidDataException();
    }

    private sealed class StrictJpegErrors : jpeg_error_mgr
    {
        public override void error_exit() => throw new InvalidDataException("JPEG decoder error.");
        public override void emit_message(int level)
        {
            if (level < 0)
                throw new InvalidDataException("JPEG damaged input.");
        }

        public override void output_message()
        {
        }
    }

    private sealed class RawGlobalsFilters : IFilterProvider
    {
        public IReadOnlyList<IFilter> GetFilters(DictionaryToken dictionary)
        {
            if (dictionary.Data.Count != 0)
                throw new NotSupportedException();
            return Array.Empty<IFilter>();
        }

        public IReadOnlyList<IFilter> GetNamedFilters(IReadOnlyList<NameToken> names)
        {
            if (names.Count != 0)
                throw new NotSupportedException();
            return Array.Empty<IFilter>();
        }

        public IReadOnlyList<IFilter> GetAllFilters() => Array.Empty<IFilter>();
    }
}
