using System.Collections.ObjectModel;

namespace Fluent.Pdf.Intelligence;
/// <summary>Closed JSON schema kinds. Exact decimal numbers and signed64-bit integers are used; external references and executable validators are absent.</summary>
public enum PdfIntelligenceValueKind
{
    /// <summary>A JSON object with explicitly declared members.</summary>
    Object,
    /// <summary>A bounded homogeneous array.</summary>
    Array,
    /// <summary>A Unicode scalar string.</summary>
    String,
    /// <summary>An exactly representable decimal.</summary>
    Number,
    /// <summary>A signed64-bit integer.</summary>
    Integer,
    /// <summary>A boolean.</summary>
    Boolean,
    /// <summary>A null value.</summary>
    Null,
    /// <summary>A value matching one explicit nested schema, or null.</summary>
    Nullable
}

/// <summary>One explicit object member and its immutable schema.</summary>
public sealed class PdfIntelligenceField
{
    /// <summary>Captures an ASCII identifier of1–64 characters and a required/optional member schema.</summary>
    public PdfIntelligenceField(string name, PdfIntelligenceSchema schema, bool required = true)
    {
        if (name == null || name.Length < 1 || name.Length > 64 || !Letter(name[0]) || name.Any(ch => !Letter(ch) && !(ch >= '0' && ch <= '9') && ch != '_'))
            throw new ArgumentException("A bounded ASCII member identifier is required.", nameof(name));
        Name = name;
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        Required = required;
    }

    private static bool Letter(char ch) => ch >= 'A' && ch <= 'Z' || ch >= 'a' && ch <= 'z';
    /// <summary>Gets the exact case-sensitive member name.</summary>
    public string Name { get; }
    /// <summary>Gets the immutable member schema.</summary>
    public PdfIntelligenceSchema Schema { get; }
    /// <summary>Gets whether the member is required.</summary>
    public bool Required { get; }
}

/// <summary>An immutable finite JSON schema tree. Unknown object members reject; string lengths count Unicode scalars.</summary>
public sealed class PdfIntelligenceSchema
{
    private PdfIntelligenceSchema(PdfIntelligenceValueKind kind, PdfIntelligenceField[]? fields = null, PdfIntelligenceSchema? item = null, int minimum = 0, int maximum = 0, decimal? lower = null, decimal? upper = null, string[]? values = null)
    {
        Kind = kind;
        Fields = new ReadOnlyCollection<PdfIntelligenceField>(fields ?? System.Array.Empty<PdfIntelligenceField>());
        ItemSchema = item;
        MinimumLength = minimum;
        MaximumLength = maximum;
        MinimumNumber = lower;
        MaximumNumber = upper;
        AllowedStrings = new ReadOnlyCollection<string>(values ?? System.Array.Empty<string>());
    }

    /// <summary>Gets the explicit JSON kind.</summary>
    public PdfIntelligenceValueKind Kind { get; }
    /// <summary>Gets explicit object members.</summary>
    public IReadOnlyList<PdfIntelligenceField> Fields { get; }
    /// <summary>Gets array item/nullable value schema or null.</summary>
    public PdfIntelligenceSchema? ItemSchema { get; }
    /// <summary>Gets minimum string-scalar/array length.</summary>
    public int MinimumLength { get; }
    /// <summary>Gets maximum string-scalar/array length.</summary>
    public int MaximumLength { get; }
    /// <summary>Gets optional exact lower numeric bound.</summary>
    public decimal? MinimumNumber { get; }
    /// <summary>Gets optional exact upper numeric bound.</summary>
    public decimal? MaximumNumber { get; }
    /// <summary>Gets an optional closed ordinal string enumeration.</summary>
    public IReadOnlyList<string> AllowedStrings { get; }

    /// <summary>Creates a closed object of at most128 distinct members.</summary>
    public static PdfIntelligenceSchema Object(IEnumerable<PdfIntelligenceField> fields)
    {
        if (fields == null)
            throw new ArgumentNullException(nameof(fields));
        var copy = new List<PdfIntelligenceField>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (field == null || copy.Count == 128 || !names.Add(field.Name))
                throw new ArgumentException("Bounded nonnull unique object fields are required.", nameof(fields));
            copy.Add(field);
        }

