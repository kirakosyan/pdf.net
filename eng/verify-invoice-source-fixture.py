"""Independent syntax/attachment/readable-total checks; deliberately not an invoice/PDF-A conformance validator."""
import argparse,hashlib,json
from pathlib import Path
import fitz
from lxml import etree

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def require(ok,message):
    if not ok:raise ValueError(message)
p=argparse.ArgumentParser();p.add_argument('fixture_directory',type=Path);p.add_argument('report',type=Path);a=p.parse_args();d=a.fixture_directory/'format-fixtures';xml=d/'invoice-source.xml';pdf=d/'invoice-unqualified-draft.pdf';a.report.parent.mkdir(parents=True,exist_ok=True)
if a.report.exists():
    old=a.report.with_name(a.report.name+'.previous-'+sha(a.report))
    if not old.exists():old.write_bytes(a.report.read_bytes())
    a.report.unlink()
root=etree.fromstring(xml.read_bytes(),etree.XMLParser(resolve_entities=False,load_dtd=False,no_network=True));ns={'rsm':'urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100','ram':'urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100'};require(root.tag=='{'+ns['rsm']+'}CrossIndustryInvoice','Wrong CII root namespace');require(len(root.findall('rsm:ExchangedDocument',ns))==1,'Wrong CII document namespace');gross=root.find('.//ram:GrandTotalAmount',ns).text;net=root.find('.//ram:LineTotalAmount',ns).text;tax=root.find('.//ram:TaxTotalAmount',ns).text;require((net,tax,gross)==('7.01','0.50','7.51'),'Unexpected controlled decimal totals');doc=fitz.open(pdf);text='\n'.join(page.get_text() for page in doc);require(all(v+' EUR' in text for v in [net,tax,gross]),'Readable totals differ from CII');require(doc.embfile_names()==['factur-x.xml'],'Unexpected associated invoice file inventory');require(doc.embfile_get('factur-x.xml')==xml.read_bytes(),'Independent reader found different invoice XML');require('/AFRelationship /Data' in '\n'.join(doc.xref_object(i) for i in range(1,doc.xref_length())),'Missing explicit MINIMUM associated-file relationship')
a.report.write_text(json.dumps({'productionQualified':False,'invoiceConformanceValidated':False,'pdfAConformanceValidated':False,'scope':'Controlled unqualified draft: XML namespace/syntax, exact embedded bytes and literal decimal/readable totals only','readerVersion':fitz.VersionBind,'xmlReaderVersion':etree.LXML_VERSION,'xmlSha256':sha(xml),'draftPdfSha256':sha(pdf),'net':net,'tax':tax,'gross':gross,'pages':len(doc),'limitations':['No edition-specific complete schema/business-rule validator was used; this is not invoice compliance evidence.','No complete independent PDF/A validator was used on this draft; contract validators are binding-only fixtures.']},indent=2)+'\n');print('Independent controlled invoice syntax/attachment/totals checks passed; no conformance claim.')
