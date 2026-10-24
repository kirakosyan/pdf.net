"""Independently parse support JSON and read the compiled C#/VB PDF outputs."""
import argparse
import hashlib
import json
from pathlib import Path
import re

import pypdf
import pymupdf


def require(condition,message):
    if not condition:raise ValueError(message)


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def validate(path):
    text=path.read_text(encoding="utf-8")
    for token in ("secret","private-operation-name","private-provider-id","private.pdf","private.invalid","password=","Original automation","Second automation page"):
        require(token not in text,"Private canary appeared in telemetry")
    value=json.loads(text)
    require(set(value)=={"schemaVersion","traceId","droppedEvents","sinkFailures","events"} and value["schemaVersion"]==1,"Unexpected support schema")
    require(re.fullmatch("[a-f0-9]{32}",value["traceId"]) is not None,"Invalid generated trace ID")
    for field in ("droppedEvents","sinkFailures"):
        require(type(value[field]) is int and value[field]>=0,"Invalid loss counter")
    sequence=0
    for event in value["events"]:
        require(set(event)=={"sequence","spanId","kind","capability","elapsedTicks","succeeded","code","remediation","completed","total","diagnosticCount","engineVersion","resources"},"Unexpected event field")
        require(type(event["sequence"]) is int and event["sequence"]>sequence,"Invalid event order");sequence=event["sequence"]
        require(re.fullmatch("[a-f0-9]{32}",event["spanId"]) is not None,"Invalid generated span ID")
        require(event["kind"] in ("Started","Progress","Finished","EngineProbe"),"Invalid kind")
        for field in ("elapsedTicks","diagnosticCount"):
            require(type(event[field]) is int and event[field]>=0,"Invalid numeric event field")
        require(event["succeeded"] is None or type(event["succeeded"]) is bool,"Invalid success flag")
        if event["succeeded"] is False:
            require(bool(event["code"]) and bool(event["remediation"]),"Failure lacks safe remediation")
        if event["kind"]=="Progress":
            require(type(event["completed"]) is int and type(event["total"]) is int and 0<=event["completed"]<=event["total"] and event["total"]>0,"Invalid progress")
        if event["engineVersion"] is not None:
            require(re.fullmatch(r"\d+\.\d+(\.\d+){0,2}",event["engineVersion"]) is not None,"Non-numeric engine version")
        for resource in event["resources"]:
            require(set(resource)=={"resource","consumed","peak"},"Unexpected resource field")
            require(resource["resource"] in ("InputBytes","ExpandedBytes","OutputBytes","Objects","Pixels","Depth","Workers"),"Unknown resource")
            require(type(resource["consumed"]) is int and type(resource["peak"]) is int and 0<=resource["consumed"]<=resource["peak"],"Invalid resource counters")
    return len(value["events"])


def run(args):
    records=[]
    expected=["Original automation Ω","Second automation page"]
    for directory in args.directories:
        for name in ("support.json","lifecycle.json","privacy.json","csharp.json","visual-basic.json"):
            path=directory/name
            records.append(dict(path=str(path),sha256=sha(path),events=validate(path)))
        for name in ("csharp.pdf","visual-basic.pdf"):
            path=directory/name
            reader=pypdf.PdfReader(path,strict=True)
            require([page.extract_text().strip() for page in reader.pages]==expected,"PDF Unicode/page order changed")
            with pymupdf.open(path) as document:
                require([page.get_text().strip() for page in document]==expected,"Second reader disagrees")
                require(len(document)==len(reader.pages)==2,"PDF page count changed")
                for a,b in zip(reader.pages,document):
                    require(float(a.mediabox.width)==b.rect.width and float(a.mediabox.height)==b.rect.height,"Independent geometry differs")
            records.append(dict(path=str(path),sha256=sha(path),independentReaders=2,exactUnicodeAndPageOrder=True))
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(dict(schemaVersion=1,purpose="source-independent-support-schema-and-output",pypdf=pypdf.__version__,mupdf=pymupdf.VersionBind,records=records,productionQualified=False),indent=2)+"\n",encoding="utf-8")
    print(f"{len(args.directories)*5} support bundles and {len(args.directories)*2} PDF outputs passed independent checks")


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directories",type=Path,nargs="+",required=True)
    parser.add_argument("--output",type=Path,required=True)
    run(parser.parse_args())
