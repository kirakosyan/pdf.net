using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>A caller-authored Type 3 glyph with exact logical text and a validated glyph content program.</summary>
public sealed class PdfType3Glyph
{
    /// <summary>Captures a unique glyph name, Unicode text and a program whose first operator declares its width.</summary>
    public PdfType3Glyph(string name, string unicodeText, PdfContentProgram program)
    {
        Guard.Text(name, nameof(name), 127);
        PdfValue.Name(name);
        if (name == ".notdef")
            throw new ArgumentException("The missing glyph is supplied by the SDK.", nameof(name));
        if (program == null)
            throw new ArgumentNullException(nameof(program));
        if (program.Kind != PdfContentKind.Type3Glyph)
            throw new ArgumentException("A validated Type 3 glyph program is required.", nameof(program));
        _ = new PdfGlyphMapping(1, unicodeText);
        Name = name;
        UnicodeText = unicodeText;
        Program = program;
    }

    /// <summary>Gets the unique glyph name.</summary>
    public string Name { get; }
    /// <summary>Gets the exact logical Unicode sequence.</summary>
    public string UnicodeText { get; }
    /// <summary>Gets the immutable glyph procedure.</summary>
    public PdfContentProgram Program { get; }
}

public sealed partial class PdfFont
{
    /// <summary>Creates a bounded immutable Type 3 face from caller-authored glyph procedures and explicit resources.</summary>
    public static PdfFont Type3(string postScriptName, PdfFontMetrics metrics, IEnumerable<PdfType3Glyph> glyphs, PdfValue? resources = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (glyphs == null)
            throw new ArgumentNullException(nameof(glyphs));
        if (resources != null && resources.Kind != PdfValueKind.Dictionary)
            throw new ArgumentException("Type 3 resources must be a dictionary.", nameof(resources));
        PdfFont? result = null;
        var report = Run("CreateType3Font", "core.fonts", policy, cancellationToken, context => result = new PdfFont(new PdfType3FontProgram(postScriptName, metrics, glyphs, resources, context), "core.fonts"));
        result!.OperationReport = report;
        return result;
    }
}

internal sealed class PdfType3FontProgram : IPdfFontProgram
{
    private readonly List<PdfType3Glyph?> _glyphs = new()
    {
        null
    };
    private readonly Dictionary<int, int> _unicode = new();
    internal PdfValue Resources { get; }
    internal string MinimumVersion { get; private set; } = "1.2";
    public PdfFontInfo Info { get; }

