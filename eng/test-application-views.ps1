[CmdletBinding()]
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$IsWindows) { throw 'Actual Windows is required for WinForms/WPF handles, keyboard and accessibility peers.' }
$root = Split-Path $PSScriptRoot -Parent
$directory = Join-Path $root 'artifacts/contracts/application-views'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$manifest = Join-Path $directory 'windows-applications.json'
[IO.File]::Delete($manifest)
$rows = [Collections.Generic.List[object]]::new()
$failures = [Collections.Generic.List[object]]::new()
foreach ($kind in @('WinForms','Wpf')) {
    $name = "Fluent.Pdf.$($kind)Sample"
    $project = Join-Path $root "examples/$name/$name.csproj"
    & dotnet restore $project --locked-mode --disable-parallel -m:1
    if ($LASTEXITCODE -ne 0) { throw "Locked application restore failed: $kind" }
    & dotnet build $project -c $Configuration --no-restore -m:1 -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw "Application build failed: $kind" }
    foreach ($tfm in @('net472','net8.0-windows','net10.0-windows')) {
        $path = Join-Path $root "examples/$name/bin/$Configuration/$tfm/$name"
        $asset = $path + $(if ($tfm -eq 'net472') { '.exe' } else { '.dll' })
        $log = Join-Path $directory "$kind-$tfm.log"
        if ($tfm -eq 'net472') { & ($path + '.exe') --self-test 2>&1 | Tee-Object -FilePath $log }
        else { & dotnet ($path + '.dll') --self-test 2>&1 | Tee-Object -FilePath $log }
        $consumerExit = $LASTEXITCODE
        $content = Get-Content -LiteralPath $log -Raw
        $marker = if ($kind -eq 'WinForms') { 'WINFORMS_VIEW_PASS;' } else { 'WPF_VIEW_PASS;' }
        if ($consumerExit -ne 0 -or !$content.Contains($marker) -or !$content.Contains(';fixture-renderer;keyboard-and-')) {
            $failures.Add([ordered]@{kind=$kind;asset=$tfm;exitCode=$consumerExit;diagnostics=$content.Trim();logSha256=(Get-FileHash $log -Algorithm SHA256).Hash.ToLowerInvariant()})
            continue
        }
        $rows.Add([ordered]@{kind=$kind;asset=$tfm;identity=$content.Trim();passed=$true;logSha256=(Get-FileHash $log -Algorithm SHA256).Hash.ToLowerInvariant();assemblySha256=(Get-FileHash $asset -Algorithm SHA256).Hash.ToLowerInvariant()})
    }
}
if ($failures.Count -ne 0) {
    [ordered]@{sourceCommit=(& git -C $root rev-parse HEAD).Trim();failures=$failures.ToArray();productionQualified=$false} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $directory 'windows-application-failures.json') -Encoding utf8NoBOM
    throw "Actual Windows application boundaries failed: $($failures.Count). Failure reports are retained; no passing aggregate was written."
}
[ordered]@{schemaVersion=1;sourceCommit=(& git -C $root rev-parse HEAD).Trim();workingTreeDirty=[bool](& git -C $root status --porcelain);os=[Runtime.InteropServices.RuntimeInformation]::OSDescription;applications=$rows.ToArray();actualRasterFidelityQualified=$false;manualScreenReaderQualified=$false;actualMinimumFrameworkQualified=$false;productionQualified=$false} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifest -Encoding utf8NoBOM
Write-Output 'Six actual Windows application boundaries passed; fixture rendering, not native fidelity or full screen-reader qualification.'
