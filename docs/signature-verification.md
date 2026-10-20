# Signature verification and explicit trust

FP-033, queue issue #51, has the following source profile. Full requirement scope and production qualification remain mandatory; no issue is marked complete by this source delivery.

`PdfSignatures.Verify` validates the exact two byte ranges and excluded Contents token, verifies detached CMS integrity, and invokes an explicit `IPdfSignatureTrustProvider` only after integrity succeeds. Each result separates mathematical validity, identity trust, certificate time validity, revocation and later-byte extent. `Accepted` requires all identity checks and fresh Good revocation; unknown/offline and revoked remain unaccepted. It does not authorize later modifications.

The optional `PinnedSignatureTrustProvider` supports exact public-DER signer pins for the existing single-certificate SHA256/RSA CMS profile. Evaluation uses a caller-supplied instant, not PDF signing-time strings or the machine clock. It never searches platform stores or constructs an implicit chain. An optional caller-owned `IPdfCertificateStatusProvider` supplies status for that exact certificate and instant; it is responsible for authenticated fresh OCSP/CRL responses. Without one, status is Unknown. Certificates/streams remain caller-owned. All nested verification/status work shares the SDK context.

```csharp
var results = PdfSignatures.Verify(source,
    new PdfProviderRegistry(integrityVerifier, explicitTrustProvider),
    new PdfSignatureTrustOptions(evaluationInstant));
```

General PKIX path construction, real OCSP/CRL authorities, TSA trust and semantic permitted-edit validation remain mandatory production work. A pin/status fixture is not a general trust-chain qualification.

Source validation is recorded in [the queue evidence ledger](evidence/issues-51-60-source.json). All four core target frameworks expose the same API; actual-runtime/final-package limitations remain explicit.
