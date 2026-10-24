using System.IO;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Staged exact-byte linearization gates and passive dictionary inspection. The core does not bundle a complete producer or independent hint validator.</summary>
public static class PdfLinearization
{
    /// <summary>Reads a bounded immutable PDF and reports structural dictionary bounds, or null for an ordinary PDF. This is not complete independent validation.</summary>
    public static PdfLinearizationInfo? Inspect(Stream input, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Check(input);
        PdfLinearizationInfo? result = null;
        PdfFont.Run("InspectLinearization", "managed.linearization", policy, cancellationToken, c =>
        {
            var bytes = PdfProfileIo.Capture(input, c);
            new PdfSyntaxReader(bytes, c).Read();
            result = InspectCore(bytes, c);
        }, PdfCapability.ReadDocument);
        return result;
    }

    /// <summary>Requires explicit complete independent validation of exact captured bytes and requested settings. Structural markers alone cannot return a valid result.</summary>
    public static PdfLinearizationValidation Validate(Stream input, PdfLinearizationOptions? options = null, IPdfLinearizationValidator? validator = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => Run(input, null, null, options, null, validator, policy, cancellationToken, false);
    /// <summary>Stages a complete rewrite, verifies page geometry and dictionary binding, then requires independent complete validation before copying to borrowed output.</summary>
    public static PdfLinearizationValidation Write(Stream input, Stream output, PdfLinearizationOptions? options = null, IPdfLinearizationProducer? producer = null, IPdfLinearizationValidator? validator = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite || ReferenceEquals(input, output))
            throw new ArgumentException("A distinct writable borrowed output is required.", nameof(output));
        return Run(input, output, null, options, producer, validator, policy, cancellationToken, true);
    }

    /// <summary>Stages and independently validates before atomically replacing a file.</summary>
    public static PdfLinearizationValidation WriteFile(Stream input, string path, PdfLinearizationOptions? options = null, IPdfLinearizationProducer? producer = null, IPdfLinearizationValidator? validator = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(path, nameof(path), 32767);
        return Run(input, null, path, options, producer, validator, policy, cancellationToken, true);
    }

