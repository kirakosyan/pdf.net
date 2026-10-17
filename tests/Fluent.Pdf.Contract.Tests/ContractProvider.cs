using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf.Contract.Tests;

// A protocol fixture for lifetime and dispatch tests. This is not a PDF codec.
internal sealed class ContractProvider : IPdfDocumentReader, IPdfDocumentWriter, IDisposable
{
    public PdfProviderDescriptor Descriptor { get; } = new("test.contract", "1.0.0", PdfCapability.ReadDocument, PdfCapability.WriteDocument);
    public bool RequiresSeekableStream { get; set; }
    internal Action<Stream, PdfDocument, CancellationToken>? OnRead { get; set; }
    internal Action<PdfDocumentSnapshot, Stream, CancellationToken>? OnWrite { get; set; }
    internal bool Disposed { get; private set; }

    public void Read(Stream input, PdfDocument target, CancellationToken cancellationToken)
    {
        if (OnRead is not null) { OnRead(input, target, cancellationToken); return; }
        int count = input.ReadByte();
        if (count < 0) throw new InvalidDataException("The contract fixture requires one count byte.");
        for (int i = 0; i < count; i++) { cancellationToken.ThrowIfCancellationRequested(); target.Pages.Add(); }
    }

    public void Write(PdfDocumentSnapshot document, Stream output, CancellationToken cancellationToken)
    {
        if (OnWrite is not null) { OnWrite(document, output, cancellationToken); return; }
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = Encoding.UTF8.GetBytes("contract:" + document.Pages.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        output.Write(bytes, 0, bytes.Length);
    }

    public void Dispose() => Disposed = true;
}

internal sealed class ServiceProvider : IPdfProvider
{
    internal ServiceProvider(PdfProviderDescriptor descriptor) => Descriptor = descriptor;
    public PdfProviderDescriptor Descriptor { get; }
}

internal sealed class NonSeekableStream : Stream
{
    private readonly MemoryStream _inner = new();
    internal NonSeekableStream(byte[]? input = null) { if (input is not null) _inner.Write(input, 0, input.Length); _inner.Position = 0; }
    internal byte[] Bytes => _inner.ToArray();
    public override bool CanRead => _inner.CanRead;
    public override bool CanWrite => _inner.CanWrite;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => _inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
}
