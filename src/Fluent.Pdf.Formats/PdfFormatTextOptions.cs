namespace Fluent.Pdf.Formats;
/// <summary>Caller-owned explicit typography and flow geometry for data/text reconstruction. No font or locale discovery occurs.</summary>
public sealed class PdfFormatTextOptions
{
    /// <summary>Captures an explicit body style and bounded page layout. Source data conversion does not promise original spreadsheet/book/typesetting geometry.</summary>
    public PdfFormatTextOptions(PdfFlowTextStyle body, PdfFlowOptions? layout = null)
    {
        Body = body ?? throw new ArgumentNullException(nameof(body));
        Layout = layout ?? new(new(new(new(0, 0, PdfPageSize.A4.Width, PdfPageSize.A4.Height))));
    }

    /// <summary>Gets explicit text style and font bindings.</summary>
    public PdfFlowTextStyle Body { get; }
    /// <summary>Gets explicit page flow policy.</summary>
    public PdfFlowOptions Layout { get; }
}

internal static class FormatText
{
    internal static PdfDocument Build(IEnumerable<PdfFlowBlock> blocks, PdfFormatTextOptions options, PdfProviderRegistry providers, PdfOperationContext c, List<Action<PdfOperationReport>> finish)
    {
        var flow = PdfFlow.LayoutCore(blocks, options.Layout, providers, c, out var completed);
        finish.Add(completed);
        return flow.CreateDocumentCore(new(maximumPages: options.Layout.MaximumPages, operationPolicy: c.Policy), providers, c, finish);
    }

    internal static List<PdfFlowBlock> Paragraphs(IEnumerable<string> values, PdfFormatTextOptions options, PdfOperationContext c)
    {
        var result = new List<PdfFlowBlock>();
        foreach (string value in values)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, value.Length * 4L + 256);
            result.Add(new PdfFlowParagraph(value, options.Body));
        }

        return result;
    }
}
