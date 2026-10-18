using System.Collections.ObjectModel;
using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable typed PDF operator and its direct, uninterpreted operands.</summary>
public sealed class PdfContentOperation
{
    /// <summary>Creates a standard operator after validating its operand grammar.</summary>
    public PdfContentOperation(PdfOperator operation, params PdfValue[] operands) : this(operation, PdfOperatorGrammar.Token(operation), operands, PdfOperatorGrammar.Version(operation))
    {
        PdfOperatorGrammar.Validate(this);
    }

    private PdfContentOperation(PdfOperator operation, string token, PdfValue[] operands, string version)
    {
        if (operands == null)
            throw new ArgumentNullException(nameof(operands));
        if (operands.Any(x => x == null))
            throw new ArgumentException("Operands cannot be CLR null.", nameof(operands));
        Operation = operation;
        Token = token;
        Operands = new ReadOnlyCollection<PdfValue>((PdfValue[])operands.Clone());
        MinimumVersion = version;
    }

    /// <summary>Creates an explicitly preserved extension token with a caller-declared version floor.</summary>
    public static PdfContentOperation Extension(string token, string minimumVersion, params PdfValue[] operands)
    {
        PdfVersionNumber.Parse(minimumVersion);
        if (string.IsNullOrEmpty(token) || token.Length > 128 || token.Any(c => c < 33 || c > 126 || "()<>[]{}/%".IndexOf(c) >= 0) || token == "true" || token == "false" || token == "null" || token == "BI" || token == "ID" || token == "EI" || PdfOperatorGrammar.TryGet(token, out _))
            throw new ArgumentException("A distinct regular ASCII extension operator token is required.", nameof(token));
        bool numeric = true;
        try
        {
            PdfValue.Number(token);
        }
        catch (ArgumentException)
        {
            numeric = false;
        }

        if (numeric)
            throw new ArgumentException("An operator cannot be a numeric token.", nameof(token));
        var result = new PdfContentOperation(PdfOperator.Unknown, token, operands, minimumVersion);
        foreach (var operand in result.Operands)
            PdfOperatorGrammar.Direct(operand);
        return result;
    }

    /// <summary>Gets the standard operator kind, or Unknown for an explicit extension.</summary>
    public PdfOperator Operation { get; }
    /// <summary>Gets its exact operator spelling.</summary>
    public string Token { get; }
    /// <summary>Gets defensively copied direct operands.</summary>
    public IReadOnlyList<PdfValue> Operands { get; }
    /// <summary>Gets the minimum declared version required by this operator.</summary>
    public string MinimumVersion { get; }
    internal PdfValue? Image { get; private set; }
    /// <summary>Gets the immutable inline-image dictionary, or null for a non-image operator.</summary>
    public PdfValue? InlineImageDictionary => Image?.DictionaryView();
    /// <summary>Gets the encoded inline-image byte count, or zero for a non-image operator.</summary>
    public long InlineImageLength => Image?.RawStreamLength ?? 0;

    /// <summary>Opens an independent read-only view of this inline image's encoded bytes.</summary>
    public Stream OpenInlineImageData() => Image?.OpenRawStream() ?? throw new InvalidOperationException("This operator is not an inline image.");
    /// <summary>Snapshots an inline image and validates its exact encoded boundary under a fresh operation budget.</summary>
    public static PdfContentOperation InlineImage(PdfValue dictionary, Stream encodedInput, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (dictionary == null)
            throw new ArgumentNullException(nameof(dictionary));
        if (dictionary.Kind != PdfValueKind.Dictionary)
            throw new ArgumentException("A direct dictionary is required.", nameof(dictionary));
        if (encodedInput == null)
            throw new ArgumentNullException(nameof(encodedInput));
        if (!encodedInput.CanRead)
            throw new ArgumentException("A readable stream is required.", nameof(encodedInput));
        PdfContentOperation? result = null;
        PdfContentProgram.Run("CreateInlineImage", policy, cancellationToken, context =>
        {
            using var input = new BorrowedStream(encodedInput, true, context);
            var bytes = PdfByteStore.Read(input, context);
            result = ImageOperation(PdfValue.StreamValue(dictionary, bytes, 0, bytes.Length));
            PdfInlineImage.Validate(result, context);
            long length = PdfInlineImage.DataLength(dictionary, bytes, 0, context);
            if (length != bytes.Length)
                throw PdfContentState.Error(context, "Content.InlineLength", "Trailing bytes follow the encoded inline image.");
        });
        return result!;
    }

    internal static PdfContentOperation ImageOperation(PdfValue image) => new(PdfOperator.InlineImage, "BI", System.Array.Empty<PdfValue>(), "1.2")
    {
        Image = image
    };
}

