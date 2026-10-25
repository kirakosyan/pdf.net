[CmdletBinding()]
param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root ('artifacts/package-metadata/' + [Guid]::NewGuid().ToString('N')) }
$null = [IO.Directory]::CreateDirectory($OutputDirectory)
[IO.File]::Delete((Join-Path $OutputDirectory 'report.json'))
$version = (Get-Content -LiteralPath (Join-Path $root 'specs/release-readiness.json') -Raw | ConvertFrom-Json).packageVersion
$rows = [Collections.Generic.List[object]]::new()
$expectedFiles = @{'README.md'=(Join-Path $root 'README.md');'LICENSE'=(Join-Path $root 'LICENSE')}
Get-ChildItem -LiteralPath (Join-Path $root 'third-party') -File -Recurse | Where-Object { $_.Extension -in @('.txt','.md','.json') -or $_.Name.EndsWith('-LICENSE') } | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath((Join-Path $root 'third-party'), $_.FullName).Replace('\','/')
    $expectedFiles["notices/$relative"] = $_.FullName
}
foreach ($project in @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'PublicAPI.Shipped.txt') } | Sort-Object Name)) {
    $path = Join-Path $project.FullName ($project.Name + '.csproj')
    $json = & dotnet msbuild $path -nologo -p:TargetFramework=netstandard2.0 '-t:_GetProjectReferenceVersions;_GetPackageFiles' -getProperty:PackageId,PackageVersion,IsPackable,IncludeSymbols,SymbolPackageFormat,DebugType,PackageReadmeFile,PackageLicenseExpression,TargetFrameworks -getItem:_ProjectReferencesWithVersions,_PackageFiles
    if ($LASTEXITCODE -ne 0) { throw 'Package metadata evaluation failed.' }
    $json | Set-Content -LiteralPath (Join-Path $OutputDirectory ($project.Name + '.json')) -Encoding utf8NoBOM
    $data = $json | ConvertFrom-Json
    $p = $data.Properties
    if ($p.PackageId -cne $project.Name -or $p.PackageVersion -cne $version -or $p.IsPackable -cne 'true' -or $p.IncludeSymbols -cne 'true' -or $p.SymbolPackageFormat -cne 'snupkg' -or $p.DebugType -cne 'portable' -or $p.PackageReadmeFile -cne 'README.md' -or $p.PackageLicenseExpression -cne 'MIT' -or $p.TargetFrameworks -cne 'net472;netstandard2.0;net8.0;net10.0') { throw "Candidate metadata drift: $($project.Name)" }
    foreach ($reference in $data.Items._ProjectReferencesWithVersions) {
        if ($reference.ProjectVersion -cne "[$version]") { throw 'SDK dependencies must be version coupled.' }
    }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($file in $data.Items._PackageFiles) {
        $destination = $file.PackagePath.Replace('\','/').Trim('/')
        if ($destination) { $destination += '/' }
        $destination += [IO.Path]::GetFileName($file.FullPath)
        if (!$seen.Add($destination) -or !$expectedFiles.ContainsKey($destination) -or [IO.Path]::GetFullPath($file.FullPath) -cne [IO.Path]::GetFullPath($expectedFiles[$destination])) { throw 'Unreviewed, duplicate or misplaced package content.' }
    }
    if ($seen.Count -ne $expectedFiles.Count) { throw 'Missing package README/license/notices.' }
    $rows.Add([ordered]@{id=$project.Name;version=$version;coupledReferences=@($data.Items._ProjectReferencesWithVersions).Count;noticeAndMetadataFiles=$seen.Count})
}
$props = [Security.SecurityElement]::Escape((Join-Path $root 'src/Directory.Build.props'))
$targets = [Security.SecurityElement]::Escape((Join-Path $root 'src/Directory.Build.targets'))
$overrideProject = Join-Path $OutputDirectory 'project-overrides.csproj'
@"
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="$props" />
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Description>Project-owned description.</Description>
    <PackageId>Fixture.OwnedPackage</PackageId>
    <Version>2.3.4</Version>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <Import Project="$targets" />
</Project>
"@ | Set-Content -LiteralPath $overrideProject -Encoding utf8NoBOM
$overrideJson = & dotnet msbuild $overrideProject -nologo -getProperty:Description,PackageId,Version,PackageVersion,IsPackable
if ($LASTEXITCODE -ne 0) { throw 'Project override metadata evaluation failed.' }
$override = ($overrideJson | ConvertFrom-Json).Properties
if ($override.Description -cne 'Project-owned description.' -or $override.PackageId -cne 'Fixture.OwnedPackage' -or $override.Version -cne '2.3.4' -or $override.PackageVersion -cne '2.3.4' -or $override.IsPackable -cne 'false') { throw 'Shared defaults overrode explicit project metadata or package opt-out.' }
$overrideJson | Set-Content -LiteralPath (Join-Path $OutputDirectory 'project-overrides.json') -Encoding utf8NoBOM
$rows.ToArray() | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'report.json') -Encoding utf8NoBOM
Write-Output "PACKAGE_METADATA_PASS;$($rows.Count)-projects"
