using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Fluent.Pdf.Formats;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class TextInterchangeTests
{
    private static byte[] Save(PdfDocument document) { using var stream = new MemoryStream(); document.Save(stream); return stream.ToArray(); }
    private static string Hash(byte[] bytes) { using var hash = SHA256.Create(); return string.Concat(hash.ComputeHash(bytes).Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture))); }
    private static byte[] Export(byte[] bytes, PdfTextInterchangeFormat format, PdfTextInterchangeBlock[] blocks, PdfTextInterchangeOptions? options = null, PdfProviderRegistry? providers = null)
    { using var input = new MemoryStream(bytes); using var output = new MemoryStream(); PdfTextInterchange.Write(input, output, format, blocks, options ?? new(Hash(bytes)), providers, new(PdfProcessingMode.Permissive)); Assert.True(input.CanRead); Assert.True(output.CanWrite); return output.ToArray(); }
    private static string Artifact(string name) { string directory = Path.Combine(AppContext.BaseDirectory, "artifacts", "text-interchange"); Directory.CreateDirectory(directory); return Path.Combine(directory, name); }
    private static PdfDocument Text(string text)
    {
        var hex = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch <= 127) hex.Append(((int)ch).ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            else if (ch == 'Ա') hex.Append("80");
            else if (ch == '\ud83d' && i + 1 < text.Length && text[i + 1] == '\ude00') { hex.Append("81"); i++; }
            else throw new InvalidOperationException("Unsupported original fixture character");
        }
        return ExtractionTests.Fixture("BT /F1 10 Tf 1 0 0 1 20 100 Tm <" + hex + "> Tj ET");
    }

    [Fact]
    public void CallerRolesKeepLiteralUnicodeAndCannotIntroduceActiveMarkup()
    {
        const string literal = "Ա😀 cafe [x](javascript:x) <script>x</script> & * _ ` |";
        using var document = Text(literal); byte[] bytes = Save(document);
        var roles = new[] { PdfTextInterchangeRole.Heading, PdfTextInterchangeRole.Paragraph, PdfTextInterchangeRole.Bullet, PdfTextInterchangeRole.Numbered, PdfTextInterchangeRole.Code, PdfTextInterchangeRole.Link };
        var blocks = roles.Select(role => new PdfTextInterchangeBlock(0, role, PdfTextInterchangeOrder.Raw, headingLevel: 2, number: 7, link: role == PdfTextInterchangeRole.Link ? "https://example.invalid/a?x=1&y=2" : null)).ToArray();
        byte[] output = Export(bytes, PdfTextInterchangeFormat.Markdown, blocks);
        string markdown = Encoding.UTF8.GetString(output);
        Assert.Contains(Hash(bytes), markdown); Assert.Contains("roles=caller-assigned", markdown);
        Assert.Contains("## Ա😀&#32;cafe", markdown); Assert.Contains("7. Ա😀", markdown);
        Assert.Contains("```\n" + literal + "\n```", markdown);
        Assert.DoesNotContain("<script>", markdown.Split(new[] { "```" }, StringSplitOptions.None)[0]);
        File.WriteAllBytes(Artifact("roles.pdf"), bytes); File.WriteAllBytes(Artifact("roles.md"), output); File.WriteAllText(Artifact("roles.txt"), literal, new UTF8Encoding(false));
    }

    [Fact]
    public void SourceMismatchAndStrictLossPreserveExistingOutput()
    {
        using var document = Text("literal"); byte[] bytes = Save(document);
        using var input = new MemoryStream(bytes); using var output = new MemoryStream(); output.WriteByte(42);
        var blocks = new[] { new PdfTextInterchangeBlock(0) };
        Assert.Equal("TextInterchange.SourceMismatch", Assert.Throws<PdfException>(() => PdfTextInterchange.Write(input, output, PdfTextInterchangeFormat.Markdown, blocks, new(new string('0', 64)))).Diagnostic.Code);
        input.Position = 0;
        Assert.Equal("TextInterchange.Reconstruction", Assert.Throws<PdfException>(() => PdfTextInterchange.Write(input, output, PdfTextInterchangeFormat.Markdown, blocks, new(Hash(bytes)))).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, output.ToArray());
    }

    [Fact]
    public void ExplicitRawCharacterRangesAndRepeatedPagesRemainOrdered()
    {
        using var document = Text("ABCDEF"); var bytes = Save(document);
        var blocks = new[] { new PdfTextInterchangeBlock(0, order: PdfTextInterchangeOrder.Raw, firstCharacter: 3, characterCount: 3), new PdfTextInterchangeBlock(0, order: PdfTextInterchangeOrder.Raw, firstCharacter: 0, characterCount: 3) };
        string text = Encoding.UTF8.GetString(Export(bytes, PdfTextInterchangeFormat.PlainText, blocks));
        Assert.True(text.IndexOf("DEF", StringComparison.Ordinal) < text.IndexOf("ABC", StringComparison.Ordinal));
        Assert.Contains("raw-range=3:3", text);
        Assert.Throws<PdfException>(() => Export(bytes, PdfTextInterchangeFormat.Markdown, new[] { new PdfTextInterchangeBlock(0, order: PdfTextInterchangeOrder.Raw, firstCharacter: 5, characterCount: 2) }));
        File.WriteAllText(Artifact("ranges.txt"), text, new UTF8Encoding(false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void TablesKeepCellTextAndExplicitHeaderPolicy(int headerRows)
    {
        using var document = TableExtractionTests.Fixture(merged: false, left: "A|B", right: "<x>&"); byte[] bytes = Save(document);
        var blocks = new[] { new PdfTextInterchangeBlock(0, PdfTextInterchangeRole.Table, table: new(PdfTableDetectionMode.Ruled, headerRows: headerRows)) };
        byte[] output = Export(bytes, PdfTextInterchangeFormat.Markdown, blocks);
        string markdown = Encoding.UTF8.GetString(output); Assert.Contains("A&#124;B", markdown); Assert.Contains("&#60;x&#62;&#38;", markdown);
        Assert.Equal(headerRows == 0, markdown.Contains("Column&#32;1"));
        File.WriteAllBytes(Artifact("table-" + headerRows + ".md"), output);
        File.WriteAllBytes(Artifact("table.pdf"), bytes);
    }

    [Fact]
    public void MergedCellsAreNotSilentlyFlattened()
    {
        using var document = TableExtractionTests.Fixture(); byte[] bytes = Save(document);
        Assert.Equal("TextInterchange.TableSpansUnsupported", Assert.Throws<PdfException>(() => Export(bytes, PdfTextInterchangeFormat.Markdown, new[] { new PdfTextInterchangeBlock(0, PdfTextInterchangeRole.Table, table: new(PdfTableDetectionMode.Ruled)) })).Diagnostic.Code);
    }

    [Fact]
    public void PageImagesUseDeterministicFlatBundleNamesWithoutPaths()
    {
        using var document = Pdf.Create().AddPage(new PdfPageSize(20, 10)); byte[] bytes = Save(document);
        var options = new PdfTextInterchangeOptions(Hash(bytes), rendering: new(72, 72));
        var blocks = new[] { new PdfTextInterchangeBlock(0, PdfTextInterchangeRole.PageImage), new PdfTextInterchangeBlock(0, PdfTextInterchangeRole.PageImage) };
        byte[] first = Export(bytes, PdfTextInterchangeFormat.MarkdownBundle, blocks, options, new(new RasterViewTests.PatternRenderer()));
        byte[] second = Export(bytes, PdfTextInterchangeFormat.MarkdownBundle, blocks, options, new(new RasterViewTests.PatternRenderer()));
        Assert.Equal(first, second);
        using var archive = new ZipArchive(new MemoryStream(first), ZipArchiveMode.Read);
        Assert.Equal(new[] { "document.md", "page-0.png", "page-1.png" }, archive.Entries.Select(e => e.FullName));
        File.WriteAllBytes(Artifact("images.zip"), first);
    }

    [Fact]
    public void ImagesRequireBundleAndExplicitRenderer()
    {
        using var document = Pdf.Create().AddPage(new PdfPageSize(20, 10)); byte[] bytes = Save(document);
        var blocks = new[] { new PdfTextInterchangeBlock(0, PdfTextInterchangeRole.PageImage) };
        Assert.Equal("TextInterchange.ImageBundleRequired", Assert.Throws<PdfException>(() => Export(bytes, PdfTextInterchangeFormat.Markdown, blocks)).Diagnostic.Code);
        Assert.Throws<PdfProviderUnavailableException>(() => Export(bytes, PdfTextInterchangeFormat.MarkdownBundle, blocks, new(Hash(bytes), rendering: new(72, 72))));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///private")]
    [InlineData("https://user:password@example.invalid/")]
    [InlineData("https://example.invalid/<script>")]
    [InlineData("https://example.invalid/\n")]
    public void UnsafeLinksRejectDuringOptionsConstruction(string link)
        => Assert.Throws<ArgumentException>(() => new PdfTextInterchangeBlock(0, PdfTextInterchangeRole.Link, link: link));

    [Fact]
    public void InvalidSelectionsBudgetsAndCancellationPreserveAtomicFile()
    {
        using var document = Text("bounded"); byte[] bytes = Save(document);
        Assert.Throws<ArgumentException>(() => new PdfTextInterchangeBlock(0, firstCharacter: 0, characterCount: 2));
        Assert.Throws<ArgumentException>(() => new PdfTextInterchangeOptions("not-a-hash"));
        Assert.Throws<PdfException>(() => Export(bytes, PdfTextInterchangeFormat.Markdown, Array.Empty<PdfTextInterchangeBlock>()));
        Assert.Throws<PdfException>(() => Export(bytes, PdfTextInterchangeFormat.Markdown, new[] { new PdfTextInterchangeBlock(2) }));
        string path = Artifact("preserved.md"); File.WriteAllText(path, "original");
        using var input = new MemoryStream(bytes);
        Assert.Throws<PdfException>(() => PdfTextInterchange.WriteFile(input, path, PdfTextInterchangeFormat.Markdown, new[] { new PdfTextInterchangeBlock(0) }, new(Hash(bytes), maximumOutputBytes: 1), policy: new(PdfProcessingMode.Permissive)));
        Assert.Equal("original", File.ReadAllText(path)); input.Position = 0;
        Assert.ThrowsAny<OperationCanceledException>(() => PdfTextInterchange.WriteFile(input, path, PdfTextInterchangeFormat.Markdown, new[] { new PdfTextInterchangeBlock(0) }, new(Hash(bytes)), cancellationToken: new(true)));
        Assert.Equal("original", File.ReadAllText(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LanguageExamplesExportSourceBoundMarkdown(bool visualBasic)
    {
        using var document = Text("example Ա😀"); byte[] bytes = Save(document);
        using var input = new MemoryStream(bytes); using var output = new MemoryStream();
        if (visualBasic) ContractExamples.VisualBasic.ContractExample.ExportMarkdown(input, output, Hash(bytes), 0);
        else ContractExamples.CSharp.ContractExample.ExportMarkdown(input, output, Hash(bytes), 0);
        Assert.Contains("example", Encoding.UTF8.GetString(output.ToArray()));
        File.WriteAllBytes(Artifact(visualBasic ? "visual-basic.md" : "csharp.md"), output.ToArray());
    }
}
