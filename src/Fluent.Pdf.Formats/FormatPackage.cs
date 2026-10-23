using System.IO;
using System.IO.Compression;
using System.Text;

namespace Fluent.Pdf.Formats;
internal sealed class FormatPackage : IDisposable
{
    private readonly Stream _source;
    private readonly ZipArchive _archive;
    private readonly Dictionary<string, (ZipArchiveEntry Entry, uint Crc)> _entries = new(StringComparer.Ordinal);
    private readonly PdfOperationContext _c;
    internal IEnumerable<string> Names => _entries.Keys;

    internal FormatPackage(Stream input, PdfOperationContext c)
    {
        _c = c;
        var bytes = FormatIo.Capture(input, c, 67108864);
        var expected = Preflight(bytes, c);
        _source = bytes.Open(0, bytes.Length);
        try
        {
            _archive = new ZipArchive(_source, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in _archive.Entries)
            {
                if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    continue;
                if (!expected.TryGetValue(entry.FullName, out uint crc) || _entries.ContainsKey(entry.FullName))
                    throw FormatIo.Error(c, "PackageIdentity", "Ambiguous ZIP part identities reject.");
                _entries.Add(entry.FullName, (entry, crc));
            }
        }
        catch (InvalidDataException)
        {
            _source.Dispose();
            c.Checkpoint();
            throw FormatIo.Error(c, "PackageEncoding", "Unsupported or malformed archive encoding rejects.");
        }
        catch
        {
            _source.Dispose();
            throw;
        }
    }

    internal void RequireOpcMain(string main, string contentType, string rootRelationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument")
    {
        var folded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in Names)
        {
            _c.Checkpoint();
            _c.Consume(PdfResourceKind.ExpandedBytes, 128);
            if (!folded.Add(name))
                throw FormatIo.Error(_c, "PackageIdentity", "OPC part identities cannot differ only by ASCII case.");
        }

        System.Xml.Linq.XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships", ct = "http://schemas.openxmlformats.org/package/2006/content-types";
        var root = Xml("_rels/.rels").Root;
        if (root?.Name != rel + "Relationships" || root.Elements().Any(e => e.Name != rel + "Relationship"))
            throw FormatIo.Error(_c, "PackageProfile", "A closed OPC root relationship inventory is required.");
        var identities = new HashSet<string>(StringComparer.Ordinal);
        string? actual = null;
        foreach (var item in root.Elements())
        {
            _c.Checkpoint();
            string id = (string? )item.Attribute("Id") ?? "";
            if (id.Length == 0 || id.Length > 256 || !identities.Add(id) || item.Attribute("TargetMode") != null && (string? )item.Attribute("TargetMode") != "Internal")
                throw FormatIo.Error(_c, "PackageIdentity", "OPC relationships must be unique and internal.");
            string resolved = Resolve("", (string? )item.Attribute("Target") ?? "", _c);
            if ((string? )item.Attribute("Type") == rootRelationshipType)
            {
                if (actual != null)
                    throw FormatIo.Error(_c, "PackageIdentity", "Exactly one main document relationship is required.");
                actual = resolved;
            }
        }

        if (actual != main || !Has(main))
            throw FormatIo.Error(_c, "PackageProfile", "The declared OPC main document does not match this explicit source profile.");
        var types = Xml("[Content_Types].xml").Root;
        if (types?.Name != ct + "Types")
            throw FormatIo.Error(_c, "PackageProfile", "An OPC content-type inventory is required.");
        var overrides = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var defaults = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? declared = null;
        foreach (var item in types.Elements())
        {
            _c.Checkpoint();
            if (item.Name == ct + "Default")
            {
                string extension = (string? )item.Attribute("Extension") ?? "";
                if (extension.Length == 0 || extension.Length > 256 || !defaults.Add(extension))
                    throw FormatIo.Error(_c, "PackageIdentity", "OPC extension declarations must be unique.");
            }
            else if (item.Name == ct + "Override")
            {
                string part = (string? )item.Attribute("PartName") ?? "";
                if (part.Length > 1025 || !part.StartsWith("/", StringComparison.Ordinal))
                    throw FormatIo.Error(_c, "PackageUri", "An OPC override requires an absolute part name.");
                Name(part.Substring(1), _c);
                part = Resolve("", part, _c);
                if (!overrides.Add(part))
                    throw FormatIo.Error(_c, "PackageIdentity", "OPC part declarations must be unique.");
                if (part == main)
                    declared = (string? )item.Attribute("ContentType");
            }
            else
                throw FormatIo.Error(_c, "PackageProfile", "Unsupported content-type declarations reject.");
        }

        if (declared != contentType)
            throw FormatIo.Error(_c, "PackageProfile", "The main document content type does not match the requested format profile.");
    }

