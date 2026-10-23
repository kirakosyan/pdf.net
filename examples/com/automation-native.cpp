#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <oleauto.h>
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>

// Original version-one ABI declaration; method order matches the frozen dual interface.
struct __declspec(uuid("CE62BFB2-FB23-4BC6-A6CD-7075ED70268C")) IPdfAutomation : IDispatch {
    virtual HRESULT STDMETHODCALLTYPE Configure(LONG, LONG, LONG, LONG, LONG) = 0;
    virtual HRESULT STDMETHODCALLTYPE OpenBytes(SAFEARRAY*) = 0;
    virtual HRESULT STDMETHODCALLTYPE OpenFile(BSTR) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_PageCount(LONG*) = 0;
    virtual HRESULT STDMETHODCALLTYPE ExtractText(LONG, LONG, VARIANT_BOOL, LONG, BSTR*) = 0;
    virtual HRESULT STDMETHODCALLTYPE SelectPages(BSTR, VARIANT_BOOL) = 0;
    virtual HRESULT STDMETHODCALLTYPE SaveBytes(VARIANT_BOOL, SAFEARRAY**) = 0;
    virtual HRESULT STDMETHODCALLTYPE SaveFile(BSTR, VARIANT_BOOL) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetFieldNames(SAFEARRAY**) = 0;
    virtual HRESULT STDMETHODCALLTYPE Cancel() = 0;
    virtual HRESULT STDMETHODCALLTYPE Close() = 0;
    virtual HRESULT STDMETHODCALLTYPE get_LastDiagnosticCode(BSTR*) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_RuntimeIdentity(BSTR*) = 0;
};
template<class T> struct OwnedCom { T* value = nullptr; ~OwnedCom() { if (value) value->Release(); } };
struct OwnedBstr { BSTR value; explicit OwnedBstr(const wchar_t* text = nullptr) : value(text ? SysAllocString(text) : nullptr) {} ~OwnedBstr() { SysFreeString(value); } };
struct OwnedArray { SAFEARRAY* value = nullptr; ~OwnedArray() { if (value) SafeArrayDestroy(value); } };
static const wchar_t* phase = L"initialize";
static void check(bool value) { if (!value) throw std::runtime_error("COM contract check failed"); }
static void ok(HRESULT value) { if (FAILED(value)) std::wcerr << L"COM_HRESULT;phase=" << phase << L";value=" << std::hex << static_cast<unsigned long>(value) << L"\n"; check(SUCCEEDED(value)); }
static std::vector<BYTE> read(const wchar_t* path) {
    HANDLE file = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    check(file != INVALID_HANDLE_VALUE);
    LARGE_INTEGER size{}; BOOL sized = GetFileSizeEx(file, &size);
    if (!sized || size.QuadPart < 1 || size.QuadPart > 1048576) { CloseHandle(file); check(false); }
    std::vector<BYTE> bytes(static_cast<size_t>(size.QuadPart)); DWORD count = 0;
    BOOL loaded = ReadFile(file, bytes.data(), static_cast<DWORD>(bytes.size()), &count, nullptr); CloseHandle(file);
    check(loaded && count == bytes.size()); return bytes;
}
static void write(const wchar_t* path, SAFEARRAY* array) {
    VARTYPE type = VT_EMPTY; ok(SafeArrayGetVartype(array, &type)); check(type == VT_UI1 && SafeArrayGetDim(array) == 1);
    LONG first = 0, last = 0; ok(SafeArrayGetLBound(array, 1, &first)); ok(SafeArrayGetUBound(array, 1, &last)); check(first == 0 && last >= 0 && last < 1048576);
    BYTE* data = nullptr; ok(SafeArrayAccessData(array, reinterpret_cast<void**>(&data)));
    HANDLE file = CreateFileW(path, GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE) { SafeArrayUnaccessData(array); check(false); }
    DWORD count = 0; BOOL saved = WriteFile(file, data, static_cast<DWORD>(last + 1), &count, nullptr); CloseHandle(file); SafeArrayUnaccessData(array);
    check(saved && count == static_cast<DWORD>(last + 1));
}
int wmain(int argc, wchar_t** argv) {
    if (argc != 3 && !(argc == 2 && std::wstring(argv[1]) == L"--expect-unregistered")) return 2;
    HRESULT initialized = CoInitializeEx(nullptr, COINIT_MULTITHREADED); if (FAILED(initialized)) return 3;
    int result = 0;
    try {
        CLSID clsid{}; ok(CLSIDFromString(L"{20F743F7-79B0-4E47-8F20-B0588A691025}", &clsid));
        OwnedCom<IPdfAutomation> pdf;
        phase = L"activation";
        HRESULT activation = CoCreateInstance(clsid, nullptr, CLSCTX_INPROC_SERVER, __uuidof(IPdfAutomation), reinterpret_cast<void**>(&pdf.value));
        if (argc == 2 && std::wstring(argv[1]) == L"--expect-unregistered") { check(activation == REGDB_E_CLASSNOTREG); std::wcout << L"COM_UNREGISTERED_PASS\n"; }
        else {
            ok(activation); phase = L"configure"; ok(pdf.value->Configure(1048576, 16777216, 1048576, 100000, 30000));
            auto bytes = read(argv[1]); OwnedArray input; input.value = SafeArrayCreateVector(VT_UI1, 0, static_cast<ULONG>(bytes.size())); check(input.value != nullptr);
            void* memory = nullptr; ok(SafeArrayAccessData(input.value, &memory)); CopyMemory(memory, bytes.data(), bytes.size()); ok(SafeArrayUnaccessData(input.value));
            phase = L"open-bytes"; ok(pdf.value->OpenBytes(input.value)); LONG pages = 0; ok(pdf.value->get_PageCount(&pages)); check(pages == 2);
            phase = L"extract-text"; OwnedBstr text; ok(pdf.value->ExtractText(0, 0, VARIANT_FALSE, 1000, &text.value)); check(std::wstring(text.value) == L"Original automation \u03A9");
            phase = L"field-array"; OwnedArray fields; ok(pdf.value->GetFieldNames(&fields.value)); VARTYPE type = VT_EMPTY; ok(SafeArrayGetVartype(fields.value, &type)); check(type == VT_VARIANT);
            LONG index = 0; VARIANT field{}; VariantInit(&field); ok(SafeArrayGetElement(fields.value, &index, &field)); check(field.vt == VT_BSTR && std::wstring(field.bstrVal) == L"customer.Name"); VariantClear(&field);
            // Exercise the same object through IDispatch as well as its native dual vtable.
            phase = L"IDispatch"; LPOLESTR name = const_cast<LPOLESTR>(L"PageCount"); DISPID id = DISPID_UNKNOWN; ok(pdf.value->GetIDsOfNames(IID_NULL, &name, 1, LOCALE_INVARIANT, &id)); check(id == 4);
            DISPPARAMS args{}; VARIANT value{}; VariantInit(&value); ok(pdf.value->Invoke(id, IID_NULL, LOCALE_INVARIANT, DISPATCH_PROPERTYGET, &args, &value, nullptr, nullptr)); check(value.vt == VT_I4 && value.lVal == 2); VariantClear(&value);
            phase = L"selection"; OwnedBstr selection(L"2,1"); ok(pdf.value->SelectPages(selection.value, VARIANT_FALSE));
            phase = L"save-array"; OwnedArray output; ok(pdf.value->SaveBytes(VARIANT_FALSE, &output.value)); write(argv[2], output.value);
            OwnedBstr identity; ok(pdf.value->get_RuntimeIdentity(&identity.value));
            phase = L"close-error-info"; ok(pdf.value->Close()); HRESULT missing = pdf.value->get_PageCount(&pages); check(missing == static_cast<HRESULT>(0x8004A104));
            OwnedCom<IErrorInfo> error; ok(GetErrorInfo(0, &error.value)); check(error.value != nullptr); OwnedBstr description; ok(error.value->GetDescription(&description.value)); check(std::wstring(description.value) == L"Automation.NoDocument");
            std::wcout << L"COM_NATIVE_PASS;bits=" << sizeof(void*) * 8 << L";" << identity.value << L"\n";
        }
    } catch (const std::exception&) { std::wcerr << L"COM_NATIVE_FAILED;phase=" << phase << L"\n"; result = 1; }
    CoUninitialize(); return result;
}
