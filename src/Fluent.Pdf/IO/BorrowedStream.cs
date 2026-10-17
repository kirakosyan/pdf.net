using System.IO;

namespace Fluent.Pdf;

internal sealed class BorrowedStream : Stream
{
    private readonly Stream _inner;
    private readonly bool _reading;
    private readonly PdfOperationContext _context;
    private readonly long _origin;
    private readonly PdfResourceKind _inputResource;
    private bool _expired;

    internal BorrowedStream(Stream inner, bool reading, PdfOperationContext context, PdfResourceKind inputResource = PdfResourceKind.InputBytes)
    {
        _inner = inner;
        _reading = reading;
        _context = context;
        _inputResource = inputResource;
        context.Checkpoint();
        _origin = inner.CanSeek ? inner.Position : 0;
        if (_origin < 0) throw new IOException("The stream returned an invalid position.");
        if (reading && inner.CanSeek)
        {
            long available = Math.Max(0, inner.Length - _origin);
            if (available > context.Policy.Limits.GetLimit(inputResource)) context.Consume(inputResource, available);
        }
    }

    public override bool CanRead => !_expired && _context.IsActive && _reading && _inner.CanRead;
    public override bool CanWrite => !_expired && _context.IsActive && !_reading && _inner.CanWrite;
    public override bool CanSeek => !_expired && _context.IsActive && _inner.CanSeek;
    public override long Length { get { EnsureActive(); return _inner.Length; } }
    public override long Position
    {
        get { EnsureActive(); return _inner.Position; }
        set
        {
            EnsureActive();
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (!_reading) _context.CheckOutputRange(_origin, value, 0);
            _inner.Position = value;
            _context.Checkpoint();
        }
    }

    public override void Flush() { EnsureActive(); if (!_reading) _inner.Flush(); _context.Checkpoint(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        EnsureActive();
        if (!_reading) throw new NotSupportedException("The lease is write-only.");
        ValidateBuffer(buffer, offset, count);
        return _context.ReadBounded(_inner, buffer, offset, count, _inputResource);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureActive();
        if (_reading) throw new NotSupportedException("The lease is read-only.");
        ValidateBuffer(buffer, offset, count);
        if (_inner.CanSeek) _context.CheckOutputRange(_origin, _inner.Position, count);
        _context.Consume(PdfResourceKind.OutputBytes, count);
        _inner.Write(buffer, offset, count);
        _context.Checkpoint();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        EnsureActive();
        if (_reading) { long result = _inner.Seek(offset, origin); _context.Checkpoint(); return result; }
        if (!_inner.CanSeek) throw new NotSupportedException("The stream cannot seek.");
        long basis = origin switch
        {
            SeekOrigin.Begin => 0,
            SeekOrigin.Current => _inner.Position,
            SeekOrigin.End => _inner.Length,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        long destination;
        try { destination = checked(basis + offset); }
        catch (OverflowException) { _context.RejectLimit(PdfResourceKind.OutputBytes); throw; }
        _context.CheckOutputRange(_origin, destination, 0);
        _inner.Position = destination;
        return destination;
    }

    public override void SetLength(long value)
    {
        EnsureActive();
        if (_reading) throw new NotSupportedException("The lease is read-only.");
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        _context.CheckOutputRange(_origin, value, 0);
        long growth = Math.Max(0, value - _inner.Length);
        _context.Consume(PdfResourceKind.OutputBytes, growth);
        _inner.SetLength(value);
        _context.Checkpoint();
    }

    protected override void Dispose(bool disposing) { _expired = true; base.Dispose(disposing); }
    private void EnsureActive() { if (_expired) throw new ObjectDisposedException(nameof(BorrowedStream)); _context.Checkpoint(); }

    private static void ValidateBuffer(byte[] buffer, int offset, int count)
    {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (offset > buffer.Length - count) throw new ArgumentException("The requested buffer range is invalid.");
    }
}
