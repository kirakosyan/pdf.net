# Bounded PDF optimization

`PdfOptimization.Optimize`, `Write` and `WriteFile` stage a full rewritten PDF, reparse it and verify exact page count/geometry before returning bytes or copying output. Caller snapshots, streams and providers remain caller-owned; the file variant commits atomically. `PdfOptimizationResult` retains exact normalized-source/output SHA256, measured byte lengths/savings, transformation counts and the complete operation report. Savings can be negative and output growth is disclosed.

The default `Lossless` preset removes unreachable active objects, shares exactly equal safe image/mask/font-program streams and compresses unfiltered internal streams only when encoded payloads shrink. Existing encoded images are retained without decoding/recompression. Stream SHA256 only selects candidates: complete dictionary/reference and byte-for-byte comparison is required before aliasing. Identity-sensitive image keys are excluded from sharing. Font-program candidates come from explicit FontDescriptor FontFile entries. References are rewritten transactionally and unknown graph values remain passive. Embedded-file relationships and decoded payloads remain retained; encoded attachment bytes may be compressed losslessly.

A full rewrite retains current document semantics rather than prior revision bytes. Existing signature byte ranges and certification locks require `AllowSignatureInvalidation`; the operation does not assert signature validity or standards conformance. Strict losses and unsupported requested behavior reject before caller output. Source originals remain unchanged.

`RemoveUnusedResources` is a separate opt-in. It inventories all page content, Forms, tiling patterns, Type3 procedures, appearance streams and default-appearance fragments without executing actions. Its global name unions deliberately retain false positives between scopes: it removes a binding only when no inspected standard content can name it. Implicit color spaces, unknown resource categories and private owner dictionaries remain retained. It never edits a shared resource dictionary in place. Unknown operators, opaque printer/reference XObjects and dynamic script/XFA resource dependencies reject this requested analysis; callers may keep all resource bindings by leaving it disabled. Complete private/legacy/implicit-owner and maximal resource-reduction profiles remain mandatory qualification work.

`CompactImages` explicitly opts into bounded image resampling, defaulting to a maximum dimension of1600 samples; callers may select1–32768. Eight-bit non-stencil planes with explicit component color spaces are sampled at target-pixel centers by nearest-neighbor selection. Color interpretation, Decode values, alpha/soft-mask associations and page placement remain retained. Each changed plane reports `Optimization.ImageDownsampled` as a fidelity reduction and requires permissive policy. Unsupported precision, external images, embedded-alpha and alternate-image profiles reject; they are never silently reduced. Existing codecs are used only through an explicit provider registry. No engine or assets are downloaded. Resampling and lossless Flate encoding do not guarantee a smaller complete file.

```csharp
var lossless = PdfOptimization.WriteFile(snapshot, destination,
    new PdfOptimizationOptions(removeUnusedResources: true));
var images = PdfOptimization.Optimize(snapshot,
    new PdfOptimizationOptions(PdfOptimizationPreset.CompactImages,
        maximumImageDimension: 1200),
    policy: new PdfOperationPolicy(PdfProcessingMode.Permissive));
```

All graph walks, content parsing, filter/codec work, hashing, pixel allocation and staging share one operation context. Pixels are charged before decode/resampling, stream storage before allocation, and checkpoints cover bounded loops. Output write budgets include private serialization and final copying. Independent completed byte views do not retain caller streams or live operation contexts.

Source tests cover duplicate font programs and RGBA masks, associated XML, unreachable objects, used/unused bindings including Forms/Type3 procedures, identity-sensitive objects, visible geometry, explicit loss/precision rejection, signature authority, cancellation/limits and atomic failures. `eng/verify-optimization-fixtures.py` uses two explicitly provisioned unrelated PDF readers on controlled original fixtures to compare lossless pixels, visible/logical glyphs and decoded image/mask/font/attachment bytes. These observations do not qualify the full production corpus, standards profiles, all supported runtimes/providers, performance/endurance/security envelopes or exact final packages. FP-072 retains its complete mandatory scope and Planned status.
