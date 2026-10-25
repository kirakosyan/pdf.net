# Fluent.Pdf.Html source reference

Version `1.0.0`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).

## Public declarations

The baseline preserves exact nullability, parameter names, defaults and constant values.

```text
Fluent.Pdf.Html.ChromiumConnectionFactory
Fluent.Pdf.Html.ChromiumContainerHost
Fluent.Pdf.Html.ChromiumContainerOptions
Fluent.Pdf.Html.ChromiumContainerOptions.ChromiumContainerOptions(string! dockerExecutable, string! dockerSha256, string! daemonEndpoint, string! imageId, string! emptyConfigurationDirectory, string! seccompProfile, string! seccompSha256, long maximumMemoryBytes, int maximumProcesses = 64, System.TimeSpan? maximumElapsedTime = null) -> void
Fluent.Pdf.Html.ChromiumContainerOptions.DaemonEndpoint.get -> string!
Fluent.Pdf.Html.ChromiumContainerOptions.DockerExecutable.get -> string!
Fluent.Pdf.Html.ChromiumContainerOptions.DockerSha256.get -> string!
Fluent.Pdf.Html.ChromiumContainerOptions.EmptyConfigurationDirectory.get -> string!
Fluent.Pdf.Html.ChromiumContainerOptions.ImageId.get -> string!
Fluent.Pdf.Html.ChromiumContainerOptions.MaximumElapsedTime.get -> System.TimeSpan
Fluent.Pdf.Html.ChromiumContainerOptions.MaximumMemoryBytes.get -> long
Fluent.Pdf.Html.ChromiumContainerOptions.MaximumProcesses.get -> int
Fluent.Pdf.Html.ChromiumContainerOptions.SeccompProfile.get -> string!
Fluent.Pdf.Html.ChromiumContainerOptions.SeccompSha256.get -> string!
Fluent.Pdf.Html.ChromiumHtmlRenderer
Fluent.Pdf.Html.ChromiumHtmlRenderer.Capture(Fluent.Pdf.PdfHtmlSource! source, Fluent.Pdf.Html.HtmlElementRequest! request, Fluent.Pdf.PdfHtmlOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.Html.HtmlElementCapture!
Fluent.Pdf.Html.ChromiumHtmlRenderer.ChromiumHtmlRenderer(Fluent.Pdf.Html.ChromiumConnectionFactory! factory, string! exactVersion, string! exactRevision) -> void
Fluent.Pdf.Html.ChromiumHtmlRenderer.ChromiumHtmlRenderer(Fluent.Pdf.Html.ChromiumConnectionFactory! factory, string! exactVersion, string! exactRevision, Fluent.Pdf.Html.HtmlResourceSessionFactory? resourceSessionFactory) -> void
Fluent.Pdf.Html.ChromiumHtmlRenderer.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Html.ChromiumHtmlRenderer.ExpectedRevision.get -> string!
Fluent.Pdf.Html.ChromiumHtmlRenderer.ExpectedVersion.get -> string!
Fluent.Pdf.Html.ChromiumHtmlRenderer.Render(Fluent.Pdf.PdfHtmlSource! source, Fluent.Pdf.PdfHtmlOptions! options, System.IO.Stream! pdfOutput, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Html.HtmlAssets
Fluent.Pdf.Html.HtmlElementCapture
Fluent.Pdf.Html.HtmlElementCapture.BaseAddress.get -> System.Uri!
Fluent.Pdf.Html.HtmlElementCapture.CaptureBounds.get -> Fluent.Pdf.Html.HtmlElementRectangle!
Fluent.Pdf.Html.HtmlElementCapture.DocumentBounds.get -> Fluent.Pdf.Html.HtmlElementRectangle!
Fluent.Pdf.Html.HtmlElementCapture.Markup.get -> string?
Fluent.Pdf.Html.HtmlElementCapture.OpenPng() -> System.IO.Stream!
Fluent.Pdf.Html.HtmlElementCapture.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.Html.HtmlElementCapture.ViewportBounds.get -> Fluent.Pdf.Html.HtmlElementRectangle!
Fluent.Pdf.Html.HtmlElementRectangle
Fluent.Pdf.Html.HtmlElementRectangle.Height.get -> double
Fluent.Pdf.Html.HtmlElementRectangle.Width.get -> double
Fluent.Pdf.Html.HtmlElementRectangle.X.get -> double
Fluent.Pdf.Html.HtmlElementRectangle.Y.get -> double
Fluent.Pdf.Html.HtmlElementRequest
Fluent.Pdf.Html.HtmlElementRequest.HtmlElementRequest(string? selector = null, string? literalText = null, int maximumNodes = 10000, long maximumPixels = 16777216, int maximumPngBytes = 8388608, int maximumMarkupCharacters = 0) -> void
Fluent.Pdf.Html.HtmlElementRequest.LiteralText.get -> string?
Fluent.Pdf.Html.HtmlElementRequest.MaximumMarkupCharacters.get -> int
Fluent.Pdf.Html.HtmlElementRequest.MaximumNodes.get -> int
Fluent.Pdf.Html.HtmlElementRequest.MaximumPixels.get -> long
Fluent.Pdf.Html.HtmlElementRequest.MaximumPngBytes.get -> int
Fluent.Pdf.Html.HtmlElementRequest.Selector.get -> string?
Fluent.Pdf.Html.HtmlHttpRequest
Fluent.Pdf.Html.HtmlHttpRequest.Address.get -> System.Uri!
Fluent.Pdf.Html.HtmlHttpRequest.ClientCertificate.get -> System.Security.Cryptography.X509Certificates.X509Certificate2?
Fluent.Pdf.Html.HtmlHttpRequest.DestinationAddress.get -> string!
Fluent.Pdf.Html.HtmlHttpRequest.Headers.get -> System.Collections.Generic.IReadOnlyDictionary<string!, string!>!
Fluent.Pdf.Html.HtmlHttpRequest.Proxy.get -> Fluent.Pdf.Html.HtmlProxySettings?
Fluent.Pdf.Html.HtmlHttpRequest.ProxyAddress.get -> string?
Fluent.Pdf.Html.HtmlHttpRequest.ServerCertificateSha256.get -> string?
Fluent.Pdf.Html.HtmlHttpResponse
Fluent.Pdf.Html.HtmlHttpResponse.Body.get -> System.IO.Stream!
Fluent.Pdf.Html.HtmlHttpResponse.Dispose() -> void
Fluent.Pdf.Html.HtmlHttpResponse.Headers.get -> System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string!, string!>>!
Fluent.Pdf.Html.HtmlHttpResponse.HtmlHttpResponse(int statusCode, string! mediaType, System.IO.Stream! body, System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string!, string!>>? headers, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Html.HtmlHttpResponse.MediaType.get -> string!
Fluent.Pdf.Html.HtmlHttpResponse.StatusCode.get -> int
Fluent.Pdf.Html.HtmlNetworkPolicy
Fluent.Pdf.Html.HtmlNetworkPolicy.HtmlNetworkPolicy(System.Collections.Generic.IEnumerable<Fluent.Pdf.Html.HtmlOriginRule!>? origins = null, System.Collections.Generic.IEnumerable<string!>? permittedNonPublicAddresses = null, int maximumRedirects = 5, long maximumResponseBytes = 8388608, Fluent.Pdf.Html.HtmlProxySettings? proxy = null) -> void
Fluent.Pdf.Html.HtmlNetworkPolicy.MaximumRedirects.get -> int
Fluent.Pdf.Html.HtmlNetworkPolicy.MaximumResponseBytes.get -> long
Fluent.Pdf.Html.HtmlNetworkPolicy.Origins.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.Html.HtmlOriginRule!>!
Fluent.Pdf.Html.HtmlNetworkPolicy.PermittedNonPublicAddresses.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.Html.HtmlNetworkPolicy.Proxy.get -> Fluent.Pdf.Html.HtmlProxySettings?
Fluent.Pdf.Html.HtmlOriginRule
Fluent.Pdf.Html.HtmlOriginRule.ClientCertificate.get -> System.Security.Cryptography.X509Certificates.X509Certificate2?
Fluent.Pdf.Html.HtmlOriginRule.Headers.get -> System.Collections.Generic.IReadOnlyDictionary<string!, string!>!
Fluent.Pdf.Html.HtmlOriginRule.HtmlOriginRule(System.Uri! origin, System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string!, string!>>? headers = null, System.Collections.Generic.IEnumerable<string!>? initialCookies = null, string? serverCertificateSha256 = null, System.Security.Cryptography.X509Certificates.X509Certificate2? clientCertificate = null) -> void
Fluent.Pdf.Html.HtmlOriginRule.InitialCookies.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.Html.HtmlOriginRule.Origin.get -> System.Uri!
Fluent.Pdf.Html.HtmlOriginRule.ServerCertificateSha256.get -> string?
Fluent.Pdf.Html.HtmlProxySettings
Fluent.Pdf.Html.HtmlProxySettings.Address.get -> System.Uri!
Fluent.Pdf.Html.HtmlProxySettings.Authorization.get -> string?
Fluent.Pdf.Html.HtmlProxySettings.HtmlProxySettings(System.Uri! address, string? authorization = null) -> void
Fluent.Pdf.Html.HtmlResourceLoader
Fluent.Pdf.Html.HtmlResourceLoader.HtmlResourceLoader(Fluent.Pdf.Html.HtmlNetworkPolicy? policy = null, Fluent.Pdf.Html.IHtmlNameResolver? resolver = null, Fluent.Pdf.Html.IHtmlHttpTransport? transport = null) -> void
Fluent.Pdf.Html.HtmlResourceLoader.OpenSession(Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.Html.IHtmlResourceSession!
Fluent.Pdf.Html.HtmlResourceRequest
Fluent.Pdf.Html.HtmlResourceRequest.Address.get -> System.Uri!
Fluent.Pdf.Html.HtmlResourceRequest.HtmlResourceRequest(System.Uri! address, string! requestId, string? redirectedRequestId = null, bool isMainDocument = false) -> void
Fluent.Pdf.Html.HtmlResourceRequest.IsMainDocument.get -> bool
Fluent.Pdf.Html.HtmlResourceRequest.RedirectedRequestId.get -> string?
Fluent.Pdf.Html.HtmlResourceRequest.RequestId.get -> string!
Fluent.Pdf.Html.HtmlResourceResult
Fluent.Pdf.Html.HtmlResourceResult.Content.get -> Fluent.Pdf.PdfHtmlResource!
Fluent.Pdf.Html.HtmlResourceResult.Headers.get -> System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string!, string!>>!
Fluent.Pdf.Html.HtmlResourceResult.HtmlResourceResult(Fluent.Pdf.PdfHtmlResource! content, int statusCode, System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string!, string!>>? headers, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Html.HtmlResourceResult.StatusCode.get -> int
Fluent.Pdf.Html.HtmlResourceSessionFactory
Fluent.Pdf.Html.IChromiumConnection
Fluent.Pdf.Html.IChromiumConnection.Receive(Fluent.Pdf.PdfOperationContext! context) -> string!
Fluent.Pdf.Html.IChromiumConnection.Send(string! message, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Html.IHtmlHttpTransport
Fluent.Pdf.Html.IHtmlHttpTransport.Send(Fluent.Pdf.Html.HtmlHttpRequest! request, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.Html.HtmlHttpResponse!
Fluent.Pdf.Html.IHtmlNameResolver
Fluent.Pdf.Html.IHtmlNameResolver.Resolve(string! host, Fluent.Pdf.PdfOperationContext! context) -> System.Collections.Generic.IReadOnlyList<System.Net.IPAddress!>!
Fluent.Pdf.Html.IHtmlResourceSession
Fluent.Pdf.Html.IHtmlResourceSession.Resolve(Fluent.Pdf.Html.HtmlResourceRequest! request, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.Html.HtmlResourceResult?
Fluent.Pdf.Html.IPdfViewHost
Fluent.Pdf.Html.IPdfViewHost.IsAvailable.get -> bool
Fluent.Pdf.Html.IPdfViewHost.Kind.get -> Fluent.Pdf.Html.PdfViewKind
Fluent.Pdf.Html.IPdfViewHost.RenderAsync(System.IO.Stream! output, Fluent.Pdf.PdfOperationContext! context) -> System.Threading.Tasks.Task!
Fluent.Pdf.Html.PdfViewKind
Fluent.Pdf.Html.PdfViewKind.Blazor = 2 -> Fluent.Pdf.Html.PdfViewKind
Fluent.Pdf.Html.PdfViewKind.Mvc = 1 -> Fluent.Pdf.Html.PdfViewKind
Fluent.Pdf.Html.PdfViewKind.Razor = 0 -> Fluent.Pdf.Html.PdfViewKind
Fluent.Pdf.Html.PdfViewKind.WebForms = 3 -> Fluent.Pdf.Html.PdfViewKind
Fluent.Pdf.Html.PdfViewKind.Xaml = 4 -> Fluent.Pdf.Html.PdfViewKind
Fluent.Pdf.Html.PdfWebViews
Fluent.Pdf.Html.PinnedHtmlHttpTransport
Fluent.Pdf.Html.PinnedHtmlHttpTransport.IsSupported.get -> bool
Fluent.Pdf.Html.PinnedHtmlHttpTransport.PinnedHtmlHttpTransport() -> void
Fluent.Pdf.Html.PinnedHtmlHttpTransport.Send(Fluent.Pdf.Html.HtmlHttpRequest! request, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.Html.HtmlHttpResponse!
Fluent.Pdf.Html.SystemHtmlNameResolver
Fluent.Pdf.Html.SystemHtmlNameResolver.Resolve(string! host, Fluent.Pdf.PdfOperationContext! context) -> System.Collections.Generic.IReadOnlyList<System.Net.IPAddress!>!
Fluent.Pdf.Html.SystemHtmlNameResolver.SystemHtmlNameResolver() -> void
static Fluent.Pdf.Html.ChromiumContainerHost.Open(Fluent.Pdf.Html.ChromiumContainerOptions! options, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.Html.IChromiumConnection!
static Fluent.Pdf.Html.HtmlAssets.CaptureDataUri(string! dataUri, System.Uri! address, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfHtmlResource!
static Fluent.Pdf.Html.HtmlAssets.CaptureFile(string! protectedRoot, string! relativePath, System.Uri! address, string! mediaType, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfHtmlResource!
static Fluent.Pdf.Html.HtmlAssets.ReadDataUri(string! dataUri, System.Uri! address, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfHtmlResource!
static Fluent.Pdf.Html.HtmlAssets.ReadFile(string! protectedRoot, string! relativePath, System.Uri! address, string! mediaType, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfHtmlResource!
static Fluent.Pdf.Html.HtmlNetworkPolicy.Offline.get -> Fluent.Pdf.Html.HtmlNetworkPolicy!
static Fluent.Pdf.Html.PdfWebViews.RenderAsync(Fluent.Pdf.Html.IPdfViewHost! host, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfHtmlOptions? htmlOptions = null, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfHtmlResource!>? resources = null, System.Uri? baseAddress = null, int maximumPages = 1000, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Threading.Tasks.Task<Fluent.Pdf.PdfDocument!>!
```

