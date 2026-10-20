"""Independent checks of original source fixtures; not SDK production qualification.

Requires Pillow and OpenSSL provisioned by the caller. Run after the queue contract
cases emitted raster-fixtures/ and signature-fixtures/ in the test output folder.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile

import PIL
from PIL import Image


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("fixture_directory", type=Path)
    parser.add_argument("report", type=Path)
    args = parser.parse_args()
    raster_directory = args.fixture_directory / "raster-fixtures"
    rasters = []
    for name in ("grid.png", "grid.bmp", "grid.tiff"):
        path = raster_directory / name
        frames = []
        with Image.open(path) as image:
            for index in range(getattr(image, "n_frames", 1)):
                image.seek(index)
                image.load()
                rgba = image.convert("RGBA")
                page = 1 - index if name.endswith("tiff") else 0
                assert rgba.size == ((5, 4) if page == 1 else (8, 6))
                for y in range(rgba.height):
                    for x in range(rgba.width):
                        assert rgba.getpixel((x, y)) == (page * 40, y, x, 255)
                dpi = [float(value) for value in image.info["dpi"]]
                assert all(abs(value - 72) < 0.02 for value in dpi)
                frames.append({"frame": index, "size": list(image.size), "dpi": dpi,
                               "allPixelsMatchOriginalFormula": True})
        rasters.append({"file": name, "sha256": sha256(path), "frames": frames})

    signatures = args.fixture_directory / "signature-fixtures"
    token, certificate = signatures / "timestamp.der", signatures / "tsa.der"
    with tempfile.TemporaryDirectory(prefix="fluent-pdf-timestamp-check-") as directory:
        pem = Path(directory) / "tsa.pem"
        subprocess.run(["openssl", "x509", "-inform", "DER", "-in", str(certificate),
                        "-out", str(pem)], check=True, capture_output=True)
        command = ["openssl", "ts", "-verify", "-token_in", "-in", str(token),
                   "-digest", bytes(range(32)).hex(), "-untrusted", str(pem), "-CAfile", str(pem)]
        verification = subprocess.run(command, capture_output=True, check=True)
        assert b"Verification: OK" in verification.stdout + verification.stderr
        version = subprocess.run(["openssl", "version"], capture_output=True, check=True)
    report = {
        "schemaVersion": 1,
        "purpose": "independent-original-source-fixtures-not-production-qualification",
        "rasterValidator": {"name": "Pillow", "version": PIL.__version__, "results": rasters},
        "timestampValidator": {
            "name": version.stdout.decode().strip(), "verification": "OK",
            "tokenSha256": sha256(token), "publicCertificateSha256": sha256(certificate),
            "outputSha256": hashlib.sha256(verification.stdout + verification.stderr).hexdigest(),
            "scope": "Exact SHA256 imprint and token signature using an explicitly trusted self-issued fixture TSA certificate; no general PKIX, revocation or PAdES qualification.",
        },
        "productionQualified": False,
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n")
    print("Independent raster pixel/DPI/order and RFC3161 source-fixture checks passed.")


if __name__ == "__main__":
    main()
