# Fluent.Pdf

Fluent.Pdf is an MIT-licensed .NET PDF SDK being designed for Windows, Linux and macOS. Its production scope combines document generation and editing, browser-quality HTML conversion, rendering, extraction, forms, OCR, security, signatures and independently validated PDF standards.

**Current delivery: document/provider contracts, operation budgets, isolated native workers and bounded [PDF syntax inspection](docs/pdf-syntax.md). The original managed [page reader and full/incremental writer](docs/pdf-writing.md), [content operators/version selection](docs/content-operators.md) optional [fonts/embedding](docs/fonts.md) and [Unicode/shaping](docs/text-shaping.md) and [vector/color/transparency](docs/vector-graphics.md) and [page canvas/geometry](docs/page-canvas.md) and [flow layout/pagination](docs/flow-layout.md) and [tables/data binding](docs/tables.md) and [lists/columns/notes](docs/composition.md) and [page templates/contents](docs/page-templates.md) and [page organization/imposition](docs/page-organization.md) and [images/reusable graphics](docs/images.md) and [annotations/appearances](docs/annotations.md) and [navigation/action policy](docs/navigation.md) and [bookmarks/presentation](docs/presentation.md) and [metadata/identity](docs/metadata.md) and [attachments/collections](docs/attachments.md) and [optional-content layers](docs/layers.md) and [text extraction](docs/text-extraction.md) and [AcroForm editing](docs/forms.md) and [form data/workflows](docs/form-workflows.md) and optional [isolated page rendering](docs/rendering.md) and [password/certificate security](docs/security.md) and [archival conversion/validation](docs/archival.md) and [explicit structural repair](docs/repair.md) and [PDF version/extension preservation](docs/pdf-compatibility.md) and optional [HTML conversion](docs/html-rendering.md) with explicit [resource/authentication policy](docs/html-resources.md) and [finite readiness/lifecycle](docs/html-readiness.md) and [print layout/structure checks](docs/html-printing.md) and [owned stamps/artifacts](docs/stamps.md) have source profiles implemented in source; the remaining SDK features and full qualification are in progress. The SDK is not production-qualified. No NuGet release is published.** The first public package must be a qualified stable release; publishing a placeholder, alpha, beta or name-reservation package is prohibited.

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

The optional [DjVu/CDR host boundary](docs/legacy-format-import.md) stages and validates explicit caller-host imports. No DjVu/CDR decoder or supported source version is bundled or qualified.

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

FP-028 adds [resource extraction](docs/resource-extraction.md): shared definitions, actual transformed uses, literal paths, artifacts and exact encoded stream export. Complete profiles and production qualification remain mandatory.

FP-026 adds [bounded text search and explicit replacement](docs/text-search.md). Complete profiles and production qualification remain mandatory.

FP-027 adds [table extraction and structured export](docs/table-extraction.md), with explicit inference/culture/merge/continuation decisions. Complete profiles and production qualification remain mandatory.

FP-099 adds [typed generation and bounded events](docs/data-generation.md), with explicit culture/resources, guarded callbacks and precommit serialization. Complete profiles and production qualification remain mandatory.

Text, Markdown and structured input now have an explicit bounded [managed conversion profile](docs/input-conversion.md). Complete source-format fidelity and production qualification remain mandatory; NuGet packing/publishing remain blocked.

An explicit optional [Word conversion source profile](docs/word-conversion.md) adds DOC/DOCX/RTF interchange and diagnostic fixed/editable reconstruction. Complete format/fidelity and production qualification remain mandatory; SDK packing/publication stay blocked.

An explicit [document template and mail-merge source profile](docs/mail-merge.md) adds native PDF/DOCX scalar, repeat, condition, table and captured-image binding. Complete semantics, fidelity and production qualification remain mandatory; SDK packing/publication stay blocked.

