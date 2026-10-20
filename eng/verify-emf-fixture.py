"""Independent Inkscape EMF import/render of one original opaque raster fixture."""
import argparse,hashlib,json,shutil,subprocess
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
p=argparse.ArgumentParser();p.add_argument('fixture_directory',type=Path);p.add_argument('report_directory',type=Path);a=p.parse_args();d=a.report_directory.resolve();d.mkdir(parents=True,exist_ok=True)
report=d/'independent-emf.json';archive(report);native=d/'native.pdf';archive(native);log=d/'reader.log';archive(log)
engine=Path(shutil.which('inkscape') or '').absolute();require(engine.is_file(),'Explicit Inkscape analysis tool is required');version=subprocess.run([str(engine),'--version'],check=True,text=True,capture_output=True).stdout.strip();require(version.startswith('Inkscape 1.4 '),'Requalify a changed EMF reader')
source=a.fixture_directory/'format-fixtures/raster-page.emf';managed=a.fixture_directory/'format-fixtures/raster-page.emf.pdf'
run=subprocess.run([str(engine),str(source.resolve()),'--export-type=pdf','--export-filename='+str(native)],text=True,capture_output=True,timeout=60);log.write_text(run.stdout+run.stderr);require(run.returncode==0 and native.is_file(),'Actual EMF reader failed')
x=fitz.open(native);y=fitz.open(managed);require(len(x)==len(y)==1,'Reader page count differs');require(all(abs(u-v)<.001 for u,v in zip(x[0].rect,y[0].rect)),'Native EMF physical geometry differs');px=x[0].get_pixmap(dpi=72,alpha=False);py=y[0].get_pixmap(dpi=72,alpha=False);require((px.width,px.height)==(py.width,py.height),'Native raster geometry differs');mean=ImageStat.Stat(ImageChops.difference(Image.frombytes('RGB',(px.width,px.height),px.samples),Image.frombytes('RGB',(py.width,py.height),py.samples))).mean;require(max(mean)<1,'Native EMF decoded raster appearance differs')
report.write_text(json.dumps({'productionQualified':False,'scope':'One controlled original opaque EMF24 record profile; no arbitrary metafile, vector/font or OS corpus qualification','readerVersion':version,'readerSha256':sha(engine),'inputSha256':sha(source),'managedPdfSha256':sha(managed),'nativePdfSha256':sha(native),'readerLogSha256':sha(log),'meanAbsolutePixelError':mean,'pointSize':[x[0].rect.width,x[0].rect.height]},indent=2)+'\n');print('Independent Inkscape EMF fixture passed.')
