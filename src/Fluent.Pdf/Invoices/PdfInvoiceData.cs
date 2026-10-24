using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml;

namespace Fluent.Pdf;
/// <summary>Exact requested invoice version/profile, not an automatic standards support claim.</summary>
public enum PdfInvoiceProfile
{
    /// <summary>Factur-X1.0.07 MINIMUM; complete XML/business/PDF validation is mandatory before publication.</summary>
    FacturX107Minimum
}

/// <summary>Immutable literal invoice line used by the same calculation for human-readable totals and CII header amounts.</summary>
public sealed class PdfInvoiceLine
{
    /// <summary>Captures a literal description, positive quantity, nonnegative unit price and percentage tax rate. Each numeric input has at most4 fractional digits and finite magnitude.</summary>
    public PdfInvoiceLine(string description, decimal quantity, decimal unitPrice, decimal taxRate)
    {
        Literal(description, nameof(description), 2000);
        if (quantity <= 0 || quantity > 1000000 || unitPrice < 0 || unitPrice > 1000000000 || taxRate < 0 || taxRate > 100 || decimal.Round(quantity, 4) != quantity || decimal.Round(unitPrice, 4) != unitPrice || decimal.Round(taxRate, 4) != taxRate)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Invoice line quantities/prices/tax must fit the explicit nonnegative bounded decimal profile.");
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxRate = taxRate;
    }

    /// <summary>Gets literal description.</summary>
    public string Description { get; }
    /// <summary>Gets positive quantity.</summary>
    public decimal Quantity { get; }
    /// <summary>Gets nonnegative unit price.</summary>
    public decimal UnitPrice { get; }
    /// <summary>Gets percentage tax rate.</summary>
    public decimal TaxRate { get; }

    internal static void Literal(string value, string name, int maximum)
    {
        Guard.Text(value, name, maximum);
        XmlConvert.VerifyXmlChars(value);
    }
}

/// <summary>Immutable tax breakdown for one explicit percentage rate. No tax exemption or jurisdiction is inferred.</summary>
public sealed class PdfInvoiceTaxCategory
{
    internal PdfInvoiceTaxCategory(decimal rate, decimal basis, decimal amount)
    {
        TaxRate = rate;
        TaxableAmount = basis;
        TaxAmount = amount;
    }

    /// <summary>Gets the percentage tax rate.</summary>
    public decimal TaxRate { get; }
    /// <summary>Gets the sum of rounded line net amounts in this category.</summary>
    public decimal TaxableAmount { get; }
    /// <summary>Gets tax rounded once on the category taxable amount.</summary>
    public decimal TaxAmount { get; }
}

/// <summary>Immutable source model with explicit edition, parties, UTC issue time, currency and rounding. No discounts, credit notes, prepayments or inferred tax jurisdiction are supported in this source profile.</summary>
public sealed class PdfInvoiceData
{
    /// <summary>Captures1–1000 lines and computes per-line net and per-rate-category tax using the selected midpoint rounding and explicit currency minor units. Supported currency codes are EUR/USD/GBP(two), JPY(zero), BHD(three).</summary>
    public PdfInvoiceData(PdfInvoiceProfile profile, string identifier, string seller, string buyer, DateTimeOffset issueTime, string currency, MidpointRounding rounding, IEnumerable<PdfInvoiceLine> lines)
    {
        if (!Enum.IsDefined(typeof(PdfInvoiceProfile), profile))
            throw new ArgumentOutOfRangeException(nameof(profile));
        PdfInvoiceLine.Literal(identifier, nameof(identifier), 80);
        PdfInvoiceLine.Literal(seller, nameof(seller), 2000);
        PdfInvoiceLine.Literal(buyer, nameof(buyer), 2000);
        if (issueTime.Offset != TimeSpan.Zero || issueTime.Ticks % TimeSpan.TicksPerSecond != 0)
            throw new ArgumentException("An explicit whole-second UTC invoice time is required.", nameof(issueTime));
        int digits = currency == "EUR" || currency == "USD" || currency == "GBP" ? 2 : currency == "JPY" ? 0 : currency == "BHD" ? 3 : -1;
        if (digits < 0)
            throw new ArgumentException("An explicitly supported ISO currency/minor-unit profile is required.", nameof(currency));
        if (rounding != MidpointRounding.AwayFromZero && rounding != MidpointRounding.ToEven)
            throw new ArgumentOutOfRangeException(nameof(rounding));
        if (lines == null)
            throw new ArgumentNullException(nameof(lines));
        var values = new List<PdfInvoiceLine>();
        var net = new List<decimal>();
        var bases = new SortedDictionary<decimal, decimal>();
        decimal totalNet = 0, totalTax = 0;
        foreach (var line in lines)
        {
            if (line == null || values.Count == 1000)
                throw new ArgumentException("Invoice lines must be nonnull and bounded to1000.", nameof(lines));
            decimal n = decimal.Round(line.Quantity * line.UnitPrice, digits, rounding);
            totalNet += n;
            bases.TryGetValue(line.TaxRate, out decimal basis);
            bases[line.TaxRate] = basis + n;
            if (totalNet > 1000000000000000m)
                throw new ArgumentException("Invoice totals exceed the finite amount profile.", nameof(lines));
            values.Add(line);
            net.Add(n);
        }

        if (values.Count == 0)
            throw new ArgumentException("A nonempty invoice is required.", nameof(lines));
        var categories = bases.Select(pair => new PdfInvoiceTaxCategory(pair.Key, pair.Value, decimal.Round(pair.Value * pair.Key / 100, digits, rounding))).ToList();
        totalTax = categories.Sum(category => category.TaxAmount);
        if (totalNet + totalTax > 1000000000000000m)
            throw new ArgumentException("Invoice totals exceed the finite amount profile.", nameof(lines));
        Profile = profile;
        Identifier = identifier;
        Seller = seller;
        Buyer = buyer;
        IssueTime = issueTime;
        Currency = currency;
        MinorUnits = digits;
        Rounding = rounding;
        Lines = new ReadOnlyCollection<PdfInvoiceLine>(values);
        LineNetAmounts = new ReadOnlyCollection<decimal>(net);
        TaxCategories = new ReadOnlyCollection<PdfInvoiceTaxCategory>(categories);
        NetTotal = totalNet;
        TaxTotal = totalTax;
        GrandTotal = totalNet + totalTax;
    }

