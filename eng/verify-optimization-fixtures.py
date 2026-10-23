"""Independent controlled optimization observations; not production qualification."""
import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path
import fitz
from PIL import Image, ImageChops


def require(value, message):
    if not value:
        raise ValueError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def raster(document):
    pix = document[0].get_pixmap(matrix=fitz.Matrix(1, 1), alpha=False)
    return Image.frombytes("RGB", (pix.width, pix.height), pix.samples)


parser = argparse.ArgumentParser()
parser.add_argument("fixture_directory", type=Path)
parser.add_argument("report_directory", type=Path)
args = parser.parse_args()
root = args.fixture_directory / "optimization-fixtures"
out = args.report_directory
out.mkdir(parents=True, exist_ok=True)
report = out / "independent-optimization.json"
if report.exists():
    previous = report.with_name(report.name + ".previous-" + sha(report))
    if not previous.exists():
        previous.write_bytes(report.read_bytes())
    report.unlink()
paths = {name: root / (name + ".pdf") for name in ["source", "lossless", "pruned", "downsampled"]}
documents = {name: fitz.open(path) for name, path in paths.items()}
for name, doc in documents.items():
    require(len(doc) == 1 and tuple(doc[0].rect) == (0, 0, 100, 100), name + ": page geometry changed")
    text = "".join(doc[0].get_text().split())
    require(text == "AA", name + ": controlled visible/logical glyph content changed: " + repr(text))
    require(doc.embfile_names() == ["note.xml"], name + ": attachment identity changed")
    expected = ("<original>Literal &amp; owned attachment.</original>\n" * 100).encode()
    require(doc.embfile_get("note.xml") == expected, name + ": decoded associated XML changed")
original = documents["source"]
for name in ["lossless", "pruned"]:
    doc = documents[name]
    for number in [5, 11, 24]:
        require(doc.xref_stream(number) == original.xref_stream(number), name + ": decoded image/mask/font bytes changed")
    require(ImageChops.difference(raster(original), raster(doc)).getbbox() is None, name + ": independent rendered content changed")
source_pixels = raster(original)
glyph_region = source_pixels.crop((0, 80, 30, 100))
require(any(all(channel < 20 for channel in glyph_region.getpixel((x, y))) for y in range(glyph_region.height) for x in range(glyph_region.width)), "Original embedded glyphs produced no visible dark coverage")
require(documents["downsampled"].xref_stream(5) == bytes([80, 100, 120]), "Downsampled image did not select declared sample centers")
require(documents["downsampled"].xref_stream(11) == bytes([255]), "Downsampled alpha changed")
require(paths["lossless"].stat().st_size < paths["source"].stat().st_size, "Controlled lossless output did not shrink")
gs = shutil.which("gs")
require(gs is not None, "Provision an independent offline PDF interpreter explicitly")
version = subprocess.check_output([gs, "--version"], text=True).strip()
for name, path in paths.items():
    command = [gs, "-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=png16m", "-r72", "-o", str(out / (name + "-%d.png")), str(path)]
    result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=60)
    (out / (name + ".reader.log")).write_text(result.stdout)
    require(result.returncode == 0, name + ": independent PDF interpreter failed")
for name in ["lossless", "pruned"]:
    with Image.open(out / "source-1.png") as first, Image.open(out / (name + "-1.png")) as second:
        require(first.size == second.size and ImageChops.difference(first.convert("RGB"), second.convert("RGB")).getbbox() is None, name + ": second independent render changed")
data = {
    "schemaVersion": 1,
    "purpose": "controlled-original-optimization-source-fixtures",
    "productionQualified": False,
    "standardsConformanceValidated": False,
    "readers": {"PyMuPDF": fitz.VersionBind, "offlineInterpreter": version},
    "losslessPixelsIdenticalPerReader": True,
    "prunedPixelsIdenticalPerReader": True,
    "visibleGlyphAndLogicalTextRetained": True,
    "decodedImageAlphaFontAndAttachmentBytesRetained": True,
    "downsampleCentersAndAlphaVerified": True,
    "artifacts": [{"file": path.name, "sha256": sha(path), "bytes": path.stat().st_size} for path in paths.values()],
    "limitations": ["Controlled original static fixtures only; no complete standards, adversarial, minimum-runtime, provider, performance or final-package qualification."]
}
report.write_text(json.dumps(data, indent=2) + "\n")
for doc in documents.values():
    doc.close()
print("Independent controlled optimization pixels, text, image/alpha/font and attachment checks passed.")
