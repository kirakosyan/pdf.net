[CmdletBinding()]
param([Parameter(Mandatory)][string]$Directory,[Parameter(Mandatory)][string]$Rid,[string]$Developer,[string]$HostArchitecture)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if($Rid -eq 'win-x86'){return} # A 32-bit Framework caller uses the explicitly selected 64-bit OS worker.
$root=Split-Path $PSScriptRoot -Parent
$inventory=Get-Content -LiteralPath (Join-Path $root 'third-party/rendering/components.json') -Raw | ConvertFrom-Json
$asset=@($inventory.deployments | Where-Object {$_.rid -ceq $Rid})
if($asset.Count -ne 1){throw "No pinned candidate renderer asset for $Rid"}
$asset=$asset[0]
$cache=Join-Path $root 'artifacts/rendering-assets'
$null=[IO.Directory]::CreateDirectory($cache)
$wheel=Join-Path $cache $asset.wheel
if(!(Test-Path -LiteralPath $wheel)){
    $metadata=Invoke-RestMethod -Uri 'https://pypi.org/pypi/pypdfium2/5.13.0/json'
    $entry=@($metadata.urls | Where-Object {$_.filename -ceq $asset.wheel -and $_.digests.sha256 -ceq $asset.wheelSha256})
    if($entry.Count -ne 1){throw 'The pinned rendering container is absent or its registry digest changed.'}
    $uri=[Uri]$entry[0].url
    if($uri.Scheme -cne 'https' -or $uri.Host -cne 'files.pythonhosted.org'){throw 'Unexpected rendering asset host.'}
    Invoke-WebRequest -Uri $uri -OutFile $wheel
}
if((Get-FileHash -LiteralPath $wheel).Hash.ToLowerInvariant() -cne $asset.wheelSha256){throw 'Rendering container checksum mismatch.'}
$notices=Join-Path $Directory 'rendering-notices'
$null=[IO.Directory]::CreateDirectory($notices)
$engine=$null
$zip=[IO.Compression.ZipFile]::OpenRead($wheel)
try{
    foreach($file in $asset.files){
        $entry=$zip.GetEntry($file.archivePath)
        if($null -eq $entry -or $entry.Length -ne $file.size -or [IO.Path]::GetFileName($file.name) -cne $file.name){throw 'Invalid exact rendering archive entry.'}
        $native=$file.name -match '\.(dll|so|dylib)$'
        $target=Join-Path $(if($native){$Directory}else{$notices}) $file.name
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target,$true)
        if((Get-FileHash -LiteralPath $target).Hash.ToLowerInvariant() -cne $file.sha256){throw "Rendering entry checksum mismatch: $($file.name)"}
        if($native){$engine=$target;$sourceHash=$file.sha256}
    }
}finally{$zip.Dispose()}
if(!$engine){throw 'No exact renderer engine asset.'}
$version=Get-Content -LiteralPath (Join-Path $notices 'version.json') -Raw | ConvertFrom-Json
if("$($version.major).$($version.minor).$($version.build).$($version.patch)" -cne $inventory.version -or @($version.flags).Count -ne 0){throw 'Rendering asset version or build flags changed.'}
$extension=if($IsWindows){'dll'}elseif($IsMacOS){'dylib'}else{'so'}
$arch=$Rid.Split('-')[1]
Push-Location $root
try{
    if($IsWindows){
        $names=[regex]::Matches([IO.File]::ReadAllText((Join-Path $root 'native/rendering/bridge.c')),'extern[^;]+?\b((?:FPDF|FORM|FPDFBitmap|FPDFDOC|FPDFPage|FPDFAnnot)_\w+)\(') | ForEach-Object {$_.Groups[1].Value}
        $definition=Join-Path $Directory 'pdfium-import.def'
        [IO.File]::WriteAllText($definition,"LIBRARY pdfium.dll`nEXPORTS`n"+($names -join "`n")+"`n")
        $command=@"
call "$Developer" -no_logo -arch=$arch -host_arch=$HostArchitecture
if errorlevel 1 exit /b 1
lib /nologo /def:"$definition" /machine:$arch /out:"$Directory/pdfium-import.lib"
if errorlevel 1 exit /b 1
cl /nologo /std:c11 /O2 /MT /W4 /WX /LD /Fe:"$Directory/fluent-pdf-render.dll" /Fo:"$Directory/fluent-pdf-render.obj" native/rendering/bridge.c "$Directory/pdfium-import.lib" /link /DEF:native/worker/bridge.def /IMPLIB:"$Directory/fluent-pdf-render.lib"
exit /b %errorlevel%
"@
        $script=Join-Path $Directory 'compile-render.cmd'
        [IO.File]::WriteAllText($script,$command.Replace("`n","`r`n"))
        & cmd.exe /d /c $script
    }else{
        $shared=@(if($IsMacOS){'-dynamiclib'}else{'-shared';'-fPIC'})
        $origin=if($IsMacOS){'@loader_path'}else{'$ORIGIN'}
        & cc -std=c11 -O2 -Wall -Wextra -Werror @shared native/rendering/bridge.c $engine "-Wl,-rpath,$origin" -o (Join-Path $Directory "fluent-pdf-render.$extension")
    }
    if($LASTEXITCODE -ne 0){throw "Native rendering bridge build failed: $Rid"}
    if($IsMacOS){
        $bridge=Join-Path $Directory 'fluent-pdf-render.dylib'
        & install_name_tool -change './libpdfium.dylib' '@loader_path/libpdfium.dylib' $bridge
        if($LASTEXITCODE -ne 0){throw 'Cannot bind the bridge to its exact private engine asset.'}
        & codesign --force --sign - $bridge
        if($LASTEXITCODE -ne 0){throw 'Cannot ad-hoc sign the original rendering bridge.'}
    }
    if((Get-FileHash -LiteralPath $engine).Hash.ToLowerInvariant() -cne $sourceHash){throw 'The pinned engine asset changed during bridge creation.'}
    $record=[ordered]@{rid=$Rid;containerVersion=$inventory.containerVersion;engineVersion=$inventory.version;sourceAssetSha256=$sourceHash;derivedAssetSha256=(Get-FileHash -LiteralPath $engine).Hash.ToLowerInvariant();derivation='unchanged pinned engine asset';bridgeDerivation=if($IsMacOS){'private @loader_path dependency; ad-hoc codesign of original bridge only'}else{'original bridge linked to exact private dependency'};inventorySha256=(Get-FileHash -LiteralPath (Join-Path $root 'third-party/rendering/components.json')).Hash.ToLowerInvariant()}
    $record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Directory 'rendering-asset.json') -Encoding utf8
    $evidence=Join-Path $root "artifacts/contracts/rendering/$Rid"
    $null=[IO.Directory]::CreateDirectory($evidence)
    Copy-Item -LiteralPath (Join-Path $Directory 'rendering-asset.json') -Destination $evidence
    Copy-Item -LiteralPath $notices -Destination $evidence -Recurse -Force
}finally{Pop-Location}
