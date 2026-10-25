# Opt-in observations and support bundles

FP-092 adds privacy-filtered operation events, numeric progress, trace correlation,
explicit engine-version probes and bounded support JSON. Complete provider tracing,
privacy review and production/runtime/package qualification remain pending.

Create an owned `PdfObservationSession` and attach it with
`PdfOperationPolicy.WithObservations`. This returns a new policy; shared defaults
remain unmodified. Contexts using that policy emit start and finish events and may
call `ReportProgress(completed, total)`. Progress remains a cancellation/deadline
checkpoint even when observations are disabled. One session gives related workers
a random trace ID; each context has a distinct random span ID. These IDs never
change document IDs or output bytes. Do not share a session across tenants unless
the host explicitly intends to combine their telemetry.

## Processing and sink isolation

PDF processing only writes to a bounded in-memory queue. It never invokes logging
callbacks, opens a telemetry connection or starts a telemetry background thread.
Call `Drain` on an application-controlled thread to deliver events to a chosen
sink. Callbacks run outside the queue/context locks. A slow callback blocks that
draining caller; ordinary callback exceptions increment `SinkFailures` and the
failed event is discarded. They do not replace PDF processing failures or change
successful output. Concurrent or reentrant drains reject explicitly.

The queue holds at most 1–16384 events, with a default of1024. Full/disposed queues
drop observations and increment `DroppedEvents`. Processing continues. Missing
starts, progress or finishes are therefore possible and loss counters must be
retained with any interpretation of a trace. Dropped telemetry is not evidence
that an operation succeeded or that a complete trace was collected. Dispose
discards pending events; it does not invalidate the PDF policy or processing.
An already dequeued callback remains owned by the draining caller and is not
aborted by disposal. Await that caller before treating sink-failure counters as final.

## Privacy boundary

Events contain fixed kind/capability values, generated correlation IDs, sequence,
monotonic duration, success, numeric progress and detached numeric resource totals.
They exclude document content, page/object locations, names, paths, URLs, hashes of
document content, credentials, operation/provider free text, diagnostic messages,
exception strings and provider-supplied remediation. Even a diagnostic *code* can
contain a credential, so only a closed SDK-authored code catalog is exported;
unrecognized codes become `Provider.Diagnostic`. Non-policy failures use
`Operation.Failed`, cancellation uses `Operation.Canceled`, and every failure event
has SDK-authored safe remediation guidance. Original exception/report details stay
in trusted application scope and are not copied into telemetry.

`RecordEngineProbe` accepts an explicit capability, numeric `Version` and result.
It does not load an engine or infer a version from a provider name. Call it only
with a real inspected/probed version. This opt-in numeric metadata contains no
engine path, user-supplied identifier or probe message. Provider-specific native,
browser and model probe integration still needs its own complete evidence.

## Stable support schema

`WriteBundle` writes UTF-8 schema1 JSON for a snapshot of pending sanitized events,
the trace ID and loss counters. Each event includes its sequence/span, kind,
capability, elapsed ticks, optional success/code/remediation/progress, diagnostic
count, optional numeric engine version and resource consumed/peak counters. It
does not drain events or observe its own export. A drained event is no longer in
the queue, so the bundle describes pending observations rather than a full archive.

The explicit exporter charges finite object/expanded/output budgets, respects
cancellation and leaves the caller's stream open. Failed stream writes can leave
partial JSON; applications needing a durable file should stage and commit it
atomically. An export failure propagates to that explicit export call. Logging
callbacks and exports are separate from the processing result.

See the compiled C# and VB `ObservePdf` consumers and
[source evidence](evidence/FP-092-source.json). Tests cover secret-bearing codes and
messages, numeric JSON, borrowed streams, export limits, blocked/throwing sinks,
full/disposed queues, cancellation, output-byte equality and64-worker trace
correlation. Default silence and sanitized output do not substitute for a complete
deployment privacy review, native/provider traces, all minimum runtimes or exact
final package qualification.
