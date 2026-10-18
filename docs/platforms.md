# Platform and runtime contract

## Required deployment profile

| Runtime/profile | Windows | Linux | macOS |
| --- | --- | --- | --- |
| .NET 10 | x64, ARM64 | x64, ARM64 | x64, ARM64 |
| .NET 8 while upstream-supported | x64, ARM64 | x64, ARM64 | x64, ARM64 |
| .NET Framework 4.7.2 minimum | x86, x64 | Not applicable | Not applicable |
| Current .NET Framework | x86, x64 | Not applicable | Not applicable |
| netstandard2.0 fallback assembly | Explicit fallback consumer tests | Explicit fallback consumer tests | Explicit fallback consumer tests |

The required package targets are net472, netstandard2.0, net8.0 and net10.0. netstandard2.0 is an API compatibility surface, not a runtime. Do not infer support for every runtime that can load it. Full Framework runs on Windows. Linux is the intended Unix target; FreeBSD and other Unix variants are outside the current scope.

At release time, record exact supported OS versions/distributions, kernel/libc, runtime patches, architecture and provider ABI. A production release cannot leave minimum OS/libc versions unspecified. Qualify representative supported Debian/Ubuntu and enterprise Linux families; musl/Alpine requires a separate explicit provider qualification before advertising support. Serverless and container variants are deployment profiles tested by their adapters.

.NET 8 reaches its upstream support boundary soon after this planning date; verify the current Microsoft lifecycle at release and make any change through the documented compatibility policy, not an unnoticed removal.

## Applicability

Core parse/edit/write/generate/extract operations are cross-platform. Native rendering, OCR and browser operations require their exact optional engines on each supported RID. Missing engines produce capability diagnostics; they do not reduce the required production qualification matrix.

COM, legacy ASPX/WebForms and Windows-specific UI/printing integrations are Windows-only by design. Linux/macOS printing uses platform adapters. Do not use skipped tests to represent these applicability rules: declare applicability in the test plan and require complete reports for every applicable case.

Reader is currently only a buildable project placeholder. No Reader OS/runtime support claim is made.

## Native deployment preflight

The partial FP-085 [deployment inspector](native-deployment.md) recognizes the current OS/process RID for the six modern lanes and Windows x86. It checks caller-pinned PE, ELF or thin 64-bit Mach-O header architecture without loading files. RID equality does not establish libc, minimum OS, exports, calling convention, runtime dependencies, code signing or sandbox compatibility. Read-only asset probes run in source lanes; modern Unix lanes also exercise read-only directories and rejected root/file symbolic links. Windows privileged link scenarios and actual minimum Framework remain unqualified. No provider capability or native engine support is enabled.

## Actual runtime evidence

Building net472 or running it on Framework 4.8.1 does not qualify actual 4.7.2. Require an isolated 4.7.2 host, verify runtime identity, execute the exact final package and retain complete reports. The prior wordnet legacy-JIT requirement is historical evidence about that package, not a configuration requirement to copy into Fluent.Pdf without reproducing it.

ARM64 qualification must execute on ARM64 hardware/OS. Cross compilation, emulation, successful installation or a container label is insufficient. Linux containers do not qualify macOS or Windows. A hosted build runner proves only the lanes it actually executes.

Source contract CI executes .NET 8, .NET 10 and an explicit netstandard2.0 fallback consumer on native Windows/Linux/macOS x64 and ARM64 runners. The Windows x64 runner also executes the net472 contracts in current Framework x86/x64 processes. Runtime identity and exact loaded assembly target are asserted. Reports describe only the contract layer; they are not final-package, minimum-OS or provider qualification. See [testing.md](testing.md).

Actual Framework 4.7.2 execution and the complete production runtime/provider matrix remain pending FP-086 work and block release.

FP-002 policy, decoder-lease, deadline and accounting tests run on these same source lanes, with no new platform/native dependency. They do not qualify a PDF parser/renderer or forced termination of native work; each future provider must integrate and qualify the mandatory operation context.

The optional isolated worker now has [passing source boundary evidence](evidence/FP-085-source.json) on the six modern hosts and current Framework x86/x64. Its [OS prerequisites and limits](native-deployment.md) apply; source success does not qualify an engine, minimum runtime or package.

FP-022 metadata inspection/editing is managed and targets all four core frameworks on the applicable Windows/Linux/macOS lanes. No XML schema is downloaded. Full production qualification and exact final package consumers remain pending.

FP-023 attachments/collections use managed APIs on all four target frameworks. Payload processing is offline and bounded; native rendering and a portfolio UI are not dependencies. Source validation does not qualify complete archival or final-package behavior.

FP-024 layer graph/evaluation/flattening is managed on all four target frameworks. Independent Poppler output checks emitted configuration behavior; SDK rendering is a separate provider capability with its own qualification.
