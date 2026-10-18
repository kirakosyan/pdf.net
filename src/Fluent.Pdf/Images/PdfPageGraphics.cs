using System.Collections.ObjectModel;
using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>The kind of an actually invoked page graphics object.</summary>
public enum PdfGraphicsOccurrenceKind
{
    /// <summary>An image XObject.</summary>
    Image,
    /// <summary>An inline image.</summary>
    InlineImage,
    /// <summary>A reusable Form XObject.</summary>
    Form
}

/// <summary>One immutable invocation, retaining its source scope and exact encoded bytes; clipping remains in the surrounding content.</summary>
public sealed class PdfGraphicsOccurrence
{
    internal PdfGraphicsOccurrence(PdfGraphicsOccurrenceKind kind, PdfValue definition, PdfValue resources, PdfMatrix matrix, PdfGraphicsFrame[] path, int index)
    {
        Kind = kind;
        Definition = definition;
        Resources = resources;
        Transform = matrix;
        Path = path;
        OperationIndex = index;
    }

    /// <summary>Gets whether this invocation is an image, inline image or form.</summary>
    public PdfGraphicsOccurrenceKind Kind { get; }
    /// <summary>Gets the immutable original definition, whose references belong to the manifest source graph.</summary>
    public PdfValue Definition { get; }
    /// <summary>Gets the accumulated matrix into native page user coordinates, including a form's own matrix.</summary>
    public PdfMatrix Transform { get; }
    /// <summary>Gets the zero-based operator index within its immediate content stream program.</summary>
    public int OperationIndex { get; }
    internal PdfValue Resources { get; }
    internal PdfGraphicsFrame[] Path { get; }
}

internal sealed class PdfGraphicsFrame
{
    internal readonly PdfContentProgram Program;
    internal readonly PdfValue Resources;
    internal readonly PdfValue? Form;
    internal readonly int Invocation;
    internal PdfGraphicsFrame(PdfContentProgram program, PdfValue resources, PdfValue? form, int invocation)
    {
        Program = program;
        Resources = resources;
        Form = form;
        Invocation = invocation;
    }
}

/// <summary>An immutable page graphics walk. The captured graph remains valid after source disposal; no document actions or optional codecs run.</summary>
public sealed partial class PdfPageGraphics
{
    internal PdfPageGraphics(PdfDocumentSnapshot source, int page, PdfGraphicsOccurrence[] occurrences, PdfOperationReport report)
    {
        Source = source;
        PageIndex = page;
        Occurrences = new ReadOnlyCollection<PdfGraphicsOccurrence>(occurrences);
        OperationReport = report;
    }

    /// <summary>Gets the detached source snapshot to which invocation identities belong.</summary>
    public PdfDocumentSnapshot Source { get; }
    /// <summary>Gets the zero-based captured page index.</summary>
    public int PageIndex { get; }
    /// <summary>Gets invoked forms and images in depth-first paint order, including repeated uses separately.</summary>
    public IReadOnlyList<PdfGraphicsOccurrence> Occurrences { get; }
    /// <summary>Gets the completed shared-context inspection report.</summary>
    public PdfOperationReport OperationReport { get; }

