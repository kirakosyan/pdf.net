using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit encoding for human-readable PDF text strings, independent of font character-code strings.</summary>
public enum PdfTextStringEncoding
{
    /// <summary>PDFDocEncoding with no marker.</summary>
    PdfDocEncoding,
    /// <summary>UTF-16 big endian with its byte-order marker.</summary>
    Utf16BigEndian,
    /// <summary>PDF 2.0 UTF-8 with its required marker.</summary>
    Utf8
}

/// <summary>Immutable exact encoded text, declared encoding and required PDF version. No implicit Unicode normalization occurs.</summary>
public sealed class PdfTextString
{
    private PdfTextString(string text, PdfValue value, PdfTextStringEncoding encoding)
    {
        Text = text;
        Value = value;
        Encoding = encoding;
    }

    /// <summary>Gets decoded logical Unicode.</summary>
    public string Text { get; }
    /// <summary>Gets the exact immutable PDF string bytes.</summary>
    public PdfValue Value { get; }
    /// <summary>Gets the explicit detected/selected encoding.</summary>
    public PdfTextStringEncoding Encoding { get; }
    /// <summary>Gets the encoding's output version floor.</summary>
    public string MinimumVersion => Encoding == PdfTextStringEncoding.Utf8 ? "2.0" : "1.2";
    /// <summary>Gets the successful explicit string operation report.</summary>
    public PdfOperationReport OperationReport { get; private set; } = null!;

    /// <summary>Creates a bounded text string. UTF-16BE is the default; selecting UTF-8 explicitly requires PDF 2.0 when used in output.</summary>
    public static PdfTextString Create(string text, PdfTextStringEncoding encoding = PdfTextStringEncoding.Utf16BigEndian, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        if (!Enum.IsDefined(typeof(PdfTextStringEncoding), encoding))
            throw new ArgumentOutOfRangeException(nameof(encoding));
        PdfTextString? result = null;
        var report = PdfFont.Run("EncodePdfTextString", "managed.text-string", policy, cancellationToken, context =>
        {
            context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 16L + 1024);
            context.Consume(PdfResourceKind.Objects, 2);
            PdfValue value;
            if (encoding == PdfTextStringEncoding.Utf16BigEndian)
                value = PdfTextStrings.Encode(text, context);
            else if (encoding == PdfTextStringEncoding.Utf8)
            {
                try
                {
                    var bytes = new UTF8Encoding(false, true).GetBytes(text);
                    var output = new byte[bytes.Length + 3];
                    output[0] = 239;
                    output[1] = 187;
                    output[2] = 191;
                    Buffer.BlockCopy(bytes, 0, output, 3, bytes.Length);
                    value = PdfValue.OwnedString(output);
                }
                catch (EncoderFallbackException)
                {
                    throw PdfWriterCore.Error(context, "Text.StringEncoding", "Text strings require well-formed Unicode.");
                }
            }
            else
            {
                var output = new byte[text.Length];
                for (int i = 0; i < text.Length; i++)
                {
                    context.Checkpoint();
                    int encoded = -1;
                    for (int b = 0; b < 256; b++)
                        if (PdfTextStrings.LegacyCharacter(b) == text[i])
                        {
                            encoded = b;
                            break;
                        }

                    if (encoded < 0)
                        throw PdfWriterCore.Error(context, "Text.LegacyUnrepresentable", "The requested logical Unicode cannot be encoded exactly with PDFDocEncoding.");
                    output[i] = (byte)encoded;
                }

                // Marker-shaped legacy bytes would change their interpretation and cannot be emitted ambiguously.
                if (Marker(output) != PdfTextStringEncoding.PdfDocEncoding)
                    throw PdfWriterCore.Error(context, "Text.AmbiguousMarker", "The legacy byte sequence collides with a Unicode encoding marker; select Unicode encoding explicitly.");
                value = PdfValue.OwnedString(output);
            }

            result = new(text, value, encoding);
            context.Checkpoint();
        }, PdfCapability.WriteDocument);
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Decodes a declared human-readable PDF string and preserves its exact source bytes. Binary/font-code strings must not use this API.</summary>
    public static PdfTextString Read(PdfValue value, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        PdfTextString? result = null;
        var report = PdfFont.Run("DecodePdfTextString", "managed.text-string", policy, cancellationToken, context =>
        {
            context.Consume(PdfResourceKind.Objects, 1);
            string text = PdfTextStrings.Decode(value, context);
            result = new(text, value, Marker(value.StringBytes));
        }, PdfCapability.ReadDocument);
        result!.OperationReport = report;
        return result;
    }

    private static PdfTextStringEncoding Marker(IReadOnlyList<byte> bytes) => bytes.Count >= 2 && bytes[0] == 254 && bytes[1] == 255 ? PdfTextStringEncoding.Utf16BigEndian : bytes.Count >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191 ? PdfTextStringEncoding.Utf8 : PdfTextStringEncoding.PdfDocEncoding;
}
