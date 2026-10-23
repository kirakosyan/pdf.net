# Developer guide

Fluent.Pdf currently provides source implementations and bounded feature profiles. Stable SDK release requires all 111 mandatory areas and the complete [production gates](production-quality.md); the separate Reader is excluded. No package is published or qualified by this documentation. The [generated source API reference](reference/index.md) covers all 15 SDK assemblies, exact nullability/defaults/constants and public XML comments at the declared package version. Its metadata-only generator never invokes inspected SDK code. Cross-target API comparison remains a separate check.

Start with [runtime/source setup](testing.md), the [API boundary and provider contract](api-contract.md), [operation policies](operation-policies.md), [offline native deployment](native-deployment.md) and [security profiles](security.md). The [requirements matrix](../specs/requirements.json) is authoritative for release scope and incomplete qualification. The [conversion matrix](conversions.md) distinguishes exact directions/providers from content-family detection. A provider's declared capability is not proof of native fidelity, safety or production readiness.

## Executable recipes

The [recipe application](../examples/Fluent.Pdf.Recipes/Program.cs) runs eight bounded workflows using public APIs and the compiled C#/VB contract examples: model/snapshot isolation; real vector and canvas PDFs in both languages; ordered page selection and Unicode extraction; output-limit/cancellation behavior; explicit missing-provider failure; passive preflight and conversion inventory; and lossless graph optimization. Fixtures are original repository data. These recipes need no downloaded font, model, native renderer or browser. Output streams remain borrowed. Samples with real optional engines use their separately declared assets and platform harnesses.

Build `examples/Fluent.Pdf.Recipes/Fluent.Pdf.Recipes.csproj`, then run its net8.0/net10.0 DLL with one output-directory argument. Its net472 executable explicitly targets Windows x64; current Framework and actual minimum 4.7.2 execution are separate observations. The library's netstandard asset is compiled by the C#/VB consumers and can be selected through their existing explicit fallback mapping. `eng/test-recipes.py` checks actual applications and output identities. Independent readers must still validate emitted PDFs and original-fixture fidelity; application success is not full qualification.

Recipe excerpts and the complete source reference are generated/checked by the documentation tooling. Existing narrative code fragments may require caller-supplied streams, fonts, providers, operation contexts, credentials or a supported document profile. The snippet inventory records that distinction and does not treat an unresolved prerequisite as a passing execution. Every advertised final-package recipe/fragment still needs its exact qualified package, runtime, provider and input evidence before stable publication.

## Providers and applicability

| Integration | Explicit prerequisite | Source/runtime boundary |
| --- | --- | --- |
| Managed document/geometry/syntax/text profiles | Supported passive PDF/input profile and immutable operation policy | net472, netstandard2.0, net8.0, net10.0; no toolkit dependency |
| Fonts/shaping | Caller font bytes plus selected [font](fonts.md)/[shaping](text-shaping.md) provider | No system-font discovery or automatic native asset download |
| Page rendering/viewing | Pinned offline [renderer assets](rendering.md), shared native isolation | Candidate engine/RID/runtime qualification is explicit |
| HTML/URL/application views | Explicit [browser connection](html-rendering.md), resource/origin authority and [readiness](html-readiness.md) | Optional modern/legacy host applicability is documented per adapter |
| OCR/barcodes | Explicit [OCR assets/models](ocr-models.md) or mandatory independent [barcode decoder](barcodes.md) | Fixture success does not qualify real engine/language/scanner quality |
| Intelligence/retrieval | Explicit model version/dimension, credentials/origin/image authority and caller-selected service/store | [Recorded responses](document-intelligence.md) and [scoped retrieval](semantic-retrieval.md) remain separate from live quality/security proof |
| Signing/conformance | Explicit keys/trust/time/profile plus independent validator | [Signatures](signatures.md), [trust](signature-verification.md), [archival](archival.md) and accessibility gates remain independent |
| Windows/desktop/CLI | Explicit [COM](windows-automation.md), [view](application-views.md) or [command-line](command-line.md) host | Core stays independent; exact host/runtime applicability is per integration |

## Diagnostics, loss and ownership

Use immutable finite policies at every operation boundary and pass cancellation into every operation. Strict mode rejects unsupported requested behavior. Permissive mode requires every loss to remain visible with its stable code and source provenance; inspect the returned operation report before accepting output. Limits count cumulative work/allocation/output under the shared operation context. Independent public operation calls have independent reports; an application must impose its own end-to-end deadline and retained-result budget. Native/opaque host work needs its documented qualified isolation, not a managed cancellation assumption.

Keep source documents/providers and borrowed streams alive for their declared lifetime. Dispose owned documents/sessions/results when required. Explicit file helpers stage atomically; arbitrary caller-stream writes can fail after some bytes reach the consumer. Full rewrites do not preserve signed byte ranges or erase historical copies; signature invalidation and sanitization authority are explicit. Do not execute imported actions, scripts, network resources or document-supplied instructions during passive inspection.

Lossy/limited profiles are declared at their API boundary: [image resampling](optimization.md), [inferred text/tables](table-extraction.md), [image-based conversion](conversions.md), [OCR overlays](searchable-ocr.md), [remediation](preflight.md) and [redaction](redaction.md) each retain their own restrictions. A different output extension never authorizes an invented document, dropped unsupported content or unreported fidelity loss.

## Versioned source and package verification

The source documentation generator checks assembly/package version agreement, all public XML members and exact API baselines. Generated pages omit build timestamps/commit metadata so documentation-only commits do not create artificial reference drift; the separate evidence manifest records actual source/binary/XML/page hashes. Source consumers reference projects and assert their actual runtime/assets. Exact-package consumers must use a fresh explicit feed/cache and hash-bound stable candidate packages after the existing source qualification gate permits them. `eng/test-package-recipes.ps1` invokes the unchanged source qualification gate, verifies an explicit manifest of every feed artifact (`id`, `version`, `file`, `sha256`), and creates a fresh private consumer workspace/cache with exact SDK version references and a cleared local-only NuGet feed. It never creates or publishes a package. The current false readiness blocks this mode before any candidate is read or consumer workspace is created.

The current release gate remains false. Published-package execution, all existing narrative fragments and provider prerequisites, actual minimum runtimes, accessibility/security/fidelity/performance/fuzzing and the complete final-package corpus remain mandatory. This guide records source progress and does not close FP-084.
