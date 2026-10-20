using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Fluent.Pdf.Workers;
/// <summary>Caller-owned per-tenant shared secret. Authentication does not provide transport confidentiality; use a protected local channel or authenticated TLS transport.</summary>
public sealed class PdfWorkerCredentials : IDisposable
{
    private readonly object _gate = new();
    private byte[]? _key;
    /// <summary>Captures a 32–64-byte authentication key and a bounded ASCII tenant identity. No credential is discovered or logged.</summary>
    public PdfWorkerCredentials(string tenant, byte[] authenticationKey)
    {
        if (tenant == null || tenant.Length < 1 || tenant.Length > 64 || tenant.Any(ch => !(ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || ch == '-' || ch == '_')))
            throw new ArgumentException("A bounded ASCII tenant identity is required.", nameof(tenant));
        if (authenticationKey == null || authenticationKey.Length < 32 || authenticationKey.Length > 64)
            throw new ArgumentException("An explicit 32–64-byte authentication key is required.", nameof(authenticationKey));
        Tenant = tenant;
        _key = (byte[])authenticationKey.Clone();
    }

    /// <summary>Gets the exact tenant identity.</summary>
    public string Tenant { get; }

    internal byte[] Authenticate(byte[] bytes, int count, PdfOperationContext c)
    {
        c.Checkpoint();
        c.Consume(PdfResourceKind.ExpandedBytes, 512);
        lock (_gate)
        {
            if (_key == null)
                throw PdfWriterCore.Error(c, "Worker.CredentialExpired", "The caller-owned worker credential has been disposed.");
            using var hmac = new HMACSHA256(_key);
            return hmac.ComputeHash(bytes, 0, count);
        }
    }

    /// <summary>Clears the captured key. Drain outstanding work before disposing caller-owned credentials.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_key != null)
                Array.Clear(_key, 0, _key.Length);
            _key = null;
        }
    }
}

/// <summary>Explicit finite server-side tenant quotas. A new service instance is an explicit quota reset; no clock-driven reset or ambient account discovery occurs.</summary>
public sealed class PdfWorkerTenant
{
    /// <summary>Captures authenticated job/count/input/output/time ceilings. Static HTML input is at most 8 MiB; generated PDF is at most 64 MiB.</summary>
    public PdfWorkerTenant(PdfWorkerCredentials credentials, int maximumConcurrentJobs = 1, int maximumJobs = 1000, int maximumInputBytes = 8388608, int maximumOutputBytes = 67108864, TimeSpan? maximumElapsedTime = null)
    {
        Credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        if (maximumConcurrentJobs < 1 || maximumConcurrentJobs > 64 || maximumJobs < 1 || maximumJobs > 1000000 || maximumInputBytes < 1 || maximumInputBytes > 8388608 || maximumOutputBytes < 1 || maximumOutputBytes > 67108864)
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrentJobs));
        MaximumElapsedTime = maximumElapsedTime ?? TimeSpan.FromSeconds(30);
        if (MaximumElapsedTime <= TimeSpan.Zero || MaximumElapsedTime > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(maximumElapsedTime));
        MaximumConcurrentJobs = maximumConcurrentJobs;
        MaximumJobs = maximumJobs;
        MaximumInputBytes = maximumInputBytes;
        MaximumOutputBytes = maximumOutputBytes;
    }

    /// <summary>Gets caller-owned credentials; the service never disposes them.</summary>
    public PdfWorkerCredentials Credentials { get; }
    /// <summary>Gets active-job ceiling.</summary>
    public int MaximumConcurrentJobs { get; }
    /// <summary>Gets accepted job/attempt ceiling for this service lifetime.</summary>
    public int MaximumJobs { get; }
    /// <summary>Gets exact source-byte ceiling.</summary>
    public int MaximumInputBytes { get; }
    /// <summary>Gets generated-PDF byte ceiling.</summary>
    public int MaximumOutputBytes { get; }
    /// <summary>Gets the tightened job deadline.</summary>
    public TimeSpan MaximumElapsedTime { get; }
}

/// <summary>Caller-owned protected worker transport. Route to an explicitly provisioned process/service and map its stable failures; do not download an engine or redirect credentials.</summary>
public interface IPdfRenderWorkerTransport
{
    /// <summary>Exchanges one authenticated request/response. Poll the context, preserve endpoint/tenant isolation and expire all stream work before returning.</summary>
    void Exchange(Stream request, Stream response, PdfOperationContext context);
}

