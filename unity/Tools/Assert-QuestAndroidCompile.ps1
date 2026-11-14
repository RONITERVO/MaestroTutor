# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$AndroidNdk,
    [Parameter(Mandatory)][string[]]$Tasks
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath $Project).Path
$builds = Split-Path -Parent $projectRoot
$mirror = Split-Path -Parent $builds
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ((Split-Path -Leaf $builds) -ne 'Builds' -or !(Test-Path -LiteralPath (Join-Path $mirror '.maestro-build-mirror.json'))) { throw 'Use an exported project in an owned build mirror.' }
$owner = Get-Content -LiteralPath (Join-Path $mirror '.maestro-build-mirror.json') -Raw | ConvertFrom-Json
if ([IO.Path]::GetFullPath($owner.source) -ne [IO.Path]::GetFullPath((Join-Path $repo 'unity/MaestroQuest'))) { throw 'Build mirror owner mismatch.' }
$report = Get-Content -LiteralPath ([IO.Path]::ChangeExtension($projectRoot, '.build.json')) -Raw | ConvertFrom-Json
if ($report.result -ne 'Succeeded' -or !$report.exportOnly -or !$report.developmentOnly -or $report.package -ne 'com.maestro.quest.development') { throw 'Expected a successful development-only export.' }
if (@(Get-ChildItem -LiteralPath $projectRoot -Recurse -File | Where-Object { $_.Extension -in @('.apk','.aab') }).Count) { throw 'Compile-only output contains an installable package.' }
# AGP keeps dependency ABIs in this intermediate and filters them during APK
# packaging. Verify the explicit selection and selected binaries; record unused
# dependencies without claiming that a final package has been filtered.
foreach ($module in @('launcher','unityLibrary')) {
    $config = Get-Content -LiteralPath (Join-Path $projectRoot "$module/build.gradle") -Raw
    $selection = [regex]::Matches($config, '(?m)^\s*abiFilters\s+"([^"]+)"\s*$')
    if ($selection.Count -ne 1 -or $selection[0].Groups[1].Value -ne 'arm64-v8a') { throw 'Expected an explicit ARM64-only module configuration.' }
}
$properties = Get-Content -LiteralPath (Join-Path $projectRoot 'gradle.properties') -Raw
if ([regex]::Matches($properties, '(?m)^unity\.abiFilters=arm64-v8a\s*$').Count -ne 1) { throw 'Unexpected Unity ABI selection.' }
$merged = Join-Path $projectRoot 'launcher/build/intermediates/merged_native_libs/release/mergeReleaseNativeLibs/out/lib'
$libraries = @(Get-ChildItem -LiteralPath $merged -Recurse -Filter '*.so' -File)
$selected = @($libraries | Where-Object { $_.Directory.Name -eq 'arm64-v8a' })
if (!$selected.Count -or !($selected.Name -contains 'libil2cpp.so') -or !($selected.Name -contains 'libunity.so')) { throw 'Compiled ARM64 player libraries are missing.' }
$readelf = Join-Path $AndroidNdk 'toolchains/llvm/prebuilt/windows-x86_64/bin/llvm-readelf.exe'
if (!(Test-Path -LiteralPath $readelf)) { throw 'Missing NDK ELF inspector.' }
$hashes = @{}; $unused = @{}; $alignments = @{}
foreach ($library in $libraries) {
    $relative = [IO.Path]::GetRelativePath($merged, $library.FullName)
    $hash = (Get-FileHash -LiteralPath $library.FullName -Algorithm SHA256).Hash
    if (!$relative.StartsWith('arm64-v8a' + [IO.Path]::DirectorySeparatorChar, [StringComparison]::Ordinal)) { $unused[$relative] = $hash; continue }
    $elf = & $readelf --file-header --program-headers --wide $library.FullName
    if ($LASTEXITCODE -ne 0) { throw "Could not inspect $relative" }
    $text = $elf -join "`n"
    if ($text -notmatch 'Class:\s+ELF64' -or $text -notmatch 'Machine:\s+AArch64' -or $text -notmatch 'Data:.*little endian') { throw "Unexpected ELF architecture: $relative" }
    $loads = @($elf | Where-Object { $_ -match '^\s*LOAD\s' })
    if (!$loads.Count) { throw "Missing ELF LOAD segments: $relative" }
    $values = @()
    foreach ($line in $loads) {
        $parts = $line.Trim() -split '\s+'
        $offset = [Convert]::ToUInt64($parts[1].Substring(2),16)
        $address = [Convert]::ToUInt64($parts[2].Substring(2),16)
        $alignment = [Convert]::ToUInt64($parts[-1].Substring(2),16)
        if ($alignment -lt 16384 -or $offset % 16384 -ne $address % 16384) { throw "ELF is not compatible with 16 KiB pages: $relative" }
        $values += $alignment
    }
    $hashes[$relative] = $hash; $alignments[$relative] = $values
}
@{ version=1; project=$projectRoot; package=$report.package; developmentOnly=$true; compileOnly=$true; releaseSigned=$false; installed=$false; selectedAbi='arm64-v8a'; nativeLibraries=$hashes; elfLoadAlignments=$alignments; unselectedDependencyLibraries=$unused; tasks=$Tasks; finalPackageFilteringVerified=$false } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $projectRoot 'maestro-compile.json')
Write-Output "Verified $($hashes.Count) selected ARM64 libraries with 16 KiB ELF LOAD alignment; $($unused.Count) unselected dependency libraries remain in the intermediate."