    internal PdfType3FontProgram(string name, PdfFontMetrics metrics, IEnumerable<PdfType3Glyph> glyphs, PdfValue? resources, PdfOperationContext context)
    {
        Resources = resources ?? PdfValue.Dictionary(new Dictionary<string, PdfValue>());
        var names = new HashSet<string>(StringComparer.Ordinal);
        using var buffer = new PdfCodecBufferStream(context, context.Available(PdfResourceKind.ExpandedBytes));
        var sink = new PdfWriterCore.Sink(buffer, context);
        sink.Value(PdfValue.Name(name), false);
        sink.Text("\n");
        sink.Value(Resources, false);
        sink.Text("\n");
        foreach (var glyph in glyphs)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 256);
            if (glyph == null || !names.Add(glyph.Name))
                throw new ArgumentException("Type 3 glyphs must be nonnull with unique names.", nameof(glyphs));
            if (_glyphs.Count == 65536)
                throw PdfWriterCore.Error(context, "Font.GlyphCount", "The Type 3 face exceeds 65,536 glyph identities.");
            PdfContentProgram.ValidateCore(glyph.Program.Operations, PdfContentKind.Type3Glyph, context);
            int id = _glyphs.Count;
            _glyphs.Add(glyph);
            string text = glyph.UnicodeText;
            var declaration = glyph.Program.Operations[0];
            if (declaration.Operands[1].NumberValue != 0)
                throw PdfWriterCore.Error(context, "Font.Type3Metrics", "This horizontal Type 3 face requires zero vertical glyph width.");
            if (declaration.Operation == PdfOperator.GlyphBounds && (declaration.Operands[2].NumberValue < metrics.Left || declaration.Operands[3].NumberValue < metrics.Bottom || declaration.Operands[4].NumberValue > metrics.Right || declaration.Operands[5].NumberValue > metrics.Top))
                throw PdfWriterCore.Error(context, "Font.Type3Bounds", "A glyph's declared bounds exceed the Type 3 font bounding box.");
            if (PdfVersionNumber.Parse(glyph.Program.MinimumVersion) > PdfVersionNumber.Parse(MinimumVersion))
                MinimumVersion = glyph.Program.MinimumVersion;
            if (text.Length == 1 && !char.IsSurrogate(text[0]))
                _unicode[(int)text[0]] = id;
            else if (text.Length == 2 && char.IsSurrogatePair(text, 0))
                _unicode[char.ConvertToUtf32(text, 0)] = id;
            sink.Value(PdfValue.Name(glyph.Name), false);
            sink.Value(PdfValue.String(Encoding.BigEndianUnicode.GetBytes(text)), false);
            sink.Text("\n");
            sink.Flush();
            glyph.Program.WriteCore(buffer, context);
        }

        sink.Text(metrics.UnitsPerEm.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + metrics.Left.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + metrics.Bottom.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + metrics.Right.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + metrics.Top.ToString(System.Globalization.CultureInfo.InvariantCulture));
        sink.Flush();
        var bytes = buffer.Finish();
        using var input = bytes.Open(0, bytes.Length);
        context.Consume(PdfResourceKind.ExpandedBytes, 4096);
        var block = new byte[4096];
        using var hash = SHA256.Create();
        int count;
        while ((count = input.Read(block, 0, block.Length)) > 0)
        {
            context.Checkpoint();
            hash.TransformBlock(block, 0, count, block, 0);
        }

        hash.TransformFinalBlock(System.Array.Empty<byte>(), 0, 0);
        string digest = string.Concat(hash.Hash!.Select(x => x.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
        Info = new PdfFontInfo(name, PdfFontFormat.Type3, _glyphs.Count, metrics, digest);
    }

    private void Check(int glyph, PdfOperationContext context)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        if (glyph < 0 || glyph >= _glyphs.Count)
            throw new ArgumentOutOfRangeException(nameof(glyph));
    }

    public int MapGlyph(int unicodeScalar, PdfOperationContext context)
    {
        PdfFont.Scalar(unicodeScalar);
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        return _unicode.TryGetValue(unicodeScalar, out int glyph) ? glyph : 0;
    }

    public double GetAdvance(int glyph, PdfOperationContext context)
    {
        Check(glyph, context);
        return glyph == 0 ? 0 : _glyphs[glyph]!.Program.Operations[0].Operands[0].NumberValue;
    }

    public string GetGlyphName(int glyph, PdfOperationContext context)
    {
        Check(glyph, context);
        return glyph == 0 ? ".notdef" : _glyphs[glyph]!.Name;
    }

    internal PdfContentProgram GlyphProgram(int glyph, PdfOperationContext context)
    {
        Check(glyph, context);
        if (glyph > 0)
            return _glyphs[glyph]!.Program;
        context.Consume(PdfResourceKind.ExpandedBytes, 256);
        return PdfContentProgram.CreateCore(new[] { new PdfContentOperation(PdfOperator.GlyphWidth, PdfValue.Integer(0), PdfValue.Integer(0)) }, PdfContentKind.Type3Glyph, context);
    }

    public PdfFontProgramWriteResult Write(Stream output, IReadOnlyList<int> glyphs, bool subset, PdfOperationContext context)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (glyphs == null)
            throw new ArgumentNullException(nameof(glyphs));
        foreach (int glyph in glyphs)
            Check(glyph, context);
        return new PdfFontProgramWriteResult(Info.PostScriptName);
    }
}