/// <summary>Explicit transient transport marker; only the idempotent static-render operation may retry it, within the caller-selected attempt ceiling.</summary>
public sealed class PdfWorkerTransientException : IOException
{
    /// <summary>Creates a credential-free transient marker.</summary>
    public PdfWorkerTransientException() : base("The configured worker transport reported a transient failure.")
    {
    }
}

/// <summary>Completed immutable worker PDF and its client operation evidence.</summary>
public sealed class PdfWorkerPdf
{
    private readonly PdfByteStore _bytes;
    internal PdfWorkerPdf(PdfByteStore bytes, PdfOperationReport report)
    {
        _bytes = bytes;
        OperationReport = report;
    }

    /// <summary>Gets completed client evidence; it does not qualify the remote renderer.</summary>
    public PdfOperationReport OperationReport { get; }

    /// <summary>Opens a fresh read-only completed PDF view.</summary>
    public Stream OpenPdfStream() => _bytes.Open(0, _bytes.Length);
}

/// <summary>Authenticated static-HTML render client with exact request/tenant/nonce/input binding and explicit bounded idempotent retries. Caller owns credentials and transport.</summary>
public sealed class PdfRenderWorkerClient : IPdfHtmlRenderer
{
    private readonly PdfWorkerCredentials _credentials;
    private readonly IPdfRenderWorkerTransport _transport;
    private readonly int _attempts;
    /// <summary>Selects explicit credentials/transport and one to three attempts. Retries are disabled by default and apply only to the transient marker.</summary>
    public PdfRenderWorkerClient(PdfWorkerCredentials credentials, IPdfRenderWorkerTransport transport, int maximumAttempts = 1)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        if (maximumAttempts < 1 || maximumAttempts > 3)
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        _attempts = maximumAttempts;
    }

    /// <summary>Gets the explicit static-render service identity, without host qualification claims.</summary>
    public PdfProviderDescriptor Descriptor { get; } = new("managed.authenticated-render-client", "1", PdfCapability.RenderHtml);

    /// <summary>Authenticates one bounded UTF8 HTML source and returns only an authenticated, nonempty, structurally parsed PDF.</summary>
    public PdfWorkerPdf RenderHtml(Stream markup, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (markup == null || !markup.CanRead)
            throw new ArgumentException("A readable borrowed HTML stream is required.", nameof(markup));
        PdfByteStore? pdf = null;
        var report = PdfFont.Run("RenderWorkerHtml", Descriptor.Id, policy, cancellationToken, c => pdf = Execute(markup, c), PdfCapability.RenderHtml);
        return new(pdf!, report);
    }

    /// <summary>Implements only default offline static HTML options and virtual-origin markup without resource maps. Other browser environments/options reject before transport.</summary>
    public void Render(PdfHtmlSource source, PdfHtmlOptions options, Stream output, PdfOperationContext context)
    {
        if (source == null || options == null || output == null || context == null)
            throw new ArgumentNullException(source == null ? nameof(source) : options == null ? nameof(options) : output == null ? nameof(output) : nameof(context));
        if (source.Markup == null || source.Resources.Count != 0 || source.Address.Scheme != "https" || source.Address.Host != "fluent-pdf.invalid" || !source.Address.IsDefaultPort || !options.PageSize.Equals(PdfPageSize.A4) || options.ViewportWidth != 1280 || options.ViewportHeight != 720 || options.EnableJavaScript || !options.PrintBackground || options.MaximumPages != 1000 || options.Locale != "en-US" || options.TimeZone != "UTC" || options.Media != PdfHtmlMedia.Print || !ReferenceEquals(options.Readiness, PdfHtmlReadinessOptions.Default) || !ReferenceEquals(options.Print, PdfHtmlPrintOptions.Default))
            throw PdfWriterCore.Error(context, "Worker.OptionsUnsupported", "This worker profile requires default offline static HTML, a virtual origin, and no caller resource map.");
        context.Consume(PdfResourceKind.ExpandedBytes, source.Markup.Length * 4L + 64);
        using var input = new MemoryStream(new UTF8Encoding(false, true).GetBytes(source.Markup), false);
        var pdf = Execute(input, context);
        WorkerWire.Copy(pdf, output, context);
    }

    private PdfByteStore Execute(Stream markup, PdfOperationContext c)
    {
        var payload = WorkerWire.Capture(markup, 8388608, c);
        if (payload.Length == 0)
            throw PdfWriterCore.Error(c, "Worker.EmptyInput", "A worker request requires explicit source bytes.");
        var digest = WorkerWire.Digest(payload, c);
        c.Consume(PdfResourceKind.ExpandedBytes, 16);
        var nonce = new byte[16];
        using (var random = RandomNumberGenerator.Create())
            random.GetBytes(nonce);
        var frame = WorkerWire.Encode(1, _credentials, nonce, digest, payload, c);
        for (int attempt = 1;; attempt++)
        {
            c.Checkpoint();
            using var response = new PdfCodecBufferStream(c, 67108864 + 256);
            try
            {
                using var input = new MemoryStream(frame, false);
                using var request = c.BorrowExpandedInput(input);
                using var output = new BorrowedStream(response, false, c);
                using var worker = c.EnterScope(PdfResourceKind.Workers);
                _transport.Exchange(request, output, c);
            }
            catch (PdfWorkerTransientException)when (attempt < _attempts)
            {
                c.Checkpoint();
                continue;
            }
            catch (Exception error)when (!(error is PdfException) && !(error is OperationCanceledException))
            {
                c.Checkpoint();
                throw PdfWriterCore.Error(c, "Worker.Transport", "The explicit worker transport failed; no credentials or raw transport text are exposed.");
            }

            c.Checkpoint();
            var store = response.Finish();
            using var captured = store.Open(0, store.Length);
            var reply = WorkerWire.Decode(WorkerWire.Capture(captured, 67108864 + 256, c), 2, _credentials, c);
            if (!WorkerWire.Equal(nonce, reply.Nonce) || !WorkerWire.Equal(digest, reply.Digest))
                throw PdfWriterCore.Error(c, "Worker.Binding", "The authenticated response does not match the exact request.");
            var pdf = WorkerWire.Store(reply.Payload, c);
            using var checkedDocument = PdfPageOrganization.Read(pdf, 1000, c);
            if (checkedDocument.Pages.Count == 0)
                throw PdfWriterCore.Error(c, "Worker.EmptyOutput", "A worker cannot substitute an empty document.");
            return pdf;
        }
    }
}

