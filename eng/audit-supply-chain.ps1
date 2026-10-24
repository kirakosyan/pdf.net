[CmdletBinding()]
param([string]$OutputDirectory = '', [string[]]$AssetDirectories = @())
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$python = if ($IsWindows) { 'python' } else { 'python3' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root ('artifacts/supply-chain/' + [Guid]::NewGuid().ToString('N')) }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new audit directory.' }
$null = [IO.Directory]::CreateDirectory($output)
$cacheOutput = & dotnet nuget locals global-packages --list --force-english-output
if ($LASTEXITCODE -ne 0 -or $cacheOutput -notmatch '^global-packages: (.+)$') { throw 'Unable to resolve the configured package cache.' }
$cache = $Matches[1].Trim()
$arguments = @((Join-Path $root 'eng/supply-chain.py'), '--root', $root, '--cache', $cache, '--output', (Join-Path $output 'inventory'), '--probe', (Join-Path $root 'eng/SupplyChainProbe/bin/Release/net10.0/SupplyChainProbe.dll'))
foreach ($directory in $AssetDirectories) { $arguments += @('--asset-directory', [IO.Path]::GetFullPath($directory)) }
& $python @arguments
if ($LASTEXITCODE -ne 0) { throw 'Source inventory failed.' }
$previousCache = $env:NUGET_HTTP_CACHE_PATH
try {
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $output 'fresh-advisory-cache'
    & dotnet list (Join-Path $root 'Fluent.Pdf.slnx') package --vulnerable --include-transitive --format json --no-restore > (Join-Path $output 'advisories.json')
    if ($LASTEXITCODE -ne 0) { throw 'Advisory query failed.' }
} finally { $env:NUGET_HTTP_CACHE_PATH = $previousCache }
[xml]$solution = Get-Content -LiteralPath (Join-Path $root 'Fluent.Pdf.slnx') -Raw
$check = @((Join-Path $root 'eng/check-vulnerabilities.py'), '--report', (Join-Path $output 'advisories.json'), '--output', (Join-Path $output 'advisory-check.json'))
foreach ($project in $solution.SelectNodes('//Project')) { $check += @('--expected-project', (Join-Path $root $project.Path)) }
& $python @check
if ($LASTEXITCODE -ne 0) { throw 'Incomplete advisory coverage or reported vulnerability.' }
Write-Output 'SOURCE_SUPPLY_CHAIN_PASS;final artifacts, native/model trust and redistribution approval remain unqualified'
