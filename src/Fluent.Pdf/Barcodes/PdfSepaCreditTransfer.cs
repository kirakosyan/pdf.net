using System.Globalization;
using System.Text;

namespace Fluent.Pdf;
/// <summary>Canonical twelve-line EPC002/UTF8 SCT payment data. This bounded profile requires an explicit BIC, EUR amount and supported country-length IBAN; it does not establish bank existence, account ownership or payment authorization.</summary>
public sealed class PdfSepaCreditTransfer
{
    private const string Countries = "AD24 AL28 AT20 BE16 BG22 CH21 CY28 CZ24 DE22 DK18 EE20 ES24 FI18 FR27 GB22 GI23 GR27 HR21 HU28 IE22 IS26 IT27 LI21 LT20 LU20 LV21 MC27 MD24 ME22 MK19 MT31 NL18 NO15 PL28 PT25 RO24 RS22 SE24 SI19 SK24 SM27 VA22";
    /// <summary>Captures exact literal fields without trimming/transliteration. Beneficiary is1–70 UTF16 units; references are mutually exclusive. Payload is at most330 UTF8 bytes so explicit ECI26 fits QR version13 at levelM.</summary>
    public PdfSepaCreditTransfer(string beneficiary, string iban, string bic, decimal amount, string creditorReference = "", string remittance = "", string purpose = "", string information = "")
    {
        Field(beneficiary, nameof(beneficiary), 70, false);
        Field(iban, nameof(iban), 34, false);
        Field(bic, nameof(bic), 11, false);
        Field(creditorReference, nameof(creditorReference), 25, true);
        Field(remittance, nameof(remittance), 140, true);
        Field(purpose, nameof(purpose), 4, true);
        Field(information, nameof(information), 70, true);
        if (iban.Length < 15 || !Upper(iban.Substring(0, 2)) || !Digits(iban.Substring(2, 2)) || !Alphanumeric(iban.Substring(4)) || !Countries.Split(' ').Contains(iban.Substring(0, 2) + iban.Length.ToString(CultureInfo.InvariantCulture)) || !CheckDigits(iban))
            throw new ArgumentException("A canonical supported country-length IBAN with a valid modulo97 checksum is required.", nameof(iban));
        if (bic.Length != 8 && bic.Length != 11 || !Upper(bic.Substring(0, 6)) || !Alphanumeric(bic.Substring(6)))
            throw new ArgumentException("An explicit canonical uppercase ISO9362 BIC8 or BIC11 syntax is required.", nameof(bic));
        if (amount < 0.01m || amount > 999999999.99m || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), "A positive exact two-minor-unit EUR amount is required.");
        if (purpose.Length != 0 && (purpose.Length != 4 || !Upper(purpose)))
            throw new ArgumentException("Purpose must be empty or four uppercase letters.", nameof(purpose));
        if (creditorReference.Length != 0 && (creditorReference.Length < 5 || !creditorReference.StartsWith("RF", StringComparison.Ordinal) || !Digits(creditorReference.Substring(2, 2)) || !Alphanumeric(creditorReference.Substring(4)) || !CheckDigits(creditorReference)))
            throw new ArgumentException("A canonical ISO11649 RF creditor reference with modulo97 checksum is required.", nameof(creditorReference));
        if (creditorReference.Length != 0 && remittance.Length != 0)
            throw new ArgumentException("Structured and unstructured remittance are mutually exclusive.", nameof(remittance));
        Beneficiary = beneficiary;
        Iban = iban;
        Bic = bic;
        Amount = amount;
        CreditorReference = creditorReference;
        Remittance = remittance;
        Purpose = purpose;
        Information = information;
        Payload = string.Join("\n", new[] { "BCD", "002", "1", "SCT", bic, beneficiary, iban, "EUR" + amount.ToString("0.00", CultureInfo.InvariantCulture), purpose, creditorReference, remittance, information });
        if (new UTF8Encoding(false, true).GetByteCount(Payload) > 330)
            throw new ArgumentException("The explicit ECI26/version13M payment profile permits at most330 complete UTF8 bytes.", nameof(remittance));
    }

    /// <summary>Gets literal creditor name.</summary>
    public string Beneficiary { get; }
    /// <summary>Gets canonical checksum-validated IBAN; account existence is not established.</summary>
    public string Iban { get; }
    /// <summary>Gets explicit syntax-validated BIC.</summary>
    public string Bic { get; }
    /// <summary>Gets exact positive EUR amount.</summary>
    public decimal Amount { get; }
    /// <summary>Gets canonical RF reference or empty.</summary>
    public string CreditorReference { get; }
    /// <summary>Gets unstructured remittance or empty.</summary>
    public string Remittance { get; }
    /// <summary>Gets the literal optional purpose code syntax.</summary>
    public string Purpose { get; }
    /// <summary>Gets literal optional beneficiary-to-originator information.</summary>
    public string Information { get; }
    /// <summary>Gets canonical LF-separated complete payment text; no transfer is submitted.</summary>
    public string Payload { get; }

    /// <summary>Validates exact canonical twelve-line EPC002/UTF8 input under the same checks. Unsupported editions, encodings, line layouts and noncanonical amounts reject.</summary>
    public static PdfSepaCreditTransfer Parse(string payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        if (payload.Length > 2000 || payload.IndexOf('\r') >= 0)
            throw new ArgumentException("Bounded canonical LF-separated payment input is required.", nameof(payload));
        var lines = payload.Split('\n');
        if (lines.Length != 12 || lines[0] != "BCD" || lines[1] != "002" || lines[2] != "1" || lines[3] != "SCT" || !lines[7].StartsWith("EUR", StringComparison.Ordinal) || !decimal.TryParse(lines[7].Substring(3), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount))
            throw new ArgumentException("The explicit twelve-line EPC002 UTF8/EUR profile is required.", nameof(payload));
        var result = new PdfSepaCreditTransfer(lines[5], lines[6], lines[4], amount, lines[9], lines[10], lines[8], lines[11]);
        if (result.Payload != payload)
            throw new ArgumentException("Payment fields must use the exact canonical profile representation.", nameof(payload));
        return result;
    }

    /// <summary>Uses the immutable invoice's literal seller, identifier and reconciled due amount. Non-EUR, zero/out-of-range amounts and overlong fields reject without conversion or truncation.</summary>
    public static PdfSepaCreditTransfer FromInvoice(PdfInvoiceData invoice, string iban, string bic)
    {
        if (invoice == null)
            throw new ArgumentNullException(nameof(invoice));
        if (invoice.Currency != "EUR")
            throw new ArgumentException("EPC SCT requires an exact EUR invoice; no currency conversion is inferred.", nameof(invoice));
        return new(invoice.Seller, iban, bic, invoice.GrandTotal, remittance: invoice.Identifier);
    }

    private static bool CheckDigits(string value)
    {
        int check = (value[2] - '0') * 10 + value[3] - '0';
        if (check < 2 || check > 98)
            return false;
        int remainder = 0;
        for (int n = 0; n < value.Length; n++)
        {
            char ch = value[(n + 4) % value.Length];
            int digit = ch >= 'A' && ch <= 'Z' ? ch - 'A' + 10 : ch - '0';
            remainder = (remainder * (digit >= 10 ? 100 : 10) + digit) % 97;
        }

        return remainder == 1;
    }

    private static bool Upper(string text) => text.All(ch => ch >= 'A' && ch <= 'Z');
    private static bool Digits(string text) => text.All(ch => ch >= '0' && ch <= '9');
    private static bool Alphanumeric(string text) => text.All(ch => ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9');
    private static void Field(string text, string name, int maximum, bool empty)
    {
        if (text == null)
            throw new ArgumentNullException(name);
        if (text.Length > maximum || !empty && text.Length == 0 || text.Any(ch => char.IsControl(ch) || ch == '\u2028' || ch == '\u2029'))
            throw new ArgumentException("A bounded literal single-line payment field is required.", name);
        try
        {
            new UTF8Encoding(false, true).GetByteCount(text);
        }
        catch (EncoderFallbackException)
        {
            throw new ArgumentException("Payment fields require valid Unicode scalars.", name);
        }
    }
}
