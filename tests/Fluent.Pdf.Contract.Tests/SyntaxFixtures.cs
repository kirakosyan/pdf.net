using System.Globalization;
using System.IO;
using System.Text;

namespace Fluent.Pdf.Contract.Tests;
// Original, hand-specified syntax fixtures. This emitter is independent of the SDK writer.
internal sealed class SyntaxFixtures : IDisposable
{
    internal readonly MemoryStream Bytes = new();
    internal SyntaxFixtures(string version = "1.7") => Text("%PDF-" + version + "\n");
    internal void Text(string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        Bytes.Write(bytes, 0, bytes.Length);
    }

    internal long Object(int number, string value, int generation = 0)
    {
        long offset = Bytes.Position;
        Text(number + " " + generation + " obj\n" + value + "\nendobj\n");
        return offset;
    }

    internal long StreamObject(int number, string dictionary, byte[] data)
    {
        long offset = Bytes.Position;
        Text(number + " 0 obj\n<< /Length " + data.Length + " " + dictionary + " >>\nstream\n");
        Bytes.Write(data, 0, data.Length);
        Text("\nendstream\nendobj\n");
        return offset;
    }

    internal long Classic((int id, int generation, long offset, bool free)[] entries, string trailer = "", int size = 16)
    {
        long xref = Bytes.Position;
        Text("xref\n");
        foreach (var entry in entries)
        {
            Text(entry.id + " 1\n" + entry.offset.ToString("0000000000", CultureInfo.InvariantCulture) + " " + entry.generation.ToString("00000", CultureInfo.InvariantCulture) + " " + (entry.free ? "f" : "n") + " \n");
        }

        Text("trailer\n<< /Size " + size + " " + trailer + " >>\nstartxref\n" + xref + "\n%%EOF\n");
        return xref;
    }

    internal static byte[] Simple(string body, string version = "1.7")
    {
        using var f = new SyntaxFixtures(version);
        long offset = f.Object(1, body);
        f.Classic(new[] { (0, 65535, 0L, true), (1, 0, offset, false) });
        return f.Bytes.ToArray();
    }

    internal static byte[] Entry(int type, long a, int b) => new[]
    {
        (byte)type,
        (byte)(a >> 24),
        (byte)(a >> 16),
        (byte)(a >> 8),
        (byte)a,
        (byte)(b >> 8),
        (byte)b
    };
    internal static byte[] ZlibStored(byte[] data)
    {
        // RFC 1950 header, one final stored block and independently accumulated Adler-32.
        using var result = new MemoryStream();
        result.WriteByte(0x78);
        result.WriteByte(1);
        result.WriteByte(1);
        result.WriteByte((byte)data.Length);
        result.WriteByte((byte)(data.Length >> 8));
        int complement = data.Length ^ 65535;
        result.WriteByte((byte)complement);
        result.WriteByte((byte)(complement >> 8));
        result.Write(data, 0, data.Length);
        uint a = 1, b = 0;
        foreach (byte value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }

        uint checksum = b << 16 | a;
        for (int shift = 24; shift >= 0; shift -= 8)
            result.WriteByte((byte)(checksum >> shift));
        return result.ToArray();
    }

    internal static byte[] CrossReferenceStream(bool compressed = false, bool predictor = false)
    {
        using var f = new SyntaxFixtures("1.5");
        long one = f.Object(1, "<< /Type /Catalog /Unknown (preserved) >>"), xref = f.Bytes.Position;
        byte[] data = Entry(0, 0, 65535).Concat(Entry(1, one, 0)).Concat(Entry(1, xref, 0)).ToArray();
        string parameters = "";
        if (predictor)
        {
            var rows = new List<byte>();
            byte[] previous = new byte[7];
            for (int row = 0; row < 3; row++)
            {
                rows.Add(2);
                for (int i = 0; i < 7; i++)
                    rows.Add(unchecked((byte)(data[row * 7 + i] - previous[i])));
                System.Array.Copy(data, row * 7, previous, 0, 7);
            }

            data = rows.ToArray();
            parameters = " /DecodeParms << /Predictor 12 /Columns 7 >>";
        }

        if (compressed)
            data = ZlibStored(data);
        f.StreamObject(2, "/Type /XRef /Size 3 /Root 1 0 R /W [1 4 2]" + (compressed ? " /Filter /FlateDecode" : "") + parameters, data);
        f.Text("startxref\n" + xref + "\n%%EOF\n");
        return f.Bytes.ToArray();
    }

    public void Dispose() => Bytes.Dispose();
}
