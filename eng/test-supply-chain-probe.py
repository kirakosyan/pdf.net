"""Reject incorrect locks and tampered cached input without changing the shared cache."""
import argparse
import json
from pathlib import Path
import subprocess
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--probe', required=True)
parser.add_argument('--requests', required=True, type=Path)
parser.add_argument('--output', required=True, type=Path)
args = parser.parse_args()
if args.output.exists():
    raise ValueError('Use a new negative-test directory')
args.output.mkdir(parents=True)
requests = json.loads(args.requests.read_text(encoding='utf-8-sig'))
original = next(p for p in requests if p['id'].lower() == 'system.text.json')
cases = []
for mode in ('wrong-lock-hash', 'wrong-identity', 'tampered-content'):
    sample = dict(original)
    if mode == 'wrong-lock-hash':
        sample['contentHash'] = 'A' * 86 + '=='
    elif mode == 'wrong-identity':
        sample['id'] = 'Different.Package'
    else:
        altered = args.output / 'tampered.zip'
        with zipfile.ZipFile(original['path']) as source, zipfile.ZipFile(altered, 'w') as destination:
            changed = False
            for entry in source.infolist():
                data = source.read(entry)
                if not changed and entry.filename.endswith('.dll'):
                    data = data[:-1] + bytes([data[-1] ^ 1])
                    changed = True
                destination.writestr(entry, data)
        if not changed:
            raise ValueError('No binary fixture was mutated')
        sample['path'] = str(altered.resolve())
    request = args.output / (mode + '-request.json')
    request.write_text(json.dumps([sample]), encoding='utf-8')
    report = args.output / (mode + '-result.json')
    report.write_text('stale success', encoding='utf-8')
    result = subprocess.run(['dotnet', args.probe, str(request), str(report)], capture_output=True, text=True)
    (args.output / (mode + '.log')).write_text(result.stdout + result.stderr, encoding='utf-8')
    if result.returncode == 0 or report.exists():
        raise AssertionError('Incorrect input accepted or stale report survived: ' + mode)
    cases.append({'case': mode, 'rejected': True, 'staleSuccessRemoved': True})
(args.output / 'report.json').write_text(json.dumps(cases, indent=2) + '\n', encoding='utf-8')
print('SUPPLY_CHAIN_PROBE_NEGATIVES_PASS;3')
