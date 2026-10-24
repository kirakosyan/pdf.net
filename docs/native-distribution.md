# Native distribution qualification status

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