## Member documentation

### <code>F:Fluent.Pdf.Html.PdfViewKind.Blazor</code>

A trusted Blazor static HTML render.

### <code>F:Fluent.Pdf.Html.PdfViewKind.Mvc</code>

A trusted MVC view yielding UTF8 HTML.

### <code>F:Fluent.Pdf.Html.PdfViewKind.Razor</code>

A trusted compiled Razor/CSHTML view yielding UTF8 HTML.

### <code>F:Fluent.Pdf.Html.PdfViewKind.WebForms</code>

A trusted ASPX/WebForms host on Windows .NET Framework, yielding UTF8 HTML.

### <code>F:Fluent.Pdf.Html.PdfViewKind.Xaml</code>

A trusted Windows XAML print host yielding a complete PDF.

### <code>M:Fluent.Pdf.Html.ChromiumContainerHost.Open(Fluent.Pdf.Html.ChromiumContainerOptions,Fluent.Pdf.PdfOperationContext)</code>

Verifies prerequisites, reserves memory, requires an immutable creation acknowledgement, then starts the unique offline read-only nonroot container and checks its bootstrap. Disposal targets the acknowledged ID and joins owned I/O; unacknowledged creation and cleanup failures retain owned identities.

### <code>M:Fluent.Pdf.Html.ChromiumContainerOptions.#ctor(System.String,System.String,System.String,System.String,System.String,System.String,System.String,System.Int64,System.Int32,System.Nullable{System.TimeSpan})</code>

