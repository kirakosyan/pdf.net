using System.IO;

namespace Fluent.Pdf;
/// <summary>The original managed PDF object/page reader and full/incremental writer. No document actions are executed.</summary>
public sealed class ManagedPdfProvider : IPdfDocumentReader, IPdfRevisionWriter
{
    /// <summary>Creates a stateless provider that can be shared by independent document sessions.</summary>
    public ManagedPdfProvider()
    {
    }

    /// <summary>Gets the shared stateless provider.</summary>
    public static ManagedPdfProvider Instance { get; } = new();
    /// <summary>Gets the core read/write capability declaration.</summary>
    public PdfProviderDescriptor Descriptor { get; } = new("managed.pdf", "1.0.0", PdfCapability.ReadDocument, PdfCapability.WriteDocument);
    /// <summary>Gets false: the reader snapshots borrowed input and the writer emits sequential output.</summary>
    public bool RequiresSeekableStream => false;

    /// <summary>Reports support for the SDK's full/incremental and table/stream choices; source-specific restrictions are checked before output.</summary>
    public bool Supports(PdfWriteOptions options) => options != null;
    /// <summary>Eagerly reads an immutable graph and a validated page tree under the supplied context.</summary>
    public void Read(Stream input, PdfDocument target, PdfOperationContext context)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
        if (target.Pages.Count != 0)
            throw PdfWriterCore.Error(context, "Reader.TargetNotEmpty", "The reader requires an empty target session.");
        var source = PdfSyntax.ReadCore(input, context);
        var trailer = source.Revisions[0].Trailer;
        if (source.Revisions.Any(revision => revision.Trailer.Entries.TryGetValue("Encrypt", out var encryption) && encryption.Kind != PdfValueKind.Null))
            throw PdfWriterCore.Error(context, "Reader.EncryptionProviderRequired", "Opening encrypted pages requires an explicitly configured encryption implementation.");
        var rootReference = Entry(trailer, "Root");
        if (rootReference.Kind != PdfValueKind.Reference)
            throw PdfWriterCore.Error(context, "Reader.InvalidCatalog", "The trailer must reference an indirect Catalog.");
        var root = Resolve(source, rootReference, context);
        if (root.Kind != PdfValueKind.Dictionary || Name(source, Entry(root, "Type"), context) != "Catalog")
            throw PdfWriterCore.Error(context, "Reader.InvalidCatalog", "The trailer must resolve to a Catalog dictionary.");
        var pages = Entry(root, "Pages");
        if (pages.Kind != PdfValueKind.Reference)
            throw PdfWriterCore.Error(context, "Reader.InvalidPageTree", "The catalog must reference a page tree.");
        var pageTree = Resolve(source, pages, context);
        if (pageTree.Kind != PdfValueKind.Dictionary || Name(source, Entry(pageTree, "Type"), context) != "Pages")
            throw PdfWriterCore.Error(context, "Reader.InvalidPageTree", "The catalog Pages reference must resolve to a Pages root.");
        var visited = new HashSet<PdfObjectId>();
        Visit(source, pages.ReferenceId, null, new Dictionary<string, PdfValue>(StringComparer.Ordinal), visited, target, context);
        target.AttachSource(source, context);
    }

    /// <summary>Preserves the original graph and page identities while applying page order/geometry changes and the selected revision strategy.</summary>
    public void Write(PdfDocumentSnapshot document, Stream output, PdfOperationContext context)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
        if (document.Pages.Count == 0)
            throw PdfWriterCore.Error(context, "Writer.EmptyDocument", "A document must contain at least one page before saving.");
        context.Consume(PdfResourceKind.Objects, document.Pages.Count * 16L + 8);
        context.Consume(PdfResourceKind.ExpandedBytes, document.Pages.Count * 8192L + 1024);
        using var edit = new PdfTransaction(document.SourceDocument, document.Options.OperationPolicy);
        PdfObjectId catalogId, pagesId;
        if (document.SourceDocument == null)
        {
            catalogId = edit.Add(PdfValue.Null);
            pagesId = edit.Add(PdfValue.Null);
            edit.Trailer = Dictionary(("Root", PdfValue.Reference(catalogId)));
        }
        else
        {
            catalogId = TerminalIdentity(document.SourceDocument, Entry(edit.Trailer, "Root"), context);
            pagesId = TerminalIdentity(document.SourceDocument, Entry(edit.Resolve(catalogId), "Pages"), context);
        }

        bool orderChanged = document.SourceDocument == null || document.Pages.Count != document.OriginalOrder.Count;
        if (!orderChanged)
            for (int i = 0; i < document.Pages.Count; i++)
                if (document.Pages[i].Source == null || !ReferenceEquals(document.Pages[i].Source!.Document, document.SourceDocument) || document.Pages[i].Source!.ObjectId != document.OriginalOrder[i])
                {
                    orderChanged = true;
                    break;
                }

        if(document.SourceDocument!=null&&edit.Resolve(catalogId).Entries.ContainsKey("StructTreeRoot")&&(orderChanged||document.Pages.Any(p=>p.Content!=null||p.CanvasLayers.Count>0)))
            throw PdfWriterCore.Error(context,"Structure.SourceEditPolicy","Editing source structure associations requires an explicit structure-preservation profile.");
        var pageIds = new List<PdfValue>(document.Pages.Count);
        var writeOptions=document.WriteOptions;
        var embeddedFonts=new Dictionary<PdfFontResource,PdfObjectId>();
        var resourceWriter=new PdfResourceWriter(edit,context,embeddedFonts);
        foreach (var page in document.Pages)
        {
            context.Checkpoint();
            var binding = page.Source;
            PdfObjectId id;
            if (binding != null && !ReferenceEquals(binding.Document, document.SourceDocument))
                throw PdfWriterCore.Error(context, "Writer.PageImportRequired", "A page from another graph requires the explicit page import operation.");
            bool geometryChanged=binding==null||!page.Geometry.Equals(binding.Geometry);
            bool changed = binding == null || orderChanged || geometryChanged || page.Content!=null || page.FontResources.Count>0 || page.GraphicsResources.Bindings.Count>0 || page.CanvasLayers.Count>0 || page.StructureRoles.Count>0;
            if (binding == null)
                id = edit.Add(PdfValue.Null);
            else
                id = binding.ObjectId;
            pageIds.Add(PdfValue.Reference(id));
            if (!changed)
                continue;
            var entries = binding == null ? new Dictionary<string, PdfValue>(StringComparer.Ordinal) : PdfWriterCore.CopyDictionary(binding.Dictionary.Entries, context);
            if (binding != null && orderChanged)
                foreach (var pair in binding.EffectiveAttributes.Entries)
                    entries[pair.Key] = pair.Value;
            entries["Type"] = PdfValue.Name("Page");
            if (orderChanged || binding == null)
                entries["Parent"] = PdfValue.Reference(pagesId);
            if(geometryChanged)
            {
                var geometry=page.Geometry;entries["MediaBox"]=geometry.Boxes.Media.Value;
                if(binding!=null||!PdfPageBoxes.Same(geometry.Boxes.Crop,geometry.Boxes.Media))entries["CropBox"]=geometry.Boxes.Crop.Value;
                else entries.Remove("CropBox");
                foreach(var pair in new[]{("BleedBox",geometry.Boxes.Bleed),("TrimBox",geometry.Boxes.Trim),("ArtBox",geometry.Boxes.Art)})
                {if(!PdfPageBoxes.Same(pair.Item2,geometry.Boxes.Crop)||binding?.Dictionary.Entries.ContainsKey(pair.Item1)==true)entries[pair.Item1]=pair.Item2.Value;else entries.Remove(pair.Item1);}
                if(geometry.UserUnit!=1){entries["UserUnit"]=PdfValues.Real(geometry.UserUnit);writeOptions=writeOptions.RequireVersion("1.6");}else entries.Remove("UserUnit");
            }

            if (binding == null)
                entries["Resources"] = Dictionary();
            if (binding == null || page.Rotation != binding.Rotation)
                entries["Rotate"] = PdfValue.Integer((int)page.Rotation);
            if(page.Content!=null)
            {
                entries["Contents"]=PdfValue.Reference(edit.Add(page.Content.ToStream(context)));
                writeOptions=writeOptions.RequireVersion(page.Content.MinimumVersion);
            }
            if(page.CanvasLayers.Count>0)
            {
                var original=entries.TryGetValue("Contents",out var baseContents)?baseContents:PdfValue.Null;
                entries["Contents"]=PdfCanvasLayerContent.Compose(edit,original,page.CanvasLayers,context);
                foreach(var layer in page.CanvasLayers)writeOptions=writeOptions.RequireVersion(layer.Content.MinimumVersion);
            }
            if(page.FontResources.Count>0)
            {
                context.Consume(PdfResourceKind.Objects,page.FontResources.Count);context.Consume(PdfResourceKind.ExpandedBytes,page.FontResources.Count*128L+256);
                var existing=binding!=null&&binding.EffectiveAttributes.Entries.TryGetValue("Resources",out var inheritedResources)?Resolve(binding.Document,inheritedResources,context):entries.TryGetValue("Resources",out var directResources)?directResources:PdfValue.Null;
                if(existing.Kind!=PdfValueKind.Null&&existing.Kind!=PdfValueKind.Dictionary)throw PdfWriterCore.Error(context,"Font.ResourceDictionary","Page Resources must resolve to a dictionary.");
                var resources=existing.Kind==PdfValueKind.Dictionary?PdfWriterCore.CopyDictionary(existing.Entries,context):new Dictionary<string,PdfValue>(StringComparer.Ordinal);
                var oldFonts=resources.TryGetValue("Font",out var fontDictionary)?fontDictionary:PdfValue.Null;if(binding!=null)oldFonts=Resolve(binding.Document,oldFonts,context);
                if(oldFonts.Kind!=PdfValueKind.Null&&oldFonts.Kind!=PdfValueKind.Dictionary)throw PdfWriterCore.Error(context,"Font.ResourceDictionary","Font resources must resolve to a dictionary.");
                var fonts=oldFonts.Kind==PdfValueKind.Dictionary?PdfWriterCore.CopyDictionary(oldFonts.Entries,context):new Dictionary<string,PdfValue>(StringComparer.Ordinal);
                foreach(var pair in page.FontResources.OrderBy(x=>x.Key,StringComparer.Ordinal))
                {
                    context.Checkpoint();if(!embeddedFonts.TryGetValue(pair.Value,out var fontId)){fontId=pair.Value.AddTo(edit,context);embeddedFonts.Add(pair.Value,fontId);}fonts[pair.Key]=PdfValue.Reference(fontId);writeOptions=writeOptions.RequireVersion(pair.Value.MinimumVersion);
                }
                resources["Font"]=PdfValue.OwnedDictionary(fonts);entries["Resources"]=PdfValue.OwnedDictionary(resources);
            }
            if(page.GraphicsResources.Bindings.Count>0)
            {
                var declared=entries.TryGetValue("Resources",out var direct)?direct:binding!=null&&binding.EffectiveAttributes.Entries.TryGetValue("Resources",out var inherited)?inherited:PdfValue.Null;
                var existing=binding==null?declared:Resolve(binding.Document,declared,context);
                if(existing.Kind!=PdfValueKind.Null&&existing.Kind!=PdfValueKind.Dictionary)throw PdfWriterCore.Error(context,"Graphics.ResourceDictionary","Page Resources must resolve to a dictionary.");
                var resources=existing.Kind==PdfValueKind.Dictionary?PdfWriterCore.CopyDictionary(existing.Entries,context):new Dictionary<string,PdfValue>(StringComparer.Ordinal);
                foreach(var group in page.GraphicsResources.Bindings.GroupBy(x=>x.Resource.Category))
                {
                    context.Checkpoint();string category=group.Key.ToString();var old=resources.TryGetValue(category,out var names)?names:PdfValue.Null;if(binding!=null)old=Resolve(binding.Document,old,context);
                    if(old.Kind!=PdfValueKind.Null&&old.Kind!=PdfValueKind.Dictionary)throw PdfWriterCore.Error(context,"Graphics.ResourceDictionary","A named resource category must resolve to a dictionary.");
                    var namesDictionary=old.Kind==PdfValueKind.Dictionary?PdfWriterCore.CopyDictionary(old.Entries,context):new Dictionary<string,PdfValue>(StringComparer.Ordinal);
                    foreach(var pair in group){context.Consume(PdfResourceKind.ExpandedBytes,128);namesDictionary[pair.Name]=resourceWriter.Embed(pair.Resource);writeOptions=writeOptions.RequireVersion(pair.Resource.MinimumVersion);}
                    resources[category]=PdfValue.OwnedDictionary(namesDictionary);
                }
                entries["Resources"]=PdfValue.OwnedDictionary(resources);
            }
            if(page.StructureRoles.Count>0){entries["StructParents"]=PdfValue.Integer(pageIds.Count-1);writeOptions=writeOptions.RequireVersion("1.3");}
            edit.Set(id, PdfValue.OwnedDictionary(entries));
        }

        if (orderChanged)
        {
            var entries = document.SourceDocument == null ? new Dictionary<string, PdfValue>(StringComparer.Ordinal) : PdfWriterCore.CopyDictionary(edit.Resolve(pagesId).Entries, context);
            // Materialized leaf attributes preserve inheritance when the page tree is flattened.
            foreach (string key in new[]
            {
                "Parent",
                "MediaBox",
                "CropBox",
                "Resources",
                "Rotate"
            }

            )
                entries.Remove(key);
            entries["Type"] = PdfValue.Name("Pages");
            entries["Kids"] = PdfValue.OwnedArray(pageIds);
            entries["Count"] = PdfValue.Integer(pageIds.Count);
            edit.Set(pagesId, PdfValue.OwnedDictionary(entries));
        }

        if (document.SourceDocument == null)
            edit.Set(catalogId, Dictionary(("Type", PdfValue.Name("Catalog")), ("Pages", PdfValue.Reference(pagesId))));
        PdfBasicStructure.Write(edit,catalogId,pageIds,document.Pages,context);
        PdfDeferredNavigation.Write(edit,catalogId,pageIds,document,context);
        if(document.PageNumbering!=null)writeOptions=writeOptions.RequireVersion("1.3");
        PdfNavigationReader.ValidateTargets(edit, PdfValue.Reference(catalogId), pageIds, context);
        edit.WriteCore(output, writeOptions, context);
    }

    private static void Visit(PdfSyntaxDocument source, PdfObjectId id, PdfObjectId? parent, Dictionary<string, PdfValue> inherited, HashSet<PdfObjectId> visited, PdfDocument target, PdfOperationContext context)
    {
        using var scope = context.EnterScope(PdfResourceKind.Depth);
        context.Consume(PdfResourceKind.Objects, 1);
        var node = Resolve(source, PdfValue.Reference(id), context, out id);
        if (!visited.Add(id))
            throw PdfWriterCore.Error(context, "Reader.PageTreeCycle", "The page tree contains a cycle or duplicate node.");
        if (node.Kind != PdfValueKind.Dictionary)
            throw PdfWriterCore.Error(context, "Reader.InvalidPageTree", "A page tree node is not a dictionary.");
        if (parent != null)
        {
            var declared = Entry(node, "Parent");
            if (declared.Kind != PdfValueKind.Reference || declared.ReferenceId != parent.Value)
                throw PdfWriterCore.Error(context, "Reader.PageParentMismatch", "A page tree node has an inconsistent parent.");
        }
        else if (Entry(node, "Parent").Kind != PdfValueKind.Null)
            throw PdfWriterCore.Error(context, "Reader.PageParentMismatch", "The page tree root must not have a parent.");
        context.Consume(PdfResourceKind.ExpandedBytes, 512);
        var attributes = new Dictionary<string, PdfValue>(inherited, StringComparer.Ordinal);
        foreach (string key in new[]
        {
            "MediaBox",
            "CropBox",
            "Resources",
            "Rotate"
        }

        )
            if (node.Entries.TryGetValue(key, out var value) && Resolve(source, value, context).Kind != PdfValueKind.Null)
                attributes[key] = value;
        string type = Name(source, Entry(node, "Type"), context);
        if (type == "Pages")
        {
            var kids = Resolve(source, Entry(node, "Kids"), context);
            var count = Resolve(source, Entry(node, "Count"), context);
            if (kids.Kind != PdfValueKind.Array || count.Kind != PdfValueKind.Number)
                throw PdfWriterCore.Error(context, "Reader.InvalidPageTree", "A Pages node requires Kids and an integer Count.");
            long expected = 0;
            try
            {
                expected = count.IntegerValue;
            }
            catch (InvalidDataException)
            {
                throw PdfWriterCore.Error(context, "Reader.InvalidPageTree", "A page tree Count is not an integer.");
            }

            int before = target.Pages.Count;
            foreach (var kid in kids.Items)
            {
                if (kid.Kind != PdfValueKind.Reference)
                    throw PdfWriterCore.Error(context, "Reader.InvalidPageTree", "Page tree children must be indirect references.");
                Visit(source, kid.ReferenceId, id, attributes, visited, target, context);
            }

            if (expected != target.Pages.Count - before)
                throw PdfWriterCore.Error(context, "Reader.PageCountMismatch", "A page tree Count disagrees with its actual leaves.");
            return;
        }

        if (type != "Page")
            throw PdfWriterCore.Error(context, "Reader.InvalidPageTree", "A page tree node has an unsupported Type.");
        if (!attributes.TryGetValue("MediaBox", out var box))
            throw PdfWriterCore.Error(context, "Reader.PageBoxRequired", "Every page needs an effective MediaBox.");
        box = Resolve(source, box, context);
        if (box.Kind != PdfValueKind.Array || box.Items.Count != 4)
            throw PdfWriterCore.Error(context, "Reader.InvalidPageBox", "A page box must have four finite numbers.");
        var coordinates = new double[4];
        for (int i = 0; i < 4; i++)
            coordinates[i] = Number(source, box.Items[i], context);
        double left = Math.Min(coordinates[0], coordinates[2]), bottom = Math.Min(coordinates[1], coordinates[3]), unit = 1;
        var userUnit = Resolve(source, Entry(node, "UserUnit"), context);
        if (userUnit.Kind != PdfValueKind.Null)
            unit = Number(source, userUnit, context);
        if (unit <= 0 || unit > 75000)
            throw PdfWriterCore.Error(context, "Reader.InvalidUserUnit", "A page UserUnit must be positive and at most 75000.");
        double width = Math.Abs(coordinates[2] - coordinates[0]) * unit, height = Math.Abs(coordinates[3] - coordinates[1]) * unit;
        if (width <= 0 || height <= 0 || double.IsInfinity(width) || double.IsInfinity(height))
            throw PdfWriterCore.Error(context, "Reader.InvalidPageBox", "The page has invalid physical dimensions.");
        long rotation = 0;
        if (attributes.TryGetValue("Rotate", out var rotate))
            try
            {
                rotation = Resolve(source, rotate, context).IntegerValue;
            }
            catch (InvalidDataException)
            {
                throw PdfWriterCore.Error(context, "Reader.InvalidRotation", "Page rotation must be an integer multiple of 90.");
            }

        if (rotation % 90 != 0)
            throw PdfWriterCore.Error(context, "Reader.InvalidRotation", "Page rotation must be a multiple of 90.");
        var normalized = (PdfPageRotation)((rotation % 360 + 360) % 360);
        var size = new PdfPageSize(width, height);
        context.Consume(PdfResourceKind.Objects, 1);
        context.Consume(PdfResourceKind.ExpandedBytes, 1024);
        var effective=PdfValue.Dictionary(attributes);
        var geometry=PdfPageGeometry.Read(source,node,effective,new PdfRect(left,bottom,Math.Abs(coordinates[2]-coordinates[0]),Math.Abs(coordinates[3]-coordinates[1])),normalized,unit,context);
        var binding = new PdfPageSource(source, id, node, effective, size, normalized, left, bottom, unit,geometry);
        target.Pages.Add(size, context).Bind(binding);
    }

    private static PdfObjectId TerminalIdentity(PdfSyntaxDocument source, PdfValue value, PdfOperationContext context)
    {
        value = Resolve(source, value, context, out var id);
        if (id.Number == 0 || value.Kind != PdfValueKind.Dictionary)
            throw PdfWriterCore.Error(context, "Reader.InvalidCatalog", "A required structural reference does not resolve to a dictionary.");
        return id;
    }

    internal static PdfValue Resolve(PdfSyntaxDocument source, PdfValue value, PdfOperationContext context) => Resolve(source, value, context, out _);
    private static PdfValue Resolve(PdfSyntaxDocument source, PdfValue value, PdfOperationContext context, out PdfObjectId terminal)
    {
        terminal = default;
        context.Checkpoint();
        if (value.Kind != PdfValueKind.Reference)
            return value;
        context.Consume(PdfResourceKind.ExpandedBytes, 64);
        var seen = new HashSet<PdfObjectId>();
        for (int depth = 0; value.Kind == PdfValueKind.Reference; depth++)
        {
            context.Checkpoint();
            if (depth >= context.Available(PdfResourceKind.Depth))
                context.RejectLimit(PdfResourceKind.Depth);
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 64);
            terminal = value.ReferenceId;
            if (!seen.Add(value.ReferenceId))
                throw PdfWriterCore.Error(context, "Reader.ReferenceCycle", "An indirect reference chain contains a cycle.");
            value = source.Resolve(value.ReferenceId);
        }

        return value;
    }

    private static string Name(PdfSyntaxDocument source, PdfValue value, PdfOperationContext context)
    {
        value = Resolve(source, value, context);
        return value.Kind == PdfValueKind.Name ? value.NameValue : "";
    }

    private static double Number(PdfSyntaxDocument source, PdfValue value, PdfOperationContext context)
    {
        value = Resolve(source, value, context);
        try
        {
            return value.NumberValue;
        }
        catch (InvalidOperationException)
        {
            throw PdfWriterCore.Error(context, "Reader.InvalidNumber", "A structural numeric value is missing or outside finite range.");
        }
    }

    private static PdfValue Entry(PdfValue dictionary, string name) => dictionary.Entries.TryGetValue(name, out var value) ? value : PdfValue.Null;
    private static PdfValue Dictionary(params (string Key, PdfValue Value)[] entries) => PdfValue.Dictionary(entries.Select(x => new KeyValuePair<string, PdfValue>(x.Key, x.Value)));
}
