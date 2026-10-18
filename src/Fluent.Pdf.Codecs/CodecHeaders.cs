using System.IO;

namespace Fluent.Pdf.Codecs;
// Original header inspection. No decoder is allowed to allocate pixels here.
internal static class CodecHeaders
{
    internal static PdfImageInfo Inspect(PdfImageCodec codec, Stream stream, PdfOperationContext context) => Inspect(codec, stream, context, out _);
    internal static PdfImageInfo Inspect(PdfImageCodec codec, Stream stream, PdfOperationContext context, out long objectWork)
    {
        var r = new Reader(stream, context);
        var info = codec switch
        {
            PdfImageCodec.Dct => Jpeg(r),
            PdfImageCodec.Jpx => Jpx(r),
            PdfImageCodec.Jbig2 => Jbig2(r),
            _ => throw new ArgumentOutOfRangeException(nameof(codec))};
        objectWork = r.ObjectWork;
        return info;
    }

    private static PdfImageInfo Jpeg(Reader r)
    {
        if (r.U16() != 0xffd8)
            throw new InvalidDataException();
        for (int count = 0; count < 65536; count++)
        {
            r.ChargeObjects(1);
            if (r.Byte() != 255)
                throw new InvalidDataException();
            int marker;
            do
            {
                marker = r.Byte();
            }
            while (marker == 255);
            if (marker == 0 || marker == 0xd9 || marker == 0xda)
                throw new InvalidDataException();
            int length = r.U16();
            if (length < 2)
                throw new InvalidDataException();
            if (marker >= 0xc0 && marker <= 0xcf && marker != 0xc4 && marker != 0xc8 && marker != 0xcc)
            {
                if (marker != 0xc0 && marker != 0xc1 && marker != 0xc2)
                    throw new NotSupportedException("JPEG process");
                int bits = r.Byte(), height = r.U16(), width = r.U16(), components = r.Byte();
                if (bits != 8 || (components != 1 && components != 3 && components != 4) || length != 8 + components * 3)
                    throw new NotSupportedException("JPEG precision or components");
                r.Skip(components * 3);
                return new(width, height, components, bits);
            }

            r.Skip(length - 2);
        }

        throw new InvalidDataException();
    }

    private static PdfImageInfo Jpx(Reader r)
    {
        long end = r.Stream.Length;
        int first = r.U16();
        if (first != 0xff4f)
        {
            r.Stream.Position = 0;
            bool found = false;
            while (r.Stream.Position < end)
            {
                r.ChargeObjects(1);
                long start = r.Stream.Position, length = r.U32();
                uint type = r.U32();
                int header = 8;
                if (length == 1)
                {
                    ulong n = ((ulong)r.U32() << 32) | r.U32();
                    if (n > long.MaxValue)
                        throw new InvalidDataException();
                    length = (long)n;
                    header = 16;
                }

                if (length == 0)
                    length = end - start;
                if (length < header || length > end - start)
                    throw new InvalidDataException();
                if (type == 0x6a703263)
                {
                    end = start + length;
                    found = true;
                    break;
                }

                r.Skip(length - header);
            }

            if (!found || r.U16() != 0xff4f)
                throw new InvalidDataException();
        }

        if (r.U16() != 0xff51)
            throw new InvalidDataException();
        int size = r.U16();
        r.U16();
        uint x = r.U32(), y = r.U32(), x0 = r.U32(), y0 = r.U32();
        uint tileX = r.U32(), tileY = r.U32(), tx0 = r.U32(), ty0 = r.U32();
        int count = r.U16();
        if (x <= x0 || y <= y0 || x - x0 > int.MaxValue || y - y0 > int.MaxValue || tileX == 0 || tileY == 0 || tx0 > x0 || ty0 > y0 || count < 1 || count > 4 || size != 38 + 3 * count)
            throw new InvalidDataException();
        long tilesX = ((long)x - tx0 + tileX - 1) / tileX, tilesY = ((long)y - ty0 + tileY - 1) / tileY;
        if (tilesX > long.MaxValue / tilesY / count)
            throw new InvalidDataException();
        r.ChargeObjects(tilesX * tilesY * count);
        int bits = 0;
        for (int i = 0; i < count; i++)
        {
            int precision = r.Byte(), sx = r.Byte(), sy = r.Byte();
            if ((precision & 128) != 0 || sx != 1 || sy != 1 || (i > 0 && (precision & 127) + 1 != bits))
                throw new NotSupportedException("JPX signed, mixed precision or subsampled components");
            bits = (precision & 127) + 1;
        }

        if (r.Stream.Position > end)
            throw new InvalidDataException();
        long position = r.Stream.Position;
        if (end - position < 2)
            throw new InvalidDataException();
        r.Stream.Position = end - 2;
        if (r.U16() != 0xffd9)
            throw new InvalidDataException();
        r.Stream.Position = position;
        return new((int)(x - x0), (int)(y - y0), count, bits);
    }

