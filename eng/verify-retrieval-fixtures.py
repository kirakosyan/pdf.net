"""Independent source-text, byte-boundary and cosine observations on original fixtures.

The embedding responses are deliberately recorded feature vectors. They do not
measure real model relevance or qualify a remote embedding service.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path

import fitz


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


parser = argparse.ArgumentParser()
parser.add_argument("fixtures", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
require(fitz.VersionBind == "1.26.6", "Pinned PyMuPDF1.26.6 is required")
paths = sorted(args.fixtures.glob("*.json"))
require(len(paths) == 11, "Expected table, five Unicode, three retrieval and two consumer groups")
records = []
labeled = {"apples": 0, "ocean": 1, "invoice": 2}
for path in paths:
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    pdf_path = path.with_suffix(".pdf")
    require(data["actualModelInferenceQualified"] is False, path.name + ": recorded fixture labeling required")
    require(hashlib.sha256(pdf_path.read_bytes()).hexdigest() == data["sourceSha256"], path.name + ": source revision differs")
    page_chunks = {}
    for chunk in data["chunks"]:
        encoded = chunk["text"].encode("utf-8", errors="strict")
        require(len(encoded) == chunk["bytes"], path.name + ": UTF8 count differs")
        require(hashlib.sha256(encoded).hexdigest() == chunk["sha256"], path.name + ": text digest differs")
        page_chunks.setdefault(chunk["page"], []).append(chunk)
    pdf = fitz.open(pdf_path)
    for page, chunks in page_chunks.items():
        offset = 0
        for chunk in chunks:
            require(chunk["offset"] == offset, path.name + ": raw source gap/overlap")
            offset += len(chunk["text"].encode("utf-16-le")) // 2
        raw = "".join(chunk["text"] for chunk in chunks)
        independent = pdf[page].get_text("text", sort=False)
        # The raw profile inserts no geometric whitespace. The independent reader
        # does; compare mapped non-whitespace glyphs separately from exact offsets.
        require("".join(raw.split()) == "".join(independent.split()), path.name + ": independent mapped source text differs")
    if path.stem.startswith("unicode-"):
        limit = int(path.stem.removeprefix("unicode-"))
        require("".join(chunk["text"] for chunk in data["chunks"]) == "ab😀\r\nfi cd", path.name + ": labeled Unicode source differs")
        require(all(chunk["bytes"] <= limit and not chunk["text"].endswith("\r") and not chunk["text"].startswith("\n") for chunk in data["chunks"]), path.name + ": Unicode/CRLF byte boundary differs")
    if path.stem == "tables":
        chunks = page_chunks[0]
        require(chunks[0]["table"] is None and chunks[-1]["table"] is None, "Prose must remain outside the inferred table")
        require(all(chunk["table"] == 0 for chunk in chunks[1:-1]), "Table source runs must retain one inferred identity")
    if path.stem in labeled:
        require(data["dimensions"] == 3 and data["model"] == "recorded-embedding-v1", path.name + ": exact embedding metadata differs")
        query = [0.1, 0.1, 0.1]
        query[labeled[path.stem]] = 1.0
        expected = []
        for i, vector in enumerate(data["vectors"]):
            require(len(vector) == 3, path.name + ": dimension differs")
            score = math.fsum(a * b for a, b in zip(query, vector)) / math.hypot(*query) / math.hypot(*vector)
            expected.append((i, score))
        expected.sort(key=lambda item: (-item[1], item[0]))
        require([item["index"] for item in data["matches"]] == [item[0] for item in expected], path.name + ": independent cosine ranking differs")
        require(all(abs(item["score"] - score) < 1e-14 for item, (_, score) in zip(data["matches"], expected)), path.name + ": independent score differs")
        require(data["chunks"][data["matches"][0]["index"]]["page"] == labeled[path.stem], path.name + ": hand-labeled top-one page differs")
    records.append({"fixture": path.stem, "sourceSha256": data["sourceSha256"], "captureSha256": data["captureSha256"], "chunks": len(data["chunks"]), "independentMappedTextCompared": True, "independentCosineCompared": path.stem in labeled})
report = {"reader": "PyMuPDF", "version": fitz.VersionBind, "actualModelInferenceQualified": False, "recordedRelevance": {"queries": 3, "topOneCorrect": 3, "productionRelevanceQualified": False}, "fixtures": records}
(args.output / "independent-retrieval.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"fixtures": len(records), "recordedTopOneCorrect": 3, "actualModelInferenceQualified": False}))
