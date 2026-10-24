"""Independent metadata/fidelity checks over the retained repeated-process outputs."""
import argparse
import importlib.util
import json
from pathlib import Path

import pypdf

spec=importlib.util.spec_from_file_location("fidelity",Path(__file__).with_name("fidelity.py"))
fidelity=importlib.util.module_from_spec(spec);spec.loader.exec_module(fidelity)


def run(args):
    args.output.mkdir(parents=True,exist_ok=False)
    report=json.loads((args.candidates/"report.json").read_text(encoding="utf-8"))
    if sorted(x["identity"]["compression"] for x in report["profiles"]) != ["compressed","plain"]:raise ValueError("Incomplete/duplicate compression profiles")
    records=[]
    for profile in report["profiles"]:
        mode=profile["identity"]["compression"]
        runs=profile["runs"]
        if len(runs)!=5:raise ValueError("Incomplete process repetitions")
        for index,row in enumerate(runs):
            if row["directory"]!=mode+"-"+str(index) or row["outputs"]!=runs[0]["outputs"]:raise ValueError("Process identity/bytes changed")
            directory=args.candidates/row["directory"]
            if directory.resolve().parent!=args.candidates.resolve():raise ValueError("Invalid run directory")
            if fidelity.sha(directory/"consumer.json")!=row["reportSha256"]:raise ValueError("Changed consumer report")
            consumer=json.loads((directory/"consumer.json").read_text(encoding="utf-8"))
            if len(consumer["records"])!=4 or len({x["file"] for x in consumer["records"]})!=4:raise ValueError("Incomplete/duplicate PDF inventory")
            for item in consumer["records"]:
                path=directory/item["file"]
                if path.resolve().parent!=directory.resolve():raise ValueError("Invalid PDF path")
                if fidelity.sha(path)!=row["outputs"][item["file"]]:raise ValueError("Changed process output")
                reader=pypdf.PdfReader(path,strict=True)
                for name in ("/CreationDate","/ModDate"):
                    if reader.metadata[name]!="D:20000102030405Z":raise ValueError("Fixed timestamp mismatch")
                ids=reader.trailer["/ID"]
                def raw(value):return value.original_bytes if hasattr(value,"original_bytes") else bytes(value)
                expected=bytes.fromhex(item["inputSha256"])[:16]
                if len(ids)!=2 or any(raw(value)!=expected for value in ids):raise ValueError("Explicit identity mismatch")
                records.append(dict(path=str(path),sha256=fidelity.sha(path),fixedMetadataVerified=True))
        # Each repetition's bytes were checked above. Render the first exact byte
        # sequence once per profile instead of treating duplicate pixels as new coverage.
        first=args.candidates/runs[0]["directory"]
        fidelity.run(argparse.Namespace(corpus=args.corpus,candidates=first,output=args.output/mode,poppler=args.poppler))
    (args.output/"report.json").write_text(json.dumps(dict(schemaVersion=1,purpose="source-independent-determinism",processReportSha256=fidelity.sha(args.candidates/"report.json"),metadataOutputs=records,fidelityProfiles=[x["identity"]["compression"] for x in report["profiles"]],productionQualified=False),indent=2)+"\n",encoding="utf-8")
    print(f"{len(records)} fixed metadata outputs and two independent fidelity profiles passed")


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ("corpus","candidates","output","poppler"):parser.add_argument("--"+name,type=Path,required=True)
    run(parser.parse_args())
