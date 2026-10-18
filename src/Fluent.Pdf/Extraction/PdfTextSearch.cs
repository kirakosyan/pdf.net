using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Bounded passive semantic search and explicit source-position-preserving text replacement.</summary>
public static class PdfTextSearch
{
    /// <summary>Searches literals/regex across runs/streams/Forms, retaining Unicode-cluster, region, style and ActualText provenance.</summary>
    public static PdfTextSearchResult FindPage(PdfDocumentSnapshot source, int pageIndex, PdfTextQuery query, PdfTextExtractionOptions? extraction = null, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Check(source, pageIndex, query);
        SearchData? data = null;
        var report = PdfFont.Run("SearchText", "managed.search", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            using var graph = new PdfGraphEdit(source, new(), c);
            data = Find(graph, pageIndex, query, extraction ?? new(), providers ?? PdfProviderRegistry.Empty);
        }, PdfCapability.ReadDocument);
        return new(pageIndex, data!.Text, data.Matches.AsReadOnly(), report);
    }

    /// <summary>Replaces every selected complete source cluster with explicitly shaped text, preserving unrelated source advances. This profile requires untagged, single-line native-horizontal page text without invoked Forms or target ActualText; unsupported edits reject transactionally.</summary>
    public static PdfDocument ReplacePage(PdfDocumentSnapshot source, int pageIndex, PdfTextQuery query, string replacement, PdfFontFallback fonts, PdfProviderRegistry providers, PdfTextReplacementOptions options, PdfOrganizationOptions? organization = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Check(source, pageIndex, query);
        if (replacement == null)
            throw new ArgumentNullException(nameof(replacement));
        if (fonts == null)
            throw new ArgumentNullException(nameof(fonts));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (replacement.IndexOfAny(new[] { '\n', '\r', '\t', '\u2028', '\u2029' }) >= 0)
            throw new ArgumentException("This replacement profile requires one finite line.", nameof(replacement));
        return PdfGraphEdit.Run("ReplacePageText", source, organization, policy ?? source.Options.OperationPolicy, cancellationToken, graph => Replace(graph, pageIndex, query, replacement, fonts, providers, options), "1.7");
    }

    private sealed class Unit
    {
        internal int Start, Length, First, Count;
        internal bool Actual;
    }

    private sealed class SearchData
    {
        internal string Text = "";
        internal readonly List<PdfTextMatch> Matches = new();
        internal PdfTextPageData Page = null!;
    }

    private static void Check(PdfDocumentSnapshot source, int page, PdfTextQuery query)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (page < 0 || page >= source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(page));
    }

    private static SearchData Find(PdfGraphEdit graph, int page, PdfTextQuery query, PdfTextExtractionOptions extraction, PdfProviderRegistry providers)
    {
        var c = graph.Context;
        c.Consume(PdfResourceKind.ExpandedBytes, 2048);
        c.Consume(PdfResourceKind.Objects, 8);
        var result = new SearchData
        {
            Page = new PdfTextExtractionEngine(graph, extraction, providers).Read(page)
        };
        var units = new List<Unit>();
        var text = new StringBuilder();
        int span = 0;
        for (int i = 0; i <= result.Page.Characters.Count;)
        {
            c.Checkpoint();
            while (span < result.Page.Spans.Count && result.Page.Spans[span].FirstCharacter < i)
                span++;
            if (span < result.Page.Spans.Count && result.Page.Spans[span].FirstCharacter == i)
            {
                var actual = result.Page.Spans[span++];
                Add(actual.Text, i, actual.CharacterCount, true);
                i += actual.CharacterCount;
                if (actual.CharacterCount == 0)
                    continue;
            }
            else if (i < result.Page.Characters.Count)
            {
                Add(result.Page.Characters[i].Text, i, 1, false);
                i++;
            }
            else
                break;
        }

        c.Consume(PdfResourceKind.ExpandedBytes, text.Length * 2L + 64);
        result.Text = text.ToString();
        Regex? regex = null;
        if (query.IsRegex)
        {
            c.Consume(PdfResourceKind.ExpandedBytes, query.Pattern.Length * 256L + 4096);
            try
            {
                regex = new Regex(query.Pattern, RegexOptions.CultureInvariant | (query.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None), TimeSpan.FromMilliseconds(query.RegexTimeoutMilliseconds));
            }
            catch (ArgumentException)
            {
                throw graph.Error("Search.Pattern", "The explicit regex pattern is invalid.");
            }
        }

        int at = 0;
        while (at <= result.Text.Length)
        {
            c.Checkpoint();
            int begin, length;
            if (regex != null)
            {
                Match match;
                try
                {
                    match = regex.Match(result.Text, at);
                }
                catch (RegexMatchTimeoutException)
                {
                    c.Checkpoint();
                    throw graph.Error("Search.RegexTimeout", "The explicit regex execution deadline elapsed.");
                }

                c.Checkpoint();
                if (!match.Success)
                    break;
                begin = match.Index;
                length = match.Length;
            }
            else
            {
                begin = result.Text.IndexOf(query.Pattern, at, query.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                if (begin < 0)
                    break;
                length = query.Pattern.Length;
            }

            AddMatch(begin, length);
            at = begin + Math.Max(length, 1);
        }

        return result;
        void Add(string value, int first, int count, bool actual)
        {
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, value.Length * 4L + 128);
            units.Add(new() { Start = text.Length, Length = value.Length, First = first, Count = count, Actual = actual });
            text.Append(value);
        }

        void AddMatch(int start, int length)
        {
            c.Consume(PdfResourceKind.ExpandedBytes, 256);
            c.Consume(PdfResourceKind.Objects, 1);
            var characters = new List<PdfExtractedCharacter>();
            PdfRect? bounds = null;
            bool partial = false, actual = false;
            foreach (var unit in units)
            {
                c.Checkpoint();
                c.Consume(PdfResourceKind.Objects, 1);
                if (unit.Start >= start + length || unit.Start + unit.Length <= start)
                    continue;
                partial |= start > unit.Start || start + length < unit.Start + unit.Length;
                actual |= unit.Actual;
                for (int j = unit.First; j < unit.First + unit.Count; j++)
                {
                    var item = result.Page.Characters[j];
                    if (query.FontName != null && item.FontName != query.FontName || query.MinimumFontSize.HasValue && item.FontSize < query.MinimumFontSize.Value || query.MaximumFontSize.HasValue && item.FontSize > query.MaximumFontSize.Value || query.Region.HasValue && !Contains(query.Region.Value, item.Quad.Bounds))
                        return;
                    c.Consume(PdfResourceKind.ExpandedBytes, 16);
                    characters.Add(item);
                    bounds = bounds.HasValue ? PdfTextExtractionEngine.Union(bounds.Value, item.Quad.Bounds) : item.Quad.Bounds;
                }
            }

            if (characters.Count == 0 && (query.Region.HasValue || query.FontName != null || query.MinimumFontSize.HasValue || query.MaximumFontSize.HasValue))
                return;
            if (result.Matches.Count >= query.MaximumMatches)
                throw graph.Error("Search.MatchLimit", "The explicit maximum match count was exceeded.");
            c.Consume(PdfResourceKind.ExpandedBytes, length * 2L + 128);
            result.Matches.Add(new(start, result.Text.Substring(start, length), characters.AsReadOnly(), bounds, partial, actual));
        }
    }

    private static bool Contains(PdfRect a, PdfRect b) => b.X >= a.X && b.Y >= a.Y && b.X + b.Width <= a.X + a.Width && b.Y + b.Height <= a.Y + a.Height;
    private static void Replace(PdfGraphEdit graph, int page, PdfTextQuery query, string replacement, PdfFontFallback fonts, PdfProviderRegistry providers, PdfTextReplacementOptions options)
    {
        var c = graph.Context;
        graph.RequireUnstructuredContent();
        var data = Find(graph, page, query, new(includeHiddenLayers: true), providers);
        if (data.Matches.Count == 0)
            return;
        var binding = graph.Snapshot.Pages[page].Source!;
        var program = data.Page.Program;
        if (program.Operations.Any(o => o.Operation == PdfOperator.DrawObject))
            throw graph.Error("Search.FormReplacementProfile", "Replacement of invoked Forms requires an explicit per-invocation cloning profile.");
        c.Consume(PdfResourceKind.ExpandedBytes, data.Page.Characters.Count * 128L + 2048);
        var selected = new Dictionary<PdfValue, Dictionary<int, PdfExtractedCharacter>>();
        var drawings = new List<PdfTextDrawing>();
        foreach (var match in data.Matches)
        {
            c.Checkpoint();
            if (match.IsPartialCluster || match.UsesActualText || match.Characters.Count == 0)
                throw graph.Error("Search.ClusterReplacementProfile", "Replacement requires complete nonempty source-code clusters without target ActualText.");
            var first = match.Characters[0];
            PdfRect frame = first.UserQuad.Bounds;
            foreach (var character in match.Characters)
            {
                var start = data.Page.Geometry.DisplayToUser().Transform(character.BaselineStart);
                var end = data.Page.Geometry.DisplayToUser().Transform(character.BaselineEnd);
                var baseline = data.Page.Geometry.DisplayToUser().Transform(first.BaselineStart);
                if (character.Vertical || character.RenderingMode != 0 || character.GeometryEstimated || Math.Abs(start.Y - end.Y) > 1e-7 || replacement.Length != 0 && Math.Abs(start.Y - baseline.Y) > 1e-7 || end.X <= start.X || !Finite(character.AdvanceTJ))
                    throw graph.Error("Search.ReplacementGeometryProfile", "Replacement requires exact single-line native-horizontal forward fill text with finite source advances.");
                frame = PdfTextExtractionEngine.Union(frame, character.UserQuad.Bounds);
                if (!selected.TryGetValue(character.EncodedString, out var codes))
                {
                    c.Consume(PdfResourceKind.ExpandedBytes, 192);
                    selected.Add(character.EncodedString, codes = new());
                }

                if (codes.ContainsKey(character.ByteOffset))
                    throw graph.Error("Search.SharedStringProfile", "A source string has multiple selected execution identities.");
                c.Consume(PdfResourceKind.ExpandedBytes, 96);
                codes.Add(character.ByteOffset, character);
            }

            if (replacement.Length == 0)
                continue;
            var shaped = PdfText.ShapeCore(replacement, fonts, providers.GetRequired<IPdfTextShaper>(PdfCapability.ShapeText), new(), c);
            double width = 0, ascent = 0, descent = 0;
            foreach (var run in shaped.Runs)
            {
                var metrics = run.Request.Font.Info.Metrics;
                double scale = options.FontSize / metrics.UnitsPerEm;
                foreach (var glyph in run.Glyphs)
                {
                    c.Checkpoint();
                    if (glyph.AdvanceX < 0 || glyph.AdvanceY != 0 || glyph.OffsetY != 0)
                        throw graph.Error("Search.ReplacementShapeProfile", "This replacement frame profile requires horizontal forward glyph progression without vertical offsets.");
                    width += glyph.AdvanceX * scale;
                }

                ascent = Math.Max(ascent, metrics.Ascender * scale);
                descent = Math.Min(descent, metrics.Descender * scale);
            }

            double factor = Math.Min(1, Math.Min(width == 0 ? 1 : frame.Width / width, ascent == descent ? 1 : frame.Height / (ascent - descent)));
            if (!Finite(factor) || factor <= 0 || factor < 1 - 1e-9 && options.Overflow == PdfTextReplacementOverflow.Reject)
                throw graph.Error("Search.ReplacementOverflow", "The explicitly shaped replacement exceeds its source advance-cell frame.");
            if (factor < 1 - 1e-9)
                c.Report(new("Search.ReplacementScaled", "Explicit shrink-to-fit reduced the replacement font size.", severity: PdfDiagnosticSeverity.Information));
            drawings.Add(PdfTextDrawing.CreateCore(shaped, frame.X, frame.Y - descent * factor, options.FontSize * factor, 1.2, "Replacement", true, c));
        }

        var resources = graph.Copy(graph.Get(binding.EffectiveAttributes, "Resources"));
        var fontResources = resources.TryGetValue("Font", out var oldFonts) ? graph.Copy(graph.Resolve(oldFonts)) : new Dictionary<string, PdfValue>(StringComparer.Ordinal);
        var output = new List<PdfContentOperation>();
        c.Consume(PdfResourceKind.ExpandedBytes, program.Operations.Count * 64L + 256);
        output.Add(new(PdfOperator.SaveState));
        foreach (var op in program.Operations)
        {
            c.Checkpoint();
            if (op.Operation == PdfOperator.ShowText || op.Operation == PdfOperator.ShowPositionedText || op.Operation == PdfOperator.NextLineShowText || op.Operation == PdfOperator.SpacedNextLineShowText)
            {
                var values = op.Operation == PdfOperator.ShowPositionedText ? op.Operands[0].Items : new[]
                {
                    op.Operands[op.Operands.Count - 1]
                };
                if (values.Any(v => selected.ContainsKey(v)))
                {
                    if (op.Operation == PdfOperator.SpacedNextLineShowText)
                    {
                        output.Add(new(PdfOperator.WordSpacing, op.Operands[0]));
                        output.Add(new(PdfOperator.CharacterSpacing, op.Operands[1]));
                    }

                    if (op.Operation == PdfOperator.NextLineShowText || op.Operation == PdfOperator.SpacedNextLineShowText)
                        output.Add(new(PdfOperator.NextTextLine));
                    var parts = new List<PdfValue>();
                    foreach (var value in values)
                    {
                        c.Checkpoint();
                        c.Consume(PdfResourceKind.ExpandedBytes, 128);
                        if (!selected.TryGetValue(value, out var codes))
                        {
                            parts.Add(value);
                            continue;
                        }

                        int at = 0;
                        foreach (var pair in codes.OrderBy(x => x.Key))
                        {
                            c.Checkpoint();
                            var character = pair.Value;
                            if (pair.Key > at)
                                parts.Add(Slice(value, at, pair.Key - at));
                            parts.Add(PdfValues.Real(-character.AdvanceTJ));
                            at = pair.Key + character.CodeLength;
                        }

                        if (at < value.StringBytes.Count)
                            parts.Add(Slice(value, at, value.StringBytes.Count - at));
                    }

                    c.Consume(PdfResourceKind.ExpandedBytes, parts.Count * 16L + 64);
                    output.Add(new(PdfOperator.ShowPositionedText, PdfValue.Array(parts.ToArray())));
                    continue;
                }
            }

            output.Add(op);
        }

        output.Add(new(PdfOperator.RestoreState));
        foreach (var drawing in drawings)
        {
            c.Consume(PdfResourceKind.ExpandedBytes, drawing.Content.Operations.Count * 32L + drawing.Fonts.Count * 256L + 1024);
            var names = new Dictionary<string, string>();
            foreach (var font in drawing.Fonts)
            {
                string name = "Replacement" + fontResources.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                while (fontResources.ContainsKey(name))
                {
                    c.Checkpoint();
                    name += "X";
                }

                fontResources.Add(name, PdfValue.Reference(font.Value.AddTo(graph.Edit, c)));
                names.Add(font.Key, name);
            }

            output.Add(new(PdfOperator.SaveState));
            output.Add(new(PdfOperator.FillGray, PdfValue.Integer(0)));
            foreach (var op in drawing.Content.Operations)
            {
                c.Checkpoint();
                output.Add(op.Operation == PdfOperator.Font ? new(PdfOperator.Font, PdfValue.Name(names[op.Operands[0].NameValue]), op.Operands[1]) : op);
                if (op.Operation == PdfOperator.BeginText)
                {
                    output.Add(new(PdfOperator.TextRenderingMode, PdfValue.Integer(0)));
                    output.Add(new(PdfOperator.CharacterSpacing, PdfValue.Integer(0)));
                    output.Add(new(PdfOperator.WordSpacing, PdfValue.Integer(0)));
                    output.Add(new(PdfOperator.HorizontalScale, PdfValue.Integer(100)));
                    output.Add(new(PdfOperator.TextRise, PdfValue.Integer(0)));
                }
            }

            output.Add(new(PdfOperator.RestoreState));
        }

        resources["Font"] = PdfValue.OwnedDictionary(fontResources);
        var entries = graph.Copy(binding.Dictionary);
        entries["Resources"] = PdfValue.OwnedDictionary(resources);
        entries["Contents"] = PdfValue.Reference(graph.Edit.Add(PdfContentProgram.CreateCore(output, PdfContentKind.Page, c).ToStream(c)));
        graph.Edit.Set(binding.ObjectId, PdfValue.OwnedDictionary(entries));
        PdfValue Slice(PdfValue value, int at, int count)
        {
            c.Consume(PdfResourceKind.ExpandedBytes, count + 64L);
            var bytes = new byte[count];
            for (int i = 0; i < count; i++)
            {
                c.Checkpoint();
                bytes[i] = value.StringBytes[at + i];
            }

            return PdfValue.OwnedString(bytes);
        }
    }

    private static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
}
