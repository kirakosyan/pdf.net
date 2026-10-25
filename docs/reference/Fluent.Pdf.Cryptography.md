# Fluent.Pdf.Cryptography source reference

Version `1.0.0`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).

## Public declarations

The baseline preserves exact nullability, parameter names, defaults and constant values.

```text
Fluent.Pdf.Cryptography.ExternalCmsSignatureProvider
Fluent.Pdf.Cryptography.ExternalCmsSignatureProvider.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Cryptography.ExternalCmsSignatureProvider.ExternalCmsSignatureProvider(byte[]! certificate, Fluent.Pdf.IPdfExternalSigningKey! key, int maximumSessions = 1) -> void
Fluent.Pdf.Cryptography.ExternalCmsSignatureProvider.Sign(System.IO.Stream! signedBytes, System.IO.Stream! cmsOutput, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Cryptography.ExternalCmsSignatureProvider.SignAsync(System.IO.Stream! signedBytes, System.IO.Stream! cmsOutput, Fluent.Pdf.PdfOperationContext! context) -> System.Threading.Tasks.Task!
Fluent.Pdf.Cryptography.ExternalCmsSignatureProvider.Verify(System.IO.Stream! signedBytes, System.IO.Stream! cms, Fluent.Pdf.PdfOperationContext! context) -> bool
Fluent.Pdf.Cryptography.ManagedCmsEnvelopeProvider
Fluent.Pdf.Cryptography.ManagedCmsEnvelopeProvider.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Cryptography.ManagedCmsEnvelopeProvider.Encrypt(System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfCertificateRecipient!>! recipients, System.IO.Stream! plaintext, System.IO.Stream! envelope, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Cryptography.ManagedCmsEnvelopeProvider.ManagedCmsEnvelopeProvider() -> void
Fluent.Pdf.Cryptography.ManagedCmsEnvelopeProvider.TryDecrypt(Fluent.Pdf.PdfCertificateCredential! credential, bool allowLegacyKeyTransport, System.IO.Stream! envelope, System.IO.Stream! plaintext, Fluent.Pdf.PdfOperationContext! context) -> bool
Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider
Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.Dispose() -> void
Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.ImportReport.get -> Fluent.Pdf.PdfOperationReport?
Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.ManagedCmsSignatureProvider() -> void
Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.ManagedCmsSignatureProvider(Fluent.Pdf.PdfCertificateCredential! credential) -> void
Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.Sign(System.IO.Stream! signedBytes, System.IO.Stream! cmsOutput, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.Verify(System.IO.Stream! signedBytes, System.IO.Stream! cms, Fluent.Pdf.PdfOperationContext! context) -> bool
Fluent.Pdf.Cryptography.PinnedSignatureTrustProvider
Fluent.Pdf.Cryptography.PinnedSignatureTrustProvider.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Cryptography.PinnedSignatureTrustProvider.Evaluate(System.IO.Stream! cms, Fluent.Pdf.PdfSignatureTrustOptions! options, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfSignatureTrustDecision!
Fluent.Pdf.Cryptography.PinnedSignatureTrustProvider.PinnedSignatureTrustProvider(System.Collections.Generic.IEnumerable<byte[]!>! trustedSignerCertificates, Fluent.Pdf.IPdfCertificateStatusProvider? statusProvider = null) -> void
Fluent.Pdf.Cryptography.Rfc3161TimestampVerifier
static Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.FromPkcs12(System.IO.Stream! input, string! password, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider!
static Fluent.Pdf.Cryptography.Rfc3161TimestampVerifier.Verify(System.IO.Stream! token, byte[]! tsaCertificate, byte[]! sha256Imprint, byte[]! nonce, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.DateTimeOffset
```

## Member documentation

### <code>M:Fluent.Pdf.Cryptography.ExternalCmsSignatureProvider.#ctor(System.Byte[],Fluent.Pdf.IPdfExternalSigningKey,System.Int32)</code>

Captures one public DER certificate, explicit key factory and finite concurrent session ceiling. No private key or PIN crosses this contract.

### <code>M:Fluent.Pdf.Cryptography.ExternalCmsSignatureProvider.Sign(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Rejects synchronous external signing before invoking the key. Use SignAsync to avoid blocking caller synchronization contexts.

### <code>M:Fluent.Pdf.Cryptography.ExternalCmsSignatureProvider.SignAsync(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Signs one digest in one session, clears temporary digest/signature bytes, and disposes the session on every path. Authentication exceptions are redacted.

### <code>M:Fluent.Pdf.Cryptography.ExternalCmsSignatureProvider.Verify(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Verifies returned CMS using the independent public-key path; never contacts the external key.

### <code>M:Fluent.Pdf.Cryptography.ManagedCmsEnvelopeProvider.#ctor</code>

Creates a stateless explicit software-envelope provider.

