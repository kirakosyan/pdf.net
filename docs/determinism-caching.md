# Deterministic inputs and tenant-private caching

FP-091 adds a bounded content-addressed cache and executable repeated-process
checks for an explicit managed metadata/optimization profile. Complete provider,
runtime, package and endurance qualification remains pending.

## Declaring a repeatable profile

Supply the same source bytes, page/model operations, explicit fonts and fallback
order, metadata values, document IDs, timestamps, writer options and optimization
settings. `PdfMetadataEditor.SetIdentity`, `SetCreationTime` and
`SetModificationTime` control identity/time without an implicit clock or random ID.
`PdfOptimizationOptions.CompressStreams` declares whether new eligible streams
are compressed. Existing encoded streams remain declared source inputs.

`eng/Determinism` pins the original fidelity-corpus hashes, derives a 16-byte ID
from each source SHA-256, uses a fixed UTC creation/modification instant and runs
both explicit compression choices. `eng/test-determinism.py` starts five fresh
processes per choice, validates input/output inventories and compares complete
output hashes within the exact runtime/OS/architecture/SDK profile. It retains all
outputs and reports; unrelated runtime compression implementations are not assumed
byte-identical. The independent fidelity checker also checks the resulting PDFs.

Provider version strings alone do not identify all inputs. Pin and verify the
actual engine/library/font/model bytes, configuration, locale, time zone and
resource content before use. Existing native deployment descriptors and OCR model
manifests bind explicit artifacts; HTML rendering uses an explicit engine revision
and job context. Their complete deterministic profiles require separate evidence.
The current managed fixture invokes no browser, OCR model or external font search.

Signing, timestamp authorities, current-time callbacks, remote resources and
probabilistic model services are outside this repeatable profile. Encryption also
uses security-required randomness; deterministic comparison must not replace its
entropy with fixed IVs or salts. Reproducing the managed fixture does not establish
deterministic signed, encrypted, browser or model output.

## Cache ownership and bounds

Create one `PdfContentCache` per tenant in trusted application scope. The object is
the access boundary: sharing it deliberately shares its content. It has no global
store, tenant-name lookup, disk persistence, default network access or automatic
credential caching. Tenant authentication and assignment belong to the host.

`Store` captures bytes under a mandatory `PdfOperationContext` and computes their
SHA-256 address. Callers cannot supply an address that overwrites different bytes.
Duplicate values refresh their recency; a detected digest collision fails with
`Cache.HashCollision`. Input must remain unchanged during capture. `TryGet` accepts
only canonical lowercase SHA-256 and returns a detached copy, so later caller
mutation cannot alter cached content. An address learned in another tenant's
instance does not grant access to that instance.

Retention is bounded independently by entry count and payload bytes. LRU eviction
removes older entries until a new value fits. The default limits are 1024 entries,
64 MiB retained payload and 4 MiB per entry; constructor ceilings are 65536 entries,
1 GiB retention and 16 MiB per entry. Metadata is bounded by entry count. Operation
copies are additional memory and consume Objects/ExpandedBytes before allocation;
captured input consumes InputBytes. Bound concurrent operation count through the
processing pool or equivalent host scheduling. A hit does not bypass a caller's
remaining operation budget. Oversized entries latch `Cache.EntryLimit`.

Eviction, `Clear` and idempotent `Dispose` clear retained arrays. Caller copies
remain caller-owned; managed clearing does not guarantee cryptographic erasure of
all process memory. Dispose prevents further stores or reads. No opaque provider
call or I/O executes under the cache lock. Hashing/copy loops checkpoint regularly.

This cache stores immutable bytes. It does not memoize a computation by a partial
input key, retain live documents/provider handles, or infer whether a credential or
model configuration may be shared. Explicitly bind all input hashes when building
a higher-level result cache.

## Evidence and remaining work

Contract tests exercise capture/return-copy mutation, canonical addresses, byte and
entry eviction, latched limits, cancellation, disposal, shared-instance races,
compiled C#/VB consumers and 64 tenants each performing 200 mixed iterations.
[Source evidence](evidence/FP-091-source.json) binds actual results and process
outputs to source/runtime/tool hashes. Full mixed provider workloads, minimum
Framework, all operating-system lanes, eight-hour endurance and exact final
packages remain mandatory. No publication is authorized by these source checks.
