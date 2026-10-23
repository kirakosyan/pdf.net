using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class InvoiceTests
{
    private static PdfInvoiceData Data(string currency = "EUR", MidpointRounding rounding = MidpointRounding.AwayFromZero) => new(PdfInvoiceProfile.FacturX107Minimum, "INV-1", "Seller & Co", "Buyer <One>", new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero), currency, rounding, new[] { new PdfInvoiceLine("Original item", 1, 1.005m, 20), new PdfInvoiceLine("Second item", 2, 3m, 5) });
    private static string Hash(byte[] bytes)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    private sealed class Producer : IPdfInvoiceProducer
    {
        internal byte[] Xml = Array.Empty<byte>(), Pdf = Array.Empty<byte>();
        internal PdfOperationContext? Context;
        internal Stream? XmlLease, PdfLease, OutputLease;
        internal bool SubstituteXml;
        public string EngineId => "fixture.invoice-producer";

        public bool Supports(PdfInvoiceProfile profile) => profile == PdfInvoiceProfile.FacturX107Minimum;
        public void Convert(Stream sourcePdf, Stream invoiceXml, PdfInvoiceData invoice, Stream candidatePdf, PdfOperationContext c)
        {
            Context = c;
            XmlLease = invoiceXml;
            PdfLease = sourcePdf;
            OutputLease = candidatePdf;
            using var xml = new MemoryStream();
            invoiceXml.CopyTo(xml);
            Xml = xml.ToArray();
            using var pdf = new MemoryStream();
            sourcePdf.CopyTo(pdf);
            Pdf = pdf.ToArray();
            if (SubstituteXml)
            {
                var old = Encoding.UTF8.GetBytes("INV-1");
                for (int n = 0; n < Pdf.Length - old.Length; n++)
                    if (Pdf[n] == old[0] && Pdf[n + 1] == old[1] && Pdf[n + 2] == old[2] && Pdf[n + 3] == old[3] && Pdf[n + 4] == old[4])
                    {
                        Pdf[n + 4] = (byte)'2';
                        break;
                    }
            }

            candidatePdf.Write(Pdf, 0, Pdf.Length);
            c.Checkpoint();
        }
    }

    private sealed class Archival : IPdfArchivalValidator
    {
        internal bool Pass = true;
        public PdfProviderDescriptor Descriptor { get; } = new("fixture.invoice-archival-binding-only", "1", PdfCapability.ValidateArchival);

        public PdfArchivalValidation Validate(PdfArchivalProfile profile, Stream input, PdfOperationContext c)
        {
            using var bytes = new MemoryStream();
            input.CopyTo(bytes);
            c.Checkpoint();
            return new(profile, Hash(bytes.ToArray()), true, Pass, Pass ? Array.Empty<PdfArchivalFinding>() : new[] { new PdfArchivalFinding("fixture.failure", "document", "No standard validation is performed by this fixture.") });
        }
    }

    private sealed class Validator : IPdfInvoiceValidator
    {
        internal bool Complete = true, Pass = true, WrongBinding;
        internal PdfOperationContext? Context;
        public string EngineId => "fixture.invoice-binding-only";

        public bool Supports(PdfInvoiceProfile profile) => true;
        public PdfInvoiceValidation Validate(Stream pdf, Stream xml, PdfInvoiceData invoice, string request, PdfOperationContext c)
        {
            Context = c;
            using var pb = new MemoryStream();
            using var xb = new MemoryStream();
            pdf.CopyTo(pb);
            xml.CopyTo(xb);
            c.Checkpoint();
            return new(invoice.Profile, WrongBinding ? new string ('0', 64) : Hash(pb.ToArray()), Hash(xb.ToArray()), request, Complete, Complete && Pass, Pass ? Array.Empty<string>() : new[] { "fixture.invoice-failure" });
        }
    }

    private static PdfInvoiceValidation Write(PdfInvoiceData data, Stream output, Producer? producer, Archival? archival, Validator? validator, PdfOperationPolicy? policy = null) => PdfInvoices.Write(data, output, new(new(TextTests.Read("business.ttf")), 10), new(new(new(new(0, 0, 500, 700))), PdfMargins.Uniform(20)), new(ManagedPdfProvider.Instance, new PdfSimpleTextShaper()), producer, archival, validator, policy);
    [Fact]
    public void CalculationsUseExplicitCurrencyMinorUnitsAndPerLineRounding()
    {
        var away = Data();
        var even = Data(rounding: MidpointRounding.ToEven);
        Assert.Equal(7.01m, away.NetTotal);
        Assert.Equal(7m, even.NetTotal);
        Assert.Equal(.5m, away.TaxTotal);
        Assert.Equal(7.51m, away.GrandTotal);
        Assert.Equal(0, Data("JPY").MinorUnits);
        Assert.Equal(3, Data("BHD").MinorUnits);
        Assert.Throws<ArgumentException>(() => Data("XYZ"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfInvoiceLine("Negative", 1, -1, 20));
    }

    [Theory]
    [InlineData(MidpointRounding.AwayFromZero, "0.05")]
    [InlineData(MidpointRounding.ToEven, "0.04")]
    public void TaxRoundsOnceOnEachRateCategory(MidpointRounding rounding, string expected)
    {
        var lines = Enumerable.Range(0, 3).Select(_ => new PdfInvoiceLine("Small", 1, .10m, 15)).Concat(new[] { new PdfInvoiceLine("Other", 1, .20m, 5) });
        var data = new PdfInvoiceData(PdfInvoiceProfile.FacturX107Minimum, "INV-1", "Seller", "Buyer", new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero), "EUR", rounding, lines);
        var category = Assert.Single(data.TaxCategories, c => c.TaxRate == 15);
        Assert.Equal(.30m, category.TaxableAmount);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), category.TaxAmount);
        Assert.Equal(data.TaxCategories.Sum(c => c.TaxAmount), data.TaxTotal);
        Assert.Equal(data.NetTotal + data.TaxTotal, data.GrandTotal);
        using var output = new MemoryStream();
        var producer = new Producer();
        Write(data, output, producer, new(), new());
        XNamespace ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
        var xml = XDocument.Parse(Encoding.UTF8.GetString(producer.Xml));
        Assert.Equal(data.TaxTotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), xml.Descendants(ram + "TaxTotalAmount").Single().Value);
    }

    [Fact]
    public void OneModelDrivesCiiReadableTotalsAndExactAssociatedXmlWithSharedValidationContext()
    {
        using var output = new MemoryStream();
        var producer = new Producer();
        var validator = new Validator();
        var result = Write(Data(), output, producer, new(), validator);
        Assert.True(result.IsCompliant);
        Assert.True(result.ArchivalValidation!.IsCompliant);
        Assert.Same(producer.Context, validator.Context);
        Assert.Throws<ObjectDisposedException>(() => producer.XmlLease!.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => producer.PdfLease!.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => producer.OutputLease!.WriteByte(0));
        var xml = XDocument.Parse(Encoding.UTF8.GetString(producer.Xml));
        XNamespace ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100", rsm = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";
        Assert.Equal(rsm + "CrossIndustryInvoice", xml.Root!.Name);
        Assert.NotNull(xml.Root.Element(rsm + "ExchangedDocument"));
        Assert.Equal("7.51", xml.Descendants(ram + "GrandTotalAmount").Single().Value);
        Assert.Equal("7.01", xml.Descendants(ram + "TaxBasisTotalAmount").Single().Value);
        Assert.Equal("Seller & Co", xml.Descendants(ram + "SellerTradeParty").Single().Element(ram + "Name")!.Value);
        output.Position = 0;
        using var doc = Pdf.Open(output);
        string text = string.Join("\n", Enumerable.Range(0, doc.Pages.Count).Select(n => PdfTextExtraction.ReadPage(doc.CreateSnapshot(), n).Text));
        Assert.Contains("7.51 EUR", text);
        Assert.Contains("7.01 EUR", text);
        var attachment = Assert.Single(PdfAttachments.Inspect(doc.CreateSnapshot()).Items);
        Assert.Equal("Data", attachment.Relationship);
        using var attached = new MemoryStream();
        PdfAttachments.Extract(attachment, attached);
        Assert.Equal(producer.Xml, attached.ToArray());
        string directory = Path.Combine(AppContext.BaseDirectory, "format-fixtures");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "invoice-source.xml"), producer.Xml);
        File.WriteAllBytes(Path.Combine(directory, "invoice-unqualified-draft.pdf"), producer.Pdf);
    }

    [Theory]
    [InlineData("missing", "Invoice.ProviderUnavailable")]
    [InlineData("archival", "Invoice.ArchivalValidation")]
    [InlineData("incomplete", "Invoice.IndependentValidation")]
    [InlineData("business", "Invoice.IndependentValidation")]
    [InlineData("binding", "Invoice.ValidatorBinding")]
    [InlineData("xml", "Invoice.AssociatedXml")]
    public void InvalidOrUnvalidatedInvoicesCannotPublishEvenOneByte(string failure, string code)
    {
        using var output = new MemoryStream();
        output.WriteByte(42);
        var producer = new Producer
        {
            SubstituteXml = failure == "xml"
        };
        var archival = new Archival
        {
            Pass = failure != "archival"
        };
        var validator = new Validator
        {
            Complete = failure != "incomplete",
            Pass = failure != "business",
            WrongBinding = failure == "binding"
        };
        Assert.Equal(code, Assert.Throws<PdfException>(() => Write(Data(), output, failure == "missing" ? null : producer, archival, validator)).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, output.ToArray());
    }
}
