# PDF syntax inspection (FP-003)

`PdfSyntax.Read(borrowedStream, policy, cancellationToken)` snapshots input from its current position, leaves it open, and eagerly indexes immutable values and revisions. It accepts headers 1.2–1.7 and 2.0, classic cross-reference tables, cross-reference streams and hybrid tables. It follows bounded `Prev` chains, applies hybrid stream precedence, checks object identities and generations, and resolves a free/missing/mismatched-generation object to PDF null without resurrecting older entries. Revisions are exposed newest first.

`PdfValue` retains exact decimal spelling, byte-valued names, uninterpreted strings, arrays, dictionaries, indirect references and opaque encoded streams. `Resolve(id, revisionIndex)` selects the revision without retaining caller resources or doing further parsing. `OpenRawStream` and `OpenOriginalStream` return independent read-only views. Unknown keys, actions and stream filters remain inspectable; no action, JavaScript, network request or application launch occurs. Syntax inspection does not decrypt strings or interpret page content.

```csharp
PdfSyntaxDocument syntax = PdfSyntax.Read(input, operationPolicy, cancellationToken);
PdfValue trailer = syntax.Revisions[0].Trailer;
PdfValue catalog = syntax.Resolve(trailer.Entries["Root"].ReferenceId);
```

The C#/VB `InspectSyntax` consumers compile for all four library TFMs and run against actual PDF bytes in source tests. The default high-level `Pdf.Open` provider registry is still empty; syntax inspection is a separate low-level contract until the managed document reader/writer integration is delivered.

Input is stored in 4 KiB segments with 64-bit positions. Allocation of each segment is charged before creation; the final partially used segment still charges its capacity. Lexical buffers, bootstrap decoding, parsed nodes, revision/dependency depth and cooperative checkpoints use the mandatory operation context. Success carries a detached operation report. Fatal `Syntax.*`, `Filter.*` and `Limit.*` diagnostics cannot be converted to successful results by permissive mode. Caller arguments and lifetime errors retain their standard exception types.

Classic entries and stream fields are checked for overflow, duplicate numbers, header identity, generation range, offset bounds and truncated data. Stream bodies use the declared direct/indirect Length and exact end tokens; no recovery scan guesses an endstream position. Duplicate decoded dictionary names are rejected. Bootstrap cross-reference streams require direct Length and decode parameters; Flate, ASCIIHex and PNG/TIFF prediction are integrated without decoding unrelated opaque document streams. Flate checks final-block structure, canonical Huffman trees, history distances, stored lengths and Adler-32.

Embedded object-stream resolution is integrated through FP-004. It validates generation-zero containers, N/First, unique object identities, increasing offsets, member indices and exact member boundaries. Filter declarations and supported parameters may be indirect; cycles fail explicitly. `DecodeStream` returns eagerly decoded immutable bytes and a separate operation report under a fresh policy. The image codec providers remain in progress. Repair, decryption, content semantics, extension interpretation and standards validation belong to their respective requirements. No conformance or production qualification is implied by a syntax result.

Original fixtures cover all supported header versions, generations/free/reused objects, revisions, hybrid precedence, raw/Flate/predictor xrefs, lexical escapes, indirect Length, malformed offsets, cycles, truncation and boundaries. The retained original page fixture has independently inspected expected semantics; see its [provenance](../tests/Fluent.Pdf.Contract.Tests/Fixtures/syntax/README.md). Deterministic mutations are regression coverage, not the required 100,000-case/24-hour production fuzz qualification. Minimum Framework, final-package consumers, complete security/endurance and all other production gates remain pending. NuGet candidate creation and publication stay blocked.
