using System.IO;

namespace Fluent.Pdf;
internal static class PdfImageCodecPipeline
{
    internal static bool TryKind(string name, out PdfImageCodec codec)
    {
        codec = name == "DCTDecode" ? PdfImageCodec.Dct : name == "JPXDecode" ? PdfImageCodec.Jpx : PdfImageCodec.Jbig2;
        return name == "DCTDecode" || name == "JPXDecode" || name == "JBIG2Decode";
    }

    internal static IPdfImageCodecProvider Required(PdfProviderRegistry? registry, PdfImageCodec codec, bool encoding, PdfOperationContext context)
    {
        IPdfImageCodecProvider provider;
        try
        {
            provider = (registry ?? PdfProviderRegistry.Empty).GetRequired<IPdfImageCodecProvider>(PdfCapability.ImageCodecs);
        }
        catch (PdfException failure)
        {
            context.Report(failure.Diagnostic);
            throw;
        }

        if (!provider.Supports(codec, encoding))
            throw PdfStreamFilters.Error(context, "Codec.Unsupported", "The selected image codec provider does not support this algorithm or direction.");
        return provider;
    }

    internal static PdfByteStore Decode(PdfImageCodec codec, Stream input, PdfValue dictionary, PdfValue parameters, PdfProviderRegistry? registry, PdfOperationContext context)
    {
        using var scope = context.EnterScope(PdfResourceKind.Depth);
        var provider = Required(registry, codec, false, context);
        PdfImageInfo info;
        using (var lease = context.BorrowExpandedInput(input))
            info = provider.Inspect(codec, lease, dictionary, context) ?? throw PdfStreamFilters.Error(context, "Codec.Metadata", "An image codec returned no geometry.");
        context.ConsumePixels(info.Width, info.Height);
        context.Consume(PdfResourceKind.Objects, 1);
        ValidateMetadata(info, dictionary, context);
        input.Position = 0;
        using var source = context.BorrowExpandedInput(input);
        using var output = new PdfCodecBufferStream(context, info.ByteLength);
        provider.Decode(codec, info, dictionary, parameters, source, output, context);
        context.Checkpoint();
        if (output.BufferedLength != info.ByteLength)
            throw PdfStreamFilters.Error(context, "Codec.OutputLength", "Decoded samples disagree with the inspected image geometry.");
        return output.Finish();
    }

    internal static PdfByteStore Encode(PdfImageCodec codec, PdfImageInfo info, PdfValue parameters, Stream input, PdfProviderRegistry? registry, PdfOperationContext context)
    {
        using var scope = context.EnterScope(PdfResourceKind.Depth);
        var provider = Required(registry, codec, true, context);
        if (input.Length != info.ByteLength)
            throw PdfStreamFilters.Error(context, "Codec.InputLength", "Encoding input disagrees with its image geometry.");
        using var source = context.BorrowExpandedInput(input);
        using var output = new PdfCodecBufferStream(context, context.Policy.Limits.MaximumExpandedBytes);
        provider.Encode(codec, info, parameters, source, output, context);
        context.Checkpoint();
        return output.Finish();
    }

    private static void ValidateMetadata(PdfImageInfo info, PdfValue dictionary, PdfOperationContext context)
    {
        foreach (var item in new[]
        {
            new KeyValuePair<string, int>("Width", info.Width),
            new("Height", info.Height),
            new("BitsPerComponent", info.BitsPerComponent)
        }

        )
            if (PdfStreamFilters.Parameter(dictionary, item.Key, item.Value, context) != item.Value)
                throw PdfStreamFilters.Error(context, "Codec.MetadataMismatch", "Encoded image geometry disagrees with the PDF image dictionary.");
        if (dictionary.Entries.TryGetValue("ColorSpace", out var space) && space.Kind == PdfValueKind.Name)
        {
            int count = space.NameValue == "DeviceGray" ? 1 : space.NameValue == "DeviceRGB" ? 3 : space.NameValue == "DeviceCMYK" ? 4 : info.Components;
            if (count != info.Components)
                throw PdfStreamFilters.Error(context, "Codec.MetadataMismatch", "Encoded image components disagree with the PDF color space.");
        }
    }
}
