using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
public static partial class PdfAccessibility
{
    /// <summary>Binds up to 128 caller-proposed semantic changes to exact source bytes and passive structural observations. No suggestion is automatically accepted.</summary>
    public static PdfSemanticReviewPlan PrepareReview(Stream input, IEnumerable<PdfSemanticSuggestion> suggestions, PdfAccessibilityProfile profile = PdfAccessibilityProfile.Ua1, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (suggestions == null) throw new ArgumentNullException(nameof(suggestions));
        PdfStructureRules.Profile(profile);
        providers ??= PdfProviderRegistry.Default;
        var captured = new List<PdfSemanticSuggestion>();
        string digest = "";
        PdfAccessibilityFinding[] findings = Array.Empty<PdfAccessibilityFinding>();
        var report = PdfFont.Run("PrepareSemanticReview", "managed.accessibility", policy, cancellationToken, c =>
        {
            var bytes = PdfProfileIo.Capture(input, c); digest = PdfArchival.Digest(bytes, c);
            using var document = PdfPageOrganization.Read(bytes, 10000, c);
            using var graph = ReviewGraph(document, new(), c);
            var reader = new PdfStructureReader(graph, profile, providers);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var suggestion in suggestions)
            {
                ReviewTick(c);
                if (suggestion == null || captured.Count == 128 || !ids.Add(suggestion.Id)) throw ReviewError(c, "Inventory", "A bounded nonnull unique suggestion inventory is required.");
                if (suggestion.Kind == PdfSemanticFixKind.Element && !reader.Elements.Any(e => e.ObjectId == suggestion.ElementId)) throw ReviewError(c, "ElementIdentity", "The proposed element is absent from this exact source.");
                if (suggestion.Kind == PdfSemanticFixKind.RoleMapping && profile != PdfAccessibilityProfile.Ua1) throw ReviewError(c, "RoleMapProfile", "Namespace-aware PDF2 role mapping requires another remediation profile.");
                if (suggestion.StructurePlan != null && suggestion.StructurePlan.Profile != profile) throw ReviewError(c, "Profile", "The structure proposal must match the review profile.");
                captured.Add(suggestion);
            }
            if (captured.Count == 0 || captured.Count > 1 && captured.Any(s => s.Kind == PdfSemanticFixKind.StructurePlan)) throw ReviewError(c, "Inventory", "A complete structure proposal must stand alone; other reviews require at least one suggestion.");
            reader.Tick(reader.Findings.Count * 16L + 64); findings = reader.Findings.ToArray();
        }, PdfCapability.ReadDocument);
        return new(digest, profile, captured.ToArray(), findings, report);
    }

    /// <summary>Applies explicit decisions only to matching source bytes, in plan order. Every accepted fix is structurally checked, serialized and reparsed before delivery; pending/rejected changes remain untouched.</summary>
    public static PdfSemanticReviewAudit ApplyReview(Stream input, Stream output, PdfSemanticReviewPlan plan, IEnumerable<PdfSemanticReviewDecision> decisions, PdfProviderRegistry? providers = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite || ReferenceEquals(input, output)) throw new ArgumentException("A distinct writable borrowed output is required.", nameof(output));
        return RunReview(input, output, null, plan, decisions, providers, options, policy, cancellationToken);
    }

    /// <summary>Stages all accepted fixes and post-fix validation before atomically replacing the destination file.</summary>
    public static PdfSemanticReviewAudit ApplyReviewFile(Stream input, string path, PdfSemanticReviewPlan plan, IEnumerable<PdfSemanticReviewDecision> decisions, PdfProviderRegistry? providers = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(path, nameof(path), 32767);
        return RunReview(input, null, path, plan, decisions, providers, options, policy, cancellationToken);
    }

    private static PdfSemanticReviewAudit RunReview(Stream input, Stream? output, string? path, PdfSemanticReviewPlan plan, IEnumerable<PdfSemanticReviewDecision> decisions, PdfProviderRegistry? providers, PdfOrganizationOptions? options, PdfOperationPolicy? policy, CancellationToken token)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (decisions == null) throw new ArgumentNullException(nameof(decisions));
        providers ??= PdfProviderRegistry.Default; options ??= new();
        var entries = new List<PdfSemanticReviewEntry>();
        string finalHash = "";
        var report = PdfFont.Run("ApplySemanticReview", "managed.accessibility", policy, token, c =>
        {
            var bytes = PdfProfileIo.Capture(input, c);
            string currentHash = PdfArchival.Digest(bytes, c);
            if (currentHash != plan.SourceSha256) throw ReviewError(c, "StalePlan", "The review belongs to different exact source bytes.");
            var selected = new Dictionary<string, PdfSemanticReviewDecision>(StringComparer.Ordinal);
            foreach (var decision in decisions)
            {
                ReviewTick(c);
                if (decision == null || selected.Count == 128 || selected.ContainsKey(decision.SuggestionId) || !plan.Suggestions.Any(s => s.Id == decision.SuggestionId)) throw ReviewError(c, "Decision", "Decisions must identify distinct proposals in this exact review.");
                selected.Add(decision.SuggestionId, decision);
            }
            using var document = PdfPageOrganization.Read(bytes, 10000, c);
            using var graph = ReviewGraph(document, options, c);
            var reader = new PdfStructureReader(graph, plan.Profile, providers);
            PdfByteStore candidate = bytes;
            foreach (var suggestion in plan.Suggestions)
            {
                ReviewTick(c);
                selected.TryGetValue(suggestion.Id, out var decision);
                string beforeHash = currentHash;
                var affected = Array.Empty<PdfObjectId>();
                if (decision?.Accept == true)
                {
                    if (suggestion.Kind == PdfSemanticFixKind.Element)
                    {
                        c.Consume(PdfResourceKind.ExpandedBytes, (suggestion.ChildOrder?.Count ?? 0) * 8L + 64);
                        RemediateCore(graph, plan.Profile, suggestion.ElementId!.Value, suggestion.Role, suggestion.Language, suggestion.AlternativeText, suggestion.ActualText, suggestion.ChildOrder?.ToArray(), providers);
                        affected = new[] { suggestion.ElementId.Value };
                    }
                    else if (suggestion.Kind == PdfSemanticFixKind.RoleMapping)
                    {
                        var rootReference = reader.Raw(graph.Catalog, "StructTreeRoot");
                        if (rootReference.Kind != PdfValueKind.Reference) throw ReviewError(c, "RoleMapRoot", "An existing indirect structure root is required.");
                        var root = graph.Resolve(rootReference);
                        var map = graph.Copy(reader.Get(root, "RoleMap"));
                        map[suggestion.CustomRole!] = PdfValue.Name(suggestion.Role!);
                        var values = graph.Copy(root); values["RoleMap"] = PdfValue.OwnedDictionary(map);
                        graph.Edit.Set(rootReference.ReferenceId, PdfValue.OwnedDictionary(values));
                        reader.Tick(reader.Elements.Count * 32L + 64);
                        var mappedReader = new PdfStructureReader(graph, plan.Profile, providers);
                        affected = reader.Elements.Where(e => e.Role == suggestion.CustomRole || reader.EffectiveRole(e.Role) != mappedReader.EffectiveRole(e.Role)).Select(e => e.ObjectId).Concat(new[] { rootReference.ReferenceId }).ToArray();
                    }
                    else
                    {
                        new PdfStructureWriter(graph, suggestion.StructurePlan!, providers).Write();
                    }
                    reader = new PdfStructureReader(graph, plan.Profile, providers);
                    if (suggestion.Kind == PdfSemanticFixKind.StructurePlan)
                    { reader.Tick(reader.Elements.Count * 32L + 64); affected = reader.Elements.Select(e => e.ObjectId).ToArray(); }
                    using var staged = new PdfCodecBufferStream(c, 67108864);
                    graph.Edit.WriteCore(staged, new(allowSignatureInvalidation: options.AllowSignatureInvalidation, minimumVersion: plan.Profile == PdfAccessibilityProfile.Ua2 ? "2.0" : "1.7"), c);
                    candidate = staged.Finish(); currentHash = PdfArchival.Digest(candidate, c);
                    using var parsed = PdfPageOrganization.Read(candidate, 10000, c);
                    using var checkedGraph = ReviewGraph(parsed, options, c);
                    var checkedReader = new PdfStructureReader(checkedGraph, plan.Profile, providers);
                    if (parsed.Pages.Count != document.Pages.Count) throw ReviewError(c, "PostValidation", "Semantic remediation must retain the page inventory.");
                    reader.Tick(checkedReader.Findings.Count * 16L + 64);
                    entries.Add(new(suggestion, decision, beforeHash, currentHash, affected, checkedReader.Findings.ToArray()));
                }
                else
                {
                    reader.Tick(reader.Findings.Count * 16L + 64);
                    entries.Add(new(suggestion, decision, beforeHash, currentHash, affected, reader.Findings.ToArray()));
                }
            }
            finalHash = currentHash;
            if (path == null) PdfProfileIo.Copy(candidate, output!, c);
            else PdfAtomicFile.Write(path, c, target => PdfProfileIo.Copy(candidate, target, c));
        }, PdfCapability.WriteDocument);
        return new(plan.SourceSha256, finalHash, plan.Profile, entries.ToArray(), report);
    }

    private static PdfGraphEdit ReviewGraph(PdfDocument document, PdfOrganizationOptions options, PdfOperationContext c)
    {
        using var scope = document.UseModelContext(c);
        return new(document.CreateSnapshot(), options, c);
    }
    private static void ReviewTick(PdfOperationContext c) { c.Checkpoint(); c.Consume(PdfResourceKind.Objects, 1); c.Consume(PdfResourceKind.ExpandedBytes, 1024); }
    private static PdfException ReviewError(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Accessibility.Review." + code, message);

    /// <summary>Writes bounded schema-one JSON with source/output hashes, proposed changes, caller-asserted decisions and post-fix findings. Reviewer notes and proposed text are intentionally included; no conformance claim is made.</summary>
    public static PdfOperationReport WriteReview(PdfSemanticReviewAudit audit, Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (audit == null) throw new ArgumentNullException(nameof(audit));
        return PdfFont.Run("WriteSemanticReview", "managed.accessibility", policy, cancellationToken, c =>
        {
            using var buffer = new PdfCodecBufferStream(c, 16777216);
            using (var writer = new StreamWriter(buffer, new UTF8Encoding(false, true), 1024, true))
            {
                writer.Write("{\"schemaVersion\":1,\"humanReviewRequired\":true,\"conformanceVerified\":false,\"beforeSha256\":"); Text(audit.BeforeSha256);
                writer.Write(",\"afterSha256\":"); Text(audit.AfterSha256); writer.Write(",\"profile\":"); Text(audit.Profile.ToString()); writer.Write(",\"entries\":[");
                bool first = true;
                foreach (var entry in audit.Entries)
                {
                    ReviewTick(c); if (!first) writer.Write(','); first = false;
                    writer.Write("{\"id\":"); Text(entry.Suggestion.Id); writer.Write(",\"kind\":"); Text(entry.Suggestion.Kind.ToString());
                    writer.Write(",\"reason\":"); Text(entry.Suggestion.Reason); writer.Write(",\"status\":"); Text(entry.Decision == null ? "pending" : entry.Applied ? "applied" : "rejected");
                    writer.Write(",\"element\":"); Text(entry.Suggestion.ElementId?.ToString());
                    writer.Write(",\"reviewer\":"); Text(entry.Decision?.Reviewer); writer.Write(",\"note\":"); Text(entry.Decision?.Note);
                    writer.Write(",\"beforeSha256\":"); Text(entry.BeforeSha256); writer.Write(",\"afterSha256\":"); Text(entry.AfterSha256);
                    writer.Write(",\"role\":"); Text(entry.Suggestion.Role); writer.Write(",\"customRole\":"); Text(entry.Suggestion.CustomRole);
                    writer.Write(",\"language\":"); Text(entry.Suggestion.Language); writer.Write(",\"alternativeText\":"); Text(entry.Suggestion.AlternativeText);
                    writer.Write(",\"actualText\":"); Text(entry.Suggestion.ActualText); writer.Write(",\"childOrder\":");
                    if (entry.Suggestion.ChildOrder == null) writer.Write("null");
                    else { writer.Write('['); bool initial = true; foreach (int child in entry.Suggestion.ChildOrder) { c.Checkpoint(); if (!initial) writer.Write(','); initial = false; writer.Write(child.ToString(CultureInfo.InvariantCulture)); } writer.Write(']'); }
                    writer.Write(",\"structurePlan\":");
                    if (entry.Suggestion.StructurePlan == null) writer.Write("null");
                    else
                    {
                        var plan = entry.Suggestion.StructurePlan;
                        writer.Write("{\"language\":"); Text(plan.Language); writer.Write(",\"profile\":"); Text(plan.Profile.ToString());
                        writer.Write(",\"replaceExisting\":" + (plan.ReplaceExisting ? "true" : "false") + ",\"root\":"); Structure(plan.Root); writer.Write('}');
                    }
                    writer.Write(",\"affectedObjects\":["); bool start = true;
                    foreach (var id in entry.AffectedObjects) { c.Checkpoint(); if (!start) writer.Write(','); start = false; Text(id.ToString()); }
                    writer.Write("],\"findings\":["); start = true;
                    foreach (var finding in entry.Findings) { c.Checkpoint(); if (!start) writer.Write(','); start = false; writer.Write("{\"rule\":"); Text(finding.Rule); writer.Write(",\"location\":"); Text(finding.Location); writer.Write('}'); }
                    writer.Write("]}");
                }
                writer.Write("]}"); writer.Flush();
                void Structure(PdfStructureItem item)
                {
                    ReviewTick(c); using var depth = c.EnterScope(PdfResourceKind.Depth);
                    if (item is PdfStructureContent content)
                    {
                        writer.Write("{\"page\":" + content.PageIndex.ToString(CultureInfo.InvariantCulture) + ",\"mcid\":" + content.Mcid.ToString(CultureInfo.InvariantCulture) + ",\"stream\":");
                        Text(content.StreamId?.ToString()); writer.Write(",\"annotation\":"); Text(content.AnnotationId?.ToString()); writer.Write('}'); return;
                    }
                    var element = (PdfStructureElement)item;
                    writer.Write("{\"role\":"); Text(element.Role); writer.Write(",\"language\":"); Text(element.Language);
                    writer.Write(",\"alternativeText\":"); Text(element.AlternativeText); writer.Write(",\"actualText\":"); Text(element.ActualText);
                    writer.Write(",\"identifier\":"); Text(element.Identifier); writer.Write(",\"table\":");
                    if (element.Table == null) writer.Write("null");
                    else
                    {
                        var table = element.Table;
                        writer.Write("{\"rowSpan\":" + table.RowSpan.ToString(CultureInfo.InvariantCulture) + ",\"columnSpan\":" + table.ColumnSpan.ToString(CultureInfo.InvariantCulture) + ",\"scope\":"); Text(table.HeaderScope);
                        writer.Write(",\"summary\":"); Text(table.Summary); writer.Write(",\"headers\":["); bool initial = true;
                        foreach (string header in table.Headers) { if (!initial) writer.Write(','); initial = false; Text(header); } writer.Write("]}");
                    }
                    writer.Write(",\"items\":["); bool firstItem = true;
                    foreach (var child in element.Items) { if (!firstItem) writer.Write(','); firstItem = false; Structure(child); }
                    writer.Write("]}");
                }
                void Text(string? text)
                {
                    if (text == null) { writer.Write("null"); return; }
                    c.Consume(PdfResourceKind.ExpandedBytes, text.Length * 16L + 64); writer.Write('"');
                    foreach (char ch in text)
                    {
                        c.Checkpoint();
                        if (ch == '"') writer.Write("\\\""); else if (ch == '\\') writer.Write("\\\\");
                        else if (ch < 32 || char.IsSurrogate(ch)) writer.Write("\\u" + ((int)ch).ToString("x4", CultureInfo.InvariantCulture)); else writer.Write(ch);
                    }
                    writer.Write('"');
                }
            }
            PdfProfileIo.Copy(buffer.Finish(), output, c);
        }, PdfCapability.ReadDocument);
    }
}
