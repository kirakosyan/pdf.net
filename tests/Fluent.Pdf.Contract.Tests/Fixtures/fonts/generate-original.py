"""Original geometric font fixtures encoded by the independent FontTools library."""
from pathlib import Path
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.t2CharStringPen import T2CharStringPen
from fontTools.ttLib import TTCollection
from fontTools.ttLib import TTFont
from fontTools.misc.psCharStrings import T2CharString
from fontTools import subset
import hashlib,json,fontTools

root=Path(__file__).parent
glyphs=['.notdef','A','B','acute','Aacute','space','uni4E00','u1F600']
mapping={32:'space',65:'A',66:'B',193:'Aacute',0x4e00:'uni4E00',0x1f600:'u1F600'}
def contours(pen,name):
    if name in ['.notdef','space']:return
    if name=='acute':points=[(0,0),(100,100),(200,100),(100,0)]
    elif name=='A':points=[(100,0),(300,700),(500,0)]
    elif name=='B':points=[(100,0),(100,700),(450,700),(450,0)]
    elif name=='uni4E00':points=[(50,300),(550,300),(550,400),(50,400)]
    elif name=='u1F600':points=[(100,100),(100,600),(500,600),(500,100)]
    else:
        contours(pen,'A')
        points=[(180,700),(280,800),(380,800),(280,700)]
    pen.moveTo(points[0])
    for point in points[1:]:pen.lineTo(point)
    pen.closePath()
for cff in [False,True]:
    builder=FontBuilder(1000,isTTF=not cff);builder.setupGlyphOrder(glyphs);builder.setupCharacterMap(mapping)
    metrics={name:(600,0) for name in glyphs};builder.setupHorizontalMetrics(metrics);builder.setupHorizontalHeader(ascent=800,descent=-200)
    builder.setupNameTable({'familyName':'Fluent Geometric Fixture','styleName':'Regular','uniqueFontIdentifier':'FluentOriginalFixture1','fullName':'Fluent Geometric Fixture Regular','psName':'FluentOriginalCff' if cff else 'FluentOriginalTrueType','version':'Version 1.000','copyright':'Original fixture shapes; MIT-licensed with this repository.'})
    builder.setupOS2(sTypoAscender=800,sTypoDescender=-200,usWinAscent=800,usWinDescent=200,sCapHeight=700)
    if cff:
        programs={}
        for name in glyphs:
            pen=T2CharStringPen(600,None);contours(pen,name);programs[name]=pen.getCharString()
        builder.setupCFF('FluentOriginalCff',{'FullName':'Fluent Geometric Fixture Regular','FamilyName':'Fluent Geometric Fixture','Weight':'Regular'},programs,{})
    else:
        outlines={}
        for name in glyphs:
            pen=TTGlyphPen(outlines)
            if name=='Aacute':pen.addComponent('A',(1,0,0,1,0,0));pen.addComponent('acute',(1,0,0,1,180,700))
            else:contours(pen,name)
            outlines[name]=pen.glyph()
        builder.setupGlyf(outlines)
    builder.setupPost();builder.setupMaxp();builder.font.recalcTimestamp=False;builder.font['head'].created=builder.font['head'].modified=3800000000
    builder.save(root/('original.otf' if cff else 'original.ttf'))

original=TTFont(root/'original.otf');(root/'original.cff').write_bytes(original['CFF '].compile(original))
original.recalcTimestamp=False
top=original['CFF '].cff.topDictIndex[0]
top.CharStrings['Aacute']=T2CharString(program=[600,180,700,65,194,'endchar'],private=top.Private,globalSubrs=original['CFF '].cff.GlobalSubrs)
original['CFF '].cff.fontNames=['FluentOriginalSeac']
for record in original['name'].names:
    if record.nameID in [1,3,4,6,16]:record.string='FluentOriginalSeac'.encode(record.getEncoding())
original.save(root/'original-seac.otf')
first=TTFont(root/'original.ttf');second=TTFont(root/'original.ttf');second['hmtx'].metrics['A']=(650,0)
first.recalcTimestamp=second.recalcTimestamp=False
for record in second['name'].names:
    if record.nameID in [1,3,4,6,16]:record.string='FluentCollectionSecond'.encode(record.getEncoding())
collection=TTCollection();collection.fonts=[first,second];collection.save(root/'original.ttc')

# A small licensed CID-keyed CJK fixture, explicitly renamed after modification.
source=Path('/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc')
if source.exists():
    font=TTCollection(source).fonts[0];version=font['name'].getDebugName(5);original_sha=hashlib.sha256(source.read_bytes()).hexdigest()
    options=subset.Options();options.layout_features=[];options.name_IDs=['*'];options.notdef_outline=True;subsetter=subset.Subsetter(options=options);subsetter.populate(unicodes=[32,65,66,0x4e00,0x4e2d,0x6587]);subsetter.subset(font)
    for record in font['name'].names:
        if record.nameID in [1,3,4,6,16,17]:record.string=('FluentCjkFixture' if record.nameID==6 else 'Fluent CJK Fixture').encode(record.getEncoding())
    cff=font['CFF '].cff;cff.fontNames=['FluentCjkFixture'];top=cff.topDictIndex[0];top.FullName='Fluent CJK Fixture';top.FamilyName='Fluent CJK Fixture'
    font.recalcTimestamp=False;font['head'].created=font['head'].modified=3800000000;font.save(root/'licensed-cjk.otf')
    (root/'licensed-cjk-source.json').write_text(json.dumps({'sourceId':'FONT-CJK-001','sourceSha256':original_sha,'sourceVersion':version,'faceIndex':0,'license':'SIL-OFL-1.1','renamedPrimaryName':'FluentCjkFixture','tool':'FontTools '+fontTools.__version__,'unicodeScalars':['0020','0041','0042','4E00','4E2D','6587']},indent=2)+'\n')
records=[]
for path in sorted(root.iterdir()):
    if path.suffix in ['.ttf','.otf','.pfa','.pfb','.cff','.ttc']:records.append({'file':path.name,'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
(root/'manifest.json').write_text(json.dumps({'fontTools':fontTools.__version__,'purpose':'original geometric font and licensed renamed CJK fixtures','records':records},indent=2)+'\n')
