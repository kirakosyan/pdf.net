# Fluent.Pdf.Compliance source reference

Version `1.0.0`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).

## Public declarations

The baseline preserves exact nullability, parameter names, defaults and constant values.

```text
Fluent.Pdf.Compliance.FacturX107MinimumReportValidator
Fluent.Pdf.Compliance.FacturX107MinimumReportValidator.EngineId.get -> string!
Fluent.Pdf.Compliance.FacturX107MinimumReportValidator.FacturX107MinimumReportValidator(Fluent.Pdf.Compliance.PdfInvoiceValidationRunner! runner) -> void
Fluent.Pdf.Compliance.FacturX107MinimumReportValidator.Supports(Fluent.Pdf.PdfInvoiceProfile profile) -> bool
Fluent.Pdf.Compliance.FacturX107MinimumReportValidator.Validate(System.IO.Stream! pdf, System.IO.Stream! xml, Fluent.Pdf.PdfInvoiceData! invoice, string! requestSha256, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfInvoiceValidation!
Fluent.Pdf.Compliance.PdfAccessibilityValidationRunner
Fluent.Pdf.Compliance.PdfArchivalValidationRunner
Fluent.Pdf.Compliance.PdfInvoiceValidationRunner
Fluent.Pdf.Compliance.VeraPdfAccessibilityReportValidator
Fluent.Pdf.Compliance.VeraPdfAccessibilityReportValidator.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Compliance.VeraPdfAccessibilityReportValidator.Validate(Fluent.Pdf.PdfAccessibilityProfile profile, System.IO.Stream! input, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfAccessibilityValidation!
Fluent.Pdf.Compliance.VeraPdfAccessibilityReportValidator.VeraPdfAccessibilityReportValidator(Fluent.Pdf.Compliance.PdfAccessibilityValidationRunner! runner) -> void
Fluent.Pdf.Compliance.VeraPdfReportValidator
Fluent.Pdf.Compliance.VeraPdfReportValidator.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Compliance.VeraPdfReportValidator.Validate(Fluent.Pdf.PdfArchivalProfile profile, System.IO.Stream! input, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfArchivalValidation!
Fluent.Pdf.Compliance.VeraPdfReportValidator.VeraPdfReportValidator(Fluent.Pdf.Compliance.PdfArchivalValidationRunner! runner) -> void
const Fluent.Pdf.Compliance.FacturX107MinimumReportValidator.RuleBundleSha256 = "711f08a9dab037032c8ddafcf0dd2bdb0ee80f7beec152e93a3289f7e49bee77" -> string!
```

## Member documentation

### <code>F:Fluent.Pdf.Compliance.FacturX107MinimumReportValidator.RuleBundleSha256</code>

Gets the SHA256 of the exact ordered seven-file schema/Schematron/code-list inventory.

### <code>M:Fluent.Pdf.Compliance.FacturX107MinimumReportValidator.#ctor(Fluent.Pdf.Compliance.PdfInvoiceValidationRunner)</code>

Captures the explicit caller-owned supervised validation host, which must execute all five phases. Binding checks prevent stale or mismatched reports; the caller remains responsible for the host's independence and truthful execution.

### <code>M:Fluent.Pdf.Compliance.FacturX107MinimumReportValidator.Supports(Fluent.Pdf.PdfInvoiceProfile)</code>

Returns true only for the exact declared MINIMUM edition. Host prerequisites remain explicit.

### <code>M:Fluent.Pdf.Compliance.FacturX107MinimumReportValidator.Validate(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfInvoiceData,System.String,Fluent.Pdf.PdfOperationContext)</code>

Captures/hashes exact private inputs, serializes a nonce-bound immutable model, invokes the host and requires complete schema, rules, metadata, model and readable-paint reports with exact request/bundle binding.

### <code>M:Fluent.Pdf.Compliance.VeraPdfAccessibilityReportValidator.#ctor(Fluent.Pdf.Compliance.PdfAccessibilityValidationRunner)</code>

Captures an explicit caller-owned host responsible for exact profile selection and supervised execution/isolation.

### <code>M:Fluent.Pdf.Compliance.VeraPdfAccessibilityReportValidator.Validate(Fluent.Pdf.PdfAccessibilityProfile,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Checks exact source digest and full engine/profile/job/rule/check report completeness; contradictory, truncated or wrong-profile reports fail closed.

### <code>M:Fluent.Pdf.Compliance.VeraPdfReportValidator.#ctor(Fluent.Pdf.Compliance.PdfArchivalValidationRunner)</code>

Captures an explicit caller-owned validation host. Complete engine deployment, isolation, report retention and qualification are caller host requirements.

### <code>M:Fluent.Pdf.Compliance.VeraPdfReportValidator.Validate(Fluent.Pdf.PdfArchivalProfile,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Hashes exact source bytes, invokes the explicit host with shared budgets and checks engine/profile/job/rule/check completeness. Truncated, ambiguous or contradictory reports fail closed.

### <code>P:Fluent.Pdf.Compliance.FacturX107MinimumReportValidator.EngineId</code>

Gets the separately identified exact source protocol, not production qualification.

### <code>P:Fluent.Pdf.Compliance.VeraPdfAccessibilityReportValidator.Descriptor</code>

Gets exact report engine identity/version and independent accessibility service.

### <code>P:Fluent.Pdf.Compliance.VeraPdfReportValidator.Descriptor</code>

Gets exact report-engine version and service identity, independent of production qualification.

### <code>T:Fluent.Pdf.Compliance.FacturX107MinimumReportValidator</code>

Bounded exact-byte report bridge for the pinned Factur-X1.0.07 MINIMUM rule bundle and independent original PDFBox/Saxon host. It does not install, discover or execute an engine implicitly.

### <code>T:Fluent.Pdf.Compliance.PdfAccessibilityValidationRunner</code>

An explicitly configured offline complete PDF/UA validation host with mandatory shared budgets and borrowed source/report streams.

### <code>T:Fluent.Pdf.Compliance.PdfArchivalValidationRunner</code>

Runs an explicitly supplied offline validation host with borrowed PDF/report streams and the SDK's mandatory shared context. The host must select the requested complete profile and enforce its own supervised execution/isolation.

### <code>T:Fluent.Pdf.Compliance.PdfInvoiceValidationRunner</code>

Runs an explicitly provisioned independent exact-edition invoice host. PDF, XML, private model and report leases expire on return; all work must honor the shared context.

### <code>T:Fluent.Pdf.Compliance.VeraPdfAccessibilityReportValidator</code>

Optional bounded veraPDF1.28.2 report bridge for complete UA1/UA2 validation, separate from human semantic review. No engine, network, file or runtime discovery is performed.

### <code>T:Fluent.Pdf.Compliance.VeraPdfReportValidator</code>

Optional bounded report bridge for veraPDF 1.28.2. It neither installs nor launches engines, downloads resources, creates temporary files or substitutes metadata inspection for full validation.

