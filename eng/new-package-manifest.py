"""Bind reviewed source-build bytes to a candidate content manifest; does not pack."""
import argparse
import importlib.util
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location('package_audit', Path(__file__).with_name('package-audit.py'))
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


def create(root, output):
    commit = subprocess.check_output(['git', '-C', str(root), 'rev-parse', 'HEAD'], text=True).strip()
    dirty = bool(subprocess.check_output(['git', '-C', str(root), 'status', '--porcelain'], text=True).strip())
    if dirty:
        raise ValueError('Candidate manifest requires clean committed source')
    readiness = json.loads((root / 'specs/release-readiness.json').read_text(encoding='utf-8-sig'))
    version = readiness['packageVersion']
    targets = ET.parse(root / 'src/Directory.Build.targets')
    if targets.findtext('./PropertyGroup/Version') != version:
        raise ValueError('Shared package version differs from readiness')
    common = {p.name: audit.digest(p.read_bytes()) for p in (root / 'README.md', root / 'LICENSE')}
    for path in (root / 'third-party').rglob('*'):
        if path.suffix in ('.txt', '.md', '.json'):
            common['notices/' + path.relative_to(root / 'third-party').as_posix()] = audit.digest(path.read_bytes())
    projects = sorted(p.parent for p in (root / 'src').glob('*/PublicAPI.Shipped.txt'))
    rows = []
    for project in projects:
        name = project.name
        xml = ET.parse(project / (name + '.csproj'))
        for group in xml.findall('ItemGroup'):
            if group.get('Condition') and any(p.tag == 'ProjectReference' or p.tag == 'PackageReference' and p.get('PrivateAssets') != 'all' for p in group):
                raise ValueError('Conditional runtime dependencies require explicit inventory support')
        dependencies = {Path(p.get('Include').replace('\\', '/')).stem: '[' + version + ']'
                        for p in xml.findall('.//ProjectReference')}
        for p in xml.findall('.//PackageReference'):
            if p.get('PrivateAssets') == 'all':
                continue
            # Current runtime references are unconditional; future conditional additions fail review.
            if p.get('Condition'):
                raise ValueError('Conditional dependency requires explicit inventory support')
            dependencies[p.get('Include')] = p.get('Version')
        groups = {tfm: dict(dependencies) for tfm in audit.TFMS}
        groups['netstandard2.0']['NETStandard.Library'] = '2.0.3'
        files, symbols = dict(common), {}
        for tfm in audit.TFMS:
            directory = project / 'bin/Release' / tfm
            for ext in ('.dll', '.xml'):
                files[f'lib/{tfm}/{name}{ext}'] = audit.digest((directory / (name + ext)).read_bytes())
            symbols[f'lib/{tfm}/{name}.pdb'] = audit.digest((directory / (name + '.pdb')).read_bytes())
        rows.append({'id': name, 'version': version, 'sourceCommit': commit,
                     'files': files, 'symbols': symbols, 'dependencies': groups})
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps({'schemaVersion': 1, 'sourceCommit': commit, 'packages': rows,
                                 'productionQualified': False}, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    args.output.unlink(missing_ok=True)
    create(args.root.resolve(), args.output)
