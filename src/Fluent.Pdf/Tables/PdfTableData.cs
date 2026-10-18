using System.Globalization;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An explicit typed scalar selector, culture-aware format and cell style for data binding.</summary>
public sealed class PdfTableDataColumn<T>
{
    /// <summary>Captures a caller-owned selector. Supported scalar values use the explicit binding culture; arbitrary object ToString is never invoked.</summary>
    public PdfTableDataColumn(PdfTableColumn width, Func<T, object?> value, PdfFlowTextStyle textStyle, string? format = null, string nullText = "", PdfTableCellStyle? cellStyle = null, PdfFlowParagraphStyle? paragraphStyle = null)
    {
        Width = width ?? throw new ArgumentNullException(nameof(width));
        Value = value ?? throw new ArgumentNullException(nameof(value));
        TextStyle = textStyle ?? throw new ArgumentNullException(nameof(textStyle));
        NullText = nullText ?? throw new ArgumentNullException(nameof(nullText));
        if (format?.Length > 256 || nullText.Length > 1000000)
            throw new ArgumentException("Binding format and null text must be bounded.");
        ParagraphStyle = paragraphStyle ?? new();
        Format = format;
        CellStyle = cellStyle ?? new();
    }

    /// <summary>Gets the immutable width definition.</summary>
    public PdfTableColumn Width { get; }
    /// <summary>Gets the explicit scalar selector; the SDK never reflects over records.</summary>
    public Func<T, object?> Value { get; }
    /// <summary>Gets immutable text style.</summary>
    public PdfFlowTextStyle TextStyle { get; }
    /// <summary>Gets the optional scalar format string.</summary>
    public string? Format { get; }
    /// <summary>Gets explicit text for null values and null records.</summary>
    public string NullText { get; }
    /// <summary>Gets immutable cell style.</summary>
    public PdfTableCellStyle CellStyle { get; }
    /// <summary>Gets immutable paragraph alignment, spacing and line constraints inside the bound cell.</summary>
    public PdfFlowParagraphStyle ParagraphStyle { get; }
}

/// <summary>Bounded typed row binding with explicit culture and scalar conversion.</summary>
public static class PdfTableData
{
    /// <summary>Enumerates at most maximumRows plus one lookahead, disposes the source, and snapshots rows under one operation budget.</summary>
    public static PdfTable Bind<T>(IEnumerable<T> source, PdfTableDataColumn<T>[] columns, CultureInfo culture, int maximumRows = 100000, PdfTableRow[]? headerRows = null, bool allowRowSplit = true, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (columns == null || columns.Length < 1 || columns.Length > 1024 || columns.Any(c => c == null))
            throw new ArgumentException("Supply one through 1024 nonnull data columns.", nameof(columns));
        if (culture == null)
            throw new ArgumentNullException(nameof(culture));
        if (culture.GetType() != typeof(CultureInfo))
            throw new ArgumentException("Binding requires a standard CultureInfo instance.", nameof(culture));
        if (maximumRows < 1 || maximumRows > 1000000)
            throw new ArgumentOutOfRangeException(nameof(maximumRows));
        var headers = headerRows ?? Array.Empty<PdfTableRow>();
        if (headers.Length > 4096 || headers.Any(r => r == null) || headers.Length > 1000000 - maximumRows)
            throw new ArgumentException("Header rows must be bounded and nonnull.", nameof(headerRows));
        PdfTable? result = null;
        var report = PdfFont.Run("BindTable", "managed.table", policy, cancellationToken, context => result = BindCore(source, columns, culture, maximumRows, headerRows, allowRowSplit, context), PdfCapability.WriteDocument);
        result!.OperationReport = report;
        return result;
    }

