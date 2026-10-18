using System.Threading;

namespace Fluent.Pdf;
/// <summary>The explicit visibility rules used for static appearance flattening.</summary>
public enum PdfAnnotationView
{
    /// <summary>Screen visibility, including Hidden and NoView.</summary>
    Screen,
    /// <summary>Print visibility, requiring Print and excluding Hidden/Invisible.</summary>
    Print
}

public sealed partial class PdfAnnotationSet
{
    /// <summary>Paints selected normal appearances into native page content and removes their interactivity. Rich-media poster conversion requires explicit permissive loss policy.</summary>
    public PdfDocument Flatten(IEnumerable<int> annotationIndices, PdfAnnotationView view = PdfAnnotationView.Screen, bool cascade = true, bool allowRichMediaPosterOnly = false, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(typeof(PdfAnnotationView), view))
            throw new ArgumentOutOfRangeException(nameof(view));
        return PdfGraphEdit.Run("FlattenAnnotations", Source, options, policy, cancellationToken, graph =>
        {
            graph.RequireUnstructuredContent();
            var context = graph.Context;
            var chosen = Selection(annotationIndices, context);
            IncludeRelated(chosen, graph, cascade);
            var programs = new Dictionary<int, List<PdfContentOperation>>();
            var scopes = new Dictionary<int, Dictionary<string, PdfValue>>();
            foreach (var record in Annotations)
            {
                context.Checkpoint();
                if (!chosen.Contains(record.Index) || record.Subtype == "Popup")
                    continue;
                if (record.Subtype == "Widget")
                    throw graph.Error("Annotation.WidgetFlattening", "Widget flattening requires the form-field ownership profile.");
                if (record.Subtype == "RichMedia")
                {
                    if (!allowRichMediaPosterOnly)
                        throw graph.Error("Annotation.RichMediaPosterPolicy", "Static rich-media flattening requires an explicit poster-only loss policy.");
                    context.Report(new("Annotation.RichMediaPosterOnly", "The explicit static conversion retains only the normal poster appearance and removes media interaction.", severity: PdfDiagnosticSeverity.Warning, lossCategory: PdfLossCategory.OmittedContent));
                }

                if (record.Definition.Entries.ContainsKey("OC") || record.Definition.Entries.ContainsKey("StructParent"))
                    throw graph.Error("Annotation.OwnerDependentFlattening", "Optional-content and structure ownership require an explicit flattening profile.");
                long flags = 0;
                if (record.Definition.Entries.TryGetValue("F", out var declared) && (!graph.Resolve(declared).TryGetInteger(out flags) || flags < 0 || flags > 1023))
                    throw graph.Error("Annotation.Flags", "Annotation flags must fit the standard ten-bit field.");
                bool standard = new[]
                {
                    "Text",
                    "Link",
                    "FreeText",
                    "Line",
                    "Square",
                    "Circle",
                    "Polygon",
                    "PolyLine",
                    "Highlight",
                    "Underline",
                    "Squiggly",
                    "StrikeOut",
                    "Stamp",
                    "Caret",
                    "Ink",
                    "Popup",
                    "FileAttachment",
                    "Sound",
                    "Movie",
                    "Screen",
                    "Widget",
                    "PrinterMark",
                    "TrapNet",
                    "Watermark",
                    "3D",
                    "Redact",
                    "Projection",
                    "RichMedia"
                }.Contains(record.Subtype, StringComparer.Ordinal);
                if ((flags & 2) != 0 || !standard && (flags & 1) != 0 || view == PdfAnnotationView.Screen && (flags & 32) != 0 || view == PdfAnnotationView.Print && (flags & 4) == 0)
                    continue;
                if ((flags & (8 | 16 | 256)) != 0)
                    throw graph.Error("Annotation.DynamicViewFlags", "NoZoom, NoRotate and ToggleNoView require an explicit view-dependent flattening profile.");
                if (record.Bounds.Width == 0 || record.Bounds.Height == 0)
                    continue;
                if (record.Definition.Entries.ContainsKey("BM"))
                    throw graph.Error("Annotation.AppearanceCompositing", "Annotation-level blend overrides require an explicit compositing profile.");
                var pageBinding = graph.Snapshot.Pages[record.PageIndex].Source!;
                if (pageBinding.Dictionary.Entries.ContainsKey("Group"))
                    throw graph.Error("Annotation.PageGroupFlattening", "Flattening into a page transparency group requires an explicit compositing profile.");
                var ap = graph.Get(record.Definition, "AP");
                var appearance = graph.Get(ap, "N");
                PdfValue reference = ap.Kind == PdfValueKind.Dictionary && ap.Entries.TryGetValue("N", out var n) ? n : PdfValue.Null;
                if (appearance.Kind == PdfValueKind.Dictionary)
                {
                    var state = graph.Get(record.Definition, "AS");
                    if (state.Kind != PdfValueKind.Name || !appearance.Entries.TryGetValue(state.NameValue, out var selected))
                        throw graph.Error("Annotation.AppearanceState", "Normal appearance subdictionaries require an explicit matching AS state.");
                    reference = selected;
                    appearance = graph.Resolve(reference);
                }

                if (appearance.Kind != PdfValueKind.Stream || PdfPageImportSource.Name(appearance, "Subtype") != "Form")
                    throw graph.Error("Annotation.AppearanceRequired", "Static flattening requires a valid explicit normal Form appearance; none is synthesized from metadata.");
                var bounds = PdfAnnotation.ReadRect(graph.Get(appearance, "BBox"), graph.Resolve, context);
                var matrix = appearance.Entries.TryGetValue("Matrix", out var m) ? PdfAnnotation.ReadMatrix(m, graph.Resolve, context) : PdfMatrix.Identity;
                var transformed = PdfGeometryMath.Transform(bounds, matrix).Bounds;
                if (transformed.Width <= 0 || transformed.Height <= 0)
                    throw graph.Error("Annotation.AppearanceBounds", "The normal appearance has degenerate transformed bounds.");
                double sx = record.Bounds.Width / transformed.Width, sy = record.Bounds.Height / transformed.Height;
                var mapping = new PdfMatrix(sx, 0, 0, sy, record.Bounds.X - transformed.X * sx, record.Bounds.Y - transformed.Y * sy);
                if (!programs.TryGetValue(record.PageIndex, out var operations))
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, 512);
                    programs.Add(record.PageIndex, operations = new());
                    var page = graph.Snapshot.Pages[record.PageIndex].Source!;
                    var resources = graph.Copy(graph.Get(page.EffectiveAttributes, "Resources"));
                    scopes.Add(record.PageIndex, resources);
                }

                var scope = scopes[record.PageIndex];
                var xobjects = scope.TryGetValue("XObject", out var old) ? graph.Copy(graph.Resolve(old)) : new Dictionary<string, PdfValue>(StringComparer.Ordinal);
                string name = "Flattened";
                int sequence = 0;
                while (xobjects.ContainsKey(name))
                {
                    context.Checkpoint();
                    name = "Flattened" + (++sequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                if (reference.Kind != PdfValueKind.Reference)
                    reference = PdfValue.Reference(graph.Edit.Add(appearance));
                xobjects.Add(name, reference);
                scope["XObject"] = PdfValue.OwnedDictionary(xobjects);
                context.Consume(PdfResourceKind.ExpandedBytes, 512);
                operations.Add(new(PdfOperator.SaveState));
                operations.Add(new(PdfOperator.Transform, mapping.Value.Items.ToArray()));
                operations.Add(new(PdfOperator.DrawObject, PdfValue.Name(name)));
                operations.Add(new(PdfOperator.RestoreState));
            }

            RemoveSelected(chosen, graph);
            foreach (var pair in programs)
            {
                var binding = graph.Snapshot.Pages[pair.Key].Source!;
                PdfPageContent.Read(binding, PdfUnknownOperatorPolicy.Reject, null, PdfProviderRegistry.Default, context);
                var entries = graph.Copy(graph.Edit.Resolve(binding.ObjectId));
                var previous = entries.TryGetValue("Contents", out var content) ? content : PdfValue.Null;
                var prefix = new PdfByteBuilder(context);
                prefix.Add(113);
                prefix.Add(10);
                var pre = PdfValue.StreamValue(PdfGraphicsNumbers.Dict(), prefix.Build(), 0, 2);
                var program = PdfContentProgram.CreateCore(pair.Value, PdfContentKind.Page, context).ToStream(context);
                var suffix = new PdfByteBuilder(context);
                suffix.Add(81);
                suffix.Add(10);
                using (var data = program.OpenRawStream())
                {
                    int b;
                    while ((b = data.ReadByte()) >= 0)
                    {
                        context.Checkpoint();
                        suffix.Add((byte)b);
                    }
                }

                var bytes = suffix.Build();
                var post = PdfValue.StreamValue(PdfGraphicsNumbers.Dict(), bytes, 0, bytes.Length);
                var streams = new List<PdfValue>
                {
                    PdfValue.Reference(graph.Edit.Add(pre))
                };
                void Append(PdfValue declared)
                {
                    context.Checkpoint();
                    using var depth = context.EnterScope(PdfResourceKind.Depth);
                    context.Consume(PdfResourceKind.Objects, 1);
                    var value = graph.Resolve(declared);
                    if (value.Kind == PdfValueKind.Null)
                        return;
                    if (value.Kind == PdfValueKind.Array)
                    {
                        foreach (var child in value.Items)
                            Append(child);
                        return;
                    }

                    if (value.Kind != PdfValueKind.Stream)
                        throw graph.Error("Annotation.SourceContents", "Page contents must be streams or arrays of streams.");
                    context.Consume(PdfResourceKind.ExpandedBytes, 16);
                    streams.Add(declared.Kind == PdfValueKind.Reference ? declared : PdfValue.Reference(graph.Edit.Add(value)));
                }

                Append(previous);
                streams.Add(PdfValue.Reference(graph.Edit.Add(post)));
                entries["Contents"] = PdfValue.OwnedArray(streams);
                entries["Resources"] = PdfValue.OwnedDictionary(scopes[pair.Key]);
                graph.Edit.Set(binding.ObjectId, PdfValue.OwnedDictionary(entries));
            }

            context.Report(new("Annotation.Flattened", "Selected annotation appearances were committed to page content and their annotation entries removed.", severity: PdfDiagnosticSeverity.Information));
        });
    }
}