    internal bool Has(string name) => _entries.ContainsKey(name);
    internal PdfByteStore Read(string name)
    {
        try
        {
            return ReadCore(name);
        }
        catch (InvalidDataException)
        {
            _c.Checkpoint();
            throw FormatIo.Error(_c, "PackageIntegrity", "Malformed compressed package data or failed CRC integrity rejects.");
        }
    }

    private PdfByteStore ReadCore(string name)
    {
        if (!_entries.TryGetValue(name, out var captured))
            throw FormatIo.Error(_c, "PackageReference", "A declared package part is missing.");
        using var input = captured.Entry.Open();
        var bytes = FormatIo.Capture(input, _c, 16777216);
        uint crc = 0xffffffff;
        using var contents = bytes.Open(0, bytes.Length);
        _c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var part = new byte[8192];
        int n;
        while ((n = contents.Read(part, 0, part.Length)) > 0)
        {
            _c.Checkpoint();
            for (int i = 0; i < n; i++)
                crc = Crc[(crc ^ part[i]) & 255] ^ (crc >> 8);
        }

        if (bytes.Length != captured.Entry.Length || (crc ^ 0xffffffff) != captured.Crc)
            throw FormatIo.Error(_c, "PackageIntegrity", "A package part failed its independent extent/CRC check.");
        return bytes;
    }

    internal System.Xml.Linq.XDocument Xml(string name)
    {
        var bytes = Read(name);
        using var source = bytes.Open(0, bytes.Length);
        return FormatIo.Xml(source, _c);
    }

    internal string Text(string name)
    {
        var bytes = Read(name);
        using var source = bytes.Open(0, bytes.Length);
        return FormatIo.Text(source, _c);
    }

    internal static string Resolve(string owner, string target, PdfOperationContext c)
    {
        if (target.Length == 0 || target.Length > 1024 || target.IndexOfAny(new[] { '\\', ':', '%', '?', '#' }) >= 0 || target.StartsWith("//", StringComparison.Ordinal))
            throw FormatIo.Error(c, "PackageUri", "Only unambiguous internal package references are supported.");
        c.Consume(PdfResourceKind.Objects, 1);
        c.Consume(PdfResourceKind.ExpandedBytes, (owner.Length + target.Length) * 48L + 4096);
        var parts = new List<string>();
        if (!target.StartsWith("/", StringComparison.Ordinal))
        {
            int end = owner.LastIndexOf('/');
            if (end >= 0)
                parts.AddRange(owner.Substring(0, end).Split('/'));
        }

        foreach (string part in target.Split('/'))
        {
            if (part == "" || part == ".")
                continue;
            if (part == "..")
            {
                if (parts.Count == 0)
                    throw FormatIo.Error(c, "PackageTraversal", "A package reference escapes its root.");
                parts.RemoveAt(parts.Count - 1);
            }
            else
                parts.Add(part);
        }

        if (parts.Count == 0)
            throw FormatIo.Error(c, "PackageUri", "A package reference requires a part name.");
        return string.Join("/", parts);
    }

    internal static PdfByteStore Write(IEnumerable<(string Name, byte[] Bytes, bool Stored)> entries, PdfOperationContext c)
    {
        using var output = new PdfCodecBufferStream(c, 67108864);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var directory = new List<(byte[] Name, uint Crc, int Method, int Length, long Compressed, long Offset)>();
        long expanded = 0;
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var chunk = new byte[8192];
        void U16(int value)
        {
            output.WriteByte((byte)value);
            output.WriteByte((byte)(value >> 8));
        }

        void U32(long value)
        {
            U16((int)value);
            U16((int)(value >> 16));
        }

        foreach (var item in entries)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, item.Name.Length * 32L + 4096);
            if (item.Bytes.Length > 16777216 || (expanded += item.Bytes.Length) > 67108864)
                throw FormatIo.Error(c, "PackageLimits", "Package part/total expansion exceeds the declared profile.");
            if (directory.Count == 4096 || !names.Add(item.Name))
                throw FormatIo.Error(c, "PackageIdentity", "A finite unique package inventory is required.");
            Name(item.Name, c);
            var name = Encoding.ASCII.GetBytes(item.Name);
            uint crc = 0xffffffff;
            for (int at = 0; at < item.Bytes.Length; at++)
            {
                if ((at & 8191) == 0)
                    c.Checkpoint();
                crc = Crc[(crc ^ item.Bytes[at]) & 255] ^ (crc >> 8);
            }

