# External and hardware signing sessions

FP-034, queue issue #52, has the following source profile. Full requirement scope and production qualification remain mandatory; no issue is marked complete by this source delivery.

`PdfSignatures.SignAsync` and `SignFileAsync` preserve the same exact-byte reservation, single signer invocation, cryptographic self-verification and atomic file publication as synchronous signing. `IPdfAsyncSignatureProvider` adds asynchronous signing without background-thread wrapping of external key operations. The synchronous API rejects asynchronous providers with `Signature.AsyncRequired` before opening a key session. Use the asynchronous entry points; this preserves caller synchronization-context safety.

The optional `ExternalCmsSignatureProvider` accepts one public DER certificate and an `IPdfExternalSigningKey` session factory. A session signs only the exact SHA256 digest using RSA2048–8192 PKCS1; no private key or PIN crosses the API. A finite provider-local semaphore caps active sessions; SDK worker scopes, the shared cancellation token and deadlines apply. Every session is disposed on success, cancellation, malformed output and provider errors. Returned raw signatures must verify against the exact digest and public certificate before CMS construction and final PDF self-verification. Provider exception messages are withheld; temporary digest/signature arrays are cleared.

```csharp
var signer = new ExternalCmsSignatureProvider(publicDer, configuredKey, maximumSessions: 2);
var result = await PdfSignatures.SignAsync(source,
    new PdfSignatureOptions("Approval"), new PdfProviderRegistry(signer));
```

The caller owns key/session implementations and their credential/network/module policy. Native PKCS#11 module loading, actual software-token/HSM provisioning, device login/error matrices and production session/endurance qualification remain mandatory. The included session fixture exercises this boundary with a local RSA key; it does not qualify hardware.

Source validation is recorded in [the queue evidence ledger](evidence/issues-51-60-source.json). All four core target frameworks expose the same API; actual-runtime/final-package limitations remain explicit.

PR #119 review fixes and focused regression evidence are recorded in [the review ledger](evidence/pr119-review-source.json).
