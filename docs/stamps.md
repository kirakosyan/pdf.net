# Watermarks, stamps and artifacts

FP-018 has an original source profile. Full scope and production qualification remain mandatory; NuGet packing/publication remain blocked.

`PdfStampContent` captures an immutable drawing, explicitly shaped/embedded text, image, passive PDF page or explicitly rendered HTML page. Font/browser/codec provisioning remains explicit. Text keeps logical ActualText and embedded resources; images retain samples, masks, physical aspect and orientation without recompression. Page capture retains CropBox, rotation, UserUnit and supported page transparency groups. It captures standalone page appearance: page groups become isolated Form groups, preserving their declared color space even when the page-only isolation flag was false. Source annotations, tags and optional-content ownership require an explicit flatten/import profile and reject in this source profile. HTML capture accepts exactly one generated page; multi-page selection is explicit through separate PDF/page capture. Rendering, import and detachment share the same operation context, including borrowed output leases, resource/time limits and permanent failures.

`PdfStamps.Apply` requires an explicit ASCII identifier, exact unique zero-based page indices, captured content and immutable `PdfStampOptions`. Placement uses displayed CropBox points with a top-left origin, independent of native page rotation/UserUnit. Foreground/background, opacity, clockwise rotation about each destination center, fitting, finite grid repetition/gaps and Artifact classification are explicit. The current compositing profile uses an isolated DeviceRGB group so outer opacity multiplies captured inner alpha. Complete blending-space/color profiles remain mandatory.

```csharp
var decoration = PdfStampContent.FromDrawing(drawing, new PdfRect(0, 0, 100, 100));
using var stamped = PdfStamps.Apply(snapshot, "review-watermark", decoration,
    new[] { 0, 2 }, new PdfStampOptions(new PdfRect(20, 30, 100, 80),
        opacity: 0.5, rotation: 15, columns: 2, horizontalGap: 10));
var inventory = PdfStamps.Inspect(stamped.CreateSnapshot());
using var updated = PdfStamps.Update(stamped.CreateSnapshot(), "review-watermark",
    decoration, new PdfStampOptions(new PdfRect(30, 40, 80, 60)));
using var removed = PdfStamps.Remove(updated.CreateSnapshot(), "review-watermark");
```

The C#/VB executable consumers exercise the public transactional API. Apply rejects an identifier already present anywhere in the document; Update/Remove affect the entire exact identifier group and require a recognized match. Each operation returns a new document and preserves the source snapshot. Ordinary signature-invalidating edit safeguards and atomic save/caller ownership rules apply.

Inspection recognizes a versioned private stream marker only when the canonical isolated invocation, exact page resource/Form identities and owned scope pair agree. Changed programs/bindings/scopes or duplicate ownership reject as `Stamp.Ownership`; removal never searches by visible text or appearance. A resource name used by an unrelated invocation remains bound after the owned invocation is removed. Each stamp owns its own graphics isolation pair; removal deletes that exact pair when no unrelated content outside it relies on the restored graphics state, avoiding guard accumulation in ordinary repeated cycles. When later unrelated content exists outside that pair, the exact state operators remain as ordinary content with the private ownership marker removed. Ownership metadata is document provenance, not authentication of an untrusted author's identity.

Decorations use an explicit Artifact marked-content scope. Semantic stamps on tagged sources require an explicit structure-association authoring profile and reject as `Stamp.StructureRequired`; full semantic/tagged import remains mandatory. Artifact addition preserves the existing page MCIDs/structure identities. No PDF/UA or other conformance claim follows from stamping. Source conformance declarations require renewed independent validation after edits.

Removal is visible content editing, not redaction. Historical/unreachable definitions are retained rather than deleting unrelated graph objects; repeated writes can therefore retain removed resource bytes and increase output size. Complete cleanup/performance/endurance profiles remain mandatory. Use the separately qualified redaction workflow for irreversible removal.

Source fixtures cover exact page selection, persistence, update/removal, 20 repeated cycles, unrelated resource use, malformed ownership, decoration/semantic policy, source kinds, explicit HTML provider/latch/ownership boundaries, cancellation and memory/object/input/pixel bounds. Original outputs cover foreground/background, multiplied alpha, repetition, rotation/clipping, EXIF orientation, shaped text, nonzero CropBox/rotation/UserUnit page capture, real pinned-browser HTML capture and tagged decoration/update. Independent readers/renderers check analytic geometry/colors, exact text/encoded image samples, parent-tree associations and exact restoration of the original raster after ten removals. These are small source observations, not production qualification.

Full owner/interactive/optional-content imports, semantic structure authoring, deterministic complete font/HTML/color/content corpora, all actual supported RIDs/minimum/current/fallback runtimes, final packages, independent standards/visual/security/resource/fuzz/concurrency/endurance/performance gates remain mandatory. See [source evidence](evidence/FP-018-source.json), [standards observations](evidence/FP-018-standards.json), [independent observations](evidence/FP-018-independent.json) and [production quality](production-quality.md).