    private static PdfImageInfo Jbig2(Reader r)
    {
        if (r.Stream.Length >= 8)
        {
            long start = r.Stream.Position;
            if (r.Byte() == 0x97 && r.Byte() == 0x4a && r.Byte() == 0x42 && r.Byte() == 0x32)
            {
                if (r.U32() != 0x0d0a1a0a)
                    throw new InvalidDataException();
                int flags = r.Byte();
                if ((flags & 1) == 0 || (flags & 252) != 0)
                    throw new NotSupportedException("JBIG2 organization");
                if ((flags & 2) == 0 && r.U32() != 1)
                    throw new NotSupportedException("JBIG2 multi-page file");
            }
            else
                r.Stream.Position = start;
        }

        while (r.Stream.Position < r.Stream.Length)
        {
            r.ChargeObjects(1);
            uint number = r.U32();
            int flags = r.Byte(), referred = r.Byte(), count = referred >> 5;
            if (count == 7)
            {
                uint extended = ((uint)referred << 24) | ((uint)r.Byte() << 16) | ((uint)r.Byte() << 8) | (uint)r.Byte();
                long n = extended & 0x1fffffff;
                r.ChargeObjects(n);
                r.Skip((n + 8) / 8);
                if (n > int.MaxValue)
                    throw new InvalidDataException();
                count = (int)n;
            }
            else if (count > 4)
                throw new InvalidDataException();
            r.Skip((long)count * (number <= 256 ? 1 : number <= 65536 ? 2 : 4));
            uint page = (flags & 64) != 0 ? r.U32() : (uint)r.Byte(), length = r.U32();
            if (length == uint.MaxValue)
                throw new NotSupportedException("JBIG2 indefinite segment length");
            if (length > r.Stream.Length - r.Stream.Position)
                throw new InvalidDataException();
            if ((flags & 63) == 48)
            {
                if (page != 1 || length != 19)
                    throw new NotSupportedException("JBIG2 page information");
                uint width = r.U32(), height = r.U32();
                if (width > int.MaxValue || height > int.MaxValue)
                    throw new InvalidDataException();
                return new((int)width, (int)height, 1, 1);
            }

            r.Skip(length);
        }

        throw new InvalidDataException();
    }

    private sealed class Reader
    {
        internal readonly Stream Stream;
        internal readonly PdfOperationContext Context;
        private readonly byte[] _one;
        internal long ObjectWork { get; private set; }

        internal void ChargeObjects(long count)
        {
            Context.Consume(PdfResourceKind.Objects, count);
            ObjectWork = checked(ObjectWork + count);
        }

        internal Reader(Stream stream, PdfOperationContext context)
        {
            Stream = stream;
            Context = context;
            context.Consume(PdfResourceKind.ExpandedBytes, 1);
            _one = new byte[1];
        }

        internal int Byte()
        {
            Context.Checkpoint();
            if (Stream.Read(_one, 0, 1) != 1)
                throw new InvalidDataException();
            return _one[0];
        }

        internal int U16() => (Byte() << 8) | Byte();
        internal uint U32() => ((uint)Byte() << 24) | ((uint)Byte() << 16) | ((uint)Byte() << 8) | (uint)Byte();
        internal void Skip(long length)
        {
            Context.Checkpoint();
            if (length < 0 || length > Stream.Length - Stream.Position)
                throw new InvalidDataException();
            Stream.Seek(length, SeekOrigin.Current);
        }
    }
}