Captures exact CLI/policy hashes, a local Unix/named-pipe daemon endpoint, immutable local image ID and a protected empty CLI configuration directory. The caller provisions/reviews every prerequisite; none is downloaded.

### <code>M:Fluent.Pdf.Html.ChromiumHtmlRenderer.#ctor(Fluent.Pdf.Html.ChromiumConnectionFactory,System.String,System.String)</code>

Requires a private host factory and exact expected four-part Chromium version and DevTools source revision. The actual Browser.getVersion response must match before document input is sent.

### <code>M:Fluent.Pdf.Html.ChromiumHtmlRenderer.#ctor(Fluent.Pdf.Html.ChromiumConnectionFactory,System.String,System.String,Fluent.Pdf.Html.HtmlResourceSessionFactory)</code>

Captures an optional explicit factory for a fresh policy-bound resource/authentication session on every job. The browser host must still deny uncontrolled network and filesystem access.

### <code>M:Fluent.Pdf.Html.ChromiumHtmlRenderer.Capture(Fluent.Pdf.PdfHtmlSource,Fluent.Pdf.Html.HtmlElementRequest,Fluent.Pdf.PdfHtmlOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures one uniquely selected main-document element under the ordinary resource/readiness/host boundary. Missing, ambiguous, changing or oversized captures reject without retry or reflow.

