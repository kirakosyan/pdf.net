# Production qualification

## Release rule

The first NuGet publication is a stable production release. All 111 SDK requirements in the canonical manifest must be implemented, reviewed and qualified; FP-096 is the separate Reader placeholder. Do not publish a reduced, empty or prerelease package to reserve an ID. The checked-in readiness manifest starts false and the package target fails closed.

A passing build, metadata scan, source test on a newer runtime or dependency installation is not a substitute for observable behavior on the final package. An alpha-quality implementation with a stable version number is not acceptable.

## Evidence model

Every required requirement records evidence IDs, the tested source commit, package SHA-256, exact OS/runtime/RID, engine/font/model versions, corpus version, commands, complete test totals and output/report hashes. Evidence must be retained with the release. Partial, failed, skipped or inconclusive required tests block release.

Record applicability before running tests. The six modern OS/architecture lanes and both actual minimum Framework bitness lanes are mandatory; add current Framework and fallback consumers. Conformance, signing and native/provider lanes may add specialized environments but may not replace these lanes.

## Required gates

| Gate | Acceptance |
| --- | --- |
| Functional/API | Every requirement acceptance criterion passes; APIs/docs/samples agree; zero required failures/skips |
| Visual | Fixed corpus, pinned fonts/engines and independent renderer; SSIM >= 0.995 for calibrated stable raster fixtures and no missing content; reviewed masks only for documented antialiasing differences |
| Text/geometry | Exact expected logical Unicode on deterministic fixtures; word-box tolerance <= 0.5 pt unless a fixture explicitly declares a justified threshold |
| Reconstruction | Labeled tables/layout/reading order report precision, recall and confidence; profile thresholds are checked in before implementation is called qualified |
| Standards | Every generated advertised PDF/A, PDF/UA, PDF/X, PDF/E and invoice profile has independent validation; accessibility includes semantic/human review |
| Signatures | Independent CMS/PAdES validation, trust/offline/revocation cases, certification-aware edits and signed-revision inspection |
| Redaction | Independent byte/object/text/image recovery attempts find none of the removed content, including historical revisions and related resources |
| OCR | Pinned labeled language/scan corpus; clean English CER <= 1%, WER <= 3%; degraded English CER <= 5%, WER <= 10%; other scripts have checked-in reviewed thresholds and no silent fallback |
| Concurrency | 64 independent workers x 200 mixed-operation iterations; deterministic correctness, no shared credentials/state and no leaked handles |
| Endurance | 8-hour mixed workload; after warmup, no unexplained persistent memory/handle growth over a documented 5% envelope |
| Cancellation | Managed operations respond within 1 s at cooperative boundaries; supervised native/browser workers terminate within 5 s after deadline/cancel, with no partial committed output |
| Performance | Pinned hardware/corpus, warm/cold p50/p95/p99, throughput, peak working set and output size; no >10% regression against reviewed baseline without an explicit accepted explanation |
| Resource limits | Every input/expansion/object/depth/pixel/worker/output/time boundary has below/at/above tests and safe failure |
| Security | At least 100,000 mutation cases per major parser/codec surface plus a recorded 24-hour guided fuzz campaign; zero unresolved exploitable crashes/hangs; regression seeds retained |
| Dependency safety | SBOM and complete notices; no unaccepted high/critical vulnerability; pinned native/model asset origins and hashes |
| Package consumers | Fresh C#/VB/F# consumers restore exact final packages on every applicable runtime; public API compatibility, symbols/SourceLink and dependency isolation pass |
| Public verification | NuGet indexing, repository signature and SHA-256 match; fresh public consumers pass complete business workflows |
| Cleanup | Evidence archived/verified; release-owned temporary VM/container resources removed or retention/failure explained |

These are proposed release acceptance thresholds, not measured results. Calibrate fixture-specific tolerances and workload sizes with recorded reasoning; do not lower a gate simply to accommodate a defect. Hardware-dependent absolute latency/memory envelopes are set by FP-088 before qualification, then enforced.

## Independent verification

Use at least two unrelated PDF readers/renderers for core interoperability, independent format readers for conversion, veraPDF where applicable, appropriate accessibility/prepress/engineering validators and independent signature/invoice validators. Record validator versions and limitations. If no reliable automatic validator covers a claim, add a standards-based manual checklist and retained expert review; never replace it with a metadata test.

Goldens require provenance and redistribution rights. Record original content hashes, expected semantics and changes. Never regenerate expected output from the implementation under test and call it independent evidence.

## Gate manifest

specs/release-readiness.json is the release switch and required lane/suite inventory. Each qualification report is immutable JSON with schemaVersion, sourceCommit, packageVersion, lane, runtime, os, architecture, suites and artifact hashes. suites contain positive test totals, zero failed, zero skipped and report paths/hashes. Publish stage additionally binds final package filenames/SHA-256.

eng/assert-production-ready.ps1 validates readiness, requirement status and retained report hashes. FP-094 must extend and independently exercise the gate when the full qualification runners exist. Foundation CI tests the current negative publication boundary only.

## Current result

No PDF functionality, native lane, conformance profile, benchmark, OCR result or stable package has been qualified by this planning baseline. All missing production evidence remains a publication blocker.
