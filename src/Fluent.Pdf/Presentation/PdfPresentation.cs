using System.Collections.ObjectModel;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>One page's passive transition and display-duration settings.</summary>
public sealed class PdfPagePresentationRecord
{
    internal PdfPagePresentationRecord(int index, PdfValue transition, PdfValue duration)
    {
        PageIndex = index;
        Transition = transition;
        DisplayDuration = duration;
    }

    /// <summary>Gets the physical input page.</summary>
    public int PageIndex { get; }
    /// <summary>Gets the preserved transition dictionary or null.</summary>
    public PdfValue Transition { get; }
    /// <summary>Gets the preserved Dur value or null.</summary>
    public PdfValue DisplayDuration { get; }
}

/// <summary>A passive presentation inventory retaining unsupported settings and diagnostics.</summary>
public sealed class PdfPresentationSet
{
    internal PdfPresentationSet(PdfDocumentSnapshot source, IReadOnlyList<PdfOutlineRecord> outlines, SortedDictionary<int, PdfValue> labels, PdfPageNumbering? numbering, PdfValue catalog, IReadOnlyList<PdfPagePresentationRecord> pages, PdfOperationReport report)
    {
        Source = source;
        Outlines = outlines;
        PageLabels = new ReadOnlyDictionary<int, PdfValue>(labels);
        Numbering = numbering;
        Catalog = catalog;
        Pages = pages;
        OperationReport = report;
    }

    /// <summary>Gets the detached input snapshot.</summary>
    public PdfDocumentSnapshot Source { get; }
    /// <summary>Gets the ordered nested outlines.</summary>
    public IReadOnlyList<PdfOutlineRecord> Outlines { get; }
    /// <summary>Gets preserved page-label range dictionaries by physical start index.</summary>
    public IReadOnlyDictionary<int, PdfValue> PageLabels { get; }
    /// <summary>Gets a bounded standard label formatter, or null when raw ranges require another profile.</summary>
    public PdfPageNumbering? Numbering { get; }
    /// <summary>Gets preserved catalog presentation settings, including unknown preference values and initial actions.</summary>
    public PdfValue Catalog { get; }
    /// <summary>Gets page transition/display-duration records.</summary>
    public IReadOnlyList<PdfPagePresentationRecord> Pages { get; }
    /// <summary>Gets the inspection diagnostics and resource report.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>Managed outline, label, viewer-preference and presentation editing without executing actions.</summary>
public static class PdfPresentation
{
    /// <summary>Inspects source outlines, labels, catalog settings and page transitions; unsupported settings are retained with warnings.</summary>
    public static PdfPresentationSet Inspect(PdfDocumentSnapshot source, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        PdfDocumentSnapshot? captured = null;
        IReadOnlyList<PdfOutlineRecord>? outlines = null;
        SortedDictionary<int, PdfValue>? labels = null;
        PdfPageNumbering? numbering = null;
        PdfValue? catalog = null;
        var pages = new List<PdfPagePresentationRecord>();
        var report = PdfFont.Run("InspectPresentation", "managed.presentation", policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(source, new(), context);
            captured = graph.Snapshot;
            var reader = new PdfNavigationReader(graph);
            outlines = PdfOutlineGraph.Read(reader);
            labels = PdfPresentationValues.Labels(reader);
            numbering = PdfPresentationValues.Numbering(reader, labels);
            var settings = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
            foreach (string key in new[]
            {
                "PageLayout",
                "PageMode",
                "ViewerPreferences",
                "OpenAction",
                "URI",
                "Requirements",
                "Perms"
            }

            )
            {
                var value = reader.Get(reader.Catalog, key);
                if (value.Kind != PdfValueKind.Null)
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, 96);
                    settings.Add(key, value);
                }
            }

