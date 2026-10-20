using System.IO;

namespace Fluent.Pdf;
// Immutable segmented input snapshots keep long offsets without retaining caller streams.
internal sealed class PdfByteStore
{
    private const int BlockSize = 4096;
    private readonly byte[][] _blocks;
    internal long Length { get; }

    private PdfByteStore(byte[][] blocks, long length)
    {
        _blocks = blocks;
        Length = length;
    }

    internal static PdfByteStore TakeBlocks(List<byte[]> blocks, long length) => new(blocks.ToArray(), length);
    internal static PdfByteStore Read(Stream input, PdfOperationContext context)
    {
        var blocks = new List<byte[]>();
        long length = 0;
        while (true)
        {
            context.Checkpoint();
            int first = input.ReadByte();
            if (first < 0)
                break;
            context.Consume(PdfResourceKind.ExpandedBytes, BlockSize);
            var block = new byte[BlockSize];
            block[0] = (byte)first;
            int count = 1;
            while (count < block.Length)
            {
                context.Checkpoint();
                int n = input.Read(block, count, block.Length - count);
                if (n == 0)
                    break;
                count += n;
            }

            blocks.Add(block);
            length = checked(length + count);
            if (count < block.Length)
                break;
        }

        return new(blocks.ToArray(), length);
    }

    internal int At(long position) => position < 0 || position >= Length ? -1 : _blocks[(int)(position / BlockSize)][(int)(position % BlockSize)];
    internal Stream Open(long start, long length)
    {
        if (start < 0 || length < 0 || start > Length - length)
            throw new ArgumentOutOfRangeException(nameof(start));
        return new View(this, start, length);
    }

    private sealed class View : Stream
    {
        private readonly PdfByteStore _owner;
        private readonly long _start, _length;
        private long _position;
        private bool _disposed;
        internal View(PdfByteStore owner, long start, long length)
        {
            _owner = owner;
            _start = start;
            _length = length;
        }

        public override bool CanRead => !_disposed;
        public override bool CanSeek => !_disposed;
        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                Check();
                return _length;
            }
        }

        public override long Position
        {
            get
            {
                Check();
                return _position;
            }

            set
            {
                Check();
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Check();
            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset > buffer.Length - count)
                throw new ArgumentOutOfRangeException(nameof(offset));
            int size = (int)Math.Min(count, Math.Max(0, _length - _position));
            int copied = 0;
            while (copied < size)
            {
                long source = _start + _position + copied;
                int inBlock = (int)(source % BlockSize);
                int extent = Math.Min(size - copied, BlockSize - inBlock);
                Buffer.BlockCopy(_owner._blocks[(int)(source / BlockSize)], inBlock, buffer, offset + copied, extent);
                copied += extent;
            }
            _position += size;
            return size;
        }

        public override int ReadByte()
        {
            Check();
            return _position >= _length ? -1 : _owner.At(_start + _position++);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            Check();
            long start = origin == SeekOrigin.Begin ? 0 : origin == SeekOrigin.Current ? _position : origin == SeekOrigin.End ? _length : throw new ArgumentOutOfRangeException(nameof(origin));
            Position = checked(start + offset);
            return _position;
        }

        public override void Flush() => Check();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            _disposed = true;
            base.Dispose(disposing);
        }

        private void Check()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(View));
        }
    }
}

internal sealed class PdfByteBuilder
{
    private readonly PdfOperationContext _context;
    private readonly List<byte[]> _blocks = new();
    internal long Length { get; private set; }

    internal PdfByteBuilder(PdfOperationContext context) => _context = context;
    internal void Add(byte value)
    {
        if (Length % 4096 == 0)
        {
            _context.Checkpoint();
            _context.Consume(PdfResourceKind.ExpandedBytes, 4096);
            _blocks.Add(new byte[4096]);
        }

        _blocks[_blocks.Count - 1][(int)(Length % 4096)] = value;
        Length++;
    }

    internal PdfByteStore Build() => PdfByteStore.TakeBlocks(_blocks, Length);
}
