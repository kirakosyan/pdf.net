"""Synthetic in-memory ZIP boundary tests; no SDK NuGet packages are produced."""
import copy
import importlib.util
import io
from pathlib import Path
import unittest
import warnings
import zipfile

spec = importlib.util.spec_from_file_location('package_audit', Path(__file__).with_name('package-audit.py'))
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class AuditTests(unittest.TestCase):
    def setUp(self):
        self.expected = {'id': 'Fluent.Pdf', 'version': '1.0.0', 'sourceCommit': 'a' * 40,
                         'files': {'README.md': audit.digest(b'readme'), 'LICENSE': audit.digest(b'MIT')},
                         'symbols': {}, 'dependencies': {tfm: {} for tfm in audit.TFMS}}
        self.files = {'README.md': b'readme', 'LICENSE': b'MIT', '[Content_Types].xml': b'container',
                      '_rels/.rels': b'rels', 'package/services/metadata/core-properties/abc.psmdcp': b'metadata'}
        for tfm in audit.TFMS:
            for ext in ('.dll', '.xml'):
                path = f'lib/{tfm}/Fluent.Pdf{ext}'
                self.files[path] = (tfm + ext).encode()
                self.expected['files'][path] = audit.digest(self.files[path])
            self.expected['symbols'][f'lib/{tfm}/Fluent.Pdf.pdb'] = audit.digest(b'portable fixture')
        groups = ''.join(f'<group targetFramework="{t}" />' for t in audit.TFMS)
        self.files['Fluent.Pdf.nuspec'] = f'<package><metadata><id>Fluent.Pdf</id><version>1.0.0</version><license type="expression">MIT</license><readme>README.md</readme><repository type="git" url="https://github.com/kirakosyan/pdf.net" commit="{"a"*40}"/><dependencies>{groups}</dependencies></metadata></package>'.encode()

    def archive(self, files=None):
        output = io.BytesIO()
        with zipfile.ZipFile(output, 'w') as archive:
            for name, data in (self.files if files is None else files).items():
                archive.writestr(name, data)
        return output.getvalue()

    def test_manifest_and_all_bound_payloads(self):
        report = audit.inspect(self.archive(), self.expected)
        self.assertEqual(len(self.files), len(report['entries']))
        symbols = {k: v for k, v in self.files.items() if k not in self.expected['files']}
        symbols.update({k: b'portable fixture' for k in self.expected['symbols']})
        self.assertTrue(audit.inspect(self.archive(symbols), self.expected, True)['symbols'])

    def test_missing_and_changed_payloads(self):
        for name in self.expected['files']:
            for missing in (True, False):
                files = self.files.copy()
                if missing:
                    del files[name]
                else:
                    files[name] += b'changed'
                with self.subTest(name=name, missing=missing), self.assertRaises(ValueError):
                    audit.inspect(self.archive(files), self.expected)

    def test_unreviewed_native_build_or_dependency_payloads(self):
        for name in ('runtimes/win-x64/native/engine.dll', 'build/run.targets', 'lib/net8.0/Other.dll'):
            files = dict(self.files, **{name: b'unreviewed'})
            with self.subTest(name=name), self.assertRaises(ValueError):
                audit.inspect(self.archive(files), self.expected)

    def test_wrong_commit_version_license_and_dependency(self):
        for old, new in ((b'a'*40, b'b'*40), (b'<version>1.0.0', b'<version>2.0.0'),
                         (b'>MIT<', b'>unknown<'), (b'<group targetFramework="net8.0" />', b'<group targetFramework="net8.0"><dependency id="Browser" version="1.0"/></group>')):
            files = self.files.copy()
            files['Fluent.Pdf.nuspec'] = files['Fluent.Pdf.nuspec'].replace(old, new)
            with self.subTest(new=new), self.assertRaises(ValueError):
                audit.inspect(self.archive(files), self.expected)

    def test_archive_paths_duplicates_case_collisions_and_symlinks(self):
        for name in ('../escape', '/root', 'C:/drive', 'a//b', './a', 'folder/'):
            with self.subTest(name=name), self.assertRaises(ValueError):
                audit.entries(self.archive({name: b'x'}))
        # Windows ZipInfo normalizes backslashes while writing; mutate both headers.
        with self.assertRaises(ValueError):
            audit.entries(self.archive({'a/b': b'x'}).replace(b'a/b', b'a\\b'))
        for name in ('README.md', 'readme.md'):
            output = io.BytesIO()
            with warnings.catch_warnings():
                warnings.simplefilter('ignore', UserWarning)
                with zipfile.ZipFile(output, 'w') as archive:
                    archive.writestr('README.md', b'x')
                    archive.writestr(name, b'x')
            with self.assertRaises(ValueError):
                audit.entries(output.getvalue())
        output = io.BytesIO()
        with zipfile.ZipFile(output, 'w') as archive:
            entry = zipfile.ZipInfo('link')
            entry.external_attr = 0o120777 << 16
            archive.writestr(entry, b'/elsewhere')
        with self.assertRaises(ValueError):
            audit.entries(output.getvalue())

    def test_expansion_is_bounded_before_read(self):
        previous = audit.MAX_ENTRY
        try:
            audit.MAX_ENTRY = 4
            with self.assertRaises(ValueError):
                audit.entries(self.archive({'a': b'12345'}))
        finally:
            audit.MAX_ENTRY = previous

    def test_exact_rebuild_includes_container_bytes(self):
        data = self.archive()
        audit.compare(data, data)
        with self.assertRaises(ValueError):
            audit.compare(data, data + b'changed')

    def test_xml_entities_and_duplicate_groups_rejected(self):
        for payload in (b'<!DOCTYPE package><package/>', self.files['Fluent.Pdf.nuspec'].replace(b'</dependencies>', b'<group targetFramework="net8.0"/></dependencies>')):
            files = self.files.copy()
            files['Fluent.Pdf.nuspec'] = payload
            with self.assertRaises(ValueError):
                audit.inspect(self.archive(files), self.expected)


if __name__ == '__main__':
    unittest.main()
