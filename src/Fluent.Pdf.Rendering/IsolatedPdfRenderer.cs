using System.IO;
using Fluent.Pdf.Workers;

namespace Fluent.Pdf.Rendering;
/// <summary>An offline candidate PDFium adapter. Each request uses a fresh hash-pinned worker; this source profile is not engine qualification.</summary>
public sealed class IsolatedPdfRenderer : IPdfPageRenderer
{
    /// <summary>Gets the original bridge ABI.</summary>
    public const string BridgeAbi = "fluent-pdf-render-1";
    /// <summary>Gets the required candidate engine build.</summary>
    public const string BridgeVersion = "153.0.7999.0";
    private readonly NativeWorkerOptions _worker;
    /// <summary>Captures a complete offline deployment. No binaries, fonts or models are downloaded by the adapter.</summary>
    public IsolatedPdfRenderer(NativeWorkerOptions worker)
    {
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        if (worker.Deployment.Abi != BridgeAbi || worker.Deployment.EngineVersion != BridgeVersion)
            throw new ArgumentException("The exact rendering bridge ABI and candidate engine version are required.", nameof(worker));
        PdfRenderAssets.Validate(worker);
    }

    /// <summary>Gets declared source capability, independent of production qualification.</summary>
    public PdfProviderDescriptor Descriptor { get; } = new("isolated.rendering", BridgeVersion, PdfCapability.RenderPages);

    /// <summary>Validates the supported profile and paints exact straight-alpha BGRA samples through isolated native execution.</summary>
    public void Render(PdfRasterRequest request, Stream bgraOutput, PdfOperationContext context)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (bgraOutput == null)
            throw new ArgumentNullException(nameof(bgraOutput));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
        var page = request.Page;
        PdfRenderProfile.Validate(page, request.Options, context);
        long inputLength = 64L + page.Document.Source.Length, outputLength = request.ByteLength + 32;
        if (inputLength > _worker.MaximumInputBytes || inputLength > int.MaxValue)
            throw PdfWriterCore.Error(context, "Render.InputLimit", "The source PDF exceeds the configured worker input envelope.");
        if (outputLength > _worker.MaximumOutputBytes)
            throw PdfWriterCore.Error(context, "Render.OutputLimit", "The raster exceeds the configured worker response envelope.");
        context.Consume(PdfResourceKind.ExpandedBytes, inputLength);
        var input = new byte[(int)inputLength];
        input[0] = 70;
        input[1] = 80;
        input[2] = 82;
        input[3] = 49;
        Put(input, 4, (uint)page.Document.Source.Length);
        Put(input, 8, (uint)request.PageIndex);
        Put(input, 12, (uint)request.FullWidth);
        Put(input, 16, (uint)request.FullHeight);
        Put(input, 20, (uint)request.X);
        Put(input, 24, (uint)request.Y);
        Put(input, 28, (uint)request.Width);
        Put(input, 32, (uint)request.Height);
        Put(input, 36, (request.Options.Annotations ? 1u : 0) | (request.Options.Color == PdfRasterColor.Gray ? 8u : 0) | (request.Options.Print ? 2048u : 0));
        uint background = request.Options.BackgroundRgb.HasValue ? request.Options.BackgroundRgb.Value | 0xff000000 : 0;
        Put(input, 40, background);
        byte[] reply;
        try
        {
            using var original = page.Document.OpenOriginalStream();
            using var lease = context.BorrowExpandedInput(original);
            int offset = 64;
            while (offset < input.Length)
            {
                context.Checkpoint();
                int read = lease.Read(input, offset, Math.Min(65536, input.Length - offset));
                if (read == 0)
                    throw PdfWriterCore.Error(context, "Render.InputLength", "The prepared PDF stream ended unexpectedly.");
                offset += read;
            }

            reply = new NativeWorkerHost().Execute(_worker, input, context, (int)outputLength);
        }
        finally
        {
            Array.Clear(input, 0, input.Length);
        }

        try
        {
            if (reply.Length < 32 || reply[0] != 70 || reply[1] != 80 || reply[2] != 82 || reply[3] != 79)
                throw Error("Render.NativeResponse", "The renderer returned an invalid response envelope.");
            int status = Get(reply, 4);
            if (status != 0)
                throw Error(status == 3 ? "Render.NativeLimit" : status == 4 ? "Render.NativeContent" : "Render.NativeRejected", "The isolated engine rejected the request, PDF content or resource extent.");
            if (reply.Length != outputLength || Get(reply, 8) != request.Width || Get(reply, 12) != request.Height || Get(reply, 16) != request.Stride || Get(reply, 20) != 4 || Get(reply, 24) != 0 || Get(reply, 28) != 0)
                throw Error("Render.NativeResponse", "The renderer returned incompatible raster geometry or format.");
            for (int at = 32; at < reply.Length; at += 4)
            {
                if ((at & 65535) == 32)
                    context.Checkpoint();
                if (reply[at + 3] == 0)
                    reply[at] = reply[at + 1] = reply[at + 2] = 0;
                if (request.Options.BackgroundRgb.HasValue && reply[at + 3] != 255)
                    throw Error("Render.NativeAlpha", "An opaque rendering returned a nonopaque sample.");
            }

            bgraOutput.Write(reply, 32, reply.Length - 32);
            context.Checkpoint();
        }
        finally
        {
            Array.Clear(reply, 0, reply.Length);
        }

        PdfException Error(string code, string message) => PdfWriterCore.Error(context, code, message);
    }

    private static int Get(byte[] bytes, int at) => unchecked((int)((uint)bytes[at] | (uint)bytes[at + 1] << 8 | (uint)bytes[at + 2] << 16 | (uint)bytes[at + 3] << 24));
    private static void Put(byte[] bytes, int at, uint value)
    {
        for (int i = 0; i < 4; i++)
            bytes[at + i] = (byte)(value >> (i * 8));
    }
}
