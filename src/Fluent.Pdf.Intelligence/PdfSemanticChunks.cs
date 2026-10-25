using System.Collections.ObjectModel;
using System.Text;
using System.Threading;

namespace Fluent.Pdf.Intelligence;
/// <summary>Finite source-order chunk profile. UTF8 bytes are a portable token ceiling only for separately qualified byte-bounded tokenizers.</summary>
public sealed class PdfSemanticChunkOptions
{
    /// <summary>Captures finite chunk/text bounds and optional explicit geometric table inference. Invisible text is excluded unless explicitly requested.</summary>
    public PdfSemanticChunkOptions(int maximumUtf8Bytes = 1024, int maximumChunks = 4096, int maximumCharacters = 1000000, bool includeInvisibleText = false, PdfTableExtractionOptions? tables = null)
    {
        if (maximumUtf8Bytes < 4 || maximumUtf8Bytes > 65536)
            throw new ArgumentOutOfRangeException(nameof(maximumUtf8Bytes));
        if (maximumChunks < 1 || maximumChunks > 65536)
            throw new ArgumentOutOfRangeException(nameof(maximumChunks));
        if (maximumCharacters < 1 || maximumCharacters > 4000000)
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        if (tables?.JoinConsecutivePages == true)
            throw new ArgumentException("Chunk tables remain page-local; continuation inference must be requested separately.", nameof(tables));
        MaximumUtf8Bytes = maximumUtf8Bytes;
        MaximumChunks = maximumChunks;
        MaximumCharacters = maximumCharacters;
        IncludeInvisibleText = includeInvisibleText;
        Tables = tables;
    }

    /// <summary>Gets the hard UTF8 byte ceiling, without an unqualified model token-count claim.</summary>
    public int MaximumUtf8Bytes { get; }
    /// <summary>Gets the total finite chunk ceiling.</summary>
    public int MaximumChunks { get; }
    /// <summary>Gets the cumulative captured UTF16 ceiling.</summary>
    public int MaximumCharacters { get; }
    /// <summary>Gets explicit nonpainting-text inclusion.</summary>
    public bool IncludeInvisibleText { get; }
    /// <summary>Gets optional geometric table inference, not author-confirmed semantics.</summary>
    public PdfTableExtractionOptions? Tables { get; }
}

/// <summary>Exact contiguous raw mapped source text, with no invented separators or overlapping primary text. Geometry describes advance cells, not visible ink.</summary>
public sealed class PdfSemanticChunk
{
    internal PdfSemanticChunk(int index, int page, int offset, string text, string sha, int bytes, int? table, PdfExtractedCharacter[] characters)
    {
        Index = index;
        PageIndex = page;
        TextOffset = offset;
        Text = text;
        Sha256 = sha;
        Utf8Bytes = bytes;
        TableIndex = table;
        Characters = new ReadOnlyCollection<PdfExtractedCharacter>(characters);
    }

    /// <summary>Gets stable source-order chunk ordinal within this exact capture.</summary>
    public int Index { get; }
    /// <summary>Gets zero-based original page index.</summary>
    public int PageIndex { get; }
    /// <summary>Gets UTF16 offset in that page's captured raw text.</summary>
    public int TextOffset { get; }
    /// <summary>Gets exact mapped text in operator execution order.</summary>
    public string Text { get; }
    /// <summary>Gets exact text UTF8 SHA256.</summary>
    public string Sha256 { get; }
    /// <summary>Gets exact UTF8 length under the utf8-bytes/v1 profile.</summary>
    public int Utf8Bytes { get; }
    /// <summary>Gets page-local inferred table ordinal or null. Table splitting preserves this identity.</summary>
    public int? TableIndex { get; }
    /// <summary>Gets original mapping/operation/displayed/native-geometry records, including inferred-geometry flags.</summary>
    public IReadOnlyList<PdfExtractedCharacter> Characters { get; }
}

/// <summary>Detached immutable chunks bound to the complete input revision. Concatenation per captured page exactly reconstructs captured raw text.</summary>
public sealed class PdfSemanticChunkResult
{
    internal PdfSemanticChunkResult(string source, string capture, PdfSemanticChunk[] chunks, int[] pages, PdfOperationReport report)
    {
        DocumentSha256 = source;
        CaptureSha256 = capture;
        Chunks = new ReadOnlyCollection<PdfSemanticChunk>(chunks);
        PageIndices = new ReadOnlyCollection<int>(pages);
        OperationReport = report;
    }

