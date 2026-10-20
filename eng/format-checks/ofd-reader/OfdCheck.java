import java.nio.file.*;
import javax.imageio.ImageIO;
import org.ofdrw.reader.OFDReader;
import org.ofdrw.converter.ImageMaker;
// Development analysis of our controlled original fixture only. No SDK runtime integration.
class OfdCheck {
    public static void main(String[] args) throws Exception {
        Path input=Path.of(args[0]), output=Path.of(args[1]); Files.createDirectories(output);
        try (var reader=new OFDReader(input)) {
            if(reader.getNumberOfPages()!=3 || reader.hasSignature()) throw new IllegalStateException("Unexpected controlled page/signature inventory");
            var maker=new ImageMaker(reader,72d/25.4); var dimensions=new StringBuilder();
            for(int i=0;i<3;i++) {
                var size=reader.getPageInfo(i+1).getSize(); dimensions.append(size.getWidth()).append('\t').append(size.getHeight()).append('\n');
                var page=maker.makePage(i); if(page==null || !ImageIO.write(page,"PNG",output.resolve("page-"+i+".png").toFile())) throw new IllegalStateException("Actual OFD image rendering failed");
            }
            Files.writeString(output.resolve("geometry.tsv"),dimensions);
        }
    }
}