    /// <summary>Walks actually invoked content and scoped Form resources under one recursion and work budget.</summary>
    public static PdfPageGraphics Inspect(PdfDocumentSnapshot source, int pageIndex, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (pageIndex < 0 || pageIndex >= source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        var occurrences = new List<PdfGraphicsOccurrence>();
        PdfDocumentSnapshot? captured = null;
        var report = PdfFont.Run("InspectPageGraphics", "managed.images", policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(source, new(), context);
            captured = graph.Snapshot;
            var page = captured.Pages[pageIndex].Source!;
            var resources = graph.Get(page.EffectiveAttributes, "Resources");
            var program = PdfPageContent.Read(page, PdfUnknownOperatorPolicy.Reject, null, PdfProviderRegistry.Default, context);
            var path = new List<PdfGraphicsFrame>
            {
                new(program, resources, null, -1)
            };
            var active = new HashSet<PdfValue>();
            Walk(program, resources, PdfMatrix.Identity);
            void Walk(PdfContentProgram content, PdfValue scope, PdfMatrix start)
            {
                using var depth = context.EnterScope(PdfResourceKind.Depth);
                var stack = new Stack<PdfMatrix>();
                var current = start;
                foreach (var indexed in content.Operations.Select((op, index) => (op, index)))
                {
                    context.Checkpoint();
                    context.Consume(PdfResourceKind.Objects, 1);
                    var op = indexed.op;
                    if (op.Operation == PdfOperator.SaveState)
                    {
                        context.Consume(PdfResourceKind.ExpandedBytes, 64);
                        stack.Push(current);
                    }
                    else if (op.Operation == PdfOperator.RestoreState)
                        current = stack.Pop();
                    else if (op.Operation == PdfOperator.Transform)
                        current = current.Multiply(new(op.Operands[0].NumberValue, op.Operands[1].NumberValue, op.Operands[2].NumberValue, op.Operands[3].NumberValue, op.Operands[4].NumberValue, op.Operands[5].NumberValue));
                    else if (op.Operation == PdfOperator.InlineImage)
                        Add(PdfGraphicsOccurrenceKind.InlineImage, op.Image!, current);
                    else if (op.Operation == PdfOperator.DrawObject)
                    {
                        var xobjects = graph.Get(scope, "XObject");
                        if (xobjects.Kind != PdfValueKind.Dictionary || !xobjects.Entries.TryGetValue(op.Operands[0].NameValue, out var value))
                            throw graph.Error("Image.ResourceMissing", "An invoked XObject is absent from its effective resource scope.");
                        value = graph.Resolve(value);
                        if (value.Kind != PdfValueKind.Stream)
                            throw graph.Error("Image.XObjectStream", "An invoked XObject must be a stream.");
                        string? kind = PdfPageImportSource.Name(value, "Subtype");
                        if (kind == "Image")
                            Add(PdfGraphicsOccurrenceKind.Image, value, current);
                        else if (kind == "Form")
                        {
                            var matrix = current.Multiply(Matrix(graph.Get(value, "Matrix"), graph));
                            Add(PdfGraphicsOccurrenceKind.Form, value, matrix);
                            if (!active.Add(value))
                                throw graph.Error("Image.FormCycle", "Invoked forms contain a recursive reference cycle.");
                            var childResources = value.Entries.ContainsKey("Resources") ? graph.Get(value, "Resources") : scope;
                            var resolved = PdfFilterDeclarations.Resolve(value, id => graph.Edit.Resolve(id), context);
                            context.Consume(PdfResourceKind.InputBytes, resolved.RawStreamLength);
                            using var decoded = PdfFilters.Decode(resolved, context, PdfProviderRegistry.Default);
                            var bytes = PdfByteStore.Read(decoded, context);
                            var child = PdfContentProgram.ParseCore(bytes, PdfContentKind.Page, PdfUnknownOperatorPolicy.Reject, null, context, color => PdfImageColorResolver.Components(color, childResources, graph.Resolve, context));
                            context.Consume(PdfResourceKind.ExpandedBytes, 128);
                            path.Add(new(child, childResources, value, indexed.index));
                            try
                            {
                                Walk(child, childResources, matrix);
                            }
                            finally
                            {
                                path.RemoveAt(path.Count - 1);
                                active.Remove(value);
                            }
                        }
                        else
                            throw graph.Error("Image.XObjectSubtype", "An invoked XObject has an unsupported subtype.");
                    }

                    void Add(PdfGraphicsOccurrenceKind kind, PdfValue value, PdfMatrix matrix)
                    {
                        context.Consume(PdfResourceKind.ExpandedBytes, 256 + path.Count * 8L);
                        occurrences.Add(new(kind, value, scope, matrix, path.ToArray(), indexed.index));
                        if (kind == PdfGraphicsOccurrenceKind.Image)
                        {
                            var width = graph.Get(value, "Width");
                            var height = graph.Get(value, "Height");
                            if (!width.TryGetInteger(out long w) || !height.TryGetInteger(out long h))
                                throw graph.Error("Image.Geometry", "Image dimensions must be positive bounded integers.");
                            if (w < 1 || h < 1 || w > int.MaxValue || h > int.MaxValue)
                                throw graph.Error("Image.Geometry", "Image dimensions must be positive bounded integers.");
                            context.ConsumePixels((int)w, (int)h);
                        }
                    }
                }
            }
        }, PdfCapability.ImageCodecs);
        return new(captured!, pageIndex, occurrences.ToArray(), report);
    }

    internal static PdfMatrix Matrix(PdfValue value, PdfGraphEdit graph)
    {
        if (value.Kind == PdfValueKind.Null)
            return PdfMatrix.Identity;
        if (value.Kind != PdfValueKind.Array || value.Items.Count != 6)
            throw graph.Error("Image.FormMatrix", "A form matrix requires six finite numbers.");
        var n = value.Items.Select(v => graph.Resolve(v).NumberValue).ToArray();
        return new(n[0], n[1], n[2], n[3], n[4], n[5]);
    }

    internal PdfGraphicsOccurrence Selected(int index) => index >= 0 && index < Occurrences.Count ? Occurrences[index] : throw new ArgumentOutOfRangeException(nameof(index));
}
