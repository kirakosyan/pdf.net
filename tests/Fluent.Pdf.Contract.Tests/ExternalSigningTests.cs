using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Fluent.Pdf.Cryptography;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class ExternalSigningTests
{
    [Fact]
    public async Task ExternalSessionsAreBoundedDisposedAndSignaturesChecked()
    {
        using var certificate = new CertificateSecurityTests.Certificate("External key");
        var key = new Key(certificate.Key);
        var provider = new ExternalCmsSignatureProvider(certificate.Value.RawData, key, 1);
        var source = SignatureTests.Prepared();
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => PdfSignatures.SignAsync(source, new("First"), new(provider))));
        Assert.Equal(4, key.Opened);
        Assert.Equal(4, key.Disposed);
        Assert.Equal(1, key.Peak);
        foreach (var result in results)
        {
            using var input = result.OpenPdfStream();
            Assert.True(Assert.Single(PdfSignatures.Inspect(PdfSyntax.Read(input), new(provider))).CryptographicallyValid);
        }
        key.BadSignature = true;
        Assert.Equal("Signature.ExternalSignature", (await Assert.ThrowsAsync<PdfException>(() => PdfSignatures.SignAsync(source, new("First"), new(provider)))).Diagnostic.Code);
        Assert.Equal(key.Opened, key.Disposed);
        key.BadSignature = false;
        key.Fail = true;
        var failure = await Assert.ThrowsAsync<PdfException>(() => PdfSignatures.SignAsync(source, new("First"), new(provider)));
        Assert.Equal("Signature.ExternalOperation", failure.Diagnostic.Code);
        Assert.DoesNotContain("secret-pin", failure.ToString());
        Assert.Equal(key.Opened, key.Disposed);
    }
    [Fact]
    public async Task CancellationNeverCommitsAndClosesExternalSession()
    {
        using var certificate = new CertificateSecurityTests.Certificate("Canceled key");
        using var cancel = new CancellationTokenSource();
        var key = new Key(certificate.Key) { Cancel = cancel };
        var provider = new ExternalCmsSignatureProvider(certificate.Value.RawData, key);
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        File.WriteAllText(path, "retained");
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfSignatures.SignFileAsync(SignatureTests.Prepared(), new("First"), new(provider), path, cancellationToken: cancel.Token));
            Assert.Equal("retained", File.ReadAllText(path));
            Assert.Equal(key.Opened, key.Disposed);
        }
        finally { File.Delete(path); }
    }
    private sealed class Key : IPdfExternalSigningKey
    {
        private readonly RSA _rsa;
        internal int Opened, Disposed, Active, Peak;
        internal bool BadSignature, Fail;
        internal CancellationTokenSource? Cancel;
        internal Key(RSA rsa) => _rsa = rsa;
        public Task<IPdfExternalSigningSession> OpenSessionAsync(PdfOperationContext context)
        {
            Opened++;
            Active++;
            Peak = Math.Max(Peak, Active);
            return Task.FromResult<IPdfExternalSigningSession>(new Session(this));
        }
        private sealed class Session : IPdfExternalSigningSession
        {
            private readonly Key _key;
            internal Session(Key key) => _key = key;
            public async Task SignHashAsync(Stream digest, Stream signature, PdfOperationContext context)
            {
                await Task.Delay(5, context.CancellationToken).ConfigureAwait(false);
                if (_key.Fail)
                    throw new InvalidOperationException("secret-pin");
                _key.Cancel?.Cancel();
                context.Checkpoint();
                using var data = new MemoryStream();
                digest.CopyTo(data);
                var bytes = _key._rsa.SignHash(data.ToArray(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                if (_key.BadSignature)
                    bytes[0] ^= 1;
                signature.Write(bytes, 0, bytes.Length);
            }
            public void Dispose()
            {
                _key.Disposed++;
                _key.Active--;
            }
        }
    }
}
