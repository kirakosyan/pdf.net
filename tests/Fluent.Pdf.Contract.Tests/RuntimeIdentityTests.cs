using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
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
        Assert.Equal(expected,typeof(Fluent.Pdf.Workers.NativeWorkerHost).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected,typeof(Fluent.Pdf.Codecs.IsolatedImageCodecProvider).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected,typeof(Fluent.Pdf.Fonts.ManagedFontProvider).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected,typeof(Fluent.Pdf.Text.IsolatedTextShaper).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected,typeof(Fluent.Pdf.Rendering.IsolatedPdfRenderer).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected,typeof(Fluent.Pdf.Cryptography.ManagedCmsEnvelopeProvider).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected,typeof(Fluent.Pdf.Compliance.VeraPdfReportValidator).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected,typeof(Fluent.Pdf.Html.ChromiumHtmlRenderer).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected,typeof(Fluent.Pdf.Word.WordnetPdfConverter).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(expected,typeof(Fluent.Pdf.Ocr.IsolatedTesseractOcr).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        var expectedArchitecture = Environment.GetEnvironmentVariable("FLUENT_PDF_EXPECTED_ARCH");
        if (!string.IsNullOrEmpty(expectedArchitecture))
            Assert.Equal(expectedArchitecture, RuntimeInformation.ProcessArchitecture.ToString(), ignoreCase: true);
        _output.WriteLine("Library: " + assembly.Location);
        _output.WriteLine("Library TFM: " + expected);
        _output.WriteLine("Actual runtime: " + RuntimeInformation.FrameworkDescription);
        _output.WriteLine("Actual OS: " + RuntimeInformation.OSDescription);
        _output.WriteLine("Process architecture: " + RuntimeInformation.ProcessArchitecture);
    }
}
