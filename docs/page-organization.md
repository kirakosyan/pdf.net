# Page organization and imposition

`PdfPageSelection.Parse` uses explicit one-based numbers, inclusive ascending or descending ranges, `last`, `all`, `odd` and `even`. Commas preserve requested order and duplicate occurrences. Its immutable indices are zero-based. Expressions, selected counts, source traversal, recursive graphs, expanded buffers and output pages have finite limits and cancellation checkpoints.

`PdfPageOrganization.Merge` imports detached `PdfPageInput` snapshots and selections into a new document. `Extract` also reorders or copies plain pages. `Insert`, `Delete` and `Split` use the same import engine; an entire operation, including intermediate serialization and parsing, shares cumulative budgets. Every returned document is caller-owned. Source sessions can be disposed before import; encoded streams and detached source graphs remain immutable. Atomic saves and caller-owned streams retain the existing save contract.

```csharp
var selection = PdfPageSelection.Parse("last-1", first.Pages.Count);
using var merged = PdfPageOrganization.Merge(new[] {
    new PdfPageInput(first.CreateSnapshot(), selection),
    new PdfPageInput(second.CreateSnapshot())
});
using var booklet = PdfPageImposition.Booklet(merged.CreateSnapshot(),
    new PdfPageSize(842, 595), PdfMargins.Uniform(18), gap: 12);
booklet.SaveFile("booklet.pdf");
```

Import assigns fresh object identities, preserves inherited resources and all five effective boxes, UserUnit and rotation, and keeps shared resources shared within each import graph. Links and local GoTo actions resolve named destinations to final page objects. Outline sibling/parent links and counts are rebuilt. Page labels retain their original values through selections and restarts. Public name trees merge with explicit collision failures. Conflicting catalog settings require caller reconciliation; unknown settings are preserved when they can be combined without ambiguity. Ordinary inspection and import execute no actions or network requests.

Excluded navigation targets reject by default. `OmitWithDiagnostic` requests omission, and strict policy still rejects that reported loss; permissive policy retains each stable omission diagnostic. Form-field collisions reject by default or use an explicitly selected deterministic root-name prefix. Global form resources receive distinct source names, and parsed default appearances are rewritten to retain their font/color bindings. NeedAppearances, calculation order and signature flags are retained. Basic source structure is pruned to selected pages; parent references, roles and number-tree keys are rebuilt without declaring accessibility conformance.

`Transform` changes selected immutable page geometry while retaining native content coordinates; use it for rotation, crop and box-preserving media resize. `Scale` changes UserUnit so page content and annotation geometry scale together in physical points. Neither operation implies rasterization. A geometry callback runs synchronously under the operation and must return a valid immutable geometry.

`PdfPageImposition.NUp` fits pages into explicit row-major sheet cells using contain, cover or stretch. `Booklet` pads to a multiple of four and places outer/inner pairs on successive front/back sheets. `Tile` preserves physical size and uses explicit overlap. Forms retain vector/text resources and transparency groups. N-up/booklet remap ordinary links and the supported local destination views; basic page MCIDs become real form-stream MCR references with their parent-tree associations. Physical sheets use new decimal numbering. They do not retain the old physical page labels.

The current explicit import profile rejects duplicate structured/form-page copies, XFA-backed forms, unsupported text-string encodings, conflicting structure-root metadata, scripted field renames requiring reconciliation, and ambiguous catalog/public-name collisions. Imposition rejects annotation appearances or geometry that require a further transformation profile, nonuniform link stretching, unsupported transformed destination views, and interactive/structured repeated tiles. These profiles remain mandatory implementation/qualification work; rejecting them does not reduce the stable release scope. Encrypted sources require a decryption implementation before managed organization. Signed sources reject rewriting unless `AllowSignatureInvalidation` explicitly authorizes it; the report retains that authorization and no signing/verification claim is made.

Executable C#/VB examples and contracts exercise detached ownership, mixed-document selection, links/outlines, named resources, field collisions/default appearances, signature policy, structure associations, insert/delete/split, geometry/scale, N-up/booklet/tiles, budgets and cancellation. Independent development PDF/reader/render checks supplement source contracts. Full mixed interactive/structure/encryption corpora, accessibility/conformance, actual minimum runtimes, final packages and all production gates remain required. NuGet creation and publishing remain blocked.
