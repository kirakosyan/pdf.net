[CmdletBinding()]
param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root ('artifacts/rebuild/' + [Guid]::NewGuid().ToString('N')) }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new evidence directory; previous results are never overwritten.' }
$null = [IO.Directory]::CreateDirectory($output)
$projects = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'PublicAPI.Shipped.txt') } | Sort-Object Name)
$commit = (& git -C $root rev-parse HEAD).Trim()
$dirty = [bool](& git -C $root status --porcelain)
$records = [Collections.Generic.List[object]]::new()
foreach ($pass in @('first','second')) {
    $passRoot = Join-Path $output $pass
    # Separate fresh object/output trees, with one canonical generated-source path.
    $map = "$passRoot=/_/artifacts%2C$root=/_"
    & dotnet build (Join-Path $root 'Fluent.Pdf.slnx') -c Release --artifacts-path $passRoot --no-incremental --property:RestoreLockedMode=true --property:ContinuousIntegrationBuild=true "-p:PathMap=$map" -m:1 -p:UseSharedCompilation=false *> (Join-Path $output "$pass-build.log")
    if ($LASTEXITCODE -ne 0) { throw "Fresh source build failed: $pass; inspect retained log." }
}
foreach ($project in $projects) {
    foreach ($tfm in @('net472','netstandard2.0','net8.0','net10.0')) {
        $relative = "bin/$($project.Name)/release_$tfm/$($project.Name)"
        foreach ($extension in @('.dll','.pdb','.xml')) {
            $first = Join-Path $output "first/$relative$extension"
            $second = Join-Path $output "second/$relative$extension"
            $sha = (Get-FileHash -LiteralPath $first -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($sha -cne (Get-FileHash -LiteralPath $second -Algorithm SHA256).Hash.ToLowerInvariant()) { throw "Source rebuild bytes differ: $relative$extension" }
            $records.Add([ordered]@{path="$relative$extension";sha256=$sha;bytes=(Get-Item -LiteralPath $first).Length})
        }
        & dotnet (Join-Path $output 'first/bin/PackageProbe/release/PackageProbe.dll') (Join-Path $output "first/$relative.dll") (Join-Path $output "first/$relative.pdb") $commit (Join-Path $output "probes/$($project.Name)-$tfm.json")
        if ($LASTEXITCODE -ne 0) { throw "Assembly/symbol provenance failed: $relative" }
    }
}
$source = @(& git -C $root ls-files --cached --others --exclude-standard src eng Directory.Build.props global.json Fluent.Pdf.slnx | Sort-Object -Unique | ForEach-Object { [ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $root $_) -Algorithm SHA256).Hash.ToLowerInvariant()} })
[ordered]@{schemaVersion=1;sourceCommit=$commit;workingTreeDirty=$dirty;sourceFiles=$source;equalArtifacts=@($records.ToArray());sourceLinkProbes=($projects.Count*4);productionQualified=$false;scope='Two fresh output/object trees in one source checkout; no SDK package created. Clean independent-checkout and final package rebuild qualification remains required.'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'report.json') -Encoding utf8NoBOM
Write-Output "SOURCE_REBUILD_PASS;$($records.Count)-identical-artifacts;$($projects.Count*4)-SourceLink-probes;$output"
