using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Fluent.Pdf.Automation;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class AutomationTests
{
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Automation", "original.pdf");

    [Fact]
    public void ComIdentityDispatchOrderAndMarshalingAreExplicitAndFrozen()
    {
        Assert.Equal(new Guid("CE62BFB2-FB23-4BC6-A6CD-7075ED70268C"), typeof(IPdfAutomation).GUID);
        Assert.Equal(new Guid("20F743F7-79B0-4E47-8F20-B0588A691025"), typeof(PdfAutomation).GUID);
        Assert.Equal("Fluent.Pdf.Automation.1", typeof(PdfAutomation).GetCustomAttribute<ProgIdAttribute>()!.Value);
        Assert.Equal(ClassInterfaceType.None, typeof(PdfAutomation).GetCustomAttribute<ClassInterfaceAttribute>()!.Value);
        Assert.Equal(ComInterfaceType.InterfaceIsDual, typeof(IPdfAutomation).GetCustomAttribute<InterfaceTypeAttribute>()!.Value);
        Assert.False(typeof(PdfAutomation).Assembly.GetCustomAttribute<ComVisibleAttribute>()!.Value);
        var names = new[]
        {
            "Configure",
            "OpenBytes",
            "OpenFile",
            "PageCount",
            "ExtractText",
            "SelectPages",
            "SaveBytes",
            "SaveFile",
            "GetFieldNames",
            "Cancel",
            "Close",
            "LastDiagnosticCode",
            "RuntimeIdentity"
        };
        for (int i = 0; i < names.Length; i++)
            Assert.Equal(i + 1, typeof(IPdfAutomation).GetMember(names[i]).Single().GetCustomAttribute<DispIdAttribute>()!.Value);
        var input = typeof(IPdfAutomation).GetMethod("OpenBytes")!.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()!;
        Assert.Equal(UnmanagedType.SafeArray, input.Value);
        Assert.Contains(input.SafeArraySubType, new[] { VarEnum.VT_EMPTY, VarEnum.VT_UI1 });
        Assert.Equal(UnmanagedType.SafeArray, typeof(IPdfAutomation).GetMethod("SaveBytes")!.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()!.Value);
        Assert.Equal(UnmanagedType.SafeArray, typeof(IPdfAutomation).GetMethod("GetFieldNames")!.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()!.Value);
        // Some runtimes omit SAFEARRAY subtypes from reflection; the independent PE metadata checker and actual native consumer verify them.
        Assert.All(typeof(IPdfAutomation).GetMethods(), method => Assert.All(method.GetParameters(), parameter => Assert.False(parameter.ParameterType.Assembly == typeof(PdfDocument).Assembly)));
    }

    [Fact]
    public void WindowsClassGuardsActivationWhilePortableSessionExecutesTheSameImplementation()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using var facade = new PdfAutomation();
            Assert.Contains(RuntimeInformation.ProcessArchitecture.ToString(), facade.RuntimeIdentity);
        }
        else
            Assert.Equal(unchecked((int)0x8004A100), Assert.Throws<COMException>(() => new PdfAutomation()).HResult);
        using var session = new PdfAutomationSession();
        session.OpenBytes(File.ReadAllBytes(Fixture));
        Assert.Equal(2, session.PageCount);
        Assert.Equal("Original automation Ω", session.ExtractText(0, 0, false, 100));
        Assert.Equal(new[] { "customer.Name" }, session.GetFieldNames());
    }

    [Fact]
    public void OwnedInputCaptureAndAtomicSelectionSaveRoundTripDoNotMutateTheCallerArray()
    {
        byte[] bytes = File.ReadAllBytes(Fixture), original = (byte[])bytes.Clone();
        using var session = new PdfAutomationSession();
        session.OpenBytes(bytes);
        Array.Clear(bytes, 0, bytes.Length);
        Assert.Equal("Original automation Ω", session.ExtractText(0, 0, false, 100));
        session.SelectPages("2,1", false);
        Assert.Equal(2, session.PageCount);
        Assert.Equal("Second automation page", session.ExtractText(0, 0, false, 100));
        var saved = session.SaveBytes(false);
        using var input = new MemoryStream(saved);
        using var result = Pdf.Open(input);
        Assert.Equal(2, result.Pages.Count);
        Assert.Equal("Second automation page", PdfTextExtraction.ReadPage(result.CreateSnapshot(), 0).RawText);
        string directory = Path.Combine(AppContext.BaseDirectory, "automation-fixtures");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "reordered.pdf"), saved);
        Assert.Equal(original, File.ReadAllBytes(Fixture));
        Assert.Equal(unchecked((int)0x8004A108), Assert.Throws<COMException>(() => session.SelectPages("999", false)).HResult);
        Assert.Equal(2, session.PageCount);
    }

    [Fact]
    public void StableErrorCodesDoNotExposePathsOrDocumentPayloadsAndFailedOpenPreservesState()
    {
        using var session = new PdfAutomationSession();
        Assert.Equal("Automation.NoDocument", Assert.Throws<COMException>(() => session.PageCount).Message);
        session.OpenFile(Fixture);
        Assert.Equal("Automation.InvalidArgument", Assert.Throws<COMException>(() => session.ExtractText(-1, 0, false, 100)).Message);
        var error = Assert.Throws<COMException>(() => session.OpenFile(Path.Combine(Path.GetTempPath(), "PRIVATE-CREDENTIAL-PATH", Guid.NewGuid().ToString("N"))));
        Assert.Equal("Automation.IO", error.Message);
        Assert.DoesNotContain("PRIVATE", error.ToString());
        Assert.Equal(2, session.PageCount);
        Assert.Throws<COMException>(() => session.OpenBytes(new byte[] { 1, 2, 3 }));
        Assert.Equal(2, session.PageCount);
        Assert.Equal("Automation.ConfigurationLocked", Assert.Throws<COMException>(() => session.Configure(1, 1, 1, 1, 1)).Message);
        session.Close();
        session.Configure(100, 1000, 100, 100, 1000);
        Assert.Equal("Limit.InputBytes", Assert.Throws<COMException>(() => session.OpenBytes(File.ReadAllBytes(Fixture))).Message);
    }

    [Fact]
    public void TextAndWriteBudgetsFailWithoutReplacingFilesAndCloseDisposeArePredictable()
    {
        using var session = new PdfAutomationSession();
        session.OpenFile(Fixture);
        Assert.Equal("Automation.CharacterLimit", Assert.Throws<COMException>(() => session.ExtractText(0, 0, false, 1)).Message);
        Assert.Equal("Automation.CharacterLimit", session.LastDiagnosticCode);
        Assert.Equal("Original automation Ω", session.ExtractText(0, 0, false, 21));
        Assert.Equal("", session.LastDiagnosticCode);
        session.Close();
        session.Configure(100000, 10000000, 1, 100000, 30000);
        session.OpenFile(Fixture);
        string path = Path.Combine(Path.GetTempPath(), "automation-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            File.WriteAllBytes(path, new byte[] { 7, 8 });
            Assert.Throws<COMException>(() => session.SaveFile(path, false));
            Assert.Equal(new byte[] { 7, 8 }, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }

        session.Cancel();
        session.Close();
        session.Close();
        session.Dispose();
        session.Dispose();
        Assert.Equal("Automation.Disposed", Assert.Throws<COMException>(() => session.PageCount).Message);
    }

    [Fact]
    public async Task ConcurrentOrdinaryCallsRejectAndCancellationTargetsOnlyTheActiveSessionCall()
    {
        using var session = new PdfAutomationSession();
        using var entered = new ManualResetEventSlim();
        var work = Task.Run(() => Assert.Throws<COMException>(() => session.Run(token =>
        {
            entered.Set();
            Assert.True(token.WaitHandle.WaitOne(TimeSpan.FromSeconds(20)));
            token.ThrowIfCancellationRequested();
            return 0;
        })));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(20)));
        Assert.Equal("Automation.Busy", Assert.Throws<COMException>(() => session.Close()).Message);
        Assert.Equal("Automation.Busy", Assert.Throws<COMException>(() => session.Dispose()).Message);
        session.Cancel();
        var failure = await work;
        Assert.Equal("Automation.Canceled", failure.Message);
        Assert.Equal(unchecked((int)0x8004A107), failure.HResult);
        session.OpenFile(Fixture);
        Assert.Equal(2, session.PageCount);
        session.Cancel();
        Assert.Equal(2, session.PageCount);
    }

    [Fact]
    public void EncryptedInputsRejectExplicitlyAndFileCaptureReleasesItsOwnedHandle()
    {
        using var session = new PdfAutomationSession();
        string path = Path.Combine(Path.GetTempPath(), "automation-owned-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            File.Copy(Fixture, path);
            session.OpenFile(path);
            File.Delete(path);
            Assert.Equal("Original automation Ω", session.ExtractText(0, 0, false, 100));
        }
        finally
        {
            File.Delete(path);
        }

        using var input = File.OpenRead(Fixture);
        using var source = Pdf.Open(input);
        using var encrypted = new MemoryStream();
        using var user = new PdfPassword(new byte[] { 117 });
        using var owner = new PdfPassword(new byte[] { 111 });
        PdfSecurity.EncryptWithPassword(source.CreateSnapshot(), encrypted, new(user, owner));
        var error = Assert.Throws<COMException>(() => session.OpenBytes(encrypted.ToArray()));
        Assert.Equal("Reader.EncryptionProviderRequired", error.Message);
        Assert.Equal(2, session.PageCount);
    }
}
