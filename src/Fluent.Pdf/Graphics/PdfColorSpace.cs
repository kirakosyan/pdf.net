using System.Collections.ObjectModel;
using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable explicitly selected PDF color space and exact channel bounds.</summary>
public sealed class PdfColorSpace
{
    private PdfColorSpace(PdfValue definition, int components, double[] range, string version, bool pattern = false, bool blending = true)
    {
        Definition = definition;
        Components = components;
        Ranges = new ReadOnlyCollection<double>(range);
        MinimumVersion = version;
        IsPattern = pattern;
        SupportsBlending = blending;
    }

    /// <summary>Gets its direct immutable PDF definition, including any nested ICC stream.</summary>
    public PdfValue Definition { get; }
    /// <summary>Gets the number of numeric color channels.</summary>
    public int Components { get; }
    /// <summary>Gets inclusive paired component bounds.</summary>
    public IReadOnlyList<double> Ranges { get; }
    /// <summary>Gets its required PDF version floor.</summary>
    public string MinimumVersion { get; }
    /// <summary>Gets whether the space requires a pattern name.</summary>
    public bool IsPattern { get; }
    internal bool SupportsBlending { get; }
    /// <summary>Gets a successful construction report, or null for a fixed device-space singleton.</summary>
    public PdfOperationReport? OperationReport { get; private set; }
    /// <summary>Gets explicit DeviceGray.</summary>
    public static PdfColorSpace Gray { get; } = new(PdfValue.Name("DeviceGray"), 1, UnitRanges(1), "1.2");
    /// <summary>Gets explicit DeviceRGB.</summary>
    public static PdfColorSpace Rgb { get; } = new(PdfValue.Name("DeviceRGB"), 3, UnitRanges(3), "1.2");
    /// <summary>Gets explicit DeviceCMYK.</summary>
    public static PdfColorSpace Cmyk { get; } = new(PdfValue.Name("DeviceCMYK"), 4, UnitRanges(4), "1.2");

    /// <summary>Creates CIE Lab with an explicit normalized white point, component ranges and optional black point.</summary>
    public static PdfColorSpace Lab(double whiteX, double whiteZ, double aMinimum = -100, double aMaximum = 100, double bMinimum = -100, double bMaximum = 100, double blackX = 0, double blackY = 0, double blackZ = 0)
    {
        White(whiteX, whiteZ);
        Black(blackX, blackY, blackZ);
        var range = new[]
        {
            0d,
            100d,
            aMinimum,
            aMaximum,
            bMinimum,
            bMaximum
        };
        PdfFunction.Bounds(range, 3);
        return new(PdfValue.Array(PdfValue.Name("Lab"), PdfGraphicsNumbers.Dict(("WhitePoint", PdfGraphicsNumbers.Array(whiteX, 1, whiteZ)), ("BlackPoint", PdfGraphicsNumbers.Array(blackX, blackY, blackZ)), ("Range", PdfGraphicsNumbers.Array(aMinimum, aMaximum, bMinimum, bMaximum)))), 3, range, "1.2");
    }

    /// <summary>Creates calibrated Gray with explicit white point, gamma and black point.</summary>
    public static PdfColorSpace CalGray(double whiteX, double whiteZ, double gamma = 1, double blackX = 0, double blackY = 0, double blackZ = 0)
    {
        White(whiteX, whiteZ);
        Black(blackX, blackY, blackZ);
        PdfGraphicsNumbers.Finite(gamma);
        if (gamma <= 0)
            throw new ArgumentOutOfRangeException(nameof(gamma));
        return new(PdfValue.Array(PdfValue.Name("CalGray"), PdfGraphicsNumbers.Dict(("WhitePoint", PdfGraphicsNumbers.Array(whiteX, 1, whiteZ)), ("BlackPoint", PdfGraphicsNumbers.Array(blackX, blackY, blackZ)), ("Gamma", PdfValues.Real(gamma)))), 1, UnitRanges(1), "1.2");
    }

    /// <summary>Creates calibrated RGB with explicit white point, three positive gammas and a 3x3 XYZ matrix.</summary>
    public static PdfColorSpace CalRgb(double whiteX, double whiteZ, double[] gamma, double[] matrix)
    {
        White(whiteX, whiteZ);
        if (gamma == null || matrix == null || gamma.Length != 3 || matrix.Length != 9)
            throw new ArgumentException("Three gamma values and nine matrix coefficients are required.");
        foreach (double value in gamma)
        {
            PdfGraphicsNumbers.Finite(value);
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(gamma));
        }

