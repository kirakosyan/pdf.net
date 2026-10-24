using System.IO;
using Fluent.Pdf.Formats;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class LegacyImportTests
{
    private sealed class Host : IPdfIsolatedLegacyImporter
    {
        public string EngineId => "original.boundary-fixture";
        internal bool Available = true;
        internal PdfLegacyImportStatus Status;
        internal Action<PdfOperationContext>? Before;
        internal Stream? Input, Output;
        internal PdfOperationContext? Context;
        internal readonly byte[] Pdf;
        internal byte[]? Replacement;
        internal int SourceBytes;
        internal Host(int pages = 1)
        {
            using var document = Fluent.Pdf.Pdf.Create();
            for (int i = 0; i < pages; i++) document.AddPage(new PdfPageSize(20 + i, 10));
            using var output = new MemoryStream();
            document.Save(output);
            Pdf = output.ToArray();
        }
        public bool Supports(PdfLegacyImportOptions options) => Available && options.VersionProfile == "fixture-only";
        public PdfLegacyImportStatus Import(Stream source, PdfLegacyImportOptions options, Stream pdfOutput, PdfOperationContext context)
        {
            Input = source; Output = pdfOutput; Context = context;
            using var worker = context.EnterScope(PdfResourceKind.Workers);
            while (source.ReadByte() != -1) SourceBytes++;
            Before?.Invoke(context);
            var payload = Replacement ?? Pdf;
            pdfOutput.Write(payload, 0, payload.Length);
            return Status;
        }
    }

    [Theory]
    [InlineData(PdfLegacyFormat.DjVu)]
    [InlineData(PdfLegacyFormat.Cdr)]
    public void ExplicitHostPreservesPageOrderAndExpiresBorrowedLeases(PdfLegacyFormat format)
    {
        var host = new Host(2);
        using var source = new MemoryStream(new byte[] { 1, 2, 3 });
        source.Position = 1;
        using var result = PdfLegacyImport.Read(source, new(format, "fixture-only"), host);
        Assert.Equal(2, result.Pages.Count);
        Assert.Equal(20, result.Pages[0].Size.Width);
        Assert.Equal(21, result.Pages[1].Size.Width);
        Assert.Equal(2, host.SourceBytes);
        Assert.True(source.CanRead);
        Assert.Throws<ObjectDisposedException>(() => host.Input!.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => host.Output!.WriteByte(0));
        Assert.Throws<ObjectDisposedException>(() => host.Context!.Checkpoint());
    }

    [Fact]
    public void MissingHostOrProfileDoesNotReadSource()
    {
        using var source = new MemoryStream(new byte[] { 1 });
        var options = new PdfLegacyImportOptions(PdfLegacyFormat.DjVu, "fixture-only");
        Assert.Equal("LegacyFormat.HostUnavailable", Assert.Throws<PdfException>(() => PdfLegacyImport.Read(source, options)).Diagnostic.Code);
        Assert.Equal("LegacyFormat.ProfileUnavailable", Assert.Throws<PdfException>(() => PdfLegacyImport.Read(source, options, new Host { Available = false })).Diagnostic.Code);
        Assert.Equal(0, source.Position);
    }

    [Theory]
    [InlineData(PdfLegacyImportStatus.UnsupportedVersion)]
    [InlineData(PdfLegacyImportStatus.MissingCodec)]
    [InlineData(PdfLegacyImportStatus.MissingFont)]
    [InlineData(PdfLegacyImportStatus.UnsupportedRecord)]
    [InlineData(PdfLegacyImportStatus.DamagedInput)]
    public void FailureStatusRejectsEvenValidStagedPdf(PdfLegacyImportStatus status)
    {
        using var source = new MemoryStream(new byte[] { 1 });
        Assert.Equal("LegacyFormat." + status, Assert.Throws<PdfException>(() => PdfLegacyImport.Read(source, new(PdfLegacyFormat.Cdr, "fixture-only"), new Host { Status = status })).Diagnostic.Code);
    }

    [Fact]
    public void HostExceptionIsSanitizedAndLatchedLimitsCannotBeHidden()
    {
        using var source = new MemoryStream(new byte[] { 1 });
        var options = new PdfLegacyImportOptions(PdfLegacyFormat.DjVu, "fixture-only");
        var host = new Host { Before = _ => throw new Exception("private-document-path") };
        var error = Assert.Throws<PdfException>(() => PdfLegacyImport.Read(source, options, host));
        Assert.Equal("LegacyFormat.HostFailed", error.Diagnostic.Code);
        Assert.DoesNotContain("private-document", error.ToString());
        source.Position = 0;
        host.Before = c => { try { c.RejectLimit(PdfResourceKind.Pixels); } catch (PdfException) { } };
        Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => PdfLegacyImport.Read(source, options, host)).Diagnostic.Code);
    }

    [Fact]
    public void InputOutputPageBoundsAndCancellationReject()
    {
        using var source = new MemoryStream(new byte[] { 1, 2 });
        Assert.Equal("Format.InputExtent", Assert.Throws<PdfException>(() => PdfLegacyImport.Read(source, new(PdfLegacyFormat.DjVu, "fixture-only", maximumInputBytes: 1), new Host())).Diagnostic.Code);
        source.Position = 0;
        Assert.Throws<PdfException>(() => PdfLegacyImport.Read(source, new(PdfLegacyFormat.DjVu, "fixture-only", maximumOutputBytes: 1), new Host()));
        source.Position = 0;
        Assert.Throws<PdfException>(() => PdfLegacyImport.Read(source, new(PdfLegacyFormat.DjVu, "fixture-only", maximumPages: 1), new Host(2)));
        source.Position = 0;
        Assert.ThrowsAny<OperationCanceledException>(() => PdfLegacyImport.Read(source, new(PdfLegacyFormat.DjVu, "fixture-only"), new Host(), cancellationToken: new(true)));
        Assert.Equal(0, source.Position);
    }

    [Fact]
    public void InvalidProfileAndUnknownHostStatusReject()
    {
        Assert.Throws<ArgumentException>(() => new PdfLegacyImportOptions(PdfLegacyFormat.DjVu, "../ambient"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfLegacyImportOptions((PdfLegacyFormat)10, "fixture-only"));
        using var source = new MemoryStream(new byte[] { 1 });
        Assert.Equal("LegacyFormat.InvalidStatus", Assert.Throws<PdfException>(() => PdfLegacyImport.Read(source, new(PdfLegacyFormat.Cdr, "fixture-only"), new Host { Status = (PdfLegacyImportStatus)99 })).Diagnostic.Code);
    }

    [Fact]
    public void InvalidPdfAndHiddenStrictLossReject()
    {
        using var source = new MemoryStream(new byte[] { 1 });
        var options = new PdfLegacyImportOptions(PdfLegacyFormat.DjVu, "fixture-only");
        Assert.Throws<PdfException>(() => PdfLegacyImport.Read(source, options, new Host { Replacement = new byte[] { 42 } }));
        source.Position = 0;
        var host = new Host { Before = c => { try { c.Report(new("LegacyFormat.FidelityLoss", "Original fixture loss", lossCategory: PdfLossCategory.FidelityReduction)); } catch (PdfException) { } } };
        Assert.Equal("LegacyFormat.FidelityLoss", Assert.Throws<PdfException>(() => PdfLegacyImport.Read(source, options, host)).Diagnostic.Code);
        source.Position = 0;
        using var result = PdfLegacyImport.Read(source, options, host, new(PdfProcessingMode.Permissive));
        Assert.Contains(result.LastOperationReport!.Diagnostics, d => d.Code == "LegacyFormat.FidelityLoss");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LanguageConsumersUseExplicitCallerHost(bool visualBasic)
    {
        using var source = new MemoryStream(new byte[] { 1 });
        var options = new PdfLegacyImportOptions(PdfLegacyFormat.Cdr, "fixture-only");
        using var result = visualBasic
            ? ContractExamples.VisualBasic.ContractExample.ImportLegacyFormat(source, options, new Host())
            : ContractExamples.CSharp.ContractExample.ImportLegacyFormat(source, options, new Host());
        Assert.Single(result.Pages);
    }
}
