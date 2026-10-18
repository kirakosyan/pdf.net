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
    private PdfContentProgram? _content;
    private readonly Dictionary<string,PdfFontResource> _fonts=new(StringComparer.Ordinal);
    private readonly Dictionary<(PdfGraphicsResourceCategory Category,string Name),PdfGraphicsResource> _graphics=new();
    /// <summary>Captures the explicit replacement graphics bindings; original inherited resources remain in Source.</summary>
    public PdfResourceSet GraphicsResources{get{EnsureAttached();return SnapshotGraphicsResources(null);}}
    /// <summary>Registers or replaces a named graphics resource in its standard category.</summary>
    public void SetResource(string resourceName,PdfGraphicsResource resource)
    {
        EnsureEditable();var binding=new PdfResourceBinding(resourceName,resource);
        if(resource.FontResource is PdfFontResource font){SetFont(resourceName,font);return;}
        var key=(resource.Category,resourceName);if(!_graphics.ContainsKey(key)&&_graphics.Count>=4096)throw new InvalidOperationException("A page supports at most 4096 explicit graphics bindings.");_graphics[key]=binding.Resource;
    }
    /// <summary>Removes a replacement binding, retaining an original source binding when present.</summary>
    public bool RemoveResource(PdfGraphicsResourceCategory category,string resourceName)
    {EnsureEditable();if(!Enum.IsDefined(typeof(PdfGraphicsResourceCategory),category))throw new ArgumentOutOfRangeException(nameof(category));if(resourceName==null)throw new ArgumentNullException(nameof(resourceName));return category==PdfGraphicsResourceCategory.Font?RemoveFont(resourceName):_graphics.Remove((category,resourceName));}
    internal PdfResourceSet SnapshotGraphicsResources(PdfOperationContext? context)
    {context?.Consume(PdfResourceKind.Objects,_graphics.Count);context?.Consume(PdfResourceKind.ExpandedBytes,_graphics.Count*192L+256);return _graphics.Count==0?PdfResourceSet.Empty:new PdfResourceSet(_graphics.OrderBy(x=>x.Key.Category).ThenBy(x=>x.Key.Name,StringComparer.Ordinal).Select(x=>new PdfResourceBinding(x.Key.Name,x.Value)).ToArray());}

    /// <summary>Registers or replaces an exact named font resource for subsequent saves.</summary>
    public void SetFont(string resourceName,PdfFontResource font)
    {EnsureEditable();Guard.Text(resourceName,nameof(resourceName),127);PdfValue.Name(resourceName);if(font==null)throw new ArgumentNullException(nameof(font));_fonts[resourceName]=font;}
    /// <summary>Removes a replacement binding, retaining the original source binding if one exists.</summary>
    public bool RemoveFont(string resourceName)
    {EnsureEditable();if(resourceName==null)throw new ArgumentNullException(nameof(resourceName));return _fonts.Remove(resourceName);}
    internal IReadOnlyDictionary<string,PdfFontResource> SnapshotFonts(PdfOperationContext? context)
    {
        context?.Consume(PdfResourceKind.Objects,_fonts.Count);context?.Consume(PdfResourceKind.ExpandedBytes,_fonts.Count*128L+256);
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string,PdfFontResource>(new Dictionary<string,PdfFontResource>(_fonts,StringComparer.Ordinal));
    }
    /// <summary>Gets or sets replacement decoded content. Null retains original content; an empty program clears it.</summary>
    public PdfContentProgram? Content
    {
        get{EnsureAttached();return _content;}
        set{EnsureEditable();if(value!=null&&value.Kind!=PdfContentKind.Page)throw new ArgumentException("Page content cannot be a Type 3 glyph program.",nameof(value));_content=value;}
    }
    /// <summary>Reads original or replacement operators under the document's operation policy without executing document actions.</summary>
    public PdfContentProgram ReadContent(PdfUnknownOperatorPolicy unknownOperators=PdfUnknownOperatorPolicy.Reject,string? unknownOperatorVersion=null,System.Threading.CancellationToken cancellationToken=default)
    {EnsureAttached();return _owner.ReadPageContent(this,unknownOperators,unknownOperatorVersion,cancellationToken);}
    /// <summary>Inspects detached original/replacement font bindings under the document policy without executing document actions.</summary>
    public IReadOnlyList<PdfPageFont> ReadFonts(System.Threading.CancellationToken cancellationToken=default)
    {EnsureAttached();return _owner.ReadPageFonts(this,cancellationToken);}
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
