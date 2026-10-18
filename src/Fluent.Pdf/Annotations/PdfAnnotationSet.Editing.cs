using System.Threading;

namespace Fluent.Pdf;
public sealed partial class PdfAnnotationSet
{
    /// <summary>Updates explicit properties/geometry/normal appearance, retaining all unspecified source keys, replies, popup ownership and indirect identity.</summary>
    public PdfDocument Update(int annotationIndex, PdfValue properties, PdfRect? bounds = null, PdfGraphicsResource? normalAppearance = null, string[]? removeProperties = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        var record = At(annotationIndex);
        if (properties == null || properties.Kind != PdfValueKind.Dictionary)
            throw new ArgumentException("A direct property dictionary is required.", nameof(properties));
        return PdfGraphEdit.Run("UpdateAnnotation", Source, options, policy, cancellationToken, graph =>
        {
            var context = graph.Context;
            if (record.Subtype == "Widget")
                throw graph.Error("Annotation.WidgetEditing", "Widget editing requires the form-field ownership profile.");
            PdfAnnotation.ValidateProperties(properties, context);
            PdfGraphImport.ValidateDetached(properties, context);
            var writer = new PdfResourceWriter(graph.Edit, context, new());
            var entries = graph.Copy(record.Definition);
            if (removeProperties != null)
            {
                if (removeProperties.Length > 4096)
                    throw graph.Error("Annotation.PropertyLimit", "Property removal requires a finite bounded key array.");
                foreach (string key in removeProperties)
                {
                    context.Checkpoint();
                    if (key == null)
                        throw new ArgumentException("Property keys cannot be null.", nameof(removeProperties));
                    PdfAnnotation.ValidateProperties(PdfGraphicsNumbers.Dict((key, PdfValue.Null)), context);
                    entries.Remove(key);
                }
            }

            foreach (var pair in properties.Entries)
                entries[pair.Key] = writer.EmbedValue(pair.Value);
            var rectangle = bounds ?? record.Bounds;
            if (bounds.HasValue)
                entries["Rect"] = rectangle.Value;
            if (normalAppearance != null)
            {
                PdfAnnotation.ValidateAppearance(normalAppearance, context);
                var ap = graph.Get(record.Definition, "AP");
                var appearances = graph.Copy(ap);
                appearances["N"] = writer.Embed(normalAppearance);
                entries["AP"] = PdfValue.OwnedDictionary(appearances);
            }

            var dictionary = PdfValue.OwnedDictionary(entries);
            PdfAnnotation.ValidateGeometry(record.Subtype, dictionary, rectangle, graph.Resolve, context);
            SetRecord(graph, record, dictionary);
            ValidateRelationships(Read(graph), graph);
            RequireVersion(graph, PdfVersionNumber.Text(Math.Max(17, normalAppearance == null ? 12 : PdfVersionNumber.Parse(normalAppearance.MinimumVersion))));
        });
    }

    /// <summary>Sets or removes one reply relationship by captured annotation identity, retaining state and popup metadata.</summary>
    public PdfDocument SetReply(int annotationIndex, int? targetIndex, bool grouped = false, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        var record = At(annotationIndex);
        if (record.Subtype == "Popup" || record.Subtype == "Link" || record.Subtype == "Widget" || record.Subtype == "RichMedia")
            throw new ArgumentException("Reply relationships require a markup annotation.", nameof(annotationIndex));
        var target = targetIndex.HasValue ? At(targetIndex.Value) : null;
        return PdfGraphEdit.Run("SetAnnotationReply", Source, options, policy, cancellationToken, graph =>
        {
            var entries = graph.Copy(record.Definition);
            entries.Remove("IRT");
            entries.Remove("RT");
            if (target != null)
            {
                if (!target.ObjectId.HasValue)
                    throw graph.Error("Annotation.ReplyIdentity", "A reply target requires an indirect annotation identity.");
                entries["IRT"] = PdfValue.Reference(target.ObjectId.Value);
                entries["RT"] = PdfValue.Name(grouped ? "Group" : "R");
            }

            SetRecord(graph, record, PdfValue.OwnedDictionary(entries));
            ValidateRelationships(Read(graph), graph);
            RequireVersion(graph, "1.5");
        });
    }

