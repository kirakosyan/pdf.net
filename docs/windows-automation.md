# Explicit Windows automation

The separate `Fluent.Pdf.Automation` assembly exposes the original `IPdfAutomation` version-one dual interface and `PdfAutomation` class. The managed contract compiles for all four SDK targets; classic in-process COM activation in this profile uses the **net472 asset on64-bit Windows with x86/x64 Framework clients**. CoreCLR COM hosting,32-bit Windows hosts, complete apartment/proxy/type-library distribution and production qualification remain mandatory additional work. The constructor rejects other operating systems. No COM/platform type enters Core's public API, and no engine, registry entry, credential or binary is discovered or downloaded during SDK operations.

| Identity | Fixed value |
| --- | --- |
| ProgID | `Fluent.Pdf.Automation.1` |
| Class CLSID | `20F743F7-79B0-4E47-8F20-B0588A691025` |
| Interface IID | `CE62BFB2-FB23-4BC6-A6CD-7075ED70268C` |
| Reserved type-library identity | `A7FCF3DB-9F8A-4452-B479-603E49722F78`, version1.0 |

Interface method order, IDs1–13 and parameter marshaling are frozen. Future incompatible contracts need a new interface identity. `ClassInterfaceType.None` prevents an automatically changing class interface; the explicit default interface carries BSTR strings,32-bit integers, VARIANT_BOOL booleans, SAFEARRAY(UI1) PDF bytes and SAFEARRAY(VARIANT) field-name arrays containing only BSTR strings. No Core document, exception, stream, provider, key or framework-specific model crosses that interface. The native sample supplies an original matching vtable declaration and exercises both direct dual-interface calls and IDispatch. This profile does not install a generated type library or an external-apartment proxy.

Each instance owns one private document. `Configure` establishes strict finite input/expansion/output/object/deadline ceilings before opening a document. Input/output are limited to16MiB each, expansion to256MiB, objects to1million and deadline to300000ms; defaults are16MiB/128MiB/16MiB/250000objects/30000ms. `OpenBytes` captures a caller array without mutating it. `OpenFile` opens only the explicit client path and closes its owned handle before returning. A failed precommit capture preserves the previous document. `Close` releases the captured document and allows reuse/configuration. Ordinary concurrent calls reject; separate instances isolate state. `Cancel` requests cooperative cancellation of the current call. Complete actual apartment/concurrency/deadline qualification remains open; it is not hard preemption of CLR marshaling or arbitrary host work.

`PageCount` reads the owned model. `ExtractText` uses zero-based pages and explicit raw0/semantic1/layout2 views, with separate nonpainting-text inclusion and finite returned UTF16 characters. Mapping/geometry does not establish visible ink. `SelectPages` accepts the existing one-based bounded range syntax, including explicit order/duplicates, and swaps the complete candidate only after successful shared-context import. `SaveBytes` returns an owned bounded byte array; `SaveFile` uses the existing atomic file-save boundary. Signature invalidation authority is explicit. `GetFieldNames` passively inspects at most4096 hierarchical AcroForm names, without executing actions/XFA. Unsupported source profiles reject. Encrypted input needs an explicitly authenticated workflow outside this facade; no password, certificate, legacy cipher or silent decryption provider is selected here. Complete encrypted/form-editing/appearance/accessibility and broader SDK COM profiles remain required.

Each parse/extract/import/save operation uses one shared Core context; no nested public operation resets its counters. Returned byte/BSTR/field-array allocations have separate finite accounting before managed output construction. CLR/COM may allocate incoming SAFEARRAY/BSTR marshaling storage before the managed entry point can reject an oversized request; these quotas do not claim to bound that pre-entry native allocation. Hosts must isolate untrusted automation clients appropriately. Core stream ownership and atomic-save policies remain in effect.

Failures return stable HRESULTs with IErrorInfo containing only the diagnostic code. User file paths, document payloads, credentials and underlying exception text are omitted. `LastDiagnosticCode` exposes the last completed call's code; the invoking client's HRESULT/IErrorInfo is authoritative when calls overlap. Runtime identity reports CLR, process architecture and assembly version without document data.

| HRESULT | Meaning |
| --- | --- |
| `8004A100` | Unsupported platform |
| `80070057` | Invalid argument |
| `8004A103` | Concurrent call or configuration locked by an open document |
| `8004A104` | No open document |
| `8004A105` | Disposed facade |
| `8004A106` | Resource/deadline limit |
| `8004A107` | Cooperative cancellation |
| `8004A108` | SDK document/unsupported-content diagnostic |
| `8004A109` | Explicit file I/O failure |
| `8004A10A` | Other redacted adapter failure |

Deploy the complete pinned net472 output and dependencies to an explicit private directory. `eng/register-automation.ps1 -Action Register -AssemblyPath <absolute-net472-dll> -Architecture X86` registers the selected registry view under CurrentUser by default. Repeat with X64 for the same AnyCPU asset. Use `-Action Unregister` with the identical path/view to remove it. `-Scope LocalMachine` is a separate explicit administrative choice; the disposable CI runner uses it because elevated Windows clients may ignore per-user COM registration. The helper checks the real net472 target using a short-lived Framework reflection-only process. It does not execute the inspected assembly or leave it locked in the caller. It writes only its owned CLSID/ProgID trees, refuses foreign owner/codebase conflicts, rolls back newly created trees on registration failure, supports idempotent registration and preserves a shared ProgID while the other bitness remains installed. Registration operations are administrative maintenance and must be serialized by the host. No global assembly-cache entry, DCOM server or automatic registration is installed.

The VBScript and original native C++ samples in `examples/com` exercise explicit bytes/files, Unicode BSTRs, field-name arrays, reordered PDF output, IDispatch/vtable calls and stable errors. `eng/test-automation.ps1` compiles actual x86/x64 clients on Windows, registers both views side by side, runs all four consumers, verifies the remaining bitness after one view is removed, removes owned registrations in cleanup and checks fresh-process rejection after removal. Only a complete passing run writes its aggregate manifest. A separately pinned, hash-checked independent PDF reader compares all four actual outputs. Its analysis wheel is not an SDK runtime dependency or distributed asset.

Portable implementation tests, an original deterministic PDF generator, direct PE FieldMarshal/vtable-order checks and independent mapped-text/page-order observations are separate evidence. Managed reflection can omit SAFEARRAY subtype values, so reflection success is not marshaling proof. Actual minimum Framework4.7.2, broader Windows/apartment/host/malformed/concurrency/security/endurance coverage, type-library/installer servicing and exact final-package consumer qualification remain mandatory. `docs/evidence/FP-081-source.json` records exact source/runtime/actual-host observations when available; no source result closes FP-081 or permits publication.
