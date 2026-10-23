"""Independent controlled EPS rendering, never arbitrary document execution/host qualification."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
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
            require(previous.read_bytes() == raw, "Prior evidence archive differs")
        else:
            previous.write_bytes(raw)
        path.unlink()


def pixels(path):
    with fitz.open(path) as pdf:
        require(pdf.page_count == 1, "Expected one controlled vector page")
        frame = pdf[0].get_pixmap(matrix=fitz.Matrix(96 / 72, 96 / 72), alpha=False)
        return Image.frombytes("RGB", (frame.width, frame.height), frame.samples)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("fixture_directory", type=Path)
    parser.add_argument("report_directory", type=Path)
    parser.add_argument("--expected-version", default="10.05.1")
    args = parser.parse_args()
    executable = shutil.which("gs")
    require(executable is not None, "Explicitly provisioned Ghostscript is required for this development check")
    version = subprocess.run([executable, "--version"], capture_output=True, text=True, check=True).stdout.strip()
    require(version == args.expected_version, "The actual analysis renderer version differs")
    root = args.fixture_directory / "format-fixtures"
    args.report_directory.mkdir(parents=True, exist_ok=True)
    report = args.report_directory / "independent-eps.json"
    archive(report)
    original, roundtrip = pixels(root / "eps-original.pdf"), pixels(root / "eps-roundtrip.pdf")
    require(original.size == roundtrip.size, "EPS round-trip page geometry changed")
    equal = ImageChops.difference(original, roundtrip).getbbox() is None
    require(equal, "Managed EPS round trip changed controlled vector/clip/style pixels")
    cases = []
    for eps_name, baseline in [("vectors.eps", "eps-original.pdf"), ("shifted-source.eps", "shifted-eps.pdf"), ("stroke-adjust-default.eps", "stroke-adjust-default.pdf"), ("stroke-adjust-false.eps", "stroke-adjust-false.pdf")]:
        native_eps = args.report_directory / (eps_name + ".ghostscript.png")
        native_pdf = args.report_directory / (baseline + ".ghostscript.png")
        archive(native_eps)
        archive(native_pdf)
        for source, target, crop in [(root / eps_name, native_eps, "-dEPSCrop"), (root / baseline, native_pdf, "-dUseCropBox")]:
            rendered_pixels = subprocess.run([executable, "-q", "-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=png16m", "-r96", crop, "-sOutputFile=" + str(target.resolve()), str(source.resolve())], capture_output=True, text=True, timeout=30)
            require(rendered_pixels.returncode == 0 and target.exists(), "The independent native raster check failed")
        left, right = Image.open(native_eps).convert("RGB"), Image.open(native_pdf).convert("RGB")
        native_equal = left.size == right.size and ImageChops.difference(left, right).getbbox() is None
        require(native_equal, f"Native EPS/PDF controlled raster pixels differ: {eps_name}")
        rendered = args.report_directory / (eps_name + ".ghostscript.pdf")
        archive(rendered)
        arguments = [executable, "-q", "-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=pdfwrite", "-dEPSCrop", "-dAutoRotatePages=/None", "-sOutputFile=" + str(rendered.resolve()), str((root / eps_name).resolve())]
        result = subprocess.run(arguments, capture_output=True, text=True, timeout=30)
        require(result.returncode == 0 and rendered.exists(), "Independent renderer rejected controlled EPS output")
        expected, actual = pixels(root / baseline), pixels(rendered)
        require(expected.size == actual.size, "Independent EPS page geometry changed")
        difference = ImageChops.difference(expected, actual)
        metric = ImageStat.Stat(difference).mean
        if eps_name in ["vectors.eps", "shifted-source.eps"]:
            require(max(metric) <= 1.0, "Controlled EPS visual difference exceeded the declared source-fixture threshold")
        cases.append({"eps": eps_name, "epsSha256": digest(root / eps_name), "baseline": baseline, "baselineSha256": digest(root / baseline),
                      "nativeEpsPdfRasterPixelsIdentical": native_equal, "nativeEpsPngSha256": digest(native_eps), "nativePdfPngSha256": digest(native_pdf),
                      "independentPdf": rendered.name, "independentPdfSha256": digest(rendered), "meanAbsoluteRgbDifference": metric,
                      "secondaryMetricScope": "EPS-to-PDF rewrite then MuPDF rasterization; primary native EPS/PDF pixels must be identical. The two original vector cases additionally retain their prior mean-difference threshold1.0. These are controlled source fixtures, not complete production visual qualification."})
    evidence = {"schemaVersion": 1, "purpose": "controlled-static-eps-source-validation", "ghostscriptVersion": version,
                "ghostscriptExecutableSha256": digest(Path(executable)), "PyMuPDF": fitz.VersionBind, "managedRoundtripPixelsIdentical": equal,
                "managedRoundtripPdfSha256": digest(root / "eps-roundtrip.pdf"), "cases": cases,
                "isolationScope": "Development renderer sees only our controlled original fixtures; this does not qualify an SDK interpreter sandbox or engine deployment.", "productionQualified": False}
    report.write_text(json.dumps(evidence, indent=2) + "\n")
    print("Independent static EPS rendering, geometry and managed round-trip fixtures passed.")


if __name__ == "__main__":
    main()
