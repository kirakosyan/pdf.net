"""Run an explicit pinned development-only OFD reader against controlled original raster pages."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import fitz
from PIL import Image,ImageChops,ImageStat

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def require(ok,message):
    if not ok:raise ValueError(message)
def archive(p):
    if p.exists():
        old=p.with_name(p.name+'.previous-'+sha(p))
        if not old.exists():old.write_bytes(p.read_bytes())
        p.unlink()
p=argparse.ArgumentParser();p.add_argument('fixture_directory',type=Path);p.add_argument('report_directory',type=Path);p.add_argument('--lib-directory',required=True,type=Path);a=p.parse_args();d=a.report_directory.resolve();d.mkdir(parents=True,exist_ok=True);report=d/'independent-ofd.json';archive(report)
catalog=Path(__file__).parent/'format-checks/ofd-reader/tool-jars.json';jars=sorted(a.lib_directory.glob('*.jar'));require(sorted(sha(j) for j in jars)==json.loads(catalog.read_text())['jarSha256'],'The exact independent reader JAR closure is required')
for path in list(d.glob('page-*.png'))+[d/'geometry.tsv',d/'reader.log']:archive(path)
source=a.fixture_directory/'format-fixtures/raster-pages.ofd';managed=a.fixture_directory/'format-fixtures/raster-pages.ofd.pdf';program=Path(__file__).parent/'format-checks/ofd-reader/OfdCheck.java';java=subprocess.run(['java','-version'],check=True,text=True,capture_output=True);run=subprocess.run(['java','-Djava.awt.headless=true','-cp',str(a.lib_directory.resolve()/'*'),str(program.resolve()),str(source.resolve()),str(d)],text=True,capture_output=True,timeout=60);(d/'reader.log').write_text(run.stdout+run.stderr);require(run.returncode==0,'Actual OFD reader failed')
geometry=[[float(v) for v in line.split()] for line in (d/'geometry.tsv').read_text().splitlines()];doc=fitz.open(managed);require(len(geometry)==len(doc)==3,'Independent OFD page inventory differs');observations=[]
for i in range(3):
    require(all(abs(a-b)<1e-6 for a,b in zip(geometry[i],[doc[i].rect.width*25.4/72,doc[i].rect.height*25.4/72])),'Native OFD physical geometry differs');image=Image.open(d/f'page-{i}.png').convert('RGB');pix=doc[i].get_pixmap(dpi=72,alpha=False);require(image.size==(pix.width,pix.height),'Native OFD raster geometry differs');mean=ImageStat.Stat(ImageChops.difference(image,Image.frombytes('RGB',image.size,pix.samples))).mean;require(max(mean)<1,'Native OFD raster appearance differs');observations.append({'page':i,'meanAbsolutePixelError':mean,'renderedPngSha256':sha(d/f'page-{i}.png')})
report.write_text(json.dumps({'productionQualified':False,'scope':'Controlled original OFD1.0 PNG/page/identity package only; not full standards or host qualification','java':java.stderr.strip(),'toolCatalogSha256':sha(catalog),'checkerSourceSha256':sha(program),'inputSha256':sha(source),'managedPdfSha256':sha(managed),'geometryMillimeters':geometry,'pages':observations,'readerLogSha256':sha(d/'reader.log')},indent=2)+'\n');print('Independent OFD reader geometry/order/pixel fixture passed.')
