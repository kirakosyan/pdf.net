# Fluent.Pdf.Ocr source reference

Version `1.0.0`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).

## Public declarations

The baseline preserves exact nullability, parameter names, defaults and constant values.

```text
Fluent.Pdf.Ocr.IsolatedTesseractOcr
Fluent.Pdf.Ocr.IsolatedTesseractOcr.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Ocr.IsolatedTesseractOcr.IsolatedTesseractOcr(Fluent.Pdf.Workers.NativeWorkerOptions! worker, string! exactEngineVersion) -> void
Fluent.Pdf.Ocr.IsolatedTesseractOcr.IsolatedTesseractOcr(Fluent.Pdf.Workers.NativeWorkerOptions! worker, string! exactEngineVersion, Fluent.Pdf.PdfOcrModelCatalog? modelCatalog) -> void
Fluent.Pdf.Ocr.IsolatedTesseractOcr.ModelCatalog.get -> Fluent.Pdf.PdfOcrModelCatalog?
Fluent.Pdf.Ocr.IsolatedTesseractOcr.Recognize(Fluent.Pdf.PdfOcrRaster! raster, Fluent.Pdf.PdfOcrOptions! options, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfOcrResult!
Fluent.Pdf.Ocr.ZxingBarcodeDecoder
Fluent.Pdf.Ocr.ZxingBarcodeDecoder.Decode(Fluent.Pdf.PdfOcrRaster! raster, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfBarcodeObservation?
Fluent.Pdf.Ocr.ZxingBarcodeDecoder.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Ocr.ZxingBarcodeDecoder.MaximumPixels.get -> int
Fluent.Pdf.Ocr.ZxingBarcodeDecoder.Supports(Fluent.Pdf.PdfBarcodeSymbology symbology) -> bool
Fluent.Pdf.Ocr.ZxingBarcodeDecoder.ZxingBarcodeDecoder(int maximumPixels = 1048576) -> void
const Fluent.Pdf.Ocr.IsolatedTesseractOcr.BridgeAbi = "fluent-pdf-ocr-1" -> string!
const Fluent.Pdf.Ocr.IsolatedTesseractOcr.BridgeVersion = "1.0" -> string!
```

## Member documentation

### <code>F:Fluent.Pdf.Ocr.IsolatedTesseractOcr.BridgeAbi</code>

Gets the original bounded OCR transport ABI, separate from the actual engine version.

### <code>F:Fluent.Pdf.Ocr.IsolatedTesseractOcr.BridgeVersion</code>

Gets the exact original IPC bridge version, separate from the caller's checked Tesseract version.

### <code>M:Fluent.Pdf.Ocr.IsolatedTesseractOcr.#ctor(Fluent.Pdf.Workers.NativeWorkerOptions,System.String)</code>

Captures the exact bridge/deployment and expected actual Tesseract5 version. The manifest must explicitly pin its canonical engine binary and eng.traineddata; runtime identity, hashes, dependencies and sandbox remain mandatory.

### <code>M:Fluent.Pdf.Ocr.IsolatedTesseractOcr.#ctor(Fluent.Pdf.Workers.NativeWorkerOptions,System.String,Fluent.Pdf.PdfOcrModelCatalog)</code>

Captures explicit versioned model declarations matching exact native data assets. Null retains the legacy English-only digest profile; no model or metadata is inferred.

### <code>M:Fluent.Pdf.Ocr.IsolatedTesseractOcr.Recognize(Fluent.Pdf.PdfOcrRaster,Fluent.Pdf.PdfOcrOptions,Fluent.Pdf.PdfOperationContext)</code>

Recognizes through mandatory isolated execution with exact native-version/model binding, finite pixels/text/TSV and native deadline plus parent supervision.

### <code>M:Fluent.Pdf.Ocr.ZxingBarcodeDecoder.#ctor(System.Int32)</code>

Captures a finite decoded raster ceiling up to1048576 pixels. The adapter precharges a conservative128-byte-per-pixel managed workspace plus1MiB before invoking the engine. Cancellation is checked around its bounded synchronous call.

### <code>M:Fluent.Pdf.Ocr.ZxingBarcodeDecoder.Decode(Fluent.Pdf.PdfOcrRaster,Fluent.Pdf.PdfOperationContext)</code>

Decodes one explicit opaque Gray8/Rgb24/Bgra32 raster. QR finder/alignment landmarks and Code128 scan-line endpoints remain top-left image-pixel coordinates. Returned Unicode is a decoder observation, not an original byte-encoding claim.

### <code>M:Fluent.Pdf.Ocr.ZxingBarcodeDecoder.Supports(Fluent.Pdf.PdfBarcodeSymbology)</code>

Reports QR Model2 and Code128 decoding.

### <code>P:Fluent.Pdf.Ocr.IsolatedTesseractOcr.Descriptor</code>

Gets the exact requested native engine version and recognition service; capability declaration is not production qualification.

### <code>P:Fluent.Pdf.Ocr.IsolatedTesseractOcr.ModelCatalog</code>

Gets complete caller-declared model metadata, or null for the legacy English-only digest profile. Neither declares qualification.

### <code>P:Fluent.Pdf.Ocr.ZxingBarcodeDecoder.Descriptor</code>

Gets the exact pinned decoder service identity.

### <code>P:Fluent.Pdf.Ocr.ZxingBarcodeDecoder.MaximumPixels</code>

Gets the finite provider raster ceiling.

### <code>T:Fluent.Pdf.Ocr.IsolatedTesseractOcr</code>

Explicit local Tesseract5 C-API adapter using one fresh hash-pinned isolated worker per recognition. No native/model asset or language is discovered or downloaded.

### <code>T:Fluent.Pdf.Ocr.ZxingBarcodeDecoder</code>

Explicit optional managed ZXing.Net0.16.11 decoder, independent of the original core encoders. One symbol per image/region; no native, desktop, image-format or network discovery.

