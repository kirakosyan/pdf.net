using System.IO;
using System.IO.Compression;
using System.Net;

namespace Fluent.Pdf.Html;
/// <summary>Origin/address-bound HTTP acquisition with private per-job cookies. Offline is the default; no environment proxy or account credentials are discovered.</summary>
public sealed class HtmlResourceLoader
{
    private readonly HtmlNetworkPolicy _policy;
    private readonly IHtmlNameResolver? _resolver;
    private readonly IHtmlHttpTransport? _transport;
    /// <summary>Captures immutable policy and explicit optional providers. Network-enabled defaults use cancellation-capable DNS and the pinned modern HTTP transport; unsupported runtimes fail before network work.</summary>
    public HtmlResourceLoader(HtmlNetworkPolicy? policy = null, IHtmlNameResolver? resolver = null, IHtmlHttpTransport? transport = null)
    {
        _policy = policy ?? HtmlNetworkPolicy.Offline;
        _resolver = resolver ?? (_policy.Origins.Count == 0 ? null : new SystemHtmlNameResolver());
        _transport = transport ?? (_policy.Origins.Count == 0 ? null : new PinnedHtmlHttpTransport());
    }

    /// <summary>Creates a new resource/cookie session for one job, under the mandatory context.</summary>
    public IHtmlResourceSession OpenSession(PdfOperationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Consume(PdfResourceKind.ExpandedBytes, 4096);
        context.Consume(PdfResourceKind.Objects, 4);
        return new Session(_policy, _resolver, _transport, context);
    }

    private sealed class Session : IHtmlResourceSession
    {
        private readonly HtmlNetworkPolicy _policy;
        private readonly IHtmlNameResolver? _resolver;
        private readonly IHtmlHttpTransport? _transport;
        private sealed class RequestState
        {
            internal int Hops;
            internal Uri? Next;
            internal bool Continued;
        }

        private readonly Dictionary<string, RequestState> _redirects = new(StringComparer.Ordinal);
        private readonly PdfOperationContext _context;
        private readonly Dictionary<string, CookieContainer> _cookies = new(StringComparer.Ordinal);
        private bool _disposed;
        internal Session(HtmlNetworkPolicy policy, IHtmlNameResolver? resolver, IHtmlHttpTransport? transport, PdfOperationContext context)
        {
            _policy = policy;
            _context = context;
            _resolver = resolver;
            _transport = transport;
            foreach (var rule in policy.Origins)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, 4096 + rule.InitialCookies.Sum(c => c.Length * 128L));
                context.Consume(PdfResourceKind.Objects, rule.InitialCookies.Sum(c => c.Length / 2L + 1) + 1);
                // One sentinel slot detects overflow before the framework container can silently evict an in-profile cookie.
                var jar = new CookieContainer(129, 129, 4096);
                try
                {
                    foreach (string cookie in rule.InitialCookies)
                    {
                        jar.SetCookies(rule.Origin, cookie);
                        if (jar.Count > 128)
                            throw Error(context, "CookieExtent", "The private cookie inventory exceeds its job bound.");
                    }
                }
                catch (CookieException)
                {
                    throw Error(context, "CookieSyntax", "An explicitly supplied cookie is invalid; credential values are omitted.");
                }

