using System.Collections.ObjectModel;
using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Native template generation with explicit layout/culture/styles and closed finite evaluation.</summary>
public sealed class PdfMailMergeOptions
{
    /// <summary>Captures bounded named styles and generation options. Missing fields and expanded visits/records remain explicit.</summary>
    public PdfMailMergeOptions(PdfGenerationOptions generation, IReadOnlyDictionary<string, PdfFlowTextStyle> styles, PdfMergeMissingField missingFields = PdfMergeMissingField.Error, int maximumVisits = 100000, int maximumRecords = 100000)
    {
        Generation = generation ?? throw new ArgumentNullException(nameof(generation));
        if (styles == null || styles.Count > 4096 || !styles.ContainsKey("body"))
            throw new ArgumentException("At most4096styles including an explicit body style are required.", nameof(styles));
        var copy = new Dictionary<string, PdfFlowTextStyle>(StringComparer.Ordinal);
        foreach (var item in styles)
        {
            if (!PdfMergeRecord.Identifier(item.Key) || item.Value == null)
                throw new ArgumentException("Style identifiers and values must be bounded and nonnull.", nameof(styles));
            copy.Add(item.Key, item.Value);
        }

        Styles = new ReadOnlyDictionary<string, PdfFlowTextStyle>(copy);
        Evaluation = new(generation.Culture.Name, missingFields, maximumVisits, maximumRecords);
    }

    /// <summary>Gets generation layout, explicit culture and precommit hooks.</summary>
    public PdfGenerationOptions Generation { get; }
    /// <summary>Gets immutable explicitly supplied text styles.</summary>
    public IReadOnlyDictionary<string, PdfFlowTextStyle> Styles { get; }
    /// <summary>Gets closed finite evaluation policy with the same explicit culture.</summary>
    public PdfMergeEvaluationOptions Evaluation { get; }
}

