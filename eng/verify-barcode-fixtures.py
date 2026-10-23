"""Independent controlled symbol/PDF observations; does not qualify engines or packages."""
import argparse
import base64
import hashlib
from importlib.metadata import version
import json
from pathlib import Path
import shutil
import subprocess
import fitz
from PIL import Image
import zxingcpp

parser = argparse.ArgumentParser()
parser.add_argument("fixtures", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


require(version("zxing-cpp") == "3.0.0", "Pinned zxing-cpp3.0.0 is required")
require(version("Pillow") == "13.0.0", "Pinned Pillow13.0.0 is required")
require(fitz.VersionBind == "1.26.6", "Pinned PyMuPDF1.26.6 is required")
gs = Path(shutil.which("gs") or "missing")
require(gs.is_file(), "An explicit development Ghostscript is required")
gs_version = subprocess.check_output([str(gs), "--version"], text=True).strip()
require(gs_version == "10.05.1", "Pinned Ghostscript10.05.1 is required")


def decode(image, name):
    expected = base64.b64decode((args.fixtures / (name + ".payload")).read_text(), validate=True)
    family, correction, qr_version, mask = (args.fixtures / (name + ".symbol")).read_text().split("\t")
    results = zxingcpp.read_barcodes(image, formats=(zxingcpp.BarcodeFormat.QRCode, zxingcpp.BarcodeFormat.Code128))
    require(len(results) == 1 and results[0].valid, name + ": exactly one valid symbol is required")
    result = results[0]
    require(result.text == expected.decode("utf-8", errors="strict"), name + ": decoded Unicode differs")
    require(result.bytes == expected, name + ": exact decoded UTF8 payload bytes differ")
    require(result.format == (zxingcpp.BarcodeFormat.QRCode if family == "QrCode" else zxingcpp.BarcodeFormat.Code128), name + ": symbol family differs")
    if family == "QrCode":
        level = {"Low": "L", "Medium": "M", "Quartile": "Q", "High": "H"}[correction]
        require(result.ec_level == level, name + ": QR error correction differs")
        require(int(result.extra["Version"]) == int(qr_version), name + ": QR version differs")
        require(int(result.extra["DataMask"]) == int(mask), name + ": QR mask differs")
    return {"family": family, "errorCorrection": correction, "qrVersion": int(qr_version), "mask": int(mask),
            "payloadSha256": hashlib.sha256(expected).hexdigest(), "payloadBytes": len(expected),
            "independentDecodedBytesEqual": True, "orientation": result.orientation}


symbols = []
paths = sorted(args.fixtures.glob("*.pgm"))
require(len(paths) == 200, "Expected160 version/ECC,36 near-capacity and4 document symbols")
for path in paths:
    with Image.open(path) as image:
        observation = decode(image, path.stem)
    symbols.append({"file": path.name, "sha256": sha(path), **observation})
documents = []
for path in sorted(args.fixtures.glob("*.pdf")):
    name = path.stem
    with fitz.open(path) as pdf:
        require(len(pdf) == 1, name + ": expected one page")
        page = pdf[0]
        require(not page.get_images(full=True), name + ": placement should retain actual vector content")
        require(len(page.get_drawings()) > 0, name + ": actual visible vector paths are required")
        first = args.output / (name + "-reader1.png")
        page.get_pixmap(matrix=fitz.Matrix(2, 2), alpha=False).save(first)
    second = args.output / (name + "-reader2.png")
    run = subprocess.run([str(gs), "-q", "-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=png16m", "-r144",
                          "-sOutputFile=" + str(second), str(path)], capture_output=True, text=True, check=True)
    (args.output / (name + "-reader2.log")).write_text(run.stdout + run.stderr)
    readers = []
    for reader, raster in (("PyMuPDF", first), ("Ghostscript", second)):
        with Image.open(raster) as image:
            observation = decode(image, name)
        readers.append({"reader": reader, "pngSha256": sha(raster), **observation})
    documents.append({"file": path.name, "sha256": sha(path), "vectorContentRetained": True, "readers": readers})
require(len(documents) == 4, "Expected four actual vector PDF documents")
report = {"schema": 1, "productionQualified": False, "sourceDecoder": "ZXing.Net0.16.11",
          "requestedSymbologies": ["QRCode", "Code128"],
          "independentDevelopmentDecoder": {"id": "zxing-cpp", "version": version("zxing-cpp"),
                                            "lineage": "Related decoder lineage; both decoders are independent of the original SDK encoder"},
          "readers": {"PyMuPDF": fitz.VersionBind, "Ghostscript": {"version": gs_version, "binarySha256": sha(gs.resolve())}},
          "Pillow": version("Pillow"), "symbols": symbols, "documents": documents,
          "fullCorpusRuntimePackageQualification": False}
(args.output / "independent-barcodes.json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps({"independentSymbols": len(symbols), "actualVectorPdfDocuments": len(documents),
                  "readerDecodes": len(documents) * 2, "productionQualified": False}))
