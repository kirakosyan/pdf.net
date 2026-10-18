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
    private PdfOperationContext? _context;
    private PdfOperationReport? _lastReport;
    private PdfSyntaxDocument? _source;
    private IReadOnlyList<PdfObjectId> _originalOrder=Array.Empty<PdfObjectId>();

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
    /// <summary>Gets the last completed provider operation report, including failed operations. Remains readable after disposal.</summary>
    public PdfOperationReport? LastOperationReport => _lastReport;
    /// <summary>Gets the immutable original object graph, or null for a newly authored session.</summary>
    public PdfSyntaxDocument? SourceDocument{get{EnsureAlive();return _source;}}

    /// <summary>Adds a default-sized page and returns this document for fluent composition.</summary>
    public PdfDocument AddPage() { _pages.Add(); return this; }
    /// <summary>Adds a page and returns this document for fluent composition.</summary>
    public PdfDocument AddPage(PdfPageSize size) { _pages.Add(size); return this; }

    /// <summary>Captures immutable page geometry independent of subsequent edits and disposal.</summary>
    public PdfDocumentSnapshot CreateSnapshot()
        =>CreateSnapshotCore(PdfWriteOptions.Default);
    private PdfDocumentSnapshot CreateSnapshotCore(PdfWriteOptions writeOptions)
    {
        EnsureAlive();
        int count=_pages.Count;
        if(count>0){_context?.Consume(PdfResourceKind.Objects,count);_context?.Consume(PdfResourceKind.ExpandedBytes,count*128L+32);}
        var pages = count==0?Array.Empty<PdfPageSnapshot>():new PdfPageSnapshot[count];
        for (int i = 0; i < pages.Length; i++) pages[i] = new PdfPageSnapshot(_pages[i].Size, _pages[i].Rotation,_pages[i].Source,_pages[i].Content,_pages[i].SnapshotFonts(_context),_pages[i].SnapshotGraphicsResources(_context));
        return new PdfDocumentSnapshot(_options, pages,_source,_originalOrder,writeOptions);
    }

    /// <summary>Writes through the registered writer at the current stream position, leaving the caller's stream open.</summary>
    /// <remarks>Stream output can be partial on provider failure/cancellation. The SDK does not rewind or truncate the caller's stream.</remarks>
    public PdfOperationReport Save(Stream output, CancellationToken cancellationToken = default,PdfWriteOptions? writeOptions=null)
    {
        EnsureAlive();
        EnsureNoActiveOperation();
        if (output is null) throw new ArgumentNullException(nameof(output));
        if (!output.CanWrite) throw new ArgumentException("A writable stream is required.", nameof(output));
        cancellationToken.ThrowIfCancellationRequested();
        var writer = _providers.GetRequired<IPdfDocumentWriter>(PdfCapability.WriteDocument);
        return RunOperation(true, "Save", PdfCapability.WriteDocument, cancellationToken, context =>
        {
            Write(writer, output, context,writeOptions??PdfWriteOptions.Default);
            context.EnsureComplete();
        });
    }

    /// <summary>Writes to a private file in the destination directory, then atomically moves/replaces the destination on success.</summary>
    /// <remarks>The destination directory must exist. An unsupported atomic replace fails without a delete-and-copy fallback.</remarks>
    public PdfOperationReport SaveFile(string path, CancellationToken cancellationToken = default,PdfWriteOptions? writeOptions=null)
    {
        EnsureAlive();
        EnsureNoActiveOperation();
        Guard.Text(path, nameof(path), 32767);
        cancellationToken.ThrowIfCancellationRequested();
        var writer = _providers.GetRequired<IPdfDocumentWriter>(PdfCapability.WriteDocument);
        return RunOperation(true, "SaveFile", PdfCapability.WriteDocument, cancellationToken, context =>
            PdfAtomicFile.Write(path,context,output=>Write(writer,output,context,writeOptions??PdfWriteOptions.Default)));
    }

    /// <summary>Disposes this session and invalidates live page/collection references. Repeated disposal is harmless.</summary>
    /// <remarks>Disposal during a provider callback is rejected. Caller streams and provider instances are never disposed.</remarks>
    public void Dispose()
    {
        if (_disposed) return;
        if (_operationActive) throw new InvalidOperationException("Cannot dispose a document during a provider operation.");
        _pages.ClearCore();
        _source=null;_originalOrder=Array.Empty<PdfObjectId>();
        _disposed = true;
    }

    internal void Read(IPdfDocumentReader reader, Stream input, CancellationToken cancellationToken)
    {
        if (reader.RequiresSeekableStream && !input.CanSeek) throw SeekRequired(PdfCapability.ReadDocument);
        RunOperation(false, "Open", PdfCapability.ReadDocument, cancellationToken, context =>
        {
            ReadCore(reader, input, context);
            context.EnsureComplete();
        });
    }

    internal void ReadFile(IPdfDocumentReader reader, string path, CancellationToken cancellationToken)
    {
        RunOperation(false, "OpenFile", PdfCapability.ReadDocument, cancellationToken, context =>
        {
            context.Checkpoint();
            using (var input = File.OpenRead(path)) ReadCore(reader, input, context);
            context.EnsureComplete();
        });
    }

    private void ReadCore(IPdfDocumentReader reader, Stream input, PdfOperationContext context)
    {
        using var lease = new BorrowedStream(input, true, context);
        reader.Read(lease, this, context);
        context.Checkpoint();
    }

    private void Write(IPdfDocumentWriter writer, Stream output, PdfOperationContext context,PdfWriteOptions writeOptions)
    {
        context.Checkpoint();
        if (writer.RequiresSeekableStream && !output.CanSeek) throw SeekRequired(PdfCapability.WriteDocument);
        if(writer is IPdfRevisionWriter revisions)
        {if(!revisions.Supports(writeOptions))throw PdfWriterCore.Error(context,"Writer.OptionsUnsupported","The selected writer does not support the requested save choices.");}
        else if(!writeOptions.IsDefault)throw PdfWriterCore.Error(context,"Writer.OptionsUnsupported","The selected writer does not implement revision-aware save choices.");
        var snapshot = CreateSnapshotCore(writeOptions);
        using var lease = new BorrowedStream(output, false, context);
        writer.Write(snapshot, lease, context);
        context.Checkpoint();
    }

    private PdfOperationReport RunOperation(bool writing, string operation, PdfCapability capability,
        CancellationToken token, Action<PdfOperationContext> action,string? providerId=null)
    {
        BeginOperation(writing);
        PdfOperationContext? context = null;
        bool succeeded = false;
        try
        {
            context = new PdfOperationContext(operation, providerId??_providers.GetIdentifier(capability), capability, _options.OperationPolicy, token);
            _context = context;
            context.Checkpoint();
            action(context);
            succeeded = true;
        }
        catch (Exception failure)
        {
            // Reassert latched policy failures, cancellation and deadline even if a callback swallowed/replaced them.
            try { context?.Checkpoint(); }
            catch (Exception policyFailure)
            {
                if (!ReferenceEquals(failure, policyFailure)) policyFailure.Data["Fluent.Pdf.UnderlyingFailure"] = failure;
                throw;
            }
            throw;
        }
        finally
        {
            try { if (context is not null) _lastReport = context.Finish(succeeded); }
            finally { _context = null; EndOperation(); context?.DisposeSources(); }
        }
        return _lastReport!;
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
    internal void BeforePageAllocation(PdfOperationContext? context=null){var selected=context??_context;selected?.Consume(PdfResourceKind.Objects,1);selected?.Consume(PdfResourceKind.ExpandedBytes,256);}
    internal PdfContentProgram ReadPageContent(PdfPage page,PdfUnknownOperatorPolicy unknown,string? version,CancellationToken token)
    {
        EnsureNoActiveOperation();if(!Enum.IsDefined(typeof(PdfUnknownOperatorPolicy),unknown))throw new ArgumentOutOfRangeException(nameof(unknown));
        if(unknown==PdfUnknownOperatorPolicy.Preserve&&version==null)throw new ArgumentException("Preservation requires an explicit extension version floor.",nameof(version));
        if(version!=null)PdfVersionNumber.Parse(version);
        PdfContentProgram? result=null;
        var report=RunOperation(false,"ReadPageContent",PdfCapability.ReadDocument,token,context=>
        {
            if(page.Content!=null)result=PdfContentProgram.CreateCore(page.Content.Operations,PdfContentKind.Page,context);
            else result=PdfPageContent.Read(page.Source,unknown,version,_providers,context);
            context.EnsureComplete();
        },"managed.content");
        result!.OperationReport=report;return result;
    }
    internal IReadOnlyList<PdfPageFont> ReadPageFonts(PdfPage page,CancellationToken token)
    {
        EnsureNoActiveOperation();IReadOnlyList<PdfPageFont>? result=null;
        RunOperation(false,"ReadPageFonts",PdfCapability.Fonts,token,context=>
        {
            var replacements=page.SnapshotFonts(context);var fonts=new SortedDictionary<string,PdfPageFont>(StringComparer.Ordinal);var binding=page.Source;
            PdfValue Resolve(PdfValue value)
            {
                if(value.Kind!=PdfValueKind.Reference)return value;context.Consume(PdfResourceKind.ExpandedBytes,64);var seen=new HashSet<PdfObjectId>();int depth=0;
                while(value.Kind==PdfValueKind.Reference){context.Checkpoint();context.Consume(PdfResourceKind.Objects,1);context.Consume(PdfResourceKind.ExpandedBytes,64);if(++depth>context.Available(PdfResourceKind.Depth))context.RejectLimit(PdfResourceKind.Depth);if(!seen.Add(value.ReferenceId))throw PdfWriterCore.Error(context,"Font.ReferenceCycle","Font resource references contain a cycle.");value=binding!.Document.Resolve(value.ReferenceId);}return value;
            }
            if(binding!=null&&binding.EffectiveAttributes.Entries.TryGetValue("Resources",out var declared))
            {
                var resources=Resolve(declared);if(resources.Kind!=PdfValueKind.Dictionary)throw PdfWriterCore.Error(context,"Font.ResourceDictionary","Page Resources must resolve to a dictionary.");
                var dictionary=resources.Entries.TryGetValue("Font",out var value)?Resolve(value):PdfValue.Null;
                if(dictionary.Kind!=PdfValueKind.Null&&dictionary.Kind!=PdfValueKind.Dictionary)throw PdfWriterCore.Error(context,"Font.ResourceDictionary","Font resources must resolve to a dictionary.");
                if(dictionary.Kind==PdfValueKind.Dictionary)foreach(var pair in dictionary.Entries)
                {context.Checkpoint();context.Consume(PdfResourceKind.Objects,1);context.Consume(PdfResourceKind.ExpandedBytes,256);if(replacements.ContainsKey(pair.Key))continue;var font=Resolve(pair.Value);if(font.Kind!=PdfValueKind.Dictionary)throw PdfWriterCore.Error(context,"Font.SourceDictionary","A named font must resolve to a dictionary.");fonts.Add(pair.Key,new PdfPageFont(pair.Key,font,binding.Document,null,_providers,_options.OperationPolicy,context));}
            }
            foreach(var pair in replacements){context.Checkpoint();context.Consume(PdfResourceKind.Objects,1);context.Consume(PdfResourceKind.ExpandedBytes,256);fonts[pair.Key]=new PdfPageFont(pair.Key,null,null,pair.Value,_providers,_options.OperationPolicy,context);}
            context.Consume(PdfResourceKind.ExpandedBytes,fonts.Count*16L+32);result=new System.Collections.ObjectModel.ReadOnlyCollection<PdfPageFont>(fonts.Values.ToArray());context.EnsureComplete();
        },"core.fonts");return result!;
    }
    internal void AttachSource(PdfSyntaxDocument source,PdfOperationContext context)
    {
        EnsureEditable();context.Consume(PdfResourceKind.ExpandedBytes,_pages.Count*16L+32);
        var order=new PdfObjectId[_pages.Count];for(int i=0;i<order.Length;i++)order[i]=_pages[i].Source!.ObjectId;
        _originalOrder=order;_source=source;
    }
    internal PdfException PageLimit()
    {
        var diagnostic = new PdfDiagnostic("Limit.Pages", "The document's configured page limit was reached.");
        _context?.Report(diagnostic);
        return new PdfException(diagnostic);
    }
    internal void EnsureEditable()
    {
        EnsureAlive();
        _context?.Checkpoint();
        if (_writing) throw new InvalidOperationException("The document cannot be edited during writing.");
    }
}
