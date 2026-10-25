# Stable release procedure

## Publication policy

Publish only when the approved SDK production scope is ready. Target first stable version: 1.0.0. The [user-authorized exception for FP-089/#93](release-exceptions.md) applies only to this version and does not qualify the deferred work. No placeholder, alpha/beta or package-name reservation publication. Keep readiness false until the exact source and package evidence satisfies every other applicable gate. The user has authorized completing qualification and publication; no further publication confirmation is required in this task.

Current state: the [sequential source queue](implementation-plan.md) includes SDK implementations, source consumers and evidence, with explicit incomplete profiles and prerequisite blockers. Complete production qualification remains incomplete. Candidate packing and NuGet publication stay blocked. See the [release checkpoint](release-qualification-status.md); no publishing policy is activated by source implementations.

## Package identity and ownership

Candidate core ID: Fluent.Pdf. The user selected the public [kirakosyan profile](https://www.nuget.org/profiles/kirakosyan) as the publisher on 2026-10-25. This identifies the intended account; it does not verify a signed-in session, package/prefix ownership or account-side OIDC policy. Recheck availability, reserved prefixes and ownership before publishing. Related package candidates are listed in architecture.md; each needs its own authorized scope. A source project/package file does not reserve a NuGet name.

Use NuGet Trusted Publishing, owner profile kirakosyan, repository kirakosyan/pdf.net, workflow filename publish.yml and GitHub environment nuget restricted to main. The pinned login action now selects that public username directly; no separate NUGET_USER repository variable is required. Scope the account-side policy to the exact package IDs being released, permit new-package publication for the first release and new versions for servicing, and exclude unrelated wildcards or unlist/relist permissions. Verify the selected owner and effective scopes in the signed-in NuGet session. Never paste or use a stored long-lived API key.

## Release sequence

The user supplied [word.net's publishing workflow](https://github.com/kirakosyan/word.net/blob/c723fb8aa4ed70fda5562da7f472e6fef54740b7/.github/workflows/publish.yml) as the working reference. Its [successful publishing run](https://github.com/kirakosyan/word.net/actions/runs/39494674611) uses the same pinned NuGet OIDC action, a nuget environment, exact-source CI artifacts and package consumers. PDF keeps its own approved production gates and must produce its own exact-artifact evidence. The NuGet policy must authorize repository pdf.net; the reference repository's successful publication establishes its own configuration.

Apply the [compatibility and maintenance policy](maintenance.md) for version selection, lifecycle review, migration notes and servicing/rollback records. A dependency update invalidates affected package qualification even when the API is unchanged. No public action is part of the offline source maintenance drill.

1. Fast-forward the active branch and verify the exact release commit is on current main. Reconcile all required issues, implementation state, API baselines and capability/version documentation.
2. Run complete source qualification on actual target runtimes, including actual Framework 4.7.2 x86/x64, modern Windows/Linux/macOS x64/ARM64 and fallback consumers.
3. Set requirement status to qualified only with reviewed evidence. Keep FP-089 planned and copy the exact checked-in requirementExceptions disclosure into source and package release-evidence.json; do not include it in qualified requirement records. Set readiness true only after all active required statuses, lane suites and retained report hashes validate. eng/assert-production-ready.ps1 -Stage BeforePack must pass.
4. Build and pack release candidates using eng/pack.ps1. Projects invoke the readiness gate before nuspec generation. Include symbols, README, license/notices, SourceLink and the exact dependency/provider assets.
5. Qualify these exact final packages in fresh consumers on every applicable lane. Generate immutable Publish-stage reports binding filenames and SHA-256, validators, resource/fuzz/concurrency reports and business workflows to the source commit.
6. Run eng/assert-production-ready.ps1 -Stage Publish with the exact candidate and evidence directories. No rebuilding or replacing packages afterward. FP-094 completes automated qualification orchestration and independent gate auditing before publication is enabled.
7. Create the matching stable tag on main and dispatch publish.yml with tag and retained qualification run/artifact IDs. It downloads the qualified artifact, validates source/tag/version/evidence/hash correspondence, obtains a short-lived OIDC credential and pushes those exact package bytes. Persistent publishing authorization must match the account owner's applicable approval rules at action time.
8. Verify public NuGet indexing using uncached flat-container requests, repository signatures, package/symbol contents and fresh public consumers. Create the GitHub release with artifact hashes and qualification reports. A successful push is not successful release verification.
9. Preserve and verify all evidence, then perform the release-owned VM/container cleanup in AGENTS.md. Report what was removed, retained and failed.

## Current automation boundary

The checked-in publish workflow is fail-closed. It accepts only a stable main-branch tag, a previous successful qualification workflow run and a named artifact containing final packages and release-evidence.json. It verifies the current checked-in requirement/readiness state and exact evidence before granting OIDC to the publishing job.

The [package provenance tooling](package-reproducibility.md) checks shared metadata, source rebuild bytes and embedded SourceLink, and provides an offline final-candidate inspector. Its source checks do not qualify final packages. Full qualification runners and actual package inspection remain mandatory FP-086/093/094 work. Foundation CI is not a qualification workflow. Never point publication at a foundation artifact or manually change statuses to bypass that distinction.

Official references: [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing), [package publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package), [repository signatures](https://learn.microsoft.com/en-us/nuget/reference/signed-packages-reference).
