using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ProgressiveHttpTests
{
    // Explicit loopback-only fixture transport, not a supported production HTTP adapter.
    private sealed class Transport : IPdfRangeSource, IDisposable
    {
        private readonly HttpClient _client = new(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false, MaxResponseHeadersLength = 4 });
        private readonly Uri _uri;
        internal Transport(Uri uri) => _uri = uri;
        private HttpResponseMessage Send(HttpRequestMessage request, PdfOperationContext c)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.ExpandedBytes, 8192);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "original-loopback-fixture");
            return _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, c.CancellationToken).GetAwaiter().GetResult();
        }

        public PdfRangeIdentity GetIdentity(PdfOperationContext context)
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, _uri);
            using var response = Send(request, context);
            response.EnsureSuccessStatusCode();
            return new(response.Content.Headers.ContentLength!.Value, response.Headers.ETag!.Tag);
        }

        public PdfRangeResponse OpenRange(long offset, int length, PdfRangeIdentity expectedIdentity, PdfOperationContext context)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _uri);
            request.Headers.Range = new RangeHeaderValue(offset, offset + length - 1);
            request.Headers.IfMatch.Add(new EntityTagHeaderValue(expectedIdentity.Version));
            context.Consume(PdfResourceKind.ExpandedBytes, length);
            using var response = Send(request, context);
            if (response.StatusCode != HttpStatusCode.PartialContent)
                throw new IOException("Exact partial response required.");
            var range = response.Content.Headers.ContentRange!;
            if (range.From != offset || range.To != offset + length - 1)
                throw new IOException("Exact Content-Range required.");
            var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            context.Checkpoint();
            return new(offset, length, new(range.Length!.Value, response.Headers.ETag!.Tag), new MemoryStream(bytes));
        }

        public void Dispose() => _client.Dispose();
    }

    private sealed class Server : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Task _task;
        private readonly byte[] _bytes;
        private volatile bool _stopping;
        internal bool Authorized = true;
        internal int RangeRequests, IdentityRequests;
        internal readonly List<(long Start, long End)> Ranges = new();
        internal Server(byte[] bytes)
        {
            _bytes = bytes;
            _listener.Start();
            Address = new Uri("http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/original.pdf");
            _task = Task.Run(Serve);
        }

        internal Uri Address { get; }

        private async Task Serve()
        {
            while (!_stopping)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync();
                }
                catch (SocketException)when (_stopping)
                {
                    return;
                }
                catch (ObjectDisposedException)when (_stopping)
                {
                    return;
                }

                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                {
                    string first = (await reader.ReadLineAsync())!;
                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    string? line;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                    {
                        int colon = line!.IndexOf(':');
                        headers.Add(line.Substring(0, colon), line.Substring(colon + 1).Trim());
                    }

                    bool head = first.StartsWith("HEAD ", StringComparison.Ordinal);
                    bool auth = Authorized && headers.TryGetValue("Authorization", out var token) && token == "Bearer original-loopback-fixture";
                    long start = 0, end = _bytes.Length - 1;
                    string status = auth ? "200 OK" : "403 Forbidden";
                    string rangeHeader = "";
                    if (head)
                        Interlocked.Increment(ref IdentityRequests);
                    else if (auth)
                    {
                        Assert.Equal("\"original-revision-1\"", headers["If-Match"]);
                        var range = headers["Range"].Substring(6).Split('-');
                        start = long.Parse(range[0]);
                        end = long.Parse(range[1]);
                        Assert.InRange(start, 0, _bytes.Length - 1);
                        Assert.InRange(end, start, _bytes.Length - 1);
                        Ranges.Add((start, end));
                        Interlocked.Increment(ref RangeRequests);
                        status = "206 Partial Content";
                        rangeHeader = "Content-Range: bytes " + start + "-" + end + "/" + _bytes.Length + "\r\n";
                    }

                    long length = auth ? end - start + 1 : 0;
                    var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nConnection: close\r\nContent-Length: " + length + "\r\nETag: \"original-revision-1\"\r\n" + rangeHeader + "\r\n");
                    await stream.WriteAsync(header, 0, header.Length);
                    if (auth && !head)
                        await stream.WriteAsync(_bytes, (int)start, (int)length);
                }
            }
        }

        public void Dispose()
        {
            _stopping = true;
            _listener.Stop();
            Assert.True(_task.Wait(TimeSpan.FromSeconds(10)), "The explicit range fixture server did not terminate.");
        }
    }

    [Fact]
    public void ActualConditionalHttpRangesReauthorizeWarmCachesAndHonorCancellation()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Progressive", "linearized.pdf"));
        using var server = new Server(bytes);
        using var transport = new Transport(server.Address);
        using var document = new PdfProgressiveDocument(transport, new(1024, 2048));
        Assert.Equal(3, document.ReadPage(0).PageCount);
        Assert.NotEmpty(server.Ranges);
        Assert.All(server.Ranges, range => Assert.InRange(range.End - range.Start + 1, 1, 1024));
        int requests = server.RangeRequests;
        Assert.Equal(3, document.ReadPage(0).PageCount);
        Assert.Equal(requests, server.RangeRequests);
        Assert.True(server.IdentityRequests >= 4);
        server.Authorized = false;
        Assert.Equal("Progressive.ProviderFailed", Assert.Throws<PdfException>(() => document.ReadPage(0)).Diagnostic.Code);
        Assert.Equal(requests, server.RangeRequests);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => document.ReadPage(0, canceled.Token));
    }
}
