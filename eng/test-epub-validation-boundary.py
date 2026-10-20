"""Actual negative checks for the development validator wrapper, including Python -O."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile


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
            require(previous.read_bytes() == raw, "Evidence archive changed")
        else:
            previous.write_bytes(raw)
        path.unlink()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("fixture_directory", type=Path)
    parser.add_argument("output_directory", type=Path)
    parser.add_argument("--lib-directory", required=True, type=Path)
    args = parser.parse_args()
    root = args.output_directory.resolve()
    root.mkdir(parents=True, exist_ok=True)
    archive(root / "negative-checks.json")
    fixtures = root / "invalid-book/format-fixtures"
    fixtures.mkdir(parents=True, exist_ok=True)
    source = args.fixture_directory / "format-fixtures"
    with zipfile.ZipFile(source / "fixed-book.epub") as original, zipfile.ZipFile(fixtures / "fixed-book.epub", "w") as changed:
        for item in original.infolist():
            changed.writestr(item, b"application/not-epub" if item.filename == "mimetype" else original.read(item))
    shutil.copyfile(source / "reflow-book.epub", fixtures / "reflow-book.epub")
    reports = root / "invalid-book-reports"
    reports.mkdir(parents=True, exist_ok=True)
    aggregate = reports / "independent-epub.json"
    # This labeled sentinel checks stale-pointer removal. It is not a validator result.
    sentinel = b'{"purpose":"stale-report-guard-fixture"}\n'
    archive(aggregate)
    aggregate.write_bytes(sentinel)
    wrapper = Path(__file__).parent / "verify-epub-fixtures.py"
    invalid = subprocess.run([sys.executable, "-O", str(wrapper), str(fixtures.parent), str(reports), "--lib-directory", str(args.lib_directory)], capture_output=True, text=True)
    require(invalid.returncode != 0 and "EPUBCheck rejected" in invalid.stderr, "Invalid EPUB did not fail the actual validator")
    require(not aggregate.exists(), "A stale passing aggregate survived failed verification")
    archived = reports / ("independent-epub.json.previous-" + hashlib.sha256(sentinel).hexdigest())
    require(archived.read_bytes() == sentinel, "Prior pointer was not retained exactly")
    actual = reports / "fixed-book.epub.epubcheck.json"
    require(actual.exists() and sum(json.loads(actual.read_text())["checker"][key] for key in ["nFatal", "nError"]) > 0, "No independent invalid-book error evidence")
    mismatched = root / "mismatched-validator"
    mismatched.mkdir(parents=True, exist_ok=True)
    (mismatched / "untrusted.jar").write_bytes(b"not-executed")
    wrong = subprocess.run([sys.executable, "-O", str(wrapper), str(args.fixture_directory), str(root / "mismatched-reports"), "--lib-directory", str(mismatched)], capture_output=True, text=True)
    require(wrong.returncode != 0 and "inventory differs" in wrong.stderr, "Different validator binaries were not rejected before execution")
    evidence = {"schemaVersion": 1, "purpose": "actual-independent-validator-negative-boundaries", "pythonOptimization": "-O",
                "wrapperSha256": digest(wrapper), "invalidBookSha256": digest(fixtures / "fixed-book.epub"), "invalidBookReportSha256": digest(actual),
                "invalidBookExitCode": invalid.returncode, "differentValidatorExitCode": wrong.returncode,
                "staleAggregateRemovedAndArchived": True, "productionQualified": False}
    (root / "negative-checks.json").write_text(json.dumps(evidence, indent=2) + "\n")
    print("Invalid EPUB, stale aggregate and mismatched validator checks rejected as expected under Python -O.")


if __name__ == "__main__":
    main()
