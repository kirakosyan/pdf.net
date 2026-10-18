using System.Collections.ObjectModel;
using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable, explicitly loaded font face with fresh budgets for each public operation.</summary>
public sealed partial class PdfFont
{
    internal IPdfFontProgram Program { get; }

    internal PdfFont(IPdfFontProgram program, string providerId)
    {
        Program = program;
        Info = program.Info;
        ProviderId = providerId;
    }

    /// <summary>Gets immutable font identity, rights and metrics.</summary>
    public PdfFontInfo Info { get; }
    /// <summary>Gets the selected font provider identity.</summary>
    public string ProviderId { get; }
    /// <summary>Gets the successful loading report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;

    /// <summary>Reads an explicitly selected face from borrowed font bytes; no machine font discovery occurs.</summary>
    public static PdfFont Read(Stream input, PdfProviderRegistry providers, int faceIndex = 0, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (!input.CanRead)
            throw new ArgumentException("A readable font stream is required.", nameof(input));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        if (faceIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(faceIndex));
        var provider = providers.GetRequired<IPdfFontProvider>(PdfCapability.Fonts);
        PdfFont? result = null;
        var report = Run("ReadFont", providers.GetIdentifier(PdfCapability.Fonts), policy, cancellationToken, context =>
        {
            using var lease = new BorrowedStream(input, true, context);
            var program = provider.Read(lease, faceIndex, context) ?? throw PdfWriterCore.Error(context, "Font.ProviderResult", "The font provider returned no face.");
            if (program.Info == null)
                throw PdfWriterCore.Error(context, "Font.ProviderResult", "The font provider returned no immutable identity.");
            result = new PdfFont(program, context.ProviderId);
        });
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Selects an exact base-14 PostScript font name through an explicit provider.</summary>
    public static PdfFont Standard(string postScriptName, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (postScriptName == null)
            throw new ArgumentNullException(nameof(postScriptName));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        var provider = providers.GetRequired<IPdfFontProvider>(PdfCapability.Fonts);
        PdfFont? result = null;
        var report = Run("SelectStandardFont", providers.GetIdentifier(PdfCapability.Fonts), policy, cancellationToken, context =>
        {
            var program = provider.Standard(postScriptName, context) ?? throw PdfWriterCore.Error(context, "Font.ProviderResult", "The font provider returned no standard face.");
            if (program.Info == null || program.Info.Format != PdfFontFormat.Standard)
                throw PdfWriterCore.Error(context, "Font.ProviderResult", "The provider returned an invalid standard-font identity.");
            result = new PdfFont(program, context.ProviderId);
        });
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Queries one scalar. Glyph zero explicitly reports an unavailable mapping without substituting another font.</summary>
    public PdfFontGlyph GetGlyph(int unicodeScalar, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Scalar(unicodeScalar);
        PdfFontGlyph? result = null;
        var report = Run("QueryFontGlyph", ProviderId, policy, cancellationToken, context =>
        {
            int glyph = MapCore(unicodeScalar, context);
            result = new PdfFontGlyph(this, unicodeScalar, glyph, Program.GetAdvance(glyph, context), VerticalCore(glyph, context), Program.GetGlyphName(glyph, context));
        });
        result!.OperationReport = report;
        return result;
    }

    internal int MapCore(int scalar, PdfOperationContext context)
    {
        context.Checkpoint();
        int glyph = Program.MapGlyph(scalar, context);
        if (glyph < 0 || glyph >= Info.GlyphCount)
            throw PdfWriterCore.Error(context, "Font.ProviderGlyph", "A provider returned a glyph outside its declared range.");
        return glyph;
    }

    internal double? VerticalCore(int glyph, PdfOperationContext context) => Program is IPdfVerticalFontProgram vertical && vertical.SupportsVerticalMetrics ? vertical.GetVerticalMetrics(glyph, context).Advance : (double? )null;
    /// <summary>Writes a full or dependency-complete subset font to borrowed output under the declared embedding restrictions.</summary>
    public PdfFontWriteResult WriteProgram(Stream output, IEnumerable<int> glyphs, bool subset = true, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (!output.CanWrite)
            throw new ArgumentException("A writable stream is required.", nameof(output));
        if (glyphs == null)
            throw new ArgumentNullException(nameof(glyphs));
        PdfFontProgramWriteResult? result = null;
        var report = Run("WriteFontProgram", ProviderId, policy, cancellationToken, context =>
        {
            var selected = new List<int>();
            foreach (int glyph in glyphs)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 16);
                if (glyph < 0 || glyph >= Info.GlyphCount)
                    throw new ArgumentOutOfRangeException(nameof(glyphs));
                selected.Add(glyph);
            }

            EmbeddingAllowed(subset, context);
            using var lease = new BorrowedStream(output, false, context);
            result = Program.Write(lease, selected.AsReadOnly(), subset, context) ?? throw PdfWriterCore.Error(context, "Font.ProviderResult", "The font provider returned no emitted-program details.");
        });
        return new PdfFontWriteResult(result!, report);
    }

