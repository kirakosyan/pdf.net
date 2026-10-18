using System.Threading;

namespace Fluent.Pdf;
public sealed partial class PdfPageGraphics
{
    /// <summary>Removes exactly one captured image or form invocation, cloning the selected enclosing form path. This is content editing, not secure redaction.</summary>
    public PdfDocument Remove(int occurrenceIndex, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => EditInvocation(Selected(occurrenceIndex), null, null, options, policy, cancellationToken);
    /// <summary>Replaces exactly one captured invocation with a detached XObject, retaining its surrounding transform and clip. Form matrices/bounds remain explicit.</summary>
    public PdfDocument ReplaceResource(int occurrenceIndex, PdfGraphicsResource replacement, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (replacement == null || replacement.Category != PdfGraphicsResourceCategory.XObject)
            throw new ArgumentException("An explicit detached image or Form XObject is required.", nameof(replacement));
        return EditInvocation(Selected(occurrenceIndex), replacement, null, options, policy, cancellationToken);
    }

    /// <summary>Replaces one image invocation within its original unit-square frame, applying the replacement's orientation explicitly without recompressing samples.</summary>
    public PdfDocument ReplaceImage(int occurrenceIndex, PdfImageAsset replacement, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (replacement == null)
            throw new ArgumentNullException(nameof(replacement));
        var occurrence = Selected(occurrenceIndex);
        if (occurrence.Kind == PdfGraphicsOccurrenceKind.Form)
            throw new ArgumentException("Select an image invocation.", nameof(occurrenceIndex));
        return EditInvocation(occurrence, replacement.Resource, replacement, options, policy, cancellationToken);
    }

    private PdfDocument EditInvocation(PdfGraphicsOccurrence occurrence, PdfGraphicsResource? replacement, PdfImageAsset? image, PdfOrganizationOptions? options, PdfOperationPolicy? policy, CancellationToken token)
    {
        return PdfGraphEdit.Run("EditGraphicsInvocation", Source, options, policy, token, graph =>
        {
            graph.RequireUnstructuredContent();
            var context = graph.Context;
            var writer = new PdfResourceWriter(graph.Edit, context, new());
            PdfValue? inserted = replacement == null ? null : writer.Embed(replacement);
            if (inserted != null && image != null && image.Metadata.Orientation != PdfImageOrientation.Normal)
            {
                var program = PdfContentProgram.CreateCore(new[] { new PdfContentOperation(PdfOperator.SaveState), new PdfContentOperation(PdfOperator.Transform, image.OrientationMatrix.Value.Items.ToArray()), new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("Image")), new PdfContentOperation(PdfOperator.RestoreState) }, PdfContentKind.Page, context);
                var stream = program.ToStream(context);
                var entries = graph.Copy(stream);
                entries["Type"] = PdfValue.Name("XObject");
                entries["Subtype"] = PdfValue.Name("Form");
                entries["BBox"] = new PdfRect(0, 0, 1, 1).Value;
                entries["Resources"] = PdfGraphicsNumbers.Dict(("XObject", PdfGraphicsNumbers.Dict(("Image", inserted))));
                inserted = PdfValue.Reference(graph.Edit.Add(stream.WithStreamDictionary(PdfValue.OwnedDictionary(entries))));
            }

            for (int level = occurrence.Path.Length - 1; level >= 0; level--)
            {
                context.Checkpoint();
                var frame = occurrence.Path[level];
                int selected = level == occurrence.Path.Length - 1 ? occurrence.OperationIndex : occurrence.Path[level + 1].Invocation;
                var resources = graph.Copy(graph.Resolve(frame.Resources));
                var xobjects = resources.TryGetValue("XObject", out var old) ? graph.Copy(graph.Resolve(old)) : new Dictionary<string, PdfValue>(StringComparer.Ordinal);
                string name = "Edited";
                int suffix = 0;
                while (xobjects.ContainsKey(name))
                {
                    context.Checkpoint();
                    name = "Edited" + (++suffix).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                if (inserted != null)
                {
                    xobjects.Add(name, inserted);
                    resources["XObject"] = PdfValue.OwnedDictionary(xobjects);
                }

                context.Consume(PdfResourceKind.ExpandedBytes, frame.Program.Operations.Count * 16L + 64);
                var operations = frame.Program.Operations.ToList();
                operations.RemoveAt(selected);
                if (inserted != null)
                    operations.Insert(selected, new(PdfOperator.DrawObject, PdfValue.Name(name)));
                var content = PdfContentProgram.CreateCore(operations, level == 0 ? PdfContentKind.Page : PdfContentKind.Page, context).ToStream(context);
                if (level == 0)
                {
                    var binding = graph.Snapshot.Pages[PageIndex].Source!;
                    var entries = graph.Copy(binding.Dictionary);
                    entries["Contents"] = PdfValue.Reference(graph.Edit.Add(content));
                    entries["Resources"] = PdfValue.OwnedDictionary(resources);
                    graph.Edit.Set(binding.ObjectId, PdfValue.OwnedDictionary(entries));
                }
                else
                {
                    var entries = graph.Copy(frame.Form!);
                    entries.Remove("Filter");
                    entries.Remove("DecodeParms");
                    entries.Remove("Length");
                    entries["Resources"] = PdfValue.OwnedDictionary(resources);
                    inserted = PdfValue.Reference(graph.Edit.Add(content.WithStreamDictionary(PdfValue.OwnedDictionary(entries))));
                }
            }
        }, replacement?.MinimumVersion ?? "1.2");
    }
}
