"""Independent JSON/XML parity, provenance, source binding and optional formal-schema checks."""
import argparse
import hashlib
import json
import math
import re
from pathlib import Path
import xml.etree.ElementTree as ET

NS = '{urn:fluent-pdf:business:1}'


def load_json(path):
    def pairs(values):
        result = {}
        for key, value in values:
            if key in result:
                raise ValueError('Duplicate JSON property: ' + key)
            result[key] = value
        return result
    def invalid(value):
        raise ValueError('Nonfinite JSON constant: ' + value)
    return json.loads(path.read_text(encoding='utf-8'), object_pairs_hook=pairs, parse_constant=invalid)


def decode_xml(path):
    raw = path.read_bytes()
    if b'<!DOCTYPE' in raw or b'<!ENTITY' in raw:
        raise ValueError('Document declarations/entities are not permitted')
    root = ET.fromstring(raw)
    if root.tag != NS + 'businessData' or set(root.attrib) != {'schemaVersion', 'sourceSha256', 'coordinateSpace', 'businessVerified'}:
        raise ValueError('Unexpected XML envelope')
    records = []
    if root.text and root.text.strip():
        raise ValueError('Envelope mixed text')
    for element in root:
        if element.tag != NS + 'record' or element.attrib:
            raise ValueError('Unexpected record vocabulary')
        record = {}
        if element.text and element.text.strip() or element.tail and element.tail.strip():
            raise ValueError('Record mixed text')
        for field in element:
            if not field.tag.startswith(NS) or set(field.attrib) != {'type'}:
                raise ValueError('Unexpected field vocabulary')
            name = field.tag[len(NS):]
            if field.tail and field.tail.strip():
                raise ValueError('Field tail text')
            if name in record:
                raise ValueError('Duplicate XML field')
            kind = field.attrib['type']
            if kind == 'array':
                if field.text and field.text.strip():
                    raise ValueError('Array mixed text')
                value = []
                for item in field:
                    if item.tag != NS + 'value' or item.attrib or len(item) or item.tail and item.tail.strip():
                        raise ValueError('Unexpected array entry')
                    value.append(item.text or '')
            else:
                if len(field):
                    raise ValueError('Scalar children')
                text = field.text or ''
                if kind == 'null':
                    if text:
                        raise ValueError('Nonempty null')
                    value = None
                elif kind == 'string':
                    value = text
                elif kind == 'boolean':
                    if text not in ('true', 'false'):
                        raise ValueError('Invalid boolean')
                    value = text == 'true'
                elif kind == 'number':
                    value = json.loads(text)
                    if type(value) not in (int, float) or not math.isfinite(value):
                        raise ValueError('Invalid finite number')
                else:
                    raise ValueError('Unknown value type')
            record[name] = value
        records.append(record)
    return {'schemaVersion': int(root.attrib['schemaVersion']), 'sourceSha256': root.attrib['sourceSha256'],
            'coordinateSpace': root.attrib['coordinateSpace'], 'businessVerified': root.attrib['businessVerified'] != 'false', 'records': records}


def validate(data, source):
    if set(data) != {'schemaVersion', 'sourceSha256', 'coordinateSpace', 'businessVerified', 'records'} or data['schemaVersion'] != 1 or data['businessVerified'] is not False or data['coordinateSpace'] != 'display-points-top-left':
        raise ValueError('Unsupported envelope or false business verification claim')
    if data['sourceSha256'] != hashlib.sha256(source).hexdigest():
        raise ValueError('Source digest mismatch')
    ids = {}
    pages = {}
    characters = {}
    for index, record in enumerate(data['records']):
        if record.get('id') != 'r' + str(index) or record.get('businessVerified') is not False:
            raise ValueError('Record identity or false verification claim')
        parent = record.get('parentId')
        if parent is not None and parent not in ids:
            raise ValueError('Missing or forward parent')
        ids[record['id']] = record
        kind = record['kind']
        if kind == 'page':
            pages[record['pageIndex']] = record
            if record['provenance'] != 'mixed':
                raise ValueError('Composite decoded text must retain mixed provenance')
        elif kind == 'character':
            characters[(record['pageIndex'], str(record['sourceIndex']))] = record
            inferred = record['mapping'] in ('Inferred', 'Unmapped')
            if record['provenance'] != ('inference' if inferred else 'source'):
                raise ValueError('Mapping confidence promoted into source fact')
        elif kind in ('table', 'cell'):
            if record['provenance'] != 'inference' or record['confidenceKind'] != 'uncalibrated-table-heuristic' or not 0 <= record['confidence'] <= 1:
                raise ValueError('Inferred table promoted into fact')
        elif kind in ('ocr', 'ocr-word'):
            score = record['confidence']
            if record['provenance'] != 'ocr' or record['confidenceKind'] != 'engine-score-0-100' or score is not None and not 0 <= score <= 100 or 'number' in record:
                raise ValueError('OCR score promoted into business value')
        elif kind == 'paragraph':
            if record['provenance'] != 'inference':
                raise ValueError('Geometric paragraph promoted into source structure')
        if 'width' in record and (record['width'] < 0 or record['height'] < 0):
            raise ValueError('Invalid geometry')
        for source_index in record.get('sourceIndices', []):
            if (record['pageIndex'], source_index) not in characters:
                raise ValueError('Unbound character provenance')
    if sorted(pages) != list(range(len(pages))) or not pages:
        raise ValueError('Invalid page inventory')
    for record in data['records']:
        page = record['pageIndex']
        if page is not None and page not in pages:
            raise ValueError('Invalid page provenance')
        if record['kind'] in ('character', 'replacement', 'paragraph', 'table', 'cell', 'ocr', 'ocr-word') and record['objectId'] != pages[page]['objectId']:
            raise ValueError('Source page owner mismatch')
    return pages


