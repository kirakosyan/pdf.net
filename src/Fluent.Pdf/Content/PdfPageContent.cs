using System.IO;

namespace Fluent.Pdf;
internal static class PdfPageContent
{
    internal static PdfContentProgram Read(PdfPageSource? binding, PdfUnknownOperatorPolicy unknown, string? version, PdfProviderRegistry providers, PdfOperationContext context)
    {
        var builder = new PdfByteBuilder(context);
        if (binding != null && binding.Dictionary.Entries.TryGetValue("Contents", out var contents))
            Append(contents, new HashSet<PdfObjectId>());
        PdfValue resources = binding != null && binding.EffectiveAttributes.Entries.TryGetValue("Resources", out var declaredResources) ? declaredResources : PdfValue.Null;
        return PdfContentProgram.ParseCore(builder.Build(), PdfContentKind.Page, unknown, version, context, binding == null ? null : color => PdfImageColorResolver.Components(color, resources, v => ManagedPdfProvider.Resolve(binding.Document, v, context), context));
        void Append(PdfValue value, HashSet<PdfObjectId> path)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            using var scope = context.EnterScope(PdfResourceKind.Depth);
            if (value.Kind == PdfValueKind.Null)
                return;
            if (value.Kind == PdfValueKind.Reference)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, 64);
                var id = value.ReferenceId;
                if (!path.Add(id))
                    throw PdfContentState.Error(context, "Content.ReferenceCycle", "Page Contents contains a reference cycle.");
                try
                {
                    Append(binding!.Document.Resolve(id), path);
                }
                finally
                {
                    path.Remove(id);
                }

                return;
            }

            if (value.Kind == PdfValueKind.Array)
            {
                foreach (var item in value.Items)
                    Append(item, path);
                return;
            }

            if (value.Kind != PdfValueKind.Stream)
                throw PdfContentState.Error(context, "Content.StreamRequired", "Page Contents must resolve to a stream or an array of streams.");
            context.Consume(PdfResourceKind.InputBytes, value.RawStreamLength);
            var resolved = PdfFilterDeclarations.Resolve(value, id => binding!.Document.Resolve(id), context);
            using var decoded = PdfFilters.Decode(resolved, context, providers);
            context.Consume(PdfResourceKind.ExpandedBytes, 4096);
            var buffer = new byte[4096];
            int count;
            while ((count = decoded.Read(buffer, 0, buffer.Length)) > 0)
            {
                context.Checkpoint();
                for (int i = 0; i < count; i++)
                    builder.Add(buffer[i]);
            }

            builder.Add(10);
        }
    }
}
