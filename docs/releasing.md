# Stable release procedure

## Publication policy

Publish only when the complete SDK production scope is ready. Target first stable version: 1.0.0. No placeholder, alpha/beta or package-name reservation publication. Keep readiness false until the exact source and package evidence satisfies every applicable gate.

Current state: specification and project boundaries only. NuGet publication and persistent publishing-policy activation are not performed by this baseline.

## Package identity and ownership

Candidate core ID: Fluent.Pdf. Gallery and flat-container endpoints returned 404 on 2026-10-17. Recheck availability, reserved prefixes and ownership before configuring publishing. Related package candidates are listed in architecture.md; each needs its own authorized scope. A source project/package file does not reserve a NuGet name.

Use NuGet Trusted Publishing, repository kirakosyan/pdf.net, workflow publish.yml and GitHub environment nuget restricted to main. Scope policies to the exact package IDs being released, without unrelated wildcards or unlist/relist permissions. Set NUGET_USER to the authorized account. Never paste or use a stored long-lived API key.

## Release sequence

1. Fast-forward the active branch and verify the exact release commit is on current main. Reconcile all required issues, implementation state, API baselines and capability/version documentation.
2. Run complete source qualification on actual target runtimes, including actual Framework 4.7.2 x86/x64, modern Windows/Linux/macOS x64/ARM64 and fallback consumers.
3. Set requirement status to qualified only with reviewed evidence. Set readiness true only after all required statuses, lane suites and retained report hashes validate. eng/assert-production-ready.ps1 -Stage BeforePack must pass.
4. Build and pack release candidates using eng/pack.ps1. Projects invoke the readiness gate before nuspec generation. Include symbols, README, license/notices, SourceLink and the exact dependency/provider assets.
5. Qualify these exact final packages in fresh consumers on every applicable lane. Generate immutable Publish-stage reports binding filenames and SHA-256, validators, resource/fuzz/concurrency reports and business workflows to the source commit.
6. Run eng/assert-production-ready.ps1 -Stage Publish with the exact candidate and evidence directories. No rebuilding or replacing packages afterward. FP-094 completes automated qualification orchestration and independent gate auditing before publication is enabled.
7. Create the matching stable tag on main and dispatch publish.yml with tag and retained qualification run/artifact IDs. It downloads the qualified artifact, validates source/tag/version/evidence/hash correspondence, obtains a short-lived OIDC credential and pushes those exact package bytes. Persistent publishing authorization must match the account owner's applicable approval rules at action time.
8. Verify public NuGet indexing using uncached flat-container requests, repository signatures, package/symbol contents and fresh public consumers. Create the GitHub release with artifact hashes and qualification reports. A successful push is not successful release verification.
9. Preserve and verify all evidence, then perform the release-owned VM/container cleanup in AGENTS.md. Report what was removed, retained and failed.

## Current automation boundary

The checked-in publish workflow is fail-closed. It accepts only a stable main-branch tag, a previous successful qualification workflow run and a named artifact containing final packages and release-evidence.json. It verifies the current checked-in requirement/readiness state and exact evidence before granting OIDC to the publishing job.

The first implementation of full qualification runners and SourceLink/package inspection remains mandatory FP-086/093/094 work. Foundation CI is not a qualification workflow. Never point publication at a foundation artifact or manually change statuses to bypass that distinction.

Official references: [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing), [package publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package), [repository signatures](https://learn.microsoft.com/en-us/nuget/reference/signed-packages-reference).
