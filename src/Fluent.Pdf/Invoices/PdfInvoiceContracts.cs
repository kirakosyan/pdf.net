using System.IO;

namespace Fluent.Pdf;
/// <summary>Explicit caller-owned complete invoice PDF/A-3 producer. All work must use the supplied operation context and separately qualified offline engine assets/host.</summary>
public interface IPdfInvoiceProducer
{
    /// <summary>Gets a nonsecret pinned ASCII engine identity.</summary>
    string EngineId { get; }

    /// <summary>Reports exact invoice edition/profile and local prerequisites.</summary>
    bool Supports(PdfInvoiceProfile profile);
    /// <summary>Produces a candidate from the SDK-generated human-readable PDF and exact CII XML. Preserve literal human totals, metadata, associated XML and required PDF/A/invoice identification. Report all losses through context. Leases expire on return.</summary>
    void Convert(Stream sourcePdf, Stream invoiceXml, PdfInvoiceData invoice, Stream candidatePdf, PdfOperationContext context);
}

/// <summary>Complete independent schema/business-rule/invoice-package validator, including readable PDF versus XML/model reconciliation.</summary>
public interface IPdfInvoiceValidator
{
    /// <summary>Gets a nonsecret pinned ASCII identity distinct from the producer.</summary>
    string EngineId { get; }

    /// <summary>Reports exact version/profile validation availability.</summary>
    bool Supports(PdfInvoiceProfile profile);
    /// <summary>Validates the exact PDF/XML/model/request and complete edition-specific schema/business/metadata/reconciliation rules. A syntax parser or metadata check cannot return compliance. No ambient network/schema download is permitted.</summary>
    PdfInvoiceValidation Validate(Stream pdf, Stream xml, PdfInvoiceData invoice, string requestSha256, PdfOperationContext context);
}

/// <summary>Complete/incomplete exact-byte independent invoice observation; not SDK production qualification.</summary>
public sealed class PdfInvoiceValidation
{
    /// <summary>Captures exact profile/PDF/XML/request binding and bounded rule findings. Compliance requires a complete result with zero findings.</summary>
    public PdfInvoiceValidation(PdfInvoiceProfile profile, string pdfSha256, string xmlSha256, string requestSha256, bool completed, bool isCompliant, IEnumerable<string> findings)
    {
        if (!Enum.IsDefined(typeof(PdfInvoiceProfile), profile))
            throw new ArgumentOutOfRangeException(nameof(profile));
        Hash(pdfSha256);
        Hash(xmlSha256);
        Hash(requestSha256);
        if (findings == null)
            throw new ArgumentNullException(nameof(findings));
        var values = new List<string>();
        foreach (string value in findings)
        {
            if (values.Count == 10000)
                throw new ArgumentException("At most10000 invoice rule findings are supported.", nameof(findings));
            Guard.Text(value, nameof(findings), 8192);
            values.Add(value);
        }

        if (isCompliant && (!completed || values.Count != 0))
            throw new ArgumentException("Compliance requires completed validation with zero findings.", nameof(isCompliant));
        Profile = profile;
        PdfSha256 = pdfSha256;
        XmlSha256 = xmlSha256;
        RequestSha256 = requestSha256;
        Completed = completed;
        IsCompliant = isCompliant;
        Findings = Array.AsReadOnly(values.ToArray());
    }

    /// <summary>Gets exact invoice version/profile.</summary>
    public PdfInvoiceProfile Profile { get; }
    /// <summary>Gets exact validated PDF digest.</summary>
    public string PdfSha256 { get; }
    /// <summary>Gets exact associated XML digest.</summary>
    public string XmlSha256 { get; }
    /// <summary>Gets the exact SDK model/settings request digest.</summary>
    public string RequestSha256 { get; }
    /// <summary>Gets complete-rule validation status.</summary>
    public bool Completed { get; }
    /// <summary>Gets independent compliance observation.</summary>
    public bool IsCompliant { get; }
    /// <summary>Gets immutable stable rule findings.</summary>
    public IReadOnlyList<string> Findings { get; }
    /// <summary>Gets separately bound complete PDF/A-3b validation after successful SDK publication.</summary>
    public PdfArchivalValidation? ArchivalValidation { get; internal set; }
    /// <summary>Gets complete SDK operation diagnostics after successful publication.</summary>
    public PdfOperationReport? OperationReport { get; internal set; }

    private static void Hash(string value)
    {
        if (value == null || value.Length != 64 || value.Any(ch => !(ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f')))
            throw new ArgumentException("Exact lowercase SHA256 values are required.", nameof(value));
    }
}
