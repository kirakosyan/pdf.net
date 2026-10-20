"""Verify that optimized Python cannot bypass independent fixture checks.

Run after emitting the original queue fixtures. Requires Pillow and OpenSSL.
"""
import argparse
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from PIL import Image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("fixture_directory", type=Path)
    args = parser.parse_args()
    checker = Path(__file__).with_name("verify-queue-fixtures.py")
    with tempfile.TemporaryDirectory(prefix="fluent-pdf-checker-regression-") as directory:
        root = Path(directory)
        fixtures = root / "fixtures"
        shutil.copytree(args.fixture_directory / "raster-fixtures", fixtures / "raster-fixtures")
        shutil.copytree(args.fixture_directory / "signature-fixtures", fixtures / "signature-fixtures")
        for optimized in (False, True):
            command = [sys.executable] + (["-O"] if optimized else [])
            subprocess.run(command + [str(checker), str(fixtures), str(root / "valid.json")], check=True)
        for corruption in ("pixels", "dimensions", "dpi"):
            original = args.fixture_directory / "raster-fixtures" / "grid.png"
            with Image.open(original) as image:
                image.load()
                dpi = image.info["dpi"]
                if corruption == "pixels":
                    image.putpixel((0, 0), (255, 0, 0, 255))
                elif corruption == "dimensions":
                    image = image.crop((0, 0, 7, 6))
                else:
                    dpi = (96, 96)
                image.save(fixtures / "raster-fixtures" / "grid.png", dpi=dpi)
            report = root / f"invalid-{corruption}.json"
            result = subprocess.run([sys.executable, "-O", str(checker), str(fixtures), str(report)], capture_output=True)
            if result.returncode == 0 or report.exists():
                raise RuntimeError(f"Optimized checker accepted corrupted {corruption} fixture")
    print("Normal/optimized valid fixtures passed; optimized pixel/dimension/DPI corruptions rejected.")


if __name__ == "__main__":
    main()
