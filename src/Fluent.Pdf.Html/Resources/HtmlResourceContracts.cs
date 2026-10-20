using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Fluent.Pdf.Html;
/// <summary>One exact resource request and browser redirect-chain identity. No browser headers or credentials are forwarded implicitly.</summary>
public sealed class HtmlResourceRequest
{
    /// <summary>Captures an absolute resource address and bounded request/previous-request identities.</summary>
    public HtmlResourceRequest(Uri address, string requestId, string? redirectedRequestId = null, bool isMainDocument = false)
    {
        if (address == null || !address.IsAbsoluteUri || address.OriginalString.Length > 16777216 || address.UserInfo.Length != 0)
            throw new ArgumentException("An absolute bounded resource address without user information is required.", nameof(address));
        if (string.IsNullOrEmpty(requestId) || requestId.Length > 256 || redirectedRequestId?.Length > 256)
            throw new ArgumentException("Bounded request identities are required.", nameof(requestId));
        Address = address;
        RequestId = requestId;
        RedirectedRequestId = redirectedRequestId;
        IsMainDocument = isMainDocument;
    }

    /// <summary>Gets the exact absolute address.</summary>
    public Uri Address { get; }
    /// <summary>Gets this browser request's identity.</summary>
    public string RequestId { get; }
    /// <summary>Gets its previous redirect request identity, if any.</summary>
    public string? RedirectedRequestId { get; }
    /// <summary>Gets whether this request is the main document or its redirect continuation.</summary>
    public bool IsMainDocument { get; }
}

/// <summary>Immutable captured content/status/headers. Content bytes have already passed the operation's capture/expansion boundary.</summary>
public sealed class HtmlResourceResult
{
    /// <summary>Captures an immutable resource and response metadata with the mandatory operation budget.</summary>
    public HtmlResourceResult(PdfHtmlResource content, int statusCode, IEnumerable<KeyValuePair<string, string>>? headers, PdfOperationContext context)
    {
        if (content == null)
            throw new ArgumentNullException(nameof(content));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (statusCode < 200 || statusCode > 599)
            throw new ArgumentOutOfRangeException(nameof(statusCode));
        Content = content;
        StatusCode = statusCode;
        Headers = HtmlResourceHeaders.Capture(headers, context);
    }

    /// <summary>Gets the captured immutable bytes and explicit media type.</summary>
    public PdfHtmlResource Content { get; }
    /// <summary>Gets the explicit HTTP-style response status.</summary>
    public int StatusCode { get; }
    /// <summary>Gets bounded response headers. Values can contain credentials and must not be logged.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers { get; }
}

/// <summary>A private resource/authentication session for one browser job. Implementations must preserve supplied policies and complete all work before disposal returns.</summary>
public interface IHtmlResourceSession : IDisposable
{
    /// <summary>Resolves explicit resource policy under the shared context, or returns null for an unprovided asset. Security denials must remain fatal in both modes.</summary>
    HtmlResourceResult? Resolve(HtmlResourceRequest request, PdfOperationContext context);
}

/// <summary>Creates a fresh resource/cookie/credential session for one job; a shared mutable authentication session is forbidden.</summary>
public delegate IHtmlResourceSession HtmlResourceSessionFactory(PdfOperationContext context);
/// <summary>An explicit DNS provider. The loader validates every returned address and passes one exact address to the transport, which must not resolve it again.</summary>
public interface IHtmlNameResolver
{
    /// <summary>Returns a bounded address inventory using the mandatory cancellation/deadline. No lookup is permitted before origin authorization.</summary>
    IReadOnlyList<IPAddress> Resolve(string host, PdfOperationContext context);
}

/// <summary>A policy-approved HTTP GET with exact connection addresses and origin-scoped credentials.</summary>
public sealed class HtmlHttpRequest
{
    internal HtmlHttpRequest(Uri address, string destination, Dictionary<string, string> headers, HtmlOriginRule rule, HtmlProxySettings? proxy, string? proxyAddress)
    {
        Address = address;
        DestinationAddress = destination;
        Headers = new ReadOnlyDictionary<string, string>(headers);
        ServerCertificateSha256 = rule.ServerCertificateSha256;
        ClientCertificate = rule.ClientCertificate;
        Proxy = proxy;
        ProxyAddress = proxyAddress;
    }

