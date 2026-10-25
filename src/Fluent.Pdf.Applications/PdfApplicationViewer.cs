using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fluent.Pdf.Applications;
/// <summary>Explicit application commands. Controls map local keyboard/button gestures without global hooks or document-directed actions.</summary>
public enum PdfApplicationViewCommand
{
    /// <summary>Next physical page, bounded at the final page.</summary>
    NextPage,
    /// <summary>Previous physical page, bounded at the first page.</summary>
    PreviousPage,
    /// <summary>First physical page.</summary>
    FirstPage,
    /// <summary>Last physical page.</summary>
    LastPage,
    /// <summary>Increase explicit zoom by 25 percent, capped at 4.</summary>
    ZoomIn,
    /// <summary>Decrease explicit zoom by 25 percent, capped at 0.25.</summary>
    ZoomOut,
    /// <summary>Restore 96 DPI display scale.</summary>
    ActualSize,
    /// <summary>Cancel only this controller's current request.</summary>
    Cancel
}

/// <summary>Immutable page/zoom/request state. Generation allows UI dispatchers to reject stale posted work.</summary>
public sealed class PdfApplicationViewState : EventArgs
{
    internal PdfApplicationViewState(int page, int count, double zoom, long generation, bool busy, string diagnostic)
    {
        PageIndex = page;
        PageCount = count;
        Zoom = zoom;
        Generation = generation;
        IsBusy = busy;
        DiagnosticCode = diagnostic;
    }

    /// <summary>Gets zero-based physical page.</summary>
    public int PageIndex { get; }
    /// <summary>Gets immutable source page count.</summary>
    public int PageCount { get; }
    /// <summary>Gets explicit scale relative to 96 DPI, within 0.25..4.</summary>
    public double Zoom { get; }
    /// <summary>Gets monotonic request/view generation.</summary>
    public long Generation { get; }
    /// <summary>Gets whether the current generation is rendering.</summary>
    public bool IsBusy { get; }
    /// <summary>Gets a stable diagnostic code only, without paths, source payloads or raw exception text.</summary>
    public string DiagnosticCode { get; }
}

/// <summary>Detached rendered page plus explicitly captured source-order text. This is not certified logical reading order or PDF/UA remediation.</summary>
public sealed class PdfApplicationFrame
{
    internal PdfApplicationFrame(PdfApplicationViewState state, PdfRaster raster, string text)
    {
        State = state;
        Raster = raster;
        CapturedText = text;
    }

    /// <summary>Gets the exact page/zoom/generation producing this frame.</summary>
    public PdfApplicationViewState State { get; }
    /// <summary>Gets immutable top-down BGRA pixels and exact displayed/native coordinate frame.</summary>
    public PdfRaster Raster { get; }
    /// <summary>Gets bounded raw mapped source text, excluding hidden layers/nonpainting text. Does not establish visible ink or author-confirmed semantics.</summary>
    public string CapturedText { get; }

    /// <summary>Encodes this detached raster as bounded PNG in a separate explicit encoding operation. Caller output remains borrowed and open; render and encoding reports have independent policies.</summary>
    public PdfOperationReport WritePng(Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (!output.CanWrite)
            throw new ArgumentException("A writable output is required.", nameof(output));
        return PdfFont.Run("EncodeApplicationFrame", "applications.view", policy, cancellationToken, c =>
        {
            using var source = Raster.OpenSamples();
            var bytes = PdfProfileIo.Capture(source, c, 67108864);
            using var lease = new BorrowedStream(output, false, c);
            PdfRasterExport.Png(lease, Raster.Request, bytes, null, c);
        }, PdfCapability.RenderPages);
    }
}

