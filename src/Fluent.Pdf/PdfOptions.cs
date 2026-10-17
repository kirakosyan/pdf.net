namespace Fluent.Pdf;

/// <summary>Immutable options captured when a document session is created.</summary>
/// <remarks>Page limits apply to the model; the immutable operation policy creates independent read/save budgets.</remarks>
public sealed class PdfOptions
{
    /// <summary>Creates options with a valid default size and a positive page limit.</summary>
    public PdfOptions(PdfPageSize? defaultPageSize = null, int maximumPages = 10000, PdfOperationPolicy? operationPolicy = null)
    {
        var size = defaultPageSize ?? PdfPageSize.A4;
        Guard.Size(size, nameof(defaultPageSize));
        if (maximumPages <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPages));
        DefaultPageSize = size;
        MaximumPages = maximumPages;
        OperationPolicy = operationPolicy ?? PdfOperationPolicy.Default;
    }

    /// <summary>Gets the shared immutable default options.</summary>
    public static PdfOptions Default { get; } = new();
    /// <summary>Gets the size used by parameterless page creation.</summary>
    public PdfPageSize DefaultPageSize { get; }
    /// <summary>Gets the maximum number of pages retained by a document.</summary>
    public int MaximumPages { get; }
    /// <summary>Gets the immutable policy applied independently to each read/save operation.</summary>
    public PdfOperationPolicy OperationPolicy { get; }
}