/// <summary>Original authenticated static-HTML worker protocol handler with finite per-tenant concurrency/jobs/bytes/deadlines. Hosting, transport protection and isolated engine provisioning remain explicit.</summary>
public sealed class PdfRenderWorkerService
{
    private sealed class Slot
    {
        internal readonly PdfWorkerTenant Options;
        internal int Active, Jobs;
        internal Slot(PdfWorkerTenant options) => Options = options;
    }

    private readonly Dictionary<string, Slot> _tenants = new(StringComparer.Ordinal);
    private readonly IPdfHtmlRenderer _renderer;
    private readonly PdfOperationPolicy _policy;
    private readonly int _maximumInput;
    /// <summary>Captures at most64 explicit tenant registrations and one provisioned renderer. It never discovers a process/engine or downloads dependencies.</summary>
    public PdfRenderWorkerService(IEnumerable<PdfWorkerTenant> tenants, PdfProviderRegistry providers, PdfOperationPolicy? policy = null)
    {
        if (tenants == null || providers == null)
            throw new ArgumentNullException(tenants == null ? nameof(tenants) : nameof(providers));
        _renderer = providers.GetRequired<IPdfHtmlRenderer>(PdfCapability.RenderHtml);
        _policy = policy ?? PdfOperationPolicy.Default;
        foreach (var tenant in tenants)
        {
            if (tenant == null || _tenants.Count == 64 || _tenants.ContainsKey(tenant.Credentials.Tenant))
                throw new ArgumentException("At most64 unique tenant registrations are required.", nameof(tenants));
            _tenants.Add(tenant.Credentials.Tenant, new(tenant));
            _maximumInput = Math.Max(_maximumInput, tenant.MaximumInputBytes);
        }

        if (_tenants.Count == 0)
            throw new ArgumentException("An explicit tenant registration is required.", nameof(tenants));
    }

