using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class ManagedInvoiceProducerTests
{
    private static PdfInvoiceData Data(string currency = "EUR") => new(PdfInvoiceProfile.FacturX107Minimum, "INV-1", "Seller & Co", "Buyer <One>", new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero), currency, MidpointRounding.AwayFromZero, new[] { new PdfInvoiceLine("Original item", 1, 1.005m, 20), new PdfInvoiceLine("Second item", 2, 3m, 5) });
    private static readonly PdfFlowOptions Layout = new(new(new(new(0, 0, 500, 700))), PdfMargins.Uniform(20));
    private static PdfFlowTextStyle Body() => new(new(TextTests.Read("business.ttf")), 10);
    private static PdfProviderRegistry Providers() => new(ManagedPdfProvider.Instance, new PdfSimpleTextShaper());
    private static ManagedPdfInvoiceProducer Producer() => new(ArchivalFixture.Icc(), "Original source sRGB fixture");
    private static string Hash(byte[] bytes)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    // Deliberately incomplete invoice observer. It must never unlock the public output gate.
    private sealed class InvoiceObserver : IPdfInvoiceValidator
    {
        internal byte[] Pdf = Array.Empty<byte>(), Xml = Array.Empty<byte>();
        public string EngineId => "fixture.invoice-observer-incomplete";
        public bool Supports(PdfInvoiceProfile profile) => profile == PdfInvoiceProfile.FacturX107Minimum;
        public PdfInvoiceValidation Validate(Stream pdf, Stream xml, PdfInvoiceData invoice, string request, PdfOperationContext context)
        {
            using var p = new MemoryStream();
            using var x = new MemoryStream();
            pdf.CopyTo(p);
            xml.CopyTo(x);
            Pdf = p.ToArray();
            Xml = x.ToArray();
            context.Checkpoint();
            return new(invoice.Profile, Hash(Pdf), Hash(Xml), request, false, false, Array.Empty<string>());
        }
    }

    [Theory]
    [InlineData("EUR", "direct")]
    [InlineData("JPY", "direct")]
    [InlineData("BHD", "direct")]
    [InlineData("EUR", "csharp")]
    [InlineData("EUR", "visual-basic")]
    public void ActualArchivalValidatorAcceptsCandidateButIncompleteInvoiceValidationCannotExposeIt(string currency, string language)
    {
        var observer = new InvoiceObserver();
        using var output = new MemoryStream();
        output.WriteByte(42);
        var validator = ArchivalFixture.Providers().GetRequired<IPdfArchivalValidator>(PdfCapability.ValidateArchival);
        var failure = Assert.Throws<PdfException>(() =>
        {
            if (language == "csharp") ContractExamples.CSharp.ContractExample.WriteManagedInvoice(Data(currency), output, Body(), Layout, Providers(), ArchivalFixture.Icc(), "Original source sRGB fixture", validator, observer);
            else if (language == "visual-basic") ContractExamples.VisualBasic.ContractExample.WriteManagedInvoice(Data(currency), output, Body(), Layout, Providers(), ArchivalFixture.Icc(), "Original source sRGB fixture", validator, observer);
            else PdfInvoices.Write(Data(currency), output, Body(), Layout, Providers(), Producer(), validator, observer);
        });
        Assert.Equal("Invoice.IndependentValidation", failure.Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, output.ToArray());
        Assert.True(output.CanWrite);
        Assert.NotEmpty(observer.Pdf); // Invoice validation is reached only after actual complete PDF/A validation passes.
        using var source = new MemoryStream(observer.Pdf);
        using var document = Pdf.Open(source);
        var attachment = Assert.Single(PdfAttachments.Inspect(document.CreateSnapshot()).Items);
        using var xml = new MemoryStream();
        PdfAttachments.Extract(attachment, xml);
        Assert.Equal(observer.Xml, xml.ToArray());
        var metadata = PdfMetadata.Inspect(document.CreateSnapshot());
        var packet = XDocument.Parse(metadata.XmpXml!);
        XNamespace fx = "urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#", prop = "http://www.aiim.org/pdfa/ns/property#";
        Assert.Equal("INVOICE", packet.Descendants(fx + "DocumentType").Single().Value);
        Assert.Equal("factur-x.xml", packet.Descendants(fx + "DocumentFileName").Single().Value);
        Assert.Equal("MINIMUM", packet.Descendants(fx + "ConformanceLevel").Single().Value);
        Assert.Equal("1.0", packet.Descendants(fx + "Version").Single().Value);
        Assert.Equal(new[] { "DocumentType", "DocumentFileName", "Version", "ConformanceLevel" }, packet.Descendants(prop + "name").Select(p => p.Value));
        string text = string.Join("\n", Enumerable.Range(0, document.Pages.Count).Select(n => PdfTextExtraction.ReadPage(document.CreateSnapshot(), n).Text));
        Assert.Contains(Data(currency).GrandTotal.ToString("F" + Data(currency).MinorUnits, System.Globalization.CultureInfo.InvariantCulture) + " " + currency, text);
        string directory = Path.Combine(AppContext.BaseDirectory, "artifacts", "invoice-producer");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, currency + "-" + language + ".pdf"), observer.Pdf);
        File.WriteAllBytes(Path.Combine(directory, currency + "-" + language + ".xml"), observer.Xml);
    }

    [Fact]
    public void ExplicitColorAndIndependentValidatorsRemainRequired()
    {
        Assert.Throws<ArgumentNullException>(() => new ManagedPdfInvoiceProducer(null!, "RGB"));
        Assert.Throws<ArgumentException>(() => new ManagedPdfInvoiceProducer(PdfColorSpace.Rgb, "RGB"));
        Assert.False(Producer().Supports((PdfInvoiceProfile)99));
        using var output = new MemoryStream();
        Assert.Equal("Invoice.ProviderUnavailable", Assert.Throws<PdfException>(() => PdfInvoices.Write(Data(), output, Body(), Layout, Providers(), Producer())).Diagnostic.Code);
        Assert.Empty(output.ToArray());
    }

    [Fact]
    public void XmlMustMatchExactModelBeforePdfInputIsRead()
    {
        using var pdf = new MemoryStream(new byte[] { 1 });
        using var xml = new MemoryStream(Encoding.UTF8.GetBytes("<unrelated/>"));
        using var output = new MemoryStream();
        var failure = Assert.Throws<PdfException>(() => PdfFont.Run("InvoiceProducerFixture", "fixture", null, default, c => Producer().Convert(pdf, xml, Data(), output, c), PdfCapability.WriteDocument));
        Assert.Equal("Invoice.XmlModelMismatch", failure.Diagnostic.Code);
        Assert.Equal(0, pdf.Position);
        Assert.Empty(output.ToArray());
        Assert.True(xml.CanRead);
        Assert.True(output.CanWrite);
    }

    private sealed class DraftCapture : IPdfInvoiceProducer
    {
        internal byte[] Pdf = Array.Empty<byte>(), Xml = Array.Empty<byte>();
        public string EngineId => "fixture.draft-capture";
        public bool Supports(PdfInvoiceProfile profile) => true;
        public void Convert(Stream pdf, Stream xml, PdfInvoiceData invoice, Stream output, PdfOperationContext context)
        {
            using var p = new MemoryStream();
            using var x = new MemoryStream();
            pdf.CopyTo(p);
            xml.CopyTo(x);
            Pdf = p.ToArray();
            Xml = x.ToArray();
            throw new InvalidOperationException("Draft observation stops before validation/output.");
        }
    }

    [Theory]
    [InlineData("attachment", "Invoice.AssociatedXml")]
    [InlineData("metadata", "Invoice.MetadataUnsupported")]
    [InlineData("cancel", null)]
    [InlineData("limit", null)]
    public void WrongAttachmentExistingMetadataCancellationAndBoundsRejectWithoutCandidateOutput(string scenario, string? expected)
    {
            var captured = new DraftCapture();
            using var ignored = new MemoryStream();
            Assert.Throws<PdfException>(() => PdfInvoices.Write(Data(), ignored, Body(), Layout, Providers(), captured, ArchivalFixture.Providers().GetRequired<IPdfArchivalValidator>(PdfCapability.ValidateArchival), new InvoiceObserver()));
            using var output = new MemoryStream();
            using var cancel = new System.Threading.CancellationTokenSource();
            var failure = Record.Exception(() => PdfFont.Run("InvoiceProducerFixture", "fixture", null, cancel.Token, c =>
            {
                var data = Data();
                using var xmlInput = new MemoryStream(captured.Xml);
                using var raw = new MemoryStream(captured.Pdf);
                using var document = scenario == "attachment" ? Pdf.Create() : Pdf.Open(raw);
                if (scenario == "attachment") document.Pages.Add(new PdfPageSize(100, 100));
                using var prepared = scenario == "metadata" ? PdfMetadata.Edit(document.CreateSnapshot(), e => e.SetXmp("<rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'><rdf:Description rdf:about=''/></rdf:RDF>")) : null;
                using var pdf = new MemoryStream();
                (prepared ?? document).Save(pdf);
                pdf.Position = 0;
                if (scenario == "cancel") cancel.Cancel();
                if (scenario == "limit") c.Consume(PdfResourceKind.ExpandedBytes, c.Available(PdfResourceKind.ExpandedBytes));
                Producer().Convert(pdf, xmlInput, data, output, c);
            }, PdfCapability.WriteDocument));
            Assert.NotNull(failure);
            if (expected != null) Assert.Equal(expected, Assert.IsType<PdfException>(failure).Diagnostic.Code);
            if (scenario == "cancel") Assert.IsAssignableFrom<OperationCanceledException>(failure);
            Assert.Empty(output.ToArray());
    }
}
