using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Fluent.Pdf.Fonts;
internal sealed class FontData
{
    internal readonly byte[] Bytes;
    internal readonly PdfOperationContext Context;
    internal FontData(byte[] bytes, PdfOperationContext context)
    {
        Bytes = bytes;
        Context = context;
    }

    internal void Range(long offset, long count)
    {
        Context.Checkpoint();
        if (offset < 0 || count < 0 || offset > Bytes.LongLength - count)
            throw Error("Font.Truncated", "A font table, offset or binary extent is outside the supplied program.");
    }

    internal byte U8(long offset)
    {
        Range(offset, 1);
        return Bytes[(int)offset];
    }

    internal int U16(long offset)
    {
        Range(offset, 2);
        return Bytes[(int)offset] * 256 + Bytes[(int)offset + 1];
    }

    internal short I16(long offset) => (short)U16(offset);
    internal uint U32(long offset)
    {
        Range(offset, 4);
        return (uint)(Bytes[(int)offset] << 24 | Bytes[(int)offset + 1] << 16 | Bytes[(int)offset + 2] << 8 | Bytes[(int)offset + 3]);
    }

    internal int I32(long offset) => (int)U32(offset);
    internal string Tag(long offset)
    {
        Range(offset, 4);
        return Encoding.ASCII.GetString(Bytes, (int)offset, 4);
    }

    internal byte[] Copy(long offset, int count)
    {
        Range(offset, count);
        Context.Consume(PdfResourceKind.ExpandedBytes, count);
        var result = new byte[count];
        Buffer.BlockCopy(Bytes, (int)offset, result, 0, count);
        return result;
    }

    internal PdfException Error(string code, string message) => PdfWriterCore.Error(Context, code, message);
    internal static byte[] Read(Stream input, PdfOperationContext context)
    {
        var bytes = PdfByteStore.Read(input, context);
        if (bytes.Length > int.MaxValue)
            throw PdfWriterCore.Error(context, "Font.ProgramRange", "The font exceeds the managed binary-program range.");
        context.Consume(PdfResourceKind.ExpandedBytes, bytes.Length);
        var result = new byte[(int)bytes.Length];
        using var source = bytes.Open(0, bytes.Length);
        int position = 0;
        while (position < result.Length)
        {
            context.Checkpoint();
            int count = source.Read(result, position, Math.Min(4096, result.Length - position));
            if (count == 0)
                throw new EndOfStreamException();
            position += count;
        }

        return result;
    }

    internal static string Hash(byte[] data, PdfOperationContext? context = null)
    {
        using var hash = SHA256.Create();
        for (int offset = 0; offset < data.Length; offset += Math.Min(4096, data.Length - offset))
        {
            context?.Checkpoint();
            int count = Math.Min(4096, data.Length - offset);
            hash.TransformBlock(data, offset, count, data, offset);
        }

        hash.TransformFinalBlock(System.Array.Empty<byte>(), 0, 0);
        return string.Concat(hash.Hash!.Select(x => x.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
    }

    internal static void Put16(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)(value >> 8);
        bytes[offset + 1] = (byte)value;
    }

    internal static void Put32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }

    internal static uint Checksum(byte[] bytes)
    {
        uint result = 0;
        for (int i = 0; i < bytes.Length; i += 4)
        {
            uint word = 0;
            for (int j = 0; j < 4; j++)
                word = (word << 8) | (i + j < bytes.Length ? bytes[i + j] : (byte)0);
            result = unchecked(result + word);
        }

        return result;
    }

    internal static void Scalar(int scalar)
    {
        if (scalar < 0 || scalar > 0x10ffff || scalar >= 0xd800 && scalar <= 0xdfff)
            throw new ArgumentOutOfRangeException(nameof(scalar));
    }

    internal static string ByteText(byte[] bytes, int offset, int count, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, count * 4L);
        var chars = new char[count];
        for (int i = 0; i < count; i++)
        {
            if ((i & 1023) == 0)
                context.Checkpoint();
            chars[i] = (char)bytes[offset + i];
        }

        return new string (chars);
    }
}
