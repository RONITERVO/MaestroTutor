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
if($permissions | Where-Object {$_ -in @('android.permission.INTERNET','android.permission.RECORD_AUDIO','android.permission.CAMERA','com.oculus.permission.USE_SCENE','com.oculus.permission.USE_ANCHOR_API','com.oculus.permission.HAND_TRACKING')}){throw 'Diagnostic package has a forbidden permission.'}
$categories=@($manifest.SelectNodes('//category') | ForEach-Object {$_.GetAttribute('name',$android)})
$features=@($manifest.SelectNodes('/manifest/uses-feature') | Where-Object {$_.GetAttribute('required',$android) -ne 'false'} | ForEach-Object {$_.GetAttribute('name',$android)})
if(($categories | Where-Object {$_ -match '(?i)vr'}) -or ($features | Where-Object {$_ -match '(?i)vr|oculus|meta'})){throw 'Diagnostic package must not require VR tracking.'}
# Android's package step removes unneeded ELF symbols. Verify that exact
# deterministic transform of the receipt-owned library, not an arbitrary APK hash.
$native=Join-Path $root 'libmaestro_storage_fault.so'
if((Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash -ne $record.nativeSha256){throw 'Native hook changed since packaging.'}
$stripped=Join-Path $root 'native-audit.so'
& (Join-Path $AndroidSdk 'ndk/27.2.12479018/toolchains/llvm/prebuilt/windows-x86_64/bin/llvm-strip.exe') --strip-unneeded -o $stripped $native
if($LASTEXITCODE){throw 'Native hook normalization failed.'}
$nativeHash=(Get-FileHash -LiteralPath $stripped -Algorithm SHA256).Hash
$archive=[IO.Compression.ZipFile]::OpenRead($apk)
try{
 foreach($item in @(@('lib/arm64-v8a/libmaestro_storage_fault.so',$nativeHash),@('lib/arm64-v8a/wrap.sh',$record.wrapperSha256))){
  $entry=$archive.GetEntry($item[0]);if(!$entry -or !$item[1]){throw 'Missing native fault packaging receipt.'}
  $stream=$entry.Open();try{$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
  if($hash -ne $item[1]){throw 'Packaged fault hook differs from its receipt.'}
 }
 $abis=@($archive.Entries | Where-Object {$_.FullName -match '^lib/[^/]+/'} | ForEach-Object {$_.FullName.Split('/')[1]} | Sort-Object -Unique)}finally{$archive.Dispose()}
if($abis.Count -ne 1 -or $abis[0] -ne 'arm64-v8a'){throw 'Expected only ARM64 libraries.'}
@{version=1;apk=$apk;sha256=$record.sha256;package=$record.package;debuggable=$true;signatureVerified=$true;noVrCategory=$true;noNetworkPermission=$true;arm64Only=$true;nativeFaultHookVerified=$true;nativeSha256=$nativeHash;wrapperSha256=$record.wrapperSha256;permissions=$permissions;runtimeFiles=$record.runtimeFiles;receipt=$receiptPath} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'audit.json') -Encoding utf8
Write-Output "Audited diagnostic APK: $apk"
