using System.Collections.ObjectModel;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Fluent.Pdf.Html;
/// <summary>One explicitly authorized origin and its private header/cookie/TLS configuration. Nothing is inherited across origins or from the host account.</summary>
public sealed class HtmlOriginRule
{
    /// <summary>Captures exact origin authority, explicit headers/initial Set-Cookie values, optional leaf pin and caller-owned client certificate.</summary>
    public HtmlOriginRule(Uri origin, IEnumerable<KeyValuePair<string, string>>? headers = null, IEnumerable<string>? initialCookies = null, string? serverCertificateSha256 = null, X509Certificate2? clientCertificate = null)
    {
        Origin = HtmlNetworkPolicy.Origin(origin);
        if (serverCertificateSha256 != null && (serverCertificateSha256.Length != 64 || serverCertificateSha256.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))))
            throw new ArgumentException("An exact lowercase certificate SHA-256 is required.", nameof(serverCertificateSha256));
        var captured = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int total = 0;
        if (headers != null)
            foreach (var pair in headers)
            {
                if (captured.Count == 64 || !HtmlResourceHeaders.Name(pair.Key) || pair.Value == null || pair.Value.Length > 8192 || pair.Value.Any(c => c < 32 || c == 127) || new[]
                {
                    "Host",
                    "Connection",
                    "Cookie",
                    "Proxy-Authorization",
                    "Proxy-Connection",
                    "Content-Length",
                    "Transfer-Encoding",
                    "Upgrade",
                    "Expect",
                    "TE",
                    "Trailer",
                    "Accept-Encoding"
                }.Contains(pair.Key, StringComparer.OrdinalIgnoreCase) || captured.ContainsKey(pair.Key) || (total += pair.Key.Length + pair.Value.Length) > 32768)
                    throw new ArgumentException("Explicit origin headers exceed their supported syntax or extent.", nameof(headers));
                captured.Add(pair.Key, pair.Value);
            }

        var cookies = new List<string>();
        if (initialCookies != null)
            foreach (string cookie in initialCookies)
            {
                if (cookies.Count == 128 || string.IsNullOrEmpty(cookie) || cookie.Length > 4096 || cookie.Any(c => c < 32 || c == 127) || (total += cookie.Length) > 32768)
                    throw new ArgumentException("Explicit initial cookies exceed their supported syntax or extent.", nameof(initialCookies));
                cookies.Add(cookie);
            }

        Headers = new ReadOnlyDictionary<string, string>(captured);
        InitialCookies = cookies.AsReadOnly();
        ServerCertificateSha256 = serverCertificateSha256;
        ClientCertificate = clientCertificate;
    }

    /// <summary>Gets the exact authorized origin.</summary>
    public Uri Origin { get; }
    /// <summary>Gets private origin-scoped headers; no value may be logged.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }
    /// <summary>Gets private initial Set-Cookie values used only in a fresh job's origin-specific jar.</summary>
    public IReadOnlyList<string> InitialCookies { get; }
    /// <summary>Gets the explicit leaf-certificate pin, or null for offline system-chain validation.</summary>
    public string? ServerCertificateSha256 { get; }
    /// <summary>Gets caller-owned TLS client identity, or null. No key or store discovery is implicit.</summary>
    public X509Certificate2? ClientCertificate { get; }
}

/// <summary>An explicitly selected HTTP CONNECT proxy. The tunnel target is a validated numeric address, not a hostname for proxy-side resolution.</summary>
public sealed class HtmlProxySettings
{
    /// <summary>Captures exact HTTP proxy authority and optional private Proxy-Authorization value. Environment proxies are never selected.</summary>
    public HtmlProxySettings(Uri address, string? authorization = null)
    {
        Address = HtmlNetworkPolicy.Origin(address);
        if (Address.Scheme != "http")
            throw new ArgumentException("This source proxy profile requires an explicit HTTP CONNECT proxy.", nameof(address));
        if (authorization != null && (authorization.Length > 8192 || authorization.Any(c => c < 32 || c > 126)))
            throw new ArgumentException("A bounded ASCII proxy credential value is required.", nameof(authorization));
        Authorization = authorization;
    }

    /// <summary>Gets the explicit proxy authority.</summary>
    public Uri Address { get; }
    /// <summary>Gets private proxy-only credentials, or null. Never copy this into an origin request or logs.</summary>
    public string? Authorization { get; }
}

