using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

namespace Fluent.Pdf;
/// <summary>One-source invoice calculation, CII/human composition and mandatory complete independent PDF/A/invoice validation gates.</summary>
public static class PdfInvoices
{
    /// <summary>Stages all layout/XML/attachment/producer/validation phases under one context. No PDF is exposed until exact associated XML, complete PDF/A-3b and invoice-schema/business/readable-total validations all pass. The managed draft producer is optional; complete independent validators remain explicit.</summary>
    public static PdfInvoiceValidation Write(PdfInvoiceData invoice, Stream output, PdfFlowTextStyle body, PdfFlowOptions layout, PdfProviderRegistry providers, IPdfInvoiceProducer? producer = null, IPdfArchivalValidator? archivalValidator = null, IPdfInvoiceValidator? invoiceValidator = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (invoice == null || body == null || layout == null || providers == null)
            throw new ArgumentNullException(invoice == null ? nameof(invoice) : body == null ? nameof(body) : layout == null ? nameof(layout) : nameof(providers));
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed invoice output is required.", nameof(output));
        PdfInvoiceValidation? result = null;
        PdfArchivalValidation? archival = null;
        var finish = new List<Action<PdfOperationReport>>();
        var report = PdfFont.Run("WriteInvoice", "managed.invoice-gate", policy, cancellationToken, c =>
        {
            if (producer == null || archivalValidator == null || invoiceValidator == null)
                throw Error(c, "ProviderUnavailable", "Explicit complete invoice production, independent PDF/A-3b and invoice/schema/business validation are required before publication.");
            string pi = Identity(producer.EngineId, c), vi = Identity(invoiceValidator.EngineId, c);
            if (pi == vi || ReferenceEquals(producer, invoiceValidator) || ReferenceEquals(producer, archivalValidator) || pi == archivalValidator.Descriptor.Id)
                throw Error(c, "IndependentValidatorRequired", "A separately identified complete invoice validator is required.");
            bool supported;
            try
            {
                supported = producer.Supports(invoice.Profile) && invoiceValidator.Supports(invoice.Profile);
            }
            catch
            {
                c.Checkpoint();
                throw Error(c, "ProviderFailed", "The exact invoice provider prerequisite check failed.");
            }

            if (!supported)
                throw Error(c, "ProfileUnavailable", "The exact invoice version/profile or local prerequisites are unavailable.");
            _ = new PdfProviderRegistry(archivalValidator).GetRequired<IPdfArchivalValidator>(PdfCapability.ValidateArchival);
            c.Report(new("Invoice.ProviderIdentity", "Explicit producer: " + pi + "; independent invoice validator: " + vi + "; archival validator: " + archivalValidator.Descriptor.Id + " version " + archivalValidator.Descriptor.Version + ".", severity: PdfDiagnosticSeverity.Information));
            var xml = Xml(invoice, c);
            string xmlHash = PdfArchival.Digest(xml, c), request = Request(invoice, xmlHash, c);
            var draft = Draft(invoice, xml, body, layout, providers, c, finish);
            using var staged = new PdfCodecBufferStream(c, 67108864);
            try
            {
                using var pdf = draft.Open(0, draft.Length);
                using var xmlSource = xml.Open(0, xml.Length);
                using var pl = new BorrowedStream(pdf, true, c);
                using var xl = new BorrowedStream(xmlSource, true, c);
                using var ol = new BorrowedStream(staged, false, c);
                producer.Convert(pl, xl, invoice, ol, c);
                c.Checkpoint();
            }
            catch
            {
                c.Checkpoint();
                throw Error(c, "ProducerFailed", "The configured invoice producer failed without exposing partial output.");
            }

            var candidate = staged.Finish();
            using var doc = PdfPageOrganization.Read(candidate, Math.Min(layout.MaximumPages, 10000), c);
            if (doc.Pages.Count == 0)
                throw Error(c, "EmptyOutput", "A nonempty valid invoice PDF is required.");
            CheckAttachment(doc, xmlHash, c);
            archival = PdfArchival.ValidateCore(candidate, PdfArchivalProfile.A3B, archivalValidator, c);
            if (!archival.Completed || !archival.IsCompliant)
                throw Error(c, "ArchivalValidation", "Complete independent PDF/A-3b validation failed; no invoice output was exposed.");
            string pdfHash = PdfArchival.Digest(candidate, c);
            try
            {
                using var pdf = candidate.Open(0, candidate.Length);
                using var xmlSource = xml.Open(0, xml.Length);
                using var pl = new BorrowedStream(pdf, true, c);
                using var xl = new BorrowedStream(xmlSource, true, c);
                result = invoiceValidator.Validate(pl, xl, invoice, request, c);
                c.Checkpoint();
            }
            catch
            {
                c.Checkpoint();
                throw Error(c, "ValidatorFailed", "The configured independent invoice validator failed.");
            }

            if (result == null || result.Profile != invoice.Profile || result.PdfSha256 != pdfHash || result.XmlSha256 != xmlHash || result.RequestSha256 != request)
                throw Error(c, "ValidatorBinding", "The invoice result belongs to different PDF/XML/model settings.");
            c.Consume(PdfResourceKind.Objects, result.Findings.Count + 1L);
            foreach (string value in result.Findings)
                c.Consume(PdfResourceKind.ExpandedBytes, value.Length * 4L + 256);
            if (!result.Completed || !result.IsCompliant)
                throw Error(c, "IndependentValidation", "Complete invoice schema/business/readable-total validation did not pass.");
            result = new(result.Profile, result.PdfSha256, result.XmlSha256, result.RequestSha256, result.Completed, result.IsCompliant, result.Findings);
            PdfProfileIo.Copy(candidate, output, c);
        }, PdfCapability.WriteDocument);
        foreach (var complete in finish)
            complete(report);
        archival!.OperationReport = report;
        result!.ArchivalValidation = archival;
        result.OperationReport = report;
        return result;
    }

