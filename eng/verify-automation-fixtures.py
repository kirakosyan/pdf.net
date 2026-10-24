"""Independent mapped source/page-order observations for original automation outputs."""
import argparse
import hashlib
import json
from pathlib import Path
import fitz


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


parser = argparse.ArgumentParser()
parser.add_argument("directory", type=Path)
parser.add_argument("--output", required=True, type=Path)
parser.add_argument("--windows-matrix", action="store_true")
args = parser.parse_args()
require(fitz.VersionBind == "1.26.6", "Pinned PyMuPDF1.26.6 is required")
paths = sorted(args.directory.rglob("*reordered.pdf"))
if args.windows_matrix:
    manifest = json.loads((args.directory / "windows-consumers.json").read_text(encoding="utf-8-sig"))
    require(manifest["asset"] == "net472" and manifest["hostArchitecture"] == "X64" and len(manifest["consumers"]) == 4, "Actual Windows consumer manifest is required")
    require(all(row["passed"] and ".NET Framework" in row["identity"] for row in manifest["consumers"]), "Every actual consumer must pass")
    expected = {Path(row["output"]).name + ":" + row["architecture"].lower(): row["outputSha256"] for row in manifest["consumers"]}
    require(all(expected[path.name + ":" + path.parent.name] == hashlib.sha256(path.read_bytes()).hexdigest() for path in paths), "Consumer output hashes differ from actual manifest")
require(len(paths) == (4 if args.windows_matrix else 1), "Expected exact actual consumer output count")
rows = []
for path in paths:
    document = fitz.open(path)
    require(len(document) == 2, str(path) + ": page count differs")
    texts = [page.get_text("text", sort=False).strip() for page in document]
    require(texts == ["Second automation page", "Original automation Ω"], str(path) + ": exact Unicode/source page order differs")
    rows.append({"path": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "pages": 2, "unicodeAndOrderChecked": True})
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps({"reader": "PyMuPDF", "version": fitz.VersionBind, "outputs": rows, "actualComActivationObserved": args.windows_matrix, "productionQualified": False}, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"outputs": len(rows), "independentTextAndOrderPassed": True, "actualComActivationObserved": args.windows_matrix}))
