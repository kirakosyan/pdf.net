# Source dependency and asset inventory

FP-095 provides source inventory and audit tooling. It does not approve redistribution or qualify final packages. No stable SDK release exists yet. Mandatory final-package, runtime, RID, fidelity, security and licensing work remains open.

`eng/audit-supply-chain.ps1` reads every checked-in NuGet lock, including SDK, native worker, examples and development tools. It groups exact dependency identities without dropping TFM/project context. Source graphs include build/test tools and the excluded Reader project; they are not a claim that those dependencies ship in the SDK. The offline inventory never downloads a runtime asset, loads dependency code or extracts arbitrary archive members.

For available cached packages, `eng/SupplyChainProbe` uses the selected SDK's NuGet reader to recompute the locked content hash and separately records the archive SHA-256. Signed NuGet content hashes are distinct from raw archive hashes. Mismatches fail; unavailable packages remain explicitly unavailable. Tool version/hash and malformed/tampered-input evidence are retained. This does not validate signer trust or certificate chains.

The inventory records upstream nuspec license declarations and hashes of embedded license/notice entries. Missing declarations stay unknown. License names and URLs are not treated as approval, and no license-equivalence inference is made. Every retained repository notice, provenance JSON and SDK build configuration is hashed. The package metadata check now includes uppercase text suffixes and extensionless `-LICENSE` files consistently; this fixes two omitted retained upstream license files in the preceding filename filter. Final packages must still pass exact content inspection against the reviewed inventory.

Optional explicit asset directories are hashed and matched to individual SHA-256 declarations by manifest and JSON pointer. A matched notice is a notice observation, not proof that its engine is qualified. Unmatched hashes remain declarations. These results do not establish native source-to-binary provenance, model quality, platform applicability or redistribution rights. See the component-specific records under `third-party/` for declared source revisions, asset/container hashes, licenses, limitations and prior failed observations.

The generated `bom.cdx.json` uses CycloneDX 1.6, with incomplete composition and explicit unqualified source scope. A separate `inventory.json` retains lock/TFM contexts, NuGet verification results and local asset observations. The official schema is an independent analysis input; schema validation establishes structural validity only. No SBOM is labeled a final distribution SBOM until the exact final packages and all shipped native/model/font assets are reconciled.

The advisory runner uses a new task-local HTTP cache and retains the configured-source response. `eng/check-vulnerabilities.py` rejects query problems, missing/duplicate project coverage and any reported top-level or transitive finding. A clean response means no known advisory was returned for that query, not proof of safety. Native projects outside the solution require their own restored-project queries. Native engines, models, source-pattern reuse and bundled data also need their specific vulnerability/support review.

Dependency, SDK, package-project, native provenance or notice changes invalidate prior qualification. Comparing inventories lists every added, changed or removed input and requires all applicable runtime/RID and fidelity qualification. The PR source matrix runs on each change; it cannot substitute for the complete production matrix. Record original/new hashes, reason, affected functionality, notices and advisory results in the update review. Never carry old final-package qualification onto changed bytes.

```powershell
dotnet build eng/SupplyChainProbe/SupplyChainProbe.csproj -c Release
pwsh -NoProfile -File eng/audit-supply-chain.ps1 -AssetDirectories <explicit-local-assets>
python eng/supply-chain.py --root . --cache <nuget-cache> --probe <SupplyChainProbe.dll> --output <new-directory> --previous <previous-inventory.json>
```

Before first publication, review the complete license/notice inventory and asset rights, bind the final SBOM and advisory reports to exact package bytes, establish actual engine/model support lifetimes, and execute every applicable required qualification gate. See [package provenance](package-reproducibility.md) and the [security support policy](../SECURITY.md).

Technical references: [CycloneDX 1.6 schema](https://github.com/CycloneDX/specification/blob/1.6/schema/bom-1.6.schema.json), [NuGet content hashing](https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/PackageArchiveReader.cs). The reports retain the actual local schema/tool hashes used.
