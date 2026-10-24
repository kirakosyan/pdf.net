using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;

/// <summary>Explicit offline, exact-language whole-word hyphenation exceptions. No dictionary download, normalization, case folding or linguistic rules are inferred.</summary>
public sealed class PdfHyphenationDictionary
{
    private readonly Dictionary<string, int[]> _words;
    private PdfHyphenationDictionary(string language, string version, string provenance, string sha256, Dictionary<string, int[]> words)
    { Language = language; Version = version; Provenance = provenance; Sha256 = sha256; _words = words; }
    /// <summary>Gets the exact language tag required by all paragraph runs.</summary>
    public string Language { get; }
    /// <summary>Gets the caller's pinned dictionary version.</summary>
    public string Version { get; }
    /// <summary>Gets the caller's source/license provenance identifier; it is not a redistribution approval.</summary>
    public string Provenance { get; }
    /// <summary>Gets the verified SHA-256 of the exact input bytes.</summary>
    public string Sha256 { get; }
    /// <summary>Gets the number of exact whole-word entries.</summary>
    public int Count => _words.Count;
    /// <summary>Gets the completed dictionary-capture report.</summary>
    public PdfOperationReport OperationReport { get; private set; } = null!;

    /// <summary>Reads at most 2 MiB of strict UTF-8 LF-terminated word TAB comma-separated UTF-16 break offsets. Unique BMP words use pinned Unicode strong/mark characters, at most128 units; offsets increase at interior extended-grapheme boundaries. Input stays caller-owned.</summary>
    public static PdfHyphenationDictionary Read(Stream input, string language, string version, string provenance, string expectedSha256, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead) throw new ArgumentException("Readable borrowed dictionary input is required.", nameof(input));
        _ = new PdfTextOptions(language: language);
        Guard.Text(version, nameof(version), 128); Guard.Text(provenance, nameof(provenance), 1024);
        if (expectedSha256 == null || expectedSha256.Length != 64 || expectedSha256.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))) throw new ArgumentException("Exact lowercase SHA-256 is required.", nameof(expectedSha256));
        PdfHyphenationDictionary? result = null;
        var report = PdfFont.Run("ReadHyphenationDictionary", "managed.hyphenation", policy, cancellationToken, c =>
        {
            using var borrowed = new BorrowedStream(input, true, c);
            using var capture = new PdfCodecBufferStream(c, 2097152);
            c.Consume(PdfResourceKind.ExpandedBytes, 4096); var buffer = new byte[4096];
            int n; while ((n = borrowed.Read(buffer, 0, buffer.Length)) != 0) capture.Write(buffer, 0, n);
            var bytes = capture.Finish();
            using var hash = SHA256.Create(); using var raw = bytes.Open(0, bytes.Length);
            string actual = BitConverter.ToString(hash.ComputeHash(raw)).Replace("-", "").ToLowerInvariant();
            if (actual != expectedSha256) throw PdfWriterCore.Error(c, "Hyphenation.SourceHash", "Dictionary bytes differ from the explicit source pin.");
            c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 12 + 1024);
            var data = new byte[(int)bytes.Length]; for (int i = 0; i < data.Length; i++) data[i] = (byte)bytes.At(i);
            string text;
            try { text = new UTF8Encoding(false, true).GetString(data); }
            catch (DecoderFallbackException) { throw PdfWriterCore.Error(c, "Hyphenation.Encoding", "Dictionary input requires strict UTF-8."); }
            if (text.Length == 0 || text[text.Length - 1] != '\n' || text.IndexOf('\r') >= 0) throw PdfWriterCore.Error(c, "Hyphenation.Syntax", "A nonempty LF-terminated dictionary is required.");
            var words = new Dictionary<string, int[]>(StringComparer.Ordinal);
            var properties = PdfUnicodeData.For(c);
            foreach (string row in text.Substring(0, text.Length - 1).Split('\n'))
            {
                c.Checkpoint(); c.Consume(PdfResourceKind.Objects, 1); c.Consume(PdfResourceKind.ExpandedBytes, row.Length * 16L + 256);
                int tab = row.IndexOf('\t');
                if (tab <= 0 || tab > 128 || row.IndexOf('\t', tab + 1) >= 0 || words.Count == 100000) throw PdfWriterCore.Error(c, "Hyphenation.Syntax", "Dictionary row extent or grammar is invalid.");
                string word = row.Substring(0, tab);
                if (word.Any(ch => !WordCharacter(ch, properties)) || words.ContainsKey(word)) throw PdfWriterCore.Error(c, "Hyphenation.Word", "Dictionary words must be unique exact supported Unicode sequences.");
                var unicode = PdfUnicode.AnalyzeCore(word, PdfParagraphDirection.Auto, c);
                var boundaries = new HashSet<int>(unicode.Graphemes.Select(g => g.Utf16Offset));
                var offsets = new List<int>(); int previous = 0;
                foreach (string value in row.Substring(tab + 1).Split(','))
                {
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int offset) || offset <= previous || offset >= word.Length || !boundaries.Contains(offset)) throw PdfWriterCore.Error(c, "Hyphenation.Boundary", "Breaks must increase at interior extended-grapheme boundaries.");
                    offsets.Add(offset); previous = offset;
                }
                words.Add(word, offsets.ToArray());
            }
            result = new(language, version, provenance, actual, words);
        }, PdfCapability.ShapeText);
        result!.OperationReport = report; return result;
    }

    private static bool WordCharacter(char c, PdfUnicodeData data)
    {
        if (c >= 0xd800 && c <= 0xdfff) return false;
        var p = data.Get(c);
        return !p.Ignorable && (p.Bidi == PdfBidiClass.L || p.Bidi == PdfBidiClass.R || p.Bidi == PdfBidiClass.AL || p.Bidi == PdfBidiClass.NSM || p.Break == PdfUnicodeData.Grapheme.SpacingMark);
    }
    internal void Apply(PdfFlowParagraph paragraph, string text, PdfUnicodeAnalysis unicode, HashSet<int> breaks, PdfOperationContext c)
    {
        if (paragraph.Runs.Any(r => !string.Equals(r.Style.TextOptions.Language, Language, StringComparison.OrdinalIgnoreCase))) throw PdfWriterCore.Error(c, "Hyphenation.Language", "All paragraph runs must explicitly match the dictionary language.");
        if (unicode.Scalars.Any(s => s.EmbeddingLevel != 0)) throw PdfWriterCore.Error(c, "Hyphenation.DirectionUnsupported", "Dictionary line shaping currently requires an entirely level-zero paragraph.");
        var properties = PdfUnicodeData.For(c);
        for (int i = 0; i < text.Length;)
        {
            c.Checkpoint(); int start = i;
            while (i < text.Length && WordCharacter(text[i], properties)) i++;
            if (start == i) { i++; continue; }
            c.Consume(PdfResourceKind.Objects, 1); c.Consume(PdfResourceKind.ExpandedBytes, (i - start) * 2L + 128);
            if (_words.TryGetValue(text.Substring(start, i - start), out var offsets))
                foreach (int offset in offsets) { c.Consume(PdfResourceKind.ExpandedBytes, 32); breaks.Add(start + offset); }
        }
    }
}
