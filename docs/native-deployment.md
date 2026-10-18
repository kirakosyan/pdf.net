# Native deployment and isolated workers (FP-085)

FP-085 combines immutable offline provisioning metadata with the optional `Fluent.Pdf.Workers` source adapter and original C worker host. The managed core does not load native libraries. The adapter creates one private process and staging directory per job, verifies every copied asset, and accepts output only after ABI/version negotiation, bounded framing and successful exit. [Native source validation](evidence/FP-085-source.json) passed on all six hosts and current Framework x86/x64; production qualification and the NuGet publication block remain in force.

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

Production qualification must still cover reviewed engine distribution and the complete runtime/security/endurance corpus, minimum Framework x86/x64 and exact final packages. Native fixtures exercise the original bridge and actual OS boundaries; they do not qualify a third-party engine. The remaining issues continue in the established dependency sequence.

The [source tests](../tests/Fluent.Pdf.Contract.Tests/NativeDeploymentTests.cs) use original inert header fixtures and explicitly pinned temporary files. They exercise header/RID/size/hash failures, missing dependencies, portable names, 64-asset and cumulative-byte boundaries, overflow, cancellation during reads, immutable reports, C#/VB examples and failed preflights preserving existing file output. Read-only files run on all source lanes. Modern Unix runtimes also exercise read-only directories and root/file symbolic links; Windows privileged-link scenarios and actual minimum Framework are separate pending evidence. These tests do not execute or qualify native code.


## Optional isolated worker adapter

`InspectWorker`/`VerifyWorker` additionally accept the native OS RID, allowing a 32-bit caller to use an explicitly pinned 64-bit worker. Ordinary `Inspect`/`Verify` still require the current process RID. Reports continue to identify the caller process RID. `OperatingSystemRuntimeIdentifier` supplies the native OS choice without guessing or attempting execution.

`NativeWorkerOptions` names an exact pinned host executable and bridge library from the same deployment, a caller-owned local temporary parent, byte limits, a memory ceiling and a deadline. No engine or executable is discovered or downloaded. `NativeWorkerHost.Execute(options, request, context)` snapshots request bytes, charges expanded-byte allocation, copies and rehashes the complete inventory into a unique protected directory, clears inherited environment and starts the explicit host with redirected private pipes. Request data is sent over stdin, not the command line. Every response binds a fresh random 128-bit job nonce; sizes are checked before allocation. The caller receives owned bytes only after the complete frame, EOF and successful exit. The current bridge ABI is defined in [bridge.h](../native/worker/bridge.h).

The host reserves process memory only while the isolated job is alive. Its effective OS ceiling is the smaller of the configured ceiling and the operation's remaining expanded-byte budget after request/reply transport headroom; less than 16 MiB fails before launch. This reservation contributes to peak usage, is released on success or failure, and never refunds retained request/reply charges. Concurrent jobs must fit the combined operation budget. Codec object reservations cover their header/tile work estimate or encoded fax rows, rather than taking every remaining object unit.

The `Execute` overload with `maximumResponseBytes` reduces the native output allocation for requests with a known response bound. Image decoding supplies the exact sample length plus its reply header. Unknown-length encoders and generic bridges retain the caller's configured maximum because the bounded-buffer ABI requires capacity before execution. No executed operation is retried to grow a buffer. Staging verifies each asset's hash and binary header in its copy pass; the private bootstrap is checked immediately before each exec attempt.

The original [host source](../native/worker/worker.c) loads the absolute staged library only after sandbox installation, verifies the actual ABI and engine-version exports, and invokes the bounded buffer bridge. Its platform boundary is:

| Host | Boundary before engine loading |
| --- | --- |
| Windows 10+ x86/x64/ARM64 | Fresh AppContainer without network capabilities; read/execute-only stage grant; atomically assigned Job Object with process/commit/CPU limits, one process and kill-on-close; system32/staged DLL loading only |
| Linux x64/ARM64 | Landlock read-only asset/system-library allowlist, seccomp rejection of sockets/process creation/tracing/namespace changes, no-new-privileges, address-space/data/CPU/file-size limits and parent-death signal |
| macOS x64/ARM64 | Default-deny Seatbelt policy allowing staged/system-library reads, data/CPU/file-size limits, resident-memory supervision and a parent-exit watcher |

