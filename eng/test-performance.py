"""Negative tests for performance qualification, including retained evidence tampering."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
import subprocess
import performance as p


def fixture():
    report = dict(schemaVersion=1, purpose="source-performance-calibration", hardware={"cpuModel": "original-fixture"},
                  runtime=".NET fixture", os="fixture", architecture="X64", processorCount=4, corpusSha256="a" * 64,
                  measurementContract={"fixture": "b" * 64}, workflow="original-fixture", sourceCommit="c" * 40, workingTreeDirty=False,
                  sdkSha256="d" * 64, consumerSha256="e" * 64,
                  cases=[])
    for category in ("small", "large", "scanned", "adversarial"):
        diagnostic = "Limit.InputBytes" if category == "adversarial" else None
        observations = [dict(elapsedMs=i, firstPageModelMs=None if diagnostic else i / 2, diagnostic=diagnostic,
                             peakWorkingSetBytes=100, outputBytes=0 if diagnostic else 10) for i in range(1, 21)]
        for state in ("process-cold-filesystem-cache-uncontrolled", "process-warm-filesystem-cache-uncontrolled"):
            report["cases"].append(dict(id=category, category=category, state=state, samples=20, diagnostic=diagnostic, inputSha256="f" * 64, maximumInputBytes=1 if diagnostic else 64, pages=1, metrics=p.summarize(observations, diagnostic)))
    baseline = copy.deepcopy(report)
    baseline.update(reviewed=True, reviewReference="Original checker fixture only; never a real baseline.")
    for case in baseline["cases"]:
        case["limits"] = {k: v * (.5 if k == "throughputPerSecond" else 2) for k, v in case["metrics"].items()}
    return report, baseline


class PerformanceTests(unittest.TestCase):
    def test_nearest_rank_and_incomplete_metrics(self):
        self.assertEqual(19, p.percentile(list(range(1, 21)), .95))
        self.assertEqual(20, p.percentile(list(range(1, 21)), .99))
        for data in ([], [float("nan")], [-1], [True]):
            with self.assertRaises(ValueError): p.percentile(data, .5)

    def test_at_ten_percent_passes_above_fails(self):
        report, baseline = fixture()
        p.compare(report, baseline)
        report["cases"][0]["metrics"]["latencyP99Ms"] *= 1.1
        p.compare(report, baseline)
        report["cases"][0]["metrics"]["latencyP99Ms"] += .001
        with self.assertRaisesRegex(ValueError, "regression"): p.compare(report, baseline)

    def test_throughput_direction_and_absolute_envelope(self):
        report, baseline = fixture()
        report["cases"][0]["metrics"]["throughputPerSecond"] *= .9
        p.compare(report, baseline)
        report["cases"][0]["metrics"]["throughputPerSecond"] -= .001
        with self.assertRaises(ValueError): p.compare(report, baseline)
        report, baseline = fixture()
        baseline["cases"][0]["limits"]["outputBytes"] = 9
        with self.assertRaises(ValueError): p.compare(report, baseline)

    def test_every_identity_must_match(self):
        for key in ("hardware", "runtime", "os", "architecture", "processorCount", "corpusSha256", "measurementContract", "workflow"):
            report, baseline = fixture(); report[key] = "different"
            with self.assertRaisesRegex(ValueError, "identity"): p.compare(report, baseline)

    def test_no_calibration_self_approval_or_missing_cases(self):
        report, baseline = fixture(); baseline["reviewed"] = False
        with self.assertRaisesRegex(ValueError, "reviewed"): p.compare(report, baseline)
        report, baseline = fixture(); report["cases"].pop()
        with self.assertRaisesRegex(ValueError, "cases"): p.compare(report, baseline)
        report, baseline = fixture(); report["cases"].append(report["cases"][0])
        with self.assertRaisesRegex(ValueError, "cases"): p.compare(report, baseline)

    def test_nonfinite_empty_metrics_and_sample_count_reject(self):
        for replacement in ({}, {"latencyP50Ms": float("inf")}):
            report, baseline = fixture(); report["cases"][0]["metrics"] = replacement
            with self.assertRaises(ValueError): p.compare(report, baseline)
        report, baseline = fixture(); report["cases"][0]["samples"] = 19
        with self.assertRaises(ValueError): p.compare(report, baseline)

    def test_source_cannot_satisfy_production(self):
        report, baseline = fixture()
        with self.assertRaisesRegex(ValueError, "Source calibration"): p.compare(report, baseline, True)
        report["purpose"] = "production-performance"
        with self.assertRaisesRegex(ValueError, "workload"): p.compare(report, baseline, True)

    def test_expected_negative_cannot_emit_output_or_hide_failure(self):
        row = dict(elapsedMs=1, firstPageModelMs=None, diagnostic="Limit.InputBytes", peakWorkingSetBytes=10, outputBytes=1)
        with self.assertRaises(ValueError): p.summarize([row], "Limit.InputBytes")
        row["outputBytes"] = 0
        with self.assertRaises(ValueError): p.summarize([row], None)

    def test_retained_paths_and_hashes_fail_closed(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            report = {"reports": [{"path": "../outside.json", "sha256": "a" * 64}]}
            with self.assertRaisesRegex(ValueError, "path"): p.verify_observations(report, root)
            p.save(root / "raw.json", {})
            report["reports"][0]["path"] = "raw.json"
            with self.assertRaisesRegex(ValueError, "hash"): p.verify_observations(report, root)

    def test_recomputes_real_groups_and_rejects_aggregate_or_output_tampering(self):
        report, _ = fixture()
        report["reports"] = []
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            for row in report["cases"]:
                cold = row["state"].startswith("process-cold")
                for number in range(20 if cold else 1):
                    directory = root / (row["id"] + "-" + str(cold) + "-" + str(number)); directory.mkdir()
                    diagnostic = row["diagnostic"]
                    if diagnostic is None: (directory / "output.pdf").write_bytes(b"0123456789")
                    values = [number + 1] if cold else list(range(1, 21))
                    raw = {k: report[k] for k in ("runtime", "os", "architecture", "processorCount", "sdkSha256", "consumerSha256")}
                    raw.update({"case": {"id": row["id"], "category": row["category"], "expectedDiagnostic": diagnostic, "sha256": row["inputSha256"], "maximumInputBytes": row["maximumInputBytes"], "pages": row["pages"]},
                                "iterations": len(values), "warmups": 0 if cold else 3, "state": row["state"],
                                "outputSha256": None if diagnostic else p.digest(directory / "output.pdf"),
                                "observations": [dict(elapsedMs=i, firstPageModelMs=None if diagnostic else i / 2, diagnostic=diagnostic,
                                                      peakWorkingSetBytes=100, outputBytes=0 if diagnostic else 10) for i in values]})
                    path = directory / "raw.json"; p.save(path, raw)
                    report["reports"].append(dict(path=path.relative_to(root).as_posix(), sha256=p.digest(path)))
            p.verify_observations(report, root)
            changed = copy.deepcopy(report); changed["cases"][0]["metrics"]["latencyP99Ms"] = 1
            with self.assertRaisesRegex(ValueError, "Aggregate"): p.verify_observations(changed, root)
            (root / "small-True-0/output.pdf").write_bytes(b"altered")
            with self.assertRaisesRegex(ValueError, "Output"): p.verify_observations(report, root)


    def test_actual_release_gate_rejects_hash_valid_incomplete_performance(self):
        source = Path(__file__).resolve().parent
        with tempfile.TemporaryDirectory(prefix="fluent-performance-gate-") as folder:
            root = Path(folder).resolve()
            (root / "eng").mkdir(); (root / "specs").mkdir(); (root / "evidence").mkdir()
            for name in ("assert-production-ready.ps1", "performance.py"):
                (root / "eng" / name).write_bytes((source / name).read_bytes())
            subprocess.run(["git", "init", "-q", str(root)], check=True, capture_output=True)
            subprocess.run(["git", "-C", str(root), "-c", "user.name=Performance fixture", "-c", "user.email=fixture@example.invalid", "commit", "--allow-empty", "-qm", "Original gate fixture"], check=True, capture_output=True)
            commit = subprocess.check_output(["git", "-C", str(root), "rev-parse", "HEAD"], text=True).strip()
            p.save(root / "specs/release-readiness.json", dict(productionReady=True, packageVersion="1.0.0", requiredLanes=["win-x64-net10.0"], requiredSuites=["performance"]))
            p.save(root / "specs/requirements.json", {"features": [dict(id="FP-088", releaseRequired=True, status="qualified")]})
            report, baseline = fixture()
            report.update(sourceCommit=commit, lane="win-x64-net10.0", stage="source", reports=[])
            p.save(root / "evidence/report.json", report); p.save(root / "evidence/baseline.json", baseline)
            suite = dict(name="performance", total=1, passed=1, failed=0, skipped=0, reportPath="report.json", sha256=p.digest(root / "evidence/report.json"), baselinePath="baseline.json", baselineSha256=p.digest(root / "evidence/baseline.json"))
            p.save(root / "evidence/release-evidence.json", dict(schemaVersion=1, purpose="production-qualification", sourceCommit=commit, packageVersion="1.0.0", stage="source", lanes=[dict(id="win-x64-net10.0", runtime="fixture", os="fixture", architecture="X64", suites=[suite])], requirements=[]))
            result = subprocess.run(["pwsh", "-NoProfile", "-File", str(root / "eng/assert-production-ready.ps1"), "-EvidenceDirectory", str(root / "evidence")], capture_output=True, text=True, timeout=30)
            self.assertNotEqual(0, result.returncode)
            self.assertIn("Missing/extra observation groups", result.stderr)
            self.assertIn("Performance regression, incomplete workload or invalid baseline", result.stderr)


if __name__ == "__main__": unittest.main()
