using System.IO;

namespace Fluent.Pdf;
// The owned XML profile has the same finite extent ceiling in both directions.
internal sealed class PdfXfdfOutput : Stream
{
    private readonly Stream _inner;
    private readonly PdfOperationContext _context;
    private long _length;
    internal PdfXfdfOutput(Stream inner, PdfOperationContext context)
    {
        _inner = inner;
        _context = context;
    }

    public override bool CanRead => false;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => _length;
    public override long Position { get => _length; set => throw new NotSupportedException(); }

    public override void Flush()
    {
        _context.Checkpoint();
        _inner.Flush();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _context.Checkpoint();
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset > buffer.Length - count)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count > 16 * 1024 * 1024 - _length)
            throw PdfWriterCore.Error(_context, "Annotation.XfdfLength", "The owned XFDF profile is limited to sixteen MiB of XML in both directions.");
        _inner.Write(buffer, offset, count);
        _length += count;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