### <code>M:Fluent.Pdf.Html.ChromiumHtmlRenderer.Render(Fluent.Pdf.PdfHtmlSource,Fluent.Pdf.PdfHtmlOptions,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Creates a fresh off-the-record browser context, intercepts all HTTP resources, waits for load/fonts and streams PDF into the borrowed SDK lease.

### <code>M:Fluent.Pdf.Html.HtmlAssets.CaptureDataUri(System.String,System.Uri,Fluent.Pdf.PdfOperationContext)</code>

Strict percent/base64 decoding under an existing job budget, with 16 MiB encoded and decoded ceilings. No payload appears in diagnostics.

### <code>M:Fluent.Pdf.Html.HtmlAssets.CaptureFile(System.String,System.String,System.Uri,System.String,Fluent.Pdf.PdfOperationContext)</code>

Captures a file under the existing job context. Rejects absolute/traversal paths, device paths and reparse/symbolic-link ancestors. A mutable untrusted directory is not an authorized root.

### <code>M:Fluent.Pdf.Html.HtmlAssets.ReadDataUri(System.String,System.Uri,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Decodes one bounded data URI into an immutable virtual resource. This performs no network or filesystem work.

### <code>M:Fluent.Pdf.Html.HtmlAssets.ReadFile(System.String,System.String,System.Uri,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Snapshots one relative file from a caller-protected root. The caller must prevent concurrent path/link mutation and unauthorized hard links throughout capture.

### <code>M:Fluent.Pdf.Html.HtmlElementCapture.OpenPng</code>

Opens immutable exact PNG bytes in a fresh read-only stream owned by the caller.

### <code>M:Fluent.Pdf.Html.HtmlElementRequest.#ctor(System.String,System.String,System.Int32,System.Int64,System.Int32,System.Int32)</code>

Requires exactly one selector or literal text. Markup is private, active source data, returned only when explicitly requested; it is not a sanitized or standalone document.

### <code>M:Fluent.Pdf.Html.HtmlHttpResponse.#ctor(System.Int32,System.String,System.IO.Stream,System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{System.String,System.String}},Fluent.Pdf.PdfOperationContext)</code>

Captures owned readable body and bounded headers. Transport allocations and header acquisition must be charged before constructing this result.

### <code>M:Fluent.Pdf.Html.HtmlHttpResponse.Dispose</code>

Closes the owned response/body and all transport work associated with it.

