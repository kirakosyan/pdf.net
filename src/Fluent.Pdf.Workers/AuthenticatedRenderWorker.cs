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

    internal byte[] Authenticate(Stream input, PdfOperationContext c)
    {
        c.Checkpoint();
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        lock (_gate)
        {
            if (_key == null)
                throw PdfWriterCore.Error(c, "Worker.CredentialExpired", "The caller-owned worker credential has been disposed.");
            using var hmac = new HMACSHA256(_key);
            using var lease = new BorrowedStream(input, true, c, null);
            return hmac.ComputeHash(lease);
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
                using var input = frame.Open();
                using var request = new BorrowedStream(input, true, c, null);
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
            var reply = WorkerWire.Decode(store, 2, _credentials, c);
            if (!WorkerWire.Equal(nonce, reply.Nonce) || !WorkerWire.Equal(digest, reply.Digest))
                throw PdfWriterCore.Error(c, "Worker.Binding", "The authenticated response does not match the exact request.");
            var pdf = reply.Payload;
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
        internal readonly HashSet<string> Nonces = new(StringComparer.Ordinal);
        internal readonly object Gate = new();
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
            Accept(slot, message.Nonce, c);
            try
            {
                using var deadline = c.EnterTimeLimit(slot.Options.MaximumElapsedTime);
                using var input = message.Payload.Open(0, message.Payload.Length);
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
                var encoded = WorkerWire.Encode(2, slot.Options.Credentials, message.Nonce, message.Digest, pdf, c);
                using var body = encoded.Open();
                WorkerWire.Copy(body, response, c);
            }
            finally
            {
                Interlocked.Decrement(ref slot.Active);
            }
        }, PdfCapability.RenderHtml);
    }

    private static void Accept(Slot slot, byte[] nonce, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 256);
        string identity = BitConverter.ToString(nonce);
        lock (slot.Gate)
        {
            c.Checkpoint();
            if (slot.Nonces.Contains(identity))
                throw PdfWriterCore.Error(c, "Worker.Replay", "This authenticated request nonce has already been accepted by this service instance.");
            if (slot.Jobs >= slot.Options.MaximumJobs || Volatile.Read(ref slot.Active) >= slot.Options.MaximumConcurrentJobs)
                throw PdfWriterCore.Error(c, "Worker.TenantQuota", "The authenticated tenant job/concurrency quota was reached.");
            slot.Nonces.Add(identity);
            slot.Jobs++;
            Interlocked.Increment(ref slot.Active);
        }
    }
}

internal static class WorkerWire
{
    internal sealed class Message
    {
        internal byte[] Nonce = null!, Digest = null!;
        internal PdfByteStore Payload = null!;
    }

    internal sealed class Frame
    {
        internal byte[] Header = null!, Mac = null!;
        internal PdfByteStore Payload = null!;
        internal Stream Open() => new PdfReadSequenceStream(false, new MemoryStream(Header, false), Payload.Open(0, Payload.Length), new MemoryStream(Mac, false));
    }

    internal static byte[] Digest(PdfByteStore bytes, PdfOperationContext c)
    {
        c.Checkpoint();
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        using var sha = SHA256.Create();
        using var input = bytes.Open(0, bytes.Length);
        using var lease = new BorrowedStream(input, true, c, null);
        var result = sha.ComputeHash(lease);
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

    internal static Frame Encode(byte kind, PdfWorkerCredentials credentials, byte[] nonce, byte[] digest, PdfByteStore payload, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, credentials.Tenant.Length + 512L);
        var header = new byte[59 + credentials.Tenant.Length];
        int at = 0;
        header[at++] = 70;
        header[at++] = 80;
        header[at++] = 87;
        header[at++] = 83;
        header[at++] = 1;
        header[at++] = kind;
        header[at++] = (byte)credentials.Tenant.Length;
        foreach (char ch in credentials.Tenant)
            header[at++] = (byte)ch;
        Buffer.BlockCopy(nonce, 0, header, at, 16);
        at += 16;
        Buffer.BlockCopy(digest, 0, header, at, 32);
        at += 32;
        for (int shift = 0; shift < 32; shift += 8)
            header[at++] = (byte)(payload.Length >> shift);
        using var input = new PdfReadSequenceStream(false, new MemoryStream(header, false), payload.Open(0, payload.Length));
        return new()
        {
            Header = header,
            Payload = payload,
            Mac = credentials.Authenticate(input, c)
        };
    }

    internal static string Tenant(PdfByteStore bytes, PdfOperationContext c)
    {
        if (bytes.Length < 93 || bytes.At(0) != 70 || bytes.At(1) != 80 || bytes.At(2) != 87 || bytes.At(3) != 83 || bytes.At(4) != 1 || bytes.At(6) < 1 || bytes.At(6) > 64 || bytes.Length < 92 + bytes.At(6))
            throw PdfWriterCore.Error(c, "Worker.Protocol", "A complete finite version-one worker frame is required.");
        c.Consume(PdfResourceKind.ExpandedBytes, bytes.At(6) * 4L + 64);
        var name = new byte[bytes.At(6)];
        for (int i = 0; i < name.Length; i++)
            name[i] = (byte)bytes.At(7 + i);
        return Encoding.ASCII.GetString(name);
    }

    internal static Message Decode(PdfByteStore bytes, byte kind, PdfWorkerCredentials credentials, PdfOperationContext c)
    {
        string tenant = Tenant(bytes, c);
        if (tenant != credentials.Tenant || bytes.At(5) != kind)
            throw PdfWriterCore.Error(c, "Worker.Authentication", "The worker frame has a different authenticated tenant/direction.");
        using var signed = bytes.Open(0, bytes.Length - 32);
        var expected = credentials.Authenticate(signed, c);
        c.Consume(PdfResourceKind.ExpandedBytes, 256);
        var actual = new byte[32];
        for (int i = 0; i < 32; i++)
            actual[i] = (byte)bytes.At(bytes.Length - 32 + i);
        if (!Equal(expected, actual))
            throw PdfWriterCore.Error(c, "Worker.Authentication", "The worker frame authentication failed.");
        int at = 7 + bytes.At(6);
        var message = new Message
        {
            Nonce = new byte[16],
            Digest = new byte[32]
        };
        for (int i = 0; i < 16; i++)
            message.Nonce[i] = (byte)bytes.At(at++);
        for (int i = 0; i < 32; i++)
            message.Digest[i] = (byte)bytes.At(at++);
        uint count = 0;
        for (int shift = 0; shift < 32; shift += 8)
            count |= (uint)bytes.At(at++) << shift;
        if (count == 0 || count > int.MaxValue || count != bytes.Length - at - 32)
            throw PdfWriterCore.Error(c, "Worker.Protocol", "The authenticated payload extent is invalid.");
        message.Payload = bytes.Slice(at, count);
        return message;
    }

    internal static PdfByteStore Capture(Stream input, int maximum, PdfOperationContext c)
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

        return output.Finish();
    }

    internal static void Copy(PdfByteStore bytes, Stream output, PdfOperationContext c)
    {
        using var input = bytes.Open(0, bytes.Length);
        Copy(input, output, c);
    }

    internal static void Copy(Stream input, Stream output, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var chunk = new byte[8192];
        using var target = new BorrowedStream(output, false, c);
        int n;
        while ((n = input.Read(chunk, 0, chunk.Length)) > 0)
        {
            c.Checkpoint();
            target.Write(chunk, 0, n);
        }
    }
}
