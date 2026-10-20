using System.Globalization;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Original transactional searchable-text overlays. Existing raster/content bytes remain intact; text renders with mode3.</summary>
public static class PdfOcrSearchable
{
    /// <summary>Applies explicit recognition data/frame/fonts to one page under one parse/shape/embed/write context. Tagged sources require a separate explicit structure-association profile and reject here.</summary>
    public static PdfDocument Apply(PdfDocumentSnapshot source, int pageIndex, PdfOcrResult recognition, PdfOcrPageFrame frame, PdfFontFallback fonts, PdfProviderRegistry providers, PdfOcrOverlayOptions? options = null, PdfOrganizationOptions? organization = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (recognition == null)
            throw new ArgumentNullException(nameof(recognition));
        if (frame == null)
            throw new ArgumentNullException(nameof(frame));
        Validate(source, pageIndex, fonts, providers);
        options ??= new();
        return PdfGraphEdit.Run("ApplySearchableOcr", source, organization, policy ?? source.Options.OperationPolicy, cancellationToken, graph => ApplyCore(graph, pageIndex, recognition, frame, fonts, providers, options), "1.4");
    }

    /// <summary>Renders, captures opaque pixels, recognizes, shapes and overlays one page under the same root context. Providers are explicit; requested integer isotropic DPI must be72–600. Original page appearance is retained.</summary>
    public static PdfDocument RecognizePage(PdfDocumentSnapshot source, int pageIndex, PdfOcrOptions recognition, PdfFontFallback fonts, PdfProviderRegistry providers, PdfRenderOptions? render = null, PdfOcrOverlayOptions? overlay = null, PdfOrganizationOptions? organization = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (recognition == null)
            throw new ArgumentNullException(nameof(recognition));
        Validate(source, pageIndex, fonts, providers);
        render ??= new(200, 200, annotations: false);
        overlay ??= new();
        if (render.DpiX != render.DpiY || render.DpiX != Math.Floor(render.DpiX) || render.DpiX < 72 || render.DpiX > 600 || !render.BackgroundRgb.HasValue)
            throw new ArgumentException("OCR requires an explicit opaque isotropic integer DPI72–600 rendering profile.", nameof(render));
        _ = providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages);
        _ = providers.GetRequired<IPdfOcrProvider>(PdfCapability.RecognizeText);
        return PdfGraphEdit.Run("RecognizeSearchablePage", source, organization, policy ?? source.Options.OperationPolicy, cancellationToken, graph =>
        {
            graph.RequireUnstructuredContent();
            var captured = PdfRendering.RenderCore(graph, pageIndex, providers, render, graph.Context);
            using var input = captured.Samples.Open(0, captured.Samples.Length);
            var raster = PdfOcrRaster.ReadCore(input, captured.Request.Width, captured.Request.Height, captured.Request.Stride, PdfOcrPixelFormat.Bgra32, (int)render.DpiX, 67108864, graph.Context);
            var result = PdfOcr.RecognizeCore(raster, recognition, providers.GetRequired<IPdfOcrProvider>(PdfCapability.RecognizeText), graph.Context);
            graph.Context.Consume(PdfResourceKind.Objects, 1);
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, 256);
            var frame = new PdfOcrPageFrame(graph.Snapshot.Pages[pageIndex].Geometry, raster.Width, raster.Height, captured.Request.Region);
            ApplyCore(graph, pageIndex, result, frame, fonts, providers, overlay);
        }, "1.4");
    }

    private static void Validate(PdfDocumentSnapshot source, int page, PdfFontFallback fonts, PdfProviderRegistry providers)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (fonts == null)
            throw new ArgumentNullException(nameof(fonts));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        if (page < 0 || page >= source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(page));
        _ = providers.GetRequired<IPdfTextShaper>(PdfCapability.ShapeText);
    }

    internal static void ApplyCore(PdfGraphEdit graph, int index, PdfOcrResult result, PdfOcrPageFrame frame, PdfFontFallback fonts, PdfProviderRegistry providers, PdfOcrOverlayOptions options)
    {
        var c = graph.Context;
        c.Checkpoint();
        graph.RequireUnstructuredContent();
        var page = graph.Snapshot.Pages[index].Source!;
        if (!page.Geometry.Equals(frame.Geometry))
            throw Error("FrameMismatch", "The recognition frame does not match exact source page geometry.");
        if (result.Words.Count > options.MaximumWords)
            throw Error("WordLimit", "The explicit overlay word ceiling was exceeded.");
        PdfOcrOutput.Validate(result, frame.RasterWidth, frame.RasterHeight, c);
        var existing = options.ExistingTextPolicy == PdfOcrExistingTextPolicy.AllowDuplicates ? null : new PdfTextExtractionEngine(graph, new(), providers).Read(index);
        var shaper = providers.GetRequired<IPdfTextShaper>(PdfCapability.ShapeText);
        var writer = new PdfResourceWriter(graph.Edit, c, new());
        c.Consume(PdfResourceKind.ExpandedBytes, result.Words.Count * 256L + 8192);
        var operations = new List<PdfContentOperation>();
        var resources = new List<PdfResourceBinding>();
        int skipped = 0;
        long comparisons = 0;
        var prepared = new List<(PdfOcrWord Word, PdfShapedText Shaped, PdfRect Bounds)>();
        foreach (var word in result.Words)
        {
            c.Checkpoint();
            if (Overlaps(word))
            {
                if (options.ExistingTextPolicy == PdfOcrExistingTextPolicy.RejectOverlap)
                    throw Error("ExistingText", "An OCR word overlaps an existing mapped digital word.");
                skipped++;
                continue;
            }

            if (word.Text.Any(ch => ch == '\r' || ch == '\n'))
                throw Error("WordLayout", "A positioned OCR word cannot contain a paragraph break.");
            var shaped = PdfText.ShapeCore(word.Text, fonts, shaper, new(), c);
            var bounds = AdvanceBounds(shaped, c);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw Error("WordLayout", "A recognized word requires a nondegenerate shaped advance frame.");
            prepared.Add((word, shaped, bounds));
        }
        var sharedFonts = PdfTextDrawing.SharedFonts(prepared.Select(x => x.Shaped), c);
        foreach (var item in prepared)
        {
            c.Checkpoint();
            var word = item.Word;
            var bounds = item.Bounds;
            var positioned = PdfTextDrawing.CreateCore(item.Shaped, 0, 0, 1, 1, "OcrFont", true, c, sharedFonts);
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, positioned.Content.Operations.Count * 48L + 512);
            var invisible = new List<PdfContentOperation>();
            foreach (var op in positioned.Content.Operations)
            {
                invisible.Add(op);
                if (op.Operation == PdfOperator.BeginText)
                    invisible.Add(new(PdfOperator.TextRenderingMode, PdfValue.Integer(3)));
            }

            var fontBindings = positioned.Fonts.Select(f => new PdfResourceBinding(f.Key, PdfGraphicsResource.Font(f.Value))).ToArray();
            var drawing = PdfDrawing.CreateCore(invisible, new(fontBindings), c);
            var form = PdfGraphicsObjects.FormCore(drawing, bounds, null, null, c);
            string name = "Word" + resources.Count.ToString(CultureInfo.InvariantCulture);
            resources.Add(new(name, form));
            var b = word.Bounds;
            var transform = frame.PixelToUser.Multiply(new(1, 0, 0, 1, b.X, b.Y + b.Height)).Multiply(new(b.Width / bounds.Width, 0, 0, -b.Height / bounds.Height, 0, 0)).Multiply(new(1, 0, 0, 1, -bounds.X, -bounds.Y));
            operations.Add(new(PdfOperator.SaveState));
            operations.Add(new(PdfOperator.Transform, transform.Value.Items.ToArray()));
            operations.Add(new(PdfOperator.DrawObject, PdfValue.Name(name)));
            operations.Add(new(PdfOperator.RestoreState));
        }

        if (skipped != 0)
            c.Report(new("Ocr.ExistingTextRetained", "Overlapping mapped digital words were retained and their OCR overlays were skipped.", severity: PdfDiagnosticSeverity.Information));
        if (resources.Count == 0)
            return;
        var overlay = PdfGraphicsObjects.FormCore(PdfDrawing.CreateCore(operations, new(resources.ToArray()), c), page.Geometry.Boxes.Media, null, null, c);
        var entries = graph.Copy(page.Dictionary);
        var pageResources = graph.Copy(graph.Get(page.EffectiveAttributes, "Resources"));
        var xobjects = pageResources.TryGetValue("XObject", out var raw) ? graph.Copy(graph.Resolve(raw)) : new Dictionary<string, PdfValue>(StringComparer.Ordinal);
        string rootName = "OcrLayer";
        int suffix = 0;
        while (xobjects.ContainsKey(rootName))
        {
            c.Checkpoint();
            rootName = "OcrLayer" + (++suffix).ToString(CultureInfo.InvariantCulture);
        }

        xobjects[rootName] = writer.Embed(overlay);
        pageResources["XObject"] = PdfValue.OwnedDictionary(xobjects);
        // A balanced old program may still leave a changed CTM. Isolate it before invoking the new layer.
        _ = PdfPageContent.Read(page, PdfUnknownOperatorPolicy.Reject, graph.Graph.Version, new(), c);
        var original = page.Dictionary.Entries.TryGetValue("Contents", out var contents) ? contents : PdfValue.Null;
        var parts = new List<PdfValue>
        {
            State(true)
        };
        if (original.Kind != PdfValueKind.Null)
            AddContents(original, new HashSet<PdfObjectId>());
        parts.Add(State(false));
        parts.Add(Stream(new PdfContentOperation(PdfOperator.SaveState), new(PdfOperator.DrawObject, PdfValue.Name(rootName)), new(PdfOperator.RestoreState)));
        entries["Resources"] = PdfValue.OwnedDictionary(pageResources);
        entries["Contents"] = PdfValue.OwnedArray(parts);
        graph.Edit.Set(page.ObjectId, PdfValue.OwnedDictionary(entries));
        PdfValue State(bool begin)
        {
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, 128);
            var bytes = new PdfByteBuilder(c);
            bytes.Add(begin ? (byte)113 : (byte)81);
            bytes.Add(10);
            var store = bytes.Build();
            return PdfValue.Reference(graph.Edit.Add(PdfValue.StreamValue(PdfValue.OwnedDictionary(new Dictionary<string, PdfValue>(StringComparer.Ordinal)), store, 0, store.Length)));
        }

        PdfValue Stream(params PdfContentOperation[] ops) => PdfValue.Reference(graph.Edit.Add(PdfContentProgram.CreateCore(ops, PdfContentKind.Page, c).ToStream(c)));
        void AddContents(PdfValue value, HashSet<PdfObjectId> path)
        {
            c.Checkpoint();
            using var depth = c.EnterScope(PdfResourceKind.Depth);
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, 64);
            bool indirect = value.Kind == PdfValueKind.Reference;
            if (indirect && !path.Add(value.ReferenceId))
                throw Error("ContentCycle", "Original content arrays contain a reference cycle.");
            try
            {
                var resolved = graph.Resolve(value);
                if (resolved.Kind == PdfValueKind.Array)
                    foreach (var item in resolved.Items)
                        AddContents(item, path);
                else if (resolved.Kind == PdfValueKind.Stream)
                    parts.Add(indirect ? value : PdfValue.Reference(graph.Edit.Add(resolved)));
                else if (resolved.Kind != PdfValueKind.Null)
                    throw Error("Content", "Original page Contents must contain streams.");
            }
            finally
            {
                if (indirect)
                    path.Remove(value.ReferenceId);
            }
        }

        bool Overlaps(PdfOcrWord word)
        {
            if (existing == null)
                return false;
            var display = PdfGeometryMath.Transform(word.Bounds, page.Geometry.UserToDisplay().Multiply(frame.PixelToUser)).Bounds;
            foreach (var candidate in existing.Words)
            {
                c.Checkpoint();
                if (++comparisons > options.MaximumOverlapComparisons)
                    throw Error("OverlapLimit", "The explicit existing-text comparison ceiling was exceeded.");
                bool mapped = true;
                foreach (var character in candidate.Characters)
                {
                    c.Checkpoint();
                    if (!character.LayerVisible || character.GeometryEstimated || character.Confidence == PdfTextMappingConfidence.Unmapped)
                    {
                        mapped = false;
                        break;
                    }
                }

                if (!mapped)
                    continue;
                var b = candidate.Bounds;
                double area = Math.Max(0, Math.Min(b.Right, display.Right) - Math.Max(b.X, display.X)) * Math.Max(0, Math.Min(b.Top, display.Top) - Math.Max(b.Y, display.Y));
                if (area > 0 && area >= display.Width * display.Height * options.OverlapCoverage && area >= b.Width * b.Height * options.OverlapCoverage)
                    return true;
            }

            return false;
        }

        PdfException Error(string code, string message) => PdfWriterCore.Error(c, "Ocr.Overlay" + code, message);
    }

    private static PdfRect AdvanceBounds(PdfShapedText text, PdfOperationContext c)
    {
        double x = 0, y = 0;
        bool any = false;
        PdfRect bounds = default;
        foreach (var run in text.Runs)
        {
            var m = run.Request.Font.Info.Metrics;
            double scale = 1.0 / m.UnitsPerEm;
            foreach (var glyph in run.Glyphs)
            {
                c.Checkpoint();
                double advance = run.Request.Font.Program.GetAdvance(glyph.GlyphIndex, c) * scale;
                var b = new PdfRect(x + glyph.OffsetX * scale + Math.Min(0, advance), y + (glyph.OffsetY + m.Descender) * scale, Math.Abs(advance), (m.Ascender - (double)m.Descender) * scale);
                if (!any)
                    bounds = b;
                else
                {
                    double left = Math.Min(bounds.X, b.X), bottom = Math.Min(bounds.Y, b.Y);
                    bounds = new(left, bottom, Math.Max(bounds.Right, b.Right) - left, Math.Max(bounds.Top, b.Top) - bottom);
                }

                any = true;
                x += glyph.AdvanceX * scale;
                y += glyph.AdvanceY * scale;
            }
        }

        return bounds;
    }
}
