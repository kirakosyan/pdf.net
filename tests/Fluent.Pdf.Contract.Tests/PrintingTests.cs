using System.IO;
using System.Text;
using System.Threading;
using Fluent.Pdf.Rendering;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class PrintingTests
{
    private const string Printer = "ipps://print.example.test/printers/explicit";
    [Fact]
    public void IppSubmissionCarriesExactChoicesAndObservationsWithoutImplicitRetries()
    {
        using var source = Pdf.Create().AddPage().AddPage();
        var transport = new Transport();
        var registry = new PdfProviderRegistry(new IppPrintProvider(transport));
        var job = PdfPrinting.Submit(source.CreateSnapshot(), new(Printer, copies: 2, duplex: PdfPrintDuplex.LongEdge, scaling: PdfPrintScaling.None, numberUp: 4, firstPage: 2, lastPage: 2), registry);
        Assert.Equal(42, job.JobId);
        Assert.Equal(PdfPrintJobState.Pending, job.State);
        Assert.Equal(1, transport.Calls);
        Assert.Equal(2, transport.Request![3]);
        var text = Encoding.UTF8.GetString(transport.Request);
        Assert.Contains("two-sided-long-edge", text);
        Assert.Contains("application/pdf", text);
        Assert.Contains("ipp-attribute-fidelity", text);
        Assert.Contains("%PDF-", text);
        Assert.Throws<ObjectDisposedException>(() => transport.Input!.ReadByte());
        transport.State = 9;
        Assert.Equal(PdfPrintJobState.Completed, PdfPrinting.GetStatus(Printer, 42, registry).State);
        Assert.Equal(9, transport.Request![3]);
        Assert.Equal(PdfPrintJobState.CancellationRequested, PdfPrinting.Cancel(Printer, 42, registry).State);
        Assert.Equal(8, transport.Request![3]);
        Assert.Equal(3, transport.Calls);
    }
    [Theory]
    [InlineData(0x0401, "Print.Permission")]
    [InlineData(0x040b, "Print.SpoolRejected")]
    [InlineData(1, "Print.Substitution")]
    public void IppRejectsSpoolErrorsAndSubstitutionsWithoutRetry(int status, string code)
    {
        using var source = Pdf.Create().AddPage();
        var transport = new Transport { Status = status };
        Assert.Equal(code, Assert.Throws<PdfException>(() => PdfPrinting.Submit(source.CreateSnapshot(), new(Printer), new(new IppPrintProvider(transport)))).Diagnostic.Code);
        Assert.Equal(1, transport.Calls);
    }
    [Fact]
    public void PrintingRejectsRangesResourcesCancellationAndAmbiguousJobIdentity()
    {
        using var source = Pdf.Create().AddPage();
        var transport = new Transport();
        var providers = new PdfProviderRegistry(new IppPrintProvider(transport));
        Assert.Equal("Print.PageRange", Assert.Throws<PdfException>(() => PdfPrinting.Submit(source.CreateSnapshot(), new(Printer, firstPage: 2), providers)).Diagnostic.Code);
        Assert.Throws<PdfException>(() => PdfPrinting.Submit(source.CreateSnapshot(), new(Printer), providers, new(limits: new(maximumExpandedBytes: 100))));
        Assert.ThrowsAny<OperationCanceledException>(() => PdfPrinting.Submit(source.CreateSnapshot(), new(Printer), providers, cancellationToken: new CancellationToken(true)));
        Assert.Equal("Print.PrinterUri", Assert.Throws<PdfException>(() => PdfPrinting.Submit(source.CreateSnapshot(), new("file:///not-a-printer"), providers)).Diagnostic.Code);
        Assert.Equal(0, transport.Calls);
        transport.Job = 43;
        Assert.Equal("Print.Protocol", Assert.Throws<PdfException>(() => PdfPrinting.GetStatus(Printer, 42, providers)).Diagnostic.Code);
        Assert.Equal(1, transport.Calls);
    }
    [Fact]
    public void PrintingStagesASignedCopyWithoutChangingOriginalSignatureBytes()
    {
        var signed = SignatureVerificationTests.Signed();
        var before = SignatureTests.Bytes(signed.OpenOriginalStream());
        using var input = signed.OpenOriginalStream();
        using var document = Pdf.Open(input);
        var transport = new Transport();
        Assert.Equal(42, PdfPrinting.Submit(document.CreateSnapshot(), new(Printer), new(new IppPrintProvider(transport))).JobId);
        Assert.Equal(before, SignatureTests.Bytes(signed.OpenOriginalStream()));
        using var verifier = new Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider();
        Assert.True(Assert.Single(PdfSignatures.Inspect(signed, new(verifier))).CryptographicallyValid);
        Assert.Throws<PdfProviderUnavailableException>(() => ContractExamples.CSharp.ContractExample.PrintPdf(document.CreateSnapshot(), new(Printer), PdfProviderRegistry.Empty));
        Assert.Throws<PdfProviderUnavailableException>(() => ContractExamples.VisualBasic.ContractExample.PrintPdf(document.CreateSnapshot(), new(Printer), PdfProviderRegistry.Empty));
    }
    [Fact]
    public void TransportFailuresAreRedactedAndResponsesStayBounded()
    {
        using var source = Pdf.Create().AddPage();
        var transport = new Transport { Fail = true };
        var providers = new PdfProviderRegistry(new IppPrintProvider(transport));
        var failure = Assert.Throws<PdfException>(() => PdfPrinting.Submit(source.CreateSnapshot(), new(Printer), providers));
        Assert.Equal("Print.Transport", failure.Diagnostic.Code);
        Assert.DoesNotContain("secret-auth-token", failure.ToString());
        Assert.Equal(1, transport.Calls);
        transport.Fail = false;
        transport.Oversized = true;
        Assert.Throws<PdfException>(() => PdfPrinting.Submit(source.CreateSnapshot(), new(Printer), providers));
        Assert.Equal(2, transport.Calls);
    }
    private sealed class Transport : IPdfIppTransport
    {
        internal int Calls, Status, State = 3, Job = 42;
        internal bool Fail, Oversized;
        internal byte[]? Request;
        internal Stream? Input;
        public void Exchange(Uri printer, Stream request, Stream response, PdfOperationContext context)
        {
            Calls++;
            Input = request;
            if (Fail)
                throw new IOException("secret-auth-token");
            if (Oversized)
            {
                var excessive = new byte[1048577];
                response.Write(excessive, 0, excessive.Length);
                return;
            }
            Assert.Equal(Printer, printer.AbsoluteUri);
            using var bytes = new MemoryStream();
            request.CopyTo(bytes);
            Request = bytes.ToArray();
            var header = new byte[] { 2, 0, (byte)(Status >> 8), (byte)Status, 0, 0, 0, 1, 2 };
            response.Write(header, 0, header.Length);
            Attribute("job-id", 0x21, Job);
            Attribute("job-state", 0x23, State);
            response.WriteByte(3);
            void Attribute(string name, byte tag, int value)
            {
                var raw = Encoding.ASCII.GetBytes(name);
                response.WriteByte(tag);
                response.WriteByte(0);
                response.WriteByte((byte)raw.Length);
                response.Write(raw, 0, raw.Length);
                response.WriteByte(0);
                response.WriteByte(4);
                response.WriteByte((byte)(value >> 24));
                response.WriteByte((byte)(value >> 16));
                response.WriteByte((byte)(value >> 8));
                response.WriteByte((byte)value);
            }
        }
    }
}
