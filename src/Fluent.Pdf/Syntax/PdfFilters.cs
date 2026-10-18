using System.Collections.ObjectModel;
using System.IO;

namespace Fluent.Pdf;
/// <summary>An explicit PDF decoding filter and immutable direct decode parameters.</summary>
public sealed class PdfFilter
{
    /// <summary>Creates a named filter; unknown names remain explicit and fail when requested without a provider.</summary>
    public PdfFilter(string name, PdfValue? parameters = null)
    {
        Guard.Text(name, nameof(name), 128);
        PdfValue.Name(name);
        if (parameters is not null && parameters.Kind != PdfValueKind.Null && parameters.Kind != PdfValueKind.Dictionary)
            throw new ArgumentException("Decode parameters must be a dictionary or PDF null.", nameof(parameters));
        Name = name;
        Parameters = parameters ?? PdfValue.Null;
    }

    /// <summary>Gets the exact decoding filter name.</summary>
    public string Name { get; }
    /// <summary>Gets immutable direct parameters or PDF null.</summary>
    public PdfValue Parameters { get; }
}

/// <summary>Bounded original managed filter chains. Image codecs are supplied through optional providers.</summary>
public static class PdfFilters
{
    /// <summary>Reads direct Filter/DecodeParms declarations in decoding order; no content is decoded.</summary>
    public static IReadOnlyList<PdfFilter> Inspect(PdfValue dictionary)
    {
        if (dictionary is null)
            throw new ArgumentNullException(nameof(dictionary));
        if (dictionary.Kind != PdfValueKind.Dictionary && dictionary.Kind != PdfValueKind.Stream)
            throw new ArgumentException("A dictionary or stream is required.", nameof(dictionary));
        if (!dictionary.Entries.TryGetValue("Filter", out var declaration) || declaration.Kind == PdfValueKind.Null)
            return new ReadOnlyCollection<PdfFilter>(Array.Empty<PdfFilter>());
        var filters = declaration.Kind == PdfValueKind.Array ? declaration.Items : new[]
        {
            declaration
        };
        IReadOnlyList<PdfValue>? parameters = null;
        if (dictionary.Entries.TryGetValue("DecodeParms", out var p) && p.Kind != PdfValueKind.Null)
        {
            if (declaration.Kind == PdfValueKind.Array)
            {
                if (p.Kind != PdfValueKind.Array || p.Items.Count != filters.Count)
                    throw new ArgumentException("Filter and DecodeParms array lengths disagree.", nameof(dictionary));
                parameters = p.Items;
            }
            else
                parameters = new[]
                {
                    p
                };
        }

        var result = new PdfFilter[filters.Count];
        for (int i = 0; i < result.Length; i++)
        {
            if (filters[i].Kind != PdfValueKind.Name)
                throw new ArgumentException("Filter declarations must be names.", nameof(dictionary));
            result[i] = new PdfFilter(filters[i].NameValue, parameters?[i]);
        }

        return new ReadOnlyCollection<PdfFilter>(result);
    }

    /// <summary>Eagerly decodes encoded stream bytes with the mandatory operation context, returning an owned read-only view.</summary>
    public static Stream Decode(PdfValue encodedStream, PdfOperationContext context,PdfProviderRegistry? providers=null)
    {
        if (encodedStream is null)
            throw new ArgumentNullException(nameof(encodedStream));
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        if (encodedStream.Kind != PdfValueKind.Stream)
            throw new ArgumentException("An encoded PDF stream is required.", nameof(encodedStream));
        return PdfStreamFilters.OpenBootstrap(encodedStream, context,providers);
    }

    /// <summary>Encodes borrowed input by applying the declared decoding chain in reverse. The returned bytes require no further work.</summary>
    public static Stream Encode(Stream decodedInput, IReadOnlyList<PdfFilter> decodingFilters, PdfOperationContext context,PdfImageInfo? imageInfo=null,PdfProviderRegistry? providers=null)
    {
        if (decodedInput is null)
            throw new ArgumentNullException(nameof(decodedInput));
        if (!decodedInput.CanRead)
            throw new ArgumentException("A readable stream is required.", nameof(decodedInput));
        if (decodingFilters is null)
            throw new ArgumentNullException(nameof(decodingFilters));
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, decodingFilters.Count);
        var filters = decodingFilters.ToArray();
        if (filters.Any(x => x is null))
            throw new ArgumentException("A filter cannot be CLR null.", nameof(decodingFilters));
        for(int i=0;i<filters.Length;i++)
        {
            string name=PdfStreamFilters.Canonical(filters[i].Name);
            if(name=="CCITTFaxDecode")
            {
                if(i!=filters.Length-1)throw PdfStreamFilters.Error(context,"Filter.Chain","A fax image codec must be the innermost decoding filter.");
                PdfCcitt.PreflightEncoding(filters[i].Parameters,context);
            }
            else if(PdfImageCodecPipeline.TryKind(name,out var codec))
            {
                if(i!=filters.Length-1)throw PdfStreamFilters.Error(context,"Filter.Chain","An image codec must be the innermost decoding filter.");
                if(imageInfo is null)throw new ArgumentNullException(nameof(imageInfo));
                PdfImageCodecPipeline.Required(providers,codec,true,context);context.ConsumePixels(imageInfo.Width,imageInfo.Height);
            }
        }
        using var input = new BorrowedStream(decodedInput, true, context);
        var bytes = PdfByteStore.Read(input, context);
        try
        {
            for (int i = filters.Length - 1; i >= 0; i--)
            {
                context.Checkpoint();
                var filter = filters[i];
                string name = PdfStreamFilters.Canonical(filter.Name);
                if (name == "FlateDecode" || name == "LZWDecode")
                    bytes = PdfStreamFilters.EncodePrediction(bytes, filter.Parameters, context);
                using var source = bytes.Open(0, bytes.Length);
                if(PdfImageCodecPipeline.TryKind(name,out var imageCodec))
                {bytes=PdfImageCodecPipeline.Encode(imageCodec,imageInfo!,filter.Parameters,source,providers,context);continue;}
                bytes = name == "FlateDecode" ? PdfFilterEncoders.Flate(source, context) : name == "LZWDecode" ? PdfLzw.Encode(source, PdfStreamFilters.Parameter(filter.Parameters, "EarlyChange", 1, context), context) : name == "ASCIIHexDecode" ? PdfFilterEncoders.Hex(source, context) : name == "ASCII85Decode" ? PdfFilterEncoders.Ascii85(source, context) : name == "RunLengthDecode" ? PdfFilterEncoders.RunLength(bytes, context) : name == "CCITTFaxDecode" ? PdfCcitt.Encode(bytes, filter.Parameters, context, true) : throw PdfStreamFilters.Error(context, "Filter.Unsupported", "The requested encoding filter has no configured implementation.");
            }

            return bytes.Open(0, bytes.Length);
        }
        catch (OverflowException)
        {
            throw PdfStreamFilters.Error(context, "Filter.Parameters", "A filter parameter or count overflowed its supported range.");
        }
        catch (InvalidDataException)
        {
            throw PdfStreamFilters.Error(context, "Filter.Parameters", "An integer filter parameter is malformed or outside range.");
        }
    }
}
