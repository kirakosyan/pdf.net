# Fluent.Pdf.Applications source reference

Version `1.0.0`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).

## Public declarations

The baseline preserves exact nullability, parameter names, defaults and constant values.

```text
Fluent.Pdf.Applications.PdfApplicationFrame
Fluent.Pdf.Applications.PdfApplicationFrame.CapturedText.get -> string!
Fluent.Pdf.Applications.PdfApplicationFrame.Raster.get -> Fluent.Pdf.PdfRaster!
Fluent.Pdf.Applications.PdfApplicationFrame.State.get -> Fluent.Pdf.Applications.PdfApplicationViewState!
Fluent.Pdf.Applications.PdfApplicationFrame.WritePng(System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.Applications.PdfApplicationViewCommand
Fluent.Pdf.Applications.PdfApplicationViewCommand.ActualSize = 6 -> Fluent.Pdf.Applications.PdfApplicationViewCommand
Fluent.Pdf.Applications.PdfApplicationViewCommand.Cancel = 7 -> Fluent.Pdf.Applications.PdfApplicationViewCommand
Fluent.Pdf.Applications.PdfApplicationViewCommand.FirstPage = 2 -> Fluent.Pdf.Applications.PdfApplicationViewCommand
Fluent.Pdf.Applications.PdfApplicationViewCommand.LastPage = 3 -> Fluent.Pdf.Applications.PdfApplicationViewCommand
Fluent.Pdf.Applications.PdfApplicationViewCommand.NextPage = 0 -> Fluent.Pdf.Applications.PdfApplicationViewCommand
Fluent.Pdf.Applications.PdfApplicationViewCommand.PreviousPage = 1 -> Fluent.Pdf.Applications.PdfApplicationViewCommand
Fluent.Pdf.Applications.PdfApplicationViewCommand.ZoomIn = 4 -> Fluent.Pdf.Applications.PdfApplicationViewCommand
Fluent.Pdf.Applications.PdfApplicationViewCommand.ZoomOut = 5 -> Fluent.Pdf.Applications.PdfApplicationViewCommand
Fluent.Pdf.Applications.PdfApplicationViewState
Fluent.Pdf.Applications.PdfApplicationViewState.DiagnosticCode.get -> string!
Fluent.Pdf.Applications.PdfApplicationViewState.Generation.get -> long
Fluent.Pdf.Applications.PdfApplicationViewState.IsBusy.get -> bool
Fluent.Pdf.Applications.PdfApplicationViewState.PageCount.get -> int
Fluent.Pdf.Applications.PdfApplicationViewState.PageIndex.get -> int
Fluent.Pdf.Applications.PdfApplicationViewState.Zoom.get -> double
Fluent.Pdf.Applications.PdfApplicationViewer
Fluent.Pdf.Applications.PdfApplicationViewer.CachedBytes.get -> long
Fluent.Pdf.Applications.PdfApplicationViewer.Cancel() -> void
Fluent.Pdf.Applications.PdfApplicationViewer.Dispose() -> void
Fluent.Pdf.Applications.PdfApplicationViewer.Execute(Fluent.Pdf.Applications.PdfApplicationViewCommand command) -> Fluent.Pdf.Applications.PdfApplicationViewState!
Fluent.Pdf.Applications.PdfApplicationViewer.PdfApplicationViewer(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfProviderRegistry! providers, long maximumCachedBytes = 16777216, long maximumSurfacePixels = 16000000, int maximumTextCharacters = 100000, bool captureText = true, Fluent.Pdf.PdfOperationPolicy? policy = null) -> void
Fluent.Pdf.Applications.PdfApplicationViewer.Print(Fluent.Pdf.PdfPrintOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfPrintJob!
Fluent.Pdf.Applications.PdfApplicationViewer.RenderAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Threading.Tasks.Task<Fluent.Pdf.Applications.PdfApplicationFrame!>!
Fluent.Pdf.Applications.PdfApplicationViewer.SetView(int pageIndex, double zoom) -> Fluent.Pdf.Applications.PdfApplicationViewState!
Fluent.Pdf.Applications.PdfApplicationViewer.State.get -> Fluent.Pdf.Applications.PdfApplicationViewState!
Fluent.Pdf.Applications.PdfApplicationViewer.StateChanged -> System.EventHandler<Fluent.Pdf.Applications.PdfApplicationViewState!>?
```

## Member documentation

### <code>E:Fluent.Pdf.Applications.PdfApplicationViewer.StateChanged</code>

Raised outside locks on the invoking/completion thread. Hosts marshal to their own dispatcher and compare generation. Subscriber exceptions are the host's responsibility.

### <code>F:Fluent.Pdf.Applications.PdfApplicationViewCommand.ActualSize</code>

Restore 96 DPI display scale.

### <code>F:Fluent.Pdf.Applications.PdfApplicationViewCommand.Cancel</code>

Cancel only this controller's current request.

### <code>F:Fluent.Pdf.Applications.PdfApplicationViewCommand.FirstPage</code>

First physical page.

### <code>F:Fluent.Pdf.Applications.PdfApplicationViewCommand.LastPage</code>

Last physical page.

