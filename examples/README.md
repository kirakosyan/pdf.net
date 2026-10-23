# Executable contract examples

The [C# example](Fluent.Pdf.Contract.CSharp/ContractExample.cs) and [VB example](Fluent.Pdf.Contract.VisualBasic/ContractExample.vb) exercise the FP-001 page model, snapshots and typed missing-provider result. `SaveWithReport` configures FP-002 permissive processing and explicit limits, returning every reported loss from an application-supplied provider. `InspectDeployment` captures exact caller-supplied asset digests/lengths and engine/ABI labels, then performs the partial FP-085 read-only integrity preflight. It never loads an engine or proves execution safety; see [native deployment](../docs/native-deployment.md). `ExecuteNativeWorker` invokes the optional isolated bridge with an explicit operation context. `InspectSyntax` reads actual bounded PDF syntax and preserves opaque bytes without executing actions. `DecodeStream` resolves an indirect encoded stream and returns immutable decoded data with its operation report. Both compile for net472, netstandard2.0, net8.0 and net10.0 and execute through [ExampleTests](../tests/Fluent.Pdf.Contract.Tests/ExampleTests.cs). See the [API contract](../docs/api-contract.md) and [operation policy contract](../docs/operation-policies.md).

Build and run using [docs/testing.md](../docs/testing.md). The examples currently reference the source project. The final release gate must rerun them with exact NuGet package references in fresh consumers; no package is produced or published at this stage. These examples do not generate a PDF or install an engine.

`WriteCanvasPdf` in both languages uses millimeters, margins, a scoped rotation and nonzero-origin page geometry. It writes a real vector PDF through the managed provider. These project-reference consumers must become exact final-package consumers at qualification.

`WriteFlowPdf` in C# and VB reads a caller-supplied font, uses an explicitly supplied shaper, wraps logical text into page canvases and writes a real PDF. Borrowed font/output streams remain open. Exact final-package consumers remain gated.

`WriteTablePdf` in both languages binds a finite typed source with explicit culture, uses fixed columns and end alignment, repeats header rows across real PDF pages, and preserves both borrowed streams. Final-package consumer qualification remains blocked.

`WriteCompositionPdf` in C# and VB combines typed lists, a fixed-height column region and an explicit footnote. It emits basic column structure and retains caller font/output streams. It does not declare PDF/UA conformance.

`WriteNavigationPdf` in C# and VB emits two pages, a named destination, an internal Link and catalog OpenAction without executing navigation.

`WritePresentationPdf` in C# and VB emits nested bookmarks, restarted page labels, a viewer mode and a passive page transition.

`RenderPagePreview` in both language consumers renders through an explicit provider registry and returns detached raw BGRA samples with exact geometry. `PageRenderer` captures a caller-provisioned offline isolated deployment. Neither method downloads engines or claims production qualification.

`AnalyzeSelectedText` in C# and VB selects one explicit source page with a caller-supplied Intelligence provider and per-job options. Remote origin authority, image authority and token/configured-cost reservations are explicit. The source consumers execute deterministic labeled adapter fixtures; qualified live model/transport and exact final-package consumers remain mandatory. See [document intelligence](../docs/document-intelligence.md).

`ChunkSelectedPage` in C# and VB captures one explicitly selected source page with a finite UTF8 byte ceiling, preserving exact raw mapped text and source provenance. See [semantic chunks and retrieval](../docs/semantic-retrieval.md); full tagged semantics and exact final-package consumers remain mandatory.

Original [VBScript](com/automation.vbs) and [native dual/IDispatch](com/automation-native.cpp) consumers exercise the explicit Windows facade. The Windows harness records actual bitness/CLR, owned registration/removal, Unicode and SAFEARRAY behavior separately from portable managed tests; complete final-package/apartment/installer qualification remains mandatory. See [Windows automation](../docs/windows-automation.md).
