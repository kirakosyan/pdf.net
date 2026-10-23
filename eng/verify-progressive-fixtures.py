"""Controlled development checks; producer self-check is not independent conformance."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import fitz
from PIL import Image, ImageChops
import pikepdf

parser = argparse.ArgumentParser()
parser.add_argument("fixtures", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


require(fitz.VersionBind == "1.26.6", "Pinned PyMuPDF1.26.6 is required")
require(pikepdf.__version__ == "10.17.0", "Pinned pikepdf10.17.0 is required")
require(pikepdf.__libqpdf_version__ == "12.4.2", "Pinned libqpdf12.4.2 is required")
gs = Path(shutil.which("gs") or "missing")
require(gs.is_file(), "Explicit development Ghostscript is required")
gs_version = subprocess.check_output([str(gs), "--version"], text=True).strip()
require(gs_version == "10.05.1", "Pinned Ghostscript10.05.1 is required")
names = ("original", "linearized")
records = {}
for name in names:
    path = args.fixtures / (name + ".pdf")
    with fitz.open(path) as pdf:
        require(len(pdf) == 3, "Expected three original pages")
        for index, page in enumerate(pdf):
            require(tuple(page.rect) == (0, 0, 120 + index * 10, 100), "Exact original page geometry differs")
            page.get_pixmap(matrix=fitz.Matrix(2, 2), alpha=False).save(args.output / f"{name}-reader1-{index}.png")
    command = [str(gs), "-q", "-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=png16m", "-r144",
               "-sOutputFile=" + str(args.output / (name + "-reader2-%d.png")), str(path)]
    completed = subprocess.run(command, text=True, capture_output=True, check=True)
    (args.output / (name + "-reader2.log")).write_text(completed.stdout + completed.stderr)
    records[name] = {"bytes": path.stat().st_size, "sha256": digest(path)}
comparisons = []
for reader, start in ((1, 0), (2, 1)):
    for index in range(start, start + 3):
        paths = [args.output / f"{name}-reader{reader}-{index}.png" for name in names]
        with Image.open(paths[0]) as before, Image.open(paths[1]) as after:
            require(before.size == after.size, "Reader page dimensions changed")
            require(ImageChops.difference(before.convert("RGB"), after.convert("RGB")).getbbox() is None,
                    "Linearized controlled page changed rendered pixels")
            colored = sum(1 for y in range(before.height) for x in range(before.width)
                          if min(before.getpixel((x, y))[:3]) < 200)
            require(colored > 1000, "Fixture must contain actual visible graphics")
        comparisons.append({"reader": reader, "page": index - start, "pixelEqual": True,
                            "originalPngSha256": digest(paths[0]), "linearizedPngSha256": digest(paths[1])})
with pikepdf.open(args.fixtures / "linearized.pdf") as pdf:
    producer_self_check = pdf.check_linearization()
require(producer_self_check, "The producer's own development hint check failed")
report = {"schema": 1, "fixtures": records, "independentControlledRenderingComparisons": comparisons,
          "producerTool": {"pikepdf": pikepdf.__version__, "libqpdf": pikepdf.__libqpdf_version__,
                           "ownHintCheck": producer_self_check, "independentHintValidation": False},
          "readers": {"PyMuPDF": fitz.VersionBind,
                      "Ghostscript": {"version": gs_version, "binarySha256": digest(gs.resolve())}},
          "completeIndependentHintQualification": False, "productionQualified": False}
(args.output / "independent-progressive.json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps({"comparisons": len(comparisons), "ownHintCheck": producer_self_check,
                  "independentHintQualification": False, "productionQualified": False}))
