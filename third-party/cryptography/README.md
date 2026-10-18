# Optional CMS dependencies

System.Formats.Asn1 10.0.13 supplies DER parsing/writing; System.Security.Cryptography.Cng 5.0.0 supplies guarded Windows RSA operations on older targets. Both are MIT licensed Microsoft .NET components. Runtime cryptographic primitives use the platform. The locked direct/transitive/build package inventory and retained notices are in components.json; final SBOM, vulnerability/license review and runtime qualification remain mandatory. No native engine, private key or package binary is distributed in this directory.
