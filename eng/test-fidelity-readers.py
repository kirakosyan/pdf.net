"""Run explicitly in the independent-reader environment; no skipped fallbacks."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("fidelity", Path(__file__).with_name("fidelity.py"))
fidelity = importlib.util.module_from_spec(spec); spec.loader.exec_module(fidelity)
corpus = Path(__file__).resolve().parents[1]/"tests/FidelityCorpus"
manifest = fidelity.load_manifest(corpus)


class ReaderBoundaries(unittest.TestCase):
    def test_original_semantics_pass(self):
        for fixture in manifest["fixtures"]:
            fidelity.semantics(corpus/fixture["file"], fixture["expected"])

    def test_semantic_losses_fail_independently_of_images(self):
        mutations = [("glyphs",b"(ABC) Tj",b"(ABB) Tj"), ("glyphs",b"12 70 Tm",b"13 70 Tm"),
                     ("forms",b"/V (AB)",b"/V (AC)"), ("structure",b"/S /P",b"/S /H"),
                     ("structure",b"/K 0",b"/K 1"), ("structure",b"/MCID 0",b"/MCID 1")]
        with tempfile.TemporaryDirectory() as directory:
            for kind, before, after in mutations:
                with self.subTest(kind=kind, before=before):
                    fixture = next(x for x in manifest["fixtures"] if x["id"]==kind)
                    source = (corpus/fixture["file"]).read_bytes()
                    self.assertIn(before,source)
                    candidate = Path(directory)/"changed.pdf"
                    candidate.write_bytes(source.replace(before,after))
                    with self.assertRaises(ValueError): fidelity.semantics(candidate,fixture["expected"])


if __name__ == "__main__": unittest.main()