### <code>F:Fluent.Pdf.Applications.PdfApplicationViewCommand.NextPage</code>

Next physical page, bounded at the final page.

### <code>F:Fluent.Pdf.Applications.PdfApplicationViewCommand.PreviousPage</code>

Previous physical page, bounded at the first page.

### <code>F:Fluent.Pdf.Applications.PdfApplicationViewCommand.ZoomIn</code>

Increase explicit zoom by 25 percent, capped at 4.

### <code>F:Fluent.Pdf.Applications.PdfApplicationViewCommand.ZoomOut</code>

Decrease explicit zoom by 25 percent, capped at 0.25.

### <code>M:Fluent.Pdf.Applications.PdfApplicationFrame.WritePng(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Encodes this detached raster as bounded PNG in a separate explicit encoding operation. Caller output remains borrowed and open; render and encoding reports have independent policies.

### <code>M:Fluent.Pdf.Applications.PdfApplicationViewer.#ctor(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfProviderRegistry,System.Int64,System.Int64,System.Int32,System.Boolean,Fluent.Pdf.PdfOperationPolicy)</code>

Captures one immutable source with explicit renderer, private bounded cache and surface/text ceilings. Independent render and captured-text operations each use the immutable policy. No fallback renderer/OCR is selected.

### <code>M:Fluent.Pdf.Applications.PdfApplicationViewer.Cancel</code>

Requests cooperative cancellation of this controller's current work without disposing providers or another controller's session.

### <code>M:Fluent.Pdf.Applications.PdfApplicationViewer.Dispose</code>

Cancels work and drops only this controller's private cache. Does not block provider callbacks or dispose caller-owned providers/documents.

### <code>M:Fluent.Pdf.Applications.PdfApplicationViewer.Execute(Fluent.Pdf.Applications.PdfApplicationViewCommand)</code>

Maps explicit local application commands. Page/zoom endpoints remain bounded; no navigation action, URL or print job is executed.

### <code>M:Fluent.Pdf.Applications.PdfApplicationViewer.Print(Fluent.Pdf.PdfPrintOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Submits only an explicit caller-selected printer/options/provider; no control or open/render operation prints automatically.

### <code>M:Fluent.Pdf.Applications.PdfApplicationViewer.RenderAsync(System.Threading.CancellationToken)</code>

Renders the current view asynchronously and captures bounded text separately. Superseded/canceled generations never return a frame; controls must also compare generation after posting to a dispatcher.

### <code>M:Fluent.Pdf.Applications.PdfApplicationViewer.SetView(System.Int32,System.Double)</code>

Applies a bounded explicit page/zoom and cancels the preceding request before raising a state change.

### <code>P:Fluent.Pdf.Applications.PdfApplicationFrame.CapturedText</code>

Gets bounded raw mapped source text, excluding hidden layers/nonpainting text. Does not establish visible ink or author-confirmed semantics.

### <code>P:Fluent.Pdf.Applications.PdfApplicationFrame.Raster</code>

Gets immutable top-down BGRA pixels and exact displayed/native coordinate frame.

### <code>P:Fluent.Pdf.Applications.PdfApplicationFrame.State</code>

Gets the exact page/zoom/generation producing this frame.

### <code>P:Fluent.Pdf.Applications.PdfApplicationViewState.DiagnosticCode</code>

Gets a stable diagnostic code only, without paths, source payloads or raw exception text.

### <code>P:Fluent.Pdf.Applications.PdfApplicationViewState.Generation</code>

Gets monotonic request/view generation.

### <code>P:Fluent.Pdf.Applications.PdfApplicationViewState.IsBusy</code>

Gets whether the current generation is rendering.

### <code>P:Fluent.Pdf.Applications.PdfApplicationViewState.PageCount</code>

Gets immutable source page count.

### <code>P:Fluent.Pdf.Applications.PdfApplicationViewState.PageIndex</code>

Gets zero-based physical page.

### <code>P:Fluent.Pdf.Applications.PdfApplicationViewState.Zoom</code>

Gets explicit scale relative to 96 DPI, within 0.25..4.

### <code>P:Fluent.Pdf.Applications.PdfApplicationViewer.CachedBytes</code>

Gets this controller's retained raster-cache bytes, excluding caller frames and in-flight operations.

### <code>P:Fluent.Pdf.Applications.PdfApplicationViewer.State</code>

Gets an immutable synchronized state snapshot.

### <code>T:Fluent.Pdf.Applications.PdfApplicationFrame</code>

Detached rendered page plus explicitly captured source-order text. This is not certified logical reading order or PDF/UA remediation.

### <code>T:Fluent.Pdf.Applications.PdfApplicationViewCommand</code>

Explicit application commands. Controls map local keyboard/button gestures without global hooks or document-directed actions.

### <code>T:Fluent.Pdf.Applications.PdfApplicationViewState</code>

Immutable page/zoom/request state. Generation allows UI dispatchers to reject stale posted work.

### <code>T:Fluent.Pdf.Applications.PdfApplicationViewer</code>

Optional toolkit-independent application controller over shared viewing primitives. Owns its private session/cache, never caller providers. No Reader, UI, network or automatic printer dependency.

