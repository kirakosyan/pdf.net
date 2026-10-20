using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Fluent.Pdf.Cryptography;
/// <summary>Bounded asynchronous external/HSM RSA signing with mandatory public-key verification before CMS construction. Hardware bindings remain explicitly caller supplied.</summary>
public sealed class ExternalCmsSignatureProvider : IPdfAsyncSignatureProvider
{
    private readonly byte[] _certificate;
    private readonly IPdfExternalSigningKey _key;
    private readonly SemaphoreSlim _sessions;
    /// <summary>Captures one public DER certificate, explicit key factory and finite concurrent session ceiling. No private key or PIN crosses this contract.</summary>
    public ExternalCmsSignatureProvider(byte[] certificate, IPdfExternalSigningKey key, int maximumSessions = 1)
    {
        if (certificate == null || certificate.Length == 0 || certificate.Length > 65536)
            throw new ArgumentException("A bounded public DER certificate is required.", nameof(certificate));
        if (maximumSessions < 1 || maximumSessions > 64)
            throw new ArgumentOutOfRangeException(nameof(maximumSessions));
        _certificate = (byte[])certificate.Clone();
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _sessions = new(maximumSessions, maximumSessions);
    }
    /// <summary>Gets external signing service metadata; does not qualify any hardware module.</summary>
    public PdfProviderDescriptor Descriptor { get; } = new("managed.external-cms-signatures", "1.0.0", PdfCapability.Signatures);
    /// <summary>Synchronously waits for explicit external signing. Prefer SignAsync for remote/hardware latency.</summary>
    public void Sign(Stream signedBytes, Stream cmsOutput, PdfOperationContext context) => SignAsync(signedBytes, cmsOutput, context).GetAwaiter().GetResult();
    /// <summary>Signs one digest in one session, clears temporary digest/signature bytes, and disposes the session on every path. Authentication exceptions are redacted.</summary>
    public async Task SignAsync(Stream signedBytes, Stream cmsOutput, PdfOperationContext context)
    {
        var c = context ?? throw new ArgumentNullException(nameof(context));
        c.Checkpoint();
        c.Consume(PdfResourceKind.ExpandedBytes, _certificate.Length * 16L + 262144);
        using var certificate = ManagedCmsSignatureProvider.Certificate(_certificate);
        using var publicKey = certificate.GetRSAPublicKey();
        if (publicKey == null || publicKey.KeySize < 2048 || publicKey.KeySize > 8192)
            throw PdfSignatures.Error(c, "KeyProfile", "External signing requires RSA2048–8192.");
        byte[] digest = ManagedCmsSignatureProvider.Hash(signedBytes, c), signature = Array.Empty<byte>();
        try
        {
            await _sessions.WaitAsync(c.CancellationToken).ConfigureAwait(false);
            try
            {
                using var worker = c.EnterScope(PdfResourceKind.Workers);
                using var session = await _key.OpenSessionAsync(c).ConfigureAwait(false) ?? throw PdfSignatures.Error(c, "ExternalSession", "The explicit external key returned no session.");
                c.Checkpoint();
                using var buffer = new PdfCodecBufferStream(c, publicKey.KeySize / 8);
                using (var input = c.BorrowExpandedInput(new MemoryStream(digest, false)))
                using (var output = new BorrowedStream(buffer, false, c))
                    await session.SignHashAsync(input, output, c).ConfigureAwait(false);
                c.Checkpoint();
                var bytes = buffer.Finish();
                if (bytes.Length != publicKey.KeySize / 8)
                    throw PdfSignatures.Error(c, "ExternalSignature", "The external signature has an invalid extent.");
                c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length);
                signature = new byte[(int)bytes.Length];
                for (int i = 0; i < signature.Length; i++)
                    signature[i] = (byte)bytes.At(i);
                if (!publicKey.VerifyHash(digest, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                    throw PdfSignatures.Error(c, "ExternalSignature", "The external signature does not match the exact digest and certificate.");
            }
            finally { _sessions.Release(); }
            c.Checkpoint();
            ManagedCmsSignatureProvider.WriteCms(_certificate, certificate, signature, cmsOutput, c);
        }
        catch (Exception error) when (!(error is PdfException) && !(error is OperationCanceledException))
        {
            c.Checkpoint();
            throw PdfSignatures.Error(c, "ExternalOperation", "The explicit external signing operation failed; credentials and provider exception text are withheld.");
        }
        finally { Array.Clear(digest, 0, digest.Length); Array.Clear(signature, 0, signature.Length); }
    }
    /// <summary>Verifies returned CMS using the independent public-key path; never contacts the external key.</summary>
    public bool Verify(Stream signedBytes, Stream cms, PdfOperationContext context)
    {
        using var verifier = new ManagedCmsSignatureProvider();
        return verifier.Verify(signedBytes, cms, context);
    }
}
