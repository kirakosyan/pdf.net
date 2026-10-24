# Fluent.Pdf.Workers source reference

Version `1.0.0`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).

## Public declarations

The baseline preserves exact nullability, parameter names, defaults and constant values.

```text
Fluent.Pdf.Workers.IPdfRenderWorkerTransport
Fluent.Pdf.Workers.IPdfRenderWorkerTransport.Exchange(System.IO.Stream! request, System.IO.Stream! response, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Workers.NativeWorkerHost
Fluent.Pdf.Workers.NativeWorkerHost.Execute(Fluent.Pdf.Workers.NativeWorkerOptions! options, byte[]! request, Fluent.Pdf.PdfOperationContext! context) -> byte[]!
Fluent.Pdf.Workers.NativeWorkerHost.Execute(Fluent.Pdf.Workers.NativeWorkerOptions! options, byte[]! request, Fluent.Pdf.PdfOperationContext! context, int maximumResponseBytes) -> byte[]!
Fluent.Pdf.Workers.NativeWorkerHost.NativeWorkerHost() -> void
Fluent.Pdf.Workers.NativeWorkerOptions
Fluent.Pdf.Workers.NativeWorkerOptions.AllowInProcessThreads.get -> bool
Fluent.Pdf.Workers.NativeWorkerOptions.CaptureWorkerDiagnostics.get -> bool
Fluent.Pdf.Workers.NativeWorkerOptions.Deployment.get -> Fluent.Pdf.PdfNativeDeployment!
Fluent.Pdf.Workers.NativeWorkerOptions.ExecutableFileName.get -> string!
Fluent.Pdf.Workers.NativeWorkerOptions.LibraryFileName.get -> string!
Fluent.Pdf.Workers.NativeWorkerOptions.MaximumElapsedTime.get -> System.TimeSpan
Fluent.Pdf.Workers.NativeWorkerOptions.MaximumInputBytes.get -> int
Fluent.Pdf.Workers.NativeWorkerOptions.MaximumMemoryBytes.get -> long
Fluent.Pdf.Workers.NativeWorkerOptions.MaximumOutputBytes.get -> int
Fluent.Pdf.Workers.NativeWorkerOptions.NativeWorkerOptions(Fluent.Pdf.PdfNativeDeployment! deployment, string! executableFileName, string! libraryFileName, string! temporaryDirectory, long maximumMemoryBytes = 268435456, int maximumInputBytes = 67108864, int maximumOutputBytes = 67108864, System.TimeSpan? maximumElapsedTime = null, bool allowInProcessThreads = false, bool captureWorkerDiagnostics = false) -> void
Fluent.Pdf.Workers.NativeWorkerOptions.TemporaryDirectory.get -> string!
Fluent.Pdf.Workers.PdfRenderWorkerClient
Fluent.Pdf.Workers.PdfRenderWorkerClient.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Workers.PdfRenderWorkerClient.PdfRenderWorkerClient(Fluent.Pdf.Workers.PdfWorkerCredentials! credentials, Fluent.Pdf.Workers.IPdfRenderWorkerTransport! transport, int maximumAttempts = 1) -> void
Fluent.Pdf.Workers.PdfRenderWorkerClient.Render(Fluent.Pdf.PdfHtmlSource! source, Fluent.Pdf.PdfHtmlOptions! options, System.IO.Stream! output, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Workers.PdfRenderWorkerClient.RenderHtml(System.IO.Stream! markup, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.Workers.PdfWorkerPdf!
Fluent.Pdf.Workers.PdfRenderWorkerService
Fluent.Pdf.Workers.PdfRenderWorkerService.PdfRenderWorkerService(System.Collections.Generic.IEnumerable<Fluent.Pdf.Workers.PdfWorkerTenant!>! tenants, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null) -> void
Fluent.Pdf.Workers.PdfRenderWorkerService.Process(System.IO.Stream! request, System.IO.Stream! response, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.Workers.PdfWorkerCredentials
Fluent.Pdf.Workers.PdfWorkerCredentials.Dispose() -> void
Fluent.Pdf.Workers.PdfWorkerCredentials.PdfWorkerCredentials(string! tenant, byte[]! authenticationKey) -> void
Fluent.Pdf.Workers.PdfWorkerCredentials.Tenant.get -> string!
Fluent.Pdf.Workers.PdfWorkerPdf
Fluent.Pdf.Workers.PdfWorkerPdf.OpenPdfStream() -> System.IO.Stream!
Fluent.Pdf.Workers.PdfWorkerPdf.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.Workers.PdfWorkerTenant
Fluent.Pdf.Workers.PdfWorkerTenant.Credentials.get -> Fluent.Pdf.Workers.PdfWorkerCredentials!
Fluent.Pdf.Workers.PdfWorkerTenant.MaximumConcurrentJobs.get -> int
Fluent.Pdf.Workers.PdfWorkerTenant.MaximumElapsedTime.get -> System.TimeSpan
Fluent.Pdf.Workers.PdfWorkerTenant.MaximumInputBytes.get -> int
Fluent.Pdf.Workers.PdfWorkerTenant.MaximumJobs.get -> int
Fluent.Pdf.Workers.PdfWorkerTenant.MaximumOutputBytes.get -> int
Fluent.Pdf.Workers.PdfWorkerTenant.PdfWorkerTenant(Fluent.Pdf.Workers.PdfWorkerCredentials! credentials, int maximumConcurrentJobs = 1, int maximumJobs = 1000, int maximumInputBytes = 8388608, int maximumOutputBytes = 67108864, System.TimeSpan? maximumElapsedTime = null) -> void
Fluent.Pdf.Workers.PdfWorkerTransientException
Fluent.Pdf.Workers.PdfWorkerTransientException.PdfWorkerTransientException() -> void
```

