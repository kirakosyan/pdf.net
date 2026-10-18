using System.IO;
using Fluent.Pdf.Workers;

namespace Fluent.Pdf.Codecs;
/// <summary>Offline DCT, JPX and JBIG2 codecs executed in a fresh pinned native worker.</summary>
public sealed class IsolatedImageCodecProvider : IPdfImageCodecProvider
{
    /// <summary>The exact original codec bridge ABI required by this broker.</summary>
    public const string BridgeAbi = "fluent-pdf-codecs-1";
    /// <summary>The pinned bridge implementation version.</summary>
    public const string BridgeVersion = "1.0.0";
    private readonly NativeWorkerOptions _worker;
    /// <summary>Captures a caller-supplied, hash-pinned offline bridge deployment with thread support explicitly enabled.</summary>
    public IsolatedImageCodecProvider(NativeWorkerOptions worker)
    {
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        if (worker.Deployment.Abi != BridgeAbi || worker.Deployment.EngineVersion != BridgeVersion || !worker.AllowInProcessThreads)
            throw new ArgumentException("The pinned codec ABI/version and isolated in-process thread option are required.", nameof(worker));
    }

    /// <summary>Gets the immutable codec capability declaration; production qualification is tracked separately.</summary>
    public PdfProviderDescriptor Descriptor { get; } = new("isolated.image-codecs", BridgeVersion, PdfCapability.ImageCodecs);

    /// <summary>Reports the three codec algorithms, including lossless JBIG2 encoding using generic MMR regions.</summary>
    public bool Supports(PdfImageCodec codec, bool encoding) => Enum.IsDefined(typeof(PdfImageCodec), codec);
    /// <summary>Inspects original bounded headers without invoking the pixel decoder.</summary>
    public PdfImageInfo Inspect(PdfImageCodec codec, Stream encodedInput, PdfValue imageDictionary, PdfOperationContext context)
    {
        try
        {
            return CodecHeaders.Inspect(codec, encodedInput, context);
        }
        catch (NotSupportedException)
        {
            throw Error(context, "Codec.UnsupportedProfile", "The image profile is outside this codec provider's supported sample representation.");
        }
        catch (InvalidDataException)
        {
            throw Error(context, "Codec.Malformed", "The encoded image header is malformed or truncated.");
        }
        catch (ArgumentOutOfRangeException)
        {
            throw Error(context, "Codec.Malformed", "The encoded image geometry is outside range.");
        }
    }

