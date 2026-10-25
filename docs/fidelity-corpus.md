# Independent fidelity corpus

FP-090 has a versioned original source corpus and an independent verification
runner. Complete SDK fidelity qualification remains pending.

The [corpus manifest](../tests/FidelityCorpus/manifest.json) binds four original,
MIT-licensed PDFs to exact hashes and authored Unicode, page geometry, field values,
structure roles and parent-tree expectations. Embedded original Type3 glyphs avoid
host font substitution. The PDF authoring script does not call Fluent.Pdf or learn
expected output from it.

`eng/Fidelity` opens each pinned PDF through the public API with finite limits and
saves atomically. It records runtime, architecture, loaded SDK hash and exact input
and output hashes. A source round trip alone is not a fidelity pass.

`eng/fidelity.py` independently checks the originals and outputs using pypdf and
MuPDF, then renders each through MuPDF and an explicit Poppler executable. It
retains both sets of page images and reports word-box drift, per-renderer SSIM and
changed pixels. Two unrelated renderers each compare original to rewritten output;
their different antialiasing conventions are not conflated. This controlled corpus
requires exact pixels, exact logical Unicode and word-box drift at most 0.5 points.
A small missing object fails even when average SSIM is high.

The checker rejects altered input hashes, missing or duplicate cases, changed
semantics and unsupported masks or relaxed tolerances. Golden updates follow the
[corpus review procedure](../tests/FidelityCorpus/README.md). The authoring command
writes a new directory and cannot overwrite reviewed fixtures.

## Running the source checks

Build the solution, run `python eng/test-fidelity.py`, and run the Fidelity
executable with `tests/FidelityCorpus` and a fresh output directory. Execute
`eng/fidelity.py` with `--corpus`, `--candidates`, `--output` and `--poppler` paths.
Use an analysis environment containing pypdf, PyMuPDF and Pillow. These tools are
not SDK dependencies and no tool is downloaded by the SDK. Reports record actual
tool versions and the Poppler executable hash. Retain the complete tool environment
and supporting binaries when qualifying a release.

The source CI runs reproducibility/checker boundaries and the compiled consumer on
both modern runtimes. Actual independent observations are recorded separately in
[source evidence](evidence/FP-090-source.json), not inferred from compilation.

## Remaining mandatory qualification

The four fixtures do not cover complete multilingual shaping, complex layouts,
all annotations/forms/tags, signed or historical revisions, standards validation,
manual accessibility review or every native rendering/provider profile. Those
corpora, reviewed tolerances, actual minimum/current/fallback runtimes and exact
final packages remain required. No fixture or passing source report changes
`productionReady`, requirement qualification or the publication block.
