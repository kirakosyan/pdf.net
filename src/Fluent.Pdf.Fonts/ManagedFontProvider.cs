using System.IO;

namespace Fluent.Pdf.Fonts;
/// <summary>Original managed offline font parsing, embedding/subsetting and exact standard-font metrics. No machine font discovery occurs.</summary>
public sealed class ManagedFontProvider : IPdfFontProvider, IPdfFontEncodingProvider, IPdfFontEncodingMetricsProvider
{
    /// <summary>Creates a stateless provider shareable by independent jobs.</summary>
    public ManagedFontProvider()
    {
    }

    /// <summary>Gets the shared stateless instance.</summary>
    public static ManagedFontProvider Instance { get; } = new();
    /// <summary>Gets this provider's explicit font service declaration.</summary>
    public PdfProviderDescriptor Descriptor { get; } = new("managed.fonts", "1.0.0", PdfCapability.Fonts);

    /// <summary>Reads an explicitly selected collection face from borrowed bytes under the mandatory operation budget.</summary>
    public IPdfFontProgram Read(Stream input, int faceIndex, PdfOperationContext context)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (faceIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(faceIndex));
        context.Checkpoint();
        byte[] bytes = FontData.Read(input, context);
        if (bytes.Length < 4)
            throw PdfWriterCore.Error(context, "Font.Truncated", "The supplied font has no complete binary header.");
        try
        {
            if (bytes[0] == 128 || bytes[0] == '%' && bytes[1] == '!')
            {
                if (faceIndex != 0)
                    throw PdfWriterCore.Error(context, "Font.CollectionFace", "A Type 1 program has only face zero.");
                return new Type1Font(bytes, context);
            }

            if (bytes[0] == 1 && bytes[2] >= 4)
            {
                if (faceIndex != 0)
                    throw PdfWriterCore.Error(context, "Font.CollectionFace", "A raw CFF font has only face zero.");
                return new RawCffFont(bytes, context);
            }

            return new SfntFont(bytes, faceIndex, context);
        }
        catch (OverflowException)
        {
            context.Checkpoint();
            throw PdfWriterCore.Error(context, "Font.ProgramRange", "Font offsets or counts overflow the supported program range.");
        }
        catch (ArgumentException)
        {
            context.Checkpoint();
            throw PdfWriterCore.Error(context, "Font.Metadata", "The font declares invalid names, metrics or Unicode metadata.");
        }
    }

    /// <summary>Selects an exact base-14 PostScript font and its deterministic metric facts.</summary>
    public IPdfFontProgram Standard(string postScriptName, PdfOperationContext context)
    {
        if (postScriptName == null)
            throw new ArgumentNullException(nameof(postScriptName));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
        return new StandardFont(postScriptName, context);
    }

    /// <summary>Decodes explicit standard/simple glyph-name encodings without executing PostScript or searching machine fonts.</summary>
    public string DecodeSimple(PdfValue encoding, PdfValue encodedText, string baseFont, PdfOperationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        return SimpleFontEncoding.Decode(encoding, encodedText, baseFont, context);
    }

    /// <summary>Gets exact declared-encoding advances from pinned base-14 glyph-name metrics, independent of ToUnicode replacements.</summary>
    public IReadOnlyList<double?> GetSimpleAdvances(PdfValue encoding, string baseFont, PdfOperationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        var names = SimpleFontEncoding.Names(encoding, baseFont, context, out var normalized);
        var font = new StandardFont(normalized, context);
        context.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var widths = new double? [256];
        for (int i = 0; i < widths.Length; i++)
            widths[i] = font.AdvanceForName(names[i], context);
        return Array.AsReadOnly(widths);
    }
}
