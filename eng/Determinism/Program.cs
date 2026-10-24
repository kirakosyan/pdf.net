using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Fluent.Pdf;

if (args.Length != 3 || args[2] != "plain" && args[2] != "compressed") throw new ArgumentException("<corpus-directory> <fresh-output-directory> <plain|compressed>");
string corpus = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
if (Directory.Exists(output)) throw new IOException("Use a fresh output directory.");
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(corpus,"manifest.json")));
Directory.CreateDirectory(output);
string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
var records = new List<object>();
var timestamp = new DateTimeOffset(2000,1,2,3,4,5,TimeSpan.Zero);
var policy = new PdfOperationPolicy(limits:new(maximumInputBytes:1048576,maximumExpandedBytes:16777216,maximumOutputBytes:1048576,maximumObjects:10000,maximumPixels:1048576,maximumDepth:64,maximumWorkers:2,maximumElapsedTime:TimeSpan.FromSeconds(10)));
using var cache = new PdfContentCache(4,4194304,1048576);
using var pool = new PdfProcessingPool(new(1,1,1048576),new(operationPolicy:policy));
foreach(var fixture in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
{
    string file=fixture.GetProperty("file").GetString()!;
    if(Path.GetFileName(file)!=file || !file.EndsWith(".pdf",StringComparison.Ordinal)) throw new InvalidDataException("Flat PDF paths required.");
    string path=Path.Combine(corpus,file);
    if(new FileInfo(path).Length>1048576)throw new InvalidDataException("Input exceeds ceiling.");
    byte[] bytes=File.ReadAllBytes(path);
    if(Hash(bytes)!=fixture.GetProperty("sha256").GetString())throw new InvalidDataException("Pinned source mismatch.");
    string destination=Path.Combine(output,file);
    if(!pool.TrySubmit(bytes,job=>
    {
        string address=cache.Store(bytes,job.Context);
        if(address!=Hash(bytes)||!cache.TryGet(address,job.Context,out var captured))throw new InvalidOperationException("Cache identity mismatch.");
        using var original=Pdf.Open(new MemoryStream(captured!,false),new(operationPolicy:policy));
        byte[] identity=SHA256.HashData(bytes).Take(16).ToArray();
        using var edited=PdfMetadata.Edit(original.CreateSnapshot(),editor=>editor.SetIdentity(identity).SetCreationTime(timestamp).SetModificationTime(timestamp).Synchronize(PdfMetadataSynchronization.InformationToXmp),policy:policy);
        PdfOptimization.WriteFile(edited.CreateSnapshot(),destination,new(compressStreams:args[2]=="compressed",cleanupObjects:true,deduplicateResources:true,removeUnusedResources:false),policy:policy);
        records.Add(new{file,inputSha256=address,outputSha256=Hash(File.ReadAllBytes(destination)),pages=edited.Pages.Count,identity=Convert.ToHexString(identity).ToLowerInvariant()});
    },out var completion))throw new InvalidOperationException("Single-job admission failed.");
    completion!.GetAwaiter().GetResult();
}
pool.CompleteAsync().GetAwaiter().GetResult();
File.WriteAllText(Path.Combine(output,"consumer.json"),JsonSerializer.Serialize(new{schemaVersion=1,purpose="source-determinism",profile="original-metadata-optimization-v1",compression=args[2],fixedTimestamp=timestamp.ToString("O",System.Globalization.CultureInfo.InvariantCulture),runtime=RuntimeInformation.FrameworkDescription,os=RuntimeInformation.OSDescription,architecture=RuntimeInformation.ProcessArchitecture.ToString(),sdkSha256=Hash(File.ReadAllBytes(typeof(Pdf).Assembly.Location)),records,productionQualified=false},new JsonSerializerOptions{WriteIndented=true}));
