using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Passive managed file embedding/extraction and full-rewrite removal. Payloads are never launched.</summary>
public static class PdfAttachments
{
    /// <summary>Enumerates active file specifications, embedded streams and portable-collection metadata without decoding payloads.</summary>
    public static PdfAttachmentSet Inspect(PdfDocumentSnapshot source, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        PdfDocumentSnapshot? captured = null;
        PdfValue collection = PdfValue.Null;
        var items = new List<PdfAttachment>();
        var streams = new List<PdfValue>();
        var report = PdfFont.Run("InspectAttachments", "managed.attachments", policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(source, new(), context);
            captured = graph.Snapshot;
            collection = graph.Get(graph.Catalog, "Collection");
            var names = PdfAttachmentGraph.Names(graph);
            var named = new Dictionary<PdfObjectId, string>();
            foreach (var pair in names)
                if (pair.Value.Kind == PdfValueKind.Reference && !named.ContainsKey(pair.Value.ReferenceId))
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, 96);
                    named.Add(pair.Value.ReferenceId, PdfTextStrings.Decode(pair.Key, context));
                }

            foreach (var id in graph.Edit.ActiveIds(context))
                Visit(graph.Edit.Resolve(id), id);
            void Visit(PdfValue value, PdfObjectId? id)
            {
                context.Checkpoint();
                using var depth = context.EnterScope(PdfResourceKind.Depth);
                context.Consume(PdfResourceKind.Objects, 1);
                if (value.Kind == PdfValueKind.Array)
                {
                    foreach (var child in value.Items)
                        if (child.Kind != PdfValueKind.Reference)
                            Visit(child, null);
                    return;
                }

                if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
                    return;
                string type = PdfAttachmentGraph.Name(graph, value, "Type");
                if (type == "EmbeddedFile" && value.Kind == PdfValueKind.Stream)
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, 32);
                    streams.Add(value);
                }

                if (type == "Filespec" || value.Entries.ContainsKey("EF"))
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, 256);
                    var ef = graph.Get(value, "EF");
                    var variants = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
                    if (ef.Kind != PdfValueKind.Null)
                    {
                        if (ef.Kind != PdfValueKind.Dictionary)
                            throw graph.Error("Attachments.EmbeddedDictionary", "EF must be a dictionary of embedded streams.");
                        if (!id.HasValue || type != "Filespec")
                            context.Report(new("Attachments.SpecificationShape", "An embedded file specification requires Type Filespec and indirect identity.", severity: PdfDiagnosticSeverity.Warning));
                        foreach (var pair in ef.Entries)
                        {
                            context.Consume(PdfResourceKind.ExpandedBytes, 96);
                            var stream = graph.Resolve(pair.Value);
                            if (stream.Kind != PdfValueKind.Stream)
                                throw graph.Error("Attachments.EmbeddedStream", "Every EF variant must resolve to a stream.");
                            variants.Add(pair.Key, stream);
                        }
                    }

                    string fileName = Text("UF") ?? Text("F") ?? "";
                    items.Add(new(captured!, id, value, id.HasValue && named.TryGetValue(id.Value, out var name) ? name : null, fileName, Text("Desc"), PdfAttachmentGraph.Name(graph, value, "AFRelationship")is string relationship && relationship.Length > 0 ? relationship : null, variants));
                    string? Text(string key)
                    {
                        var text = graph.Get(value, key);
                        return text.Kind == PdfValueKind.Null ? null : PdfTextStrings.Decode(text, context);
                    }
                }

                foreach (var child in value.Entries.Values)
                    if (child.Kind != PdfValueKind.Reference)
                        Visit(child, null);
            }
        }, PdfCapability.ReadDocument);
        return new(captured!, items, streams, collection, report);
    }

    /// <summary>Embeds a borrowed stream under a unique Unicode name, with size/MD5 checksum and explicit metadata. Associated files use PDF 2.0.</summary>
    public static PdfDocument Add(PdfDocumentSnapshot source, Stream input, PdfAttachmentOptions attachment, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (!input.CanRead)
            throw new ArgumentException("A readable stream is required.", nameof(input));
        if (attachment == null)
            throw new ArgumentNullException(nameof(attachment));
        return PdfGraphEdit.Run("AddAttachment", source, options, policy, cancellationToken, graph =>
        {
            var context = graph.Context;
            var names = PdfAttachmentGraph.Names(graph);
            if (names.Any(p => PdfTextStrings.Decode(p.Key, context) == attachment.FileName))
                throw graph.Error("Attachments.DuplicateName", "The EmbeddedFiles name already exists.");
            context.Consume(PdfResourceKind.Objects, 32);
            context.Consume(PdfResourceKind.ExpandedBytes, 4096 + attachment.FileName.Length * 8L);
            using var lease = new BorrowedStream(input, true, context);
            var bytes = PdfAttachmentGraph.Buffer(lease, context, out var checksum);
            var parameters = new Dictionary<string, PdfValue>
            {
                {
                    "Size",
                    PdfValue.Integer(bytes.Length)
                },
                {
                    "CheckSum",
                    PdfValue.String(checksum)
                }
            };
            if (attachment.CreationTime.HasValue)
                parameters["CreationDate"] = PdfTextStrings.Encode(PdfMetadataDates.Format(attachment.CreationTime.Value), context);
            if (attachment.ModificationTime.HasValue)
                parameters["ModDate"] = PdfTextStrings.Encode(PdfMetadataDates.Format(attachment.ModificationTime.Value), context);
            var stream = PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("EmbeddedFile")), ("Subtype", PdfValue.Name(attachment.MimeType)), ("Params", PdfValue.OwnedDictionary(parameters)));
            var data = PdfValue.Reference(graph.Edit.Add(PdfValue.StreamValue(stream, bytes, 0, bytes.Length)));
            var specification = new Dictionary<string, PdfValue>
            {
                {
                    "Type",
                    PdfValue.Name("Filespec")
                },
                {
                    "F",
                    PdfValue.String(attachment.FileName.Select(c => c >= 32 && c < 127 ? (byte)c : (byte)'_').ToArray())
                },
                {
                    "UF",
                    PdfTextStrings.Encode(attachment.FileName, context)
                },
                {
                    "EF",
                    PdfGraphicsNumbers.Dict(("F", data), ("UF", data))
                }
            };
            if (attachment.Description != null)
                specification["Desc"] = PdfTextStrings.Encode(attachment.Description, context);
            if (attachment.Relationship != null)
                specification["AFRelationship"] = PdfValue.Name(attachment.Relationship);
            var file = PdfValue.Reference(graph.Edit.Add(PdfValue.OwnedDictionary(specification)));
            context.Consume(PdfResourceKind.ExpandedBytes, 128);
            names.Add((PdfTextStrings.Encode(attachment.FileName, context), file));
            PdfAttachmentGraph.SetNames(graph, names);
            if (attachment.Relationship != null)
            {
                var catalog = graph.Copy(graph.Catalog);
                var prior = graph.Get(graph.Catalog, "AF");
                if (prior.Kind != PdfValueKind.Null && prior.Kind != PdfValueKind.Array)
                    throw graph.Error("Attachments.AssociatedArray", "Catalog AF must be an array.");
                context.Consume(PdfResourceKind.ExpandedBytes, (prior.Kind == PdfValueKind.Null ? 0 : prior.Items.Count) * 16L + 64);
                var af = prior.Kind == PdfValueKind.Null ? new List<PdfValue>() : prior.Items.ToList();
                af.Add(file);
                catalog["AF"] = PdfValue.OwnedArray(af);
                graph.SetCatalog(catalog);
            }
        }, attachment.Relationship == null ? "1.7" : "2.0");
    }

    /// <summary>Decodes and validates one embedded variant before copying to a borrowed output stream. Existing output bytes are preserved on validation failure.</summary>
    public static PdfOperationReport Extract(PdfAttachment attachment, Stream output, string? variant = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (attachment == null)
            throw new ArgumentNullException(nameof(attachment));
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (!output.CanWrite)
            throw new ArgumentException("A writable stream is required.", nameof(output));
        return PdfFont.Run("ExtractAttachment", "managed.attachments", policy, cancellationToken, context =>
        {
            var bytes = PdfAttachmentGraph.Decode(attachment, variant, context);
            using var lease = new BorrowedStream(output, false, context);
            PdfAttachmentGraph.Copy(bytes, lease, context);
        }, PdfCapability.ReadDocument);
    }

    /// <summary>Extracts to the supplied existing directory using a portable leaf filename. Default no-overwrite commit is atomic and race-safe.</summary>
    public static PdfOperationReport ExtractToDirectory(PdfAttachment attachment, string directory, string? fileName = null, bool overwrite = false, string? variant = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (attachment == null)
            throw new ArgumentNullException(nameof(attachment));
        Guard.Text(directory, nameof(directory), 32767);
        string name = PdfAttachmentOptions.PortableName(fileName ?? attachment.FileName), root = Path.GetFullPath(directory);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("The extraction directory must already exist.");
        string target = Path.Combine(root, name);
        return PdfFont.Run("ExtractAttachmentFile", "managed.attachments", policy, cancellationToken, context =>
        {
            var bytes = PdfAttachmentGraph.Decode(attachment, variant, context);
            if (overwrite)
                PdfAtomicFile.Write(target, context, output =>
                {
                    using var lease = new BorrowedStream(output, false, context);
                    PdfAttachmentGraph.Copy(bytes, lease, context);
                });
            else
            {
                string temporary = Path.Combine(root, ".fluent-pdf-attachment-" + Guid.NewGuid().ToString("N") + ".tmp");
                bool created = false;
                Exception? failure = null;
                try
                {
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        created = true;
                        using var lease = new BorrowedStream(output, false, context);
                        PdfAttachmentGraph.Copy(bytes, lease, context);
                        output.Flush(true);
                    }

                    context.EnsureComplete();
                    File.Move(temporary, target);
                }
                catch (Exception error)
                {
                    failure = error;
                    throw;
                }
                finally
                {
                    if (created)
                        try
                        {
                            File.Delete(temporary);
                        }
                        catch (Exception cleanup)when (failure != null && (cleanup is IOException || cleanup is UnauthorizedAccessException))
                        {
                            failure.Data["Fluent.Pdf.CleanupFailure"] = cleanup;
                        }
                }
            }
        }, PdfCapability.ReadDocument);
    }

    /// <summary>Sets or removes an explicitly authored portable-collection dictionary. Validates standard schema/sort/item types; unknown source collections remain inspectable.</summary>
    public static PdfDocument SetCollection(PdfDocumentSnapshot source, PdfValue? collection, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfGraphEdit.Run("SetAttachmentCollection", source, options, policy, cancellationToken, graph =>
    {
        var catalog = graph.Copy(graph.Catalog);
        if (collection == null || collection.Kind == PdfValueKind.Null)
            catalog.Remove("Collection");
        else
        {
            PdfAttachmentCollection.Validate(collection, graph);
            catalog["Collection"] = collection;
        }

        graph.SetCatalog(catalog);
    }, "1.7");
    /// <summary>Sets or removes a collection item on an indirect file specification, validating it against the current collection schema.</summary>
    public static PdfDocument SetCollectionItem(PdfAttachment attachment, PdfValue? item, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (attachment == null)
            throw new ArgumentNullException(nameof(attachment));
        if (!attachment.Id.HasValue)
            throw new ArgumentException("An indirect file specification is required.", nameof(attachment));
        return PdfGraphEdit.Run("SetAttachmentCollectionItem", attachment.Source, options, policy, cancellationToken, graph =>
        {
            var entries = graph.Copy(graph.Edit.Resolve(attachment.Id.Value));
            if (item == null || item.Kind == PdfValueKind.Null)
                entries.Remove("CI");
            else
            {
                PdfAttachmentCollection.ValidateItem(item, graph.Get(graph.Catalog, "Collection"), graph);
                entries["CI"] = PdfValue.Reference(graph.Edit.Add(item));
            }

            graph.Edit.Set(attachment.Id.Value, PdfValue.OwnedDictionary(entries));
        }, "1.7");
    }

    /// <summary>Removes every embedded file, associated-file edge and collection entry with a full rewrite, dropping orphan payloads and historical revisions.</summary>
    public static PdfDocument RemoveAll(PdfDocumentSnapshot source, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfGraphEdit.Run("RemoveAllAttachments", source, options, policy, cancellationToken, PdfAttachmentGraph.RemoveAll);
}