An explicit [logical structure/accessibility source profile](docs/accessibility.md) adds author-supplied roles, language, descriptions, tables, Form/annotation associations and identity-preserving reading-order remediation. Complete UA1/UA2 and production qualification remain mandatory; SDK packing/publication stay blocked.

An explicit [local OCR source profile](docs/local-ocr.md) adds bounded raster/Unicode/word contracts and an optional isolated Tesseract5 adapter with pinned caller assets. Complete real-engine/model/language/RID and production qualification remain mandatory; SDK packing/publication stay blocked.

Explicit local model catalogs, exact mixed-model binding and bounded Unicode CER/WER measurement now have a source profile. Complete multilingual/model/native-host and production qualification remain pending; a retained mixed-language accuracy case fails its prior threshold. See [multilingual OCR](docs/ocr-models.md).

Explicit raster-aligned invisible OCR overlays, finite existing-digital-text handling, shared-context render/recognize/write and bounded text/JSON/hOCR/ALTO exports now have a source profile. Complete structure, real OCR/native/host and production qualification remain pending. See [searchable OCR](docs/searchable-ocr.md).

Opt-in bounded scan correction, original-image preservation, inverse word-boundary geometry and separate prose/table/form reading-order evidence now have a source profile. Complete automatic curve/semantic/native/corpus and production qualification remain pending. See [scan processing](docs/scan-processing.md).

Explicit raster/full-rewrite redaction and sanitization now have a source profile: selected samples are replaced before encoding and no original graph/history is adopted. Complete selective editable-content preservation, independent renderer/security corpora and production qualification remain mandatory. See [redaction](docs/redaction.md).

Explicit pre-created signature fields, exact incremental detached CMS signing, bounded PKCS12/borrowed-key providers and conservative DocMDP/FieldMDP edit barriers now have a source profile. Complete signature/trust/revocation/PAdES/edit-class/native/runtime qualification remains mandatory. See [signatures](docs/signatures.md).

Queue issues #51–#60 add [explicit signature trust](docs/signature-verification.md), [external signing](docs/external-signing.md), [PAdES orchestration/timestamps](docs/pades.md), [revision audit](docs/revision-audit.md), [comparison](docs/document-comparison.md), [XFA](docs/xfa.md), [bounded calculations](docs/scripts.md), [ICC color operations](docs/color-rendering.md), [raster containers](docs/raster-export.md) and [headless viewing](docs/viewing.md). These are explicit source profiles; complete requirements and production qualification remain pending.

Explicit [printing](docs/printing.md) now has a bounded orchestration/IPP protocol source profile. Complete native spool-system, printer/transport and production qualification remain mandatory.

Optional [trusted application views](docs/web-views.md) now share HTML/PDF import budgets, with a real compiled Razor/MVC/Blazor source sample. Complete legacy/XAML/browser and production qualification remain pending.

An original [authenticated render-worker protocol](docs/render-service.md) now has explicit tenants, bounded static-render retries/quotas and hosted/container source examples. Complete browser/native/host/container and production qualification remain mandatory.

Original [raster-document import](docs/raster-import.md) now composes managed PNG/JPEG and explicitly supplied frame codecs with exact selection and preserved density/color/alpha. Complete codec/host and production qualification remain pending.

The optional [SVG/vector format profile](docs/svg.md) now has original static import and editable opaque-vector export, with explicit unsupported-content failures and independent round-trip fixtures. Complete format/host and production qualification remain pending.

Bounded typed spreadsheet data interchange now covers CSV, SpreadsheetML, XLSX and ODS with explicit ranges, merges and passive PDF/formula provenance. Full coverage and production qualification remain mandatory; see [spreadsheets](docs/spreadsheets.md).

Explicit image-based PPTX export and strict picture-only import now preserve selection order, common EMU slide geometry and lossless embedded raster assets. Full coverage and production qualification remain mandatory; see [presentations](docs/presentations.md).