### <code>M:Fluent.Pdf.Html.HtmlNetworkPolicy.#ctor(System.Collections.Generic.IEnumerable{Fluent.Pdf.Html.HtmlOriginRule},System.Collections.Generic.IEnumerable{System.String},System.Int32,System.Int64,Fluent.Pdf.Html.HtmlProxySettings)</code>

Captures exact authorized origins, separately permitted nonpublic numeric addresses, redirect/body bounds and explicit proxy.

### <code>M:Fluent.Pdf.Html.HtmlOriginRule.#ctor(System.Uri,System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{System.String,System.String}},System.Collections.Generic.IEnumerable{System.String},System.String,System.Security.Cryptography.X509Certificates.X509Certificate2)</code>

Captures exact origin authority, explicit headers/initial Set-Cookie values, optional leaf pin and caller-owned client certificate.

### <code>M:Fluent.Pdf.Html.HtmlProxySettings.#ctor(System.Uri,System.String)</code>

Captures exact HTTP proxy authority and optional private Proxy-Authorization value. Environment proxies are never selected.

### <code>M:Fluent.Pdf.Html.HtmlResourceLoader.#ctor(Fluent.Pdf.Html.HtmlNetworkPolicy,Fluent.Pdf.Html.IHtmlNameResolver,Fluent.Pdf.Html.IHtmlHttpTransport)</code>

Captures immutable policy and explicit optional providers. Network-enabled defaults use cancellation-capable DNS and the pinned modern HTTP transport; unsupported runtimes fail before network work.

### <code>M:Fluent.Pdf.Html.HtmlResourceLoader.OpenSession(Fluent.Pdf.PdfOperationContext)</code>

Creates a new resource/cookie session for one job, under the mandatory context.

### <code>M:Fluent.Pdf.Html.HtmlResourceRequest.#ctor(System.Uri,System.String,System.String,System.Boolean)</code>

Captures an absolute resource address and bounded request/previous-request identities.

### <code>M:Fluent.Pdf.Html.HtmlResourceResult.#ctor(Fluent.Pdf.PdfHtmlResource,System.Int32,System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{System.String,System.String}},Fluent.Pdf.PdfOperationContext)</code>

Captures an immutable resource and response metadata with the mandatory operation budget.

### <code>M:Fluent.Pdf.Html.IChromiumConnection.Receive(Fluent.Pdf.PdfOperationContext)</code>

Receives one complete JSON DevTools message, at most 64 MiB of characters, charging before allocation. EOF/process failure must throw; never return an empty fabricated message.

### <code>M:Fluent.Pdf.Html.IChromiumConnection.Send(System.String,Fluent.Pdf.PdfOperationContext)</code>

Sends one complete JSON DevTools message. Implementations must bound/charge transport allocations and honor the supplied cancellation/deadline.

### <code>M:Fluent.Pdf.Html.IHtmlHttpTransport.Send(Fluent.Pdf.Html.HtmlHttpRequest,Fluent.Pdf.PdfOperationContext)</code>

Performs one policy-approved GET under shared budgets/cancellation. TLS must not download certificates/revocation data outside the resource policy. Returned response owns all request resources.

### <code>M:Fluent.Pdf.Html.IHtmlNameResolver.Resolve(System.String,Fluent.Pdf.PdfOperationContext)</code>

Returns a bounded address inventory using the mandatory cancellation/deadline. No lookup is permitted before origin authorization.

### <code>M:Fluent.Pdf.Html.IHtmlResourceSession.Resolve(Fluent.Pdf.Html.HtmlResourceRequest,Fluent.Pdf.PdfOperationContext)</code>

Resolves explicit resource policy under the shared context, or returns null for an unprovided asset. Security denials must remain fatal in both modes.

### <code>M:Fluent.Pdf.Html.IPdfViewHost.RenderAsync(System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Renders once into an expiring bounded output lease. Forward the shared cancellation token to request/dispatcher work and finish every write before returning.

### <code>M:Fluent.Pdf.Html.PdfWebViews.RenderAsync(Fluent.Pdf.Html.IPdfViewHost,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfHtmlOptions,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfHtmlResource},System.Uri,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures one view under the same context as HTML rendering/PDF import. HTML resources are explicit, scripts default off, and Windows/legacy restrictions reject before host invocation. XAML accepts no HTML options/resources.

### <code>M:Fluent.Pdf.Html.PinnedHtmlHttpTransport.#ctor</code>

Public parameterless construction. Modern HTTP/1.1 transport connecting only to supplied numeric addresses. No redirect, cookie, decompression, proxy or credential discovery is implicit.

### <code>M:Fluent.Pdf.Html.PinnedHtmlHttpTransport.Send(Fluent.Pdf.Html.HtmlHttpRequest,Fluent.Pdf.PdfOperationContext)</code>

Performs one GET to the prevalidated IP, keeping original Host/TLS name, disabling external chain downloads and owning all response resources.

### <code>M:Fluent.Pdf.Html.SystemHtmlNameResolver.#ctor</code>

Public parameterless construction. Explicit cancellation-capable system DNS on modern library assets. Legacy/fallback assets require a separately supplied qualified resolver.

