using System.Threading;

namespace Fluent.Pdf;
/// <summary>Bounded multi-pass contents/index layout with final page decorations.</summary>
public static class PdfTemplateLayout
{
    /// <summary>Lays out body blocks with bounded contents/index convergence and final header/footer callbacks.</summary>
    public static PdfPageTemplateResult Layout(IEnumerable<PdfFlowBlock> blocks, PdfFlowOptions options, PdfProviderRegistry providers, PdfPageTemplate template, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (blocks == null)
            throw new ArgumentNullException(nameof(blocks));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        if (template == null)
            throw new ArgumentNullException(nameof(template));
        var shaper = providers.GetRequired<IPdfTextShaper>(PdfCapability.ShapeText);
        PdfPageTemplateResult? result = null;
        PdfFlowEngine? finalEngine = null;
        var finish = new List<Action<PdfOperationReport>>();
        var report = PdfFont.Run("LayoutPageTemplate", "managed.templates", policy, cancellationToken, context =>
        {
            var source = new List<PdfFlowBlock>();
            foreach (var block in blocks)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 64);
                source.Add(block ?? throw new ArgumentException("Blocks cannot contain null.", nameof(blocks)));
            }

            var definitions = new List<PdfFlowHeading>();
            bool placeholders = false;
            var identities = new HashSet<string>(StringComparer.Ordinal);
            void Inspect(IReadOnlyList<PdfFlowBlock> content)
            {
                using var depth = context.EnterScope(PdfResourceKind.Depth);
                foreach (var block in content)
                {
                    context.Checkpoint();
                    context.Consume(PdfResourceKind.Objects, 1);
                    if (block is PdfFlowHeading heading)
                    {
                        if (!identities.Add(heading.Id))
                            throw Error("Template.DuplicateHeading", "Body heading destination IDs must be unique.");
                        definitions.Add(heading);
                    }

                    if (block is PdfFlowContents || block is PdfFlowIndex)
                        placeholders = true;
                    if (block is PdfFlowComponent component)
                        Inspect(component.Content);
                    if (block is PdfFlowColumns columns)
                        Inspect(columns.Content);
                    if (block is PdfFlowList list)
                        foreach (var item in list.Items)
                            Inspect(item.Content);
                    if (block is PdfTable table)
                        foreach (var cell in table.Grid)
                            Inspect(cell.Cell.Content);
                }
            }

            Inspect(source);
            var margins = options.Margins;
            var bodyOptions = new PdfFlowOptions(options.Geometry, new(margins.Left, margins.Top + template.HeaderHeight + (template.HeaderHeight > 0 ? template.Gap : 0), margins.Right, margins.Bottom + template.FooterHeight + (template.FooterHeight > 0 ? template.Gap : 0)), options.Overflow, options.MaximumPages, options.TabInterval, options.TabStops.ToArray());
            var targets = new Dictionary<string, PdfFlowHeadingPlacement>(StringComparer.Ordinal);
            string? previous = null;
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            PdfFlowResult? body = null;
            int passes = 0;
            bool converged = false;
            // Only these engines share the exact immutable options, shaper and operation context.
            // Header/footer and nested-layout engines retain their own measurements.
            var paragraphs = new Dictionary<PdfFlowParagraph, PdfFlowParagraphData>();
            for (; passes < template.MaximumPasses; passes++)
            {
                context.Checkpoint();
                var expanded = Materialize(source, definitions, targets, template.Numbering, bodyOptions, context);
                var engine = new PdfFlowEngine(bodyOptions, shaper, context, paragraphs);
                var current = engine.Layout(expanded);
                var next = new Dictionary<string, PdfFlowHeadingPlacement>(StringComparer.Ordinal);
                foreach (var heading in current.Pages.SelectMany(p => p.Headings))
                    if (!next.ContainsKey(heading.Heading.Id))
                        next.Add(heading.Heading.Id, heading);
                foreach (var definition in definitions)
                    if (!next.ContainsKey(definition.Id))
                        throw Error("Template.HeadingDestination", "A declared body heading has no final visible placement.");
                string signature = current.Pages.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + string.Join(";", definitions.Select(h =>
                {
                    var target = next[h.Id];
                    return h.Id + "=" + target.PageIndex + "," + target.Bounds.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + target.Bounds.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                }));
                body = current;
                finalEngine = engine;
                targets = next;
                if (!placeholders || signature == previous)
                {
                    converged = true;
                    passes++;
                    break;
                }

                if (!signatures.Add(signature))
                    throw Error("Template.PaginationCycle", "Contents/index pagination entered a repeated nonconvergent state.");
                previous = signature;
            }

            if (body == null || !converged)
                throw Error("Template.PaginationConvergence", "The bounded contents/index pagination did not converge.");
            context.Consume(PdfResourceKind.ExpandedBytes, body!.Pages.Count * 128L + definitions.Count * 256L + 256);
            var decorations = new PdfCanvasResult[body.Pages.Count];
            var display = options.Geometry.DisplayBounds();
            double width = display.Width - margins.Left - margins.Right;
            for (int i = 0; i < decorations.Length; i++)
            {
                var pageContext = new PdfPageTemplateContext(i, body.Pages.Count, template.Numbering.GetLabel(i), body.Pages[i], context);
                var header = template.Header?.Invoke(pageContext);
                context.Checkpoint();
                var footer = template.Footer?.Invoke(pageContext);
                context.Checkpoint();
                decorations[i] = PdfCanvas.CreateCore(options.Geometry, canvas =>
                {
                    if (header != null)
                        DrawDecoration(canvas, header, new(margins.Left, margins.Top, width, template.HeaderHeight), bodyOptions, shaper, context, finish);
                    if (footer != null)
                        DrawDecoration(canvas, footer, new(margins.Left, display.Height - margins.Bottom - template.FooterHeight, width, template.FooterHeight), bodyOptions, shaper, context, finish);
                }, new(PdfLengthUnit.Point, clipToContent: false), context, out var complete);
                finish.Add(complete);
            }

            var entries = definitions.Select(h =>
            {
                var placement = targets[h.Id];
                var point = body.Pages[placement.PageIndex].Canvas.ToUser.Transform(new(placement.Bounds.X, placement.Bounds.Y));
                return new PdfContentsEntry(h.Id, h.Title, placement.PageIndex, point);
            }).ToArray();
            var links = new List<PdfTemplateLink>();
            for (int i = 0; i < body.Pages.Count; i++)
                foreach (var locator in body.Pages[i].TemplateLocators)
                {
                    context.Checkpoint();
                    context.Consume(PdfResourceKind.ExpandedBytes, 96);
                    var target = targets[locator.Id];
                    var point = body.Pages[target.PageIndex].Canvas.ToUser.Transform(new(target.Bounds.X, target.Bounds.Y));
                    var bounds = PdfGeometryMath.Transform(locator.Bounds, body.Pages[i].Canvas.ToUser).Bounds;
                    links.Add(new(i, bounds, target.PageIndex, point));
                }

            result = new(body, decorations, template, entries, links.ToArray(), passes);
            PdfException Error(string code, string message) => PdfWriterCore.Error(context, code, message);
        }, PdfCapability.WriteDocument);
        finalEngine!.Complete(report);
        result!.Body.OperationReport = report;
        foreach (var complete in finish)
            complete(report);
        result.OperationReport = report;
        return result;
    }

    private static IReadOnlyList<PdfFlowBlock> Materialize(IReadOnlyList<PdfFlowBlock> blocks, IReadOnlyList<PdfFlowHeading> headings, IReadOnlyDictionary<string, PdfFlowHeadingPlacement> targets, PdfPageNumbering numbering, PdfFlowOptions options, PdfOperationContext context)
    {
        using var depth = context.EnterScope(PdfResourceKind.Depth);
        var output = new List<PdfFlowBlock>();
        foreach (var block in blocks)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.ExpandedBytes, 64);
            if (block is PdfFlowContents contents)
            {
                var selected = headings.Where(h => h.Level >= contents.MinimumLevel && h.Level <= contents.MaximumLevel).ToArray();
                output.Add(Table(selected.Select(h => (h.Id, h.Title, (h.Level - contents.MinimumLevel) * contents.LevelIndent)), contents.TextStyle, contents.PageColumnWidth));
            }
            else if (block is PdfFlowIndex index)
            {
                var rows = new List<(string Id, string Title, double Indent)>();
                foreach (var term in index.Terms)
                    for (int i = 0; i < term.DestinationIds.Count; i++)
                        rows.Add((term.DestinationIds[i], i == 0 ? term.Text : string.Empty, 0));
                output.Add(Table(rows, index.TextStyle, index.PageColumnWidth));
            }
            else if (block is PdfFlowComponent component)
                output.Add(new PdfFlowComponent(Materialize(component.Content, headings, targets, numbering, options, context), component.KeepTogether));
            else if (block is PdfFlowColumns columns)
                output.Add(new PdfFlowColumns(Materialize(columns.Content, headings, targets, numbering, options, context), columns.Count, columns.Gap, columns.Height, columns.Order));
            else if (block is PdfFlowList list)
                output.Add(list.WithContent(item => Materialize(item, headings, targets, numbering, options, context)));
            else if (block is PdfTable table)
            {
                IEnumerable<PdfTableRow> Rows()
                {
                    foreach (var row in table.Rows)
                    {
                        context.Checkpoint();
                        context.Consume(PdfResourceKind.ExpandedBytes, row.Cells.Count * 256L + 128);
                        yield return new PdfTableRow(row.Cells.Select(cell => new PdfTableCell(Materialize(cell.Content, headings, targets, numbering, options, context), cell.ColumnSpan, cell.RowSpan, cell.Style)), row.MinimumHeight, row.AllowSplit);
                    }
                }

                context.Consume(PdfResourceKind.ExpandedBytes, table.Columns.Count * 16L + 64);
                output.Add(PdfTable.CreateCore(table.Columns.ToArray(), Rows(), table.HeaderRows, table.AllowRowSplit, Math.Max(1, table.Rows.Count), context));
            }
            else
                output.Add(block);
        }

        return output;
        PdfTable Table(IEnumerable<(string Id, string Title, double Indent)> locators, PdfFlowTextStyle style, double pageWidth)
        {
            var ids = new List<string>();
            var rows = new List<PdfTableRow>();
            var none = PdfTableBorder.None;
            var cellStyle = new PdfTableCellStyle(PdfMargins.Uniform(0), left: none, top: none, right: none, bottom: none);
            foreach (var locator in locators)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.ExpandedBytes, 512);
                if (!headings.Any(h => h.Id == locator.Id))
                    throw PdfWriterCore.Error(context, "Template.MissingDestination", "An index locator references an unknown body destination.");
                int page = targets.TryGetValue(locator.Id, out var destination) ? destination.PageIndex : 0;
                ids.Add(locator.Id);
                rows.Add(new(new[] { new PdfTableCell(new[] { new PdfFlowParagraph(locator.Title, style, new(leftIndent: locator.Indent, orphanLines: 1, widowLines: 1)) }, style: cellStyle), new PdfTableCell(new[] { new PdfFlowParagraph(numbering.GetLabel(page), style, new(PdfFlowAlignment.End, orphanLines: 1, widowLines: 1)) }, style: cellStyle) }, allowSplit: false));
            }

            if (pageWidth >= options.Frame.Width)
                throw PdfWriterCore.Error(context, "Template.ContentsWidth", "The locator column leaves no positive contents-title frame.");
            var table = PdfTable.CreateCore(new[] { PdfTableColumn.Auto(), PdfTableColumn.Fixed(pageWidth) }, rows, 0, false, Math.Max(1, rows.Count), context);
            table.TemplateTargets = ids.ToArray();
            return table;
        }
    }

    private static void DrawDecoration(PdfCanvas canvas, PdfPageDecoration decoration, PdfRect bounds, PdfFlowOptions options, IPdfTextShaper shaper, PdfOperationContext context, List<Action<PdfOperationReport>> finish)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw PdfWriterCore.Error(context, "Template.DecorationFrame", "A selected decoration has no positive reserved frame.");
        if (decoration.Blocks != null)
        {
            var flowOptions = new PdfFlowOptions(new(new(new(0, 0, bounds.Width, bounds.Height))), PdfMargins.Uniform(0), options.Overflow, options.MaximumPages, options.TabInterval, options.TabStops.ToArray());
            var engine = new PdfFlowEngine(flowOptions, shaper, context);
            var flow = engine.Layout(decoration.Blocks);
            if (flow.Pages.Count != 1)
                throw PdfWriterCore.Error(context, "Template.DecorationOverflow", "A header/footer exceeds its fixed reserved frame.");
            foreach (var placement in flow.Pages[0].Canvas.Placements)
                canvas.CopyPlacement(placement, new(1, 0, 0, 1, bounds.X, bounds.Y));
            finish.Add(report =>
            {
                engine.Complete(report);
                flow.OperationReport = report;
            });
            return;
        }

        if (decoration.Drawing != null)
        {
            canvas.DrawDrawing(decoration.Drawing, decoration.SourceBounds, bounds, PdfCanvasFit.Contain);
            return;
        }

        if (decoration.HtmlProvider == null)
            throw new PdfProviderUnavailableException(PdfCapability.RenderHtml);
        if (!decoration.HtmlProvider.Descriptor.Capabilities.Contains(PdfCapability.RenderHtml))
            throw PdfWriterCore.Error(context, "Template.HtmlCapability", "The explicit fragment provider does not advertise HTML rendering.");
        context.Consume(PdfResourceKind.InputBytes, decoration.HtmlText!.Length * 2L);
        context.Consume(PdfResourceKind.ExpandedBytes, decoration.HtmlText.Length * 2L + 128);
        var drawing = decoration.HtmlProvider.Render(decoration.HtmlText, new(bounds.Width, bounds.Height), context) ?? throw PdfWriterCore.Error(context, "Template.HtmlProviderResult", "The explicit HTML fragment provider returned no drawing.");
        canvas.DrawDrawing(drawing, new(0, 0, bounds.Width, bounds.Height), bounds, PdfCanvasFit.Stretch);
    }
}
