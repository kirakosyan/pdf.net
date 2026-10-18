# HTML readiness and private browser lifecycle

FP-046 has an original managed source profile. It adds finite readiness to the optional version-pinned HTML renderer; complete browser/host/platform qualification remains mandatory. Packing and publication stay blocked.

## Configure a finite boundary

`PdfHtmlReadinessOptions` captures an immutable snapshot of main-document CSS selectors, an optional explicitly trusted JavaScript promise expression, document/font/image checks, network quietness and a polling interval. The default timeout is 30 seconds; the operation's remaining deadline can expire sooner. Source scripts remain disabled unless `PdfHtmlOptions.EnableJavaScript` explicitly enables them. A promise with scripts disabled rejects before a host is created.

```csharp
var readiness = new PdfHtmlReadinessOptions(
    timeout: TimeSpan.FromSeconds(10),
    selectors: new[] { "#ready" },
    javaScriptPromise: "window.applicationReady",
    networkQuietPeriod: TimeSpan.FromMilliseconds(500),
    maximumInFlightRequests: 1);
var options = new PdfHtmlOptions(enableJavaScript: true,
    readiness: readiness, locale: "hy-AM", timeZone: "Asia/Yerevan",
    media: PdfHtmlMedia.Screen);
using var document = PdfHtml.Render(source, providers, options, explicitPolicy,
    cancellationToken);
```

The promise expression is supplied by the caller and uses JavaScript await semantics in the main world. The adapter does not discover a readiness expression in a document or execute PDF actions. Rejection and script/selector errors omit private expressions and source payloads from diagnostics. The temporary remote object group is released after a successful promise.

The adapter waits for the requested navigation's DOMContentLoaded event, then inspects document readyState, selectors, FontFaceSet and main-document images synchronously in a private isolated world. This inspection also works when source scripts are disabled. It repeats only until all requested conditions and the configured network boundary hold. It never waits on an internal asynchronous font promise with scripts disabled.

## Shared phase deadlines

`PdfOperationContext.EnterTimeLimit` temporarily tightens the **same** operation context/token to the earliest active phase/original deadline. HTML navigation, DNS, resource capture, browser I/O, explicit promise waiting and DOM/asset readiness all observe that boundary. After readiness, disposal restores only the original/outer time remaining for printing/import. It never grants extra operation time.

An expired phase latches `Limit.ElapsedTime`, including when a provider tries to dispose the phase before another checkpoint. Caller cancellation remains `OperationCanceledException`. Scope cleanup preserves an earlier fatal failure, and an undisposed phase rejects with `Operation.ScopeLeaked`. Phase scopes support 1 ms through one day, at most 128 simultaneously, with pre-allocation object/memory accounting. Nested and actual timer/cancellation behavior have source tests.

The browser readiness timeout supports 1 ms through five minutes. The root operation can reject earlier. Shared input/expanded/object/pixel/worker/output limits remain active; larger caller-selected timeouts do not increase any other budget.

## Assets and network quietness

Observed HTTP requests are tracked by bounded identities. Quietness means the pending inventory stays at or below `MaximumInFlightRequests` for `NetworkQuietPeriod`. Activity entirely within the allowed threshold does not reset that interval; this permits an explicitly tolerated background poller. A zero quiet period disables that additional wait. It does not imply that future requests or document mutations are impossible.

The profile bounds selectors to 64/16 Ki UTF-16 units total, each at most 1,024; explicit expressions to 16 Ki units; in-flight request identities to 4,096; the readiness inspection count to 65,536; and inspected font/image inventories to 10,000 each. Quiet periods are zero through five seconds, in-flight allowances zero through 64, and inspection intervals 10 ms through one second. Policies can reject earlier. Selector/expression Unicode must be well formed.

Known failed fonts/images are reported substitutions/omissions. Explicitly skipping an asset wait also reports any still-pending fonts/images at the selected boundary. Strict mode rejects these losses; permissive results retain the complete diagnostics. Failed/empty image geometry produces `Html.ImageUnavailable`. A missing selector, unresolved promise, never-completed asset or excess background activity expires finitely and produces no returned document.

## Environment and lifecycle

Viewport bounds retain the existing options. Explicit locale, timezone and print/screen media are applied before navigation; unknown engine values reject. The engine version/revision is still verified before document input. Print CSS and complete tagged output semantics belong to FP-047 and later accessibility requirements.

Every conversion uses a fresh required private host/context. Crashed/detached targets and broken transport abort the current job, with redacted `Html.BrowserTerminated` / `Html.BrowserConnection` diagnostics. The owned host must terminate/join its work before disposal returns; the next job creates a fresh host. There is no automatic replay of caller scripts or authenticated requests after a crash.

This source profile handles one explicit top-level document. Replacement top-level navigation, child-frame readiness, worker/OOPIF targets and interactive dialogs fail explicitly. Complete frame/worker/navigation/site profiles remain mandatory; unsupported content is not silently printed as complete. A host callback and a source build do not qualify OS network/memory/process boundaries.

## Source evidence

Source cases exercise finite missing-selector/asset/promise/network boundaries, explicit promise permission/rejection, delayed conditions, tolerated polling, strict/permissive losses, locale/timezone/media, transport failure and fresh jobs after crashes. Both C# and VB consumers exercise the expanded options. The six-argument binary constructor remains available; only the expanded overload carries optional parameters, satisfying API analyzer rules without suppressions.

The explicit trusted-original Linux browser observer additionally covers late fonts/SVG with exact Unicode, an awaited promise with actual locale/timezone/media checks, infinite background polling, failed images, never-resolving promise timeout, owned host termination and a successful fresh job. An actual caller cancellation during a pending promise also terminates the owned host within five seconds, with no returned document. Independent pypdf/MuPDF/Poppler readers check the positive outputs' logical text; unrelated renderers check delayed SVG dimensions/color.

An initial observer used a fixed 25 ms wait per protocol message and fell behind the polling producer. It now waits for actual completion or the same cancellation token, disposes completed read tasks and joins failed/cancelled reads. Exact original/modified hashes and diff are retained. SDK limits, timeout and sandbox settings are unchanged. This improves the original fixture observer; it does not qualify a deployable SDK host.

Complete supported browser/OS/RID/minimum-runtime/fallback/final-package consumers, full dynamic/font/image/frame/site corpus, fidelity, resource-boundary, malicious-input/fuzz, concurrency/endurance/performance and host isolation/termination qualification remain mandatory. See [source checks](evidence/FP-046-source.json), [standards decisions](evidence/FP-046-standards.json) and [independent observations](evidence/FP-046-independent.json).
