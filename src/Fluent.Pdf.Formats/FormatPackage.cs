using System.IO;
using System.IO.Compression;
using System.Text;

namespace Fluent.Pdf.Formats;
internal sealed class FormatPackage : IDisposable
{
    private readonly Stream _source; private readonly ZipArchive _archive; private readonly Dictionary<string, (ZipArchiveEntry Entry, uint Crc)> _entries = new(StringComparer.Ordinal); private readonly PdfOperationContext _c;
    internal IEnumerable<string> Names => _entries.Keys;
    internal FormatPackage(Stream input, PdfOperationContext c)
    {
        _c = c; var bytes = FormatIo.Capture(input, c, 67108864); var expected = Preflight(bytes, c); _source = bytes.Open(0, bytes.Length);
        try
        {
            _archive = new ZipArchive(_source, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in _archive.Entries)
            {
                if (entry.FullName.EndsWith("/", StringComparison.Ordinal)) continue;
                if (!expected.TryGetValue(entry.FullName, out uint crc) || _entries.ContainsKey(entry.FullName)) throw FormatIo.Error(c, "PackageIdentity", "Ambiguous ZIP part identities reject."); _entries.Add(entry.FullName, (entry, crc));
            }
        }
        catch { _source.Dispose(); throw; }
    }
    internal bool Has(string name) => _entries.ContainsKey(name);
    internal PdfByteStore Read(string name)
    {
        if (!_entries.TryGetValue(name, out var captured)) throw FormatIo.Error(_c, "PackageReference", "A declared package part is missing.");
        using var input = captured.Entry.Open(); var bytes = FormatIo.Capture(input, _c, 16777216); uint crc = 0xffffffff;
        using var contents = bytes.Open(0, bytes.Length); _c.Consume(PdfResourceKind.ExpandedBytes, 8192); var part = new byte[8192]; int n;
        while ((n = contents.Read(part, 0, part.Length)) > 0) { _c.Checkpoint(); for (int i = 0; i < n; i++) crc = Crc[(crc ^ part[i]) & 255] ^ (crc >> 8); }
        if (bytes.Length != captured.Entry.Length || (crc ^ 0xffffffff) != captured.Crc) throw FormatIo.Error(_c, "PackageCrc", "A package part failed its independent extent/CRC check."); return bytes;
    }
    internal System.Xml.Linq.XDocument Xml(string name) { var bytes = Read(name); using var source = bytes.Open(0, bytes.Length); return FormatIo.Xml(source, _c); }
    internal string Text(string name) { var bytes = Read(name); using var source = bytes.Open(0, bytes.Length); return FormatIo.Text(source, _c); }
    internal static string Resolve(string owner, string target, PdfOperationContext c)
    {
        if (target.Length == 0 || target.Length > 1024 || target.IndexOfAny(new[] { '\\', ':', '%', '?', '#' }) >= 0 || target.StartsWith("//", StringComparison.Ordinal)) throw FormatIo.Error(c, "PackageUri", "Only unambiguous internal package references are supported.");
        var parts = new List<string>(); if (!target.StartsWith("/", StringComparison.Ordinal)) { int end = owner.LastIndexOf('/'); if (end >= 0) parts.AddRange(owner.Substring(0, end).Split('/')); }
        foreach (string part in target.Split('/')) { if (part == "" || part == ".") continue; if (part == "..") { if (parts.Count == 0) throw FormatIo.Error(c, "PackageTraversal", "A package reference escapes its root."); parts.RemoveAt(parts.Count - 1); } else parts.Add(part); }
        if (parts.Count == 0) throw FormatIo.Error(c, "PackageUri", "A package reference requires a part name."); return string.Join("/", parts);
    }
    internal static PdfByteStore Write(IEnumerable<(string Name, byte[] Bytes, bool Stored)> entries, PdfOperationContext c)
    {
        using var output = new PdfCodecBufferStream(c, 67108864); var names = new HashSet<string>(StringComparer.Ordinal); int count = 0;
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        foreach (var item in entries)
        {
            c.Checkpoint(); c.Consume(PdfResourceKind.Objects, 1); c.Consume(PdfResourceKind.ExpandedBytes, item.Name.Length * 4L + 1024);
            if (++count > 4096 || !names.Add(item.Name)) throw FormatIo.Error(c, "PackageIdentity", "A finite unique package inventory is required."); Name(item.Name, c);
            var entry = zip.CreateEntry(item.Name, item.Stored ? CompressionLevel.NoCompression : CompressionLevel.Optimal); entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using var target = entry.Open(); for (int at = 0; at < item.Bytes.Length;) { c.Checkpoint(); int n = Math.Min(8192, item.Bytes.Length - at); target.Write(item.Bytes, at, n); at += n; }
        }
        return output.Finish();
    }
    private static Dictionary<string, uint> Preflight(PdfByteStore bytes, PdfOperationContext c)
    {
        int Byte(long p) { if (p < 0 || p >= bytes.Length) throw FormatIo.Error(c, "PackageExtent", "ZIP header exceeds its extent."); return bytes.At(p); }
        int U16(long p) => Byte(p) | Byte(p + 1) << 8;
        uint U32(long p) => (uint)Byte(p) | (uint)Byte(p + 1) << 8 | (uint)Byte(p + 2) << 16 | (uint)Byte(p + 3) << 24;
        long end = -1;
        for (long at = bytes.Length - 22; at >= Math.Max(0, bytes.Length - 65557); at--) { c.Checkpoint(); if (U32(at) == 0x06054b50 && at + 22 + U16(at + 20) == bytes.Length) { end = at; break; } }
        if (end < 0 || U16(end + 4) != 0 || U16(end + 6) != 0 || U16(end + 8) != U16(end + 10) || U16(end + 10) < 1 || U16(end + 10) > 4096) throw FormatIo.Error(c, "PackageProfile", "A finite single-disk non-ZIP64 package is required.");
        long directory = U32(end + 16), size = U32(end + 12), atDirectory = directory, expanded = 0;
        if (directory + size != end) throw FormatIo.Error(c, "PackageExtent", "The central directory must match its declared extent.");
        var result = new Dictionary<string, uint>(StringComparer.Ordinal); var all = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < U16(end + 10); index++)
        {
            c.Checkpoint(); c.Consume(PdfResourceKind.Objects, 1); c.Consume(PdfResourceKind.ExpandedBytes, 1024);
            if (atDirectory > end - 46 || U32(atDirectory) != 0x02014b50) throw FormatIo.Error(c, "PackageHeader", "A complete central-directory entry is required.");
            int flags = U16(atDirectory + 8), method = U16(atDirectory + 10), nameLength = U16(atDirectory + 28), extra = U16(atDirectory + 30), comment = U16(atDirectory + 32); long compressed = U32(atDirectory + 20), length = U32(atDirectory + 24), local = U32(atDirectory + 42);
            if ((flags & ~0x0808) != 0 || (method != 0 && method != 8) || nameLength < 1 || nameLength > 1024 || atDirectory + 46 + nameLength + extra + comment > end || length > 16777216 || (expanded += length) > 67108864 || U16(atDirectory + 34) != 0) throw FormatIo.Error(c, "PackageLimits", "Encrypted/unsupported ZIP methods, part extents and expansion limits reject.");
            c.Consume(PdfResourceKind.ExpandedBytes, nameLength * 4L + length); var nameBytes = new byte[nameLength]; for (int i = 0; i < nameLength; i++) { int value = Byte(atDirectory + 46 + i); if (value < 32 || value > 126) throw FormatIo.Error(c, "PackageName", "This package profile requires bounded canonical ASCII part names."); nameBytes[i] = (byte)value; }
            string name = Encoding.ASCII.GetString(nameBytes); Name(name, c); if (!all.Add(name)) throw FormatIo.Error(c, "PackageIdentity", "Duplicate package names reject.");
            if (local > directory - 30 || U32(local) != 0x04034b50 || U16(local + 6) != flags || U16(local + 8) != method || U16(local + 26) != nameLength) throw FormatIo.Error(c, "PackageHeader", "Local and central ZIP headers disagree.");
            for (int i = 0; i < nameLength; i++) if (Byte(local + 30 + i) != nameBytes[i]) throw FormatIo.Error(c, "PackageIdentity", "Local and central ZIP names disagree.");
            long data = local + 30 + nameLength + U16(local + 28); if (data > directory || compressed > directory - data) throw FormatIo.Error(c, "PackageExtent", "Compressed part bytes exceed their package extent.");
            if (!name.EndsWith("/", StringComparison.Ordinal)) result.Add(name, U32(atDirectory + 16)); atDirectory += 46 + nameLength + extra + comment;
        }
        if (atDirectory != end) throw FormatIo.Error(c, "PackageExtent", "The complete central-directory inventory is required."); return result;
    }
    private static void Name(string name, PdfOperationContext c) { if (name.Length > 1024 || name.StartsWith("/", StringComparison.Ordinal) || name.IndexOfAny(new[] { '\\', ':', '%', '\0' }) >= 0 || name.TrimEnd('/').Split('/').Any(part => part == "" || part == "." || part == "..")) throw FormatIo.Error(c, "PackageTraversal", "Unsafe or ambiguous ZIP part paths reject."); }
    private static readonly uint[] Crc = BuildCrc();
    private static uint[] BuildCrc() { var values = new uint[256]; for (uint i = 0; i < values.Length; i++) { uint v = i; for (int bit = 0; bit < 8; bit++) v = (v & 1) == 0 ? v >> 1 : (v >> 1) ^ 0xedb88320; values[i] = v; } return values; }
    public void Dispose() { _archive.Dispose(); _source.Dispose(); }
}
