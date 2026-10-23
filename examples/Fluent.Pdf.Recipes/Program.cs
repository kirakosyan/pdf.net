using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Fluent.Pdf;
using Fluent.Pdf.Formats;
using PdfFacade = Fluent.Pdf.Pdf;
using Cs = ContractExamples.CSharp.ContractExample;
using Vb = ContractExamples.VisualBasic.ContractExample;

namespace Fluent.Pdf.Recipes;
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
            return 2;
        string directory = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(directory);
        Run("model-snapshot", Model);
        Run("language-vector", () =>
        {
            Write("cs-vector.pdf", Cs.WriteVectorPdf);
            Write("vb-vector.pdf", Vb.WriteVectorPdf);
        });
        Run("language-canvas", () =>
        {
            Write("cs-canvas.pdf", Cs.WriteCanvasPdf);
            Write("vb-canvas.pdf", Vb.WriteCanvasPdf);
        });
        Run("select-and-extract", () => Select(directory));
        Run("limits-and-cancellation", Limits);
        Run("explicit-provider", MissingProvider);
        Run("preflight-and-formats", () => Preflight(directory));
        Run("lossless-optimization", () => Optimize(directory));
        Console.WriteLine("RECIPES_RUNTIME;" + RuntimeInformation.FrameworkDescription + ";" + RuntimeInformation.ProcessArchitecture + ";" + typeof(PdfDocument).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName + ";" + Mode);
        return 0;
        void Write(string name, Func<Stream, PdfOperationReport> recipe)
        {
            using var output = File.Create(Path.Combine(directory, name));
            recipe(output);
            Check(output.CanWrite, "Borrowed output was closed.");
        }
    }

#if RECIPE_PACKAGE_MODE
    private const string Mode = "package-consumer";
#else
    private const string Mode = "source-consumer";
#endif
    private static void Run(string id, Action action)
    {
        action();
        Console.WriteLine("RECIPE_PASS;" + id);
    }

    private static void Check(bool condition, string failure)
    {
        if (!condition)
            throw new InvalidOperationException(failure);
    }

    private static FileStream Input() => File.OpenRead(Path.Combine(AppContext.BaseDirectory, "original.pdf"));
#region recipe:model-snapshot
    private static void Model()
    {
        Check(Cs.Run() == 2 && Vb.Run() == 2, "Both compiled language models must run.");
        using var document = PdfFacade.Create().AddPage(new PdfPageSize(120, 60)).AddPage(new PdfPageSize(60, 120));
        var snapshot = document.CreateSnapshot();
        document.Pages.Move(0, 1);
        Check(snapshot.Pages[0].Size.Width == 120 && document.Pages[0].Size.Width == 60, "Snapshot identity changed.");
    }

#endregion
#region recipe:select-and-extract
    private static void Select(string directory)
    {
        using var input = Input();
        using var original = PdfFacade.Open(input);
        var source = original.CreateSnapshot();
        var selection = PdfPageSelection.Parse("2,1", source.Pages.Count);
        using var selected = PdfPageOrganization.Extract(source, selection);
        using var output = File.Create(Path.Combine(directory, "selected.pdf"));
        selected.Save(output);
        Check(input.CanRead && output.CanWrite, "Stream ownership changed.");
        Check(PdfTextExtraction.ReadPage(selected.CreateSnapshot(), 0).RawText == "Second automation page", "Physical order was lost.");
        input.Position = 0;
        Check(Cs.ExtractPageText(input) == "Original automation Ω", "C# text mapping changed.");
        input.Position = 0;
        Check(Vb.ExtractPageText(input) == "Original automation Ω", "VB text mapping changed.");
    }

#endregion
#region recipe:limits-and-cancellation
    private static void Limits()
    {
        using var input = Input();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            using var canceled = PdfFacade.Open(input, cancellationToken: cancellation.Token);
            throw new InvalidOperationException("Cancellation was ignored.");
        }
        catch (OperationCanceledException)
        {
            Check(input.CanRead && input.Position == 0, "Canceled input was consumed or closed.");
        }

        var policy = new PdfOperationPolicy(limits: new(maximumOutputBytes: 1));
        using var document = PdfFacade.Create(new(operationPolicy: policy)).AddPage();
        using var output = new MemoryStream();
        try
        {
            document.Save(output);
            throw new InvalidOperationException("Output ceiling was ignored.");
        }
        catch (PdfException error)
        {
            Check(error.Diagnostic.Code == "Limit.OutputBytes" && output.CanWrite, "The finite failure contract changed.");
        }
    }

#endregion
#region recipe:explicit-provider
    private static void MissingProvider()
    {
        using var input = Input();
        using var document = PdfFacade.Open(input);
        try
        {
            PdfRendering.RenderPage(document.CreateSnapshot(), 0, PdfProviderRegistry.Empty);
            throw new InvalidOperationException("An implicit renderer was selected.");
        }
        catch (PdfProviderUnavailableException error)
        {
            Check(error.Diagnostic.Code == "Provider.Unavailable", "The missing-provider code changed.");
        }
    }

#endregion
#region recipe:preflight-and-formats
    private static void Preflight(string directory)
    {
        using var input = Input();
        var report = PdfPreflight.Inspect(input);
        Check(!report.ConformanceValidated && input.CanRead, "Inspection cannot certify conformance or take ownership.");
        using var output = File.Create(Path.Combine(directory, "preflight.json"));
        PdfPreflight.WriteReport(report, output);
        using var cs = File.Create(Path.Combine(directory, "cs-formats.json"));
        Cs.WriteConversionMatrix(cs);
        using var vb = File.Create(Path.Combine(directory, "vb-formats.json"));
        Vb.WriteConversionMatrix(vb);
    }

#endregion
#region recipe:lossless-optimization
    private static void Optimize(string directory)
    {
        using var input = Input();
        using var document = PdfFacade.Open(input);
        var result = PdfOptimization.Optimize(document.CreateSnapshot());
        using var output = File.Create(Path.Combine(directory, "optimized.pdf"));
        using var bytes = result.OpenPdfStream();
        bytes.CopyTo(output);
        Check(output.CanWrite && input.CanRead, "Optimization took caller stream ownership.");
    }
#endregion
}