    private static PdfLinearizationValidation Run(Stream input, Stream? output, string? path, PdfLinearizationOptions? options, IPdfLinearizationProducer? producer, IPdfLinearizationValidator? validator, PdfOperationPolicy? policy, CancellationToken token, bool writing)
    {
        Check(input);
        options ??= new();
        PdfLinearizationValidation? result = null;
        var report = PdfFont.Run(writing ? "WriteLinearizedPdf" : "ValidateLinearizedPdf", "managed.linearization", policy, token, c =>
        {
            Require(producer, validator, options, writing, c);
            var source = PdfProfileIo.Capture(input, c);
            using var before = PdfPageOrganization.Read(source, 10000, c);
            if (before.Pages.Count == 0 || options.FirstPageIndex >= before.Pages.Count)
                throw Error(c, "PageIndex", "A valid first page in a nonempty document is required.");
            var candidate = source;
            if (writing)
            {
                PdfDocumentSnapshot snapshot;
                using (before.UseModelContext(c))
                    snapshot = before.CreateSnapshot();
                if (snapshot.SourceDocument!.HasSignatureByteRanges)
                {
                    if (!options.AllowSignatureInvalidation)
                        throw Error(c, "SignatureRewrite", "Linearization rewrites signed bytes; explicit signature invalidation authority is required.");
                    c.Report(new("Linearization.SignatureRewriteAllowed", "The caller explicitly authorized a full rewrite that invalidates existing signature byte ranges.", severity: PdfDiagnosticSeverity.Information));
                }

                PdfSignaturePolicy.RequireWriter(snapshot.SourceDocument!, new(allowSignatureInvalidation: options.AllowSignatureInvalidation), c, false);
                using var stage = new PdfCodecBufferStream(c, 67108864);
                try
                {
                    using var raw = source.Open(0, source.Length);
                    using var il = new BorrowedStream(raw, true, c, null);
                    using var ol = new BorrowedStream(stage, false, c);
                    producer!.Linearize(il, ol, options, c);
                    c.Checkpoint();
                }
                catch
                {
                    c.Checkpoint();
                    throw Error(c, "ProducerFailed", "The configured linearization producer failed without exposing candidate output.");
                }

                candidate = stage.Finish();
                using var after = PdfPageOrganization.Read(candidate, 10000, c);
                if (before.Pages.Count != after.Pages.Count)
                    throw Error(c, "PageRetention", "Linearization must retain every page in the original order.");
                for (int n = 0; n < before.Pages.Count; n++)
                {
                    c.Checkpoint();
                    if (!before.Pages[n].Geometry.Equals(after.Pages[n].Geometry))
                        throw Error(c, "PageRetention", "Linearization must retain all page boxes and rotations.");
                }
            }

            var info = InspectCore(candidate, c);
            if (info == null || info.PageCount != before.Pages.Count || info.FirstPageIndex != options.FirstPageIndex)
                throw Error(c, "DictionaryBinding", "The candidate lacks matching linearization page settings.");
            string digest = PdfArchival.Digest(candidate, c), request = Request(options, c);
            PdfLinearizationValidation observed;
            try
            {
                using var raw = candidate.Open(0, candidate.Length);
                using var lease = new BorrowedStream(raw, true, c, null);
                observed = validator!.Validate(lease, options, request, c);
                c.Checkpoint();
            }
            catch
            {
                c.Checkpoint();
                throw Error(c, "ValidatorFailed", "The configured independent linearization validator failed.");
            }

            if (observed == null || observed.SourceSha256 != digest || observed.RequestSha256 != request)
                throw Error(c, "ValidatorBinding", "The independent verdict belongs to another candidate or request.");
            c.Consume(PdfResourceKind.Objects, observed.Findings.Count + 1L);
            foreach (string finding in observed.Findings)
                c.Consume(PdfResourceKind.ExpandedBytes, finding.Length * 4L + 256);
            result = new(observed.SourceSha256, observed.RequestSha256, observed.Completed, observed.IsValid, observed.Findings);
            if (writing)
            {
                if (!result.Completed || !result.IsValid)
                    throw Error(c, "IndependentValidation", "Complete independent hint/offset validation did not succeed; output was not exposed.");
                if (path != null)
                    PdfAtomicFile.Write(path, c, target => PdfProfileIo.Copy(candidate, target, c));
                else
                    PdfProfileIo.Copy(candidate, output!, c);
            }
        }, writing ? PdfCapability.WriteDocument : PdfCapability.ReadDocument);
        result!.OperationReport = report;
        return result;
    }