## Member documentation

### <code>M:Fluent.Pdf.Workers.IPdfRenderWorkerTransport.Exchange(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Exchanges one authenticated request/response. Poll the context, preserve endpoint/tenant isolation and expire all stream work before returning.

### <code>M:Fluent.Pdf.Workers.NativeWorkerHost.#ctor</code>

Public parameterless construction. Runs one pinned native bridge in a fresh platform-sandboxed process; never loads it into the caller.

### <code>M:Fluent.Pdf.Workers.NativeWorkerHost.Execute(Fluent.Pdf.Workers.NativeWorkerOptions,System.Byte[],Fluent.Pdf.PdfOperationContext)</code>

Executes a bounded binary request and returns owned response bytes only after a valid reply and successful exit.

Remarks: Every call stages/rechecks immutable assets, clears inherited environment, and destroys its own job directory. Request snapshots and response allocation charge the operation expanded-byte budget. Providers retain responsibility for PDF input/output leases.

### <code>M:Fluent.Pdf.Workers.NativeWorkerHost.Execute(Fluent.Pdf.Workers.NativeWorkerOptions,System.Byte[],Fluent.Pdf.PdfOperationContext,System.Int32)</code>

Executes with a request-specific response ceiling no greater than the configured maximum.

### <code>M:Fluent.Pdf.Workers.NativeWorkerOptions.#ctor(Fluent.Pdf.PdfNativeDeployment,System.String,System.String,System.String,System.Int64,System.Int32,System.Int32,System.Nullable{System.TimeSpan},System.Boolean,System.Boolean)</code>

Captures pinned executable/library names, an owned-job root and hard input/output ceilings.

### <code>M:Fluent.Pdf.Workers.PdfRenderWorkerClient.#ctor(Fluent.Pdf.Workers.PdfWorkerCredentials,Fluent.Pdf.Workers.IPdfRenderWorkerTransport,System.Int32)</code>

Selects explicit credentials/transport and one to three attempts. Retries are disabled by default and apply only to the transient marker.

### <code>M:Fluent.Pdf.Workers.PdfRenderWorkerClient.Render(Fluent.Pdf.PdfHtmlSource,Fluent.Pdf.PdfHtmlOptions,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Implements only default offline static HTML options and virtual-origin markup without resource maps. Other browser environments/options reject before transport.

### <code>M:Fluent.Pdf.Workers.PdfRenderWorkerClient.RenderHtml(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Authenticates one bounded UTF8 HTML source and returns only an authenticated, nonempty, structurally parsed PDF.

### <code>M:Fluent.Pdf.Workers.PdfRenderWorkerService.#ctor(System.Collections.Generic.IEnumerable{Fluent.Pdf.Workers.PdfWorkerTenant},Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy)</code>

Captures at most64 explicit tenant registrations and one provisioned renderer. It never discovers a process/engine or downloads dependencies.

### <code>M:Fluent.Pdf.Workers.PdfRenderWorkerService.Process(System.IO.Stream,System.IO.Stream,System.Threading.CancellationToken)</code>

Authenticates before rendering, enforces quotas and validates the complete PDF before returning an authenticated response. Caller streams stay open; host maps failures without leaking credentials.

### <code>M:Fluent.Pdf.Workers.PdfWorkerCredentials.#ctor(System.String,System.Byte[])</code>

Captures a 32–64-byte authentication key and a bounded ASCII tenant identity. No credential is discovered or logged.

### <code>M:Fluent.Pdf.Workers.PdfWorkerCredentials.Dispose</code>

Clears the captured key. Drain outstanding work before disposing caller-owned credentials.

### <code>M:Fluent.Pdf.Workers.PdfWorkerPdf.OpenPdfStream</code>

Opens a fresh read-only completed PDF view.

### <code>M:Fluent.Pdf.Workers.PdfWorkerTenant.#ctor(Fluent.Pdf.Workers.PdfWorkerCredentials,System.Int32,System.Int32,System.Int32,System.Int32,System.Nullable{System.TimeSpan})</code>

Captures authenticated job/count/input/output/time ceilings. Static HTML input is at most 8 MiB; generated PDF is at most 64 MiB.

