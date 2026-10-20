using System.Collections.ObjectModel;

namespace Fluent.Pdf;
/// <summary>A finite closed merge instruction. No node contains executable callbacks, reflected member access or resource resolution.</summary>
public abstract class PdfMergeBlock
{
    internal PdfMergeBlock()
    {
    }

    internal static IReadOnlyList<PdfMergeBlock> Capture(PdfMergeBlock[] blocks)
    {
        if (blocks == null || blocks.Length > 4096 || blocks.Any(b => b == null))
            throw new ArgumentException("At most4096nonnull merge blocks are accepted.", nameof(blocks));
        return Array.AsReadOnly((PdfMergeBlock[])blocks.Clone());
    }
}

/// <summary>An immutable literal or scalar-field run with an explicitly named text style.</summary>
public sealed class PdfMergeRun
{
    private PdfMergeRun(string value, string style, string? format, bool field)
    {
        if (value == null || value.Length > 1000000)
            throw new ArgumentException("Bounded text is required.", nameof(value));
        if (!PdfMergeRecord.Identifier(style))
            throw new ArgumentException("A bounded style identifier is required.", nameof(style));
        if (format?.Length > 256)
            throw new ArgumentException("A bounded explicit format is required.", nameof(format));
        if (field)
            PdfMergeRecord.Path(value);
        Value = value;
        Style = style;
        Format = format;
        IsField = field;
    }

    /// <summary>Creates literal text. Placeholder-looking data remains literal.</summary>
    public static PdfMergeRun Text(string text, string style = "body") => new(text, style, null, false);
    /// <summary>Creates one closed scalar-field lookup with an optional explicit standard/custom scalar format.</summary>
    public static PdfMergeRun Field(string path, string style = "body", string? format = null) => new(path, style, format, true);
    /// <summary>Gets literal text or a field path.</summary>
    public string Value { get; }
    /// <summary>Gets the explicit style name.</summary>
    public string Style { get; }
    /// <summary>Gets the optional scalar format.</summary>
    public string? Format { get; }
    /// <summary>Gets whether this is a field lookup.</summary>
    public bool IsField { get; }
}

/// <summary>A native paragraph with finite literal/field runs and explicit paragraph geometry.</summary>
public sealed class PdfMergeParagraph : PdfMergeBlock
{
    /// <summary>Captures at most4096nonempty runs and immutable paragraph options.</summary>
    public PdfMergeParagraph(PdfMergeRun[] runs, PdfFlowParagraphStyle? style = null)
    {
        if (runs == null || runs.Length == 0 || runs.Length > 4096 || runs.Any(r => r == null))
            throw new ArgumentException("One to4096nonnull merge runs are required.", nameof(runs));
        Runs = Array.AsReadOnly((PdfMergeRun[])runs.Clone());
        Style = style ?? new();
    }

    /// <summary>Gets immutable runs.</summary>
    public IReadOnlyList<PdfMergeRun> Runs { get; }
    /// <summary>Gets paragraph layout options.</summary>
    public PdfFlowParagraphStyle Style { get; }
}

/// <summary>A finite record repetition; child paths are relative to each immutable row, with root access explicitly prefixed by root.</summary>
public sealed class PdfMergeRepeat : PdfMergeBlock
{
    /// <summary>Captures a closed collection path and finite children.</summary>
    public PdfMergeRepeat(string path, params PdfMergeBlock[] children)
    {
        PdfMergeRecord.Path(path);
        Path = path;
        Children = Capture(children);
    }

    /// <summary>Gets the collection field path.</summary>
    public string Path { get; }
    /// <summary>Gets immutable child instructions.</summary>
    public IReadOnlyList<PdfMergeBlock> Children { get; }
}