        return new(PdfIntelligenceValueKind.Object, fields: copy.ToArray());
    }

    /// <summary>Creates a bounded array with at most1024 items.</summary>
    public static PdfIntelligenceSchema Array(PdfIntelligenceSchema items, int minimumItems = 0, int maximumItems = 100)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (minimumItems < 0 || maximumItems < minimumItems || maximumItems > 1024)
            throw new ArgumentOutOfRangeException(nameof(maximumItems));
        return new(PdfIntelligenceValueKind.Array, item: items, minimum: minimumItems, maximum: maximumItems);
    }

    /// <summary>Creates a bounded Unicode scalar string and optional closed enumeration of at most128 values.</summary>
    public static PdfIntelligenceSchema String(int minimumLength = 0, int maximumLength = 16384, IEnumerable<string>? allowedValues = null)
    {
        if (minimumLength < 0 || maximumLength < minimumLength || maximumLength > 1000000)
            throw new ArgumentOutOfRangeException(nameof(maximumLength));
        var values = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        if (allowedValues != null)
            foreach (string value in allowedValues)
            {
                if (value == null || value.Length > maximumLength * 2L || values.Count == 128 || !unique.Add(value))
                    throw new ArgumentException("At most128 distinct bounded literal strings are supported.", nameof(allowedValues));
                int count = Scalars(value);
                if (count < minimumLength || count > maximumLength)
                    throw new ArgumentException("An enumeration value exceeds the string bounds.", nameof(allowedValues));
                values.Add(value);
            }

        return new(PdfIntelligenceValueKind.String, minimum: minimumLength, maximum: maximumLength, values: values.ToArray());
    }

    /// <summary>Creates an exact decimal schema with optional finite bounds.</summary>
    public static PdfIntelligenceSchema Number(decimal? minimum = null, decimal? maximum = null)
    {
        if (minimum.HasValue && maximum.HasValue && minimum > maximum)
            throw new ArgumentOutOfRangeException(nameof(maximum));
        return new(PdfIntelligenceValueKind.Number, lower: minimum, upper: maximum);
    }

    /// <summary>Creates an exact signed64-bit integer schema.</summary>
    public static PdfIntelligenceSchema Integer(long minimum = long.MinValue, long maximum = long.MaxValue)
    {
        if (maximum < minimum)
            throw new ArgumentOutOfRangeException(nameof(maximum));
        return new(PdfIntelligenceValueKind.Integer, lower: minimum, upper: maximum);
    }

    /// <summary>Creates a boolean schema.</summary>
    public static PdfIntelligenceSchema Boolean() => new(PdfIntelligenceValueKind.Boolean);
    /// <summary>Creates a null-only schema.</summary>
    public static PdfIntelligenceSchema Null() => new(PdfIntelligenceValueKind.Null);
    /// <summary>Allows null or one explicit nested value schema.</summary>
    public static PdfIntelligenceSchema Nullable(PdfIntelligenceSchema value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        return value.Kind == PdfIntelligenceValueKind.Nullable || value.Kind == PdfIntelligenceValueKind.Null ? value : new(PdfIntelligenceValueKind.Nullable, item: value);
    }

    internal void Validate(object? value, PdfOperationContext c, int depth = 0)
    {
        c.Checkpoint();
        c.Consume(PdfResourceKind.Objects, 1);
        using var scope = c.EnterScope(PdfResourceKind.Depth);
        if (depth >= 16)
            throw PdfDocumentIntelligence.Error(c, "SchemaDepth", "The finite intelligence schema/value depth is exceeded.");
        bool valid;
        switch (Kind)
        {
            case PdfIntelligenceValueKind.Object:
                if (value is not Dictionary<string, object?> members)
                    break;
                if (members.Keys.Any(key => !Fields.Any(field => field.Name == key)))
                    break;
                foreach (var field in Fields)
                {
                    if (!members.TryGetValue(field.Name, out var member))
                    {
                        if (field.Required)
                            throw PdfDocumentIntelligence.Error(c, "Schema", "A required structured result member is absent.");
                        continue;
                    }

                    field.Schema.Validate(member, c, depth + 1);
                }

                return;
            case PdfIntelligenceValueKind.Array:
                if (value is not List<object?> list || list.Count < MinimumLength || list.Count > MaximumLength)
                    break;
                foreach (var item in list)
                    ItemSchema!.Validate(item, c, depth + 1);
                return;
            case PdfIntelligenceValueKind.String:
                if (value is not string text)
                    break;
                int length = Scalars(text);
                if (length >= MinimumLength && length <= MaximumLength && (AllowedStrings.Count == 0 || AllowedStrings.Contains(text)))
                    return;
                break;
            case PdfIntelligenceValueKind.Number:
            case PdfIntelligenceValueKind.Integer:
                valid = value is decimal number && (!MinimumNumber.HasValue || number >= MinimumNumber.Value) && (!MaximumNumber.HasValue || number <= MaximumNumber.Value) && (Kind != PdfIntelligenceValueKind.Integer || decimal.Truncate(number) == number);
                if (valid)
                    return;
                break;
            case PdfIntelligenceValueKind.Boolean:
                if (value is bool)
                    return;
                break;
            case PdfIntelligenceValueKind.Null:
                if (value == null)
                    return;
                break;
            case PdfIntelligenceValueKind.Nullable:
                if (value == null)
                    return;
                ItemSchema!.Validate(value, c, depth + 1);
                return;
        }

        throw PdfDocumentIntelligence.Error(c, "Schema", "The structured model result does not match the exact requested schema.");
    }

    internal object Declaration(PdfOperationContext c, int depth = 0)
    {
        c.Checkpoint();
        c.Consume(PdfResourceKind.ExpandedBytes, 512);
        c.Consume(PdfResourceKind.Objects, 1);
        using var scope = c.EnterScope(PdfResourceKind.Depth);
        if (depth >= 16)
            throw PdfDocumentIntelligence.Error(c, "SchemaDepth", "The finite intelligence schema depth is exceeded.");
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = Kind.ToString().ToLowerInvariant()
        };
        if (Kind == PdfIntelligenceValueKind.Nullable)
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["anyOf"] = new List<object?>
                {
                    ItemSchema!.Declaration(c, depth + 1),
                    new Dictionary<string, object?>
                    {
                        ["type"] = "null"
                    }
                }
            };
        if (Kind == PdfIntelligenceValueKind.Object)
        {
            var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
            var required = new List<object?>();
            foreach (var field in Fields)
            {
                c.Consume(PdfResourceKind.ExpandedBytes, 128);
                properties.Add(field.Name, field.Schema.Declaration(c, depth + 1));
                if (field.Required)
                    required.Add(field.Name);
            }

            result["properties"] = properties;
            result["required"] = required;
            result["additionalProperties"] = false;
        }
        else if (Kind == PdfIntelligenceValueKind.Array)
        {
            result["items"] = ItemSchema!.Declaration(c, depth + 1);
            result["minItems"] = (decimal)MinimumLength;
            result["maxItems"] = (decimal)MaximumLength;
        }
        else if (Kind == PdfIntelligenceValueKind.String)
        {
            result["minLength"] = (decimal)MinimumLength;
            result["maxLength"] = (decimal)MaximumLength;
            if (AllowedStrings.Count != 0)
                result["enum"] = AllowedStrings.Select(value => (object? )value).ToList();
        }
        else if (Kind == PdfIntelligenceValueKind.Number || Kind == PdfIntelligenceValueKind.Integer)
        {
            if (MinimumNumber.HasValue)
                result["minimum"] = MinimumNumber.Value;
            if (MaximumNumber.HasValue)
                result["maximum"] = MaximumNumber.Value;
        }

        return result;
    }

    internal static int Scalars(string text)
    {
        int count = 0;
        for (int n = 0; n < text.Length; n++, count++)
            if (char.IsHighSurrogate(text[n]))
            {
                if (++n == text.Length || !char.IsLowSurrogate(text[n]))
                    throw new ArgumentException("Valid Unicode scalars are required.", nameof(text));
            }
            else if (char.IsLowSurrogate(text[n]))
                throw new ArgumentException("Valid Unicode scalars are required.", nameof(text));
        return count;
    }
}
