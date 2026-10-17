using System.Collections.ObjectModel;

namespace Fluent.Pdf;

/// <summary>An immutable, detached page-model snapshot passed to writers.</summary>
/// <remarks>Snapshots remain valid after the source document changes or is disposed. They are not PDF bytes.</remarks>
public sealed class PdfDocumentSnapshot
{
    internal PdfDocumentSnapshot(PdfOptions options, PdfPageSnapshot[] pages)
    {
        Options = options;
        Pages = new ReadOnlyCollection<PdfPageSnapshot>(pages);
    }

    /// <summary>Gets the immutable options captured from the source session.</summary>
    public PdfOptions Options { get; }
    /// <summary>Gets immutable pages in document order.</summary>
    public IReadOnlyList<PdfPageSnapshot> Pages { get; }
}

/// <summary>A detached, immutable page geometry value.</summary>
public readonly struct PdfPageSnapshot
{
    internal PdfPageSnapshot(PdfPageSize size, PdfPageRotation rotation) { Size = size; Rotation = rotation; }
    /// <summary>Gets the unrotated physical size.</summary>
    public PdfPageSize Size { get; }
    /// <summary>Gets the clockwise display rotation.</summary>
    public PdfPageRotation Rotation { get; }
}
