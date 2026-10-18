namespace Fluent.Pdf;
internal static class PdfFlowTaggedPage
{
    internal static (PdfContentProgram Content, PdfResourceSet Resources, IReadOnlyList<string> Roles) Build(PdfFlowPage page, PdfFlowOptions options, PdfOperationContext context, List<Action<PdfOperationReport>> finish)
    {
        var columns = new HashSet<int>(page.Columns.Select(c => c.PlacementIndex));
        var resources = new List<PdfResourceBinding>();
        var operations = new List<PdfContentOperation>();
        var roles = new List<string>();
        var pending = new List<PdfCanvasPlacement>();
        void Flush()
        {
            if (pending.Count == 0)
                return;
            if (resources.Count == 4096)
                throw PdfWriterCore.Error(context, "Structure.SegmentLimit", "The page exceeds the bounded logical segment count.");
            var segment = PdfCanvas.CreateCore(options.Geometry, c =>
            {
                foreach (var placement in pending)
                    c.CopyPlacement(placement, PdfMatrix.Identity);
            }, new(PdfLengthUnit.Point, margins: options.Margins), context, out var complete);
            finish.Add(complete);
            var form = PdfGraphicsObjects.FormCore(segment.Drawing, options.Geometry.Boxes.Media, null, null, context);
            finish.Add(report => form.OperationReport = report);
            string name = "S" + resources.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            context.Consume(PdfResourceKind.ExpandedBytes, 512);
            resources.Add(new(name, form));
            int mcid = roles.Count;
            roles.Add("Div");
            operations.Add(new(PdfOperator.SaveState));
            operations.Add(new(PdfOperator.BeginMarkedContentProperties, PdfValue.Name("Div"), PdfGraphicsNumbers.Dict(("MCID", PdfValue.Integer(mcid)))));
            operations.Add(new(PdfOperator.DrawObject, PdfValue.Name(name)));
            operations.Add(new(PdfOperator.EndMarkedContent));
            operations.Add(new(PdfOperator.RestoreState));
            pending.Clear();
        }

        foreach (var placement in page.Canvas.Placements)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.ExpandedBytes, 32);
            if (columns.Contains(placement.Index))
            {
                Flush();
                pending.Add(placement);
                Flush();
            }
            else
                pending.Add(placement);
        }

        Flush();
        var content = PdfContentProgram.CreateCore(operations, PdfContentKind.Page, context);
        finish.Add(report => content.OperationReport = report);
        return (content, new(resources.ToArray()), System.Array.AsReadOnly(roles.ToArray()));
    }
}
