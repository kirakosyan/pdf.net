using System.IO;
using System.Threading;

namespace Fluent.Pdf;

/// <summary>The document creation/opening facade. No provider is discovered or downloaded implicitly.</summary>
public static class Pdf
{
    /// <summary>Creates an empty in-memory editing session with immutable defaults and explicit providers.</summary>
    public static PdfDocument Create(PdfOptions? options = null, PdfProviderRegistry? providers = null) =>
        new(options ?? PdfOptions.Default, providers ?? PdfProviderRegistry.Empty);

    /// <summary>Reads eagerly from a borrowed readable stream, leaving it open at the position reached by the provider.</summary>
    /// <remarks>On failure the partially populated document is disposed. No parser is built into the current contract layer.</remarks>
    public static PdfDocument Open(Stream input, PdfOptions? options = null, PdfProviderRegistry? providers = null, CancellationToken cancellationToken = default)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        if (!input.CanRead) throw new ArgumentException("A readable stream is required.", nameof(input));
        cancellationToken.ThrowIfCancellationRequested();
        var registry = providers ?? PdfProviderRegistry.Empty;
        var reader = registry.GetRequired<IPdfDocumentReader>(PdfCapability.ReadDocument);
        var document = Create(options, registry);
        try { document.Read(reader, input, cancellationToken); return document; }
        catch { document.Dispose(); throw; }
    }

    /// <summary>Opens a file with an explicit reader; the SDK-owned file handle is closed before return.</summary>
    public static PdfDocument OpenFile(string path, PdfOptions? options = null, PdfProviderRegistry? providers = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(path, nameof(path), 32767);
        cancellationToken.ThrowIfCancellationRequested();
        var registry = providers ?? PdfProviderRegistry.Empty;
        registry.GetRequired<IPdfDocumentReader>(PdfCapability.ReadDocument);
        using var input = File.OpenRead(path);
        return Open(input, options, registry, cancellationToken);
    }
}
