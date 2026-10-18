# FP-001 public API contract

FP-001 implements an in-memory page model and explicit provider-backed I/O boundary. FP-002 adds [operation policies, diagnostics and budgets](operation-policies.md). The [syntax inspection API](pdf-syntax.md) reads low-level values and revisions. FP-005 adds the original managed [page reader and full/incremental writer](pdf-writing.md). FP-007 adds [typed content and version selection](content-operators.md). FP-008 adds explicit [font providers, embedding and mappings](fonts.md). FP-009 adds [Unicode analysis, shaping and positioned text](text-shaping.md), with an optional isolated native provider. FP-010 adds [vector paths, color resources and transparency](vector-graphics.md). FP-040 adds optional [isolated headless rendering](rendering.md) with an explicit pixel-grid/alpha contract and a candidate engine profile. HTML and OCR remain later capabilities. A registered capability is provider metadata, not a production qualification claim.

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

`CreateSnapshot()` copies current geometry and immutable original object/page bindings into a detached `PdfDocumentSnapshot`. It survives subsequent editing and disposal. Writer invocations also capture immutable save choices; later content features extend snapshots explicitly.

## Ownership and concurrency

Dispose each document. Disposal is idempotent and invalidates retained live pages/collections. Live APIs then throw `ObjectDisposedException`; `IsDisposed` and the detached `LastOperationReport` remain readable. Removed pages throw `InvalidOperationException` while their former owner is alive. Immutable options, values, metadata and detached snapshots remain readable.

Serialize all access to a single mutable document. Reentrant I/O is rejected, and writer callbacks cannot mutate or dispose their source document. This is a misuse guard, not synchronization for concurrent access. Independent documents can share immutable configuration and a thread-safe provider registry. Registered provider instances remain caller-owned; the caller must supply thread-safe providers for parallel jobs and dispose them after all jobs complete.

## Explicit providers

`PdfProviderRegistry` captures immutable descriptors once and accepts up to 64 caller-owned providers. IDs and version strings are nonempty and limited to 128 characters. IDs compare ordinally without case. Duplicate IDs, duplicate services, invalid capabilities, null entries and read/write declarations lacking the matching service interface are rejected during registration. Lists are defensively copied and exposed read-only. There is no global registry, engine discovery, fallback or automatic download.

`PdfProviderRegistry.Default` contains the managed reader/writer; `Empty` disables all services. Custom registries retain their explicitly declared services. `Supports` reports registration. `GetRequired<T>` resolves the requested contract. `ReadDocument` uses `IPdfDocumentReader`; `WriteDocument` uses `IPdfDocumentWriter`. `RenderPages` uses `IPdfPageRenderer`; `CmsEnvelopes` uses `IPdfCmsEnvelopeProvider` for explicit certificate security. `RenderHtml` and `RecognizeText` reserve capability identities for later requirements. The separate [native deployment and worker adapter](native-deployment.md) checks caller-pinned local assets and supplies bounded isolated ABI/dependency execution. It grants no PDF capability. Native validation and production qualification remain pending; descriptor version text alone proves neither.

Readers synchronously populate an empty document, honoring its options and mandatory `PdfOperationContext`. Writers receive a detached immutable snapshot, borrowed output and the same operation context. They must finish all work and release depth/worker scopes before returning, without retaining live documents or leases. Context accounting is synchronized; each I/O stream still requires serialized access. Provider execution is trusted extension code; these contracts do not isolate or forcibly interrupt a provider that ignores cancellation. Native/browser supervision belongs to the relevant worker requirements.

## Streams and paths

| Entry point | Ownership and behavior |
| --- | --- |
| `Pdf.Open(Stream, ..., CancellationToken)` | Reads from current position. Requires readable input. Caller retains the stream. Partial documents are disposed on failure. |
| `Pdf.OpenFile(string, ..., CancellationToken)` | SDK opens and closes the file before returning, including error paths. |
| `document.Save(Stream, CancellationToken, writeOptions)` | Returns a detached `PdfOperationReport`. Writes from current position. Requires writable output. Caller retains the stream. Does not rewind/truncate; failure or cancellation can leave partial bytes. |
| `document.SaveFile(string, CancellationToken, writeOptions)` | Returns the report after commit. Requires an existing destination directory. Writes a private same-directory temporary file, flushes it, then uses atomic move/replace. Failure before commit preserves the destination. |

Providers receive directional, budgeted stream leases that expire on return, including failure. Closing a lease never closes the underlying caller stream. Reading through an output lease and writing through an input lease are rejected. A provider declares whether it requires seekability; incompatible streams fail before the callback. The managed reader explicitly snapshots input; the SDK adds no buffering/rewinding around other providers. Providers that need read/write scratch storage must own and budget that storage themselves. Output cannot seek before its starting position or beyond the configured relative extent. [Operation policies](operation-policies.md) define cumulative writes, growth and bounded input EOF lookahead.

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
| Resource/deadline or unreleased scope | Latched `PdfException` with `Limit.<resource>`, `Limit.ElapsedTime`, `Limit.Diagnostics` or `Operation.ScopeLeaked` |
| Reported fidelity/content loss | Strict: fatal diagnostic; permissive: retained warning in the successful report |
| Provider or filesystem failure | Original exception propagates unless a latched policy failure/cancellation/deadline takes precedence; no generic success/fallback result |