### <code>M:Fluent.Pdf.Cryptography.ManagedCmsEnvelopeProvider.Encrypt(System.Collections.Generic.IReadOnlyList{Fluent.Pdf.PdfCertificateRecipient},System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Encrypts a 20/24-byte PDF seed payload for up to 64 explicit recipients with RSA keys of 2048–8192 bits.

### <code>M:Fluent.Pdf.Cryptography.ManagedCmsEnvelopeProvider.TryDecrypt(Fluent.Pdf.PdfCertificateCredential,System.Boolean,System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Decrypts only a matching explicit recipient and RSA handle. DER and payload extents are bounded; legacy RSA/SHA-1 transport needs explicit permission.

### <code>M:Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.#ctor</code>

Creates an offline integrity verifier without a signing key.

### <code>M:Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.#ctor(Fluent.Pdf.PdfCertificateCredential)</code>

Borrows an explicit public certificate/RSA credential. Certificates loaded by the caller from PKCS12, stores or hardware providers use the same boundary; the key remains caller-owned.

### <code>M:Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.Dispose</code>

Releases only keys/certificates imported by FromPkcs12; borrowed caller credentials remain alive.

### <code>M:Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.FromPkcs12(System.IO.Stream,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Imports a bounded explicit PKCS12 through managed codecs and a memory-only RSA import. The password is never logged; this returned provider owns the imported certificate/key and must be disposed.

### <code>M:Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.Sign(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Signs exact streamed bytes with a borrowed/imported RSA key and emits one detached DER CMS. No timestamp or signed attribute is synthesized.

### <code>M:Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.Verify(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Verifies one detached DER SHA256/RSA CMS with one embedded signer certificate and no signed attributes. Integrity is independent of certificate trust, expiry, revocation and permitted later edits.

### <code>M:Fluent.Pdf.Cryptography.PinnedSignatureTrustProvider.#ctor(System.Collections.Generic.IEnumerable{System.Byte[]},Fluent.Pdf.IPdfCertificateStatusProvider)</code>

Captures at most64 explicit DER signer certificates. No machine certificate store is consulted. A missing status authority yields Unknown.

### <code>M:Fluent.Pdf.Cryptography.PinnedSignatureTrustProvider.Evaluate(System.IO.Stream,Fluent.Pdf.PdfSignatureTrustOptions,Fluent.Pdf.PdfOperationContext)</code>

Evaluates a single embedded certificate. Mathematical verification and signer binding must already have succeeded.

### <code>M:Fluent.Pdf.Cryptography.Rfc3161TimestampVerifier.Verify(System.IO.Stream,System.Byte[],System.Byte[],System.Byte[],Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Checks the timestamp CMS signature, signer-purpose/profile, certificate validity at generation, exact SHA256 imprint and nonce. Returns authenticated generation time; performs no network or ambient-store lookup.

### <code>P:Fluent.Pdf.Cryptography.ExternalCmsSignatureProvider.Descriptor</code>

Gets external signing service metadata; does not qualify any hardware module.

### <code>P:Fluent.Pdf.Cryptography.ManagedCmsEnvelopeProvider.Descriptor</code>

Gets source capability metadata, independent of production qualification.

### <code>P:Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.Descriptor</code>

Gets explicit detached signature service metadata; this is not trust or production qualification.

### <code>P:Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider.ImportReport</code>

Gets the bounded PKCS12 import report, or null for a borrowed credential/verifier.

### <code>P:Fluent.Pdf.Cryptography.PinnedSignatureTrustProvider.Descriptor</code>

Gets explicit service identity.

### <code>T:Fluent.Pdf.Cryptography.ExternalCmsSignatureProvider</code>

Bounded asynchronous external/HSM RSA signing with mandatory public-key verification before CMS construction. Hardware bindings remain explicitly caller supplied.

### <code>T:Fluent.Pdf.Cryptography.ManagedCmsEnvelopeProvider</code>

Offline RSA/AES PDF seed envelopes using platform cryptography and the pinned ASN.1 codec. No certificate stores, network trust retrieval or private-key export.

### <code>T:Fluent.Pdf.Cryptography.ManagedCmsSignatureProvider</code>

Original offline DER CMS SignedData using SHA256/RSA-PKCS1 and one explicit public certificate. No store/key discovery, network, trust inference or private-key export.

### <code>T:Fluent.Pdf.Cryptography.PinnedSignatureTrustProvider</code>

Offline exact-certificate trust pins for the managed single-signer CMS profile. This is explicit identity pinning, not general PKIX path building or timestamp validation.

### <code>T:Fluent.Pdf.Cryptography.Rfc3161TimestampVerifier</code>

Offline cryptographic RFC3161 verification with an exact caller-pinned TSA certificate, SHA256 imprint and nonce. Chain/revocation trust remains separate.

