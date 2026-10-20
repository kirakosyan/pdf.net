# Document comparison and change reports

FP-029, queue issue #55, has the following source profile. Full requirement scope and production qualification remain mandatory; no issue is marked complete by this source delivery.

`PdfComparison.Compare` compares ordinal pages under one shared graph/text/render context. It independently reports page presence, logical extracted text, page/character advance geometry, Info metadata, exact decoded XMP hashes and optional rendered-channel differences. Text remains extraction-derived, including its mapping diagnostics. It does not infer page alignment, table/structure equivalence or semantic reading-order correspondence.

Geometry uses an explicit displayed-point tolerance for character cells; exact page boxes/rotation/UserUnit changes remain observable. Visual comparison requires an explicit renderer and carries explicit DPI/region/alpha/annotation/print choices plus an absolute channel tolerance. Different raster extents are separate visual differences. Exact case-sensitive ignored Info keys are captured immutably; XMP remains byte-sensitive. Complex Info values reject. Page changes retain both source page identities; metadata changes retain catalog identities.

`WriteReport` emits bounded version-one JSON or escaped, static HTML to a borrowed stream. `CreateAnnotatedDocument` creates a separate revised-document copy with explicit red comment appearances and stable unique names. Document/removed-page differences attach to page zero and comments refer to detailed structured observations. Tagged/empty destinations reject without an explicit structure-association profile. It does not erase or rewrite the report's immutable source snapshots.

General alignment, table/structure/semantic diffing, richer visual regions and labeled precision/recall corpora remain mandatory. Zero differences covers only selected dimensions and tolerances, never whole-document equivalence.

Source validation is recorded in [the queue evidence ledger](evidence/issues-51-60-source.json). All four core target frameworks expose the same API; actual-runtime/final-package limitations remain explicit.
