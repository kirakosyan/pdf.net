#!/usr/bin/env python3
"""Independent PDF/text/format and exact original-vector raster checks.

Caller-provisioned PyMuPDF/Pillow/Ghostscript are analysis-only prerequisites.
This verifies original fixtures, not the full conversion/security corpus.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile

import PIL
from PIL import Image
import pymupdf


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--directories", type=Path, nargs="+", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    original = root / "tests/Fluent.Pdf.Contract.Tests/Fixtures/Automation/original.pdf"
    vector = root / "tests/Fluent.Pdf.Contract.Tests/Fixtures/Cli/vector.pdf"
    with pymupdf.open(vector) as pdf:
        reference = pdf[0].get_pixmap(matrix=pymupdf.Matrix(2, 2), alpha=False)
        expected_pixels = reference.samples
        dimensions = reference.width, reference.height
    rows = []
    with tempfile.TemporaryDirectory(prefix="fluent-pdf-cli-independent-") as temporary:
        scratch = Path(temporary)
        for directory in args.directories:
            for name in ["selected.pdf", "rewritten.pdf", "decrypted.pdf"]:
                path = directory / name
                expected = ["Second automation page", "Original automation Ω"] if name == "selected.pdf" else ["Original automation Ω", "Second automation page"]
                with pymupdf.open(path) as pdf:
                    assert not pdf.needs_pass and len(pdf) == 2
                    assert [page.get_text().strip() for page in pdf] == expected
                output = scratch / "text.txt"
                subprocess.run(["gs", "-q", "-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=txtwrite", "-sOutputFile=" + str(output), str(path.resolve())], check=True, capture_output=True, timeout=30)
                text = output.read_text(encoding="utf-8-sig")
                assert all(part in text for part in expected) and text.index(expected[0]) < text.index(expected[1])
                rows.append(dict(path=str(path), sha256=hashlib.sha256(path.read_bytes()).hexdigest(), twoIndependentReaders=True, exactUnicodeAndOrder=True))
            svg = directory / "vector.svg"
            with pymupdf.open(svg) as pdf:
                assert len(pdf) == 1
                actual = pdf[0].get_pixmap(matrix=pymupdf.Matrix(2, 2), alpha=False)
                assert (actual.width, actual.height) == dimensions and actual.samples == expected_pixels
            eps = directory / "vector.eps"
            output = scratch / "eps.png"
            subprocess.run(["gs", "-q", "-dSAFER", "-dBATCH", "-dNOPAUSE", "-dEPSCrop", "-r144", "-sDEVICE=png16m", "-sOutputFile=" + str(output), str(eps.resolve())], check=True, capture_output=True, timeout=30)
            with Image.open(output) as image:
                assert image.size == dimensions and image.convert("RGB").tobytes() == expected_pixels
            rows.append(dict(directory=str(directory), svgSha256=hashlib.sha256(svg.read_bytes()).hexdigest(), epsSha256=hashlib.sha256(eps.read_bytes()).hexdigest(), exactOriginalVectorPixels=True))
            report = json.loads((directory / "preflight.json").read_text())
            assert report["report"]["sourceSha256"] == hashlib.sha256(original.read_bytes()).hexdigest()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(dict(schemaVersion=1, pdfReader="PyMuPDF", pdfReaderVersion=pymupdf.VersionBind,
                                         secondReader="Ghostscript", secondReaderVersion=subprocess.check_output(["gs", "--version"], text=True).strip(),
                                         pngReader="Pillow", pngReaderVersion=PIL.__version__, observations=rows,
                                         completeFidelityQualified=False, completeSecurityQualified=False, productionQualified=False), indent=2) + "\n")
    print("Independent CLI fixtures passed:", len(rows), "reader/vector groups")


if __name__ == "__main__":
    main()
