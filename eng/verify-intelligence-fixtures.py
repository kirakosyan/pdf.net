"""Independent JSON/schema/source observations on original recorded adapter fixtures."""
import argparse
import base64
import hashlib
from importlib.metadata import version
import io
import json
from pathlib import Path
import fitz
from PIL import Image
from jsonschema import Draft202012Validator

parser = argparse.ArgumentParser()
parser.add_argument("fixtures", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


require(version("jsonschema") == "4.25.1", "Pinned jsonschema4.25.1 is required")
require(fitz.VersionBind == "1.26.6", "Pinned PyMuPDF1.26.6 is required")
require(version("Pillow") == "13.0.0", "Pinned Pillow13.0.0 is required")
records = []
paths = sorted(args.fixtures.glob("*.request.json"))
require(len(paths) == 11, "Expected six tasks, two privacy cases, nested schema and two compiled consumers")
for path in paths:
    name = path.name.removesuffix(".request.json")
    request = json.loads(path.read_text(encoding="utf-8-sig"))
    accepted_path = args.fixtures / (name + ".accepted.json")
    accepted = json.loads(accepted_path.read_text(encoding="utf-8-sig"))
    require(request["tool_choice"] == "none" and "tools" not in request, name + ": tool authority must be absent")
    require(request["store"] is False and request["stream"] is False, name + ": exact finite nonstored request required")
    require(request["temperature"] == 0 and request["n"] == 1, name + ": explicit sampling/choice profile required")
    require([item["role"] for item in request["messages"]] == ["system", "user"], name + ": untrusted data must not add roles")
    content = request["messages"][1]["content"]
    controls = json.loads(content[0]["text"])
    require(controls["request_sha256"] == (args.fixtures / (name + ".binding.txt")).read_text(encoding="utf-8-sig"), name + ": binding differs")
    schema = request["response_format"]["json_schema"]["schema"]
    Draft202012Validator.check_schema(schema)
    Draft202012Validator(schema["properties"]["data"]).validate(accepted)
    require("PRIVATE UNSELECTED PAGE" not in path.read_text(encoding="utf-8-sig"), name + ": unselected page leaked")
    require("PRIVATE METADATA" not in path.read_text(encoding="utf-8-sig"), name + ": document metadata leaked")
    sources = {item["source_id"]: item for item in controls["untrusted_sources"]}
    images = [item for item in content if item["type"] == "image_url"]
    image_sources = [item for item in sources.values() if item["kind"] == "image"]
    require(len(images) == len(image_sources), name + ": explicit image binding differs")
    for source, image in zip(image_sources, images):
        prefix = "data:image/png;base64,"
        uri = image["image_url"]["url"]
        require(uri.startswith(prefix), name + ": external resource URL is forbidden")
        data = base64.b64decode(uri[len(prefix):], validate=True)
        require(hashlib.sha256(data).hexdigest() == source["source_sha256"], name + ": captured PNG hash differs")
        with Image.open(io.BytesIO(data)) as decoded:
            require(decoded.format == "PNG", name + ": independent PNG reader required")
            decoded.load()
            require(decoded.width > 0 and decoded.height > 0, name + ": empty image")
    source_pdf = args.fixtures / (name + ".source.pdf")
    with fitz.open(source_pdf) as pdf:
        for source in sources.values():
            region = [float(value) for value in source["capture_region"]]
            require(source["page_index"] == 0, name + ": unexpected page")
            page = pdf[source["page_index"]]
            require(region[0] >= 0 and region[1] >= 0 and region[0] + region[2] <= page.rect.width + 1e-9 and region[1] + region[3] <= page.rect.height + 1e-9, name + ": capture outside source page")
            if source["kind"] != "text":
                continue
            text = source["untrusted_text"]
            require(hashlib.sha256(text.encode("utf-8")).hexdigest() == source["source_sha256"], name + ": captured text hash differs")
            clip = fitz.Rect(region[0], region[1], region[0] + region[2], region[1] + region[3])
            independent = page.get_text(clip=clip).strip()
            require(independent == text.strip(), name + ": independent source extraction differs beyond terminal whitespace")
    evidence_count = 0
    for line in (args.fixtures / (name + ".evidence.tsv")).read_text(encoding="utf-8-sig").splitlines():
        pointer, identity, page, x, y, width, height, digest, offset, encoded_quote = line.split("\t")
        source = sources[identity]
        require(digest == source["source_sha256"] and int(page) == source["page_index"], name + ": evidence identity differs")
        require([x, y, width, height] == source["capture_region"], name + ": exact source frame differs")
        value = accepted
        if pointer:
            for member in pointer[1:].split("/"):
                member = member.replace("~1", "/").replace("~0", "~")
                value = value[int(member)] if isinstance(value, list) else value[member]
        quote = base64.b64decode(encoded_quote, validate=True).decode("utf-8")
        if source["kind"] == "text":
            text = source["untrusted_text"]
            first = text.find(quote)
            require(quote and first >= 0 and text.find(quote, first + 1) == -1, name + ": quote is not exact and unique")
            require(int(offset) == len(text[:first].encode("utf-16-le")) // 2, name + ": quote offset differs")
        else:
            require(not quote and not offset, name + ": image has an unverified quote")
        evidence_count += 1
    require(evidence_count > 0, name + ": source provenance is absent")
    records.append({"fixture": name, "task": controls["task"], "requestSha256": sha(path),
                    "acceptedSha256": sha(accepted_path), "sourcePdfSha256": sha(source_pdf),
                    "schemaValidatedIndependently": True, "sourceReaderChecked": True,
                    "sourceCount": len(sources), "imageCount": len(images), "citationsChecked": evidence_count})
report = {"schema": 1, "productionQualified": False, "actualModelInferenceQualified": False,
          "scope": "Original deterministic recorded responses, independent schema/source/frame/hash checks; no live remote model or linguistic/OCR quality claim",
          "readers": {"jsonschema": version("jsonschema"), "PyMuPDF": fitz.VersionBind, "Pillow": version("Pillow")},
          "fixtures": records}
(args.output / "independent-intelligence.json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps({"recordedAdapterFixtures": len(records), "independentSchemaAndProvenanceChecks": True,
                  "actualModelInferenceQualified": False, "productionQualified": False}))
