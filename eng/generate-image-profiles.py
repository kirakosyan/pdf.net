#!/usr/bin/env python3
"""Original deterministic ICC v2.1 matrix/shaper data from normative standard RGB facts.

This writes original profile bytes. No third-party profile or implementation is used.
See neutral color-fact provenance in the image contract for the standards lineage.
"""
from pathlib import Path
import struct, math, hashlib, json
ROOT=Path(__file__).resolve().parents[1]
def u32(n):return struct.pack('>I',n)
def fixed(v):return struct.pack('>i',round(v*65536))
def inverse(m):
 a=[list(row)+[float(i==j) for j in range(3)] for i,row in enumerate(m)]
 for c in range(3):
  pivot=max(range(c,3),key=lambda r:abs(a[r][c]));a[c],a[pivot]=a[pivot],a[c];s=a[c][c];a[c]=[v/s for v in a[c]]
  for r in range(3):
   if r!=c:
    s=a[r][c];a[r]=[a[r][j]-s*a[c][j] for j in range(6)]
 return [r[3:] for r in a]
def mul(a,b):return [[sum(a[i][k]*b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
def vector(a,v):return [sum(a[i][j]*v[j] for j in range(3)) for i in range(3)]
def xyz(v):return b'XYZ '+b'\0'*4+b''.join(fixed(x) for x in v)
def text(s):return b'text'+b'\0'*4+s.encode('ascii')+b'\0'
def description(s):
 value=s.encode('ascii')+b'\0';return b'desc'+b'\0'*4+u32(len(value))+value+b'\0'*78
# Matrix columns are standard RGB chromaticities scaled to the D65 white.
primaries=[(.64,.33),(.30,.60),(.15,.06)];white=[.3127/.3290,1,(1-.3127-.3290)/.3290]
p=[[x/y for x,y in primaries],[1,1,1],[(1-x-y)/y for x,y in primaries]];scales=vector(inverse(p),white);rgb=[[p[r][c]*scales[c] for c in range(3)] for r in range(3)]
# Original Bradford adaptation to the exact ICC D50 header white.
bradford=[[.8951,.2664,-.1614],[-.7502,1.7135,.0367],[.0389,-.0685,1.0296]];d50=[0xf6d6/65536,1,0xd32d/65536];source=vector(bradford,white);target=vector(bradford,d50);diagonal=[[target[r]/source[r] if r==c else 0 for c in range(3)] for r in range(3)];adapt=mul(mul(inverse(bradford),diagonal),bradford);matrix=mul(adapt,rgb)
# v2 permits normalized black; the exact standard inverse transfer is sampled
# into the standard unsigned 16-bit curve representation (4096 entries).
curve=b'curv'+b'\0'*4+u32(4096)
for i in range(4096):
 x=i/4095;y=x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4;curve+=struct.pack('>H',round(y*65535))
records=[]
for gray in [False,True]:
 label='gray' if gray else 'rgb';tags=[('desc',description('Fluent.Pdf original standard '+label.upper())),('cprt',text('Copyright 2026 Fluent.Pdf contributors. MIT License.')),('wtpt',xyz(d50)),('bkpt',xyz([0,0,0])),('chad',b'sf32'+b'\0'*4+b''.join(fixed(v) for row in adapt for v in row))]
 if gray:tags += [('kTRC',curve)]
 else:
  tags += [(name,xyz([matrix[r][c] for r in range(3)])) for c,name in enumerate(['rXYZ','gXYZ','bXYZ'])]
  tags += [(name,curve) for name in ['rTRC','gTRC','bTRC']]
 header=bytearray(128);header[4:8]=b'FPDF';header[8:12]=bytes([2,0x10,0,0]);header[12:16]=b'mntr';header[16:20]=b'GRAY' if gray else b'RGB ';header[20:24]=b'XYZ ';header[24:36]=struct.pack('>6H',2026,10,18,0,0,0);header[36:40]=b'acsp';header[64:68]=u32(0);header[68:80]=b''.join(fixed(v) for v in d50);header[80:84]=b'FPDF'
 offset=132+12*len(tags);table=bytearray(u32(len(tags)));payload=bytearray();shared={}
 for name,value in tags:
  identity=id(value)
  if identity in shared:first,length=shared[identity]
  else:
   first=offset+len(payload);length=len(value);payload+=value;payload+=b'\0'*((-len(payload))%4);shared[identity]=(first,length)
  table+=name.encode('ascii')+u32(first)+u32(length)
 data=header+table+payload;data[0:4]=u32(len(data));path=ROOT/'src/Fluent.Pdf/Images/Data'/('standard-'+label+'-v2.icc');path.write_bytes(data);records.append({'file':path.name,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
print(json.dumps(records,indent=2))