            catalog = PdfValue.OwnedDictionary(settings);
            PdfPresentationValues.Preferences(reader, reader.Get(reader.Catalog, "ViewerPreferences"), false);
            CheckName<PdfPageLayout>("PageLayout");
            CheckName<PdfPageMode>("PageMode");
            for (int i = 0; i < reader.Pages.Count; i++)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.ExpandedBytes, 128);
                var page = reader.Resolve(reader.Pages[i]);
                var transition = reader.Get(page, "Trans");
                var duration = reader.Get(page, "Dur");
                PdfPresentationValues.Transition(reader, transition);
                if (duration.Kind != PdfValueKind.Null && !ValidDuration(duration))
                    PdfPresentationValues.Warning(reader, "Presentation.UnsupportedDuration", "The source display duration is preserved for explicit inspection.");
                pages.Add(new(i, transition, duration));
            }

            static bool ValidDuration(PdfValue value)
            {
                if (value.Kind != PdfValueKind.Number)
                    return false;
                try
                {
                    return value.NumberValue >= 0 && value.NumberValue <= 86400;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }

            void CheckName<T>(string key)
                where T : struct
            {
                var value = reader.Get(reader.Catalog, key);
                if (value.Kind != PdfValueKind.Null && (value.Kind != PdfValueKind.Name || !Enum.TryParse<T>(value.NameValue, out var parsed) || !Enum.IsDefined(typeof(T), parsed)))
                    PdfPresentationValues.Warning(reader, "Presentation.Unsupported" + key, "An unsupported catalog presentation setting is preserved for explicit inspection.");
            }
        }, PdfCapability.ReadDocument);
        return new(captured!, outlines!, labels!, numbering, catalog!, pages.AsReadOnly(), report);
    }

    /// <summary>Replaces the outline tree with a finite ordered batch. An empty batch removes outlines.</summary>
    public static PdfDocument SetOutlines(PdfDocumentSnapshot source, IEnumerable<PdfBookmark> bookmarks, PdfActionPolicy? actionPolicy = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (bookmarks == null)
            throw new ArgumentNullException(nameof(bookmarks));
        return PdfGraphEdit.Run("SetOutlines", source, options, policy, cancellationToken, graph => PdfOutlineGraph.Write(graph, bookmarks, actionPolicy ?? new()), "1.7");
    }

    /// <summary>Sets or clears standard physical-page label ranges. Every supplied range must start within this document.</summary>
    public static PdfDocument SetPageLabels(PdfDocumentSnapshot source, PdfPageNumbering? numbering, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfGraphEdit.Run("SetPageLabels", source, options, policy, cancellationToken, graph =>
    {
        var catalog = graph.Copy(graph.Catalog);
        if (numbering == null)
            catalog.Remove("PageLabels");
        else
        {
            var values = new List<PdfValue>();
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, numbering.Ranges.Count * 512L + 128);
            foreach (var range in numbering.Ranges)
            {
                graph.Context.Checkpoint();
                if (range.StartPageIndex >= graph.Snapshot.Pages.Count)
                    throw graph.Error("Presentation.PageLabelRange", "Every label range must begin on an existing physical page.");
                var entries = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
                if (range.Style != PdfPageLabelStyle.None)
                {
                    entries["S"] = PdfValue.Name(range.Style switch
                    {
                        PdfPageLabelStyle.Decimal => "D",
                        PdfPageLabelStyle.LowerRoman => "r",
                        PdfPageLabelStyle.UpperRoman => "R",
                        PdfPageLabelStyle.LowerLetters => "a",
                        _ => "A"
                    });
                    entries["St"] = PdfValue.Integer(range.FirstNumber);
                }

                if (range.Prefix.Length > 0)
                    entries["P"] = PdfTextStrings.Encode(range.Prefix, graph.Context);
                values.Add(PdfValue.Integer(range.StartPageIndex));
                values.Add(PdfValue.OwnedDictionary(entries));
            }

            catalog["PageLabels"] = PdfValue.Reference(graph.Edit.Add(PdfGraphicsNumbers.Dict(("Nums", PdfValue.OwnedArray(values)))));
        }

        graph.SetCatalog(catalog);
    }, "1.7");
    /// <summary>Sets catalog layout and mode. Null values retain the corresponding source setting.</summary>
    public static PdfDocument SetView(PdfDocumentSnapshot source, PdfPageLayout? layout = null, PdfPageMode? mode = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (layout.HasValue && !Enum.IsDefined(typeof(PdfPageLayout), layout.Value))
            throw new ArgumentOutOfRangeException(nameof(layout));
        if (mode.HasValue && !Enum.IsDefined(typeof(PdfPageMode), mode.Value))
            throw new ArgumentOutOfRangeException(nameof(mode));
        return PdfGraphEdit.Run("SetViewerMode", source, options, policy, cancellationToken, graph =>
        {
            var values = graph.Copy(graph.Catalog);
            if (layout.HasValue)
                values["PageLayout"] = PdfValue.Name(layout.Value.ToString());
            if (mode.HasValue)
                values["PageMode"] = PdfValue.Name(mode.Value.ToString());
            graph.SetCatalog(values);
        }, "1.7");
    }

    /// <summary>Sets or clears a standard catalog OpenAction destination array. No action runs.</summary>
    public static PdfDocument SetInitialView(PdfDocumentSnapshot source, PdfDestination? destination, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfGraphEdit.Run("SetInitialView", source, options, policy, cancellationToken, graph =>
    {
        var reader = new PdfNavigationReader(graph);
        var values = graph.Copy(graph.Catalog);
        if (destination == null)
            values.Remove("OpenAction");
        else
            values["OpenAction"] = destination.Materialize(reader.Pages, graph.Context);
        graph.SetCatalog(values);
    }, "1.7");
    /// <summary>Patches standard ViewerPreferences, or replaces the dictionary explicitly. Unknown supplied keys/values fail; preserved source keys remain inspectable.</summary>
    public static PdfDocument SetViewerPreferences(PdfDocumentSnapshot source, PdfValue preferences, bool replace = false, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (preferences == null)
            throw new ArgumentNullException(nameof(preferences));
        return PdfGraphEdit.Run("SetViewerPreferences", source, options, policy, cancellationToken, graph =>
        {
            PdfGraphImport.ValidateDetached(preferences, graph.Context);
            var reader = new PdfNavigationReader(graph);
            PdfPresentationValues.Preferences(reader, preferences, true);
            var values = replace ? new Dictionary<string, PdfValue>(StringComparer.Ordinal) : graph.Copy(reader.Get(graph.Catalog, "ViewerPreferences"));
            foreach (var pair in preferences.Entries)
                values[pair.Key] = pair.Value;
            var merged = PdfValue.OwnedDictionary(values);
            PdfPresentationValues.Preferences(reader, merged, false);
            var catalog = graph.Copy(graph.Catalog);
            if (values.Count == 0)
                catalog.Remove("ViewerPreferences");
            else
                catalog["ViewerPreferences"] = merged;
            if (values.ContainsKey("Enforce"))
                catalog["Version"] = PdfValue.Name("2.0");
            graph.SetCatalog(catalog);
        }, "1.7");
    }

    /// <summary>Sets or clears a page transition and Dur value. Null settings remove the respective source keys; durations are finite seconds in 0..86400.</summary>
    public static PdfDocument SetPageTransition(PdfDocumentSnapshot source, int pageIndex, PdfPageTransition? transition, double? displayDuration = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (pageIndex < 0 || pageIndex >= source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (displayDuration.HasValue && (double.IsNaN(displayDuration.Value) || double.IsInfinity(displayDuration.Value) || displayDuration < 0 || displayDuration > 86400))
            throw new ArgumentOutOfRangeException(nameof(displayDuration));
        return PdfGraphEdit.Run("SetPageTransition", source, options, policy, cancellationToken, graph =>
        {
            var binding = graph.Snapshot.Pages[pageIndex].Source!;
            var values = graph.Copy(binding.Dictionary);
            if (transition == null)
                values.Remove("Trans");
            else
                values["Trans"] = transition.Value();
            if (displayDuration.HasValue)
                values["Dur"] = PdfValues.Real(displayDuration.Value);
            else
                values.Remove("Dur");
            graph.Edit.Set(binding.ObjectId, PdfValue.OwnedDictionary(values));
        }, "1.7");
    }
}