### <code>M:Fluent.Pdf.Workers.PdfWorkerTransientException.#ctor</code>

Creates a credential-free transient marker.

### <code>P:Fluent.Pdf.Workers.NativeWorkerOptions.AllowInProcessThreads</code>

Gets whether Linux permits threads in the same process under additional unprivileged-user and task-count restrictions.

### <code>P:Fluent.Pdf.Workers.NativeWorkerOptions.CaptureWorkerDiagnostics</code>

Gets whether bounded worker stderr may be retained in failure exception data; enable only when that output may be disclosed.

### <code>P:Fluent.Pdf.Workers.NativeWorkerOptions.Deployment</code>

Gets the immutable pinned deployment, including the native worker executable.

### <code>P:Fluent.Pdf.Workers.NativeWorkerOptions.ExecutableFileName</code>

Gets the pinned worker executable file name.

### <code>P:Fluent.Pdf.Workers.NativeWorkerOptions.LibraryFileName</code>

Gets the pinned engine bridge library file name.

### <code>P:Fluent.Pdf.Workers.NativeWorkerOptions.MaximumElapsedTime</code>

Gets the per-worker deadline, additionally constrained by the operation context.

### <code>P:Fluent.Pdf.Workers.NativeWorkerOptions.MaximumInputBytes</code>

Gets the maximum request bytes accepted by the bridge.

### <code>P:Fluent.Pdf.Workers.NativeWorkerOptions.MaximumMemoryBytes</code>

Gets the process memory ceiling in bytes.

### <code>P:Fluent.Pdf.Workers.NativeWorkerOptions.MaximumOutputBytes</code>

Gets the maximum response bytes accepted from the bridge.

### <code>P:Fluent.Pdf.Workers.NativeWorkerOptions.TemporaryDirectory</code>

Gets the trusted local parent for private, single-job staging directories.

### <code>P:Fluent.Pdf.Workers.PdfRenderWorkerClient.Descriptor</code>

Gets the explicit static-render service identity, without host qualification claims.

### <code>P:Fluent.Pdf.Workers.PdfWorkerCredentials.Tenant</code>

Gets the exact tenant identity.

### <code>P:Fluent.Pdf.Workers.PdfWorkerPdf.OperationReport</code>

Gets completed client evidence; it does not qualify the remote renderer.

### <code>P:Fluent.Pdf.Workers.PdfWorkerTenant.Credentials</code>

Gets caller-owned credentials; the service never disposes them.

### <code>P:Fluent.Pdf.Workers.PdfWorkerTenant.MaximumConcurrentJobs</code>

Gets active-job ceiling.

### <code>P:Fluent.Pdf.Workers.PdfWorkerTenant.MaximumElapsedTime</code>

Gets the tightened job deadline.

### <code>P:Fluent.Pdf.Workers.PdfWorkerTenant.MaximumInputBytes</code>

Gets exact source-byte ceiling.

### <code>P:Fluent.Pdf.Workers.PdfWorkerTenant.MaximumJobs</code>

Gets accepted job/attempt ceiling for this service lifetime.

### <code>P:Fluent.Pdf.Workers.PdfWorkerTenant.MaximumOutputBytes</code>

Gets generated-PDF byte ceiling.

### <code>T:Fluent.Pdf.Workers.IPdfRenderWorkerTransport</code>

Caller-owned protected worker transport. Route to an explicitly provisioned process/service and map its stable failures; do not download an engine or redirect credentials.

### <code>T:Fluent.Pdf.Workers.NativeWorkerHost</code>

Runs one pinned native bridge in a fresh platform-sandboxed process; never loads it into the caller.

### <code>T:Fluent.Pdf.Workers.NativeWorkerOptions</code>

Explicit offline deployment and limits for a fresh isolated native bridge process.

### <code>T:Fluent.Pdf.Workers.PdfRenderWorkerClient</code>

Authenticated static-HTML render client with exact request/tenant/nonce/input binding and explicit bounded idempotent retries. Caller owns credentials and transport.

### <code>T:Fluent.Pdf.Workers.PdfRenderWorkerService</code>

Original authenticated static-HTML worker protocol handler with finite per-tenant concurrency/jobs/bytes/deadlines. Hosting, transport protection and isolated engine provisioning remain explicit.

### <code>T:Fluent.Pdf.Workers.PdfWorkerCredentials</code>

Caller-owned per-tenant shared secret. Authentication does not provide transport confidentiality; use a protected local channel or authenticated TLS transport.

### <code>T:Fluent.Pdf.Workers.PdfWorkerPdf</code>

Completed immutable worker PDF and its client operation evidence.

### <code>T:Fluent.Pdf.Workers.PdfWorkerTenant</code>

Explicit finite server-side tenant quotas. A new service instance is an explicit quota reset; no clock-driven reset or ambient account discovery occurs.

### <code>T:Fluent.Pdf.Workers.PdfWorkerTransientException</code>

Explicit transient transport marker; only the idempotent static-render operation may retry it, within the caller-selected attempt ceiling.

