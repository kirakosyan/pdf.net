"""Independent passive PDF graph and audit/hash checks for our fault-seeded source fixture."""
import argparse,hashlib,json
from pathlib import Path
import fitz

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def require(ok,message):
    if not ok:raise ValueError(message)
p=argparse.ArgumentParser();p.add_argument('fixture_directory',type=Path);p.add_argument('report',type=Path);a=p.parse_args();d=a.fixture_directory/'format-fixtures';before=d/'preflight-original.pdf';after=d/'preflight-fixed.pdf';audit_path=d/'preflight-audit.json';audit=json.loads(audit_path.read_text());a.report.parent.mkdir(parents=True,exist_ok=True)
if a.report.exists():
    old=a.report.with_name(a.report.name+'.previous-'+sha(a.report))
    if not old.exists():old.write_bytes(a.report.read_bytes())
    a.report.unlink()
require(audit['schema']==1 and audit['conformanceValidated'] is False,'Unexpected audit semantics');require(audit['before']['sourceSha256']==sha(before) and audit['after']['sourceSha256']==sha(after),'Stale before/after audit binding');source=fitz.open(before);fixed=fitz.open(after);require(len(source)==len(fixed)==1 and source[0].rect==fixed[0].rect,'Page geometry changed');source_objects='\n'.join(source.xref_object(i) for i in range(1,source.xref_length()));fixed_objects='\n'.join(fixed.xref_object(i) for i in range(1,fixed.xref_length()));require('/S /JavaScript' in source_objects and '/S /Launch' in source_objects,'Faults absent from actual source');require('/S /JavaScript' not in fixed_objects and '/S /Launch' not in fixed_objects,'Blocked action remains in written PDF');require(not any(f['code']=='Security.BlockedAction' for f in audit['after']['findings']),'Post-fix report falsely retains selected finding');require(any(f['code']=='Font.NotEmbedded' for f in audit['after']['findings']) and any(f['code']=='Image.InvalidGeometry' for f in audit['after']['findings']),'Unresolved faults disappeared from audit');require(audit['fixes'][0]['code']=='RemoveBlockedActions' and audit['fixes'][0]['affectedLocations'] and audit['fixes'][0]['lossDescription'],'Missing explicit fix provenance/loss')
a.report.write_text(json.dumps({'productionQualified':False,'conformanceValidated':False,'scope':'Controlled original source with seeded blocked actions, unembedded font and invalid image dimensions; passive graph/hash audit only','readerVersion':fitz.VersionBind,'sourceSha256':sha(before),'outputSha256':sha(after),'auditSha256':sha(audit_path),'blockedActionsRemoved':True,'otherFaultsRemain':True,'pageCount':len(fixed)},indent=2)+'\n');print('Independent passive preflight rewrite/audit fixture passed.')
