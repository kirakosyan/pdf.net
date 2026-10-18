namespace Fluent.Pdf;
/// <summary>The context in which a complete content program executes.</summary>
public enum PdfContentKind
{
    /// <summary>Page or form content.</summary>
    Page,
    /// <summary>A Type 3 font glyph procedure.</summary>
    Type3Glyph
}

internal static class PdfContentState
{
    internal static void Validate(IReadOnlyList<PdfContentOperation> operations, PdfContentKind kind, PdfOperationContext context)
    {
        var graphics = new Stack<IDisposable>();
        var marked = new Stack<IDisposable>();
        var compatibility = new Stack<IDisposable>();
        IDisposable? text = null;
        bool glyphMetrics = false;
        int index = 0;
        bool uncoloredGlyph=false;
        try
        {
            foreach (var operation in operations)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                var code = operation.Operation;
                switch (code)
                {
                    case PdfOperator.SaveState:
                        graphics.Push(context.EnterScope(PdfResourceKind.Depth));
                        break;
                    case PdfOperator.RestoreState:
                        if (graphics.Count == 0)
                            throw Error(context, "Content.GraphicsUnderflow", "Q has no matching q.");
                        graphics.Pop().Dispose();
                        break;
                    case PdfOperator.BeginText:
                        if (text != null)
                            throw Error(context, "Content.NestedText", "Text objects cannot nest.");
                        text = context.EnterScope(PdfResourceKind.Depth);
                        break;
                    case PdfOperator.EndText:
                        if (text == null)
                            throw Error(context, "Content.TextUnderflow", "ET has no matching BT.");
                        text.Dispose();
                        text = null;
                        break;
                    case PdfOperator.BeginMarkedContent:
                    case PdfOperator.BeginMarkedContentProperties:
                        marked.Push(context.EnterScope(PdfResourceKind.Depth));
                        break;
                    case PdfOperator.EndMarkedContent:
                        if (marked.Count == 0)
                            throw Error(context, "Content.MarkedUnderflow", "EMC has no matching marked-content begin.");
                        marked.Pop().Dispose();
                        break;
                    case PdfOperator.BeginCompatibility:
                        compatibility.Push(context.EnterScope(PdfResourceKind.Depth));
                        break;
                    case PdfOperator.EndCompatibility:
                        if (compatibility.Count == 0)
                            throw Error(context, "Content.CompatibilityUnderflow", "EX has no matching BX.");
                        compatibility.Pop().Dispose();
                        break;
                    case PdfOperator.MoveText:
                    case PdfOperator.MoveTextLeading:
                    case PdfOperator.TextMatrix:
                    case PdfOperator.NextTextLine:
                    case PdfOperator.ShowText:
                    case PdfOperator.ShowPositionedText:
                    case PdfOperator.NextLineShowText:
                    case PdfOperator.SpacedNextLineShowText:
                        if (text == null)
                            throw Error(context, "Content.TextRequired", "Text positioning/showing requires an active text object.");
                        break;
                    case PdfOperator.GlyphWidth:
                    case PdfOperator.GlyphBounds:
                        if (kind != PdfContentKind.Type3Glyph || glyphMetrics || index != 0)
                            throw Error(context, "Content.GlyphMetrics", "Glyph metrics must be the first operator, once, in a Type 3 glyph program.");
                        glyphMetrics = true;
                        uncoloredGlyph=code==PdfOperator.GlyphBounds;
                        break;
                }

                if (text != null && ForbiddenInText(code))
                    throw Error(context, "Content.GraphicsInsideText", "Path construction/painting and object painting require text objects to be closed.");
                if(uncoloredGlyph&&(SetsColor(code)||code==PdfOperator.Shading||code==PdfOperator.InlineImage&&!Mask(operation)))throw Error(context,"Content.Type3Color","A d1 glyph cannot set colors or paint a non-mask inline image.");
                index++;
            }

            if (text != null || graphics.Count != 0 || marked.Count != 0 || compatibility.Count != 0)
                throw Error(context, "Content.UnbalancedState", "The complete program leaves graphics, text, marked-content or compatibility state open.");
            if (kind == PdfContentKind.Type3Glyph && !glyphMetrics)
                throw Error(context, "Content.GlyphMetrics", "A Type 3 glyph program requires width or width/bounds metrics.");
        }
        finally
        {
            text?.Dispose();
            foreach (var stack in new[]
            {
                graphics,
                marked,
                compatibility
            }

            )
                while (stack.Count > 0)
                    stack.Pop().Dispose();
        }
    }

    private static bool ForbiddenInText(PdfOperator code) => code == PdfOperator.SaveState || code == PdfOperator.RestoreState || code == PdfOperator.Transform || code == PdfOperator.MoveTo || code == PdfOperator.LineTo || code == PdfOperator.CurveTo || code == PdfOperator.CurveFromCurrent || code == PdfOperator.CurveToEndpoint || code == PdfOperator.ClosePath || code == PdfOperator.Rectangle || code == PdfOperator.Stroke || code == PdfOperator.CloseStroke || code == PdfOperator.Fill || code == PdfOperator.LegacyFill || code == PdfOperator.FillEvenOdd || code == PdfOperator.FillStroke || code == PdfOperator.FillStrokeEvenOdd || code == PdfOperator.CloseFillStroke || code == PdfOperator.CloseFillStrokeEvenOdd || code == PdfOperator.EndPath || code == PdfOperator.Clip || code == PdfOperator.ClipEvenOdd || code == PdfOperator.DrawObject || code == PdfOperator.Shading || code == PdfOperator.InlineImage;
    internal static PdfException Error(PdfOperationContext context, string code, string message) => PdfWriterCore.Error(context, code, message);
    private static bool SetsColor(PdfOperator code)=>code==PdfOperator.StrokeColorSpace||code==PdfOperator.FillColorSpace||code==PdfOperator.StrokeColor||code==PdfOperator.FillColor||code==PdfOperator.StrokeColorExtended||code==PdfOperator.FillColorExtended||code==PdfOperator.StrokeGray||code==PdfOperator.FillGray||code==PdfOperator.StrokeRgb||code==PdfOperator.FillRgb||code==PdfOperator.StrokeCmyk||code==PdfOperator.FillCmyk;
    private static bool Mask(PdfContentOperation operation)
    {var image=operation.Image!;return (image.Entries.TryGetValue("ImageMask",out var value)||image.Entries.TryGetValue("IM",out value))&&value.Kind==PdfValueKind.Boolean&&value.BooleanValue;}
}
