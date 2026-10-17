# FP-001 public API contract

FP-001 implements an in-memory page model and explicit provider-backed I/O boundary. There is no built-in PDF parser, writer, renderer, HTML engine or OCR engine. A registered capability is provider metadata, not a production qualification claim. Full operation policies and content diagnostics are FP-002 work.

## Facade and editing

All public contracts live in `Fluent.Pdf` and have identical signatures on `net472`, `netstandard2.0`, `net8.0` and `net10.0`. Alias the facade to avoid confusing its type name with the namespace:

```csharp
using Fluent.Pdf;
using PdfFacade = Fluent.Pdf.Pdf;

var options = new PdfOptions(PdfPageSize.Letter, maximumPages: 10);
using var document = PdfFacade.Create(options)
    .AddPage()
    .AddPage(PdfPageSize.A4);
document.Pages[0].Rotation = PdfPageRotation.Clockwise90;
var snapshot = document.CreateSnapshot();
document.Pages.Move(0, 1);
// snapshot still contains the original page order and geometry.
```

```vb
Imports Fluent.Pdf
Imports PdfFacade = Fluent.Pdf.Pdf

Dim options As New PdfOptions(PdfPageSize.Letter, maximumPages:=10)
Using document As PdfDocument = PdfFacade.Create(options).
    AddPage().
    AddPage(PdfPageSize.A4)
    document.Pages(0).Rotation = PdfPageRotation.Clockwise90
    Dim snapshot = document.CreateSnapshot()
    document.Pages.Move(0, 1)
End Using
```

