# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$LogPath)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $LogPath -PathType Leaf) -or (Get-Item -LiteralPath $LogPath).Length -eq 0) {
    throw "Unity verification log is missing or empty: $LogPath"
}
# Unity can report invalid MonoBehaviour messages while still exiting zero and
# passing NUnit tests. These are engine diagnostics, not expected test assertions.
$diagnostics = @(Select-String -LiteralPath $LogPath -Pattern '^\s*Script error\b')
if ($diagnostics.Count -gt 0) {
    $lines = ($diagnostics | ForEach-Object { $_.LineNumber }) -join ', '
    throw "Unity script diagnostics found at lines $lines. See $LogPath"
}

# Invalid GUID metadata makes Unity silently omit source/assets from import, so
# even a zero exit and passing older tests cannot validate the intended build.
$ignoredAssets = @(Select-String -LiteralPath $LogPath -Pattern '^\s*The \.meta file .+ does not have a valid GUID and its corresponding Asset file will be ignored\b')
if ($ignoredAssets.Count -gt 0) {
    $lines = ($ignoredAssets | ForEach-Object { $_.LineNumber }) -join ', '
    throw "Unity ignored-asset metadata diagnostics found at lines $lines. See $LogPath"
}
