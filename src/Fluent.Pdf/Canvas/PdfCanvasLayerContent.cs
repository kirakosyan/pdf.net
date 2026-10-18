namespace Fluent.Pdf;
internal static class PdfCanvasLayerContent
{
    internal static PdfContentProgram Combine(PdfContentProgram original, IReadOnlyList<PdfCanvasPageLayer> layers, PdfOperationContext context)
    {
        IEnumerable<PdfContentOperation> Operations()
        {
            foreach (var layer in layers.Reverse().Where(x => x.Mode == PdfCanvasApplyMode.Underlay))
                foreach (var operation in layer.Content.Operations)
                    yield return operation;
            yield return new(PdfOperator.SaveState);
            foreach (var operation in original.Operations)
                yield return operation;
            yield return new(PdfOperator.RestoreState);
            foreach (var layer in layers.Where(x => x.Mode == PdfCanvasApplyMode.Overlay))
                foreach (var operation in layer.Content.Operations)
                    yield return operation;
        }

        return PdfContentProgram.CreateCore(Operations(), PdfContentKind.Page, context);
    }

    internal static PdfValue Compose(PdfTransaction edit, PdfValue original, IReadOnlyList<PdfCanvasPageLayer> layers, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, layers.Count * 64L + 256);
        var contents = new List<PdfValue>();
        foreach (var layer in layers.Reverse().Where(x => x.Mode == PdfCanvasApplyMode.Underlay))
            contents.Add(PdfValue.Reference(edit.Add(layer.Content.ToStream(context))));
        if (original.Kind != PdfValueKind.Null)
        {
            var opening = new PdfByteBuilder(context);
            opening.Add((byte)'q');
            opening.Add(10);
            var openingBytes = opening.Build();
            contents.Add(PdfValue.Reference(edit.Add(PdfValue.StreamValue(PdfValue.Dictionary(System.Array.Empty<KeyValuePair<string, PdfValue>>()), openingBytes, 0, openingBytes.Length))));
            Append(original, new HashSet<PdfObjectId>());
            var bytes = new PdfByteBuilder(context);
            bytes.Add(10);
            bytes.Add((byte)'Q');
            bytes.Add(10);
            var store = bytes.Build();
            contents.Add(PdfValue.Reference(edit.Add(PdfValue.StreamValue(PdfValue.Dictionary(System.Array.Empty<KeyValuePair<string, PdfValue>>()), store, 0, store.Length))));
        }

        foreach (var layer in layers.Where(x => x.Mode == PdfCanvasApplyMode.Overlay))
            contents.Add(PdfValue.Reference(edit.Add(layer.Content.ToStream(context))));
        return PdfValue.OwnedArray(contents);
        void Append(PdfValue value, HashSet<PdfObjectId> path)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 64);
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            var resolved = value;
            if (value.Kind == PdfValueKind.Reference)
            {
                if (!path.Add(value.ReferenceId))
                    throw PdfWriterCore.Error(context, "Canvas.ContentCycle", "Source Contents contains a cycle.");
                resolved = edit.Resolve(value.ReferenceId);
                if (resolved.Kind == PdfValueKind.Stream)
                {
                    contents.Add(value);
                    path.Remove(value.ReferenceId);
                    return;
                }

                try
                {
                    Append(resolved, path);
                }
                finally
                {
                    path.Remove(value.ReferenceId);
                }

                return;
            }

            if (value.Kind == PdfValueKind.Array)
            {
                foreach (var item in value.Items)
                    Append(item, path);
                return;
            }

            if (value.Kind == PdfValueKind.Stream)
            {
                contents.Add(PdfValue.Reference(edit.Add(value)));
                return;
            }

            if (value.Kind != PdfValueKind.Null)
                throw PdfWriterCore.Error(context, "Canvas.ContentStream", "Source Contents must resolve to streams.");
        }
    }
}
