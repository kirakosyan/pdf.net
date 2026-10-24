/* Original independent source-validation host. Standards assets and PDFBox/Saxon are explicit pinned external inputs. */
import java.io.*;
import java.math.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.security.*;
import java.util.*;
import java.awt.geom.*;
import javax.xml.XMLConstants;
import javax.xml.parsers.*;
import javax.xml.transform.*;
import javax.xml.transform.dom.DOMSource;
import javax.xml.transform.stream.*;
import javax.xml.validation.*;
import org.w3c.dom.*;
import org.w3c.dom.ls.*;
import org.xml.sax.*;
import org.apache.pdfbox.Loader;
import org.apache.pdfbox.cos.*;
import org.apache.pdfbox.contentstream.*;
import org.apache.pdfbox.contentstream.operator.Operator;
import org.apache.pdfbox.pdmodel.*;
import org.apache.pdfbox.pdmodel.common.filespecification.*;
import org.apache.pdfbox.pdmodel.font.*;
import org.apache.pdfbox.pdmodel.graphics.image.*;
import org.apache.pdfbox.pdmodel.graphics.state.RenderingMode;
import org.apache.pdfbox.pdmodel.graphics.blend.BlendMode;
import org.apache.pdfbox.text.PDFTextStripper;
import org.apache.pdfbox.util.Matrix;

