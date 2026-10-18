using System.Threading;

namespace Fluent.Pdf;
/// <summary>Detached passive version and raw catalog compatibility inventory. This is not PDF conformance validation.</summary>
public sealed class PdfCompatibilityInspection
{
    internal PdfCompatibilityInspection(PdfDocumentSnapshot source, string header, string effective, PdfValue extensions, PdfValue associatedFiles, PdfValue namespaces, PdfOperationReport report)
    {
        Source = source;
        HeaderVersion = header;
        EffectiveVersion = effective;
        Extensions = extensions;
        CatalogAssociatedFiles = associatedFiles;
        StructureNamespaces = namespaces;
        OperationReport = report;
    }

    /// <summary>Gets the captured immutable source, retaining indirect extension/namespace relationships.</summary>
    public PdfDocumentSnapshot Source { get; }
    /// <summary>Gets the physical original header version.</summary>
    public string HeaderVersion { get; }
    /// <summary>Gets the larger supported header/catalog version. It does not establish feature conformance.</summary>
    public string EffectiveVersion { get; }
    /// <summary>Gets the resolved outer Extensions dictionary or null. Unknown payload values remain exact immutable PDF values.</summary>
    public PdfValue Extensions { get; }
    /// <summary>Gets the resolved catalog AF value or null. Associated files on other objects are outside this catalog inventory.</summary>
    public PdfValue CatalogAssociatedFiles { get; }
    /// <summary>Gets the resolved structure-root Namespaces value or null. Namespace semantics are not inferred or rewritten.</summary>
    public PdfValue StructureNamespaces { get; }
    /// <summary>Gets diagnostics and shared resource accounting.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>Passive format-version and extension inventory using the normal managed object graph.</summary>
public static class PdfCompatibility
{
    /// <summary>Inspects declared versions and preserves raw extension/namespace values without following URLs, running actions or decrypting an encrypted input.</summary>
    public static PdfCompatibilityInspection Inspect(PdfDocumentSnapshot source, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        PdfDocumentSnapshot? captured = null;
        string header = "", effective = "";
        PdfValue extensions = PdfValue.Null, associated = PdfValue.Null, namespaces = PdfValue.Null;
        var report = PdfFont.Run("InspectCompatibility", "managed.compatibility", policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(source, new(), context);
            captured = graph.Snapshot;
            header = graph.Graph.Version;
            int version = PdfVersionNumber.Parse(header);
            var declared = graph.Get(graph.Catalog, "Version");
            if (declared.Kind != PdfValueKind.Null)
            {
                if (declared.Kind != PdfValueKind.Name)
                    throw graph.Error("Compatibility.Version", "A supported catalog version name is required.");
                try
                {
                    version = Math.Max(version, PdfVersionNumber.Parse(declared.NameValue));
                }
                catch (ArgumentException)
                {
                    throw graph.Error("Compatibility.Version", "The catalog version is outside the supported version matrix.");
                }
            }

            effective = PdfVersionNumber.Text(version);
            associated = graph.Get(graph.Catalog, "AF");
            var structure = graph.Get(graph.Catalog, "StructTreeRoot");
            if (structure.Kind != PdfValueKind.Null)
            {
                if (structure.Kind != PdfValueKind.Dictionary)
                    throw graph.Error("Compatibility.StructureRoot", "The structure root must resolve to a dictionary.");
                namespaces = graph.Get(structure, "Namespaces");
            }

            extensions = graph.Get(graph.Catalog, "Extensions");
            if (extensions.Kind != PdfValueKind.Null)
            {
                if (extensions.Kind != PdfValueKind.Dictionary)
                    throw graph.Error("Compatibility.Extensions", "Catalog Extensions must resolve to a dictionary.");
                foreach (var pair in extensions.Entries)
                {
                    context.Checkpoint();
                    context.Consume(PdfResourceKind.Objects, 1);
                    context.Consume(PdfResourceKind.ExpandedBytes, 128);
                    if (pair.Key == "Type")
                    {
                        if (pair.Value.Kind != PdfValueKind.Name || pair.Value.NameValue != "Extensions")
                            throw graph.Error("Compatibility.Extensions", "Extensions Type must be the direct name Extensions.");
                        continue;
                    }

                    var declaration = graph.Resolve(pair.Value);
                    if (declaration.Kind != PdfValueKind.Dictionary)
                        throw graph.Error("Compatibility.Extensions", "A developer declaration must resolve to a dictionary.");
                    // Inventory keeps malformed/unsupported source metadata visible rather than silently repairing it.
                    if (version >= 20 && (!declaration.Entries.TryGetValue("URL", out var url) || url.Kind != PdfValueKind.String))
                        context.Report(new("Compatibility.ExtensionDocumentation", "A PDF 2.0 developer declaration lacks its required direct documentation string; raw data is retained.", severity: PdfDiagnosticSeverity.Warning));
                }
            }

            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 512);
        }, PdfCapability.ReadDocument);
        return new(captured!, header, effective, extensions, associated, namespaces, report);
    }
}
