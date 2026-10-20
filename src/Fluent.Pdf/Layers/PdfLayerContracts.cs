namespace Fluent.Pdf;
/// <summary>The explicit optional-content usage event.</summary>
public enum PdfLayerUsage
{
    /// <summary>Screen viewing.</summary>
    View,
    /// <summary>A viewer print operation after applying initial viewing usage.</summary>
    Print,
    /// <summary>A viewer export operation after applying initial viewing usage.</summary>
    Export,
    /// <summary>The configured states without usage applications, as used by a stand-alone printing/aggregation application.</summary>
    Configured
}

/// <summary>An immutable registered optional-content group and its passive source dictionary.</summary>
public sealed class PdfLayerGroup
{
    internal PdfLayerGroup(PdfDocumentSnapshot source, PdfObjectId id, string name, PdfValue definition)
    {
        Source = source;
        Id = id;
        Name = name;
        Definition = definition;
    }

    /// <summary>Gets the captured source.</summary>
    public PdfDocumentSnapshot Source { get; }
    /// <summary>Gets the exact indirect identity used in membership/configuration dictionaries.</summary>
    public PdfObjectId Id { get; }
    /// <summary>Gets the Unicode layer name.</summary>
    public string Name { get; }
    /// <summary>Gets raw intent, usage and custom values without evaluating external context.</summary>
    public PdfValue Definition { get; }
}

/// <summary>A detached layer/configuration inventory.</summary>
public sealed class PdfLayerSet
{
    internal PdfLayerSet(PdfDocumentSnapshot source, List<PdfLayerGroup> groups, PdfValue properties, List<PdfValue> configurations, PdfOperationReport report)
    {
        Source = source;
        Groups = groups.AsReadOnly();
        Properties = properties;
        Configurations = configurations.AsReadOnly();
        OperationReport = report;
    }

    /// <summary>Gets the captured source.</summary>
    public PdfDocumentSnapshot Source { get; }
    /// <summary>Gets registered OCG identities, retaining duplicate display names.</summary>
    public IReadOnlyList<PdfLayerGroup> Groups { get; }
    /// <summary>Gets the raw resolved OCProperties dictionary, or null.</summary>
    public PdfValue Properties { get; }
    /// <summary>Gets resolved alternate configurations in source order.</summary>
    public IReadOnlyList<PdfValue> Configurations { get; }
    /// <summary>Gets inventory diagnostics and resource usage.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>An explicit visibility decision and its operation report.</summary>
public sealed class PdfLayerVisibility
{
    internal PdfLayerVisibility(bool visible, PdfOperationReport report)
    {
        Visible = visible;
        OperationReport = report;
    }

    /// <summary>Gets whether the membership is visible for the selected configuration, intents and usage event.</summary>
    public bool Visible { get; }
    /// <summary>Gets diagnostics and resource usage.</summary>
    public PdfOperationReport OperationReport { get; }
}
