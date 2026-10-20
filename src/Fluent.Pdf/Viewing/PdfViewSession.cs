using System.Threading;
using System.Threading.Tasks;

namespace Fluent.Pdf;
/// <summary>A toolkit-independent viewer over one immutable snapshot, with bounded outstanding requests and byte-capped LRU samples. Dispose cancels pending requests without blocking provider callbacks.</summary>
public sealed class PdfViewSession : IDisposable
{
    private readonly object _gate = new();
    private readonly PdfDocumentSnapshot _source;
    private readonly PdfProviderRegistry _providers;
    private readonly PdfOperationPolicy _policy;
    private readonly long _maximumBytes;
    private readonly int _maximumRequests;
    private readonly Dictionary<string, LinkedListNode<(string Key, PdfRaster Raster)>> _cache = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Key, PdfRaster Raster)> _lru = new();
    private readonly HashSet<CancellationTokenSource> _requests = new();
    private long _cachedBytes; private bool _disposed;
    /// <summary>Captures immutable content, explicit renderer, per-request policy and positive cache/request bounds. The session never owns caller providers.</summary>
    public PdfViewSession(PdfDocumentSnapshot source, PdfProviderRegistry providers, long maximumCachedBytes = 16777216, int maximumOutstandingRequests = 8, PdfOperationPolicy? policy = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _ = providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages);
        if (maximumCachedBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumCachedBytes));
        if (maximumOutstandingRequests < 1 || maximumOutstandingRequests > 64)
            throw new ArgumentOutOfRangeException(nameof(maximumOutstandingRequests));
        _maximumBytes = maximumCachedBytes;
        _maximumRequests = maximumOutstandingRequests;
        _policy = policy ?? source.Options.OperationPolicy;
    }
    /// <summary>Gets retained sample bytes, excluding caller-owned returned rasters and in-flight bounded operations.</summary>
    public long CachedBytes
    {
        get
        {
            lock (_gate)
                return _cachedBytes;
        }
    }
    /// <summary>Gets number of retained rasters.</summary>
    public int CachedRasterCount
    {
        get
        {
            lock (_gate)
                return _cache.Count;
        }
    }
    /// <summary>Renders asynchronously on a worker with independent request cancellation. Cached hits retain immutable samples; each caller still owns its returned reference.</summary>
    public Task<PdfRaster> RenderAsync(int pageIndex, PdfRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (pageIndex < 0 || pageIndex >= _source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        options ??= new();
        cancellationToken.ThrowIfCancellationRequested();
        var selected = options;
        string key = Key(pageIndex, selected);
        CancellationTokenSource request;
        lock (_gate)
        {
            Alive();
            if (_cache.TryGetValue(key, out var cached))
            {
                _lru.Remove(cached);
                _lru.AddFirst(cached);
                return Task.FromResult(cached.Value.Raster);
            }
            if (_requests.Count == _maximumRequests)
                throw new PdfException(new PdfDiagnostic("View.RequestLimit", "The viewer outstanding-request bound is exhausted."));
            request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _requests.Add(request);
        }
        return RenderRequest();
        async Task<PdfRaster> RenderRequest()
        {
            try
            {
                var result = await Task.Run(() => PdfRendering.RenderPage(_source, pageIndex, _providers, selected, _policy, request.Token), request.Token).ConfigureAwait(false);
                request.Token.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    Alive();
                    request.Token.ThrowIfCancellationRequested();
                    long bytes = result.Request.ByteLength;
                    if (bytes <= _maximumBytes)
                    {
                        if (_cache.TryGetValue(key, out var old))
                        {
                            _lru.Remove(old);
                            _cache.Remove(key);
                            _cachedBytes -= old.Value.Raster.Request.ByteLength;
                        }
                        while (_cachedBytes > _maximumBytes - bytes && _lru.Last != null)
                        {
                            var last = _lru.Last!;
                            _cache.Remove(last.Value.Key);
                            _cachedBytes -= last.Value.Raster.Request.ByteLength;
                            _lru.RemoveLast();
                        }
                        var node = _lru.AddFirst((key, result));
                        _cache.Add(key, node);
                        _cachedBytes += bytes;
                    }
                }
                return result;
            }
            finally { lock (_gate) _requests.Remove(request); request.Dispose(); }
        }
    }
    /// <summary>Renders a page thumbnail that fits explicit positive pixel bounds on the same full-page grid.</summary>
    public Task<PdfRaster> RenderThumbnailAsync(int pageIndex, int maximumWidth, int maximumHeight, CancellationToken cancellationToken = default)
    {
        if (pageIndex < 0 || pageIndex >= _source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (maximumWidth < 1 || maximumHeight < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumWidth));
        var bounds = _source.Pages[pageIndex].Geometry.DisplayBounds();
        double dpi = Math.Min(9600, Math.Min(maximumWidth / bounds.Width, maximumHeight / bounds.Height) * 72);
        if (double.IsInfinity(dpi) || double.IsNaN(dpi) || dpi <= 0)
            throw new ArgumentException("Thumbnail requires a nonempty displayed page.");
        // A downward floating-point step keeps the ceil-based grid within the caller's exact ceiling.
        for (int i = 0; i < 4 && (Math.Ceiling(bounds.Width * dpi / 72) > maximumWidth || Math.Ceiling(bounds.Height * dpi / 72) > maximumHeight); i++)
            dpi = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(dpi) - 1);
        return RenderAsync(pageIndex, new(dpi, dpi), cancellationToken);
    }

    /// <summary>Cancels all requests currently in flight. Later requests receive fresh tokens; cancellation callbacks run outside the cache lock.</summary>
    public void CancelPending()
    {
        CancellationTokenSource[] requests;
        lock (_gate)
        {
            Alive();
            requests = _requests.ToArray();
        }
        foreach (var request in requests)
            Cancel(request);
    }
    /// <summary>Clears only the session's retained rasters. Caller-owned results stay readable.</summary>
    public void ClearCache()
    {
        lock (_gate)
        {
            Alive();
            _cache.Clear();
            _lru.Clear();
            _cachedBytes = 0;
        }
    }
    /// <summary>Builds exact full-page pixel-grid tiles with integer boundaries. Regions map to the same displayed CropBox grid used by the renderer, including rotation and UserUnit.</summary>
    public IReadOnlyList<PdfRenderOptions> PlanTiles(int pageIndex, double dpi, int tilePixels = 256, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (pageIndex < 0 || pageIndex >= _source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (tilePixels < 1 || tilePixels > 8192)
            throw new ArgumentOutOfRangeException(nameof(tilePixels));
        var options = new PdfRenderOptions(dpi, dpi);
        lock (_gate)
            Alive();
        var result = new List<PdfRenderOptions>();
        PdfFont.Run("PlanViewTiles", "managed.view", policy ?? _policy, cancellationToken, c =>
        {
            var bounds = _source.Pages[pageIndex].Geometry.DisplayBounds();
            double w = Math.Ceiling(bounds.Width * dpi / 72), h = Math.Ceiling(bounds.Height * dpi / 72);
            if (w < 1 || h < 1 || w > int.MaxValue || h > int.MaxValue)
                throw PdfWriterCore.Error(c, "View.GridExtent", "The full-page grid exceeds integer tile coordinates.");
            int width = (int)w, height = (int)h;
            double sx = width / bounds.Width, sy = height / bounds.Height;
            for (int y = 0; y < height;)
            {
                int bottom = (int)Math.Min(height, (long)y + tilePixels);
                for (int x = 0; x < width;)
                {
                    c.Checkpoint();
                    c.Consume(PdfResourceKind.Objects, 1);
                    c.Consume(PdfResourceKind.ExpandedBytes, 256);
                    int right = (int)Math.Min(width, (long)x + tilePixels);
                    double left = x / sx, top = y / sy, r = right == width ? bounds.Right : right / sx, b = bottom == height ? bounds.Top : bottom / sy;
                    result.Add(PdfRenderOptions.Tile(dpi, new(left, top, r - left, b - top), x, y, right - x, bottom - y));
                    x = right;
                }
                y = bottom;
            }
        }, PdfCapability.RenderPages);
        return result.AsReadOnly();
    }
    /// <summary>Maps top-left displayed physical points to native PDF user coordinates for selection/navigation overlays.</summary>
    public PdfPoint DisplayToUser(int pageIndex, PdfPoint point)
    {
        lock (_gate)
            Alive();
        if (pageIndex < 0 || pageIndex >= _source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        return _source.Pages[pageIndex].Geometry.DisplayToUser().Transform(point);
    }
    /// <summary>Returns source character advance cells intersecting a displayed physical-point region. Extraction is explicit; no OCR or script execution occurs.</summary>
    public IReadOnlyList<PdfExtractedCharacter> SelectText(int pageIndex, PdfRect region, PdfTextExtractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            Alive();
        var characters = new List<PdfExtractedCharacter>();
        PdfFont.Run("SelectViewText", "managed.view", _policy, cancellationToken, c =>
        {
            if (pageIndex < 0 || pageIndex >= _source.Pages.Count)
                throw new ArgumentOutOfRangeException(nameof(pageIndex));
            using var graph = new PdfGraphEdit(_source, new(), c);
            var data = new PdfTextExtractionEngine(graph, options ?? new(), _providers).Read(pageIndex);
            foreach (var ch in data.Characters)
            {
                c.Checkpoint();
                var bounds = ch.Quad.Bounds;
                if (bounds.Right >= region.X && bounds.X <= region.Right && bounds.Top >= region.Y && bounds.Y <= region.Top)
                {
                    c.Consume(PdfResourceKind.ExpandedBytes, 16);
                    characters.Add(ch);
                }
            }
        }, PdfCapability.ReadDocument);
        return characters.AsReadOnly();
    }
    /// <summary>Finds the nearest extracted advance cell within an explicit displayed-point radius, retaining source character/operation provenance.</summary>
    public PdfExtractedCharacter? HitTest(int pageIndex, PdfPoint point, double radius = 2, CancellationToken cancellationToken = default)
    {
        if (double.IsNaN(radius) || double.IsInfinity(radius) || radius < 0 || radius > 1000)
            throw new ArgumentOutOfRangeException(nameof(radius));
        var candidates = SelectText(pageIndex, new(point.X - radius, point.Y - radius, radius * 2, radius * 2), cancellationToken: cancellationToken);
        PdfExtractedCharacter? nearest = null;
        double best = double.MaxValue;
        foreach (var ch in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var b = ch.Quad.Bounds;
            double dx = Math.Max(b.X - point.X, Math.Max(0, point.X - b.Right)), dy = Math.Max(b.Y - point.Y, Math.Max(0, point.Y - b.Top)), distance = dx * dx + dy * dy;
            if (distance <= radius * radius && distance < best)
            {
                best = distance;
                nearest = ch;
            }
        }
        return nearest;
    }
    /// <summary>Cancels current requests and releases only session cache references. It does not dispose caller providers or block their cancellation callbacks.</summary>
    public void Dispose()
    {
        CancellationTokenSource[] requests;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            requests = _requests.ToArray();
            _cache.Clear();
            _lru.Clear();
            _cachedBytes = 0;
        }
        foreach (var request in requests)
            Cancel(request);
    }
    private static void Cancel(CancellationTokenSource request)
    {
        try
        {
            request.Cancel();
        }
        catch (ObjectDisposedException) { }
    }
    private void Alive()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PdfViewSession));
    }
    private static string Key(int page, PdfRenderOptions o)
    {
        string N(double d) => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        return page + "|" + N(o.DpiX) + "|" + N(o.DpiY) + "|" + (o.Region.HasValue ? N(o.Region.Value.X) + "," + N(o.Region.Value.Y) + "," + N(o.Region.Value.Width) + "," + N(o.Region.Value.Height) : "full") + "|" + o.PixelWindow + "|" + o.BackgroundRgb + "|" + o.Color + "|" + o.Annotations + "|" + o.Print;
    }
}
