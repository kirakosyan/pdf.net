using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable ordered zero-based selection; duplicate indices request independent page copies.</summary>
public sealed class PdfPageSelection
{
    private PdfPageSelection(int[] indices) => Indices = new ReadOnlyCollection<int>(indices);
    /// <summary>Gets physical page indices in requested output order.</summary>
    public IReadOnlyList<int> Indices { get; }

    /// <summary>Parses one-based numbers, inclusive ascending/descending ranges, last, all, odd and even. Commas preserve order and duplicates.</summary>
    public static PdfPageSelection Parse(string expression, int pageCount, int maximumSelectedPages = 100000, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (expression == null || expression.Length == 0 || expression.Length > 1000000)
            throw new ArgumentException("A bounded nonempty page expression is required.", nameof(expression));
        if (pageCount < 1 || pageCount > 1000000 || maximumSelectedPages < 1 || maximumSelectedPages > 1000000)
            throw new ArgumentOutOfRangeException(nameof(pageCount));
        PdfPageSelection? result = null;
        PdfFont.Run("SelectPages", "managed.organization", policy, cancellationToken, context => result = ParseCore(expression, pageCount, maximumSelectedPages, context), PdfCapability.ReadDocument);
        return result!;
    }

    internal static PdfPageSelection ParseCore(string expression, int pageCount, int maximum, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, expression.Length * 32L + 128);
        var values = new List<int>();
        void Add(int index)
        {
            context.Checkpoint();
            if (values.Count == maximum)
                throw PdfWriterCore.Error(context, "Organization.SelectionLimit", "The page selection exceeds its explicit finite limit.");
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 16);
            values.Add(index);
        }

        int Number(string token)
        {
            if (token == "last")
                return pageCount;
            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 1 || value > pageCount)
                throw PdfWriterCore.Error(context, "Organization.PageRange", "Every page number must identify an existing one-based physical page.");
            return value;
        }

        foreach (string item in expression.Split(','))
        {
            string token = item.Trim().ToLowerInvariant();
            if (token == "all" || token == "odd" || token == "even")
            {
                for (int i = token == "even" ? 1 : 0; i < pageCount; i += token == "all" ? 1 : 2)
                    Add(i);
                continue;
            }

            var parts = token.Split('-');
            if (parts.Length > 2 || parts.Length == 0)
                throw PdfWriterCore.Error(context, "Organization.RangeSyntax", "Use comma-separated pages or inclusive two-endpoint ranges.");
            int start = Number(parts[0].Trim()), end = parts.Length == 1 ? start : Number(parts[1].Trim()), step = start <= end ? 1 : -1;
            for (int n = start;; n += step)
            {
                Add(n - 1);
                if (n == end)
                    break;
            }
        }

        if (values.Count == 0)
            throw PdfWriterCore.Error(context, "Organization.EmptySelection", "The page selection is empty.");
        return new(values.ToArray());
    }

    internal static PdfPageSelection CreateCore(IEnumerable<int> indices, PdfOperationContext context)
    {
        var copy = new List<int>();
        foreach (int index in indices)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 16);
            if (index < 0 || copy.Count == 1000000)
                throw PdfWriterCore.Error(context, "Organization.PageRange", "Internal page selections require bounded nonnegative indices.");
            copy.Add(index);
        }

        if (copy.Count == 0)
            throw PdfWriterCore.Error(context, "Organization.EmptySelection", "A page selection must retain at least one page.");
        return new(copy.ToArray());
    }
}
