using System.IO;

namespace Fluent.Pdf.Compliance;
/// <summary>An explicitly configured offline complete PDF/UA validation host with mandatory shared budgets and borrowed source/report streams.</summary>
public delegate void PdfAccessibilityValidationRunner(PdfAccessibilityProfile profile, Stream input, Stream report, PdfOperationContext context);
/// <summary>Optional bounded veraPDF1.28.2 report bridge for complete UA1/UA2 validation, separate from human semantic review. No engine, network, file or runtime discovery is performed.</summary>
public sealed class VeraPdfAccessibilityReportValidator : IPdfAccessibilityValidator
{
    private readonly PdfAccessibilityValidationRunner _runner;
    /// <summary>Captures an explicit caller-owned host responsible for exact profile selection and supervised execution/isolation.</summary>
    public VeraPdfAccessibilityReportValidator(PdfAccessibilityValidationRunner runner) => _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    /// <summary>Gets exact report engine identity/version and independent accessibility service.</summary>
    public PdfProviderDescriptor Descriptor { get; } = new("verapdf.accessibility-report-bridge", "1.28.2", PdfCapability.ValidateAccessibility);

    /// <summary>Checks exact source digest and full engine/profile/job/rule/check report completeness; contradictory, truncated or wrong-profile reports fail closed.</summary>
    public PdfAccessibilityValidation Validate(PdfAccessibilityProfile profile, Stream input, PdfOperationContext context)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        PdfStructureRules.Profile(profile);
        using var lease = context.BorrowExpandedInput(input);
        var bytes = PdfByteStore.Read(lease, context);
        string digest = PdfArchival.Digest(bytes, context);
        using var output = new PdfCodecBufferStream(context, 16777216);
        using (var source = bytes.Open(0, bytes.Length))
        using (var borrowedInput = context.BorrowExpandedInput(source))
        using (var borrowedOutput = new BorrowedStream(output, false, context))
            _runner(profile, borrowedInput, borrowedOutput, context);
        context.Checkpoint();
        string expected = profile == PdfAccessibilityProfile.Ua1 ? "PDF/UA-1 validation profile" : "PDF/UA-2 + Tagged PDF validation profile";
        var report = VeraPdfReport.Read(output.Finish(), expected, "Accessibility.", context);
        context.Consume(PdfResourceKind.ExpandedBytes, report.Findings.Length * 128L + 256);
        context.Consume(PdfResourceKind.Objects, report.Findings.Length);
        return new(profile, digest, report.Complete, report.Compliant, report.Findings.Select(f => new PdfAccessibilityFinding(f.Rule, f.Location, f.Message)).ToArray());
    }
}
