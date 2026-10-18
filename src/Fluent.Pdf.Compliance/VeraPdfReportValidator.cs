using System.Globalization;
using System.IO;
using System.Xml;

namespace Fluent.Pdf.Compliance;
/// <summary>Runs an explicitly supplied offline validation host with borrowed PDF/report streams and the SDK's mandatory shared context. The host must select the requested complete profile and enforce its own supervised execution/isolation.</summary>
public delegate void PdfArchivalValidationRunner(PdfArchivalProfile profile, Stream input, Stream report, PdfOperationContext context);
/// <summary>Optional bounded report bridge for veraPDF 1.28.2. It neither installs nor launches engines, downloads resources, creates temporary files or substitutes metadata inspection for full validation.</summary>
public sealed class VeraPdfReportValidator : IPdfArchivalValidator
{
    private readonly PdfArchivalValidationRunner _runner;
    /// <summary>Captures an explicit caller-owned validation host. Complete engine deployment, isolation, report retention and qualification are caller host requirements.</summary>
    public VeraPdfReportValidator(PdfArchivalValidationRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    /// <summary>Gets exact report-engine version and service identity, independent of production qualification.</summary>
    public PdfProviderDescriptor Descriptor { get; } = new("verapdf.report-bridge", "1.28.2", PdfCapability.ValidateArchival);

    /// <summary>Hashes exact source bytes, invokes the explicit host with shared budgets and checks engine/profile/job/rule/check completeness. Truncated, ambiguous or contradictory reports fail closed.</summary>
    public PdfArchivalValidation Validate(PdfArchivalProfile profile, Stream input, PdfOperationContext context)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        string token = PdfArchival.ProfileIdentifier(profile);
        using var sourceLease = context.BorrowExpandedInput(input);
        var bytes = PdfByteStore.Read(sourceLease, context);
        string digest = PdfArchival.Digest(bytes, context);
        using var output = new PdfCodecBufferStream(context, 16777216);
        using (var source = bytes.Open(0, bytes.Length))
        using (var borrowedInput = context.BorrowExpandedInput(source))
        using (var borrowedOutput = new BorrowedStream(output, false, context))
            _runner(profile, borrowedInput, borrowedOutput, context);
        context.Checkpoint();
        var xml = output.Finish();
        context.Consume(PdfResourceKind.ExpandedBytes, xml.Length * 8L + 65536);
        using var raw = xml.Open(0, xml.Length);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 16777216,
            CloseInput = false,
            ConformanceLevel = ConformanceLevel.Document
        };
        using var reader = XmlReader.Create(raw, settings);
        var scopes = new List<IDisposable>();
        var elements = new List<string>();
        int roots = 0;
        var findings = new List<PdfArchivalFinding>();
        int jobs = 0, reports = 0, details = 0, batch = 0, aggregates = 0, coreVersions = 0, modelVersions = 0;
        bool declared = false, normal = false, cleanBatch = false, cleanAggregate = false;
        long failedRules = -1, passedRules = -1, failedChecks = -1, passedChecks = -1;
        try
        {
            while (reader.Read())
            {
                context.Checkpoint();
                int depth = reader.Depth + 1;
                while (scopes.Count > depth)
                {
                    scopes[scopes.Count - 1].Dispose();
                    scopes.RemoveAt(scopes.Count - 1);
                }

                while (scopes.Count < depth)
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, 32);
                    scopes.Add(context.EnterScope(PdfResourceKind.Depth));
                }

