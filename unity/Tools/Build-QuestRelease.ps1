# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Editor,
    [Parameter(Mandatory)][string]$BuildMirror,
    [Parameter(Mandatory)][string]$AndroidSdk,
    [Parameter(Mandatory)][string]$AndroidJdk,
    [Parameter(Mandatory)][string]$ReleaseProfile,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
try { & (Join-Path $PSScriptRoot 'Build-QuestPackage.ps1') @PSBoundParameters -Channel Release; $global:LASTEXITCODE = 0 }
catch { $global:LASTEXITCODE = 1; throw }
