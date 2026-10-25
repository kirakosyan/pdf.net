using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Fluent.Pdf.Contract.Tests;
public sealed class RuntimeIdentityTests
{
    private readonly ITestOutputHelper _output;
    public RuntimeIdentityTests(ITestOutputHelper output) => _output = output;
    [Fact]
    public void TheRequestedLibraryAssetIsActuallyLoaded()
    {
#if TEST_NETSTANDARD_ASSEMBLY
        const string expected = ".NETStandard,Version=v2.0";
#elif NETFRAMEWORK
        const string expected = ".NETFramework,Version=v4.7.2";
#elif NET8_0
        const string expected = ".NETCoreApp,Version=v8.0";
#else
        const string expected = ".NETCoreApp,Version=v10.0";
#endif
        var assembly = typeof(PdfDocument).Assembly;
        Assert.Equal(expected, assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected, typeof(Fluent.Pdf.Workers.NativeWorkerHost).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected, typeof(Fluent.Pdf.Codecs.IsolatedImageCodecProvider).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected, typeof(Fluent.Pdf.Fonts.ManagedFontProvider).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected, typeof(Fluent.Pdf.Text.IsolatedTextShaper).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected, typeof(Fluent.Pdf.Rendering.IsolatedPdfRenderer).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected, typeof(Fluent.Pdf.Cryptography.ManagedCmsEnvelopeProvider).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected, typeof(Fluent.Pdf.Compliance.VeraPdfReportValidator).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected, typeof(Fluent.Pdf.Html.ChromiumHtmlRenderer).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected, typeof(Fluent.Pdf.Word.WordnetPdfConverter).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected, typeof(Fluent.Pdf.Ocr.IsolatedTesseractOcr).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected, typeof(Fluent.Pdf.Formats.PdfConversions).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected, typeof(Fluent.Pdf.Intelligence.PdfDocumentIntelligence).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected, typeof(Fluent.Pdf.Automation.PdfAutomation).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected, typeof(Fluent.Pdf.Applications.PdfApplicationViewer).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        var expectedArchitecture = Environment.GetEnvironmentVariable("FLUENT_PDF_EXPECTED_ARCH");
        if (!string.IsNullOrEmpty(expectedArchitecture))
            Assert.Equal(expectedArchitecture, RuntimeInformation.ProcessArchitecture.ToString(), ignoreCase: true);
        _output.WriteLine("Library: " + assembly.Location);
        _output.WriteLine("Library TFM: " + expected);
        _output.WriteLine("Actual runtime: " + RuntimeInformation.FrameworkDescription);
        _output.WriteLine("Actual OS: " + RuntimeInformation.OSDescription);
        _output.WriteLine("Process architecture: " + RuntimeInformation.ProcessArchitecture);
        var libraries = new[]
        {
            assembly,
            typeof(Fluent.Pdf.Workers.NativeWorkerHost).Assembly,
            typeof(Fluent.Pdf.Codecs.IsolatedImageCodecProvider).Assembly,
            typeof(Fluent.Pdf.Fonts.ManagedFontProvider).Assembly,
            typeof(Fluent.Pdf.Text.IsolatedTextShaper).Assembly,
            typeof(Fluent.Pdf.Rendering.IsolatedPdfRenderer).Assembly,
            typeof(Fluent.Pdf.Cryptography.ManagedCmsEnvelopeProvider).Assembly,
            typeof(Fluent.Pdf.Compliance.VeraPdfReportValidator).Assembly,
            typeof(Fluent.Pdf.Html.ChromiumHtmlRenderer).Assembly,
            typeof(Fluent.Pdf.Word.WordnetPdfConverter).Assembly,
            typeof(Fluent.Pdf.Ocr.IsolatedTesseractOcr).Assembly,
            typeof(Fluent.Pdf.Formats.PdfConversions).Assembly,
            typeof(Fluent.Pdf.Intelligence.PdfDocumentIntelligence).Assembly,
            typeof(Fluent.Pdf.Automation.PdfAutomation).Assembly,
            typeof(Fluent.Pdf.Applications.PdfApplicationViewer).Assembly
        };
        string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "unsupported";
        int? frameworkRelease = null;
#if NETFRAMEWORK
        if (os == "win")
#else
        if (OperatingSystem.IsWindows())
#endif
        {
            using var key = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? Microsoft.Win32.RegistryView.Registry64 : Microsoft.Win32.RegistryView.Registry32);
            using var full = key.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full", writable: false);
            if (full?.GetValue("Release")is int release)
                frameworkRelease = release;
        }

        var core = typeof(object).Assembly;
        _output.WriteLine("Runtime evidence: " + JsonSerializer.Serialize(new { schemaVersion = 1, purpose = "source-runtime-identity", osFamily = os, osDescription = RuntimeInformation.OSDescription, processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(), osArchitecture = RuntimeInformation.OSArchitecture.ToString(), runtime = RuntimeInformation.FrameworkDescription, runtimeVersion = Environment.Version.ToString(), rid = os + "-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(), frameworkRelease, coreLibrary = new { name = core.GetName().Name, fileVersion = FileVersionInfo.GetVersionInfo(core.Location).FileVersion, sha256 = Digest(core.Location) }, culture = CultureInfo.CurrentCulture.Name, uiCulture = CultureInfo.CurrentUICulture.Name, sdkAssets = libraries.Select(a => new { name = a.GetName().Name, targetFramework = a.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName, sha256 = Digest(a.Location), moduleVersionId = a.ManifestModule.ModuleVersionId.ToString("D") }).ToArray(), productionQualified = false }));
    }

    private static string Digest(string path)
    {
        using var input = File.OpenRead(path);
        using var algorithm = SHA256.Create();
        return string.Concat(algorithm.ComputeHash(input).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }
}