    /// <summary>Authenticates before rendering, enforces quotas and validates the complete PDF before returning an authenticated response. Caller streams stay open; host maps failures without leaking credentials.</summary>
    public PdfOperationReport Process(Stream request, Stream response, CancellationToken cancellationToken = default)
    {
        if (request == null || !request.CanRead || response == null || !response.CanWrite)
            throw new ArgumentException("Readable/writable borrowed worker streams are required.");
        return PdfFont.Run("ServeStaticHtmlWorker", "managed.authenticated-render-service", _policy, cancellationToken, c =>
        {
            var frame = WorkerWire.Capture(request, _maximumInput + 256, c);
            string tenant = WorkerWire.Tenant(frame, c);
            if (!_tenants.TryGetValue(tenant, out var slot))
                throw PdfWriterCore.Error(c, "Worker.Authentication", "The worker request is not authenticated for a configured tenant.");
            var message = WorkerWire.Decode(frame, 1, slot.Options.Credentials, c);
            if (message.Payload.Length == 0 || message.Payload.Length > slot.Options.MaximumInputBytes)
                throw PdfWriterCore.Error(c, "Worker.TenantQuota", "The authenticated tenant input quota was reached.");
            if (!WorkerWire.Equal(WorkerWire.Digest(message.Payload, c), message.Digest))
                throw PdfWriterCore.Error(c, "Worker.Binding", "The authenticated source digest does not match its bytes.");
            Acquire(slot, c);
            try
            {
                using var deadline = c.EnterTimeLimit(slot.Options.MaximumElapsedTime);
                ChargeJob(slot, c);
                using var input = new MemoryStream(message.Payload, false);
                var source = PdfHtmlSource.CreateCore(null, new Uri("https://fluent-pdf.invalid/document"), null, input, c);
                using var output = new PdfCodecBufferStream(c, slot.Options.MaximumOutputBytes);
                try
                {
                    using var target = new BorrowedStream(output, false, c);
                    _renderer.Render(source, new PdfHtmlOptions(), target, c);
                }
                catch (Exception error)when (!(error is PdfException) && !(error is OperationCanceledException))
                {
                    c.Checkpoint();
                    throw PdfWriterCore.Error(c, "Worker.RenderFailed", "The provisioned worker renderer failed; source and raw provider exception text are withheld.");
                }

                c.Checkpoint();
                var pdf = output.Finish();
                using var checkedDocument = PdfPageOrganization.Read(pdf, 1000, c);
                if (checkedDocument.Pages.Count == 0)
                    throw PdfWriterCore.Error(c, "Worker.EmptyOutput", "A worker cannot substitute an empty document.");
                using var body = pdf.Open(0, pdf.Length);
                var payload = WorkerWire.Capture(body, slot.Options.MaximumOutputBytes, c);
                var encoded = WorkerWire.Encode(2, slot.Options.Credentials, message.Nonce, message.Digest, payload, c);
                WorkerWire.Copy(WorkerWire.Store(encoded, c), response, c);
            }
            finally
            {
                Interlocked.Decrement(ref slot.Active);
            }
        }, PdfCapability.RenderHtml);
    }

    private static void Acquire(Slot slot, PdfOperationContext c)
    {
        while (true)
        {
            c.Checkpoint();
            int current = Volatile.Read(ref slot.Active);
            if (current >= slot.Options.MaximumConcurrentJobs)
                throw PdfWriterCore.Error(c, "Worker.TenantQuota", "The authenticated tenant concurrency quota was reached.");
            if (Interlocked.CompareExchange(ref slot.Active, current + 1, current) == current)
                return;
        }
    }

    private static void ChargeJob(Slot slot, PdfOperationContext c)
    {
        while (true)
        {
            c.Checkpoint();
            int current = Volatile.Read(ref slot.Jobs);
            if (current >= slot.Options.MaximumJobs)
                throw PdfWriterCore.Error(c, "Worker.TenantQuota", "The authenticated tenant job quota was reached.");
            if (Interlocked.CompareExchange(ref slot.Jobs, current + 1, current) == current)
                return;
        }
    }
}

internal static class WorkerWire
{
    internal sealed class Message
    {
        internal byte[] Nonce = null!, Digest = null!, Payload = null!;
    }

    internal static byte[] Digest(byte[] bytes, PdfOperationContext c)
    {
        c.Checkpoint();
        c.Consume(PdfResourceKind.ExpandedBytes, 32);
        using var sha = SHA256.Create();
        var result = sha.ComputeHash(bytes);
        c.Checkpoint();
        return result;
    }

    internal static bool Equal(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;
        int difference = 0;
        for (int i = 0; i < a.Length; i++)
            difference |= a[i] ^ b[i];
        return difference == 0;
    }

