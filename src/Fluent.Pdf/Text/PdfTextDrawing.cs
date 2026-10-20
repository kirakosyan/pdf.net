using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An eagerly embedded positioned text program and its named font resources, ready for page/canvas composition.</summary>
public sealed class PdfTextDrawing
{
    private PdfTextDrawing(PdfContentProgram content, Dictionary<string, PdfFontResource> fonts)
    {
        Content = content;
        Fonts = new ReadOnlyDictionary<string, PdfFontResource>(fonts);
    }

    /// <summary>Gets exact positioned glyph operators wrapped in logical ActualText.</summary>
    public PdfContentProgram Content { get; }
    /// <summary>Gets all named embedded resources required by the program.</summary>
    public IReadOnlyDictionary<string, PdfFontResource> Fonts { get; }
    /// <summary>Gets the successful bounded construction report.</summary>
    public PdfOperationReport OperationReport { get; private set; } = null!;

    /// <summary>Creates explicit glyph matrices in PDF user coordinates. Paragraphs advance by the supplied positive line height.</summary>
    public static PdfTextDrawing Create(PdfShapedText text, double x, double y, double fontSize, double lineHeight = 1.2, string resourcePrefix = "FT", bool subset = true, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        foreach (double value in new[]
        {
            x,
            y,
            fontSize,
            lineHeight
        }

        )
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentException("Finite text geometry is required.");
        if (fontSize <= 0 || lineHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        if (string.IsNullOrEmpty(resourcePrefix) || resourcePrefix.Length > 64 || resourcePrefix.Any(c => !(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z')))
            throw new ArgumentException("A bounded ASCII letter resource prefix is required.", nameof(resourcePrefix));
        PdfTextDrawing? result = null;
        var report = PdfFont.Run("CreateTextDrawing", "managed.text", policy, cancellationToken, context => result = CreateCore(text, x, y, fontSize, lineHeight, resourcePrefix, subset, context), PdfCapability.ShapeText);
        result!.OperationReport = report;
        result.Content.OperationReport = report;
        foreach (var font in result.Fonts.Values)
            font.OperationReport = report;
        return result;
    }

    internal static PdfTextDrawing CreateCore(PdfShapedText text, double x, double y, double size, double lineHeight, string prefix, bool subset, PdfOperationContext context, IReadOnlyDictionary<PdfFont, PdfFontResource>? sharedFonts = null)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, text.Unicode.Text.Length * 4L + 256);
        var actual = new byte[text.Unicode.Text.Length * 2 + 2];
        actual[0] = 254;
        actual[1] = 255;
        for (int i = 0; i < text.Unicode.Text.Length; i++)
        {
            actual[i * 2 + 2] = (byte)(text.Unicode.Text[i] >> 8);
            actual[i * 2 + 3] = (byte)text.Unicode.Text[i];
        }

        var operations = new List<PdfContentOperation>();
        var fonts = new Dictionary<string, PdfFontResource>(StringComparer.Ordinal);
        operations.Add(new PdfContentOperation(PdfOperator.SaveState));
        operations.Add(new PdfContentOperation(PdfOperator.BeginMarkedContentProperties, PdfValue.Name("Span"), PdfValue.Dictionary(new[] { new KeyValuePair<string, PdfValue>("ActualText", PdfValue.String(actual)) })));
        operations.Add(new PdfContentOperation(PdfOperator.BeginText));
        double penX = x, penY = y;
        int paragraph = -1;
        foreach (var run in text.Runs)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.ExpandedBytes, run.Glyphs.Count * 320L + 256);
            if (run.ParagraphIndex != paragraph)
            {
                paragraph = run.ParagraphIndex;
                penX = text.Options.Vertical ? x - paragraph * size * lineHeight : x;
                penY = text.Options.Vertical ? y : y - paragraph * size * lineHeight;
            }

            var mappings = Mappings(run, context);

            // Positions already include vertical origins; horizontal CID encoding avoids applying a second origin displacement.
            var resource = sharedFonts == null ? PdfFontResource.CreateCore(run.Request.Font, mappings, subset, false, context) : sharedFonts[run.Request.Font];
            string name = prefix + fonts.Count.ToString(CultureInfo.InvariantCulture);
            fonts.Add(name, resource);
            operations.Add(new PdfContentOperation(PdfOperator.Font, PdfValue.Name(name), Number(size)));
            var bytes = sharedFonts == null ? resource.EncodedText.StringBytes : resource.EncodeMappings(mappings, context).StringBytes;
            int width = bytes.Count / run.Glyphs.Count;
            double scale = size / run.Request.Font.Info.Metrics.UnitsPerEm;
            for (int i = 0; i < run.Glyphs.Count; i++)
            {
                context.Checkpoint();
                var glyph = run.Glyphs[i];
                var code = new byte[width];
                for (int b = 0; b < width; b++)
                    code[b] = bytes[i * width + b];
                operations.Add(new PdfContentOperation(PdfOperator.TextMatrix, PdfValue.Integer(1), PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(1), Number(penX + glyph.OffsetX * scale), Number(penY + glyph.OffsetY * scale)));
                operations.Add(new PdfContentOperation(PdfOperator.ShowText, PdfValue.String(code)));
                penX += glyph.AdvanceX * scale;
                penY += glyph.AdvanceY * scale;
            }
        }

        operations.Add(new PdfContentOperation(PdfOperator.EndText));
        operations.Add(new PdfContentOperation(PdfOperator.EndMarkedContent));
        operations.Add(new PdfContentOperation(PdfOperator.RestoreState));
        return new PdfTextDrawing(PdfContentProgram.CreateCore(operations, PdfContentKind.Page, context), fonts);
    }

    internal static IReadOnlyDictionary<PdfFont, PdfFontResource> SharedFonts(IEnumerable<PdfShapedText> texts, PdfOperationContext context)
    {
        var mappings = new Dictionary<PdfFont, List<PdfGlyphMapping>>();
        foreach (var text in texts)
            foreach (var run in text.Runs)
            {
                context.Checkpoint();
                if (!mappings.TryGetValue(run.Request.Font, out var items))
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, 128);
                    mappings.Add(run.Request.Font, items = new());
                }
                items.AddRange(Mappings(run, context));
            }
        var resources = new Dictionary<PdfFont, PdfFontResource>();
        foreach (var pair in mappings)
            resources.Add(pair.Key, PdfFontResource.CreateCore(pair.Key, pair.Value, true, false, context));
        return resources;
    }

    private static List<PdfGlyphMapping> Mappings(PdfShapedRun run, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, run.Glyphs.Count * 128L + 256);
        var clusters = run.Glyphs.Select(g => g.Cluster).Distinct().OrderBy(v => v).ToArray();
        var ends = new Dictionary<int, int>();
        for (int i = 0; i < clusters.Length; i++)
            ends.Add(clusters[i], i + 1 < clusters.Length ? clusters[i + 1] : run.Request.Start + run.Request.Length);
        var seen = new HashSet<int>();
        var mappings = new List<PdfGlyphMapping>();
        foreach (var glyph in run.Glyphs)
        {
            context.Checkpoint();
            int length = ends[glyph.Cluster] - glyph.Cluster;
            if (length > 512)
                throw PdfWriterCore.Error(context, "Text.ClusterEncodingLimit", "A logical glyph cluster exceeds the font resource mapping limit.");
            mappings.Add(new PdfGlyphMapping(glyph.GlyphIndex, seen.Add(glyph.Cluster) ? run.Request.Text.Substring(glyph.Cluster, length) : string.Empty));
        }

        return mappings;
    }

    private static PdfValue Number(double value) => PdfValues.Real(value);
}
