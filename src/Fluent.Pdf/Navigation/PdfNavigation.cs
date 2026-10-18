using System.Threading;

namespace Fluent.Pdf;
/// <summary>A named destination and its resolved local page identity.</summary>
public sealed class PdfNamedDestination
{
    internal PdfNamedDestination(string name, PdfValue definition, int pageIndex)
    {
        Name = name;
        Definition = definition;
        PageIndex = pageIndex;
    }

    /// <summary>Gets the decoded destination name.</summary>
    public string Name { get; }
    /// <summary>Gets the preserved source value.</summary>
    public PdfValue Definition { get; }
    /// <summary>Gets the target's physical index in the captured snapshot.</summary>
    public int PageIndex { get; }
}

/// <summary>A bounded passive inspection of all active objects, including orphan action definitions and physical attachment edges.</summary>
public sealed class PdfNavigationSet
{
    internal PdfNavigationSet(PdfDocumentSnapshot source, IReadOnlyList<PdfActionRecord> actions, IReadOnlyList<PdfNamedDestination> destinations, PdfOperationReport report)
    {
        Source = source;
        Actions = actions;
        NamedDestinations = destinations;
        OperationReport = report;
    }

    /// <summary>Gets the detached normalized input snapshot.</summary>
    public PdfDocumentSnapshot Source { get; }
    /// <summary>Gets every action definition, known attachment slot and opaque active-content observation.</summary>
    public IReadOnlyList<PdfActionRecord> Actions { get; }
    /// <summary>Gets the named destinations, resolving page identities without executing actions.</summary>
    public IReadOnlyList<PdfNamedDestination> NamedDestinations { get; }
    /// <summary>Gets the bounded inspection report.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>Original managed navigation authoring, inspection and explicit action-policy sanitization.</summary>
public static class PdfNavigation
{
    /// <summary>Inspects all active objects and physical action slots. Historical revisions and embedded foreign document formats are not interpreted.</summary>
    public static PdfNavigationSet Inspect(PdfDocumentSnapshot source, PdfActionPolicy? actionPolicy = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        PdfDocumentSnapshot? captured = null;
        List<PdfActionRecord>? actions = null;
        var destinations = new List<PdfNamedDestination>();
        var report = PdfFont.Run("InspectNavigation", "managed.navigation", policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(source, new(), context);
            captured = graph.Snapshot;
            var reader = new PdfNavigationReader(graph);
            actions = PdfActionInventory.Read(reader, actionPolicy ?? new());
            foreach (var pair in reader.NamedDestinations())
            {
                var destination = reader.ResolveDestination(pair.Value);
                int index = reader.Pages.ToList().FindIndex(p => p.ReferenceId == destination.Items[0].ReferenceId);
                context.Consume(PdfResourceKind.ExpandedBytes, 128);
                destinations.Add(new(reader.Text(pair.Key), pair.Value, index));
            }
        }, PdfCapability.ReadDocument);
        return new(captured!, actions!.AsReadOnly(), destinations.AsReadOnly(), report);
    }

