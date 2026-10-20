using System.IO;
using System.Threading;

namespace Fluent.Pdf;
public static partial class PdfSecurity
{
    /// <summary>Writes a certificate-encrypted full rewrite using an explicit offline CMS provider and equally authorized recipient groups.</summary>
    public static PdfSecurityResult EncryptWithCertificates(PdfDocumentSnapshot source, Stream output, PdfCertificateProtection protection, PdfProviderRegistry providers, bool allowSignatureInvalidation = false, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (protection == null)
            throw new ArgumentNullException(nameof(protection));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        Output(output);
        var provider = providers.GetRequired<IPdfCmsEnvelopeProvider>(PdfCapability.CmsEnvelopes);
        return EncryptCore(source, output, (_, context) => PdfPublicKeySecurity.Create(protection, provider, context), protection.Cipher == PdfPublicKeyCipher.Aes256 ? "2.0" : "1.6", allowSignatureInvalidation, policy, cancellationToken);
    }

    /// <summary>Writes a plaintext full rewrite after matching an explicit certificate/RSA credential through the chosen offline CMS provider.</summary>
    public static PdfSecurityResult DecryptWithCertificate(Stream input, Stream output, PdfCertificateCredential credential, PdfProviderRegistry providers, bool allowLegacyEncryption = false, bool allowSignatureInvalidation = false, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (credential == null)
            throw new ArgumentNullException(nameof(credential));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        InputOutput(input, output);
        var provider = providers.GetRequired<IPdfCmsEnvelopeProvider>(PdfCapability.CmsEnvelopes);
        return DecryptCore(input, output, (dictionary, _, context) => PdfPublicKeySecurity.Authenticate(dictionary, credential, provider, allowLegacyEncryption, context), allowLegacyEncryption, allowSignatureInvalidation, policy, cancellationToken);
    }
}
