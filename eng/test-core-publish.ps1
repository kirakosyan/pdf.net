#requires -Version 7.2
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$directory = Join-Path $root ('artifacts/contracts/core-push-offline/' + [Guid]::NewGuid().ToString('N'))
$packages = Join-Path $directory 'packages'
$null = [IO.Directory]::CreateDirectory($packages)
foreach ($name in @('Fluent.Pdf.1.0.0.nupkg','Fluent.Pdf.1.0.0.snupkg')) { [IO.File]::WriteAllText((Join-Path $packages $name),'invalid-package;offline-command-fixture') }
$global:FluentPdfCorePushCalls = [Collections.Generic.List[object]]::new()
$global:FluentPdfCorePushStubExit = 0
function dotnet { $global:FluentPdfCorePushCalls.Add(@($args)); $global:LASTEXITCODE = $global:FluentPdfCorePushStubExit }
if ((Get-Command dotnet).CommandType -ne 'Function') { throw 'Offline stub required.' }
$prior = $env:NUGET_API_KEY
try {
    $env:NUGET_API_KEY = ''
    $rejected = $false
    try { & (Join-Path $PSScriptRoot 'push-core.ps1') -Directory $directory } catch { $rejected = $_.Exception.Message -like '*Missing short-lived OIDC*' }
    if (!$rejected -or $global:FluentPdfCorePushCalls.Count) { throw 'Empty credential did not reject before invocation.' }
    $env:NUGET_API_KEY = 'offline-invalid-token'
    & (Join-Path $PSScriptRoot 'push-core.ps1') -Directory $directory
    $call = $global:FluentPdfCorePushCalls[0]
    if ($global:FluentPdfCorePushCalls.Count -ne 1 -or $call.Count -ne 8 -or $call[0] -cne 'nuget' -or $call[1] -cne 'push' -or $call[2] -cne (Join-Path $packages 'Fluent.Pdf.1.0.0.nupkg') -or $call[3] -cne '--api-key' -or $call[4] -cne $env:NUGET_API_KEY -or $call[5] -cne '--source' -or $call[6] -cne 'https://api.nuget.org/v3/index.json' -or $call[7] -cne '--skip-duplicate') { throw 'Incorrect intercepted push arguments.' }
    $global:FluentPdfCorePushStubExit = 17; $rejected = $false
    try { & (Join-Path $PSScriptRoot 'push-core.ps1') -Directory $directory } catch { $rejected = $_.Exception.Message -like '*publication failed*' }
    if (!$rejected) { throw 'Push failure did not propagate.' }
    [IO.File]::WriteAllText((Join-Path $packages 'Unexpected.nupkg'),'offline-only')
    $rejected = $false
    try { & (Join-Path $PSScriptRoot 'push-core.ps1') -Directory $directory } catch { $rejected = $_.Exception.Message -like '*Only the scoped core*' }
    if (!$rejected -or $global:FluentPdfCorePushCalls.Count -ne 2) { throw 'Extra package did not reject before invocation.' }
} finally {
    $env:NUGET_API_KEY = $prior
    Remove-Variable -Scope Global -Name FluentPdfCorePushCalls,FluentPdfCorePushStubExit
}
Write-Output 'CORE_PUSH_OFFLINE_PASS;4 checks;no CLI/OIDC/network/upload'
exit 0
