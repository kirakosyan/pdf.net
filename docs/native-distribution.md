# Native distribution qualification status

## Offline source provisioning tools

`eng/native-distribution.py` now reads an explicitly SHA-256-pinned version-one inventory, verifies every file and its notice asset, and stages an offline installation under a unique private directory. It copies and rehashes exact bytes, verifies the stage, then commits with an OS operation that cannot replace an existing destination. A failed copy or commit removes only the owned stage inventory. The source tool never executes assets, resolves a network address, discovers libraries or downloads anything.

The inventory binds each portable relative path, exact length/hash, binary/data/model/font/notice role, version, declared origin, license and notice path. Executables/libraries additionally declare every observed static import and loader search path; imports bind either an inventoried library or an explicit system prerequisite. Original bounded PE import/delay-import, ELF64 dynamic-table and thin Mach-O load-command readers verify the declarations and architecture. Runtime-loaded modules, loader search semantics, installed system ABI, signing/notarization and legal redistribution approval remain separate required review. A static import observation is not complete runtime closure.

Supply existing absolute protected source/parent directories. The owner must prevent concurrent path, mount and file replacement; these source tools are not a race-free loader for attacker-controlled trees. The profile accepts at most 4 MiB of JSON, 4,096 files, 512 MiB per file and a caller-declared total of at most 10 GiB. Hashing/copying uses bounded chunks. Unknown properties, duplicate/case-colliding paths, traversal, reparse/symbolic links, hard-linked files, unpinned notices/imports, changed bytes and an existing destination reject. The installer requires Python 3.10+; no SDK runtime depends on Python.

Use `inspect-binary /absolute/file` to observe static imports, then author and review the inventory. Observation output does not approve redistribution or generate a qualified manifest. Run `verify --manifest /absolute/inventory.json --manifest-sha256 EXACT_SHA256 --source /absolute/assets --report /absolute/new-report.json`, followed by `install` with the same arguments and an explicit `--destination /absolute/new-installation` on the same filesystem as its private stage. Parent directories must already exist. A successful installation retains `provision-manifest.json`; existing reports/destinations are never overwritten.

For removal, use `eng/remove-native-installation.ps1` with explicit absolute `-InstallationDirectory`, `-PythonExecutable`, `-ReportDirectory` outside that installation, and the exact `-ManifestSha256`. It verifies the retained inventory, refuses any unowned file/directory or link, retains a verification report, then removes only the exact inventoried files and empty owned directories with literal-path operations. Preserve release evidence before invoking removal. Do not use global pruning or recursive deletion.

Source tests use original inert header fixtures and exact pinned original native worker/bridge files. They cover wrong/missing metadata/assets, changed bytes, notice/import failures, nested installs, interruption before commit, concurrent no-replacement commits and literal-path removal that preserves unowned files. They do not install or qualify third-party browser/rendering/OCR engines on clean air-gapped hosts. #93 stays skipped and release-blocking.

FP-105/#105 remains planned and blocked on complete deployment inputs. No native distribution package or qualified offline provisioning manifest is delivered by this checkpoint. Source worker fixtures and locally provisioned analysis assets are not shipping artifacts.

| Required input | Observed boundary |
| --- | --- |
| Browser host | The HTML adapter requires a caller-provisioned supervised Chromium connection; the repository does not supply a complete qualified cross-platform host distribution. |
| Scientific typesetter | The TeX adapter requires `IPdfIsolatedTexTypesetter`; a qualified implementation and complete font/format/package closure are absent. |
| Native rendering/OCR/format assets | Version and hash declarations and source observations exist, but complete per-RID dependency closure, redistribution review and production qualification remain outstanding. |
| Platform isolation | Existing worker source tests exercise isolation prerequisites; they do not certify every shipped engine or clean air-gapped installation. |
| Supply chain | Source SBOM and notice inventories are explicitly incomplete distribution observations, not approval to redistribute all native/model/font bytes. |

Resume with a reviewed per-RID inventory that binds each executable, library, model, font and data file to origin, version, SHA-256, license/notice, ABI, native loader dependencies and OS/kernel prerequisites. Include installation and removal instructions with no ambient package discovery or runtime downloads. Test fresh network-disabled hosts against those exact bytes, including missing/wrong dependencies, isolation denial, cancellation, resource limits and cleanup, and retain actual runtime identity and dependency-probe reports. Do not reuse a different source or package's qualification.

No clean offline installation acceptance runs were performed for this checkpoint. Packaging remains blocked, including the user-skipped mandatory #93 qualification. Continue independent source work with #106; publication is separately prohibited.

See [native deployment](native-deployment.md), [HTML rendering](html-rendering.md), [TeX](tex.md), [supply chain](supply-chain.md), [production gates](production-quality.md) and [evidence](evidence/FP-105-blocked.json).

The subsequent [private browser container source checkpoint](browser-container.md) retains explicit image/package/file inventories and modified analysis-policy provenance. It is a private provisioned source fixture, not a reviewed native distribution or clean air-gapped installation. Complete #105 acceptance remains blocked.
