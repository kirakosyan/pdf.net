#!/usr/bin/env python3
"""Explicit synthetic checker-unit fixtures, never actual runtime evidence."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zipfile

path = Path(__file__).with_name("check-runtime-evidence.py")
spec = importlib.util.spec_from_file_location("runtime_evidence", path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
matrix = json.loads((path.parents[1] / "specs/runtime-lanes.json").read_bytes())


def fixture(lane):
    framework = lane["runtimeFamily"] == "framework"
    minimum = lane["minimumFramework"]
    return dict(schemaVersion=1, purpose="source-runtime-identity", productionQualified=False, osFamily=lane["os"], osDescription="SYNTHETIC CHECKER UNIT FIXTURE",
                processArchitecture=lane["processArchitecture"], osArchitecture=lane["osArchitecture"], rid=lane["rid"],
                runtime=".NET Framework 4.7.3062.0" if minimum else ".NET Framework 4.8.9363.0" if framework else ".NET " + str(lane["runtimeMajor"]) + ".0.1",
                runtimeVersion="4.0.30319.42000" if framework else str(lane["runtimeMajor"]) + ".0.1", frameworkRelease=461814 if minimum else 533320 if framework else None,
                coreLibrary=dict(name="mscorlib" if framework else "System.Private.CoreLib", fileVersion="4.7.3062.0" if minimum else "4.8.9363.0" if framework else "10.0.1", sha256="1" * 64),
                sdkAssets=[dict(name="Fixture.Sdk", targetFramework=lane["libraryTarget"], sha256="2" * 64, moduleVersionId="11111111-1111-1111-1111-111111111111")])


def trx_fixture(path):
    ns = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'
    root = ET.Element('{' + ns + '}TestRun')
    summary = ET.SubElement(root, 'ResultSummary', outcome='Completed')
    ET.SubElement(summary, 'Counters', total='2', passed='2', failed='0', notExecuted='0')
    results = ET.SubElement(root, 'Results')
    result = ET.SubElement(results, 'UnitTestResult', testId='11111111-1111-1111-1111-111111111111', executionId='22222222-2222-2222-2222-222222222222', testName='Fixture.RuntimeIdentityTests.TheRequestedLibraryAssetIsActuallyLoaded', outcome='Passed')
    output = ET.SubElement(result, 'Output')
    ET.SubElement(output, 'StdOut').text = 'Runtime evidence: ' + json.dumps(fixture(matrix['lanes'][0]))
    ET.SubElement(results, 'UnitTestResult', testId='33333333-3333-3333-3333-333333333333', executionId='44444444-4444-4444-4444-444444444444', testName='Fixture.RequiredBoundary', outcome='Passed')
    # Default namespace is inherited by the child elements in the serialized XML.
    ET.register_namespace('', ns)
    ET.ElementTree(root).write(path, encoding='utf-8', xml_declaration=True)
    return root


class RuntimeCheckerTests(unittest.TestCase):
    def test_all_twenty_two_declarations_are_required_and_fixture_consistent(self):
        self.assertEqual(22, len(matrix["lanes"]))
        self.assertEqual(22, len({r["id"] for r in matrix["lanes"]}))
        for lane in matrix["lanes"]:
            self.assertTrue(lane["required"])
            module.validate_identity(fixture(lane), lane, {"Fixture.Sdk"})

    def test_newer_framework_cannot_claim_minimum_even_with_target472(self):
        lane = next(r for r in matrix["lanes"] if r["id"] == "win-x86-framework472")
        for field, value in [("frameworkRelease", 533320), ("runtime", ".NET Framework 4.8.9363.0"), ("coreLibrary", dict(name="mscorlib", fileVersion="4.8.9363.0", sha256="1" * 64))]:
            value_fixture = fixture(lane)
            value_fixture[field] = value
            with self.assertRaises(ValueError): module.validate_identity(value_fixture, lane, {"Fixture.Sdk"})

    def test_wrong_runtime_native_host_loaded_asset_and_missing_identities_reject(self):
        lane = next(r for r in matrix["lanes"] if r["id"] == "osx-x64-net10.0")
        for field, value in [("runtime", ".NET 8.0.32"), ("runtimeVersion", "8.0.32"), ("osFamily", "linux"), ("processArchitecture", "Arm64"), ("osArchitecture", "Arm64"), ("rid", "osx-arm64"), ("sdkAssets", []), ("productionQualified", True)]:
            value_fixture = fixture(lane); value_fixture[field] = value
            with self.assertRaises(ValueError): module.validate_identity(value_fixture, lane, {"Fixture.Sdk"})
        for field, value in [("targetFramework", ".NETStandard,Version=v2.0"), ("sha256", "not-a-digest"), ("moduleVersionId", "missing"), ("moduleVersionId", "-" * 36), ("moduleVersionId", "00000000-0000-0000-0000-000000000000")]:
            value_fixture = fixture(lane); value_fixture["sdkAssets"][0][field] = value
            with self.assertRaises(ValueError): module.validate_identity(value_fixture, lane, {"Fixture.Sdk"})

    def test_trx_requires_completed_unique_unskipped_results_and_one_identity(self):
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / 'synthetic.trx'
            root = trx_fixture(report)
            identity, counts, ids = module.read_trx(report)
            self.assertIn('SYNTHETIC', identity['osDescription'])
            self.assertEqual(2, counts['passed'])
            inventory = Path(directory) / 'inventory.json'
            inventory.write_text(json.dumps(dict(schemaVersion=1, purpose='reviewed-runtime-test-inventory', tests=ids)))
            module.exact_test_inventory(inventory, ids)
            with self.assertRaises(ValueError): module.exact_test_inventory(inventory, ids[:-1])
            mutations = [
                lambda r: r.find('ResultSummary').set('outcome', 'Aborted'),
                lambda r: r.find('ResultSummary/Counters').set('total', '3'),
                lambda r: r.find('ResultSummary/Counters').set('notExecuted', '1'),
                lambda r: r.find('Results/UnitTestResult').set('outcome', 'NotExecuted'),
                lambda r: r.find('Results').remove(r.find('Results/UnitTestResult')),
                lambda r: r.find('Results').append(copy.deepcopy(r.find('Results/UnitTestResult'))),
                lambda r: r.find('Results/UnitTestResult[2]').set('testId', '11111111-1111-1111-1111-111111111111'),
                lambda r: r.find('Results/UnitTestResult[2]').set('executionId', '22222222-2222-2222-2222-222222222222'),
                lambda r: r.find('Results/UnitTestResult/Output/StdOut').clear(),
                lambda r: setattr(r.find('Results/UnitTestResult/Output/StdOut'), 'text', 'Runtime evidence: {}\nRuntime evidence: {}'),
            ]
            for mutate in mutations:
                changed = copy.deepcopy(root); mutate(changed)
                ET.ElementTree(changed).write(report, encoding='utf-8', xml_declaration=True)
                with self.assertRaises(ValueError): module.read_trx(report)

    def test_distinct_theories_can_share_truncated_display_name(self):
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / 'synthetic.trx'
            root = trx_fixture(report)
            results = root.find('Results')
            theory = copy.deepcopy(results[1])
            theory.set('testId', '55555555-5555-5555-5555-555555555555')
            theory.set('executionId', '66666666-6666-6666-6666-666666666666')
            results.append(theory)
            root.find('ResultSummary/Counters').set('total', '3')
            root.find('ResultSummary/Counters').set('passed', '3')
            ET.ElementTree(root).write(report, encoding='utf-8', xml_declaration=True)
            _, counts, ids = module.read_trx(report)
            self.assertEqual(3, counts['passed'])
            inventory = Path(directory) / 'inventory.json'
            inventory.write_text(json.dumps(dict(schemaVersion=1, purpose='reviewed-runtime-test-inventory', tests=ids)))
            module.exact_test_inventory(inventory, ids)
            with self.assertRaises(ValueError): module.exact_test_inventory(inventory, ids[:-1])

    def test_package_binding_rejects_wrong_asset_graph_and_artifact_hash(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            lane = next(r for r in matrix['lanes'] if r['id'] == 'linux-x64-net10.0')
            identity = fixture(lane)
            # These are synthetic bytes for checker tests, never candidate packages.
            payload = b'SYNTHETIC CHECKER UNIT FIXTURE, NOT A DLL'
            identity['sdkAssets'][0]['sha256'] = hashlib.sha256(payload).hexdigest()
            package = folder / 'synthetic.nupkg'
            with zipfile.ZipFile(package, 'w') as archive:
                archive.writestr('lib/net10.0/Fixture.Sdk.dll', payload)
                archive.writestr('Fixture.Sdk.nuspec', '<package><metadata><id>Fixture.Sdk</id><version>1.0.0</version></metadata></package>')
            graph = folder / 'synthetic.deps.json'
            graph.write_text(json.dumps(dict(libraries={'Fixture.Sdk/1.0.0': dict(type='package')})))
            value = dict(schemaVersion=1, packageVersion='1.0.0', packages=[dict(id='Fixture.Sdk', version='1.0.0', file=package.name, sha256=module.digest(package))], consumerDependencyGraph=dict(file=graph.name, sha256=module.digest(graph)))
            manifest = folder / 'manifest.json'
            manifest.write_text(json.dumps(value))
            module.package_bindings(manifest, identity, '1.0.0')
            extra = folder / 'unbound.nupkg'
            extra.write_bytes(b'SYNTHETIC UNBOUND ARTIFACT')
            with self.assertRaises(ValueError): module.package_bindings(manifest, identity, '1.0.0')
            extra.unlink()
            wrong_value = copy.deepcopy(value); wrong_value['packages'][0]['id'] = 'Fixture.Other'
            manifest.write_text(json.dumps(wrong_value))
            with self.assertRaises(ValueError): module.package_bindings(manifest, identity, '1.0.0')
            manifest.write_text(json.dumps(value))
            wrong = copy.deepcopy(identity); wrong['sdkAssets'][0]['sha256'] = '0' * 64
            with self.assertRaises(ValueError): module.package_bindings(manifest, wrong, '1.0.0')
            with self.assertRaises(ValueError): module.package_bindings(manifest, identity, '2.0.0')
            graph.write_text(json.dumps(dict(libraries={'Fixture.Sdk/1.0.0': dict(type='project')})))
            with self.assertRaises(ValueError): module.package_bindings(manifest, identity, '1.0.0')
            value['consumerDependencyGraph']['sha256'] = module.digest(graph)
            manifest.write_text(json.dumps(value))
            with self.assertRaises(ValueError): module.package_bindings(manifest, identity, '1.0.0')
            value['packages'][0]['sha256'] = '0' * 64
            manifest.write_text(json.dumps(value))
            with self.assertRaises(ValueError): module.package_bindings(manifest, identity, '1.0.0')

    def test_existing_output_or_input_alias_is_never_removed(self):
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / 'synthetic.trx'
            trx_fixture(report)
            original = report.read_bytes()
            process = subprocess.run([sys.executable, str(path), '--lane', matrix['lanes'][0]['id'], '--trx', str(report), '--output', str(report)], capture_output=True)
            self.assertNotEqual(0, process.returncode)
            self.assertIn(b'fresh-output-path-required', process.stderr)
            self.assertEqual(original, report.read_bytes())


if __name__ == "__main__":
    unittest.main()
