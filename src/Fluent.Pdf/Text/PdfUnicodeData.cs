using System.IO;

namespace Fluent.Pdf;
internal sealed class PdfUnicodeData
{
    internal enum Grapheme
    {
        Other,
        CR,
        LF,
        Control,
        Extend,
        ZWJ,
        RegionalIndicator,
        Prepend,
        SpacingMark,
        L,
        V,
        T,
        LV,
        LVT
    }

    internal readonly struct Property
    {
        internal Property(uint script, uint packed, uint extension)
        {
            Script = script;
            Packed = packed;
            Extension = extension;
        }

        internal uint Script { get; }
        private uint Packed { get; }
        internal uint Extension { get; }
        internal PdfBidiClass Bidi => (PdfBidiClass)(Packed & 31);
        internal Grapheme Break => (Grapheme)((Packed >> 5) & 15);
        internal int IndicConjunct => (int)((Packed >> 9) & 3);
        internal bool Ignorable => (Packed & 2048) != 0;
        internal bool Pictographic => (Packed & 4096) != 0;
    }

    private readonly (int First, int Last, Property Value)[] _ranges;
    private readonly uint[][] _extensions;
    private readonly Dictionary<int, int> _mirrors;
    private readonly Dictionary<int, (int Partner, bool Open)> _brackets;
    private static readonly Lazy<PdfUnicodeData> Instance = new(() => new PdfUnicodeData());
    private const int ResidentCharge = 1048576;
    internal static PdfUnicodeData For(PdfOperationContext context)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.ExpandedBytes, ResidentCharge);
        var data = Instance.Value;
        context.Checkpoint();
        return data;
    }

    private PdfUnicodeData()
    {
        using var stream = typeof(PdfUnicodeData).Assembly.GetManifestResourceStream("Fluent.Pdf.Text.Data.Unicode18.bin") ?? throw new InvalidDataException("Pinned Unicode data is absent.");
        using var input = new BinaryReader(stream);
        if (input.ReadUInt32() != 0x31555046 || input.ReadInt32() != 18)
            throw new InvalidDataException("Pinned Unicode data has an invalid identity.");
        int ranges = input.ReadInt32(), extensions = input.ReadInt32(), mirrors = input.ReadInt32(), brackets = input.ReadInt32();
        input.ReadInt32();
        if (ranges < 1 || ranges > 20000 || extensions < 1 || extensions > 4096 || mirrors < 0 || mirrors > 2048 || brackets < 0 || brackets > 512)
            throw new InvalidDataException("Pinned Unicode counts are invalid.");
        _ranges = new (int, int, Property)[ranges];
        int previous = -1;
        for (int i = 0; i < ranges; i++)
        {
            int first = input.ReadInt32(), last = input.ReadInt32();
            uint script = input.ReadUInt32(), properties = input.ReadUInt32(), extension = input.ReadUInt32();
            if (first != previous + 1 || last < first || last > 0x10ffff || extension >= extensions || (properties & 31) > 22 || ((properties >> 5) & 15) > 13)
                throw new InvalidDataException("Pinned Unicode ranges are invalid.");
            _ranges[i] = (first, last, new Property(script, properties, extension));
            previous = last;
        }

        if (previous != 0x10ffff)
            throw new InvalidDataException("Pinned Unicode ranges are incomplete.");
        _extensions = new uint[extensions][];
        for (int i = 0; i < extensions; i++)
        {
            int count = input.ReadInt32();
            if (count < 0 || count > 64)
                throw new InvalidDataException("Pinned script extension count is invalid.");
            var values = new uint[count];
            for (int j = 0; j < count; j++)
                values[j] = input.ReadUInt32();
            _extensions[i] = values;
        }

        _mirrors = new Dictionary<int, int>();
        for (int i = 0; i < mirrors; i++)
            _mirrors.Add(input.ReadInt32(), input.ReadInt32());
        _brackets = new Dictionary<int, (int, bool)>();
        for (int i = 0; i < brackets; i++)
            _brackets.Add(input.ReadInt32(), (input.ReadInt32(), input.ReadInt32() == 1));
        if (stream.ReadByte() != -1)
            throw new InvalidDataException("Pinned Unicode data has trailing bytes.");
    }

    internal Property Get(int scalar)
    {
        int low = 0, high = _ranges.Length - 1;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            var range = _ranges[middle];
            if (scalar < range.First)
                high = middle - 1;
            else if (scalar > range.Last)
                low = middle + 1;
            else
                return range.Value;
        }

        throw new ArgumentOutOfRangeException(nameof(scalar));
    }

    internal int Mirror(int scalar) => _mirrors.TryGetValue(scalar, out int value) ? value : scalar;
    internal bool Bracket(int scalar, out int partner, out bool open)
    {
        if (_brackets.TryGetValue(scalar, out var value))
        {
            partner = value.Partner;
            open = value.Open;
            return true;
        }

        partner = 0;
        open = false;
        return false;
    }

    internal IReadOnlyList<uint> ScriptExtensions(Property property) => _extensions[property.Extension];
}