/// <summary>A boolean-only branch. No truthiness coercion or expression execution occurs.</summary>
public sealed class PdfMergeCondition : PdfMergeBlock
{
    /// <summary>Captures the boolean path and explicit true/false instruction lists.</summary>
    public PdfMergeCondition(string path, PdfMergeBlock[] whenTrue, PdfMergeBlock[]? whenFalse = null)
    {
        PdfMergeRecord.Path(path);
        Path = path;
        WhenTrue = Capture(whenTrue);
        WhenFalse = Capture(whenFalse ?? Array.Empty<PdfMergeBlock>());
    }

    /// <summary>Gets the boolean field path.</summary>
    public string Path { get; }
    /// <summary>Gets the true instructions.</summary>
    public IReadOnlyList<PdfMergeBlock> WhenTrue { get; }
    /// <summary>Gets the false instructions.</summary>
    public IReadOnlyList<PdfMergeBlock> WhenFalse { get; }
}

/// <summary>An explicit captured image field. Native output places it before subsequent text; source bytes and geometry remain caller-selected.</summary>
public sealed class PdfMergeImageBlock : PdfMergeBlock
{
    /// <summary>Captures one closed image field path.</summary>
    public PdfMergeImageBlock(string path)
    {
        PdfMergeRecord.Path(path);
        Path = path;
    }

    /// <summary>Gets the image field path.</summary>
    public string Path { get; }
}

/// <summary>A finite immutable native PDF template program.</summary>
public sealed class PdfMergeTemplate
{
    /// <summary>Captures an ASCII identifier and at most4096nonnull instructions.</summary>
    public PdfMergeTemplate(string identifier, params PdfMergeBlock[] blocks)
    {
        if (!PdfMergeRecord.Identifier(identifier) || identifier.Any(ch => ch > 127))
            throw new ArgumentException("An ASCII template identifier is required.", nameof(identifier));
        Identifier = identifier;
        Blocks = PdfMergeBlock.Capture(blocks);
    }

    /// <summary>Gets the stable template identifier.</summary>
    public string Identifier { get; }
    /// <summary>Gets immutable root instructions.</summary>
    public IReadOnlyList<PdfMergeBlock> Blocks { get; }
}

/// <summary>An explicit data-table column, with closed scalar/literal cell runs and optional header text.</summary>
public sealed class PdfMergeTableColumn
{
    /// <summary>Captures column geometry, at most4096nonnull runs and an optional literal header.</summary>
    public PdfMergeTableColumn(PdfTableColumn column, PdfMergeRun[] runs, string? header = null)
    {
        Column = column ?? throw new ArgumentNullException(nameof(column));
        if (runs == null || runs.Length == 0 || runs.Length > 4096 || runs.Any(r => r == null) || header?.Length > 1000000)
            throw new ArgumentException("Bounded nonempty cell runs and header text are required.", nameof(runs));
        Runs = Array.AsReadOnly((PdfMergeRun[])runs.Clone());
        Header = header;
    }

    /// <summary>Gets immutable column width policy.</summary>
    public PdfTableColumn Column { get; }
    /// <summary>Gets immutable row-relative cell runs.</summary>
    public IReadOnlyList<PdfMergeRun> Runs { get; }
    /// <summary>Gets optional literal header text; null means no header text for this column.</summary>
    public string? Header { get; }
}

/// <summary>A finite native table bound to a closed record collection.</summary>
public sealed class PdfMergeTable : PdfMergeBlock
{
    /// <summary>Captures a collection path and one to256nonnull columns. Header rows repeat through the ordinary table engine.</summary>
    public PdfMergeTable(string path, params PdfMergeTableColumn[] columns)
    {
        PdfMergeRecord.Path(path);
        if (columns == null || columns.Length == 0 || columns.Length > 256 || columns.Any(c => c == null))
            throw new ArgumentException("One to256nonnull columns are required.", nameof(columns));
        Path = path;
        Columns = Array.AsReadOnly((PdfMergeTableColumn[])columns.Clone());
    }

    /// <summary>Gets the collection path.</summary>
    public string Path { get; }
    /// <summary>Gets immutable column definitions.</summary>
    public IReadOnlyList<PdfMergeTableColumn> Columns { get; }
}
