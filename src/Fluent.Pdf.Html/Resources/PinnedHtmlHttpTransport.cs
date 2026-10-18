using System.IO;
using System.Net;
#if NET8_0_OR_GREATER
using System.Net.Http;
#endif
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Fluent.Pdf.Html;
/// <summary>Explicit cancellation-capable system DNS on modern library assets. Legacy/fallback assets require a separately supplied qualified resolver.</summary>
public sealed class SystemHtmlNameResolver : IHtmlNameResolver
{
    /// <summary>Resolves a policy-authorized hostname under the shared deadline; the caller must validate and pin every result before connecting.</summary>
    public IReadOnlyList<IPAddress> Resolve(string host, PdfOperationContext context)
    {
        if (string.IsNullOrEmpty(host) || host.Length > 253)
            throw new ArgumentException("A bounded authorized hostname is required.", nameof(host));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
#if NET8_0_OR_GREATER
        context.Consume(PdfResourceKind.ExpandedBytes, 1048576);
        using var worker = context.EnterScope(PdfResourceKind.Workers);
        try
        {
            var addresses = Dns.GetHostAddressesAsync(host, context.CancellationToken).GetAwaiter().GetResult();
            context.Checkpoint();
            return Array.AsReadOnly(addresses);
        }
        catch (SocketException)
        {
            throw HtmlResourceLoader.Error(context, "DnsFailure", "An explicitly authorized hostname could not be resolved; its address is omitted.");
        }
#else
        throw HtmlResourceLoader.Error(context, "DnsRuntime", "This library profile requires an explicit cancellation-safe name resolver; no lookup was attempted.");
#endif
    }
}