    /// <summary>Adds one reciprocal same-page popup for an existing annotation. Popup windows have no appearance stream.</summary>
    public PdfDocument AddPopup(int annotationIndex, PdfRect bounds, bool open = false, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        var record = At(annotationIndex);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentException("A popup requires positive bounds.", nameof(bounds));
        return PdfGraphEdit.Run("AddAnnotationPopup", Source, options, policy, cancellationToken, graph =>
        {
            if (!record.ObjectId.HasValue || record.Subtype == "Popup" || record.Subtype == "Link" || record.Subtype == "Widget" || record.Subtype == "RichMedia" || record.Definition.Entries.ContainsKey("Popup"))
                throw graph.Error("Annotation.PopupParent", "A popup requires an indirect non-popup parent without another popup.");
            var binding = graph.Snapshot.Pages[record.PageIndex].Source!;
            var popup = graph.Edit.Add(PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Annot")), ("Subtype", PdfValue.Name("Popup")), ("Rect", bounds.Value), ("P", PdfValue.Reference(binding.ObjectId)), ("Parent", PdfValue.Reference(record.ObjectId.Value)), ("Open", PdfValue.Boolean(open))));
            var entries = graph.Copy(record.Definition);
            entries["Popup"] = PdfValue.Reference(popup);
            graph.Edit.Set(record.ObjectId.Value, PdfValue.OwnedDictionary(entries));
            var annotations = graph.Get(graph.Edit.Resolve(binding.ObjectId), "Annots");
            SetPageAnnotations(graph, record.PageIndex, annotations.Items.Concat(new[] { PdfValue.Reference(popup) }));
            ValidateRelationships(Read(graph), graph);
            RequireVersion(graph, "1.3");
        });
    }

    /// <summary>Deletes the selected entries and, by default, their reply/popup descendants. Unreferenced source objects may remain; this is not secure erasure.</summary>
    public PdfDocument Delete(IEnumerable<int> annotationIndices, bool cascade = true, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfGraphEdit.Run("DeleteAnnotations", Source, options, policy, cancellationToken, graph =>
    {
        graph.RequireUnstructuredContent();
        var chosen = Selection(annotationIndices, graph.Context);
        IncludeRelated(chosen, graph, cascade);
        RemoveSelected(chosen, graph);
    });
    internal void RemoveSelected(HashSet<int> chosen, PdfGraphEdit graph)
    {
        graph.Context.Consume(PdfResourceKind.ExpandedBytes, Annotations.Count * 128L + 128);
        var removed = new HashSet<PdfValue>();
        var pages = new Dictionary<int, HashSet<int>>();
        foreach (var record in Annotations)
        {
            graph.Context.Checkpoint();
            graph.Context.Consume(PdfResourceKind.Objects, 1);
            if (!chosen.Contains(record.Index))
                continue;
            if (record.Subtype == "Widget")
                throw graph.Error("Annotation.WidgetDeletion", "Widget removal requires the form-field ownership profile.");
            removed.Add(record.Definition);
            if (!pages.TryGetValue(record.PageIndex, out var ordinals))
                pages.Add(record.PageIndex, ordinals = new());
            ordinals.Add(record.PageOrdinal);
        }

        foreach (var record in Annotations)
        {
            graph.Context.Checkpoint();
            if (chosen.Contains(record.Index) || !record.Definition.Entries.TryGetValue("Popup", out var popup) || !removed.Contains(graph.Resolve(popup)))
                continue;
            var entries = graph.Copy(record.Definition);
            entries.Remove("Popup");
            SetRecord(graph, record, PdfValue.OwnedDictionary(entries));
        }

        foreach (var pair in pages)
        {
            graph.Context.Checkpoint();
            var binding = graph.Snapshot.Pages[pair.Key].Source!;
            var original = graph.Get(graph.Edit.Resolve(binding.ObjectId), "Annots");
            SetPageAnnotations(graph, pair.Key, original.Items.Where((_, i) => !pair.Value.Contains(i)));
        }

        ValidateRelationships(Read(graph), graph);
    }

    internal static void SetRecord(PdfGraphEdit graph, PdfAnnotationRecord record, PdfValue value)
    {
        if (record.ObjectId.HasValue)
            graph.Edit.Set(record.ObjectId.Value, value);
        else
        {
            var binding = graph.Snapshot.Pages[record.PageIndex].Source!;
            var original = graph.Get(graph.Edit.Resolve(binding.ObjectId), "Annots");
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, original.Items.Count * 16L + 32);
            var items = original.Items.ToArray();
            items[record.PageOrdinal] = value;
            SetPageAnnotations(graph, record.PageIndex, items);
        }
    }

    internal PdfAnnotationRecord At(int index) => index >= 0 && index < Annotations.Count ? Annotations[index] : throw new ArgumentOutOfRangeException(nameof(index));
    internal static void RequireVersion(PdfGraphEdit graph, string version)
    {
        var catalog = graph.Copy(graph.Catalog);
        var declared = graph.Get(graph.Catalog, "Version");
        int current = declared.Kind == PdfValueKind.Name ? PdfVersionNumber.Parse(declared.NameValue) : PdfVersionNumber.Parse(graph.Graph.Version);
        catalog["Version"] = PdfValue.Name(PdfVersionNumber.Text(Math.Max(current, PdfVersionNumber.Parse(version))));
        graph.SetCatalog(catalog);
    }

    /// <summary>Imports a captured annotation selection to one target page, preserving referenced resources, reply/popups and encoded payloads. Other source pages require explicit destination mapping.</summary>
    public PdfDocument ImportTo(PdfDocumentSnapshot target, int targetPageIndex, IEnumerable<int> annotationIndices, bool includeRelated = true, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (targetPageIndex < 0 || targetPageIndex >= target.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(targetPageIndex));
        return PdfGraphEdit.Run("ImportAnnotations", target, options, policy, cancellationToken, graph =>
        {
            var chosen = Selection(annotationIndices, graph.Context);
            using var sourceGraph = new PdfGraphEdit(Source, new(), graph.Context);
            IncludeRelated(chosen, sourceGraph, includeRelated);
            var importer = new PdfGraphImport(graph, Source);
            var page = graph.Snapshot.Pages[targetPageIndex].Source!;
            var names = new HashSet<string>(Read(graph).Where(a => a.Identifier != null).Select(a => a.Identifier!), StringComparer.Ordinal);
            foreach (var record in Annotations)
            {
                graph.Context.Checkpoint();
                if (!chosen.Contains(record.Index))
                    continue;
                if (record.Subtype == "Widget")
                    throw graph.Error("Annotation.WidgetImport", "Widget import requires the form-field ownership profile.");
                if (record.Identifier != null && !names.Add(record.Identifier))
                    throw graph.Error("Annotation.IdentifierCollision", "An imported annotation identifier already exists on the target.");
                if (!record.ObjectId.HasValue)
                    throw graph.Error("Annotation.ImportIdentity", "Annotation import requires indirect source identities.");
                graph.Context.Consume(PdfResourceKind.ExpandedBytes, 128);
                importer.Map.Add(record.ObjectId.Value, graph.Edit.Add(PdfValue.Null));
                var sourcePage = Source.Pages[record.PageIndex].Source!.ObjectId;
                if (!importer.Map.ContainsKey(sourcePage))
                    importer.Map.Add(sourcePage, page.ObjectId);
            }

            // Do not import an unselected reply ancestor or popup parent implicitly.
            foreach (var record in Annotations)
                if (chosen.Contains(record.Index))
                    foreach (string key in new[]
                    {
                        "IRT",
                        "Parent",
                        "Popup"
                    }

                    )
                        if (record.Definition.Entries.TryGetValue(key, out var relation) && relation.Kind == PdfValueKind.Reference && !importer.Map.ContainsKey(relation.ReferenceId))
                            throw graph.Error("Annotation.ImportRelationship", "The annotation import selection omits a required related entry.");
            var additions = new List<PdfValue>();
            foreach (var record in Annotations)
            {
                if (!chosen.Contains(record.Index))
                    continue;
                var copied = importer.Clone(record.Definition);
                var entries = graph.Copy(copied);
                entries["P"] = PdfValue.Reference(page.ObjectId);
                var id = importer.Map[record.ObjectId!.Value];
                graph.Edit.Set(id, PdfValue.OwnedDictionary(entries));
                graph.Context.Consume(PdfResourceKind.ExpandedBytes, 16);
                additions.Add(PdfValue.Reference(id));
            }

            var old = graph.Get(page.Dictionary, "Annots");
            SetPageAnnotations(graph, targetPageIndex, (old.Kind == PdfValueKind.Array ? old.Items : System.Array.Empty<PdfValue>()).Concat(additions));
            ValidateRelationships(Read(graph), graph);
            RequireVersion(graph, Source.SourceDocument!.Version);
        });
    }
}
