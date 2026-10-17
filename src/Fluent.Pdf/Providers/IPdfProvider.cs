using System.IO;

namespace Fluent.Pdf;

/// <summary>An explicitly configured, caller-owned provider. Documents never dispose shared providers.</summary>
public interface IPdfProvider
{
    /// <summary>Gets immutable registration metadata; the registry captures this value once.</summary>
    PdfProviderDescriptor Descriptor { get; }
}

/// <summary>Populates an empty document synchronously; implementations must honor cancellation and supplied options.</summary>
public interface IPdfDocumentReader : IPdfProvider
{
    /// <summary>Gets whether a seekable input stream is required.</summary>
    bool RequiresSeekableStream { get; }
    /// <summary>Reads from a borrowed read-only lease into the target; neither the lease nor target may be retained after return.</summary>
    /// <remarks>The input position may advance. Charge resources before allocation, report losses and poll context checkpoints. No implicit rewind or ownership transfer occurs.</remarks>
    void Read(Stream input, PdfDocument target, PdfOperationContext context);
}

/// <summary>Writes an immutable document snapshot synchronously.</summary>
public interface IPdfDocumentWriter : IPdfProvider
{
    /// <summary>Gets whether a seekable output stream is required.</summary>
    bool RequiresSeekableStream { get; }
    /// <summary>Writes at the current position of a borrowed write-only lease and honors cancellation.</summary>
    /// <remarks>Charge resources before allocation and report every loss through the context. The lease expires on return. Stream output may be partial on failure; file-path saves are committed atomically.</remarks>
    void Write(PdfDocumentSnapshot document, Stream output, PdfOperationContext context);
}
