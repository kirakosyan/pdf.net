namespace Fluent.Pdf;
/// <summary>The standard AcroForm terminal field kind.</summary>
public enum PdfFormFieldKind
{
    /// <summary>An inspectable unsupported source field type.</summary>
    Unknown,
    /// <summary>A text field.</summary>
    Text,
    /// <summary>A checkbox button.</summary>
    CheckBox,
    /// <summary>A radio button group.</summary>
    RadioButton,
    /// <summary>A combo choice field.</summary>
    ComboBox,
    /// <summary>A list choice field.</summary>
    ListBox,
    /// <summary>A push button.</summary>
    PushButton,
    /// <summary>A signature field; creating an unsigned field does not sign a document.</summary>
    Signature
}

/// <summary>Common and kind-specific standard field behavior flags. Kind discriminator bits are derived from the field kind.</summary>
[Flags]
public enum PdfFormFieldFlags : uint
{
    /// <summary>No optional flags.</summary>
    None = 0,
    /// <summary>Read-only interaction.</summary>
    ReadOnly = 1,
    /// <summary>A value is required for submission.</summary>
    Required = 2,
    /// <summary>Exclude the value from form-data export.</summary>
    NoExport = 4,
    /// <summary>Multiline text.</summary>
    Multiline = 4096,
    /// <summary>Password presentation; this flag does not encrypt the stored value.</summary>
    Password = 8192,
    /// <summary>A selected radio option cannot be toggled off interactively.</summary>
    NoToggleToOff = 16384,
    /// <summary>An editable combo choice.</summary>
    Edit = 262144,
    /// <summary>The choice options are sorted for presentation.</summary>
    Sort = 524288,
    /// <summary>Text represents a file selection.</summary>
    FileSelect = 1048576,
    /// <summary>Multiple list selections.</summary>
    MultiSelect = 2097152,
    /// <summary>Disable viewer spell checking.</summary>
    DoNotSpellCheck = 4194304,
    /// <summary>Disable viewer text scrolling.</summary>
    DoNotScroll = 8388608,
    /// <summary>Distribute text into MaxLen character cells.</summary>
    Comb = 16777216,
    /// <summary>Radio widgets with equal export names are selected together.</summary>
    RadiosInUnison = 33554432,
    /// <summary>Commit a choice immediately when selection changes.</summary>
    CommitOnSelectionChange = 67108864
}

/// <summary>One immutable choice export/display pair.</summary>
public sealed class PdfFormChoice
{
    /// <summary>Captures bounded Unicode export and display text; an omitted display uses the export value.</summary>
    public PdfFormChoice(string exportValue, string? displayValue = null)
    {
        if (exportValue == null)
            throw new ArgumentNullException(nameof(exportValue));
        if (exportValue.Length > 1000000 || displayValue?.Length > 1000000)
            throw new ArgumentOutOfRangeException(nameof(exportValue));
        ExportValue = exportValue;
        DisplayValue = displayValue ?? exportValue;
    }

    /// <summary>Gets the exported value.</summary>
    public string ExportValue { get; }
    /// <summary>Gets the viewer display value.</summary>
    public string DisplayValue { get; }
}

