using System.Security.Cryptography;

namespace Fluent.Pdf.CommandLine;
// Private process-owned staging. Output cannot reach a destination before processing succeeds.
internal sealed class BoundedMemory(int maximum) : MemoryStream
{
    internal long Remaining => maximum - Length;

    private void Check(long count)
    {
        if (count < 0 || Position > maximum - count)
            throw new CliFailure("CLI.OutputLimit", 4);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Check(count);
        base.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Check(buffer.Length);
        base.Write(buffer);
    }

    public override void WriteByte(byte value)
    {
        Check(1);
        base.WriteByte(value);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
    {
        Check(count);
        return base.WriteAsync(buffer, offset, count, token);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
    {
        Check(buffer.Length);
        return base.WriteAsync(buffer, token);
    }

    public override void SetLength(long value)
    {
        if (value < 0 || value > maximum)
            throw new CliFailure("CLI.OutputLimit", 4);
        base.SetLength(value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && TryGetBuffer(out var buffer))
            CryptographicOperations.ZeroMemory(buffer.Array!);
        base.Dispose(disposing);
    }
}
