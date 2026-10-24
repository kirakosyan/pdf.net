[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=Split-Path $PSScriptRoot -Parent
$catalog=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'format-checks/invoice-107.json') -Raw | ConvertFrom-Json
$directory=Join-Path $root 'artifacts/invoice-fixtures'
$rules=Join-Path $directory 'rules'
$classes=Join-Path $directory 'classes'
$null=[IO.Directory]::CreateDirectory($rules)
$null=[IO.Directory]::CreateDirectory($classes)
$jar=Join-Path $directory $catalog.engine.artifact
if(!(Test-Path -LiteralPath $jar) -or (Get-FileHash -LiteralPath $jar).Hash.ToLowerInvariant() -ne $catalog.engine.sha256){Invoke-WebRequest -Uri ('https://github.com/ZUGFeRD/mustangproject/releases/download/core-2.26.0/'+$catalog.engine.artifact) -OutFile $jar}
if((Get-FileHash -LiteralPath $jar).Hash.ToLowerInvariant() -ne $catalog.engine.sha256){throw 'Exact independent engine pin mismatch.'}
$headers=@{Accept='application/vnd.github+json'}
if($env:GH_TOKEN){$headers.Authorization='Bearer '+$env:GH_TOKEN}
foreach($file in $catalog.files){
    $path=Join-Path $rules $file.name
    if(!(Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -ne $file.sha256){
        $response=Invoke-RestMethod -Uri ('https://api.github.com/repos/ZUGFeRD/mustangproject/git/blobs/'+$file.gitBlob) -Headers $headers
        if($response.sha -ne $file.gitBlob -or $response.encoding -ne 'base64' -or $response.size -ne $file.bytes){throw 'Exact standard input identity mismatch.'}
        [IO.File]::WriteAllBytes($path,[Convert]::FromBase64String($response.content))
    }
    if((Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -ne $file.sha256){throw 'Standard/schema/notice digest mismatch.'}
}
$source=Join-Path $PSScriptRoot 'InvoiceValidatorHost/InvoiceValidationHost.java'
& javac --release 17 -cp $jar -d $classes $source
if($LASTEXITCODE -ne 0){throw 'Original independent source invoice host did not compile.'}
$version=& java -version 2>&1 | Out-String
if($LASTEXITCODE -ne 0){throw 'Explicit independent source Java prerequisite failed.'}
$saxon=& java -cp $jar net.sf.saxon.Version 2>&1 | Out-String
if($LASTEXITCODE -ne 0 -or !$saxon.Contains('SAXON-J-HE 12.10')){throw 'Exact pinned Saxon analysis dependency identity differs.'}
$paths=@(Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object Name -ne 'manifest.json' | Sort-Object FullName | ForEach-Object {@{path=[IO.Path]::GetRelativePath($directory,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}})
@{purpose='explicit-independent-exact-edition-invoice-source-host';sourceCommit=(& git -C $root rev-parse HEAD).Trim();workingTreeDirty=[bool](& git -C $root status --porcelain);hostSourceSha256=(Get-FileHash -LiteralPath $source).Hash.ToLowerInvariant();catalogSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'format-checks/invoice-107.json')).Hash.ToLowerInvariant();ruleBundleSha256=$catalog.ruleBundleSha256;javaRuntime=$version;javaExecutableSha256=(Get-FileHash -LiteralPath (Get-Command java -CommandType Application).Source).Hash.ToLowerInvariant();saxonRuntime=$saxon;files=$paths;productionQualified=$false;runtimeBinaryDiscoveryBySdk=$false} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $directory 'manifest.json') -Encoding utf8NoBOM
$reports=Join-Path $root 'artifacts/contracts/invoice-validation'
$null=[IO.Directory]::CreateDirectory($reports)
Copy-Item -LiteralPath (Join-Path $directory 'manifest.json') -Destination (Join-Path $reports 'engine.json')
Write-Output 'Exact Factur-X1.0.07 MINIMUM independent source assets/host prepared; production qualification remains pending.'
