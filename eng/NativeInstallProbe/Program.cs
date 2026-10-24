using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fluent.Pdf;
using Fluent.Pdf.Rendering;
using Fluent.Pdf.Workers;

// Original, explicitly invoked source observer. No engine downloads or installed-system ABI certification.
if(args.Length!=4) throw new ArgumentException("Supply absolute manifest, exact SHA256, installed directory and fresh report directory.");
if(args.Any(a=>a.Length>4096)||!Path.IsPathFullyQualified(args[0])||!Path.IsPathFullyQualified(args[2])||!Path.IsPathFullyQualified(args[3])) throw new ArgumentException("Explicit bounded absolute source paths required.");
if(new FileInfo(args[0]).Length>4194304)throw new InvalidDataException("Manifest extent differs.");
byte[] bytes=File.ReadAllBytes(args[0]);if(bytes.Length>4194304||Digest(bytes)!=args[1])throw new InvalidDataException("Pinned manifest differs.");
string installedManifest=Path.Combine(args[2],"provision-manifest.json");if(new FileInfo(installedManifest).Length!=bytes.Length||!File.ReadAllBytes(installedManifest).SequenceEqual(bytes))throw new InvalidDataException("Installation identity differs.");
using var manifest=JsonDocument.Parse(bytes);
var m=manifest.RootElement;string rid=m.GetProperty("runtimeIdentifier").GetString()!;
if(rid!=PdfNativeDeploymentInspector.OperatingSystemRuntimeIdentifier||m.GetProperty("engineId").GetString()!="page.rendering"||m.GetProperty("engineVersion").GetString()!=IsolatedPdfRenderer.BridgeVersion||m.GetProperty("abi").GetString()!=IsolatedPdfRenderer.BridgeAbi)throw new InvalidDataException("Exact source engine/RID/ABI prerequisite differs.");
var assets=m.GetProperty("assets").EnumerateArray().Where(a=>a.GetProperty("role").GetString() is "executable" or "library").Select(a=>new PdfNativeAsset(a.GetProperty("path").GetString()!,a.GetProperty("length").GetInt64(),a.GetProperty("sha256").GetString()!)).ToArray();
if(assets.Length!=3)throw new InvalidDataException("This source rendering probe requires its closed three-binary profile.");
string output=Path.GetFullPath(args[3]);if(Directory.Exists(output)||File.Exists(output))throw new IOException("Fresh source evidence directory required.");Directory.CreateDirectory(output);
string jobs=Path.Combine(output,"jobs");Directory.CreateDirectory(jobs);
string executable=rid.StartsWith("win-",StringComparison.Ordinal)?"worker.exe":"worker",extension=rid.StartsWith("win-",StringComparison.Ordinal)?"dll":rid.StartsWith("osx-",StringComparison.Ordinal)?"dylib":"so",bridge="fluent-pdf-render."+extension;
PdfNativeDeployment Deployment(string root,IEnumerable<PdfNativeAsset> pins)=>new(root,rid,"page.rendering",IsolatedPdfRenderer.BridgeVersion,IsolatedPdfRenderer.BridgeAbi,67108864,pins.ToArray());
var deployment=Deployment(args[2],assets);
var inspection=PdfNativeDeploymentInspector.InspectWorker(deployment);if(!inspection.AssetsVerified)throw new InvalidDataException("Installed engine preflight failed.");
var worker=new NativeWorkerOptions(deployment,executable,bridge,jobs,268435456,67108864,16777216,TimeSpan.FromSeconds(10));
var providers=new PdfProviderRegistry(new IsolatedPdfRenderer(worker));
using var document=Pdf.Create().AddPage(new PdfPageGeometry(new PdfPageBoxes(new PdfRect(0,0,80,60))));
using(var program=new MemoryStream(Encoding.ASCII.GetBytes("1 0 0 rg 30 35 10 10 re f")))document.Pages[0].Content=PdfContentProgram.Parse(program);
using(var pdf=File.Create(Path.Combine(output,"original.pdf")))document.Save(pdf);
var policy=new PdfOperationPolicy(limits:new(maximumExpandedBytes:1073741824));
var full=PdfRendering.RenderPage(document.CreateSnapshot(),0,providers,new(72,72),policy);byte[] samples=Read(full);
if(full.Request.Width!=80||full.Request.Height!=60||!Pixel(samples,full.Request,35,20).SequenceEqual(new byte[]{0,0,255,255})||!Pixel(samples,full.Request,1,1).SequenceEqual(new byte[]{255,255,255,255}))throw new InvalidDataException("Independent installed engine pixel fixture differs.");
File.WriteAllBytes(Path.Combine(output,"page.bgra"),samples);
var region=PdfRendering.RenderPage(document.CreateSnapshot(),0,providers,new(72,72,new PdfRect(30,15,10,10)),policy);byte[] window=Read(region);
for(int y=0;y<region.Request.Height;y++)if(!samples.Skip((region.Request.Y+y)*full.Request.Stride+region.Request.X*4).Take(region.Request.Stride).SequenceEqual(window.Skip(y*region.Request.Stride).Take(region.Request.Stride)))throw new InvalidDataException("Installed engine region does not match full raster.");
string missing=Path.Combine(output,"missing-engine");Directory.CreateDirectory(missing);
foreach(string name in new[]{executable,bridge})File.Copy(Path.Combine(args[2],name),Path.Combine(missing,name));
var absent=PdfNativeDeploymentInspector.InspectWorker(Deployment(missing,assets));if(absent.AssetsVerified||absent.Diagnostics.Count(d=>d.Code=="Native.AssetMissing")!=1)throw new InvalidDataException("Missing offline engine was accepted or misidentified.");
var wrongPins=assets.Select((a,i)=>i==0?new PdfNativeAsset(a.FileName,a.Length,new string('0',64)):a).ToArray();
var wrong=PdfNativeDeploymentInspector.InspectWorker(Deployment(args[2],wrongPins));if(wrong.AssetsVerified||!wrong.Diagnostics.Any(d=>d.Code=="Native.HashMismatch"))throw new InvalidDataException("Changed offline asset pin was accepted.");
if(Directory.EnumerateFileSystemEntries(jobs).Any())throw new IOException("Owned installed-engine jobs remain.");
var result=new{schemaVersion=1,purpose="offline-installed-real-renderer-source-observation",runtime=RuntimeInformation.FrameworkDescription,os=RuntimeInformation.OSDescription,architecture=RuntimeInformation.ProcessArchitecture.ToString(),rid,engine=IsolatedPdfRenderer.BridgeVersion,abi=IsolatedPdfRenderer.BridgeAbi,manifestSha256=args[1],coreSha256=FileHash(typeof(PdfDocument).Assembly.Location),adapterSha256=FileHash(typeof(IsolatedPdfRenderer).Assembly.Location),workerSha256=assets.Single(a=>a.FileName==executable).Sha256,checks=new[]{"complete-pinned-installation-preflight","actual-full-raster","actual-region-window","missing-assets-rejected","wrong-hash-rejected","no-owned-worker-directory"},width=full.Request.Width,height=full.Request.Height,stride=full.Request.Stride,samplesSha256=Digest(samples),pdfSha256=FileHash(Path.Combine(output,"original.pdf")),networkDeniedByWorker=true,cleanAirGappedHostQualified=false,systemAbiQualified=false,redistributionApproved=false,productionQualified=false};
File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("OFFLINE_INSTALLED_RENDERER_SOURCE_PASS;"+rid+";"+result.runtime+";not-production-qualification");
static byte[] Read(PdfRaster raster){using var input=raster.OpenSamples();using var output=new MemoryStream();input.CopyTo(output);return output.ToArray();}
static IEnumerable<byte> Pixel(byte[] samples,PdfRasterRequest r,int x,int y)=>samples.Skip(y*r.Stride+x*4).Take(4);
static string Digest(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
static string FileHash(string path){using var file=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();}
