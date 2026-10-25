"""Offline candidate inspection. Never extracts, builds, downloads or publishes packages."""
import argparse
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import xml.etree.ElementTree as ET
import zipfile

TFMS = ('net472', 'netstandard2.0', 'net8.0', 'net10.0')
SDK_IDS = {'Fluent.Pdf' + suffix for suffix in ('', '.Workers', '.Codecs', '.Fonts', '.Text', '.Rendering',
           '.Cryptography', '.Compliance', '.Html', '.Word', '.Ocr', '.Formats', '.Intelligence', '.Automation', '.Applications')}
MAX_ENTRY = 64 * 1024 * 1024
MAX_TOTAL = 256 * 1024 * 1024


def digest(data):
    return hashlib.sha256(data).hexdigest()


def read_archive(path):
    with Path(path).open('rb') as stream:
        data = stream.read(MAX_TOTAL + 1)
    if len(data) > MAX_TOTAL:
        raise ValueError('Archive input limit')
    return data


def entries(data):
    if len(data) > MAX_TOTAL:
        raise ValueError('Archive input limit')
    result, total = {}, 0
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        if len(archive.infolist()) > 10000:
            raise ValueError('Archive entry limit')
        folded = set()
        for item in archive.infolist():
            name = item.filename
            if item.orig_filename != name or not name or '\\' in name or ':' in name or name.startswith('/') or any(p in ('..', '.') for p in name.split('/')) or item.is_dir():
                raise ValueError('Unsafe or directory archive entry')
            if name.casefold() in folded or str(PurePosixPath(name)) != name:
                raise ValueError('Duplicate or ambiguous archive entry')
            if item.flag_bits & 1 or (item.external_attr >> 16) & 0o170000 == 0o120000:
                raise ValueError('Encrypted or symlink archive entry')
            total += item.file_size
            if item.file_size > MAX_ENTRY or total > MAX_TOTAL:
                raise ValueError('Expanded archive limit')
            folded.add(name.casefold())
            result[name] = archive.read(item)  # ZIP CRC checked; precharged declared size.
    return result


def normalized_tfm(value):
    return {'.NETFramework4.7.2': 'net472', '.NETStandard2.0': 'netstandard2.0'}.get(value, value)


def inspect(data, expected, symbols=False):
    files = entries(data)
    name, version, commit = expected['id'], expected['version'], expected['sourceCommit']
    spec_name = name + '.nuspec'
    if spec_name not in files or len(files[spec_name]) > 1024 * 1024:
        raise ValueError('Missing or oversized manifest')
    xml = files[spec_name]
    if b'<!' in xml:
        raise ValueError('XML declarations/entities are unsupported')
    root = ET.fromstring(xml)
    for node in root.iter():
        node.tag = node.tag.rsplit('}', 1)[-1]
    metadata = root.find('metadata')
    if metadata is None or metadata.findtext('id') != name or metadata.findtext('version') != version:
        raise ValueError('Package identity/version mismatch')
    repository = metadata.find('repository')
    if repository is None or repository.get('type') != 'git' or repository.get('url') != 'https://github.com/kirakosyan/pdf.net' or repository.get('commit') != commit:
        raise ValueError('Repository/commit mismatch')
    license_node = metadata.find('license')
    if license_node is None or license_node.get('type') != 'expression' or license_node.text != 'MIT':
        raise ValueError('License metadata mismatch')
    if not symbols:
        if metadata.findtext('readme') != 'README.md':
            raise ValueError('Missing README metadata')
        groups = metadata.findall('dependencies/group')
        actual = {}
        for group in groups:
            tfm = normalized_tfm(group.get('targetFramework'))
            if tfm in actual:
                raise ValueError('Duplicate dependency group')
            deps = {}
            for dep in group:
                key = dep.get('id')
                if dep.tag != 'dependency' or key in deps:
                    raise ValueError('Duplicate/unsupported dependency')
                deps[key] = dep.get('version')
            actual[tfm] = deps
        if actual != expected['dependencies']:
            raise ValueError('Unreviewed dependency graph or version range')
    required = expected['symbols' if symbols else 'files']
    for path, sha in required.items():
        if path not in files or digest(files[path]) != sha:
            raise ValueError('Missing or changed qualified entry: ' + path)
    # NuGet container metadata may vary, but every payload byte must be reviewed.
    containers = {'[Content_Types].xml', '_rels/.rels', spec_name}
    core_properties = [p for p in files if re.fullmatch(r'package/services/metadata/core-properties/[A-Za-z0-9]+\.psmdcp', p)]
    if len(core_properties) != 1 or not containers.issubset(files):
        raise ValueError('Incomplete NuGet container')
    allowed = set(required) | containers | set(core_properties)
    if set(files) != allowed:
        raise ValueError('Unreviewed package payload')
    return {'id': name, 'version': version, 'symbols': symbols, 'sha256': digest(data),
            'entries': {p: digest(v) for p, v in sorted(files.items())}}


def compare(first, second):
    # Byte equality includes ZIP metadata, not just decompressed payload hashes.
    if digest(first) != digest(second):
        raise ValueError('Rebuild archive bytes differ')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', required=True)
    parser.add_argument('--directory', required=True)
    parser.add_argument('--rebuild-directory', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    output = Path(args.output)
    output.unlink(missing_ok=True)
    manifest = json.loads(Path(args.manifest).read_text(encoding='utf-8-sig'))
    if manifest.get('schemaVersion') != 1 or not re.fullmatch('[0-9a-f]{40}', manifest.get('sourceCommit', '')):
        raise ValueError('Invalid source binding')
    projects = manifest['packages']
    ids = [p['id'] for p in projects]
    if len(ids) != len(SDK_IDS) or set(ids) != SDK_IDS:
        raise ValueError('Complete SDK inventory required')
    reports, names = [], set()
    for project in projects:
        if project['sourceCommit'] != manifest['sourceCommit'] or not re.fullmatch(r'Fluent\.Pdf(?:\.[A-Za-z]+)?', project['id']) or not re.fullmatch(r'[1-9][0-9]*\.[0-9]+\.[0-9]+', project['version']):
            raise ValueError('Invalid candidate identity')
        for symbols in (False, True):
            filename = project['id'] + '.' + project['version'] + ('.snupkg' if symbols else '.nupkg')
            names.add(filename)
            first, second = (read_archive(Path(d).joinpath(filename)) for d in (args.directory, args.rebuild_directory))
            reports.append(inspect(first, project, symbols))
            inspect(second, project, symbols)
            compare(first, second)
    for directory in (args.directory, args.rebuild_directory):
        if {p.name for p in Path(directory).iterdir()} != names:
            raise ValueError('Unbound candidate directory entries')
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps({'schemaVersion': 1, 'sourceCommit': manifest['sourceCommit'],
                                 'manifestSha256': digest(Path(args.manifest).read_bytes()),
                                 'packages': reports, 'productionQualified': False}, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
