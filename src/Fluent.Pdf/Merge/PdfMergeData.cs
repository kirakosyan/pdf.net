using System.Collections.ObjectModel;
using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>A closed immutable merge value: scalar, record, finite record collection or explicitly captured PNG image. No reflected properties or expressions are evaluated.</summary>
public sealed class PdfMergeValue
{
    private PdfMergeValue(object? value, int kind)
    {
        Value = value;
        Kind = kind;
    }

    internal object? Value { get; }
    internal int Kind { get; }

    /// <summary>Captures a supported immutable scalar. Arbitrary objects, floating nonfinite numbers and custom formatting implementations are rejected.</summary>
    public static PdfMergeValue Scalar(object? value)
    {
        if (value != null && value is not string && value is not char && value is not bool && value is not byte && value is not sbyte && value is not short && value is not ushort && value is not int && value is not uint && value is not long && value is not ulong && value is not float && value is not double && value is not decimal && value is not DateTime && value is not DateTimeOffset && value is not TimeSpan && value is not Guid)
            throw new ArgumentException("Only closed immutable scalar values are supported.", nameof(value));
        if (value is string text && text.Length > 1000000 || value is double d && (double.IsNaN(d) || double.IsInfinity(d)) || value is float f && (float.IsNaN(f) || float.IsInfinity(f)))
            throw new ArgumentException("Scalar text/numbers must have finite bounded extents.", nameof(value));
        return new(value, 0);
    }

    /// <summary>Captures an immutable nested record.</summary>
    public static PdfMergeValue Record(PdfMergeRecord value) => new(value ?? throw new ArgumentNullException(nameof(value)), 1);
    /// <summary>Captures at most100000immutable records; no enumeration or implicit conversion is performed.</summary>
    public static PdfMergeValue Records(params PdfMergeRecord[] values)
    {
        if (values == null || values.Length > 100000 || values.Any(v => v == null))
            throw new ArgumentException("A bounded nonnull record array is required.", nameof(values));
        return new(Array.AsReadOnly((PdfMergeRecord[])values.Clone()), 2);
    }

    /// <summary>Captures an explicitly loaded immutable image and physical geometry.</summary>
    public static PdfMergeValue Image(PdfMergeImage value) => new(value ?? throw new ArgumentNullException(nameof(value)), 3);
}

/// <summary>An ordinal immutable merge field collection. Nested values are already immutable and contain no callbacks.</summary>
public sealed class PdfMergeRecord
{
    /// <summary>Captures at most4096nonnull named fields. Names are closed identifiers; no object reflection or dictionary mutation occurs during evaluation.</summary>
    public PdfMergeRecord(IReadOnlyDictionary<string, PdfMergeValue> fields)
    {
        if (fields == null)
            throw new ArgumentNullException(nameof(fields));
        if (fields.Count > 4096)
            throw new ArgumentException("At most4096fields are accepted.", nameof(fields));
        var copy = new Dictionary<string, PdfMergeValue>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (!Identifier(field.Key))
                throw new ArgumentException("Merge field names must be bounded identifiers.", nameof(fields));
            copy.Add(field.Key, field.Value ?? throw new ArgumentException("Merge field values must be nonnull.", nameof(fields)));
        }

        Fields = new ReadOnlyDictionary<string, PdfMergeValue>(copy);
    }

    /// <summary>Gets immutable ordinal fields.</summary>
    public IReadOnlyDictionary<string, PdfMergeValue> Fields { get; }

    internal static bool Identifier(string value) => !string.IsNullOrEmpty(value) && value.Length <= 128 && (value[0] == '_' || char.IsLetter(value[0])) && value.All(ch => ch == '_' || char.IsLetterOrDigit(ch));
    internal static void Path(string value)
    {
        if (value == "@index")
            return;
        if (value == null || value.Length > 1024 || !value.Split('.').All(Identifier))
            throw new ArgumentException("A field path must contain only bounded identifiers separated by dots.", nameof(value));
    }
}

/// <summary>Explicit immutable PNG bytes and physical geometry for both native PDF and DOCX merge sources.</summary>
public sealed class PdfMergeImage
{
    private PdfMergeImage(PdfByteStore bytes, double width, double height)
    {
        Bytes = bytes;
        Width = width;
        Height = height;
    }

    internal PdfByteStore Bytes { get; }
    /// <summary>Gets physical width in points.</summary>
    public double Width { get; }
    /// <summary>Gets physical height in points.</summary>
    public double Height { get; }

    /// <summary>Captures and validates at most16MiB PNG bytes at the borrowed current position. No file, resource or codec is discovered; the input remains open.</summary>
    public static PdfMergeImage ReadPng(Stream input, double width, double height, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("A readable borrowed PNG source is required.", nameof(input));
        PdfGraphicsNumbers.Finite(width);
        PdfGraphicsNumbers.Finite(height);
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        PdfMergeImage? result = null;
        PdfFont.Run("CaptureMergeImage", "managed.merge", policy, cancellationToken, c =>
        {
            var bytes = PdfWordConversion.Capture(input, 16777216, c);
            byte[] signature =
            {
                137,
                80,
                78,
                71,
                13,
                10,
                26,
                10
            };
            if (bytes.Length < 8 || signature.Where((v, i) => bytes.At(i) != v).Any())
                throw PdfWriterCore.Error(c, "Merge.ImageFormat", "An explicit PNG source is required.");
            _ = PdfImagePng.Read(bytes, new(), c);
            result = new(bytes, width, height);
        }, PdfCapability.WriteDocument);
        return result!;
    }
}

/// <summary>Explicit handling of an absent field; a present null scalar is distinct.</summary>
public enum PdfMergeMissingField
{
    /// <summary>Fail with a field diagnostic before publication.</summary>
    Error,
    /// <summary>Use empty text/collections and record an informational policy diagnostic.</summary>
    Empty,
    /// <summary>Retain scalar placeholders with an informational diagnostic; absent structural/image fields still reject.</summary>
    PreserveScalar
}
