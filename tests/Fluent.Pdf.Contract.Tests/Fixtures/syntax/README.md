# Original syntax fixture

`one-page-classic.pdf` is original hand-authored PDF 1.4 syntax under the repository MIT license. It contains one Letter page and the literal text `Original syntax fixture`; it was created independently of the SDK parser/writer. SHA-256: `12dd4c61abc06a8cb10a7667d93bf8ee20cb85ba2ed50e4b73f534590f08eac1`.

Independent local inspection used Poppler pdfinfo 26.05.0 (one page, 612 x 792 pt, unencrypted PDF 1.4) and pypdf 6.10.0 with strict reading (one page and exact expected text). These observations establish this fixture's expected semantics; they are not full production qualification. The runtime tests assert the catalog, page geometry and exact raw content stream.

Generated lexical/revision/xref fixtures in SyntaxFixtures.cs are also original. Fixed/dynamic Flate constants in BootstrapFilterTests.cs encode original deterministic plaintext using Python zlib 1.3.2. Expected values are explicit and are never regenerated from the decoder under test.
