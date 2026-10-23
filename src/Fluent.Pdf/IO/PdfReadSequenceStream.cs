using System.IO;

namespace Fluent.Pdf;
// A forward-only view over already bounded inputs. Ownership is selected explicitly.
internal sealed class PdfReadSequenceStream : Stream
{
    private readonly Stream[] _streams;
    private readonly bool _leaveOpen;
    private int _current;
    private bool _disposed;
    internal PdfReadSequenceStream(bool leaveOpen, params Stream[] streams)
    {
        _leaveOpen = leaveOpen;
        _streams = streams;
    }

    public override bool CanRead => !_disposed;
    public override bool CanWrite => false;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PdfReadSequenceStream));
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset > buffer.Length - count)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count == 0)
            return 0;
        while (_current < _streams.Length)
        {
            int n = _streams[_current].Read(buffer, offset, count);
            if (n != 0)
                return n;
            _current++;
        }

        return 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing && !_leaveOpen)
            foreach (var stream in _streams)
                stream.Dispose();
        _disposed = true;
        base.Dispose(disposing);
    }

    public override void Flush()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PdfReadSequenceStream));
    }

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
