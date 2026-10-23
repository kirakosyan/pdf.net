#!/usr/bin/env python3
"""Original standard-library PDF source for managed CLI conversion checks."""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
stream = b"0 0 1 rg 5 5 110 50 re f\n"
objects = [b"<< /Type /Catalog /Pages 2 0 R >>",
           b"<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
           b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 120 60] /Resources <<>> /Contents 4 0 R >>",
           b"<< /Length " + str(len(stream)).encode() + b" >>\nstream\n" + stream + b"endstream"]
data = bytearray(b"%PDF-1.7\n")
offsets = [0]
for index, value in enumerate(objects, 1):
    offsets.append(len(data))
    data.extend(str(index).encode() + b" 0 obj\n" + value + b"\nendobj\n")
xref = len(data)
data.extend(b"xref\n0 5\n0000000000 65535 f \n")
for offset in offsets[1:]:
    data.extend(f"{offset:010d} 00000 n \n".encode())
data.extend(b"trailer\n<< /Size 5 /Root 1 0 R >>\nstartxref\n" + str(xref).encode() + b"\n%%EOF\n")
(root / "tests/Fluent.Pdf.Contract.Tests/Fixtures/Cli/vector.pdf").write_bytes(data)
print("Generated original vector CLI input.")
