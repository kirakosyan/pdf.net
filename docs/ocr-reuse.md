# OCR reuse assessment

## Inspected source

The local wordnet checkout could not fast-forward because AGENTS.md and docs/releasing.md have uncommitted changes. It was left untouched. This assessment uses a fresh upstream source snapshot at commit `141236a3fe890d220b807a270cec2d86a8f7e3b5`, retrieved on 2026-10-17.

Reviewed sources: [provider project](https://github.com/kirakosyan/word.net/blob/141236a3fe890d220b807a270cec2d86a8f7e3b5/src/wordnet.Ocr/wordnet.Ocr.csproj), [recognition implementation](https://github.com/kirakosyan/word.net/blob/141236a3fe890d220b807a270cec2d86a8f7e3b5/src/wordnet.Ocr/Ocr/EnglishOcr.cs), [native loader](https://github.com/kirakosyan/word.net/blob/141236a3fe890d220b807a270cec2d86a8f7e3b5/src/wordnet.Ocr/Ocr/NativeModule.cs), [reading order](https://github.com/kirakosyan/word.net/blob/141236a3fe890d220b807a270cec2d86a8f7e3b5/src/wordnet.Ocr/Ocr/OcrReadingOrder.cs), [PDF import](https://github.com/kirakosyan/word.net/blob/141236a3fe890d220b807a270cec2d86a8f7e3b5/src/wordnet.Ocr/PdfImport.cs), [options](https://github.com/kirakosyan/word.net/blob/141236a3fe890d220b807a270cec2d86a8f7e3b5/src/wordnet/EnglishOcrOptions.cs).

## Observed implementation

- Tesseract 5 C API is loaded from an explicit absolute native-library path; eng.traineddata is explicitly provisioned locally.
- OCR rasterization uses the PDF rendering/Skia path, separate from managed document export.
- Defaults are 200 DPI and 16 Mi pixels per page; accepted DPI range is 72–600.
- Per-page API, monitor and returned-text buffers are released. A loaded native module is retained for process lifetime.
- The native cancellation monitor delegates to the operation cancellation token; character and expanded-byte limits bound returned text/TSV.
- Optional preprocessing tries right-angle orientations and bounded deskew, selecting by confidence. Confidence is not a guarantee of correctness.
- Optional two-column order uses TSV word geometry and is unsuitable as a blanket policy for tables/forms.
- The project targets net462, net472, netstandard2.0, net8.0 and net10.0 and depends on the core document project plus optional rendering/native assets.

## Reuse decision

Use these as reviewed MIT source patterns for FP-074–077: native loading, function binding, cancellation lifetime, buffer cleanup, pixel/text limits and orientation/reading-order diagnostics. Retain source attribution and exact commit references for any adapted code. No source has been copied into this planning baseline.

Do not make Fluent.Pdf core depend on wordnet.Ocr. Its current document-model dependency and internal recognition APIs do not form a reusable standalone OCR API. Either adapt the narrow layer into Fluent.Pdf.Ocr with attribution, or factor a separate shared component after a compatibility review. Document conversion remains a separate optional adapter.

## Production work still required

The inspected recognizer explicitly selects English. Add language/model discovery, version/hash/license manifests, word/line geometry, confidence and structured OCR output; qualify each advertised language. Map all preprocessing transforms back to original PDF coordinates for searchable overlays.

Qualify Tesseract, rendering, native dependencies and cancellation on Windows/Linux/macOS x64/ARM64. Source paths for an OS are not qualification evidence. Do not inherit wordnet's package-specific legacy-JIT workaround or historical platform claims without reproducing them for the actual Fluent.Pdf packages.

Publish CER/WER, orientation and reading-order metrics on labeled clean/degraded/multilingual fixtures, with safe failure on missing models and unsupported native ABIs. Preserve original raster content unless cleanup is explicitly requested.
