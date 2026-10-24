[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$workflow = Join-Path $root '.github/workflows/publish.yml'
$text = Get-Content -LiteralPath $workflow -Raw
$match = [regex]::Match($text, '(?ms)^      - name: Push qualified package and symbol bytes\r?\n.*?^        run: \|\r?\n(?<script>(?:          [^\r\n]*(?:\r?\n|$))+)')
if (!$match.Success -or !$text.Contains('NUGET_API_KEY: ${{ steps.login.outputs.NUGET_API_KEY }}')) { throw 'Cannot locate exact OIDC push step.' }
$body = [regex]::Replace($match.Groups['script'].Value, '(?m)^          ', '')
$push = [scriptblock]::Create($body)
$directory = Join-Path $root ('artifacts/contracts/publish-token/' + [Guid]::NewGuid().ToString('N'))
$packages = Join-Path $directory 'qualified/packages'
$null = [IO.Directory]::CreateDirectory($packages)
# Deliberately invalid command-argument fixture, never an SDK package or candidate.
[IO.File]::WriteAllText((Join-Path $packages 'argument-fixture.nupkg'), 'not-a-package;offline-mock-only')
$script:calls = [System.Collections.Generic.List[object]]::new()
$script:stubExit = 0
function dotnet {
    $script:calls.Add(@($args))
    $global:LASTEXITCODE = $script:stubExit
}
if ((Get-Command dotnet).CommandType -ne 'Function') { throw 'Offline dotnet stub is mandatory.' }
$previous = $env:NUGET_API_KEY
Push-Location $directory
try {
    $env:NUGET_API_KEY = ''
    $rejected = $false
    try { & $push } catch { $rejected = $_.Exception.Message -like '*Missing short-lived OIDC*' }
    if (!$rejected -or $script:calls.Count -ne 0) { throw 'Missing token did not reject before command invocation.' }
    $env:NUGET_API_KEY = 'offline-test-token-not-a-credential'
    & $push
    if ($script:calls.Count -ne 1) { throw 'Expected one intercepted push.' }
    $arguments = $script:calls[0]
    if ($arguments.Count -ne 7 -or $arguments[0] -cne 'nuget' -or $arguments[1] -cne 'push' -or $arguments[3] -cne '--api-key' -or $arguments[4] -cne $env:NUGET_API_KEY -or $arguments[5] -cne '--source' -or $arguments[6] -cne 'https://api.nuget.org/v3/index.json') { throw 'Short-lived token was not passed to the exact push command.' }
    if ([IO.Path]::GetFullPath($arguments[2]) -cne [IO.Path]::GetFullPath((Join-Path $packages 'argument-fixture.nupkg'))) { throw 'Wrong package path.' }
    $script:stubExit = 17
    $rejected = $false
    try { & $push } catch { $rejected = $_.Exception.Message -like '*Package publication failed*' }
    if (!$rejected -or $script:calls.Count -ne 2) { throw 'Push failure did not propagate.' }
} finally {
    $env:NUGET_API_KEY = $previous
    Pop-Location
}
[ordered]@{schemaVersion=1;purpose='offline-publish-argument-drill';sourceCommit=(& git -C $root rev-parse HEAD).Trim();workflowSha256=(Get-FileHash -LiteralPath $workflow).Hash;checks=3;passed=3;failed=0;networkRequests=0;oidcLogins=0;packagesPublished=0;qualified=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'report.json') -Encoding utf8NoBOM
Write-Output "Offline publishing-token drill passed; no dotnet CLI, OIDC login or network request executed. Report: $directory/report.json"
