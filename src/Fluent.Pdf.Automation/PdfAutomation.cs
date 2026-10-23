using System.Runtime.InteropServices;

namespace Fluent.Pdf.Automation;
/// <summary>Frozen version-one Windows automation contract. HRESULT/IErrorInfo report stable codes without document payloads or paths. Page indices are zero-based; selection expressions are one-based.</summary>
[ComVisible(true), Guid("CE62BFB2-FB23-4BC6-A6CD-7075ED70268C"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IPdfAutomation
{
    /// <summary>Configures finite strict budgets only while no document is open. Input/output each1..16MiB, expansion1..256MiB, objects1..1million, timeout1..300000ms.</summary>
    [DispId(1)]
    void Configure(int maximumInputBytes, int maximumExpandedBytes, int maximumOutputBytes, int maximumObjects, int timeoutMilliseconds);
    /// <summary>Captures a SAFEARRAY(UI1) into a private document. Failure preserves the previously open document.</summary>
    [DispId(2)]
    void OpenBytes([In, MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_UI1)] byte[] document);
    /// <summary>Explicitly captures one caller-selected file; the owned file handle closes before return. No document-directed path is opened.</summary>
    [DispId(3)]
    void OpenFile([MarshalAs(UnmanagedType.BStr)] string path);
    /// <summary>Gets current captured page count; absent/disposed sessions fail.</summary>
    [DispId(4)]
    int PageCount { get; }

    /// <summary>Extracts raw0, semantic1 or layout2 mapped text. Nonpainting inclusion is explicit; the BSTR output has a finite UTF16 ceiling.</summary>
    [DispId(5)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string ExtractText(int pageIndex, int view, [MarshalAs(UnmanagedType.VariantBool)] bool includeInvisibleText, int maximumCharacters);
    /// <summary>Atomically replaces the private document with selected/reordered pages. Complete signature invalidation authority is explicit.</summary>
    [DispId(6)]
    void SelectPages([MarshalAs(UnmanagedType.BStr)] string expression, [MarshalAs(UnmanagedType.VariantBool)] bool allowSignatureInvalidation);
    /// <summary>Returns an owned SAFEARRAY(UI1) after a bounded full write; no stream or Core type crosses COM.</summary>
    [DispId(7)]
    [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_UI1)]
    byte[] SaveBytes([MarshalAs(UnmanagedType.VariantBool)] bool allowSignatureInvalidation);
    /// <summary>Writes a caller-selected path atomically under the same finite policy.</summary>
    [DispId(8)]
    void SaveFile([MarshalAs(UnmanagedType.BStr)] string path, [MarshalAs(UnmanagedType.VariantBool)] bool allowSignatureInvalidation);
    /// <summary>Passively returns at most4096 fully qualified field names as SAFEARRAY(VARIANT) containing BSTR values, without evaluating actions or XFA.</summary>
    [DispId(9)]
    [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_VARIANT)]
    object[] GetFieldNames();
    /// <summary>Requests cooperative cancellation of the active call. Concurrent ordinary calls reject; timeout/deadline remains finite.</summary>
    [DispId(10)]
    void Cancel();
    /// <summary>Closes the private document. The facade remains reusable.</summary>
    [DispId(11)]
    void Close();
    /// <summary>Gets the last completed call's stable diagnostic code, without payloads or credential/path details.</summary>
    [DispId(12)]
    string LastDiagnosticCode {[return: MarshalAs(UnmanagedType.BStr)]
        get; }

    /// <summary>Gets nonsecret CLR/process/assembly identity for actual bitness/runtime evidence.</summary>
    [DispId(13)]
    string RuntimeIdentity {[return: MarshalAs(UnmanagedType.BStr)]
        get; }
}

/// <summary>Windows-only COM class. Activate only the net472 asset through explicitly registered x86/x64 in-process clients. Other assets preserve the managed contract without supplying CoreCLR COM hosting.</summary>
[ComVisible(true), Guid("20F743F7-79B0-4E47-8F20-B0588A691025"), ProgId("Fluent.Pdf.Automation.1"), ClassInterface(ClassInterfaceType.None), ComDefaultInterface(typeof(IPdfAutomation))]
public sealed class PdfAutomation : IPdfAutomation, IDisposable
{
    private readonly PdfAutomationSession _session;
    /// <summary>Creates a private strict session on Windows; other systems reject with stable platform HRESULT.</summary>
    public PdfAutomation()
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            throw new COMException("Automation.Platform", unchecked((int)0x8004A100));
        _session = new();
    }

    /// <inheritdoc/>
    public void Configure(int maximumInputBytes, int maximumExpandedBytes, int maximumOutputBytes, int maximumObjects, int timeoutMilliseconds) => _session.Configure(maximumInputBytes, maximumExpandedBytes, maximumOutputBytes, maximumObjects, timeoutMilliseconds);
    /// <inheritdoc/>
    public void OpenBytes(byte[] document) => _session.OpenBytes(document);
    /// <inheritdoc/>
    public void OpenFile(string path) => _session.OpenFile(path);
    /// <inheritdoc/>
    public int PageCount => _session.PageCount;

    /// <inheritdoc/>
    public string ExtractText(int pageIndex, int view, bool includeInvisibleText, int maximumCharacters) => _session.ExtractText(pageIndex, view, includeInvisibleText, maximumCharacters);
    /// <inheritdoc/>
    public void SelectPages(string expression, bool allowSignatureInvalidation) => _session.SelectPages(expression, allowSignatureInvalidation);
    /// <inheritdoc/>
    public byte[] SaveBytes(bool allowSignatureInvalidation) => _session.SaveBytes(allowSignatureInvalidation);
    /// <inheritdoc/>
    public void SaveFile(string path, bool allowSignatureInvalidation) => _session.SaveFile(path, allowSignatureInvalidation);
    /// <inheritdoc/>
    public object[] GetFieldNames() => _session.GetFieldNames();
    /// <inheritdoc/>
    public void Cancel() => _session.Cancel();
    /// <inheritdoc/>
    public void Close() => _session.Close();
    /// <inheritdoc/>
    public string LastDiagnosticCode => _session.LastDiagnosticCode;
    /// <inheritdoc/>
    public string RuntimeIdentity => _session.RuntimeIdentity;

    /// <summary>Closes owned managed state; repeated disposal is harmless. Dispose is not an additional COM interface member.</summary>
    [ComVisible(false)]
    public void Dispose() => _session.Dispose();
}
