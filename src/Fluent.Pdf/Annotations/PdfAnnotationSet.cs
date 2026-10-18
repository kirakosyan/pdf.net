using System.Collections.ObjectModel;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable original annotation entry. Unknown keys and appearance states remain in its source-owned definition.</summary>
public sealed class PdfAnnotationRecord
{
    internal PdfAnnotationRecord(int index, int page, int ordinal, PdfObjectId? id, PdfValue definition, PdfRect bounds, string subtype, IReadOnlyDictionary<string, string> text)
    {
        Index = index;
        PageIndex = page;
        PageOrdinal = ordinal;
        ObjectId = id;
        Definition = definition;
        Bounds = bounds;
        Subtype = subtype;
        TextProperties = text;
    }

    /// <summary>Gets its stable index within the captured set.</summary>
    public int Index { get; }
    /// <summary>Gets its zero-based source page.</summary>
    public int PageIndex { get; }
    /// <summary>Gets its zero-based position in the original page annotation array.</summary>
    public int PageOrdinal { get; }
    /// <summary>Gets the original indirect identity, or null for a direct annotation dictionary.</summary>
    public PdfObjectId? ObjectId { get; }
    /// <summary>Gets the exact immutable dictionary, retaining references into the captured source graph.</summary>
    public PdfValue Definition { get; }
    /// <summary>Gets native page-coordinate bounds.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets the original subtype name, including unsupported kinds.</summary>
    public string Subtype { get; }
    /// <summary>Gets decoded standard text properties by PDF key, including original date/state strings.</summary>
    public IReadOnlyDictionary<string, string> TextProperties { get; }
    /// <summary>Gets the optional stable annotation name.</summary>
    public string? Identifier => TextProperties.TryGetValue("NM", out var value) ? value : null;
}

/// <summary>A detached annotation manifest; referenced bytes survive source disposal and remain passive.</summary>
public sealed partial class PdfAnnotationSet
{
    internal PdfAnnotationSet(PdfDocumentSnapshot source, PdfAnnotationRecord[] records, PdfOperationReport report)
    {
        Source = source;
        Annotations = new ReadOnlyCollection<PdfAnnotationRecord>(records);
        OperationReport = report;
    }

    /// <summary>Gets the immutable captured source graph and pages.</summary>
    public PdfDocumentSnapshot Source { get; }
    /// <summary>Gets all page annotations, including popups, widgets, links and unknown subtypes.</summary>
    public IReadOnlyList<PdfAnnotationRecord> Annotations { get; }
    /// <summary>Gets the completed bounded inspection report.</summary>
    public PdfOperationReport OperationReport { get; }