                _cookies.Add(rule.Origin.AbsoluteUri, jar);
            }
        }

        public HtmlResourceResult? Resolve(HtmlResourceRequest request, PdfOperationContext context)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(IHtmlResourceSession));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!ReferenceEquals(context, _context))
                throw new ArgumentException("A resource session belongs to its original operation context.", nameof(context));
            context.Checkpoint();
            if (_policy.Origins.Count == 0)
                return null;
            var rule = _policy.RequireOrigin(request.Address, context);
            context.Consume(PdfResourceKind.ExpandedBytes, 1024);
            context.Consume(PdfResourceKind.Objects, 1);
            if (_redirects.Count == 1024 || _redirects.ContainsKey(request.RequestId))
                throw Error(context, "RequestIdentity", "The resource request inventory is duplicate or exceeds its job bound.");
            int redirects = 0;
            if (request.RedirectedRequestId != null)
            {
                if (!_redirects.TryGetValue(request.RedirectedRequestId, out var previous) || previous.Next != request.Address || previous.Continued)
                    throw Error(context, "RedirectIdentity", "A redirect does not match its unique preceding response destination.");
                previous.Continued = true;
                redirects = previous.Hops + 1;
                if (redirects > _policy.MaximumRedirects)
                    throw Error(context, "RedirectLimit", "The explicit redirect-chain limit was reached.");
            }

            var state = new RequestState
            {
                Hops = redirects
            };
            _redirects.Add(request.RequestId, state);
            string destination = ResolveAddress(request.Address, context);
            string? proxyAddress = _policy.Proxy == null ? null : ResolveAddress(_policy.Proxy.Address, context);
            context.Consume(PdfResourceKind.ExpandedBytes, rule.Headers.Sum(p => (p.Key.Length + p.Value.Length) * 4L) + 4096);
            var headers = rule.Headers.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            context.Consume(PdfResourceKind.ExpandedBytes, _cookies[rule.Origin.AbsoluteUri].Count * 8192L + 4096);
            string cookieHeader = _cookies[rule.Origin.AbsoluteUri].GetCookieHeader(request.Address);
            if (cookieHeader.Length > 32768)
                throw Error(context, "CookieExtent", "The private cookie header exceeds its job bound.");
            if (cookieHeader.Length != 0)
                headers["Cookie"] = cookieHeader;
            var approved = new HtmlHttpRequest(request.Address, destination, headers, rule, _policy.Proxy, proxyAddress);
            using var worker = context.EnterScope(PdfResourceKind.Workers);
            try
            {
                using var response = _transport!.Send(approved, context) ?? throw Error(context, "TransportResponse", "The explicit transport returned no response.");
                context.Checkpoint();
                foreach (var pair in response.Headers.Where(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)))
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, pair.Value.Length * 128L + 512);
                    context.Consume(PdfResourceKind.Objects, pair.Value.Length / 2L + 1);
                    try
                    {
                        _cookies[rule.Origin.AbsoluteUri].SetCookies(request.Address, pair.Value);
                        if (_cookies[rule.Origin.AbsoluteUri].Count > 128)
                            throw Error(context, "CookieExtent", "The private cookie inventory exceeds its job bound.");
                    }
                    catch (CookieException)
                    {
                        throw Error(context, "CookieSyntax", "A response cookie is invalid; credential values are omitted.");
                    }
                }

                bool redirect = new[]
                {
                    301,
                    302,
                    303,
                    307,
                    308
                }.Contains(response.StatusCode);
                if (redirect)
                {
                    var locations = response.Headers.Where(h => h.Key.Equals("Location", StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (locations.Length != 1 || !Uri.TryCreate(request.Address, locations[0].Value, out var next))
                        throw Error(context, "RedirectLocation", "A redirect requires one valid destination.");
                    _policy.RequireOrigin(next, context); // A new request resolves/validates/pins DNS again before any connection.
                    if (redirects == _policy.MaximumRedirects)
                        throw Error(context, "RedirectLimit", "The explicit redirect-chain limit was reached.");
                    state.Next = next;
                    return new(Empty(request.Address, context), response.StatusCode, response.Headers, context);
                }

                if (response.StatusCode < 200 || response.StatusCode >= 300)
                {
                    if (request.IsMainDocument)
                        throw Error(context, "MainResourceStatus", "The authorized main document returned an unsuccessful status.");
                    context.Report(new("Html.ResourceStatus", "An authorized subresource returned an unsuccessful status; response content is omitted from diagnostics.", lossCategory: PdfLossCategory.OmittedContent));
                }

                string[] encodings = response.Headers.Where(h => h.Key.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase)).SelectMany(h => h.Value.Split(',')).Select(v => v.Trim().ToLowerInvariant()).Where(v => v != "identity" && v.Length != 0).ToArray();
                if (encodings.Length > 1 || encodings.Length == 1 && encodings[0] != "gzip" && encodings[0] != "deflate" && encodings[0] != "br")
                    throw Error(context, "ContentEncoding", "The response compression is outside the explicit decoding profile.");
                var bytes = Capture(response.Body, encodings.FirstOrDefault(), _policy.MaximumResponseBytes, context);
                var resource = new PdfHtmlResource(request.Address, response.MediaType, bytes);
                // Encoded-body and hop framing headers no longer describe the captured decoded bytes.
                var exposed = response.Headers.Where(h => !new[] { "Content-Encoding", "Content-Length", "Transfer-Encoding", "Connection", "Proxy-Authenticate" }.Contains(h.Key, StringComparer.OrdinalIgnoreCase)).ToArray();
                return new(resource, response.StatusCode, exposed, context);
            }
            catch (OperationCanceledException)
            {
                context.Checkpoint();
                throw;
            }
            catch (IOException)
            {
                context.Checkpoint();
                throw Error(context, "ResourceIo", "A permitted resource could not be captured; response data and addresses are omitted.");
            }
            catch (PdfException)
            {
                throw;
            }
            catch (Exception)
            {
                context.Checkpoint();
                throw Error(context, "ResourceProvider", "An explicit resource provider failed; private request and response data are omitted.");
            }
        }

        private string ResolveAddress(Uri address, PdfOperationContext context)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.ExpandedBytes, 8192);
            IReadOnlyList<IPAddress> values;
            if (IPAddress.TryParse(address.IdnHost.Trim('[', ']'), out var numeric))
                values = new[]
                {
                    numeric
                };
            else
            {
                try
                {
                    values = _resolver!.Resolve(address.IdnHost, context);
                }
                catch (OperationCanceledException)
                {
                    context.Checkpoint();
                    throw;
                }
                catch (PdfException)
                {
                    throw;
                }
                catch (Exception)
                {
                    context.Checkpoint();
                    throw Error(context, "DnsProvider", "The explicit name provider failed; private address and provider data are omitted.");
                }
            }

            context.Checkpoint();
            if (values == null || values.Count == 0 || values.Count > 64)
                throw Error(context, "DnsExtent", "The explicit name resolver returned an invalid address inventory.");
            var captured = new List<IPAddress>();
            foreach (var value in values)
            {
                if (value == null)
                    throw Error(context, "DnsExtent", "The explicit name resolver returned an invalid address.");
                var copy = IPAddress.Parse(value.ToString());
                _policy.RequireAddress(copy, context);
                captured.Add(copy);
            }

            return HtmlNetworkPolicy.Normalize(captured[0]);
        }

        public void Dispose()
        {
            _disposed = true;
            _cookies.Clear();
            _redirects.Clear();
        }
    }

    private static PdfHtmlResource Empty(Uri address, PdfOperationContext context)
    {
        var builder = new PdfByteBuilder(context);
        return new(address, "application/octet-stream", builder.Build());
    }

    private static PdfByteStore Capture(Stream body, string? encoding, long maximum, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 1052672);
        context.Consume(PdfResourceKind.Objects, 4);
        using var input = new BorrowedStream(body, true, context);
        Stream? decoder = null;
        try
        {
            if (encoding == "gzip")
                decoder = new GZipStream(input, CompressionMode.Decompress, true);
            else if (encoding == "deflate")
            {
#if NET8_0_OR_GREATER
                decoder = new ZLibStream(input, CompressionMode.Decompress, true);
#else
                throw Error(context, "ContentEncodingRuntime", "HTTP zlib decoding requires an explicit supported provider on this library profile.");
#endif
            }
            else if (encoding == "br")
            {
#if NET8_0_OR_GREATER
                decoder = new BrotliStream(input, CompressionMode.Decompress, true);
#else
                throw Error(context, "ContentEncodingRuntime", "Brotli requires an explicit supported decoding provider on this library profile.");
#endif
            }

            using var expanded = context.BorrowExpandedInput(decoder ?? input);
            var builder = new PdfByteBuilder(context);
            var buffer = new byte[4096];
            int count;
            while ((count = expanded.Read(buffer, 0, (int)Math.Min(buffer.Length, maximum - builder.Length + 1))) > 0)
            {
                if (count > maximum - builder.Length)
                    throw Error(context, "ResponseExtent", "The decoded resource exceeds its explicit byte ceiling.");
                for (int i = 0; i < count; i++)
                    builder.Add(buffer[i]);
                context.Checkpoint();
            }

            return builder.Build();
        }
        catch (InvalidDataException)
        {
            throw Error(context, "ContentEncoding", "The permitted resource has malformed compressed bytes.");
        }
        finally
        {
            decoder?.Dispose();
        }
    }

    internal static PdfException Error(PdfOperationContext context, string code, string message) => PdfWriterCore.Error(context, "Html." + code, message);
}
