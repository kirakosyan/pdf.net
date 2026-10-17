using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;

namespace Fluent.Pdf;
/// <summary>The lexical kind of an immutable PDF value.</summary>
public enum PdfValueKind
{
    /// <summary>The null object.</summary>
    Null,
    /// <summary>A Boolean.</summary>
    Boolean,
    /// <summary>A decimal PDF number, retaining its original spelling.</summary>
    Number,
    /// <summary>A byte-valued name, represented as Latin-1 characters.</summary>
    Name,
    /// <summary>An uninterpreted byte string.</summary>
    String,
    /// <summary>An ordered sequence.</summary>
    Array,
    /// <summary>A dictionary with ordinal byte-valued names.</summary>
    Dictionary,
    /// <summary>An indirect reference.</summary>
    Reference,
    /// <summary>A dictionary and opaque encoded stream.</summary>
    Stream
}

/// <summary>An indirect object number and generation. Object zero is the null reference.</summary>
public readonly struct PdfObjectId : IEquatable<PdfObjectId>
{
    /// <summary>Creates a nonnegative object number with a generation in 0–65535.</summary>
    public PdfObjectId(int number, int generation = 0)
    {
        if (number < 0)
            throw new ArgumentOutOfRangeException(nameof(number));
        if (generation < 0 || generation > 65535)
            throw new ArgumentOutOfRangeException(nameof(generation));
        Number = number;
        Generation = generation;
    }

    /// <summary>Gets the object number.</summary>
    public int Number { get; }
    /// <summary>Gets the generation.</summary>
    public int Generation { get; }

    /// <summary>Compares the number and generation.</summary>
    public bool Equals(PdfObjectId other) => Number == other.Number && Generation == other.Generation;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfObjectId other && Equals(other);
    /// <inheritdoc/>
    public override int GetHashCode() => unchecked(Number * 397 ^ Generation);
    /// <summary>Formats an indirect reference using invariant decimal notation.</summary>
    public override string ToString() => Number.ToString(CultureInfo.InvariantCulture) + " " + Generation.ToString(CultureInfo.InvariantCulture) + " R";
    /// <summary>Compares the number and generation.</summary>
    public static bool operator ==(PdfObjectId left, PdfObjectId right) => left.Equals(right);
    /// <summary>Compares the number and generation.</summary>
    public static bool operator !=(PdfObjectId left, PdfObjectId right) => !left.Equals(right);
}

/// <summary>An immutable syntax value. Unknown keys and encoded bytes are retained; no action is executed.</summary>
public sealed class PdfValue
{
    private readonly object? _value;
    private readonly IReadOnlyDictionary<string, PdfValue>? _dictionary;
    private readonly PdfByteStore? _bytes;
    private readonly long _start, _length;
    private PdfValue(PdfValueKind kind, object? value = null, IReadOnlyDictionary<string, PdfValue>? dictionary = null, PdfByteStore? bytes = null, long start = 0, long length = 0)
    {
        Kind = kind;
        _value = value;
        _dictionary = dictionary;
        _bytes = bytes;
        _start = start;
        _length = length;
    }

    /// <summary>Gets the lexical kind.</summary>
    public PdfValueKind Kind { get; }
    /// <summary>Gets a Boolean; throws if the kind differs.</summary>
    public bool BooleanValue => Kind == PdfValueKind.Boolean ? (bool)_value! : throw WrongKind();
    /// <summary>Gets the exact decimal spelling; throws if the kind differs.</summary>
    public string NumberText => Kind == PdfValueKind.Number ? (string)_value! : throw WrongKind();
    /// <summary>Gets a finite approximate number; throws for a different kind or a value outside Double range.</summary>
    public double NumberValue => Kind == PdfValueKind.Number && double.TryParse(NumberText, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double n) && !double.IsInfinity(n) && !double.IsNaN(n) ? n : throw WrongKind();
    /// <summary>Gets the decoded byte-valued name as Latin-1 characters.</summary>
    public string NameValue => Kind == PdfValueKind.Name ? (string)_value! : throw WrongKind();
    /// <summary>Gets immutable byte-string contents. Interpretation and decryption belong to the owning feature.</summary>
    public IReadOnlyList<byte> StringBytes => Kind == PdfValueKind.String ? (IReadOnlyList<byte>)_value! : throw WrongKind();
    /// <summary>Gets the immutable array elements.</summary>
    public IReadOnlyList<PdfValue> Items => Kind == PdfValueKind.Array ? (IReadOnlyList<PdfValue>)_value! : throw WrongKind();
    /// <summary>Gets immutable dictionary entries for dictionaries and streams.</summary>
    public IReadOnlyDictionary<string, PdfValue> Entries => _dictionary ?? throw WrongKind();
    /// <summary>Gets the indirect reference identity.</summary>
    public PdfObjectId ReferenceId => Kind == PdfValueKind.Reference ? (PdfObjectId)_value! : throw WrongKind();
    /// <summary>Gets the encoded stream length.</summary>
    public long RawStreamLength => Kind == PdfValueKind.Stream ? _length : throw WrongKind();

