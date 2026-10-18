from pathlib import Path
from PIL import Image,PngImagePlugin
import struct,zlib,binascii,hashlib,json
root=Path(__file__).resolve().parents[1];out=root/'tests/Fluent.Pdf.Contract.Tests/Fixtures/images';out.mkdir(exist_ok=True)
rgb=Image.new('RGB',(3,2));rgb.putdata([(255,0,0),(0,255,0),(0,0,255),(255,255,255),(0,0,0),(255,255,0)])
rgb.save(out/'rgb.png');rgba=rgb.convert('RGBA');rgba.putalpha(Image.frombytes('L',(3,2),bytes([255,128,0,64,255,192])));rgba.save(out/'rgba.png')
srgb=PngImagePlugin.PngInfo();srgb.add(b'sRGB',b'\x01');rgb.save(out/'srgb.png',pnginfo=srgb)
palette=Image.new('P',(3,2));palette.putpalette([255,0,0,0,255,0,0,0,255,255,255,0]+[0]*756);palette.putdata([0,1,2,3,1,0]);palette.save(out/'palette-alpha.png',bits=2,transparency=bytes([0,128,255,64]))
mono=Image.new('1',(8,2));mono.putdata([0,255]*4+[255,0]*4);mono.save(out/'mono.png')
for orientation in [1,2,3,4,5,6,7,8]:
 exif=Image.Exif();exif[274]=orientation;rgb.save(out/f'orientation-{orientation}.jpg',quality=95,subsampling=0,exif=exif,dpi=(144,72))
cmyk=Image.new('CMYK',(3,2));cmyk.putdata([(0,255,255,0),(255,0,255,0),(255,255,0,0),(0,0,0,0),(0,0,0,255),(0,0,255,0)]);cmyk.save(out/'cmyk.jpg',quality=95)
profile=(root/'src/Fluent.Pdf/Images/Data/standard-rgb-v2.icc').read_bytes();rgb.save(out/'icc.png',icc_profile=profile,dpi=(144,72));rgb.save(out/'icc.jpg',icc_profile=profile,quality=95,subsampling=0)
def chunk(name,payload):return struct.pack('>I',len(payload))+name+payload+struct.pack('>I',binascii.crc32(name+payload)&0xffffffff)
def png(name,w,h,bits,ctype,raw,interlace=0):
 data=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,bits,ctype,0,0,interlace))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b'');(out/name).write_bytes(data)
values=list(rgb.get_flattened_data());passes=[]
for sx,sy,dx,dy in [(0,0,8,8),(4,0,8,8),(0,4,4,8),(2,0,4,4),(0,2,2,4),(1,0,2,2),(0,1,1,2)]:
 xs=list(range(sx,3,dx));ys=list(range(sy,2,dy))
 if not xs:continue
 for y in ys:passes.append(b'\0'+bytes(v for x in xs for v in values[y*3+x]))
png('adam7-rgb.png',3,2,8,2,b''.join(passes),1)
raw=bytes.fromhex('000011112222333344445555666677778888');png('rgb16.png',3,1,16,2,b'\0'+raw)
records=[{'file':p.name,'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(out.iterdir()) if p.suffix in ['.png','.jpg']]
(out/'provenance.json').write_text(json.dumps({'schemaVersion':1,'purpose':'Original synthetic color/mask/orientation fixtures; no third-party document/image content','license':'MIT','tools':{'Python':__import__('sys').version.split()[0],'Pillow':__import__('PIL').__version__},'generation':'Original constant samples; Pillow PNG/JPEG encoding; separate literal PNG/Adam7 encoder for interlace and 16-bit cases','files':records},indent=2)+'\n')
print('Generated',len(records),'owned image fixtures.')
