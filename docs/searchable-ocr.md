# Searchable PDF and structured OCR output

FP-076 has a source profile. Complete OCR/native/structure/fidelity qualification and exact final packages remain mandatory; NuGet packing and publication stay blocked.

PdfOcrPageFrame binds exact raster dimensions to an explicit displayed CropBox region. Its affine transform includes nonzero crop origins, quarter-turn rotation and UserUnit; pixel coordinates always start at the top left. A mismatched page geometry or out-of-raster word rejects. The caller must provide the frame of the raster actually recognized.

PdfOcrSearchable.Apply returns a private document with embedded explicitly shaped fonts, ToUnicode/ActualText mappings and text rendering mode3. Word advance frames map onto the exact recognized boxes. Original raster samples, encoded image streams and content are retained. Balanced graphics-state isolation prevents original content transforms from displacing the new layer. The source document remains unchanged, and existing signature-invalidating edits still require explicit organization policy.

The default existing-text policy skips OCR words sufficiently covered by mapped digital words. Coverage must meet the explicit fraction on both rectangles; hidden, unmapped or estimated source geometry does not establish adequacy. This is a stated geometric policy, not a correctness guarantee. RejectOverlap and explicit AllowDuplicates are available. Comparison work and overlay word counts are finite. Mixed pages retain existing words while adding uncovered OCR words. Raw engine order is preserved; inferred reading order belongs to FP-077.

RecognizePage preflights all explicit rendering/OCR/shaping providers before calling them and inherits the source operation policy unless the caller supplies an explicit override. It uses one root context for normalization, rendering, capture, recognition, font binding, overlay construction and private writing. Its source profile requires an opaque isotropic integer DPI72–600, default200, and excludes annotation rendering by default. Each allocated raster stage shares cumulative pixel/byte limits. No native/model/network discovery or unrestricted fallback occurs. The shared rendering helper preserves the original rendering API's exact sample/alpha/color checks.

Tagged sources currently reject with Structure.SourceEditPolicy. This avoids inventing semantic associations for recognized words; complete explicit structure associations and accessibility qualification remain mandatory. Paragraph breaks inside a positioned word also reject. Complex scripts require an explicitly capable shaper and fonts; font discovery and silent glyph substitution remain prohibited.

PdfOcrOutput writes UTF8 without BOM to borrowed streams or atomically replaces one explicit file:

| Profile | Contract |
| --- | --- |
| PlainText | Exact raw recognized Unicode, including engine whitespace |
| Json | Version1; top-left pixel coordinates, exact dimensions/raw text, model declarations/identity, engine IDs and nullable confidence0–100 |
| HOcr | XHTML page/contiguous engine-line/word records, unique IDs, outward integer boxes and nullable rounded engine confidence0–100 |
| AltoXml | ALTO4 namespace, pixel measurement units, contiguous engine-line/word records, outward integer boxes and nullable confidence0–1 |

hOCR and ALTO line containers follow consecutive engine block/paragraph/line IDs, without inferring reading order. Null confidence remains absent. XML1.0-forbidden characters produce a typed diagnostic before writing XML; JSON preserves valid Unicode and escapes controls. The ALTO source identifier is the neutral caller-provided-raster label, not a discovered file or network path. Stream failures may leave a prefix; atomic file failures retain an existing destination.

C# and VB consumers apply layers and export JSON on all SDK TFMs. Original independent cases compare four cropped, rotated, nondefault-UserUnit pages containing an image. Three readers extract expected Unicode; independent rendering finds identical pixels and encoded image hashes. ALTO passes the unmodified primary4.4 schema with a pinned local XLink dependency. hOCR/JSON retain Unicode, geometry and confidence. The tool's physical-coordinate adapter for an observed MuPDF UserUnit API mismatch retains its original checker, exact diff/hashes and original failure; labels and tolerances stay unchanged.

These are original managed-provider/source observations, not a complete OCR corpus, isolated native-engine workflow, minimum runtime or package qualification. See [source evidence](evidence/FP-076-source.json), [independent observations](evidence/FP-076-independent.json), [standards](evidence/FP-076-standards.json), [local OCR](local-ocr.md), [model accuracy](ocr-models.md) and [production gates](production-quality.md).

Overlay construction shapes words separately for their explicit frames, collects glyph mappings per page/font, and embeds one shared subset per selected font. Word Forms reference that same resource identity.
