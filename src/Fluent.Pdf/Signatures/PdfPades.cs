using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Requested ETSI baseline signature profile. Selecting a value does not establish compliance.</summary>
public enum PdfPadesProfile
{
    /// <summary>Baseline B.</summary>
    B,
    /// <summary>Baseline T with trusted timestamp.</summary>
    T,
    /// <summary>Baseline LT with validation evidence.</summary>
    LT,
    /// <summary>Baseline LTA with archival timestamp/renewal.</summary>
    LTA
}
/// <summary>Explicit optional incremental signature-profile producer. Endpoint, credential and timeouts belong to its explicit configuration.</summary>
public interface IPdfPadesProvider : IPdfProvider
{
    /// <summary>Appends the requested profile to exact source bytes. All previous bytes and validation evidence must survive. No implicit network access is authorized.</summary>
    void Upgrade(Stream source, Stream output, PdfPadesProfile profile, PdfOperationContext context);
}
/// <summary>Complete independent PAdES validation bound to exact bytes and requested profile.</summary>
public sealed class PdfPadesValidation
{
    /// <summary>Captures a lowercase SHA256, exact profile and complete validation outcome.</summary>
    public PdfPadesValidation(string sourceSha256, PdfPadesProfile profile, bool completed, bool compliant)
    {
        if (sourceSha256 == null || sourceSha256.Length != 64 || sourceSha256.Any(ch => !(ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f')))
            throw new ArgumentException("An exact SHA256 is required.", nameof(sourceSha256));
        if (!Enum.IsDefined(typeof(PdfPadesProfile), profile))
            throw new ArgumentOutOfRangeException(nameof(profile));
        if (compliant && !completed)
            throw new ArgumentException("Compliance requires complete validation.", nameof(compliant));
        SourceSha256 = sourceSha256;
        Profile = profile;
        Completed = completed;
        Compliant = compliant;
    }
    /// <summary>Gets exact validated byte identity.</summary>
    public string SourceSha256
    {
        get;
    }
    /// <summary>Gets exact validated profile.</summary>
    public PdfPadesProfile Profile
    {
        get;
    }
    /// <summary>Gets whether full trust/time/revocation/profile checks completed.</summary>
    public bool Completed
    {
        get;
    }
    /// <summary>Gets the independent compliance observation.</summary>
    public bool Compliant
    {
        get;
    }
}
/// <summary>Explicit independent validator with caller-configured trust and network authority; no engine is discovered.</summary>
public interface IPdfPadesValidator : IPdfProvider
{
    /// <summary>Checks all requested profile, TSA, trust, revocation, freshness and renewal rules on exact staged bytes.</summary>
    PdfPadesValidation Validate(Stream source, PdfPadesProfile profile, PdfOperationContext context);
}
/// <summary>Fail-closed incremental PAdES orchestration with exact-byte independent validation before publication.</summary>
public static class PdfPades
{
    /// <summary>Stages an explicit producer's output, checks exact original-prefix and DSS evidence retention, then requires a complete exact-profile validator report. Returns no unvalidated bytes.</summary>
    public static PdfSignedResult Upgrade(PdfSyntaxDocument source, PdfPadesProfile profile, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || providers == null)
            throw new ArgumentNullException(source == null ? nameof(source) : nameof(providers));
        if (!Enum.IsDefined(typeof(PdfPadesProfile), profile))
            throw new ArgumentOutOfRangeException(nameof(profile));
        var producer = providers.GetRequired<IPdfPadesProvider>(PdfCapability.Pades);
        var validator = providers.GetRequired<IPdfPadesValidator>(PdfCapability.ValidatePades);
        if (producer.Descriptor.Id == validator.Descriptor.Id)
            throw new ArgumentException("Independent producer and validator identities are required.", nameof(providers));
        PdfByteStore? staged = null;
        var selected = policy ?? source.ReadPolicy;
        var report = PdfFont.Run("UpgradePades", "managed.pades", selected, cancellationToken, c =>
        {
            if (!source.HasSignatureByteRanges || source.DecryptionApplied)
                throw Error(c, "SourceProfile", "An unencrypted existing signed document is required.");
            using var buffer = new PdfCodecBufferStream(c, c.Available(PdfResourceKind.ExpandedBytes));
            using (var input = c.BorrowExpandedInput(source.OpenOriginalStream()))
            using (var output = new BorrowedStream(buffer, false, c))
                producer.Upgrade(input, output, profile, c);
            c.Checkpoint();
            staged = buffer.Finish();
            if (staged.Length <= source.Source.Length)
                throw Error(c, "IncrementalExtent", "Profile upgrades require an appended revision.");
            for (long i = 0; i < source.Source.Length; i++)
            {
                if ((i & 4095) == 0)
                    c.Checkpoint();
                if (source.Source.At(i) != staged.At(i))
                    throw Error(c, "OriginalPrefix", "A producer changed existing signed bytes.");
            }
            var parsed = new PdfSyntaxReader(staged, c).Read();
            if (parsed.Revisions.Count <= source.Revisions.Count || !parsed.Revisions.Skip(parsed.Revisions.Count - source.Revisions.Count).Select(r => r.StartXref).SequenceEqual(source.Revisions.Select(r => r.StartXref)))
                throw Error(c, "RevisionChain", "The complete original revision chain must remain linked.");
            var prior = Evidence(source, c);
            var current = Evidence(parsed, c);
            if (!prior.IsSubsetOf(current))
                throw Error(c, "EvidenceRetention", "Existing reachable DSS validation streams cannot be removed during renewal.");
            PdfPadesValidation result;
            using (var input = c.BorrowExpandedInput(staged.Open(0, staged.Length)))
                result = validator.Validate(input, profile, c);
            c.Checkpoint();
            if (result == null || !result.Completed || !result.Compliant || result.Profile != profile || result.SourceSha256 != PdfArchival.Digest(staged, c))
                throw Error(c, "Validation", "Complete independent validation of the exact requested profile and output bytes is required.");
        }, PdfCapability.Pades);
        return new(staged!, report, selected);
    }
    private static HashSet<string> Evidence(PdfSyntaxDocument source, PdfOperationContext c)
    {
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<PdfObjectId>();
        var root = PdfSignatures.ResolveCore(source, source.Revisions[0].Trailer.Entries["Root"], c);
        if (root.Entries.TryGetValue("DSS", out var dss))
            Walk(dss);
        return hashes;
        void Walk(PdfValue value)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, 128);
            using var depth = c.EnterScope(PdfResourceKind.Depth);
            if (value.Kind == PdfValueKind.Reference)
            {
                if (seen.Add(value.ReferenceId))
                    Walk(source.Resolve(value.ReferenceId));
                return;
            }
            if (value.Kind == PdfValueKind.Stream)
            {
                using var decoded = PdfFilters.Decode(PdfFilterDeclarations.Resolve(value, id => source.Resolve(id), c), c);
                hashes.Add(PdfArchival.Digest(PdfByteStore.Read(decoded, c), c));
            }
            if (value.Kind == PdfValueKind.Array)
            foreach (var child in value.Items)
                Walk(child);
            if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
            foreach (var child in value.Entries.Values)
                Walk(child);
        }
    }
    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Pades." + code, message);
}
