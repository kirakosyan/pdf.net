using System.IO;
using System.Text;
using System.Threading;
using Fluent.Pdf.Formats;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class PrinterLanguageTests
{
    private const string Header = "%!PS-Adobe-3.0 EPSF-3.0\n%%BoundingBox: 0 0 100 50\n%%EndComments\n";
    private static MemoryStream Input(string text) => new(Encoding.UTF8.GetBytes(text));
    private static void Save(string name, byte[] bytes)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "format-fixtures");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name), bytes);
    }

    [Fact]
    public void OpaqueVectorsCurvesDashTransformsAndClipsRoundTripThroughLiteralEps()
    {
        using var svg = Input(SvgTests.Fixture);
        using var source = PdfSvg.Import(svg);
        using var output = new MemoryStream();
        Assert.True(PdfStaticEps.WritePage(source.CreateSnapshot(), 0, output).Succeeded);
        Assert.True(output.CanWrite);
        string eps = Encoding.UTF8.GetString(output.ToArray());
        Assert.Contains("curveto", eps);
        Assert.Contains("setdash", eps);
        Assert.DoesNotContain("colorimage", eps);
        Assert.DoesNotContain("findfont", eps);
        output.Position = 0;
        using var imported = PdfStaticEps.Import(output);
        Assert.True(output.CanRead);
        Assert.Equal(source.Pages[0].Size, imported.Pages[0].Size);
        Assert.DoesNotContain(PdfPageGraphics.Inspect(imported.CreateSnapshot(), 0).Occurrences, item => item.Kind == PdfGraphicsOccurrenceKind.Image);
        Save("vectors.eps", output.ToArray());
        string directory = Path.Combine(AppContext.BaseDirectory, "format-fixtures");
        source.SaveFile(Path.Combine(directory, "eps-original.pdf"));
        imported.SaveFile(Path.Combine(directory, "eps-roundtrip.pdf"));
    }

    [Fact]
    public void ShiftedBoundingBoxesAndRelativeSegmentsPreservePhysicalGeometry()
    {
        const string eps = "%!PS-Adobe-3.0 EPSF-3.0\n%%BoundingBox: 10 20 110 70\n%%EndComments\nnewpath 10 20 moveto 100 0 rlineto 0 50 rlineto -100 0 rlineto closepath 1 0 0 setrgbcolor fill showpage\n";
        using var input = Input(eps);
        using var document = PdfStaticEps.Import(input);
        Assert.Equal(new PdfPageSize(100, 50), document.Pages[0].Size);
        using var csharpInput = Input(eps);
        using var csharp = ContractExamples.CSharp.ContractExample.ImportStaticEps(csharpInput);
        using var vbInput = Input(eps);
        using var vb = ContractExamples.VisualBasic.ContractExample.ImportStaticEps(vbInput);
        Assert.Equal(document.Pages[0].Size, csharp.Pages[0].Size);
        Assert.Equal(document.Pages[0].Size, vb.Pages[0].Size);
        Save("shifted-source.eps", Encoding.UTF8.GetBytes(eps));
        document.SaveFile(Path.Combine(AppContext.BaseDirectory, "format-fixtures", "shifted-eps.pdf"));
    }

    [Theory]
    [InlineData("{ } loop", "Printer.StaticEps.Operator")]
    [InlineData("(/not-read) (r) file", "Printer.StaticEps.Operator")]
    [InlineData("findfont", "Printer.StaticEps.Operator")]
    [InlineData("false setoverprint", "Printer.StaticEps.Operator")]
    [InlineData("%%IncludeFile: /not-read\n", "Printer.StaticEps.Resources")]
    public void ProceduresFilesFontsEffectsAndExternalDscCannotBecomePartialSuccess(string body, string code)
    {
        using var input = Input(Header + body);
        var error = Assert.Throws<PdfException>(() => PdfStaticEps.Import(input));
        Assert.Equal(code, error.Diagnostic.Code);
        Assert.DoesNotContain("not-read", error.Message);
        Assert.True(input.CanRead);
    }

    [Fact]
    public void UnfinishedStateLimitsInvalidNumbersAndCancellationPreserveTheirExactFailure()
    {
        using var version = Input(Header.Replace("%%EndComments", "%%LanguageLevel: 3\n%%EndComments"));
        Assert.Equal("Printer.StaticEps.Header", Assert.Throws<PdfException>(() => PdfStaticEps.Import(version)).Diagnostic.Code);
        using var state = Input(Header + "gsave showpage");
        Assert.Equal("Printer.StaticEps.State", Assert.Throws<PdfException>(() => PdfStaticEps.Import(state)).Diagnostic.Code);
        using var huge = Input(Header + "1e400 0 moveto");
        Assert.Equal("Printer.StaticEps.Number", Assert.Throws<PdfException>(() => PdfStaticEps.Import(huge)).Diagnostic.Code);
        using var depth = Input(Header + "gsave gsave grestore grestore showpage");
        Assert.Equal("Limit.Depth", Assert.Throws<PdfException>(() => PdfStaticEps.Import(depth, new(limits: new(maximumDepth: 1)))).Diagnostic.Code);
        using var limited = Input(Header);
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfStaticEps.Import(limited, new(limits: new(maximumObjects: 1)))).Diagnostic.Code);
        Assert.Equal(0, limited.Position);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var stopped = Input(Header);
        Assert.Throws<OperationCanceledException>(() => PdfStaticEps.Import(stopped, cancellationToken: cancelled.Token));
        Assert.Equal(0, stopped.Position);
    }

    [Fact]
    public void UnsupportedPdfObjectsRejectBeforeEpsPublication()
    {
        using var source = ExtractionTests.Fixture("BT /F1 10 Tf 1 0 0 1 20 100 Tm (ABC) Tj ET");
        using var output = new MemoryStream();
        output.WriteByte(42);
        Assert.Equal("Printer.StaticEps.ExportUnsupported", Assert.Throws<PdfException>(() => PdfStaticEps.WritePage(source.CreateSnapshot(), 0, output)).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, output.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PostScriptDefaultAndExplicitStrokeAdjustmentArePreserved(bool explicitOff)
    {
        string eps = Header + (explicitOff ? "false setstrokeadjust\n" : "") + "newpath 10.25 10.25 moveto 90.25 10.25 lineto 1.1 setlinewidth stroke showpage\n";
        using var input = Input(eps);
        using var document = PdfStaticEps.Import(input);
        using var output = new MemoryStream();
        document.Save(output);
        string pdf = Encoding.ASCII.GetString(output.ToArray());
        if (explicitOff)
        {
            Assert.DoesNotContain("/SA true", pdf);
            Assert.Contains("/SA false", pdf);
            using var reexported = new MemoryStream();
            PdfStaticEps.WritePage(document.CreateSnapshot(), 0, reexported);
            Assert.Contains("false setstrokeadjust", Encoding.UTF8.GetString(reexported.ToArray()));
        }
        else
            Assert.Contains("/SA true", pdf);
        string stem = explicitOff ? "stroke-adjust-false" : "stroke-adjust-default";
        Save(stem + ".eps", Encoding.UTF8.GetBytes(eps));
        Save(stem + ".pdf", output.ToArray());
    }

    [Fact]
    public void AsciiLineBoundariesCannotHideCommandsInsideComments()
    {
        using var hidden = Input(Header + "% ignored comment\r(/not-read) (r) file\n");
        Assert.Equal("Printer.StaticEps.Operator", Assert.Throws<PdfException>(() => PdfStaticEps.Import(hidden)).Diagnostic.Code);
        using var cr = Input((Header + "showpage\n").Replace("\n", "\r"));
        using var document = PdfStaticEps.Import(cr);
        Assert.Equal(new PdfPageSize(100, 50), document.Pages[0].Size);
        using var unicode = Input(Header + "1\u20000 moveto");
        Assert.Equal("Printer.StaticEps.Number", Assert.Throws<PdfException>(() => PdfStaticEps.Import(unicode)).Diagnostic.Code);
    }

    private sealed class BoundaryInterpreter : IPdfIsolatedPrinterInterpreter
    {
        private readonly byte[] _pdf;
        internal bool Available = true;
        internal Action<PdfOperationContext>? Before;
        internal Stream? SourceLease, OutputLease;
        internal PdfOperationContext? Context;
        internal PdfPrinterLanguageProfile Profile;
        internal int MaximumPages;
        internal BoundaryInterpreter(byte[] pdf) => _pdf = pdf;
        public string EngineId => "test.boundary-fixture";

        public bool Supports(PdfPrinterLanguageProfile profile) => Available && profile == PdfPrinterLanguageProfile.PclXl;
        public void Convert(Stream input, PdfPrinterLanguageProfile profile, int maximumPages, Stream pdfOutput, PdfOperationContext context)
        {
            using var worker = context.EnterScope(PdfResourceKind.Workers);
            SourceLease = input;
            OutputLease = pdfOutput;
            Context = context;
            Profile = profile;
            MaximumPages = maximumPages;
            Assert.Equal(27, input.ReadByte());
            Before?.Invoke(context);
            pdfOutput.Write(_pdf, 0, _pdf.Length);
            context.Checkpoint();
        }
    }

    private static BoundaryInterpreter Boundary()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(20, 10));
        using var output = new MemoryStream();
        source.Save(output);
        return new(output.ToArray());
    }

    [Fact]
    public void OptionalInterpreterChecksExactAvailabilityBeforeInputAndExpiresSharedContextLeases()
    {
        using var input = new MemoryStream(new byte[] { 27, 37, 88 });
        Assert.Equal("Printer.InterpreterUnavailable", Assert.Throws<PdfException>(() => PdfPrinterLanguages.Import(input, PdfPrinterLanguageProfile.Pcl5c)).Diagnostic.Code);
        Assert.Equal(0, input.Position);
        var host = Boundary();
        Assert.Equal("Printer.ProfileUnsupported", Assert.Throws<PdfException>(() => PdfPrinterLanguages.Import(input, PdfPrinterLanguageProfile.Pcl5c, host)).Diagnostic.Code);
        Assert.Equal(0, input.Position);
        using var document = PdfPrinterLanguages.Import(input, PdfPrinterLanguageProfile.PclXl, host, maximumPages: 1, policy: new(limits: new(maximumWorkers: 1)));
        Assert.Equal(new PdfPageSize(20, 10), document.Pages[0].Size);
        Assert.True(input.CanRead);
        Assert.Equal(PdfPrinterLanguageProfile.PclXl, host.Profile);
        Assert.Equal(1, host.MaximumPages);
        Assert.Throws<ObjectDisposedException>(() => host.SourceLease!.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => host.OutputLease!.WriteByte(0));
        Assert.Throws<ObjectDisposedException>(() => host.Context!.Checkpoint());
    }

    [Fact]
    public void InterpreterFailuresRedactPayloadsAndCannotHideLatchedBudgetOrLossFailures()
    {
        var host = Boundary();
        host.Before = _ => throw new InvalidOperationException("private secret value");
        using var input = new MemoryStream(new byte[] { 27 });
        var error = Assert.Throws<PdfException>(() => PdfPrinterLanguages.Import(input, PdfPrinterLanguageProfile.PclXl, host));
        Assert.Equal("Printer.InterpreterFailed", error.Diagnostic.Code);
        Assert.DoesNotContain("private", error.Message);
        input.Position = 0;
        host.Before = c => c.RejectLimit(PdfResourceKind.ExpandedBytes);
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PdfPrinterLanguages.Import(input, PdfPrinterLanguageProfile.PclXl, host)).Diagnostic.Code);
        input.Position = 0;
        host.Before = c => c.Report(new("Printer.CommandUnsupported", "The explicit test boundary reports an unsupported command.", lossCategory: PdfLossCategory.UnsupportedContent));
        Assert.Equal("Printer.CommandUnsupported", Assert.Throws<PdfException>(() => PdfPrinterLanguages.Import(input, PdfPrinterLanguageProfile.PclXl, host)).Diagnostic.Code);
        input.Position = 0;
        using var accepted = PdfPrinterLanguages.Import(input, PdfPrinterLanguageProfile.PclXl, host, policy: new(PdfProcessingMode.Permissive));
        Assert.Contains(accepted.LastOperationReport!.Diagnostics, d => d.Code == "Printer.CommandUnsupported" && d.Severity == PdfDiagnosticSeverity.Warning);
    }
}