Diagnostics contain bounded code/message/provider strings (128/2,048/128 characters), capability, severity, loss category, operation, zero-based page, object ID and remediation. The context binds operation/provider/capability provenance and retains every accepted diagnostic. Built-in messages contain no document payload or credentials. Providers are responsible for their own diagnostic text. Failed operations retain their detached report, including a latched policy failure when present; failed opens dispose the partial document. See [operation-policies.md](operation-policies.md).

## Unshipped API changes in FP-002

Provider read/write methods now require `PdfOperationContext` in place of the token-only parameter. Cancellation is available as `context.CancellationToken` and is linked to the deadline, so token identity differs from the caller token. `Save`/`SaveFile` now return `PdfOperationReport`; existing statement-style calls still compile, but compiled binaries/delegates must rebuild. `PdfOptions` and `PdfDiagnostic` constructors add policy/provenance parameters. All changes are in the unshipped baseline and apply identically to every TFM; no released package contract is changed.

## Evidence and compatibility

[PublicAPI.Unshipped.txt](../src/Fluent.Pdf/PublicAPI.Unshipped.txt) records every current public declaration. Public API analyzers fail builds on missing/stale declarations; `eng/check-api.ps1` compares all TFM assemblies in strict mode, including parameter names. The unshipped contract can evolve only with review and synchronized consumers/docs.

[Contract tests](../tests/Fluent.Pdf.Contract.Tests) exercise editing, ownership, callback failure, cancellation, resource boundaries, nonseekable streams, atomic saves, retained handles and C#/VB examples. A 64-logical-worker by 200-iteration fixture checks independent document isolation; it emits no PDF and does not establish full mixed-workload production concurrency or throughput. [Testing instructions](testing.md) define evidence and remaining qualification gaps.

Actual Framework 4.7.2 and exact final-package execution remain pending. No requirement is production-qualified by these source tests. `productionReady` remains false and packing/publication remain blocked.

FP-005 also adds `PdfTransaction`, `PdfWriteOptions`, immutable original page bindings, the stateless default managed provider and optional revision choices on save methods. These changes remain in the unshipped baseline; compiled binaries/delegates must rebuild. See [managed writing](pdf-writing.md).

FP-011 adds immutable five-box page geometry, explicit units, rotated coordinate conversions, scoped canvas transforms/clips, vector/text/image placement frames and atomic page canvas application. See [page canvas and geometry](page-canvas.md).

FP-012 adds immutable styled paragraphs/runs, explicit line/tab/overflow and pagination policies, floating vectors, detached line/page geometry and shared-context canvas output. See [flow layout and pagination](flow-layout.md).

FP-013 adds immutable merged/nested table grids, bounded typed scalar binding with explicit culture, automatic/fixed widths, repeated headers and safe row fragments integrated into flow. See [tables and binding](tables.md).

FP-014 adds immutable list counters/restarts, reusable components, section parity, column regions, notes and physical baseline shifts. `CreateDocument` adds cancellation and a bounded composition operation; column output has real basic structure/parent-tree associations without claiming PDF/UA. See [composition](composition.md).

FP-015 adds final page template callbacks, explicit decorations, stable heading destinations, linked contents/index locators and standard numbering ranges. See [page templates](page-templates.md).

FP-016 adds explicit page selections, detached page organization and vector imposition with shared budgets, reference remapping and explicit interactive profiles. See [page organization](page-organization.md).

FP-017 adds immutable image assets, bounded PNG/JPEG packaging, masks/orientation, per-operation exact deduplication and captured graphics invocation extraction/editing. See [images and reusable graphics](images.md).

FP-019 adds immutable annotation definitions/source manifests, generated markup and shaped text appearances, metadata/relationships, staged editing/import, explicit flattening and owned-graph XFDF. See [annotations](annotations.md).

FP-020 adds managed `PdfDestination`, immutable `PdfAction`/`PdfActionPolicy` and `PdfNavigation` editing/inspection/sanitization. Source records and explicit profile limits are in [navigation.md](navigation.md).

FP-021 adds `PdfBookmark`, `PdfPresentation`, passive transition contracts and source outline/label/preference inventories. See [presentation.md](presentation.md) for indices, standard names and supported value/formatter profiles.

FP-022 adds `PdfMetadata`, its synchronous editor and explicit synchronization/removal policies. See [metadata.md](metadata.md) for XML limits, authority, timestamp precision and privacy boundaries.

FP-023 adds `PdfAttachments`, detached file inventories and explicit embedding/extraction options. [attachments.md](attachments.md) specifies portable filenames, variant selection, collection types and privacy boundaries.

