# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Receipt,[Parameter(Mandatory)][string]$AndroidSdk,[Parameter(Mandatory)][string]$AndroidJdk)
$ErrorActionPreference='Stop'
$receiptPath=(Resolve-Path -LiteralPath $Receipt).Path;$root=Split-Path -Parent $receiptPath
$record=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
if($record.package -ne 'com.maestro.quest.storageprobe' -or $record.productionApp){throw 'Wrong diagnostic package receipt.'}
$apk=(Resolve-Path -LiteralPath $record.apk).Path
if((Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash -ne $record.sha256){throw 'APK changed since packaging.'}
$previousJava=[Environment]::GetEnvironmentVariable('JAVA_HOME')
try{
 $env:JAVA_HOME=(Resolve-Path -LiteralPath $AndroidJdk).Path
 $manifestText=& (Join-Path $AndroidSdk 'cmdline-tools/16.0/bin/apkanalyzer.bat') manifest print $apk
 if($LASTEXITCODE){throw 'Manifest inspection failed.'}
 $signature=& (Join-Path $AndroidSdk 'build-tools/35.0.1/apksigner.bat') verify --verbose --print-certs $apk
 if($LASTEXITCODE){throw 'Signature verification failed.'}
}finally{[Environment]::SetEnvironmentVariable('JAVA_HOME',$previousJava)}
$manifestText | Set-Content -LiteralPath (Join-Path $root 'manifest.xml') -Encoding utf8
$signature | Set-Content -LiteralPath (Join-Path $root 'signature.txt') -Encoding utf8
[xml]$manifest=$manifestText -join "`n";$android='http://schemas.android.com/apk/res/android'
if($manifest.manifest.package -ne 'com.maestro.quest.storageprobe' -or $manifest.manifest.application.GetAttribute('debuggable',$android) -ne 'true'){throw 'Wrong manifest identity or debug status.'}
$permissions=@($manifest.SelectNodes('/manifest/uses-permission') | ForEach-Object {$_.GetAttribute('name',$android)})
if($permissions -contains 'android.permission.INTERNET'){throw 'Diagnostic package must not have network permission.'}
$categories=@($manifest.SelectNodes('//category') | ForEach-Object {$_.GetAttribute('name',$android)})
$features=@($manifest.SelectNodes('/manifest/uses-feature') | Where-Object {$_.GetAttribute('required',$android) -ne 'false'} | ForEach-Object {$_.GetAttribute('name',$android)})
if(($categories | Where-Object {$_ -match '(?i)vr'}) -or ($features | Where-Object {$_ -match '(?i)vr|oculus|meta'})){throw 'Diagnostic package must not require VR tracking.'}
$archive=[IO.Compression.ZipFile]::OpenRead($apk)
try{$abis=@($archive.Entries | Where-Object {$_.FullName -match '^lib/[^/]+/'} | ForEach-Object {$_.FullName.Split('/')[1]} | Sort-Object -Unique)}finally{$archive.Dispose()}
if($abis.Count -ne 1 -or $abis[0] -ne 'arm64-v8a'){throw 'Expected only ARM64 libraries.'}
@{version=1;apk=$apk;sha256=$record.sha256;package=$record.package;debuggable=$true;signatureVerified=$true;noVrCategory=$true;noNetworkPermission=$true;arm64Only=$true;permissions=$permissions;runtimeFiles=$record.runtimeFiles;receipt=$receiptPath} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'audit.json') -Encoding utf8
Write-Output "Audited diagnostic APK: $apk"
