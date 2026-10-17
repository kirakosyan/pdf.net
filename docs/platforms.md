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

## Actual runtime evidence

Building net472 or running it on Framework 4.8.1 does not qualify actual 4.7.2. Require an isolated 4.7.2 host, verify runtime identity, execute the exact final package and retain complete reports. The prior wordnet legacy-JIT requirement is historical evidence about that package, not a configuration requirement to copy into Fluent.Pdf without reproducing it.

ARM64 qualification must execute on ARM64 hardware/OS. Cross compilation, emulation, successful installation or a container label is insufficient. Linux containers do not qualify macOS or Windows. A hosted build runner proves only the lanes it actually executes.

Current foundation CI builds on Windows/Linux/macOS x64 runners. It does not yet implement the required runtime/provider test matrix; FP-086 owns that work.
