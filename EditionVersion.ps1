# Works out the version for this edited Helios build automatically.
#
# The version is the official Helios release this source is based on, with ".5" added:
# based on 1.6.6200.7  ->  shown as 1.6.6200.7.5
#
# Windows only allows four numbers in a version, so internally (program files, installer)
# the last two are combined as revision * 10 + 5:  1.6.6200.7.5  ->  1.6.6200.75
# That keeps the order right: 1.6.6200.7.5 < 1.6.6200.8.5 (75 < 85) < 1.6.6200.10.5 (105).
#
# Output (one line, used by build.cmd):  <internal version> <displayed version>
#   e.g.  1.6.6200.75 1.6.6200.7.5

param(
    # the number added to the end of the official version
    [ValidateRange(0, 9)]
    [int]$Suffix = 5
)

$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

function Get-BaseTag {
    # nearest official release tag (four numbers, e.g. 1.6.6200.7) in this source's history
    $tag = git describe --tags --abbrev=0 --match "[0-9]*.[0-9]*.[0-9]*.[0-9]*" 2>$null
    if ($LASTEXITCODE -eq 0) { return "$tag".Trim() }
    return $null
}

$base = Get-BaseTag
if (-not $base) {
    # a fork or shallow copy may not have the official release tags yet; get them from the official repository
    [Console]::Error.WriteLine("Fetching release tags from the official Helios repository...")
    git fetch --quiet --tags https://github.com/HeliosVirtualCockpit/Helios.git 2>$null | Out-Null
    $base = Get-BaseTag
}
if (-not $base -or $base -notmatch '^(\d+)\.(\d+)\.(\d+)\.(\d+)$') {
    throw "Could not find the official Helios version this source is based on (found '$base')."
}

$major = [int]$Matches[1]; $minor = [int]$Matches[2]; $build = [int]$Matches[3]; $revision = [int]$Matches[4]
$internalRevision = $revision * 10 + $Suffix
if ($internalRevision -gt 65535) { throw "Version $base.$Suffix is too large to encode." }

Write-Output "$major.$minor.$build.$internalRevision $base.$Suffix"
