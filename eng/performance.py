"""Run finite original source measurements; validate reviewed performance envelopes.

Cold means a fresh process, never a claimed cold OS file cache. Calibration cannot
qualify a runtime, provider, package, hardware baseline or complete SDK workload.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import subprocess
import time


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def save(path, value):
    Path(path).write_text(json.dumps(value, indent=2, allow_nan=False) + "\n", encoding="utf-8")


def percentile(values, fraction):
    if not values or any(type(v) not in (int, float) or not math.isfinite(v) or v < 0 for v in values):
        raise ValueError("Missing, negative or nonfinite observations.")
    return sorted(values)[max(0, math.ceil(len(values) * fraction) - 1)]


def summarize(rows, diagnostic):
    if not rows or any(r["diagnostic"] != diagnostic for r in rows):
        raise ValueError("Unexpected success/failure inventory.")
    elapsed = [r["elapsedMs"] for r in rows]
    if any(v <= 0 for v in elapsed):
        raise ValueError("Elapsed observations must be positive.")
    result = {"latencyP50Ms": percentile(elapsed, .5), "latencyP95Ms": percentile(elapsed, .95),
              "latencyP99Ms": percentile(elapsed, .99), "throughputPerSecond": len(rows) * 1000 / sum(elapsed),
              "peakWorkingSetBytes": percentile([r["peakWorkingSetBytes"] for r in rows], 1),
              "outputBytes": percentile([r["outputBytes"] for r in rows], 1)}
    first = [r["firstPageModelMs"] for r in rows]
    if diagnostic:
        if any(v is not None for v in first) or result["outputBytes"] != 0:
            raise ValueError("Rejected input exposed a model or output.")
    else:
        if any(v is None for v in first) or result["outputBytes"] <= 0:
            raise ValueError("Missing first-page model or output.")
        result.update({"firstPageModelP50Ms": percentile(first, .5), "firstPageModelP95Ms": percentile(first, .95),
                       "firstPageModelP99Ms": percentile(first, .99)})
    if result["peakWorkingSetBytes"] <= 0:
        raise ValueError("Working-set measurement unavailable.")
    return result


def compare(report, baseline, production=False):
    if report.get("schemaVersion") != 1 or baseline.get("schemaVersion") != 1:
        raise ValueError("Unknown performance schema.")
    if baseline.get("reviewed") is not True or not baseline.get("reviewReference"):
        raise ValueError("A reviewed baseline is required; calibration cannot approve itself.")
    for field in ("hardware", "runtime", "os", "architecture", "processorCount", "corpusSha256", "measurementContract", "workflow"):
        if not report.get(field) or report[field] != baseline.get(field):
            raise ValueError("Incomparable performance identity: " + field)
    if production:
        if report.get("purpose") != "production-performance" or not report.get("sourceCommit") or report.get("workingTreeDirty") is not False:
            raise ValueError("Source calibration is not production performance evidence.")
        required = {"managed-read-write", "range-first-page", "render", "html", "conversion", "ocr"}
        if set(report.get("qualifiedWorkflows", [])) != required or baseline.get("qualifiedWorkflows") != report["qualifiedWorkflows"]:
            raise ValueError("Complete reviewed performance workload coverage is required.")
    actual = {(r["id"], r["state"]): r for r in report["cases"]}
    expected = {(r["id"], r["state"]): r for r in baseline["cases"]}
    if len(actual) != len(report["cases"]) or len(expected) != len(baseline["cases"]) or not actual or actual.keys() != expected.keys():
        raise ValueError("Missing/extra/duplicate performance cases.")
    if {r["category"] for r in actual.values()} != {"small", "large", "scanned", "adversarial"}:
        raise ValueError("All four corpus categories are required.")
    states = {"process-cold-filesystem-cache-uncontrolled", "process-warm-filesystem-cache-uncontrolled"}
    for case_id in {r["id"] for r in actual.values()}:
        if {r["state"] for r in actual.values() if r["id"] == case_id} != states:
            raise ValueError("Every case requires both process states.")
    for key, row in actual.items():
        old = expected[key]
        for name in ("inputSha256", "maximumInputBytes", "pages"):
            if row.get(name) is None or row[name] != old.get(name):
                raise ValueError("Incomparable input identity/policy: " + name)
        required_metrics = {"latencyP50Ms", "latencyP95Ms", "latencyP99Ms", "throughputPerSecond", "peakWorkingSetBytes", "outputBytes"}
        if row["diagnostic"] is None:
            required_metrics.update({"firstPageModelP50Ms", "firstPageModelP95Ms", "firstPageModelP99Ms"})
        if set(row["metrics"]) != required_metrics:
            raise ValueError("Required metric inventory is incomplete or unknown.")
        if row["samples"] < 20 or row["samples"] != old["samples"] or row["category"] != old["category"] or row["diagnostic"] != old["diagnostic"]:
            raise ValueError("Incomparable sample/diagnostic inventory.")
        if row["metrics"].keys() != old["metrics"].keys() or row["metrics"].keys() != old["limits"].keys():
            raise ValueError("Metric/limit inventory mismatch.")
        for metric, value in row["metrics"].items():
            previous, limit = old["metrics"][metric], old["limits"][metric]
            if any(type(x) not in (float, int) or not math.isfinite(x) or x < 0 for x in (value, previous, limit)):
                raise ValueError("Nonfinite/negative performance metric or limit.")
            # Ten percent is a maximum regression, never a caller-selectable relaxation.
            if metric == "throughputPerSecond":
                if value < limit or value < previous * .9:
                    raise ValueError("Throughput regression: " + str(key))
            elif value > limit or value > previous * 1.1:
                raise ValueError("Performance regression: " + str(key) + " " + metric)


def verify_observations(report, directory):
    """Recompute all aggregates from exact retained raw observations before comparison."""
    groups, paths = {}, set()
    for record in report["reports"]:
        relative = Path(record["path"])
        path = (directory / relative).resolve()
        if relative.is_absolute() or not path.is_relative_to(directory.resolve()) or path in paths:
            raise ValueError("Invalid or duplicate observation path.")
        paths.add(path)
        if digest(path) != record["sha256"]:
            raise ValueError("Observation hash mismatch.")
        raw = load(path)
        for name in ("runtime", "os", "architecture", "processorCount", "sdkSha256", "consumerSha256"):
            if raw[name] != report[name]:
                raise ValueError("Raw observation identity mismatch: " + name)
        key = (raw["case"]["id"], raw["state"])
        if raw["iterations"] != len(raw["observations"]) or raw["iterations"] < 1:
            raise ValueError("Raw sample inventory mismatch.")
        cold = raw["state"] == "process-cold-filesystem-cache-uncontrolled"
        if cold and (raw["iterations"] != 1 or raw["warmups"] != 0):
            raise ValueError("A cold sample requires a fresh single-iteration process.")
        if not cold and (raw["state"] != "process-warm-filesystem-cache-uncontrolled" or raw["warmups"] != 3):
            raise ValueError("Unrecognized warm state.")
        group = groups.setdefault(key, {"case": raw["case"], "rows": [], "processes": 0})
        if group["case"] != raw["case"]:
            raise ValueError("Corpus case changed between samples.")
        group["rows"].extend(raw["observations"])
        group["processes"] += 1
        if raw["case"]["expectedDiagnostic"] is None:
            pdf = path.parent / "output.pdf"
            if digest(pdf) != raw["outputSha256"] or any(r["outputBytes"] != pdf.stat().st_size for r in raw["observations"]):
                raise ValueError("Output bytes/hash mismatch.")
        elif (path.parent / "output.pdf").exists() or raw["outputSha256"] is not None:
            raise ValueError("Rejected measurement retained output.")
    if not groups or set(groups) != {(r["id"], r["state"]) for r in report["cases"]}:
        raise ValueError("Missing/extra observation groups.")
    for row in report["cases"]:
        group = groups[(row["id"], row["state"])]
        for target, source in (("inputSha256", "sha256"), ("maximumInputBytes", "maximumInputBytes"), ("pages", "pages")):
            if row[target] != group["case"][source]:
                raise ValueError("Raw corpus identity/policy mismatch.")
        if group["case"]["category"] != row["category"] or group["case"]["expectedDiagnostic"] != row["diagnostic"]:
            raise ValueError("Aggregate case mismatch.")
        if len(group["rows"]) != row["samples"] or summarize(group["rows"], row["diagnostic"]) != row["metrics"]:
            raise ValueError("Aggregate differs from raw samples.")
        if row["state"].startswith("process-warm") and group["processes"] != 1:
            raise ValueError("Warm samples must share one process.")


def run(args):
    root = Path(__file__).resolve().parents[1]
    corpus_path, assembly, output = args.corpus.resolve(), args.assembly.resolve(), args.output.resolve()
    if output.exists():
        raise ValueError("Use a fresh result directory.")
    corpus = load(corpus_path)
    pinned = load(root / "eng/performance-corpus.json")
    if corpus != pinned:
        raise ValueError("Corpus differs from the committed original manifest.")
    hardware = load(args.hardware)
    for name in ("cpuModel", "physicalMemoryBytes", "logicalProcessors", "provenance"):
        if not hardware.get(name):
            raise ValueError("Incomplete hardware profile: " + name)
    for name in ("physicalMemoryBytes", "logicalProcessors"):
        if type(hardware[name]) is not int or hardware[name] <= 0:
            raise ValueError("Hardware counts must be positive integers.")
    if not 20 <= args.samples <= 1000:
        raise ValueError("Use 20 through 1000 measured samples per case/state.")
    output.mkdir(parents=True)
    rows, manifests, identity = [], [], None
    for case in corpus["cases"]:
        case_path = corpus_path.parent / (case["id"] + ".case.json")
        # One case descriptor is a bounded derived input, never a regenerated golden.
        save(case_path, case)
        for state in ("process-cold-filesystem-cache-uncontrolled", "process-warm-filesystem-cache-uncontrolled"):
            samples = []
            for repeat in range(args.samples if state.startswith("process-cold") else 1):
                destination = output / (case["id"] + "-" + state.split("-")[1] + "-" + str(repeat))
                command = ["dotnet", str(assembly), "measure", str(case_path), str(destination),
                           "1" if state.startswith("process-cold") else str(args.samples), "0" if state.startswith("process-cold") else "3"]
                started = time.monotonic()
                completed = subprocess.run(command, capture_output=True, text=True, timeout=600)
                save(output / (destination.name + ".process.json"), {"exitCode": completed.returncode, "wallSeconds": time.monotonic() - started,
                     "stdout": completed.stdout[-8192:], "stderr": completed.stderr[-8192:]})
                if completed.returncode:
                    raise RuntimeError("Measurement failed: " + destination.name + " " + completed.stderr[-2000:])
                path = destination / "measurements.json"
                value = load(path)
                observed = {k: value[k] for k in ("runtime", "os", "architecture", "processorCount", "sdkSha256", "consumerSha256")}
                if identity is not None and identity != observed:
                    raise ValueError("Runtime/assembly changed during measurement.")
                identity = observed
                if value["case"] != case or value["state"] != state:
                    raise ValueError("Unexpected case/state.")
                samples.extend(value["observations"])
                manifests.append({"path": path.relative_to(output).as_posix(), "sha256": digest(path)})
            if len(samples) != args.samples:
                raise ValueError("Incomplete sample inventory.")
            rows.append({"id": case["id"], "category": case["category"], "state": state, "diagnostic": case["expectedDiagnostic"],
                         "inputSha256": case["sha256"], "maximumInputBytes": case["maximumInputBytes"], "pages": case["pages"],
                         "samples": len(samples), "metrics": summarize(samples, case["expectedDiagnostic"])})
    if hardware["logicalProcessors"] != identity["processorCount"]:
        raise ValueError("Hardware/process processor-count mismatch.")
    report = {"schemaVersion": 1, "purpose": "source-performance-calibration", "hardware": hardware, **identity,
              "corpusSha256": digest(corpus_path), "workflow": corpus["workflow"],
              "measurementContract": {p: digest(root / p) for p in ("eng/Performance/Program.cs", "eng/performance.py")},
              "sourceCommit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(),
              "workingTreeDirty": bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=root)),
              "cases": rows, "reports": manifests, "productionQualified": False,
              "limitations": ["Eager model first-page availability is not rendering/range transfer/OCR latency.",
                              "OS file cache is uncontrolled in both process states.", "Synthetic source corpus and unreviewed hardware are not complete production qualification.",
                              "Process peak working set includes runtime/consumer/warmup; SDK counters do not measure total RSS."]}
    verify_observations(report, output)
    save(output / "performance.json", report)
    print("SOURCE_PERFORMANCE_CALIBRATION_RECORDED;" + str(len(rows)) + "-case-states;not-production-qualified")


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)
    execute = sub.add_parser("run")
    for name in ("corpus", "assembly", "hardware", "output"):
        execute.add_argument("--" + name, type=Path, required=True)
    execute.add_argument("--samples", type=int, default=20)
    check = sub.add_parser("check")
    check.add_argument("--report", type=Path, required=True)
    check.add_argument("--baseline", type=Path, required=True)
    check.add_argument("--production", action="store_true")
    check.add_argument("--source-commit")
    check.add_argument("--lane")
    check.add_argument("--stage", choices=("source", "package"))
    args = parser.parse_args()
    if args.command == "run":
        run(args)
    else:
        report = load(args.report)
        if args.production and (not args.source_commit or not args.lane or not args.stage or
                                report.get("sourceCommit") != args.source_commit or report.get("lane") != args.lane or report.get("stage") != args.stage):
            raise ValueError("Qualification commit/lane/stage binding is required.")
        verify_observations(report, args.report.resolve().parent)
        compare(report, load(args.baseline), args.production)
        print("PERFORMANCE_ENVELOPES_PASS;reviewed-baseline;complete-SDK-qualification-separate")


if __name__ == "__main__":
    main()
