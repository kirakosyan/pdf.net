"""Explicit actual-engine source offline install/probe. No download, package production or redistribution approval."""
import argparse
import copy
import importlib.util
import json
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import uuid

spec=importlib.util.spec_from_file_location('provision',Path(__file__).with_name('native-distribution.py'))
native=importlib.util.module_from_spec(spec);spec.loader.exec_module(native)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--rid',required=True)
    parser.add_argument('--source',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    repo=Path(__file__).resolve().parents[1]
    native.protected(args.source,directory=True);native.protected(args.output,directory=True)
    root=args.output/uuid.uuid4().hex;root.mkdir(mode=0o700)
    source=root/'source';source.mkdir(mode=0o700)
    catalog=json.loads((repo/'third-party/rendering/components.json').read_text())
    entry=next(x for x in catalog['deployments'] if x['rid']==args.rid)
    built=json.loads((args.source.parent/'native-fixtures.json').read_text(encoding='utf-8-sig'))
    deployment=next(x for x in built['deployments'] if x['rid']==args.rid)
    metadata=json.loads((args.source/'rendering-asset.json').read_text(encoding='utf-8-sig'))
    assert metadata['rid']==args.rid and metadata['engineVersion']==catalog['version']
    extension='dll' if args.rid.startswith('win-') else 'dylib' if args.rid.startswith('osx-') else 'so'
    exe='worker.exe' if args.rid.startswith('win-') else 'worker'
    bridge='fluent-pdf-render.'+extension
    engine='pdfium.dll' if args.rid.startswith('win-') else 'libpdfium.'+extension
    pins={x['name']:x for x in deployment['files']}
    assert pins[engine]['sha256'].lower()==metadata['derivedAssetSha256']
    assets=[]
    origin='urn:fluent-pdf:source:'+built['sourceCommit']
    def add(path,original,pin,role,version,license_id,notice,asset_origin):
        native.protected(original)
        assert native.digest(original)==pin.lower()
        target=source/path;target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(original,target)
        assert native.digest(target)==pin.lower()
        info=native.binary_metadata(target) if role in ('executable','library') else {'imports':[],'searchPaths':[]}
        assets.append(dict(path=path,length=target.stat().st_size,sha256=pin.lower(),role=role,version=version,origin=asset_origin,license=license_id,notice=notice,imports=[dict(name=n,kind='asset' if Path(n).name in (exe,bridge,engine) else 'system',target=Path(n).name if Path(n).name in (exe,bridge,engine) else n) for n in info['imports']],searchPaths=info['searchPaths']))
    own_notice='notices/Fluent.Pdf-LICENSE.txt'
    add(own_notice,repo/'LICENSE',native.digest(repo/'LICENSE'),'notice','source-'+built['sourceCommit'][:12],'MIT',own_notice,origin)
    for file in entry['files']:
        if file['name']==engine: continue
        path='notices/'+file['name']
        add(path,args.source/'rendering-notices'/file['name'],file['sha256'],'data' if file['name']=='version.json' else 'notice',catalog['version'],'See exact upstream component notice',own_notice if file['name']=='version.json' else path,'urn:pypdfium2:'+catalog['containerVersion']+':sha256:'+entry['wheelSha256'])
    for name in (exe,bridge,engine):
        add(name,args.source/name,pins[name]['sha256'],'executable' if name==exe else 'library',catalog['version'] if name==engine else 'source-'+built['sourceCommit'][:12],'BSD-3-Clause and retained component licenses' if name==engine else 'MIT','notices/pdfium.txt' if name==engine else own_notice,origin if name!=engine else 'urn:pypdfium2:'+catalog['containerVersion']+':sha256:'+entry['wheelSha256'])
    manifest=dict(schemaVersion=1,runtimeIdentifier=args.rid,engineId='page.rendering',engineVersion=catalog['version'],abi='fluent-pdf-render-1',maximumTotalBytes=sum(a['length'] for a in assets),prerequisites=['Declared static system imports; actual complete OS ABI remains unqualified','Explicit supported native-worker OS isolation prerequisites; no ambient engine discovery'],assets=assets)
    path=root/'source-manifest.json';path.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    manifest,payload=native.load_manifest(path,native.digest(path))
    calls=[];original_socket=socket.socket
    def denied(*a,**kw):calls.append(True);raise AssertionError('Source provisioning attempted network access')
    socket.socket=denied
    try:
        installed=root/'installed';observation=native.install(manifest,payload,source,installed)
        try:native.install(manifest,payload,source,installed)
        except native.ProvisionError:pass
        else:raise AssertionError('Installer replaced an existing actual engine installation')
        wrong=copy.deepcopy(manifest);next(a for a in wrong['assets'] if a['path']==engine)['sha256']='0'*64
        try:native.verify(wrong,source)
        except native.ProvisionError as e:assert str(e)=='Native.HashMismatch'
        else:raise AssertionError('Changed actual engine pin was accepted')
        assert not calls
    finally:socket.socket=original_socket
    (root/'installation.json').write_text(json.dumps(observation,indent=2)+'\n')
    (root/'binary-build-origin.json').write_text(json.dumps(dict(sourceCommit=built['sourceCommit'],renderingAsset=metadata,sourceCatalogSha256=native.digest(repo/'third-party/rendering/components.json'),files=pins,productionQualified=False),indent=2)+'\n')
    for tfm in ('net8.0','net10.0'):
        report=root/tfm
        process=subprocess.run(['dotnet',str(repo/'eng/NativeInstallProbe/bin/Release'/tfm/'NativeInstallProbe.dll'),str(path),native.digest(path),str(installed),str(report)],capture_output=True,text=True,timeout=90,check=False)
        (root/(tfm+'.stdout.log')).write_text(process.stdout,encoding='utf-8');(root/(tfm+'.stderr.log')).write_text(process.stderr,encoding='utf-8')
        assert process.returncode==0,process.stderr
        result=json.loads((report/'result.json').read_text());samples=(report/'page.bgra').read_bytes()
        assert len(samples)==80*60*4 and result['width']==80 and result['height']==60 and result['stride']==320
        for y in range(60):
            for x in range(80):assert samples[(y*80+x)*4:(y*80+x+1)*4]==(bytes((0,0,255,255)) if 30<=x<40 and 15<=y<25 else bytes((255,255,255,255)))
        assert native.digest(report/'page.bgra')==result['samplesSha256'] and len(result['checks'])==6
        assert not list((report/'jobs').iterdir())
        print(process.stdout.strip())
    final=native.verify(manifest,installed)
    (root/'final-verification.json').write_text(json.dumps(final,indent=2)+'\n')
    print('INSTALLED_RENDERER_SOURCE_OBSERVED;'+str(root))


if __name__=='__main__':main()
