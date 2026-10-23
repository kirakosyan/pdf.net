using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fluent.Pdf.Workers;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class RenderServiceTests
{
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    private static MemoryStream Input(string value = "<h1>Original worker input</h1>") => new(Encoding.UTF8.GetBytes(value));
    [Fact]
    public void AuthenticatedServiceReturnsOnlyBoundPdfAndExpiresTransportLeases()
    {
        using var credentials = new PdfWorkerCredentials("tenant_a", Key);
        var renderer = new Renderer();
        var service = new PdfRenderWorkerService(new[] { new PdfWorkerTenant(credentials) }, new(renderer));
        var transport = new Loopback(service);
        var client = new PdfRenderWorkerClient(credentials, transport);
        using var input = Input();
        var result = client.RenderHtml(input);
        using var pdf = result.OpenPdfStream();
        using var document = Pdf.Open(pdf);
        Assert.Single(document.Pages);
        Assert.True(input.CanRead);
        Assert.Equal("<h1>Original worker input</h1>", renderer.Markup);
        Assert.Equal(1, transport.Calls);
        Assert.Throws<ObjectDisposedException>(() => transport.Request!.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => transport.Response!.WriteByte(1));
        Assert.Equal("managed.authenticated-render-client", result.OperationReport.ProviderId);
        Assert.Throws<ObjectDisposedException>(() => renderer.Context!.Checkpoint());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RequestAndResponseTamperingNeverReturnsPdf(bool request)
    {
        using var credentials = new PdfWorkerCredentials("tenant_a", Key);
        var renderer = new Renderer();
        var transport = new Loopback(new(new[] { new PdfWorkerTenant(credentials) }, new(renderer)))
        {
            TamperRequest = request,
            TamperResponse = !request
        };
        using var input = Input();
        Assert.Equal("Worker.Authentication", Assert.Throws<PdfException>(() => new PdfRenderWorkerClient(credentials, transport).RenderHtml(input)).Diagnostic.Code);
        Assert.Equal(request ? 0 : 1, renderer.Calls);
    }

    [Fact]
    public void ReplayCannotSatisfyANewNonceAndCredentialsCannotCrossTenants()
    {
        using var credentials = new PdfWorkerCredentials("tenant_a", Key);
        using var wrong = new PdfWorkerCredentials("tenant_b", Key);
        var renderer = new Renderer();
        var transport = new Loopback(new(new[] { new PdfWorkerTenant(credentials) }, new(renderer)));
        var client = new PdfRenderWorkerClient(credentials, transport);
        using (var input = Input())
            _ = client.RenderHtml(input);
        transport.Replay = true;
        using (var input = Input())
            Assert.Equal("Worker.Binding", Assert.Throws<PdfException>(() => client.RenderHtml(input)).Diagnostic.Code);
        transport.Replay = false;
        using (var input = Input())
            Assert.Equal("Worker.Authentication", Assert.Throws<PdfException>(() => new PdfRenderWorkerClient(wrong, transport).RenderHtml(input)).Diagnostic.Code);
        Assert.Equal(1, renderer.Calls);
    }

    [Fact]
    public void OnlyExplicitTransientRetriesRepeatTheExactIdempotentRequest()
    {
        using var credentials = new PdfWorkerCredentials("tenant_a", Key);
        var renderer = new Renderer();
        var transport = new Loopback(new(new[] { new PdfWorkerTenant(credentials) }, new(renderer)))
        {
            Transient = 1
        };
        using (var input = Input())
            _ = new PdfRenderWorkerClient(credentials, transport, 2).RenderHtml(input);
        Assert.Equal(2, transport.Calls);
        Assert.Equal(transport.Requests[0], transport.Requests[1]);
        Assert.Equal(1, renderer.Calls);
        transport.Transient = 1;
        using (var input = Input())
            Assert.Equal("Worker.Transport", Assert.Throws<PdfException>(() => new PdfRenderWorkerClient(credentials, transport).RenderHtml(input)).Diagnostic.Code);
        Assert.Equal(3, transport.Calls);
    }

    [Fact]
    public void TenantJobAndInputQuotasRemainIndependent()
    {
        using var a = new PdfWorkerCredentials("tenant_a", Key);
        using var b = new PdfWorkerCredentials("tenant_b", Key);
        var renderer = new Renderer();
        var transport = new Loopback(new(new[] { new PdfWorkerTenant(a, maximumJobs: 1), new PdfWorkerTenant(b, maximumInputBytes: 64) }, new(renderer)));
        using (var input = Input())
            _ = new PdfRenderWorkerClient(a, transport).RenderHtml(input);
        using (var input = Input())
            Assert.Equal("Worker.TenantQuota", Assert.Throws<PdfException>(() => new PdfRenderWorkerClient(a, transport).RenderHtml(input)).Diagnostic.Code);
        using (var input = Input(new string ('a', 65)))
            Assert.Equal("Worker.TenantQuota", Assert.Throws<PdfException>(() => new PdfRenderWorkerClient(b, transport).RenderHtml(input)).Diagnostic.Code);
        using (var input = Input())
            _ = new PdfRenderWorkerClient(b, transport).RenderHtml(input);
        Assert.Equal(2, renderer.Calls);
    }

    [Fact]
    public async Task ConcurrentTenantLimitRejectsUntilTheOriginalLeaseCompletes()
    {
        using var credentials = new PdfWorkerCredentials("tenant_a", Key);
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderer = new Renderer
        {
            Before = c =>
            {
                started.TrySetResult(true);
                release.Wait(c.CancellationToken);
            }
        };
        var transport = new Loopback(new(new[] { new PdfWorkerTenant(credentials) }, new(renderer)));
        var client = new PdfRenderWorkerClient(credentials, transport);
        using var input = Input();
        var running = Task.Run(() => client.RenderHtml(input));
        try
        {
            await Task.WhenAny(started.Task, running, Task.Delay(10000));
            Assert.True(started.Task.IsCompleted);
            using var second = Input();
            Assert.Equal("Worker.TenantQuota", Assert.Throws<PdfException>(() => client.RenderHtml(second)).Diagnostic.Code);
        }
        finally
        {
            release.Set();
        }

        _ = await running;
        renderer.Before = null;
        using var third = Input();
        _ = client.RenderHtml(third);
        Assert.Equal(2, renderer.Calls);
    }

    [Fact]
    public void ResourceFailuresCancellationAndDisposedKeysCannotInvokeTheBackend()
    {
        using var credentials = new PdfWorkerCredentials("tenant_a", Key);
        var renderer = new Renderer();
        var transport = new Loopback(new(new[] { new PdfWorkerTenant(credentials) }, new(renderer)));
        var client = new PdfRenderWorkerClient(credentials, transport);
        using (var input = Input())
            Assert.Throws<PdfException>(() => client.RenderHtml(input, new(limits: new(maximumExpandedBytes: 100))));
        using (var input = Input())
            Assert.ThrowsAny<OperationCanceledException>(() => client.RenderHtml(input, cancellationToken: new CancellationToken(true)));
        credentials.Dispose();
        using (var input = Input())
            Assert.Equal("Worker.CredentialExpired", Assert.Throws<PdfException>(() => client.RenderHtml(input)).Diagnostic.Code);
        using (var input = Input())
            Assert.Equal("Worker.CredentialExpired", Assert.Throws<PdfException>(() => ContractExamples.CSharp.ContractExample.RenderWorker(input, client)).Diagnostic.Code);
        using (var input = Input())
            Assert.Equal("Worker.CredentialExpired", Assert.Throws<PdfException>(() => ContractExamples.VisualBasic.ContractExample.RenderWorker(input, client)).Diagnostic.Code);
        Assert.Equal(0, transport.Calls);
        Assert.Equal(0, renderer.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BackendFailureAndInvalidPdfPublishNoAuthenticatedSuccess(bool throws)
    {
        using var credentials = new PdfWorkerCredentials("tenant_a", Key);
        var renderer = new Renderer
        {
            Fail = throws,
            Invalid = !throws
        };
        var transport = new Loopback(new(new[] { new PdfWorkerTenant(credentials) }, new(renderer)));
        using var input = Input();
        var failure = Assert.Throws<PdfException>(() => new PdfRenderWorkerClient(credentials, transport).RenderHtml(input));
        if (throws)
            Assert.Equal("Worker.RenderFailed", failure.Diagnostic.Code);
        Assert.DoesNotContain("secret-worker-model", failure.ToString());
        Assert.Equal(0, transport.LastResponseLength);
    }

    [Fact]
    public async Task AcceptedNonceIsRejectedDuringAndAfterTheJobWithoutSpendingAnotherQuota()
    {
        using var credentials = new PdfWorkerCredentials("tenant_a", Key);
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderer = new Renderer
        {
            Before = c =>
            {
                started.TrySetResult(true);
                release.Wait(c.CancellationToken);
            }
        };
        var service = new PdfRenderWorkerService(new[] { new PdfWorkerTenant(credentials, maximumJobs: 2) }, new(renderer));
        var transport = new Loopback(service);
        var client = new PdfRenderWorkerClient(credentials, transport);
        using var input = Input();
        var running = Task.Run(() => client.RenderHtml(input));
        try
        {
            await Task.WhenAny(started.Task, running, Task.Delay(10000));
            Assert.True(started.Task.IsCompleted);
            using var duplicate = new MemoryStream(transport.Requests[0]);
            using var response = new MemoryStream();
            Assert.Equal("Worker.Replay", Assert.Throws<PdfException>(() => service.Process(duplicate, response)).Diagnostic.Code);
            Assert.Equal(0, response.Length);
        }
        finally
        {
            release.Set();
        }

        _ = await running;
        using (var duplicate = new MemoryStream(transport.Requests[0]))
        using (var response = new MemoryStream())
            Assert.Equal("Worker.Replay", Assert.Throws<PdfException>(() => service.Process(duplicate, response)).Diagnostic.Code);
        renderer.Before = null;
        using var second = Input();
        _ = client.RenderHtml(second);
        Assert.Equal(2, renderer.Calls);
    }

    [Fact]
    public void SixtyMebibytePdfFitsDefaultExpandedByteBudgetWithoutFullFrameCopies()
    {
        using var credentials = new PdfWorkerCredentials("tenant_a", Key);
        using var fixture = new SyntaxFixtures();
        long a = fixture.Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        long b = fixture.Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        long d = fixture.Object(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << >> >>");
        long e = fixture.StreamObject(4, "", new byte[60 * 1024 * 1024]);
        fixture.Classic(new[] { (0, 65535, 0L, true), (1, 0, a, false), (2, 0, b, false), (3, 0, d, false), (4, 0, e, false) }, "/Root 1 0 R", 5);
        var renderer = new LargeRenderer(fixture.Bytes.ToArray());
        // Keep the default byte ceilings; allow slow Framework CI hardware more elapsed time.
        var policy = new PdfOperationPolicy(limits: new(maximumElapsedTime: TimeSpan.FromMinutes(3)));
        var service = new PdfRenderWorkerService(new[] { new PdfWorkerTenant(credentials, maximumElapsedTime: TimeSpan.FromMinutes(3)) }, new(renderer), policy);
        using var markup = Input();
        var result = new PdfRenderWorkerClient(credentials, new DirectTransport(service)).RenderHtml(markup, policy);
        using var pdf = result.OpenPdfStream();
        Assert.Equal(renderer.Bytes.Length, pdf.Length);
        using var hash = System.Security.Cryptography.SHA256.Create();
        Assert.Equal(hash.ComputeHash(renderer.Bytes), hash.ComputeHash(pdf));
    }

    private sealed class DirectTransport : IPdfRenderWorkerTransport
    {
        private readonly PdfRenderWorkerService _service;
        internal DirectTransport(PdfRenderWorkerService service) => _service = service;
        public void Exchange(Stream request, Stream response, PdfOperationContext context) => _service.Process(request, response, context.CancellationToken);
    }

    private sealed class LargeRenderer : IPdfHtmlRenderer
    {
        internal readonly byte[] Bytes;
        internal LargeRenderer(byte[] bytes) => Bytes = bytes;
        public PdfProviderDescriptor Descriptor { get; } = new("fixture.large-pdf", "1", PdfCapability.RenderHtml);

        public void Render(PdfHtmlSource source, PdfHtmlOptions options, Stream output, PdfOperationContext context) => output.Write(Bytes, 0, Bytes.Length);
    }

    private sealed class Renderer : IPdfHtmlRenderer
    {
        internal bool Fail, Invalid;
        internal int Calls;
        internal string? Markup;
        internal PdfOperationContext? Context;
        internal Action<PdfOperationContext>? Before;
        private readonly byte[] _pdf = Create();
        private static byte[] Create()
        {
            using var document = Pdf.Create().AddPage();
            using var output = new MemoryStream();
            document.Save(output);
            return output.ToArray();
        }

        public PdfProviderDescriptor Descriptor { get; } = new("fixture.worker-browser", "1", PdfCapability.RenderHtml);

        public void Render(PdfHtmlSource source, PdfHtmlOptions options, Stream output, PdfOperationContext context)
        {
            Interlocked.Increment(ref Calls);
            Markup = source.Markup;
            Context = context;
            Assert.False(options.EnableJavaScript);
            Before?.Invoke(context);
            context.Checkpoint();
            if (Fail)
                throw new IOException("secret-worker-model");
            if (Invalid)
            {
                output.WriteByte(1);
                return;
            }

            output.Write(_pdf, 0, _pdf.Length);
        }
    }

    private sealed class Loopback : IPdfRenderWorkerTransport
    {
        private readonly PdfRenderWorkerService _service;
        internal int Calls, Transient;
        internal bool TamperRequest, TamperResponse, Replay;
        internal Stream? Request, Response;
        internal byte[]? LastReply;
        internal long LastResponseLength;
        internal readonly List<byte[]> Requests = new();
        private readonly object _gate = new();
        internal Loopback(PdfRenderWorkerService service) => _service = service;
        public void Exchange(Stream request, Stream response, PdfOperationContext context)
        {
            Interlocked.Increment(ref Calls);
            Request = request;
            Response = response;
            using var captured = new MemoryStream();
            request.CopyTo(captured);
            var bytes = captured.ToArray();
            lock (_gate)
                Requests.Add(bytes);
            if (Transient > 0)
            {
                Transient--;
                throw new PdfWorkerTransientException();
            }

            if (Replay)
            {
                response.Write(LastReply!, 0, LastReply!.Length);
                return;
            }

            if (TamperRequest)
                bytes[bytes.Length - 1] ^= 1;
            using var source = new MemoryStream(bytes);
            using var output = new MemoryStream();
            try
            {
                _service.Process(source, output, context.CancellationToken);
            }
            finally
            {
                LastResponseLength = output.Length;
            }

            var reply = output.ToArray();
            if (TamperResponse)
                reply[reply.Length - 1] ^= 1;
            LastReply = reply;
            response.Write(reply, 0, reply.Length);
        }
    }
}