    internal void EmbeddingAllowed(bool subset, PdfOperationContext context)
    {
        if (Info.Format == PdfFontFormat.Standard)
            return;
        if ((Info.EmbeddingRights & (PdfFontEmbeddingRights.Restricted | PdfFontEmbeddingRights.BitmapOnly)) != 0)
            throw PdfWriterCore.Error(context, "Font.EmbeddingRestricted", "The font's declared rights forbid outline embedding.");
        if (subset && (Info.EmbeddingRights & PdfFontEmbeddingRights.NoSubsetting) != 0)
            throw PdfWriterCore.Error(context, "Font.SubsettingRestricted", "The font's declared rights forbid subsetting.");
    }

    internal static PdfOperationReport Run(string operation, string providerId, PdfOperationPolicy? policy, CancellationToken token, Action<PdfOperationContext> action, PdfCapability capability = PdfCapability.Fonts)
    {
        var context = new PdfOperationContext(operation, providerId, capability, policy ?? PdfOperationPolicy.Default, token);
        bool success = false;
        PdfOperationReport report;
        try
        {
            context.Checkpoint();
            action(context);
            context.EnsureComplete();
            success = true;
        }
        catch
        {
            context.Checkpoint();
            throw;
        }
        finally
        {
            report = context.Finish(success);
            context.DisposeSources();
        }

        return report;
    }

    internal static void Scalar(int scalar)
    {
        if (scalar < 0 || scalar > 0x10ffff || scalar >= 0xd800 && scalar <= 0xdfff)
            throw new ArgumentOutOfRangeException(nameof(scalar));
    }
}

/// <summary>An exact font/glyph mapping and its unscaled advances; zero is the explicit missing-glyph identity.</summary>
public sealed class PdfFontGlyph
{
    internal PdfFontGlyph(PdfFont font, int scalar, int glyph, double advance, double? vertical, string name)
    {
        Font = font;
        UnicodeScalar = scalar;
        GlyphIndex = glyph;
        Advance = advance;
        VerticalAdvance = vertical;
        GlyphName = name;
    }

    /// <summary>Gets the explicitly selected font.</summary>
    public PdfFont Font { get; }
    /// <summary>Gets the queried Unicode scalar.</summary>
    public int UnicodeScalar { get; }
    /// <summary>Gets its glyph identity, or zero when unavailable.</summary>
    public int GlyphIndex { get; }
    /// <summary>Gets the unscaled horizontal advance.</summary>
    public double Advance { get; }
    /// <summary>Gets the unscaled vertical advance.</summary>
    public double? VerticalAdvance { get; }
    /// <summary>Gets the provider's glyph name.</summary>
    public string GlyphName { get; }
    /// <summary>Gets the successful query or fallback-resolution report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;
}

/// <summary>Emitted font program details and the complete successful operation report.</summary>
public sealed class PdfFontWriteResult
{
    internal PdfFontWriteResult(PdfFontProgramWriteResult program, PdfOperationReport report)
    {
        Program = program;
        OperationReport = report;
    }

    /// <summary>Gets the emitted program format details.</summary>
    public PdfFontProgramWriteResult Program { get; }
    /// <summary>Gets the successful bounded-write report.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>An immutable caller-ordered font fallback list; resolution never consults operating-system font order.</summary>
public sealed class PdfFontFallback
{
    /// <summary>Captures up to 256 explicitly loaded fonts in deterministic preference order.</summary>
    public PdfFontFallback(params PdfFont[] fonts)
    {
        if (fonts == null)
            throw new ArgumentNullException(nameof(fonts));
        if (fonts.Length == 0 || fonts.Length > 256 || fonts.Any(x => x == null))
            throw new ArgumentException("Supply one through 256 nonnull fonts.", nameof(fonts));
        Fonts = new ReadOnlyCollection<PdfFont>((PdfFont[])fonts.Clone());
    }

    /// <summary>Gets the captured preference order.</summary>
    public IReadOnlyList<PdfFont> Fonts { get; }

    /// <summary>Resolves one scalar to the first actual glyph and fails explicitly when every font lacks it.</summary>
    public PdfFontGlyph Resolve(int unicodeScalar, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfFont.Scalar(unicodeScalar);
        PdfFontGlyph? result = null;
        var report = PdfFont.Run("ResolveFontFallback", "core.fonts", policy, cancellationToken, context => result = ResolveCore(unicodeScalar, context));
        result!.OperationReport = report;
        return result;
    }

    internal PdfFontGlyph ResolveCore(int scalar, PdfOperationContext context)
    {
        foreach (var font in Fonts)
        {
            context.Checkpoint();
            int glyph = font.MapCore(scalar, context);
            if (glyph == 0)
                continue;
            return new PdfFontGlyph(font, scalar, glyph, font.Program.GetAdvance(glyph, context), font.VerticalCore(glyph, context), font.Program.GetGlyphName(glyph, context));
        }

        throw PdfWriterCore.Error(context, "Font.MissingGlyph", "No explicitly configured fallback font maps the requested Unicode scalar.");
    }
}
