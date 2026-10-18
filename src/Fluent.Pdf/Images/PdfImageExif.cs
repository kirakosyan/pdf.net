namespace Fluent.Pdf;
internal static class PdfImageExif
{
    internal static PdfImageMetadata Read(PdfByteStore bytes, long start, long length, PdfOperationContext context)
    {
        int Byte(long p)
        {
            if (p < 0 || p >= length)
                throw PdfImages.Error(context, "Image.ExifBounds", "Exif metadata points outside its declared segment.");
            return bytes.At(start + p);
        }

        if (length < 8)
            throw PdfImages.Error(context, "Image.ExifHeader", "An Exif profile requires a complete TIFF header.");
        bool little = Byte(0) == 'I' && Byte(1) == 'I';
        if (!little && (Byte(0) != 'M' || Byte(1) != 'M'))
            throw PdfImages.Error(context, "Image.ExifEndian", "Exif metadata requires TIFF byte order.");
        uint U16(long p) => little ? (uint)(Byte(p) | Byte(p + 1) << 8) : (uint)(Byte(p) << 8 | Byte(p + 1));
        uint U32(long p) => little ? U16(p) | U16(p + 2) << 16 : U16(p) << 16 | U16(p + 2);
        if (U16(2) != 42)
            throw PdfImages.Error(context, "Image.ExifHeader", "This image requires the classic TIFF Exif metadata profile.");
        long ifd = U32(4);
        int count = (int)U16(ifd);
        if (ifd < 8 || ifd > length - 6L - count * 12L)
            throw PdfImages.Error(context, "Image.ExifBounds", "The Exif directory is truncated.");
        context.Consume(PdfResourceKind.Objects, count);
        context.Consume(PdfResourceKind.ExpandedBytes, 512);
        var seen = new HashSet<uint>();
        int orientation = 1, unit = 2;
        double horizontal = 0, vertical = 0;
        for (int i = 0; i < count; i++)
        {
            context.Checkpoint();
            long entry = ifd + 2 + i * 12L;
            uint tag = U16(entry);
            if (tag != 274 && tag != 282 && tag != 283 && tag != 296)
                continue;
            if (!seen.Add(tag) || U32(entry + 4) != 1)
                throw PdfImages.Error(context, "Image.ExifTag", "Exif display tags must be unique scalar values.");
            uint type = U16(entry + 2);
            if (tag == 274 || tag == 296)
            {
                if (type != 3)
                    throw PdfImages.Error(context, "Image.ExifTag", "Exif orientation and resolution unit require SHORT values.");
                int n = (int)U16(entry + 8);
                if (tag == 274)
                    orientation = n;
                else
                    unit = n;
            }
            else
            {
                if (type != 5)
                    throw PdfImages.Error(context, "Image.ExifTag", "Exif resolution requires unsigned rational values.");
                long offset = U32(entry + 8);
                uint numerator = U32(offset), denominator = U32(offset + 4);
                if (numerator == 0 || denominator == 0)
                    throw PdfImages.Error(context, "Image.ExifResolution", "Exif density requires a positive finite rational.");
                double value = (double)numerator / denominator;
                if (tag == 282)
                    horizontal = value;
                else
                    vertical = value;
            }
        }

        if (orientation < 1 || orientation > 8 || unit < 1 || unit > 3 || (horizontal == 0) != (vertical == 0))
            throw PdfImages.Error(context, "Image.ExifDisplay", "Exif display orientation or paired resolution is invalid.");
        double aspect = horizontal > 0 ? vertical / horizontal : 1;
        return unit == 1 ? new((PdfImageOrientation)orientation, pixelAspectRatio: aspect) : new((PdfImageOrientation)orientation, horizontal * (unit == 3 ? 2.54 : 1), vertical * (unit == 3 ? 2.54 : 1));
    }
}
