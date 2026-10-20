using System.IO;
using System.Security.Cryptography;

namespace Fluent.Pdf;
internal static class PdfAttachmentGraph
{
    internal static string Name(PdfGraphEdit graph, PdfValue value, string key)
    {
        var item = graph.Get(value, key);
        return item.Kind == PdfValueKind.Name ? item.NameValue : "";
    }

    internal static List<(PdfValue Key, PdfValue Value)> Names(PdfGraphEdit graph)
    {
        var names = graph.Get(graph.Catalog, "Names");
        if (names.Kind != PdfValueKind.Null && names.Kind != PdfValueKind.Dictionary)
            throw graph.Error("Attachments.NameDictionary", "Catalog Names must be a dictionary.");
        var tree = graph.Get(names, "EmbeddedFiles");
        return tree.Kind == PdfValueKind.Null ? new() : new PdfNavigationReader(graph.Edit, graph.Context, graph.Catalog, Array.Empty<PdfValue>()).NameTree(tree);
    }

    internal static void SetNames(PdfGraphEdit graph, List<(PdfValue Key, PdfValue Value)> pairs)
    {
        var context = graph.Context;
        context.Consume(PdfResourceKind.ExpandedBytes, pairs.Count * 128L + 256);
        pairs.Sort((a, b) => Compare(a.Key, b.Key));
        var items = new List<PdfValue>();
        PdfValue? previous = null;
        foreach (var pair in pairs)
        {
            context.Checkpoint();
            if (previous != null && Compare(pair.Key, previous) == 0)
                throw graph.Error("Attachments.DuplicateName", "EmbeddedFiles byte keys must be distinct.");
            previous = pair.Key;
            items.Add(pair.Key);
            items.Add(pair.Value);
        }

        var catalog = graph.Copy(graph.Catalog);
        var names = graph.Copy(graph.Get(graph.Catalog, "Names"));
        if (pairs.Count == 0)
            names.Remove("EmbeddedFiles");
        else
            names["EmbeddedFiles"] = PdfValue.Reference(graph.Edit.Add(PdfGraphicsNumbers.Dict(("Names", PdfValue.OwnedArray(items)))));
        catalog["Names"] = PdfValue.OwnedDictionary(names);
        graph.SetCatalog(catalog);
        int Compare(PdfValue a, PdfValue b)
        {
            var left = a.StringBytes;
            var right = b.StringBytes;
            for (int i = 0; i < Math.Min(left.Count, right.Count); i++)
            {
                if ((i & 1023) == 0)
                    context.Checkpoint();
                int difference = left[i] - right[i];
                if (difference != 0)
                    return difference;
            }

            return left.Count.CompareTo(right.Count);
        }
    }

