"""Independent-corpus checker boundaries. Synthetic mutations are not qualification."""
import copy
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("fidelity", Path(__file__).with_name("fidelity.py"))
fidelity = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fidelity)
root = Path(__file__).resolve().parents[1]
contract = dict(dpi=144, minimumSsim=1.0, maximumChangedPixels=0, masks=[], toleranceReview=None)


class Boundaries(unittest.TestCase):
    def test_identical_and_constant_images(self):
        for value in (0, 128, 255):
            pixels = bytes([value])*8*8*3
            self.assertEqual(1.0, fidelity.compare_pixels(pixels,pixels,8,8,contract)["ssim"])

    def test_small_omission_fails_even_with_high_average_ssim(self):
        white = bytes([255])*128*128*3
        changed = bytearray(white)
        changed[0:3] = b"\x00\x00\x00"
        self.assertGreater(fidelity.ssim(white,changed,128,128), .995)
        with self.assertRaisesRegex(ValueError, "Missing/changed"):
            fidelity.compare_pixels(white,changed,128,128,contract)

    def test_dimensions_and_channels_are_not_coerced(self):
        for width,height,a,b in [(0,1,b"",b""),(1,1,b"abc",b"ab"),(2,1,b"abc",b"abc")]:
            with self.assertRaises(ValueError): fidelity.ssim(a,b,width,height)

    def test_masks_and_relaxed_or_nan_thresholds_fail(self):
        for key,value in [("minimumSsim",.994),("minimumSsim",float("nan")),("maximumChangedPixels",1),("masks",[[0,0,8,8]]),("toleranceReview","unverified")]:
            altered = {**contract,key:value}
            with self.assertRaises(ValueError): fidelity.compare_pixels(bytes(192),bytes(192),8,8,altered)

    def test_missing_duplicate_and_changed_corpus_reject(self):
        with tempfile.TemporaryDirectory() as directory:
            corpus = Path(directory)/"corpus"
            shutil.copytree(root/"tests/FidelityCorpus",corpus)
            original = json.loads((corpus/"manifest.json").read_text(encoding="utf-8"))
            for mutation in (lambda m:m["fixtures"].pop(), lambda m:m["fixtures"].append(m["fixtures"][0]), lambda m:m["fixtures"][0].update(file="../escape.pdf"), lambda m:m["fixtures"][0].update(license="unknown")):
                manifest = copy.deepcopy(original); mutation(manifest)
                (corpus/"manifest.json").write_text(json.dumps(manifest),encoding="utf-8")
                with self.assertRaises(ValueError): fidelity.load_manifest(corpus)
            (corpus/"manifest.json").write_text(json.dumps(original),encoding="utf-8")
            (corpus/"glyphs.pdf").write_bytes(b"changed")
            with self.assertRaisesRegex(ValueError,"Changed fixture"): fidelity.load_manifest(corpus)

    def test_original_generation_is_byte_reproducible(self):
        generator = importlib.util.spec_from_file_location("generator",root/"eng/create-fidelity-corpus.py")
        module = importlib.util.module_from_spec(generator); generator.loader.exec_module(module)
        with tempfile.TemporaryDirectory() as directory:
            corpus = Path(directory)/"corpus"; module.create(corpus)
            actual = fidelity.load_manifest(corpus)
            self.assertEqual(actual,fidelity.load_manifest(root/"tests/FidelityCorpus"))
            for fixture in actual["fixtures"]:
                self.assertEqual((corpus/fixture["file"]).read_bytes(),(root/"tests/FidelityCorpus"/fixture["file"]).read_bytes())


if __name__ == "__main__": unittest.main()
