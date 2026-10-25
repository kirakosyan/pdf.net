# Continue release work in a local Windows Codex session

The cloud task has no attached Windows terminal or PC connection. Open the Windows repository in a local Codex session to run commands there. Do not send credentials or expose a remote shell to the internet. The user's authorization to complete qualification, defer #93 for 1.0.0 and publish qualified packages persists in the handoff below.

Use this task prompt in the local session:

> Continue Fluent.Pdf production readiness from branch codex/release-readiness-policy in kirakosyan/pdf.net. Read AGENTS.md and follow its initial git pull --ff-only rule; preserve any unrelated changes and existing VM assets. Fetch and review the release-policy branch before using its tooling. The user authorized completing implementation, actual qualification and publishing Fluent.PDF 1.0.0. #93 is explicitly deferred for 1.0.0 under docs/release-exceptions.md; do not call it qualified or waive automated security checks. Read docs/release-qualification-status.md, observe this Windows host with eng/get-windows-release-host.ps1, and reuse the existing isolated Framework 4.7.2 guest if available. Complete current-source and exact-package testing; earlier reports establish only their earlier commits. Work through remaining implementation dependencies before claiming qualification. No additional publication permission is needed; publish only exact artifacts that pass the approved gates. Preserve and hash reports and report any missing host, validator, asset, manual review or NuGet configuration explicitly.

After the branch is checked out in a clean verified worktree, the host observation needs only built-in Windows PowerShell 5.1:

```powershell
$report = Join-Path $PWD ("artifacts\windows-release-host-" + [guid]::NewGuid().ToString('N') + '.json')
powershell.exe -NoProfile -File .\eng\get-windows-release-host.ps1 -OutputPath $report
Get-Content -LiteralPath $report
```

This preflight reads the actual 64-bit Framework registry release and native host architecture. It changes no Windows feature, runtime, Docker installation or registry key. It runs no SDK tests. An x64 host with release 528040 or later is eligible for current Framework source lanes only. Actual 4.7.2 release keys 461808/461814 identify minimum-lane eligibility, subject to same-process verification when tests execute. A targeting pack supplies compilation references; it does not install a side-by-side older 4.x runtime. Do not uninstall or downgrade the physical PC's serviced Framework.

The [earlier Windows evidence](runtime-qualification.md#windows-source-observations-2026-10-23) already records a separate offline Hyper-V guest with Framework 4.7.2 and successful x86/x64 source tests at an older snapshot. Reuse that guest if it still exists; no new VM is needed merely because a new release attempt begins. Preserve its original disk and test an owned copy. The local session must identify its actual paths and provisioning state rather than infer them from an old report.

Use the exact commit's `framework-source-bundle-win-x64` source artifact and [offline bundle runner](../eng/run-framework-bundle.ps1) for new complete source tests. On the minimum guest, run x86 and x64 with `-RequireFramework472`, fresh owned workspace/results directories and no modern runtime installer. On the physical current-Framework PC, omit that switch. Run [check-runtime-evidence.py](../eng/check-runtime-evidence.py) against the resulting TRX and retain identities, hashes, complete totals and raw failure logs. Source bundles do not contain final SDK packages and cannot qualify final consumers.

The 22-lane inventory remains required. Full native/model distributions, independent workflow/conformance/accuracy evidence, applicable accessibility review, the production orchestrator and fresh exact-package consumers remain separate work. Finish these before setting readiness true. The user selected the public [kirakosyan NuGet profile](https://www.nuget.org/profiles/kirakosyan); the publishing workflow selects this username directly. In the signed-in account, configure and verify scoped Trusted Publishing for repository `kirakosyan/pdf.net`, workflow filename `publish.yml`, environment `nuget` and the exact package IDs. Permit first publication of new packages and later versions, without unlist/relist scopes. No NUGET_USER repository variable is needed. Package/prefix ownership and the effective account-side policy remain unverified from the cloud session. Never paste a long-lived API key.
