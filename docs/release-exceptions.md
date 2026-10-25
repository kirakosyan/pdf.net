# Version 1.0.0 release exception

On 2026-10-25 the user requested that [#93 / FP-089](requirements/FP-089.md) stop blocking the first production release because specialist security review is unavailable. The decision is recorded in [release-readiness.json](../specs/release-readiness.json). The requirement remains planned, unqualified and open; the catalogue still includes all 111 SDK requirements. Version 1.0.0 has 110 active requirement qualifications and one explicit deferral.

The exception defers FP-089's extended security/fuzz campaign and specialist review. It does not claim that these activities passed. Automated security regression suites, malformed-content/resource/cancellation/isolation boundaries, dependency advisory checks and fixes for known exploitable defects remain required. It does not waive independent format validation, accessibility review, other requirements' functional security behavior or any runtime/package-consumer lane.

The shared [policy checker](../eng/release-policy.ps1) accepts only FP-089, issue 93, version 1.0.0 and the recorded authorization date. Duplicate, incomplete, expanded or stale exceptions reject. The deferred feature must stay required in the catalogue and must not be marked qualified. Subsequent versions cannot inherit this exception automatically.

Both source and exact-package `release-evidence.json` must contain the exact checked-in `requirementExceptions` array. The production gate rejects missing or changed disclosure and rejects FP-089 in the qualified requirement records. Release notes must disclose the deferred specialist/extended-campaign assurance; they must not advertise a completed security audit.

The [policy regressions](../eng/test-release-policy.ps1) exercise the real gate in isolated synthetic fixtures and prove that other requirements, missing lanes and readiness still block. These fixtures contain no SDK package and do not constitute production qualification.

Generate a fresh inventory with `pwsh -NoProfile -File eng/get-release-status.ps1 -OutputPath artifacts/release-status/current.json`. The report lists active acceptance criteria, pending implementation work and unqualified dependencies, with actual checkout identity. It never marks evidence qualified or runs tests, and it rejects overwriting an existing report.
