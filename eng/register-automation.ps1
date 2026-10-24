[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Register','Unregister')][string]$Action,
    [Parameter(Mandatory)][string]$AssemblyPath,
    [Parameter(Mandatory)][ValidateSet('X86','X64')][string]$Architecture,
    [ValidateSet('CurrentUser','LocalMachine')][string]$Scope = 'CurrentUser'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$IsWindows) { throw 'COM registration requires actual Windows.' }
if (![Environment]::Is64BitOperatingSystem) { throw 'This registration profile requires64-bit Windows; x86 clients use its WOW64 view.' }
$assembly = [IO.Path]::GetFullPath($AssemblyPath)
if (!(Test-Path -LiteralPath $assembly -PathType Leaf)) { throw 'The explicit net472 assembly is absent.' }
$name = [Reflection.AssemblyName]::GetAssemblyName($assembly)
if ($name.Name -cne 'Fluent.Pdf.Automation' -or $name.Version.ToString() -ne '1.0.0.0') { throw 'Unexpected frozen automation assembly identity.' }
$previousCheckPath = $env:FLUENT_PDF_AUTOMATION_ASSEMBLY_CHECK
try {
    $env:FLUENT_PDF_AUTOMATION_ASSEMBLY_CHECK = $assembly
    # Reflection-only inspection runs in a short-lived Framework process. It neither
    # executes the assembly nor leaves a deployment file locked in the caller.
    $inspection = @'
try {
  $a = [Reflection.Assembly]::ReflectionOnlyLoadFrom($env:FLUENT_PDF_AUTOMATION_ASSEMBLY_CHECK)
  $t = @($a.GetCustomAttributesData() | Where-Object { $_.AttributeType.FullName -eq 'System.Runtime.Versioning.TargetFrameworkAttribute' })
  if ($t.Count -ne 1 -or $t[0].ConstructorArguments[0].Value -cne '.NETFramework,Version=v4.7.2') { exit 2 }
  Write-Output 'NET472_METADATA_PASS'
} catch { exit 3 }
'@
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($inspection))
    $frameworkShell = Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe'
    $checked = & $frameworkShell -NoProfile -NonInteractive -EncodedCommand $encoded
    if ($LASTEXITCODE -ne 0 -or $checked -cne 'NET472_METADATA_PASS') { throw 'Registration requires the exact net472 assembly, not a CoreCLR or netstandard asset.' }
} finally { $env:FLUENT_PDF_AUTOMATION_ASSEMBLY_CHECK = $previousCheckPath }
$uri = ([Uri]$assembly).AbsoluteUri
$view = if ($Architecture -eq 'X86') { [Microsoft.Win32.RegistryView]::Registry32 } else { [Microsoft.Win32.RegistryView]::Registry64 }
$hive = if ($Scope -eq 'CurrentUser') { [Microsoft.Win32.RegistryHive]::CurrentUser } else { [Microsoft.Win32.RegistryHive]::LocalMachine }
$base = [Microsoft.Win32.RegistryKey]::OpenBaseKey($hive, $view)
$class = 'Software\Classes\CLSID\{20F743F7-79B0-4E47-8F20-B0588A691025}'
$progid = 'Software\Classes\Fluent.Pdf.Automation.1'
$owner = 'Fluent.Pdf.Automation.SourceProfile.v1'
try {
    $present = @{}
    foreach ($path in @($class, $progid)) {
        $key = $base.OpenSubKey($path)
        $present[$path] = $null -ne $key
        if ($null -ne $key) {
            try { if ($key.GetValue('Owner') -cne $owner -or $key.GetValue('OwnedCodeBase') -cne $uri) { throw 'A conflicting registration exists; no key was overwritten or removed.' } }
            finally { $key.Dispose() }
        }
    }
    $otherView = if ($Architecture -eq 'X86') { [Microsoft.Win32.RegistryView]::Registry64 } else { [Microsoft.Win32.RegistryView]::Registry32 }
    $otherBase = [Microsoft.Win32.RegistryKey]::OpenBaseKey($hive, $otherView)
    $otherRegistered = $false
    try {
        $other = $otherBase.OpenSubKey($class)
        if ($null -ne $other) {
            try {
                if ($other.GetValue('Owner') -cne $owner -or $other.GetValue('OwnedCodeBase') -cne $uri) { throw 'The other bitness has a conflicting CLSID owner; no registration was changed.' }
                $otherRegistered = $true
            } finally { $other.Dispose() }
        }
    } finally { $otherBase.Dispose() }
    if ($Action -eq 'Unregister') {
        $base.DeleteSubKeyTree($class, $false)
        # ProgIDs may be shared across WOW64 views; preserve the other bitness.
        if (!$otherRegistered) { $base.DeleteSubKeyTree($progid, $false) }
    } elseif ($present[$class]) {
        if (!$present[$progid]) { throw 'Partial owned registration exists; explicitly unregister before repair.' }
        $key = $base.OpenSubKey($class + '\InprocServer32')
        try { if ($null -eq $key -or $key.GetValue('CodeBase') -cne $uri -or $key.GetValue('Assembly') -cne $name.FullName -or $key.GetValue('Class') -cne 'Fluent.Pdf.Automation.PdfAutomation') { throw 'Existing owned registration is incomplete; explicitly unregister before repair.' } }
        finally { if ($null -ne $key) { $key.Dispose() } }
    } else {
        if ($present[$progid] -and !$otherRegistered) { throw 'Partial owned ProgID exists; explicitly unregister before repair.' }
        $created = [Collections.Generic.List[string]]::new()
        try {
            foreach ($path in @($class, $progid)) {
                if (!$present[$path]) {
                    $created.Add($path)
                    $key = $base.CreateSubKey($path)
                    try { $key.SetValue('Owner', $owner); $key.SetValue('OwnedCodeBase', $uri) } finally { $key.Dispose() }
                }
            }
            if (!$present[$progid]) {
                $key = $base.CreateSubKey($progid + '\CLSID')
                try { $key.SetValue('', '{20F743F7-79B0-4E47-8F20-B0588A691025}') } finally { $key.Dispose() }
            }
            $key = $base.CreateSubKey($class + '\ProgID')
            try { $key.SetValue('', 'Fluent.Pdf.Automation.1') } finally { $key.Dispose() }
            foreach ($suffix in @('\InprocServer32', '\InprocServer32\1.0.0.0')) {
                $key = $base.CreateSubKey($class + $suffix)
                try {
                    $key.SetValue('', 'mscoree.dll'); $key.SetValue('ThreadingModel', 'Both')
                    $key.SetValue('Class', 'Fluent.Pdf.Automation.PdfAutomation'); $key.SetValue('Assembly', $name.FullName)
                    $key.SetValue('RuntimeVersion', 'v4.0.30319'); $key.SetValue('CodeBase', $uri)
                } finally { $key.Dispose() }
            }
        } catch {
            foreach ($path in $created) { $base.DeleteSubKeyTree($path, $false) }
            throw
        }
    }
    [ordered]@{ action=$Action;scope=$Scope;architecture=$Architecture;assembly=$assembly;assemblySha256=(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash.ToLowerInvariant();clsid='20F743F7-79B0-4E47-8F20-B0588A691025' } | ConvertTo-Json -Compress
} finally { $base.Dispose() }
