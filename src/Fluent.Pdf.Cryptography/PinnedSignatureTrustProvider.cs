using System.Formats.Asn1;
using System.IO;
using System.Security.Cryptography;

namespace Fluent.Pdf.Cryptography;

/// <summary>Offline exact-certificate trust pins for the managed single-signer CMS profile. This is explicit identity pinning, not general PKIX path building or timestamp validation.</summary>
public sealed class PinnedSignatureTrustProvider : IPdfSignatureTrustProvider
{
    private readonly byte[][] _pins;
    private readonly IPdfCertificateStatusProvider? _status;
    /// <summary>Captures at most64 explicit DER signer certificates. No machine certificate store is consulted. A missing status authority yields Unknown.</summary>
    public PinnedSignatureTrustProvider(IEnumerable<byte[]> trustedSignerCertificates, IPdfCertificateStatusProvider? statusProvider = null)
    {
        if (trustedSignerCertificates == null)
            throw new ArgumentNullException(nameof(trustedSignerCertificates));
        var pins = new List<byte[]>();
        foreach (var pin in trustedSignerCertificates)
        {
            if (pins.Count == 64 || pin == null || pin.Length == 0 || pin.Length > 65536)
                throw new ArgumentException("At most64 bounded DER certificates are required.", nameof(trustedSignerCertificates));
            pins.Add((byte[])pin.Clone());
        }
        _pins = pins.ToArray();
        _status = statusProvider;
    }
    /// <summary>Gets explicit service identity.</summary>
    public PdfProviderDescriptor Descriptor { get; } = new("managed.pinned-signature-trust", "1.0.0", PdfCapability.SignatureTrust);
    /// <summary>Evaluates a single embedded certificate. Mathematical verification and signer binding must already have succeeded.</summary>
    public PdfSignatureTrustDecision Evaluate(Stream cms, PdfSignatureTrustOptions options, PdfOperationContext context)
    {
        if (cms == null || options == null || context == null)
            throw new ArgumentNullException(cms == null ? nameof(cms) : options == null ? nameof(options) : nameof(context));
        var c = context;
        c.Checkpoint();
        c.Consume(PdfResourceKind.ExpandedBytes, _pins.Sum(p => (long)p.Length));
        var raw = SignerCertificate(cms, c);
        c.Consume(PdfResourceKind.ExpandedBytes, raw.Length * 16L + 65536);
        BoundedSignatureDer.Validate(raw, raw.Length, c);
        Org.BouncyCastle.X509.X509Certificate certificate;
        try
        {
            certificate = new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(raw) ?? throw new Org.BouncyCastle.Security.Certificates.CertificateException("Unsupported certificate choice.");
        }
        catch (Org.BouncyCastle.Security.Certificates.CertificateException)
        {
            c.Checkpoint();
            throw new PdfException(new PdfDiagnostic("Signature.UnsupportedProfile", "Trust requires a supported DER X509 certificate."));
        }
        bool time = options.EvaluationTime.UtcDateTime >= certificate.NotBefore.ToUniversalTime() && options.EvaluationTime.UtcDateTime <= certificate.NotAfter.ToUniversalTime();
        bool trusted = _pins.Any(p => p.SequenceEqual(raw));
        var status = PdfRevocationStatus.Unknown;
        if (_status != null)
        {
            using var input = c.BorrowExpandedInput(new MemoryStream(raw, false));
            status = _status.Check(input, options.EvaluationTime, c);
            c.Checkpoint();
            if (!Enum.IsDefined(typeof(PdfRevocationStatus), status))
                throw PdfSignatures.Error(c, "TrustResult", "Unknown certificate-status enumeration.");
        }
        using var hash = SHA256.Create();
        string fingerprint = BitConverter.ToString(hash.ComputeHash(raw)).Replace("-", "").ToLowerInvariant();
        c.Checkpoint();
        return new(trusted, time, status, fingerprint);
    }
    internal static byte[] SignerCertificate(Stream cms, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 2097154);
        var bytes = new byte[1048577];
        int at = 0;
        using var input = c.BorrowExpandedInput(cms);
        while (at < bytes.Length)
        {
            c.Checkpoint();
            int n = input.Read(bytes, at, Math.Min(4096, bytes.Length - at));
            if (n == 0)
                break;
            at += n;
        }
        if (at == bytes.Length)
            throw PdfSignatures.Error(c, "CmsExtent", "CMS exceeds the explicit trust profile.");
        try
        {
            var reader = new AsnReader(new ReadOnlyMemory<byte>(bytes, 0, at), AsnEncodingRules.DER);
            var outer = reader.ReadSequence();
            if (outer.ReadObjectIdentifier() != "1.2.840.113549.1.7.2")
                throw new AsnContentException();
            var data = outer.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true)).ReadSequence();
            _ = data.ReadInteger();
            _ = data.ReadSetOf();
            _ = data.ReadSequence();
            var certificates = data.ReadSetOf(new Asn1Tag(TagClass.ContextSpecific, 0, true));
            var certificate = certificates.ReadEncodedValue();
            if (certificates.HasData || certificate.Length > 65536 || certificate.Span[0] != 0x30)
                throw new AsnContentException();
            return certificate.ToArray();
        }
        catch (AsnContentException) { c.Checkpoint(); throw new PdfException(new PdfDiagnostic("Signature.UnsupportedProfile", "Trust requires the single embedded signer certificate profile.")); }
    }
}
