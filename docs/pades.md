# PAdES upgrades and offline timestamp verification

FP-035, queue issue #53, has the following source profile. Full requirement scope and production qualification remain mandatory; no issue is marked complete by this source delivery.

`PdfPades.Upgrade` orchestrates explicit B, T, LT or LTA producers and independent validators. Profiles are requests, never compliance flags. Both providers must be explicitly registered with distinct identities. Core bundles no TSA client or complete PAdES producer/validator.

Output stays private until it appends a revision, preserves every original byte and the complete linked cross-reference history, retains all previously reachable decoded DSS evidence streams, and passes a complete independent report bound to the exact output SHA256 and requested profile. A missing, incomplete, mismatched or noncompliant report rejects without exposing staged bytes. The returned immutable `PdfSignedResult` can be saved atomically. Byte/evidence retention alone never establishes PAdES conformance or valid renewal.

The optional `Rfc3161TimestampVerifier.Verify` verifies a bounded DER RFC3161 token against an exact caller-pinned public TSA certificate, SHA256 imprint and nonce. It checks CMS integrity, timestamp signer purpose, signer certificate validity at authenticated generation time and request binding. It rejects trailing bytes, wrong imprints/nonces and unsupported encodings. This offline operation performs no network or ambient-store lookup and does not establish chain/revocation trust.

Endpoints, credentials, trust anchors, network authority, freshness and timeouts belong to explicitly configured providers. SDK limits/cancellation are mandatory. The report-gate fixture is deliberately not a PAdES conformance fixture. Complete B/B-T/B-LT/B-LTA production adapters, independent PAdES validation, real TSA/revocation evidence and renewal corpora remain mandatory.

Source validation is recorded in [the queue evidence ledger](evidence/issues-51-60-source.json). All four core target frameworks expose the same API; actual-runtime/final-package limitations remain explicit.
