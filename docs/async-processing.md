# Bounded asynchronous document processing

FP-087 provides `PdfProcessingPool` on all four managed targets. It runs synchronous job callbacks on a fixed number of background workers and returns tasks to callers. Each admitted job owns its copied input, a private document and one `PdfOperationContext` covering input reads, model allocation, provider calls and writes. Live document/page references reject access from another worker and expire at completion. Ordinary documents outside a pool retain their existing caller-serialized access contract.

`TrySubmit` returns false without copying input, allocating a task or retaining a waiting producer when either queue capacity or resident input capacity is exhausted. Await an admitted task before retrying. Input must remain unchanged during admission; subsequent caller changes cannot affect the private copy. A job input exceeding its operation policy rejects before admission. Pending job count is bounded separately from running jobs. Resident input accounting includes both. Callback closures, caller-owned output streams and provider instances are outside copied-input accounting; callers must bound those resources and avoid retaining sensitive state in closures. Per-job allocation/output/native-worker limits remain mandatory. The pool does not claim a process working-set ceiling.

The default limits are four workers, sixteen queued jobs and 64 MiB of resident copied input. Workers are capped at 64 and queued jobs at 4,096. `Progress` returns a detached snapshot of queued/running jobs, retained input bytes and succeeded/failed/canceled totals. Polling introduces no hidden progress-event backlog. Providers remain caller-owned and must support independent concurrent contexts, or use separate single-worker pools/provider instances.

```csharp
using var pool = new PdfProcessingPool(new(maximumWorkers: 2));
if (!pool.TrySubmit(pdfBytes, job =>
{
    job.ReadDocument();
    job.Document.AddPage();
    job.SaveFile(outputPath);
}, out var completion, cancellationToken))
    throw new InvalidOperationException("Processing capacity is exhausted.");

await completion!;
await pool.CompleteAsync();
```

Use the job's `Save`/`SaveFile` methods so writes retain the root budget and cancellation. Direct document I/O during the callback rejects as reentrant. `Save` leaves caller streams open and may produce partial stream bytes on failure; each `SaveFile` is an atomic file operation. A callback containing multiple saves is not a multi-file transaction: later failure cannot undo an earlier completed save. Optional providers receive `job.Context`; an independent top-level API invoked by a callback retains that API's own documented operation boundary.

`CompleteAsync` stops admission and drains work. `ShutdownAsync` cancels queued and active jobs, then waits for all workers and owned cleanup. `Dispose` requests shutdown without blocking; await `Completion` to observe finished cleanup. Queued cancellation removes the job and releases its input reservation immediately. Active cancellation does not complete its task until the callback/provider has exited and the document/input lease are disposed. Cancellation and failure of one job do not poison later independent jobs. Callback exceptions propagate through their tasks; unrelated `OperationCanceledException` instances are faults. Callbacks must be synchronous; direct async-void callbacks reject before admission. Never block a worker waiting for the same pool to drain.

Managed callbacks must poll the context or use its cancellation token; arbitrary uncooperative managed code cannot be safely force-terminated. Existing isolated native supervision retains its five-second cancellation/termination bound and cleans owned staging before task completion. Browser/OCR providers still require their documented isolated host and resource policies. The pool creates no process, browser, model, credential cache or network connection by default and does not qualify an external host.

Source tests cover queue/byte boundaries, capture isolation, cancellation races, shutdown cleanup, thread/lifetime rejection, atomic output failure, strict/permissive losses, latched failures, leaked worker scopes, both compiled language consumers, and 64 workers executing 200 mixed read/edit/write iterations each. A separate native integration case observes pool-driven cancellation and stage cleanup. [Source evidence](evidence/FP-087-source.json) records actual runs and their limits. Full native/browser/OCR workload coverage, eight-hour endurance/handle monitoring, pinned performance envelopes, fresh final-package consumers and all production gates remain mandatory. The minimum-runtime report for the earlier FP-086 commit does not qualify this new implementation.