/// <summary>Optional toolkit-independent application controller over shared viewing primitives. Owns its private session/cache, never caller providers. No Reader, UI, network or automatic printer dependency.</summary>
public sealed class PdfApplicationViewer : IDisposable
{
    private readonly object _gate = new();
    private readonly PdfDocumentSnapshot _source;
    private readonly PdfViewSession _session;
    private readonly PdfOperationPolicy _policy;
    private readonly PdfProviderRegistry _providers;
    private readonly long _maximumSurfacePixels;
    private readonly int _maximumTextCharacters;
    private readonly bool _captureText;
    private PdfApplicationViewState _state;
    private CancellationTokenSource? _active;
    private bool _disposed;
    /// <summary>Captures one immutable source with explicit renderer, private bounded cache and surface/text ceilings. Independent render and captured-text operations each use the immutable policy. No fallback renderer/OCR is selected.</summary>
    public PdfApplicationViewer(PdfDocumentSnapshot source, PdfProviderRegistry providers, long maximumCachedBytes = 16777216, long maximumSurfacePixels = 16000000, int maximumTextCharacters = 100000, bool captureText = true, PdfOperationPolicy? policy = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        if (source.Pages.Count == 0)
            throw new ArgumentException("At least one source page is required.", nameof(source));
        if (maximumCachedBytes < 0 || maximumCachedBytes > 67108864)
            throw new ArgumentOutOfRangeException(nameof(maximumCachedBytes));
        if (maximumSurfacePixels < 1 || maximumSurfacePixels > 16000000)
            throw new ArgumentOutOfRangeException(nameof(maximumSurfacePixels));
        if (maximumTextCharacters < 1 || maximumTextCharacters > 1000000)
            throw new ArgumentOutOfRangeException(nameof(maximumTextCharacters));
        _policy = policy ?? source.Options.OperationPolicy;
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _session = new(source, providers ?? throw new ArgumentNullException(nameof(providers)), maximumCachedBytes, maximumOutstandingRequests: 4, policy: policy);
        _maximumSurfacePixels = maximumSurfacePixels;
        _maximumTextCharacters = maximumTextCharacters;
        _captureText = captureText;
        _state = new(0, source.Pages.Count, 1, 0, false, "");
    }

    /// <summary>Raised outside locks on the invoking/completion thread. Hosts marshal to their own dispatcher and compare generation. Subscriber exceptions are the host's responsibility.</summary>
    public event EventHandler<PdfApplicationViewState>? StateChanged;
    /// <summary>Gets an immutable synchronized state snapshot.</summary>
    public PdfApplicationViewState State
    {
        get
        {
            lock (_gate)
            {
                Alive();
                return _state;
            }
        }
    }

    /// <summary>Gets this controller's retained raster-cache bytes, excluding caller frames and in-flight operations.</summary>
    public long CachedBytes => _session.CachedBytes;

