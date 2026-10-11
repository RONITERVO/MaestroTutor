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
