# Fluent.Pdf

Fluent.Pdf is an MIT-licensed .NET PDF SDK being designed for Windows, Linux and macOS. Its production scope combines document generation and editing, browser-quality HTML conversion, rendering, extraction, forms, OCR, security, signatures and independently validated PDF standards.

**Current delivery: document/provider contracts, operation budgets, isolated native workers and bounded [PDF syntax inspection](docs/pdf-syntax.md). The original managed [page reader and full/incremental writer](docs/pdf-writing.md), [content operators/version selection](docs/content-operators.md) optional [fonts/embedding](docs/fonts.md) and [Unicode/shaping](docs/text-shaping.md) and [vector/color/transparency](docs/vector-graphics.md) and [page canvas/geometry](docs/page-canvas.md) and [flow layout/pagination](docs/flow-layout.md) and [tables/data binding](docs/tables.md) are implemented in source; rendering and the remaining SDK features are in progress. The SDK is not production-qualified. No NuGet release is published.** The first public package must be a qualified stable release; publishing a placeholder, alpha, beta or name-reservation package is prohibited.

## Product direction

- A managed document core with a fluent task API, a precise object model and optional native providers.
- Full .NET Framework compatibility on Windows and modern .NET support on Windows, Linux and macOS.
- Offline operation, explicit native/font/model provisioning, bounded resource use, cancellation and useful diagnostics.
- Faithful output verified with independent readers, validators, multilingual fixtures and real runtime/package consumers.
- One coherent API for PDF workflows, with optional HTML, rendering, OCR, document-format, signing and intelligence integrations.

The specification defines **112 requirement areas: 111 mandatory SDK production requirements and one separate Reader placeholder**. These are requirements, not implemented capability claims. See the [capability catalogue](docs/feature-catalogue.md) and [product specification](docs/product-spec.md).

## Runtime and package plan

| Surface | Planned target | Qualification requirement |
| --- | --- | --- |
| Managed core | `net472;netstandard2.0;net8.0;net10.0` | API compatibility and package-only consumers |
| Modern .NET | Windows, Linux and macOS; x64 and ARM64 | Native execution on each applicable OS/architecture |
| .NET Framework | Windows x86 and x64, minimum 4.7.2 | Actual 4.7.2 and current Framework runs |
| Native providers | Separate optional packages/provisioning | Exact engine, ABI, model and RID evidence |
| PDF Reader | Separate non-packable project | Placeholder only; future specification deferred |

A TFM or compatible package declaration does not establish OS, runtime or provider support. The [platform contract](docs/platforms.md) defines applicability and qualification. The .NET 8 support window must be rechecked before release.

The proposed package ID `Fluent.Pdf` returned HTTP 404 from the NuGet gallery and flat-container index on 2026-10-17. This is an availability observation, not a reservation or guarantee of acceptance. Recheck the ID and companion package prefixes before publication.

## Start here

1. Read [AGENTS.md](AGENTS.md), [architecture](docs/architecture.md) and [research findings](docs/research.md).
2. Follow the [implementation order](docs/implementation-plan.md) and linked [GitHub backlog](docs/github-backlog.md).
3. Implement one dependency-ready requirement against its acceptance criteria and the [production gates](docs/production-quality.md).
4. Keep [requirements.json](specs/requirements.json), documentation and issue evidence synchronized.
5. Publish only through the [stable release procedure](docs/releasing.md).

## Local contract checks

Install the .NET 10 SDK, .NET 8 runtime and PowerShell 7, then run:

```powershell
pwsh -File eng/validate-spec.ps1
pwsh -File eng/build-native-fixtures.ps1
dotnet restore Fluent.Pdf.slnx --locked-mode
dotnet tool restore
dotnet build Fluent.Pdf.slnx -c Release --no-restore
pwsh -File eng/check-api.ps1
pwsh -File eng/test-contracts.ps1
pwsh -File eng/test-release-block.ps1
```

These checks exercise document editing, provider/stream lifetimes, strict/permissive losses, resource limits, cancellation/deadlines and atomic file saves through test fixtures. See the [API contract and C#/VB examples](docs/api-contract.md), [operation policies](docs/operation-policies.md) and [test/evidence procedure](docs/testing.md). Native boundary/codec tests build and execute original pinned worker fixtures; see [native deployment](docs/native-deployment.md) for prerequisites and limits. Managed writer tests emit real PDF and signed incremental revisions; separate legacy contract fixtures emit labeled test bytes. Packing intentionally fails while production readiness is false.

## Repository layout

- `specs/`: canonical requirements, source inventory and fail-closed release readiness.
- `docs/requirements/`: independently assignable scopes, dependencies, acceptance and validation.
- `src/Fluent.Pdf/`: managed document model, syntax/filters and original reader/writer with optional provider contracts.
- `tests/` and `examples/`: behavioral contract tests and executable C#/VB consumers.
- `apps/PdfReader/`: separate Reader project placeholder, excluded from SDK publication.
- `eng/`: specification validation and production release controls.
- `.github/workflows/`: source contract CI and manually dispatched, gated stable publication.

## OCR and document interoperability

The reviewed wordnet upstream implementation provides useful Tesseract 5 loading, cancellation, orientation and resource-limit patterns. The [reuse assessment](docs/ocr-reuse.md) separates reusable code from unqualified language/platform support. Optional document conversion uses an adapter to avoid a circular dependency or mandatory OCR/browser installation.

## License

[MIT](LICENSE). Optional dependencies, native engines, fonts and OCR models retain their own licenses and redistribution requirements. Shipping assets require the inventory in [FP-095](docs/requirements/FP-095.md).
