using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit archival preflight and full rewrites gated on complete independent validation of the exact result.</summary>
public static partial class PdfArchival
{
    /// <summary>Returns the stable independent-validator profile token.</summary>
    public static string ProfileIdentifier(PdfArchivalProfile profile) => profile switch
    {
        PdfArchivalProfile.A1A => "1a",
        PdfArchivalProfile.A1B => "1b",
        PdfArchivalProfile.A2A => "2a",
        PdfArchivalProfile.A2B => "2b",
        PdfArchivalProfile.A2U => "2u",
        PdfArchivalProfile.A3A => "3a",
        PdfArchivalProfile.A3B => "3b",
        PdfArchivalProfile.A3U => "3u",
        PdfArchivalProfile.A4 => "4",
        PdfArchivalProfile.A4E => "4e",
        PdfArchivalProfile.A4F => "4f",
        _ => throw new ArgumentOutOfRangeException(nameof(profile))};
    /// <summary>Inspects structural/profile blockers. Absence of findings never establishes PDF/A conformance.</summary>
    public static PdfArchivalInspection Inspect(PdfDocumentSnapshot source, PdfArchivalProfile profile, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        _ = ProfileIdentifier(profile);
        PdfArchivalFinding[] findings = Array.Empty<PdfArchivalFinding>();
        var report = PdfFont.Run("InspectArchival", "managed.archival", policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(source, new(), context);
            findings = Preflight(graph, profile);
        }, PdfCapability.WriteDocument);
        return new(findings, report);
    }

    /// <summary>Validates exact source bytes through an explicit independent provider. A normal PDF lacking archival identification is not silently converted.</summary>
    public static PdfArchivalValidation Validate(Stream input, PdfArchivalProfile profile, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (!input.CanRead)
            throw new ArgumentException("Readable borrowed input is required.", nameof(input));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        _ = ProfileIdentifier(profile);
        var validator = providers.GetRequired<IPdfArchivalValidator>(PdfCapability.ValidateArchival);
        PdfArchivalValidation? result = null;
        var report = PdfFont.Run("ValidateArchival", providers.GetIdentifier(PdfCapability.ValidateArchival), policy, cancellationToken, context =>
        {
            using var lease = new BorrowedStream(input, true, context);
            var bytes = PdfByteStore.Read(lease, context);
            result = ValidateCore(bytes, profile, validator, context);
        }, PdfCapability.ValidateArchival);
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Writes only an independently validated full rewrite. Unsupported repairs fail; metadata replacement is explicit and strict mode rejects its reported loss.</summary>
    public static PdfArchivalConversion Convert(PdfDocumentSnapshot source, Stream output, PdfArchivalOptions options, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (!output.CanWrite)
            throw new ArgumentException("Writable borrowed output is required.", nameof(output));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        var validator = providers.GetRequired<IPdfArchivalValidator>(PdfCapability.ValidateArchival);
        PdfArchivalValidation? validated = null;
        var report = PdfFont.Run("ConvertArchival", providers.GetIdentifier(PdfCapability.ValidateArchival), policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(source, new(allowSignatureInvalidation: options.AllowSignatureInvalidation), context);
            var findings = Preflight(graph, options.Profile);
            if (findings.Length != 0)
                throw Error(context, "Preflight", findings[0].Message);
            Metadata(graph, options);
            if (Part(options.Profile) == 1)
                AddExplicitCidSets(graph);
            if (options.OutputColorSpace != null)
            {
                var space = options.OutputColorSpace;
                if (Part(options.Profile) == 1 && PdfVersionNumber.Parse(space.MinimumVersion) > 14)
                    throw Error(context, "IccVersion", "PDF/A-1 requires an explicitly compatible ICC v2 output profile.");
                context.Consume(PdfResourceKind.Objects, 3);
                context.Consume(PdfResourceKind.ExpandedBytes, 512);
                var profileId = graph.Edit.Add(space.Definition.Items[1]);
                var intent = PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("OutputIntent")), ("S", PdfValue.Name("GTS_PDFA1")), ("OutputConditionIdentifier", PdfTextStrings.Encode(options.OutputCondition, context)), ("DestOutputProfile", PdfValue.Reference(profileId)));
                var catalog = graph.Copy(graph.Catalog);
                catalog["OutputIntents"] = PdfValue.Array(PdfValue.Reference(graph.Edit.Add(intent)));
                graph.SetCatalog(catalog);
            }

            string version = Part(options.Profile) == 1 ? "1.4" : Part(options.Profile) == 4 ? "2.0" : "1.7";
            // This private staging path must pass complete independent profile validation before caller output is touched.
            var catalogVersion = graph.Copy(graph.Catalog);
            catalogVersion["Version"] = PdfValue.Name(version);
            graph.SetCatalog(catalogVersion);
            using var buffer = new PdfCodecBufferStream(context, context.Available(PdfResourceKind.ExpandedBytes));
            using (var destination = new BorrowedStream(buffer, false, context))
                graph.Edit.WriteCore(destination, new(version: version, allowSignatureInvalidation: options.AllowSignatureInvalidation), context, validatedArchivalDowngrade: true);
            var encoded = buffer.Finish();
            validated = ValidateCore(encoded, options.Profile, validator, context);
            if (!validated.Completed || !validated.IsCompliant)
            {
                context.ReportFailure(new PdfDiagnostic("Archival.IndependentValidation", "The independent validator did not establish the requested archival profile; no converted output was copied."), "Fluent.Pdf.ArchivalValidation", validated);
                throw new InvalidOperationException("A fatal validation diagnostic unexpectedly returned.");
            }

            Copy(encoded, output, context);
        }, PdfCapability.ValidateArchival);
        validated!.OperationReport = report;
        return new(validated, report);
    }

    private static PdfArchivalValidation ValidateCore(PdfByteStore bytes, PdfArchivalProfile profile, IPdfArchivalValidator validator, PdfOperationContext context)
    {
        string digest = Digest(bytes, context);
        using var raw = bytes.Open(0, bytes.Length);
        using var lease = context.BorrowExpandedInput(raw);
        var result = validator.Validate(profile, lease, context);
        context.Checkpoint();
        if (result == null || result.Profile != profile || result.SourceSha256 != digest)
            throw Error(context, "ValidatorBinding", "The validator returned a result for another profile or input digest.");
        context.Consume(PdfResourceKind.Objects, result.Findings.Count + 1L);
        foreach (var finding in result.Findings)
            context.Consume(PdfResourceKind.ExpandedBytes, (finding.Message.Length + finding.Location.Length + finding.Rule.Length) * 2L + 64);
        context.Consume(PdfResourceKind.Objects, 2);
        context.Consume(PdfResourceKind.ExpandedBytes, result.Findings.Count * 16L + 256);
        return new(result.Profile, result.SourceSha256, result.Completed, result.IsCompliant, result.Findings.ToArray());
    }

    internal static string Digest(PdfByteStore bytes, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 65536 + 4096);
        var part = new byte[65536];
        using var hash = SHA256.Create();
        using var input = bytes.Open(0, bytes.Length);
        int count;
        while ((count = input.Read(part, 0, part.Length)) > 0)
        {
            context.Checkpoint();
            hash.TransformBlock(part, 0, count, part, 0);
        }

        hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        context.Checkpoint();
        return BitConverter.ToString(hash.Hash!).Replace("-", "").ToLowerInvariant();
    }

    private static void Copy(PdfByteStore bytes, Stream output, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 65536);
        var part = new byte[65536];
        using var raw = bytes.Open(0, bytes.Length);
        using var lease = new BorrowedStream(output, false, context);
        int count;
        while ((count = raw.Read(part, 0, part.Length)) > 0)
        {
            context.Checkpoint();
            lease.Write(part, 0, count);
        }

        context.Checkpoint();
    }

    internal static int Part(PdfArchivalProfile profile) => profile <= PdfArchivalProfile.A1B ? 1 : profile <= PdfArchivalProfile.A2U ? 2 : profile <= PdfArchivalProfile.A3U ? 3 : 4;
    private static PdfException Error(PdfOperationContext context, string code, string message) => PdfWriterCore.Error(context, "Archival." + code, message);
}