            crc ^= 0xffffffff;
            PdfByteStore? compressed = null;
            if (!item.Stored)
            {
                using var buffer = new PdfCodecBufferStream(c, 17825792);
                using (var deflate = new DeflateStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
                    for (int at = 0; at < item.Bytes.Length; at += 8192)
                    {
                        c.Checkpoint();
                        deflate.Write(item.Bytes, at, Math.Min(8192, item.Bytes.Length - at));
                    }

                compressed = buffer.Finish();
            }

            long length = compressed?.Length ?? item.Bytes.Length, offset = output.BufferedLength;
            int method = item.Stored ? 0 : 8;
            U32(0x04034b50);
            U16(20);
            U16(0);
            U16(method);
            U16(0);
            U16(33);
            U32(crc);
            U32(length);
            U32(item.Bytes.Length);
            U16(name.Length);
            U16(0);
            output.Write(name, 0, name.Length);
            if (compressed == null)
            {
                for (int at = 0; at < item.Bytes.Length; at += 8192)
                {
                    c.Checkpoint();
                    output.Write(item.Bytes, at, Math.Min(8192, item.Bytes.Length - at));
                }
            }
            else
            {
                using var input = compressed.Open(0, compressed.Length);
                int n;
                while ((n = input.Read(chunk, 0, chunk.Length)) > 0)
                {
                    c.Checkpoint();
                    output.Write(chunk, 0, n);
                }
            }

            directory.Add((name, crc, method, item.Bytes.Length, length, offset));
        }

        long start = output.BufferedLength;
        foreach (var item in directory)
        {
            c.Checkpoint();
            U32(0x02014b50);
            U16(20);
            U16(20);
            U16(0);
            U16(item.Method);
            U16(0);
            U16(33);
            U32(item.Crc);
            U32(item.Compressed);
            U32(item.Length);
            U16(item.Name.Length);
            U16(0);
            U16(0);
            U16(0);
            U16(0);
            U32(0);
            U32(item.Offset);
            output.Write(item.Name, 0, item.Name.Length);
        }

