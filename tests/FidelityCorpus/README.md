# Original fidelity corpus, version 1

These four PDFs, their glyph outlines and expected semantics are original work
distributed under the repository [MIT license](../../LICENSE). No external font,
reference-product output or SDK-generated PDF is included.

`eng/create-fidelity-corpus.py` authors the PDF syntax directly. It writes only a
new directory; `eng/test-fidelity.py` proves byte reproducibility against the
checked-in files. The manifest records individual hashes, rights and expectations.

The source profile covers original Type3 Unicode glyphs, vector paint, one tagged
paragraph with its parent-tree binding, and one text widget with an explicit
appearance. The form's appearance text is included by MuPDF extraction and omitted
by pypdf page-content extraction; both expectations are explicit. The fixtures are
unsigned and make no standards-conformance claim.

Propose a corpus change in a separate review with the old/new hashes, provenance,
rights, authored semantic changes, both independent-reader reports and rendered
diffs. Never replace expectations with SDK output. No masks or relaxed pixel
tolerances are supported in this version. Adding an exception requires reviewed
checker and manifest changes, with a reason proving it cannot conceal content loss.

This narrow corpus is not the complete FP-090 multilingual, signatures, standards,
accessibility, annotation, form, layout or provider qualification corpus.
