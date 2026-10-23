#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$ImageReference = 'mcr.microsoft.com/windows/servercore:ltsc2019-amd64',
    [switch]$StartDockerDesktop
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$IsWindows) { throw 'Run this launcher on the Windows PC. Linux Docker cannot run this container.' }
if ($ImageReference -notmatch '^mcr\.microsoft\.com/windows/servercore(?::[a-zA-Z0-9_.-]+|@sha256:[a-f0-9]{64})$') { throw 'Use an explicit official Windows Server Core tag or digest.' }
$directory = [IO.Path]::GetFullPath($OutputDirectory)
$report = Join-Path $directory 'framework-container-probe.json'
if (Test-Path -LiteralPath $report) { throw 'Use a fresh owned report path.' }
$docker = (Get-Command docker -CommandType Application -ErrorAction Stop).Source
$selectors = @('DOCKER_HOST','DOCKER_CONTEXT','DOCKER_TLS','DOCKER_TLS_VERIFY','DOCKER_CERT_PATH')
$previous = @{}
foreach ($selector in $selectors) { $previous[$selector] = [Environment]::GetEnvironmentVariable($selector); [Environment]::SetEnvironmentVariable($selector,$null) }
$endpoint = 'npipe:////./pipe/docker_engine'
function Invoke-LocalDocker([string[]]$DockerArguments) {
    $result = @(& $docker --host $endpoint @DockerArguments)
    if ($LASTEXITCODE -ne 0) { throw 'The local Windows Docker operation failed; no runtime qualification is recorded.' }
    return $result
}
try {
    if ($StartDockerDesktop) {
        $desktop = Join-Path $env:ProgramFiles 'Docker/Docker/Docker Desktop.exe'
        if (!(Test-Path -LiteralPath $desktop -PathType Leaf)) { throw 'Docker Desktop is not installed at its standard path.' }
        if (!(Get-Process -Name 'Docker Desktop' -ErrorAction SilentlyContinue)) { $null = Start-Process -FilePath $desktop }
        $deadline = [DateTime]::UtcNow.AddSeconds(60)
        do {
            $null = & $docker --host $endpoint info --format '{{.OSType}}' 2>$null
            if ($LASTEXITCODE -eq 0) { break }
            Start-Sleep -Seconds 2
        } while ([DateTime]::UtcNow -lt $deadline)
        if ($LASTEXITCODE -ne 0) { throw 'Docker Desktop did not become ready within 60 seconds.' }
    }
    $kind = (Invoke-LocalDocker @('info','--format','{{.OSType}}')) -join ''
    if ($kind.Trim() -cne 'windows') { throw 'In Docker Desktop select Switch to Windows containers, then rerun. The launcher does not interrupt/switch an existing daemon.' }
    $null = Invoke-LocalDocker @('pull',$ImageReference)
    $image = ((Invoke-LocalDocker @('image','inspect',$ImageReference)) -join "`n" | ConvertFrom-Json)[0]
    if ($image.Os -cne 'windows' -or $image.Architecture -cne 'amd64') { throw 'The candidate must be a Windows amd64 image.' }
    $digests = @($image.RepoDigests | Where-Object { $_ -cmatch '^mcr\.microsoft\.com/windows/servercore@sha256:[a-f0-9]{64}$' })
    if ($digests.Count -ne 1) { throw 'The exact pulled image digest is required.' }
    $container = 'fluent-pdf-fp086-' + [guid]::NewGuid().ToString('N')
    # The outer process and each child use Windows PowerShell on the actual
    # container CLR. Neither a tag nor the host's Framework qualifies the image.
    $child = @'
$ErrorActionPreference = 'Stop'
$release = Get-ItemPropertyValue -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release
$core = [object].Assembly.Location
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($core).FileVersion
if ($release -notin @(461808,461814) -or !$version.StartsWith('4.7.')) { throw 'This image does not contain actual minimum Framework4.7.2.' }
[pscustomobject]@{ frameworkRelease=$release; coreFileVersion=$version; coreSha256=(Get-FileHash -LiteralPath $core -Algorithm SHA256).Hash.ToLowerInvariant(); runtimeVersion=[Environment]::Version.ToString(); processBits=[IntPtr]::Size*8; osVersion=[Environment]::OSVersion.VersionString; productionQualified=$false } | ConvertTo-Json -Compress
'@
    $childEncoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($child))
    $probe = @'
$ErrorActionPreference = 'Stop'
$encoded = '__CHILD_ENCODED__'
$observations = @()
foreach ($item in @(@{bits=64;path='C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'},@{bits=32;path='C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe'})) {
    $raw = & $item.path -NoProfile -NonInteractive -EncodedCommand $encoded
    if ($LASTEXITCODE -ne 0) { throw 'The actual Framework child process failed.' }
    $record = ($raw -join "`n") | ConvertFrom-Json
    if ($record.processBits -ne $item.bits) { throw 'The actual child process bitness is wrong.' }
    $observations += $record
}
$observations | ConvertTo-Json -Compress
'@
    $probe = $probe.Replace('__CHILD_ENCODED__',$childEncoded)
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($probe))
    $start = [DateTime]::UtcNow.ToString('o')
    # No repository/credential mounts, ports, Docker socket or network access.
    # Hyper-V isolation requires compatible Windows features on the physical PC.
    try {
        $raw = Invoke-LocalDocker @('run','--rm','--name',$container,'--isolation','hyperv','--network','none','--memory','2g','--cpu-count','2',$digests[0],'powershell.exe','-NoProfile','-NonInteractive','-EncodedCommand',$encoded)
        $observations = @(($raw -join "`n") | ConvertFrom-Json)
        if ($observations.Count -ne 2) { throw 'Both actual Framework bitness observations are required.' }
        $null = [IO.Directory]::CreateDirectory($directory)
        $result = [ordered]@{ schemaVersion=1; purpose='minimum-framework-container-preflight'; requestedImage=$ImageReference; imageDigest=$digests[0]; imageId=$image.Id; isolation='hyperv'; startedUtc=$start; observations=$observations; sdkTestsExecuted=$false; exactPackageConsumersExecuted=$false; trustedHostProvenanceQualified=$false; productionQualified=$false }
        $json = $result | ConvertTo-Json -Depth 8
        $stream = [IO.File]::Open($report,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try { $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json + "`n"); $stream.Write($bytes,0,$bytes.Length) } finally { $stream.Dispose() }
    } finally {
        # Exact unique task-owned name only. Retain pulled image; no global prune.
        $existing = @(& $docker --host $endpoint container ls --all --filter "name=^/$container$" --format '{{.Names}}' 2>$null)
        if ($LASTEXITCODE -eq 0 -and $existing -ccontains $container) {
            $null = & $docker --host $endpoint rm --force $container 2>$null
            if ($LASTEXITCODE -ne 0) { Write-Warning "Task container cleanup failed: $container" }
        }
    }
    Write-Output "Container preflight recorded: $report. SDK/package/full-lane qualification remains pending."
} finally {
    foreach ($selector in $selectors) { [Environment]::SetEnvironmentVariable($selector,$previous[$selector]) }
}
