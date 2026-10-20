"""Independent original format-fixture checks; not production qualification.

Requires caller-provisioned CairoSVG, Pillow and PyMuPDF. No input resources are
fetched: only the original controlled generated SVG/PDF fixtures are rendered.
"""
import argparse
import hashlib
import json
from pathlib import Path
import io
import fitz
import cairosvg
from PIL import Image, ImageChops, ImageStat
import PIL


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def pdf_pixels(path):
    with fitz.open(path) as document:
        require(document.page_count == 1, f"{path.name}: expected one original vector page")
        pixel = document[0].get_pixmap(matrix=fitz.Matrix(96 / 72, 96 / 72), alpha=False)
        return Image.frombytes("RGB", (pixel.width, pixel.height), pixel.samples)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("fixture_directory", type=Path)
    parser.add_argument("report", type=Path)
    args = parser.parse_args()
    if args.report.exists():
        previous = args.report.read_bytes()
        archived = args.report.with_name(args.report.stem + "-previous-" + hashlib.sha256(previous).hexdigest() + args.report.suffix)
        if archived.exists():
            require(archived.read_bytes() == previous, "Prior evidence archive differs")
        else:
            archived.write_bytes(previous)
        args.report.unlink()
    root = args.fixture_directory / "format-fixtures"
    paths = {name: root / name for name in ("vectors-source.svg", "vectors-export.svg", "vectors.pdf", "vectors-roundtrip.pdf")}
    source = Image.open(io.BytesIO(cairosvg.svg2png(bytestring=paths["vectors-source.svg"].read_bytes(), background_color="white"))).convert("RGB")
    exported = Image.open(io.BytesIO(cairosvg.svg2png(bytestring=paths["vectors-export.svg"].read_bytes(), background_color="white", output_width=200, output_height=120))).convert("RGB")
    original_pdf, roundtrip_pdf = pdf_pixels(paths["vectors.pdf"]), pdf_pixels(paths["vectors-roundtrip.pdf"])
    require(source.size == (200, 120) and exported.size == source.size and original_pdf.size == source.size and roundtrip_pdf.size == source.size, "Original physical SVG/PDF dimensions differ")
    svg_equal = ImageChops.difference(source, exported).getbbox() is None
    pdf_equal = ImageChops.difference(original_pdf, roundtrip_pdf).getbbox() is None
    require(svg_equal, "SVG export changed independently rendered original vectors/clips/styles")
    require(pdf_equal, "SVG round trip changed independently rendered PDF pixels")
    cross_engine_error = ImageStat.Stat(ImageChops.difference(source, original_pdf)).mean
    report = {"schemaVersion": 1, "purpose": "original-format-source-fixtures-not-production-qualification",
              "validators": {"CairoSVG": cairosvg.__version__, "PyMuPDF": fitz.VersionBind, "Pillow": PIL.__version__},
              "sourceSvgAndExportPixelsIdentical": svg_equal, "pdfRoundtripPixelsIdentical": pdf_equal,
              "crossRendererMeanAbsoluteRgbDifference": cross_engine_error,
              "crossRendererMetricScope": "Reported raw antialiasing difference; this is not a production visual-threshold qualification.",
              "artifacts": [{"file": name, "sha256": digest(path)} for name, path in paths.items()], "productionQualified": False}
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n")
    print("Independent SVG vectors/clips/styles and PDF round-trip fixture checks passed.")


if __name__ == "__main__":
    main()
