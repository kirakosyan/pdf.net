[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$directory=Join-Path $root 'artifacts/archival-fixtures'
$null=[IO.Directory]::CreateDirectory($directory)
$jar=Join-Path $directory 'greenfield-apps-1.28.2.jar'
$expected='687d4d8bcfec48c9f7e931cf78ed061f1eeccfba55d4cdc000ba9b4d8aa46ce6'
if(!(Test-Path -LiteralPath $jar) -or (Get-FileHash -LiteralPath $jar -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected){
    Invoke-WebRequest -Uri 'https://repo.maven.apache.org/maven2/org/verapdf/apps/greenfield-apps/1.28.2/greenfield-apps-1.28.2.jar' -OutFile $jar
}
if((Get-FileHash -LiteralPath $jar -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected){throw 'Independent validator fixture digest mismatch.'}
$null=Get-Command java -ErrorAction Stop
$javaVersion=& java -version 2>&1 | Out-String
if($LASTEXITCODE -ne 0){throw 'An explicit Java runtime is required for independent archival source fixtures.'}
$reports=Join-Path $root 'artifacts/contracts/archival'
$null=[IO.Directory]::CreateDirectory($reports)
[ordered]@{purpose='independent-source-validation-not-production-qualification';engine='veraPDF';version='1.28.2';jarSha256=$expected;javaRuntime=$javaVersion;limitations=@('Caller-supplied test host; supervised deadlines and shared budgets do not qualify OS isolation, minimum runtimes, complete corpus or final packages')} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $reports 'engine.json') -Encoding utf8