    /// <summary>Gets the original HTTP(S) address used for Host and TLS server-name validation.</summary>
    public Uri Address { get; }
    /// <summary>Gets the exact validated numeric destination. A transport must connect here without another DNS lookup.</summary>
    public string DestinationAddress { get; }
    /// <summary>Gets origin-scoped request headers. Values may be secret and must never enter diagnostics.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }
    /// <summary>Gets an optional explicit lowercase SHA-256 leaf-certificate pin. Host-name verification remains required.</summary>
    public string? ServerCertificateSha256 { get; }
    /// <summary>Gets the explicitly supplied caller-owned TLS client identity, or null. No store discovery or key export is authorized.</summary>
    public X509Certificate2? ClientCertificate { get; }
    /// <summary>Gets explicit proxy settings, or null. System/environment proxy discovery is forbidden.</summary>
    public HtmlProxySettings? Proxy { get; }
    /// <summary>Gets the exact validated numeric proxy address, or null.</summary>
    public string? ProxyAddress { get; }
}

/// <summary>Owned transport response. The resource loader disposes its body after bounded capture; this is not a caller-owned input stream.</summary>
public sealed class HtmlHttpResponse : IDisposable
{
    /// <summary>Captures owned readable body and bounded headers. Transport allocations and header acquisition must be charged before constructing this result.</summary>
    public HtmlHttpResponse(int statusCode, string mediaType, Stream body, IEnumerable<KeyValuePair<string, string>>? headers, PdfOperationContext context)
    {
        if (statusCode < 200 || statusCode > 599)
            throw new ArgumentOutOfRangeException(nameof(statusCode));
        if (string.IsNullOrEmpty(mediaType) || mediaType.Length > 256 || mediaType.Any(c => c < 32 || c > 126))
            throw new ArgumentException("A bounded ASCII media type is required.", nameof(mediaType));
        if (body == null || !body.CanRead)
            throw new ArgumentException("An owned readable response body is required.", nameof(body));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Consume(PdfResourceKind.Objects, 1);
        context.Consume(PdfResourceKind.ExpandedBytes, 256);
        StatusCode = statusCode;
        MediaType = mediaType;
        Body = body;
        Headers = HtmlResourceHeaders.Capture(headers, context);
    }

    /// <summary>Gets the response status.</summary>
    public int StatusCode { get; }
    /// <summary>Gets the explicit media type.</summary>
    public string MediaType { get; }
    /// <summary>Gets bounded headers, which can contain secrets and must not be logged.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers { get; }
    /// <summary>Gets the owned response stream for bounded capture.</summary>
    public Stream Body { get; }

    /// <summary>Closes the owned response/body and all transport work associated with it.</summary>
    public void Dispose() => Body.Dispose();
}

/// <summary>Explicit transport boundary. Redirects/cookies/proxies/decompression must not be applied implicitly; connect exactly to validated numeric endpoints.</summary>
public interface IHtmlHttpTransport
{
    /// <summary>Performs one policy-approved GET under shared budgets/cancellation. TLS must not download certificates/revocation data outside the resource policy. Returned response owns all request resources.</summary>
    HtmlHttpResponse Send(HtmlHttpRequest request, PdfOperationContext context);
}

internal static class HtmlResourceHeaders
{
    internal static IReadOnlyList<KeyValuePair<string, string>> Capture(IEnumerable<KeyValuePair<string, string>>? source, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 512);
        var result = new List<KeyValuePair<string, string>>();
        int length = 0;
        if (source != null)
            foreach (var pair in source)
            {
                context.Checkpoint();
                if (result.Count == 128 || !Name(pair.Key) || pair.Value == null || pair.Value.Length > 8192 || pair.Value.Any(c => c < 32 || c == 127) || (length += pair.Key.Length + pair.Value.Length) > 32768)
                    throw PdfWriterCore.Error(context, "Html.HeaderExtent", "Resource headers exceed their supported syntax or extent.");
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, (pair.Key.Length + pair.Value.Length) * 2L + 64);
                result.Add(pair);
            }

        return result.AsReadOnly();
    }

    internal static bool Name(string name) => !string.IsNullOrEmpty(name) && name.Length <= 128 && name.All(c => c > 32 && c < 127 && "()<>@,;:\\\"/[]?={} \t".IndexOf(c) < 0);
}