        long size = output.BufferedLength - start;
        U32(0x06054b50);
        U16(0);
        U16(0);
        U16(directory.Count);
        U16(directory.Count);
        U32(size);
        U32(start);
        U16(0);
        return output.Finish();
    }

    private static Dictionary<string, uint> Preflight(PdfByteStore bytes, PdfOperationContext c)
    {
        int Byte(long p)
        {
            if (p < 0 || p >= bytes.Length)
                throw FormatIo.Error(c, "PackageExtent", "ZIP header exceeds its extent.");
            return bytes.At(p);
        }

        int U16(long p) => Byte(p) | Byte(p + 1) << 8;
        uint U32(long p) => (uint)Byte(p) | (uint)Byte(p + 1) << 8 | (uint)Byte(p + 2) << 16 | (uint)Byte(p + 3) << 24;
        long end = -1;
        for (long at = bytes.Length - 22; at >= Math.Max(0, bytes.Length - 65557); at--)
        {
            c.Checkpoint();
            if (U32(at) == 0x06054b50 && at + 22 + U16(at + 20) == bytes.Length)
            {
                end = at;
                break;
            }
        }

        if (end < 0 || U16(end + 4) != 0 || U16(end + 6) != 0 || U16(end + 8) != U16(end + 10) || U16(end + 10) < 1 || U16(end + 10) > 4096)
            throw FormatIo.Error(c, "PackageProfile", "A finite single-disk non-ZIP64 package is required.");
        long directory = U32(end + 16), size = U32(end + 12), atDirectory = directory, expanded = 0;
        if (directory + size != end)
            throw FormatIo.Error(c, "PackageExtent", "The central directory must match its declared extent.");
        var ranges = new List<(long Start, long End)>();
        var result = new Dictionary<string, uint>(StringComparer.Ordinal);
        var all = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < U16(end + 10); index++)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, 1024);
            if (atDirectory > end - 46 || U32(atDirectory) != 0x02014b50)
                throw FormatIo.Error(c, "PackageHeader", "A complete central-directory entry is required.");
            int flags = U16(atDirectory + 8), method = U16(atDirectory + 10), nameLength = U16(atDirectory + 28), extra = U16(atDirectory + 30), comment = U16(atDirectory + 32);
            long compressed = U32(atDirectory + 20), length = U32(atDirectory + 24), local = U32(atDirectory + 42);
            if ((flags & ~0x0808) != 0 || (method != 0 && method != 8) || nameLength < 1 || nameLength > 1024 || atDirectory + 46 + nameLength + extra + comment > end || length > 16777216 || (expanded += length) > 67108864 || U16(atDirectory + 34) != 0)
                throw FormatIo.Error(c, "PackageLimits", "Encrypted/unsupported ZIP methods, part extents and expansion limits reject.");
            c.Consume(PdfResourceKind.ExpandedBytes, nameLength * 32L + length + extra * 8L + comment * 4L + 4096);
            var nameBytes = new byte[nameLength];
            for (int i = 0; i < nameLength; i++)
            {
                int value = Byte(atDirectory + 46 + i);
                if (value < 32 || value > 126)
                    throw FormatIo.Error(c, "PackageName", "This package profile requires bounded canonical ASCII part names.");
                nameBytes[i] = (byte)value;
            }

            Extras(atDirectory + 46 + nameLength, extra);
            string name = Encoding.ASCII.GetString(nameBytes);
            Name(name, c);
            if (!all.Add(name))
                throw FormatIo.Error(c, "PackageIdentity", "Duplicate package names reject.");
            if (local > directory - 30 || U32(local) != 0x04034b50 || U16(local + 6) != flags || U16(local + 8) != method || U16(local + 26) != nameLength)
                throw FormatIo.Error(c, "PackageHeader", "Local and central ZIP headers disagree.");
            for (int i = 0; i < nameLength; i++)
                if (Byte(local + 30 + i) != nameBytes[i])
                    throw FormatIo.Error(c, "PackageIdentity", "Local and central ZIP names disagree.");
            int localExtra = U16(local + 28);
            c.Consume(PdfResourceKind.ExpandedBytes, localExtra * 4L);
            long data = local + 30 + nameLength + localExtra;
            if (data > directory || compressed > directory - data)
                throw FormatIo.Error(c, "PackageExtent", "Compressed part bytes exceed their package extent.");
            Extras(local + 30 + nameLength, localExtra);
            uint expectedCrc = U32(atDirectory + 16);
            long extent = data + compressed;
            if ((flags & 8) == 0)
            {
                if (U32(local + 14) != expectedCrc || U32(local + 18) != compressed || U32(local + 22) != length)
                    throw FormatIo.Error(c, "PackageHeader", "Local and central ZIP sizes/CRC disagree.");
            }
            else
            {
                if (extent > directory - 12)
                    throw FormatIo.Error(c, "PackageExtent", "A complete data descriptor is required.");
                long descriptor = U32(extent) == 0x08074b50 ? extent + 4 : extent;
                if (descriptor > directory - 12 || U32(descriptor) != expectedCrc || U32(descriptor + 4) != compressed || U32(descriptor + 8) != length)
                    throw FormatIo.Error(c, "PackageHeader", "ZIP data descriptor disagrees with its central entry.");
                extent = descriptor + 12;
            }

            ranges.Add((local, extent));
            if (!name.EndsWith("/", StringComparison.Ordinal))
                result.Add(name, U32(atDirectory + 16));
            atDirectory += 46 + nameLength + extra + comment;
        }

        if (atDirectory != end)
            throw FormatIo.Error(c, "PackageExtent", "The complete central-directory inventory is required.");
        c.Checkpoint();
        ranges.Sort((left, right) => left.Start.CompareTo(right.Start));
        c.Checkpoint();
        long priorEnd = 0;
        foreach (var range in ranges)
        {
            c.Checkpoint();
            if (range.Start < priorEnd)
                throw FormatIo.Error(c, "PackageExtent", "Overlapping local package extents reject.");
            priorEnd = range.End;
        }

        return result;
        void Extras(long start, int length)
        {
            long limit = start + length;
            for (long at = start; at < limit;)
            {
                c.Checkpoint();
                if (at > limit - 4 || U16(at + 2) > limit - at - 4)
                    throw FormatIo.Error(c, "PackageHeader", "Malformed ZIP extra-field extent rejects.");
                int id = U16(at);
                if (id == 1 || id == 0x7075)
                    throw FormatIo.Error(c, "PackageProfile", "ZIP64 and alternate Unicode path identities require another package profile.");
                at += 4 + U16(at + 2);
            }
        }
    }

    private static void Name(string name, PdfOperationContext c)
    {
        if (name.Length > 1024 || name.StartsWith("/", StringComparison.Ordinal) || name.IndexOfAny(new[] { '\\', ':', '%', '\0' }) >= 0 || name.TrimEnd('/').Split('/').Any(part => part == "" || part == "." || part == ".."))
            throw FormatIo.Error(c, "PackageTraversal", "Unsafe or ambiguous ZIP part paths reject.");
    }

    private static readonly uint[] Crc = BuildCrc();
    private static uint[] BuildCrc()
    {
        var values = new uint[256];
        for (uint i = 0; i < values.Length; i++)
        {
            uint v = i;
            for (int bit = 0; bit < 8; bit++)
                v = (v & 1) == 0 ? v >> 1 : (v >> 1) ^ 0xedb88320;
            values[i] = v;
        }

        return values;
    }

    public void Dispose()
    {
        _archive.Dispose();
        _source.Dispose();
    }
}
