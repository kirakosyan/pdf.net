using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Fluent.Pdf;
/// <summary>Standard page-label numbering styles.</summary>
public enum PdfPageLabelStyle
{
    /// <summary>Prefix only.</summary>
    None,
    /// <summary>Invariant decimal numbers.</summary>
    Decimal,
    /// <summary>Lowercase Roman numbers from one through 3999.</summary>
    LowerRoman,
    /// <summary>Uppercase Roman numbers from one through 3999.</summary>
    UpperRoman,
    /// <summary>Lowercase repeated-letter PDF labels.</summary>
    LowerLetters,
    /// <summary>Uppercase repeated-letter PDF labels.</summary>
    UpperLetters
}

/// <summary>An immutable numbering restart at a zero-based physical page.</summary>
public sealed class PdfPageNumberRange
{
    /// <summary>Creates a page-number restart at a zero-based physical page.</summary>
    public PdfPageNumberRange(int startPageIndex, PdfPageLabelStyle style = PdfPageLabelStyle.Decimal, int firstNumber = 1, string prefix = "")
    {
        if (startPageIndex < 0 || firstNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(startPageIndex));
        if (!Enum.IsDefined(typeof(PdfPageLabelStyle), style))
            throw new ArgumentOutOfRangeException(nameof(style));
        if (prefix == null || prefix.Length > 128)
            throw new ArgumentException("A bounded explicit page-label prefix is required.", nameof(prefix));
        StartPageIndex = startPageIndex;
        Style = style;
        FirstNumber = firstNumber;
        Prefix = prefix;
    }

    /// <summary>Gets the zero-based physical page where this range starts.</summary>
    public int StartPageIndex { get; }
    /// <summary>Gets the standard PDF label style.</summary>
    public PdfPageLabelStyle Style { get; }
    /// <summary>Gets the positive first counter value.</summary>
    public int FirstNumber { get; }
    /// <summary>Gets the literal label prefix.</summary>
    public string Prefix { get; }
}

/// <summary>Immutable physical-page numbering ranges with invariant formatting.</summary>
public sealed class PdfPageNumbering
{
    /// <summary>Creates ordered numbering ranges; an empty array selects decimal numbering.</summary>
    public PdfPageNumbering(params PdfPageNumberRange[] ranges)
    {
        if (ranges == null || ranges.Length > 4096 || ranges.Any(r => r == null))
            throw new ArgumentException("Supply up to 4096 nonnull page-number ranges.", nameof(ranges));
        var copy = ranges.Length == 0 ? new[]
        {
            new PdfPageNumberRange(0)
        }

        : ranges.OrderBy(r => r.StartPageIndex).ToArray();
        if (copy[0].StartPageIndex != 0 || copy.Select(r => r.StartPageIndex).Distinct().Count() != copy.Length)
            throw new ArgumentException("Number ranges must start at page zero and have distinct starts.", nameof(ranges));
        Ranges = new ReadOnlyCollection<PdfPageNumberRange>(copy);
    }

    /// <summary>Gets the immutable ordered numbering ranges.</summary>
    public IReadOnlyList<PdfPageNumberRange> Ranges { get; }

    /// <summary>Formats the label for a zero-based physical page using invariant numbering.</summary>
    public string GetLabel(int pageIndex)
    {
        if (pageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        var range = Ranges[0];
        foreach (var current in Ranges)
            if (current.StartPageIndex <= pageIndex)
                range = current;
            else
                break;
        long number = (long)range.FirstNumber + pageIndex - range.StartPageIndex;
        if (number > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        return range.Prefix + Counter((int)number, range.Style);
    }

    private static string Counter(int number, PdfPageLabelStyle style)
    {
        if (style == PdfPageLabelStyle.None)
            return string.Empty;
        if (style == PdfPageLabelStyle.Decimal)
            return number.ToString(CultureInfo.InvariantCulture);
        var text = new StringBuilder();
        if (style == PdfPageLabelStyle.LowerLetters || style == PdfPageLabelStyle.UpperLetters)
        {
            // PDF page labels repeat letters after Z: A..Z, AA..ZZ, AAA..ZZZ.
            int repeat = (number - 1) / 26 + 1;
            if (repeat > 128)
                throw new ArgumentOutOfRangeException(nameof(number), "The repeated-letter label exceeds its bounded profile.");
            text.Append((char)('A' + (number - 1) % 26), repeat);
        }
        else
        {
            if (number > 3999)
                throw new ArgumentOutOfRangeException(nameof(number), "Roman page labels support 1 through 3999.");
            int[] values =
            {
                1000,
                900,
                500,
                400,
                100,
                90,
                50,
                40,
                10,
                9,
                5,
                4,
                1
            };
            string[] names =
            {
                "M",
                "CM",
                "D",
                "CD",
                "C",
                "XC",
                "L",
                "XL",
                "X",
                "IX",
                "V",
                "IV",
                "I"
            };
            for (int i = 0; i < values.Length; i++)
                while (number >= values[i])
                {
                    text.Append(names[i]);
                    number -= values[i];
                }
        }

        string label = text.ToString();
        return style == PdfPageLabelStyle.LowerLetters || style == PdfPageLabelStyle.LowerRoman ? label.ToLowerInvariant() : label;
    }
}