### <code>M:Fluent.Pdf.Html.SystemHtmlNameResolver.Resolve(System.String,Fluent.Pdf.PdfOperationContext)</code>

Resolves a policy-authorized hostname under the shared deadline; the caller must validate and pin every result before connecting.

### <code>P:Fluent.Pdf.Html.ChromiumContainerOptions.DaemonEndpoint</code>

Gets the explicitly selected local daemon endpoint.

### <code>P:Fluent.Pdf.Html.ChromiumContainerOptions.DockerExecutable</code>

Gets the explicitly pinned Docker CLI path.

### <code>P:Fluent.Pdf.Html.ChromiumContainerOptions.DockerSha256</code>

Gets its exact lowercase SHA256.

### <code>P:Fluent.Pdf.Html.ChromiumContainerOptions.EmptyConfigurationDirectory</code>

Gets the caller-protected empty Docker configuration directory.

### <code>P:Fluent.Pdf.Html.ChromiumContainerOptions.ImageId</code>

Gets the immutable local image ID; pulling is prohibited.

### <code>P:Fluent.Pdf.Html.ChromiumContainerOptions.MaximumElapsedTime</code>

Gets its finite wall deadline; operation/readiness cancellation may expire sooner.

### <code>P:Fluent.Pdf.Html.ChromiumContainerOptions.MaximumMemoryBytes</code>

Gets the hard browser-container memory/swap ceiling reserved against the operation budget.

### <code>P:Fluent.Pdf.Html.ChromiumContainerOptions.MaximumProcesses</code>

Gets the container process/thread ceiling.

### <code>P:Fluent.Pdf.Html.ChromiumContainerOptions.SeccompProfile</code>

Gets the exact caller-reviewed default-deny seccomp profile path.

### <code>P:Fluent.Pdf.Html.ChromiumContainerOptions.SeccompSha256</code>

Gets its exact lowercase SHA256.

### <code>P:Fluent.Pdf.Html.ChromiumHtmlRenderer.Descriptor</code>

Gets the pinned declared engine identity. This does not declare platform or production qualification.

### <code>P:Fluent.Pdf.Html.ChromiumHtmlRenderer.ExpectedRevision</code>

Gets the exact required browser source revision.

### <code>P:Fluent.Pdf.Html.ChromiumHtmlRenderer.ExpectedVersion</code>

Gets the exact required browser product version.

### <code>P:Fluent.Pdf.Html.HtmlElementCapture.BaseAddress</code>

Gets the final private document address for relative resource interpretation; credentials are never embedded by the SDK.

### <code>P:Fluent.Pdf.Html.HtmlElementCapture.CaptureBounds</code>

Gets the outward-integer-rounded rectangle clipped to the document surface; PNG dimensions equal this extent at device scale one.

### <code>P:Fluent.Pdf.Html.HtmlElementCapture.DocumentBounds</code>

Gets the transformed element border bounding rectangle relative to the document origin.

### <code>P:Fluent.Pdf.Html.HtmlElementCapture.Markup</code>

Gets optional original active outer markup. Treat it as private untrusted source; it is not a resource bundle or sanitized HTML.

### <code>P:Fluent.Pdf.Html.HtmlElementCapture.OperationReport</code>

Gets the successful shared-budget operation report.

### <code>P:Fluent.Pdf.Html.HtmlElementCapture.ViewportBounds</code>

Gets the transformed element border bounding rectangle relative to the unchanged CSS viewport.

### <code>P:Fluent.Pdf.Html.HtmlElementRectangle.Height</code>

Gets its nonnegative CSS height.

### <code>P:Fluent.Pdf.Html.HtmlElementRectangle.Width</code>

Gets its nonnegative CSS width.

### <code>P:Fluent.Pdf.Html.HtmlElementRectangle.X</code>

Gets its left position in the declared CSS coordinate space.

### <code>P:Fluent.Pdf.Html.HtmlElementRectangle.Y</code>

Gets its top position in the declared CSS coordinate space.

### <code>P:Fluent.Pdf.Html.HtmlElementRequest.LiteralText</code>

Gets the ordinal substring matched within individual text nodes, selecting their unique parent element; scripts/styles are excluded.

### <code>P:Fluent.Pdf.Html.HtmlElementRequest.MaximumMarkupCharacters</code>

Gets the explicit outer-markup character ceiling; zero disables markup extraction.

### <code>P:Fluent.Pdf.Html.HtmlElementRequest.MaximumNodes</code>

Gets the maximum visited light-DOM element/text nodes.

### <code>P:Fluent.Pdf.Html.HtmlElementRequest.MaximumPixels</code>

Gets the maximum rounded CSS capture area at fixed device scale one.

### <code>P:Fluent.Pdf.Html.HtmlElementRequest.MaximumPngBytes</code>

Gets the maximum accepted encoded PNG extent.

### <code>P:Fluent.Pdf.Html.HtmlElementRequest.Selector</code>

Gets the unique main-document CSS selector, or null for literal text search.

### <code>P:Fluent.Pdf.Html.HtmlHttpRequest.Address</code>

Gets the original HTTP(S) address used for Host and TLS server-name validation.

### <code>P:Fluent.Pdf.Html.HtmlHttpRequest.ClientCertificate</code>

