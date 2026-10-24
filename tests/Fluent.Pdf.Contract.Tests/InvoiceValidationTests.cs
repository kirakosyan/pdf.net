using System.IO;
using System.Text;
using System.Xml.Linq;
using Fluent.Pdf.Compliance;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class InvoiceValidationTests
{
    private static PdfInvoiceData Data(string currency = "EUR") => new PdfInvoiceData(PdfInvoiceProfile.FacturX107Minimum, "INDEPENDENT-1", "Original Seller", "Original Buyer", new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero), currency, MidpointRounding.AwayFromZero, new[] { new PdfInvoiceLine("Original item", 1, 10m, 20) }).WithSellerRegistration("Original-Seller-001", "FR");
    private static PdfInvoiceValidation Write(PdfInvoiceData data, Stream output, IPdfInvoiceValidator validator, IPdfInvoiceProducer? producer = null)
        => PdfInvoices.Write(data, output, new(new(TextTests.Read("business.ttf")), 10), new(new(new(new(0, 0, 500, 700))), PdfMargins.Uniform(20)), new(ManagedPdfProvider.Instance, new PdfSimpleTextShaper()), producer ?? new ManagedPdfInvoiceProducer(ArchivalFixture.Icc(), "Original independent invoice fixture"), ArchivalFixture.Providers().GetRequired<IPdfArchivalValidator>(PdfCapability.ValidateArchival), validator, new(limits: new(maximumExpandedBytes: 2147483648, maximumElapsedTime: TimeSpan.FromMinutes(2))));

    [Theory]
    [InlineData("EUR")]
    [InlineData("JPY")]
    [InlineData("BHD")]
    public void ExactEditionRulesAndIndependentPackageArithmeticAndReadablePaintPermitOnlyValidatedOutput(string currency)
    {
        using var output = new MemoryStream();
        var result = Write(Data(currency), output, InvoiceValidationFixture.Validator());
        Assert.True(result.Completed); Assert.True(result.IsCompliant); Assert.True(result.ArchivalValidation!.IsCompliant); Assert.NotEmpty(output.ToArray()); Assert.True(output.CanWrite);
        string directory = Path.Combine(AppContext.BaseDirectory, "artifacts", "validated-invoices"); Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, currency + ".pdf"), output.ToArray());
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("visual-basic")]
    public void CompiledConsumerUsesBothActualValidatorsAndExplicitSharedHostBudget(string language)
    {
        using var output=new MemoryStream();
        var data=Data();var body=new PdfFlowTextStyle(new(TextTests.Read("business.ttf")),10);var layout=new PdfFlowOptions(new(new(new(0,0,500,700))),PdfMargins.Uniform(20));var providers=new PdfProviderRegistry(ManagedPdfProvider.Instance,new PdfSimpleTextShaper());
        var archival=ArchivalFixture.Providers().GetRequired<IPdfArchivalValidator>(PdfCapability.ValidateArchival);var invoice=InvoiceValidationFixture.Validator();var policy=new PdfOperationPolicy(limits:new(maximumExpandedBytes:2147483648,maximumElapsedTime:TimeSpan.FromMinutes(2)));
        var result=language=="csharp" ? ContractExamples.CSharp.ContractExample.WriteValidatedInvoice(data,output,body,layout,providers,ArchivalFixture.Icc(),"Original independent invoice fixture",archival,invoice,policy) : ContractExamples.VisualBasic.ContractExample.WriteValidatedInvoice(data,output,body,layout,providers,ArchivalFixture.Icc(),"Original independent invoice fixture",archival,invoice,policy);
        Assert.True(result.IsCompliant);Assert.True(result.ArchivalValidation!.IsCompliant);
        string directory=Path.Combine(AppContext.BaseDirectory,"artifacts","validated-invoices");Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,"EUR-"+language+".pdf"),output.ToArray());
    }

    [Theory]
    [InlineData(MidpointRounding.AwayFromZero,"7.51")]
    [InlineData(MidpointRounding.ToEven,"7.50")]
    public void IndependentDecimalArithmeticCoversPerLineAndPerCategoryRounding(MidpointRounding rounding,string gross)
    {
        var data=new PdfInvoiceData(PdfInvoiceProfile.FacturX107Minimum,"ROUND-1","Original Seller","Original Buyer",Data().IssueTime,"EUR",rounding,new[]{new PdfInvoiceLine("First",1,1.005m,20),new PdfInvoiceLine("Second",2,3m,5)}).WithSellerRegistration("Original-Seller-001","FR");
        using var output=new MemoryStream();Assert.True(Write(data,output,InvoiceValidationFixture.Validator()).IsCompliant);
        Assert.Equal(gross,data.GrandTotal.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("seller", "Invoice.ReportBinding")]
    [InlineData("schema", "Invoice.ReportBinding")]
    [InlineData("country", "Invoice.ReportBinding")]
    [InlineData("total", "Invoice.ReportBinding")]
    [InlineData("binding", "Invoice.ReportBinding")]
    public void IndependentRuleModelAndExactInputFailuresPreserveDestination(string mode, string expected)
    {
        var validator = InvoiceValidationFixture.Validator(changeInputs: directory =>
        {
            if (mode == "binding") return;
            string path = Path.Combine(directory, "invoice.xml"); var xml = XDocument.Load(path); XNamespace ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
            if (mode == "seller") xml.Descendants(ram + "SellerTradeParty").Single().Element(ram + "SpecifiedLegalOrganization")!.Element(ram + "ID")!.Remove();
            if (mode == "schema") xml.Descendants(ram + "TaxBasisTotalAmount").Single().Remove();
            if (mode == "country") xml.Descendants(ram + "CountryID").Single().Value = "ZZ";
            if (mode == "total") xml.Descendants(ram + "GrandTotalAmount").Single().Value = "12.01";
            xml.Save(path);
        }, changeReport: directory =>
        {
            string path = Path.Combine(directory, "report.xml"); var report = XDocument.Load(path); report.Root!.SetAttributeValue("modelSha256", new string('0',64)); report.Save(path);
            if(mode!="binding")
            {
                string phase=mode=="schema" ? "schema" : mode is "seller" or "country" ? "business" : "model";
                Assert.Equal("false",(string?)report.Root.Element(phase)!.Attribute("compliant"));
                Assert.Equal("false",(string?)report.Root.Element("metadata")!.Attribute("compliant"));
            }
        });
        using var output = new MemoryStream(); output.WriteByte(42);
        // Modified captured XML is deliberately no longer bound to the SDK input and must fail before any result can be trusted.
        Assert.Equal(expected, Assert.Throws<PdfException>(() => Write(Data(), output, validator)).Diagnostic.Code);
        Assert.Equal(new byte[] {42}, output.ToArray());
    }

    [Fact]
    public void ActualBusinessCodeListsRejectInvalidSellerCountryWithoutPublishing()
    {
        using var output = new MemoryStream(); output.WriteByte(42);
        var failure = Assert.Throws<PdfException>(() => Write(Data().WithSellerRegistration("Original-Seller-001", "ZZ"), output, InvoiceValidationFixture.Validator()));
        Assert.Equal("Invoice.IndependentValidation", failure.Diagnostic.Code);
        Assert.Contains("FX-SCH-A-", Assert.IsType<string>(failure.Data["Fluent.Pdf.InvoiceFindings"]));
        Assert.Equal(new byte[] {42}, output.ToArray());
    }

    private sealed class MutantProducer(string mode) : IPdfInvoiceProducer
    {
        public string EngineId => "fixture.original-invoice-paint-mutant";
        public bool Supports(PdfInvoiceProfile profile) => profile == PdfInvoiceProfile.FacturX107Minimum;
        public void Convert(Stream source, Stream xml, PdfInvoiceData invoice, Stream output, PdfOperationContext c)
        {
            using var candidate = new PdfCodecBufferStream(c, 67108864);
            new ManagedPdfInvoiceProducer(ArchivalFixture.Icc(), "Original independent invoice fixture").Convert(source, xml, invoice, candidate, c);
            using var document = PdfPageOrganization.Read(candidate.Finish(), 1000, c);
            PdfDocumentSnapshot snapshot; using(document.UseModelContext(c)) snapshot = document.CreateSnapshot();
            using var graph = new PdfGraphEdit(snapshot, new(), c);
            string prefix = mode == "invisible" ? "q 3 Tr\n" : "q 25 0 475 700 re W n\n";
            var first = Add(prefix); var last = Add("Q\n");
            var id = graph.Snapshot.Pages[0].Source!.ObjectId; var page = graph.Copy(graph.Edit.Resolve(id)); var original = page["Contents"];
            var middle = graph.Resolve(original); var content = middle.Kind == PdfValueKind.Array ? middle.Items : new[] { original };
            page["Contents"] = PdfValue.Array(new[] { first }.Concat(content).Concat(new[] {last}).ToArray());
            graph.Edit.Set(id, PdfValue.Dictionary(page));
            // Test-only mutation still must pass the real archival validator before the invoice validator is reached.
            graph.Edit.WriteCore(output, new(version:"1.7"), c, validatedArchivalDowngrade:true);
            PdfValue Add(string text)
            {
                c.Consume(PdfResourceKind.ExpandedBytes, 1024); using var input = new MemoryStream(Encoding.ASCII.GetBytes(text)); var bytes = PdfByteStore.Read(input,c);
                return PdfValue.Reference(graph.Edit.Add(PdfValue.StreamValue(PdfValue.Dictionary(Array.Empty<KeyValuePair<string,PdfValue>>()),bytes,0,bytes.Length)));
            }
        }
    }

    [Theory]
    [InlineData("invisible")]
    [InlineData("clipped")]
    public void PdfACompliantInvisibleOrPointScaleClippedTextCannotPassReadableReconciliation(string mode)
    {
        using var output = new MemoryStream(); output.WriteByte(42);
        var failure = Assert.Throws<PdfException>(() => Write(Data(),output,InvoiceValidationFixture.Validator(),new MutantProducer(mode)));
        Assert.Equal("Invoice.IndependentValidation",failure.Diagnostic.Code);
        Assert.Contains("Invoice.Readable",Assert.IsType<string>(failure.Data["Fluent.Pdf.InvoiceFindings"]));
        Assert.Equal(new byte[]{42},output.ToArray());
    }

    [Fact]
    public void SellerIdentityAndUnrepresentableHeaderPrecisionFailBeforeProduction()
    {
        Assert.Throws<ArgumentException>(() => Data().WithSellerRegistration("Id", "fr"));
        var missing = new PdfInvoiceData(PdfInvoiceProfile.FacturX107Minimum, "ID", "Seller", "Buyer", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "EUR", MidpointRounding.AwayFromZero, new[] {new PdfInvoiceLine("Line",1,1,0)});
        using var output = new MemoryStream();
        Assert.Equal("Invoice.SellerIdentityRequired", Assert.Throws<PdfException>(() => Write(missing, output, InvoiceValidationFixture.Validator())).Diagnostic.Code);
        var precise = new PdfInvoiceData(PdfInvoiceProfile.FacturX107Minimum, "ID", "Seller", "Buyer", missing.IssueTime, "BHD", MidpointRounding.AwayFromZero, new[] {new PdfInvoiceLine("Line",1,1.005m,0)}).WithSellerRegistration("Seller-001","FR");
        Assert.Equal("Invoice.ProfilePrecision", Assert.Throws<PdfException>(() => Write(precise, output, InvoiceValidationFixture.Validator())).Diagnostic.Code);
        Assert.Equal(1.005m,precise.NetTotal); Assert.Empty(output.ToArray());
    }
}