    /// <summary>Applies a bounded explicit page/zoom and cancels the preceding request before raising a state change.</summary>
    public PdfApplicationViewState SetView(int pageIndex, double zoom)
    {
        if (pageIndex < 0 || pageIndex >= _source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (double.IsNaN(zoom) || double.IsInfinity(zoom) || zoom < .25 || zoom > 4)
            throw new ArgumentOutOfRangeException(nameof(zoom));
        CancellationTokenSource? old;
        PdfApplicationViewState state;
        lock (_gate)
        {
            Alive();
            old = _active;
            state = _state = new(pageIndex, _source.Pages.Count, zoom, Next(), false, "");
        }

        CancelSource(old);
        StateChanged?.Invoke(this, state);
        return state;
    }

    /// <summary>Maps explicit local application commands. Page/zoom endpoints remain bounded; no navigation action, URL or print job is executed.</summary>
    public PdfApplicationViewState Execute(PdfApplicationViewCommand command)
    {
        if (!Enum.IsDefined(typeof(PdfApplicationViewCommand), command))
            throw new ArgumentOutOfRangeException(nameof(command));
        if (command == PdfApplicationViewCommand.Cancel)
        {
            Cancel();
            return State;
        }

        var state = State;
        int page = state.PageIndex;
        double zoom = state.Zoom;
        switch (command)
        {
            case PdfApplicationViewCommand.NextPage:
                page = Math.Min(state.PageCount - 1, page + 1);
                break;
            case PdfApplicationViewCommand.PreviousPage:
                page = Math.Max(0, page - 1);
                break;
            case PdfApplicationViewCommand.FirstPage:
                page = 0;
                break;
            case PdfApplicationViewCommand.LastPage:
                page = state.PageCount - 1;
                break;
            case PdfApplicationViewCommand.ZoomIn:
                zoom = Math.Min(4, zoom * 1.25);
                break;
            case PdfApplicationViewCommand.ZoomOut:
                zoom = Math.Max(.25, zoom / 1.25);
                break;
            case PdfApplicationViewCommand.ActualSize:
                zoom = 1;
                break;
        }

        return SetView(page, zoom);
    }

    /// <summary>Renders the current view asynchronously and captures bounded text separately. Superseded/canceled generations never return a frame; controls must also compare generation after posting to a dispatcher.</summary>
    public async Task<PdfApplicationFrame> RenderAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CancellationTokenSource request;
        CancellationTokenSource? old;
        PdfApplicationViewState state;
        lock (_gate)
        {
            Alive();
            old = _active;
            request = _active = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            state = _state = new(_state.PageIndex, _source.Pages.Count, _state.Zoom, Next(), true, "");
        }

        CancelSource(old);
        try
        {
            StateChanged?.Invoke(this, state);
            var bounds = _source.Pages[state.PageIndex].Geometry.DisplayBounds();
            double dpi = 96 * state.Zoom;
            double width = Math.Ceiling(bounds.Width * dpi / 72), height = Math.Ceiling(bounds.Height * dpi / 72);
            if (width < 1 || height < 1 || width > int.MaxValue || height > int.MaxValue || width > _maximumSurfacePixels / height)
                throw new PdfException(new("ApplicationView.SurfaceLimit", "The requested UI surface exceeds its explicit finite pixel ceiling."));
            var raster = await _session.RenderAsync(state.PageIndex, new(dpi, dpi), request.Token).ConfigureAwait(false);
            string text = "";
            if (_captureText)
            {
                text = await Task.Run(() => CaptureText(state.PageIndex, request.Token), request.Token).ConfigureAwait(false);
            }

            request.Token.ThrowIfCancellationRequested();
            PdfApplicationViewState complete;
            lock (_gate)
            {
                Alive();
                request.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(_active, request) || _state.Generation != state.Generation)
                    throw new OperationCanceledException(request.Token);
                complete = _state = new(state.PageIndex, state.PageCount, state.Zoom, state.Generation, false, "");
            }

            StateChanged?.Invoke(this, complete);
            return new(complete, raster, text);
        }
        catch (Exception error)
        {
            PdfApplicationViewState? failed = null;
            lock (_gate)
            {
                if (!_disposed && ReferenceEquals(_active, request) && _state.Generation == state.Generation)
                    failed = _state = new(state.PageIndex, state.PageCount, state.Zoom, state.Generation, false, error is OperationCanceledException ? "ApplicationView.Canceled" : error is PdfException pdf ? pdf.Diagnostic.Code : "ApplicationView.Failed");
            }

            if (failed != null)
                StateChanged?.Invoke(this, failed);
            throw;
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_active, request))
                    _active = null;
            }

            request.Dispose();
        }
    }

    /// <summary>Requests cooperative cancellation of this controller's current work without disposing providers or another controller's session.</summary>
    public void Cancel()
    {
        CancellationTokenSource? active;
        lock (_gate)
        {
            Alive();
            active = _active;
        }

        CancelSource(active);
    }

    /// <summary>Submits only an explicit caller-selected printer/options/provider; no control or open/render operation prints automatically.</summary>
    public PdfPrintJob Print(PdfPrintOptions options, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            Alive();
        return PdfPrinting.Submit(_source, options, providers, policy, cancellationToken);
    }

    private string CaptureText(int pageIndex, CancellationToken token)
    {
        string text = "";
        PdfFont.Run("CaptureApplicationText", "applications.view", _policy, token, c =>
        {
            using var graph = new PdfGraphEdit(_source, new(allowSignatureInvalidation: true), c);
            var page = new PdfTextExtractionEngine(graph, new(includeInvisibleText: false), _providers).Read(pageIndex);
            if (page.Raw.Length > _maximumTextCharacters)
                throw PdfWriterCore.Error(c, "ApplicationView.TextLimit", "Captured UI text exceeds its finite UTF16 ceiling.");
            c.Consume(PdfResourceKind.OutputBytes, page.Raw.Length * 2L);
            text = page.Raw;
        }, PdfCapability.ReadDocument);
        return text;
    }

    private long Next()
    {
        if (_state.Generation == long.MaxValue)
            throw new InvalidOperationException("The viewer generation limit was reached.");
        return _state.Generation + 1;
    }

    private void Alive()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PdfApplicationViewer));
    }

    private static void CancelSource(CancellationTokenSource? source)
    {
        try
        {
            source?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>Cancels work and drops only this controller's private cache. Does not block provider callbacks or dispose caller-owned providers/documents.</summary>
    public void Dispose()
    {
        CancellationTokenSource? active;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            active = _active;
        }

        CancelSource(active);
        _session.Dispose();
    }
}
