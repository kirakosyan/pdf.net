using System.Collections.ObjectModel;

namespace Fluent.Pdf;

/// <summary>An immutable, detached page-model snapshot passed to writers.</summary>
/// <remarks>Snapshots remain valid after the source document changes or is disposed. They are not PDF bytes.</remarks>
public sealed class PdfDocumentSnapshot
{
    internal PdfDocumentSnapshot(PdfOptions options, PdfPageSnapshot[] pages,PdfSyntaxDocument? source=null,IReadOnlyList<PdfObjectId>? originalOrder=null,PdfWriteOptions? writeOptions=null)
    {
        Options = options;
        Pages = new ReadOnlyCollection<PdfPageSnapshot>(pages);
        SourceDocument=source;OriginalOrder=originalOrder??System.Array.Empty<PdfObjectId>();WriteOptions=writeOptions??PdfWriteOptions.Default;
    }

    /// <summary>Gets the immutable options captured from the source session.</summary>
    public PdfOptions Options { get; }
    /// <summary>Gets immutable pages in document order.</summary>
    public IReadOnlyList<PdfPageSnapshot> Pages { get; }
    /// <summary>Gets the immutable original graph, or null for a newly authored document.</summary>
    public PdfSyntaxDocument? SourceDocument{get;}
    /// <summary>Gets the immutable save choices captured for this writer invocation.</summary>
    public PdfWriteOptions WriteOptions{get;}
    internal IReadOnlyList<PdfObjectId> OriginalOrder{get;}
}

/// <summary>A detached, immutable page geometry value.</summary>
public readonly struct PdfPageSnapshot
{
    internal PdfPageSnapshot(PdfPageSize size, PdfPageRotation rotation,PdfPageSource? source=null,PdfContentProgram? content=null) { Size = size; Rotation = rotation;Source=source;Content=content; }
    /// <summary>Gets the unrotated physical size.</summary>
    public PdfPageSize Size { get; }
    /// <summary>Gets the clockwise display rotation.</summary>
    public PdfPageRotation Rotation { get; }
    /// <summary>Gets the immutable original page binding, or null for a newly authored page.</summary>
    public PdfPageSource? Source{get;}
    /// <summary>Gets immutable replacement content, or null to retain original bytes.</summary>
    public PdfContentProgram? Content{get;}
}