Offline HTML5 export now provides explicitly rendered self-contained pages or declared-loss inferred text, with static CSP, escaped markup and page provenance. Full coverage and production qualification remain mandatory; see [html-export](docs/html-export.md).

Original bounded EPUB3 fixed-raster/reflow packages, passive text-only spine import and documented escaped MobiXML source output now have explicit identities, navigation and loss/DRM diagnostics. Full coverage and production qualification remain mandatory; see [ebooks](docs/ebooks.md).

An original closed static EPS vector profile now imports/exports literal drawing data, with a separate exact-version optional isolated interpreter boundary for broader PS/EPS/PCL requests. Full coverage and production qualification remain mandatory; see [printer-languages](docs/printer-languages.md).

Original bounded XPS/OpenXPS raster-page packages and strict PNG-brush input now preserve page geometry/order with validated internal relationships and explicit unsupported print-ticket/construct diagnostics. Full coverage and production qualification remain mandatory; see [xps](docs/xps.md).

Explicit optional isolated TeX engine/package prerequisites and escaped inferred ASCII LaTeX export now have a shared-context source profile. Full coverage and production qualification remain mandatory; see [tex](docs/tex.md).

Managed explicit EMF24 raster-page interchange and exact optional CGM/WMF/EMF/EMF+ converter boundaries now have a shared-context source profile. Full coverage and production qualification remain mandatory; see [metafiles](docs/metafiles.md).

Closed passive FO page-master/block/table layout and finite literal-child XML transforms now have a shared-context source profile. Full coverage and production qualification remain mandatory; see [formatting-objects](docs/formatting-objects.md).

Original bounded OFD1.0 raster-page packages, explicit literal identity, owned resources and signed-package rejection now have a shared-context source profile. Full coverage and production qualification remain mandatory; see [ofd](docs/ofd.md).

Content-family discovery, an exact nine-profile machine-readable save matrix and staged shared-context stream/atomic-file export orchestration now have a source profile. Full coverage and production qualification remain mandatory; see [conversions](docs/conversions.md).

Exact-edition PDF/X producer/independent-validator gates with explicit ICC print conditions, retained page geometry and exact-byte/request binding now have a source profile. Full coverage and production qualification remain mandatory; see [prepress](docs/prepress.md).

Passive advanced-content inventory with encoded payload digests, exact-byte preserve-only copying and exact-edition PDF/E-1 independent conversion gates now have a source profile. Full coverage and production qualification remain mandatory; see [engineering](docs/engineering.md).

Immutable invoice currency/rounding calculations, synchronized CII/readable totals, exact associated XML and mandatory independent PDF/A/invoice publication gates now have a source profile. Full coverage and production qualification remain mandatory; see [invoices](docs/invoices.md).

Passive font/image/security preflight, exact-source explicit action-remediation plans, post-write verification and schema-one before/after audit reports now have a source profile. Full coverage and production qualification remain mandatory; see [preflight](docs/preflight.md).

Bounded lossless graph cleanup, exact safe resource sharing, conservative unused-binding analysis and explicit eight-bit image resampling now have a shared-context implementation. Full corpus/runtime/provider/final-package and production qualification remain mandatory; see [optimization](docs/optimization.md).

Bounded revision-bound range sessions, selective page/object access, private LRU caches and independently gated linearized output now have a source implementation. Complete hint/producer/transport/corpus/runtime/final-package qualification remains mandatory; see [progressive access](docs/progressive-access.md).

Original QR/Code128B generation with mandatory independent decoding, explicit page-region detector landmarks and canonical EPC invoice/payment payload validation now have a source implementation. Complete corpus/scanner/runtime/host/final-package qualification remains mandatory; see [barcode workflows](docs/barcodes.md).

Explicit six-task model adapters, selected source capture, remote/image authority, token/configured-cost reservations and schema/citation validation now have a source implementation in the optional Intelligence assembly. Full model/transport/security/quality/runtime/final-package qualification remains mandatory; see [document intelligence](docs/document-intelligence.md).

