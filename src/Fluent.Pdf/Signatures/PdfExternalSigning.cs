using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fluent.Pdf;
/// <summary>An explicitly asynchronous detached signer using the same cryptographic verifier. PdfSignatures synchronous entry points reject this interface; callers must use SignAsync or SignFileAsync.</summary>
public interface IPdfAsyncSignatureProvider : IPdfSignatureProvider
{
    /// <summary>Signs once; all borrowed streams and sessions must finish before completion.</summary>
    Task SignAsync(Stream signedBytes, Stream cmsOutput, PdfOperationContext context);
}
/// <summary>A caller-owned external key/session factory. Authentication and key identity stay inside the provider.</summary>
public interface IPdfExternalSigningKey
{
    /// <summary>Opens one bounded authenticated session without exporting a private key.</summary>
    Task<IPdfExternalSigningSession> OpenSessionAsync(PdfOperationContext context);
}
/// <summary>One external RSA-SHA256-PKCS1 signing session; disposal must release the hardware/remote lease.</summary>
public interface IPdfExternalSigningSession : IDisposable
{
    /// <summary>Signs the exact32-byte SHA256 digest with RSA-PKCS1 and emits the raw RSA signature, never CMS. No secret logging is permitted.</summary>
    Task SignHashAsync(Stream sha256Digest, Stream signatureOutput, PdfOperationContext context);
}
public static partial class PdfSignatures
{
    /// <summary>Asynchronously signs privately, verifies exact CMS integrity, and returns only completed immutable output.</summary>
    public static async Task<PdfSignedResult> SignAsync(PdfSyntaxDocument source, PdfSignatureOptions options, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Validate(source, options, providers);
        PdfByteStore? bytes = null;
        var selected = policy ?? source.ReadPolicy;
        var report = await PdfFont.RunAsync("SignPdfAsync", "managed.signatures", selected, cancellationToken, async c => bytes = await BuildAsync(source, options, providers, c).ConfigureAwait(false), PdfCapability.Signatures).ConfigureAwait(false);
        return new(bytes!, report, selected);
    }
    /// <summary>Asynchronously signs and self-verifies before atomic file replacement.</summary>
    public static async Task<PdfSignedResult> SignFileAsync(PdfSyntaxDocument source, PdfSignatureOptions options, PdfProviderRegistry providers, string path, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Validate(source, options, providers);
        Guard.Text(path, nameof(path), 32767);
        PdfByteStore? bytes = null;
        var selected = policy ?? source.ReadPolicy;
        var report = await PdfFont.RunAsync("SignPdfFileAsync", "managed.signatures", selected, cancellationToken, async c =>
        {
            bytes = await BuildAsync(source, options, providers, c).ConfigureAwait(false);
            PdfAtomicFile.Write(path, c, output => Copy(bytes, output, c));
        }, PdfCapability.Signatures).ConfigureAwait(false);
        return new(bytes!, report, selected);
    }
}
