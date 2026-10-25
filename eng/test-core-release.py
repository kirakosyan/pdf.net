"""Synthetic rejection tests only; no SDK, runtime or package qualification."""
import copy
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET
import core_release as core


class EvidenceBoundaries(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / "synthetic.xml"
        self.manifest = {"content": {"files": {"lib/" + tfm + "/Fluent.Pdf.dll": "0" * 64 for tfm in core.TFMS}}}
        self.row = dict(checks="12", failed="0", skipped="0", architecture="X64", platform="Linux",
                        os="synthetic", framework=".NET 10.0.0", runtimeVersion="10.0.0",
                        frameworkRelease="", coreLibrary="System.Private.CoreLib", coreLibraryVersion="10.0",
                        sdkSha256="0" * 64)

    def check(self, asset="net10.0", host="linux-x64", minimum=False):
        ET.ElementTree(ET.Element("consumer", self.row)).write(self.path)
        return core.consumer(self.path, self.manifest, asset, host, minimum)

    def minimum(self):
        self.row.update(platform="Windows", framework="Framework", runtimeVersion="4.0.30319",
                        frameworkRelease="461814", coreLibrary="mscorlib", coreLibraryVersion="4.7.3946.0")

    def test_structurally_valid_synthetic_observation(self):
        self.assertEqual("net10.0", self.check()["asset"])

    def test_wrong_loaded_bytes(self):
        self.row["sdkSha256"] = "1" * 64
        with self.assertRaisesRegex(ValueError, "different core"): self.check()

    def test_failed_or_skipped_or_missing_case(self):
        for name, value in (("failed", "1"), ("skipped", "1"), ("checks", "11")):
            with self.subTest(name=name):
                original = self.row[name]; self.row[name] = value
                with self.assertRaisesRegex(ValueError, "incomplete"): self.check()
                self.row[name] = original

    def test_wrong_host_os(self):
        self.row["platform"] = "Windows"
        with self.assertRaisesRegex(ValueError, "OS differs"): self.check()

    def test_wrong_process_architecture(self):
        self.row["architecture"] = "Arm64"
        with self.assertRaisesRegex(ValueError, "architecture"): self.check()

    def test_newer_runtime_does_not_cover_net8(self):
        with self.assertRaisesRegex(ValueError, "runtime"): self.check("net8.0")

    def test_fallback_requires_actual_modern_runtime(self):
        self.row["coreLibrary"] = "mscorlib"
        with self.assertRaisesRegex(ValueError, "CLR"): self.check("netstandard2.0")

    def test_actual_minimum_synthetic_record(self):
        self.minimum()
        self.assertTrue(self.check("net472", "win-x64", True)["frameworkMinimum"])

    def test_framework48_is_not_minimum(self):
        self.minimum(); self.row["frameworkRelease"] = "528040"
        with self.assertRaisesRegex(ValueError, "release"): self.check("net472", "win-x64", True)

    def test_old_registry_value_with_newer_library_rejects(self):
        self.minimum(); self.row["coreLibraryVersion"] = "4.8.0.0"
        with self.assertRaisesRegex(ValueError, "core library"): self.check("net472", "win-x64", True)

    def test_minimum_is_not_current(self):
        self.minimum()
        with self.assertRaisesRegex(ValueError, "release"): self.check("net472", "win-x64")

    def test_scope_cannot_publish_another_package_version_or_qualified_roadmap(self):
        baseline = core.policy()
        for key, value in (("packageId", "Fluent.Pdf.Html"), ("packageVersion", "1.0.1"),
                           ("fullRoadmapQualified", True), ("publishCompanionPackages", True),
                           ("scope", "full-sdk"), ("requiredConsumerHosts", ["linux-x64"])):
            with self.subTest(key=key):
                changed = copy.deepcopy(baseline); changed[key] = value
                with patch.object(core, "read", return_value=changed):
                    with self.assertRaisesRegex(ValueError, "scope changed"): core.policy()


if __name__ == "__main__":
    unittest.main()
