using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Fluent.Pdf;
using PdfFacade = Fluent.Pdf.Pdf;

internal static class Program
{
    private static int checks;
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { checks++; return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    public static int Main(string[] args)
    {
        try
        {
            var output = Path.GetFullPath(args[0]);
            if (Directory.Exists(output)) throw new Exception("Use fresh consumer output.");
            Directory.CreateDirectory(output);
            using var document = PdfFacade.Create().AddPage(new PdfPageSize(240, 160)).AddPage();
            using var bytes = new MemoryStream();
            Require(document.Save(bytes).Succeeded, "Save failed");
            Require(bytes.CanWrite && Encoding.ASCII.GetString(bytes.ToArray(), 0, 5) == "%PDF-", "Real PDF / caller output ownership");
            File.WriteAllBytes(Path.Combine(output, "generated.pdf"), bytes.ToArray());
            bytes.Position = 0;
            using var reopened = PdfFacade.Open(bytes);
            Require(reopened.Pages.Count == 2 && reopened.Pages[0].Size.Width == 240 && reopened.Pages[0].Size.Height == 160, "Page geometry round trip");
            reopened.Pages[0].Rotation = PdfPageRotation.Clockwise90;
            using var edited = PdfMetadata.Edit(reopened.CreateSnapshot(), e => e.SetInformation("Title", "Core 1.0.0 – Հայերեն").SetInformation("Author", "Package consumer"));
            edited.SaveFile(Path.Combine(output, "edited.pdf"));
            using var loaded = PdfFacade.OpenFile(Path.Combine(output, "edited.pdf"));
            Require(loaded.Pages.Count == 2 && loaded.Pages[0].Rotation == PdfPageRotation.Clockwise90, "Atomic save and rotation");
            Require(PdfMetadata.Inspect(loaded.CreateSnapshot()).Text["Title"] == "Core 1.0.0 – Հայերեն", "Unicode metadata");
            using var transaction = new PdfTransaction(loaded.SourceDocument!);
            var id = transaction.Add(PdfValue.Name("package-consumer"));
            using var incremental = new MemoryStream();
            transaction.Save(incremental, new PdfWriteOptions(PdfWriteMode.Incremental));
            incremental.Position = 0;
            var syntax = PdfSyntax.Read(incremental);
            Require(syntax.Revisions.Count == 2 && syntax.Resolve(id).NameValue == "package-consumer", "Incremental write");
            File.WriteAllBytes(Path.Combine(output, "incremental.pdf"), incremental.ToArray());
            Require(bytes.CanRead, "Caller input ownership");
            Reject<OperationCanceledException>(() => document.Save(new MemoryStream(), new CancellationToken(true)));
            using var limited = PdfFacade.Create(new PdfOptions(operationPolicy: new PdfOperationPolicy(limits: new PdfResourceLimits(maximumOutputBytes: 16)))).AddPage();
            Reject<PdfException>(() => limited.Save(new MemoryStream()));
            using var malformed = new MemoryStream(Encoding.ASCII.GetBytes("not a PDF"));
            Reject<PdfException>(() => PdfFacade.Open(malformed));
            var snapshot = document.CreateSnapshot();
            document.Dispose();
            Require(snapshot.Pages.Count == 2, "Owned snapshot lifetime");
            Reject<ObjectDisposedException>(() => document.AddPage());
            var assembly = typeof(PdfDocument).Assembly;
            string sha;
            using (var algorithm = SHA256.Create()) using (var stream = File.OpenRead(assembly.Location)) sha = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
#if NETFRAMEWORK
            string architecture = IntPtr.Size == 8 ? "X64" : "X86";
            string framework = "Framework";
            string platform = "Windows";
            using var registry = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64);
            using var key = registry.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
            string release = Convert.ToString(key!.GetValue("Release"), System.Globalization.CultureInfo.InvariantCulture)!;
#else
            string architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
            string framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            string platform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "Windows" : System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) ? "macOS" : "Linux";
            string release = "";
#endif
            var core = typeof(object).Assembly;
            new XElement("consumer", new XAttribute("checks", checks), new XAttribute("failed", 0), new XAttribute("skipped", 0),
                new XAttribute("architecture", architecture), new XAttribute("framework", framework), new XAttribute("frameworkRelease", release),
                new XAttribute("runtimeVersion", Environment.Version.ToString()), new XAttribute("coreLibrary", core.GetName().Name!),
                new XAttribute("coreLibraryVersion", FileVersionInfo.GetVersionInfo(core.Location).FileVersion!), new XAttribute("sdkSha256", sha),
                new XAttribute("os", Environment.OSVersion.ToString()), new XAttribute("platform", platform)).Save(Path.Combine(output, "consumer.xml"));
            Console.WriteLine("CORE_CONSUMER_PASS;" + checks + ";" + architecture + ";" + framework);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
