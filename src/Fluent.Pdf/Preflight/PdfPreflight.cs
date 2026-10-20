using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Passive source preflight and explicit full-rewrite remediation with exact source binding, post-fix verification and machine-readable audit.</summary>
public static class PdfPreflight
{
    private const string ActionLoss = "Default-policy blocked actions and opaque active-content definitions/attachment slots are removed from all active objects. Original behavior is lost; full rewrite is not secure erasure.";
    /// <summary>Captures at most64 MiB and inspects actions, font embedding/mapping, image geometry, signatures and explicit capability declarations without mutation or payload execution. Complete standard conformance remains unvalidated.</summary>
    public static PdfPreflightReport Inspect(Stream input, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Input(input);
        providers ??= PdfProviderRegistry.Empty;
        PdfPreflightReport? result = null;
        var report = PdfFont.Run("InspectPreflight", "managed.preflight", policy, cancellationToken, c =>
        {
            var bytes = PdfProfileIo.Capture(input, c);
            using var doc = PdfPageOrganization.Read(bytes, 10000, c);
            result = Read(bytes, doc, providers, c);
        }, PdfCapability.ReadDocument);
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Applies an explicitly selected plan only to matching source bytes. All fixes require permissive declared-loss authority; stages and verifies output before caller writes. Original bytes/history are not mutated.</summary>
    public static PdfRemediationAudit Apply(Stream input, Stream output, PdfRemediationPlan plan, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Input(input);
        if (output == null || !output.CanWrite || ReferenceEquals(input, output))
            throw new ArgumentException("A distinct writable borrowed remediation output is required.", nameof(output));
        return Run(input, output, null, plan, policy, cancellationToken);
    }

    /// <summary>Stages full rewrite and post-fix verification before atomic destination replacement.</summary>
    public static PdfRemediationAudit ApplyFile(Stream input, string path, PdfRemediationPlan plan, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Input(input);
        Guard.Text(path, nameof(path), 32767);
        return Run(input, null, path, plan, policy, cancellationToken);
    }

    /// <summary>Writes stable schema1 JSON with exact source binding and all findings; reports never claim complete standard compliance.</summary>
    public static PdfOperationReport WriteReport(PdfPreflightReport report, Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));
        Output(output);
        return PdfFont.Run("WritePreflightReport", "managed.preflight", policy, cancellationToken, c => WriteJson(report, null, output, c), PdfCapability.ReadDocument);
    }

    /// <summary>Writes stable schema1 audit JSON with before/after hashes, findings, selected fixes, affected object locations and declared loss descriptions.</summary>
    public static PdfOperationReport WriteAudit(PdfRemediationAudit audit, Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (audit == null)
            throw new ArgumentNullException(nameof(audit));
        Output(output);
        return PdfFont.Run("WriteRemediationAudit", "managed.preflight", policy, cancellationToken, c => WriteJson(audit.Before, audit, output, c), PdfCapability.ReadDocument);
    }

    private static PdfRemediationAudit Run(Stream input, Stream? output, string? path, PdfRemediationPlan plan, PdfOperationPolicy? policy, CancellationToken token)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        PdfRemediationAudit? audit = null;
        var report = PdfFont.Run("RemediatePreflight", "managed.preflight", policy, token, c =>
        {
            var bytes = PdfProfileIo.Capture(input, c);
            if (PdfArchival.Digest(bytes, c) != plan.SourceSha256)
                throw Error(c, "StalePlan", "The remediation plan belongs to different exact source bytes.");
            using var doc = PdfPageOrganization.Read(bytes, 10000, c);
            var before = Read(bytes, doc, PdfProviderRegistry.Empty, c);
            if (before.HasSignatures)
            {
                if (!plan.AllowSignatureInvalidation)
                    throw Error(c, "SignatureAuthority", "The explicit full rewrite can invalidate source signatures and requires separate authority.");
                c.Report(new("Preflight.SignatureInvalidated", "The explicit rewrite can invalidate source signature bytes.", lossCategory: PdfLossCategory.FidelityReduction));
            }

            foreach (string fix in plan.Fixes)
            {
                if (fix != "RemoveBlockedActions" || !before.Findings.Any(f => f.RemediationCode == fix))
                    throw Error(c, "FixUnavailable", "The explicitly selected fix is not applicable to this exact source.");
                c.Report(new("Preflight.RemoveBlockedActions", ActionLoss, lossCategory: PdfLossCategory.FidelityReduction));
            }

            PdfDocumentSnapshot snapshot;
            using (doc.UseModelContext(c))
                snapshot = doc.CreateSnapshot();
            using var graph = new PdfGraphEdit(snapshot, new(allowSignatureInvalidation: plan.AllowSignatureInvalidation), c);
            PdfActionInventory.Sanitize(new(graph), new());
            if (PdfActionInventory.Read(new(graph), new()).Any(a => !a.IsAllowed))
                throw Error(c, "PostValidation", "Blocked actions remain in the staged graph.");
            using var staged = new PdfCodecBufferStream(c, 67108864);
            graph.Edit.WriteCore(staged, new(version: graph.Graph.Version, allowSignatureInvalidation: plan.AllowSignatureInvalidation), c);
            var candidate = staged.Finish();
            using var fixedDocument = PdfPageOrganization.Read(candidate, 10000, c);
            if (fixedDocument.Pages.Count != doc.Pages.Count)
                throw Error(c, "PostValidation", "The explicit action fix changed the page inventory.");
            for (int n = 0; n < doc.Pages.Count; n++)
            {
                c.Checkpoint();
                if (!doc.Pages[n].Geometry.Equals(fixedDocument.Pages[n].Geometry))
                    throw Error(c, "PostValidation", "The explicit action fix changed page geometry.");
            }

            var after = Read(candidate, fixedDocument, PdfProviderRegistry.Empty, c);
            if (after.Findings.Any(f => f.RemediationCode == "RemoveBlockedActions"))
                throw Error(c, "PostValidation", "The complete rewritten bytes still contain blocked actions.");
            audit = new(before, after, plan, null);
            if (path == null)
                PdfProfileIo.Copy(candidate, output!, c);
            else
                PdfAtomicFile.Write(path, c, target => PdfProfileIo.Copy(candidate, target, c));
        }, PdfCapability.WriteDocument);
        audit!.Before.OperationReport = report;
        audit.After.OperationReport = report;
        audit.OperationReport = report;
        return audit;
    }

    private static PdfPreflightReport Read(PdfByteStore bytes, PdfDocument doc, PdfProviderRegistry providers, PdfOperationContext c)
    {
        if (doc.Pages.Count == 0)
            throw Error(c, "EmptyInput", "A nonempty valid PDF is required for source preflight.");
        PdfDocumentSnapshot snapshot;
        using (doc.UseModelContext(c))
            snapshot = doc.CreateSnapshot();
        using var graph = new PdfGraphEdit(snapshot, new(), c);
        var findings = new List<PdfPreflightFinding>();
        foreach (var action in PdfActionInventory.Read(new(graph), new()))
            if (!action.IsAllowed)
                Add("Security.BlockedAction", action.Location, PdfDiagnosticSeverity.Warning, "RemoveBlockedActions", ActionLoss);
        foreach (var id in graph.Edit.ActiveIds(c))
        {
            c.Consume(PdfResourceKind.ExpandedBytes, 128);
            Visit(graph.Edit.Resolve(id), id.Number + " " + id.Generation + " obj");
        }

        bool signatures = graph.Graph.HasSignatureByteRanges;
        if (signatures)
            Add("Security.SignaturePresent", "document", PdfDiagnosticSeverity.Information);
        Add("Conformance.NotValidated", "document", PdfDiagnosticSeverity.Information);
        return new(PdfArchival.Digest(bytes, c), findings, providers, signatures, null);
        void Add(string code, string location, PdfDiagnosticSeverity severity, string? fix = null, string? loss = null)
        {
            c.Checkpoint();
            if (findings.Count == 10000)
                throw Error(c, "FindingExtent", "The bounded preflight finding inventory exceeds10000.");
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, (code.Length + location.Length + (loss?.Length ?? 0)) * 4L + 256);
            findings.Add(new(code, location, severity, fix, loss));
        }

        void Visit(PdfValue value, string location)
        {
            using var scope = c.EnterScope(PdfResourceKind.Depth);
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind == PdfValueKind.Array)
            {
                for (int n = 0; n < value.Items.Count; n++)
                {
                    c.Consume(PdfResourceKind.ExpandedBytes, location.Length * 2L + 64);
                    Visit(value.Items[n], location + "[" + n + "]");
                }

                return;
            }

            if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
                return;
            string type = PdfAttachmentGraph.Name(graph, value, "Type"), subtype = PdfAttachmentGraph.Name(graph, value, "Subtype");
            if (type == "Font")
            {
                if (subtype == "Type1" || subtype == "TrueType" || subtype == "CIDFontType0" || subtype == "CIDFontType2")
                {
                    var descriptor = graph.Get(value, "FontDescriptor");
                    bool embedded = descriptor.Kind == PdfValueKind.Dictionary && new[]
                    {
                        "FontFile",
                        "FontFile2",
                        "FontFile3"
                    }.Any(k => graph.Get(descriptor, k).Kind == PdfValueKind.Stream);
                    if (!embedded)
                        Add("Font.NotEmbedded", location, PdfDiagnosticSeverity.Warning);
                }

                if (subtype == "Type0" && graph.Get(value, "ToUnicode").Kind != PdfValueKind.Stream)
                    Add("Font.UnicodeMappingAbsent", location, PdfDiagnosticSeverity.Warning);
            }

            if (subtype == "Image")
            {
                var w = graph.Get(value, "Width");
                var h = graph.Get(value, "Height");
                if (w.Kind != PdfValueKind.Number || h.Kind != PdfValueKind.Number || !long.TryParse(w.NumberText, NumberStyles.None, CultureInfo.InvariantCulture, out long width) || !long.TryParse(h.NumberText, NumberStyles.None, CultureInfo.InvariantCulture, out long height) || width < 1 || height < 1 || width > int.MaxValue || height > int.MaxValue)
                    Add("Image.InvalidGeometry", location, PdfDiagnosticSeverity.Error);
            }

            foreach (var pair in value.Entries)
            {
                c.Consume(PdfResourceKind.ExpandedBytes, location.Length * 2L + pair.Key.Length * 2L + 64);
                Visit(pair.Value, location + "/" + pair.Key);
            }
        }
    }

    private static void WriteJson(PdfPreflightReport before, PdfRemediationAudit? audit, Stream output, PdfOperationContext c)
    {
        using var staged = new PdfCodecBufferStream(c, 8388608);
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        using (var w = new StreamWriter(staged, new UTF8Encoding(false, true), 1024, true))
        {
            w.Write("{\"schema\":1,\"conformanceValidated\":false,");
            if (audit == null)
            {
                w.Write("\"report\":");
                Report(before);
            }
            else
            {
                w.Write("\"before\":");
                Report(before);
                w.Write(",\"after\":");
                Report(audit.After);
                w.Write(",\"fixes\":[");
                bool first = true;
                foreach (string fix in audit.Plan.Fixes)
                {
                    if (!first)
                        w.Write(',');
                    first = false;
                    w.Write("{\"code\":");
                    String(fix);
                    w.Write(",\"lossDescription\":");
                    String(ActionLoss);
                    w.Write(",\"affectedLocations\":[");
                    bool f = true;
                    foreach (var finding in before.Findings.Where(x => x.RemediationCode == fix))
                    {
                        if (!f)
                            w.Write(',');
                        f = false;
                        String(finding.Location);
                    }

                    w.Write("]}");
                }

                w.Write(']');
            }

            w.Write('}');
            w.Flush();
            void Report(PdfPreflightReport report)
            {
                w.Write("{\"sourceSha256\":");
                String(report.SourceSha256);
                w.Write(",\"hasSignatures\":" + (report.HasSignatures ? "true" : "false") + ",\"availableCapabilities\":[");
                bool first = true;
                foreach (var capability in report.AvailableCapabilities)
                {
                    if (!first)
                        w.Write(',');
                    first = false;
                    String(capability.ToString());
                }

                w.Write("],\"findings\":[");
                first = true;
                foreach (var finding in report.Findings)
                {
                    c.Checkpoint();
                    if (!first)
                        w.Write(',');
                    first = false;
                    w.Write("{\"code\":");
                    String(finding.Code);
                    w.Write(",\"location\":");
                    String(finding.Location);
                    w.Write(",\"severity\":");
                    String(finding.Severity.ToString());
                    w.Write(",\"remediationCode\":");
                    String(finding.RemediationCode);
                    w.Write(",\"lossDescription\":");
                    String(finding.LossDescription);
                    w.Write('}');
                }

                w.Write("]}");
            }

            void String(string? value)
            {
                if (value == null)
                {
                    w.Write("null");
                    return;
                }

                c.Consume(PdfResourceKind.ExpandedBytes, value.Length * 16L + 128);
                w.Write('"');
                foreach (char ch in value)
                {
                    c.Checkpoint();
                    if (ch == '"')
                        w.Write("\\\"");
                    else if (ch == '\\')
                        w.Write("\\\\");
                    else if (ch < 32)
                        w.Write("\\u" + ((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        w.Write(ch);
                }

                w.Write('"');
            }
        }

        PdfProfileIo.Copy(staged.Finish(), output, c);
    }

    private static void Input(Stream input)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("A readable borrowed PDF input is required.", nameof(input));
    }

    private static void Output(Stream output)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed JSON output is required.", nameof(output));
    }

    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Preflight." + code, message);
}