internal static class PdfOperatorGrammar
{
    private static readonly (PdfOperator Kind, string Token, string Grammar, string Version)[] Definitions =
    {
        (PdfOperator.SaveState, "q", "", "1.2"),
        (PdfOperator.RestoreState, "Q", "", "1.2"),
        (PdfOperator.Transform, "cm", "nnnnnn", "1.2"),
        (PdfOperator.LineWidth, "w", "n", "1.2"),
        (PdfOperator.LineCap, "J", "n", "1.2"),
        (PdfOperator.LineJoin, "j", "n", "1.2"),
        (PdfOperator.MiterLimit, "M", "n", "1.2"),
        (PdfOperator.Dash, "d", "an", "1.2"),
        (PdfOperator.RenderingIntent, "ri", "N", "1.2"),
        (PdfOperator.Flatness, "i", "n", "1.2"),
        (PdfOperator.ExtendedState, "gs", "N", "1.2"),
        (PdfOperator.MoveTo, "m", "nn", "1.2"),
        (PdfOperator.LineTo, "l", "nn", "1.2"),
        (PdfOperator.CurveTo, "c", "nnnnnn", "1.2"),
        (PdfOperator.CurveFromCurrent, "v", "nnnn", "1.2"),
        (PdfOperator.CurveToEndpoint, "y", "nnnn", "1.2"),
        (PdfOperator.ClosePath, "h", "", "1.2"),
        (PdfOperator.Rectangle, "re", "nnnn", "1.2"),
        (PdfOperator.Stroke, "S", "", "1.2"),
        (PdfOperator.CloseStroke, "s", "", "1.2"),
        (PdfOperator.Fill, "f", "", "1.2"),
        (PdfOperator.LegacyFill, "F", "", "1.2"),
        (PdfOperator.FillEvenOdd, "f*", "", "1.2"),
        (PdfOperator.FillStroke, "B", "", "1.2"),
        (PdfOperator.FillStrokeEvenOdd, "B*", "", "1.2"),
        (PdfOperator.CloseFillStroke, "b", "", "1.2"),
        (PdfOperator.CloseFillStrokeEvenOdd, "b*", "", "1.2"),
        (PdfOperator.EndPath, "n", "", "1.2"),
        (PdfOperator.Clip, "W", "", "1.2"),
        (PdfOperator.ClipEvenOdd, "W*", "", "1.2"),
        (PdfOperator.BeginText, "BT", "", "1.2"),
        (PdfOperator.EndText, "ET", "", "1.2"),
        (PdfOperator.CharacterSpacing, "Tc", "n", "1.2"),
        (PdfOperator.WordSpacing, "Tw", "n", "1.2"),
        (PdfOperator.HorizontalScale, "Tz", "n", "1.2"),
        (PdfOperator.Leading, "TL", "n", "1.2"),
        (PdfOperator.Font, "Tf", "Nn", "1.2"),
        (PdfOperator.TextRenderingMode, "Tr", "n", "1.2"),
        (PdfOperator.TextRise, "Ts", "n", "1.2"),
        (PdfOperator.MoveText, "Td", "nn", "1.2"),
        (PdfOperator.MoveTextLeading, "TD", "nn", "1.2"),
        (PdfOperator.TextMatrix, "Tm", "nnnnnn", "1.2"),
        (PdfOperator.NextTextLine, "T*", "", "1.2"),
        (PdfOperator.ShowText, "Tj", "s", "1.2"),
        (PdfOperator.ShowPositionedText, "TJ", "a", "1.2"),
        (PdfOperator.NextLineShowText, "'", "s", "1.2"),
        (PdfOperator.SpacedNextLineShowText, "\"", "nns", "1.2"),
        (PdfOperator.StrokeColorSpace, "CS", "N", "1.2"),
        (PdfOperator.FillColorSpace, "cs", "N", "1.2"),
        (PdfOperator.StrokeColor, "SC", "c", "1.2"),
        (PdfOperator.StrokeColorExtended, "SCN", "C", "1.2"),
        (PdfOperator.FillColor, "sc", "c", "1.2"),
        (PdfOperator.FillColorExtended, "scn", "C", "1.2"),
        (PdfOperator.StrokeGray, "G", "n", "1.2"),
        (PdfOperator.FillGray, "g", "n", "1.2"),
        (PdfOperator.StrokeRgb, "RG", "nnn", "1.2"),
        (PdfOperator.FillRgb, "rg", "nnn", "1.2"),
        (PdfOperator.StrokeCmyk, "K", "nnnn", "1.2"),
        (PdfOperator.FillCmyk, "k", "nnnn", "1.2"),
        (PdfOperator.Shading, "sh", "N", "1.3"),
        (PdfOperator.DrawObject, "Do", "N", "1.2"),
        (PdfOperator.GlyphWidth, "d0", "nn", "1.2"),
        (PdfOperator.GlyphBounds, "d1", "nnnnnn", "1.2"),
        (PdfOperator.MarkPoint, "MP", "N", "1.2"),
        (PdfOperator.MarkPointProperties, "DP", "Np", "1.2"),
        (PdfOperator.BeginMarkedContent, "BMC", "N", "1.2"),
        (PdfOperator.BeginMarkedContentProperties, "BDC", "Np", "1.2"),
        (PdfOperator.EndMarkedContent, "EMC", "", "1.2"),
        (PdfOperator.BeginCompatibility, "BX", "", "1.2"),
        (PdfOperator.EndCompatibility, "EX", "", "1.2")
    };
    internal static string Token(PdfOperator kind) => Definition(kind).Token;
    internal static string Version(PdfOperator kind) => Definition(kind).Version;
    internal static bool TryGet(string token, out PdfOperator kind)
    {
        foreach (var item in Definitions)
            if (item.Token == token)
            {
                kind = item.Kind;
                return true;
            }

        kind = PdfOperator.Unknown;
        return false;
    }