    /// <summary>Gets exact requested profile.</summary>
    public PdfInvoiceProfile Profile { get; }
    /// <summary>Gets the explicit seller identifier required by the MINIMUM rules, or null until supplied.</summary>
    public string? SellerRegistrationIdentifier { get; private set; }
    /// <summary>Gets the explicit uppercase seller country code, or null until supplied. Independent code-list validation remains mandatory.</summary>
    public string? SellerCountryCode { get; private set; }
    /// <summary>Returns an independent immutable model with explicit seller identification and a two-letter uppercase country code. No identity, jurisdiction or country is inferred.</summary>
    public PdfInvoiceData WithSellerRegistration(string identifier, string countryCode)
    {
        PdfInvoiceLine.Literal(identifier, nameof(identifier), 80);
        if (countryCode == null || countryCode.Length != 2 || countryCode.Any(ch => ch < 'A' || ch > 'Z'))
            throw new ArgumentException("An explicit two-letter uppercase seller country code is required.", nameof(countryCode));
        return new(Profile, Identifier, Seller, Buyer, IssueTime, Currency, Rounding, Lines) { SellerRegistrationIdentifier = identifier, SellerCountryCode = countryCode };
    }
    /// <summary>Gets literal invoice identifier.</summary>
    public string Identifier { get; }
    /// <summary>Gets literal seller name.</summary>
    public string Seller { get; }
    /// <summary>Gets literal buyer name.</summary>
    public string Buyer { get; }
    /// <summary>Gets explicit whole-second UTC issue time.</summary>
    public DateTimeOffset IssueTime { get; }
    /// <summary>Gets explicit currency code.</summary>
    public string Currency { get; }
    /// <summary>Gets currency minor-unit exponent.</summary>
    public int MinorUnits { get; }
    /// <summary>Gets explicit midpoint rounding policy.</summary>
    public MidpointRounding Rounding { get; }
    /// <summary>Gets immutable literal lines.</summary>
    public IReadOnlyList<PdfInvoiceLine> Lines { get; }
    /// <summary>Gets per-line rounded net amounts.</summary>
    public IReadOnlyList<decimal> LineNetAmounts { get; }
    /// <summary>Gets the ordered per-rate tax breakdown.</summary>
    public IReadOnlyList<PdfInvoiceTaxCategory> TaxCategories { get; }
    /// <summary>Gets the exact sum of rounded line net amounts.</summary>
    public decimal NetTotal { get; }
    /// <summary>Gets the exact sum of rounded category tax amounts.</summary>
    public decimal TaxTotal { get; }
    /// <summary>Gets reconciled gross/due amount; no prepayment or discount is inferred.</summary>
    public decimal GrandTotal { get; }

    internal string Money(decimal value) => value.ToString("F" + MinorUnits, CultureInfo.InvariantCulture);
    internal string XmlMoney(decimal value) => value.ToString("F" + Math.Min(MinorUnits, 2), CultureInfo.InvariantCulture);
}
