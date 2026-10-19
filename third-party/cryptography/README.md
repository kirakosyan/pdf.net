# Optional CMS dependencies

System.Formats.Asn1 10.0.13 supplies DER parsing/writing; System.Security.Cryptography.Cng 5.0.0 supplies guarded Windows RSA operations on older targets. Both are MIT licensed Microsoft .NET components. Runtime cryptographic primitives use the platform. The locked direct/transitive/build package inventory and retained notices are in components.json; final SBOM, vulnerability/license review and runtime qualification remain mandatory. No native engine, private key or package binary is distributed in this directory.

The detached signature source provider reuses these exact pinned dependencies for original DER SignedData, SHA256/RSA-PKCS1, explicit borrowed public-certificate/RSA handles and explicit ephemeral PKCS12 import. No dependency, private key, native engine or model is added. Signing/trust/revocation/PAdES and hostile key-import/runtime qualification remain mandatory.