Gets the explicitly supplied caller-owned TLS client identity, or null. No store discovery or key export is authorized.

### <code>P:Fluent.Pdf.Html.HtmlHttpRequest.DestinationAddress</code>

Gets the exact validated numeric destination. A transport must connect here without another DNS lookup.

### <code>P:Fluent.Pdf.Html.HtmlHttpRequest.Headers</code>

Gets origin-scoped request headers. Values may be secret and must never enter diagnostics.

### <code>P:Fluent.Pdf.Html.HtmlHttpRequest.Proxy</code>

Gets explicit proxy settings, or null. System/environment proxy discovery is forbidden.

### <code>P:Fluent.Pdf.Html.HtmlHttpRequest.ProxyAddress</code>

Gets the exact validated numeric proxy address, or null.

### <code>P:Fluent.Pdf.Html.HtmlHttpRequest.ServerCertificateSha256</code>

Gets an optional explicit lowercase SHA-256 leaf-certificate pin. Host-name verification remains required.

### <code>P:Fluent.Pdf.Html.HtmlHttpResponse.Body</code>

Gets the owned response stream for bounded capture.

### <code>P:Fluent.Pdf.Html.HtmlHttpResponse.Headers</code>

Gets bounded headers, which can contain secrets and must not be logged.

### <code>P:Fluent.Pdf.Html.HtmlHttpResponse.MediaType</code>

Gets the explicit media type.

### <code>P:Fluent.Pdf.Html.HtmlHttpResponse.StatusCode</code>

Gets the response status.

### <code>P:Fluent.Pdf.Html.HtmlNetworkPolicy.MaximumRedirects</code>

Gets the redirect-chain ceiling.

### <code>P:Fluent.Pdf.Html.HtmlNetworkPolicy.MaximumResponseBytes</code>

Gets the maximum decoded resource body length.

### <code>P:Fluent.Pdf.Html.HtmlNetworkPolicy.Offline</code>

Gets the shared immutable default with no network origins.

### <code>P:Fluent.Pdf.Html.HtmlNetworkPolicy.Origins</code>

Gets exact authorized origins.

### <code>P:Fluent.Pdf.Html.HtmlNetworkPolicy.PermittedNonPublicAddresses</code>

Gets separately authorized numeric nonpublic addresses. An origin must still be authorized.

### <code>P:Fluent.Pdf.Html.HtmlNetworkPolicy.Proxy</code>

Gets the explicit proxy, or null.

### <code>P:Fluent.Pdf.Html.HtmlOriginRule.ClientCertificate</code>

Gets caller-owned TLS client identity, or null. No key or store discovery is implicit.

### <code>P:Fluent.Pdf.Html.HtmlOriginRule.Headers</code>

Gets private origin-scoped headers; no value may be logged.

### <code>P:Fluent.Pdf.Html.HtmlOriginRule.InitialCookies</code>

Gets private initial Set-Cookie values used only in a fresh job's origin-specific jar.

### <code>P:Fluent.Pdf.Html.HtmlOriginRule.Origin</code>

Gets the exact authorized origin.

### <code>P:Fluent.Pdf.Html.HtmlOriginRule.ServerCertificateSha256</code>

Gets the explicit leaf-certificate pin, or null for offline system-chain validation.

### <code>P:Fluent.Pdf.Html.HtmlProxySettings.Address</code>

Gets the explicit proxy authority.

### <code>P:Fluent.Pdf.Html.HtmlProxySettings.Authorization</code>

Gets private proxy-only credentials, or null. Never copy this into an origin request or logs.

### <code>P:Fluent.Pdf.Html.HtmlResourceRequest.Address</code>

Gets the exact absolute address.

### <code>P:Fluent.Pdf.Html.HtmlResourceRequest.IsMainDocument</code>

Gets whether this request is the main document or its redirect continuation.

### <code>P:Fluent.Pdf.Html.HtmlResourceRequest.RedirectedRequestId</code>

Gets its previous redirect request identity, if any.

### <code>P:Fluent.Pdf.Html.HtmlResourceRequest.RequestId</code>

Gets this browser request's identity.

### <code>P:Fluent.Pdf.Html.HtmlResourceResult.Content</code>

Gets the captured immutable bytes and explicit media type.

### <code>P:Fluent.Pdf.Html.HtmlResourceResult.Headers</code>

Gets bounded response headers. Values can contain credentials and must not be logged.

### <code>P:Fluent.Pdf.Html.HtmlResourceResult.StatusCode</code>

Gets the explicit HTTP-style response status.

### <code>P:Fluent.Pdf.Html.IPdfViewHost.IsAvailable</code>

Reports whether the explicitly provisioned framework/dispatcher/compiled view is available.

### <code>P:Fluent.Pdf.Html.IPdfViewHost.Kind</code>

Gets the exact view family; XAML emits PDF, other families emit well-formed UTF8 HTML.

### <code>P:Fluent.Pdf.Html.PinnedHtmlHttpTransport.IsSupported</code>

Gets whether this compiled library asset supplies the built-in cancellable, DNS-pinned TLS transport.

### <code>T:Fluent.Pdf.Html.ChromiumConnectionFactory</code>

