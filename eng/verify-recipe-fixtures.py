#!/usr/bin/env python3
"""Independent original recipe PDF, geometry, Unicode and language parity checks."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile

import pymupdf


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--directories", nargs="+", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    rows = []
    with tempfile.TemporaryDirectory(prefix="fluent-pdf-recipes-independent-") as temporary:
        for directory in args.directories:
            for path in sorted(directory.glob("*.pdf")):
                with pymupdf.open(path) as document:
                    assert not document.needs_pass
                    if path.name in ["selected.pdf", "optimized.pdf"]:
                        expected = ["Second automation page", "Original automation Ω"] if path.name == "selected.pdf" else ["Original automation Ω", "Second automation page"]
                        assert len(document) == 2 and [page.get_text().strip() for page in document] == expected
                    else:
                        assert len(document) == 1
                        assert document[0].rotation == (90 if "canvas" in path.name else 0)
                    count = len(document)
                subprocess.run(["gs", "-q", "-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=nullpage", str(path.resolve())], check=True, capture_output=True, timeout=30)
                rows.append(dict(path=str(path), sha256=hashlib.sha256(path.read_bytes()).hexdigest(), pages=count, independentReaders=2))
            for kind in ["canvas", "vector"]:
                with pymupdf.open(directory / ("cs-" + kind + ".pdf")) as cs, pymupdf.open(directory / ("vb-" + kind + ".pdf")) as vb:
                    a, b = cs[0].get_pixmap(), vb[0].get_pixmap()
                    assert (a.width, a.height, a.samples) == (b.width, b.height, b.samples)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(dict(schemaVersion=1, pdfReader="PyMuPDF", pdfReaderVersion=pymupdf.VersionBind,
                                         secondReader="Ghostscript", secondReaderVersion=subprocess.check_output(["gs", "--version"], text=True).strip(),
                                         outputs=rows, exactLanguageVectorCanvasPixels=True, completeFidelityQualified=False, productionQualified=False), indent=2) + "\n")
    print("INDEPENDENT_RECIPES_PASS;" + str(len(rows)) + "-PDFs;two-readers;exact-language-pixels")


if __name__ == "__main__":
    main()
