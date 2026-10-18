using System.IO;
using Fluent.Pdf.Workers;

namespace Fluent.Pdf.Text;
/// <summary>Explicit offline OpenType shaping through a fresh, hash-pinned isolated HarfBuzz process.</summary>
public sealed class IsolatedTextShaper : IPdfTextShaper
{
    /// <summary>Gets the original bridge ABI.</summary>
    public const string BridgeAbi = "fluent-pdf-text-1";
    /// <summary>Gets the exact required native HarfBuzz version.</summary>
    public const string BridgeVersion = "14.2.1";
    private readonly NativeWorkerOptions _worker;
    /// <summary>Captures a complete caller-provided deployment. No binaries or fonts are downloaded at runtime.</summary>
    public IsolatedTextShaper(NativeWorkerOptions worker)
    {
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        if (worker.Deployment.Abi != BridgeAbi || worker.Deployment.EngineVersion != BridgeVersion)
            throw new ArgumentException("The exact text bridge ABI and native engine version are required.", nameof(worker));
    }

    /// <summary>Gets the capability declaration, independent of production qualification.</summary>
    public PdfProviderDescriptor Descriptor { get; } = new("isolated.text", BridgeVersion, PdfCapability.ShapeText);

    /// <summary>Shapes with original font bytes and full logical context, accepting only a complete version/geometry-matched reply.</summary>
    public IReadOnlyList<PdfShapedGlyph> Shape(PdfShapingRequest request, PdfOperationContext context)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
        using var font = new PdfCodecBufferStream(context, _worker.MaximumInputBytes);
        int face = request.Font.CopyShapingSource(font, context);
        var source = font.Finish();
        long length = 48L + source.Length + request.Text.Length * 2L + request.Language.Length + request.Features.Count * 16L;
        if (length > _worker.MaximumInputBytes || length > int.MaxValue)
            throw PdfWriterCore.Error(context, "Native.InputLimit", "The shaping request exceeds its configured native envelope.");
        int maximum = (int)Math.Min((_worker.MaximumOutputBytes - 16L) / 24, Math.Min(int.MaxValue, context.Available(PdfResourceKind.Objects)));
        if (maximum < 1)
            throw PdfWriterCore.Error(context, "Native.OutputLimit", "The shaping response envelope cannot hold a glyph.");
        context.Consume(PdfResourceKind.ExpandedBytes, length);
        context.Consume(PdfResourceKind.ExpandedBytes, _worker.MaximumMemoryBytes);
        var bytes = new byte[(int)length];
        bytes[0] = 70;
        bytes[1] = 80;
        bytes[2] = 84;
        bytes[3] = 49;
        Put(bytes, 4, (uint)source.Length);
        Put(bytes, 8, (uint)request.Text.Length);
        Put(bytes, 12, (uint)face);
        Put(bytes, 16, (uint)request.Start);
        Put(bytes, 20, (uint)request.Length);
        Put(bytes, 24, request.Direction == PdfTextDirection.LeftToRight ? 4u : request.Direction == PdfTextDirection.RightToLeft ? 5u : 6u);
        Put(bytes, 28, request.ScriptTag);
        Put(bytes, 32, (uint)request.Language.Length);
        Put(bytes, 36, (uint)request.Features.Count);
        Put(bytes, 40, (uint)maximum);
        for (int i = 0; i < source.Length; i++)
            bytes[48 + i] = (byte)source.At(i);
        int offset = 48 + (int)source.Length;
        foreach (char c in request.Text)
        {
            bytes[offset++] = (byte)c;
            bytes[offset++] = (byte)(c >> 8);
        }

        foreach (char c in request.Language)
            bytes[offset++] = (byte)c;
        foreach (var feature in request.Features)
        {
            uint tag = 0;
            foreach (char c in feature.Tag)
                tag = tag << 8 | c;
            Put(bytes, offset, tag);
            Put(bytes, offset + 4, feature.Value);
            Put(bytes, offset + 8, (uint)feature.Start);
            Put(bytes, offset + 12, (uint)feature.End);
            offset += 16;
        }

        byte[] reply;
        try
        {
            reply = new NativeWorkerHost().Execute(_worker, bytes, context);
        }
        finally
        {
            Array.Clear(bytes, 0, bytes.Length);
        }

        try
        {
            if (reply.Length < 16 || reply[0] != 70 || reply[1] != 80 || reply[2] != 84 || reply[3] != 79)
                throw PdfWriterCore.Error(context, "Text.NativeResponse", "The isolated shaper returned an invalid envelope.");
            int status = Get(reply, 4), count = Get(reply, 8);
            if (status != 0)
                throw PdfWriterCore.Error(context, status == 3 ? "Text.NativeLimit" : "Text.MalformedFont", "The isolated shaper rejected the request or exceeded its bounded response capacity.");
            if (count < 0 || count > maximum || 16L + count * 24L != reply.Length || Get(reply, 12) != request.Font.Info.Metrics.UnitsPerEm)
                throw PdfWriterCore.Error(context, "Text.NativeResponse", "The isolated shaper returned incompatible glyph geometry.");
            context.Consume(PdfResourceKind.ExpandedBytes, count * 48L + 32);
            var glyphs = new PdfShapedGlyph[count];
            for (int i = 0; i < count; i++)
            {
                context.Checkpoint();
                int at = 16 + i * 24;
                int glyph = Get(reply, at), cluster = Get(reply, at + 4);
                if (glyph < 0 || glyph >= request.Font.Info.GlyphCount || cluster < request.Start || cluster >= request.Start + request.Length)
                    throw PdfWriterCore.Error(context, "Text.NativeResponse", "The isolated shaper returned an invalid glyph or logical cluster.");
                glyphs[i] = new PdfShapedGlyph(glyph, cluster, Get(reply, at + 8), Get(reply, at + 12), Get(reply, at + 16), Get(reply, at + 20));
            }

            return glyphs;
        }
        finally
        {
            Array.Clear(reply, 0, reply.Length);
        }
    }

    private static int Get(byte[] bytes, int at) => unchecked((int)((uint)bytes[at] | (uint)bytes[at + 1] << 8 | (uint)bytes[at + 2] << 16 | (uint)bytes[at + 3] << 24));
    private static void Put(byte[] bytes, int at, uint value)
    {
        for (int i = 0; i < 4; i++)
            bytes[at + i] = (byte)(value >> (i * 8));
    }
}