/// <summary>Modern HTTP/1.1 transport connecting only to supplied numeric addresses. No redirect, cookie, decompression, proxy or credential discovery is implicit.</summary>
public sealed class PinnedHtmlHttpTransport : IHtmlHttpTransport
{
    /// <summary>Gets whether this compiled library asset supplies the built-in cancellable, DNS-pinned TLS transport.</summary>
    public bool IsSupported
    {
        get
        {
#if NET8_0_OR_GREATER
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>Performs one GET to the prevalidated IP, keeping original Host/TLS name, disabling external chain downloads and owning all response resources.</summary>
    public HtmlHttpResponse Send(HtmlHttpRequest request, PdfOperationContext context)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
#if NET8_0_OR_GREATER
        context.Consume(PdfResourceKind.ExpandedBytes, 1179648);
        context.Consume(PdfResourceKind.Objects, 8);
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            Credentials = null,
            PreAuthenticate = false,
            AutomaticDecompression = DecompressionMethods.None,
            MaxResponseHeadersLength = 32,
            MaxConnectionsPerServer = 1,
            PooledConnectionLifetime = TimeSpan.Zero,
            PooledConnectionIdleTimeout = TimeSpan.Zero,
            MaxResponseDrainSize = 0,
            ResponseDrainTimeout = TimeSpan.Zero,
            ConnectTimeout = context.Policy.Limits.MaximumElapsedTime
        };
        handler.SslOptions = new SslClientAuthenticationOptions
        {
            AllowRenegotiation = false,
            AllowTlsResume = false,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            CertificateChainPolicy = new X509ChainPolicy
            {
                DisableCertificateDownloads = true,
                RevocationMode = X509RevocationMode.NoCheck,
                UrlRetrievalTimeout = TimeSpan.Zero
            },
            RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
            {
                context.Checkpoint();
                if (certificate == null || (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0)
                    return false;
                if (request.ServerCertificateSha256 == null)
                    return errors == SslPolicyErrors.None;
                context.Consume(PdfResourceKind.ExpandedBytes, 1048576);
                byte[] encoded = certificate.GetRawCertData();
                if (encoded.Length > 1048576)
                    return false;
                context.Consume(PdfResourceKind.ExpandedBytes, encoded.Length * 2L + 256);
                using var digest = SHA256.Create();
                string observed = BitConverter.ToString(digest.ComputeHash(encoded)).Replace("-", "").ToLowerInvariant();
                return observed == request.ServerCertificateSha256;
            }
        };
        if (request.ClientCertificate != null)
            handler.SslOptions.ClientCertificateContext = SslStreamCertificateContext.Create(request.ClientCertificate, additionalCertificates: null, offline: true);
        handler.ConnectCallback = async (endpoint, token) =>
        {
            if (!endpoint.DnsEndPoint.Host.Equals(request.Address.IdnHost, StringComparison.OrdinalIgnoreCase) || endpoint.DnsEndPoint.Port != request.Address.Port)
                throw new IOException("The transport attempted a different endpoint.");
            var numeric = IPAddress.Parse(request.ProxyAddress ?? request.DestinationAddress);
            int port = request.Proxy?.Address.Port ?? request.Address.Port;
            var socket = new Socket(numeric.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(numeric, port), token).ConfigureAwait(false);
                context.Checkpoint();
                if (socket.RemoteEndPoint is not IPEndPoint actual || HtmlNetworkPolicy.Normalize(actual.Address) != HtmlNetworkPolicy.Normalize(numeric) || actual.Port != port)
                    throw new IOException("The connected peer differs from the approved endpoint.");
                var stream = new NetworkStream(socket, ownsSocket: true);
                try
                {
                    if (request.Proxy != null)
                        await Tunnel(stream, request, context, token).ConfigureAwait(false);
                    return stream;
                }
                catch
                {
                    stream.Dispose();
                    throw;
                }
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        };
        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        HttpResponseMessage? response = null;
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, request.Address)
            {
                Version = new Version(1, 1),
                VersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            foreach (var pair in request.Headers)
                if (!message.Headers.TryAddWithoutValidation(pair.Key, pair.Value))
                    throw new IOException("An explicitly supplied header is outside the request profile.");
            message.Headers.AcceptEncoding.ParseAdd("gzip, deflate, br");
            response = client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken).GetAwaiter().GetResult();
            context.Checkpoint();
            var headers = response.Headers.SelectMany(h => h.Value.Select(v => new KeyValuePair<string, string>(h.Key, v))).Concat(response.Content.Headers.SelectMany(h => h.Value.Select(v => new KeyValuePair<string, string>(h.Key, v)))).ToArray();
            string contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var body = response.Content.ReadAsStream(context.CancellationToken);
            var owned = new OwnedBody(body, response, client, context);
            response = null;
            try
            {
                return new HtmlHttpResponse((int)owned.Response.StatusCode, contentType, owned, headers, context);
            }
            catch
            {
                owned.Dispose();
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            response?.Dispose();
            client.Dispose();
            context.Checkpoint();
            throw;
        }
        catch (HttpRequestException)
        {
            response?.Dispose();
            client.Dispose();
            context.Checkpoint();
            throw HtmlResourceLoader.Error(context, "HttpTransport", "An authorized HTTP/TLS request failed; credentials, URLs and response bodies are omitted.");
        }
        catch (IOException)
        {
            response?.Dispose();
            client.Dispose();
            context.Checkpoint();
            throw HtmlResourceLoader.Error(context, "HttpTransport", "An authorized HTTP transport failed; private request data is omitted.");
        }
        catch
        {
            response?.Dispose();
            client.Dispose();
            throw;
        }
#else
        throw HtmlResourceLoader.Error(context, "HttpTransportRuntime", "This library asset requires an explicit DNS-pinned cancellation-safe HTTP/TLS transport; no connection was attempted.");
#endif
    }

#if NET8_0_OR_GREATER
    private static async Task Tunnel(Stream stream, HtmlHttpRequest request, PdfOperationContext context, System.Threading.CancellationToken token)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 131072);
        string authority = new IPEndPoint(IPAddress.Parse(request.DestinationAddress), request.Address.Port).ToString();
        string text = "CONNECT " + authority + " HTTP/1.1\r\nHost: " + authority + "\r\n" + (request.Proxy!.Authorization == null ? "" : "Proxy-Authorization: " + request.Proxy.Authorization + "\r\n") + "\r\n";
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
        var header = new List<byte>();
        var one = new byte[1];
        while (true)
        {
            if (header.Count == 32768)
                throw new IOException("The explicit proxy reply exceeds its header bound.");
            int n = await stream.ReadAsync(one, token).ConfigureAwait(false);
            context.Checkpoint();
            if (n == 0)
                throw new EndOfStreamException("The explicit proxy closed its tunnel reply.");
            header.Add(one[0]);
            int count = header.Count;
            if (count >= 4 && header[count - 4] == 13 && header[count - 3] == 10 && header[count - 2] == 13 && header[count - 1] == 10)
                break;
        }

        string line = Encoding.ASCII.GetString(header.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.None)[0];
        var parts = line.Split(' ');
        if (parts.Length < 2 || !parts[0].StartsWith("HTTP/1.", StringComparison.Ordinal) || !int.TryParse(parts[1], out int status) || status != 200)
            throw new IOException("The explicit proxy refused the pinned-address tunnel.");
    }

    private sealed class OwnedBody : Stream
    {
        private readonly Stream _body;
        internal readonly HttpResponseMessage Response;
        private readonly HttpClient _client;
        private readonly PdfOperationContext _context;
        private bool _disposed;
        internal OwnedBody(Stream body, HttpResponseMessage response, HttpClient client, PdfOperationContext context)
        {
            _body = body;
            Response = response;
            _client = client;
            _context = context;
        }

        public override bool CanRead => !_disposed && _body.CanRead;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            _context.Checkpoint();
            int read = _body.ReadAsync(buffer.AsMemory(offset, count), _context.CancellationToken).AsTask().GetAwaiter().GetResult();
            _context.Checkpoint();
            return read;
        }

        public override void Flush() => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing)
                {
                    _body.Dispose();
                    Response.Dispose();
                    _client.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
#endif
}
