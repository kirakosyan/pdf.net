[CmdletBinding()]
param([Parameter(Mandatory)][string]$Directory,[Parameter(Mandatory)][string]$Rid,[string]$Developer,[string]$HostArchitecture)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $arch=$Rid.Split('-')[1]
    $extension=if($IsWindows){'dll'}elseif($IsMacOS){'dylib'}else{'so'}
    $engine=if($IsWindows){'tesseract.dll'}elseif($IsMacOS){'libtesseract.dylib'}else{'libtesseract.so'}
    if($IsWindows){
        $command=@"
call "$Developer" -no_logo -arch=$arch -host_arch=$HostArchitecture
if errorlevel 1 exit /b 1
cl /nologo /std:c11 /O2 /MT /W4 /WX /D_CRT_SECURE_NO_WARNINGS /D_WIN32_WINNT=0x0A00 /LD /Fe:"$Directory/fluent-pdf-ocr.dll" /Fo:"$Directory/fluent-pdf-ocr.obj" native/ocr/bridge.c /link /DEF:native/worker/bridge.def /IMPLIB:"$Directory/fluent-pdf-ocr.lib"
if errorlevel 1 exit /b 1
cl /nologo /std:c11 /O2 /MT /W4 /WX /D_CRT_SECURE_NO_WARNINGS /LD /Fe:"$Directory/$engine" /Fo:"$Directory/ocr-fixture.obj" tests/native/ocr-fixture.c /link /DEF:tests/native/ocr-fixture.def /IMPLIB:"$Directory/ocr-fixture.lib"
if errorlevel 1 exit /b 1
cl /nologo /std:c11 /O2 /MT /W4 /WX /D_CRT_SECURE_NO_WARNINGS /DOMIT_TSV /LD /Fe:"$Directory/ocr-missing.dll" /Fo:"$Directory/ocr-missing.obj" tests/native/ocr-fixture.c /link /DEF:tests/native/ocr-missing.def /IMPLIB:"$Directory/ocr-missing.lib"
exit /b %errorlevel%
"@
        $script=Join-Path $Directory 'compile-ocr.cmd'
        [IO.File]::WriteAllText($script,$command.Replace("`n","`r`n"))
        & cmd.exe /d /c $script
        if($LASTEXITCODE -ne 0){throw "Original OCR contract fixture build failed: $Rid"}
    } else {
        $shared=@(if($IsMacOS){'-dynamiclib'}else{'-shared';'-fPIC'})
        $loader=@(if(!$IsMacOS){'-ldl'})
        & cc -std=c11 -O2 -Wall -Wextra -Werror @shared native/ocr/bridge.c @loader -o (Join-Path $Directory "fluent-pdf-ocr.$extension")
        if($LASTEXITCODE -ne 0){throw "OCR bridge build failed: $Rid"}
        & cc -std=c11 -O2 -Wall -Wextra -Werror @shared tests/native/ocr-fixture.c -o (Join-Path $Directory $engine)
        if($LASTEXITCODE -ne 0){throw "Original C-API fixture build failed: $Rid"}
        & cc -std=c11 -O2 -Wall -Wextra -Werror @shared -DOMIT_TSV tests/native/ocr-fixture.c -o (Join-Path $Directory "ocr-missing.$extension")
        if($LASTEXITCODE -ne 0){throw "Original missing-symbol fixture build failed: $Rid"}
    }
    [IO.File]::WriteAllText((Join-Path $Directory 'eng.traineddata'),'original-model-fixture',[Text.UTF8Encoding]::new($false))
    $names=@("fluent-pdf-ocr.$extension",$engine,"ocr-missing.$extension",'eng.traineddata')
    [ordered]@{rid=$Rid;purpose='original-C-API-protocol-and-ownership-fixture-not-recognition';bridgeVersion='1.0';reportedFixtureVersion='5.5.0-fixture';actualTesseractEngine=$false;files=@($names|ForEach-Object{
        $path=Join-Path $Directory $_
        [ordered]@{name=$_;length=([IO.FileInfo]::new($path)).Length;sha256=(Get-FileHash -LiteralPath $path).Hash}
    })} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Directory 'ocr-asset.json') -Encoding utf8
} finally {Pop-Location}