    private static (PdfOperator Kind, string Token, string Grammar, string Version) Definition(PdfOperator kind)
    {
        foreach (var item in Definitions)
            if (item.Kind == kind)
                return item;
        throw new ArgumentOutOfRangeException(nameof(kind), "A standard non-image operator is required.");
    }

    internal static void Validate(PdfContentOperation operation)
    {
        var definition = Definition(operation.Operation);
        var operands = operation.Operands;
        string grammar = definition.Grammar;
        foreach (var value in operands)
            Direct(value);
        if (grammar == "c" || grammar == "C")
        {
            int colors = operands.Count;
            if (grammar == "C" && colors > 0 && operands[colors - 1].Kind == PdfValueKind.Name)
                colors--;
            if (colors < 0 || colors > 32 || (colors == 0 && operands.Count == 0))
                throw new ArgumentException("Color operands require components or a pattern name.");
            for (int i = 0; i < colors; i++)
                Number(operands[i]);
            return;
        }

        if (operands.Count != grammar.Length)
            throw new ArgumentException("The operator has an incorrect operand count.");
        for (int i = 0; i < grammar.Length; i++)
        {
            var value = operands[i];
            char rule = grammar[i];
            if (rule == 'n')
                Number(value);
            else if (rule == 'N' && value.Kind != PdfValueKind.Name || rule == 's' && value.Kind != PdfValueKind.String || rule == 'a' && value.Kind != PdfValueKind.Array || rule == 'p' && value.Kind != PdfValueKind.Name && value.Kind != PdfValueKind.Dictionary)
                throw new ArgumentException("An operator operand has an incorrect kind.");
        }

        if (operation.Operation == PdfOperator.ShowPositionedText)
            foreach (var item in operands[0].Items)
            {
                if (item.Kind == PdfValueKind.Number)
                    Number(item);
                else if (item.Kind != PdfValueKind.String)
                    throw new ArgumentException("TJ arrays contain only strings and numeric adjustments.");
            }

        if (operation.Operation == PdfOperator.Dash)
        {
            bool positive = operands[0].Items.Count == 0;
            foreach (var item in operands[0].Items)
            {
                double n = Number(item);
                if (n < 0)
                    throw new ArgumentException("Dash lengths cannot be negative.");
                positive |= n > 0;
            }

            if (!positive)
                throw new ArgumentException("A nonempty dash pattern cannot consist entirely of zeros.");
        }

        if (operation.Operation == PdfOperator.LineWidth && Number(operands[0]) < 0)
            throw new ArgumentException("Line width cannot be negative.");
        if (operation.Operation == PdfOperator.MiterLimit && Number(operands[0]) < 1)
            throw new ArgumentException("Miter limit must be at least one.");
        if (operation.Operation == PdfOperator.LineCap || operation.Operation == PdfOperator.LineJoin)
            Integer(operands[0], 0, 2);
        if (operation.Operation == PdfOperator.TextRenderingMode)
            Integer(operands[0], 0, 7);
        if (operation.Operation == PdfOperator.Flatness && (Number(operands[0]) < 0 || Number(operands[0]) > 100))
            throw new ArgumentException("Flatness is outside 0–100.");
    }

    internal static void Direct(PdfValue value)
    {
        var pending = new Stack<PdfValue>();
        pending.Push(value);
        while (pending.Count > 0)
        {
            var item = pending.Pop();
            if (item.Kind == PdfValueKind.Reference || item.Kind == PdfValueKind.Stream)
                throw new ArgumentException("Content operands cannot contain indirect references or streams.");
            if (item.Kind == PdfValueKind.Number)
                Number(item);
            if (item.Kind == PdfValueKind.Array)
                foreach (var child in item.Items)
                    pending.Push(child);
            if (item.Kind == PdfValueKind.Dictionary)
                foreach (var child in item.Entries.Values)
                    pending.Push(child);
        }
    }

    private static double Number(PdfValue value)
    {
        try
        {
            return value.NumberValue;
        }
        catch (InvalidOperationException)
        {
            throw new ArgumentException("A finite numeric operand is required.");
        }
    }

    private static void Integer(PdfValue value, int minimum, int maximum)
    {
        double number = Number(value);
        if (number < minimum || number > maximum || number != Math.Floor(number))
            throw new ArgumentException("The integer operator operand is outside range.");
    }
}
