using System.IO;
using System.Threading;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Tsp;
using Org.BouncyCastle.Math;

namespace Fluent.Pdf.Cryptography;
/// <summary>Offline cryptographic RFC3161 verification with an exact caller-pinned TSA certificate, SHA256 imprint and nonce. Chain/revocation trust remains separate.</summary>
public static class Rfc3161TimestampVerifier
{
    /// <summary>Checks the timestamp CMS signature, signer-purpose/profile, certificate validity at generation, exact SHA256 imprint and nonce. Returns authenticated generation time; performs no network or ambient-store lookup.</summary>
    public static DateTimeOffset Verify(Stream token, byte[] tsaCertificate, byte[] sha256Imprint, byte[] nonce, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (token == null || tsaCertificate == null || sha256Imprint == null || nonce == null)
            throw new ArgumentNullException(token == null ? nameof(token) : tsaCertificate == null ? nameof(tsaCertificate) : sha256Imprint == null ? nameof(sha256Imprint) : nameof(nonce));
        if (tsaCertificate.Length == 0 || tsaCertificate.Length > 65536 || sha256Imprint.Length != 32 || nonce.Length == 0 || nonce.Length > 32)
            throw new ArgumentException("A bounded exact certificate, SHA256 imprint and nonce are required.");
        DateTimeOffset result = default;
        PdfFont.Run("VerifyRfc3161Timestamp", "managed.rfc3161", policy, cancellationToken, c =>
        {
            c.Consume(PdfResourceKind.ExpandedBytes, 8388608 + tsaCertificate.Length * 16L);
            var bytes = new byte[1048577];
            int at = 0;
            using (var input = new BorrowedStream(token, true, c))
                while (at < bytes.Length)
                {
                    c.Checkpoint();
                    int n = input.Read(bytes, at, Math.Min(4096, bytes.Length - at));
                    if (n == 0)
                        break;
                    at += n;
                }
            if (at == bytes.Length)
                throw PdfSignatures.Error(c, "TimestampExtent", "Timestamp token exceeds its finite extent.");
            try
            {
                BoundedSignatureDer.Validate(bytes, at, c);
                BoundedSignatureDer.Validate(tsaCertificate, tsaCertificate.Length, c);
                var encoded = new ReadOnlyMemory<byte>(bytes, 0, at);
                var der = new System.Formats.Asn1.AsnReader(encoded, System.Formats.Asn1.AsnEncodingRules.DER);
                _ = der.ReadSequence();
                der.ThrowIfNotEmpty();
                BoundedSignatureDer.TimestampContent(encoded, tsaCertificate, c);
                var cms = new CmsSignedData(encoded.ToArray());
                var timestamp = new TimeStampToken(cms);
                var certificate = new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(tsaCertificate);
                timestamp.Validate(certificate);
                var info = timestamp.TimeStampInfo;
                if (info.MessageImprintAlgOid != "2.16.840.1.101.3.4.2.1" || !info.GetMessageImprintDigest().SequenceEqual(sha256Imprint) || info.Nonce == null || !info.Nonce.Equals(new BigInteger(1, nonce)))
                    throw PdfSignatures.Error(c, "TimestampBinding", "The timestamp imprint or nonce does not match the exact request.");
                certificate.CheckValidity(info.GenTime);
                c.Checkpoint();
                result = new DateTimeOffset(DateTime.SpecifyKind(info.GenTime, DateTimeKind.Utc));
            }
            catch (Exception failure) when (!(failure is PdfException) && !(failure is OperationCanceledException))
            {
                c.Checkpoint();
                throw PdfSignatures.Error(c, "TimestampInvalid", "The RFC3161 signature, certificate or token profile is invalid.");
            }
        }, PdfCapability.Signatures);
        return result;
    }
}