    /// <summary>Opens an independent read-only view of opaque encoded bytes, without decoding or executing content.</summary>
    public Stream OpenRawStream() => Kind == PdfValueKind.Stream ? _bytes!.Open(_start, _length) : throw WrongKind();
    /// <summary>Gets the shared immutable null value.</summary>
    public static PdfValue Null { get; } = new(PdfValueKind.Null);

    /// <summary>Creates a Boolean.</summary>
    public static PdfValue Boolean(bool value) => new(PdfValueKind.Boolean, value);
    /// <summary>Creates an exact integer.</summary>
    public static PdfValue Integer(long value) => Number(value.ToString(CultureInfo.InvariantCulture));
    /// <summary>Creates an exact PDF decimal token. Exponents and a bare sign/dot are rejected.</summary>
    public static PdfValue Number(string text)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));
        int digits = 0, dots = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c >= '0' && c <= '9')
                digits++;
            else if (c == '.')
                dots++;
            else if (i != 0 || (c != '+' && c != '-'))
                throw new ArgumentException("A PDF decimal token is required.", nameof(text));
        }

        if (digits == 0 || dots > 1)
            throw new ArgumentException("A PDF decimal token is required.", nameof(text));
        return new(PdfValueKind.Number, text);
    }

    /// <summary>Creates a byte-valued name. Characters above U+00FF are rejected.</summary>
    public static PdfValue Name(string value)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));
        if (value.Any(c => c > 255 || c == 0))
            throw new ArgumentException("A non-NUL byte-valued name is required.", nameof(value));
        return new(PdfValueKind.Name, value);
    }

    /// <summary>Copies uninterpreted string bytes.</summary>
    public static PdfValue String(params byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        return OwnedString((byte[])bytes.Clone());
    }

    /// <summary>Copies an ordered array of immutable values.</summary>
    public static PdfValue Array(params PdfValue[] items)
    {
        if (items is null)
            throw new ArgumentNullException(nameof(items));
        if (items.Any(v => v is null))
            throw new ArgumentException("Array values cannot be CLR null.", nameof(items));
        return new(PdfValueKind.Array, new ReadOnlyCollection<PdfValue>((PdfValue[])items.Clone()));
    }

    /// <summary>Copies entries with unique ordinal byte-valued keys.</summary>
    public static PdfValue Dictionary(IEnumerable<KeyValuePair<string, PdfValue>> entries)
    {
        if (entries is null)
            throw new ArgumentNullException(nameof(entries));
        var result = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
        foreach (var pair in entries)
        {
            Name(pair.Key);
            if (pair.Value is null)
                throw new ArgumentException("Dictionary values cannot be CLR null.", nameof(entries));
            if (result.ContainsKey(pair.Key))
                throw new ArgumentException("Dictionary keys must be unique.", nameof(entries));
            result.Add(pair.Key, pair.Value);
        }

        return OwnedDictionary(result);
    }

    /// <summary>Creates an indirect reference.</summary>
    public static PdfValue Reference(PdfObjectId id) => new(PdfValueKind.Reference, id);
    internal static PdfValue OwnedString(byte[] bytes) => new(PdfValueKind.String, new ReadOnlyCollection<byte>(bytes));
    internal static PdfValue OwnedArray(List<PdfValue> items) => new(PdfValueKind.Array, items.AsReadOnly());
    internal static PdfValue OwnedDictionary(Dictionary<string, PdfValue> entries) => new(PdfValueKind.Dictionary, dictionary: new ReadOnlyDictionary<string, PdfValue>(entries));
    internal static PdfValue StreamValue(PdfValue dictionary, PdfByteStore bytes, long start, long length) => new(PdfValueKind.Stream, dictionary: dictionary.Entries, bytes: bytes, start: start, length: length);
    internal long IntegerValue => Kind == PdfValueKind.Number && long.TryParse(NumberText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long n) ? n : throw new InvalidDataException("A bounded integer is required.");

    private static InvalidOperationException WrongKind() => new("The PDF value has a different kind or numeric range.");
}
