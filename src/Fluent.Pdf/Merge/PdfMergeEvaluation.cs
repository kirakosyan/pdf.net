using System.Globalization;

namespace Fluent.Pdf;
/// <summary>Closed template evaluation policy, with explicit formatting culture, missing fields and finite visit/record ceilings.</summary>
public sealed class PdfMergeEvaluationOptions
{
    /// <summary>Captures a standard culture by name; empty selects invariant. No ambient culture, reflected property or expression evaluation is used.</summary>
    public PdfMergeEvaluationOptions(string cultureName, PdfMergeMissingField missingFields = PdfMergeMissingField.Error, int maximumVisits = 100000, int maximumRecords = 100000)
    {
        if (cultureName == null)
            throw new ArgumentNullException(nameof(cultureName));
        if (!Enum.IsDefined(typeof(PdfMergeMissingField), missingFields))
            throw new ArgumentOutOfRangeException(nameof(missingFields));
        if (maximumVisits < 1 || maximumVisits > 1000000 || maximumRecords < 1 || maximumRecords > 1000000)
            throw new ArgumentOutOfRangeException(nameof(maximumVisits));
        Culture = CultureInfo.ReadOnly(new CultureInfo(cultureName, false));
        MissingFields = missingFields;
        MaximumVisits = maximumVisits;
        MaximumRecords = maximumRecords;
    }

    /// <summary>Gets the captured immutable standard culture.</summary>
    public CultureInfo Culture { get; }
    /// <summary>Gets the explicit absent-field policy.</summary>
    public PdfMergeMissingField MissingFields { get; }
    /// <summary>Gets the total instruction/field visit ceiling.</summary>
    public int MaximumVisits { get; }
    /// <summary>Gets the total expanded record ceiling across all nested repetitions.</summary>
    public int MaximumRecords { get; }
}

internal sealed class PdfMergeEvaluation
{
    internal readonly PdfMergeRecord Root;
    internal readonly PdfMergeEvaluationOptions Options;
    internal readonly PdfOperationContext Context;
    private int _visits, _records;
    internal PdfMergeEvaluation(PdfMergeRecord root, PdfMergeEvaluationOptions options, PdfOperationContext context)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Context = context;
        PdfTableData.ValidateCulture(options.Culture, context);
    }

    internal void Visit(long bytes = 128)
    {
        Context.Checkpoint();
        if (_visits++ == Options.MaximumVisits)
            throw Error("VisitLimit", "The explicit template instruction/field visit ceiling was exceeded.");
        Context.Consume(PdfResourceKind.Objects, 1);
        Context.Consume(PdfResourceKind.ExpandedBytes, bytes);
    }

    internal void Record()
    {
        Visit();
        if (_records++ == Options.MaximumRecords)
            throw Error("RecordLimit", "The explicit total template record ceiling was exceeded.");
    }

    internal PdfMergeValue? Lookup(PdfMergeRecord current, string path, int index, bool scalar = false)
    {
        Visit(path.Length * 8L + 128);
        try
        {
            PdfMergeRecord.Path(path);
        }
        catch (ArgumentException)
        {
            throw Error("Path", "A template field path contains unsupported syntax.");
        }

        if (path == "@index")
        {
            if (index < 0)
                throw Error("IndexScope", "A repeat index is available only inside a selected record.");
            return PdfMergeValue.Scalar(index);
        }

        if (path.StartsWith("root.", StringComparison.Ordinal))
        {
            current = Root;
            path = path.Substring(5);
        }

        var names = path.Split('.');
        for (int i = 0; i < names.Length; i++)
        {
            if (!current.Fields.TryGetValue(names[i], out var value))
                return Missing(scalar);
            if (i + 1 == names.Length)
                return value;
            if (value.Kind != 1)
                throw Error("PathType", "An intermediate field path requires an explicitly captured record.");
            current = (PdfMergeRecord)value.Value!;
        }

        throw Error("Path", "A field path is empty.");
    }

    private PdfMergeValue? Missing(bool scalar)
    {
        if (Options.MissingFields == PdfMergeMissingField.Error)
            throw Error("MissingField", "A required template field is absent; field data is not echoed.");
        if (Options.MissingFields == PdfMergeMissingField.PreserveScalar && !scalar)
            throw Error("MissingStructuralField", "Preserving a missing scalar placeholder cannot resolve a structural/image field.");
        Context.Report(new("Merge." + (Options.MissingFields == PdfMergeMissingField.Empty ? "MissingEmpty" : "MissingPreserved"), "An absent field followed the explicit missing-data policy; field data is not echoed.", severity: PdfDiagnosticSeverity.Information));
        return null;
    }

    internal string Format(PdfMergeRecord current, string path, string? format, int index)
    {
        var value = Lookup(current, path, index, true);
        if (value == null)
            return Options.MissingFields == PdfMergeMissingField.PreserveScalar ? "{{" + path + (format == null ? "" : ":" + format) + "}}" : "";
        if (value.Kind != 0)
            throw Error("FieldType", "A scalar template field cannot format records, collections or images.");
        string result = PdfTableData.Format(value.Value, format, "", Options.Culture, Context);
        Context.Consume(PdfResourceKind.InputBytes, result.Length * 2L);
        Context.Consume(PdfResourceKind.ExpandedBytes, result.Length * 4L + 32);
        return result;
    }

    internal bool Condition(PdfMergeRecord current, string path, int index)
    {
        var value = Lookup(current, path, index);
        if (value == null)
            return false;
        if (value.Kind != 0 || value.Value is not bool flag)
            throw Error("ConditionType", "Template conditions require an explicit boolean scalar.");
        return flag;
    }

    internal IReadOnlyList<PdfMergeRecord> Records(PdfMergeRecord current, string path, int index)
    {
        var value = Lookup(current, path, index);
        if (value == null)
            return Array.Empty<PdfMergeRecord>();
        if (value.Kind != 2)
            throw Error("CollectionType", "Template repetitions require an explicit finite record collection.");
        return (IReadOnlyList<PdfMergeRecord>)value.Value!;
    }

    internal PdfMergeImage? Image(PdfMergeRecord current, string path, int index)
    {
        var value = Lookup(current, path, index);
        if (value == null)
            return null;
        if (value.Kind != 3)
            throw Error("ImageType", "Template images require explicitly captured image values.");
        return (PdfMergeImage)value.Value!;
    }

    internal PdfException Error(string code, string message) => PdfWriterCore.Error(Context, "Merge." + code, message);
}