    internal static PdfLinearizationInfo? InspectCore(PdfByteStore bytes, PdfOperationContext c)
    {
        var cursor = new PdfSyntaxCursor(bytes, c, 9);
        long number = cursor.Integer(), generation = cursor.Integer();
        cursor.Require("obj");
        if (number <= 0 || number > int.MaxValue || generation < 0 || generation > 65535)
            throw Error(c, "Dictionary", "The first object header is invalid.");
        var value = cursor.Value();
        if (value.Kind != PdfValueKind.Dictionary || !value.Entries.TryGetValue("Linearized", out var linearized))
            return null;
        cursor.Require("endobj");
        if (cursor.Position > 1024 || linearized.Kind != PdfValueKind.Number || linearized.NumberValue != 1)
            throw Error(c, "Dictionary", "A supported linearization1 dictionary must be the first object within1024 bytes.");
        long Integer(string key)
        {
            if (!value.Entries.TryGetValue(key, out var item) || item.Kind != PdfValueKind.Number || !long.TryParse(item.NumberText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integer))
                throw Error(c, "Dictionary", "A required direct linearization integer is missing or malformed.");
            return integer;
        }

        long length = Integer("L"), firstEnd = Integer("E"), first = Integer("O"), pages = Integer("N"), main = Integer("T");
        long page = value.Entries.ContainsKey("P") ? Integer("P") : 0;
        if (length != bytes.Length || firstEnd <= cursor.Position || firstEnd > length || first <= 0 || first > int.MaxValue || pages <= 0 || pages > int.MaxValue || main <= 0 || main >= length || page < 0 || page >= pages)
            throw Error(c, "DictionaryBounds", "Linearization length, first-page or index bounds are inconsistent.");
        if (!value.Entries.TryGetValue("H", out var hints) || hints.Kind != PdfValueKind.Array || hints.Items.Count != 2 && hints.Items.Count != 4)
            throw Error(c, "HintBounds", "A primary and optional overflow hint range is required.");
        var ranges = new long[hints.Items.Count];
        for (int n = 0; n < ranges.Length; n++)
        {
            var item = hints.Items[n];
            if (item.Kind != PdfValueKind.Number || !long.TryParse(item.NumberText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out ranges[n]))
                throw Error(c, "HintBounds", "Hint ranges require direct integers.");
        }

        for (int n = 0; n < ranges.Length; n += 2)
            if (ranges[n] <= cursor.Position || ranges[n + 1] <= 0 || ranges[n] > length - ranges[n + 1])
                throw Error(c, "HintBounds", "A declared hint range is outside the exact candidate.");
        if (ranges.Length == 4 && ranges[0] < ranges[2] + ranges[3] && ranges[2] < ranges[0] + ranges[1])
            throw Error(c, "HintBounds", "Primary and overflow hint ranges overlap.");
        return new(length, ranges[0], ranges[1], firstEnd, (int)first, (int)pages, main, (int)page);
    }

    private static string Request(PdfLinearizationOptions options, PdfOperationContext c)
    {
        using var buffer = new PdfCodecBufferStream(c, 1024);
        using (var writer = new BinaryWriter(buffer, new UTF8Encoding(false, true), true))
        {
            writer.Write("Fluent.Pdf.Linearization.1");
            writer.Write(options.FirstPageIndex);
            writer.Write(options.AllowSignatureInvalidation);
            writer.Flush();
        }

        return PdfArchival.Digest(buffer.Finish(), c);
    }

    private static void Require(IPdfLinearizationProducer? producer, IPdfLinearizationValidator? validator, PdfLinearizationOptions options, bool writing, PdfOperationContext c)
    {
        if (validator == null || writing && producer == null)
            throw Error(c, "ProviderUnavailable", "Explicit complete independent validation is required; writing also requires an explicit linearization producer.");
        string vi, pi;
        try
        {
            vi = validator.EngineId;
            pi = writing ? producer!.EngineId : "none";
            c.Checkpoint();
        }
        catch
        {
            c.Checkpoint();
            throw Error(c, "ProviderFailed", "The configured linearization identity check failed.");
        }

        Identity(vi);
        Identity(pi);
        if (writing && (ReferenceEquals(producer, validator) || pi == vi))
            throw Error(c, "IndependentValidatorRequired", "The complete validator must be independently implemented and separately identified.");
        bool available;
        try
        {
            available = validator.Supports(options) && (!writing || producer!.Supports(options));
            c.Checkpoint();
        }
        catch
        {
            c.Checkpoint();
            throw Error(c, "ProviderFailed", "The configured linearization prerequisite check failed.");
        }

        if (!available)
            throw Error(c, "ProfileUnavailable", "The exact requested linearization settings or local assets are unavailable.");
        c.Report(new("Linearization.ProviderIdentity", "Explicit producer: " + pi + "; independent validator: " + vi + ".", severity: PdfDiagnosticSeverity.Information));
        void Identity(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 80 || id.Any(ch => !(ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || "._-".IndexOf(ch) >= 0)))
                throw Error(c, "ProviderIdentity", "A bounded pinned nonsecret ASCII provider identity is required.");
        }
    }

    private static void Check(Stream input)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("A readable borrowed input is required.", nameof(input));
    }

    private static PdfException Error(PdfOperationContext c, string code, string message)
    {
        var diagnostic = new PdfDiagnostic("Linearization." + code, message, PdfCapability.WriteDocument, "managed.linearization");
        c.Report(diagnostic);
        return new(diagnostic);
    }
}
