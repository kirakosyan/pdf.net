[CmdletBinding()]
param([string]$ZigExecutable='', [string]$ZigSha256='')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=Split-Path $PSScriptRoot -Parent
$architecture=[Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
$os=if($IsWindows){'win'}elseif($IsMacOS){'osx'}else{'linux'}
if ($ZigExecutable -and !$IsWindows) { throw 'The explicit Zig source-fixture path currently targets Windows only; use the host C compiler on Unix.' }
$architectures=if($IsWindows -and $architecture -eq 'x64'){@('x64','x86')}else{@($architecture)}
$records=@()
foreach($arch in $architectures) {
    $directory=Join-Path $root "artifacts/browser-cli-fixture/$os-$arch"
    $null=[IO.Directory]::CreateDirectory($directory)
    $source=Join-Path $root 'tests/native/browser-cli-fixture.c'
    $executable=Join-Path $directory $(if($IsWindows){'browser-cli-fixture.exe'}else{'browser-cli-fixture'})
    if($ZigExecutable) {
        if(![IO.Path]::IsPathFullyQualified($ZigExecutable) -or $ZigSha256 -notmatch '^[0-9a-f]{64}$' -or (Get-FileHash -LiteralPath $ZigExecutable -Algorithm SHA256).Hash.ToLowerInvariant() -ne $ZigSha256) { throw 'An explicitly pinned absolute task-local compiler is required.' }
        $env:ZIG_GLOBAL_CACHE_DIR=Join-Path $directory 'compiler-cache'
        $target=if($arch -eq 'x64'){'x86_64-windows-gnu'}elseif($arch -eq 'x86'){'x86-windows-gnu'}else{'aarch64-windows-gnu'}
        & $ZigExecutable cc -target $target -std=c11 -O2 -Wall -Wextra -Werror $source -o $executable
    } elseif($IsWindows) {
        $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $component=if($arch -eq 'arm64'){'Microsoft.VisualStudio.Component.VC.Tools.ARM64'}else{'Microsoft.VisualStudio.Component.VC.Tools.x86.x64'}
        $installation=(& $vswhere -latest -products '*' -requires $component -property installationPath | Select-Object -First 1)
        if(!$installation) { throw 'The source fixture requires its existing Visual C toolchain.' }
        $developer=Join-Path $installation 'Common7/Tools/VsDevCmd.bat'
        $command=@"
call "$developer" -no_logo -arch=$arch -host_arch=$architecture
if errorlevel 1 exit /b 1
cl /nologo /std:c11 /O2 /MT /W4 /WX /D_CRT_SECURE_NO_WARNINGS /Fe:"$executable" /Fo:"$directory/fixture.obj" "$source"
exit /b %errorlevel%
"@
        $script=Join-Path $directory 'compile.cmd'
        [IO.File]::WriteAllText($script,$command.Replace("`n","`r`n"))
        & cmd.exe /d /c $script
    } else { & cc -std=c11 -O2 -Wall -Wextra -Werror $source -o $executable }
    if($LASTEXITCODE -ne 0){throw 'Original browser CLI fixture compilation failed.'}
    $records+=@{rid="$os-$arch";path=$executable;sha256=(Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant()}
}
@{purpose='original-browser-transport-process-peer-not-browser-qualification';sourceCommit=(& git -C $root rev-parse HEAD).Trim();workingTreeDirty=[bool](& git -C $root status --porcelain);sourceSha256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant();files=$records} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'artifacts/browser-cli-fixture/manifest.json') -Encoding utf8
$null=[IO.Directory]::CreateDirectory((Join-Path $root 'artifacts/contracts'))
Copy-Item -LiteralPath (Join-Path $root 'artifacts/browser-cli-fixture/manifest.json') -Destination (Join-Path $root 'artifacts/contracts/browser-cli-fixture-manifest.json')