/// <summary>An authored widget with caller-selected appearance data and explicit page ownership.</summary>
public sealed class PdfFormWidgetDefinition
{
    /// <summary>Captures native page bounds and a normal Form appearance. Button-state widgets also require an Off Form and non-Off export name.</summary>
    public PdfFormWidgetDefinition(int pageIndex, PdfRect bounds, PdfGraphicsResource normalAppearance, string? exportValue = null, PdfGraphicsResource? offAppearance = null, int annotationFlags = 4)
    {
        if (pageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentException("Positive widget bounds are required.", nameof(bounds));
        if (annotationFlags < 0 || (annotationFlags & ~1023) != 0)
            throw new ArgumentOutOfRangeException(nameof(annotationFlags));
        PageIndex = pageIndex;
        Bounds = bounds;
        NormalAppearance = normalAppearance ?? throw new ArgumentNullException(nameof(normalAppearance));
        if (exportValue != null)
        {
            PdfMetadataValues.Key(exportValue);
            if (exportValue == "Off")
                throw new ArgumentException("The on-state name must differ from Off.", nameof(exportValue));
        }

        ExportValue = exportValue;
        OffAppearance = offAppearance;
        AnnotationFlags = annotationFlags;
    }

    /// <summary>Gets the zero-based physical page index.</summary>
    public int PageIndex { get; }
    /// <summary>Gets native unrotated widget bounds.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets the normal/on-state private Form appearance.</summary>
    public PdfGraphicsResource NormalAppearance { get; }
    /// <summary>Gets a button's on-state PDF name, or null for a non-state widget.</summary>
    public string? ExportValue { get; }
    /// <summary>Gets the required Off appearance for a state button.</summary>
    public PdfGraphicsResource? OffAppearance { get; }
    /// <summary>Gets standard annotation flags; Print is the default.</summary>
    public int AnnotationFlags { get; }
}

/// <summary>An immutable terminal field definition. Partial names are separated by periods in Name.</summary>
public sealed class PdfFormFieldDefinition
{
    /// <summary>Captures explicit values, widget appearances and optional edit font. No field action or calculation is executed.</summary>
    public PdfFormFieldDefinition(string name, PdfFormFieldKind kind, PdfFormWidgetDefinition[] widgets, PdfValue? value = null, PdfValue? defaultValue = null, PdfFormFieldFlags flags = PdfFormFieldFlags.None, PdfFormChoice[]? choices = null, int? maximumLength = null, string? alternateName = null, string? mappingName = null, PdfFontResource? editingFont = null, double fontSize = 12)
    {
        Name = Guard.Text(name, nameof(name), 8192);
        if (name.Split('.').Any(x => x.Length == 0))
            throw new ArgumentException("Every hierarchical partial name must be nonempty.", nameof(name));
        if (!Enum.IsDefined(typeof(PdfFormFieldKind), kind) || kind == PdfFormFieldKind.Unknown)
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (widgets == null)
            throw new ArgumentNullException(nameof(widgets));
        if (widgets.Length == 0 || widgets.Length > 4096 || widgets.Any(x => x == null))
            throw new ArgumentException("One through 4096 widget definitions are required.", nameof(widgets));
        if (choices?.Length > 100000 || choices?.Any(x => x == null) == true)
            throw new ArgumentException("Choices must be non-null bounded values.", nameof(choices));
        if (maximumLength <= 0 || maximumLength > 1000000)
            throw new ArgumentOutOfRangeException(nameof(maximumLength));
        if (alternateName?.Length > 1000000 || mappingName?.Length > 1000000)
            throw new ArgumentOutOfRangeException(nameof(alternateName));
        PdfGraphicsNumbers.Finite(fontSize);
        if (fontSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        Kind = kind;
        Widgets = Array.AsReadOnly((PdfFormWidgetDefinition[])widgets.Clone());
        Value = value ?? PdfValue.Null;
        DefaultValue = defaultValue ?? PdfValue.Null;
        Flags = flags;
        Choices = Array.AsReadOnly(choices == null ? Array.Empty<PdfFormChoice>() : (PdfFormChoice[])choices.Clone());
        MaximumLength = maximumLength;
        AlternateName = alternateName;
        MappingName = mappingName;
        EditingFont = editingFont;
        FontSize = fontSize;
    }

    /// <summary>Gets the fully qualified hierarchical field name.</summary>
    public string Name { get; }
    /// <summary>Gets the terminal kind.</summary>
    public PdfFormFieldKind Kind { get; }
    /// <summary>Gets owned widget definitions.</summary>
    public IReadOnlyList<PdfFormWidgetDefinition> Widgets { get; }
    /// <summary>Gets explicit current value data; PDF null means absent.</summary>
    public PdfValue Value { get; }
    /// <summary>Gets explicit reset value data; PDF null means absent.</summary>
    public PdfValue DefaultValue { get; }
    /// <summary>Gets requested behavior flags.</summary>
    public PdfFormFieldFlags Flags { get; }
    /// <summary>Gets ordered choice export/display pairs.</summary>
    public IReadOnlyList<PdfFormChoice> Choices { get; }
    /// <summary>Gets optional text MaxLen.</summary>
    public int? MaximumLength { get; }
    /// <summary>Gets the optional alternate/accessibility name.</summary>
    public string? AlternateName { get; }
    /// <summary>Gets the optional export mapping name.</summary>
    public string? MappingName { get; }
    /// <summary>Gets an explicit default editing font resource, independently of private appearance fonts.</summary>
    public PdfFontResource? EditingFont { get; }
    /// <summary>Gets the default appearance font size in native appearance units.</summary>
    public double FontSize { get; }
}

/// <summary>A detached passive widget record.</summary>
public sealed class PdfFormWidget
{
    internal PdfFormWidget(PdfObjectId id, int? page, PdfRect bounds, PdfValue definition)
    {
        Id = id;
        PageIndex = page;
        Bounds = bounds;
        Definition = definition;
    }

