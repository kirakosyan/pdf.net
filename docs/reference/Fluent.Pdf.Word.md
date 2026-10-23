# Fluent.Pdf.Word source reference

Version `1.0.0`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).

## Public declarations

The baseline preserves exact nullability, parameter names, defaults and constant values.

```text
Fluent.Pdf.Word.PdfDocxMailMerge
Fluent.Pdf.Word.PdfDocxMergeOptions
Fluent.Pdf.Word.PdfDocxMergeOptions.Evaluation.get -> Fluent.Pdf.PdfMergeEvaluationOptions!
Fluent.Pdf.Word.PdfDocxMergeOptions.PdfDocxMergeOptions(Fluent.Pdf.PdfMergeEvaluationOptions! evaluation, Fluent.Pdf.PdfWordConversionOptions! word) -> void
Fluent.Pdf.Word.PdfDocxMergeOptions.Word.get -> Fluent.Pdf.PdfWordConversionOptions!
Fluent.Pdf.Word.PdfWordnetFont
Fluent.Pdf.Word.PdfWordnetFont.Bold.get -> bool
Fluent.Pdf.Word.PdfWordnetFont.Family.get -> string!
Fluent.Pdf.Word.PdfWordnetFont.Font.get -> Fluent.Pdf.PdfFont!
Fluent.Pdf.Word.PdfWordnetFont.Italic.get -> bool
Fluent.Pdf.Word.PdfWordnetFont.PdfWordnetFont(string! family, Fluent.Pdf.PdfFont! font, bool bold = false, bool italic = false) -> void
Fluent.Pdf.Word.WordnetPdfConverter
Fluent.Pdf.Word.WordnetPdfConverter.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Word.WordnetPdfConverter.Export(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfWordFormat format, System.IO.Stream! output, Fluent.Pdf.PdfWordConversionOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Word.WordnetPdfConverter.FallbackFamily.get -> string!
Fluent.Pdf.Word.WordnetPdfConverter.Fonts.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.Word.PdfWordnetFont!>!
Fluent.Pdf.Word.WordnetPdfConverter.Import(System.IO.Stream! source, Fluent.Pdf.PdfWordFormat format, System.IO.Stream! pdfOutput, Fluent.Pdf.PdfWordConversionOptions! options, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Word.WordnetPdfConverter.Supports(Fluent.Pdf.PdfWordFormat format, bool import) -> bool
Fluent.Pdf.Word.WordnetPdfConverter.WordnetPdfConverter(bool allowBundledFonts, string! fallbackFamily = "Noto Sans", Fluent.Pdf.Word.PdfWordnetFont![]? fonts = null) -> void
const Fluent.Pdf.Word.WordnetPdfConverter.PackageSha256 = "073bc06d91367c340f6f031e8cf2066f8a18d735cd93968d37f346d77c7bd4e5" -> string!
const Fluent.Pdf.Word.WordnetPdfConverter.SourceRevision = "bc6ada467dd4626dd23dfbee5f848b21e9f67dc5" -> string!
const Fluent.Pdf.Word.WordnetPdfConverter.UpstreamVersion = "1.1.0" -> string!
static Fluent.Pdf.Word.PdfDocxMailMerge.GeneratePdf(System.IO.Stream! template, Fluent.Pdf.PdfMergeRecord! data, Fluent.Pdf.Word.PdfDocxMergeOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Word.PdfDocxMailMerge.Write(System.IO.Stream! template, Fluent.Pdf.PdfMergeRecord! data, Fluent.Pdf.Word.PdfDocxMergeOptions! options, System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Word.PdfDocxMailMerge.WriteFile(System.IO.Stream! template, Fluent.Pdf.PdfMergeRecord! data, Fluent.Pdf.Word.PdfDocxMergeOptions! options, string! path, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
```

## Member documentation

### <code>F:Fluent.Pdf.Word.WordnetPdfConverter.PackageSha256</code>

Gets the adopted package SHA256. Locked dependency closure and final artifacts require their own review.

### <code>F:Fluent.Pdf.Word.WordnetPdfConverter.SourceRevision</code>

Gets the actual published package's source revision, distinct from later source-only APIs.

### <code>F:Fluent.Pdf.Word.WordnetPdfConverter.UpstreamVersion</code>

Gets the exact adopted dependency package version.

### <code>M:Fluent.Pdf.Word.PdfDocxMailMerge.GeneratePdf(System.IO.Stream,Fluent.Pdf.PdfMergeRecord,Fluent.Pdf.Word.PdfDocxMergeOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Fills a private DOCX package and imports it through the explicitly registered optional Word converter under the same operation; returns a caller-owned PDF model.

