[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$PackageManifest,
    [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
# Candidate consumers are permitted only after the existing source qualification
# gate. This script never creates a package, changes readiness or publishes.
& (Join-Path $root 'eng/assert-production-ready.ps1') -Stage BeforePack -EvidenceDirectory $EvidenceDirectory
if ($LASTEXITCODE -ne 0) { throw 'Source qualification did not permit candidate-package consumers.' }
$readiness = Get-Content -LiteralPath (Join-Path $root 'specs/release-readiness.json') -Raw | ConvertFrom-Json
$manifest = Get-Content -LiteralPath $PackageManifest -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.packageVersion -cne $readiness.packageVersion) { throw 'Candidate package manifest/version mismatch.' }
$packages = [IO.Path]::GetFullPath($PackageDirectory)
$packageRows = @($manifest.packages)
$ids = @($packageRows | ForEach-Object { "$($_.id)/$($_.version)" })
if (@($ids | Sort-Object -Unique).Count -ne $ids.Count) { throw 'Duplicate candidate package identities.' }
$projects = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'PublicAPI.Shipped.txt') } | Sort-Object Name)
foreach ($project in $projects) {
    $entry = @($packageRows | Where-Object { $_.id -ceq $project.Name -and $_.version -ceq $readiness.packageVersion })
    if ($entry.Count -ne 1 -or [IO.Path]::GetFileName($entry[0].file) -cne $entry[0].file -or $entry[0].sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Missing or invalid exact SDK candidate package.' }
}
foreach ($entry in $packageRows) {
    if ([IO.Path]::GetFileName($entry.file) -cne $entry.file -or $entry.sha256 -notmatch '^[a-fA-F0-9]{64}$' -or $entry.file -notlike '*.nupkg') { throw 'Invalid feed artifact binding.' }
    $path = Join-Path $packages $entry.file
    if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $entry.sha256) { throw 'Candidate feed artifact hash mismatch.' }
}
$feedFiles = @(Get-ChildItem -LiteralPath $packages -Filter '*.nupkg' -File | ForEach-Object Name)
if ($feedFiles.Count -ne $packageRows.Count -or @($feedFiles | Where-Object { $_ -cnotin @($packageRows | ForEach-Object file) }).Count) { throw 'The explicit feed contains unbound or duplicate artifacts.' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts/contracts/documentation/package-consumers' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
$workspace = Join-Path $output ([Guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($workspace)
Copy-Item -LiteralPath (Join-Path $root 'Directory.Build.props') -Destination (Join-Path $workspace 'Directory.Build.props')
$null = [IO.Directory]::CreateDirectory((Join-Path $workspace 'eng'))
foreach ($name in @('Fluent.Pdf.Contract.CSharp','Fluent.Pdf.Contract.VisualBasic','Fluent.Pdf.Recipes')) {
    $target = Join-Path $workspace "examples/$name"
    $null = [IO.Directory]::CreateDirectory($target)
    Get-ChildItem -LiteralPath (Join-Path $root "examples/$name") -File | Where-Object { $_.Extension -in @('.cs','.vb','.csproj','.vbproj') } | Copy-Item -Destination $target
}
$fixture = Join-Path $workspace 'tests/Fluent.Pdf.Contract.Tests/Fixtures/Automation'
$null = [IO.Directory]::CreateDirectory($fixture)
Copy-Item -LiteralPath (Join-Path $root 'tests/Fluent.Pdf.Contract.Tests/Fixtures/Automation/original.pdf') -Destination (Join-Path $fixture 'original.pdf')
$group = [Xml.Linq.XElement]::new('ItemGroup')
foreach ($project in $projects) {
    $reference = [Xml.Linq.XElement]::new('PackageReference')
    $reference.SetAttributeValue('Include',$project.Name)
    $reference.SetAttributeValue('Version',"[$($readiness.packageVersion)]")
    $group.Add($reference)
}
$props = [Xml.Linq.XElement]::new('Project'); $props.Add($group)
$props.Save((Join-Path $workspace 'eng/ContractProjectReference.props'))
$config = [Xml.Linq.XElement]::new('configuration')
$sources = [Xml.Linq.XElement]::new('packageSources'); $sources.Add([Xml.Linq.XElement]::new('clear'))
$source = [Xml.Linq.XElement]::new('add'); $source.SetAttributeValue('key','ExplicitCandidateFeed'); $source.SetAttributeValue('value',$packages); $sources.Add($source); $config.Add($sources)
$configuration = Join-Path $workspace 'NuGet.Config'; $config.Save($configuration)
$consumer = Join-Path $workspace 'examples/Fluent.Pdf.Recipes/Fluent.Pdf.Recipes.csproj'
$cache = Join-Path $workspace 'fresh-packages'
& dotnet restore $consumer --configfile $configuration --packages $cache --no-cache --disable-parallel -m:1
if ($LASTEXITCODE -ne 0) { throw 'Fresh explicit-feed package consumer restore failed.' }
& dotnet build $consumer -c Release --no-restore -m:1 -p:UseSharedCompilation=false -p:DefineConstants=RECIPE_PACKAGE_MODE
if ($LASTEXITCODE -ne 0) { throw 'Exact package consumer compilation failed.' }
$python = if ($IsWindows) { 'python' } else { 'python3' }
$assemblies = @((Join-Path $workspace 'examples/Fluent.Pdf.Recipes/bin/Release/net8.0/Fluent.Pdf.Recipes.dll'),(Join-Path $workspace 'examples/Fluent.Pdf.Recipes/bin/Release/net10.0/Fluent.Pdf.Recipes.dll'))
& $python (Join-Path $root 'eng/test-recipes.py') --assemblies @assemblies --package-mode --output (Join-Path $workspace 'package-recipes.json')
if ($LASTEXITCODE -ne 0) { throw 'Actual package recipe consumers failed.' }
if ($IsWindows) {
    & $python (Join-Path $root 'eng/test-recipes.py') --assemblies (Join-Path $workspace 'examples/Fluent.Pdf.Recipes/bin/Release/net472/Fluent.Pdf.Recipes.exe') --package-mode --output (Join-Path $workspace 'framework-package-recipes.json')
    if ($LASTEXITCODE -ne 0) { throw 'Actual Framework package recipe consumer failed.' }
}
Copy-Item -LiteralPath $PackageManifest -Destination (Join-Path $workspace 'input-package-manifest.json')
Write-Output "Exact package consumers retained at $workspace; complete runtime/minimum-Framework/native/provider/corpus qualification remains separate."
