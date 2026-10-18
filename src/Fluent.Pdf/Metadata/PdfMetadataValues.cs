using System.Xml.Linq;

namespace Fluent.Pdf;
internal static class PdfMetadataValues
{
    internal sealed class Mapping
    {
        internal readonly string Info;
        internal readonly XName Name;
        internal readonly int Kind;
        internal readonly bool Date;
        internal Mapping(string info, XName name, int kind = 0, bool date = false)
        {
            Info = info;
            Name = name;
            Kind = kind;
            Date = date;
        }
    }

    internal static readonly IReadOnlyList<Mapping> Mappings = Array.AsReadOnly(new[] { new Mapping("Title", PdfMetadataXml.Dc + "title", 1), new Mapping("Author", PdfMetadataXml.Dc + "creator", 2), new Mapping("Subject", PdfMetadataXml.Dc + "description", 1), new Mapping("Keywords", PdfMetadataXml.Pdf + "Keywords"), new Mapping("Creator", PdfMetadataXml.Xmp + "CreatorTool"), new Mapping("Producer", PdfMetadataXml.Pdf + "Producer"), new Mapping("CreationDate", PdfMetadataXml.Xmp + "CreateDate", date: true), new Mapping("ModDate", PdfMetadataXml.Xmp + "ModifyDate", date: true), new Mapping("Trapped", PdfMetadataXml.Pdf + "Trapped") });
    internal static void Key(string name)
    {
        Guard.Text(name, nameof(name), 127);
        PdfValue.Name(name);
    }

    internal static PdfValue Information(PdfGraphEdit graph)
    {
        var value = graph.Get(graph.Edit.Trailer, "Info");
        if (value.Kind != PdfValueKind.Null && value.Kind != PdfValueKind.Dictionary)
            throw graph.Error("Metadata.InformationDictionary", "The trailer Info entry must resolve to a dictionary.");
        return value;
    }

    internal static void ValidateIdentity(PdfValue value, PdfGraphEdit graph, bool writing)
    {
        if (value.Kind == PdfValueKind.Null)
            return;
        bool valid = value.Kind == PdfValueKind.Array && value.Items.Count == 2 && value.Items.All(x => graph.Resolve(x).Kind == PdfValueKind.String && graph.Resolve(x).StringBytes.Count > 0);
        if (!valid)
        {
            if (writing)
                throw graph.Error("Metadata.IdentifierShape", "An ID must contain two nonempty byte strings.");
            graph.Context.Report(new PdfDiagnostic("Metadata.UnsupportedIdentity", "The source trailer ID is retained in its raw representation.", severity: PdfDiagnosticSeverity.Warning));
        }
    }

