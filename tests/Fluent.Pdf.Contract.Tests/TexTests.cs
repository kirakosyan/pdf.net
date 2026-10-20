using System.IO;
using System.Text;
using Fluent.Pdf.Formats;
using Xunit;
namespace Fluent.Pdf.Contract.Tests;
public sealed class TexTests
{
    private sealed class Host : IPdfIsolatedTexTypesetter
    {
        internal bool Available = true; internal Action<PdfOperationContext>? Before; internal Stream? Input, Output; internal PdfOperationContext? Context;
        public string EngineId => "test.boundary";
        public bool Supports(PdfTexOptions options) => Available && options.Profile == PdfTexProfile.PdfLatex && options.RequiredPackages.SequenceEqual(new[] { "amsmath" });
        public void Typeset(Stream source, PdfTexOptions options, Stream pdfOutput, PdfOperationContext context) { using var worker = context.EnterScope(PdfResourceKind.Workers); Input = source; Output = pdfOutput; Context = context; Before?.Invoke(context); using var document = Pdf.Create().AddPage(new PdfPageSize(20, 10)); using var pdf = new MemoryStream(); document.Save(pdf); pdfOutput.Write(pdf.ToArray(), 0, (int)pdf.Length); }
    }
    [Fact]
    public void OptionalHostRequiresExactLocalPrerequisitesAndExpiresLeases()
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("\\write18{not-executed}")); var options = new PdfTexOptions(PdfTexProfile.PdfLatex, new[] { "amsmath" }, 1); Assert.Equal("Tex.TypesetterUnavailable", Assert.Throws<PdfException>(() => PdfTex.Import(source, options)).Diagnostic.Code); Assert.Equal(0, source.Position); var host = new Host { Available = false }; Assert.Equal("Tex.PrerequisiteUnavailable", Assert.Throws<PdfException>(() => PdfTex.Import(source, options, host)).Diagnostic.Code); Assert.Equal(0, source.Position); host.Available = true; using var result = PdfTex.Import(source, options, host, new(limits: new(maximumWorkers: 1))); Assert.Single(result.Pages); Assert.True(source.CanRead); Assert.Throws<ObjectDisposedException>(() => host.Input!.ReadByte()); Assert.Throws<ObjectDisposedException>(() => host.Output!.WriteByte(0)); Assert.Throws<ObjectDisposedException>(() => host.Context!.Checkpoint()); Assert.Throws<ArgumentException>(() => new PdfTexOptions(PdfTexProfile.PdfLatex, new[] { "../ambient" }));
    }
    [Fact]
    public void FailuresCannotHideLatchedLimitsOrLeakPrivateMessages()
    {
        using var source = new MemoryStream(new byte[] { 37 }); var options = new PdfTexOptions(PdfTexProfile.PdfLatex, new[] { "amsmath" }); var host = new Host { Before = _ => throw new Exception("private-path") }; var error = Assert.Throws<PdfException>(() => PdfTex.Import(source, options, host)); Assert.Equal("Tex.TypesetterFailed", error.Diagnostic.Code); Assert.DoesNotContain("private", error.Message); source.Position = 0; host.Before = c => c.RejectLimit(PdfResourceKind.Pixels); Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => PdfTex.Import(source, options, host)).Diagnostic.Code);
    }
    [Fact]
    public void InferredExportEscapesCommandsAndRequiresLossAuthorityBeforePublication()
    {
        const string literal = "\\input{secret} $x_1$ & 50% # ~ ^ <>"; using var svg = new MemoryStream(Encoding.UTF8.GetBytes("<svg width='500' height='80'><text x='4' y='20' font-size='10'>" + System.Security.SecurityElement.Escape(literal) + "</text></svg>")); using var document = PdfSvg.Import(svg, new(fonts: new(TextTests.Read("business.ttf"))), new(new PdfSimpleTextShaper())); using var output = new MemoryStream(); output.WriteByte(42); Assert.Equal("Tex.InferredText", Assert.Throws<PdfException>(() => PdfTex.WriteInferredText(document.CreateSnapshot(), new[] { 0 }, output)).Diagnostic.Code); Assert.Equal(new byte[] { 42 }, output.ToArray()); output.SetLength(0); var report = PdfTex.WriteInferredText(document.CreateSnapshot(), new[] { 0 }, output, policy: new(PdfProcessingMode.Permissive)); string tex = Encoding.UTF8.GetString(output.ToArray()); Assert.Contains("\\textbackslash{}input\\{secret\\}", tex); Assert.DoesNotContain("\\input{secret}", tex); Assert.Contains(report.Diagnostics, d => d.LossCategory == PdfLossCategory.FidelityReduction); string directory = Path.Combine(AppContext.BaseDirectory, "format-fixtures"); Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, "inferred-text.tex"), output.ToArray());
    }
}
