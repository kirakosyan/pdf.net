# Task facade and explicit plugin composition

`PdfTasks` in the optional Formats assembly captures an immutable provider registry, operation policy and collection-adapter registry. Each call starts a fresh task budget with that policy. It never installs/discovers plugins, changes global registration or owns caller providers. The default is strict, with no optional providers; an input document's policy does not silently override the facade policy.

| Task | Existing implementation and ownership |
| --- | --- |
| `Generate` / `GenerateFile` | Flow layout, document composition and original managed writing share one context; complete PDF stages within64MiB before stream copy or atomic file replacement. Explicit block fonts and a shaping provider are required. The temporary document is disposed. |
| `Convert` / `ConvertFile` | `PdfConversions.Export` / `ExportFile` with unchanged selected page order, exact profile, provider requirements and diagnostics. |
| `Extract` | `PdfTextExtraction.ReadPage`; returns detached text, mapping and geometry observations. No OCR is inferred. |
| `Fill` | `PdfForms.FillData`; returns a new caller-owned document. Explicit appearance fonts/shaping remain in `PdfFormTextStyle`, and save is a separate operation. No calculations/actions execute. |
| `Optimize` / `OptimizeFile` | `PdfOptimization.Write` / `WriteFile`; preserves explicit loss/signature authority, measured counts, hashes and staged/atomic output behavior. |
| `Inspect` | Passive `PdfPreflight.Inspect`, including the injected provider inventory. |
| `Collect` / `Process` | Bounded `PdfDocumentCollection` capture and sequential processing with the exact injected adapter ID. No filesystem enumeration or plugin assembly loading. |

Input snapshots, streams, fonts, providers, options and adapters stay caller-owned. Stream output may be partial if its final copy fails; file output is atomic. Cancellation and latched limits propagate from the existing core. Shared immutable facade configuration does not make a mutable plugin thread-safe: independently running calls require stateless/thread-safe services or separate instances. Collection adapters are trusted code, not a security sandbox, and must use their mandatory shared context rather than start new public operations to reset a batch budget. Inspect every item outcome because ordinary per-item failures remain ordered results; fatal limits/cancellation abort the batch.

Generation intentionally uses the original managed PDF writer and explicitly injected shaping, without adding a second engine or license dependency. Returned core result types retain their normal ownership and qualification limits. Form appearances use their own explicit style registry; the facade does not overwrite it or guess fonts.

C#/VB `GenerateTaskPdf` consumers and [the F# script](../examples/TaskFacades.FSharp/Example.fsx) execute the facade against source assemblies. Run the F# consumer after a Release build with `dotnet fsi --exec examples/TaskFacades.FSharp/Example.fsx artifacts/fsharp-task-example`. This script executes on the installed SDK's modern runtime; it is not actual minimum Framework or package-only qualification.

Source tests compare generated/optimized/converted bytes and form/extraction outcomes with direct core calls, test missing-provider errors, cumulative generation budgets, plugin-instance isolation, concurrent jobs, cancellation and atomic destination preservation. Independent readers inspect original PDFs/images. Full underlying feature/runtime/provider and exact final-package qualification remains mandatory; a concise facade adds no capabilities its core engine lacks. See [evidence](evidence/FP-109-source.json).