                context.Consume(PdfResourceKind.Objects, 1 + reader.AttributeCount);
                context.Consume(PdfResourceKind.ExpandedBytes, 256L + reader.Value.Length * 2L + reader.AttributeCount * 256L);
                if (reader.NodeType != XmlNodeType.Element)
                    continue;
                if (reader.NamespaceURI.Length != 0)
                    throw Error(context, "ReportSchema", "The validator report has an unexpected XML namespace.");
                while (elements.Count > reader.Depth)
                    elements.RemoveAt(elements.Count - 1);
                if (elements.Count != reader.Depth)
                    throw Error(context, "ReportSchema", "The XML report structure is inconsistent.");
                elements.Add(reader.LocalName);
                if (reader.Depth == 0 && reader.LocalName != "report")
                    throw Error(context, "ReportSchema", "A validator report root is required.");
                switch (reader.LocalName)
                {
                    case "report":
                        roots++;
                        if (reader.Depth != 0)
                            throw Error(context, "ReportSchema", "A single report root is required.");
                        break;
                    case "releaseDetails":
                        Path("report", "buildInformation", "releaseDetails");
                        if (reader.GetAttribute("id") == "core")
                        {
                            coreVersions++;
                            Version();
                        }

                        if (reader.GetAttribute("id") == "validation-model")
                        {
                            modelVersions++;
                            Version();
                        }

                        break;
                    case "job":
                        Path("report", "jobs", "job");
                        jobs++;
                        break;
                    case "validationReport":
                        Path("report", "jobs", "job", "validationReport");
                        reports++;
                        if (reader.GetAttribute("profileName") != "PDF/A-" + token.ToUpperInvariant() + " validation profile")
                            throw Error(context, "ReportProfile", "The validator report does not match the requested complete profile.");
                        declared = Boolean("isCompliant");
                        normal = reader.GetAttribute("jobEndStatus") == "normal";
                        break;
                    case "details":
                        Path("report", "jobs", "job", "validationReport", "details");
                        details++;
                        passedRules = Number("passedRules");
                        failedRules = Number("failedRules");
                        passedChecks = Number("passedChecks");
                        failedChecks = Number("failedChecks");
                        break;
                    case "rule":
                        Path("report", "jobs", "job", "validationReport", "details", "rule");
                        if (reader.GetAttribute("status") == "failed")
                        {
                            if (findings.Count >= 10000)
                                throw Error(context, "ReportExtent", "The validator report exceeds its bounded violation inventory.");
                            string rule = (reader.GetAttribute("specification") ?? "") + ":" + (reader.GetAttribute("clause") ?? "") + ":" + (reader.GetAttribute("testNumber") ?? "");
                            if (rule.Length > 256)
                                throw Error(context, "ReportExtent", "A validator rule identifier exceeds its supported extent.");
                            context.Consume(PdfResourceKind.ExpandedBytes, rule.Length * 4L + 512);
                            findings.Add(new(rule, "independent-validator", "Independent profile validation failed this rule."));
                        }

                        break;
                    case "batchSummary":
                        Path("report", "batchSummary");
                        batch++;
                        cleanBatch = Number("totalJobs") == 1 && Number("failedToParse") == 0 && Number("encrypted") == 0 && Number("outOfMemory") == 0 && Number("veraExceptions") == 0;
                        break;
                    case "validationReports":
                        Path("report", "batchSummary", "validationReports");
                        aggregates++;
                        cleanAggregate = Number("compliant") == (declared ? 1 : 0) && Number("nonCompliant") == (declared ? 0 : 1) && Number("failedJobs") == 0;
                        break;
                }
            }

            if (roots != 1 || jobs != 1 || reports != 1 || details != 1 || batch != 1 || aggregates != 1 || coreVersions != 1 || modelVersions != 1 || passedRules <= 0 || passedChecks <= 0 || failedRules != findings.Count || failedChecks < failedRules || declared != (failedRules == 0 && failedChecks == 0))
                throw Error(context, "ReportCompleteness", "The validator report is incomplete, ambiguous or contradictory.");
            bool complete = normal && cleanBatch && cleanAggregate;
            context.Consume(PdfResourceKind.ExpandedBytes, findings.Count * 32L + 128);
            return new(profile, digest, complete, complete && declared, findings.ToArray());
        }
        catch (XmlException)
        {
            context.Checkpoint();
            throw Error(context, "ReportXml", "The validator report is malformed or contains a prohibited XML construct.");
        }
        finally
        {
            for (int i = scopes.Count - 1; i >= 0; i--)
                scopes[i].Dispose();
        }

        void Path(params string[] expected)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, expected.Length * 16L + 64);
            if (!elements.SequenceEqual(expected))
                throw Error(context, "ReportSchema", "A validator report element occurs outside its required structure.");
        }

        void Version()
        {
            if (reader.GetAttribute("version") != "1.28.2")
                throw Error(context, "ReportEngine", "This report bridge requires the exact pinned validation engine version.");
        }

        long Number(string name) => long.TryParse(reader.GetAttribute(name), NumberStyles.None, CultureInfo.InvariantCulture, out long number) && number >= 0 && number <= 1000000000 ? number : throw Error(context, "ReportCount", "A bounded nonnegative validator count is required.");
        bool Boolean(string name) => reader.GetAttribute(name) switch
        {
            "true" => true,
            "false" => false,
            _ => throw Error(context, "ReportSchema", "A validator compliance boolean is malformed.")};
    }

    private static PdfException Error(PdfOperationContext context, string code, string message) => PdfWriterCore.Error(context, "Archival." + code, message);
}
