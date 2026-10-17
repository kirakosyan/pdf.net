using System.IO;

namespace Fluent.Pdf;
/// <summary>Immutable eagerly decoded data and its operation report; no caller handle or operation context is retained.</summary>
public sealed class PdfDecodedData
{
    private readonly PdfByteStore _bytes;
    internal PdfDecodedData(PdfByteStore bytes, PdfOperationReport report)
    {
        _bytes = bytes;
        OperationReport = report;
    }

    /// <summary>Gets the decoded byte count.</summary>
    public long Length => _bytes.Length;
    /// <summary>Gets the complete successful decode report.</summary>
    public PdfOperationReport OperationReport { get; }

    /// <summary>Opens an independent read-only view of the decoded snapshot.</summary>
    public Stream OpenRead() => _bytes.Open(0, _bytes.Length);
}
