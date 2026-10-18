using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.IO;
using Fluent.Pdf;
using Fluent.Pdf.Codecs;

internal static unsafe class Exports
{
    private static readonly nint Abi = Marshal.StringToCoTaskMemUTF8("fluent-pdf-codecs-1");
    private static readonly nint Version = Marshal.StringToCoTaskMemUTF8("1.0.0");
    [UnmanagedCallersOnly(EntryPoint = "fluent_pdf_bridge_abi_v1", CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static nint GetAbi() => Abi;
    [UnmanagedCallersOnly(EntryPoint = "fluent_pdf_bridge_version_v1", CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static nint GetVersion() => Version;
    [UnmanagedCallersOnly(EntryPoint = "fluent_pdf_bridge_execute_v1", CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static int Execute(byte* input, nuint length, byte* output, nuint capacity, nuint* written)
    {
        if (written == null || length < 64 || length > int.MaxValue || capacity < 48 || capacity > int.MaxValue)
            return 99;
        *written = 48;
        new Span<byte>(output, 48).Clear();
        output[0] = 70;
        output[1] = 80;
        output[2] = 67;
        output[3] = 79;
        PdfOperationContext? context = null;
        try
        {
            var request = new ReadOnlySpan<byte>(input, (int)length);
            if (request[0] != 70 || request[1] != 80 || request[2] != 67 || request[3] != 49 || request[4] > 1 || request[5] > 2 || request[6] < 1 || request[6] > 100 || request[7] > 2 || I32(request, 60) != 0)
                throw new InvalidDataException();
            bool encode = request[4] == 1;
            var codec = (PdfImageCodec)request[5];
            var info = new PdfImageInfo(I32(request, 8), I32(request, 12), I32(request, 16), I32(request, 20));
            int dataLength = I32(request, 24), globalsLength = I32(request, 28);
            if (dataLength < 0 || globalsLength < 0 || 64L + dataLength + globalsLength != (long)length)
                throw new InvalidDataException();
            var limits = new PdfResourceLimits(maximumInputBytes: (long)length, maximumExpandedBytes: I64(request, 32), maximumOutputBytes: (long)capacity - 48, maximumObjects: I64(request, 40), maximumPixels: I64(request, 48), maximumElapsedTime: TimeSpan.FromMilliseconds(I32(request, 56)));
            context = new PdfOperationContext("ImageCodec", "isolated.image-codecs", PdfCapability.ImageCodecs, new PdfOperationPolicy(limits: limits), default);
            if (!encode || codec != PdfImageCodec.Jbig2)
                context.ConsumePixels(info.Width, info.Height);
            // Charge all image planes and codec workspace before entering third-party code.
            long workspace = checked(info.ByteLength * 32 + dataLength * 4L + globalsLength * 4L + 1048576);
            context.Consume(PdfResourceKind.ExpandedBytes, workspace);
            using var source = new UnmanagedMemoryStream(input + 64, dataLength);
            if (!encode)
            {
                var actual = CodecHeaders.Inspect(codec, source, context);
                if (actual.Width != info.Width || actual.Height != info.Height || actual.Components != info.Components || actual.BitsPerComponent != info.BitsPerComponent)
                    throw new InvalidDataException();
                source.Position = 0;
            }
            else if (dataLength != info.ByteLength)
                throw new InvalidDataException();
            using var destination = new UnmanagedMemoryStream(output + 48, 0, (long)capacity - 48, FileAccess.Write);
            using var sink = new BorrowedStream(destination, false, context);
            byte[] globals = request.Slice(64 + dataLength, globalsLength).ToArray();
            try
            {
                CodecAlgorithms.Run(encode, codec, info, request[6], request[7] - 1, source, globals, sink, context);
            }
            finally
            {
                Array.Clear(globals);
            }

            if (!encode && destination.Length != info.ByteLength)
                throw new InvalidDataException();
            context.EnsureComplete();
            var report = context.Finish(true);
            Put64(output, 32, report.Resources.Single(x => x.Resource == PdfResourceKind.ExpandedBytes).Consumed);
            Put64(output, 40, report.Resources.Single(x => x.Resource == PdfResourceKind.Objects).Consumed);
            Put(output, 8, info.Width);
            Put(output, 12, info.Height);
            Put(output, 16, info.Components);
            Put(output, 20, info.BitsPerComponent);
            Put(output, 24, (int)destination.Length);
            *written = (nuint)(48 + destination.Length);
            return 0;
        }
        catch (NotSupportedException)
        {
            Put(output, 4, 2);
            return 0;
        }
        catch (PdfException)
        {
            Put(output, 4, 3);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Put(output, 4, 3);
            return 0;
        }
        catch (OutOfMemoryException)
        {
            Put(output, 4, 3);
            return 0;
        }
        catch
        {
            Put(output, 4, 1);
            return 0;
        }
        finally
        {
            if (context != null)
            {
                context.Finish(false);
                context.DisposeSources();
            }
        }
    }

    private static int I32(ReadOnlySpan<byte> data, int at) => System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(data.Slice(at, 4));
    private static long I64(ReadOnlySpan<byte> data, int at) => System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(data.Slice(at, 8));
    private static void Put(byte* data, int at, int value)
    {
        for (int i = 0; i < 4; i++)
            data[at + i] = (byte)((uint)value >> (i * 8));
    }

    private static void Put64(byte* data, int at, long value)
    {
        for (int i = 0; i < 8; i++)
            data[at + i] = (byte)((ulong)value >> (i * 8));
    }
}
