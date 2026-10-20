using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Strict passive syntax/active-reference/orphan inspection, without recovery or action execution.</summary>
public static class PdfStructuralValidation
{
    /// <summary>Parses normally and reports active graph observations. Syntax failures stay fatal in both modes; no repair scanner is invoked.</summary>
    public static PdfStructuralInspection Inspect(Stream input, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (!input.CanRead)
            throw new ArgumentException("Readable borrowed input is required.", nameof(input));
        PdfSyntaxDocument? syntax = null;
        PdfRepairRecord[] findings = Array.Empty<PdfRepairRecord>();
        var report = PdfFont.Run("InspectStructure", "managed.structure", policy, cancellationToken, context =>
        {
            using var lease = new BorrowedStream(input, true, context);
            var bytes = PdfByteStore.Read(lease, context);
            syntax = new PdfSyntaxReader(bytes, context).Read();
            var records = new List<PdfRepairRecord>();
            var active = Active(syntax, context);
            var reachable = Reach(syntax.Revisions[0].Trailer, active, id => syntax.Resolve(id), context, (source, target) => Add("Structure.UnresolvedReference", source, "A reference to " + target.Number + " " + target.Generation + " resolves to PDF null because its exact active identity is missing."));
            foreach (var id in active.OrderBy(id => id.Number))
                if (!reachable.Contains(id))
                {
                    Add("Structure.OrphanObject", id, "An active indexed object is unreachable from the current trailer; no object was modified or discarded.");
                    InspectReferences(syntax.Resolve(id), id, active, context, (owner, target) => Add("Structure.UnresolvedReference", owner, "An orphan contains a reference to missing exact identity " + target.Number + " " + target.Generation + "."));
                }

            context.Consume(PdfResourceKind.ExpandedBytes, records.Count * 8L);
            findings = records.ToArray();
            void Add(string code, PdfObjectId? id, string message)
            {
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, message.Length * 2L + 256);
                records.Add(new(code, id, null, message));
            }
        }, PdfCapability.ReadDocument);
        syntax!.OperationReport = report;
        return new(syntax, findings, report);
    }

    internal static void InspectReferences(PdfValue value, PdfObjectId owner, HashSet<PdfObjectId> active, PdfOperationContext context, Action<PdfObjectId?, PdfObjectId> missing)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        using var depth = context.EnterScope(PdfResourceKind.Depth);
        if (value.Kind == PdfValueKind.Reference)
        {
            if (!active.Contains(value.ReferenceId))
                missing(owner, value.ReferenceId);
        }
        else if (value.Kind == PdfValueKind.Array)
            foreach (var child in value.Items)
                InspectReferences(child, owner, active, context, missing);
        else if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
            foreach (var child in value.Entries.Values)
                InspectReferences(child, owner, active, context, missing);
    }

    internal static HashSet<PdfObjectId> Active(PdfSyntaxDocument syntax, PdfOperationContext context)
    {
        var seen = new HashSet<int>();
        var active = new HashSet<PdfObjectId>();
        foreach (var revision in syntax.Revisions)
            foreach (var entry in revision.Entries)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.ExpandedBytes, 96);
                if (seen.Add(entry.Id.Number) && entry.Kind != PdfCrossReferenceKind.Free)
                    active.Add(entry.Id);
            }

        return active;
    }

    internal static HashSet<PdfObjectId> Reach(PdfValue trailer, HashSet<PdfObjectId> active, Func<PdfObjectId, PdfValue> resolve, PdfOperationContext context, Action<PdfObjectId?, PdfObjectId> missing)
    {
        var reached = new HashSet<PdfObjectId>();
        var pending = new Queue<PdfObjectId>();
        Visit(trailer, null);
        while (pending.Count != 0)
        {
            context.Checkpoint();
            var id = pending.Dequeue();
            Visit(resolve(id), id);
        }

        return reached;
        void Visit(PdfValue value, PdfObjectId? owner)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            if (value.Kind == PdfValueKind.Reference)
            {
                if (!active.Contains(value.ReferenceId))
                {
                    missing(owner, value.ReferenceId);
                    return;
                }

                if (reached.Add(value.ReferenceId))
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, 128);
                    pending.Enqueue(value.ReferenceId);
                }
            }
            else if (value.Kind == PdfValueKind.Array)
                foreach (var child in value.Items)
                    Visit(child, owner);
            else if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
                foreach (var child in value.Entries.Values)
                    Visit(child, owner);
        }
    }
}
