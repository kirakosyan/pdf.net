#requires -Version 7.2
[CmdletBinding()]
param([ValidateSet('Pack','Consumers')][string]$Stage,
      [Parameter(Mandatory)][string]$Directory,
      [string]$HostId = '', [string]$OutputDirectory = '', [switch]$PublicFeed)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$python = if ($IsWindows) { 'python' } else { 'python3' }
$directory = [IO.Path]::GetFullPath($Directory)
function Run([string]$File, [string[]]$Arguments) {
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Core release command failed: $File" }
}
function PrepareConsumer([string]$Destination, [string]$Fallback, [string]$Architecture = '') {
    if (Test-Path -LiteralPath $Destination) { throw 'Fresh package consumer workspace required.' }
    $null = [IO.Directory]::CreateDirectory($Destination)
    Copy-Item -LiteralPath (Join-Path $root 'eng/CoreConsumer/Program.cs'),(Join-Path $root 'eng/CoreConsumer/CoreConsumer.csproj') -Destination $Destination
    $configuration = [Xml.Linq.XElement]::new('configuration')
    $sources = [Xml.Linq.XElement]::new('packageSources')
    $sources.Add([Xml.Linq.XElement]::new('clear'))
    foreach ($pair in @(@('Candidate',(Join-Path $directory 'packages')),@('NuGet','https://api.nuget.org/v3/index.json'))) {
        if ($PublicFeed -and $pair[0] -eq 'Candidate') { continue }
        $entry = [Xml.Linq.XElement]::new('add'); $entry.SetAttributeValue('key',$pair[0]); $entry.SetAttributeValue('value',$pair[1]); $sources.Add($entry)
    }
    $configuration.Add($sources)
    $configuration.Save((Join-Path $Destination 'NuGet.Config'))
    $project = Join-Path $Destination 'CoreConsumer.csproj'
    $properties = @("-p:CoreAssetTfm=$Fallback")
    if ($Fallback) { $properties += '-p:TargetFrameworks=net10.0' }
    if ($Architecture) { $properties += @('-p:TargetFrameworks=net472',"-p:PlatformTarget=$Architecture") }
    Run 'dotnet' (@('restore',$project,'--configfile',(Join-Path $Destination 'NuGet.Config'),'--packages',(Join-Path $Destination 'fresh-packages'),'--no-cache','--disable-parallel','-m:1') + $properties)
    Run 'dotnet' (@('build',$project,'-c','Release','--no-restore','-m:1','-p:UseSharedCompilation=false') + $properties)
}
if ($Stage -eq 'Pack') {
    if (!$IsWindows -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') { throw 'Pack the exact Windows source-build core bytes used by the minimum-Framework bundle.' }
    & (Join-Path $root 'eng/assert-core-release.ps1') -ProjectName Fluent.Pdf -PackageVersion 1.0.0
    if (Test-Path -LiteralPath $directory) { throw 'Fresh core candidate directory required.' }
    $commit = (& git -C $root rev-parse HEAD).Trim()
    foreach ($tfm in @('net472','netstandard2.0','net8.0','net10.0')) {
        $binary = Join-Path $root "src/Fluent.Pdf/bin/Release/$tfm/Fluent.Pdf"
        Run 'dotnet' @((Join-Path $root 'eng/PackageProbe/bin/Release/net10.0/PackageProbe.dll'),"$binary.dll","$binary.pdb",$commit,(Join-Path $directory "source-probes/$tfm.json"))
    }
    Run 'dotnet' @('pack',(Join-Path $root 'src/Fluent.Pdf/Fluent.Pdf.csproj'),'-c','Release','--no-build','--no-restore','-o',(Join-Path $directory 'packages'),'-p:FluentPdfCoreRelease=true','-p:ContinuousIntegrationBuild=true')
    Run $python @((Join-Path $root 'eng/core_release.py'),'manifest','--directory',$directory)
    foreach ($architecture in @('x86','x64')) {
        $workspace = Join-Path $directory "consumer-build-$architecture"
        PrepareConsumer $workspace '' $architecture
        $target = Join-Path $directory "framework/$architecture"
        $null = [IO.Directory]::CreateDirectory($target)
        Get-ChildItem -LiteralPath (Join-Path $workspace 'bin/Release/net472') -File | Copy-Item -Destination $target
    }
} else {
    if ($HostId -notin @('win-x64','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64')) { throw 'Actual consumer host ID required.' }
    Run $python @((Join-Path $root 'eng/core_release.py'),'audit','--directory',$directory)
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    foreach ($mode in @('normal','fallback')) {
        $workspace = Join-Path $output "$mode-workspace"
        PrepareConsumer $workspace $(if ($mode -eq 'fallback') { 'netstandard2.0' } else { '' })
        # Each modern runtime executes the exact restored package DLL.
        foreach ($tfm in @('net8.0','net10.0')) {
            if ($mode -eq 'fallback' -and $tfm -eq 'net8.0') { continue }
            Run 'dotnet' @((Join-Path $workspace "bin/Release/$tfm/CoreConsumer.dll"),(Join-Path $output "$mode-$tfm"))
        }
    }
    if ($HostId -eq 'win-x64') {
        foreach ($architecture in @('x86','x64')) {
            Run (Join-Path $directory "framework/$architecture/CoreConsumer.exe") @((Join-Path $output "framework-$architecture"))
        }
    }
    Run $python @((Join-Path $root 'eng/core_release.py'),'host','--directory',$directory,'--reports',$output,'--host',$HostId)
}
