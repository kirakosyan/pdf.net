using System.Collections.ObjectModel;
using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit field-value interchange formats. XML/JSON use the documented version-one SDK data schema.</summary>
public enum PdfFormDataFormat
{
    /// <summary>FDF field-value dictionaries; actions, appearances and other import instructions require a separate profile.</summary>
    Fdf,
    /// <summary>Standard XFDF field/value elements; field kind is supplied by the target form.</summary>
    Xfdf,
    /// <summary>Version-one typed XML field values.</summary>
    Xml,
    /// <summary>Version-one typed JSON field values.</summary>
    Json
}

/// <summary>One exact qualified field name and detached current value.</summary>
public sealed class PdfFormDataField
{
    /// <summary>Captures a name and PDF null, name, text string or array of text strings. Complete Unicode/value validation occurs under the data operation budget.</summary>
    public PdfFormDataField(string name, PdfValue value)
    {
        Name = Guard.Text(name, nameof(name), 8192);
        if (name.Split('.').Any(x => x.Length == 0))
            throw new ArgumentException("Qualified names require nonempty segments.", nameof(name));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets the case-sensitive qualified name.</summary>
    public string Name { get; }
    /// <summary>Gets detached value data. Name values retain exact case-sensitive button export bytes.</summary>
    public PdfValue Value { get; }
}

/// <summary>Immutable bounded field-value data. Parsing never imports actions, opens file references or executes scripts.</summary>
public sealed class PdfFormData
{
    private PdfFormData(List<PdfFormDataField> fields) => Fields = fields.AsReadOnly();
    /// <summary>Gets fields in captured order.</summary>
    public IReadOnlyList<PdfFormDataField> Fields { get; }
    /// <summary>Gets the successful capture/parse report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;

    /// <summary>Captures distinct, case-sensitive field names and validates supported value shapes.</summary>
    public static PdfFormData Create(IEnumerable<PdfFormDataField> fields, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (fields == null)
            throw new ArgumentNullException(nameof(fields));
        PdfFormData? result = null;
        var report = PdfFont.Run("CaptureFormData", "managed.forms", policy, cancellationToken, c => result = Capture(fields, c), PdfCapability.ReadDocument);
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Reads the selected format from the current position without closing the input. All interchange profiles have a sixteen MiB extent ceiling.</summary>
    public static PdfFormData Read(Stream input, PdfFormDataFormat format, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("A readable borrowed stream is required.", nameof(input));
        Check(format);
        PdfFormData? result = null;
        var report = PdfFont.Run("ReadFormData", "managed.forms", policy, cancellationToken, context =>
        {
            using var lease = new BorrowedStream(input, true, context);
            using var bounded = new PdfFormDataStream(lease, context, true);
            var bytes = PdfByteStore.Read(bounded, context);
            context.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * (format == PdfFormDataFormat.Xml || format == PdfFormDataFormat.Xfdf ? 32L : 8L) + 8192);
            try
            {
                var fields = format == PdfFormDataFormat.Fdf ? PdfFormFdf.Read(bytes, context) : format == PdfFormDataFormat.Json ? PdfFormJson.Read(bytes, context) : PdfFormXml.Read(bytes, format == PdfFormDataFormat.Xfdf, context);
                result = Capture(fields, context);
            }
            catch (ArgumentException)
            {
                throw Error(context, "Schema", "The external field data has an invalid name or value shape.");
            }
        }, PdfCapability.ReadDocument);
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Writes field values only, leaving output open. FDF retains PDF value kinds; XFDF carries strings and target field types restore button names.</summary>
    public PdfOperationReport Write(Stream output, PdfFormDataFormat format, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed stream is required.", nameof(output));
        Check(format);
        return PdfFont.Run("WriteFormData", "managed.forms", policy, cancellationToken, context =>
        {
            var captured = Capture(Fields, context);
            using var lease = new BorrowedStream(output, false, context);
            using var bounded = new PdfFormDataStream(lease, context, false);
            if (format == PdfFormDataFormat.Fdf)
                PdfFormFdf.Write(captured.Fields, bounded, context);
            else if (format == PdfFormDataFormat.Json)
                PdfFormJson.Write(captured.Fields, bounded, context);
            else
                PdfFormXml.Write(captured.Fields, bounded, format == PdfFormDataFormat.Xfdf, context);
        }, PdfCapability.WriteDocument);
    }

    internal static PdfFormData Capture(IEnumerable<PdfFormDataField> fields, PdfOperationContext context)
    {
        var captured = new List<PdfFormDataField>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        using var enumerator = fields.GetEnumerator();
        while (true)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            if (!enumerator.MoveNext())
                break;
            context.Consume(PdfResourceKind.ExpandedBytes, 192);
            if (captured.Count >= 100000)
                throw Error(context, "Count", "The interchange profile permits at most one hundred thousand fields.");
            var field = enumerator.Current ?? throw new ArgumentException("Fields cannot be null.", nameof(fields));
            PdfTextStrings.Encode(field.Name, context);
            if (!names.Add(field.Name))
                throw Error(context, "DuplicateName", "Field-value data requires distinct exact names.");
            Validate(field.Value, context);
            captured.Add(field);
        }

        return new(captured);
    }

