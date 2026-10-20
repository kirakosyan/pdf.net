using System.Net;
using System.Net.Http.Headers;
using Fluent.Pdf;
using Fluent.Pdf.Workers;

namespace Fluent.Pdf.WebSample;
// An explicit HTTPS sample transport. The application owns and disposes it.
internal sealed class WorkerHttpTransport : IPdfRenderWorkerTransport, IDisposable
{
    private readonly Uri _endpoint;
    private readonly HttpClient _client;
    internal WorkerHttpTransport(Uri endpoint)
    {
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != "https" || endpoint.UserInfo.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.AbsoluteUri.Length > 4096)
            throw new ArgumentException("An explicit credential-free HTTPS endpoint is required.", nameof(endpoint));
        _endpoint = endpoint;
        _client = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false, AutomaticDecompression = DecompressionMethods.None });
    }

    public void Exchange(Stream request, Stream response, PdfOperationContext context)
    {
        // Run asynchronous HTTP away from a potentially blocked UI synchronization context.
        Task.Run(async () =>
        {
            context.Checkpoint();
            using var message = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = new StreamContent(request)
            };
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.fluent-pdf.worker");
            using var result = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken).ConfigureAwait(false);
            if ((int)result.StatusCode == 429 || (int)result.StatusCode == 502 || (int)result.StatusCode == 503 || (int)result.StatusCode == 504)
                throw new PdfWorkerTransientException();
            if (!result.IsSuccessStatusCode)
                throw new PdfException(new PdfDiagnostic("Worker.TransportRejected", "The configured worker endpoint rejected the authenticated request."));
            if (result.Content.Headers.ContentType?.MediaType != "application/vnd.fluent-pdf.worker" || result.Content.Headers.ContentLength > 67109120)
                throw new PdfException(new PdfDiagnostic("Worker.Protocol", "The worker response type/extent is unsupported."));
            await using var body = await result.Content.ReadAsStreamAsync(context.CancellationToken).ConfigureAwait(false);
            await body.CopyToAsync(response, context.CancellationToken).ConfigureAwait(false);
            context.Checkpoint();
        }).GetAwaiter().GetResult();
    }

    public void Dispose() => _client.Dispose();
}
