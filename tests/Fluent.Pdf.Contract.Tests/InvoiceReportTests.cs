using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Fluent.Pdf.Compliance;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class InvoiceReportTests
{
    // Original report-protocol fixtures only. Real conformance/paint checks run in InvoiceValidationTests.
    private const string Request = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static PdfInvoiceData Data() => new PdfInvoiceData(PdfInvoiceProfile.FacturX107Minimum, "Id", "Seller", "Buyer", new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero), "EUR", MidpointRounding.ToEven, new[] {new PdfInvoiceLine("Item",1,1,0)}).WithSellerRegistration("Seller-1","FR");
    private static string Hash(Stream input)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
    }
    private static XDocument Report(Stream pdf, Stream xml, Stream model) => new(new XElement("invoiceValidation",
        new XAttribute("schemaVersion","1"),new XAttribute("engine","invoice.fx107-minimum.pdfbox3-saxon.v1"),new XAttribute("profile","Factur-X1.0.07-MINIMUM"),new XAttribute("bundleSha256",FacturX107MinimumReportValidator.RuleBundleSha256),new XAttribute("pdfSha256",Hash(pdf)),new XAttribute("xmlSha256",Hash(xml)),new XAttribute("modelSha256",Hash(model)),new XAttribute("requestSha256",Request),
        new[] {"schema","business","metadata","model","readable"}.Select(name => new XElement(name,new XAttribute("completed","true"),new XAttribute("compliant","true"),new XAttribute("checks","1")))));
    private static FacturX107MinimumReportValidator Provider(Action<XDocument>? change = null, Func<string,string>? wire = null, Action<Stream[]>? leases = null) => new((pdf,xml,model,output,c) =>
    {
        leases?.Invoke(new[] {pdf,xml,model,output});
        var report = Report(pdf,xml,model); change?.Invoke(report);
        byte[] bytes = Encoding.UTF8.GetBytes(wire?.Invoke(report.ToString()) ?? report.ToString());
        c.Consume(PdfResourceKind.ExpandedBytes,bytes.Length);
        output.Write(bytes,0,bytes.Length);
    });
    private static PdfInvoiceValidation Validate(FacturX107MinimumReportValidator provider, PdfOperationContext c)
    {
        using var pdf = new MemoryStream(new byte[]{1,2,3}); using var xml = new MemoryStream(new byte[]{4,5,6});
        return provider.Validate(pdf,xml,Data(),Request,c);
    }

    [Theory]
    [InlineData("missing","Invoice.ReportIncomplete")]
    [InlineData("duplicate","Invoice.ReportSchema")]
    [InlineData("count","Invoice.ReportSchema")]
    [InlineData("contradictory","Invoice.ReportIncomplete")]
    [InlineData("incompletePass","Invoice.ReportSchema")]
    [InlineData("finding","Invoice.ReportFinding")]
    [InlineData("bundle","Invoice.ReportBinding")]
    [InlineData("extra","Invoice.ReportBinding")]
    [InlineData("nested","Invoice.ReportSchema")]
    public void IncompleteContradictoryPrivateOrMismatchedReportsCannotAuthorizeOutput(string mode,string expected)
    {
        var provider = Provider(doc =>
        {
            var root=doc.Root!;
            switch(mode)
            {
                case "missing": root.Element("readable")!.Remove(); break;
                case "duplicate": root.Add(new XElement(root.Element("schema")!)); break;
                case "count": root.Element("model")!.SetAttributeValue("checks","1000001"); break;
                case "contradictory": root.Add(new XElement("finding",new XAttribute("code","Invoice.Readable"))); break;
                case "incompletePass": root.Element("readable")!.SetAttributeValue("completed","false"); break;
                case "finding": root.Add(new XElement("finding",new XAttribute("code","private invoice text"))); break;
                case "bundle": root.SetAttributeValue("bundleSha256",new string('0',64)); break;
                case "extra": root.SetAttributeValue("private","not allowed"); break;
                case "nested": root.Element("readable")!.Add(new XElement("nested")); break;
            }
        });
        Assert.Equal(expected,Assert.Throws<PdfException>(()=>PolicyTests.Run(c=>Validate(provider,c))).Diagnostic.Code);
    }

    [Fact]
    public void EntityExpansionAndReplayedReportsAreRejectedAndLeasesExpire()
    {
        Assert.Equal("Invoice.ReportXml",Assert.Throws<PdfException>(()=>PolicyTests.Run(c=>Validate(Provider(wire:s=>"<!DOCTYPE invoiceValidation [<!ENTITY secret SYSTEM 'file:///must-not-read'>]>"+s),c))).Diagnostic.Code);
        string? captured=null; Stream[]? leases=null;
        var provider=Provider(wire:s=>{if(captured==null)captured=s;return captured;},leases:streams=>leases=streams);
        PolicyTests.Run(c=>Assert.True(Validate(provider,c).IsCompliant));
        foreach(var lease in leases!) Assert.Throws<ObjectDisposedException>(()=>lease.ReadByte());
        Assert.Equal("Invoice.ReportBinding",Assert.Throws<PdfException>(()=>PolicyTests.Run(c=>Validate(provider,c))).Diagnostic.Code);
    }

    [Fact]
    public void UnsupportedReadableProfileStaysIncompleteAndStableFindingsRemainAvailable()
    {
        var provider=Provider(doc=>
        {
            var phase=doc.Root!.Element("readable")!;phase.SetAttributeValue("completed","false");phase.SetAttributeValue("compliant","false");
            doc.Root.Add(new XElement("finding",new XAttribute("code","Invoice.PaintProfile")));
        });
        PolicyTests.Run(c=>{var result=Validate(provider,c);Assert.False(result.Completed);Assert.False(result.IsCompliant);Assert.Equal(new[]{"Invoice.PaintProfile"},result.Findings);});
    }

    [Fact]
    public void BudgetAndSwallowedCancellationFailuresRemainLatched()
    {
        Assert.Equal("Limit.ExpandedBytes",Assert.Throws<PdfException>(()=>PolicyTests.Run(c=>Validate(Provider(),c),new(maximumExpandedBytes:64))).Diagnostic.Code);
        using var cancel=new CancellationTokenSource();
        var provider=new FacturX107MinimumReportValidator((pdf,xml,model,output,c)=>{cancel.Cancel();try{c.Checkpoint();}catch(OperationCanceledException){}});
        Assert.Equal(cancel.Token,Assert.Throws<OperationCanceledException>(()=>PdfFont.Run("InvoiceReport","fixture.invoice-report",null,cancel.Token,c=>Validate(provider,c))).CancellationToken);
    }
}
