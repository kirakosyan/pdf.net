"""Independent fidelity checks for a pinned, original source corpus.

Analysis-only dependencies: pypdf, PyMuPDF, Pillow and an explicit Poppler binary.
Neither expected semantics nor original PDFs are generated from SDK output.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import platform
import subprocess


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def ssim(left, right, width, height):
    """Mean 8x8-window SSIM on each RGB channel; population moments, L=255.

    Edge windows use their actual sample count. Exact changed-pixel checks run
    alongside this metric so a small omission cannot disappear in an average.
    """
    require(width > 0 and height > 0 and len(left) == len(right) == width*height*3, "Raster dimensions differ")
    if left == right:
        return 1.0
    scores = []
    c1, c2 = (0.01*255)**2, (0.03*255)**2
    for y in range(0, height, 8):
        for x in range(0, width, 8):
            for channel in range(3):
                indices = [(row*width+col)*3+channel for row in range(y, min(y+8, height)) for col in range(x, min(x+8, width))]
                a, b = [left[i] for i in indices], [right[i] for i in indices]
                am, bm = sum(a)/len(a), sum(b)/len(b)
                av = sum((v-am)**2 for v in a)/len(a)
                bv = sum((v-bm)**2 for v in b)/len(b)
                covariance = sum((u-am)*(v-bm) for u,v in zip(a,b))/len(a)
                scores.append(((2*am*bm+c1)*(2*covariance+c2))/((am*am+bm*bm+c1)*(av+bv+c2)))
    return sum(scores)/len(scores)


def compare_pixels(left, right, width, height, contract):
    # No mask or tolerance exception is implemented in this first corpus. A
    # manifest edit cannot silently turn missing pixels into accepted output.
    require(contract == dict(dpi=144, minimumSsim=1.0, maximumChangedPixels=0, masks=[], toleranceReview=None), "Unreviewed/unsupported visual tolerance change")
    score = ssim(left, right, width, height)
    changed = sum(left[i:i+3] != right[i:i+3] for i in range(0, len(left), 3))
    require(math.isfinite(score) and score >= .995 and changed == 0, f"Missing/changed visual content: SSIM={score}, pixels={changed}")
    return dict(ssim=score, changedPixels=changed, width=width, height=height)


def load_manifest(corpus):
    path = corpus / "manifest.json"
    manifest = json.loads(path.read_text(encoding="utf-8"))
    require(manifest.get("schemaVersion") == 1 and manifest.get("corpusVersion") == "original-fidelity-1", "Unknown corpus contract")
    entries = manifest["fixtures"]
    require(sorted(item["id"] for item in entries) == ["forms", "glyphs", "graphics", "structure"], "Incomplete/duplicate corpus")
    for item in entries:
        require(item["file"] == item["id"] + ".pdf", "Noncanonical fixture path")
        path = corpus / item["file"]
        require(path.stat().st_size <= 1048576 and sha(path) == item["sha256"], "Changed fixture bytes")
        require(item["license"] == "MIT" and bool(item["provenance"]), "Missing fixture rights/provenance")
    return manifest


def semantics(path, expected):
    import pypdf
    import pymupdf
    reader = pypdf.PdfReader(path, strict=True)
    require(len(reader.pages) == expected["pages"], "Page count changed")
    texts = [page.extract_text().strip() for page in reader.pages]
    require(texts == expected["text"], "Logical Unicode content changed")
    fields = reader.get_fields() or {}
    values = {name: str(field.get("/V", "")) for name, field in fields.items()}
    require(values == expected["formValues"], "Form field/value changed")
    require(any(field.get("/FT") == "/Sig" and bool(field.get("/V")) for field in fields.values()) == expected["signed"], "Signature presence changed")
    root = reader.trailer["/Root"]
    tree = root.get("/StructTreeRoot")
    roles = [str(child.get_object()["/S"]) for child in tree.get_object()["/K"]] if tree else []
    require(roles == expected["structureRoles"], "Structure roles changed")
    if tree:
        element = tree.get_object()["/K"][0].get_object()
        require(element["/K"] == 0 and element["/Pg"].indirect_reference == reader.pages[0].indirect_reference, "Structure page/MCID changed")
        parent = tree.get_object()["/ParentTree"].get_object()["/Nums"]
        require(parent[0] == 0 and parent[1][0].get_object() == element, "Parent-tree binding changed")
        marked = [(str(operands[0]), int(operands[1]["/MCID"])) for operands, operator in reader.pages[0].get_contents().operations if operator == b"BDC"]
        require(marked == [("/P",0)] and reader.pages[0]["/StructParents"] == 0, "Marked-content binding changed")
    for page in reader.pages:
        require([float(v) for v in page.mediabox] == expected["mediaBox"] and page.rotation == expected["rotation"], "Page geometry changed")
    with pymupdf.open(path) as document:
        require(len(document) == expected["pages"], "Second reader page count changed")
        words = []
        for page in document:
            require(page.get_text().strip() == expected["displayText"][page.number], "Second reader Unicode changed")
            words.append([list(word[:5]) for word in page.get_text("words")])
        require([word[4] for word in words[0]] == expected["displayText"][0].split("\n"), "Missing/extra logical word")
        # Original Type3 font advances 500 units per glyph at16pt; the baseline
        # and advance are authored expectations, not measured SDK output.
        require(abs(words[0][0][0]-expected["textOrigin"][0]) <= .5 and abs(words[0][0][2]-words[0][0][0]-expected["textAdvance"]) <= .5, "Word geometry changed")
    return dict(text=texts, forms=values, structureRoles=roles, words=words)


def run(args):
    import PIL
    from PIL import Image
    import pypdf
    import pymupdf
    corpus, candidates, output = args.corpus.resolve(), args.candidates.resolve(), args.output.resolve()
    manifest = load_manifest(corpus)
    output.mkdir(parents=True, exist_ok=False)
    consumer = json.loads((candidates / "consumer.json").read_text(encoding="utf-8"))
    require(sorted(x["file"] for x in consumer["records"]) == sorted(x["file"] for x in manifest["fixtures"]), "Incomplete consumer output inventory")
    records = []
    for entry in manifest["fixtures"]:
        original, candidate = corpus / entry["file"], candidates / entry["file"]
        record = next(x for x in consumer["records"] if x["file"] == entry["file"])
        require(record["inputSha256"] == sha(original) and record["outputSha256"] == sha(candidate), "Consumer artifact hash mismatch")
        reference_semantics = semantics(original, entry["expected"])
        actual_semantics = semantics(candidate, entry["expected"])
        for a, b in zip(reference_semantics["words"][0], actual_semantics["words"][0]):
            require(all(abs(x-y) <= .5 for x,y in zip(a[:4],b[:4])), "Word-box drift exceeds0.5pt")
        rasters = {}
        for renderer in ("mupdf", "poppler"):
            images = []
            for label, source in (("reference", original), ("candidate", candidate)):
                destination = output / (entry["id"] + "-" + renderer + "-" + label + ".png")
                if renderer == "mupdf":
                    with pymupdf.open(source) as document:
                        pixmap = document[0].get_pixmap(matrix=pymupdf.Matrix(2,2), colorspace=pymupdf.csRGB, alpha=False)
                        pixmap.save(destination)
                else:
                    result = subprocess.run([str(args.poppler.resolve()), "-f", "1", "-singlefile", "-r", "144", "-png", str(source), str(destination.with_suffix(""))], capture_output=True, timeout=30, check=True)
                    (output / (destination.stem + ".log")).write_bytes(result.stdout + result.stderr)
                with Image.open(destination) as image:
                    require(image.size == (288,200), "Unexpected raster dimensions")
                    images.append(image.convert("RGB").tobytes())
            rasters[renderer] = compare_pixels(*images, 288, 200, entry["visual"])
        records.append(dict(id=entry["id"], inputSha256=sha(original), outputSha256=sha(candidate), semantics=actual_semantics, rasters=rasters))
    result = dict(schemaVersion=1, purpose="source-independent-fidelity", corpusVersion=manifest["corpusVersion"], corpusManifestSha256=sha(corpus / "manifest.json"), consumerReportSha256=sha(candidates / "consumer.json"), host=platform.platform(), tools=dict(pypdf=pypdf.__version__, mupdf=pymupdf.VersionBind, pillow=PIL.__version__, popplerSha256=sha(args.poppler), popplerVersion=subprocess.run([str(args.poppler.resolve()), "-v"], capture_output=True, text=True, check=True).stderr.strip()), records=records, productionQualified=False, pending=manifest["pending"])
    (output / "report.json").write_text(json.dumps(result, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    print(f"{len(records)} fixtures passed two-reader semantics and two-renderer exact pixels")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("corpus", "candidates", "output", "poppler"):
        parser.add_argument("--"+name, type=Path, required=True)
    run(parser.parse_args())
