"""Offline source inventory and CycloneDX 1.6 SBOM. No redistribution approval implied."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET
import zipfile


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def prop(name, value):
    return {'name': 'fluent-pdf:' + name, 'value': str(value)}


def lock_inventory(paths, root):
    packages, contexts = {}, []
    for path in sorted(paths):
        lock = json.loads(path.read_text(encoding='utf-8-sig'))
        if lock.get('version') != 1:
            raise ValueError('Unsupported lock schema')
        for tfm, dependencies in lock['dependencies'].items():
            ids = []
            for name, item in dependencies.items():
                if item['type'] == 'Project':
                    continue
                if item['type'] not in ('Direct', 'Transitive') or not re.fullmatch(r'[A-Za-z0-9_.-]+', name):
                    raise ValueError('Unsupported dependency identity/type')
                version = item['resolved']
                if not re.fullmatch(r'[A-Za-z0-9.+-]+', version):
                    raise ValueError('Unsafe dependency version')
                digest = item['contentHash']
                if len(base64.b64decode(digest, validate=True)) != 64:
                    raise ValueError('Invalid NuGet content hash')
                key = name.lower() + '/' + version
                previous = packages.get(key)
                if previous and previous['contentHash'] != digest:
                    raise ValueError('Same identity has conflicting locked bytes')
                record = packages.setdefault(key, {'id': name, 'version': version, 'contentHash': digest, 'contexts': []})
                context = {'lock': path.relative_to(root).as_posix(), 'tfm': tfm, 'type': item['type']}
                if context not in record['contexts']:
                    record['contexts'].append(context)
                ids.append(key)
            contexts.append({'lock': path.relative_to(root).as_posix(), 'tfm': tfm, 'packages': sorted(ids)})
    return packages, contexts


def package_license(path, expected_id, expected_version):
    # Inspect only bounded metadata/license members, never extract arbitrary paths.
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        if len(names) > 20000 or len(set(n.casefold() for n in names)) != len(names):
            raise ValueError('Ambiguous or oversized dependency archive')
        manifests = [n for n in names if '/' not in n and n.lower().endswith('.nuspec')]
        if len(manifests) != 1:
            raise ValueError('Missing unique package manifest')
        def read(name):
            if archive.getinfo(name).file_size > 2 * 1024 * 1024:
                raise ValueError('License/metadata size limit')
            return archive.read(name)
        content = read(manifests[0])
        if b'<!' in content:
            raise ValueError('Unsupported XML declaration/entity')
        xml = ET.fromstring(content)
        for node in xml.iter():
            node.tag = node.tag.rsplit('}', 1)[-1]
        metadata = xml.find('metadata')
        if metadata is None or metadata.findtext('id', '').lower() != expected_id.lower() or metadata.findtext('version') != expected_version:
            raise ValueError('Dependency metadata identity differs from lock')
        license_node = metadata.find('license')
        result = {'declaration': None, 'type': None, 'noticeEntries': []}
        if license_node is not None:
            result.update(declaration=license_node.text, type=license_node.get('type'))
            if license_node.get('type') == 'file' and license_node.text not in names:
                raise ValueError('Declared license file absent')
        for name in names:
            if name.endswith('/'):
                continue
            leaf = name.rsplit('/', 1)[-1].lower()
            if leaf.startswith(('license', 'notice', 'thirdparty', 'third-party')) or license_node is not None and license_node.get('type') == 'file' and name == license_node.text:
                data = read(name)
                result['noticeEntries'].append({'entry': name, 'sha256': hashlib.sha256(data).hexdigest(), 'bytes': len(data)})
        return result


def changed_inputs(previous, current):
    before = {p['path']: p['sha256'] for p in previous['inputs']}
    after = {p['path']: p['sha256'] for p in current['inputs']}
    return sorted(p for p in set(before) | set(after) if before.get(p) != after.get(p))


def declarations(value, pointer=''):
    if isinstance(value, dict):
        for key, item in value.items():
            child = pointer + '/' + key.replace('~', '~0').replace('/', '~1')
            if key.lower().endswith('sha256') and isinstance(item, str):
                if not re.fullmatch('[a-fA-F0-9]{64}', item):
                    raise ValueError('Invalid declared asset hash at ' + child)
                yield child, item.lower()
            yield from declarations(item, child)
    elif isinstance(value, list):
        for i, item in enumerate(value):
            yield from declarations(item, pointer + '/' + str(i))


def generate(root, cache, output, probe, asset_directories=(), previous=None):
    if output.exists():
        raise ValueError('Use a new output directory; never replace retained evidence')
    output.mkdir(parents=True)
    tracked = subprocess.check_output(['git', '-C', str(root), 'ls-files', '--cached', '--others', '--exclude-standard'], text=True).splitlines()
    locks = sorted({root / p for p in tracked if p.endswith('/packages.lock.json')})
    packages, contexts = lock_inventory(locks, root)
    notices = sorted(p for p in (root / 'third-party').rglob('*') if p.is_file())
    configuration = [root / p for p in tracked if p.endswith(('.csproj', '.vbproj', '.props', '.targets')) or p in ('global.json', 'NuGet.Config')]
    inputs = [{'path': p.relative_to(root).as_posix(), 'sha256': sha(p)} for p in sorted(set(locks + notices + configuration))]
    requests, available = [], {}
    for key, item in sorted(packages.items()):
        name, version = key.split('/')
        path = cache / name / version / (name + '.' + version + '.nupkg')
        if path.is_file():
            requests.append({'path': str(path), 'id': item['id'], 'version': version, 'contentHash': item['contentHash']})
            available[key] = path
    request = output / 'nuget-request.json'
    request.write_text(json.dumps(requests, indent=2) + '\n', encoding='utf-8')
    subprocess.run(['dotnet', str(probe), str(request), str(output / 'nuget-probe.json')], check=True)
    checked = json.loads((output / 'nuget-probe.json').read_text(encoding='utf-8'))
    checked_rows = {p['id'].lower() + '/' + p['version']: p for p in checked['packages']}
    if set(checked_rows) != set(available):
        raise ValueError('Incomplete NuGet content verification')
    components = []
    for key, item in sorted(packages.items()):
        item['verification'] = 'unavailable' if key not in available else 'NuGet-content-hash-matched'
        component = {'type': 'library', 'bom-ref': 'pkg:nuget/' + key.replace('/', '@'),
                     'name': item['id'], 'version': item['version'],
                     'purl': 'pkg:nuget/' + key.replace('/', '@'),
                     'properties': [prop('locked-content-sha512-base64', item['contentHash']),
                                    prop('verification', item['verification']), prop('scope', 'source-build-inventory-not-final-distribution')]}
        if key in available:
            item['archiveSha256'] = checked_rows[key]['archiveSha256']
            item['license'] = package_license(available[key], item['id'], item['version'])
            component['hashes'] = [{'alg': 'SHA-256', 'content': item['archiveSha256']}]
            declaration = item['license']['declaration']
            if declaration:
                # Preserve upstream text, without interpreting license compatibility or granting rights.
                component['licenses'] = [{'license': {'name': declaration}}]
        components.append(component)
    declared = []
    for path in notices:
        relative = path.relative_to(root).as_posix()
        components.append({'type': 'file', 'bom-ref': relative, 'name': relative,
                           'hashes': [{'alg': 'SHA-256', 'content': sha(path)}],
                           'properties': [prop('role', 'retained-declaration-or-notice; not proof of external asset bytes')]})
        if path.suffix.lower() == '.json':
            for pointer, digest in declarations(json.loads(path.read_text(encoding='utf-8-sig'))):
                declared.append({'manifest': relative, 'pointer': pointer, 'sha256': digest})
    by_hash = {}
    total_bytes, total_files = 0, 0
    for directory in asset_directories:
        for path in sorted(directory.rglob('*')):
            if not path.is_file() or path.is_symlink():
                continue
            total_files += 1
            total_bytes += path.stat().st_size
            if total_files > 10000 or total_bytes > 10 * 1024**3:
                raise ValueError('Explicit asset inventory limit')
            by_hash.setdefault(sha(path), []).append(str(path))
    matches = [dict(d, observedPaths=by_hash[d['sha256']]) for d in declared if d['sha256'] in by_hash]
    commit = subprocess.check_output(['git', '-C', str(root), 'rev-parse', 'HEAD'], text=True).strip()
    report = {'schemaVersion': 1, 'sourceCommit': commit,
              'workingTreeDirty': bool(subprocess.check_output(['git', '-C', str(root), 'status', '--porcelain'], text=True).strip()),
              'inputs': inputs, 'packages': list(packages.values()), 'contexts': contexts,
              'declarations': declared, 'observedDeclarationMatches': matches,
              'missingPackageCount': len(packages) - len(available), 'productionQualified': False,
              'limitations': ['NuGet content hashes and raw archive hashes are distinct; signer trust is not verified',
                              'License text and origin fields are upstream declarations, not redistribution approval',
                              'Source build graphs include tools/tests and do not represent final package dependency scope',
                              'Only explicitly supplied local asset directories were inspected; all other native/model hashes remain declarations']}
    if previous is not None:
        changes = changed_inputs(previous, report)
        report['dependencyChanges'] = changes
        report['requiresAllApplicableRuntimeRidFidelityQualification'] = bool(changes)
    bom = {'bomFormat': 'CycloneDX', 'specVersion': '1.6', 'version': 1,
           'metadata': {'component': {'type': 'application', 'name': 'Fluent.Pdf source inventory', 'version': commit},
                        'properties': [prop('production-qualified', 'false'), prop('distribution-complete', 'false')]},
           'components': components, 'compositions': [{'aggregate': 'incomplete'}]}
    (output / 'inventory.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    (output / 'bom.cdx.json').write_text(json.dumps(bom, indent=2) + '\n', encoding='utf-8')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--cache', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--probe', type=Path, required=True)
    parser.add_argument('--asset-directory', type=Path, action='append', default=[])
    parser.add_argument('--previous', type=Path)
    args = parser.parse_args()
    prior = json.loads(args.previous.read_text(encoding='utf-8-sig')) if args.previous else None
    result = generate(args.root.resolve(), args.cache.resolve(), args.output.resolve(), args.probe.resolve(),
                      [p.resolve() for p in args.asset_directory], prior)
    print(f"SOURCE_INVENTORY_PASS;{len(result['packages'])}-locked-packages;{result['missingPackageCount']}-unavailable;unqualified")
