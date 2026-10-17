using System.IO;
using System.Threading;

namespace Fluent.Pdf;

/// <summary>An owned in-memory editing session with explicit provider-backed I/O.</summary>
/// <remarks>Single-document mutation and I/O must be serialized by the caller. Provider instances remain caller-owned.</remarks>
public sealed class PdfDocument : IDisposable
{
    private readonly PdfOptions _options;
    private readonly PdfProviderRegistry _providers;
    private readonly PdfPageCollection _pages;
    private bool _disposed;
    private bool _operationActive;
    private bool _writing;

    internal PdfDocument(PdfOptions options, PdfProviderRegistry providers)
    {
        _options = options;
        _providers = providers;
        _pages = new PdfPageCollection(this);
    }

    /// <summary>Gets whether this session was disposed. This property remains usable after disposal.</summary>
    public bool IsDisposed => _disposed;
    /// <summary>Gets the immutable options captured for this session.</summary>
    public PdfOptions Options { get { EnsureAlive(); return _options; } }
    /// <summary>Gets the explicitly configured immutable provider registry.</summary>
    public PdfProviderRegistry Providers { get { EnsureAlive(); return _providers; } }
    /// <summary>Gets the zero-based editable page model.</summary>
    public PdfPageCollection Pages { get { EnsureAlive(); return _pages; } }

    /// <summary>Adds a default-sized page and returns this document for fluent composition.</summary>
    public PdfDocument AddPage() { _pages.Add(); return this; }
    /// <summary>Adds a page and returns this document for fluent composition.</summary>
    public PdfDocument AddPage(PdfPageSize size) { _pages.Add(size); return this; }

    /// <summary>Captures immutable page geometry independent of subsequent edits and disposal.</summary>
    public PdfDocumentSnapshot CreateSnapshot()
    {
        EnsureAlive();
        var pages = new PdfPageSnapshot[_pages.Count];
        for (int i = 0; i < pages.Length; i++) pages[i] = new PdfPageSnapshot(_pages[i].Size, _pages[i].Rotation);
        return new PdfDocumentSnapshot(_options, pages);
    }

    /// <summary>Writes through the registered writer at the current stream position, leaving the caller's stream open.</summary>
    /// <remarks>Stream output can be partial on provider failure/cancellation. The SDK does not rewind or truncate the caller's stream.</remarks>
    public void Save(Stream output, CancellationToken cancellationToken = default)
    {
        EnsureAlive();
        EnsureNoActiveOperation();
        if (output is null) throw new ArgumentNullException(nameof(output));
        if (!output.CanWrite) throw new ArgumentException("A writable stream is required.", nameof(output));
        cancellationToken.ThrowIfCancellationRequested();
        var writer = _providers.GetRequired<IPdfDocumentWriter>(PdfCapability.WriteDocument);
        Write(writer, output, cancellationToken);
    }

    /// <summary>Writes to a private file in the destination directory, then atomically moves/replaces the destination on success.</summary>
    /// <remarks>The destination directory must exist. An unsupported atomic replace fails without a delete-and-copy fallback.</remarks>
    public void SaveFile(string path, CancellationToken cancellationToken = default)
    {
        EnsureAlive();
        EnsureNoActiveOperation();
        Guard.Text(path, nameof(path), 32767);
        cancellationToken.ThrowIfCancellationRequested();
        var writer = _providers.GetRequired<IPdfDocumentWriter>(PdfCapability.WriteDocument);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        var temporary = Path.Combine(directory, ".fluent-pdf-" + Guid.NewGuid().ToString("N") + ".tmp");
        bool created = false;
        Exception? failure = null;
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                Write(writer, output, cancellationToken);
                output.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
            else File.Move(temporary, fullPath);
        }
        catch (Exception ex) { failure = ex; throw; }
        finally
        {
            if (created)
            {
                try { File.Delete(temporary); }
                catch (Exception cleanup) when (failure is not null && (cleanup is IOException || cleanup is UnauthorizedAccessException))
                {
                    failure.Data["Fluent.Pdf.CleanupFailure"] = cleanup;
                }
            }
        }
    }

    /// <summary>Disposes this session and invalidates live page/collection references. Repeated disposal is harmless.</summary>
    /// <remarks>Disposal during a provider callback is rejected. Caller streams and provider instances are never disposed.</remarks>
    public void Dispose()
    {
        if (_disposed) return;
        if (_operationActive) throw new InvalidOperationException("Cannot dispose a document during a provider operation.");
        _pages.ClearCore();
        _disposed = true;
    }

    internal void Read(IPdfDocumentReader reader, Stream input, CancellationToken cancellationToken)
    {
        if (reader.RequiresSeekableStream && !input.CanSeek) throw SeekRequired(PdfCapability.ReadDocument);
        BeginOperation(false);
        try
        {
            using var lease = new BorrowedStream(input, true);
            reader.Read(lease, this, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally { EndOperation(); }
    }

    private void Write(IPdfDocumentWriter writer, Stream output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (writer.RequiresSeekableStream && !output.CanSeek) throw SeekRequired(PdfCapability.WriteDocument);
        BeginOperation(true);
        try
        {
            var snapshot = CreateSnapshot();
            using var lease = new BorrowedStream(output, false);
            writer.Write(snapshot, lease, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally { EndOperation(); }
    }

    private static PdfException SeekRequired(PdfCapability capability) =>
        new(new PdfDiagnostic("Stream.SeekRequired", "The configured provider requires a seekable stream.", capability));

    private void BeginOperation(bool writing)
    {
        EnsureAlive();
        EnsureNoActiveOperation();
        _operationActive = true;
        _writing = writing;
    }

    private void EndOperation() { _writing = false; _operationActive = false; }

    private void EnsureNoActiveOperation()
    {
        if (_operationActive) throw new PdfException(new PdfDiagnostic("Operation.Reentrant", "Nested I/O on the same document is not supported."));
    }

    internal void EnsureAlive() { if (_disposed) throw new ObjectDisposedException(nameof(PdfDocument)); }
    internal void EnsureEditable()
    {
        EnsureAlive();
        if (_writing) throw new InvalidOperationException("The document cannot be edited during writing.");
    }
}