    internal static byte[] Encode(byte kind, PdfWorkerCredentials credentials, byte[] nonce, byte[] digest, byte[] payload, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, payload.Length + credentials.Tenant.Length + 256L);
        var frame = new byte[91 + credentials.Tenant.Length + payload.Length];
        int at = 0;
        frame[at++] = 70;
        frame[at++] = 80;
        frame[at++] = 87;
        frame[at++] = 83;
        frame[at++] = 1;
        frame[at++] = kind;
        frame[at++] = (byte)credentials.Tenant.Length;
        foreach (char ch in credentials.Tenant)
            frame[at++] = (byte)ch;
        Buffer.BlockCopy(nonce, 0, frame, at, 16);
        at += 16;
        Buffer.BlockCopy(digest, 0, frame, at, 32);
        at += 32;
        for (int shift = 0; shift < 32; shift += 8)
            frame[at++] = (byte)(payload.Length >> shift);
        Buffer.BlockCopy(payload, 0, frame, at, payload.Length);
        at += payload.Length;
        var mac = credentials.Authenticate(frame, at, c);
        Buffer.BlockCopy(mac, 0, frame, at, mac.Length);
        return frame;
    }

    internal static string Tenant(byte[] bytes, PdfOperationContext c)
    {
        if (bytes.Length < 93 || bytes[0] != 70 || bytes[1] != 80 || bytes[2] != 87 || bytes[3] != 83 || bytes[4] != 1 || bytes[6] < 1 || bytes[6] > 64 || bytes.Length < 92 + bytes[6])
            throw PdfWriterCore.Error(c, "Worker.Protocol", "A complete finite version-one worker frame is required.");
        c.Consume(PdfResourceKind.ExpandedBytes, bytes[6] * 4L + 64);
        return Encoding.ASCII.GetString(bytes, 7, bytes[6]);
    }

    internal static Message Decode(byte[] bytes, byte kind, PdfWorkerCredentials credentials, PdfOperationContext c)
    {
        string tenant = Tenant(bytes, c);
        if (tenant != credentials.Tenant || bytes[5] != kind)
            throw PdfWriterCore.Error(c, "Worker.Authentication", "The worker frame has a different authenticated tenant/direction.");
        var expected = credentials.Authenticate(bytes, bytes.Length - 32, c);
        c.Consume(PdfResourceKind.ExpandedBytes, 96);
        var actual = new byte[32];
        Buffer.BlockCopy(bytes, bytes.Length - 32, actual, 0, 32);
        if (!Equal(expected, actual))
            throw PdfWriterCore.Error(c, "Worker.Authentication", "The worker frame authentication failed.");
        int at = 7 + bytes[6];
        var message = new Message
        {
            Nonce = new byte[16],
            Digest = new byte[32]
        };
        Buffer.BlockCopy(bytes, at, message.Nonce, 0, 16);
        at += 16;
        Buffer.BlockCopy(bytes, at, message.Digest, 0, 32);
        at += 32;
        uint count = (uint)bytes[at] | (uint)bytes[at + 1] << 8 | (uint)bytes[at + 2] << 16 | (uint)bytes[at + 3] << 24;
        at += 4;
        if (count == 0 || count > int.MaxValue || count != bytes.Length - at - 32)
            throw PdfWriterCore.Error(c, "Worker.Protocol", "The authenticated payload extent is invalid.");
        c.Consume(PdfResourceKind.ExpandedBytes, count);
        message.Payload = new byte[(int)count];
        Buffer.BlockCopy(bytes, at, message.Payload, 0, (int)count);
        return message;
    }

    internal static byte[] Capture(Stream input, int maximum, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var chunk = new byte[8192];
        using var source = new BorrowedStream(input, true, c);
        using var output = new PdfCodecBufferStream(c, maximum);
        while (true)
        {
            c.Checkpoint();
            int n = source.Read(chunk, 0, (int)Math.Min(chunk.Length, maximum - output.BufferedLength + 1));
            if (n == 0)
                break;
            if (output.BufferedLength + n > maximum)
                throw PdfWriterCore.Error(c, "Worker.Extent", "The explicit worker frame/source ceiling was exceeded.");
            output.Write(chunk, 0, n);
        }

        var bytes = output.Finish();
        c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length);
        var result = new byte[(int)bytes.Length];
        using var captured = bytes.Open(0, bytes.Length);
        int position = 0;
        while (position < result.Length)
        {
            c.Checkpoint();
            int n = captured.Read(result, position, result.Length - position);
            if (n == 0)
                throw new EndOfStreamException();
            position += n;
        }

        return result;
    }

    internal static PdfByteStore Store(byte[] bytes, PdfOperationContext c)
    {
        using var input = new MemoryStream(bytes, false);
        return PdfByteStore.Read(input, c);
    }

    internal static void Copy(PdfByteStore bytes, Stream output, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var chunk = new byte[8192];
        using var input = bytes.Open(0, bytes.Length);
        using var target = new BorrowedStream(output, false, c);
        int n;
        while ((n = input.Read(chunk, 0, chunk.Length)) > 0)
        {
            c.Checkpoint();
            target.Write(chunk, 0, n);
        }
    }
}
