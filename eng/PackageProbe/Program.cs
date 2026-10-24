using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

// Offline metadata only. Inspected code is never loaded or invoked.
if (args.Length != 4) throw new ArgumentException("Supply assembly, portable PDB, source commit and report path.");
File.Delete(args[3]); // A failed repeat cannot leave a stale passing report.
string assembly = Path.GetFullPath(args[0]), pdb = Path.GetFullPath(args[1]), commit = args[2];
if (!System.Text.RegularExpressions.Regex.IsMatch(commit, "^[a-f0-9]{40}$")) throw new ArgumentException("Invalid source commit.");
using var peStream = File.OpenRead(assembly);
using var pe = new PEReader(peStream);
using var pdbStream = File.OpenRead(pdb);
using var provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
var reader = provider.GetMetadataReader();
var header = reader.DebugMetadataHeader ?? throw new InvalidDataException("Missing portable PDB identity.");
var id = new BlobContentId(header.Id);
var codeView = pe.ReadDebugDirectory().Where(d => d.Type == DebugDirectoryEntryType.CodeView).ToArray();
if (codeView.Length != 1) throw new InvalidDataException("Missing unique assembly PDB binding.");
var code = pe.ReadCodeViewDebugDirectoryData(codeView[0]);
if (code.Guid != id.Guid || codeView[0].Stamp != id.Stamp || code.Age != 1)
    throw new InvalidDataException("Assembly/PDB identities differ.");
if (!pe.ReadDebugDirectory().Any(d => d.Type == DebugDirectoryEntryType.Reproducible))
    throw new InvalidDataException("Missing deterministic assembly marker.");
var sourceLinkKind = new Guid("CC110556-A091-4D38-9FEC-25AB9A351A6A");
var links = reader.CustomDebugInformation.Select(reader.GetCustomDebugInformation)
    .Where(d => reader.GetGuid(d.Kind) == sourceLinkKind).ToArray();
if (links.Length != 1) throw new InvalidDataException("Missing unique SourceLink record.");
using var sourceLink = JsonDocument.Parse(reader.GetBlobBytes(links[0].Value));
var documents = sourceLink.RootElement.GetProperty("documents");
string prefix = "https://raw.githubusercontent.com/kirakosyan/pdf.net/" + commit + "/";
if (!documents.EnumerateObject().Any() || documents.EnumerateObject().Any(p => !p.Name.EndsWith('*') || p.Value.GetString() != prefix + "*"))
    throw new InvalidDataException("SourceLink does not bind the exact repository commit.");
var documentNames = reader.Documents.Select(h => reader.GetString(reader.GetDocument(h).Name)).ToArray();
if (documentNames.Length == 0 || documentNames.Any(p => !p.StartsWith("/_/", StringComparison.Ordinal)))
    throw new InvalidDataException("Unmapped source path in portable PDB.");
var metadata = pe.GetMetadataReader();
var definition = metadata.GetAssemblyDefinition();
var result = new { schemaVersion = 1, assembly = metadata.GetString(definition.Name),
    version = definition.Version.ToString(), sourceCommit = commit,
    assemblySha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly))),
    pdbSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(pdb))),
    documentCount = documentNames.Length, sourceLink = sourceLink.RootElement.Clone(), productionQualified = false };
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!);
File.WriteAllText(args[3], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) + "\n");