        foreach (double value in matrix)
            PdfGraphicsNumbers.Finite(value);
        return new(PdfValue.Array(PdfValue.Name("CalRGB"), PdfGraphicsNumbers.Dict(("WhitePoint", PdfGraphicsNumbers.Array(whiteX, 1, whiteZ)), ("Gamma", PdfGraphicsNumbers.Array(gamma)), ("Matrix", PdfGraphicsNumbers.Array(matrix)))), 3, UnitRanges(3), "1.2");
    }

    /// <summary>Creates a Separation color with an explicit one-input tint transform into the alternate space.</summary>
    public static PdfColorSpace Separation(string name, PdfColorSpace alternate, PdfFunction tint)
    {
        Name(name);
        Tint(alternate, tint, 1);
        return new(PdfValue.Array(PdfValue.Name("Separation"), PdfValue.Name(name), alternate.Definition, tint.Definition), 1, UnitRanges(1), alternate.MinimumVersion, blending: false);
    }

    /// <summary>Creates DeviceN with exact distinct colorant names and an explicit multi-input tint transform.</summary>
    public static PdfColorSpace DeviceN(string[] names, PdfColorSpace alternate, PdfFunction tint)
    {
        if (names == null || names.Length < 1 || names.Length > 32 || names.Where(x => x != "None").Distinct(StringComparer.Ordinal).Count() != names.Count(x => x != "None") || names.Contains("All", StringComparer.Ordinal))
            throw new ArgumentException("One through 32 distinct colorant names are required.", nameof(names));
        foreach (string name in names)
            Name(name);
        Tint(alternate, tint, names.Length);
        return new(PdfValue.Array(PdfValue.Name("DeviceN"), PdfValue.Array(names.Select(PdfValue.Name).ToArray()), alternate.Definition, tint.Definition), names.Length, UnitRanges(names.Length), PdfVersionNumber.Text(Math.Max(names.Length > 8 ? 16 : 13, PdfVersionNumber.Parse(alternate.MinimumVersion))), blending: false);
    }

    /// <summary>Creates colored Pattern, or uncolored Pattern with an explicit non-pattern base space.</summary>
    public static PdfColorSpace Pattern(PdfColorSpace? baseSpace = null)
    {
        if (baseSpace?.IsPattern == true)
            throw new ArgumentException("A pattern cannot use another pattern as its base.", nameof(baseSpace));
        return new(baseSpace == null ? PdfValue.Name("Pattern") : PdfValue.Array(PdfValue.Name("Pattern"), baseSpace.Definition), baseSpace?.Components ?? 0, baseSpace?.Ranges.ToArray() ?? System.Array.Empty<double>(), baseSpace?.MinimumVersion ?? "1.2", true, false);
    }

    /// <summary>Reads a bounded complete ICC v2/v4 Gray/RGB/CMYK profile and preserves it exactly; unsupported profiles fail without substitution.</summary>
    public static PdfColorSpace Icc(Stream input, PdfColorSpace? alternate = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (!input.CanRead)
            throw new ArgumentException("A readable ICC stream is required.", nameof(input));
        PdfColorSpace? result = null;
        var report = PdfFont.Run("ReadIccColorSpace", "managed.graphics", policy, cancellationToken, context =>
        {
            using var lease = new BorrowedStream(input, true, context);
            var bytes = PdfByteStore.Read(lease, context);
            result = IccCore(bytes, alternate, context);
        }, PdfCapability.WriteDocument);
        result!.OperationReport = report;
        return result;
    }

    internal static PdfColorSpace IccCore(PdfByteStore bytes, PdfColorSpace? alternate, PdfOperationContext context)
    {
        int components = PdfIccHeader.Validate(bytes, context);
        var selected = alternate ?? (components == 1 ? Gray : components == 3 ? Rgb : Cmyk);
        if (selected.Components != components || selected.IsPattern || !selected.SupportsBlending)
            throw PdfWriterCore.Error(context, "Graphics.IccAlternate", "The explicit ICC alternate must have the same channel count and a nonspecial blending color space.");
        var dictionary = PdfGraphicsNumbers.Dict(("N", PdfValue.Integer(components)), ("Alternate", selected.Definition));
        var profile = PdfValue.StreamValue(dictionary, bytes, 0, bytes.Length);
        return new(PdfValue.Array(PdfValue.Name("ICCBased"), profile), components, UnitRanges(components), PdfIccHeader.MinimumVersion(bytes));
    }
    /// <summary>Creates an exact eight-bit palette over an explicit non-indexed, non-pattern base space.</summary>
    public static PdfColorSpace Indexed(PdfColorSpace baseSpace, int highValue, byte[] palette, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (baseSpace == null) throw new ArgumentNullException(nameof(baseSpace));
        if (palette == null) throw new ArgumentNullException(nameof(palette));
        PdfColorSpace? result = null;
        var report = PdfFont.Run("CreateIndexedColorSpace", "managed.graphics", policy, cancellationToken, c => result = IndexedCore(baseSpace, highValue, palette, c), PdfCapability.WriteDocument);
        result!.OperationReport = report; return result;
    }
    internal static PdfColorSpace IndexedCore(PdfColorSpace baseSpace, int highValue, byte[] palette, PdfOperationContext context)
    {
        if (highValue < 0 || highValue > 255 || baseSpace.IsPattern || baseSpace.Definition.Kind == PdfValueKind.Array && baseSpace.Definition.Items[0].NameValue == "Indexed" || palette.Length != (highValue + 1) * baseSpace.Components)
            throw PdfWriterCore.Error(context, "Graphics.IndexedPalette", "An indexed palette requires exact byte entries for zero through hival over a non-indexed base.");
        context.Consume(PdfResourceKind.ExpandedBytes, palette.Length * 2L + 256);
        return new(PdfValue.Array(PdfValue.Name("Indexed"), baseSpace.Definition, PdfValue.Integer(highValue), PdfValue.String(palette)), 1, new double[] { 0, highValue }, baseSpace.MinimumVersion, blending: false);
    }

    /// <summary>Captures this exact color space as a named graphics resource with a bounded validation report.</summary>
    public PdfGraphicsResource AsResource(PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfGraphicsResource.Create(PdfGraphicsResourceCategory.ColorSpace, Definition, MinimumVersion, policy, cancellationToken);
    private static void Name(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 127)
            throw new ArgumentException("A bounded colorant name is required.", nameof(name));
        PdfValue.Name(name);
    }

    private static void Tint(PdfColorSpace alternate, PdfFunction tint, int inputs)
    {
        if (alternate == null || tint == null)
            throw new ArgumentNullException(nameof(alternate));
        if (!alternate.SupportsBlending || alternate.IsPattern || tint.InputComponents != inputs || tint.OutputComponents != alternate.Components)
            throw new ArgumentException("The tint transform must match its input names and nonspecial alternate color space.");
    }

    private static double[] UnitRanges(int components)
    {
        var range = new double[components * 2];
        for (int i = 0; i < components; i++)
            range[i * 2 + 1] = 1;
        return range;
    }

    private static void White(double x, double z)
    {
        PdfGraphicsNumbers.Finite(x);
        PdfGraphicsNumbers.Finite(z);
        if (x <= 0 || z <= 0)
            throw new ArgumentOutOfRangeException(nameof(x));
    }

    private static void Black(double x, double y, double z)
    {
        PdfGraphicsNumbers.Finite(x);
        PdfGraphicsNumbers.Finite(y);
        PdfGraphicsNumbers.Finite(z);
        if (x < 0 || y < 0 || z < 0)
            throw new ArgumentOutOfRangeException(nameof(x));
    }
}