    /// <summary>Inspects original annotation dictionaries, metadata and relationships without evaluating actions or media.</summary>
    public static PdfAnnotationSet Inspect(PdfDocumentSnapshot source, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        PdfDocumentSnapshot? captured = null;
        PdfAnnotationRecord[]? records = null;
        var report = PdfFont.Run("InspectAnnotations", "managed.annotations", policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(source, new(), context);
            captured = graph.Snapshot;
            records = Read(graph);
            ValidateRelationships(records, graph);
        }, PdfCapability.ReadDocument);
        return new(captured!, records!, report);
    }

    internal static PdfAnnotationRecord[] Read(PdfGraphEdit graph)
    {
        var context = graph.Context;
        var result = new List<PdfAnnotationRecord>();
        var identities = new HashSet<PdfObjectId>();
        for (int page = 0; page < graph.Snapshot.Pages.Count; page++)
        {
            context.Checkpoint();
            var binding = graph.Snapshot.Pages[page].Source!;
            var array = graph.Get(graph.Edit.Resolve(binding.ObjectId), "Annots");
            if (array.Kind == PdfValueKind.Null)
                continue;
            if (array.Kind != PdfValueKind.Array)
                throw graph.Error("Annotation.Array", "Page annotations must resolve to an array.");
            for (int ordinal = 0; ordinal < array.Items.Count; ordinal++)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 1024);
                var declared = array.Items[ordinal];
                PdfObjectId? id = declared.Kind == PdfValueKind.Reference ? declared.ReferenceId : null;
                if (id.HasValue && !identities.Add(id.Value))
                    throw graph.Error("Annotation.SharedOwnership", "An annotation cannot occur more than once in the page annotation arrays.");
                var value = graph.Resolve(declared);
                if (value.Kind != PdfValueKind.Dictionary)
                    throw graph.Error("Annotation.Dictionary", "An annotation must resolve to a dictionary.");
                var subtype = graph.Get(value, "Subtype");
                if (subtype.Kind != PdfValueKind.Name)
                    throw graph.Error("Annotation.Subtype", "An annotation requires an explicit subtype name.");
                var bounds = PdfAnnotation.ReadRect(graph.Get(value, "Rect"), graph.Resolve, context);
                var text = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string key in new[]
                {
                    "NM",
                    "Contents",
                    "T",
                    "Subj",
                    "CreationDate",
                    "M",
                    "State",
                    "StateModel"
                }

                )
                    if (value.Entries.TryGetValue(key, out var field) && graph.Resolve(field).Kind != PdfValueKind.Null)
                        text.Add(key, PdfTextStrings.Decode(graph.Resolve(field), context));
                if (!Enum.TryParse<PdfAnnotationKind>(subtype.NameValue, out _) && subtype.NameValue != "Popup" && subtype.NameValue != "Link" && subtype.NameValue != "Widget")
                    context.Report(new("Annotation.UnknownSubtype", "An unsupported annotation subtype remains inspectable without execution or appearance regeneration.", severity: PdfDiagnosticSeverity.Information));
                result.Add(new(result.Count, page, ordinal, id, value, bounds, subtype.NameValue, new ReadOnlyDictionary<string, string>(text)));
            }
        }

        return result.ToArray();
    }

    internal static void ValidateRelationships(IReadOnlyList<PdfAnnotationRecord> records, PdfGraphEdit graph)
    {
        graph.Context.Consume(PdfResourceKind.ExpandedBytes, records.Count * 96L + 64);
        var byValue = new Dictionary<PdfValue, PdfAnnotationRecord>();
        foreach (var record in records)
        {
            if (byValue.ContainsKey(record.Definition))
                throw graph.Error("Annotation.SharedOwnership", "An annotation dictionary cannot have multiple page entries.");
            byValue.Add(record.Definition, record);
        }

        foreach (var record in records)
        {
            var current = record.Definition;
            var seen = new HashSet<PdfValue>();
            var scopes = new List<IDisposable>();
            try
            {
                while (current.Entries.TryGetValue("IRT", out var reply))
                {
                    graph.Context.Checkpoint();
                    graph.Context.Consume(PdfResourceKind.Objects, 1);
                    graph.Context.Consume(PdfResourceKind.ExpandedBytes, 64);
                    scopes.Add(graph.Context.EnterScope(PdfResourceKind.Depth));
                    if (!seen.Add(current))
                        throw graph.Error("Annotation.ReplyCycle", "The annotation reply graph contains a cycle.");
                    current = graph.Resolve(reply);
                    if (!byValue.ContainsKey(current))
                        throw graph.Error("Annotation.ReplyTarget", "An annotation reply target must belong to the captured page annotations.");
                }
            }
            finally
            {
                for (int i = scopes.Count - 1; i >= 0; i--)
                    scopes[i].Dispose();
            }

            if (record.Definition.Entries.TryGetValue("Popup", out var popup))
            {
                var value = graph.Resolve(popup);
                if (!byValue.TryGetValue(value, out var child) || child.Subtype != "Popup" || child.PageIndex != record.PageIndex || !ReferenceEquals(graph.Get(value, "Parent"), record.Definition))
                    throw graph.Error("Annotation.PopupRelationship", "A popup requires reciprocal same-page parent and popup relationships.");
            }

            if (record.Subtype == "Popup")
            {
                var parent = graph.Get(record.Definition, "Parent");
                if (!byValue.TryGetValue(parent, out var owner) || owner.PageIndex != record.PageIndex || !ReferenceEquals(graph.Get(parent, "Popup"), record.Definition))
                    throw graph.Error("Annotation.PopupRelationship", "A popup must identify a reciprocal same-page annotation parent.");
            }
        }
    }

    internal HashSet<int> Selection(IEnumerable<int> indices, PdfOperationContext context)
    {
        if (indices == null)
            throw new ArgumentNullException(nameof(indices));
        var chosen = new HashSet<int>();
        using var enumerator = indices.GetEnumerator();
        while (true)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            if (!enumerator.MoveNext())
                break;
            int index = enumerator.Current;
            if (index < 0 || index >= Annotations.Count)
                throw new ArgumentOutOfRangeException(nameof(indices));
            context.Consume(PdfResourceKind.ExpandedBytes, 64);
            chosen.Add(index);
        }

        return chosen;
    }

    internal void IncludeRelated(HashSet<int> chosen, PdfGraphEdit graph, bool cascade)
    {
        var byValue = new Dictionary<PdfValue, int>();
        var children = new Dictionary<int, List<int>>();
        graph.Context.Consume(PdfResourceKind.ExpandedBytes, Annotations.Count * 160L + 128);
        foreach (var record in Annotations)
            byValue.Add(record.Definition, record.Index);
        foreach (var record in Annotations)
            foreach (string key in new[]
            {
                "IRT",
                "Parent"
            }

            )
            {
                graph.Context.Checkpoint();
                graph.Context.Consume(PdfResourceKind.Objects, 1);
                if (!record.Definition.Entries.TryGetValue(key, out var target) || !byValue.TryGetValue(graph.Resolve(target), out int parent))
                    continue;
                if (!children.TryGetValue(parent, out var list))
                    children.Add(parent, list = new());
                list.Add(record.Index);
            }

        var queue = new Queue<int>(chosen);
        while (queue.Count > 0)
        {
            graph.Context.Checkpoint();
            if (!children.TryGetValue(queue.Dequeue(), out var descendants))
                continue;
            foreach (int index in descendants)
            {
                if (chosen.Contains(index))
                    continue;
                if (!cascade)
                    throw graph.Error("Annotation.RelatedEntries", "The selection would orphan a reply or popup; select related entries or request cascading removal.");
                chosen.Add(index);
                queue.Enqueue(index);
            }
        }
    }

    internal static void SetPageAnnotations(PdfGraphEdit graph, int pageIndex, IEnumerable<PdfValue> annotations)
    {
        var binding = graph.Snapshot.Pages[pageIndex].Source!;
        var entries = graph.Copy(graph.Edit.Resolve(binding.ObjectId));
        var list = new List<PdfValue>();
        foreach (var annotation in annotations)
        {
            graph.Context.Checkpoint();
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, 16);
            list.Add(annotation);
        }

        graph.Context.Consume(PdfResourceKind.ExpandedBytes, 32);
        if (list.Count == 0)
            entries.Remove("Annots");
        else
            entries["Annots"] = PdfValue.OwnedArray(list);
        graph.Edit.Set(binding.ObjectId, PdfValue.OwnedDictionary(entries));
    }
}
