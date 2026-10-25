using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Fluent.Pdf.Compliance;

/// <summary>Runs an explicitly provisioned independent exact-edition invoice host. PDF, XML, private model and report leases expire on return; all work must honor the shared context.</summary>
public delegate void PdfInvoiceValidationRunner(Stream pdf, Stream xml, Stream model, Stream report, PdfOperationContext context);

/// <summary>Bounded exact-byte report bridge for the pinned Factur-X1.0.07 MINIMUM rule bundle and independent original PDFBox/Saxon host. It does not install, discover or execute an engine implicitly.</summary>
public sealed class FacturX107MinimumReportValidator : IPdfInvoiceValidator
{
    /// <summary>Gets the SHA256 of the exact ordered seven-file schema/Schematron/code-list inventory.</summary>
    public const string RuleBundleSha256 = "711f08a9dab037032c8ddafcf0dd2bdb0ee80f7beec152e93a3289f7e49bee77";
    private readonly PdfInvoiceValidationRunner _runner;
    /// <summary>Captures the explicit caller-owned supervised validation host, which must execute all five phases. Binding checks prevent stale or mismatched reports; the caller remains responsible for the host's independence and truthful execution.</summary>
    public FacturX107MinimumReportValidator(PdfInvoiceValidationRunner runner) => _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    /// <summary>Gets the separately identified exact source protocol, not production qualification.</summary>
    public string EngineId => "invoice.fx107-minimum.pdfbox3-saxon.v1";
    /// <summary>Returns true only for the exact declared MINIMUM edition. Host prerequisites remain explicit.</summary>
    public bool Supports(PdfInvoiceProfile profile) => profile == PdfInvoiceProfile.FacturX107Minimum;
    /// <summary>Captures/hashes exact private inputs, serializes a nonce-bound immutable model, invokes the host and requires complete schema, rules, metadata, model and readable-paint reports with exact request/bundle binding.</summary>
    public PdfInvoiceValidation Validate(Stream pdf, Stream xml, PdfInvoiceData invoice, string requestSha256, PdfOperationContext context)
    {
        if (pdf == null || xml == null || invoice == null || context == null) throw new ArgumentNullException(pdf == null ? nameof(pdf) : xml == null ? nameof(xml) : invoice == null ? nameof(invoice) : nameof(context));
        if (!Supports(invoice.Profile)) throw Error(context, "Profile", "The exact invoice report profile is unsupported.");
        if (requestSha256 == null || requestSha256.Length != 64 || requestSha256.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))) throw new ArgumentException("An exact lowercase request SHA256 is required.", nameof(requestSha256));
        using var pl = context.BorrowExpandedInput(pdf);
        using var xl = context.BorrowExpandedInput(xml);
        var p = PdfProfileIo.Capture(pl, context, 67108864);
        var x = PdfProfileIo.Capture(xl, context, 1048576);
        string ph = PdfArchival.Digest(p, context), xh = PdfArchival.Digest(x, context);
        var model = Model(invoice, requestSha256, context);
        string mh = PdfArchival.Digest(model, context);
        using var output = new PdfCodecBufferStream(context, 4194304);
        using (var pi = p.Open(0, p.Length)) using (var xi = x.Open(0, x.Length)) using (var mi = model.Open(0, model.Length))
        using (var ple = context.BorrowExpandedInput(pi)) using (var xle = context.BorrowExpandedInput(xi)) using (var mle = context.BorrowExpandedInput(mi)) using (var ol = new BorrowedStream(output, false, context))
            _runner(ple, xle, mle, ol, context);
        context.Checkpoint();
        var report = output.Finish();
        context.Consume(PdfResourceKind.ExpandedBytes, report.Length * 4L + 32768);
        using var raw = report.Open(0, report.Length);
        using var reader = XmlReader.Create(raw, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4194304, CloseInput = false });
        var phases = new HashSet<string>(StringComparer.Ordinal);
        var findings = new List<string>();
        bool root = false, ended = false, completed = true, compliant = true;
        try
        {
            while (reader.Read())
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1 + reader.AttributeCount);
                context.Consume(PdfResourceKind.ExpandedBytes, 128 + reader.Value.Length * 2L + reader.AttributeCount * 256L);
                if (reader.Depth > 2 || reader.NamespaceURI.Length != 0) throw Error(context, "ReportSchema", "The invoice report exceeds its closed namespace/depth profile.");
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == 0) { ended = true; continue; }
                if (reader.NodeType is XmlNodeType.XmlDeclaration or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace) continue;
                if (reader.NodeType != XmlNodeType.Element) throw Error(context, "ReportSchema", "Only the closed invoice report structure is supported.");
                if (reader.Depth == 0)
                {
                    if (root || reader.LocalName != "invoiceValidation" || reader.IsEmptyElement || reader.AttributeCount != 8 || reader.GetAttribute("schemaVersion") != "1" || reader.GetAttribute("engine") != EngineId || reader.GetAttribute("bundleSha256") != RuleBundleSha256 || reader.GetAttribute("pdfSha256") != ph || reader.GetAttribute("xmlSha256") != xh || reader.GetAttribute("modelSha256") != mh || reader.GetAttribute("requestSha256") != requestSha256 || reader.GetAttribute("profile") != "Factur-X1.0.07-MINIMUM")
                        throw Error(context, "ReportBinding", "The independent invoice report does not bind the exact engine, rules, bytes and private model request.");
                    root = true;
                }
                else if (reader.Depth == 1 && new[] { "schema", "business", "metadata", "model", "readable" }.Contains(reader.LocalName))
                {
                    if (!root || ended || !reader.IsEmptyElement || !phases.Add(reader.LocalName) || reader.AttributeCount != 3) throw Error(context, "ReportSchema", "Every invoice validation phase must appear exactly once.");
                    bool done = Flag("completed"), pass = Flag("compliant");
                    if (!long.TryParse(reader.GetAttribute("checks"), NumberStyles.None, CultureInfo.InvariantCulture, out long checks) || checks < 1 || checks > 1000000 || pass && !done) throw Error(context, "ReportSchema", "A bounded positive invoice phase check count is required.");
                    completed &= done; compliant &= pass;
                }
                else if (reader.Depth == 1 && reader.LocalName == "finding" && reader.IsEmptyElement && reader.AttributeCount == 1)
                {
                    string code = reader.GetAttribute("code") ?? "";
                    bool official = code.Length == 15 && code.StartsWith("FX-SCH-A-", StringComparison.Ordinal) && code.Substring(9).All(c => c >= '0' && c <= '9');
                    if (findings.Count == 10000 || !(official || new[] { "Invoice.Xsd", "Invoice.BusinessReport", "Invoice.Metadata", "Invoice.Model", "Invoice.Readable", "Invoice.PaintProfile", "Invoice.XmlIdentity" }.Contains(code))) throw Error(context, "ReportFinding", "Only bounded stable invoice rule codes may enter diagnostics.");
                    findings.Add(code);
                }
                else throw Error(context, "ReportSchema", "The invoice report contains an unsupported structure.");
            }
        }
        catch (XmlException) { context.Checkpoint(); throw Error(context, "ReportXml", "The independent invoice report is malformed; private payload is omitted."); }
        if (!root || !ended || phases.Count != 5 || compliant && findings.Count != 0 || !compliant && findings.Count == 0) throw Error(context, "ReportIncomplete", "The independent invoice report is incomplete or contradictory.");
        return new(invoice.Profile, ph, xh, requestSha256, completed, completed && compliant, findings);

        bool Flag(string key)
        {
            string? text = reader.GetAttribute(key);
            if (text != "true" && text != "false") throw Error(context, "ReportSchema", "Invoice phase flags must be explicit booleans.");
            return text == "true";
        }
    }

    private static PdfByteStore Model(PdfInvoiceData data, string request, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.Objects, data.Lines.Count * 8L + data.TaxCategories.Count * 4L + 64);
        c.Consume(PdfResourceKind.ExpandedBytes, 32768 + (data.Identifier.Length + data.Seller.Length + data.Buyer.Length + (data.SellerRegistrationIdentifier?.Length ?? 0)) * 12L + data.Lines.Sum(l => l.Description.Length * 12L + 2048));
        using var output = new PdfCodecBufferStream(c, 16777216);
        using (var w = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false, true), CloseOutput = false }))
        {
            w.WriteStartElement("invoiceModel"); w.WriteAttributeString("schemaVersion", "1"); w.WriteAttributeString("nonce", Guid.NewGuid().ToString("N")); w.WriteAttributeString("requestSha256", request);
            Text("identifier", data.Identifier); Text("seller", data.Seller); Text("buyer", data.Buyer); Text("sellerRegistrationIdentifier", data.SellerRegistrationIdentifier ?? ""); Text("sellerCountry", data.SellerCountryCode ?? "");
            Text("issueDate", data.IssueTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); Text("currency", data.Currency); Text("digits", data.MinorUnits.ToString(CultureInfo.InvariantCulture)); Text("rounding", data.Rounding == MidpointRounding.AwayFromZero ? "half-up" : "half-even");
            Text("net", N(data.NetTotal)); Text("tax", N(data.TaxTotal)); Text("gross", N(data.GrandTotal));
            for (int n = 0; n < data.Lines.Count; n++)
            {
                c.Checkpoint(); var line = data.Lines[n]; w.WriteStartElement("line"); Text("description", line.Description); Text("quantity", N(line.Quantity)); Text("price", N(line.UnitPrice)); Text("rate", N(line.TaxRate)); Text("net", N(data.LineNetAmounts[n])); w.WriteEndElement();
            }
            foreach (var category in data.TaxCategories) { c.Checkpoint(); w.WriteStartElement("category"); Text("rate", N(category.TaxRate)); Text("basis", N(category.TaxableAmount)); Text("tax", N(category.TaxAmount)); w.WriteEndElement(); }
            w.WriteEndElement(); w.Flush();
            void Text(string name, string value) => w.WriteElementString(name, value);
        }
        return output.Finish();
        static string N(decimal n) => n.ToString(CultureInfo.InvariantCulture);
    }
    private static PdfException Error(PdfOperationContext c, string code, string text) => PdfWriterCore.Error(c, "Invoice." + code, text);
}
