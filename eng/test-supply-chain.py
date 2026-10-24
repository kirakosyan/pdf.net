import base64
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
import zipfile


def module(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + '.py'))
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


inventory = module('supply-chain')
advisories = module('check-vulnerabilities')


class SupplyChainTests(unittest.TestCase):
    def test_lock_identity_deduplication_and_contexts(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            item = {'type': 'Direct', 'resolved': '1.0.0', 'contentHash': base64.b64encode(bytes(64)).decode()}
            path = root / 'packages.lock.json'
            path.write_text(json.dumps({'version': 1, 'dependencies': {'net8.0': {'Example': item}, 'net10.0': {'Example': item}}}))
            packages, contexts = inventory.lock_inventory([path], root)
            self.assertEqual(1, len(packages))
            self.assertEqual(2, len(contexts))
            self.assertEqual(2, len(packages['example/1.0.0']['contexts']))

    def test_conflicting_hashes_and_unsafe_identities_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            item = {'type': 'Direct', 'resolved': '1.0.0', 'contentHash': base64.b64encode(bytes(64)).decode()}
            for name, version, digest in (('../unsafe', '1.0.0', item['contentHash']), ('Example', '../unsafe', item['contentHash']), ('Example', '1.0.0', 'bad')):
                path = root / 'packages.lock.json'
                path.write_text(json.dumps({'version': 1, 'dependencies': {'net8.0': {name: dict(item, resolved=version, contentHash=digest)}}}))
                with self.assertRaises(ValueError):
                    inventory.lock_inventory([path], root)
            paths = [root / 'one.json', root / 'two.json']
            for number, path in enumerate(paths):
                path.write_text(json.dumps({'version': 1, 'dependencies': {'net8.0': {'Example': dict(item, contentHash=base64.b64encode(bytes([number])*64).decode())}}}))
            with self.assertRaises(ValueError):
                inventory.lock_inventory(paths, root)

    def test_changed_added_removed_dependency_inputs_require_requalification(self):
        before = {'inputs': [{'path': 'a', 'sha256': '1'}, {'path': 'b', 'sha256': '2'}]}
        after = {'inputs': [{'path': 'a', 'sha256': '3'}, {'path': 'c', 'sha256': '4'}]}
        self.assertEqual(['a', 'b', 'c'], inventory.changed_inputs(before, after))
        self.assertEqual([], inventory.changed_inputs(before, before))

    def test_native_model_hashes_remain_pointer_bound_declarations(self):
        data = {'models': [{'sha256': 'a'*64}], 'packageSha256': 'b'*64}
        self.assertEqual([('/models/0/sha256', 'a'*64), ('/packageSha256', 'b'*64)], list(inventory.declarations(data)))
        with self.assertRaises(ValueError):
            list(inventory.declarations({'models': [{'sha256': 'invalid'}]}))

    def test_license_file_and_notice_hashes(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'synthetic.zip'
            with zipfile.ZipFile(path, 'w') as archive:
                archive.writestr('Example.nuspec', '<package><metadata><id>Example</id><version>1.0.0</version><license type="file">LICENSE.txt</license></metadata></package>')
                archive.writestr('LICENSE.txt', 'retained original notice')
            result = inventory.package_license(path, 'Example', '1.0.0')
            self.assertEqual('file', result['type'])
            self.assertEqual(1, len(result['noticeEntries']))
            with self.assertRaises(ValueError):
                inventory.package_license(path, 'Different', '1.0.0')

    def test_missing_license_or_entity_does_not_pass(self):
        for xml in ('<!DOCTYPE package><package/>', '<package><metadata><id>Example</id><version>1.0.0</version><license type="file">missing.txt</license></metadata></package>'):
            with tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / 'synthetic.zip'
                with zipfile.ZipFile(path, 'w') as archive:
                    archive.writestr('Example.nuspec', xml)
                with self.assertRaises(ValueError):
                    inventory.package_license(path, 'Example', '1.0.0')

    def test_advisory_coverage_errors_and_findings_fail_closed(self):
        report = {'version': 1, 'sources': ['explicit-source'], 'projects': [{'path': 'project.csproj'}]}
        self.assertEqual(0, advisories.check(report, ['project.csproj'])['returnedFindings'])
        for invalid in (dict(report, problems=[{'level': 'error'}]), dict(report, sources=[]), dict(report, projects=[]), dict(report, projects=report['projects']*2)):
            with self.assertRaises(ValueError):
                advisories.check(invalid, ['project.csproj'])
        finding = {'id': 'Example', 'resolvedVersion': '1.0.0', 'vulnerabilities': [{'severity': 'Critical', 'advisoryurl': 'https://example.invalid/advisory'}]}
        for group in ('topLevelPackages', 'transitivePackages'):
            report['projects'][0]['frameworks'] = [{'framework': 'net10.0', group: [finding]}]
            with self.assertRaises(ValueError):
                advisories.check(report, ['project.csproj'])


if __name__ == '__main__':
    unittest.main()
