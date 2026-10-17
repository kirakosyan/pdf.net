[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=Split-Path $PSScriptRoot -Parent
$destination=Join-Path $root 'artifacts/native-fixtures'
$architecture=[Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
$os=if($IsWindows){'win'}elseif($IsMacOS){'osx'}else{'linux'}
$architectures=if($IsWindows -and $architecture -eq 'x64'){@('x64','x86')}else{@($architecture)}
$records=[Collections.Generic.List[object]]::new()
Push-Location $root
try {
    foreach($arch in $architectures) {
        $rid="$os-$arch"
        $directory=Join-Path $destination $rid
        $null=[IO.Directory]::CreateDirectory($directory)
        if($IsWindows) {
            $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
            $component=if($arch -eq 'arm64'){'Microsoft.VisualStudio.Component.VC.Tools.ARM64'}else{'Microsoft.VisualStudio.Component.VC.Tools.x86.x64'}
            $installation=(& $vswhere -latest -products '*' -requires $component -property installationPath | Select-Object -First 1)
            if(!$installation) { throw 'A supported Visual C compiler is required for native worker fixtures.' }
            $developer=Join-Path $installation 'Common7/Tools/VsDevCmd.bat'
            $command=@"
call "$developer" -no_logo -arch=$arch -host_arch=$architecture
if errorlevel 1 exit /b 1
cl /nologo /std:c11 /O2 /MT /W4 /WX /D_CRT_SECURE_NO_WARNINGS /D_WIN32_WINNT=0x0A00 /Fe:"$directory/worker.exe" /Fo:"$directory/worker.obj" native/worker/worker.c userenv.lib advapi32.lib
if errorlevel 1 exit /b 1
cl /nologo /std:c11 /O2 /MT /W4 /WX /D_CRT_SECURE_NO_WARNINGS /LD /Fe:"$directory/bridge.dll" /Fo:"$directory/bridge.obj" tests/native/bridge-fixture.c ws2_32.lib /link /DEF:native/worker/bridge.def /IMPLIB:"$directory/bridge.lib"
if errorlevel 1 exit /b 1
cl /nologo /std:c11 /O2 /MT /W4 /WX /D_CRT_SECURE_NO_WARNINGS /DFIXTURE_ABI=\"wrong-abi\" /LD /Fe:"$directory/wrong-abi.dll" /Fo:"$directory/wrong-abi.obj" tests/native/bridge-fixture.c ws2_32.lib /link /DEF:native/worker/bridge.def /IMPLIB:"$directory/wrong-abi.lib"
if errorlevel 1 exit /b 1
cl /nologo /std:c11 /O2 /MT /W4 /WX /LD /Fe:"$directory/required.dll" /Fo:"$directory/required.obj" tests/native/dependency-fixture.c /link /IMPLIB:"$directory/required.lib"
if errorlevel 1 exit /b 1
cl /nologo /std:c11 /O2 /MT /W4 /WX /D_CRT_SECURE_NO_WARNINGS /DFIXTURE_DEPENDENCY /LD /Fe:"$directory/dependent.dll" /Fo:"$directory/dependent.obj" tests/native/bridge-fixture.c "$directory/required.lib" ws2_32.lib /link /DEF:native/worker/bridge.def /IMPLIB:"$directory/dependent.lib"
exit /b %errorlevel%
"@
            $script=Join-Path $directory 'compile.cmd'
            [IO.File]::WriteAllText($script,$command.Replace("`n","`r`n"))
            & cmd.exe /d /c $script
            if($LASTEXITCODE -ne 0) { throw "Native build failed: $rid" }
        } else {
            $common=@('-std=c11','-O2','-Wall','-Wextra','-Werror')
            $shared=@(if($IsMacOS){'-dynamiclib'}else{'-shared';'-fPIC'})
            $extension=if($IsMacOS){'dylib'}else{'so'}
            $workerExtra=@(if($IsMacOS){'-Wno-deprecated-declarations'}else{'-ldl'})
            & cc @common 'native/worker/worker.c' @workerExtra '-o' (Join-Path $directory 'worker')
            if($LASTEXITCODE -ne 0) { throw "Native worker build failed: $rid" }
            & cc @common @shared 'tests/native/bridge-fixture.c' '-o' (Join-Path $directory "bridge.$extension")
            if($LASTEXITCODE -ne 0) { throw "Native bridge build failed: $rid" }
            & cc @common @shared '-DFIXTURE_ABI="wrong-abi"' 'tests/native/bridge-fixture.c' '-o' (Join-Path $directory "wrong-abi.$extension")
            if($LASTEXITCODE -ne 0) { throw "Wrong-ABI fixture build failed: $rid" }
            $dependencyIdentity=@(if($IsMacOS){'-Wl,-install_name,@rpath/required.dylib'}else{'-Wl,-soname,required.so'})
            & cc @common @shared @dependencyIdentity 'tests/native/dependency-fixture.c' '-o' (Join-Path $directory "required.$extension")
            if($LASTEXITCODE -ne 0) { throw "Dependency fixture build failed: $rid" }
            $origin=if($IsMacOS){'@loader_path'}else{'$ORIGIN'}
            & cc @common @shared '-DFIXTURE_DEPENDENCY' 'tests/native/bridge-fixture.c' (Join-Path $directory "required.$extension") "-Wl,-rpath,$origin" '-o' (Join-Path $directory "dependent.$extension")
            if($LASTEXITCODE -ne 0) { throw "Dependent bridge fixture build failed: $rid" }
        }
        if($arch -eq $architecture) {
            & dotnet restore 'tests/native-aot/NativeFixture.csproj' --locked-mode
            if($LASTEXITCODE -ne 0) { throw "Locked native-AOT fixture restore failed: $rid" }
            & dotnet publish 'tests/native-aot/NativeFixture.csproj' -c Release -r $rid --no-restore -o $directory
            if($LASTEXITCODE -ne 0) { throw "Native-AOT fixture build failed: $rid" }
        }
        $files=@(Get-ChildItem -LiteralPath $directory -File | Where-Object { $_.Name -match '^(worker(\.exe)?|(bridge|wrong-abi|dependent|required|native-aot-fixture)\.(dll|so|dylib))$' } | ForEach-Object {
            [ordered]@{name=$_.Name;length=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
        })
        $records.Add([ordered]@{rid=$rid;files=$files})
    }
    [ordered]@{schemaVersion=1;purpose='original-native-test-fixtures';sourceCommit=(& git rev-parse HEAD).Trim();
        nativeAot=[ordered]@{sdk=(& dotnet --version).Trim();lockSha256=(Get-FileHash -LiteralPath (Join-Path $root 'tests/native-aot/packages.lock.json')).Hash};
        deployments=@($records.ToArray())} |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $destination 'native-fixtures.json') -Encoding utf8
    $contracts=Join-Path $root 'artifacts/contracts'
    $null=[IO.Directory]::CreateDirectory($contracts)
    Copy-Item -LiteralPath (Join-Path $destination 'native-fixtures.json') -Destination (Join-Path $contracts 'native-fixtures.json')
} finally { Pop-Location }