    internal static PdfTable BindCore<T>(IEnumerable<T> source, PdfTableDataColumn<T>[] columns, CultureInfo culture, int maximumRows, PdfTableRow[]? headerRows, bool allowRowSplit, PdfOperationContext context)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (columns == null || columns.Length < 1 || columns.Length > 1024 || columns.Any(c => c == null))
            throw new ArgumentException("Supply one through 1024 nonnull data columns.", nameof(columns));
        if (culture == null)
            throw new ArgumentNullException(nameof(culture));
        if (culture.GetType() != typeof(CultureInfo))
            throw new ArgumentException("Binding requires a standard CultureInfo instance.", nameof(culture));
        if (maximumRows < 1 || maximumRows > 1000000)
            throw new ArgumentOutOfRangeException(nameof(maximumRows));
        var headers = headerRows ?? Array.Empty<PdfTableRow>();
        if (headers.Length > 4096 || headers.Any(r => r == null) || headers.Length > 1000000 - maximumRows)
            throw new ArgumentException("Header rows must be bounded and nonnull.", nameof(headerRows));
        context.Consume(PdfResourceKind.ExpandedBytes, 4096);
        var formatting = CultureInfo.ReadOnly((CultureInfo)culture.Clone());
        PdfTable? result = null;
        context.Consume(PdfResourceKind.ExpandedBytes, columns.Length * 32L + headers.Length * 16L + 128);
        ValidateCulture(formatting, context);
        var captured = (PdfTableDataColumn<T>[])columns.Clone();
        var capturedHeaders = (PdfTableRow[])headers.Clone();
        IEnumerable<PdfTableRow> Rows()
        {
            foreach (var header in capturedHeaders)
                yield return header;
            int count = 0;
            using var enumerator = source.GetEnumerator();
            while (true)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                if (!enumerator.MoveNext())
                    yield break;
                if (count++ == maximumRows)
                    throw PdfWriterCore.Error(context, "Table.RowLimit", "The data source exceeds its explicit finite row limit.");
                T record = enumerator.Current;
                if (ReferenceEquals(record, source))
                    throw PdfWriterCore.Error(context, "Table.DataCycle", "The data source directly contains itself.");
                context.Consume(PdfResourceKind.ExpandedBytes, captured.Length * 256L + 128);
                var cells = new PdfTableCell[captured.Length];
                for (int i = 0; i < cells.Length; i++)
                {
                    context.Checkpoint();
                    var column = captured[i];
                    object? value = record is null ? null : column.Value(record);
                    if (ReferenceEquals(value, source) || value is not null && ReferenceEquals(value, record) && !Scalar(value))
                        throw PdfWriterCore.Error(context, "Table.DataCycle", "A scalar binding points back to its source or record.");
                    string text = Format(value, column.Format, column.NullText, formatting, context);
                    context.Consume(PdfResourceKind.InputBytes, text.Length * 2L);
                    context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 4L + 128);
                    cells[i] = new PdfTableCell(new[] { new PdfFlowParagraph(text, column.TextStyle, column.ParagraphStyle) }, style: column.CellStyle);
                }

