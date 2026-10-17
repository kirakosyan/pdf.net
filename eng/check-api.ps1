[CmdletBinding()]
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $baseline = "src/Fluent.Pdf/bin/$Configuration/netstandard2.0/Fluent.Pdf.dll"
    foreach ($tfm in @('net472','net8.0','net10.0')) {
        dotnet apicompat --left $baseline --right "src/Fluent.Pdf/bin/$Configuration/$tfm/Fluent.Pdf.dll" --strict-mode --enable-rule-cannot-change-parameter-name
        if ($LASTEXITCODE -ne 0) { throw "Public API mismatch: $tfm" }
    }
} finally { Pop-Location }
