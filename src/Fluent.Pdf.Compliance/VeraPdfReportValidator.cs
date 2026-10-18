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
        var result = VeraPdfReport.Read(xml, "PDF/A-" + token.ToUpperInvariant() + " validation profile", "Archival.", context);
        return new(profile, digest, result.Complete, result.Compliant, result.Findings);
    }
}
