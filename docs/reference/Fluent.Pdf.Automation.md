# Fluent.Pdf.Automation source reference

Version `1.0.0`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).

## Public declarations

The baseline preserves exact nullability, parameter names, defaults and constant values.

```text
Fluent.Pdf.Automation.IPdfAutomation
Fluent.Pdf.Automation.IPdfAutomation.Cancel() -> void
Fluent.Pdf.Automation.IPdfAutomation.Close() -> void
Fluent.Pdf.Automation.IPdfAutomation.Configure(int maximumInputBytes, int maximumExpandedBytes, int maximumOutputBytes, int maximumObjects, int timeoutMilliseconds) -> void
Fluent.Pdf.Automation.IPdfAutomation.ExtractText(int pageIndex, int view, bool includeInvisibleText, int maximumCharacters) -> string!
Fluent.Pdf.Automation.IPdfAutomation.GetFieldNames() -> object![]!
Fluent.Pdf.Automation.IPdfAutomation.LastDiagnosticCode.get -> string!
Fluent.Pdf.Automation.IPdfAutomation.OpenBytes(object! document) -> void
Fluent.Pdf.Automation.IPdfAutomation.OpenFile(string! path) -> void
Fluent.Pdf.Automation.IPdfAutomation.PageCount.get -> int
Fluent.Pdf.Automation.IPdfAutomation.RuntimeIdentity.get -> string!
Fluent.Pdf.Automation.IPdfAutomation.SaveBytes(bool allowSignatureInvalidation) -> byte[]!
Fluent.Pdf.Automation.IPdfAutomation.SaveFile(string! path, bool allowSignatureInvalidation) -> void
Fluent.Pdf.Automation.IPdfAutomation.SelectPages(string! expression, bool allowSignatureInvalidation) -> void
Fluent.Pdf.Automation.PdfAutomation
Fluent.Pdf.Automation.PdfAutomation.Cancel() -> void
Fluent.Pdf.Automation.PdfAutomation.Close() -> void
Fluent.Pdf.Automation.PdfAutomation.Configure(int maximumInputBytes, int maximumExpandedBytes, int maximumOutputBytes, int maximumObjects, int timeoutMilliseconds) -> void
Fluent.Pdf.Automation.PdfAutomation.Dispose() -> void
Fluent.Pdf.Automation.PdfAutomation.ExtractText(int pageIndex, int view, bool includeInvisibleText, int maximumCharacters) -> string!
Fluent.Pdf.Automation.PdfAutomation.GetFieldNames() -> object![]!
Fluent.Pdf.Automation.PdfAutomation.LastDiagnosticCode.get -> string!
Fluent.Pdf.Automation.PdfAutomation.OpenBytes(object! document) -> void
Fluent.Pdf.Automation.PdfAutomation.OpenFile(string! path) -> void
Fluent.Pdf.Automation.PdfAutomation.PageCount.get -> int
Fluent.Pdf.Automation.PdfAutomation.PdfAutomation() -> void
Fluent.Pdf.Automation.PdfAutomation.RuntimeIdentity.get -> string!
Fluent.Pdf.Automation.PdfAutomation.SaveBytes(bool allowSignatureInvalidation) -> byte[]!
Fluent.Pdf.Automation.PdfAutomation.SaveFile(string! path, bool allowSignatureInvalidation) -> void
Fluent.Pdf.Automation.PdfAutomation.SelectPages(string! expression, bool allowSignatureInvalidation) -> void
```

## Member documentation

### <code>M:Fluent.Pdf.Automation.IPdfAutomation.Cancel</code>

Requests cooperative cancellation of the active call. Concurrent ordinary calls reject; timeout/deadline remains finite.

### <code>M:Fluent.Pdf.Automation.IPdfAutomation.Close</code>

Closes the private document. The facade remains reusable.

### <code>M:Fluent.Pdf.Automation.IPdfAutomation.Configure(System.Int32,System.Int32,System.Int32,System.Int32,System.Int32)</code>

Configures finite strict budgets only while no document is open. Input/output each1..16MiB, expansion1..256MiB, objects1..1million, timeout1..300000ms.

### <code>M:Fluent.Pdf.Automation.IPdfAutomation.ExtractText(System.Int32,System.Int32,System.Boolean,System.Int32)</code>