/// <summary>Immutable offline-by-default origin/address/redirect/resource policy. Permissive PDF mode never weakens a network denial.</summary>
public sealed class HtmlNetworkPolicy
{
    /// <summary>Captures exact authorized origins, separately permitted nonpublic numeric addresses, redirect/body bounds and explicit proxy.</summary>
    public HtmlNetworkPolicy(IEnumerable<HtmlOriginRule>? origins = null, IEnumerable<string>? permittedNonPublicAddresses = null, int maximumRedirects = 5, long maximumResponseBytes = 8388608, HtmlProxySettings? proxy = null)
    {
        if (maximumRedirects < 0 || maximumRedirects > 20)
            throw new ArgumentOutOfRangeException(nameof(maximumRedirects));
        if (maximumResponseBytes < 1 || maximumResponseBytes > 16777216)
            throw new ArgumentOutOfRangeException(nameof(maximumResponseBytes));
        var rules = new Dictionary<string, HtmlOriginRule>(StringComparer.Ordinal);
        if (origins != null)
            foreach (var rule in origins)
            {
                if (rule == null || rules.Count == 128 || rules.ContainsKey(rule.Origin.AbsoluteUri))
                    throw new ArgumentException("At most 128 distinct nonnull origins are permitted.", nameof(origins));
                rules.Add(rule.Origin.AbsoluteUri, rule);
            }

        var addresses = new HashSet<string>(StringComparer.Ordinal);
        if (permittedNonPublicAddresses != null)
            foreach (var address in permittedNonPublicAddresses)
            {
                if (addresses.Count == 128 || !IPAddress.TryParse(address, out var parsed))
                    throw new ArgumentException("At most 128 explicit numeric nonpublic addresses are permitted.", nameof(permittedNonPublicAddresses));
                addresses.Add(Normalize(parsed));
            }

        Origins = new ReadOnlyCollection<HtmlOriginRule>(rules.Values.ToArray());
        PermittedNonPublicAddresses = new ReadOnlyCollection<string>(addresses.ToArray());
        MaximumRedirects = maximumRedirects;
        MaximumResponseBytes = maximumResponseBytes;
        Proxy = proxy;
    }

    /// <summary>Gets the shared immutable default with no network origins.</summary>
    public static HtmlNetworkPolicy Offline { get; } = new();
    /// <summary>Gets exact authorized origins.</summary>
    public IReadOnlyList<HtmlOriginRule> Origins { get; }
    /// <summary>Gets separately authorized numeric nonpublic addresses. An origin must still be authorized.</summary>
    public IReadOnlyList<string> PermittedNonPublicAddresses { get; }
    /// <summary>Gets the redirect-chain ceiling.</summary>
    public int MaximumRedirects { get; }
    /// <summary>Gets the maximum decoded resource body length.</summary>
    public long MaximumResponseBytes { get; }
    /// <summary>Gets the explicit proxy, or null.</summary>
    public HtmlProxySettings? Proxy { get; }

    internal HtmlOriginRule RequireOrigin(Uri address, PdfOperationContext context)
    {
        Uri origin;
        try
        {
            origin = Origin(address);
        }
        catch (ArgumentException)
        {
            throw PdfWriterCore.Error(context, "Html.OriginDenied", "A resource address is outside the explicit HTTP(S) origin policy.");
        }

        return Origins.FirstOrDefault(rule => rule.Origin == origin) ?? throw PdfWriterCore.Error(context, "Html.OriginDenied", "The resource origin is not explicitly authorized; no lookup or connection was attempted.");
    }

    internal static Uri Origin(Uri value)
    {
        if (value == null || !value.IsAbsoluteUri || value.Scheme != "https" && value.Scheme != "http" || value.UserInfo.Length != 0 || value.Fragment.Length != 0 || value.AbsoluteUri.Length > 4096)
            throw new ArgumentException("A bounded HTTP(S) address without user information or fragments is required.", nameof(value));
        return new Uri(value.GetLeftPart(UriPartial.Authority) + "/");
    }

    internal static string Normalize(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4().ToString() : address.ToString();
    internal void RequireAddress(IPAddress address, PdfOperationContext context)
    {
        string text = Normalize(address);
        var normalized = IPAddress.Parse(text);
        if (!Public(normalized) && !PermittedNonPublicAddresses.Contains(text, StringComparer.Ordinal))
            throw PdfWriterCore.Error(context, "Html.AddressDenied", "DNS or redirect resolution selected a nonpublic address outside the explicit address grant.");
    }

    internal static bool Public(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            return Public(address.MapToIPv4());
        var b = address.GetAddressBytes();
        if (b.Length == 4)
        {
            if (b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224 || b[0] == 169 && b[1] == 254 || b[0] == 172 && b[1] >= 16 && b[1] <= 31 || b[0] == 192 && b[1] == 168 || b[0] == 100 && b[1] >= 64 && b[1] <= 127 || b[0] == 198 && (b[1] == 18 || b[1] == 19))
                return false;
            if (b[0] == 192 && b[1] == 0 && (b[2] == 0 || b[2] == 2) || b[0] == 192 && b[1] == 88 && b[2] == 99 || b[0] == 198 && b[1] == 51 && b[2] == 100 || b[0] == 203 && b[1] == 0 && b[2] == 113)
                return false;
            return true;
        }

        // Only native globally routed unicast; deny deprecated transition/tunnel and documentation ranges.
        if (b.Length != 16 || (b[0] & 224) != 32 || address.ScopeId != 0)
            return false;
        if (b[0] == 32 && b[1] == 2 || b[0] == 32 && b[1] == 1 && (b[2] <= 1 || b[2] == 13 && b[3] == 184) || b[0] == 63 && b[1] == 255)
            return false;
        return true;
    }
}