    /// <summary>Gets complete source PDF SHA256, distinct from text/capture identity.</summary>
    public string DocumentSha256 { get; }
    /// <summary>Gets capture hash including revision, options, selected pages, boundaries, text and inference provenance.</summary>
    public string CaptureSha256 { get; }
    /// <summary>Gets all primary chunks in selected page/source order.</summary>
    public IReadOnlyList<PdfSemanticChunk> Chunks { get; }
    /// <summary>Gets explicitly captured page indices, including empty pages.</summary>
    public IReadOnlyList<int> PageIndices { get; }
    /// <summary>Gets shared parse/extraction/chunk resource accounting.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>Original bounded layout-aware raw-text chunking. Logical reading order, ActualText substitution and author-confirmed semantics require separate profiles.</summary>
public static class PdfSemanticChunks
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    /// <summary>Captures only explicit ascending pages. Preserves every captured cluster, prefers line/whitespace boundaries, and never crosses an inferred table boundary. Does not establish visible ink or redact concealed text.</summary>
    public static PdfSemanticChunkResult Capture(PdfDocumentSnapshot source, IEnumerable<int> pages, PdfSemanticChunkOptions? options = null, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (pages == null)
            throw new ArgumentNullException(nameof(pages));
        options ??= new();
        providers ??= PdfProviderRegistry.Empty;
        var selected = new List<int>();
        foreach (int page in pages)
        {
            if (selected.Count == 4096 || page < 0 || page >= source.Pages.Count || selected.Count != 0 && page <= selected[selected.Count - 1])
                throw new ArgumentException("Select 1..4096 valid ascending distinct source pages.", nameof(pages));
            selected.Add(page);
        }

        if (selected.Count == 0)
            throw new ArgumentException("Explicit pages are required.", nameof(pages));
        var chunks = new List<PdfSemanticChunk>();
        string document = "", capture = "";
        var report = PdfFont.Run("ChunkDocument", "explicit.retrieval", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            using var graph = new PdfGraphEdit(source, new(allowSignatureInvalidation: true), c);
            document = PdfArchival.Digest(graph.Graph.Source, c);
            var engine = new PdfTextExtractionEngine(graph, new(includeInvisibleText: options.IncludeInvisibleText), providers);
            int total = 0;
            foreach (int page in selected)
            {
                c.Checkpoint();
                var data = engine.Read(page);
                if ((long)total + data.Raw.Length > options.MaximumCharacters)
                    throw Error(c, "Characters", "Captured raw text exceeds its cumulative ceiling.");
                total += data.Raw.Length;
                PdfExtractedTable? table = options.Tables == null ? null : PdfTableExtraction.Detect(graph, data, options.Tables, providers);
                c.Consume(PdfResourceKind.ExpandedBytes, data.Characters.Count * 128L + 256);
                if (options.Tables != null && table == null)
                    c.Report(new("Retrieval.TableNoCandidate", "No table satisfies the explicit page-local inference profile.", severity: PdfDiagnosticSeverity.Information, pageIndex: page));
                var membership = new HashSet<int>();
                if (table != null)
                    foreach (var cell in table.Cells)
                        foreach (var character in cell.Characters)
                            membership.Add(character.Index);
                var lineEnds = new HashSet<int>();
                foreach (var line in data.Lines)
                    if (line.Words.Count != 0)
                        lineEnds.Add(line.Words.SelectMany(word => word.Characters).Max(character => character.Index));
                int at = 0, offset = 0;
                while (at < data.Characters.Count)
                {
                    c.Checkpoint();
                    int end = at, bytes = 0, preferred = -1;
                    bool inTable = membership.Contains(data.Characters[at].Index);
                    while (end < data.Characters.Count && membership.Contains(data.Characters[end].Index) == inTable)
                    {
                        c.Checkpoint();
                        var character = data.Characters[end];
                        int count;
                        try
                        {
                            count = StrictUtf8.GetByteCount(character.Text);
                        }
                        catch (EncoderFallbackException)
                        {
                            throw Error(c, "Unicode", "A captured cluster contains malformed Unicode.");
                        }

                        if (bytes + count > options.MaximumUtf8Bytes)
                            break;
                        bytes += count;
                        end++;
                        bool crlf = character.Text.EndsWith("\r", StringComparison.Ordinal) && end < data.Characters.Count && data.Characters[end].Text.StartsWith("\n", StringComparison.Ordinal);
                        if (!crlf && (lineEnds.Contains(character.Index) || character.Text.Length != 0 && char.IsWhiteSpace(character.Text[character.Text.Length - 1])))
                            preferred = end;
                    }

                    if (end == at)
                        throw Error(c, "ClusterBudget", "One source Unicode cluster exceeds the chunk byte ceiling.");
                    bool budgetSplit = end < data.Characters.Count && membership.Contains(data.Characters[end].Index) == inTable;
                    if (budgetSplit && preferred > at)
                        end = preferred;
                    if (end < data.Characters.Count && data.Characters[end - 1].Text.EndsWith("\r", StringComparison.Ordinal) && data.Characters[end].Text.StartsWith("\n", StringComparison.Ordinal))
                    {
                        if (end == at + 1)
                            throw Error(c, "ClusterBudget", "An indivisible CRLF pair exceeds the chunk byte ceiling or crosses a table boundary.");
                        end--;
                    }

                    if (chunks.Count == options.MaximumChunks)
                        throw Error(c, "Chunks", "The cumulative chunk ceiling was reached.");
                    int length = 0;
                    for (int i = at; i < end; i++)
                        length = checked(length + data.Characters[i].Text.Length);
                    c.Consume(PdfResourceKind.Objects, end - at + 1);
                    c.Consume(PdfResourceKind.ExpandedBytes, length * 4L + (end - at) * 320L + 256);
                    var characters = data.Characters.GetRange(at, end - at).Select(character => new PdfExtractedCharacter(character.Index, character.OperationIndex, character.Code, character.CodeLength, character.Text, character.Confidence, character.Quad, character.UserQuad, character.BaselineStart, character.BaselineEnd, character.FontName, character.FontSize, character.RenderingMode, character.LayerVisible, character.GeometryEstimated)).ToArray();
                    string text = data.Raw.Substring(offset, length);
                    var encoded = IntelligenceJson.Utf8(text, c);
                    chunks.Add(new(chunks.Count, page, offset, text, PdfArchival.Digest(encoded, c), checked((int)encoded.Length), inTable ? 0 : null, characters));
                    offset += length;
                    at = end;
                }

                if (offset != data.Raw.Length)
                    throw Error(c, "SourceCoverage", "Captured character and raw-text coverage disagree.");
            }

            var descriptor = new Dictionary<string, object?>
            {
                ["source"] = document,
                ["pages"] = selected.Select(page => (object? )(decimal)page).ToList(),
                ["byteLimit"] = (decimal)options.MaximumUtf8Bytes,
                ["invisible"] = options.IncludeInvisibleText,
                ["tables"] = TableDescriptor(options.Tables),
                ["chunks"] = chunks.Select(chunk => (object? )new Dictionary<string, object?> { ["page"] = (decimal)chunk.PageIndex, ["offset"] = (decimal)chunk.TextOffset, ["hash"] = chunk.Sha256, ["table"] = chunk.TableIndex.HasValue ? (object? )(decimal)chunk.TableIndex.Value : null }).ToList()
            };
            capture = PdfArchival.Digest(IntelligenceJson.Bytes(descriptor, c), c);
            c.Report(new("Retrieval.SourceOrder", "Chunks preserve captured raw source order. Geometry/table inference does not establish logical semantics, visible ink, or author-confirmed accessibility.", severity: PdfDiagnosticSeverity.Information));
        }, PdfCapability.ReadDocument);
        return new(document, capture, chunks.ToArray(), selected.ToArray(), report);
    }

    private static object? TableDescriptor(PdfTableExtractionOptions? options)
    {
        if (options == null)
            return null;
        var region = options.Region;
        string Number(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        return new Dictionary<string, object?>
        {
            ["mode"] = options.Mode.ToString(),
            ["tolerance"] = Number(options.Tolerance),
            ["gap"] = Number(options.MinimumColumnGap),
            ["minimumRows"] = (decimal)options.MinimumRows,
            ["minimumColumns"] = (decimal)options.MinimumColumns,
            ["maximumCells"] = (decimal)options.MaximumCells,
            ["headerRows"] = (decimal)options.HeaderRows,
            ["numericCulture"] = options.NumericCulture,
            ["region"] = region.HasValue ? new List<object?>
            {
                Number(region.Value.X),
                Number(region.Value.Y),
                Number(region.Value.Width),
                Number(region.Value.Height)
            }

            : null
        };
    }

    internal static PdfException Error(PdfOperationContext c, string code, string message)
    {
        var diagnostic = new PdfDiagnostic("Retrieval." + code, message, PdfCapability.ReadDocument, "explicit.retrieval");
        c.Report(diagnostic);
        return new(diagnostic);
    }
}
