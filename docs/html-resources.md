# HTML resource and authentication policies

FP-045 has an original source profile in the optional, non-packable `Fluent.Pdf.Html` adapter. It adds explicit resource acquisition to the existing captured-markup/resource renderer. Complete profiles and production qualification remain mandatory; packing and publication stay blocked.

## Explicit acquisition

The three-argument `ChromiumHtmlRenderer` remains offline. Its four-argument overload accepts a `HtmlResourceSessionFactory` and creates/disposes one private session per render. Supplied markup and captured resources take precedence. An absent resource can then go through that session under the **same** operation context. No browser request headers, account credentials or environment proxy settings are forwarded.

`HtmlResourceLoader` defaults to `HtmlNetworkPolicy.Offline`, which performs no DNS or transport work. Network access requires explicit `HtmlOriginRule` entries for each HTTP(S) scheme/host/port. An origin grant does not grant access to a nonpublic IP. Those require a second, exact numeric-address grant. Security denials remain fatal in permissive PDF mode.

For every request, the loader authorizes the origin before DNS, checks every returned address, and supplies one canonical numeric endpoint to `IHtmlHttpTransport`. The transport must connect to that endpoint without resolving again. Redirect responses remain real browser redirects, preserving the final document base URL. Each continuation must match its unique previous response/destination, stays within the hop ceiling, and repeats origin/DNS/address checks. The built-in address classifier conservatively excludes private, loopback, link-local, documentation, multicast and transition ranges. Complete registry review, routing/firewall policy and SSRF qualification remain required.

```csharp
var network = new HtmlNetworkPolicy(new[] {
    new HtmlOriginRule(new Uri("https://authorized.example/"),
        headers: explicitOriginHeaders,
        initialCookies: explicitInitialCookies)
});
var resources = new HtmlResourceLoader(network);
var renderer = new ChromiumHtmlRenderer(qualifiedPrivateHost,
    exactBrowserVersion, exactBrowserRevision, resources.OpenSession);
var source = PdfHtmlSource.FromResource(
    new Uri("https://authorized.example/document"), explicitCapturedAssets);
using var document = PdfHtml.Render(source, new PdfProviderRegistry(renderer),
    policy: explicitOperationPolicy);
document.SaveFile(destination);
```

Source preparation performs no request. Network work starts only inside explicit rendering/session resolution. A custom session/name/transport provider is a trusted boundary and must preserve policy, pre-allocation accounting, cancellation and resource ownership.

## Authentication and transport

Headers, initial cookies, optional server-certificate SHA-256 and caller-owned TLS client certificates belong to one exact origin. Every job gets separate cookie jars, further separated by origin even where HTTP cookie Domain rules would otherwise permit sharing. Cookie overflow rejects before an in-profile cookie can be silently evicted. Response cookies stay in the managed jars; they are not installed into the browser's JavaScript cookie store. Complete interactive/script-managed login profiles remain mandatory.

`HtmlProxySettings` explicitly selects an HTTP CONNECT proxy and optional proxy-only credentials. Both its address and the target address pass policy. CONNECT names the numeric target; origin requests never receive proxy credentials. No environment proxy or proxy-side DNS is selected implicitly. Other proxy profiles remain mandatory work.

| Compiled library asset | Built-in DNS/HTTP profile |
| --- | --- |
| net8.0 / net10.0 | Cancellable system DNS; HTTP/1.1 over exact numeric sockets; original Host/TLS name; explicit CONNECT; owned streamed response |
| net472 / netstandard2.0 | Provider contracts and policy/capture behavior; built-in DNS/transport fail before network work. Explicit cancellation-safe, pinned providers are required; complete legacy/portable transports remain mandatory |

The modern transport disables automatic redirects, cookies, decompression, proxy/account discovery, connection reuse and external certificate/revocation downloads. Default TLS uses offline system-chain/name validation. An explicit leaf pin authorizes that exact certificate while still requiring the server name. Client certificate contexts are built offline; certificates/keys remain caller-owned and must stay alive and unchanged throughout all jobs. No certificate/key export or store identity discovery is performed.

Owned responses close their body, HTTP message/client and connection after capture or failure. Body reads observe the operation token, including a stalled body. Compression is decoded explicitly through shared input/expanded leases: gzip in the source profiles, zlib/deflate and Brotli in modern assets. Legacy decoding extensions remain mandatory. Unknown/stacked codings reject. Diagnostics omit request addresses, headers, cookies, provider exception details and response bodies; applications must not log the private configuration/result header values themselves.

## Files, data URIs and limits

`HtmlAssets.ReadFile` snapshots one explicit relative file into a virtual HTTP(S) resource. `CaptureFile` uses an existing operation context. The root must be an absolute **caller-protected local directory containing authorized regular files**. The caller must prevent path/link replacement and unauthorized hard links throughout capture. Static traversal, absolute/device/alternate-stream/encoded paths and symbolic-link/reparse ancestors reject. This source helper does not provide an atomic filesystem sandbox for mutable untrusted directories; complete race-safe OS adapters remain mandatory. Browser `file:` access is never enabled.

`ReadDataUri` / `CaptureDataUri` perform explicit strict percent/base64 decoding into immutable octets at a virtual address, with an explicit/default media type. They do not guess text encodings or fetch anything. Native browser data/blob allocations still require the qualified host's memory/process boundary; the capture helper alone does not qualify those paths.

The profile bounds origin/address grants to 128 each, DNS answers to 64, resource request identities to 1,024, redirects to 0–20 (default 5), decoded responses to at most 16 MiB (default 8 MiB), response headers to 128/32 Ki characters, and private cookies to 128 per origin/4 Ki characters per cookie. File/data snapshots have 16 MiB source ceilings. Shared input/expanded/object/worker/time/output limits can reject earlier. Transport/decoder/header/cookie allocations are conservatively charged before creation. A permissive unsuccessful subresource records `Html.ResourceStatus`; an unsuccessful main document rejects.

## Evidence and remaining qualification

Source tests cover offline/origin/address admission, mixed DNS answers, rebinding, redirect identity/hops, scoped headers/cookies/proxy credentials, cookie overflow, decoding/resource limits, redaction, protected file/data capture, ownership and cancellation. Modern actual loopback fixtures exercise HTTP, mutual TLS, wrong pin/name rejection, CONNECT and cancellation during a stalled body. Legacy/fallback tests explicitly verify the built-in transport's fail-closed applicability; those are not successful legacy HTTP qualification.

The explicit Linux browser observer additionally uses an authenticated redirect, per-job session cookie, final-base-relative SVG and captured web font. The resulting PDF is read by pypdf, MuPDF and Poppler, with exact text and SVG size/color observations. This small original fixture does not qualify the browser host's security boundary or the full site/visual corpus. `includeResourceFixtures: true` enables it in the existing explicit probe manifest; choose a short private temporary parent for Chromium's Unix socket path limit.

Complete supported-runtime/RID transports and protected-file adapters, browser OS isolation, interactive authentication/site profiles, certificate/IDNA/address-registry review, malicious corpus/fuzzing, concurrency/endurance/performance and exact final-package consumers remain required. See [source evidence](evidence/FP-045-source.json), [standards observations](evidence/FP-045-standards.json) and [independent output observations](evidence/FP-045-independent.json).
