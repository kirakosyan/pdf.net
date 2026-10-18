namespace Fluent.Pdf;
/// <summary>An immutable nested bookmark with an optional local destination or passive action.</summary>
public sealed class PdfBookmark
{
    /// <summary>Creates a bounded bookmark. Destinations and actions are mutually exclusive; children retain their order.</summary>
    public PdfBookmark(string title, PdfDestination? destination = null, PdfAction? action = null, bool isOpen = true, bool bold = false, bool italic = false, PdfColor? color = null, params PdfBookmark[] children)
    {
        if (string.IsNullOrEmpty(title) || title.Length > 8192)
            throw new ArgumentException("A nonempty bounded title is required.", nameof(title));
        if (destination != null && action != null)
            throw new ArgumentException("Choose a destination or an action.", nameof(action));
        if (color != null && (!ReferenceEquals(color.ColorSpace, PdfColorSpace.Rgb) || color.Components.Count != 3))
            throw new ArgumentException("Bookmark colors require explicit device RGB.", nameof(color));
        if (children == null || children.Length > 4096 || children.Any(c => c == null))
            throw new ArgumentException("Supply up to 4096 nonnull children.", nameof(children));
        Title = title;
        Destination = destination;
        Action = action;
        IsOpen = isOpen;
        Bold = bold;
        Italic = italic;
        Color = color;
        Children = System.Array.AsReadOnly((PdfBookmark[])children.Clone());
    }

    /// <summary>Returns a bookmark with replaced ordered children, retaining destination, action, expansion and styling.</summary>
    public PdfBookmark WithChildren(params PdfBookmark[] children) => new(Title, Destination, Action, IsOpen, Bold, Italic, Color, children);
    /// <summary>Gets the Unicode title.</summary>
    public string Title { get; }
    /// <summary>Gets the optional local destination.</summary>
    public PdfDestination? Destination { get; }
    /// <summary>Gets the optional passive action.</summary>
    public PdfAction? Action { get; }
    /// <summary>Gets the initial expansion state.</summary>
    public bool IsOpen { get; }
    /// <summary>Gets bold title styling.</summary>
    public bool Bold { get; }
    /// <summary>Gets italic title styling.</summary>
    public bool Italic { get; }
    /// <summary>Gets optional device RGB title color.</summary>
    public PdfColor? Color { get; }
    /// <summary>Gets the immutable ordered children.</summary>
    public IReadOnlyList<PdfBookmark> Children { get; }
}

/// <summary>A passive outline observation preserving unknown source keys.</summary>
public sealed class PdfOutlineRecord
{
    internal PdfOutlineRecord(PdfObjectId id, string title, PdfValue definition, int? target, bool open, IReadOnlyList<PdfOutlineRecord> children)
    {
        ObjectId = id;
        Title = title;
        Definition = definition;
        TargetPageIndex = target;
        IsOpen = open;
        Children = children;
        VisibleCount = 1 + (open ? children.Sum(c => c.VisibleCount) : 0);
    }

    internal int VisibleCount { get; }
    /// <summary>Gets the native source identity.</summary>
    public PdfObjectId ObjectId { get; }
    /// <summary>Gets the decoded Unicode title.</summary>
    public string Title { get; }
    /// <summary>Gets the original dictionary, including unsupported keys and actions.</summary>
    public PdfValue Definition { get; }
    /// <summary>Gets a resolved local target, or null for a grouping/remote/other action.</summary>
    public int? TargetPageIndex { get; }
    /// <summary>Gets the source expansion state.</summary>
    public bool IsOpen { get; }
    /// <summary>Gets the ordered immutable children.</summary>
    public IReadOnlyList<PdfOutlineRecord> Children { get; }
}
