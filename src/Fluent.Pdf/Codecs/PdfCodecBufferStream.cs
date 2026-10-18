using System.IO;

namespace Fluent.Pdf;
internal sealed class PdfCodecBufferStream : Stream
{
    private readonly PdfByteBuilder _bytes;
    private readonly PdfOperationContext _context;
    private readonly long _limit;
    private bool _disposed;
    internal PdfCodecBufferStream(PdfOperationContext context, long limit)
    {
        _context = context;
        _limit = limit;
        _bytes = new(context);
    }

    internal long BufferedLength => _bytes.Length;

    internal PdfByteStore Finish() => _bytes.Build();
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => !_disposed;

    public override long Length
    {
        get
        {
            Check();
            return _bytes.Length;
        }
    }

    public override long Position
    {
        get
        {
            Check();
            return _bytes.Length;
        }

        set => throw new NotSupportedException();
    }

    public override void Flush() => Check();
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (buffer is null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset > buffer.Length - count)
            throw new ArgumentOutOfRangeException(nameof(offset));
        Reserve(count);
        for (int i = 0; i < count; i++)
            _bytes.Add(buffer[offset + i]);
        _context.Checkpoint();
    }

    public override void WriteByte(byte value)
    {
        Reserve(1);
        _bytes.Add(value);
    }

    private void Reserve(int count)
    {
        Check();
        if (count > _limit - _bytes.Length)
            throw PdfStreamFilters.Error(_context, "Codec.OutputLength", "The image codec exceeded its declared output extent.");
    }

    private void Check()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PdfCodecBufferStream));
        _context.Checkpoint();
    }

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        base.Dispose(disposing);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
