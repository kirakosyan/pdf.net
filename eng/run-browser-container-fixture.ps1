[CmdletBinding()]
param([Parameter(Mandatory)][string]$DockerExecutable,[Parameter(Mandatory)][string]$DaemonEndpoint,[Parameter(Mandatory)][switch]$ProvisionSourceImage)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if(!$ProvisionSourceImage){throw 'Explicit source provisioning is required.'}
$root=Split-Path $PSScriptRoot -Parent
$directory=Join-Path $root ('artifacts/contracts/browser-container/'+[Guid]::NewGuid().ToString('N'))
$null=[IO.Directory]::CreateDirectory($directory)
$python=if($IsWindows){'python'}else{'python3'}
$headers=@{Accept='application/vnd.github+json'}
if($env:GH_TOKEN){$headers.Authorization='Bearer '+$env:GH_TOKEN}
foreach($entry in @(@('original.json','6e5e47e242892aaa290f916ba1045f9213ef0b84'),@('LICENSE','d645695673349e3947e8e5ae42332d0ac3164cd7'))) {
    $response=Invoke-RestMethod -Uri ('https://api.github.com/repos/moby/profiles/git/blobs/'+$entry[1]) -Headers $headers
    if($response.encoding -ne 'base64' -or $response.size -gt 1048576 -or $response.sha -ne $entry[1]){throw 'Unrecognized explicit source analysis input.'}
    [IO.File]::WriteAllBytes((Join-Path $directory $entry[0]),[Convert]::FromBase64String($response.content))
}
& $python (Join-Path $PSScriptRoot 'prepare-browser-fixture-policy.py') --original (Join-Path $directory 'original.json') --license (Join-Path $directory 'LICENSE') --output (Join-Path $directory 'policy')
if($LASTEXITCODE -ne 0){throw 'Source policy modification provenance failed.'}
$build=@(& (Join-Path $PSScriptRoot 'build-browser-container-fixture.ps1') -DockerExecutable $DockerExecutable -DaemonEndpoint $DaemonEndpoint -ProvisionSourceImage)
$sentinel=@($build | Where-Object {$_ -is [string] -and $_.StartsWith('BROWSER_SOURCE_IMAGE_PROVISIONED;')})
if($sentinel.Count -ne 1){throw 'Source image provisioning did not retain its identity.'}
$imageDirectory=$sentinel[0].Substring('BROWSER_SOURCE_IMAGE_PROVISIONED;'.Length)
$image=Get-Content -LiteralPath (Join-Path $imageDirectory 'source-image.json') -Raw | ConvertFrom-Json
Copy-Item -LiteralPath (Join-Path $imageDirectory 'source-image.json') -Destination $directory
$config=Join-Path $directory 'empty-config'
$null=[IO.Directory]::CreateDirectory($config)
$policy=Join-Path $directory 'policy/fixture.json'
foreach($tfm in @('net8.0','net10.0')) {
    $output=Join-Path $directory $tfm
    $manifest=Join-Path $directory "$tfm-manifest.json"
    @{dockerExecutable=$DockerExecutable;dockerSha256=$image.dockerSha256;daemonEndpoint=$DaemonEndpoint;imageId=$image.imageId;emptyConfigurationDirectory=$config;seccompProfile=$policy;seccompSha256=(Get-FileHash -LiteralPath $policy -Algorithm SHA256).Hash.ToLowerInvariant();outputDirectory=$output} | ConvertTo-Json | Set-Content -LiteralPath $manifest -Encoding utf8NoBOM
    & dotnet (Join-Path $root "eng/HtmlContainerProbe/bin/Release/$tfm/HtmlContainerProbe.dll") $manifest 2>&1 | Tee-Object -FilePath (Join-Path $directory "$tfm.log")
    if($LASTEXITCODE -ne 0){throw "Actual private browser source probe failed: $tfm"}
    & $python (Join-Path $PSScriptRoot 'verify-browser-captures.py') $output (Join-Path $output 'independent.json')
    if($LASTEXITCODE -ne 0){throw 'Independent original source pixel/text observations failed.'}
}
$owned=@(& $DockerExecutable --host $DaemonEndpoint ps --all --filter 'label=org.fluent-pdf.owner' --format '{{.Names}}')
if($LASTEXITCODE -ne 0){throw 'Owned-container final observation failed.'}
[IO.File]::WriteAllText((Join-Path $directory 'owned-containers-after.txt'),($owned -join "`n"),[Text.UTF8Encoding]::new($false))
if(@($owned | Where-Object {$_}).Count -ne 0){throw 'An owned source browser container remains for explicit reconciliation.'}
Write-Output "BROWSER_CONTAINER_SOURCE_OBSERVED;$directory"
