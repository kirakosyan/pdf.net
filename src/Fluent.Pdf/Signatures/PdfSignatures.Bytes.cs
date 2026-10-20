using System.IO;

namespace Fluent.Pdf;

public static partial class PdfSignatures
{
    private static PdfException UnsupportedProfile(string message) => new(new PdfDiagnostic("Signature.ByteRangeProfile", message, severity: PdfDiagnosticSeverity.Warning));
    private static int Find(byte[] bytes, int start, string text, PdfOperationContext c) => FindAt(i => bytes[i], bytes.Length, start, text, c);
    private static int FindAt(Func<long, int> at, long length, long start, string text, PdfOperationContext c)
    {
        long end = Math.Min(length, start + 4194304);
        for (long i = start; i <= end - text.Length; i++)
        {
            if ((i & 4095) == 0)
                c.Checkpoint();
            int j = 0;
            while (j < text.Length && at(i + j) == text[j])
                j++;
            if (j == text.Length)
                return checked((int)i);
        }

        throw UnsupportedProfile("The signature token exceeds the bounded direct-object profile.");
    }

    private static (long[] Ranges, byte[] Cms) Ranges(PdfSyntaxDocument source, PdfObjectId id, PdfValue signature, PdfOperationContext c, int revisionIndex = 0)
    {
        if (source.Source.Length > int.MaxValue - 32)
            throw Error(c, "Extent", "The contiguous signature inspection profile exceeds its finite extent.");
        PdfValue Resolve(PdfValue v) => ResolveCore(source, v, c, revisionIndex);
        var declared = Get(signature, "ByteRange", Resolve);
        var contents = Get(signature, "Contents", Resolve);
        var sub = Get(signature, "SubFilter", Resolve);
        if (declared.Kind != PdfValueKind.Array || declared.Items.Count != 4 || contents.Kind != PdfValueKind.String || sub.Kind != PdfValueKind.Name || sub.NameValue != "adbe.pkcs7.detached")
            throw UnsupportedProfile("Only exact two-pair detached CMS signature ranges are supported.");
        if (contents.StringBytes.Count > 1048576)
            throw Error(c, "CmsExtent", "CMS contents exceed the finite signature profile.");
        c.Consume(PdfResourceKind.ExpandedBytes, contents.StringBytes.Count + 512);
        var ranges = new long[4];
        for (int i = 0; i < 4; i++)
            if (!declared.Items[i].TryGetInteger(out ranges[i]) || ranges[i] < 0)
                throw Error(c, "ByteRange", "Signature ranges must be nonnegative integers.");
        if (ranges[0] != 0 || ranges[1] >= ranges[2] || ranges[2] > source.Source.Length || ranges[3] > source.Source.Length - ranges[2])
            throw Error(c, "ByteRange", "Signature ranges must cover a complete revision with one Contents gap.");
        var entry = source.Revisions.Skip(revisionIndex).SelectMany(r => r.Entries).FirstOrDefault(e => e.Id.Number == id.Number);
        if (entry == null || entry.Id != id || entry.Kind != PdfCrossReferenceKind.Offset)
            throw UnsupportedProfile("An uncompressed indirect signature object is required.");
        var cursor = new PdfSyntaxCursor(source.Source, c, entry.Offset, Math.Min(source.Source.Length, entry.Offset + 4194304));
        cursor.Integer();
        cursor.Integer();
        cursor.Require("obj");
        cursor.Skip();
        if (cursor.Peek() != '<' || source.Source.At(cursor.Position + 1) != '<')
            throw UnsupportedProfile("The signature object must be a direct dictionary.");
        cursor.Position += 2;
        long found = -1;
        while (true)
        {
            cursor.Skip();
            if (cursor.Peek() == '>')
                break;
            var key = cursor.Value();
            if (key.Kind != PdfValueKind.Name)
                throw UnsupportedProfile("Signature dictionary keys must be names.");
            cursor.Skip();
            if (key.NameValue == "Contents")
            {
                found = cursor.Position;
                break;
            }
            cursor.Value();
        }

        if (found != ranges[1] || ranges[2] - ranges[1] != contents.StringBytes.Count * 2L + 2 || source.Source.At(ranges[1]) != 60 || source.Source.At(ranges[2] - 1) != 62)
            throw Error(c, "ByteRange", "The excluded gap must be exactly this signature's complete hex Contents token.");
        for (int i = 0; i < contents.StringBytes.Count; i++)
        {
            if ((i & 4095) == 0)
                c.Checkpoint();
            int a = Hex(source.Source.At(found + 1 + i * 2)), b = Hex(source.Source.At(found + 2 + i * 2));
            if (a < 0 || b < 0 || (a << 4 | b) != contents.StringBytes[i])
                throw Error(c, "ByteRange", "The excluded token differs from the parsed CMS contents.");
        }

        long end = ranges[2] + ranges[3], tail = end;
        while (tail > 0 && White(source.Source.At(tail - 1)))
            tail--;
        const string eof = "%%EOF";
        if (tail < 5)
            throw Error(c, "RevisionExtent", "The covered revision must end at an exact PDF EOF boundary.");
        for (int i = 0; i < 5; i++)
            if (source.Source.At(tail - 5 + i) != eof[i])
                throw Error(c, "RevisionExtent", "The covered revision must end at an exact PDF EOF boundary.");
        if (contents.StringBytes.Count > 1048576)
            throw Error(c, "CmsExtent", "CMS contents exceed the finite signature profile.");
        if (end != source.Source.Length)
        {
            using var prefix = source.Source.Open(0, end);
            var revision = new PdfSyntaxReader(PdfByteStore.Read(prefix, c), c).Read();
            if (!source.Revisions.Skip(revisionIndex).Any(r => r.StartXref == revision.Revisions[0].StartXref))
                throw Error(c, "RevisionExtent", "The signed prefix must be a linked source revision.");
            var original = revision.Resolve(id);
            if (original.Kind != PdfValueKind.Dictionary || !original.Entries.TryGetValue("Contents", out var oldContents) || oldContents.Kind != PdfValueKind.String || !oldContents.StringBytes.SequenceEqual(contents.StringBytes))
                throw Error(c, "RevisionExtent", "The covered prefix must resolve this exact signature in a complete signed PDF revision.");
        }

        var padded = contents.StringBytes.ToArray();
        int length = DerLength(padded, c);
        for (int i = length; i < padded.Length; i++)
            if (padded[i] != 0)
                throw Error(c, "CmsPadding", "Only zero padding may follow the single CMS value.");
        c.Consume(PdfResourceKind.ExpandedBytes, length);
        var cms = new byte[length];
        Array.Copy(padded, cms, length);
        return (ranges, cms);
        static bool White(int b) => b == 0 || b == 9 || b == 10 || b == 12 || b == 13 || b == 32;
        static int Hex(int b) => b >= 48 && b <= 57 ? b - 48 : b >= 65 && b <= 70 ? b - 55 : b >= 97 && b <= 102 ? b - 87 : -1;
    }

