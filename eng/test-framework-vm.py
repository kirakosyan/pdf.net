#!/usr/bin/env python3
"""Synthetic source-VM boundary tests. No Windows, installer or Docker execution."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import stat
import subprocess
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock
import xml.etree.ElementTree as ET
import zipfile


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(filename))
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


vm = load("framework_vm", "framework-vm.py")
fixtures = load("runtime_fixtures", "test-runtime-evidence.py")


def save(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n")


class SourceVmTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="pdf-synthetic-vm-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.setup = self.root / "setup"
        self.results = self.root / "results"
        self.setup.mkdir()
        self.results.mkdir()
        self.commit = "a" * 40
        self.sdk_names = sorted(p.parent.name for p in (vm.ROOT / "src").glob("*/PublicAPI.Shipped.txt"))
        self.inputs = {"tests/" + name + ".dll": ("SYNTHETIC NOT A DLL " + name).encode() for name in self.sdk_names}
        self.inputs["tests/Fluent.Pdf.Contract.Tests.dll"] = b"SYNTHETIC NOT A TEST ASSEMBLY"
        self.inputs["testplatform/tools/net462/Common7/IDE/Extensions/TestPlatform/vstest.console.exe"] = b"SYNTHETIC NOT A RUNNER"
        for name in ("run-framework-contracts.ps1", "run-framework-bundle.ps1"):
            self.inputs[name] = (vm.ROOT / "eng" / name).read_bytes()
        self.bundle = dict(schemaVersion=1, purpose="offline-framework-source-bundle", sourceCommit=self.commit,
                           productionQualified=False, exactPackageConsumers=False,
                           files=[dict(path=k, bytes=len(v), sha256=hashlib.sha256(v).hexdigest()) for k, v in self.inputs.items()])
        raw = json.dumps(self.bundle).encode()
        with zipfile.ZipFile(self.setup / "source-bundle.zip", "w") as archive:
            archive.writestr("bundle.json", raw)
            for name, payload in self.inputs.items():
                archive.writestr(name, payload)
        for name in vm.SETUP_FILES - {"source-bundle.zip", "baseline-x86.trx", "baseline-x64.trx"}:
            if name in {"qualify.ps1", "install.bat"}:
                (self.setup / name).write_bytes((vm.ROOT / "eng/framework-vm" / name).read_bytes())
            else:
                (self.setup / name).write_text("SYNTHETIC NONEXECUTABLE CHECKER FIXTURE")
        self.status = dict(schemaVersion=1, purpose="framework472-vm-source-execution", sourceCommit=self.commit,
                           passed=True, stage="complete", productionQualified=False, exactPackageConsumers=False,
                           runtimeOverrideNames=[], installedFrameworkRelease=461814,
                           cppRuntimes=[dict(architecture=a, exitCode=0, sha256=vm.digest(self.setup / ("vc_redist." + a + ".exe"))) for a in ("x86", "x64")],
                           lanes=[dict(architecture=a, passed=True) for a in ("x86", "x64")])
        for architecture in ("x86", "x64"):
            self.trx(self.setup / ("baseline-" + architecture + ".trx"), architecture, minimum=False)
            directory = self.results / architecture
            directory.mkdir()
            self.trx(directory / "runtime-contracts.trx", architecture, minimum=True)
            identity, counts, _ = vm.checker().read_trx(directory / "runtime-contracts.trx")
            inputs = {k: hashlib.sha256(v).hexdigest() for k, v in self.inputs.items()}
            save(directory / "framework-execution.json", dict(executionPassed=True, exitCode=0, timedOut=False,
                 filtered=False, requireFramework472=True, architecture=architecture, installedFrameworkRelease=461814,
                 testAssemblySha256=inputs["tests/Fluent.Pdf.Contract.Tests.dll"],
                 runnerSha256=inputs["testplatform/tools/net462/Common7/IDE/Extensions/TestPlatform/vstest.console.exe"],
                 runtimeIdentity=identity, counters=counts, productionQualified=False, exactPackageConsumersExecuted=False))
            baseline_identity, baseline_counts, _ = vm.checker().read_trx(self.setup / ("baseline-" + architecture + ".trx"))
            baseline_execution = vm.read_json(directory / "framework-execution.json")
            baseline_execution.update(requireFramework472=False, installedFrameworkRelease=baseline_identity["frameworkRelease"],
                                      runtimeIdentity=baseline_identity, counters=baseline_counts)
            save(self.setup / ("baseline-" + architecture + "-execution.json"), baseline_execution)
            save(directory / "bundle-origin.json", dict(sourceCommit=self.commit,
                 bundleManifestSha256=hashlib.sha256(raw).hexdigest(), productionQualified=False))
        save(self.results / "status.json", self.status)
        self.manifest = dict(schemaVersion=1, purpose="framework472-vm-source-setup", sourceCommit=self.commit,
                             productionQualified=False, exactPackageConsumers=False,
                             bundleManifestSha256=hashlib.sha256(raw).hexdigest())
        self.rehash_setup()

    def rehash_setup(self):
        self.manifest["files"] = [dict(path=n, bytes=(self.setup / n).stat().st_size, sha256=vm.digest(self.setup / n)) for n in sorted(vm.SETUP_FILES)]
        save(self.setup / "setup.json", self.manifest)

    def trx(self, path, architecture, minimum):
        lane = next(x for x in fixtures.matrix["lanes"] if x["id"] == "win-" + architecture + ("-framework472" if minimum else "-framework-current"))
        identity = fixtures.fixture(lane)
        example = identity["sdkAssets"][0]
        identity["sdkAssets"] = [dict(example, name=n, sha256=hashlib.sha256(self.inputs["tests/" + n + ".dll"]).hexdigest()) for n in self.sdk_names]
        tree = fixtures.trx_fixture(path)
        tree.find("Results/UnitTestResult/Output/StdOut").text = "Runtime evidence: " + json.dumps(identity)
        ET.ElementTree(tree).write(path, encoding="utf-8", xml_declaration=True)

    def verify(self):
        return vm.validate_results(self.results, self.setup, self.manifest, self.bundle)

    def test_complete_synthetic_same_binary_source_observations_remain_unqualified(self):
        manifest, bundle = vm.validate_setup(self.setup, self.commit)
        self.assertEqual(self.bundle, bundle)
        result = vm.validate_results(self.results, self.setup, manifest, bundle)
        self.assertEqual(["win-x86-framework472", "win-x64-framework472"], [x["lane"] for x in result["lanes"]])
        self.assertIs(False, result["productionQualified"])
        self.assertIs(False, result["exactPackageConsumers"])

    def test_foreign_source_newer_framework_missing_architecture_and_jit_override_reject(self):
        for key, value in [("sourceCommit", "b" * 40), ("passed", False), ("installedFrameworkRelease", 533320),
                           ("productionQualified", True), ("exactPackageConsumers", True),
                           ("runtimeOverrideNames", ["COMPlus_UseLegacyJit"]), ("lanes", [self.status["lanes"][0]])]:
            with self.subTest(key=key):
                changed = dict(self.status, **{key: value})
                save(self.results / "status.json", changed)
                with self.assertRaises(ValueError):
                    self.verify()
        save(self.results / "status.json", self.status)

    def test_changed_sdk_runner_filtered_execution_and_false_origin_reject(self):
        path = self.results / "x86/framework-execution.json"
        original = vm.read_json(path)
        for key, value in [("executionPassed", False), ("exitCode", 1), ("timedOut", True), ("filtered", True),
                           ("testAssemblySha256", "0" * 64), ("runnerSha256", "0" * 64),
                           ("runtimeIdentity", {}), ("productionQualified", True)]:
            with self.subTest(key=key):
                save(path, dict(original, **{key: value}))
                with self.assertRaises(ValueError):
                    self.verify()
        save(path, original)
        origin = self.results / "x64/bundle-origin.json"
        changed = vm.read_json(origin)
        changed["sourceCommit"] = "0" * 40
        save(origin, changed)
        with self.assertRaisesRegex(ValueError, "Foreign source"):
            self.verify()

    def test_complete_passing_subset_is_not_the_full_source_suite(self):
        path = self.results / "x86/runtime-contracts.trx"
        ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
        tree = ET.parse(path)
        results = tree.getroot().find("t:Results", ns)
        results.remove(results[1])
        counters = tree.getroot().find("t:ResultSummary/t:Counters", ns)
        counters.set("total", "1")
        counters.set("passed", "1")
        tree.write(path, encoding="utf-8", xml_declaration=True)
        with self.assertRaisesRegex(ValueError, "complete same-binary baseline"):
            self.verify()

    def test_changed_setup_hash_foreign_commit_and_extra_file_reject(self):
        with self.assertRaises(ValueError):
            vm.validate_setup(self.setup, "0" * 40)
        (self.setup / "qualify.ps1").write_text("changed")
        with self.assertRaisesRegex(ValueError, "file changed"):
            vm.validate_setup(self.setup, self.commit)
        self.rehash_setup()
        (self.setup / "unmanifested").write_text("extra")
        with self.assertRaisesRegex(ValueError, "Unmanifested"):
            vm.validate_setup(self.setup, self.commit)

    def test_portable_paths_reject_escapes_windows_aliases_and_devices(self):
        for path in ["../escape", "/absolute", "C:/absolute", "a\\b", "x/../y", "x/./y",
                     "x//y", "trailing. ", "NUL.txt", "folder/COM1.log", "bad\0name"]:
            with self.subTest(path=repr(path)), self.assertRaises(ValueError):
                vm.safe_path(path)
        self.assertEqual(".signature.p7s", str(vm.safe_path(".signature.p7s")))

    def test_zip_case_aliases_symlinks_and_expansion_extent_reject(self):
        original = (self.setup / "source-bundle.zip").read_bytes()
        with zipfile.ZipFile(self.setup / "source-bundle.zip", "a") as archive:
            archive.writestr("tests/fluent.pdf.dll", b"case alias")
        self.rehash_setup()
        with self.assertRaisesRegex(ValueError, "Duplicate source ZIP"):
            vm.validate_setup(self.setup, self.commit)
        # Independent fixture to exercise the expansion bound before file reads.
        with mock.patch.object(vm, "MAX_BYTES", 1), self.assertRaisesRegex(ValueError, "extent"):
            vm.validate_setup(self.setup, self.commit)
        (self.setup / "source-bundle.zip").write_bytes(original)
        with zipfile.ZipFile(self.setup / "source-bundle.zip", "a") as archive:
            link = zipfile.ZipInfo("unsafe-link")
            link.external_attr = (stat.S_IFLNK | 0o777) << 16
            archive.writestr(link, "../outside")
        self.rehash_setup()
        with self.assertRaisesRegex(ValueError, "symlink"):
            vm.validate_setup(self.setup, self.commit)

    def test_rebinding_manifest_cannot_replace_guest_control_source(self):
        (self.setup / "qualify.ps1").write_text("SYNTHETIC REPLACEMENT SCRIPT")
        self.rehash_setup()
        with self.assertRaisesRegex(ValueError, "Guest control source"):
            vm.validate_setup(self.setup, self.commit)

    def test_guest_export_cannot_replace_host_receipts_or_follow_symlinks(self):
        source, target = self.root / "export", self.root / "copied"
        source.mkdir()
        target.mkdir()
        (source / "cleanup.json").write_text("untrusted")
        with self.assertRaisesRegex(ValueError, "Unexpected guest"):
            vm.copy_export(source, target)
        (source / "cleanup.json").unlink()
        (source / "status.json").write_text("synthetic path classification")
        with mock.patch.object(Path, "is_symlink", lambda p: p == source / "status.json"), self.assertRaisesRegex(ValueError, "Unsafe"):
            vm.copy_export(source, target)

    def test_archive_verifies_every_report_and_never_claims_runtime_execution(self):
        digest = vm.archive_reports(self.results)
        self.assertEqual(digest, vm.digest(self.results / "evidence.zip"))
        index = vm.read_json(self.results / "archive-index.json")
        self.assertIs(False, index["productionQualified"])
        self.assertGreater(len(index["files"]), 3)

    def test_existing_state_and_nested_paths_are_never_overwritten(self):
        work = self.root / "existing"
        work.mkdir()
        sentinel = work / "preserve"
        sentinel.write_text("original")
        args = SimpleNamespace(setup=self.setup, work=work, results=self.root / "new-results", commit=self.commit, timeout_seconds=60)
        with mock.patch.object(vm.subprocess, "run") as external, self.assertRaises(ValueError):
            vm.run(args)
        external.assert_not_called()
        self.assertEqual("original", sentinel.read_text())
        args.work, args.results = self.root / "nested", self.root / "nested/results"
        with self.assertRaises(ValueError):
            vm.run(args)

    def test_wrong_host_rejects_before_any_docker_or_network_request(self):
        args = SimpleNamespace(setup=self.setup, work=self.root / "work", results=self.root / "host-rejection", commit=self.commit, timeout_seconds=60)
        with mock.patch.object(vm.platform, "system", return_value="NotLinux"), mock.patch.object(vm.subprocess, "run") as external:
            with self.assertRaises(ValueError):
                vm.run(args)
        external.assert_not_called()
        receipt = vm.read_json(args.results / "cleanup.json")
        self.assertTrue(receipt["evidenceArchivedAndVerified"])
        self.assertTrue(receipt["stateRemoved"])

    def lifecycle(self, log_error=False, archive_error=False):
        work, results = self.root / "fake-owned-vm", self.root / "fake-host-results"
        args = SimpleNamespace(setup=self.setup, work=work, results=results, commit=self.commit, timeout_seconds=60)
        commands = []
        password = "a" * 32 + "aA1!"
        actual_stat = Path.stat
        actual_exists = Path.exists

        def fake_stat(path, *args, **kwargs):
            if path == Path("/dev/kvm"):
                return SimpleNamespace(st_mode=stat.S_IFCHR | 0o600)
            return actual_stat(path, *args, **kwargs)

        def fake_exists(path, *args, **kwargs):
            # Python 3.14's exists() uses os.path rather than Path.stat().
            if path == Path("/dev/kvm"):
                return True
            return actual_exists(path, *args, **kwargs)

        def fake_docker(command, **kwargs):
            commands.append(command)
            self.assertEqual(["docker", "--host=unix:///var/run/docker.sock"], command[:2])
            self.assertNotIn("DOCKER_HOST", kwargs["env"])
            self.assertNotIn("DOCKER_CONTEXT", kwargs["env"])
            self.assertEqual("unchanged-docker-config", kwargs["env"]["DOCKER_CONFIG"])
            if command[2] == "info":
                return SimpleNamespace(stdout=json.dumps(dict(OSType="linux", Architecture="x86_64", MemTotal=8 * 1024**3)))
            if command[-2:] == ["up", "-d"]:
                raise subprocess.CalledProcessError(17, command, stderr="synthetic partial create " + password)
            if "logs" in command:
                if log_error:
                    raise OSError("synthetic log failure")
                return SimpleNamespace(stdout="synthetic log " + password + " " + vm.quote(password, safe=""))
            return SimpleNamespace(stdout="", returncode=0)

        with mock.patch.dict(vm.os.environ, {"GITHUB_ACTIONS": "true", "RUNNER_ENVIRONMENT": "github-hosted",
                "DOCKER_HOST": "tcp://must-not-use", "DOCKER_CONTEXT": "must-not-use", "DOCKER_CONFIG": "unchanged-docker-config"}), \
             mock.patch.object(vm.platform, "system", return_value="Linux"), mock.patch.object(vm.platform, "machine", return_value="x86_64"), \
             mock.patch.object(Path, "stat", fake_stat), mock.patch.object(Path, "exists", fake_exists), \
             mock.patch.object(vm.os, "access", return_value=True), \
             mock.patch.object(vm.shutil, "disk_usage", return_value=SimpleNamespace(free=100 * 1024**3)), \
             mock.patch.object(vm.uuid, "uuid4", return_value=SimpleNamespace(hex="a" * 32)), \
             mock.patch.object(vm.subprocess, "run", side_effect=fake_docker):
            if archive_error:
                with mock.patch.object(vm, "archive_reports", side_effect=OSError("synthetic archive failure")), self.assertRaises(ValueError):
                    vm.run(args)
            else:
                with self.assertRaises(ValueError):
                    vm.run(args)
        self.assertTrue(any("down" in c for c in commands))
        self.assertNotIn(password, (results / "host-error.txt").read_text())
        if (results / "vm.log").exists():
            self.assertNotIn(password, (results / "vm.log").read_text())
            self.assertNotIn(vm.quote(password, safe=""), (results / "vm.log").read_text())
        return work, vm.read_json(results / "cleanup.json")

    def test_partial_creation_failure_stops_only_owned_resources_and_redacts_secrets(self):
        work, receipt = self.lifecycle()
        self.assertFalse(work.exists())
        self.assertTrue(receipt["stateRemoved"])
        self.assertTrue(receipt["imageRetained"])

    def test_log_failure_does_not_skip_container_cleanup(self):
        _, receipt = self.lifecycle(log_error=True)
        self.assertTrue(receipt["containersRemoved"])
        self.assertTrue(receipt["stateRemoved"])

    def test_archive_failure_stops_container_but_preserves_state_for_recovery(self):
        work, receipt = self.lifecycle(archive_error=True)
        self.assertTrue(work.exists())
        self.assertTrue(receipt["containersRemoved"])
        self.assertFalse(receipt["stateRemoved"])
        self.assertFalse(receipt["evidenceArchivedAndVerified"])


if __name__ == "__main__":
    unittest.main()
