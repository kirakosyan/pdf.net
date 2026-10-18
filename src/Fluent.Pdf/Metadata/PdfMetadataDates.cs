using System.Globalization;
using System.Text.RegularExpressions;

namespace Fluent.Pdf;
internal static class PdfMetadataDates
{
    private sealed class Parts
    {
        internal string Digits = "";
        internal int? Offset;
        internal string Fraction = "";
    }

    internal static string Format(DateTimeOffset value)
    {
        if (value.Ticks % TimeSpan.TicksPerSecond != 0)
            throw new ArgumentException("Info timestamps require an explicit whole-second value.", nameof(value));
        var parts = new Parts
        {
            Digits = value.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            Offset = (int)value.Offset.TotalMinutes
        };
        return Pdf(parts);
    }

    internal static void Validate(string value, PdfOperationContext context) => ParsePdf(value, context);
    internal static string ToXmp(string value, PdfOperationContext context)
    {
        var p = ParsePdf(value, context);
        var d = p.Digits;
        if (d.Length == 10)
            throw PdfWriterCore.Error(context, "Metadata.DatePrecisionUnsupported", "XMP date-times require minute precision; explicitly choose the missing minute before synchronization.");
        string result = d.Substring(0, 4);
        if (d.Length >= 6)
            result += "-" + d.Substring(4, 2);
        if (d.Length >= 8)
            result += "-" + d.Substring(6, 2);
        if (d.Length >= 10)
            result += "T" + d.Substring(8, 2);
        if (d.Length >= 12)
            result += ":" + d.Substring(10, 2);
        if (d.Length >= 14)
            result += ":" + d.Substring(12, 2);
        if (p.Offset.HasValue)
            result += Zone(p.Offset.Value, false);
        return result;
    }

    internal static string ToPdf(string value, PdfOperationContext context)
    {
        var p = ParseXmp(value, context);
        if (p.Fraction.Any(c => c != '0'))
            context.Report(new PdfDiagnostic("Metadata.SubsecondPrecisionLoss", "The explicitly requested Info synchronization cannot represent XMP subsecond precision.", lossCategory: PdfLossCategory.FidelityReduction));
        return Pdf(p);
    }

    internal static DateTimeOffset? Instant(string value, bool xmp, PdfOperationContext context)
    {
        var p = xmp ? ParseXmp(value, context) : ParsePdf(value, context);
        if (p.Digits.Length != 14 || !p.Offset.HasValue || Math.Abs(p.Offset.Value) > 840)
            return null;
        var date = DateTime.ParseExact(p.Digits, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None);
        DateTimeOffset result;
        try
        {
            result = new DateTimeOffset(date, TimeSpan.FromMinutes(p.Offset.Value));
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (p.Fraction.Length > 0)
        {
            string ticks = (p.Fraction + "0000000").Substring(0, 7);
            result = result.AddTicks(int.Parse(ticks, CultureInfo.InvariantCulture));
        }

        return result;
    }

    private static Parts ParsePdf(string value, PdfOperationContext context)
    {
        context.Checkpoint();
        if (value.Length > 40 || !value.StartsWith("D:", StringComparison.Ordinal))
            throw Invalid(context);
        string text = value.Substring(2);
        int split = text.IndexOfAny(new[] { 'Z', '+', '-' });
        string digits = split < 0 ? text : text.Substring(0, split), zone = split < 0 ? "" : text.Substring(split);
        var p = new Parts
        {
            Digits = digits,
            Offset = ParseZone(zone, true, context)
        };
        Check(p, context);
        return p;
    }

    private static Parts ParseXmp(string value, PdfOperationContext context)
    {
        context.Checkpoint();
        if (value.Length > 64)
            throw Invalid(context);
        var match = Regex.Match(value, @"\A([0-9]{4})(?:-([0-9]{2})(?:-([0-9]{2})(?:T([0-9]{2})(?::([0-9]{2})(?::([0-9]{2})(?:\.([0-9]+))?)?)?)?)?)?(Z|[+-][0-9]{2}:[0-9]{2})?\z", RegexOptions.CultureInvariant);
        if (!match.Success)
            throw Invalid(context);
        var p = new Parts
        {
            Digits = string.Concat(Enumerable.Range(1, 6).Select(i => match.Groups[i].Value)),
            Fraction = match.Groups[7].Value,
            Offset = ParseZone(match.Groups[8].Value, false, context)
        };
        Check(p, context);
        return p;
    }

    private static int? ParseZone(string zone, bool pdf, PdfOperationContext context)
    {
        if (zone == "")
            return null;
        if (zone == "Z")
            return 0;
        string pattern = pdf ? @"\A([+-])([0-9]{2})(?:'([0-9]{2})'?)?\z" : @"\A([+-])([0-9]{2}):([0-9]{2})\z";
        var match = Regex.Match(zone, pattern, RegexOptions.CultureInvariant);
        if (!match.Success)
            throw Invalid(context);
        int hours = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), minutes = match.Groups[3].Success ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
        if (hours > 23 || minutes > 59)
            throw Invalid(context);
        return (hours * 60 + minutes) * (match.Groups[1].Value == "-" ? -1 : 1);
    }

    private static void Check(Parts p, PdfOperationContext context)
    {
        if (p.Digits.Length < 4 || p.Digits.Length > 14 || p.Digits.Length % 2 != 0 || p.Digits.Any(c => c < '0' || c > '9'))
            throw Invalid(context);
        int Part(int at, int fallback) => p.Digits.Length > at ? int.Parse(p.Digits.Substring(at, 2), CultureInfo.InvariantCulture) : fallback;
        try
        {
            _ = new DateTime(int.Parse(p.Digits.Substring(0, 4), CultureInfo.InvariantCulture), Part(4, 1), Part(6, 1), Part(8, 0), Part(10, 0), Part(12, 0), DateTimeKind.Unspecified);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw Invalid(context);
        }
    }

    private static string Pdf(Parts p) => "D:" + p.Digits + (p.Offset.HasValue ? Zone(p.Offset.Value, true) : "");
    private static string Zone(int offset, bool pdf)
    {
        if (offset == 0)
            return "Z";
        int minutes = Math.Abs(offset);
        return (offset < 0 ? "-" : "+") + (minutes / 60).ToString("00", CultureInfo.InvariantCulture) + (pdf ? "'" : ":") + (minutes % 60).ToString("00", CultureInfo.InvariantCulture) + (pdf ? "'" : "");
    }

    private static PdfException Invalid(PdfOperationContext context) => PdfWriterCore.Error(context, "Metadata.InvalidDate", "The metadata date has invalid precision, calendar fields or offset syntax.");
}
