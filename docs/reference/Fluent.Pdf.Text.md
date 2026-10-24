# Fluent.Pdf.Text source reference

Version `1.0.0`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).

## Public declarations

The baseline preserves exact nullability, parameter names, defaults and constant values.

```text
Fluent.Pdf.Text.IsolatedTextShaper
Fluent.Pdf.Text.IsolatedTextShaper.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Text.IsolatedTextShaper.IsolatedTextShaper(Fluent.Pdf.Workers.NativeWorkerOptions! worker) -> void
Fluent.Pdf.Text.IsolatedTextShaper.Shape(Fluent.Pdf.PdfShapingRequest! request, Fluent.Pdf.PdfOperationContext! context) -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfShapedGlyph>!
const Fluent.Pdf.Text.IsolatedTextShaper.BridgeAbi = "fluent-pdf-text-1" -> string!
const Fluent.Pdf.Text.IsolatedTextShaper.BridgeVersion = "14.2.1" -> string!
```

## Member documentation

### <code>F:Fluent.Pdf.Text.IsolatedTextShaper.BridgeAbi</code>

Gets the original bridge ABI.

### <code>F:Fluent.Pdf.Text.IsolatedTextShaper.BridgeVersion</code>

Gets the exact required native HarfBuzz version.

### <code>M:Fluent.Pdf.Text.IsolatedTextShaper.#ctor(Fluent.Pdf.Workers.NativeWorkerOptions)</code>

Captures a complete caller-provided deployment. No binaries or fonts are downloaded at runtime.

### <code>M:Fluent.Pdf.Text.IsolatedTextShaper.Shape(Fluent.Pdf.PdfShapingRequest,Fluent.Pdf.PdfOperationContext)</code>

Shapes with original font bytes and full logical context, accepting only a complete version/geometry-matched reply.

### <code>P:Fluent.Pdf.Text.IsolatedTextShaper.Descriptor</code>

Gets the capability declaration, independent of production qualification.

### <code>T:Fluent.Pdf.Text.IsolatedTextShaper</code>

Explicit offline OpenType shaping through a fresh, hash-pinned isolated HarfBuzz process.