    // MD5 is solely the legacy PDF CheckSum field, never a security/integrity or authenticity claim.
    internal static PdfByteStore Buffer(Stream input, PdfOperationContext context, out byte[] checksum)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 9216);
        using var md5 = MD5.Create();
        var chunk = new byte[8192];
        using var buffer = new PdfCodecBufferStream(context, context.Available(PdfResourceKind.ExpandedBytes));
        int count;
        while ((count = input.Read(chunk, 0, chunk.Length)) != 0)
        {
            context.Checkpoint();
            if (count > context.Available(PdfResourceKind.ExpandedBytes))
                context.RejectLimit(PdfResourceKind.ExpandedBytes);
            buffer.Write(chunk, 0, count);
            md5.TransformBlock(chunk, 0, count, chunk, 0);
        }

        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        checksum = md5.Hash!;
        context.Checkpoint();
        return buffer.Finish();
    }

    internal static PdfByteStore Decode(PdfAttachment attachment, string? variant, PdfOperationContext context)
    {
        if (variant == null)
        {
            if (attachment.Variants.ContainsKey("UF"))
                variant = "UF";
            else if (attachment.Variants.ContainsKey("F"))
                variant = "F";
            else if (attachment.Variants.Count == 1)
                variant = attachment.Variants.Keys.First();
            else
                throw PdfWriterCore.Error(context, "Attachments.VariantRequired", "Select one embedded variant explicitly; external files are never fetched.");
        }

        if (!attachment.Variants.TryGetValue(variant, out var stream))
            throw PdfWriterCore.Error(context, "Attachments.VariantMissing", "The selected embedded variant does not exist.");
        using var graph = new PdfGraphEdit(attachment.Source, new(), context);
        var type = graph.Get(stream, "Type");
        if (type.Kind != PdfValueKind.Null && (type.Kind != PdfValueKind.Name || type.NameValue != "EmbeddedFile"))
            throw graph.Error("Attachments.StreamType", "An embedded variant has an invalid stream Type.");
        using var decoded = PdfFilters.Decode(PdfFilterDeclarations.Resolve(stream, id => graph.Resolve(PdfValue.Reference(id)), context), context);
        var bytes = Buffer(decoded, context, out var checksum);
        var parameters = graph.Get(stream, "Params");
        if (parameters.Kind != PdfValueKind.Null && parameters.Kind != PdfValueKind.Dictionary)
            throw graph.Error("Attachments.Parameters", "Embedded Params must be a dictionary.");
        var size = graph.Get(parameters, "Size");
        if (size.Kind != PdfValueKind.Null && (!size.TryGetInteger(out long length) || length < 0 || length != bytes.Length))
            throw graph.Error("Attachments.SizeMismatch", "The decoded embedded payload does not match its declared Size.");
        var expected = graph.Get(parameters, "CheckSum");
        if (expected.Kind != PdfValueKind.Null)
        {
            if (expected.Kind != PdfValueKind.String || expected.StringBytes.Count != 16)
                throw graph.Error("Attachments.ChecksumShape", "An embedded CheckSum must contain 16 MD5 bytes.");
            int difference = 0;
            for (int i = 0; i < checksum.Length; i++)
                difference |= checksum[i] ^ expected.StringBytes[i];
            if (difference != 0)
                throw graph.Error("Attachments.ChecksumMismatch", "The decoded embedded payload does not match its declared CheckSum.");
        }

        foreach (var key in new[]
        {
            "CreationDate",
            "ModDate"
        }

        )
        {
            var date = graph.Get(parameters, key);
            if (date.Kind != PdfValueKind.Null)
                PdfMetadataDates.Validate(PdfTextStrings.Decode(date, context), context);
        }

        return bytes;
    }

    internal static void Copy(PdfByteStore bytes, Stream output, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var chunk = new byte[8192];
        using var input = bytes.Open(0, bytes.Length);
        int count;
        while ((count = input.Read(chunk, 0, chunk.Length)) != 0)
        {
            context.Checkpoint();
            output.Write(chunk, 0, count);
        }
    }

    internal static void RemoveAll(PdfGraphEdit graph)
    {
        var context = graph.Context;
        var ids = new List<PdfObjectId>();
        var purge = new HashSet<PdfObjectId>();
        foreach (var id in graph.Edit.ActiveIds(context))
        {
            context.Consume(PdfResourceKind.ExpandedBytes, 32);
            ids.Add(id);
        }

        foreach (var id in ids)
        {
            var value = graph.Edit.Resolve(id);
            if (IsPayload(value))
                Mark(PdfValue.Reference(id));
            Visit(value, false);
        }

        foreach (var id in ids)
        {
            if (purge.Contains(id))
            {
                graph.Edit.Remove(id);
                continue;
            }

            var original = graph.Edit.Resolve(id);
            var result = Visit(original, true);
            if (!ReferenceEquals(original, result))
                graph.Edit.Set(id, result);
        }

        var catalog = graph.Copy(graph.Catalog);
        catalog.Remove("Collection");
        graph.SetCatalog(catalog);
        graph.PruneUnreachable();
        bool IsPayload(PdfValue value) => (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream) && (Name(graph, value, "Type") == "EmbeddedFile" || value.Entries.ContainsKey("EF") || Name(graph, value, "Subtype") == "FileAttachment");
        void Mark(PdfValue value)
        {
            var seen = new HashSet<PdfObjectId>();
            while (value.Kind == PdfValueKind.Reference)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.ExpandedBytes, 96);
                if (!seen.Add(value.ReferenceId))
                    throw graph.Error("Attachments.ReferenceCycle", "An embedded reference chain contains a cycle.");
                purge.Add(value.ReferenceId);
                value = graph.Edit.Resolve(value.ReferenceId);
            }
        }

        PdfValue Visit(PdfValue value, bool rewrite, bool compact = false)
        {
            context.Checkpoint();
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            context.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind == PdfValueKind.Reference)
                return rewrite && purge.Contains(value.ReferenceId) ? PdfValue.Null : value;
            if (value.Kind == PdfValueKind.Array)
            {
                List<PdfValue>? changed = null;
                for (int i = 0; i < value.Items.Count; i++)
                {
                    var result = Visit(value.Items[i], rewrite);
                    if (rewrite && !ReferenceEquals(result, value.Items[i]) && changed == null)
                    {
                        context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 32L + 64);
                        changed = value.Items.Take(i).ToList();
                    }

                    if (changed != null && (!compact || result.Kind != PdfValueKind.Null || ReferenceEquals(result, value.Items[i])))
                        changed.Add(result);
                }

                return changed == null ? value : PdfValue.OwnedArray(changed);
            }

            if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
                return value;
            if (!rewrite && value.Entries.TryGetValue("EF", out var ef))
            {
                ef = graph.Resolve(ef);
                if (ef.Kind != PdfValueKind.Dictionary)
                    throw graph.Error("Attachments.EmbeddedDictionary", "EF must be a dictionary before file removal.");
                foreach (var stream in ef.Entries.Values)
                    Mark(stream);
            }

            if (!rewrite && value.Entries.TryGetValue("RF", out var related))
            {
                related = graph.Resolve(related);
                if (related.Kind != PdfValueKind.Dictionary)
                    throw graph.Error("Attachments.RelatedDictionary", "RF must be a dictionary before file removal.");
                foreach (var pair in related.Entries)
                {
                    var array = graph.Resolve(pair.Value);
                    if (array.Kind != PdfValueKind.Array || array.Items.Count % 2 != 0)
                        throw graph.Error("Attachments.RelatedArray", "Related files require name/stream pairs.");
                    for (int i = 1; i < array.Items.Count; i += 2)
                        Mark(array.Items[i]);
                }
            }

            if (rewrite && (IsPayload(value) || Name(graph, value, "Type") == "OBJR" && value.Entries.TryGetValue("Obj", out var obj) && obj.Kind == PdfValueKind.Reference && purge.Contains(obj.ReferenceId)))
                return PdfValue.Null;
            Dictionary<string, PdfValue>? entries = null;
            foreach (var pair in value.Entries)
            {
                if (pair.Key == "AF" || pair.Key == "AFRelationship" || pair.Key == "EmbeddedFiles" || pair.Key == "RF")
                {
                    if (rewrite)
                    {
                        entries ??= graph.Copy(value);
                        entries.Remove(pair.Key);
                    }

                    continue;
                }

                var result = Visit(pair.Value, rewrite, pair.Key == "Annots" || pair.Key == "Kids" || pair.Key == "AF" || pair.Key == "Fields");
                if (rewrite && !ReferenceEquals(result, pair.Value))
                {
                    entries ??= graph.Copy(value);
                    if (result.Kind == PdfValueKind.Null)
                        entries.Remove(pair.Key);
                    else
                        entries[pair.Key] = result;
                }
            }

            return entries == null ? value : value.WithDictionary(PdfValue.OwnedDictionary(entries));
        }

    }
}
