using System.IO;

namespace Fluent.Pdf;

/// <summary>An expiring synchronous worker callback boundary owning one private document, input lease and shared operation budget.</summary>
/// <remarks>Use Save/SaveFile on this boundary. Live model references cannot be used on other threads or after completion.</remarks>
public sealed class PdfProcessingJob
{
    private readonly PdfDocument _document;
    private readonly PdfOperationContext _context;
    private readonly MemoryStream _storage;
    private readonly BorrowedStream _input;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private IDisposable? _model;
    private bool _active = true;
    private bool _read;
    internal PdfProcessingJob(byte[] input, PdfOptions options, PdfProviderRegistry providers, PdfOperationContext context)
    {
        _context = context;
        context.Consume(PdfResourceKind.ExpandedBytes, 512);
        _document = Pdf.Create(options, providers);
        _document.BindProcessingThread();
        _storage = new MemoryStream(input, false);
        BorrowedStream? lease = null;
        try { _input = lease = new BorrowedStream(_storage, true, context); _model = _document.UseModelContext(context); }
        catch { lease?.Dispose(); _storage.Dispose(); _document.Dispose(); throw; }
    }
    /// <summary>Gets the job's private mutable document, valid only on the callback thread.</summary>
    public PdfDocument Document { get { Check(); return _document; } }
    /// <summary>Gets the expiring read-only input lease. Input reads charge the shared operation budget.</summary>
    public Stream Input { get { Check(); return _input; } }
    /// <summary>Gets the shared context required by optional providers and bounded work.</summary>
    public PdfOperationContext Context { get { Check(); return _context; } }
    /// <summary>Reads the input once into the empty private document through its explicit registered reader.</summary>
    public void ReadDocument()
    {
        Check();
        if (_read || _document.Pages.Count != 0 || _document.SourceDocument != null)
            throw new InvalidOperationException("Read input only once, before editing the private document.");
        _read = true;
        var reader = _document.Providers.GetRequired<IPdfDocumentReader>(PdfCapability.ReadDocument);
        if (reader.RequiresSeekableStream && !_input.CanSeek) throw new PdfException(new PdfDiagnostic("Stream.SeekRequired", "The reader requires seekable input."));
        reader.Read(_input, _document, _context);
        _context.Checkpoint();
    }
    /// <summary>Writes using the shared budget, preserving caller stream ownership. Stream output can be partial on failure.</summary>
    public void Save(Stream output, PdfWriteOptions? writeOptions = null)
    {
        Check();
        if (output == null) throw new ArgumentNullException(nameof(output));
        if (!output.CanWrite) throw new ArgumentException("A writable stream is required.", nameof(output));
        _document.SuspendModelContext(() => _document.WriteWithinContext(_document.Providers.GetRequired<IPdfDocumentWriter>(PdfCapability.WriteDocument), output, _context, writeOptions ?? PdfWriteOptions.Default));
    }
    /// <summary>Atomically writes a file using the shared budget. Failed/canceled writes preserve any prior destination.</summary>
    public void SaveFile(string path, PdfWriteOptions? writeOptions = null)
    {
        Check();
        Guard.Text(path, nameof(path), 32767);
        _document.SuspendModelContext(() => PdfAtomicFile.Write(path, _context, target => _document.WriteWithinContext(_document.Providers.GetRequired<IPdfDocumentWriter>(PdfCapability.WriteDocument), target, _context, writeOptions ?? PdfWriteOptions.Default)));
    }
    private void Check()
    {
        if (!_active) throw new ObjectDisposedException(nameof(PdfProcessingJob));
        if (_thread != Environment.CurrentManagedThreadId) throw new InvalidOperationException("The processing callback owns this job's model and input.");
        _context.Checkpoint();
    }
    internal void Close()
    {
        _active = false;
        _model?.Dispose(); _model = null;
        _input.Dispose(); _storage.Dispose(); _document.Dispose();
    }
}
