[CmdletBinding()]
param([Parameter(Mandatory)][string]$Directory,[Parameter(Mandatory)][string]$Rid,[string]$Developer,[string]$HostArchitecture)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $paths=(& dotnet msbuild native/text/Assets.csproj -getProperty:PkgHarfBuzzSharp_NativeAssets_Linux -getProperty:PkgHarfBuzzSharp_NativeAssets_macOS -getProperty:PkgHarfBuzzSharp_NativeAssets_Win32 | Out-String | ConvertFrom-Json).Properties
    if($LASTEXITCODE -ne 0){throw 'Cannot resolve locked native shaping assets.'}
    $arch=$Rid.Split('-')[1]
    $extension=if($IsWindows){'dll'}elseif($IsMacOS){'dylib'}else{'so'}
    $package=if($IsWindows){$paths.PkgHarfBuzzSharp_NativeAssets_Win32}elseif($IsMacOS){$paths.PkgHarfBuzzSharp_NativeAssets_macOS}else{$paths.PkgHarfBuzzSharp_NativeAssets_Linux}
    $assetRid=if($IsMacOS){'osx'}else{$Rid}
    $source=Join-Path $package "runtimes/$assetRid/native/libHarfBuzzSharp.$extension"
    $dependency=Join-Path $Directory $(if(!$IsWindows -and !$IsMacOS){'libHarfBuzzSharp.so.0.61421.0'}else{"libHarfBuzzSharp.$extension"})
    if($IsMacOS){
        & lipo $source -thin $arch -output $dependency
        if($LASTEXITCODE -ne 0){throw 'Cannot create the explicitly inventoried thin shaping asset.'}
        & install_name_tool -id '@rpath/libHarfBuzzSharp.dylib' $dependency
        if($LASTEXITCODE -ne 0){throw 'Cannot set the private shaping dependency identity.'}
        & codesign --force --sign - $dependency
        if($LASTEXITCODE -ne 0){throw 'Cannot ad-hoc sign the derived shaping asset.'}
    }else{Copy-Item -LiteralPath $source -Destination $dependency}
    if($IsWindows){
        $names=[regex]::Matches([IO.File]::ReadAllText((Join-Path $root 'native/text/bridge.c')),'extern[^;]+?\b(hb_\w+)\(') | ForEach-Object {$_.Groups[1].Value}
        $definition=Join-Path $Directory 'harfbuzz-import.def'
        [IO.File]::WriteAllText($definition,"LIBRARY libHarfBuzzSharp.dll`nEXPORTS`n"+($names -join "`n")+"`n")
        $command=@"
call "$Developer" -no_logo -arch=$arch -host_arch=$HostArchitecture
if errorlevel 1 exit /b 1
lib /nologo /def:"$definition" /machine:$arch /out:"$Directory/harfbuzz-import.lib"
if errorlevel 1 exit /b 1
cl /nologo /std:c11 /O2 /MT /W4 /WX /LD /Fe:"$Directory/fluent-pdf-text.dll" /Fo:"$Directory/fluent-pdf-text.obj" native/text/bridge.c "$Directory/harfbuzz-import.lib" /link /DEF:native/worker/bridge.def /IMPLIB:"$Directory/fluent-pdf-text.lib"
exit /b %errorlevel%
"@
        $script=Join-Path $Directory 'compile-text.cmd'
        [IO.File]::WriteAllText($script,$command.Replace("`n","`r`n"))
        & cmd.exe /d /c $script
    }else{
        $shared=@(if($IsMacOS){'-dynamiclib'}else{'-shared';'-fPIC'})
        $origin=if($IsMacOS){'@loader_path'}else{'$ORIGIN'}
        & cc -std=c11 -O2 -Wall -Wextra -Werror @shared native/text/bridge.c $dependency "-Wl,-rpath,$origin" -o (Join-Path $Directory "fluent-pdf-text.$extension")
    }
    if($LASTEXITCODE -ne 0){throw "Native shaping bridge build failed: $Rid"}
    foreach($notice in @('LICENSE.txt','THIRD-PARTY-NOTICES.txt')){Copy-Item -LiteralPath (Join-Path $package $notice) -Destination (Join-Path $Directory "harfbuzz-$notice")}
    [ordered]@{rid=$Rid;packageVersion='14.2.1.301';nativeVersion='14.2.1';sourceAssetSha256=(Get-FileHash -LiteralPath $source).Hash;derivedAssetSha256=(Get-FileHash -LiteralPath $dependency).Hash;derivation=if($IsMacOS){'lipo thin; install_name_tool private identity; ad-hoc codesign'}else{'unchanged pinned asset'}} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Directory 'text-asset.json') -Encoding utf8
}finally{Pop-Location}
