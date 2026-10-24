"""Independent source observations; never complete invoice/business-rule qualification."""
import argparse, hashlib, json
from pathlib import Path
import fitz
from lxml import etree
from pypdf import PdfReader


def verify(directory):
    expected = {"EUR-direct": ("EUR", "7.01", "0.50", "7.51"), "JPY-direct": ("JPY", "7", "0", "7"), "BHD-direct": ("BHD", "7.00", "0.50", "7.50"), "EUR-csharp": ("EUR", "7.01", "0.50", "7.51"), "EUR-visual-basic": ("EUR", "7.01", "0.50", "7.51")}
    records = []
    for name, (currency, net, tax, gross) in expected.items():
        pdf, xml = directory / (name + ".pdf"), directory / (name + ".xml")
        root = etree.fromstring(xml.read_bytes(), etree.XMLParser(resolve_entities=False, no_network=True, load_dtd=False))
        ns = {"ram": "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100"}
        def value(n):
            nodes = root.findall(".//ram:" + n, ns)
            if len(nodes) != 1: raise ValueError("Ambiguous invoice field " + n)
            return nodes[0].text
        assert (value("InvoiceCurrencyCode"), value("TaxBasisTotalAmount"), value("TaxTotalAmount"), value("GrandTotalAmount"), value("DuePayableAmount")) == (currency, net, tax, gross, gross)
        reader = PdfReader(pdf)
        assert reader.pdf_header == "%PDF-1.7"
        catalog = reader.trailer["/Root"]
        assert len(catalog["/AF"]) == 1 and len(catalog["/OutputIntents"]) == 1
        file = catalog["/AF"][0].get_object()
        assert file["/AFRelationship"] == "/Data" and file["/F"] == file["/UF"] == "factur-x.xml"
        assert file["/EF"]["/F"].get_data() == xml.read_bytes() == file["/EF"]["/UF"].get_data()
        packet = etree.fromstring(catalog["/Metadata"].get_data(), etree.XMLParser(resolve_entities=False, no_network=True))
        fx = "{urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#}"
        for field, expected_value in [("DocumentType", "INVOICE"), ("DocumentFileName", "factur-x.xml"), ("Version", "1.0"), ("ConformanceLevel", "MINIMUM")]:
            found = list(packet.iter(fx + field))
            assert len(found) == 1 and found[0].text == expected_value
        with fitz.open(pdf) as doc:
            text = "\n".join(page.get_text() for page in doc)
            assert all((v + "0" if currency == "BHD" else v) + " " + currency in text for v in (net, tax, gross))
            assert doc.embfile_names() == ["factur-x.xml"] and doc.embfile_get("factur-x.xml") == xml.read_bytes()
            assert len(doc) == len(reader.pages) > 0
        records.append({"fixture": name, "pdfSha256": hashlib.sha256(pdf.read_bytes()).hexdigest(), "xmlSha256": hashlib.sha256(xml.read_bytes()).hexdigest(), "pages": len(reader.pages)})
    return {"purpose": "independent-source-invoice-attachment-metadata-readable-total-observations", "productionQualified": False, "completeInvoiceValidation": False, "pymupdfVersion": fitz.VersionBind, "lxmlVersion": etree.LXML_VERSION, "observations": records}


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("directory", type=Path)
    p.add_argument("output", type=Path)
    args = p.parse_args()
    result = verify(args.directory)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("Five independent invoice candidate observations passed; complete invoice qualification remains pending.")
