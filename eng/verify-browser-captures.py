"""Independent finite original-fixture observations; never complete browser qualification."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image, __version__ as pillow_version
from pypdf import PdfReader


def verify(directory):
    evidence = json.loads((directory / "captures.json").read_text(encoding="utf-8"))
    expected = {"nested-rotation": ((20, 40), (0, 255, 0)), "offscreen-vh": ((80, 72), (255, 0, 0)), "authenticated": ((30, 20), (255, 0, 255))}
    records = []
    for name, (size, color) in expected.items():
        path = directory / (name + ".png")
        with Image.open(path) as image:
            image.load()
            rgb = image.convert("RGB")
            assert image.size == size and rgb.getextrema() == tuple((v, v) for v in color), name
        records.append({"name": name, "size": size, "exactColor": color, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    with Image.open(directory / "ancestor-overflow.png") as image:
        rgb = image.convert("RGB")
        assert rgb.size == (60, 40)
        assert all(rgb.getpixel((x, y)) == ((0, 0, 255) if x < 30 and y < 30 else (255, 255, 255)) for y in range(40) for x in range(60))
    literal = next(r for r in evidence["captures"] if r["name"] == "literal-block")
    assert "Original literal block" in literal["markup"]
    assert len(evidence["errors"]) == 8
    assert evidence["authentication"] == {"MainRequests": 1, "StyleRequests": 1, "SameContext": True}
    reader = PdfReader(directory / "original.pdf")
    text = "\n".join(p.extract_text() for p in reader.pages)
    assert "ABC" in text and "Original browser source fixture" in text
    return {"purpose": "independent-png-pixels-and-pdf-text-source-observations", "productionQualified": False, "pillowVersion": pillow_version, "pixels": records, "ancestorClippingExact": True, "pdfPages": len(reader.pages), "errors": evidence["errors"]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    args.output.write_text(json.dumps(verify(args.directory), indent=2) + "\n", encoding="utf-8")
    print("Independent source PNG/PDF observations passed; full qualification remains pending.")