    internal static PdfByteStore Xml(PdfInvoiceData invoice, PdfOperationContext c)
    {
        const string rsm = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100", ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100", udt = "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100";
        using var output = new PdfCodecBufferStream(c, 1048576);
        c.Consume(PdfResourceKind.Objects, 64);
        c.Consume(PdfResourceKind.ExpandedBytes, 16384 + (invoice.Identifier.Length + invoice.Seller.Length + invoice.Buyer.Length) * 8L);
        using (var w = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false, true), CloseOutput = false }))
        {
            w.WriteStartElement("rsm", "CrossIndustryInvoice", rsm);
            w.WriteAttributeString("xmlns", "ram", null, ram);
            w.WriteAttributeString("xmlns", "udt", null, udt);
            Start("ExchangedDocumentContext");
            Start("GuidelineSpecifiedDocumentContextParameter");
            Text("ID", "urn:factur-x.eu:1p0:minimum");
            End();
            End();
            Start("ExchangedDocument");
            Text("ID", invoice.Identifier);
            Text("TypeCode", "380");
            Start("IssueDateTime");
            w.WriteStartElement("udt", "DateTimeString", udt);
            w.WriteAttributeString("format", "102");
            w.WriteString(invoice.IssueTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            End();
            End();
            End();
            Start("SupplyChainTradeTransaction");
            Start("ApplicableHeaderTradeAgreement");
            Start("SellerTradeParty");
            Text("Name", invoice.Seller);
            End();
            Start("BuyerTradeParty");
            Text("Name", invoice.Buyer);
            End();
            End();
            Start("ApplicableHeaderTradeDelivery");
            End();
            Start("ApplicableHeaderTradeSettlement");
            Text("InvoiceCurrencyCode", invoice.Currency);
            Start("SpecifiedTradeSettlementHeaderMonetarySummation");
            Text("TaxBasisTotalAmount", invoice.Money(invoice.NetTotal));
            Start("TaxTotalAmount");
            w.WriteAttributeString("currencyID", invoice.Currency);
            w.WriteString(invoice.Money(invoice.TaxTotal));
            End();
            Text("GrandTotalAmount", invoice.Money(invoice.GrandTotal));
            Text("DuePayableAmount", invoice.Money(invoice.GrandTotal));
            End();
            End();
            End();
            End();
            w.Flush();
            void Start(string name)
            {
                c.Checkpoint();
                bool documentElement = name == "ExchangedDocumentContext" || name == "ExchangedDocument" || name == "SupplyChainTradeTransaction";
                w.WriteStartElement(documentElement ? "rsm" : "ram", name, documentElement ? rsm : ram);
            }

            void Text(string name, string value)
            {
                Start(name);
                w.WriteString(value);
                End();
            }

            void End() => w.WriteEndElement();
        }

        return output.Finish();
    }

    private static PdfByteStore Draft(PdfInvoiceData data, PdfByteStore xml, PdfFlowTextStyle body, PdfFlowOptions layout, PdfProviderRegistry providers, PdfOperationContext c, List<Action<PdfOperationReport>> finish)
    {
        c.Consume(PdfResourceKind.Objects, data.Lines.Count + 16L);
        c.Consume(PdfResourceKind.ExpandedBytes, 16384 + (data.Identifier.Length + data.Seller.Length + data.Buyer.Length) * 8L);
        var blocks = new List<PdfFlowBlock>
        {
            new PdfFlowParagraph("Invoice " + data.Identifier, body),
            new PdfFlowParagraph("Seller: " + data.Seller, body),
            new PdfFlowParagraph("Buyer: " + data.Buyer, body),
            new PdfFlowParagraph("Date: " + data.IssueTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), body)
        };
        for (int n = 0; n < data.Lines.Count; n++)
        {
            c.Checkpoint();
            var line = data.Lines[n];
            c.Consume(PdfResourceKind.ExpandedBytes, line.Description.Length * 8L + 4096);
            blocks.Add(new PdfFlowParagraph(line.Description + " | Quantity " + line.Quantity.ToString(CultureInfo.InvariantCulture) + " | Unit " + line.UnitPrice.ToString(CultureInfo.InvariantCulture) + " | Net " + data.Money(data.LineNetAmounts[n]) + " | Tax rate " + line.TaxRate.ToString(CultureInfo.InvariantCulture) + "% " + data.Currency, body));
        }

        foreach (var category in data.TaxCategories)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.ExpandedBytes, 4096);
            blocks.Add(new PdfFlowParagraph("Tax rate " + category.TaxRate.ToString(CultureInfo.InvariantCulture) + "% | Basis " + data.Money(category.TaxableAmount) + " | Tax " + data.Money(category.TaxAmount) + " " + data.Currency, body));
        }

        blocks.Add(new PdfFlowParagraph("Net total " + data.Money(data.NetTotal) + " " + data.Currency, body));
        blocks.Add(new PdfFlowParagraph("Tax total " + data.Money(data.TaxTotal) + " " + data.Currency, body));
        blocks.Add(new PdfFlowParagraph("Grand total / Due " + data.Money(data.GrandTotal) + " " + data.Currency, body));
        var flow = PdfFlow.LayoutCore(blocks, layout, providers, c, out var complete);
        finish.Add(complete);
        using var document = flow.CreateDocumentCore(new(maximumPages: layout.MaximumPages, operationPolicy: c.Policy), providers, c, finish);
        PdfDocumentSnapshot snapshot;
        using (document.UseModelContext(c))
            snapshot = document.CreateSnapshot();
        using var graph = new PdfGraphEdit(snapshot, new(), c);
        c.Consume(PdfResourceKind.Objects, 16);
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        using var xmlSource = xml.Open(0, xml.Length);
        var bytes = PdfAttachmentGraph.Buffer(xmlSource, c, out var checksum);
        var parameters = PdfGraphicsNumbers.Dict(("Size", PdfValue.Integer(bytes.Length)), ("CheckSum", PdfValue.String(checksum)), ("ModDate", PdfTextStrings.Encode(PdfMetadataDates.Format(data.IssueTime), c)));
        var stream = PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("EmbeddedFile")), ("Subtype", PdfValue.Name("application/xml")), ("Params", parameters));
        var payload = PdfValue.Reference(graph.Edit.Add(PdfValue.StreamValue(stream, bytes, 0, bytes.Length)));
        var file = PdfValue.Reference(graph.Edit.Add(PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Filespec")), ("F", PdfValue.String(Encoding.ASCII.GetBytes("factur-x.xml"))), ("UF", PdfTextStrings.Encode("factur-x.xml", c)), ("AFRelationship", PdfValue.Name("Data")), ("EF", PdfGraphicsNumbers.Dict(("F", payload), ("UF", payload))))));
        PdfAttachmentGraph.SetNames(graph, new() { (PdfTextStrings.Encode("factur-x.xml", c), file) });
        var catalog = graph.Copy(graph.Catalog);
        catalog["AF"] = PdfValue.Array(file);
        graph.SetCatalog(catalog);
        using var output = new PdfCodecBufferStream(c, 67108864);
        graph.Edit.WriteCore(output, new(version: "2.0"), c);
        return output.Finish();
    }

    internal static void CheckAttachment(PdfDocument doc, string xmlHash, PdfOperationContext c)
    {
        PdfDocumentSnapshot snapshot;
        using (doc.UseModelContext(c))
            snapshot = doc.CreateSnapshot();
        using var graph = new PdfGraphEdit(snapshot, new(), c);
        var names = PdfAttachmentGraph.Names(graph);
        var af = graph.Get(graph.Catalog, "AF");
        if (names.Count != 1 || af.Kind != PdfValueKind.Array || af.Items.Count != 1 || names[0].Value.Kind != PdfValueKind.Reference || af.Items[0].Kind != PdfValueKind.Reference || names[0].Value.ReferenceId != af.Items[0].ReferenceId || PdfTextStrings.Decode(names[0].Key, c) != "factur-x.xml")
            throw Error(c, "AssociatedXml", "Exactly one identity-preserving named/associated invoice XML is required.");
        var file = graph.Resolve(names[0].Value);
        if (file.Kind != PdfValueKind.Dictionary || PdfAttachmentGraph.Name(graph, file, "AFRelationship") != "Data" || PdfTextStrings.Decode(graph.Get(file, "UF"), c) != "factur-x.xml" || PdfTextStrings.Decode(graph.Get(file, "F"), c) != "factur-x.xml")
            throw Error(c, "AssociatedXml", "The MINIMUM profile requires the exact XML filename and Data relationship.");
        var ef = graph.Get(file, "EF");
        if (ef.Kind != PdfValueKind.Dictionary || !ef.Entries.TryGetValue("F", out var f) || !ef.Entries.TryGetValue("UF", out var uf) || f.Kind != PdfValueKind.Reference || uf.Kind != PdfValueKind.Reference || f.ReferenceId != uf.ReferenceId)
            throw Error(c, "AssociatedXml", "Filename variants must reference one exact associated XML stream.");
        var stream = graph.Resolve(f);
        if (stream.Kind != PdfValueKind.Stream || PdfAttachmentGraph.Name(graph, stream, "Type") != "EmbeddedFile" || PdfAttachmentGraph.Name(graph, stream, "Subtype") != "application/xml")
            throw Error(c, "AssociatedXml", "An application/xml embedded stream is required.");
        using var decoded = PdfFilters.Decode(PdfFilterDeclarations.Resolve(stream, id => graph.Resolve(PdfValue.Reference(id)), c), c);
        if (PdfArchival.Digest(PdfProfileIo.Capture(decoded, c, 1048576), c) != xmlHash)
            throw Error(c, "AssociatedXml", "The producer changed or substituted the exact generated invoice XML.");
    }

    private static string Request(PdfInvoiceData data, string xmlHash, PdfOperationContext c)
    {
        using var output = new PdfCodecBufferStream(c, 8388608);
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        using (var writer = new BinaryWriter(output, new UTF8Encoding(false, true), true))
        {
            writer.Write((int)data.Profile);
            writer.Write(xmlHash);
            writer.Write((int)data.Rounding);
            writer.Write(data.MinorUnits);
            writer.Write(data.IssueTime.Ticks);
            foreach (var line in data.Lines)
            {
                c.Checkpoint();
                c.Consume(PdfResourceKind.ExpandedBytes, line.Description.Length * 6L + 1024);
                writer.Write(line.Description);
                writer.Write(line.Quantity);
                writer.Write(line.UnitPrice);
                writer.Write(line.TaxRate);
            }

            writer.Flush();
        }

        return PdfArchival.Digest(output.Finish(), c);
    }

    private static string Identity(string value, PdfOperationContext c)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 80 || value.Any(ch => !(ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || "._-".IndexOf(ch) >= 0)))
            throw Error(c, "ProviderIdentity", "A pinned nonsecret ASCII provider identity is required.");
        return value;
    }

    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Invoice." + code, message);
}