    internal static void Validate(PdfValue value, PdfOperationContext context)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        if (value.Kind == PdfValueKind.String)
        {
            PdfTextStrings.Decode(value, context);
            return;
        }

        if (value.Kind == PdfValueKind.Name)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, value.NameValue.Length * 4L + 64);
            return;
        }

        if (value.Kind == PdfValueKind.Null)
            return;
        if (value.Kind == PdfValueKind.Array)
        {
            foreach (var item in value.Items)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                if (item.Kind != PdfValueKind.String)
                    throw Error(context, "Value", "Multi-selection data contains only text strings.");
                PdfTextStrings.Decode(item, context);
            }

            return;
        }

        throw Error(context, "Value", "Data values support only null, names, text strings and text arrays.");
    }

    internal static string Type(PdfValue value) => value.Kind == PdfValueKind.Null ? "null" : value.Kind == PdfValueKind.Name ? "name" : value.Kind == PdfValueKind.Array ? "texts" : "text";
    internal static PdfException Error(PdfOperationContext context, string code, string message) => PdfWriterCore.Error(context, "Forms.Data" + code, message);
    private static void Check(PdfFormDataFormat format)
    {
        if (!Enum.IsDefined(typeof(PdfFormDataFormat), format))
            throw new ArgumentOutOfRangeException(nameof(format));
    }

    internal static PdfValue Typed(string type, List<string> values, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, values.Count * 32L + 64);
        if (type == "null" && values.Count == 0)
            return PdfValue.Null;
        if (type == "name" && values.Count == 1)
        {
            try
            {
                return PdfValue.Name(values[0]);
            }
            catch (ArgumentException)
            {
                throw Error(context, "Name", "A typed button export must be a byte-valued PDF name.");
            }
        }

        if (type == "text" && values.Count == 1)
            return PdfTextStrings.Encode(values[0], context);
        if (type == "texts")
            return PdfValue.Array(values.Select(v => PdfTextStrings.Encode(v, context)).ToArray());
        throw Error(context, "Schema", "The typed field-value shape does not match its declared type.");
    }
}

internal sealed class PdfFormDataStream : Stream
{
    private readonly Stream _inner;
    private readonly PdfOperationContext _context;
    private readonly bool _reading;
    private long _length;
    internal PdfFormDataStream(Stream inner, PdfOperationContext context, bool reading)
    {
        _inner = inner;
        _context = context;
        _reading = reading;
    }

    public override bool CanRead => _reading;
    public override bool CanWrite => !_reading;
    public override bool CanSeek => false;
    public override long Length => _length;
    public override long Position { get => _length; set => throw new NotSupportedException(); }

    public override void Flush()
    {
        _context.Checkpoint();
        _inner.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        _context.Checkpoint();
        if (!_reading)
            throw new NotSupportedException();
        int n = _inner.Read(buffer, offset, count);
        Charge(n);
        return n;
    }

    public override int ReadByte()
    {
        _context.Checkpoint();
        if (!_reading)
            throw new NotSupportedException();
        int b = _inner.ReadByte();
        if (b >= 0)
            Charge(1);
        return b;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _context.Checkpoint();
        if (_reading)
            throw new NotSupportedException();
        Charge(count);
        _inner.Write(buffer, offset, count);
    }

    private void Charge(int count)
    {
        if (count > 16 * 1024 * 1024 - _length)
            throw PdfFormData.Error(_context, "Length", "The field-value interchange exceeds its sixteen MiB profile ceiling.");
        _length += count;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
