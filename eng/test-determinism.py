"""Compare full outputs from five fresh processes for each declared compression profile."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(args):
    output=args.output.resolve(); output.mkdir(parents=True,exist_ok=False)
    corpus=args.corpus.resolve()
    manifest=json.loads((corpus/"manifest.json").read_text(encoding="utf-8"))
    expected={x["file"]:x["sha256"] for x in manifest["fixtures"]}
    if len(expected)!=4:raise ValueError("Pinned four-fixture corpus required")
    profiles=[]
    for mode in ("plain","compressed"):
        baseline=None; identity=None; runs=[]
        for index in range(5):
            directory=output/(mode+"-"+str(index))
            process=subprocess.run(["dotnet",str(args.assembly.resolve()),str(corpus),str(directory),mode],capture_output=True,timeout=60)
            (output/(directory.name+".log")).write_bytes(process.stdout+process.stderr)
            if process.returncode:raise RuntimeError("Deterministic consumer failed: "+directory.name)
            report=json.loads((directory/"consumer.json").read_text(encoding="utf-8"))
            current={x["file"]:x["outputSha256"] for x in report["records"]}
            if {x["file"]:x["inputSha256"] for x in report["records"]}!=expected:raise ValueError("Source inventory mismatch")
            if len(report["records"])!=len(expected):raise ValueError("Duplicate output records")
            for file,digest in current.items():
                if sha(directory/file)!=digest:raise ValueError("Output hash mismatch")
            profile={k:report[k] for k in ("profile","compression","fixedTimestamp","runtime","os","architecture","sdkSha256")}
            if index and (baseline!=current or identity!=profile):raise ValueError("Repeated-process bytes or declared inputs changed")
            baseline=current;identity=profile;runs.append(dict(directory=directory.name,reportSha256=sha(directory/"consumer.json"),outputs=current))
        profiles.append(dict(identity=identity,runs=runs))
    (output/"report.json").write_text(json.dumps(dict(schemaVersion=1,purpose="source-repeated-process-determinism",corpusManifestSha256=sha(corpus/"manifest.json"),profiles=profiles,processes=10,outputs=40,productionQualified=False),indent=2)+"\n",encoding="utf-8")
    print("10 fresh processes, 40 byte-identical outputs within two explicit profiles")


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ("assembly","corpus","output"):parser.add_argument("--"+name,type=Path,required=True)
    run(parser.parse_args())
