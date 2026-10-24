#!/usr/bin/env python3
"""Independent PNG decode and PDF page-extent observations for original UI fixtures.

Uses analysis-only Pillow/PyMuPDF supplied by the caller. Constant fixture pixels
test PNG/application boundaries, never native PDF rendering fidelity.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path

import PIL
from PIL import Image
import pymupdf


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--frames", type=Path, nargs="+", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    with pymupdf.open(args.source) as pdf:
        assert len(pdf) == 2
        assert pdf[0].get_text().strip() == "Original automation Ω"
        dimensions = [(math.ceil(page.rect.width * 96 / 72), math.ceil(page.rect.height * 96 / 72)) for page in pdf]
    rows = []
    for directory in args.frames:
        files = sorted(directory.glob("*.png"))
        assert files, "No frames supplied: " + str(directory)
        for path in files:
            index = 1 if path.name == "page-1.png" else 0
            with Image.open(path) as image:
                assert image.format == "PNG" and image.size == dimensions[index]
                assert image.convert("RGBA").getextrema()[3] == (255, 255)
                rgb = image.convert("RGB")
                assert rgb.getextrema() == ((90, 90), (70, 70), (200, 200))
                rows.append(dict(path=str(path), sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                                 width=image.width, height=image.height, exactConstantPixels=True))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(dict(schemaVersion=1, sourceSha256=hashlib.sha256(args.source.read_bytes()).hexdigest(),
                                         pngReader="Pillow", pngReaderVersion=PIL.__version__,
                                         pdfReader="PyMuPDF", pdfReaderVersion=pymupdf.VersionBind,
                                         frames=rows, pdfRenderingFidelityQualified=False), indent=2) + "\n")
    print("Independent application PNG extents/pixels passed:", len(rows))


if __name__ == "__main__":
    main()
