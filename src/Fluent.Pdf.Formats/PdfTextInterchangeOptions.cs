namespace Fluent.Pdf.Formats;

/// <summary>Explicit interchange representation.</summary>
public enum PdfTextInterchangeFormat
{
    /// <summary>UTF8 literal text with source-page references.</summary>
    PlainText,
    /// <summary>Escaped CommonMark with optional pipe-table extension; no image files.</summary>
    Markdown,
    /// <summary>Deterministic ZIP with document.md and explicitly rendered PNG page assets.</summary>
    MarkdownBundle
}

/// <summary>Caller-assigned block role; never an assertion that the PDF declares this semantic role.</summary>
public enum PdfTextInterchangeRole
{
    /// <summary>Literal paragraph.</summary>
    Paragraph,
    /// <summary>Explicit level1–6 heading.</summary>
    Heading,
    /// <summary>Flat bullet item.</summary>
    Bullet,
    /// <summary>Flat ordered item with explicit counter.</summary>
    Numbered,
    /// <summary>Literal fenced code.</summary>
    Code,
    /// <summary>Literal label with explicit HTTPS target.</summary>
    Link,
    /// <summary>Explicit ruled/borderless table inference; only unit cell spans are supported.</summary>
    Table,
    /// <summary>Whole page rendered to a generated relative PNG asset; bundle only.</summary>
    PageImage
}

/// <summary>Text order is explicit and independent of block ordering.</summary>
public enum PdfTextInterchangeOrder
{
    /// <summary>Source text execution order.</summary>
    Raw,
    /// <summary>Geometrically inferred grouped text.</summary>
    Grouped,
    /// <summary>Geometrically inferred spacing/layout text.</summary>
    Layout
}

/// <summary>Immutable caller-assigned source-page projection. Enumerable order is reading order; repeats are intentional.</summary>
public sealed class PdfTextInterchangeBlock
{
    /// <summary>Selects a page and role. Optional raw character ranges use extracted source-character indices, not UTF16 offsets. Tables and images cannot use ranges.</summary>
    public PdfTextInterchangeBlock(int pageIndex, PdfTextInterchangeRole role = PdfTextInterchangeRole.Paragraph, PdfTextInterchangeOrder order = PdfTextInterchangeOrder.Grouped, int? firstCharacter = null, int? characterCount = null, int headingLevel = 1, int number = 1, string? link = null, PdfTableExtractionOptions? table = null)
    {
        if (pageIndex < 0 || pageIndex >= 10000) throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (!Enum.IsDefined(typeof(PdfTextInterchangeRole), role) || !Enum.IsDefined(typeof(PdfTextInterchangeOrder), order)) throw new ArgumentOutOfRangeException(nameof(role));
        if (firstCharacter.HasValue != characterCount.HasValue || firstCharacter < 0 || characterCount < 1) throw new ArgumentException("Supply both a nonnegative first character and positive count.");
        if (firstCharacter.HasValue && (order != PdfTextInterchangeOrder.Raw || role == PdfTextInterchangeRole.Table || role == PdfTextInterchangeRole.PageImage)) throw new ArgumentException("Character ranges require raw text order and a text role.");
        if (headingLevel < 1 || headingLevel > 6 || number < 1 || number > 999999999) throw new ArgumentOutOfRangeException(nameof(headingLevel));
        if ((role == PdfTextInterchangeRole.Link) != (link != null)) throw new ArgumentException("Only link roles require an explicit target.");
        if (link != null)
        {
            Guard.Text(link, nameof(link), 4096);
            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0 || link.Any(ch => char.IsControl(ch) || char.IsWhiteSpace(ch)) || link.IndexOfAny(new[] { '<', '>', '"', '\\' }) >= 0)
                throw new ArgumentException("An explicit HTTPS URI without credentials, whitespace or markup is required.", nameof(link));
        }
        if ((role == PdfTextInterchangeRole.Table) != (table != null)) throw new ArgumentException("Only table roles require a table inference profile.");
        if (table != null && (table.JoinConsecutivePages || table.HeaderRows > 1)) throw new ArgumentException("This profile supports one page and zero or one explicit header row.", nameof(table));
        PageIndex = pageIndex; Role = role; Order = order; FirstCharacter = firstCharacter; CharacterCount = characterCount;
        HeadingLevel = headingLevel; Number = number; Link = link; Table = table;
    }
    /// <summary>Gets zero-based source page.</summary>
    public int PageIndex { get; }
    /// <summary>Gets caller-assigned role.</summary>
    public PdfTextInterchangeRole Role { get; }
    /// <summary>Gets explicit text order.</summary>
    public PdfTextInterchangeOrder Order { get; }
    /// <summary>Gets optional raw character offset.</summary>
    public int? FirstCharacter { get; }
    /// <summary>Gets optional raw character count.</summary>
    public int? CharacterCount { get; }
    /// <summary>Gets explicit heading level.</summary>
    public int HeadingLevel { get; }
    /// <summary>Gets explicit flat-list counter.</summary>
    public int Number { get; }
    /// <summary>Gets explicit HTTPS link. It is never fetched.</summary>
    public string? Link { get; }
    /// <summary>Gets explicit table inference profile.</summary>
    public PdfTableExtractionOptions? Table { get; }
}

/// <summary>Exact-source binding, text selection and finite export extent.</summary>
public sealed class PdfTextInterchangeOptions
{
    /// <summary>Requires the expected SHA256 of captured PDF bytes to prevent applying a stale semantic projection. Image rendering is optional and must cover the whole page.</summary>
    public PdfTextInterchangeOptions(string sourceSha256, PdfTextExtractionOptions? text = null, PdfRenderOptions? rendering = null, int maximumOutputBytes = 16777216)
    {
        Guard.Text(sourceSha256, nameof(sourceSha256), 64);
        if (sourceSha256.Length != 64 || sourceSha256.Any(ch => !(ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f'))) throw new ArgumentException("A lowercase SHA256 digest is required.", nameof(sourceSha256));
        if (maximumOutputBytes < 1 || maximumOutputBytes > 16777216) throw new ArgumentOutOfRangeException(nameof(maximumOutputBytes));
        if (rendering?.Region != null) throw new ArgumentException("Page images require whole-page rendering.", nameof(rendering));
        SourceSha256 = sourceSha256; Text = text ?? new(includeInvisibleText: false); Rendering = rendering; MaximumOutputBytes = maximumOutputBytes;
    }
    /// <summary>Gets exact expected captured-source digest.</summary>
    public string SourceSha256 { get; }
    /// <summary>Gets text/layer selection.</summary>
    public PdfTextExtractionOptions Text { get; }
    /// <summary>Gets optional explicit whole-page raster policy.</summary>
    public PdfRenderOptions? Rendering { get; }
    /// <summary>Gets the UTF8 document extent; bundle extent is additionally limited to64MiB.</summary>
    public int MaximumOutputBytes { get; }
}
