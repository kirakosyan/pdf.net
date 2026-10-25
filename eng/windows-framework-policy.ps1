# Pure classification of a live Framework release key; it never edits the registry.
function Get-FluentPdfFrameworkHostLanes {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int]$Release, [Parameter(Mandatory)][string]$OSArchitecture)
    if ($OSArchitecture -ine 'X64') { return @() }
    if ($Release -in @(461808,461814)) { return @('win-x86-framework472','win-x64-framework472') }
    if ($Release -ge 528040) { return @('win-x86-framework-current','win-x64-framework-current') }
    return @()
}
