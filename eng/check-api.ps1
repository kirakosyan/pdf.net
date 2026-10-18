[CmdletBinding()]
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    foreach ($project in @('Fluent.Pdf','Fluent.Pdf.Workers','Fluent.Pdf.Codecs','Fluent.Pdf.Fonts')) {
        $baseline = "src/$project/bin/$Configuration/netstandard2.0/$project.dll"
        foreach ($tfm in @('net472','net8.0','net10.0')) {
            dotnet apicompat --left $baseline --right "src/$project/bin/$Configuration/$tfm/$project.dll" --strict-mode --enable-rule-cannot-change-parameter-name
            if ($LASTEXITCODE -ne 0) { throw "Public API mismatch: $project $tfm" }
        }
    }
} finally { Pop-Location }