def verify(directory, formal=False, pdf_reader=False):
    schema_root = Path(__file__).resolve().parents[1] / 'specs'
    schema = load_json(schema_root / 'business-data-v1.schema.json')
    if formal:
        import jsonschema
        from lxml import etree
        jsonschema.Draft202012Validator.check_schema(schema)
        validator = jsonschema.Draft202012Validator(schema)
        xsd = etree.XMLSchema(etree.parse(str(schema_root / 'business-data-v1.xsd')))
    observations = []
    for name in ['digital', 'table', 'fields', 'structure', 'mixed', 'csharp', 'visual-basic']:
        source = (directory / (name + '.pdf')).read_bytes()
        data = load_json(directory / (name + '.json'))
        xml = decode_xml(directory / (name + '.xml'))
        if data != xml:
            raise ValueError('JSON/XML mismatch: ' + name)
        pages = validate(data, source)
        vocabulary = schema['properties']['records']['items']
        for record in data['records']:
            if set(record) - vocabulary['properties'].keys() or not set(vocabulary['required']) <= record.keys():
                raise ValueError('Unknown or missing record fields')
            for condition in vocabulary['allOf']:
                if condition['if']['properties']['kind']['const'] == record['kind'] and not set(condition['then']['required']) <= record.keys():
                    raise ValueError('Missing kind-specific fields')
            for key, value in record.items():
                rule = vocabulary['properties'][key]
                if 'const' in rule and value != rule['const'] or 'enum' in rule and value not in rule['enum']:
                    raise ValueError('Unexpected record field value')
                if 'type' in rule:
                    types = rule['type'] if isinstance(rule['type'], list) else [rule['type']]
                    actual = 'null' if value is None else 'boolean' if type(value) is bool else 'integer' if type(value) is int else 'number' if type(value) is float else 'string' if type(value) is str else 'array' if type(value) is list else 'object'
                    if actual not in types and not (actual == 'integer' and 'number' in types):
                        raise ValueError('Unexpected scalar/array type')
                if isinstance(value, list) and (len(value) > 100000 or any(type(x) is not str for x in value)):
                    raise ValueError('Invalid string reference array')
                if value is not None and 'pattern' in rule and not re.fullmatch(rule['pattern'], value):
                    raise ValueError('Invalid identifier')
        if formal:
            validator.validate(data)
            xsd.assertValid(etree.parse(str(directory / (name + '.xml'))))
        if pdf_reader:
            import pypdf
            reader = pypdf.PdfReader(directory / (name + '.pdf'))
            if len(reader.pages) != len(pages):
                raise ValueError('Independent PDF page count mismatch')
            for index, page in enumerate(reader.pages):
                ref = page.indirect_reference
                if pages[index]['objectId'] != str(ref.idnum) + ':' + str(ref.generation):
                    raise ValueError('Independent page object identity mismatch')
                expected = ''.join((page.extract_text() or '').split())
                actual = ''.join(pages[index]['text'].split())
                if expected != actual:
                    raise ValueError('Independent source-order text mismatch: ' + name)
        if name == 'table':
            cells = [r for r in data['records'] if r['kind'] == 'cell']
            if len(cells) != 3 or cells[0]['columnSpan'] != 2 or not any(r['number'] == 1234.5 for r in cells):
                raise ValueError('Golden merged/numeric table mismatch')
        if name == 'mixed':
            kinds = {r['kind'] for r in data['records']}
            if not {'character', 'ocr', 'ocr-word'} <= kinds:
                raise ValueError('Digital/OCR separation lost')
        observations.append({'fixture': name, 'sourceSha256': data['sourceSha256'], 'recordCount': len(data['records']),
                             'jsonSha256': hashlib.sha256((directory / (name + '.json')).read_bytes()).hexdigest(),
                             'xmlSha256': hashlib.sha256((directory / (name + '.xml')).read_bytes()).hexdigest(),
                             'pairedSemanticMatch': True, 'formalSchemas': formal, 'independentPdfReader': pdf_reader})
    return {'schemaVersion': 1, 'productionQualified': False, 'ocrAccuracyMeasured': False, 'observations': observations}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--directory', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--formal-schemas', action='store_true')
    parser.add_argument('--pdf-reader', action='store_true')
    args = parser.parse_args()
    report = verify(args.directory, args.formal_schemas, args.pdf_reader)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('Verified seven paired business-observation JSON/XML exports and provenance bindings.')
