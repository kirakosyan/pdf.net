"""Independent pinned GhostXPS checks on controlled original XPS/OpenXPS fixtures."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
import zipfile
import fitz
from PIL import Image, ImageChops, ImageStat


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def archive(path):
    if path.exists():
        raw = path.read_bytes()
        previous = path.with_name(path.name + ".previous-" + hashlib.sha256(raw).hexdigest())
        if previous.exists():
            require(previous.read_bytes() == raw, "Evidence archive differs")
        else:
            previous.write_bytes(raw)
        path.unlink()


def pixels(page):
    value = page.get_pixmap(matrix=fitz.Matrix(96 / 72, 96 / 72), alpha=False)
    return Image.frombytes("RGB", (value.width, value.height), value.samples)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("fixture_directory", type=Path)
    parser.add_argument("report_directory", type=Path)
    parser.add_argument("--reader", type=Path, required=True)
    args = parser.parse_args()
    root = args.fixture_directory / "format-fixtures"
    args.report_directory.mkdir(parents=True, exist_ok=True)
    report = args.report_directory / "independent-xps.json"
    archive(report)
    identity = subprocess.run([str(args.reader), "-h"], capture_output=True, text=True, timeout=30)
    help_text = identity.stdout + identity.stderr
    require("Version: 10.05.1" in help_text and "Languages: PJL XPS" in help_text, "The actual version/language inventory is not the pinned XPS-only reader")
    cases = []
    for name, namespace in [("raster-pages.xps", "http://schemas.microsoft.com/xps/2005/06"), ("raster-pages.oxps", "http://schemas.openxps.org/oxps/v1.0")]:
        source = root / name
        with zipfile.ZipFile(source) as package:
            require(package.testzip() is None, "Independent package CRC check failed")
            document = ET.fromstring(package.read("Documents/1/FixedDocument.fdoc"))
            require(document.tag == "{" + namespace + "}FixedDocument", "Fixed-layout namespace changed")
            require([page.get("Source") for page in document] == ["Pages/1.fpage", "Pages/2.fpage", "Pages/3.fpage"], "Fixed page order changed")
            for index in range(3):
                image = Image.open(io.BytesIO(package.read(f"Documents/1/Resources/Images/{index + 1}.png")))
                require("dpi" not in image.info, "PNG density was not normalized for fixed-page image space")
                require(image.size == ((21, 11) if index == 1 else (11, 21)), "Fixed-page sample dimensions changed")
                require(image.getpixel((0, 0)) == ((0, 0, 0, 255) if index == 1 else (40, 0, 0, 255)), "Fixed-page raster samples/order changed")
        native = args.report_directory / (name + ".ghostxps.pdf")
        log = args.report_directory / (name + ".ghostxps.log")
        archive(native)
        archive(log)
        command = [str(args.reader), "-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=pdfwrite", "-dAutoFilterColorImages=false", "-sColorImageFilter=FlateEncode", "-dAutoFilterGrayImages=false", "-sGrayImageFilter=FlateEncode", "-sOutputFile=" + str(native.resolve()), str(source.resolve())]
        process = subprocess.run(command, capture_output=True, text=True, timeout=30)
        log.write_text(process.stdout + process.stderr)
        require(process.returncode == 0 and native.exists(), f"Independent reader rejected {name}")
        comparisons = []
        with fitz.open(root / (name + ".pdf")) as managed, fitz.open(native) as independent:
            require(managed.page_count == independent.page_count == 3, "Independent fixed-page count changed")
            for index in range(3):
                a, b = managed[index], independent[index]
                require(abs(a.rect.width - b.rect.width) < 1e-4 and abs(a.rect.height - b.rect.height) < 1e-4, "Independent fixed-page physical geometry changed")
                left, right = pixels(a), pixels(b)
                require(left.size == right.size, "Independent fixed-page pixel geometry changed")
                metric = ImageStat.Stat(ImageChops.difference(left, right)).mean
                require(max(metric) <= 1.0, "Original fixed-page pattern difference exceeds the declared source-fixture threshold")
                comparisons.append({"page": index, "points": [a.rect.width, a.rect.height], "meanAbsoluteRgbDifference": metric, "threshold": 1.0})
        # A renderer must actually recognize the requested format; a generic ZIP/image fallback is not validation.
        with fitz.open(source) as alternative:
            observed = {"detectedFormat": alternative.metadata.get("format"), "pagePoints": [[page.rect.width, page.rect.height] for page in alternative]}
        cases.append({"artifact": name, "sha256": digest(source), "managedPdfSha256": digest(root / (name + ".pdf")), "independentPdfSha256": digest(native), "readerExitCode": process.returncode,
                      "pageComparisons": comparisons, "alternativeReaderObservation": observed,
                      "alternativeReaderScope": "Only actual XPS format detection can qualify this alternative reader. Separate retained fractional-unit observations show truncation; generic ZIP detection for OpenXPS is not passing format evidence."})
    evidence = {"schemaVersion": 1, "purpose": "controlled-original-fixed-layout-source-validation", "reader": "GhostXPS", "version": "10.05.1", "readerSha256": digest(args.reader),
                "readerSourceCommit": "7f64bc848f6b67ae204182e6e088eec110061490", "PyMuPDF": fitz.VersionBind, "cases": cases,
                "scope": "Controlled raster patterns, physical geometry and resource order only; no complete glyph/font/print-ticket/color, SDK native sandbox or package qualification.", "productionQualified": False}
    report.write_text(json.dumps(evidence, indent=2) + "\n")
    print("Independent GhostXPS XPS/OpenXPS fixed-layout fixture checks passed.")


if __name__ == "__main__":
    main()
