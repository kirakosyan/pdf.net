"""Generate original three-page development fixtures with an explicit analysis tool.

Run using a task-local environment containing pikepdf10.17.0/libqpdf12.4.2.
The producer's own linearization check is not independent qualification.
"""
import argparse
import hashlib
import json
from pathlib import Path
import pikepdf

parser = argparse.ArgumentParser()
parser.add_argument("output", type=Path)
args = parser.parse_args()
if pikepdf.__version__ != "10.17.0" or pikepdf.__libqpdf_version__ != "12.4.2":
    raise RuntimeError("The exact recorded development-tool versions are required")
args.output.mkdir(parents=True, exist_ok=True)
pdf = pikepdf.Pdf.new()
for index in range(3):
    page = pdf.add_blank_page(page_size=(120 + index * 10, 100))
    page.Contents = pdf.make_stream(f"{index / 2} 0 0 rg 10 10 50 40 re f\n".encode("ascii"))
pdf.save(args.output / "original.pdf", static_id=True, compress_streams=False)
pdf.save(args.output / "linearized.pdf", linearize=True, static_id=True,
         compress_streams=False, object_stream_mode=pikepdf.ObjectStreamMode.disable)
print(json.dumps({path.name: {"bytes": path.stat().st_size,
                             "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
                  for path in sorted(args.output.glob("*.pdf"))}, indent=2))