FP-024 adds `PdfLayers`, registered group inventories, explicit configuration/usage evaluation, page XObject binding and a state-preserving flattening profile. See [layers.md](layers.md) for supported behavior and rejected state dependencies.

FP-025 adds `PdfTextExtraction.ReadPage`, source-code/ActualText provenance and separate raw/semantic/geometric result views. [text-extraction.md](text-extraction.md) defines advance-cell coordinates, mapping certainty, layer selection and current profile limits.

FP-036 adds passive `PdfForms` field/widget inventories, original hierarchy/state/choice authoring, shaped Unicode appearances, common-flag/widget/tab edits and explicit passive calculation metadata. See [forms.md](forms.md) for ownership, encoding, structure and interactive viewer profile limits.

FP-037 adds immutable field-value packets, explicit FDF/XFDF/XML/JSON profiles, transactional filling with shared-context appearances, field copying/default-font rebinding, widget decoration and selective flattening. [form-workflows.md](form-workflows.md) defines format, inheritance, ownership and signature limits.

FP-031 adds explicit full-rewrite password/certificate security, owner/user/recipient results, disposable prepared-byte credentials, permission/cipher/metadata choices and an optional offline CMS boundary. [Security contracts](security.md) describe source applicability, ownership, permission semantics and mandatory gaps.

FP-066 adds explicit archival profiles, common structural preflight, conversion gated on complete independent exact-byte validation, detached findings and mandatory shared validation-host contracts. Optional Fluent.Pdf.Compliance supplies a bounded pinned report bridge; it launches no engine. See [archival behavior and qualification gaps](archival.md).

FP-006 adds separate strict structural inspection and explicit recovery, immutable source/output views and digests, exact stream and active-identity authority, explicit orphan/signature policies and a machine-readable ledger. The ordinary parser never repairs. See [repair behavior and remaining profiles](repair.md).

FP-098 adds explicit PDF text encodings and passive version/extension/namespace/associated-file inventory, typed UTF-8 Info authoring and reserved extension Type handling. See the [read/write/preserve matrix](pdf-compatibility.md) and qualification limits.

FP-044 adds bounded immutable HTML/resources, explicit script/print requests and optional renderer contracts. The original optional Chromium protocol adapter checks exact version/revision pins, creates a private context, serves supplied resources and streams its result into the ordinary editable model. See [HTML source profiles and required host qualification](html-rendering.md).

FP-045 adds explicit per-job resource sessions, exact-origin/IP/redirect admission, private origin cookies/headers, explicit proxies/client certificates and bounded file/data snapshots in the optional HTML adapter. [Resource contracts and runtime applicability](html-resources.md) distinguish modern transport observations from mandatory legacy/host qualification.

FP-046 adds immutable finite HTML readiness, locale/timezone/media controls and shared `PdfOperationContext.EnterTimeLimit` phase scopes. The same context/token applies through resource acquisition, browser waits and output. Expired/leaked scopes fail closed; original/outer time is never extended. See [readiness and lifecycle contracts](html-readiness.md).

FP-047 adds immutable `PdfHtmlPrintOptions` through `PdfHtmlOptions.WithPrint`, preserving existing constructor signatures. Explicit paper/CSS fitting, orientation, scale, point margins, bounded passive print templates and requested page-MCID structure validation share the original operation context. See [HTML print contracts](html-printing.md).

FP-018 adds immutable detached stamp sources/options plus transactional `PdfStamps.Apply`, `Inspect`, `Update` and `Remove`. Exact page/group ownership and canonical private streams distinguish removable stamps from unrelated content; decoration classification remains separate from semantic tag authoring. See [stamp contracts](stamps.md).

FP-028 adds `PdfResourceExtraction.ReadPage`, immutable `PdfPageResourceManifest` and explicit resource stream/font-program export/inspection. See [resource extraction](resource-extraction.md) for coordinate, identity and encoded-byte semantics.

FP-026 adds `PdfTextQuery`, `PdfTextSearch.FindPage`, provenance-rich matches and `ReplacePage` with explicit fonts/shaping/overflow. See [search and replacement contracts](text-search.md).

FP-027 adds `PdfTableExtraction.Read`, explicit options and immutable cells/tables/sequences, plus explicit merged-cell CSV and versioned typed JSON exports. See [table extraction contracts](table-extraction.md).

FP-099 adds `PdfDataGeneration`, `PdfGenerationTemplate<T>`, explicit immutable options, a guarded synchronous context and immutable precommit event/page views. See [generation contracts](data-generation.md).

FP-050 adds `PdfInputConversion`, explicit format/options/named drawing contracts and passive URI page-link snapshots. Parsing, binding, layout, link geometry and writing share the operation budget. See [input conversion contracts](input-conversion.md).

FP-053 adds `PdfWordConversion`, explicit format/reconstruction/limit contracts and `IPdfWordConverter`. The optional `Fluent.Pdf.Word` adapter pins the actual published dependency and requires explicit font-pack adoption. See [Word contracts](word-conversion.md).
