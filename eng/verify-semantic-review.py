"""Independent ledger binding checks; optional pypdf/MuPDF source observations."""
import argparse
import hashlib
import json
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify(directory, readers=False):
    reports = []
    pairs = [('ledger.json', 'reviewed.pdf'), ('csharp.json', 'csharp.pdf'), ('visual-basic.json', 'visual-basic.pdf'),
             ('author-Ua1.json', 'author-Ua1.pdf'), ('author-Ua2.json', 'author-Ua2.pdf'),
             ('validator-Ua1.json', 'validator-Ua1.pdf'), ('validator-Ua2.json', 'validator-Ua2.pdf')]
    for ledger_name, pdf_name in pairs:
        path = directory / ledger_name
        ledger = json.loads(path.read_text(encoding='utf-8'))
        if ledger['schemaVersion'] != 1 or ledger['conformanceVerified'] is not False or ledger['humanReviewRequired'] is not True:
            raise ValueError('Ledger cannot claim conformance or completed human review')
        if ledger['afterSha256'] != digest(directory / pdf_name):
            raise ValueError('Output digest mismatch')
        expected = ledger['beforeSha256']
        for entry in ledger['entries']:
            if entry['beforeSha256'] != expected:
                raise ValueError('Broken ordered ledger chain')
            if entry['status'] not in ('pending', 'rejected', 'applied'):
                raise ValueError('Unknown disposition')
            if entry['status'] == 'applied':
                if not entry['reviewer'] or not entry['note'] or not entry['affectedObjects']:
                    raise ValueError('Applied fix lacks explicit decision or location')
            elif entry['beforeSha256'] != entry['afterSha256']:
                raise ValueError('Unaccepted proposal changed bytes')
            if entry['status'] == 'pending' and (entry['reviewer'] is not None or entry['note'] is not None):
                raise ValueError('Pending suggestion fabricated reviewer')
            expected = entry['afterSha256']
        if expected != ledger['afterSha256']:
            raise ValueError('Final ledger binding mismatch')
        if ledger_name == 'ledger.json' and [e['status'] for e in ledger['entries']] != ['applied', 'applied', 'rejected', 'pending']:
            raise ValueError('Missing original ordered review outcomes')
        reports.append({'ledger': ledger_name, 'sha256': digest(path), 'pdf': pdf_name, 'pdfSha256': digest(directory / pdf_name), 'steps': len(ledger['entries'])})
    versions = {}
    if readers:
        import pypdf
        import pymupdf
        versions = {'pypdf': pypdf.__version__, 'PyMuPDF': pymupdf.VersionBind}
        original = pypdf.PdfReader(directory / 'original.pdf', strict=True)
        with pymupdf.open(directory / 'original.pdf') as reference:
            for name in ['reviewed.pdf', 'csharp.pdf', 'visual-basic.pdf', 'role-map.pdf', 'author-Ua1.pdf', 'author-Ua2.pdf', 'validator-Ua1.pdf', 'validator-Ua2.pdf']:
                read = pypdf.PdfReader(directory / name, strict=True)
                if len(read.pages) != 1 or len(original.pages) != 1:
                    raise ValueError('Fixture page inventory changed')
                root_ref = read.trailer['/Root']['/StructTreeRoot'].indirect_reference
                structure = root_ref.get_object()
                visited = set()
                roles, mcids, owners = [], [], {}

                def walk(raw, parent):
                    value = raw.get_object() if hasattr(raw, 'get_object') else raw
                    if isinstance(value, list):
                        for child in value:
                            walk(child, parent)
                    elif isinstance(value, int):
                        mcids.append(value)
                        owners[value] = parent
                    elif value.get('/Type') == '/StructElem':
                        identity = (raw.idnum, raw.generation)
                        if identity in visited or value['/P'].indirect_reference != parent:
                            raise ValueError('Duplicate element or broken parent link')
                        visited.add(identity)
                        roles.append(str(value['/S']))
                        walk(value.get('/K', []), raw)
                    elif value.get('/Type') == '/MCR':
                        mcids.append(int(value['/MCID']))
                        owners[int(value['/MCID'])] = parent
                    else:
                        raise ValueError('Unexpected fixture structure node')

                walk(structure['/K'].indirect_reference, root_ref)
                if sorted(mcids) != [0, 1]:
                    raise ValueError('MCID coverage changed')
                nums = structure['/ParentTree']['/Nums']
                parent_arrays = {int(nums[i]): nums[i + 1].get_object() for i in range(0, len(nums), 2)}
                associations = parent_arrays[int(read.pages[0]['/StructParents'])]
                if any(associations[mcid].indirect_reference != owner for mcid, owner in owners.items()):
                    raise ValueError('Parent-tree MCID association changed')
                if name in ('reviewed.pdf', 'csharp.pdf', 'visual-basic.pdf', 'validator-Ua1.pdf', 'validator-Ua2.pdf') and mcids != [1, 0]:
                    raise ValueError('Accepted reading-order permutation was lost')
                if name == 'role-map.pdf' and structure['/RoleMap']['/CustomHeading'] != '/H1':
                    raise ValueError('Explicit role map missing')
                if name.startswith('author-') and not {'/H1', '/L', '/LI', '/Lbl', '/LBody', '/Table', '/TH', '/TD'}.issubset(set(roles)):
                    raise ValueError('Explicit author semantics missing')
                with pymupdf.open(directory / name) as document:
                    left, right = reference[0].get_pixmap(dpi=144), document[0].get_pixmap(dpi=144)
                    if (left.width, left.height, left.samples) != (right.width, right.height, right.samples):
                        raise ValueError('Semantic edit changed original fixture pixels')
                reports.append({'pdf': name, 'sha256': digest(directory / name), 'structureReader': 'pypdf', 'elements': len(visited), 'mcidOrder': mcids, 'pixelsEqual144Dpi': True})
    return {'schemaVersion': 1, 'purpose': 'independent-source-semantic-review', 'productionQualified': False, 'humanReviewPerformed': False, 'readerVersions': versions, 'observations': reports}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--directory', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--pdf-readers', action='store_true')
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.unlink(missing_ok=True)
    report = verify(args.directory, args.pdf_readers)
    args.output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('Semantic ledger/source fixture observations passed; human and complete production qualification remain pending.')
