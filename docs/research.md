# Reference research and REA findings

## Scope and method

Research date: 2026-10-17. Reference identities are neutralized in all tracked artifacts. Raw package archives, official documentation snapshots, original source URLs, metadata inventories and REA evidence remain in an external research directory retained on the workstation. [research-evidence.json](../specs/research-evidence.json) records their SHA-256 lineage without introducing the excluded names into this repository.

This is an evidence-backed feature and architecture study, not a completed runtime comparison or an implementation recovered for redistribution. Original requirement language and our own API are used.

1. Download exact current stable package versions from the NuGet flat container; retain bytes, versions and hashes.
2. Inspect the archive, TFMs, documentation and dependency manifests. Resolve a dependency-only package to its managed implementation.
3. Use REA 6.5.0 for managed identity, selected members/CIL anchors and native-boundary declarations.
4. Supplement REA with a task-local System.Reflection.Metadata census of visible types and public/protected declarations across every shipped managed TFM.
5. Review official feature, conversion, forms, security, accessibility and API documentation; cross-check against declarations.
6. Review current wordnet OCR source from a pinned upstream revision.
7. Convert the observed capability families and explicit improvement requirements into stable requirement IDs with acceptance and independent validation.

## Package inventory

| Source | Version | Package bytes | Archive SHA-256 |
| --- | --- | --- | --- |
| Reference A | 26.10.0 | 207,629,488 | FCE227EF00D0C109E2FC88B5587CF90414AC3219A8BBD625F38EACF17AC9A39D |
| Reference B distribution bundle | 2026.10.3 | 49,045 | 796A80F5A24254FC7E39CFE33C2528FAA4811977C5BC2963F36B9362B2077C92 |
| Reference B managed implementation | 2026.10.3 | 30,082,700 | A0D435361821DD6F34379E043E19869BF2476F3B83D478E45949AD35E072538D |

A contains managed assemblies for Framework 4.8, Standard 2.0 and .NET 8/9/10. B's top package contains dependency declarations and empty TFM markers; its managed dependency contains Standard 2.0 and .NET 6/8 assemblies. The distribution also declares native/provider dependencies. The managed implementation, not the 49 KB bundle, is the analyzed API surface.

## Observed declarations

| Reference assembly | Visible types | Public/protected method declarations |
| --- | --- | --- |
| A Framework 4.8 | 1,060 | 7,912 |
| A Standard 2.0 | 1,211 | 9,122 |
| A .NET 8/9/10, each | 1,211 | 9,124 |
| B Standard 2.0 | 331 | 3,379 |
| B .NET 6/8, each | 331 | 3,380 |

Counts include exposed embedded dependencies, inherited-boundary vocabulary and obsolete/compatibility declarations. They are not counts of product features, unique overload signatures across builds, or verified working behavior. Raw signature blobs use build-local metadata tokens and must not be compared as stable cross-build signatures.

A central Standard 2.0 assembly contains 21,292 total type definitions and 159,449 method definitions. B contains 15,979 and 146,245 respectively. Much of this is non-public, bundled or generated code. Copying this shape would produce an unnecessarily large public API; the target architecture instead defines a reviewed provider-neutral contract.

REA successfully inspected seven selected central A types / 900 declared methods and six selected central B types / 560 declared methods, including document/page/form/text/OCR/signing/accessibility and browser/extraction/cleanup surfaces. Selection names were checked for unmatched entries; the final retained selections have none.

Native-boundary inspection completed with four module references / 11 P/Invoke declarations for A and twelve / 605 for B. Additional native-implementation declarations are metadata, not proof of a native library's presence or correct execution. These observations support explicit provider/ABI boundaries and avoiding an ambient desktop dependency in the managed core.

## REA limitations and reproducibility

Stock REA's 64 MiB metadata-representation budget was insufficient for these large assemblies. A task-local copy changed that one capacity to 512 MiB in the source module and compiled CLI bundle; the installed tool was left unchanged. The manifest records original/modified source and bundled hashes.

With 512 MiB, identity/native-boundary and bounded selected-member inspection completed. Unrestricted all-method projection still exceeded the bound. Retain those failed/partial reports as limitations rather than interpreting empty arrays as absent APIs. The supplementary metadata census covered declarations without loading or executing reference code.

The commands were:
- `rea inspect-managed-artifact <assembly> --format json`
- `rea inspect-managed-members <assembly> --type <exact-type> --type <exact-type> --format json`
- `rea inspect-managed-native-boundaries <assembly> --format json`

The task-local dispatcher was used for expanded-budget runs. Array-valued CLI selections must be repeated options; a JSON array passed as one value selects one literal name and is not a valid multi-type selection.

Native engine bodies were not fully decompiled, providers were not installed on every RID, and reference runtime/conformance/performance claims were not qualified. Evaluation restrictions and opaque or undocumented internals are not removed or bypassed. No reference implementation code or binaries enter the SDK.

## Evidence classes used by requirements

| ID | Meaning and retained sources |
| --- | --- |
| A01 | A package/archive, all-TFM declaration census and REA evidence |
| B01 | B distribution and managed implementation, all-TFM census and REA evidence |
| D01 | Official feature/format/conversion/forms/accessibility/security pages, snapshots S01–S07 |
| D02 | Official feature page and document/browser API references, snapshots S08–S10 |
| WN01 | Pinned wordnet source/project/license and dependency manifest S11 |
| ENH | Original quality, integration or capability improvement requirement |
| USER | Explicitly requested separate Reader placeholder |

The external source index maps snapshot IDs to exact URLs and hashes. Snapshot metadata is retained even where raw URLs are intentionally excluded from public artifacts. API declaration evidence is labeled declared/observed, not runtime-proven.

## Capability synthesis

The [catalogue](feature-catalogue.md) covers the combined capability families, including features not prominent on marketing pages: XFA conversion, low-level operators and PDF XML, layers, portfolios, table/geometry extraction, comparison, signed revision audit, HSM/PAdES, engineering/3D/geospatial preservation, invoice XML, DjVu/CDR import, PDF-to-Markdown, language-aware hyphenation, HTML element/block capture, document templates and semantic chunking.

The union is a normative production target. Optional providers, codec/version support tables and standards profiles still require implementation and actual qualification. The study does not claim that every reference declaration works, that closed/undocumented producer-specific formats can be reconstructed losslessly, or that a finite static scan proves complete behavioral equivalence.

## Original improvements

Require bounded processing and atomic saves, consistent strict/permissive diagnostics, explicit active-content/network policy, optional offline engines, per-tenant isolation, cross-platform package consumers, independent validation and measurable fidelity/performance gates. These are deliberate product requirements, not copied reference implementation details.

The reviewed wordnet OCR patterns are assessed in [ocr-reuse.md](ocr-reuse.md). They justify reuse of a narrow native/recognition layer with attribution, while multilingual models, searchable overlays and macOS remain new qualification obligations.

## Name availability

The gallery and flat-container index for Fluent.Pdf both returned HTTP 404 on 2026-10-17. No existing listing was observed. This does not reserve the name, establish prefix rights or guarantee publication acceptance; the release procedure requires a fresh check.
