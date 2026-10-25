"""Independent ZIP/XML observations, optionally supplemented by two PDF readers.

This checks original source fixtures, not standards certification or final packages.
"""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET
import zipfile


def verify(directory, pdf_readers=False):
    results = []
    ns = '{urn:fluent-pdf:objects:1}'
    for name in ('unicode.zip', 'csharp.zip', 'visual-basic.zip'):
        path = directory / name
        with zipfile.ZipFile(path) as archive:
            if archive.testzip() is not None:
                raise ValueError('ZIP CRC mismatch')
            names = archive.namelist()
            if name == 'unicode.zip':
                if names != ['Հայերեն-東京-😀.pdf', 'é.txt'] or archive.read(names[0]) != bytes([0, 128, 255]) or archive.read(names[1]) != bytes([7]):
                    raise ValueError('Unicode identity, ordering or payload changed')
            else:
                if names != ['document.xml']:
                    raise ValueError('Unexpected archive inventory')
                root = ET.fromstring(archive.read(names[0]))
                if root.tag != ns + 'pdf' or root.attrib != {'version': '1', 'pdfVersion': '1.7'}:
                    raise ValueError('Unexpected interchange vocabulary')
                if len(root.findall(ns + 'object')) < 3:
                    raise ValueError('Missing PDF object inventory')
            if any(not item.flag_bits & 0x800 or item.date_time != (1980, 1, 1, 0, 0, 0) or item.compress_type != 0 for item in archive.infolist()):
                raise ValueError('Noncanonical archive profile')
        results.append({'path': name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'entries': names, 'reader': 'Python zipfile'})
    for profile in ('Table', 'Stream'):
        path = directory / ('objects-' + profile + '.xml')
        root = ET.parse(path).getroot()
        if '+0001.2300' not in [e.text for e in root.iter(ns + 'number')]:
            raise ValueError('Exact decimal token was lost')
        if not list(root.iter(ns + 'data')):
            raise ValueError('Opaque stream bytes were lost')
        results.append({'path': path.name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'reader': 'Python ElementTree'})
    versions = {}
    if pdf_readers:
        import pypdf
        import pymupdf
        versions = {'pypdf': pypdf.__version__, 'PyMuPDF': pymupdf.VersionBind}
        original = pypdf.PdfReader(directory / 'original.pdf', strict=True)
        with pymupdf.open(directory / 'original.pdf') as reference:
            for name in ('csharp.pdf', 'visual-basic.pdf'):
                path = directory / name
                read = pypdf.PdfReader(path, strict=True)
                if len(read.pages) != len(original.pages):
                    raise ValueError('Page inventory changed')
                if [p.extract_text() for p in read.pages] != [p.extract_text() for p in original.pages]:
                    raise ValueError('Independent text extraction changed')
                with pymupdf.open(path) as document:
                    if document.page_count != reference.page_count:
                        raise ValueError('Independent page inventory changed')
                    for left, right in zip(reference, document):
                        a, b = left.get_pixmap(dpi=144), right.get_pixmap(dpi=144)
                        if (a.width, a.height, a.samples) != (b.width, b.height, b.samples):
                            raise ValueError('Independent 144 DPI raster changed')
                results.append({'path': name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'readers': ['pypdf', 'MuPDF'], 'pageCount': len(read.pages), 'textEqual': True, 'pixelsEqual144Dpi': True})
    return {'schemaVersion': 1, 'purpose': 'independent-source-collection-fixtures', 'productionQualified': False, 'pdfReaders': versions, 'observations': results}


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
    print('Independent collection fixture observations passed; production qualification remains pending.')