    /// <summary>Creates or replaces one Unicode named destination. Existing ambiguous legacy names are rejected.</summary>
    public static PdfDocument SetNamedDestination(PdfDocumentSnapshot source, string name, PdfDestination? destination, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 8192)
            throw new ArgumentException("A nonempty bounded destination name is required.", nameof(name));
        return PdfGraphEdit.Run("SetNamedDestination", source, options, policy, cancellationToken, graph =>
        {
            var reader = new PdfNavigationReader(graph);
            var pairs = reader.NamedDestinations();
            var catalog = graph.Copy(graph.Catalog);
            var names = graph.Copy(reader.Get(graph.Catalog, "Names"));
            var matches = pairs.Where(p => reader.Text(p.Key) == name).ToArray();
            if (matches.Length > 1)
                throw graph.Error("Navigation.NamedDestination", "The destination name is ambiguous.");
            pairs.RemoveAll(p => reader.Text(p.Key) == name);
            // Consolidate legacy destinations as byte-valued string keys, preserving all other name trees.
            var normalized = new List<(PdfValue Key, PdfValue Value)>();
            foreach (var pair in pairs)
                normalized.Add((pair.Key.Kind == PdfValueKind.Name ? PdfValue.String(pair.Key.NameValue.Select(c => (byte)c).ToArray()) : pair.Key, pair.Value));
            if (destination != null)
                normalized.Add((PdfTextStrings.Encode(name, graph.Context), destination.Materialize(reader.Pages, graph.Context)));
            var sorted = normalized.OrderBy(p => PdfNavigationReader.BytesKey(p.Key), StringComparer.Ordinal).ToArray();
            if (sorted.Select(p => PdfNavigationReader.BytesKey(p.Key)).Distinct(StringComparer.Ordinal).Count() != sorted.Length)
                throw graph.Error("Navigation.NamedDestination", "Destination byte keys collide during legacy consolidation.");
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, sorted.Length * 64L + 256);
            names["Dests"] = PdfValue.Reference(graph.Edit.Add(PdfGraphicsNumbers.Dict(("Names", PdfValue.OwnedArray(sorted.SelectMany(p => new[] { p.Key, p.Value }).ToList())))));
            catalog["Names"] = PdfValue.OwnedDictionary(names);
            catalog.Remove("Dests");
            graph.SetCatalog(catalog);
            PdfNavigationReader.ValidateTargets(graph.Edit, graph.Edit.Trailer.Entries["Root"], reader.Pages, graph.Context);
        }, "1.7");
    }

    /// <summary>Adds a Link annotation in native unrotated page coordinates. No URI is fetched or action executed.</summary>
    public static PdfDocument AddLink(PdfDocumentSnapshot source, int pageIndex, PdfRect bounds, PdfAction action, PdfActionPolicy? actionPolicy = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (pageIndex < 0 || pageIndex >= source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (action == null)
            throw new ArgumentNullException(nameof(action));
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentException("A positive link rectangle is required.", nameof(bounds));
        return PdfGraphEdit.Run("AddLink", source, options, policy, cancellationToken, graph =>
        {
            var reader = new PdfNavigationReader(graph);
            var definition = action.Materialize(reader, actionPolicy ?? new());
            var binding = graph.Snapshot.Pages[pageIndex].Source!;
            var declared = reader.Get(binding.Dictionary, "Annots");
            if (declared.Kind != PdfValueKind.Null && declared.Kind != PdfValueKind.Array)
                throw graph.Error("Navigation.Annotations", "Annots must resolve to an array.");
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, (declared.Kind == PdfValueKind.Array ? declared.Items.Count * 24L : 0) + 1024);
            var annots = declared.Kind == PdfValueKind.Array ? declared.Items.ToList() : new List<PdfValue>();
            annots.Add(PdfValue.Reference(graph.Edit.Add(PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Annot")), ("Subtype", PdfValue.Name("Link")), ("P", reader.Pages[pageIndex]), ("Rect", bounds.Value), ("Border", PdfGraphicsNumbers.Array(0, 0, 0)), ("A", definition)))));
            PdfAnnotationSet.SetPageAnnotations(graph, pageIndex, annots);
            PdfNavigationReader.ValidateTargets(graph.Edit, graph.Edit.Trailer.Entries["Root"], reader.Pages, graph.Context);
        }, "1.7");
    }

    /// <summary>Sets or removes a catalog OpenAction or additional action (WC, WS, DS, WP, DP).</summary>
    public static PdfDocument SetDocumentAction(PdfDocumentSnapshot source, string trigger, PdfAction? action, PdfActionPolicy? actionPolicy = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (!new[]
        {
            "OpenAction",
            "WC",
            "WS",
            "DS",
            "WP",
            "DP"
        }.Contains(trigger))
            throw new ArgumentException("A standard document action trigger is required.", nameof(trigger));
        return SetAction(source, null, trigger, action, actionPolicy, options, policy, cancellationToken);
    }

    /// <summary>Sets or removes a page O (open) or C (close) additional action.</summary>
    public static PdfDocument SetPageAction(PdfDocumentSnapshot source, int pageIndex, string trigger, PdfAction? action, PdfActionPolicy? actionPolicy = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (pageIndex < 0 || pageIndex >= source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (trigger != "O" && trigger != "C")
            throw new ArgumentException("A standard page action trigger is required.", nameof(trigger));
        return SetAction(source, pageIndex, trigger, action, actionPolicy, options, policy, cancellationToken);
    }

    private static PdfDocument SetAction(PdfDocumentSnapshot source, int? page, string trigger, PdfAction? action, PdfActionPolicy? actionPolicy, PdfOrganizationOptions? options, PdfOperationPolicy? policy, CancellationToken token) => PdfGraphEdit.Run("SetNavigationAction", source, options, policy, token, graph =>
    {
        var reader = new PdfNavigationReader(graph);
        var owner = page.HasValue ? graph.Snapshot.Pages[page.Value].Source!.Dictionary : graph.Catalog;
        var values = graph.Copy(owner);
        var definition = action?.Materialize(reader, actionPolicy ?? new());
        if (trigger == "OpenAction")
        {
            if (definition == null)
                values.Remove(trigger);
            else
                values[trigger] = definition;
        }
        else
        {
            var additional = graph.Copy(reader.Get(owner, "AA"));
            if (definition == null)
                additional.Remove(trigger);
            else
                additional[trigger] = definition;
            if (additional.Count == 0)
                values.Remove("AA");
            else
                values["AA"] = PdfValue.OwnedDictionary(additional);
        }

        if (page.HasValue)
            graph.Edit.Set(reader.Pages[page.Value].ReferenceId, PdfValue.OwnedDictionary(values));
        else
            graph.SetCatalog(values);
        PdfNavigationReader.ValidateTargets(graph.Edit, graph.Edit.Trailer.Entries["Root"], reader.Pages, graph.Context);
    }, "1.7");
    /// <summary>Explicitly removes policy-blocked actions and opaque active content from all active objects. This full rewrite is not secure erasure.</summary>
    public static PdfDocument Sanitize(PdfDocumentSnapshot source, PdfActionPolicy? actionPolicy = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfGraphEdit.Run("SanitizeActions", source, options, policy, cancellationToken, graph =>
    {
        var reader = new PdfNavigationReader(graph);
        var selected = actionPolicy ?? new();
        // Inventory first: malformed/cyclic action graphs fail explicitly rather than bypassing validation.
        var removed = PdfActionInventory.Read(reader, selected).Count(r => !r.IsAllowed);
        PdfActionInventory.Sanitize(reader, selected);
        if (removed > 0)
            graph.Context.Report(new PdfDiagnostic("Navigation.ActionsRemoved", "Policy-blocked actions or active-content locations were explicitly removed.", severity: PdfDiagnosticSeverity.Information));
        if (PdfActionInventory.Read(new(graph), selected).Any(r => !r.IsAllowed))
            throw graph.Error("Navigation.SanitizationIncomplete", "A blocked action remains after sanitization.");
    }, "1.7");
}