/// <summary>Finite exact channel values in an explicitly selected immutable color space.</summary>
public sealed class PdfColor
{
    /// <summary>Captures matching bounded channels. Pattern names are supplied separately when painting a pattern.</summary>
    public PdfColor(PdfColorSpace colorSpace, params double[] components)
    {
        ColorSpace = colorSpace ?? throw new ArgumentNullException(nameof(colorSpace));
        if (components == null || components.Length != colorSpace.Components)
            throw new ArgumentException("Color channels must match their space.", nameof(components));
        for (int i = 0; i < components.Length; i++)
        {
            PdfGraphicsNumbers.Finite(components[i]);
            if (components[i] < colorSpace.Ranges[i * 2] || components[i] > colorSpace.Ranges[i * 2 + 1])
                throw new ArgumentOutOfRangeException(nameof(components));
        }

        Components = new ReadOnlyCollection<double>((double[])components.Clone());
    }

    /// <summary>Gets the exact color space.</summary>
    public PdfColorSpace ColorSpace { get; }
    /// <summary>Gets immutable validated component values.</summary>
    public IReadOnlyList<double> Components { get; }
}

internal static class PdfIccHeader
{
    internal static string MinimumVersion(PdfByteStore bytes)
    {
        int major = bytes.At(8), minor = bytes.At(9) >> 4;
        return major == 2 ? minor <= 1 ? "1.3" : minor <= 3 ? "1.4" : minor == 4 ? "1.5" : "2.0" : minor == 0 ? "1.5" : minor == 1 ? "1.6" : minor == 2 ? "1.7" : "2.0";
    }

