[CmdletBinding()]
param([ValidateSet('Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$evidence = Join-Path $root 'artifacts/contracts/documentation'
$null = [IO.Directory]::CreateDirectory($evidence)
$project = Join-Path $root 'examples/Fluent.Pdf.Recipes/Fluent.Pdf.Recipes.csproj'
# An earlier fallback run replaces consumer-local SDK assets. Restore the normal
# mappings before asserting them, so repeated runs cannot reuse the wrong asset.
& dotnet build $project -c $Configuration --no-restore -m:1 -p:UseSharedCompilation=false -p:UseNetStandardAssembly=false
if ($LASTEXITCODE -ne 0) { throw 'Normal recipe consumer build failed.' }
& dotnet (Join-Path $root "eng/Documentation/bin/$Configuration/net10.0/Documentation.dll") $root $evidence --check
if ($LASTEXITCODE -ne 0) { throw 'Generated public API reference/version drift.' }
$python = if ($IsWindows) { 'python' } else { 'python3' }
& $python (Join-Path $root 'eng/check-documentation.py') --output (Join-Path $evidence 'snippets.json')
if ($LASTEXITCODE -ne 0) { throw 'Documentation/source excerpt drift.' }
& dotnet fsi --exec (Join-Path $root 'examples/TaskFacades.FSharp/Example.fsx') (Join-Path $evidence 'fsharp')
if ($LASTEXITCODE -ne 0) { throw 'FSharp task facade source consumer failed.' }
& $python (Join-Path $root 'eng/test-recipes.py') --assemblies (Join-Path $root "examples/Fluent.Pdf.Recipes/bin/$Configuration/net8.0/Fluent.Pdf.Recipes.dll") (Join-Path $root "examples/Fluent.Pdf.Recipes/bin/$Configuration/net10.0/Fluent.Pdf.Recipes.dll") --output (Join-Path $evidence 'modern-recipes.json')
if ($LASTEXITCODE -ne 0) { throw 'Actual modern source recipes failed.' }
if ($IsWindows -and [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq 'X64') {
    & $python (Join-Path $root 'eng/test-recipes.py') --assemblies (Join-Path $root "examples/Fluent.Pdf.Recipes/bin/$Configuration/net472/Fluent.Pdf.Recipes.exe") --output (Join-Path $evidence 'framework-recipes.json')
    if ($LASTEXITCODE -ne 0) { throw 'Actual current Framework source recipes failed.' }
}
& dotnet build $project -c $Configuration -f net10.0 --no-restore -m:1 -p:UseSharedCompilation=false -p:UseNetStandardAssembly=true
if ($LASTEXITCODE -ne 0) { throw 'Explicit netstandard recipe consumer build failed.' }
& $python (Join-Path $root 'eng/test-recipes.py') --assemblies (Join-Path $root "examples/Fluent.Pdf.Recipes/bin/$Configuration/net10.0/Fluent.Pdf.Recipes.dll") --netstandard --output (Join-Path $evidence 'netstandard-recipes.json')
if ($LASTEXITCODE -ne 0) { throw 'Actual explicit netstandard source recipes failed.' }
& (Join-Path $root 'eng/validate-spec.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Documentation/local link validation failed.' }
Write-Output 'Source documentation/recipes passed; published-package, complete narrative/provider and production qualification remain separate.'