/// <summary>Closed native PDF merge programs. Evaluation, image/table binding, layout, providers and writing use one shared root context.</summary>
public static class PdfMailMerge
{
    /// <summary>Evaluates into a caller-owned editable document; no arbitrary template code is executed.</summary>
    public static PdfDocument Generate(PdfMergeTemplate template, PdfMergeRecord data, PdfMailMergeOptions options, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfDataGeneration.Generate(new[] { data }, Program(template, options), options.Generation, providers, policy, cancellationToken);
    /// <summary>Evaluates and writes at a borrowed current output position. The stream remains open; failure may leave a prefix.</summary>
    public static PdfOperationReport Write(PdfMergeTemplate template, PdfMergeRecord data, PdfMailMergeOptions options, PdfProviderRegistry providers, Stream output, PdfWriteOptions? writeOptions = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfDataGeneration.Write(new[] { data }, Program(template, options), options.Generation, providers, output, writeOptions, policy, cancellationToken);
    /// <summary>Evaluates and serializes privately before atomic destination replacement.</summary>
    public static PdfOperationReport WriteFile(PdfMergeTemplate template, PdfMergeRecord data, PdfMailMergeOptions options, PdfProviderRegistry providers, string path, PdfWriteOptions? writeOptions = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfDataGeneration.WriteFile(new[] { data }, Program(template, options), options.Generation, providers, path, writeOptions, policy, cancellationToken);
    private static PdfGenerationTemplate<PdfMergeRecord> Program(PdfMergeTemplate template, PdfMailMergeOptions options)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        return new(template.Identifier, (generation, record) => new Engine(generation, record, options).Render(template.Blocks, record, -1));
    }

    private sealed class Engine
    {
        private readonly PdfGenerationContext _generation;
        private readonly PdfMailMergeOptions _options;
        private readonly PdfMergeEvaluation _evaluation;
        private PdfOperationContext C => _generation.OperationContext;

        internal Engine(PdfGenerationContext generation, PdfMergeRecord data, PdfMailMergeOptions options)
        {
            _generation = generation;
            _options = options;
            _evaluation = new(data, options.Evaluation, C);
        }

        internal void Render(IReadOnlyList<PdfMergeBlock> blocks, PdfMergeRecord current, int index)
        {
            using var depth = C.EnterScope(PdfResourceKind.Depth);
            foreach (var block in blocks)
            {
                _evaluation.Visit();
                if (block is PdfMergeParagraph paragraph)
                    _generation.Add(Paragraph(paragraph.Runs, current, index, paragraph.Style));
                else if (block is PdfMergeRepeat repeat)
                {
                    var records = _evaluation.Records(current, repeat.Path, index);
                    for (int i = 0; i < records.Count; i++)
                    {
                        _evaluation.Record();
                        Render(repeat.Children, records[i], i);
                    }
                }
                else if (block is PdfMergeCondition condition)
                    Render(_evaluation.Condition(current, condition.Path, index) ? condition.WhenTrue : condition.WhenFalse, current, index);
                else if (block is PdfMergeImageBlock image)
                {
                    var value = _evaluation.Image(current, image.Path, index);
                    if (value == null)
                        continue;
                    var asset = PdfImagePng.Read(value.Bytes, new(), C);
                    var geometry = new PdfPageGeometry(new PdfPageBoxes(new PdfRect(0, 0, value.Width, value.Height)));
                    var canvas = PdfCanvas.CreateCore(geometry, draw => draw.DrawImageAsset(asset, new(0, 0, value.Width, value.Height)), new(), C, out var finishImage);
                    _generation.RegisterFinisher(finishImage);
                    _generation.Add(new PdfFlowFloat(canvas.Drawing, new(0, 0, value.Width, value.Height), value.Width, value.Height, gap: 0, clear: PdfFlowClear.Both));
                    _generation.Add(new PdfFlowParagraph("", Style("body"), new(clear: PdfFlowClear.Both)));
                }
                else if (block is PdfMergeTable table)
                {
                    var records = _evaluation.Records(current, table.Path, index);
                    _evaluation.Visit(table.Columns.Count * 128L + 128);
                    var rows = new List<PdfTableRow>();
                    bool header = table.Columns.Any(c => c.Header != null);
                    if (header)
                    {
                        var cells = new List<PdfTableCell>();
                        foreach (var column in table.Columns)
                        {
                            _evaluation.Visit();
                            cells.Add(new(new[] { new PdfFlowParagraph(column.Header ?? "", Style(column.Runs[0].Style)) }));
                        }

                        rows.Add(new(cells));
                    }

                    for (int i = 0; i < records.Count; i++)
                    {
                        _evaluation.Record();
                        var cells = new List<PdfTableCell>();
                        foreach (var column in table.Columns)
                        {
                            _evaluation.Visit();
                            cells.Add(new(new[] { Paragraph(column.Runs, records[i], i, null) }));
                        }

                        rows.Add(new(cells));
                    }

                    if (rows.Count == 0)
                        continue;
                    if (rows.Count > 1000000)
                        throw _evaluation.Error("TableLimit", "The table exceeds its finite row/header ceiling.");
                    _generation.Add(PdfTable.CreateCore(table.Columns.Select(c => c.Column).ToArray(), rows, header ? 1 : 0, true, Math.Min(1000000, _options.Evaluation.MaximumRecords + 1), C));
                }
            }
        }

        private PdfFlowTextStyle Style(string name) => _options.Styles.TryGetValue(name, out var style) ? style : throw _evaluation.Error("Style", "A template run names an absent explicit text style.");
        private PdfFlowParagraph Paragraph(IReadOnlyList<PdfMergeRun> source, PdfMergeRecord current, int index, PdfFlowParagraphStyle? style)
        {
            var runs = new List<PdfFlowRun>();
            foreach (var run in source)
            {
                _evaluation.Visit(run.Value.Length * 4L + 256);
                string text = run.IsField ? _evaluation.Format(current, run.Value, run.Format, index) : run.Value;
                runs.Add(new(text, Style(run.Style)));
            }

            return new(runs, style);
        }
    }
}