    /// <summary>Gets its indirect identity.</summary>
    public PdfObjectId Id { get; }
    /// <summary>Gets its attached page, or null for an unattached source widget.</summary>
    public int? PageIndex { get; }
    /// <summary>Gets native unrotated bounds.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets raw appearance/state, tab/structure and action metadata without executing it.</summary>
    public PdfValue Definition { get; }
}

/// <summary>A logical field with one or more valid same-name terminal representations.</summary>
public sealed class PdfFormField
{
    internal PdfFormField(PdfDocumentSnapshot source, string name, PdfFormFieldKind kind, uint flags, PdfValue value, PdfValue reset, List<PdfObjectId> ids, List<PdfFormWidget> widgets, PdfValue inherited)
    {
        Source = source;
        Name = name;
        Kind = kind;
        Flags = flags;
        Value = value;
        DefaultValue = reset;
        Representations = ids.AsReadOnly();
        Widgets = widgets.AsReadOnly();
        Inherited = inherited;
    }

    /// <summary>Gets the captured immutable source.</summary>
    public PdfDocumentSnapshot Source { get; }
    /// <summary>Gets the fully qualified name.</summary>
    public string Name { get; }
    /// <summary>Gets the interpreted standard kind, or Unknown.</summary>
    public PdfFormFieldKind Kind { get; }
    /// <summary>Gets all raw flag bits, retaining unsupported source flags.</summary>
    public uint Flags { get; }
    /// <summary>Gets the resolved current inherited value.</summary>
    public PdfValue Value { get; }
    /// <summary>Gets the resolved inherited reset value.</summary>
    public PdfValue DefaultValue { get; }
    /// <summary>Gets exact terminal dictionary identities sharing this logical field.</summary>
    public IReadOnlyList<PdfObjectId> Representations { get; }
    /// <summary>Gets attached and unattached widgets in source tree order.</summary>
    public IReadOnlyList<PdfFormWidget> Widgets { get; }
    /// <summary>Gets captured inherited field/default-appearance properties for inspection.</summary>
    public PdfValue Inherited { get; }
}

/// <summary>A passive AcroForm inventory and operation report.</summary>
public sealed class PdfFormSet
{
    internal PdfFormSet(PdfDocumentSnapshot source, PdfValue dictionary, List<PdfFormField> fields, PdfOperationReport report)
    {
        Source = source;
        Dictionary = dictionary;
        Fields = fields.AsReadOnly();
        OperationReport = report;
    }

    /// <summary>Gets the captured source.</summary>
    public PdfDocumentSnapshot Source { get; }
    /// <summary>Gets the resolved raw form dictionary, or null.</summary>
    public PdfValue Dictionary { get; }
    /// <summary>Gets logical terminal fields, preserving consistent unnamed representations.</summary>
    public IReadOnlyList<PdfFormField> Fields { get; }
    /// <summary>Gets passive validation diagnostics and resource usage.</summary>
    public PdfOperationReport OperationReport { get; }
}
