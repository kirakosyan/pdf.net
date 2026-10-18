[CmdletBinding()]
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$evidence = Join-Path $root 'artifacts/contracts/api'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$comparisons = [System.Collections.Generic.List[object]]::new()
Push-Location $root
try {
    foreach ($project in @('Fluent.Pdf','Fluent.Pdf.Workers','Fluent.Pdf.Codecs','Fluent.Pdf.Fonts','Fluent.Pdf.Text','Fluent.Pdf.Rendering')) {
        $baseline = "src/$project/bin/$Configuration/netstandard2.0/$project.dll"
        foreach ($tfm in @('net472','net8.0','net10.0')) {
            $candidate = "src/$project/bin/$Configuration/$tfm/$project.dll"
            $leftHash = (Get-FileHash -LiteralPath $baseline -Algorithm SHA256).Hash.ToLowerInvariant()
            $rightHash = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash.ToLowerInvariant()
            $log = Join-Path $evidence "$project-$tfm.log"
            dotnet apicompat --left $baseline --right $candidate --strict-mode --enable-rule-cannot-change-parameter-name 2>&1 | Tee-Object -FilePath $log
            $comparisonExit = $LASTEXITCODE
            $comparisons.Add([ordered]@{ project=$project; targetFramework=$tfm; leftSha256=$leftHash; rightSha256=$rightHash; exitCode=$comparisonExit })
            if ($comparisonExit -ne 0) { throw "API comparison failed: $project $tfm (exit $comparisonExit). Retained diagnostics: $log" }
        }
    }
} finally {
    $comparisons | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $evidence 'comparisons.json') -Encoding utf8NoBOM
    Pop-Location
}