Kernel facilities are prerequisites. Windows requires enabled Firewall profiles for AppContainer network isolation; the bootstrap checks them and never changes host policy. An unavailable sandbox fails with `Native.SandboxUnavailable`; no unrestricted fallback is allowed. Linux kernels without Landlock cannot run this host. Native memory accounting differs by OS: Windows Job private commit, Linux address space/data, macOS virtual-map growth plus observed resident high water. These do not establish a universal physical-memory envelope; retained workload measurements and FP-088 remain required before production claims. No desktop, network capability or writable document path is granted to the engine.

Host supervision polls cancellation/deadlines while bounded transports run independently. A native crash, missing dependency/export, wrong ABI/version, malformed response, budget failure or elapsed deadline latches a fatal operation error. Failed file saves preserve existing destinations. Engine output is never connected directly to a persisted destination. Termination closes the owned process/Job and pipes, joins transport tasks and removes only the owned flat staging inventory; Windows also removes the unique AppContainer profile. Unexpected cleanup failures remain errors and identify the retained directory through exception data. No global caches or provider credentials are inherited.

Build the original executable and independent crash/error fixtures with `pwsh -File eng/build-native-fixtures.ps1`. The script builds native source for the current RID, plus Windows x86 on x64 hosts, and records exact binary digests in `artifacts/native-fixtures/native-fixtures.json`. The [behavioral suite](../tests/Fluent.Pdf.Contract.Tests/NativeWorkerTests.cs) exercises real loader/ABI/version failures, missing and complete transitive dependencies, native access faults, hangs, memory/output bounds, denied file/network access, atomic output preservation and recovery on the next independent job. C#/VB `ExecuteNativeWorker` consumers compile against the adapter on all TFMs. This adapter remains non-packable while the SDK is unqualified.


## Self-contained managed native bridges

The original NativeAOT fixture is a self-contained native library built from `tests/native-aot`. It exercises the same ABI/process boundary, including a managed thread attempting to read a caller file. Windows Framework x86 consumers use the explicitly pinned native OS worker; no .NET 10 runtime installation is required by that child. This fixture is test infrastructure, not an image codec or PDF engine.

Linux thread creation remains disabled by default. `AllowInProcessThreads` permits only clone flags for the same thread group; process/namespace creation remains denied. New threads inherit Landlock and seccomp. This mode requires a non-root UID without effective/permitted/inheritable capabilities and lowers the worker's inherited NPROC limit to at most 1,024. UID task accounting can deny creation if the host lacks headroom; other processes' limits are never changed. Windows/macOS retain their platform thread behavior. The configured memory/CPU/deadline envelope still applies; thread/resource/concurrency qualification remains mandatory.

In both Linux modes seccomp denies `setrlimit` and any `prlimit64` call that supplies a new limit. Only a query of the worker's own limits is allowed. A privileged non-threaded bridge therefore cannot raise its hard limits after sandbox installation.

Child .NET runtime settings are recomputed per job: a GC heap cap of one quarter of the process envelope, workstation/nonconcurrent GC, diagnostic IPC disabled and two reported processors. Caller settings are not inherited. NativeAOT code still runs only after sandbox installation.

`CaptureWorkerDiagnostics` is false by default. Explicit opt-in may retain at most 8 KiB of stderr in failure `Exception.Data["Native.WorkerDiagnostics"]`; engine output can contain sensitive content, so the caller controls that disclosure. It never changes default error messages. Loader details remain separately bounded metadata from loading before document input is read.

The Unix launcher retries only an `ETXTBSY` failure reported before exec starts, at most nine times with 10 ms waits. A concurrent trusted fork can briefly retain a close-on-exec staging write descriptor. Each attempt rechecks the private bootstrap against its pin, observes caller cancellation, and shares the native startup/execution deadline. Executed requests, protocol failures and other launch errors are never retried. Required source tests include concurrent staging/launch and a small worker running while its caller holds 128 MiB. Failure-only macOS diagnostics distinguish allocation, measurement and resident-ceiling failures; disclosure remains opt-in. Memory ceilings are unchanged.