Explicit raw-source chunk coverage, table-aware boundaries, pinned embedding dimensions/versions and tenant/document/revision/ACL-bound cosine retrieval now have a source implementation in the optional Intelligence assembly. Full tagged semantics, real model/store/relevance/security/runtime/final-package qualification remains mandatory; see [semantic chunks and retrieval](docs/semantic-retrieval.md).

A separate frozen Windows automation facade, finite owned sessions, explicit marshaling, redacted HRESULTs and bitness-aware registration now have a source implementation. Actual COM activation and complete Windows/Framework/apartment/installer/final-package qualification are recorded separately; see [Windows automation](docs/windows-automation.md).

An optional toolkit-independent controller and WinForms/WPF/ASP.NET browser samples now use shared private viewing sessions with explicit keyboard, accessibility, generation and cancellation contracts. Full host/fidelity/assistive-technology/final-package qualification remains mandatory; see [application views](docs/application-views.md).

An optional modern CLI now consumes the public SDK with separate data/diagnostic channels, stable exit codes, protected password-file input, bounded jobs, atomic publication and retained batch outcomes. Full host/provider/final-distribution qualification remains mandatory; see [command-line automation](docs/command-line.md).

The [developer guide](docs/developer-guide.md) now links a versioned generated source API reference, explicit provider/platform prerequisites and real C#/VB-backed recipe applications. Narrative fragments and published-package qualification remain explicitly distinct.

The [runtime evidence tooling](docs/runtime-qualification.md) records actual process/core-library/Framework-registry/loaded SDK identities against the unchanged 22-lane matrix, with strict test and exact-package consistency checks. A Windows Docker preflight launcher can probe an explicitly resolved candidate image. Actual minimum-runtime hosts, complete package consumers and production qualification remain mandatory; see [source evidence](docs/evidence/FP-086-source.json).

FP-087 adds a bounded asynchronous processing pool with private worker-owned documents, shared job contexts, nonblocking admission, copied-input limits and cancellation-aware cleanup. Full workload/runtime/package/endurance qualification remains required; see [asynchronous processing](docs/async-processing.md).

FP-088 adds reproducible source performance measurements and fail-closed reviewed regression envelopes. Calibration is separate from production budgets and full provider/runtime qualification; see [performance measurement](docs/performance.md).

FP-090 adds a pinned original fidelity corpus with independent Unicode, geometry, form, structure and dual-renderer pixel checks. Complete corpus and production qualification remain pending; see [fidelity checks](docs/fidelity-corpus.md).

FP-091 adds private bounded content caches and repeated-process checks for explicit metadata and compression inputs. See [determinism and tenancy](docs/determinism-caching.md) for isolation rules and the remaining qualification.

FP-092 adds opt-in sanitized observations, numeric progress, caller-drained sinks and bounded support JSON. See [observability](docs/observability.md) for privacy, loss counters and qualification limits.

[Package provenance](docs/package-reproducibility.md) describes the guarded candidate metadata, source rebuild checks and offline symbol/archive inspection. Final packages remain unqualified and publication is blocked.

[Supply-chain inventory](docs/supply-chain.md) records locked dependencies, retained notices, explicit asset observations and advisory coverage. It does not approve redistribution or qualify final packages.

[Document collections](docs/document-collections.md) provide bounded ordered ZIP archives, explicit trusted batch adapters and typed PDF object XML. Protection, history, isolation and qualification limits are explicit.

[Semantic remediation](docs/semantic-remediation.md) binds explicit proposals and reviewer decisions to exact source bytes, revalidates accepted fixes and preserves an audit ledger without claiming accessibility conformance.

[Image cleanup](docs/image-cleanup.md) preserves original pixels, applies explicit bounded transforms and reports before/after blank-page evidence without automatic deletion.

[Business observations](docs/business-data.md) exports source-bound versioned JSON/XML with declared facts, inferred structures and OCR scores kept distinct.
