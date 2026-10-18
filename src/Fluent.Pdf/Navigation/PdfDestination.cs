namespace Fluent.Pdf;
/// <summary>Standard local destination view modes, in native unrotated page coordinates.</summary>
public enum PdfDestinationView
{
    /// <summary>Left, top and zoom; null retains the viewer's current value.</summary>
    XYZ,
    /// <summary>Fit the complete page.</summary>
    Fit,
    /// <summary>Fit width at an optional top coordinate.</summary>
    FitH,
    /// <summary>Fit height at an optional left coordinate.</summary>
    FitV,
    /// <summary>Fit left, bottom, right and top coordinates.</summary>
    FitR,
    /// <summary>Fit the content bounding box.</summary>
    FitB,
    /// <summary>Fit content width at an optional top coordinate.</summary>
    FitBH,
    /// <summary>Fit content height at an optional left coordinate.</summary>
    FitBV
}

/// <summary>An immutable destination captured against a physical page when applied to a snapshot.</summary>
public sealed class PdfDestination
{
    /// <summary>Creates a standard destination. XYZ takes three values, FitR four nonnull values, H/V modes one, and Fit/FitB none.</summary>
    public PdfDestination(int pageIndex, PdfDestinationView view = PdfDestinationView.Fit, params double? [] coordinates)
    {
        if (pageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (!Enum.IsDefined(typeof(PdfDestinationView), view))
            throw new ArgumentOutOfRangeException(nameof(view));
        int count = view == PdfDestinationView.XYZ ? 3 : view == PdfDestinationView.FitR ? 4 : view == PdfDestinationView.Fit || view == PdfDestinationView.FitB ? 0 : 1;
        if (coordinates == null || coordinates.Length != count || coordinates.Any(n => n.HasValue && (double.IsNaN(n.Value) || double.IsInfinity(n.Value))))
            throw new ArgumentException("Supply the finite coordinates required by the view.", nameof(coordinates));
        if (view == PdfDestinationView.FitR && (coordinates.Any(n => !n.HasValue) || coordinates[2] <= coordinates[0] || coordinates[3] <= coordinates[1]))
            throw new ArgumentException("FitR requires a positive rectangle.", nameof(coordinates));
        if (view == PdfDestinationView.XYZ && coordinates[2] < 0)
            throw new ArgumentOutOfRangeException(nameof(coordinates));
        PageIndex = pageIndex;
        View = view;
        Coordinates = System.Array.AsReadOnly((double? [])coordinates.Clone());
    }

    /// <summary>Gets the physical page index in the captured input.</summary>
    public int PageIndex { get; }
    /// <summary>Gets the standard destination view.</summary>
    public PdfDestinationView View { get; }
    /// <summary>Gets the immutable native coordinates.</summary>
    public IReadOnlyList<double?> Coordinates { get; }

    internal PdfValue Materialize(IReadOnlyList<PdfValue> pages, PdfOperationContext context)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.ExpandedBytes, 256);
        if (PageIndex >= pages.Count)
            throw PdfWriterCore.Error(context, "Navigation.TargetPage", "The destination page is absent from the captured document.");
        var values = new List<PdfValue>
        {
            pages[PageIndex],
            PdfValue.Name(View.ToString())
        };
        values.AddRange(Coordinates.Select(n => n.HasValue ? PdfValues.Real(n.Value) : PdfValue.Null));
        return PdfValue.OwnedArray(values);
    }
}
