# Compatibility and maintenance policy

This policy governs review of Fluent.Pdf changes. There is no supported published release yet. FP-106 remains planned until maintainer review, complete runtime qualification and the final-package servicing drill are accepted. Source targets and CI runners are not a support promise.

## Version and API changes

Use [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html): compatible fixes increment patch, compatible additions increment minor, and incompatible public changes require a major version. Published bytes are immutable; all coupled SDK/provider packages advance together with exact reviewed dependency versions. Before first publication, keep unshipped declarations distinct from the eventual published API baseline.

Compatibility includes binary and source signatures, parameter names used by named arguments, nullable contracts, exceptions/diagnostic codes, ownership, cancellation, resource accounting, defaults, document output and provider ABI. A passing API comparison only covers part of this contract. See Microsoft's [library change guidance](https://learn.microsoft.com/en-us/dotnet/core/compatibility/library-change-rules).

Every incompatible proposal must include a reviewed migration note: affected signatures/behaviors, old/new C# and VB examples, supported version boundaries, output or stored-data impact, safer alternatives and rollback limits. Retain the prior shipped baseline and compare candidate assemblies against it; changing the baseline to erase a failure is not acceptance. Cross-TFM comparisons in `eng/check-api.ps1` remain separately required. Review overload additions for ambiguity, especially `null`, optional parameters and inference.

Deprecate before removal in an ordinary major release: document an available replacement in a minor release, add a non-error `Obsolete` warning with migration guidance, retain behavior during the current major line, and remove only after the next major's migration review. Security-critical disabling may require an exceptional behavior change with explicit affected-version advisory, mitigation and release notes; it does not waive qualification or silently change resource/security policy.

## Runtime, OS and provider support

The release matrix must intersect actual exact-package qualification with upstream-supported runtime/OS combinations and qualified provider prerequisites. Record OS edition/version, Linux distribution/kernel/libc, architecture, runtime patch, provider ABI/engine/model/font identities and the date each upstream policy was checked. No minimum OS version is inferred from a hosted runner. An API-compatible fallback assembly is not a runtime lifecycle.

Lifecycle snapshot checked 2026-10-24: [.NET support](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) lists .NET 8 maintenance through 2026-11-10 and .NET 10 through 2028-11-14, with current patches required. [.NET Framework policy](https://learn.microsoft.com/en-us/lifecycle/faq/dotnet-framework) ties Framework 4.x support to its underlying Windows product. Recheck these policies and the exact OS/provider lifecycle before every release. Actual minimum Framework 4.7.2 execution remains a separate compatibility qualification; a newer in-place runtime cannot substitute for it. An obsolete OS retained for compatibility testing must not become a supported production deployment claim.

Do not silently drop the required net8.0 target or any mandatory lane when upstream support ends. Record a reviewed scope/lifecycle decision with migration guidance, update the specification and support matrix together, and continue to block publication while that decision or qualification is missing. See [platforms](platforms.md).

The first release must identify a responsible maintainer, supported stable version lines, security triage/update commitments and end-of-support notice policy. Those commitments are currently unapproved; there is no promised response SLA or indefinite maintenance of third-party assets. For now every production-support claim is empty. Subsequent releases must retain the historical matrix and explicitly record which supported line receives each fix.

## Dependency and security servicing

Use the private channel in [SECURITY.md](../SECURITY.md). Record affected versions/RIDs, exploit prerequisites and a minimal original reproduction privately. Assign severity and an owner, decide mitigation and coordinated disclosure, then retain the reviewed fix, regression and release disposition. Do not expose private customer documents or keys in reports.

For any engine, library, model, font or data update, retain old/new provenance, exact bytes/hashes, license/notice review, advisories and an affected-operation/RID analysis. Update locked dependencies and the source inventory, rerun applicable fidelity, ABI, sandbox, cancellation, resource, concurrency and fuzz tests, then regenerate the final distribution SBOM. Even an API-compatible security patch invalidates previous package hashes and affected qualification. No runtime download or ambient engine upgrade is introduced as a servicing shortcut.

The release record must bind source commit/tag, build tools and locks, all package/symbol/native asset hashes, supported matrix, complete raw report digests, notices/SBOM, migration/advisory notes and approver identity. Archive verified evidence before cleanup. Follow [releasing](releasing.md), [supply chain](supply-chain.md) and [package provenance](package-reproducibility.md).

## Rollback, deprecation and unlisting

Before any external action, identify exact package IDs/versions and obtain the release owner's authorization. Freeze promotion of the affected bytes; preserve their evidence. A consumer rollback restores a previously qualified, still-safe exact version and compatible configuration/data, then re-executes the affected consumer checks. Never recommend rolling back to a known vulnerable version merely because it was previously released. If no safe version exists, disable the affected feature or stop use until a qualified fix is available.

Publish a fix under a new version through the normal gates. Never rebuild or replace an existing published version, retag it, or discard the historical advisory. [NuGet unlisting](https://learn.microsoft.com/en-us/nuget/nuget-org/policies/deleting-packages) reduces discovery but exact-version restore remains possible; it is not revocation or deletion. [Deprecation](https://learn.microsoft.com/en-us/nuget/nuget-org/deprecate-packages) communicates the affected version and replacement separately. Review whether both are appropriate. Retain authorized actions and verify public metadata plus fresh consumers. A later relisting requires separate review of the unchanged bytes and the reason they are safe.

## Executable source drill

Run `python eng/test-maintenance.py --output artifacts/maintenance-drill` after `dotnet tool restore`. The drill builds original isolated API fixtures, checks compatible implementation/addition changes and rejects removed methods/renamed parameters using the pinned API tool. A synthetic dependency byte change demonstrates evidence-hash invalidation; the real unchanged BeforePack and Publish gates must still reject the unqualified checkout. It retains commands, outputs and hashes without creating SDK packages, modifying readiness, contacting a gallery or changing dependencies.

This is a source policy/tool exercise, not a security fix, clean final-package consumer, approved SLA, actual rollback or publication rehearsal with credentials. The final-package maintenance drill and policy review remain mandatory before support is advertised.
