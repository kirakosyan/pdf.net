"""Independent standard-library scalar reference for the seven original cleanup fixtures."""
import argparse
import hashlib
import json
from pathlib import Path


def verify(directory):
    rows = json.loads((directory / 'observations.json').read_text(encoding='utf-8-sig'))
    names = ['white', 'near-white', 'noise', 'faint-text', 'border', 'color', 'resize']
    if [r['name'] for r in rows] != names:
        raise ValueError('Exact original fixture inventory required')
    records = []
    for row in rows:
        name = row['name']
        before = (directory / (name + '-before.raw')).read_bytes()
        after = (directory / (name + '-after.raw')).read_bytes()
        channels = 3 if name == 'color' else 1
        original = bytearray([255] * (16 * 12 * channels))
        if name == 'near-white':
            original[:] = bytes([248] * len(original))
        elif name == 'noise':
            original[6 * 16 + 8] = 0
        elif name == 'faint-text':
            for y in range(3, 9):
                original[y * 16 + 8] = 220
        elif name == 'border':
            for y in range(12):
                for x in range(16):
                    if y in (0, 11) or x in (0, 15):
                        original[y * 16 + x] = 0
        elif name == 'color':
            original[:] = bytes([20, 80, 160] * 192)
        elif name == 'resize':
            original[:] = bytes((x * 11 + y * 7) % 256 for y in range(12) for x in range(16))
        if before != original:
            raise ValueError('Source preservation or corpus mismatch: ' + name)
        width, height = 16, 12
        expected = original
        if name == 'noise':
            expected = bytes(sorted(original[max(0, min(11, y + dy)) * 16 + max(0, min(15, x + dx))]
                                    for dy in (-1, 0, 1) for dx in (-1, 0, 1))[4]
                             for y in range(12) for x in range(16))
        elif name == 'faint-text':
            expected = bytes(0 if b < 200 else 255 for b in original)
        elif name == 'border':
            width, height = 14, 10
            expected = bytes(original[y * 16 + x] for y in range(1, 11) for x in range(1, 15))
        elif name == 'resize':
            width, height = 8, 6
            expected = bytes(original[(2 * y + 1) * 16 + 2 * x + 1] for y in range(6) for x in range(8))
        if after != expected or (row['width'], row['height'], row['channels']) != (width, height, channels):
            raise ValueError('Independent expected pixel/geometry mismatch: ' + name)
        bg = [20, 80, 160] if channels == 3 else [255]
        def foreground(data):
            return sum(max(abs(data[i + c] - bg[c]) for c in range(channels)) > 12 for i in range(0, len(data), channels))
        first, last = foreground(before), foreground(after)
        if (row['beforeForeground'], row['afterForeground']) != (first, last) or abs(row['confidence'] - (1 - first / 192)) > 1e-12:
            raise ValueError('Incorrect foreground/confidence evidence: ' + name)
        records.append({'fixture': name, 'beforeSha256': hashlib.sha256(before).hexdigest(),
                        'afterSha256': hashlib.sha256(after).hexdigest(), 'originalPixels': 192,
                        'outputPixels': width * height, 'beforeForeground': first, 'afterForeground': last,
                        'referenceMismatchPixels': 0,
                        'sameGeometryMeanAbsoluteError': sum(abs(a - b) for a, b in zip(before, after)) / len(before) if len(before) == len(after) else None})
    return {'schemaVersion': 1, 'productionQualified': False, 'ocrAccuracyMeasured': False,
            'confidenceCalibrated': False, 'observations': records}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--directory', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    report = verify(args.directory)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('Verified seven original cleanup pixel/geometry/measurement observations.')
