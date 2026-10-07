param(
    [Parameter(Mandatory = $true)][string]$Group,
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$assetRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Assets')) + [IO.Path]::DirectorySeparatorChar
$manifestPath = Join-Path $projectRoot 'Docs/Art/Previews/Diorama/CleanupManifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$entry = @($manifest | Where-Object { $_.name -eq $Group })
if ($entry.Count -ne 1) { throw "Unknown or duplicate cleanup group: $Group" }
$entry = $entry[0]
if (@($entry.external.PSObject.Properties).Count -ne 0 -or @($entry.literals.PSObject.Properties).Count -ne 0) {
    throw "Group still has external GUID or script references: $Group"
}
$targets = @()
foreach ($relative in $entry.paths) {
    foreach ($suffix in @('', '.meta')) {
        $absolute = [IO.Path]::GetFullPath((Join-Path $projectRoot ($relative + $suffix)))
        if (-not $absolute.StartsWith($assetRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Cleanup path escapes Assets: $absolute"
        }
        if (Test-Path -LiteralPath $absolute) {
            $item = Get-Item -LiteralPath $absolute -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Cleanup cannot follow a junction or symlink: $absolute"
            }
            $targets += $absolute
        }
    }
}
if ($targets.Count -eq 0) { Write-Output "Already removed: $Group"; return }
$files = @($targets | ForEach-Object { Get-ChildItem -LiteralPath $_ -Recurse -File -Force })
$sourceBytes = ($files | Measure-Object -Property Length -Sum).Sum
Write-Output "$Group : $($targets.Count) validated targets, $($files.Count) files, $sourceBytes bytes"
if (-not $Apply) { $targets; return }
# All targets have been normalized and checked before the first recursive removal.
foreach ($absolute in $targets) { Remove-Item -LiteralPath $absolute -Recurse -Force }
$ledger = Join-Path $projectRoot 'Docs/Art/Previews/Diorama/CleanupCompleted.csv'
[PSCustomObject]@{
    group = $Group
    utc = [DateTime]::UtcNow.ToString('o')
    files = $files.Count
    sourceBytes = $sourceBytes
    paths = ($entry.paths -join '|')
} | Export-Csv -LiteralPath $ledger -Append -NoTypeInformation -Encoding UTF8
