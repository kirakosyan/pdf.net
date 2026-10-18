using System.Threading;

namespace Fluent.Pdf;
/// <summary>Managed annotation authoring, preserving passive content and explicit Form appearances.</summary>
public static class PdfAnnotations
{
    /// <summary>Adds an explicit finite batch to one page; identifiers and forward reply targets resolve atomically within the captured document.</summary>
    public static PdfDocument Add(PdfDocumentSnapshot source, int pageIndex, IEnumerable<PdfAnnotation> annotations, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (pageIndex < 0 || pageIndex >= source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (annotations == null)
            throw new ArgumentNullException(nameof(annotations));
        string version = "1.2";
        return PdfGraphEdit.Run("AddAnnotations", source, options, policy, cancellationToken, graph =>
        {
            var context = graph.Context;
            var existing = PdfAnnotationSet.Read(graph);
            PdfAnnotationSet.ValidateRelationships(existing, graph);
            var names = new Dictionary<string, PdfObjectId>(StringComparer.Ordinal);
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in existing)
            {
                if (record.Identifier == null)
                    continue;
                if (!occupied.Add(record.Identifier))
                {
                    names.Remove(record.Identifier);
                    continue;
                }

                if (record.ObjectId.HasValue)
                    names.Add(record.Identifier, record.ObjectId.Value);
            }

            var batch = new List<(PdfAnnotation Annotation, PdfObjectId Id)>();
            using var enumerator = annotations.GetEnumerator();
            while (true)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                if (!enumerator.MoveNext())
                    break;
                if (batch.Count == 10000)
                    throw graph.Error("Annotation.BatchLimit", "Annotation batches are limited to ten thousand entries.");
                var annotation = enumerator.Current ?? throw new ArgumentException("Annotation batches cannot contain null.", nameof(annotations));
                context.Consume(PdfResourceKind.ExpandedBytes, 1024);
                if (!occupied.Add(annotation.Identifier))
                    throw graph.Error("Annotation.IdentifierCollision", "Annotation identifiers must be unique within the document.");
                var id = graph.Edit.Add(PdfValue.Null);
                names.Add(annotation.Identifier, id);
                batch.Add((annotation, id));
                version = PdfVersionNumber.Text(Math.Max(PdfVersionNumber.Parse(version), PdfVersionNumber.Parse(annotation.MinimumVersion)));
            }

            var writer = new PdfResourceWriter(graph.Edit, context, new());
            var binding = graph.Snapshot.Pages[pageIndex].Source!;
            var original = graph.Get(binding.Dictionary, "Annots");
            context.Consume(PdfResourceKind.ExpandedBytes, (original.Kind == PdfValueKind.Array ? original.Items.Count : 0) * 16L + batch.Count * 32L + 256);
            var list = original.Kind == PdfValueKind.Array ? original.Items.ToList() : new List<PdfValue>();
            foreach (var item in batch)
            {
                var annotation = item.Annotation;
                var properties = annotation.Kind == PdfAnnotationKind.RichMedia ? PdfAnnotationMedia.Materialize(annotation.Properties, writer, graph) : writer.EmbedValue(annotation.Properties);
                var entries = graph.Copy(properties);
                entries["Type"] = PdfValue.Name("Annot");
                entries["Subtype"] = PdfValue.Name(annotation.Kind.ToString());
                entries["Rect"] = annotation.Bounds.Value;
                entries["P"] = PdfValue.Reference(binding.ObjectId);
                entries["NM"] = PdfTextStrings.Encode(annotation.Identifier, context);
                annotation.Metadata.Apply(entries, context);
                entries["AP"] = PdfGraphicsNumbers.Dict(("N", writer.Embed(annotation.NormalAppearance)));
                if (annotation.Kind == PdfAnnotationKind.FileAttachment && entries.TryGetValue("FS", out var fs) && fs.Kind != PdfValueKind.Reference)
                    entries["FS"] = PdfValue.Reference(graph.Edit.Add(fs));
                if (annotation.Metadata.ReplyTo != null)
                {
                    if (!names.TryGetValue(annotation.Metadata.ReplyTo, out var target))
                        throw graph.Error("Annotation.ReplyTarget", "The annotation reply identifier is absent, ambiguous or lacks an indirect target identity.");
                    entries["IRT"] = PdfValue.Reference(target);
                }

                graph.Edit.Set(item.Id, PdfValue.OwnedDictionary(entries));
                list.Add(PdfValue.Reference(item.Id));
            }

            PdfAnnotationSet.SetPageAnnotations(graph, pageIndex, list);
            PdfAnnotationSet.ValidateRelationships(PdfAnnotationSet.Read(graph), graph);
            // Negotiate floors through the catalog so the shared completion boundary observes this staged batch.
            var catalog = graph.Copy(graph.Catalog);
            var current = graph.Get(graph.Catalog, "Version");
            int floor = current.Kind == PdfValueKind.Name ? PdfVersionNumber.Parse(current.NameValue) : PdfVersionNumber.Parse(graph.Graph.Version);
            catalog["Version"] = PdfValue.Name(PdfVersionNumber.Text(Math.Max(floor, PdfVersionNumber.Parse(version))));
            graph.SetCatalog(catalog);
        });
    }
}
