using System.IO;

namespace Fluent.Pdf;

internal sealed class BorrowedStream : Stream
{
    private readonly Stream _inner;
    private readonly bool _reading;
    private bool _expired;

    internal BorrowedStream(Stream inner, bool reading) { _inner = inner; _reading = reading; }

    public override bool CanRead => !_expired && _reading && _inner.CanRead;
    public override bool CanWrite => !_expired && !_reading && _inner.CanWrite;
    public override bool CanSeek => !_expired && _inner.CanSeek;
    public override long Length { get { EnsureActive(); return _inner.Length; } }
    public override long Position { get { EnsureActive(); return _inner.Position; } set { EnsureActive(); _inner.Position = value; } }
    public override void Flush() { EnsureActive(); if (!_reading) _inner.Flush(); }
    public override int Read(byte[] buffer, int offset, int count) { EnsureActive(); if (!_reading) throw new NotSupportedException("The lease is write-only."); return _inner.Read(buffer, offset, count); }
    public override void Write(byte[] buffer, int offset, int count) { EnsureActive(); if (_reading) throw new NotSupportedException("The lease is read-only."); _inner.Write(buffer, offset, count); }
    public override long Seek(long offset, SeekOrigin origin) { EnsureActive(); return _inner.Seek(offset, origin); }
    public override void SetLength(long value) { EnsureActive(); if (_reading) throw new NotSupportedException("The lease is read-only."); _inner.SetLength(value); }
    protected override void Dispose(bool disposing) { _expired = true; base.Dispose(disposing); }
    private void EnsureActive() { if (_expired) throw new ObjectDisposedException(nameof(BorrowedStream)); }
}
