using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Fluent.Pdf.Workers;

internal static class WorkerPlatform
{
    internal static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    internal static void ProtectDirectory(string path)
    {
        if (!IsWindows)
        {
            if (chmod(path,448) != 0) throw new IOException("The private worker directory could not be protected.");
            return;
        }
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW("D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;OW)",1,out var descriptor,out _))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            if (!SetFileSecurityW(path,0x80000004,descriptor)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { LocalFree(descriptor); }
    }

    internal static void MakeExecutable(string path)
    {
        if (!IsWindows && chmod(path,320) != 0) throw new IOException("The pinned worker could not be made executable.");
    }

    internal static void Terminate(Process process)
    {
        try { process.Kill(); }
        catch (InvalidOperationException) when (process.HasExited) { }
    }

    internal static void DeleteProfile(string profile)
    {
        // Only the unique profile owned by this job is eligible. The bootstrap
        // ordinarily removes it; this also handles parent cancellation/crashes.
        int result=DeleteAppContainerProfile(profile);
        if (result<0 && result!=unchecked((int)0x80070002) && result!=unchecked((int)0x80070490))
            throw new IOException("The native job AppContainer profile could not be removed.");
    }

    [DllImport("libc",SetLastError=true)] private static extern int chmod(string path,uint mode);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string text,uint revision,out IntPtr descriptor,out uint size);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileSecurityW(string path,uint information,IntPtr descriptor);
    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("userenv.dll",CharSet=CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DeleteAppContainerProfile(string name);
}