The [C# consumer](../examples/Fluent.Pdf.Contract.CSharp/ContractExample.cs) and [VB consumer](../examples/Fluent.Pdf.Contract.VisualBasic/ContractExample.vb) compile on all four TFMs and execute in the contract suite. They also assert missing-writer behavior. These consumers use project references until final package qualification is permitted.

`PdfOptions` is immutable and defaults to A4 and a maximum of 10,000 pages. Sizes use points, with finite positive width/height. A default-initialized `PdfPageSize` is invalid and is rejected at model boundaries. Rotations are the four named quarter-turn values. Page indices are zero-based. Add, move, remove and clear validate before mutating their collection; moving preserves page identity. Removed/cleared page handles become invalid. Collection changes invalidate enumeration, including changes before its first move.

`CreateSnapshot()` copies the current geometry into an immutable, detached `PdfDocumentSnapshot`. It survives subsequent editing and document disposal. Snapshots currently contain only the geometry represented by this model; later content features must extend them deliberately.

## Ownership and concurrency

Dispose each document. Disposal is idempotent and invalidates retained live pages/collections. Live APIs then throw `ObjectDisposedException`; `IsDisposed` remains readable. Removed pages throw `InvalidOperationException` while their former owner is alive. Immutable options, values, metadata and detached snapshots remain readable.

Serialize all access to a single mutable document. Reentrant I/O is rejected, and writer callbacks cannot mutate or dispose their source document. This is a misuse guard, not synchronization for concurrent access. Independent documents can share immutable configuration and a thread-safe provider registry. Registered provider instances remain caller-owned; the caller must supply thread-safe providers for parallel jobs and dispose them after all jobs complete.

## Explicit providers

`PdfProviderRegistry` captures immutable descriptors once and accepts up to 64 caller-owned providers. IDs and version strings are nonempty and limited to 128 characters. IDs compare ordinally without case. Duplicate IDs, duplicate services, invalid capabilities, null entries and read/write declarations lacking the matching service interface are rejected during registration. Lists are defensively copied and exposed read-only. There is no global registry, engine discovery, fallback or automatic download.

`Supports` reports registration. `GetRequired<T>` resolves the requested contract. `ReadDocument` uses `IPdfDocumentReader`; `WriteDocument` uses `IPdfDocumentWriter`. `RenderPages`, `RenderHtml` and `RecognizeText` reserve capability identities; their operation interfaces and implementations belong to later requirements. Engine hashes, ABI/RID probes and qualification manifests also remain later work; descriptor version text alone proves none of these.

Readers synchronously populate an empty document, honoring its options and cancellation token. They must finish all work before returning and must not retain the document or stream. Writers receive a detached immutable snapshot and borrowed output. Provider execution is trusted extension code; these contracts do not isolate or forcibly interrupt a provider that ignores cancellation. Native/browser supervision belongs to the relevant worker requirements.

## Streams and paths

| Entry point | Ownership and behavior |
| --- | --- |
| `Pdf.Open(Stream, ..., CancellationToken)` | Reads from current position. Requires readable input. Caller retains the stream. Partial documents are disposed on failure. |
| `Pdf.OpenFile(string, ..., CancellationToken)` | SDK opens and closes the file before returning, including error paths. |
| `document.Save(Stream, CancellationToken)` | Writes from current position. Requires writable output. Caller retains the stream. Does not rewind/truncate; failure or cancellation can leave partial bytes. |
| `document.SaveFile(string, CancellationToken)` | Requires an existing destination directory. Writes a private same-directory temporary file, flushes it, then uses atomic move/replace. Failure before commit preserves the destination. |

Providers receive directional stream leases that expire on return, including failure. Closing a lease never closes the underlying caller stream. Reading through an output lease and writing through an input lease are rejected. A provider declares whether it requires seekability; incompatible streams fail before the callback. No implicit buffering/rewinding is added. Providers that need read/write scratch storage must own that storage themselves.

Path saves never delete an existing destination before replacement and never fall back to copying. Filesystem atomic-replace failures propagate. Atomic visibility is distinct from power-loss durability of directory metadata. Cancellation is checked before work, after callbacks and before file commit. Cancellation after commit cannot roll back the destination. Temporary cleanup preserves the primary exception and attaches cleanup I/O failures in `Exception.Data["Fluent.Pdf.CleanupFailure"]`.

## Failure contract

| Condition | Result |
| --- | --- |
| No registered capability | `PdfProviderUnavailableException`, diagnostic `Provider.Unavailable` and requested capability; no I/O performed |
| Registered provider lacks requested generic contract | `PdfException`, `Provider.ContractMismatch`, capability and provider ID |
| Page budget reached | `PdfException`, `Limit.Pages`; model remains unchanged |
| Provider requires seekability | `PdfException`, `Stream.SeekRequired`; callback not invoked |
| Nested document I/O | `PdfException`, `Operation.Reentrant`; no nested file/stream I/O |
| Canceled operation | `OperationCanceledException`; caller streams remain open |
| Invalid caller argument or object lifetime | Standard argument, invalid-operation or disposed-object exception |
| Provider or filesystem failure | Original exception propagates; no generic success/fallback result |

Diagnostics contain bounded code/message/provider strings (128/2,048/128 characters) and optional capability. Built-in messages contain no document payload or credentials. Providers are responsible for their own diagnostic text. FP-002 will extend the policy, severity, provenance and loss-reporting context; do not invent permissive content handling in this layer.

## Evidence and compatibility

[PublicAPI.Unshipped.txt](../src/Fluent.Pdf/PublicAPI.Unshipped.txt) records every current public declaration. Public API analyzers fail builds on missing/stale declarations; `eng/check-api.ps1` compares all TFM assemblies in strict mode, including parameter names. The unshipped contract can evolve only with review and synchronized consumers/docs.

[Contract tests](../tests/Fluent.Pdf.Contract.Tests) exercise editing, ownership, callback failure, cancellation, resource boundaries, nonseekable streams, atomic saves, retained handles and C#/VB examples. A 64-logical-worker by 200-iteration fixture checks independent document isolation; it emits no PDF and does not establish full mixed-workload production concurrency or throughput. [Testing instructions](testing.md) define evidence and remaining qualification gaps.

Actual Framework 4.7.2 and exact final-package execution remain pending. No requirement is production-qualified by these source tests. `productionReady` remains false and packing/publication remain blocked.