    /// <summary>Decodes through a process boundary and accepts only a complete, geometry-matched result.</summary>
    public void Decode(PdfImageCodec codec, PdfImageInfo info, PdfValue imageDictionary, PdfValue decodeParameters, Stream encodedInput, Stream sampleOutput, PdfOperationContext context) => Execute(false, codec, info, decodeParameters, encodedInput, sampleOutput, context);
    /// <summary>Encodes through a process boundary. DCT quality is explicit; JPX and JBIG2 encoding is lossless.</summary>
    public void Encode(PdfImageCodec codec, PdfImageInfo info, PdfValue parameters, Stream sampleInput, Stream encodedOutput, PdfOperationContext context) => Execute(true, codec, info, parameters, sampleInput, encodedOutput, context);
    private void Execute(bool encode, PdfImageCodec codec, PdfImageInfo info, PdfValue parameters, Stream input, Stream output, PdfOperationContext context)
    {
        context.Checkpoint();
        if (!encode && info.ByteLength > _worker.MaximumOutputBytes - 48L)
            throw Error(context, "Native.OutputLimit", "Decoded image samples exceed the configured worker response envelope.");
        int quality = PdfStreamFilters.Parameter(parameters, "Quality", 90, context), transform = PdfStreamFilters.Parameter(parameters, "ColorTransform", -1, context);
        if (quality < 1 || quality > 100 || transform < -1 || transform > 1)
            throw Error(context, "Codec.Parameters", "An image codec parameter is outside its supported range.");
        if (parameters.Kind == PdfValueKind.Dictionary)
            foreach (var key in parameters.Entries.Keys)
                if (!(key == "Quality" && encode && codec == PdfImageCodec.Dct) && !(key == "ColorTransform" && codec == PdfImageCodec.Dct) && !(key == "JBIG2Globals" && !encode && codec == PdfImageCodec.Jbig2))
                    throw Error(context, "Codec.Parameters", "An image codec parameter is unsupported for this algorithm or direction.");
        if (encode && codec == PdfImageCodec.Dct)
            context.Report(new PdfDiagnostic("Codec.LossyEncoding", "The explicitly requested DCT encoder quantizes samples using the selected quality.", severity: PdfDiagnosticSeverity.Information));
        byte[] globals = Array.Empty<byte>();
        if (parameters.Kind == PdfValueKind.Dictionary && parameters.Entries.TryGetValue("JBIG2Globals", out var global) && global.Kind != PdfValueKind.Null)
        {
            if (codec != PdfImageCodec.Jbig2 || global.Kind != PdfValueKind.Stream)
                throw Error(context, "Codec.Parameters", "JBIG2 globals must resolve to an encoded stream.");
            using var decoded = PdfFilters.Decode(global, context);
            globals = Read(decoded, context, _worker.MaximumInputBytes);
        }

        byte[] data = Read(input, context, _worker.MaximumInputBytes - 64 - globals.Length);
        long requestLength = 64L + data.Length + globals.Length;
        if (requestLength > _worker.MaximumInputBytes)
            throw Error(context, "Native.InputLimit", "The codec request exceeds its configured envelope.");
        context.Consume(PdfResourceKind.ExpandedBytes, requestLength);
        // Reserve the complete isolated allocation envelope before any third-party code runs.
        // This intentionally requires an operation budget that can cover the configured process.
        context.Consume(PdfResourceKind.ExpandedBytes, _worker.MaximumMemoryBytes);
        using var objects = context.ReserveRemainingObjects();
        var request = new byte[(int)requestLength];
        request[0] = 70;
        request[1] = 80;
        request[2] = 67;
        request[3] = 49;
        request[4] = encode ? (byte)1 : (byte)0;
        request[5] = (byte)codec;
        request[6] = (byte)quality;
        request[7] = (byte)(transform + 1);
        Put(request, 8, info.Width);
        Put(request, 12, info.Height);
        Put(request, 16, info.Components);
        Put(request, 20, info.BitsPerComponent);
        Put(request, 24, data.Length);
        Put(request, 28, globals.Length);
        Put64(request, 32, _worker.MaximumMemoryBytes);
        Put64(request, 40, objects.Amount);
        Put64(request, 48, (long)info.Width * info.Height);
        Put(request, 56, (int)Math.Min(int.MaxValue, context.Policy.Limits.MaximumElapsedTime.TotalMilliseconds));
        Array.Copy(data, 0, request, 64, data.Length);
        Array.Copy(globals, 0, request, 64 + data.Length, globals.Length);
        byte[] reply;
        try
        {
            reply = new NativeWorkerHost().Execute(_worker, request, context);
        }
        finally
        {
            Array.Clear(request, 0, request.Length);
            Array.Clear(data, 0, data.Length);
            Array.Clear(globals, 0, globals.Length);
        }

        try
        {
            if (reply.Length < 48 || reply[0] != 70 || reply[1] != 80 || reply[2] != 67 || reply[3] != 79 || Get(reply, 28) != 0)
                throw Error(context, "Codec.Protocol", "The isolated codec returned an invalid reply.");
            int status = Get(reply, 4);
            if (status != 0)
                throw Error(context, status == 2 ? "Codec.UnsupportedProfile" : status == 3 ? "Codec.ResourceLimit" : "Codec.Malformed", "The isolated codec rejected the requested image.");
            if (Get(reply, 8) != info.Width || Get(reply, 12) != info.Height || Get(reply, 16) != info.Components || Get(reply, 20) != info.BitsPerComponent || Get(reply, 24) != reply.Length - 48 || (!encode && reply.Length - 48 != info.ByteLength) || Get64(reply, 32) < 0 || Get64(reply, 32) > _worker.MaximumMemoryBytes)
                throw Error(context, "Codec.Protocol", "The isolated codec returned inconsistent image geometry, budget or length.");
            objects.Complete(Get64(reply, 40));
            output.Write(reply, 48, reply.Length - 48);
            context.Checkpoint();
        }
        finally
        {
            Array.Clear(reply, 0, reply.Length);
        }
    }

    private static byte[] Read(Stream input, PdfOperationContext context, int maximum)
    {
        if (maximum < 0)
            throw Error(context, "Native.InputLimit", "No request capacity remains for encoded image bytes.");
        var builder = new PdfByteBuilder(context);
        context.Consume(PdfResourceKind.ExpandedBytes, 4096);
        var buffer = new byte[4096];
        int n;
        while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (n > maximum - builder.Length)
                throw Error(context, "Native.InputLimit", "The image exceeds its configured request envelope.");
            for (int i = 0; i < n; i++)
                builder.Add(buffer[i]);
        }

        var bytes = builder.Build();
        if (bytes.Length > int.MaxValue)
            throw Error(context, "Native.InputLimit", "The image exceeds the bridge's integer range.");
        context.Consume(PdfResourceKind.ExpandedBytes, bytes.Length);
        var result = new byte[(int)bytes.Length];
        using var stream = bytes.Open(0, bytes.Length);
        int position = 0;
        while ((n = stream.Read(result, position, result.Length - position)) > 0)
            position += n;
        return result;
    }

    private static PdfException Error(PdfOperationContext context, string code, string message) => PdfStreamFilters.Error(context, code, message);
    private static void Put(byte[] bytes, int offset, int value)
    {
        for (int i = 0; i < 4; i++)
            bytes[offset + i] = (byte)((uint)value >> (i * 8));
    }

    private static void Put64(byte[] bytes, int offset, long value)
    {
        for (int i = 0; i < 8; i++)
            bytes[offset + i] = (byte)((ulong)value >> (i * 8));
    }

    private static int Get(byte[] bytes, int offset) => (int)((uint)bytes[offset] | ((uint)bytes[offset + 1] << 8) | ((uint)bytes[offset + 2] << 16) | ((uint)bytes[offset + 3] << 24));
    private static long Get64(byte[] bytes, int offset)
    {
        ulong value = 0;
        for (int i = 0; i < 8; i++)
            value |= (ulong)bytes[offset + i] << (8 * i);
        return (long)value;
    }
}
