# Package and symbol provenance

FP-093 has source tooling; final package qualification is pending. Candidate creation and publication remain blocked. No SDK package has been produced by this checkpoint.

All fifteen managed SDK projects use `src/Directory.Build.targets` for one candidate version, MIT metadata, repository identity, README, XML documentation and portable symbols. SDK-to-SDK dependencies use an exact version range. The complete checked-in notice inventory is retained under `notices/`; it includes explicitly identified test and analysis components and is not a declaration that every listed tool is a runtime dependency. The managed core still has no browser, OCR, GUI or native package reference. Optional RID assets and final supply-chain review remain separate requirements.

Every project's actual `GenerateNuspec` path invokes the unchanged fail-closed production gate. `eng/test-release-block.ps1` exercises all fifteen pack targets when readiness is false. `eng/test-package-metadata.ps1` checks evaluated properties and dependency versions without creating a package. SourceLink is supplied by the pinned .NET SDK; no additional runtime package is needed.

`eng/test-rebuild.ps1` builds the solution twice with fresh, separate object/output directories, locked restore, deterministic compilation and canonical source paths. It requires byte-identical DLL, portable PDB and XML files for all fifteen assemblies and all four TFMs. The metadata-only `eng/PackageProbe` checks each assembly/PDB identity, deterministic marker, mapped source paths and embedded SourceLink repository/commit. It never loads inspected SDK code or accesses remote URLs. The evidence records dirty source explicitly. Two output directories in one checkout do not establish independent-checkout or final-package reproducibility.

After source qualification permits candidates, `eng/pack.ps1` requires a clean checkout, builds the source, records a content manifest and packs all fifteen components with symbols. Partial failure output must be retained for investigation and cannot be published. The source content manifest binds README/license/notices, every DLL/XML/PDB and the expected dependency groups. Review this inventory together with the actual evaluated project graph before package qualification.

`eng/package-audit.py` inspects two explicit candidate directories against that manifest. It rejects missing, changed or unexpected payloads, version/dependency/commit drift, ambiguous paths, duplicates, symlinks, XML declarations/entities and bounded archive expansion. It does not extract or execute package content. It requires exact archive-byte equality between rebuilds, including container metadata. Its output is inspection evidence with `productionQualified:false`, never publication authorization. Signed repository artifacts need a separate post-publication signature verification step; the pre-publication inspector deliberately rejects additional signature payloads.

```powershell
pwsh -NoProfile -File eng/test-package-metadata.ps1
pwsh -NoProfile -File eng/test-rebuild.ps1
# Only after the existing BeforePack gate passes:
pwsh -NoProfile -File eng/pack.ps1 -EvidenceDirectory <qualified-source> -OutputDirectory <new-candidates>
python eng/package-audit.py --manifest <reviewed-manifest.json> --directory <candidates> --rebuild-directory <independent-rebuild> --output <inspection.json>
```

Actual final packages, independent clean-checkout rebuilds, source-content checksum/download verification, signatures, the complete native/provider distribution, API baselines against the first shipped version, all package-only runtime consumers and the full production suites are still mandatory. The existing 45 cross-TFM API comparisons remain source compatibility evidence. No requirement is qualified by a synthetic ZIP fixture or a matching build hash.