                yield return new PdfTableRow(cells);
            }
        }

        result = PdfTable.CreateCore(captured.Select(c => c.Width).ToArray(), Rows(), capturedHeaders.Length, allowRowSplit, maximumRows + capturedHeaders.Length, context);
        return result!;
    }

    internal static string Format(object? value, string? format, string nullText, CultureInfo culture, PdfOperationContext context)
    {
        if (value == null)
            return nullText;
        if (!Scalar(value))
            throw PdfWriterCore.Error(context, "Table.ValueType", "Only explicitly selected scalar values can be formatted; collections and arbitrary objects require explicit content construction.");
        if (format != null && format.Length > 1 && char.IsLetter(format[0]) && format.Skip(1).All(char.IsDigit))
        {
            if (format.Length > 3 || !int.TryParse(format.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out int precision) || precision > 99)
                throw PdfWriterCore.Error(context, "Table.FormatPrecision", "Standard numeric precision is limited to 99 before formatting can allocate.");
        }

        if (value is not string)
        {
            var n = culture.NumberFormat;
            var d = culture.DateTimeFormat;
            int expansion = new[]
            {
                1,
                n.CurrencySymbol.Length,
                n.CurrencyGroupSeparator.Length,
                n.NumberGroupSeparator.Length,
                n.PercentGroupSeparator.Length,
                n.NegativeSign.Length,
                n.PositiveSign.Length,
                n.NaNSymbol.Length,
                n.NegativeInfinitySymbol.Length,
                n.PositiveInfinitySymbol.Length,
                n.PercentSymbol.Length,
                n.PerMilleSymbol.Length,
                d.MonthNames.Max(t => t.Length),
                d.DayNames.Max(t => t.Length),
                d.AMDesignator.Length,
                d.PMDesignator.Length
            }.Max();
            int length = Math.Max(64, format?.Length ?? 32);
            // Bounded runtime scalars and the validated culture expand at most this conservative envelope.
            context.Consume(PdfResourceKind.ExpandedBytes, (length + 512L) * (expansion + 1L) * 2 + 128);
        }

        string result;
        if (value is string text)
        {
            if (format != null)
                throw PdfWriterCore.Error(context, "Table.StringFormat", "A string scalar does not accept an implicit numeric/date format.");
            result = text;
        }
        else if (value is IFormattable scalar)
            result = scalar.ToString(format, culture) ?? string.Empty;
        else
            result = value is bool boolean ? (boolean ? bool.TrueString : bool.FalseString) : ((char)value).ToString();
        if (result.Length > 1000000)
            throw PdfWriterCore.Error(context, "Table.FormattedValueLimit", "A formatted scalar exceeds one million UTF-16 units.");
        return result;
    }

    internal static void ValidateCulture(CultureInfo culture, PdfOperationContext context)
    {
        if (culture.GetType() != typeof(CultureInfo))
            throw PdfWriterCore.Error(context, "Table.CultureType", "Binding requires a captured standard CultureInfo instance.");
        var n = culture.NumberFormat;
        var d = culture.DateTimeFormat;
        var symbols = new[]
        {
            n.CurrencySymbol,
            n.CurrencyDecimalSeparator,
            n.CurrencyGroupSeparator,
            n.NumberDecimalSeparator,
            n.NumberGroupSeparator,
            n.PercentDecimalSeparator,
            n.PercentGroupSeparator,
            n.PercentSymbol,
            n.PerMilleSymbol,
            n.NegativeSign,
            n.PositiveSign,
            n.NaNSymbol,
            n.NegativeInfinitySymbol,
            n.PositiveInfinitySymbol,
            d.AMDesignator,
            d.PMDesignator,
            d.DateSeparator,
            d.TimeSeparator
        };
        if (symbols.Any(t => t.Length > 64) || n.NativeDigits.Any(t => t.Length > 8) || d.MonthNames.Any(t => t.Length > 64) || d.MonthGenitiveNames.Any(t => t.Length > 64) || d.AbbreviatedMonthNames.Any(t => t.Length > 64) || d.AbbreviatedMonthGenitiveNames.Any(t => t.Length > 64) || d.DayNames.Any(t => t.Length > 64) || d.AbbreviatedDayNames.Any(t => t.Length > 64))
            throw PdfWriterCore.Error(context, "Table.CultureLimit", "Culture symbols and names exceed their bounded scalar-format profile.");
        var patterns = new[]
        {
            d.ShortDatePattern,
            d.LongDatePattern,
            d.ShortTimePattern,
            d.LongTimePattern,
            d.FullDateTimePattern,
            d.MonthDayPattern,
            d.YearMonthPattern
        };
        if (patterns.Any(t => t.Length > 256) || d.Calendar.GetType().Assembly != typeof(Calendar).Assembly)
            throw PdfWriterCore.Error(context, "Table.CultureLimit", "Culture patterns or calendars exceed the bounded runtime-format profile.");
        context.Consume(PdfResourceKind.ExpandedBytes, patterns.Sum(t => (long)t.Length * 2) + 4096);
    }

    private static bool Scalar(object value) => value is string || value is char || value is bool || value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal || value is DateTime || value is DateTimeOffset || value is TimeSpan || value is Guid;
}
