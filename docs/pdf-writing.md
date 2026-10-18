# Managed PDF reading and writing (FP-005)

`Pdf.Create`, `Pdf.Open` and `Pdf.OpenFile` select the original stateless `ManagedPdfProvider` by default. `PdfProviderRegistry.Empty` explicitly disables all providers; custom registries retain exactly their registered services. The core reader/writer has no native, image, font, browser or GUI dependency. Reading and saving execute no document actions.

The reader eagerly snapshots borrowed input, preserves the object graph and validates reachable page-tree parents, unique nodes and subtree counts. It resolves standard inherited attributes, quarter-turn rotation, nonzero origins and UserUnit scaling. Each live page has an immutable `PdfPageSource`; snapshots retain original bindings after editing-session disposal. Encrypted page interpretation requires an explicit encryption implementation; encrypted object streams reject before ordinary decompression.

```csharp
using var document = Pdf.OpenFile("input.pdf");
document.Pages[0].Rotation = PdfPageRotation.Clockwise90;
document.SaveFile("updated.pdf",
    writeOptions: new PdfWriteOptions(PdfWriteMode.Incremental));
```

`PdfWriteOptions` selects full/incremental mode, automatic/classic/stream xrefs, an optional target version and explicit signature invalidation permission. `IPdfRevisionWriter` declares support for these choices; legacy custom writers reject nondefault choices before writing. Save methods append optional `writeOptions` after cancellation. Existing source calls continue to compile; consumers must rebuild against the unshipped signatures.

Full rewriting preserves active object numbers/generations, unknown entries and opaque encoded stream bytes. Stream Length is derived accurately. Names/byte strings are escaped without interpreting content; exact numeric token spellings remain available. `PdfValues.Real` converts finite numbers to round-trippable decimal tokens without exponents. Standard inherited page attributes are materialized when page order changes. Full rewriting retains unreferenced active objects; secure erasure/redaction is a separate requirement.

Incremental saving copies the complete original file byte for byte, then appends changed/new objects, freed generations, cross references and a Prev link. An unchanged save with automatic choices is an exact copy. The immutable source never rebases after a save; subsequent saves apply the full current overlay to the same original. Caller streams receive a complete independent PDF segment at their current position, including the original file in incremental mode; callers own outer framing/prefixes.

Trailer ID bytes are preserved. Classic full tables include free entries/list links; incremental tables use sparse subsections. Xref streams use 64-bit offsets and preserve known free generations. Automatic selection stays within configured offset/object ceilings, selecting streams for sparse graphs or a potential extent beyond classic offsets. Streams require PDF 1.5; headers/catalog versions negotiate upward. Explicitly too-low versions, downgrades and conflicting trailer extension keys reject before output.

Signature byte ranges in the original graph/history protect against implicit full rewriting, including when an overlay replaces/frees a signature. Incremental saves preserve every original signed byte. Full rewriting requires `allowSignatureInvalidation: true` and records the explicit decision. Trust-chain, certification-permission and signing validation remain separate capabilities. The original signed fixture contains an actual detached CMS signature independently verified with OpenSSL and Poppler. Both emitted incremental formats retain valid signature verification in the independent probe.

`PdfTransaction` exposes a separate low-level overlay for adding, replacing and freeing immutable values. Fresh numbers advance beyond the original Size/high-water mark; identities are never reused. `PdfValues.Stream` snapshots borrowed encoded input under a fresh policy. Transactions inherit the source read policy unless overridden. Callers serialize mutation/saving; reentrant mutation rejects.

Graph work, state buffers, page snapshots, input, depth, output extent and deadlines use the mandatory operation context. Failed stream saves can leave partial output. File saves stage in the destination directory, flush, and atomically move/replace after checks finish. Failure preserves the destination and cleans owned staging; there is no delete/copy fallback. Empty high-level documents need a page before saving.

Source tests cover original independent syntax, opaque value/byte round trips, table/stream revisions, generations/free entries, trailer IDs, inheritance, unsupported requests, every output-byte fault boundary, atomic failure/cancellation and actual signed-byte preservation. Independent pypdf/Poppler checks verify emitted pages/text/rotation and retained signatures. Standards, minimum-runtime, final-package, security, endurance and fuzz qualification remain mandatory. All 111 SDK requirements remain required; candidate creation and NuGet publication stay blocked.
