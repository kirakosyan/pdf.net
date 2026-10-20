using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit exact-byte profile gates. No bundled complete PDF/X producer or validator is implied.</summary>
public static class PdfProfileConversions
{
    /// <summary>Validates exact nonempty source PDF with explicit ICC/condition settings. Profile availability is checked before source capture.</summary>
    public static PdfProfileValidation Validate(Stream input, PdfProfileOptions options, IPdfProfileValidator? validator = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Check(input, options);
        PdfProfileValidation? result = null;
        var report = PdfFont.Run("ValidateProfile", "managed.profile-gate", policy, cancellationToken, c =>
        {
            Require(null, validator, options, c, false);
            var bytes = PdfProfileIo.Capture(input, c);
            using var doc = PdfPageOrganization.Read(bytes, 10000, c);
            if (doc.Pages.Count == 0)
                throw Error(c, "EmptyInput", "A nonempty valid PDF is required.");
            string request = Request(options, c);
            CheckOutputIntent(doc, options, c);
            result = ValidateCore(bytes, options, request, validator!, c);
        }, PdfCapability.ReadDocument);
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Stages producer output, checks page geometry/ICC/condition and requires complete independent exact-byte validation before caller writes. Missing, stale, incomplete or failed validation cannot expose a candidate.</summary>
    public static PdfProfileValidation Convert(Stream input, Stream output, PdfProfileOptions options, IPdfProfileProducer? producer = null, IPdfProfileValidator? validator = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Check(input, options);
        if (ReferenceEquals(input, output))
            throw new ArgumentException("Input and output streams must be distinct.", nameof(output));
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed PDF output is required.", nameof(output));
        PdfProfileValidation? result = null;
        var report = PdfFont.Run("ConvertProfile", "managed.profile-gate", policy, cancellationToken, c =>
        {
            Require(producer, validator, options, c, true);
            var source = PdfProfileIo.Capture(input, c);
            using var before = PdfPageOrganization.Read(source, 10000, c);
            if (before.Pages.Count == 0)
                throw Error(c, "EmptyInput", "A nonempty valid PDF is required.");
            PdfDocumentSnapshot snapshot;
            using (before.UseModelContext(c))
                snapshot = before.CreateSnapshot();
            if (snapshot.SourceDocument!.HasSignatureByteRanges)
            {
                if (!options.AllowSignatureInvalidation)
                    throw Error(c, "SignatureRewrite", "Profile conversion can invalidate signatures; explicit authority is required.");
                c.Report(new("Profile.SignatureInvalidated", "The requested profile rewrite can invalidate source signatures.", lossCategory: PdfLossCategory.FidelityReduction));
            }

            var advanced = PdfEngineering.ReadCore(snapshot, c);
            using var raw = source.Open(0, source.Length);
            using var staged = new PdfCodecBufferStream(c, 67108864);
            try
            {
                using var il = new BorrowedStream(raw, true, c);
                using var ol = new BorrowedStream(staged, false, c);
                producer!.Convert(il, options, ol, c);
                c.Checkpoint();
            }
            catch
            {
                c.Checkpoint();
                throw Error(c, "ProducerFailed", "The configured producer failed without exposing partial output.");
            }

            var candidate = staged.Finish();
            using var after = PdfPageOrganization.Read(candidate, 10000, c);
            if (after.Pages.Count != before.Pages.Count || after.Pages.Count == 0)
                throw Error(c, "PageRetention", "Profile conversion must retain the complete nonempty page inventory.");
            for (int n = 0; n < before.Pages.Count; n++)
            {
                c.Checkpoint();
                if (!before.Pages[n].Geometry.Equals(after.Pages[n].Geometry))
                    throw Error(c, "PageRetention", "The source profile requires exact page box/rotation retention.");
            }

            if (advanced.Count > 0 && PdfArchival.Digest(source, c) != PdfArchival.Digest(candidate, c))
            {
                if (!options.AllowAdvancedContentLoss)
                    throw Error(c, "AdvancedContentRetention", "Advanced-content rewrites require a qualified preservation proof or explicit declared loss authority; this source gate proves only exact-byte retention.");
                c.Report(new("Profile.AdvancedContentLoss", "The caller authorized a rewrite whose 3D/media/geospatial/attachment associations and semantics are not proven preserved. Inspect the exact source inventory before authorizing this loss.", lossCategory: PdfLossCategory.FidelityReduction));
            }

            CheckOutputIntent(after, options, c);
            result = ValidateCore(candidate, options, Request(options, c), validator!, c);
            if (!result.Completed || !result.IsCompliant)
                throw Error(c, "IndependentValidation", "The complete independent validator did not establish the requested profile; output was not exposed.");
            PdfProfileIo.Copy(candidate, output, c);
        }, PdfCapability.WriteDocument);
        result!.OperationReport = report;
        return result;
    }

    internal static PdfProfileValidation ValidateCore(PdfByteStore bytes, PdfProfileOptions options, string request, IPdfProfileValidator validator, PdfOperationContext c)
    {
        string digest = PdfArchival.Digest(bytes, c);
        PdfProfileValidation result;
        try
        {
            using var raw = bytes.Open(0, bytes.Length);
            using var lease = new BorrowedStream(raw, true, c);
            result = validator.Validate(lease, options, request, c);
            c.Checkpoint();
        }
        catch
        {
            c.Checkpoint();
            throw Error(c, "ValidatorFailed", "The configured independent validator failed.");
        }

        if (result == null || result.Profile != options.Profile || result.SourceSha256 != digest || result.RequestSha256 != request)
            throw Error(c, "ValidatorBinding", "The independent result belongs to another profile, settings or exact input.");
        c.Consume(PdfResourceKind.Objects, result.Findings.Count + 1L);
        foreach (string finding in result.Findings)
            c.Consume(PdfResourceKind.ExpandedBytes, finding.Length * 4L + 256);
        return new(result.Profile, result.SourceSha256, result.RequestSha256, result.Completed, result.IsCompliant, result.Findings);
    }

    internal static string Request(PdfProfileOptions options, PdfOperationContext c)
    {
        using var buffer = new PdfCodecBufferStream(c, 16790000);
        c.Consume(PdfResourceKind.ExpandedBytes, 8192 + options.OutputCondition.Length * 6L);
        using (var writer = new BinaryWriter(buffer, new UTF8Encoding(false, true), true))
        {
            writer.Write((int)options.Profile);
            writer.Write(options.OutputCondition);
            writer.Write(options.AllowSignatureInvalidation);
            writer.Write(options.AllowAdvancedContentLoss);
            if (options.OutputColorSpace != null)
            {
                var profile = options.OutputColorSpace.Definition.Items[1];
                using var raw = profile.OpenRawStream();
                var bytes = PdfProfileIo.Capture(raw, c, 16777216);
                writer.Write(PdfArchival.Digest(bytes, c));
            }
            else
                writer.Write("no-print-intent");
            writer.Flush();
        }

        return PdfArchival.Digest(buffer.Finish(), c);
    }

    internal static void CheckOutputIntent(PdfDocument doc, PdfProfileOptions options, PdfOperationContext c)
    {
        if (options.Profile == PdfConformanceProfile.E12008)
            return;
        PdfDocumentSnapshot snapshot;
        using (doc.UseModelContext(c))
            snapshot = doc.CreateSnapshot();
        using var graph = new PdfGraphEdit(snapshot, new(), c);
        var intents = graph.Get(graph.Catalog, "OutputIntents");
        if (intents.Kind != PdfValueKind.Array || intents.Items.Count != 1)
            throw Error(c, "OutputIntent", "One explicit complete PDF/X output intent is required.");
        var intent = graph.Resolve(intents.Items[0]);
        if (intent.Kind != PdfValueKind.Dictionary)
            throw Error(c, "OutputIntent", "An explicit output intent dictionary is required.");
        var kind = graph.Get(intent, "S");
        if (kind.Kind != PdfValueKind.Name || kind.NameValue != "GTS_PDFX" || PdfTextStrings.Decode(graph.Get(intent, "OutputConditionIdentifier"), c) != options.OutputCondition)
            throw Error(c, "OutputIntent", "The output condition must match the explicit PDF/X request.");
        var stream = graph.Get(intent, "DestOutputProfile");
        if (stream.Kind != PdfValueKind.Stream)
            throw Error(c, "OutputIntent", "The output intent must embed the requested complete ICC profile.");
        var resolved = PdfFilterDeclarations.Resolve(stream, id => graph.Resolve(PdfValue.Reference(id)), c);
        using var decoded = PdfFilters.Decode(resolved, c);
        var actual = PdfProfileIo.Capture(decoded, c, 16777216);
        using var expectedRaw = options.OutputColorSpace!.Definition.Items[1].OpenRawStream();
        var expected = PdfProfileIo.Capture(expectedRaw, c, 16777216);
        if (PdfArchival.Digest(actual, c) != PdfArchival.Digest(expected, c))
            throw Error(c, "OutputIntent", "The embedded ICC bytes differ from the explicit requested output profile.");
    }

    private static void Require(IPdfProfileProducer? producer, IPdfProfileValidator? validator, PdfProfileOptions options, PdfOperationContext c, bool conversion)
    {
        if (validator == null || conversion && producer == null)
            throw Error(c, "ProviderUnavailable", "An explicit complete producer and independent validator are required for conversion; validation requires an independent validator.");
        string validatorIdentity = validator.EngineId;
        string? producerIdentity = conversion ? producer!.EngineId : null;
        Identity(validatorIdentity, c);
        if (conversion)
        {
            Identity(producerIdentity!, c);
            if (ReferenceEquals(producer, validator) || producerIdentity == validatorIdentity)
                throw Error(c, "IndependentValidatorRequired", "Configure a separately identified independent validator.");
        }

        bool supported;
        try
        {
            supported = validator.Supports(options.Profile) && (!conversion || producer!.Supports(options));
        }
        catch
        {
            c.Checkpoint();
            throw Error(c, "ProviderFailed", "The exact provider prerequisite check failed.");
        }

        if (!supported)
            throw Error(c, "ProfileUnavailable", "The requested exact standard edition or explicit print prerequisites are unavailable.");
        c.Report(new("Profile.ProviderIdentity", "Explicit producer: " + (producerIdentity ?? "none") + "; independent validator: " + validatorIdentity + ".", severity: PdfDiagnosticSeverity.Information));
    }

    private static void Identity(string value, PdfOperationContext c)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 80 || value.Any(ch => !(ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || "._-".IndexOf(ch) >= 0)))
            throw Error(c, "ProviderIdentity", "A bounded nonsecret ASCII pinned provider identity is required.");
    }

    private static void Check(Stream input, PdfProfileOptions options)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("Readable borrowed PDF input is required.", nameof(input));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
    }

    internal static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Profile." + code, message);
}

internal static class PdfProfileIo
{
    internal static PdfByteStore Capture(Stream input, PdfOperationContext c, int maximum = 67108864)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var b = new byte[8192];
        using var output = new PdfCodecBufferStream(c, maximum);
        using var source = new BorrowedStream(input, true, c);
        while (true)
        {
            c.Checkpoint();
            int n = source.Read(b, 0, (int)Math.Min(b.Length, maximum - output.BufferedLength + 1));
            if (n == 0)
                break;
            if (n > maximum - output.BufferedLength)
                throw PdfProfileConversions.Error(c, "InputExtent", "The explicit input/candidate extent is exceeded.");
            output.Write(b, 0, n);
        }

        return output.Finish();
    }

    internal static void Copy(PdfByteStore bytes, Stream output, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var b = new byte[8192];
        using var raw = bytes.Open(0, bytes.Length);
        using var target = new BorrowedStream(output, false, c);
        int n;
        while ((n = raw.Read(b, 0, b.Length)) > 0)
        {
            c.Checkpoint();
            target.Write(b, 0, n);
        }
    }
}
