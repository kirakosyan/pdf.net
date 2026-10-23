# Fluent.Pdf.Rendering source reference

Version `1.0.0`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).

## Public declarations

The baseline preserves exact nullability, parameter names, defaults and constant values.

```text
Fluent.Pdf.Rendering.IPdfIppTransport
Fluent.Pdf.Rendering.IPdfIppTransport.Exchange(System.Uri! printer, System.IO.Stream! request, System.IO.Stream! response, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Rendering.IppPrintProvider
Fluent.Pdf.Rendering.IppPrintProvider.Cancel(string! printer, int jobId, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfPrintJob!
Fluent.Pdf.Rendering.IppPrintProvider.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Rendering.IppPrintProvider.GetStatus(string! printer, int jobId, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfPrintJob!
Fluent.Pdf.Rendering.IppPrintProvider.IppPrintProvider(Fluent.Pdf.Rendering.IPdfIppTransport! transport) -> void
Fluent.Pdf.Rendering.IppPrintProvider.Submit(System.IO.Stream! pdf, int pageCount, Fluent.Pdf.PdfPrintOptions! options, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfPrintJob!
Fluent.Pdf.Rendering.IsolatedPdfRenderer
Fluent.Pdf.Rendering.IsolatedPdfRenderer.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Rendering.IsolatedPdfRenderer.IsolatedPdfRenderer(Fluent.Pdf.Workers.NativeWorkerOptions! worker) -> void
Fluent.Pdf.Rendering.IsolatedPdfRenderer.Render(Fluent.Pdf.PdfRasterRequest! request, System.IO.Stream! bgraOutput, Fluent.Pdf.PdfOperationContext! context) -> void
const Fluent.Pdf.Rendering.IsolatedPdfRenderer.BridgeAbi = "fluent-pdf-render-1" -> string!
const Fluent.Pdf.Rendering.IsolatedPdfRenderer.BridgeVersion = "153.0.7999.0" -> string!
```

## Member documentation

### <code>F:Fluent.Pdf.Rendering.IsolatedPdfRenderer.BridgeAbi</code>

Gets the original bridge ABI.

### <code>F:Fluent.Pdf.Rendering.IsolatedPdfRenderer.BridgeVersion</code>

Gets the required candidate engine build.

### <code>M:Fluent.Pdf.Rendering.IPdfIppTransport.Exchange(System.Uri,System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Exchanges exactly one application/ipp request/response over an explicit local or authenticated remote connection. Leases expire on return; SDK response storage is bounded.

### <code>M:Fluent.Pdf.Rendering.IppPrintProvider.#ctor(Fluent.Pdf.Rendering.IPdfIppTransport)</code>

Captures an explicitly configured authenticated transport. The caller retains ownership.

### <code>M:Fluent.Pdf.Rendering.IppPrintProvider.Cancel(System.String,System.Int32,Fluent.Pdf.PdfOperationContext)</code>

Requests cancellation once. An acknowledgment cannot recall physical output.

### <code>M:Fluent.Pdf.Rendering.IppPrintProvider.GetStatus(System.String,System.Int32,Fluent.Pdf.PdfOperationContext)</code>

Queries one spool job; completion is only a spool observation.

### <code>M:Fluent.Pdf.Rendering.IppPrintProvider.Submit(System.IO.Stream,System.Int32,Fluent.Pdf.PdfPrintOptions,Fluent.Pdf.PdfOperationContext)</code>

Submits a PDF once with attribute fidelity required. A rejected/ambiguous response never triggers a retry.

### <code>M:Fluent.Pdf.Rendering.IsolatedPdfRenderer.#ctor(Fluent.Pdf.Workers.NativeWorkerOptions)</code>

Captures a complete offline deployment. No binaries, fonts or models are downloaded by the adapter.

### <code>M:Fluent.Pdf.Rendering.IsolatedPdfRenderer.Render(Fluent.Pdf.PdfRasterRequest,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Validates the supported profile and paints exact straight-alpha BGRA samples through isolated native execution.

### <code>P:Fluent.Pdf.Rendering.IppPrintProvider.Descriptor</code>

Gets the source IPP adapter identity, without hardware/platform qualification claims.

### <code>P:Fluent.Pdf.Rendering.IsolatedPdfRenderer.Descriptor</code>

Gets declared source capability, independent of production qualification.

### <code>T:Fluent.Pdf.Rendering.IPdfIppTransport</code>

Caller-owned authenticated IPP transport. It must bind the exact printer endpoint, enforce its network/credential policy and terminate on context cancellation/deadline. No redirects or implicit retries are permitted.

### <code>T:Fluent.Pdf.Rendering.IppPrintProvider</code>

Original IPP 2.0 PDF print-job adapter for explicit CUPS/IPP endpoints. No printer discovery, host process launch or runtime download occurs.

### <code>T:Fluent.Pdf.Rendering.IsolatedPdfRenderer</code>

An offline candidate PDFium adapter. Each request uses a fresh hash-pinned worker; this source profile is not engine qualification.

