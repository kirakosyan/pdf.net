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


def spreadsheet_data(root):
    import openpyxl
    import zipfile
    import xml.etree.ElementTree as ET
    from odf.opendocument import load
    from odf.table import Table, TableCell
    from odf import teletype
    import importlib.metadata as metadata
    workbook = openpyxl.load_workbook(root / "table.xlsx", data_only=False)
    require(workbook.sheetnames == ["Data"], "XLSX sheet identity changed")
    sheet = workbook["Data"]
    require(sheet["A1"].value == "=SUM(A1:A2)" and sheet["A1"].data_type == "s", "Literal formula text became executable")
    require(str(sheet["A2"].value) == "12.5" and sheet["B2"].value is True, "XLSX scalar types changed")
    require(sheet["C2"].value.strftime("%Y-%m-%d") == "2026-10-20", "XLSX ISO date changed")
    require("A1:B1" in [str(r) for r in sheet.merged_cells.ranges], "XLSX merged rectangle changed")
    with zipfile.ZipFile(root / "table.xlsx") as package:
        for name in package.namelist():
            if name.startswith("xl/worksheets/"):
                xml = ET.fromstring(package.read(name))
                require(not xml.findall(".//{http://schemas.openxmlformats.org/spreadsheetml/2006/main}f"), "Executable XLSX formula emitted")
    ods = load(str(root / "table.ods"))
    tables = ods.spreadsheet.getElementsByType(Table)
    require(len(tables) == 1 and tables[0].getAttribute("name") == "Data", "ODS table identity changed")
    cells = tables[0].getElementsByType(TableCell)
    require(cells[0].getAttribute("valuetype") == "string" and teletype.extractText(cells[0]) == "=SUM(A1:A2)", "ODS literal text changed")
    require(cells[0].getAttribute("numbercolumnsspanned") == "2", "ODS merge changed")
    require(all(cell.getAttribute("formula") is None for cell in cells), "Executable ODS formula emitted")
    xml = ET.parse(root / "table.xml")
    ns = {"s": "urn:schemas-microsoft-com:office:spreadsheet"}
    first = xml.find(".//s:Data", ns)
    require(first.text == "=SUM(A1:A2)" and first.get("{" + ns["s"] + "}Type") == "String", "SpreadsheetML literal type changed")
    require(not any("{" + ns["s"] + "}Formula" in cell.attrib for cell in xml.findall(".//s:Cell", ns)), "Executable SpreadsheetML formula emitted")
    return {"validators": {"openpyxl": openpyxl.__version__, "odfpy": metadata.version("odfpy"), "xml": "Python ElementTree"},
            "typedValuesMergesAndLiteralFormulas": "passed", "productionQualified": False,
            "artifacts": [{"file": name, "sha256": digest(root / name)} for name in ["table.xlsx", "table.ods", "table.xml"]]}


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
    if (root / "table.xlsx").exists():
        report["spreadsheets"] = spreadsheet_data(root)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n")
    print("Independent SVG vectors/clips/styles and PDF round-trip fixture checks passed.")


if __name__ == "__main__":
    main()