public final class InvoiceValidationHost {
    private static final String RAM="urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
    private static final String FX="urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#";
    private static final String BUNDLE="711f08a9dab037032c8ddafcf0dd2bdb0ee80f7beec152e93a3289f7e49bee77";
    private static final Map<String,String> PINS=Map.ofEntries(
        Map.entry("FACTUR-X_MINIMUM.xsd","fd3b2d1a850d2dcab9d5eda7749e66f6ad031c9b0a3866494d85f88012c5171c"),
        Map.entry("FACTUR-X_MINIMUM_urn_un_unece_uncefact_data_standard_QualifiedDataType_100.xsd","c65c4929cb6f51d59aec498851a9026f0a915555555b1c3e5eafd99f191be5f5"),
        Map.entry("FACTUR-X_MINIMUM_urn_un_unece_uncefact_data_standard_ReusableAggregateBusinessInformationEntity_100.xsd","bbec5def7921c02fddb531b4aa527dd9db56d37c5109b66ff7bdeeea652d40b5"),
        Map.entry("FACTUR-X_MINIMUM_urn_un_unece_uncefact_data_standard_UnqualifiedDataType_100.xsd","43042860a12cc9921df64486de7238756d382b7d92acdea1a4df25483c757dd4"),
        Map.entry("FACTUR-X_MINIMUM.sch","0efd521bc52b734c344ddc17e9ef2a69b7f600639b903021871859ca8931a148"),
        Map.entry("FACTUR-X_MINIMUM.xslt","2b96e354873ce19c25e6fcc71ceb655a80d0a0fbaa1ab7f16c24489c5e7e209b"),
        Map.entry("FACTUR-X_MINIMUM_codedb.xml","e00a34bd04288aabe23b3f831b86d0fe51fd9beafb65fc98bce50e3f66c67eb7"));
    private final LinkedHashMap<String,Phase> phases=new LinkedHashMap<>();
    private final TreeSet<String> findings=new TreeSet<>();
    private final Map<String,byte[]> assets=new HashMap<>();
    private record Phase(boolean complete,boolean pass,long checks){}
    private static byte[] bytes(Path p,long maximum)throws Exception {if(!p.isAbsolute()||Files.isSymbolicLink(p)||Files.size(p)>maximum)throw new IOException("Explicit bounded input required");return Files.readAllBytes(p);}
    private static String sha(byte[] b)throws Exception {return HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(b));}
    private static Document parse(byte[] b)throws Exception {
        DocumentBuilderFactory f=DocumentBuilderFactory.newInstance();f.setNamespaceAware(true);f.setFeature("http://apache.org/xml/features/disallow-doctype-decl",true);f.setFeature("http://xml.org/sax/features/external-general-entities",false);f.setFeature("http://xml.org/sax/features/external-parameter-entities",false);f.setXIncludeAware(false);f.setExpandEntityReferences(false);
        f.setAttribute(XMLConstants.ACCESS_EXTERNAL_DTD,"");f.setAttribute(XMLConstants.ACCESS_EXTERNAL_SCHEMA,"");DocumentBuilder d=f.newDocumentBuilder();d.setErrorHandler(new ErrorHandler(){public void warning(SAXParseException e)throws SAXException{throw e;}public void error(SAXParseException e)throws SAXException{throw e;}public void fatalError(SAXParseException e)throws SAXException{throw e;}});return d.parse(new ByteArrayInputStream(b));
    }
    private static String one(Element e,String ns,String name)throws Exception {NodeList n=e.getElementsByTagNameNS(ns,name);if(n.getLength()!=1)throw new IOException("Ambiguous field");return n.item(0).getTextContent();}
    private static String model(Element e,String name)throws Exception {int count=0;String value=null;for(Node n=e.getFirstChild();n!=null;n=n.getNextSibling())if(n instanceof Element x && x.getTagName().equals(name)){count++;value=x.getTextContent();}if(count!=1)throw new IOException("Model field");return value;}
    private static BigDecimal decimal(Element e,String name)throws Exception {return new BigDecimal(model(e,name));}
    private void phase(String name,String code,Checked test){try{long checks=test.run();if(checks<1)throw new IOException("Empty phase");phases.put(name,new Phase(true,true,checks));}catch(Exception failure){if(Boolean.getBoolean("fluent.pdf.invoice.originalFixtureDiagnostics"))System.err.println(name+": "+failure.getClass().getName()+": "+failure.getMessage());findings.add(failure instanceof Unavailable ? "Invoice.PaintProfile" : code);phases.put(name,new Phase(!(failure instanceof Unavailable),false,1));}}
    private interface Checked {long run()throws Exception;}
    private static final class Unavailable extends IOException {Unavailable(){super("Readable painting profile is unavailable");}}
    private void load(Path root)throws Exception {for(var p:PINS.entrySet()){byte[] b=bytes(root.resolve(p.getKey()),1048576);if(!sha(b).equals(p.getValue()))throw new IOException("Rule pin mismatch");assets.put(p.getKey(),b);}StringBuilder catalog=new StringBuilder();for(String n:new TreeSet<>(PINS.keySet()))catalog.append(n).append('\t').append(PINS.get(n)).append('\n');if(!sha(catalog.toString().getBytes(StandardCharsets.UTF_8)).equals(BUNDLE))throw new IOException("Bundle mismatch");}
    private byte[] asset(String system)throws Exception {
        String name=system.substring(system.lastIndexOf('/')+1);if(!system.equals(name)&&!system.equals("https://fluent-pdf.invalid/rules/"+name))throw new IOException("External resolution denied");
        // The pinned upstream bundle renamed companion files but retained edition-prefixed schemaLocation values.
        // Resolve those exact names to the same unmodified, hash-verified companion bytes; never fetch an alternate schema.
        if(name.startsWith("Factur-X_1.0.07_MINIMUM_")&&name.endsWith(".xsd"))name="FACTUR-X_MINIMUM_"+name.substring("Factur-X_1.0.07_MINIMUM_".length());
        if(!assets.containsKey(name))throw new IOException("External resolution denied");return assets.get(name);
    }
    private void schema(Document xml)throws Exception {
        SchemaFactory sf=SchemaFactory.newInstance(XMLConstants.W3C_XML_SCHEMA_NS_URI);sf.setFeature(XMLConstants.FEATURE_SECURE_PROCESSING,true);sf.setProperty(XMLConstants.ACCESS_EXTERNAL_DTD,"");sf.setProperty(XMLConstants.ACCESS_EXTERNAL_SCHEMA,"");
        sf.setResourceResolver((type,ns,pub,sys,base)->{try{return new Input(sys,asset(sys));}catch(Exception e){if(Boolean.getBoolean("fluent.pdf.invoice.originalFixtureDiagnostics"))System.err.println("Schema requested: "+sys+" base="+base);throw new IllegalArgumentException("Schema resolution denied");}});
        StreamSource source=new StreamSource(new ByteArrayInputStream(assets.get("FACTUR-X_MINIMUM.xsd")));source.setSystemId("https://fluent-pdf.invalid/rules/FACTUR-X_MINIMUM.xsd");sf.newSchema(source).newValidator().validate(new DOMSource(xml));
    }
    private long business(Document xml)throws Exception {
        var factory=new net.sf.saxon.TransformerFactoryImpl();factory.setFeature(XMLConstants.FEATURE_SECURE_PROCESSING,true);factory.setAttribute("http://saxon.sf.net/feature/allow-external-functions",false);
        URIResolver resolver=(href,base)->{try{StreamSource source=new StreamSource(new ByteArrayInputStream(asset(href)));source.setSystemId("https://fluent-pdf.invalid/rules/"+href);return source;}catch(Exception e){throw new TransformerException("Rule resource resolution denied");}};factory.setURIResolver(resolver);
        StreamSource style=new StreamSource(new ByteArrayInputStream(assets.get("FACTUR-X_MINIMUM.xslt")));style.setSystemId("https://fluent-pdf.invalid/rules/FACTUR-X_MINIMUM.xslt");var transformer=factory.newTransformer(style);transformer.setURIResolver(resolver);ByteArrayOutputStream out=new ByteArrayOutputStream();transformer.transform(new DOMSource(xml),new StreamResult(new LimitedOutput(out,4194304)));
        Document report=parse(out.toByteArray());String svrl="http://purl.oclc.org/dsdl/svrl";NodeList fired=report.getElementsByTagNameNS(svrl,"fired-rule");if(fired.getLength()==0)throw new IOException("No business rules fired");
        NodeList failed=report.getElementsByTagNameNS(svrl,"failed-assert");for(int i=0;i<failed.getLength();i++){String id=((Element)failed.item(i)).getAttribute("id");if(!id.matches("FX-SCH-A-[0-9]{6}"))throw new IOException("Unknown rule code");findings.add(id);}
        if(failed.getLength()!=0||report.getElementsByTagNameNS(svrl,"successful-report").getLength()!=0)throw new IOException("Independent business rule failed");return fired.getLength();
    }
    private long reconcile(Document xml,Element data,List<String> readable)throws Exception {
        int digits=Integer.parseInt(model(data,"digits"));String currency=model(data,"currency");int expected=switch(currency){case "EUR","USD","GBP"->2;case "JPY"->0;case "BHD"->3;default->-1;};if(digits!=expected)throw new IOException("Currency model");
        RoundingMode rounding=switch(model(data,"rounding")){case "half-up"->RoundingMode.HALF_UP;case "half-even"->RoundingMode.HALF_EVEN;default->throw new IOException("Rounding model");};BigDecimal net=BigDecimal.ZERO;TreeMap<BigDecimal,BigDecimal> bases=new TreeMap<>();
        NodeList lines=data.getElementsByTagName("line");if(lines.getLength()<1||lines.getLength()>1000)throw new IOException("Line count");
        readable.add("Invoice "+model(data,"identifier"));readable.add("Seller: "+model(data,"seller"));readable.add("Seller registration: "+model(data,"sellerRegistrationIdentifier"));readable.add("Seller country: "+model(data,"sellerCountry"));readable.add("Buyer: "+model(data,"buyer"));readable.add("Date: "+model(data,"issueDate"));
        for(int i=0;i<lines.getLength();i++){Element line=(Element)lines.item(i);BigDecimal q=decimal(line,"quantity"),p=decimal(line,"price"),r=decimal(line,"rate");if(q.signum()<=0||p.signum()<0||r.signum()<0||r.compareTo(BigDecimal.valueOf(100))>0)throw new IOException("Line domain");BigDecimal n=q.multiply(p).setScale(digits,rounding);equal(n,decimal(line,"net"));net=net.add(n);bases.merge(r,n,BigDecimal::add);readable.add(model(line,"description")+" | Quantity "+plain(q)+" | Unit "+plain(p)+" | Net "+money(n,digits)+" | Tax rate "+plain(r)+"% "+currency);}
        BigDecimal tax=BigDecimal.ZERO;NodeList categories=data.getElementsByTagName("category");if(categories.getLength()!=bases.size())throw new IOException("Category count");int index=0;for(var b:bases.entrySet()){BigDecimal t=b.getValue().multiply(b.getKey()).divide(BigDecimal.valueOf(100)).setScale(digits,rounding);Element cat=(Element)categories.item(index++);equal(b.getKey(),decimal(cat,"rate"));equal(b.getValue(),decimal(cat,"basis"));equal(t,decimal(cat,"tax"));tax=tax.add(t);readable.add("Tax rate "+plain(b.getKey())+"% | Basis "+money(b.getValue(),digits)+" | Tax "+money(t,digits)+" "+currency);}
        BigDecimal gross=net.add(tax);equal(net,decimal(data,"net"));equal(tax,decimal(data,"tax"));equal(gross,decimal(data,"gross"));
        equal(net,new BigDecimal(one(xml.getDocumentElement(),RAM,"TaxBasisTotalAmount")));equal(tax,new BigDecimal(one(xml.getDocumentElement(),RAM,"TaxTotalAmount")));equal(gross,new BigDecimal(one(xml.getDocumentElement(),RAM,"GrandTotalAmount")));equal(gross,new BigDecimal(one(xml.getDocumentElement(),RAM,"DuePayableAmount")));
        require(one(xml.getDocumentElement(),RAM,"InvoiceCurrencyCode").equals(currency));Element seller=element(xml,RAM,"SellerTradeParty"),buyer=element(xml,RAM,"BuyerTradeParty");require(one(seller,RAM,"Name").equals(model(data,"seller")));require(one(seller,RAM,"ID").equals(model(data,"sellerRegistrationIdentifier")));require(one(seller,RAM,"CountryID").equals(model(data,"sellerCountry")));require(one(buyer,RAM,"Name").equals(model(data,"buyer")));Element doc=element(xml,"urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100","ExchangedDocument");require(one(doc,RAM,"ID").equals(model(data,"identifier")));require(one(doc,"urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100","DateTimeString").equals(model(data,"issueDate").replace("-","")));
        readable.add("Net total "+money(net,digits)+" "+currency);readable.add("Tax total "+money(tax,digits)+" "+currency);readable.add("Grand total / Due "+money(gross,digits)+" "+currency);return lines.getLength()+categories.getLength()+16L;
    }
    private static Element element(Document d,String ns,String name)throws Exception {NodeList n=d.getElementsByTagNameNS(ns,name);require(n.getLength()==1);return (Element)n.item(0);}
    private static void require(boolean b)throws Exception {if(!b)throw new IOException("Independent invoice check failed");}
    private static void equal(BigDecimal a,BigDecimal b)throws Exception {require(a.compareTo(b)==0);}
    private static String plain(BigDecimal n){return n.toPlainString();}
    private static String money(BigDecimal n,int digits){return n.setScale(digits,RoundingMode.UNNECESSARY).toPlainString();}
    private long metadata(PDDocument pdf,byte[] xml)throws Exception {
        PDDocumentCatalog c=pdf.getDocumentCatalog();require(!pdf.isEncrypted()&&pdf.getNumberOfPages()>0&&pdf.getNumberOfPages()<=1000);require(c.getMetadata()!=null);Document xmp=parse(c.getMetadata().toByteArray());for(String[] p:new String[][]{{"DocumentType","INVOICE"},{"DocumentFileName","factur-x.xml"},{"Version","1.0"},{"ConformanceLevel","MINIMUM"}})require(one(xmp.getDocumentElement(),FX,p[0]).equals(p[1]));
        require(c.getNames()!=null&&c.getNames().getEmbeddedFiles()!=null);var tree=c.getNames().getEmbeddedFiles();require(tree.getKids()==null||tree.getKids().isEmpty());var files=tree.getNames();require(files!=null&&files.size()==1&&files.containsKey("factur-x.xml"));PDComplexFileSpecification f=files.get("factur-x.xml");require(f.getFilename().equals("factur-x.xml")&&f.getFileUnicode().equals("factur-x.xml"));COSDictionary fs=f.getCOSObject();require(fs.getNameAsString(COSName.getPDFName("AFRelationship")).equals("Data"));COSArray af=c.getCOSObject().getCOSArray(COSName.getPDFName("AF"));require(af!=null&&af.size()==1&&af.getObject(0)==fs);var ef=f.getEmbeddedFile();var uf=f.getEmbeddedFileUnicode();require(ef!=null&&uf!=null&&ef.getCOSObject()==uf.getCOSObject()&&ef.getSubtype().equals("application/xml"));byte[] embedded=read(ef.createInputStream(),1048576);require(Arrays.equals(embedded,xml));return 12;
    }
    private long readable(PDDocument pdf,List<String> expected)throws Exception {
        require(expected.size()>0);var catalog=pdf.getDocumentCatalog();require(catalog.getCOSObject().getDictionaryObject(COSName.OCPROPERTIES)==null&&catalog.getCOSObject().getDictionaryObject(COSName.ACRO_FORM)==null&&catalog.getCOSObject().getDictionaryObject(COSName.OPEN_ACTION)==null&&catalog.getCOSObject().getDictionaryObject(COSName.AA)==null);long glyphs=0;
        for(PDPage page:pdf.getPages()){if(!page.getAnnotations().isEmpty())throw new Unavailable();PaintAudit audit=new PaintAudit(page);audit.processPage(page);if(audit.unsupported)throw new Unavailable();if(Boolean.getBoolean("fluent.pdf.invoice.originalFixtureDiagnostics"))System.err.println("Paint: valid="+audit.valid+",glyphs="+audit.glyphs);require(audit.valid&&audit.glyphs>0);glyphs+=audit.glyphs;}
        PDFTextStripper stripper=new PDFTextStripper();stripper.setSortByPosition(false);stripper.setSuppressDuplicateOverlappingText(false);String actual=stripper.getText(pdf);if(Boolean.getBoolean("fluent.pdf.invoice.originalFixtureDiagnostics")){System.err.println("Actual: "+normalize(actual));System.err.println("Expected: "+normalize(String.join("\n",expected)));}require(normalize(actual).equals(normalize(String.join("\n",expected))));return glyphs;
    }
    private static String normalize(String text)throws Exception {require(text.length()<=4194304);return text.replaceAll("\\s+"," ").trim();}
    private static byte[] read(InputStream source,int maximum)throws Exception {try(source){ByteArrayOutputStream out=new ByteArrayOutputStream();source.transferTo(new LimitedOutput(out,maximum));return out.toByteArray();}}
    private void run(String[] args)throws Exception {
        if(args.length!=5)throw new IOException("Explicit PDF/XML/model/report/rules paths required");byte[] p=bytes(Path.of(args[0]),67108864),x=bytes(Path.of(args[1]),1048576),m=bytes(Path.of(args[2]),16777216);load(Path.of(args[4]));Document invoice=parse(x),model=parse(m);Element data=model.getDocumentElement();require(data.getTagName().equals("invoiceModel")&&data.getAttribute("schemaVersion").equals("1"));String request=data.getAttribute("requestSha256");require(request.matches("[0-9a-f]{64}"));List<String> expected=new ArrayList<>();
        phase("schema","Invoice.Xsd",()->{schema(invoice);return 1;});phase("business","Invoice.BusinessReport",()->business(invoice));phase("model","Invoice.Model",()->reconcile(invoice,data,expected));
        try(PDDocument pdf=Loader.loadPDF(p)){phase("metadata","Invoice.Metadata",()->metadata(pdf,x));phase("readable","Invoice.Readable",()->readable(pdf,expected));}
        StringBuilder report=new StringBuilder("<?xml version='1.0' encoding='UTF-8'?><invoiceValidation schemaVersion='1' engine='invoice.fx107-minimum.pdfbox3-saxon.v1' profile='Factur-X1.0.07-MINIMUM' bundleSha256='").append(BUNDLE).append("' pdfSha256='").append(sha(p)).append("' xmlSha256='").append(sha(x)).append("' modelSha256='").append(sha(m)).append("' requestSha256='").append(request).append("'>");for(var e:phases.entrySet())report.append('<').append(e.getKey()).append(" completed='").append(e.getValue().complete).append("' compliant='").append(e.getValue().pass).append("' checks='").append(e.getValue().checks).append("'/>");for(String code:findings)report.append("<finding code='").append(code).append("'/>");report.append("</invoiceValidation>");Files.writeString(Path.of(args[3]),report,StandardCharsets.UTF_8,StandardOpenOption.CREATE_NEW);
    }
    public static void main(String[] args){try{new InvoiceValidationHost().run(args);}catch(Throwable error){System.err.println("Independent invoice host failed; private data omitted.");System.exit(2);}}

    private static final class LimitedOutput extends FilterOutputStream {private final long maximum;private long count;LimitedOutput(OutputStream out,long max){super(out);maximum=max;}public void write(int b)throws IOException{if(++count>maximum)throw new IOException("Output bound");out.write(b);}public void write(byte[] b,int off,int n)throws IOException{if(n>maximum-count)throw new IOException("Output bound");count+=n;out.write(b,off,n);}}
    private static final class Input implements LSInput {private InputStream stream;private String id;Input(String id,byte[] data){this.id=id;stream=new ByteArrayInputStream(data);}public Reader getCharacterStream(){return null;}public void setCharacterStream(Reader r){throw new UnsupportedOperationException();}public InputStream getByteStream(){return stream;}public void setByteStream(InputStream s){stream=s;}public String getStringData(){return null;}public void setStringData(String s){throw new UnsupportedOperationException();}public String getSystemId(){return id;}public void setSystemId(String s){id=s;}public String getPublicId(){return null;}public void setPublicId(String s){}public String getBaseURI(){return "https://fluent-pdf.invalid/rules/";}public void setBaseURI(String s){}public String getEncoding(){return "UTF-8";}public void setEncoding(String s){}public boolean getCertifiedText(){return false;}public void setCertifiedText(boolean b){}}

    // Closed independent readable-paint audit. Arbitrary graphics/forms/OCG/annotations are not silently accepted as readable invoice evidence.
    private static final class PaintAudit extends PDFGraphicsStreamEngine {
        boolean valid=true,unsupported=false;long glyphs;GeneralPath path=new GeneralPath();final Map<Long,List<Area>> occupied=new HashMap<>();long cellVisits;
        PaintAudit(PDPage page){super(page);}
        protected void showGlyph(Matrix trm,PDFont font,int code,org.apache.pdfbox.util.Vector displacement)throws IOException {
            glyphs++;if(glyphs>1000000)throw new IOException("Glyph extent");var s=getGraphicsState();if(s.getTextState().getRenderingMode()!=RenderingMode.FILL||s.getNonStrokeAlphaConstant()!=1||s.getSoftMask()!=null||s.getBlendMode()!=BlendMode.NORMAL||s.getNonStrokingColor().toRGB()!=0||!font.isEmbedded()||!(font instanceof PDVectorFont vector)){valid=false;return;}
            String unicode=font.toUnicode(code);if(unicode==null||unicode.isEmpty()){valid=false;return;}
            if(font instanceof PDType0Font type0&&type0.getDescendantFont() instanceof PDCIDFontType2 cid){var cmap=cid.getTrueTypeFont().getUnicodeCmapLookup();if(unicode.codePointCount(0,unicode.length())!=1||cmap==null||cmap.getGlyphId(unicode.codePointAt(0))!=type0.codeToGID(code))throw new Unavailable();}else throw new Unavailable();
            var glyph=vector.getPath(code);if(glyph==null){valid=false;return;}AffineTransform at=trm.createAffineTransform();at.concatenate(font.getFontMatrix().createAffineTransform());Area area=new Area(at.createTransformedShape(glyph));Area outside=new Area(area);Area clip=new Area(s.getCurrentClippingPath());clip.add(new Area(new java.awt.BasicStroke(0.0002f).createStrokedShape(clip)));outside.subtract(clip);if(Boolean.getBoolean("fluent.pdf.invoice.originalFixtureDiagnostics")&&glyphs==1)System.err.println("First original glyph shape="+area.getBounds2D()+" clip="+s.getCurrentClippingPath().getBounds2D());if(!outside.isEmpty()){valid=false;if(Boolean.getBoolean("fluent.pdf.invoice.originalFixtureDiagnostics"))System.err.println("Clipped original glyph "+unicode+" shape="+area.getBounds2D()+" outside="+outside.getBounds2D()+" clip="+s.getCurrentClippingPath().getBounds2D());}
            if(!area.isEmpty()){
                Rectangle2D bounds=area.getBounds2D();if(!Double.isFinite(bounds.getX())||!Double.isFinite(bounds.getY())||Math.abs(bounds.getX())>1000000||Math.abs(bounds.getY())>1000000||bounds.getWidth()>4096||bounds.getHeight()>4096)throw new IOException("Glyph geometry extent");
                int x0=(int)Math.floor(bounds.getMinX()/32),x1=(int)Math.floor(bounds.getMaxX()/32),y0=(int)Math.floor(bounds.getMinY()/32),y1=(int)Math.floor(bounds.getMaxY()/32);
                Set<Area> observed=Collections.newSetFromMap(new IdentityHashMap<>());
                for(int xi=x0;xi<=x1;xi++)for(int yi=y0;yi<=y1;yi++){
                    if(++cellVisits>4000000)throw new IOException("Glyph spatial extent");long key=((long)xi<<32)^(yi&0xffffffffL);List<Area> entries=occupied.computeIfAbsent(key,k->new ArrayList<>());
                    for(Area old:entries)if(observed.add(old)&&old.getBounds2D().intersects(bounds)){Area overlap=new Area(area);overlap.intersect(old);if(!overlap.isEmpty())valid=false;}
                    entries.add(area);
                }
            }
        }
        protected void unsupportedOperator(Operator o,List<COSBase> values)throws IOException{valid=false;unsupported=true;if(Boolean.getBoolean("fluent.pdf.invoice.originalFixtureDiagnostics"))System.err.println("Unsupported operator: "+o.getName());}
        public void appendRectangle(Point2D a,Point2D b,Point2D c,Point2D d){path.moveTo(a.getX(),a.getY());path.lineTo(b.getX(),b.getY());path.lineTo(c.getX(),c.getY());path.lineTo(d.getX(),d.getY());path.closePath();}
        public void drawImage(PDImage image){valid=false;unsupported=true;}public void clip(int rule){path.setWindingRule(rule);getGraphicsState().intersectClippingPath(new Area(path));}public void moveTo(float x,float y){path.moveTo(x,y);}public void lineTo(float x,float y){path.lineTo(x,y);}public void curveTo(float a,float b,float c,float d,float e,float f){path.curveTo(a,b,c,d,e,f);}public Point2D getCurrentPoint(){return path.getCurrentPoint();}public void closePath(){path.closePath();}public void endPath(){path.reset();}public void strokePath(){valid=false;unsupported=true;path.reset();}public void fillPath(int rule){valid=false;unsupported=true;path.reset();}public void fillAndStrokePath(int rule){valid=false;unsupported=true;path.reset();}public void shadingFill(COSName name){valid=false;unsupported=true;}
    }
}
