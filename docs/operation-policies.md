# FP-002 operation policies and resource budgets

Every read or save creates a fresh `PdfOperationContext` from immutable `PdfOptions.OperationPolicy`. Contexts have separate counters, diagnostics, deadline and linked cancellation; there is no mutable global policy. Reader/writer providers must accept the context. No PDF codec, renderer or native worker is implemented by this layer.

## Configure and inspect a result

```csharp
var limits = new PdfResourceLimits(maximumInputBytes: 1048576,
    maximumOutputBytes: 1048576, maximumElapsedTime: TimeSpan.FromSeconds(10));
var policy = new PdfOperationPolicy(PdfProcessingMode.Permissive, limits);
using var document = Pdf.Create(new PdfOptions(operationPolicy: policy), providers);
document.AddPage();
PdfOperationReport report = document.Save(output);
foreach (var diagnostic in report.Diagnostics)
{
    // Inspect Code, LossCategory, PageIndex, ObjectId and Remediation.
}
```

`providers` and `output` above are caller-supplied. [Executable C#/VB examples](../examples/README.md) use the same API. Successful saves return a detached immutable report; reads expose it on the returned document's `LastOperationReport`. Failed provider operations retain a report on the document, also readable after disposal. Failed opens dispose the partial document and throw. Pre-operation validation/unavailable-provider errors do not replace the previous report. Reports describe the most recently completed operation, not cumulative document history; callers retain earlier reports if needed.

## Strict and permissive behavior

Strict is the default. Providers must report each unsupported request, omission, substitution or fidelity reduction with a non-None `PdfLossCategory`, a stable code and all known page/object context:

```csharp
context.Report(new PdfDiagnostic("Content.Unsupported", "The requested behavior is unavailable.",
    lossCategory: PdfLossCategory.UnsupportedContent, pageIndex: 0, objectId: 42,
    remediation: "Provide supported content."));
```

Strict processing records the loss as Error and throws `PdfException`. Permissive processing records it as Warning and includes every accepted record in the successful report. Contexts bind operation, provider ID and capability to the SDK-selected values, preventing misleading supplied provenance. Page indices are zero-based; object IDs are nonnegative when known. Severity Error without a loss category is fatal in either mode. Informational and non-loss warning diagnostics can succeed in strict mode. No mode bypasses missing-provider failures or resource limits.

Diagnostics retain bounded code/message/provider/operation/remediation strings (128/2,048/128/128/2,048 characters). Diagnostic count is bounded too. Exhausting retention fails with `Limit.Diagnostics`; records are never silently dropped to make an operation succeed. Document contents/credentials must not enter diagnostics by default. Region-specific metadata and feature-specific remediation are future engine work.

Fatal policy failures are latched. Catching or replacing one inside a provider cannot turn the operation into success. The boundary reasserts it, with an intervening exception retained in `Exception.Data["Fluent.Pdf.UnderlyingFailure"]`. `PdfOperationReport.Failure` retains the fatal policy diagnostic separately from the bounded accepted diagnostic list. Original I/O exceptions and caller cancellation otherwise propagate normally. Failed stream output can be partial; failed path saves never commit the temporary output.

## Resource ownership and defaults

| Resource | Default ceiling | Accounting responsibility |
| --- | --- | --- |
| InputBytes | 64 MiB | SDK input lease; cumulative bytes delivered and seekable remaining-size preflight |
| ExpandedBytes | 256 MiB | Bounded decoder-output lease or provider charge before decoded allocation |
| OutputBytes | 256 MiB | SDK output lease; cumulative writes/growth plus relative extent |
| Objects | 1,000,000 | Provider charges before parsed/generated object allocation |
| Pixels | 100,000,000 | Provider charges dimensions before image/render allocation |
| Depth | 128 | Total outstanding recursion scopes, conservatively shared across the operation |
| Workers | 8 | Total outstanding worker scopes |
| Diagnostics | 1,024 | Context retention |
| Elapsed time | 30 seconds | Monotonic checkpoints and deadline cancellation token |

All numeric limits are positive. Elapsed time accepts one millisecond through one day. Values are ceilings, not production performance/host-memory claims. Page count remains independently bounded by `PdfOptions.MaximumPages` (default 10,000); a page-limit failure during a read is also latched.

`Consume(resource, amount)` accepts nonnegative cumulative amounts and rejects Depth/Workers. Charging uses subtraction before addition so 64-bit overflow cannot wrap into remaining capacity. `ConsumePixels(width, height)` validates positive dimensions and divides remaining capacity before multiplying, preventing dimension overflow. Do not allocate first and account afterward.

`EnterScope(Depth|Workers)` acquires one unit and returns an idempotent disposable lease. Release it in `using`/`finally`, including on cancellation/error. Peak and current scoped counts are reported. Unreleased scopes fail with `Operation.ScopeLeaked` before success or file commit. Counter updates and diagnostic reporting are synchronized for cooperating provider workers; document/model/stream access remains serialized. Every worker must complete before the provider returns. This is accounting, not a process sandbox or an automatic thread scheduler.

## Bounded streams and decoding

Input/output stream ownership stays with the caller. Seekable input is rejected before its callback if the remaining known length exceeds the input ceiling. Reads charge delivered bytes cumulatively, including rereads after seeking. A read request larger than the remaining budget is capped before calling the underlying stream. Distinguishing exact-limit EOF from an oversized read requires at most one extra probe byte. That byte is never returned on an over-limit read; the operation fails and latches the limit. Accepted usage remains within the ceiling. Decoder-output leases use the same rule for ExpandedBytes.

Providers that decode compressed content can wrap their decoder:

```csharp
using var decoder = CreateExplicitDecoder(input); // Codec selection belongs to the provider.
using var expanded = context.BorrowExpandedInput(decoder);
// Read bounded chunks from expanded; it charges the shared ExpandedBytes budget.
```

The provider owns `decoder`. Closing the borrowed view leaves it open; the view expires with the operation even if retained. Multiple decode stages share the same cumulative expanded counter. Parallel decoder reads reserve shared capacity before I/O and refund unused capacity afterward. Waiting readers check cancellation/deadline at bounded intervals; no context lock is held across external I/O. Only one EOF probe can be in flight at an exhausted shared limit. Outstanding reads also block successful operation completion.

In-memory decode/parse/render paths must instead call `Consume`/`ConsumePixels` before allocation and poll checkpoints between bounded chunks. SDK input/output leases already charge their own I/O, so providers must not double-charge those transfers. Codec internals/native allocations still require engine-specific limits/isolation and qualification. A real Deflate fixture tests bounded expansion, including a one-MiB highly compressible input; it is not a PDF filter implementation.

The isolated worker adapter holds temporary expanded-byte reservations for process envelopes and bounded object reservations for codec work. Reservations count against concurrent availability and recorded peaks. Disposing a reservation releases its unused capacity, including after a fatal failure; completing codec work converts its actual object use to a cumulative charge. Retained request/reply bytes remain cumulative. An outstanding reservation blocks successful operation completion. Native ceilings may be reduced to fit available operation memory, and never increased beyond caller configuration.

Output checks apply before each write, position change or growth. Writing repeatedly after seeking does not refund spent bytes. `SetLength` growth is charged; subsequent writes are additionally charged. Output cannot address a position before the operation's starting position or beyond its relative extent ceiling. Invalid/overflowing seeks cannot bypass the limit. No implicit input buffering, output truncation or copy fallback is added. Providers must own and budget any extra scratch storage. Fixed BCL buffering is distinct from the decoded payload budget.

## Cancellation, deadlines and commit

`context.CancellationToken` links the caller token and the operation deadline. It differs in identity from the caller token. `Checkpoint()` checks the original caller cancellation, monotonic elapsed time and latched failure. Caller cancellation throws `OperationCanceledException` associated with the caller token. Deadline expiration throws `PdfException` with `Limit.ElapsedTime`, including when a provider catches the linked token's cancellation or returns after the deadline.

SDK I/O and model mutations during reads perform cooperative checks; providers must also poll within decode/parse/layout/render loops and pass the linked token to cooperative dependencies. A checkpoint must respond within the production managed-cancellation envelope. Synchronous external streams and uncooperative extension code cannot be forcibly interrupted by this layer. Mandatory supervised native/browser termination is later worker integration work, not qualified by these tests.

Path saving keeps the context active across write, flush and pre-commit validation. Any fatal policy, outstanding scope, cancellation or elapsed deadline observed before commit preserves the original destination and cleans only the SDK-owned temporary file. The successful pre-commit check is the cancellation/timeout boundary: cancellation during/after the filesystem's atomic commit cannot undo it and is not reported as a failed unpublished save. No extra post-commit checkpoint claims rollback. Atomic visibility does not establish power-loss durability of directory metadata.

## Evidence boundary

[Policy tests](../tests/Fluent.Pdf.Contract.Tests/PolicyTests.cs), [I/O/real-decompression tests](../tests/Fluent.Pdf.Contract.Tests/PolicyIoTests.cs), [file failure tests](../tests/Fluent.Pdf.Contract.Tests/FileTests.cs) and C#/VB examples run in all applicable [source lanes](testing.md). Tests cover below/at/above boundaries, 64-bit overflow, exact EOF, policy suppression, deadline/cancellation, diagnostics, retained lifetimes and file preservation. A controllable internal monotonic clock tests exact deadline boundaries; actual timer cancellation is tested separately.

Actual minimum Framework 4.7.2, final-package consumers, future parser/renderer integration, full fuzz/endurance/fidelity and native termination remain unqualified. The [production gates](production-quality.md) and all 111 SDK requirements still apply. No package is created or published, and readiness stays false.

FP-040 page rendering uses one operation context for snapshot preparation, page/Form parsing, resource validation, native IPC/execution and output validation. Pixel windows are charged before raster allocation; full-page dimensions do not cause full-page allocation for a region. Borrowed output, exact extent/alpha/device-gray checks and native deadlines fail before a result is returned. Explicit unsupported profiles fail in both modes. See [the rendering source profile](rendering.md).

FP-031 security transformations retain one context through parsing, key derivation, CMS, object crypt filters and full rewrite. Private bounded staging prevents credential/authentication failures from publishing plaintext. Password/key/seed temporaries are cleared; plaintext stays in operation memory and the explicit caller output. No SDK plaintext temporary-file path or runtime credential discovery exists. See [security policies and limits](security.md).

Archival preflight, XML/ICC/font repair, staging, hashing, independent validation and final output share one context. Scoped XML depth, finite report/finding/CID extents and checkpointed copying fail closed. Strict metadata repair losses and incomplete/contradictory validation stop before converted bytes are copied. The optional report bridge never creates files/processes; caller hosts must implement their explicit execution/storage/isolation policies.

FP-006 uses one context through source snapshot, exact-frame scan, identity/reference analysis, private full rewrite, strict post-read, hashing and final output. Unsafe or unsupported recovery fails in both modes. Explicit orphan removal remains a reported loss; source and ledger views stay detached from caller streams. Ledger serialization is a separate explicit operation with its own supplied policy.

FP-098 text conversion and compatibility inventory are explicit bounded operations. Typed metadata edits charge their own shared context and retain the encoding's version floor. No global binary-string inference, URL fetch, language normalization or extension execution occurs. Malformed encodings and incompatible exact output versions reject in both modes.
