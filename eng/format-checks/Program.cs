using System.Security.Cryptography;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
if (args.Length != 2) throw new ArgumentException("Expected controlled fixture path and output report path.");
string input = Path.GetFullPath(args[0]), report = Path.GetFullPath(args[1]);
Directory.CreateDirectory(Path.GetDirectoryName(report)!);
if (File.Exists(report))
{
    byte[] previous = File.ReadAllBytes(report); string archived = report + ".previous-" + Convert.ToHexString(SHA256.HashData(previous));
    if (File.Exists(archived)) { if (!File.ReadAllBytes(archived).SequenceEqual(previous)) throw new InvalidDataException("Evidence archive differs."); }
    else File.WriteAllBytes(archived, previous);
    File.Delete(report);
}
using var document = input.EndsWith(".pptx", StringComparison.Ordinal) ? (OpenXmlPackage)PresentationDocument.Open(input, false) : SpreadsheetDocument.Open(input, false);
var failures = new OpenXmlValidator(FileFormatVersions.Office2019).Validate(document).Select(e => new { e.Id, e.Description, Part = e.Part?.Uri.ToString(), Path = e.Path?.XPath }).ToArray();
File.WriteAllText(report, JsonSerializer.Serialize(new { schemaVersion = 1, purpose = "controlled-original-format-source-validation", validator = "DocumentFormat.OpenXml", version = "3.3.0", formatVersion = "Office2019", artifact = Path.GetFileName(input), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))), failureCount = failures.Length, failures, productionQualified = false }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
Console.WriteLine("Open XML 3.3.0 Office2019 fixture validation failures: " + failures.Length);
return failures.Length == 0 ? 0 : 1;