### <code>M:Fluent.Pdf.Word.PdfDocxMailMerge.Write(System.IO.Stream,Fluent.Pdf.PdfMergeRecord,Fluent.Pdf.Word.PdfDocxMergeOptions,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Fills an owned private package and writes DOCX at the borrowed output position, keeping both streams open. Scalar data is escaped as text and never reevaluated.

### <code>M:Fluent.Pdf.Word.PdfDocxMailMerge.WriteFile(System.IO.Stream,Fluent.Pdf.PdfMergeRecord,Fluent.Pdf.Word.PdfDocxMergeOptions,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Fills and serializes privately before atomic DOCX destination replacement.

### <code>M:Fluent.Pdf.Word.PdfDocxMergeOptions.#ctor(Fluent.Pdf.PdfMergeEvaluationOptions,Fluent.Pdf.PdfWordConversionOptions)</code>

Captures explicit evaluation/culture and Word limits; no field, font, image or external resource is discovered.

### <code>M:Fluent.Pdf.Word.PdfWordnetFont.#ctor(System.String,Fluent.Pdf.PdfFont,System.Boolean,System.Boolean)</code>

Captures a bounded requested family, explicit immutable font and face style. No file or machine font is opened.

### <code>M:Fluent.Pdf.Word.WordnetPdfConverter.#ctor(System.Boolean,System.String,Fluent.Pdf.Word.PdfWordnetFont[])</code>

Constructs an explicit converter. The pinned dependency has bundled font availability that cannot be disabled; callers must explicitly adopt it. No OS fonts or OCR/model services are discovered.

### <code>M:Fluent.Pdf.Word.WordnetPdfConverter.Export(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfWordFormat,System.IO.Stream,Fluent.Pdf.PdfWordConversionOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationContext)</code>

Reconstructs explicit editable text or fixed raster pages; omissions and inferred structure are diagnostic. Source actions are never activated, and no OCR/renderer is installed implicitly.

### <code>M:Fluent.Pdf.Word.WordnetPdfConverter.Import(System.IO.Stream,Fluent.Pdf.PdfWordFormat,System.IO.Stream,Fluent.Pdf.PdfWordConversionOptions,Fluent.Pdf.PdfOperationContext)</code>

Converts DOC/DOCX through the pinned package; RTF first uses the original bounded bridge. Input/output remain borrowed and every phase uses the supplied root context.

### <code>M:Fluent.Pdf.Word.WordnetPdfConverter.Supports(Fluent.Pdf.PdfWordFormat,System.Boolean)</code>

Reports source-format direction availability. Feature restrictions and losses are evaluated against actual content.

### <code>P:Fluent.Pdf.Word.PdfDocxMergeOptions.Evaluation</code>

Gets the shared closed evaluation policy.

### <code>P:Fluent.Pdf.Word.PdfDocxMergeOptions.Word</code>

Gets source/output/archive/provider limits for the filled package and optional PDF conversion.

### <code>P:Fluent.Pdf.Word.PdfWordnetFont.Bold</code>

Gets the supplied bold face selector.

### <code>P:Fluent.Pdf.Word.PdfWordnetFont.Family</code>

Gets the explicit family requested by document runs.

### <code>P:Fluent.Pdf.Word.PdfWordnetFont.Font</code>

Gets the caller-selected immutable face.

### <code>P:Fluent.Pdf.Word.PdfWordnetFont.Italic</code>

Gets the supplied italic face selector.

### <code>P:Fluent.Pdf.Word.WordnetPdfConverter.Descriptor</code>

Gets explicit conversion capability without implying fidelity/production qualification.

### <code>P:Fluent.Pdf.Word.WordnetPdfConverter.FallbackFamily</code>

Gets the exact explicit fallback family; dependency substitutions remain diagnostic.

### <code>P:Fluent.Pdf.Word.WordnetPdfConverter.Fonts</code>

Gets immutable explicitly selected faces; the adopted dependency font pack remains available.

### <code>T:Fluent.Pdf.Word.PdfDocxMailMerge</code>

Bounded original DOCX content-control/scalar binding through the optional Word adapter; template expressions never execute arbitrary code.

### <code>T:Fluent.Pdf.Word.PdfDocxMergeOptions</code>

Closed DOCX merge policy sharing the native data/culture evaluator and explicit Word export/provider limits.

### <code>T:Fluent.Pdf.Word.PdfWordnetFont</code>

An explicitly selected immutable TrueType face for the optional Word adapter.

### <code>T:Fluent.Pdf.Word.WordnetPdfConverter</code>

Optional original bridge to the exact published wordnet dependency, plus bounded original RTF/WordprocessingML reconstruction. Full production qualification remains pending.

