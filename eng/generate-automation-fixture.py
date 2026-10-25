"""Generate an original deterministic PDF for portable and actual COM consumers."""
from pathlib import Path

objects = {}
def obj(number, body):
    objects[number] = body.encode("ascii")

def stream(number, body):
    encoded = body.encode("ascii")
    objects[number] = b"<< /Length " + str(len(encoded)).encode("ascii") + b" >>\nstream\n" + encoded + b"\nendstream"

obj(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm 11 0 R >>")
obj(2, "<< /Type /Pages /Kids [3 0 R 8 0 R] /Count 2 >>")
for page, content in [(3, 7), (8, 9)]:
    obj(page, f"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 200] /Resources << /Font << /F1 4 0 R >> >> /Contents {content} 0 R >>")
obj(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /FirstChar 1 /LastChar 126 /Widths [" + " ".join(["500"] * 126) + "] /ToUnicode 5 0 R /FontDescriptor 6 0 R >>")
stream(5, "begincmap 1 begincodespacerange <00> <ff> endcodespacerange 1 beginbfrange <20> <7e> <0020> endbfrange 1 beginbfchar <01> <03A9> endbfchar endcmap")
obj(6, "<< /Type /FontDescriptor /FontName /Helvetica /Flags 32 /FontBBox [0 -200 1000 800] /Ascent 800 /Descent -200 /CapHeight 700 /ItalicAngle 0 /StemV 80 >>")
stream(7, "BT /F1 12 Tf 1 0 0 1 20 100 Tm (Original automation \\001) Tj ET")
stream(9, "BT /F1 12 Tf 1 0 0 1 20 100 Tm (Second automation page) Tj ET")
obj(10, "<< /T (customer) /Kids [12 0 R] >>")
obj(11, "<< /Fields [10 0 R] >>")
obj(12, "<< /Parent 10 0 R /FT /Tx /T (Name) /V (Original value) >>")
data = bytearray(b"%PDF-1.7\n")
offsets = [0]
for number in range(1, 13):
    offsets.append(len(data))
    data.extend(f"{number} 0 obj\n".encode("ascii") + objects[number] + b"\nendobj\n")
xref = len(data)
data.extend(b"xref\n0 13\n0000000000 65535 f \n")
for offset in offsets[1:]:
    data.extend(f"{offset:010d} 00000 n \n".encode("ascii"))
data.extend(f"trailer\n<< /Size 13 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n".encode("ascii"))
target = Path(__file__).resolve().parents[1] / "tests/Fluent.Pdf.Contract.Tests/Fixtures/Automation/original.pdf"
target.parent.mkdir(parents=True, exist_ok=True)
target.write_bytes(data)
print(f"Wrote original automation fixture: {len(data)} bytes")