    internal static void Remove(PdfGraphEdit graph, PdfMetadataRemovalOptions options)
    {
        var context = graph.Context;
        var ids = new List<PdfObjectId>();
        foreach (var id in graph.Edit.ActiveIds(context))
        {
            context.Consume(PdfResourceKind.ExpandedBytes, 16);
            ids.Add(id);
        }

        bool embedded = false;
        var purge = new HashSet<PdfObjectId>();
        if (graph.Edit.Trailer.Entries.TryGetValue("Info", out var info))
            Mark(info);
        foreach (var id in ids)
        {
            var value = graph.Edit.Resolve(id);
            if (value.Kind == PdfValueKind.Stream && Name(value, "Type") == "Metadata")
                Add(id);
            Visit(value, false);
        }

        if (embedded && options.EmbeddedFiles == PdfMetadataEmbeddedFilePolicy.Reject)
            throw graph.Error("Metadata.EmbeddedFilePolicyRequired", "Embedded files may contain internal metadata; remove them first or explicitly retain their bytes.");
        if (embedded)
            context.Report(new PdfDiagnostic("Metadata.EmbeddedFilesRetained", "Embedded file bytes are explicitly retained and may still contain internal metadata.", severity: PdfDiagnosticSeverity.Warning));
        foreach (var id in ids)
        {
            if (purge.Contains(id))
            {
                graph.Edit.Remove(id);
                continue;
            }

            var original = graph.Edit.Resolve(id);
            var result = Visit(original, true);
            if (!ReferenceEquals(result, original))
                graph.Edit.Set(id, result);
        }

        var trailer = graph.Copy(graph.Edit.Trailer);
        trailer.Remove("Info");
        if (options.RemoveIdentifiers)
            trailer.Remove("ID");
        graph.Edit.Trailer = PdfValue.OwnedDictionary(trailer);
        // This privacy operation deliberately drops unreachable active objects as well as previous revisions.
        // An old indirect Info string must not survive merely because its obsolete dictionary was removed.
        var reachable = new HashSet<PdfObjectId>();
        Reach(graph.Edit.Trailer);
        foreach (var id in ids)
            if (!purge.Contains(id) && !reachable.Contains(id))
                graph.Edit.Remove(id);
        string Name(PdfValue dictionary, string key)
        {
            var item = graph.Get(dictionary, key);
            return item.Kind == PdfValueKind.Name ? item.NameValue : "";
        }

        void Add(PdfObjectId id)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, 48);
            purge.Add(id);
        }

        void Mark(PdfValue value)
        {
            var seen = new HashSet<PdfObjectId>();
            while (value.Kind == PdfValueKind.Reference)
            {
                context.Checkpoint();
                if (!seen.Add(value.ReferenceId))
                    throw graph.Error("Metadata.ReferenceCycle", "Metadata references contain a cycle.");
                Add(value.ReferenceId);
                value = graph.Edit.Resolve(value.ReferenceId);
            }
        }

        PdfValue Visit(PdfValue value, bool rewrite)
        {
            context.Checkpoint();
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            context.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind == PdfValueKind.Reference)
                return purge.Contains(value.ReferenceId) ? PdfValue.Null : value;
            if (value.Kind == PdfValueKind.Array)
            {
                List<PdfValue>? changed = null;
                for (int i = 0; i < value.Items.Count; i++)
                {
                    var replacement = Visit(value.Items[i], rewrite);
                    if (rewrite && !ReferenceEquals(replacement, value.Items[i]))
                    {
                        context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 16L + 64);
                        changed ??= value.Items.ToList();
                        changed[i] = replacement;
                    }
                }

                return changed == null ? value : PdfValue.OwnedArray(changed);
            }

            if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
                return value;
            if (value.Entries.ContainsKey("EF") || Name(value, "Type") == "EmbeddedFile")
                embedded = true;
            Dictionary<string, PdfValue>? entries = null;
            foreach (var pair in value.Entries)
            {
                if (pair.Key == "Metadata" || pair.Key == "PieceInfo" || (pair.Key == "LastModified" && (Name(value, "Type") == "Page" || Name(value, "Subtype") == "Form")))
                {
                    if (!rewrite)
                        Mark(pair.Value);
                    else
                    {
                        entries ??= graph.Copy(value);
                        entries.Remove(pair.Key);
                    }

                    continue;
                }

                var replacement = Visit(pair.Value, rewrite);
                if (rewrite && !ReferenceEquals(pair.Value, replacement))
                {
                    entries ??= graph.Copy(value);
                    entries[pair.Key] = replacement;
                }
            }

            return entries == null ? value : value.WithDictionary(PdfValue.OwnedDictionary(entries));
        }

        void Reach(PdfValue value)
        {
            context.Checkpoint();
            if (value.Kind == PdfValueKind.Reference)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, 48);
                if (!reachable.Add(value.ReferenceId))
                    return;
                value = graph.Edit.Resolve(value.ReferenceId);
            }

            using var depth = context.EnterScope(PdfResourceKind.Depth);
            context.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind == PdfValueKind.Array)
                foreach (var item in value.Items)
                    Reach(item);
            else if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
                foreach (var item in value.Entries.Values)
                    Reach(item);
        }
    }
}
