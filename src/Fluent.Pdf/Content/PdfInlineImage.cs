using System.IO;

namespace Fluent.Pdf;
internal static class PdfInlineImage
{
    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.Ordinal)
    {
        ["W"] = "Width",
        ["H"] = "Height",
        ["BPC"] = "BitsPerComponent",
        ["CS"] = "ColorSpace",
        ["D"] = "Decode",
        ["DP"] = "DecodeParms",
        ["F"] = "Filter",
        ["IM"] = "ImageMask",
        ["I"] = "Interpolate"
    };
    internal static PdfContentOperation Read(PdfSyntaxCursor cursor, Func<PdfValue, int>? colorComponents = null)
    {
        var context = cursor.Context;
        var entries = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
        while (!cursor.TryKeyword("ID"))
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 128);
            var key = cursor.Value();
            if (key.Kind != PdfValueKind.Name)
                throw PdfContentState.Error(context, "Content.InlineDictionary", "Inline-image keys must be names.");
            string name = Expand(key.NameValue);
            if (entries.ContainsKey(name))
                throw PdfContentState.Error(context, "Content.InlineDuplicateKey", "An inline image repeats a canonical dictionary key.");
            entries.Add(name, cursor.Value());
        }

        int white = cursor.Peek();
        if (!PdfSyntaxCursor.White(white))
            throw PdfContentState.Error(context, "Content.InlineSeparator", "ID requires a whitespace separator.");
        cursor.Position++;
        if (white == 13 && cursor.Peek() == 10)
            cursor.Position++;
        var dictionary = PdfValue.OwnedDictionary(entries);
        long start = cursor.Position;
        var preliminary = PdfContentOperation.ImageOperation(PdfValue.StreamValue(dictionary, cursor.Source, start, 0));
        Validate(preliminary, context);
        long length = DataLength(dictionary, cursor.Source, start, context, colorComponents);
        cursor.Position = start + length;
        if (!PdfSyntaxCursor.White(cursor.Peek()))
            throw PdfContentState.Error(context, "Content.InlineTerminator", "Encoded inline data requires whitespace before EI.");
        cursor.Skip();
        cursor.Require("EI");
        var result = PdfContentOperation.ImageOperation(PdfValue.StreamValue(dictionary, cursor.Source, start, length));
        return result;
    }

    private static string Expand(string key) => Abbreviations.TryGetValue(key, out string? name) ? name : key;
    private static PdfValue Entry(PdfValue dictionary, string key)
    {
        if (dictionary.Entries.TryGetValue(key, out var value))
            return value;
        foreach (var pair in Abbreviations)
            if (pair.Value == key && dictionary.Entries.TryGetValue(pair.Key, out value))
                return value;
        return PdfValue.Null;
    }

    private static long Positive(PdfValue dictionary, string key, PdfOperationContext context)
    {
        var value = Entry(dictionary, key);
        if (value.Kind != PdfValueKind.Number || !value.TryGetInteger(out long integer) || integer <= 0 || integer > int.MaxValue)
            throw PdfContentState.Error(context, "Content.InlineGeometry", "Inline images require positive 32-bit integer dimensions.");
        return integer;
    }

    internal static void Validate(PdfContentOperation operation, PdfOperationContext context, bool chargePixels = true)
    {
        var image = operation.Image!;
        long width = Positive(image, "Width", context), height = Positive(image, "Height", context);
        if (chargePixels) context.ConsumePixels(width, height);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in image.Entries)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 64);
            if (!seen.Add(Expand(pair.Key)))
                throw PdfContentState.Error(context, "Content.InlineDuplicateKey", "An inline image repeats a canonical key or abbreviation.");
            PdfContentProgram.Inspect(pair.Value, context);
        }

        bool mask = Mask(image, context);
        var bits = Entry(image, "BitsPerComponent");
        if (!mask && (bits.Kind != PdfValueKind.Number || !bits.TryGetInteger(out long n) || (n != 1 && n != 2 && n != 4 && n != 8 && n != 16)))
            throw PdfContentState.Error(context, "Content.InlineBits", "Inline image precision must be 1, 2, 4, 8 or 16 bits.");
        if (mask && bits.Kind != PdfValueKind.Null && (bits.Kind != PdfValueKind.Number || !bits.TryGetInteger(out long m) || m != 1))
            throw PdfContentState.Error(context, "Content.InlineMask", "An image mask has one bit per sample.");
        if (mask && Entry(image, "ColorSpace").Kind != PdfValueKind.Null)
            throw PdfContentState.Error(context, "Content.InlineMask", "An image mask cannot declare a color space.");
    }

    private static bool Mask(PdfValue dictionary, PdfOperationContext context)
    {
        var value = Entry(dictionary, "ImageMask");
        if (value.Kind == PdfValueKind.Null)
            return false;
        if (value.Kind != PdfValueKind.Boolean)
            throw PdfContentState.Error(context, "Content.InlineMask", "ImageMask must be boolean.");
        return value.BooleanValue;
    }

    internal static long DataLength(PdfValue dictionary, PdfByteStore source, long start, PdfOperationContext context, Func<PdfValue, int>? colorComponents = null)
    {
        long width = Positive(dictionary, "Width", context), height = Positive(dictionary, "Height", context);
        var filter = Entry(dictionary, "Filter");
        if (filter.Kind == PdfValueKind.Array)
        {
            if (filter.Items.Count == 0)
                filter = PdfValue.Null;
            else
                filter = filter.Items[0];
        }

        if (filter.Kind == PdfValueKind.Null)
        {
            bool mask = Mask(dictionary, context);
            long bits = mask ? 1 : Positive(dictionary, "BitsPerComponent", context);
            int components = mask ? 1 : Components(Entry(dictionary, "ColorSpace"), context, colorComponents);
            long length;
            try
            {
                length = checked(((checked(width * components * bits) + 7) / 8) * height);
            }
            catch (OverflowException)
            {
                throw PdfContentState.Error(context, "Content.InlineGeometry", "Inline image dimensions overflow the supported byte extent.");
            }

            if (length > source.Length - start)
                throw PdfContentState.Error(context, "Content.InlineTruncated", "Raw inline image data is truncated.");
            return length;
        }

        if (filter.Kind != PdfValueKind.Name)
            throw PdfContentState.Error(context, "Content.InlineFilter", "Inline filters must be names.");
        using var input = source.Open(start, source.Length - start);
        string name = PdfStreamFilters.Canonical(filter.NameValue);
        var parameters = Entry(dictionary, "DecodeParms");
        if (parameters.Kind == PdfValueKind.Array)
            parameters = parameters.Items.Count > 0 ? parameters.Items[0] : PdfValue.Null;
        if (name == "FlateDecode")
            PdfInflate.Decode(input, context, false);
        else if (name == "LZWDecode")
        {
            int early = 1;
            if (parameters.Kind == PdfValueKind.Dictionary && parameters.Entries.TryGetValue("EarlyChange", out var e))
            {
                if (!e.TryGetInteger(out long n) || n < 0 || n > 1)
                    throw PdfContentState.Error(context, "Content.InlineFilter", "EarlyChange must be zero or one.");
                early = (int)n;
            }

            PdfLzw.Decode(input, early, context, false);
        }
        else if (name == "CCITTFaxDecode")
        {
            var canonical = PdfWriterCore.CopyDictionary(dictionary.Entries, context);
            canonical["Width"] = PdfValue.Integer(width);
            canonical["Height"] = PdfValue.Integer(height);
            PdfCcitt.Decode(input, parameters, PdfValue.OwnedDictionary(canonical), context, false);
        }
        else if (name == "DCTDecode")
            Jpeg(input, context);
        else if (name == "ASCIIHexDecode")
        {
            while (Read(input, context) != '>')
            {
            }
        }
        else if (name == "ASCII85Decode")
        {
            while (Read(input, context) != '~')
            {
            }

            if (Read(input, context) != '>')
                throw PdfContentState.Error(context, "Content.InlineFilter", "An ASCII85 terminator is malformed.");
        }
        else if (name == "RunLengthDecode")
        {
            while (true)
            {
                int n = Read(input, context);
                if (n == 128)
                    break;
                int bytes = n < 128 ? n + 1 : 1;
                for (int i = 0; i < bytes; i++)
                    Read(input, context);
            }
        }
        else
            throw PdfContentState.Error(context, "Content.InlineBoundaryUnsupported", "This inline filter has no supported deterministic byte boundary.");
        return input.Position;
    }

    private static int Components(PdfValue color, PdfOperationContext context, Func<PdfValue, int>? colorComponents)
    {
        if (color.Kind == PdfValueKind.Name)
        {
            string name = color.NameValue;
            if (name == "DeviceGray" || name == "G")
                return 1;
            if (name == "DeviceRGB" || name == "RGB")
                return 3;
            if (name == "DeviceCMYK" || name == "CMYK")
                return 4;
        }

        if (color.Kind == PdfValueKind.Array && color.Items.Count > 0 && color.Items[0].Kind == PdfValueKind.Name)
        {
            string name = color.Items[0].NameValue;
            if (name == "Indexed" || name == "I" || name == "Separation")
                return 1;
            if (name == "Lab" || name == "CalRGB")
                return 3;
            if (name == "CalGray")
                return 1;
            if (name == "DeviceN" && color.Items.Count >= 2 && color.Items[1].Kind == PdfValueKind.Array && color.Items[1].Items.Count > 0)
                return color.Items[1].Items.Count;
        }

        if (colorComponents != null) return colorComponents(color);
        throw PdfContentState.Error(context, "Content.InlineColorSpaceRequired", "Raw inline image boundaries require a direct color space with a known component count.");
    }

    private static int Read(Stream input, PdfOperationContext context)
    {
        context.Checkpoint();
        int value = input.ReadByte();
        if (value < 0)
            throw PdfContentState.Error(context, "Content.InlineTruncated", "Encoded inline image data is truncated.");
        return value;
    }

    private static void Jpeg(Stream input, PdfOperationContext context)
    {
        if (Read(input, context) != 255 || Read(input, context) != 216)
            throw PdfContentState.Error(context, "Content.InlineJpeg", "The inline JPEG has no SOI marker.");
        bool scan = false;
        while (true)
        {
            int value = Read(input, context);
            if (value != 255)
            {
                if (scan)
                    continue;
                throw PdfContentState.Error(context, "Content.InlineJpeg", "A JPEG marker prefix is missing.");
            }

            int marker;
            do
            {
                marker = Read(input, context);
            }
            while (marker == 255);
            if (scan && (marker == 0 || marker >= 208 && marker <= 215))
                continue;
            if (marker == 217)
                return;
            if (marker == 216 || marker == 0 || marker >= 208 && marker <= 215)
                throw PdfContentState.Error(context, "Content.InlineJpeg", "An unexpected standalone JPEG marker occurs.");
            if (marker == 1)
                continue;
            int length = Read(input, context) * 256 + Read(input, context);
            if (length < 2)
                throw PdfContentState.Error(context, "Content.InlineJpeg", "A JPEG marker length is invalid.");
            for (int i = 2; i < length; i++)
                Read(input, context);
            scan = marker == 218 || scan && marker == 220;
        }
    }

    internal static void Write(PdfContentOperation operation, PdfWriterCore.Sink sink, PdfOperationContext context)
    {
        sink.Text("BI\n");
        foreach (var pair in operation.Image!.Entries)
        {
            context.Checkpoint();
            sink.Value(PdfValue.Name(pair.Key), false);
            sink.Text(" ");
            sink.Value(pair.Value, false);
            sink.Text("\n");
        }

        sink.Text("ID\n");
        using (var input = operation.OpenInlineImageData())
            sink.Copy(input);
        sink.Text("\nEI\n");
    }
}
