#!/usr/bin/env python3
"""Fail-closed runtime/package evidence consistency checks, never qualification.

Observations must originate in an actual test process. Trusted host/provenance,
minimum-machine provisioning and complete suites remain separate release gates.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import uuid
import xml.etree.ElementTree as ET
import zipfile


ROOT = Path(__file__).resolve().parents[1]


def require(value, reason):
    if not value:
        raise ValueError(reason)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def validate_identity(identity, lane, sdk_names):
    require(identity.get("schemaVersion") == 1 and identity.get("purpose") == "source-runtime-identity", "identity-schema")
    require(identity.get("productionQualified") is False, "observation-cannot-qualify")
    require(identity.get("osFamily") == lane["os"] and bool(identity.get("osDescription")), "actual-os")
    require(identity.get("processArchitecture", "").lower() == lane["processArchitecture"], "actual-process-architecture")
    require(identity.get("osArchitecture", "").lower() == lane["osArchitecture"], "native-host-architecture")
    require(identity.get("rid") == lane["rid"], "actual-rid")
    require(re.fullmatch(r"[a-f0-9]{64}", identity.get("coreLibrary", {}).get("sha256", "")), "core-library-digest")
    runtime, core = identity.get("runtime", ""), identity["coreLibrary"]
    require(str(identity.get("runtimeVersion", "")).split(".")[0] == str(lane["runtimeMajor"]), "actual-runtime-major")
    if lane["runtimeFamily"] == "framework":
        require(runtime.startswith(".NET Framework ") and core.get("name") == "mscorlib", "actual-framework-process")
        release = identity.get("frameworkRelease")
        require(type(release) is int, "live-framework-registry-observation")
        if lane["minimumFramework"]:
            require(release in lane["frameworkRelease"] and core.get("fileVersion", "").startswith("4.7.") and runtime.startswith(".NET Framework 4.7."), "actual-minimum-framework472")
        else:
            require(release >= 528040 and bool(core.get("fileVersion")), "actual-current-framework")
    else:
        require(re.match(r"^\.NET " + str(lane["runtimeMajor"]) + r"\.", runtime) and core.get("name") == "System.Private.CoreLib", "actual-coreclr-process")
    assets = identity.get("sdkAssets", [])
    require(len(assets) == len(sdk_names) and {a.get("name") for a in assets} == sdk_names, "complete-sdk-asset-inventory")
    for asset in assets:
        require(asset.get("targetFramework") == lane["libraryTarget"], "actual-loaded-sdk-target")
        require(re.fullmatch(r"[a-f0-9]{64}", asset.get("sha256", "")), "actual-sdk-binary-identity")
        module_id = asset.get("moduleVersionId", "")
        require(re.fullmatch(r"[a-fA-F0-9]{8}(?:-[a-fA-F0-9]{4}){3}-[a-fA-F0-9]{12}", module_id) and uuid.UUID(module_id).int != 0, "actual-sdk-module-identity")


def read_trx(path):
    require(path.stat().st_size <= 67108864, "trx-extent")
    tree = ET.parse(path).getroot()
    namespace = {"t": tree.tag.split("}")[0][1:]}
    summary = tree.find("t:ResultSummary", namespace)
    require(summary is not None and summary.get("outcome") in {"Passed", "Completed"}, "completed-trx-run")
    counters = tree.find(".//t:Counters", namespace)
    require(counters is not None, "trx-counters")
    counts = {key: int(value) for key, value in counters.attrib.items()}
    require(counts.get("total", 0) > 0 and counts.get("passed") == counts["total"], "required-test-total")
    for key in ["failed", "notExecuted", "aborted", "inconclusive", "passedButRunAborted", "error", "timeout", "notRunnable", "disconnected"]:
        require(counts.get(key, 0) == 0, "required-failure-or-skip")
    results = tree.findall(".//t:UnitTestResult", namespace)
    require(len(results) == counts["total"] and all(r.get("outcome") == "Passed" for r in results), "complete-trx-results")
    ids = [r.get("testName") for r in results]
    require(all(ids) and len(set(ids)) == len(ids), "duplicate-or-missing-test-result")
    matches = [r for r in results if r.get("testName", "").endswith("RuntimeIdentityTests.TheRequestedLibraryAssetIsActuallyLoaded")]
    require(len(matches) == 1, "same-process-runtime-test")
    text = matches[0].findtext("t:Output/t:StdOut", namespaces=namespace) or ""
    lines = [line[len("Runtime evidence: "):] for line in text.splitlines() if line.startswith("Runtime evidence: ")]
    require(len(lines) == 1, "same-process-runtime-json")
    return json.loads(lines[0]), counts, ids


def exact_test_inventory(path, ids):
    value = json.loads(path.read_bytes())
    require(value.get("schemaVersion") == 1 and value.get("purpose") == "reviewed-runtime-test-inventory", "reviewed-inventory-schema")
    expected = value.get("tests", [])
    require(expected and len(set(expected)) == len(expected) and set(expected) == set(ids), "full-reviewed-test-inventory")


def package_bindings(manifest_path, identity, version):
    value = json.loads(manifest_path.read_bytes())
    require(value.get("schemaVersion") == 1 and value.get("packageVersion") == version, "exact-package-version")
    records = value.get("packages", [])
    require(len({(r["id"], r["version"]) for r in records}) == len(records), "duplicate-package-identity")
    root = manifest_path.resolve().parent
    for record in records:
        require(Path(record["file"]).name == record["file"] and record["file"].endswith(".nupkg"), "package-path")
        require(digest(root / record["file"]) == record["sha256"], "package-file-hash")
    short = {".NETFramework,Version=v4.7.2": "net472", ".NETStandard,Version=v2.0": "netstandard2.0", ".NETCoreApp,Version=v8.0": "net8.0", ".NETCoreApp,Version=v10.0": "net10.0"}
    for asset in identity["sdkAssets"]:
        candidates = [r for r in records if r["id"] == asset["name"] and r["version"] == version]
        require(len(candidates) == 1, "complete-exact-sdk-packages")
        with zipfile.ZipFile(root / candidates[0]["file"]) as package:
            name = "lib/" + short[asset["targetFramework"]] + "/" + asset["name"] + ".dll"
            info = package.getinfo(name)
            require(0 < info.file_size <= 67108864 and hashlib.sha256(package.read(info)).hexdigest() == asset["sha256"], "loaded-package-asset-hash")
    graph = value.get("consumerDependencyGraph", {})
    require(Path(graph.get("file", "")).name == graph.get("file") and digest(root / graph["file"]) == graph.get("sha256"), "consumer-dependency-graph-hash")
    dependencies = json.loads((root / graph["file"]).read_bytes())["libraries"]
    for asset in identity["sdkAssets"]:
        require(dependencies.get(asset["name"] + "/" + version, {}).get("type") == "package", "fresh-package-not-project-reference")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--lane", required=True)
    parser.add_argument("--trx", type=Path, required=True)
    parser.add_argument("--mode", choices=["source", "package"], default="source")
    parser.add_argument("--package-manifest", type=Path)
    parser.add_argument("--test-inventory", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    # Never remove/overwrite caller inputs or an earlier report. Callers own
    # their report lifecycle; each invocation requires a fresh output path.
    require(not args.output.exists() and not args.output.is_symlink(), "fresh-output-path-required")
    for path in [args.trx, args.package_manifest, args.test_inventory]:
        require(path is None or path.resolve() != args.output.resolve(), "output-input-alias")
    matrix = json.loads((ROOT / "specs/runtime-lanes.json").read_bytes())
    readiness = json.loads((ROOT / "specs/release-readiness.json").read_bytes())
    require({r["id"] for r in matrix["lanes"]} == set(readiness["requiredLanes"]), "mandatory-lane-matrix-drift")
    matches = [r for r in matrix["lanes"] if r["id"] == args.lane]
    require(len(matches) == 1, "declared-required-lane")
    identity, counts, ids = read_trx(args.trx)
    validate_identity(identity, matches[0], {p.parent.name for p in (ROOT / "src").glob("*/PublicAPI.Shipped.txt")})
    if args.test_inventory:
        exact_test_inventory(args.test_inventory, ids)
    if args.mode == "package":
        require(args.package_manifest is not None and args.test_inventory is not None, "exact-packages-and-reviewed-full-inventory-required")
        package_bindings(args.package_manifest, identity, readiness["packageVersion"])
    result = dict(schemaVersion=1, purpose="runtime-evidence-consistency", lane=args.lane, mode=args.mode, observation=identity,
                  counters=counts, trxSha256=digest(args.trx), laneMatrixSha256=digest(ROOT / "specs/runtime-lanes.json"),
                  reviewedFullTestInventoryVerified=bool(args.test_inventory), exactPackageBindingsVerified=args.mode == "package",
                  trustedHostProvenanceQualified=False, allRequiredSuitesQualified=False, productionQualified=False)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x", encoding="utf-8") as output:
        output.write(json.dumps(result, indent=2) + "\n")
    print("RUNTIME_EVIDENCE_CONSISTENCY_PASS;" + args.lane + ";" + args.mode + ";not-production-qualification")


if __name__ == "__main__":
    main()
