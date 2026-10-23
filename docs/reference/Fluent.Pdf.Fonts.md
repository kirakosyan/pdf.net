# Fluent.Pdf.Fonts source reference

Version `1.0.0`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).

## Public declarations

The baseline preserves exact nullability, parameter names, defaults and constant values.

```text
Fluent.Pdf.Fonts.ManagedFontProvider
Fluent.Pdf.Fonts.ManagedFontProvider.DecodeSimple(Fluent.Pdf.PdfValue! encoding, Fluent.Pdf.PdfValue! encodedText, string! baseFont, Fluent.Pdf.PdfOperationContext! context) -> string!
Fluent.Pdf.Fonts.ManagedFontProvider.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Fonts.ManagedFontProvider.GetSimpleAdvances(Fluent.Pdf.PdfValue! encoding, string! baseFont, Fluent.Pdf.PdfOperationContext! context) -> System.Collections.Generic.IReadOnlyList<double?>!
Fluent.Pdf.Fonts.ManagedFontProvider.ManagedFontProvider() -> void
Fluent.Pdf.Fonts.ManagedFontProvider.Read(System.IO.Stream! input, int faceIndex, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.IPdfFontProgram!
Fluent.Pdf.Fonts.ManagedFontProvider.Standard(string! postScriptName, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.IPdfFontProgram!
static Fluent.Pdf.Fonts.ManagedFontProvider.Instance.get -> Fluent.Pdf.Fonts.ManagedFontProvider!
```

## Member documentation

### <code>M:Fluent.Pdf.Fonts.ManagedFontProvider.#ctor</code>

Creates a stateless provider shareable by independent jobs.

### <code>M:Fluent.Pdf.Fonts.ManagedFontProvider.DecodeSimple(Fluent.Pdf.PdfValue,Fluent.Pdf.PdfValue,System.String,Fluent.Pdf.PdfOperationContext)</code>

Decodes explicit standard/simple glyph-name encodings without executing PostScript or searching machine fonts.

### <code>M:Fluent.Pdf.Fonts.ManagedFontProvider.GetSimpleAdvances(Fluent.Pdf.PdfValue,System.String,Fluent.Pdf.PdfOperationContext)</code>

Gets exact declared-encoding advances from pinned base-14 glyph-name metrics, independent of ToUnicode replacements.

### <code>M:Fluent.Pdf.Fonts.ManagedFontProvider.Read(System.IO.Stream,System.Int32,Fluent.Pdf.PdfOperationContext)</code>

Reads an explicitly selected collection face from borrowed bytes under the mandatory operation budget.

### <code>M:Fluent.Pdf.Fonts.ManagedFontProvider.Standard(System.String,Fluent.Pdf.PdfOperationContext)</code>

Selects an exact base-14 PostScript font and its deterministic metric facts.

### <code>P:Fluent.Pdf.Fonts.ManagedFontProvider.Descriptor</code>

Gets this provider's explicit font service declaration.

### <code>P:Fluent.Pdf.Fonts.ManagedFontProvider.Instance</code>

Gets the shared stateless instance.

### <code>T:Fluent.Pdf.Fonts.ManagedFontProvider</code>

Original managed offline font parsing, embedding/subsetting and exact standard-font metrics. No machine font discovery occurs.

