"""Compile only our controlled escaped source fixture; this is not an untrusted TeX sandbox."""
import argparse, hashlib, json, os, shutil, subprocess
from pathlib import Path
import fitz

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def archive(p):
    if p.exists():
        old=p.with_name(p.name+'.previous-'+sha(p))
        if not old.exists(): old.write_bytes(p.read_bytes())
        p.unlink()
p=argparse.ArgumentParser(); p.add_argument('fixture_directory',type=Path); p.add_argument('report_directory',type=Path); args=p.parse_args()
source=args.fixture_directory/'format-fixtures/inferred-text.tex'; directory=args.report_directory.resolve(); directory.mkdir(parents=True,exist_ok=True)
report=directory/'independent-tex.json'; archive(report)
for path in directory.glob('inferred-text.*'): archive(path)
engine=Path(shutil.which('pdflatex') or '').absolute()
if not engine.is_file(): raise ValueError('Explicit local pdfLaTeX is required')
version=subprocess.run([str(engine),'--version'],check=True,capture_output=True,text=True).stdout
if '1.40.26' not in version: raise ValueError('Requalify a changed compiler version')
run=subprocess.run([str(engine),'-no-shell-escape','-halt-on-error','-interaction=nonstopmode','-output-directory',str(directory),str(source.resolve())],capture_output=True,text=True,timeout=60)
log=directory/'compiler.stdout.log'; archive(log); log.write_text(run.stdout+run.stderr)
if run.returncode: raise ValueError('Controlled LaTeX compilation failed')
output=directory/'inferred-text.pdf'; doc=fitz.open(output); text=''.join(page.get_text() for page in doc)
if len(doc)!=1 or ''.join(text.split()) != r'\input{secret}$x_1$&50%#~^<>1': raise ValueError('Compiled output did not preserve escaped literal text')
fmt=subprocess.run(['kpsewhich','-engine=pdftex','-format=fmt','pdflatex.fmt'],check=True,capture_output=True,text=True).stdout.strip(); format_path=Path(fmt)
report.write_text(json.dumps({'productionQualified':False,'scope':'Controlled original ASCII escaping fixture only; no host-isolation or scientific-corpus qualification','compilerVersion':version.strip(),'compiler':{'path':str(engine),'sha256':sha(engine)},'format':{'path':str(format_path),'sha256':sha(format_path)},'inputSha256':sha(source),'outputSha256':sha(output),'logSha256':sha(log),'pages':len(doc),'extractedText':text,'limitations':['Compiler uses explicit local TeX Live assets; T1 bitmap fonts may be generated from local METAFONT sources. This analysis does not supply an SDK worker.','The host compiler run is development analysis of controlled generated source, not a malicious-input sandbox.']},indent=2)+'\n')
print('Independent pdfLaTeX controlled escape fixture passed.')
