The optional image codec bridge uses the following unmodified, locked dependencies. They are compiled into the isolated native executable library, and are absent from the managed core and broker dependency graphs.

| Component | Version | License | Use |
| --- | --- | --- | --- |
| CoreJ2K | 2.4.1.108 | BSD-3-Clause | JPEG 2000 encoding/decoding |
| BitMiracle.LibJpeg.NET | 1.5.324 | BSD-3-Clause and IJG notices | JPEG encoding/decoding |
| PdfPig.Filters.Jbig2.PdfboxJbig2 | 0.1.16.2 | Apache-2.0 | JBIG2 decoding |
| PdfPig | 0.1.16 | Apache-2.0 | JBIG2 filter token/provider dependency; no document parser is invoked |

The license and notice files in this directory are copied to native bridge build/publish output. The lock file records all six native compiler runtime assets and exact content hashes. The bridge, worker and every deployment dependency must also appear in the caller's explicit SHA-256 manifest; there is no runtime discovery or download.

These dependency selections have source interoperability checks, not production qualification. Full source/security/license review, fuzzing, platform evidence and final artifact SBOM/signature verification remain mandatory release gates. The test-only TIFF library used to independently verify the original CCITT encoder is absent from this runtime bridge.