    internal static int Validate(PdfByteStore bytes, PdfOperationContext context)
    {
        if (bytes.Length < 132 || bytes.Length > uint.MaxValue || U32(0) != (ulong)bytes.Length || Tag(36) != "acsp")
            throw Error("Graphics.IccHeader", "ICC length or signature is invalid.");
        int major = bytes.At(8);
        if (major != 2 && major != 4)
            throw Error("Graphics.IccVersion", "Only explicitly supported ICC v2/v4 profiles can be embedded through this color-space factory.");
        if ((bytes.At(9) >> 4) > 9 || (bytes.At(9) & 15) > 9 || bytes.At(10) != 0 || bytes.At(11) != 0)
            throw Error("Graphics.IccVersion", "The ICC version encoding is malformed.");
        string color = Tag(16), pcs = Tag(20), kind = Tag(12);
        int components = color == "GRAY" ? 1 : color == "RGB " ? 3 : color == "CMYK" ? 4 : 0;
        if (components == 0 || pcs != "XYZ " && pcs != "Lab " || kind != "scnr" && kind != "mntr" && kind != "prtr" && kind != "spac" && kind != "abst")
            throw Error("Graphics.IccProfile", "The ICC channel/connection-space/profile class is unsupported.");
        if (U32(64) > 3 || U32(68) != 0xf6d6 || U32(72) != 0x10000 || U32(76) != 0xd32d)
            throw Error("Graphics.IccHeader", "ICC rendering intent or required D50 illuminant is invalid.");
        uint count = U32(128);
        if (count == 0 || 132L + count * 12L > bytes.Length)
            throw Error("Graphics.IccTags", "The ICC tag table is truncated.");
        context.Consume(PdfResourceKind.Objects, count);
        context.Consume(PdfResourceKind.ExpandedBytes, count * 320L + 128);
        var spans = new List<(long First, long End)>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (uint i = 0; i < count; i++)
        {
            context.Checkpoint();
            long at = 132L + i * 12L;
            string name = Tag(at);
            uint first = U32(at + 4), length = U32(at + 8);
            if (!names.Add(name) || first % 4 != 0 || first < 132L + count * 12L || length < 8 || (ulong)first + length > (ulong)bytes.Length)
                throw Error("Graphics.IccTags", "An ICC tag is duplicated, unaligned or outside its profile.");
            spans.Add((first, (long)first + length));
        }

        var sorted = spans.OrderBy(x => x.First).ThenBy(x => x.End).ToArray();
        for (int i = 1; i < sorted.Length; i++)
            if (sorted[i].First < sorted[i - 1].End && sorted[i] != sorted[i - 1])
                throw Error("Graphics.IccTags", "ICC tag storage overlaps without exact shared tag data.");
        if (!names.Contains("wtpt") || !(names.Contains("A2B0") || components == 1 && names.Contains("kTRC") || components == 3 && new[]
        {
            "rTRC",
            "gTRC",
            "bTRC",
            "rXYZ",
            "gXYZ",
            "bXYZ"
        }.All(names.Contains)))
            throw Error("Graphics.IccTransform", "The ICC profile has no supported complete input transform declaration.");
        return components;
        uint U32(long at) => ((uint)bytes.At(at) << 24) | ((uint)bytes.At(at + 1) << 16) | ((uint)bytes.At(at + 2) << 8) | (uint)bytes.At(at + 3);
        string Tag(long at) => new(new[] { (char)bytes.At(at), (char)bytes.At(at + 1), (char)bytes.At(at + 2), (char)bytes.At(at + 3) });
        PdfException Error(string code, string message) => PdfWriterCore.Error(context, code, message);
    }
}