Opens one private, explicitly provisioned Chromium process/transport. The host must enforce offline network, read-only pinned assets/fonts, memory/process/time limits and termination within five seconds; Dispose must join all owned work even after a latched failure. No SDK engine download or host discovery occurs.

### <code>T:Fluent.Pdf.Html.ChromiumContainerHost</code>

Explicit bounded Docker CLI transport to one private offline Linux browser container. Complete daemon/kernel/image/security/RID qualification remains required.

### <code>T:Fluent.Pdf.Html.ChromiumContainerOptions</code>

Explicit local Linux-container browser prerequisites. No CLI, daemon, image, policy or credential discovery is permitted.

### <code>T:Fluent.Pdf.Html.ChromiumHtmlRenderer</code>

Original DevTools HTML/CSS/SVG/canvas/explicit-script printing over a caller-owned supervised Chromium host. No executable or runtime is discovered or downloaded.

### <code>T:Fluent.Pdf.Html.HtmlAssets</code>

Explicit immutable file/data-URI snapshots mapped to virtual HTTP(S) addresses. Browser file access is never enabled.

### <code>T:Fluent.Pdf.Html.HtmlElementCapture</code>

A detached painted-page bounding-box capture. Ancestor clipping and overlapping page content remain visible; this does not isolate or reflow an element.

### <code>T:Fluent.Pdf.Html.HtmlElementRectangle</code>

A CSS rectangle with top-left origin and downward positive Y; values are CSS pixels, never PDF points.

### <code>T:Fluent.Pdf.Html.HtmlElementRequest</code>

Explicit unique CSS selector or ordinal literal text-node search in the main light DOM, with finite traversal, pixel and output bounds.

### <code>T:Fluent.Pdf.Html.HtmlHttpRequest</code>

A policy-approved HTTP GET with exact connection addresses and origin-scoped credentials.

### <code>T:Fluent.Pdf.Html.HtmlHttpResponse</code>

Owned transport response. The resource loader disposes its body after bounded capture; this is not a caller-owned input stream.

### <code>T:Fluent.Pdf.Html.HtmlNetworkPolicy</code>

Immutable offline-by-default origin/address/redirect/resource policy. Permissive PDF mode never weakens a network denial.

### <code>T:Fluent.Pdf.Html.HtmlOriginRule</code>

One explicitly authorized origin and its private header/cookie/TLS configuration. Nothing is inherited across origins or from the host account.

### <code>T:Fluent.Pdf.Html.HtmlProxySettings</code>

An explicitly selected HTTP CONNECT proxy. The tunnel target is a validated numeric address, not a hostname for proxy-side resolution.

### <code>T:Fluent.Pdf.Html.HtmlResourceLoader</code>

Origin/address-bound HTTP acquisition with private per-job cookies. Offline is the default; no environment proxy or account credentials are discovered.

### <code>T:Fluent.Pdf.Html.HtmlResourceRequest</code>

One exact resource request and browser redirect-chain identity. No browser headers or credentials are forwarded implicitly.

### <code>T:Fluent.Pdf.Html.HtmlResourceResult</code>

Immutable captured content/status/headers. Content bytes have already passed the operation's capture/expansion boundary.

### <code>T:Fluent.Pdf.Html.HtmlResourceSessionFactory</code>

Creates a fresh resource/cookie/credential session for one job; a shared mutable authentication session is forbidden.

### <code>T:Fluent.Pdf.Html.IChromiumConnection</code>

A caller-provisioned, privately owned DevTools connection for one supervised browser job. Creating a connection must never reuse a prior job's process/profile/credentials.

### <code>T:Fluent.Pdf.Html.IHtmlHttpTransport</code>

Explicit transport boundary. Redirects/cookies/proxies/decompression must not be applied implicitly; connect exactly to validated numeric endpoints.

### <code>T:Fluent.Pdf.Html.IHtmlNameResolver</code>

An explicit DNS provider. The loader validates every returned address and passes one exact address to the transport, which must not resolve it again.

### <code>T:Fluent.Pdf.Html.IHtmlResourceSession</code>

A private resource/authentication session for one browser job. Implementations must preserve supplied policies and complete all work before disposal returns.

### <code>T:Fluent.Pdf.Html.IPdfViewHost</code>

Caller-owned application rendering host. Only trusted application templates/components/models belong here; this boundary does not sandbox arbitrary template code.

### <code>T:Fluent.Pdf.Html.PdfViewKind</code>

Explicit trusted application-view boundary. These names do not assert that a host/framework is installed.

### <code>T:Fluent.Pdf.Html.PdfWebViews</code>

Optional framework-neutral bridge from explicit trusted application views into the SDK's bounded HTML/PDF import. Core imports no web or XAML types.

### <code>T:Fluent.Pdf.Html.PinnedHtmlHttpTransport</code>

Modern HTTP/1.1 transport connecting only to supplied numeric addresses. No redirect, cookie, decompression, proxy or credential discovery is implicit.

### <code>T:Fluent.Pdf.Html.SystemHtmlNameResolver</code>

Explicit cancellation-capable system DNS on modern library assets. Legacy/fallback assets require a separately supplied qualified resolver.

