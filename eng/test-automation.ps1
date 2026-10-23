[CmdletBinding()]
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$IsWindows -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64') { throw 'Actual x64 Windows is required for the x86/x64 COM consumer matrix.' }
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'artifacts/contracts/automation'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$manifest = Join-Path $output 'windows-consumers.json'
[IO.File]::Delete($manifest)
$assembly = Join-Path $root "src/Fluent.Pdf.Automation/bin/$Configuration/net472/Fluent.Pdf.Automation.dll"
$source = Join-Path $root 'examples/com/automation-native.cpp'
$inputPdf = Join-Path $root 'tests/Fluent.Pdf.Contract.Tests/Fixtures/Automation/original.pdf'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$installation = (& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1)
if (!$installation) { throw 'The explicit x86/x64 native COM consumer compiler is absent.' }
$records = [Collections.Generic.List[object]]::new()
$registered = [Collections.Generic.List[string]]::new()
try {
    # Explicit machine scope on a disposable runner avoids elevated-client lookup restrictions.
    foreach ($architecture in @('X86','X64')) {
        $directory = Join-Path $output $architecture.ToLowerInvariant()
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        & (Join-Path $PSScriptRoot 'register-automation.ps1') -Action Register -AssemblyPath $assembly -Architecture $architecture -Scope LocalMachine | Set-Content (Join-Path $directory 'registration.json')
        $registered.Add($architecture)
        & (Join-Path $PSScriptRoot 'register-automation.ps1') -Action Register -AssemblyPath $assembly -Architecture $architecture -Scope LocalMachine | Set-Content (Join-Path $directory 'idempotent-registration.json')
    }
foreach ($architecture in @('X86','X64')) {
    $directory = Join-Path $output $architecture.ToLowerInvariant()
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $exe = Join-Path $directory 'automation-native.exe'
    $object = Join-Path $directory 'automation-native.obj'
    $compilerArchitecture = if ($architecture -eq 'X86') { 'x86' } else { 'x64' }
    & $env:ComSpec /d /c (Join-Path $PSScriptRoot 'build-automation-native.cmd') $installation $compilerArchitecture $source $exe $object 2>&1 | Tee-Object -FilePath (Join-Path $directory 'compiler.log')
    if ($LASTEXITCODE -ne 0) { throw "Native COM consumer compilation failed: $architecture" }
        $cscript = Join-Path $env:WINDIR $(if ($architecture -eq 'X86') { 'SysWOW64/cscript.exe' } else { 'System32/cscript.exe' })
        $vbsOutput = Join-Path $directory 'vbs-reordered.pdf'
        $vbsLog = Join-Path $directory 'vbs.log'
        & $cscript //nologo //B (Join-Path $root 'examples/com/automation.vbs') $inputPdf $vbsOutput 2>&1 | Tee-Object -FilePath $vbsLog
        if ($LASTEXITCODE -ne 0) { throw "Actual VBScript COM consumer failed: $architecture" }
        $vbs = Get-Content $vbsLog -Raw
        if (!$vbs.Contains('COM_VBS_PASS;') -or !$vbs.Contains(";$architecture;") -or !$vbs.Contains('.NET Framework')) { throw "VBScript CLR/bitness identity is missing: $architecture" }
        $nativeOutput = Join-Path $directory 'native-reordered.pdf'
        $nativeLog = Join-Path $directory 'native.log'
        & $exe $inputPdf $nativeOutput 2>&1 | Tee-Object -FilePath $nativeLog
        if ($LASTEXITCODE -ne 0) { throw "Actual native COM consumer failed: $architecture" }
        $native = Get-Content $nativeLog -Raw
        $bits = if ($architecture -eq 'X86') { 32 } else { 64 }
        if (!$native.Contains("COM_NATIVE_PASS;bits=$bits;") -or !$native.Contains(";$architecture;") -or !$native.Contains('.NET Framework')) { throw "Native CLR/bitness identity is missing: $architecture" }
        foreach ($row in @(@{consumer='VBScript';path=$vbsOutput;log=$vbsLog;identity=$vbs.Trim()}, @{consumer='native-dual-and-IDispatch';path=$nativeOutput;log=$nativeLog;identity=$native.Trim()})) {
            $records.Add([ordered]@{ architecture=$architecture;consumer=$row.consumer;identity=$row.identity;passed=$true;output=$row.path;outputSha256=(Get-FileHash $row.path -Algorithm SHA256).Hash.ToLowerInvariant();logSha256=(Get-FileHash $row.log -Algorithm SHA256).Hash.ToLowerInvariant() })
        }
}
    & (Join-Path $PSScriptRoot 'register-automation.ps1') -Action Unregister -AssemblyPath $assembly -Architecture X86 -Scope LocalMachine | Set-Content (Join-Path $output 'x86/unregistration.json')
    $probeScript = Join-Path $env:WINDIR 'System32/cscript.exe'
    & $probeScript //nologo //B (Join-Path $root 'examples/com/automation.vbs') $inputPdf (Join-Path $output 'x64/shared-progid-probe.pdf') 2>&1 | Tee-Object -FilePath (Join-Path $output 'x64/shared-progid-probe.log')
    if ($LASTEXITCODE -ne 0) { throw 'Removing x86 registration damaged the remaining x64 ProgID activation.' }
} finally {
    foreach ($architecture in $registered) {
        & (Join-Path $PSScriptRoot 'register-automation.ps1') -Action Unregister -AssemblyPath $assembly -Architecture $architecture -Scope LocalMachine | Set-Content (Join-Path $output ($architecture.ToLowerInvariant() + '/final-unregistration.json'))
    }
}
foreach ($architecture in @('X86','X64')) {
    $directory = Join-Path $output $architecture.ToLowerInvariant()
    & (Join-Path $directory 'automation-native.exe') --expect-unregistered 2>&1 | Tee-Object -FilePath (Join-Path $directory 'unregistered.log')
    if ($LASTEXITCODE -ne 0) { throw "Fresh-process activation after removal did not reject: $architecture" }
}
$value = [ordered]@{ schemaVersion=1;sourceCommit=(& git -C $root rev-parse HEAD).Trim();workingTreeDirty=[bool](& git -C $root status --porcelain);os=[Runtime.InteropServices.RuntimeInformation]::OSDescription;hostArchitecture='X64';assemblySha256=(Get-FileHash $assembly -Algorithm SHA256).Hash.ToLowerInvariant();asset='net472';registrationScope='LocalMachine';sideBySideBitness=$true;remainingBitnessAfterRemovalPassed=$true;consumers=$records.ToArray();actualMinimumFrameworkQualified=$false;finalPackagesQualified=$false;productionQualified=$false }
$value | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifest -Encoding utf8NoBOM
Write-Output 'Actual Windows COM consumers passed: VBScript/native x86/x64, registration/removal and stable IErrorInfo.'
