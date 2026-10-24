"""Verify original source export fixtures; optional independent CommonMark parser."""
import argparse
import hashlib
import importlib.metadata
import io
import json
from pathlib import Path
import re
import struct
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--independent-parser', action='store_true')
    args = parser.parse_args()
    directory = Path(args.directory)
    observations = []
    md = None
    if args.independent_parser:
        from markdown_it import MarkdownIt
        md = MarkdownIt('commonmark').enable('table')
    literal = (directory / 'roles.txt').read_text(encoding='utf-8')
    digest = hashlib.sha256((directory / 'roles.pdf').read_bytes()).hexdigest()
    for name in ('roles.md', 'table-0.md', 'table-1.md', 'csharp.md', 'visual-basic.md'):
        data = (directory / name).read_bytes()
        text = data.decode('utf-8')
        assert re.match(r'<!-- source-sha256=[0-9a-f]{64}; roles=caller-assigned; grouping=inferred -->\n', text), name
        if name == 'roles.md':
            assert 'source-sha256=' + digest in text
        if md:
            tokens = md.parse(text)
            for token in tokens:
                if token.type == 'html_block':
                    assert re.fullmatch(r'<!-- (source-sha256=[0-9a-f]{64}; roles=caller-assigned; grouping=inferred|block=\d+; page-index=\d+; order=(Raw|Grouped|Layout); role=(Paragraph|Heading|Bullet|Numbered|Code|Link|Table|PageImage); (selection=whole-page|raw-range=\d+:\d+)) -->\n', token.content), token.content
                for child in token.children or []:
                    assert child.type not in ('html_inline', 'image'), child.type
            if name == 'roles.md':
                inline = [t for t in tokens if t.type == 'inline']
                assert len(inline) == 5
                for token in inline:
                    assert ''.join(t.content for t in token.children if t.type == 'text') == literal
                assert [t.tag for t in tokens if t.type == 'heading_open'] == ['h2']
                assert [t.content for t in tokens if t.type == 'fence'] == [literal + '\n']
                links = [t.attrGet('href') for token in tokens for t in token.children or [] if t.type == 'link_open']
                assert links == ['https://example.invalid/a?x=1&y=2']
                assert [t.attrGet('start') for t in tokens if t.type == 'ordered_list_open'] == [7]
            elif name.startswith('table-'):
                values = [''.join(t.content for t in token.children or [] if t.type == 'text') for token in tokens if token.type == 'inline']
                expected = ['H1', 'H2', 'A|B', '<x>&']
                if name == 'table-0.md': expected = ['Column 1', 'Column 2'] + expected
                assert values == expected, values
                assert len([t for t in tokens if t.type == 'table_open']) == 1
            else:
                values = [''.join(t.content for t in token.children or [] if t.type == 'text') for token in tokens if token.type == 'inline']
                assert values == ['example Ա😀'], values
        observations.append({'file': name, 'sha256': hashlib.sha256(data).hexdigest(), 'independentParsed': bool(md)})
    with zipfile.ZipFile(directory / 'images.zip') as archive:
        assert archive.namelist() == ['document.md', 'page-0.png', 'page-1.png']
        assert archive.testzip() is None
        markup = archive.read('document.md').decode('utf-8')
        for index in range(2):
            name = 'page-' + str(index) + '.png'
            data = archive.read(name)
            assert data[:8] == b'\x89PNG\r\n\x1a\n'
            assert struct.unpack('>II', data[16:24]) == (20, 10)
            assert '](' + name + ')' in markup
            if md:
                from PIL import Image
                with Image.open(io.BytesIO(data)) as image:
                    image.load()
                    assert image.size == (20, 10)
        if md:
            images = [t for token in md.parse(markup) for t in token.children or [] if t.type == 'image']
            assert [t.attrGet('src') for t in images] == ['page-0.png', 'page-1.png']
        observations.append({'file': 'images.zip', 'sha256': hashlib.sha256((directory / 'images.zip').read_bytes()).hexdigest(), 'pngHeaders': 2, 'independentDecoded': bool(md)})
    ranges = (directory / 'ranges.txt').read_text(encoding='utf-8')
    assert ranges.index('DEF') < ranges.index('ABC') and 'raw-range=3:3' in ranges
    report = {'schemaVersion': 1, 'purpose': 'original-source-text-interchange-observations', 'productionQualified': False,
              'parserVersion': importlib.metadata.version('markdown-it-py') if md else None,
              'imageDecoderVersion': importlib.metadata.version('Pillow') if md else None, 'observations': observations}
    Path(args.output).write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('TEXT_INTERCHANGE_FIXTURES_PASS;6-artifacts;independent-parser=' + str(bool(md)))


if __name__ == '__main__':
    main()
