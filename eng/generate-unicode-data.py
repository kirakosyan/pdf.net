from pathlib import Path
import re,struct,json,hashlib,gzip,sys
source=Path(sys.argv[1]);repo=Path(__file__).resolve().parent.parent;out=repo/'src/Fluent.Pdf/Text/Data';out.mkdir(parents=True,exist_ok=True)
classes=['L','R','AL','EN','ES','ET','AN','CS','NSM','BN','B','S','WS','ON','LRE','LRO','RLE','RLO','PDF','LRI','RLI','FSI','PDI']
graphemes=['Other','CR','LF','Control','Extend','ZWJ','Regional_Indicator','Prepend','SpacingMark','L','V','T','LV','LVT']
def lines(name):
 for line in (source/name).read_text().splitlines():
  line=line.split('#')[0].strip()
  if line:yield [x.strip() for x in line.split(';')]
def span(text):
 pair=text.split('..');return int(pair[0],16),int(pair[-1],16)
aliases={}
for row in lines('PropertyValueAliases.txt'):
 if row[0]=='sc':
  for name in row[1:]:aliases[name]=row[1]
def tag(name):return int.from_bytes(aliases.get(name,name).encode('ascii'),'big')
size=0x110000;bidi=[0]*size;gcb=[0]*size;scripts=[tag('Zzzz')]*size;flags=[0]*size;incb=[0]*size;extension=[0]*size;sets=[()];set_ids={():0}
missing={'Left_To_Right':'L','Right_To_Left':'R','Arabic_Letter':'AL','European_Terminator':'ET'}
for line in (source/'extracted/DerivedBidiClass.txt').read_text().splitlines():
 if '@missing:' in line:
  a,b=[x.strip() for x in line.split('@missing:')[1].split(';')];first,last=span(a);bidi[first:last+1]=[classes.index(missing.get(b,b))]*(last-first+1)
for row in lines('extracted/DerivedBidiClass.txt'):
 first,last=span(row[0]);bidi[first:last+1]=[classes.index(row[1])]*(last-first+1)
for row in lines('auxiliary/GraphemeBreakProperty.txt'):
 first,last=span(row[0]);gcb[first:last+1]=[graphemes.index(row[1])]*(last-first+1)
for row in lines('Scripts.txt'):
 first,last=span(row[0]);scripts[first:last+1]=[tag(row[1])]*(last-first+1)
for row in lines('ScriptExtensions.txt'):
 first,last=span(row[0]);values=tuple(sorted(tag(x) for x in row[1].split()))
 if values not in set_ids:set_ids[values]=len(sets);sets.append(values)
 extension[first:last+1]=[set_ids[values]]*(last-first+1)
for row in lines('DerivedCoreProperties.txt'):
 first,last=span(row[0])
 if row[1]=='Default_Ignorable_Code_Point':
  for scalar in range(first,last+1):flags[scalar]|=1
 if row[1]=='InCB':incb[first:last+1]=[['None','Consonant','Extend','Linker'].index(row[2])]*(last-first+1)
for row in lines('emoji/emoji-data.txt'):
 if row[1]=='Extended_Pictographic':
  first,last=span(row[0])
  for scalar in range(first,last+1):flags[scalar]|=2
records=[];previous=None;start=0
for scalar in range(size):
 value=(scripts[scalar],bidi[scalar]|gcb[scalar]<<5|incb[scalar]<<9|flags[scalar]<<11,extension[scalar])
 if value!=previous:
  if previous is not None:records.append((start,scalar-1,*previous))
  start=scalar;previous=value
records.append((start,size-1,*previous))
mirrors=[(int(row[0],16),int(row[1],16)) for row in lines('BidiMirroring.txt')]
brackets=[(int(row[0],16),int(row[1],16),1 if row[2]=='o' else 2) for row in lines('BidiBrackets.txt')]
payload=bytearray(b'FPU1'+struct.pack('<IIIIII',18,len(records),len(sets),len(mirrors),len(brackets),0))
for record in records:payload+=struct.pack('<IIIII',*record)
for values in sets:
 payload+=struct.pack('<I',len(values))
 for value in values:payload+=struct.pack('<I',value)
for row in mirrors:payload+=struct.pack('<II',*row)
for row in brackets:payload+=struct.pack('<III',*row)
(out/'Unicode18.bin').write_bytes(payload)
notices=repo/'third-party/unicode';notices.mkdir(parents=True,exist_ok=True);(notices/'LICENSE.txt').write_bytes((source/'LICENSE').read_bytes())
fixtures=repo/'tests/Fluent.Pdf.Contract.Tests/Fixtures/unicode';fixtures.mkdir(parents=True,exist_ok=True)
for name in ['BidiTest.txt','BidiCharacterTest.txt','auxiliary/GraphemeBreakTest.txt']:
 with (fixtures/(Path(name).name+'.gz')).open('wb') as raw:
  with gzip.GzipFile(fileobj=raw,mode='wb',mtime=0,filename='') as target:target.write((source/name).read_bytes())
manifest={'unicodeVersion':'18.0.0','snapshotCommit':'4fd8eca90b30aa4c72543ccddb4c15d05becdf8d','purpose':'pinned normative property/test data; no imported algorithm implementation','records':len(records),'residentChargeBytes':1048576,'binaryBytes':len(payload),'binarySha256':hashlib.sha256(payload).hexdigest(),'bidiClasses':classes,'graphemeClasses':graphemes,'sources':[{'sourceId':'UNICODE-'+str(p.relative_to(source)).replace('/','-'),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(source.rglob('*.txt'))]}
(notices/'data.json').write_text(json.dumps(manifest,indent=2)+'\n');print(json.dumps({k:manifest[k] for k in ['records','binaryBytes','binarySha256']}))
