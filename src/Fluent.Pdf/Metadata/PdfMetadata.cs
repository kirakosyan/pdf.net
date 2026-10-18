using System.Collections.ObjectModel;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>The explicit authority used when synchronizing equivalent Info and XMP properties.</summary>
public enum PdfMetadataSynchronization
{
    /// <summary>Copy representable Info fields into XMP, retaining unrelated properties.</summary>
    InformationToXmp,
    /// <summary>Copy representable XMP fields into Info, retaining unrelated entries.</summary>
    XmpToInformation,
    /// <summary>Use XMP when its MetadataDate is at least the Info ModDate; otherwise use Info.</summary>
    PreferNewest
}

/// <summary>How document metadata removal handles files whose internal metadata is outside the PDF graph.</summary>
public enum PdfMetadataEmbeddedFilePolicy
{
    /// <summary>Reject removal when embedded files exist, requiring an explicit attachment decision.</summary>
    Reject,
    /// <summary>Preserve embedded file bytes and report that their internal metadata remains.</summary>
    Retain
}

/// <summary>Explicit metadata-removal decisions. Removal always rewrites the active graph without prior revisions.</summary>
public sealed class PdfMetadataRemovalOptions
{
    /// <summary>Captures whether identifiers are removed and whether embedded files may retain internal metadata.</summary>
    public PdfMetadataRemovalOptions(bool removeIdentifiers = true, PdfMetadataEmbeddedFilePolicy embeddedFiles = PdfMetadataEmbeddedFilePolicy.Reject)
    {
        if (!Enum.IsDefined(typeof(PdfMetadataEmbeddedFilePolicy), embeddedFiles))
            throw new ArgumentOutOfRangeException(nameof(embeddedFiles));
        RemoveIdentifiers = removeIdentifiers;
        EmbeddedFiles = embeddedFiles;
    }

    /// <summary>Gets whether trailer identifiers are removed.</summary>
    public bool RemoveIdentifiers { get; }
    /// <summary>Gets the explicit embedded-file decision.</summary>
    public PdfMetadataEmbeddedFilePolicy EmbeddedFiles { get; }
}

/// <summary>A detached passive document metadata inventory, including raw values and parsing diagnostics.</summary>
public sealed class PdfMetadataSet
{
    internal PdfMetadataSet(PdfDocumentSnapshot source, PdfValue information, Dictionary<string, string> text, string? xmp, PdfValue identity, PdfOperationReport report)
    {
        Source = source;
        Information = information;
        Text = new ReadOnlyDictionary<string, string>(text);
        XmpXml = xmp;
        Identity = identity;
        OperationReport = report;
    }

    /// <summary>Gets the captured source used for subsequent metadata edits.</summary>
    public PdfDocumentSnapshot Source { get; }
    /// <summary>Gets the resolved original Info dictionary or null, retaining unsupported values.</summary>
    public PdfValue Information { get; }
    /// <summary>Gets decoded Info text strings. Date strings retain their original precision and offsets.</summary>
    public IReadOnlyDictionary<string, string> Text { get; }
    /// <summary>Gets the validated XML packet, or null when no document XMP exists.</summary>
    public string? XmpXml { get; }
    /// <summary>Gets the resolved trailer ID value, including unsupported source representations.</summary>
    public PdfValue Identity { get; }
    /// <summary>Gets the operation diagnostics and resource usage.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>Managed passive metadata editing with explicit clocks, identifiers and synchronization authority.</summary>
public static class PdfMetadata
{
    /// <summary>Inspects Info, XMP and trailer identity without fetching resources or executing document actions.</summary>
    public static PdfMetadataSet Inspect(PdfDocumentSnapshot source, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        PdfDocumentSnapshot? captured = null;
        PdfValue information = PdfValue.Null, identity = PdfValue.Null;
        var text = new Dictionary<string, string>(StringComparer.Ordinal);
        string? xml = null;
        var report = PdfFont.Run("InspectMetadata", "managed.metadata", policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(source, new(), context);
            captured = graph.Snapshot;
            information = PdfMetadataValues.Information(graph);
            identity = graph.Get(graph.Edit.Trailer, "ID");
            foreach (var pair in information.Kind == PdfValueKind.Null ? new Dictionary<string, PdfValue>() : information.Entries)
            {
                context.Checkpoint();
                var value = graph.Resolve(pair.Value);
                if (value.Kind == PdfValueKind.String)
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, 96);
                    text.Add(pair.Key, PdfTextStrings.Decode(value, context));
                }
                else if (pair.Key != "Trapped" || value.Kind != PdfValueKind.Name || !new[]
                {
                    "True",
                    "False",
                    "Unknown"
                }.Contains(value.NameValue))
                    context.Report(new PdfDiagnostic("Metadata.UnsupportedInformationValue", "A source Info entry is retained in its raw representation.", severity: PdfDiagnosticSeverity.Warning));
            }

            var packet = PdfMetadataXml.Read(graph, graph.Get(graph.Catalog, "Metadata"));
            xml = packet?.Serialize(context);
            PdfMetadataValues.ValidateIdentity(identity, graph, false);
        }, PdfCapability.ReadDocument);
        return new(captured!, information, text, xml, identity, report);
    }

    /// <summary>Applies one bounded synchronous metadata edit and returns a new document. No time or identity is inferred.</summary>
    public static PdfDocument Edit(PdfDocumentSnapshot source, Action<PdfMetadataEditor> edit, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (edit == null)
            throw new ArgumentNullException(nameof(edit));
        return PdfGraphEdit.Run("EditMetadata", source, options, policy, cancellationToken, graph =>
        {
            var editor = new PdfMetadataEditor(graph);
            try
            {
                edit(editor);
                editor.Commit();
            }
            finally
            {
                editor.Expire();
            }
        });
    }

    /// <summary>Removes Info, PDF metadata streams and piece information in a full rewrite; embedded file retention requires an explicit decision.</summary>
    public static PdfDocument Remove(PdfDocumentSnapshot source, PdfMetadataRemovalOptions? removal = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfGraphEdit.Run("RemoveMetadata", source, options, policy, cancellationToken, graph => PdfMetadataValues.Remove(graph, removal ?? new()));
}
