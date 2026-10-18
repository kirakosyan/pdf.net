# Agent instructions for Fluent.Pdf

## Repository freshness

Before investigation, planning, reading project files, edits, builds, tests, commits or pushes in an active Git checkout, update its current branch from upstream with `git pull --ff-only`. If local changes, divergence or a missing upstream block that initial update, stop work in that checkout and report it. Never stash, reset or overwrite someone else's work to satisfy this rule. For direct work on main, first confirm it includes origin/main. A newly created task branch inherits the verified base; set upstream on its first push.

Read README.md, docs/product-spec.md, docs/architecture.md, docs/production-quality.md and the assigned requirement before implementation. Inspect issue comments and dependencies first. Work within the current user's authorized scope.

## Product and naming

The product/package candidate is Fluent.Pdf. The scope is Windows, Linux and macOS; Linux is the intended meaning of Unix. Target a production-quality stable SDK. Keep the specified excluded reference-product names out of tracked files, code, comments, commit messages, issues, PRs, package metadata and generated documentation. eng/validate-spec.ps1 checks the exclusion using encoded terms.

Use original implementation and our own API. Reference analysis establishes requirements and interoperability observations. Never copy reference implementation bodies or distribute reference binaries. Keep raw reference packages, raw source URLs and tool outputs outside this repository; tracked research uses neutral source IDs and digests. Do not run imported document actions during ordinary inspection.

## Current status and truthfulness

The repository contains research, specification, a backlog, document/provider contracts and operation policies/budgets. Source implementations include isolated native workers, bounded syntax/filter processing, optional image codecs, managed page reading/full/incremental writing, typed content/version handling, an optional managed font provider, Unicode/bidi analysis with optional isolated shaping, vector/color/transparency resources, page canvas/geometry, paragraph flow/pagination, merged/nested tables with bounded data binding, lists/columns/notes with basic column structure, bounded page templates with linked contents/indexes, explicit page organization/imposition profiles, and bounded image/Form extraction/editing with PNG/JPEG metadata and masks, plus annotation/appearance editing and explicit XFDF/flattening profiles, plus passive navigation/action authoring, all-active-object inspection and explicit retention policies. No rendering engine is implemented. See docs/api-contract.md and docs/operation-policies.md for available behavior and qualification gaps. Do not call a build, contract fixture, package upload, emulator/VM boot or installation proof of PDF functionality or production readiness.

All SDK requirements marked releaseRequired in specs/requirements.json are mandatory for stable publication. The Reader requirement is excluded. Do not silently narrow the required scope to make a release pass. Status changes require reviewed implementation and evidence. Do not publish placeholders, alpha/beta packages or package-name reservations.

## Architecture rules

- Keep the managed core independent of native/browser/OCR/GUI packages and platform-specific types.
- Public contracts must work consistently across net472, netstandard2.0, net8.0 and net10.0.
- Use optional provider interfaces for rendering, HTML, OCR, fonts/shaping, codecs, format adapters, signing and model integrations.
- Document provider capabilities by OS/RID/runtime/version. No runtime binary or model download by default.
- No System.Drawing/GDI+ assumption in cross-platform core. Windows-only COM, ASPX, printer or UI integration belongs in separate adapters.
- Apply operation policies at every parse/decode/layout/render/save boundary. Avoid hidden global mutable configuration.
- Providers receive the mandatory PdfOperationContext. Charge objects/pixels/expanded bytes before allocation; use scoped depth/workers, bounded decoder leases and cooperative checkpoints. Never catch and hide latched failures, bypass the SDK stream lease or return with outstanding workers/scopes.
- Strict mode rejects unsupported requested behavior. Permissive mode reports every loss with stable diagnostic codes.
- Bound input, expansion, recursion, pixels, objects, workers, native execution and output. Preserve caller stream ownership; save files atomically.
- Documents have explicit thread-safety rules. Independent jobs isolate browser contexts, fonts, caches, credentials and temporary paths.
- Keep ordinary processing offline. Remote resources and model services require explicit caller configuration and network policy.
- Reuse reviewed wordnet patterns with MIT attribution and a pinned commit. Do not introduce package cycles.

## Task delivery

Take one dependency-ready issue at a time unless the user explicitly requests parallel agent work. Do not spawn other agents merely because the backlog is large. Each requirement has scope, dependencies, acceptance, validation and non-goals. Resolve routine design choices within the settled architecture; record consequential API/dependency tradeoffs.

For each delivered requirement:
1. Read its full issue and requirement, then verify dependencies and current capability state.
2. Implement original code and meaningful tests for its observable behavior, error paths and resource boundaries.
3. Run the applicable source and exact-package consumers on actual supported runtimes; record unsupported environments accurately.
4. Update capability declarations, API baselines, docs, samples, dependency notices and evidence.
5. Reconcile issue status and implementation-plan links after merge. Do not mark dependent work complete by association.

Use rg for file/text search and batch independent reads. Keep secrets, licensed datasets and research archives out of Git. Never patch global tooling or someone else's checkout to hide a failure. A bounded task-local analysis-tool modification must retain its exact diff, original/modified hashes and limitations.

## Validation and publication

Run eng/validate-spec.ps1 and the relevant build/tests after changes. CI checks source contracts, API compatibility and release blocking; see docs/testing.md. It does not qualify PDF processing or final packages. Production gates in docs/production-quality.md are literal acceptance requirements, including independent conformance, visual/semantic fidelity, actual minimum runtimes, concurrency, fuzzing, resource limits and clean package consumers. A skipped required test or missing required lane blocks release.

Use docs/releasing.md and the fail-closed eng/assert-production-ready.ps1. Never bypass the package target, weaken a gate, fabricate evidence, copy reports from a different commit or infer qualification from an API inventory. Candidate creation and public publication both require complete qualification. Use NuGet Trusted Publishing with exact package scopes; never paste stored API keys. Publish only the exact qualified artifacts and verify indexing, signatures and fresh public consumers.

## Safe cleanup

Archive and verify qualification reports, source/package hashes and release-owned resource inventory before cleanup. Gracefully stop disposable qualification VMs and helper processes; remove only this release's temporary containers, images, networks, volumes, disks, snapshots, downloads and credentials. Protect shared resources and retained OCR/font/model assets. Do not use global Docker pruning.

Before any recursive Windows deletion or move, resolve and verify absolute target paths remain within the explicitly intended workspace/resource directory. Use native PowerShell LiteralPath operations end to end. Report removed, retained and failed resources. Never remove research evidence until it is preserved and the owner has authorized its disposal.
