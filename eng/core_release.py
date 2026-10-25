"""Version-specific core artifact checks. Never reports full-roadmap qualification."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import shutil
import xml.etree.ElementTree as ET
import zipfile
import time
import urllib.request
import urllib.error
import re

ROOT = Path(__file__).resolve().parents[1]
HOSTS = ("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")
TFMS = ("net472", "netstandard2.0", "net8.0", "net10.0")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def module(name):
    spec = importlib.util.spec_from_file_location(name.replace("-", "_"), ROOT / "eng" / (name + ".py"))
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def commit():
    return subprocess.check_output(["git", "-C", str(ROOT), "rev-parse", "HEAD"], text=True).strip()


def policy():
    value = read(ROOT / "specs/core-release.json")
    require(value["schemaVersion"] == 1 and value["packageId"] == "Fluent.Pdf" and value["packageVersion"] == "1.0.0"
            and value["scope"] == "implemented-managed-core-profiles" and value["authorizedOn"] == "2026-10-25"
            and value["authorization"].strip() and value["fullRoadmapQualified"] is False
            and value["publishCompanionPackages"] is False and value["scopeDocument"] == "docs/releases/1.0.0.md"
            and value["requiredConsumerHosts"] == list(HOSTS) and value["requiredAssets"] == list(TFMS),
            "Core 1.0.0 release scope changed")
    return value


def audit(directory):
    directory = Path(directory)
    manifest = read(directory / "core-manifest.json")
    require(manifest["schemaVersion"] == 1 and manifest["sourceCommit"] == commit()
            and manifest["scope"] == policy() and manifest["fullRoadmapQualified"] is False,
            "Core candidate origin/scope differs from this checkout")
    expected = manifest["content"]
    require(expected["id"] == "Fluent.Pdf" and expected["version"] == "1.0.0"
            and expected["sourceCommit"] == commit(), "Foreign core candidate")
    packages = directory / "packages"
    require({p.name for p in packages.iterdir()} ==
            {"Fluent.Pdf.1.0.0.nupkg", "Fluent.Pdf.1.0.0.snupkg"}, "Only the exact core package and symbols are authorized")
    inspector = module("package-audit")
    results = []
    for symbols, suffix in ((False, "nupkg"), (True, "snupkg")):
        path = packages / ("Fluent.Pdf.1.0.0." + suffix)
        result = inspector.inspect(inspector.read_archive(path), expected, symbols)
        require(result["sha256"] == manifest["packageHashes"][path.name], "Core package changed after capture")
        results.append(result)
    return manifest, results


def create_manifest(directory):
    directory = Path(directory)
    all_manifest = directory / "source-content.json"
    module("new-package-manifest").create(ROOT, all_manifest)
    entries = read(all_manifest)["packages"]
    expected = next(x for x in entries if x["id"] == "Fluent.Pdf")
    write(directory / "core-manifest.json", {
        "schemaVersion": 1, "sourceCommit": commit(), "scope": policy(), "fullRoadmapQualified": False,
        "content": expected,
        "packageHashes": {p.name: sha(p) for p in sorted((directory / "packages").iterdir())}})
    _, results = audit(directory)
    write(directory / "package-audit.json", results)


def consumer(path, manifest, tfm, host, framework_minimum=False):
    row = ET.parse(path).getroot().attrib
    require(row["checks"] == "12" and row["failed"] == "0" and row["skipped"] == "0", "Core consumer cases incomplete")
    require(tfm in TFMS and row["sdkSha256"] == manifest["content"]["files"]["lib/" + tfm + "/Fluent.Pdf.dll"],
            "Consumer loaded a different core assembly")
    expected_platform = {"win": "Windows", "linux": "Linux", "osx": "macOS"}[host.split("-")[0]]
    require(row["platform"] == expected_platform and row["os"], "Consumer OS differs")
    architecture = host.split("-")[1]
    require(row["architecture"].lower() == architecture, "Consumer process architecture differs")
    if tfm == "net472":
        release = int(row["frameworkRelease"])
        require(row["framework"] == "Framework" and row["coreLibrary"] == "mscorlib" and row["runtimeVersion"].startswith("4."),
                "Consumer did not execute on Framework")
        require(release in (461808, 461814) if framework_minimum else release >= 528040, "Actual Framework release differs")
        if framework_minimum:
            require(row["coreLibraryVersion"].startswith("4.7."), "Minimum Framework core library differs")
    else:
        require(row["coreLibrary"] == "System.Private.CoreLib" and row["framework"].startswith(".NET "), "Modern CLR required")
        expected_major = "10" if tfm == "netstandard2.0" else tfm[3:].split(".")[0]
        require(row["runtimeVersion"].split(".")[0] == expected_major, "Wrong modern runtime")
    return dict(row, asset=tfm, host=host, frameworkMinimum=framework_minimum, reportSha256=sha(path))


def verify_host(directory, reports, host):
    manifest, _ = audit(directory)
    rows = []
    for tfm in ("net8.0", "net10.0"):
        rows.append(consumer(Path(reports) / ("normal-" + tfm) / "consumer.xml", manifest, tfm, host))
    rows.append(consumer(Path(reports) / "fallback-net10.0/consumer.xml", manifest, "netstandard2.0", host))
    if host == "win-x64":
        for arch in ("x86", "x64"):
            rows.append(consumer(Path(reports) / ("framework-" + arch) / "consumer.xml", manifest, "net472", "win-" + arch))
    write(Path(reports) / "host.json", {"sourceCommit": commit(), "host": host,
          "fullRoadmapQualified": False, "packageSha256": manifest["packageHashes"]["Fluent.Pdf.1.0.0.nupkg"], "consumers": rows})
    return rows


def collect(directory, reports):
    manifest, _ = audit(directory)
    rows = []
    for host in HOSTS:
        root = Path(reports) / ("core-consumers-" + host)
        host_rows = verify_host(directory, root, host)
        names = ["normal-net8.0", "normal-net10.0", "fallback-net10.0"]
        if host == "win-x64": names += ["framework-x86", "framework-x64"]
        for row, name in zip(host_rows, names):
            row["reportPath"] = "core-consumers-" + host + "/" + name + "/consumer.xml"
            rows.append(row)
    minimum = Path(reports) / "minimum-framework"
    vm = read(minimum / "verification.json")
    cleanup = read(minimum / "cleanup.json")
    require(vm["sourceCommit"] == commit() and len(vm["lanes"]) == 2 and all(x["failed"] == 0 and x["skipped"] == 0 and x["passed"] == x["total"] > 0 for x in vm["lanes"]),
            "Minimum-Framework complete source checks failed")
    require(all(cleanup[k] is True for k in ("evidenceArchivedAndVerified", "containersRemoved", "networkRemoved", "stateRemoved")),
            "Owned VM reports/cleanup incomplete")
    for arch in ("x86", "x64"):
        row = consumer(minimum / arch / "core-consumer/consumer.xml", manifest, "net472", "win-" + arch, True)
        row["reportPath"] = "minimum-framework/" + arch + "/core-consumer/consumer.xml"
        rows.append(row)
    require(len(rows) == 22, "Missing actual core consumer lane")
    independent = read(Path(reports) / "independent/independent.json")
    require(independent["sourceCommit"] == commit() and independent["passed"] is True, "Missing independent generated-PDF checks")
    evidence = {"schemaVersion": 1, "purpose": "implemented-core-release-verification", "sourceCommit": commit(),
                "scope": policy(), "fullRoadmapQualified": False, "consumers": rows,
                "packageHashes": manifest["packageHashes"], "independent": independent}
    shutil.copytree(reports, Path(directory) / "reports")
    write(Path(directory) / "core-evidence.json", evidence)
    return evidence


def verify_release(directory):
    manifest, _ = audit(directory)
    evidence = read(Path(directory) / "core-evidence.json")
    require(evidence["purpose"] == "implemented-core-release-verification" and evidence["sourceCommit"] == commit()
            and evidence["scope"] == policy() and evidence["fullRoadmapQualified"] is False
            and evidence["packageHashes"] == manifest["packageHashes"], "Core evidence origin differs")
    expected = {(h, t, False) for h in HOSTS for t in ("net8.0", "net10.0", "netstandard2.0")}
    expected |= {("win-" + a, "net472", minimum) for a in ("x86", "x64") for minimum in (False, True)}
    actual = {(r["host"], r["asset"], r["frameworkMinimum"]) for r in evidence["consumers"]}
    require(len(evidence["consumers"]) == len(actual) == 22 and actual == expected, "Incomplete or duplicate package consumers")
    for row in evidence["consumers"]:
        require(row["checks"] == "12" and row["failed"] == row["skipped"] == "0"
                and row["sdkSha256"] == manifest["content"]["files"]["lib/" + row["asset"] + "/Fluent.Pdf.dll"],
                "Consumer evidence changed")
        report = (Path(directory) / "reports" / row["reportPath"]).resolve()
        require(report.is_relative_to((Path(directory) / "reports").resolve()), "Consumer report escapes evidence")
        observed = consumer(report, manifest, row["asset"], row["host"], row["frameworkMinimum"])
        require(all(row[k] == v for k, v in observed.items()), "Raw consumer report changed")
    require(evidence["independent"]["sourceCommit"] == commit() and evidence["independent"]["passed"] is True,
            "Independent check identity differs")
    for record in evidence["independent"]["files"]:
        require(record["file"] in ("generated.pdf", "edited.pdf", "incremental.pdf"), "Foreign independent fixture")
        require(sha(Path(directory) / "reports/core-consumers-linux-x64/normal-net10.0" / record["file"]) == record["sha256"],
                "Independent generated bytes changed")
    return evidence


def independent(directory, reports, output):
    manifest, _ = audit(directory)
    source = Path(reports) / "normal-net10.0"
    results = []
    for name in ("generated.pdf", "edited.pdf", "incremental.pdf"):
        path = source / name
        subprocess.run(["qpdf", "--check", str(path)], check=True, timeout=30)
        info = subprocess.check_output(["pdfinfo", str(path)], text=True, timeout=30)
        require(re.search(r"^Pages:\s+2\s*$", info, re.MULTILINE), "Independent page count differs")
        if name != "generated.pdf":
            require("Core 1.0.0" in info and "Package consumer" in info, "Independent metadata differs")
        results.append({"file": name, "sha256": sha(path), "pdfinfo": info})
    write(Path(output) / "independent.json", {"sourceCommit": commit(), "passed": True,
          "qpdf": subprocess.check_output(["qpdf", "--version"], text=True).strip(),
          "pdfinfo": subprocess.run(["pdfinfo", "-v"], capture_output=True, text=True, check=True).stderr.strip(),
          "files": results})


def public(directory):
    manifest, _ = audit(directory)
    url = "https://api.nuget.org/v3-flatcontainer/fluent.pdf/1.0.0/fluent.pdf.1.0.0.nupkg"
    raw = None
    for attempt in range(31):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers={"Cache-Control": "no-cache"}), timeout=30) as response:
                raw = response.read(32 * 1024 * 1024 + 1)
            break
        except urllib.error.HTTPError as error:
            if error.code not in (404, 429, 503) or attempt == 30: raise
            time.sleep(10)
    require(raw is not None and len(raw) <= 32 * 1024 * 1024, "Public package unavailable or oversized")
    inspected = module("package-audit").inspect(raw, manifest["content"], signed=True)
    destination = Path(directory) / "public/Fluent.Pdf.1.0.0.nupkg"
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(raw)
    subprocess.run(["dotnet", "nuget", "verify", str(destination), "--all"], check=True, timeout=180)
    with urllib.request.urlopen("https://api.nuget.org/v3-flatcontainer/fluent.pdf/index.json", timeout=30) as response:
        require("1.0.0" in json.load(response)["versions"], "Version not publicly indexed")
    write(Path(directory) / "public-verification.json", {
        "sourceCommit": commit(), "version": "1.0.0", "repositorySignatureVerified": True, "indexed": True,
        "unsignedCandidateSha256": manifest["packageHashes"]["Fluent.Pdf.1.0.0.nupkg"],
        "publicSignedSha256": inspected["sha256"], "allReviewedPayloadBytesMatch": True, "fullRoadmapQualified": False})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("stage", choices=("manifest", "audit", "host", "collect", "verify", "independent", "public"))
    parser.add_argument("--directory", type=Path, required=True)
    parser.add_argument("--reports", type=Path)
    parser.add_argument("--host", choices=HOSTS)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if args.stage == "manifest": create_manifest(args.directory)
    elif args.stage == "audit": audit(args.directory)
    elif args.stage == "host": verify_host(args.directory, args.reports, args.host)
    elif args.stage == "collect": collect(args.directory, args.reports)
    elif args.stage == "verify": verify_release(args.directory)
    elif args.stage == "independent": independent(args.directory, args.reports, args.output)
    else: public(args.directory)
    print("CORE_RELEASE_CHECK_PASS;" + args.stage)


if __name__ == "__main__":
    main()
