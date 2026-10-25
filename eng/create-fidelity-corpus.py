"""Original hand-authored PDF fixtures. No SDK or reference implementation is used.

Only writes a NEW directory. Expected semantics are declared here, never learned
from SDK output. PDF Type3 glyph outlines are original and require no host fonts.
"""
import hashlib
import json
from pathlib import Path
import sys


def stream(data, dictionary=b""):
    return b"<< /Length " + str(len(data)).encode() + b" " + dictionary + b" >>\nstream\n" + data + b"\nendstream"


def pdf(objects):
    data = bytearray(b"%PDF-1.7\n% Original MIT fidelity fixture\n")
    offsets = [0]
    for index, obj in enumerate(objects, 1):
        offsets.append(len(data))
        data.extend(str(index).encode() + b" 0 obj\n" + obj + b"\nendobj\n")
    xref = len(data)
    data.extend(f"xref\n0 {len(objects)+1}\n0000000000 65535 f \n".encode())
    for offset in offsets[1:]:
        data.extend(f"{offset:010d} 00000 n \n".encode())
    data.extend(f"trailer\n<< /Size {len(objects)+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n".encode())
    return bytes(data)


def create(directory):
    directory.mkdir(parents=True, exist_ok=False)
    fixtures = []
    glyphs = [b"500 0 0 0 500 700 d1 40 0 m 250 700 l 460 0 l 380 0 l 250 460 l 120 0 l f",
              b"500 0 0 0 500 700 d1 40 0 80 700 re 120 0 300 80 re 120 310 240 80 re 120 620 300 80 re 340 80 80 230 re 340 390 80 230 re f",
              b"500 0 0 0 500 700 d1 40 0 160 70 re 300 0 160 70 re 80 70 80 500 re 340 70 80 500 re 160 570 180 80 re f"]
    cmap = b"/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CIDSystemInfo << /Registry (Original) /Ordering (Fidelity) /Supplement 0 >> def /CMapName /Original def /CMapType 2 def 1 begincodespacerange <00> <FF> endcodespacerange 3 beginbfchar <41> <0041> <42> <0042> <43> <03A9> endbfchar endcmap CMapName currentdict /CMap defineresource pop end end"
    for kind in ("glyphs", "graphics", "structure", "forms"):
        text = b"BT /F1 16 Tf 1 0 0 1 12 70 Tm (ABC) Tj ET"
        if kind == "structure": text = b"/P << /MCID 0 >> BDC " + text + b" EMC"
        content = text + b"\nq 1 0 0 rg 12 12 40 20 re f Q\n"
        if kind == "graphics": content += b"q 0 0 1 RG 2 w 65 12 m 120 55 l S Q\n"
        catalog = b"<< /Type /Catalog /Pages 2 0 R"
        page = b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 144 100] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R"
        extras = []
        if kind == "structure":
            catalog += b" /MarkInfo << /Marked true >> /Lang (en) /StructTreeRoot 10 0 R"
            page += b" /StructParents 0"
            extras = [b"<< /Type /StructTreeRoot /K [11 0 R] /ParentTree 12 0 R >>", b"<< /Type /StructElem /S /P /P 10 0 R /Pg 3 0 R /K 0 >>", b"<< /Nums [0 [11 0 R]] >>"]
        if kind == "forms":
            catalog += b" /AcroForm << /Fields [10 0 R] /NeedAppearances false >>"
            page += b" /Annots [10 0 R]"
            extras = [b"<< /Type /Annot /Subtype /Widget /FT /Tx /T (OriginalField) /V (AB) /Rect [70 12 130 32] /P 3 0 R /F 4 /AP << /N 11 0 R >> >>", stream(b"BT /F1 12 Tf 1 0 0 1 2 4 Tm (AB) Tj ET", b"/Type /XObject /Subtype /Form /BBox [0 0 60 20] /Resources << /Font << /F1 5 0 R >> >>")]
        objects = [catalog + b" >>", b"<< /Type /Pages /Count 1 /Kids [3 0 R] >>", page + b" >>", stream(content), b"<< /Type /Font /Subtype /Type3 /FontBBox [0 0 500 700] /FontMatrix [0.001 0 0 0.001 0 0] /CharProcs << /gA 6 0 R /gB 7 0 R /gC 8 0 R >> /Encoding << /Type /Encoding /Differences [65 /gA /gB /gC] >> /FirstChar 65 /LastChar 67 /Widths [500 500 500] /Resources <<>> /ToUnicode 9 0 R >>", *(stream(g) for g in glyphs), stream(cmap), *extras]
        data = pdf(objects)
        (directory / (kind + ".pdf")).write_bytes(data)
        fixtures.append(dict(id=kind, file=kind + ".pdf", sha256=hashlib.sha256(data).hexdigest(), license="MIT", provenance="Original hand-authored PDF syntax and glyph outlines in eng/create-fidelity-corpus.py; no SDK output used", expected=dict(pages=1, text=["ABΩ"], displayText=["ABΩ\nAB" if kind == "forms" else "ABΩ"], mediaBox=[0,0,144,100], rotation=0, formValues={"OriginalField":"AB"} if kind == "forms" else {}, structureRoles=["/P"] if kind == "structure" else [], signed=False, textOrigin=[12,70], textAdvance=24), visual=dict(dpi=144, minimumSsim=1.0, maximumChangedPixels=0, masks=[], toleranceReview=None)))
    (directory / "manifest.json").write_text(json.dumps(dict(schemaVersion=1, corpusVersion="original-fidelity-1", productionQualified=False, fixtures=fixtures, pending=["Multilingual shaping and real document layouts", "All form/annotation/structure profiles", "Signed and historical-revision inputs", "Independent standards conformance and accessibility review", "Complete rendering/provider/platform corpus"]), ensure_ascii=False, indent=2)+"\n", encoding="utf-8")


if __name__ == "__main__":
    if len(sys.argv) != 2: raise SystemExit("<new-directory>")
    create(Path(sys.argv[1]))
