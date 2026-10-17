# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference = 'Stop'
$prefix = '<manifest xmlns:a="http://schemas.android.com/apk/res/android"><application>'
$suffix = '</application></manifest>'
$valid = '<meta-data a:name="com.oculus.supportedDevices" a:value="quest3" />'
& (Join-Path $PSScriptRoot 'Assert-QuestReleaseManifest.ps1') -Manifest ([xml]($prefix + $valid + $suffix))
$invalid = @(
    '',
    '<meta-data a:name="other" a:value="quest3" />',
    '<meta-data a:name="com.oculus.supportedDevices" a:value="quest2|questpro|quest3|quest3s|stanley" />',
    '<meta-data a:name="com.oculus.supportedDevices" a:value="eureka" />',
    '<meta-data a:name="com.oculus.supportedDevices" a:value="quest3|quest3s" />',
    '<meta-data a:name="com.oculus.supportedDevices" a:value="quest3|vrglasses" />',
    '<meta-data a:name="com.oculus.supportedDevices" a:value="Quest3" />',
    ($valid + $valid)
)
foreach ($entry in $invalid) {
    $rejected = $false
    try { & (Join-Path $PSScriptRoot 'Assert-QuestReleaseManifest.ps1') -Manifest ([xml]($prefix + $entry + $suffix)) }
    catch { if ($_.Exception.Message -notlike 'Quest release must declare exactly quest3*') { throw }; $rejected = $true }
    if (!$rejected) { throw 'Invalid supported-device declaration was accepted.' }
}
Write-Output 'Quest release manifest: valid Quest 3 accepted; eight invalid/missing/duplicate device declarations refused.'