Extracts raw0, semantic1 or layout2 mapped text. Nonpainting inclusion is explicit; the BSTR output has a finite UTF16 ceiling.

### <code>M:Fluent.Pdf.Automation.IPdfAutomation.GetFieldNames</code>

Passively returns at most4096 fully qualified field names as SAFEARRAY(VARIANT) containing BSTR values, without evaluating actions or XFA.

### <code>M:Fluent.Pdf.Automation.IPdfAutomation.OpenBytes(System.Object)</code>

Captures a VARIANT containing SAFEARRAY(UI1) into a private document. Failure preserves the previously open document.

### <code>M:Fluent.Pdf.Automation.IPdfAutomation.OpenFile(System.String)</code>

Explicitly captures one caller-selected file; the owned file handle closes before return. No document-directed path is opened.

### <code>M:Fluent.Pdf.Automation.IPdfAutomation.SaveBytes(System.Boolean)</code>

Returns an owned SAFEARRAY(UI1) after a bounded full write; no stream or Core type crosses COM.

### <code>M:Fluent.Pdf.Automation.IPdfAutomation.SaveFile(System.String,System.Boolean)</code>

Writes a caller-selected path atomically under the same finite policy.

### <code>M:Fluent.Pdf.Automation.IPdfAutomation.SelectPages(System.String,System.Boolean)</code>

Atomically replaces the private document with selected/reordered pages. Complete signature invalidation authority is explicit.

### <code>M:Fluent.Pdf.Automation.PdfAutomation.#ctor</code>

Creates a private strict session on Windows; other systems reject with stable platform HRESULT.

### <code>M:Fluent.Pdf.Automation.PdfAutomation.Cancel</code>

### <code>M:Fluent.Pdf.Automation.PdfAutomation.Close</code>

### <code>M:Fluent.Pdf.Automation.PdfAutomation.Configure(System.Int32,System.Int32,System.Int32,System.Int32,System.Int32)</code>

### <code>M:Fluent.Pdf.Automation.PdfAutomation.Dispose</code>

Closes owned managed state; repeated disposal is harmless. Dispose is not an additional COM interface member.

### <code>M:Fluent.Pdf.Automation.PdfAutomation.ExtractText(System.Int32,System.Int32,System.Boolean,System.Int32)</code>

### <code>M:Fluent.Pdf.Automation.PdfAutomation.GetFieldNames</code>

### <code>M:Fluent.Pdf.Automation.PdfAutomation.OpenBytes(System.Object)</code>

### <code>M:Fluent.Pdf.Automation.PdfAutomation.OpenFile(System.String)</code>

### <code>M:Fluent.Pdf.Automation.PdfAutomation.SaveBytes(System.Boolean)</code>

### <code>M:Fluent.Pdf.Automation.PdfAutomation.SaveFile(System.String,System.Boolean)</code>

### <code>M:Fluent.Pdf.Automation.PdfAutomation.SelectPages(System.String,System.Boolean)</code>

### <code>P:Fluent.Pdf.Automation.IPdfAutomation.LastDiagnosticCode</code>

Gets the last completed call's stable diagnostic code, without payloads or credential/path details.

### <code>P:Fluent.Pdf.Automation.IPdfAutomation.PageCount</code>

Gets current captured page count; absent/disposed sessions fail.

### <code>P:Fluent.Pdf.Automation.IPdfAutomation.RuntimeIdentity</code>

Gets nonsecret CLR/process/assembly identity for actual bitness/runtime evidence.

### <code>P:Fluent.Pdf.Automation.PdfAutomation.LastDiagnosticCode</code>

### <code>P:Fluent.Pdf.Automation.PdfAutomation.PageCount</code>

### <code>P:Fluent.Pdf.Automation.PdfAutomation.RuntimeIdentity</code>

### <code>T:Fluent.Pdf.Automation.IPdfAutomation</code>

Frozen version-one Windows automation contract. HRESULT/IErrorInfo report stable codes without document payloads or paths. Page indices are zero-based; selection expressions are one-based.

### <code>T:Fluent.Pdf.Automation.PdfAutomation</code>

Windows-only COM class. Activate only the net472 asset through explicitly registered x86/x64 in-process clients. Other assets preserve the managed contract without supplying CoreCLR COM hosting.

