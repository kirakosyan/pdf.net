# Native deployment integrity preflight (partial FP-085)

Issue #3 begins with immutable offline provisioning metadata and read-only integrity checks. This is a partial implementation. Native loading, actual ABI compatibility, sandboxing, worker execution and crash recovery are unimplemented. An integrity result never enables a provider capability or qualifies an engine. FP-085 remains planned in the canonical manifest; all production gates and the NuGet publication block remain in force.

## Caller contract

`PdfNativeAsset` pins a portable single file name, exact positive length, SHA-256 and binary/data role. Names cannot traverse directories, name device files or contain alternate-stream syntax. `PdfNativeDeployment` captures a local fully qualified root, target RID, exact engine identity/version, a declared ABI label, a positive total-byte ceiling and 1–64 uniquely named assets. The complete declared inventory must fit the ceiling without overflow. At least one asset is a binary. Arrays are copied; metadata and reports are immutable.

Provision all assets before inspection. Obtain exact engine versions, digests and dependency inventories through a separately reviewed supply-chain process. The inspector does not discover imports, resolve system libraries, check signatures or establish that an asset inventory is complete. A trusted caller controls the manifest, directory ancestry and immutable provisioning tree. Mapped network mounts cannot be identified portably; callers must ensure storage is local. UNC/device roots and drive-relative paths are rejected.

```csharp
// Values below come from the caller's retained, reviewed provisioning manifest.
var deployment = new PdfNativeDeployment(
    localDirectory, targetRid, engineId, exactEngineVersion, declaredAbi,
    maximumProvisionedBytes, pinnedAssets);
PdfNativeDeploymentReport report = PdfNativeDeploymentInspector.Inspect(deployment, cancellationToken);
foreach (PdfDiagnostic diagnostic in report.Diagnostics)
    HandleProvisioningFailure(diagnostic);
// AssetsVerified describes integrity checks only. No execution is performed or authorized.
```

The executable [C#](../examples/Fluent.Pdf.Contract.CSharp/ContractExample.cs) and [VB](../examples/Fluent.Pdf.Contract.VisualBasic/ContractExample.vb) `InspectDeployment` consumers take the actual caller inventory rather than embedded example hashes. Source consumers compile for every core TFM. Exact final-package consumers remain blocked with candidate creation until full SDK qualification.

## Inspection and diagnostics

`Inspect` first matches the current **process** architecture and OS to the declared RID. It checks an ordinary root directory and ordinary files without root/file reparse points, opens each asset for reading, verifies its exact length and SHA-256 with one reusable 64 KiB buffer, then checks applicable binary headers. It creates no files, performs no download, loads no library and searches no PATH. The explicit total-byte ceiling bounds declared asset sizes independently of PDF input/expansion budgets; inspection additionally reads at most 128 header bytes per binary and one EOF probe per asset. Cancellation is cooperative before and after file reads; it cannot interrupt opaque filesystem calls.

| Declared RID | Header checks |
| --- | --- |
| win-x86 / win-x64 / win-arm64 | MZ/PE signature, bounded PE-header offset and matching COFF machine |
| linux-x64 / linux-arm64 | Little-endian 64-bit ELF, version/type and matching machine |
| osx-x64 / osx-arm64 | Thin little-endian 64-bit Mach-O, file type and matching CPU |

Unknown/malformed headers and universal Mach-O containers are rejected explicitly. These are header checks, not complete executable validation. The engine version and ABI labels are declared metadata; matching bytes and headers do not prove exports, calling conventions, libc, minimum OS, loader dependencies, code-signing validity or functional behavior.

| Code | Meaning |
| --- | --- |
| Native.RuntimeMismatch | Unsupported current host or declared RID does not match it |
| Native.AssetMissing | Root or a required pinned file is absent |
| Native.AssetInaccessible | Permissions or I/O prevent inspection |
| Native.UnsafePath | Root/file is a link/reparse point or an inappropriate file type |
| Native.SizeMismatch | Exact declared size disagrees before/during hashing |
| Native.HashMismatch | SHA-256 disagrees |
| Native.BinaryFormatUnsupported | Unsupported, malformed or truncated header |
| Native.ArchitectureMismatch | Header architecture disagrees with the declared/current process RID |

Failures appear in inventory order and identify the engine and portable asset name; messages omit directory paths and payload bytes. Root/RID failures stop before file inspection. `VerifiedAssetCount` counts accepted files and `AssetsVerified` is true only when the entire inventory passed. Reports are detached observations of inspected bytes. They are not reusable load handles and become stale when files change.

Within a provider operation, use `PdfNativeDeploymentInspector.Verify(deployment, context)` to apply operation cancellation/deadline checkpoints and report any preflight failure through the mandatory context. Those errors latch in both strict and permissive modes. Catching one cannot turn the enclosing save into success, and the existing atomic save boundary preserves the destination. No operation context or caller stream is retained.

## Security boundary and remaining delivery

Portable path and reparse checks are not a race-free secure loader. Ancestor links/mounts, filesystem replacement races and system-library resolution require a separately qualified platform loader/worker design. Do not inspect an attacker-controlled provisioning tree or use a report as permission for in-process loading. No engine, native library, model, worker binary or new runtime dependency is included here.

FP-085 still requires reviewed RID-specific distribution, engine/dependency/ABI probes inside isolated workers, authenticated transport, safe search/load rules, actual OS sandbox/resource limits, bounded process-tree termination, crash/hang recovery and cleanup. Actual native crash injection is absent. Production qualification must cover all applicable real runtimes, minimum Framework x86/x64, wrong ABI, read-only deployment, concurrency, resource limits, supply chain and exact packages. Issues #4–#20 remain pending in the established dependency sequence.

The [source tests](../tests/Fluent.Pdf.Contract.Tests/NativeDeploymentTests.cs) use original inert header fixtures and explicitly pinned temporary files. They exercise header/RID/size/hash failures, missing dependencies, portable names, 64-asset and cumulative-byte boundaries, overflow, cancellation during reads, immutable reports, C#/VB examples and failed preflights preserving existing file output. Read-only files run on all source lanes. Modern Unix runtimes also exercise read-only directories and root/file symbolic links; Windows privileged-link scenarios and actual minimum Framework are separate pending evidence. These tests do not execute or qualify native code.
