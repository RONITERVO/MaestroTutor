# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param([Parameter(Mandatory)][xml]$Manifest)
$ErrorActionPreference = 'Stop'
$android = 'http://schemas.android.com/apk/res/android'
$devices = @($Manifest.SelectNodes('/manifest/application/meta-data') | Where-Object { $_.GetAttribute('name', $android) -eq 'com.oculus.supportedDevices' })
# Inspect the final merged APK, after both OpenXR and Meta SDK manifest writers.
# Canonical public identifiers: https://developers.meta.com/vr/resources/publish-mobile-manifest/
# This release has hardware evidence for Quest 3 only. Do not inherit SDK defaults.
if ($devices.Count -ne 1 -or $devices[0].GetAttribute('value', $android) -cne 'quest3') {
    throw 'Quest release must declare exactly quest3 as its supported device. Review device QA before broadening this policy.'
}
