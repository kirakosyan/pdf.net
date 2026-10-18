namespace Fluent.Pdf.Word;
internal sealed class WordModel
{
    internal readonly List<WordSection> Sections = new();
}

internal sealed class WordSection
{
    internal double Width = 612, Height = 792;
    internal readonly List<WordBlock> Blocks = new();
}

internal abstract class WordBlock
{
}

internal sealed class WordParagraph : WordBlock
{
    internal readonly List<WordRun> Runs = new();
    internal int Heading;
}

internal sealed class WordRun
{
    internal string Text = "", Family = "Noto Sans";
    internal double Size = 12;
    internal bool Bold, Italic;
}

internal sealed class WordPicture : WordBlock
{
    internal PdfByteStore Bytes = null!;
    internal double Width, Height;
    internal bool Fixed;
}

internal sealed class WordTable : WordBlock
{
    internal readonly List<List<WordParagraph>> Rows = new();
    internal readonly List<double> Widths = new();
}

internal static class WordErrors
{
    internal static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Word." + code, message);
    internal static void Loss(PdfOperationContext c, string code, string message) => c.Report(new("Word." + code, message, lossCategory: PdfLossCategory.FidelityReduction));
    internal static void Node(PdfOperationContext c, long bytes = 256)
    {
        c.Checkpoint();
        c.Consume(PdfResourceKind.Objects, 1);
        c.Consume(PdfResourceKind.ExpandedBytes, bytes);
    }
}
