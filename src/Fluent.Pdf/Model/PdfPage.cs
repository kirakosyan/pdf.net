namespace Fluent.Pdf;

/// <summary>A page owned by one document session; removed or disposed pages cannot be reused.</summary>
public sealed class PdfPage
{
    private readonly PdfDocument _owner;
    private PdfPageSize _size;
    private PdfPageRotation _rotation;
    private bool _attached = true;
    private PdfPageGeometry _geometry;

    internal PdfPage(PdfDocument owner, PdfPageSize size) { _owner = owner; _size = size;_geometry=new(new PdfPageBoxes(new PdfRect(0,0,size.Width,size.Height))); }
    /// <summary>Gets or atomically replaces explicit page boxes, UserUnit and rotation.</summary>
    public PdfPageGeometry Geometry
    {get{EnsureAttached();return _geometry;}set{EnsureEditable();if(value==null)throw new ArgumentNullException(nameof(value));_geometry=value;_size=value.Size;_rotation=value.Rotation;}}
    internal IReadOnlyList<string> StructureRoles { get; private set; } = System.Array.Empty<string>();
    internal void SetStructureRoles(IReadOnlyList<string> roles) { EnsureEditable(); StructureRoles = roles; }
    private readonly List<(PdfRect Bounds, PdfPage Target, PdfPoint Point)> _deferredLinks = new();
    internal void AddDeferredLink(PdfRect bounds, PdfPage target, PdfPoint point, PdfOperationContext context)
    { EnsureEditable(); target.EnsureAttached(); if(!ReferenceEquals(_owner,target._owner))throw new ArgumentException("A deferred destination belongs to another document.",nameof(target));if(bounds.Width<=0||bounds.Height<=0)throw new ArgumentException("A link requires a positive rectangle.",nameof(bounds));context.Consume(PdfResourceKind.Objects,1);context.Consume(PdfResourceKind.ExpandedBytes,96);_deferredLinks.Add((bounds,target,point)); }
    internal IReadOnlyList<PdfPageLinkSnapshot> SnapshotLinks(IReadOnlyDictionary<PdfPage,int> indices,PdfOperationContext? context)
    { context?.Consume(PdfResourceKind.ExpandedBytes,_deferredLinks.Count*64L+32);var values=new PdfPageLinkSnapshot[_deferredLinks.Count];for(int i=0;i<values.Length;i++){if(!indices.TryGetValue(_deferredLinks[i].Target,out int target))throw new InvalidOperationException("An internal destination page has been removed.");values[i]=new(_deferredLinks[i].Bounds,target,_deferredLinks[i].Point);}return System.Array.AsReadOnly(values); }
    private PdfPageSource? _source;
    private PdfContentProgram? _content;
    private IReadOnlyList<PdfCanvasPageLayer> _canvasLayers=System.Array.Empty<PdfCanvasPageLayer>();
    internal int CanvasSequence{get;private set;}
    /// <summary>Gets immutable generated content layers in application order.</summary>
    public IReadOnlyList<PdfCanvasPageLayer> CanvasLayers{get{EnsureAttached();return _canvasLayers;}}
    internal IReadOnlyList<PdfCanvasPageLayer> SnapshotCanvasLayers(PdfOperationContext? context)
    {context?.Consume(PdfResourceKind.ExpandedBytes,_canvasLayers.Count*16L+32);return _canvasLayers.Count==0?System.Array.Empty<PdfCanvasPageLayer>():System.Array.AsReadOnly(_canvasLayers.ToArray());}
    internal PdfOperationReport ApplyCanvas(PdfCanvasResult canvas,PdfCanvasApplyMode mode,System.Threading.CancellationToken token)
    {EnsureEditable();if(StructureRoles.Count>0&&mode!=PdfCanvasApplyMode.Replace)throw new InvalidOperationException("Structured page overlays require an explicit structure edit profile.");return _owner.ApplyCanvas(this,canvas,mode,token);}
    internal void CommitCanvas(PdfResourceBinding binding,PdfContentProgram program,PdfCanvasApplyMode mode,int sequence,PdfOperationContext context)
    {
        EnsureEditable();var key=(PdfGraphicsResourceCategory.XObject,binding.Name);if(!_graphics.ContainsKey(key)&&_graphics.Count>=4096)throw PdfWriterCore.Error(context,"Canvas.ResourceLimit","The page replacement resource scope is full; compose paint layers into one canvas.");
        context.Consume(PdfResourceKind.ExpandedBytes,_graphics.Count*192L+_canvasLayers.Count*32L+1024);var resources=new Dictionary<(PdfGraphicsResourceCategory,string),PdfGraphicsResource>(_graphics){[key]=binding.Resource};
        IReadOnlyList<PdfCanvasPageLayer> layers=mode==PdfCanvasApplyMode.Replace?System.Array.Empty<PdfCanvasPageLayer>():System.Array.AsReadOnly(_canvasLayers.Concat(new[]{new PdfCanvasPageLayer(mode,binding.Name,program)}).ToArray());
        context.Checkpoint();_graphics=resources;_canvasLayers=layers;CanvasSequence=sequence;if(mode==PdfCanvasApplyMode.Replace){_content=program;StructureRoles=System.Array.Empty<string>();}
    }

    private readonly Dictionary<string,PdfFontResource> _fonts=new(StringComparer.Ordinal);
    private Dictionary<(PdfGraphicsResourceCategory Category,string Name),PdfGraphicsResource> _graphics=new();
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
        set{EnsureEditable();if(value!=null&&value.Kind!=PdfContentKind.Page)throw new ArgumentException("Page content cannot be a Type 3 glyph program.",nameof(value));_content=value;StructureRoles=System.Array.Empty<string>();_canvasLayers=System.Array.Empty<PdfCanvasPageLayer>();}
    }
    /// <summary>Reads original or replacement operators under the document's operation policy without executing document actions.</summary>
    public PdfContentProgram ReadContent(PdfUnknownOperatorPolicy unknownOperators=PdfUnknownOperatorPolicy.Reject,string? unknownOperatorVersion=null,System.Threading.CancellationToken cancellationToken=default)
    {EnsureAttached();return _owner.ReadPageContent(this,unknownOperators,unknownOperatorVersion,cancellationToken);}
    /// <summary>Inspects detached original/replacement font bindings under the document policy without executing document actions.</summary>
    public IReadOnlyList<PdfPageFont> ReadFonts(System.Threading.CancellationToken cancellationToken=default)
    {EnsureAttached();return _owner.ReadPageFonts(this,cancellationToken);}
    /// <summary>Gets the immutable original binding, or null for a newly authored page.</summary>
    public PdfPageSource? Source{get{EnsureAttached();return _source;}}
    internal void Bind(PdfPageSource source){_source=source;_rotation=source.Rotation;_geometry=source.Geometry;}

    /// <summary>Gets or changes the unrotated physical page dimensions.</summary>
    public PdfPageSize Size
    {
        get { EnsureAttached(); return _size; }
        set { EnsureEditable(); Guard.Size(value, nameof(value));if(value==_size)return;Geometry=_geometry.Resize(value); }
    }

    /// <summary>Gets or changes the clockwise display rotation without changing the physical size.</summary>
    public PdfPageRotation Rotation
    {
        get { EnsureAttached(); return _rotation; }
        set
        {
            EnsureEditable();
            if (!Enum.IsDefined(typeof(PdfPageRotation), value)) throw new ArgumentOutOfRangeException(nameof(value));
            if(_rotation==value)return;Geometry=_geometry.WithRotation(value);
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