    private static int DerLength(byte[] bytes, PdfOperationContext c)
    {
        if (bytes.Length < 2 || bytes[0] != 48)
            throw Error(c, "CmsEncoding", "A DER CMS sequence is required.");
        int n = bytes[1], header = 2;
        if (n >= 128)
        {
            int count = n & 127;
            if (count == 0 || count > 4 || bytes.Length < 2 + count || bytes[2] == 0)
                throw Error(c, "CmsEncoding", "A bounded minimal definite DER length is required.");
            long size = 0;
            for (int i = 0; i < count; i++)
                size = (size << 8) | bytes[2 + i];
            if (size < 128 || size > 1048576)
                throw Error(c, "CmsEncoding", "The CMS length is not minimal or exceeds its ceiling.");
            n = (int)size;
            header += count;
        }

        if (n > bytes.Length - header)
            throw Error(c, "CmsEncoding", "The CMS sequence is truncated.");
        return n + header;
    }

    internal static void Copy(PdfByteStore bytes, Stream output, PdfOperationContext c)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed stream is required.", nameof(output));
        c.Checkpoint();
        if (!output.CanSeek || output.Position != 0 || output.Length != 0)
            throw Error(c, "OutputExtent", "Exact signed files require an empty seekable destination.");
        c.Consume(PdfResourceKind.ExpandedBytes, 65536);
        var buffer = new byte[65536];
        using var input = c.BorrowExpandedInput(bytes.Open(0, bytes.Length));
        using var sink = new BorrowedStream(output, false, c);
        int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            c.Checkpoint();
            sink.Write(buffer, 0, count);
        }
    }

    private sealed class RangeStream : Stream
    {
        private readonly Func<long, long, Stream> _open;
        private readonly long[] _ranges;
        private long _position;
        private bool _disposed;
        internal RangeStream(PdfByteStore source, long[] ranges)
        {
            _open = source.Open;
            _ranges = ranges;
        }

        internal RangeStream(byte[] source, long[] ranges)
        {
            _open = (offset, length) => new MemoryStream(source, checked((int)offset), checked((int)length), false);
            _ranges = ranges;
        }

        public override bool CanRead => !_disposed;
        public override bool CanSeek => !_disposed;
        public override bool CanWrite => false;
        public override long Length => _ranges[1] + _ranges[3];

        public override long Position
        {
            get => _position;
            set
            {
                Check();
                if (value < 0 || value > Length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _position = value;
            }
        }

        private void Check()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RangeStream));
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Check();
            if (buffer == null || offset < 0 || count < 0 || offset > buffer.Length - count)
                throw new ArgumentException("A valid read buffer is required.");
            int total = 0;
            while (count > 0 && _position < Length)
            {
                long firstRemaining = _ranges[1] - _position;
                long start = firstRemaining > 0 ? _position : _ranges[2] + _position - _ranges[1];
                int extent = (int)Math.Min(count, firstRemaining > 0 ? firstRemaining : Length - _position);
                using var input = _open(start, extent);
                int read = input.Read(buffer, offset, extent);
                if (read == 0)
                    throw new EndOfStreamException();
                _position += read;
                offset += read;
                count -= read;
                total += read;
            }
            return total;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            Position = checked((origin == SeekOrigin.Begin ? 0 : origin == SeekOrigin.Current ? Position : Length) + offset);
            return Position;
        }

        public override void Flush()
        {
            Check();
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
