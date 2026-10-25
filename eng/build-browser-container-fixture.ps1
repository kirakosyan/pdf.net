[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DockerExecutable,
    [Parameter(Mandatory)][string]$DaemonEndpoint,
    [Parameter(Mandatory)][switch]$ProvisionSourceImage
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if (!$ProvisionSourceImage -or ![IO.Path]::IsPathFullyQualified($DockerExecutable)) { throw 'Explicit source-image provisioning and an absolute Docker CLI are required.' }
$root=Split-Path $PSScriptRoot -Parent
$directory=Join-Path $root ('artifacts/browser-container-source/'+[Guid]::NewGuid().ToString('N'))
$context=Join-Path $directory 'context'
$null=[IO.Directory]::CreateDirectory($context)
foreach($file in @('Dockerfile','host.py','fontconfig.xml')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "browser-host/$file") -Destination $context
}
Copy-Item -LiteralPath (Join-Path $root 'tests/Fluent.Pdf.Contract.Tests/Fixtures/text/business.ttf') -Destination $context
$imageFile=Join-Path $directory 'image-id.txt'
& $DockerExecutable --host $DaemonEndpoint build --iidfile $imageFile --tag ('fluent-pdf-browser-source:'+([Guid]::NewGuid().ToString('N'))) $context 2>&1 | Tee-Object -FilePath (Join-Path $directory 'build.log')
if ($LASTEXITCODE -ne 0) { throw "Explicit source browser image build failed; evidence retained at $directory" }
$image=(Get-Content -LiteralPath $imageFile -Raw).Trim()
if ($image -notmatch '^sha256:[0-9a-f]{64}$') { throw 'The built source image identity is invalid.' }
& $DockerExecutable --host $DaemonEndpoint image inspect $image | Set-Content -LiteralPath (Join-Path $directory 'image-inspect.json') -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Source image inspection failed.' }
foreach($file in @('packages.tsv','browser-version.txt','files.sha256')) {
    & $DockerExecutable --host $DaemonEndpoint run --rm --pull never --network none --read-only --cap-drop ALL --security-opt no-new-privileges --entrypoint /bin/cat $image "/opt/fluent-pdf/browser/$file" | Set-Content -LiteralPath (Join-Path $directory $file) -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'Source image inventory observation failed.' }
}
$record=[ordered]@{schemaVersion=1;purpose='explicit-private-browser-source-image-not-distribution-or-production-qualification';imageId=$image;sourceCommit=(& git -C $root rev-parse HEAD).Trim();workingTreeDirty=[bool](& git -C $root status --porcelain);dockerExecutable=$DockerExecutable;dockerSha256=(Get-FileHash -LiteralPath $DockerExecutable -Algorithm SHA256).Hash.ToLowerInvariant();daemonEndpoint=$DaemonEndpoint;contextFiles=@(Get-ChildItem -LiteralPath $context -File | ForEach-Object { @{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()} });productionQualified=$false;redistributionApproved=$false}
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $directory 'source-image.json') -Encoding utf8
Write-Output "BROWSER_SOURCE_IMAGE_PROVISIONED;$directory"
