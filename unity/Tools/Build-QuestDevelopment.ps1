# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Editor,
    [Parameter(Mandatory)][string]$BuildMirror,
    [Parameter(Mandatory)][string]$AndroidSdk,
    [Parameter(Mandatory)][string]$AndroidJdk,
    [switch]$UpdateBehaviourCatalog,
    [switch]$RenderImports,
    [switch]$RenderRecipes,
    [switch]$RenderRules
)
$ErrorActionPreference = 'Stop'
try { & (Join-Path $PSScriptRoot 'Build-QuestPackage.ps1') @PSBoundParameters -Channel Development; $global:LASTEXITCODE = 0 }
catch { $global:LASTEXITCODE = 1; throw }
