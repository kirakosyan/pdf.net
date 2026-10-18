namespace Fluent.Pdf;

/// <summary>A page owned by one document session; removed or disposed pages cannot be reused.</summary>
public sealed class PdfPage
{
    private readonly PdfDocument _owner;
    private PdfPageSize _size;
    private PdfPageRotation _rotation;
    private bool _attached = true;

    internal PdfPage(PdfDocument owner, PdfPageSize size) { _owner = owner; _size = size; }
    private PdfPageSource? _source;
    /// <summary>Gets the immutable original binding, or null for a newly authored page.</summary>
    public PdfPageSource? Source{get{EnsureAttached();return _source;}}
    internal void Bind(PdfPageSource source){_source=source;_rotation=source.Rotation;}

    /// <summary>Gets or changes the unrotated physical page dimensions.</summary>
    public PdfPageSize Size
    {
        get { EnsureAttached(); return _size; }
        set { EnsureEditable(); Guard.Size(value, nameof(value)); _size = value; }
    }

    /// <summary>Gets or changes the clockwise display rotation without changing the physical size.</summary>
    public PdfPageRotation Rotation
    {
        get { EnsureAttached(); return _rotation; }
        set
        {
            EnsureEditable();
            if (!Enum.IsDefined(typeof(PdfPageRotation), value)) throw new ArgumentOutOfRangeException(nameof(value));
            _rotation = value;
        }
    }

    internal void Detach() => _attached = false;

    private void EnsureAttached()
    {
        _owner.EnsureAlive();
        if (!_attached) throw new InvalidOperationException("The page has been removed from its document.");
    }

    private void EnsureEditable() { EnsureAttached(); _owner.EnsureEditable(); }
}
