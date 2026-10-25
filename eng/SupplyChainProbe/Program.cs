using System.Security.Cryptography;
using System.Text.Json;
using NuGet.Packaging;

// Offline package metadata and NuGet content hashing. No extraction or trust-chain download.
if (args.Length != 2) throw new ArgumentException("Supply request JSON and a new output path.");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
File.Delete(args[1]);
using var request = JsonDocument.Parse(File.ReadAllBytes(args[0]));
var rows = new List<object>();
foreach (var item in request.RootElement.EnumerateArray())
{
    string path = item.GetProperty("path").GetString()!;
    if (new FileInfo(path).Length > 512L * 1024 * 1024) throw new InvalidDataException("Package size limit.");
    using var reader = new PackageArchiveReader(path);
    var identity = reader.GetIdentity();
    string hash = reader.GetContentHash(CancellationToken.None);
    if (!string.Equals(identity.Id, item.GetProperty("id").GetString(), StringComparison.OrdinalIgnoreCase) ||
        identity.Version.ToNormalizedString() != item.GetProperty("version").GetString() ||
        hash != item.GetProperty("contentHash").GetString())
        throw new InvalidDataException("Package identity/content hash differs from dependency lock.");
    using var stream = File.OpenRead(path);
    rows.Add(new { id = identity.Id, version = identity.Version.ToNormalizedString(), nugetContentHash = hash,
        archiveSha256 = Convert.ToHexStringLower(SHA256.HashData(stream)) });
}
var tool = typeof(PackageArchiveReader).Assembly;
var report = new { schemaVersion = 1, packages = rows, toolVersion = tool.GetName().Version!.ToString(),
    toolSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(tool.Location))), signatureTrustValidated = false };
File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
