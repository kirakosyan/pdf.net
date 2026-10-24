using System.IO;
using System.Xml.Linq;

namespace Fluent.Pdf;

/// <summary>Prepares the SDK's exact MINIMUM invoice draft with explicit ICC output and invoice/PDF-A metadata. Independent archival and invoice validation remain mandatory in PdfInvoices.Write.</summary>
public sealed class ManagedPdfInvoiceProducer : IPdfInvoiceProducer
{
    private readonly PdfArchivalOptions _options;

    /// <summary>Captures an explicit RGB/CMYK ICC output space and output-condition identity. No color profile, font, validator or native engine is discovered.</summary>
    public ManagedPdfInvoiceProducer(PdfColorSpace outputColorSpace, string outputCondition)
    {
        _options = new(PdfArchivalProfile.A3B, outputColorSpace ?? throw new ArgumentNullException(nameof(outputColorSpace)), outputCondition);
    }

    /// <summary>Gets the original managed producer identity, distinct from independent validators.</summary>
    public string EngineId => "managed.invoice-minimum.v1";

    /// <summary>Returns true only for the explicit Factur-X1.0.07 MINIMUM source profile; this is not production qualification.</summary>
    public bool Supports(PdfInvoiceProfile profile) => profile == PdfInvoiceProfile.FacturX107Minimum;

    /// <summary>Prepares an unvalidated private candidate from the SDK-generated draft. Exact XML/model/attachment binding is required, existing XMP rejects, and caller streams remain open. Use PdfInvoices.Write to enforce complete independent validation before exposing invoice output.</summary>
    public void Convert(Stream sourcePdf, Stream invoiceXml, PdfInvoiceData invoice, Stream candidatePdf, PdfOperationContext context)
    {
        if (sourcePdf == null || invoiceXml == null || invoice == null || candidatePdf == null || context == null)
            throw new ArgumentNullException(sourcePdf == null ? nameof(sourcePdf) : invoiceXml == null ? nameof(invoiceXml) : invoice == null ? nameof(invoice) : candidatePdf == null ? nameof(candidatePdf) : nameof(context));
        if (!sourcePdf.CanRead || !invoiceXml.CanRead || !candidatePdf.CanWrite)
            throw new ArgumentException("Readable draft/XML and writable borrowed candidate streams are required.");
        context.Checkpoint();
        if (!Supports(invoice.Profile))
            throw Error(context, "ProfileUnavailable", "The exact invoice profile is not supported by this managed producer.");
        var xml = PdfProfileIo.Capture(invoiceXml, context, 1048576);
        string xmlHash = PdfArchival.Digest(xml, context);
        if (xmlHash != PdfArchival.Digest(PdfInvoices.Xml(invoice, context), context))
            throw Error(context, "XmlModelMismatch", "The supplied XML does not match the exact SDK-generated invoice model.");
        var source = PdfProfileIo.Capture(sourcePdf, context, 67108864);
        using var document = PdfPageOrganization.Read(source, 10000, context);
        if (document.Pages.Count == 0)
            throw Error(context, "EmptyOutput", "A nonempty SDK invoice draft is required.");
        PdfInvoices.CheckAttachment(document, xmlHash, context);
        PdfDocumentSnapshot snapshot;
        using (document.UseModelContext(context))
            snapshot = document.CreateSnapshot();
        using var graph = new PdfGraphEdit(snapshot, new(), context);
        if (graph.Get(graph.Catalog, "Metadata").Kind != PdfValueKind.Null)
            throw Error(context, "MetadataUnsupported", "This producer accepts SDK drafts without existing XMP; metadata replacement requires an explicit separate workflow.");
        context.Consume(PdfResourceKind.Objects, 96);
        context.Consume(PdfResourceKind.ExpandedBytes, 32768);
        var editor = new PdfMetadataEditor(graph);
        try
        {
            editor.SetXmp(Packet());
            editor.SetInformation("Title", "Invoice " + invoice.Identifier);
            editor.SetInformation("Author", invoice.Seller);
            editor.SetCreationTime(invoice.IssueTime);
            editor.SetModificationTime(invoice.IssueTime);
            editor.Commit();
        }
        finally
        {
            editor.Expire();
        }
        var candidate = PdfArchival.PrepareCore(graph, _options);
        context.Checkpoint();
        PdfProfileIo.Copy(candidate, candidatePdf, context);
    }

    private static string Packet()
    {
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#", fx = "urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#";
        XNamespace extension = "http://www.aiim.org/pdfa/ns/extension/", schema = "http://www.aiim.org/pdfa/ns/schema#", property = "http://www.aiim.org/pdfa/ns/property#";
        var fields = new[]
        {
            (Name: "DocumentType", Value: "INVOICE", Description: "Type of the associated business document"),
            (Name: "DocumentFileName", Value: "factur-x.xml", Description: "Name of the associated invoice XML file"),
            (Name: "Version", Value: "1.0", Description: "Version of the invoice XML schema"),
            (Name: "ConformanceLevel", Value: "MINIMUM", Description: "Invoice XML conformance level")
        };
        return new XElement(XName.Get("xmpmeta", "adobe:ns:meta/"),
            new XElement(rdf + "RDF", new XAttribute(XNamespace.Xmlns + "rdf", rdf),
                new XElement(rdf + "Description", new XAttribute(rdf + "about", ""), new XAttribute(XNamespace.Xmlns + "fx", fx),
                    fields.Select(f => new XElement(fx + f.Name, f.Value))),
                new XElement(rdf + "Description", new XAttribute(rdf + "about", ""),
                    new XAttribute(XNamespace.Xmlns + "pdfaExtension", extension), new XAttribute(XNamespace.Xmlns + "pdfaSchema", schema), new XAttribute(XNamespace.Xmlns + "pdfaProperty", property),
                    new XElement(extension + "schemas", new XElement(rdf + "Bag", new XElement(rdf + "li", new XAttribute(rdf + "parseType", "Resource"),
                        new XElement(schema + "schema", "Factur-X invoice extension schema"), new XElement(schema + "namespaceURI", fx.NamespaceName), new XElement(schema + "prefix", "fx"),
                        new XElement(schema + "property", new XElement(rdf + "Seq", fields.Select(f =>
                            new XElement(rdf + "li", new XAttribute(rdf + "parseType", "Resource"), new XElement(property + "name", f.Name), new XElement(property + "valueType", "Text"),
                                new XElement(property + "category", "external"), new XElement(property + "description", f.Description))))))))))).ToString(SaveOptions.DisableFormatting);
    }

    private static PdfException Error(PdfOperationContext context, string code, string message) => PdfWriterCore.Error(context, "Invoice." + code, message);
}
