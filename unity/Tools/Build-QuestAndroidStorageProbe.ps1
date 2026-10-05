# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Editor,[Parameter(Mandatory)][string]$BuildMirror,[Parameter(Mandatory)][string]$AndroidSdk,[Parameter(Mandatory)][string]$AndroidJdk)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'QuestBuildProcesses.ps1')
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));$source=Join-Path $repoRoot 'unity/MaestroQuest'
$mirror=(Resolve-Path -LiteralPath $BuildMirror).Path;$editorPath=(Resolve-Path -LiteralPath $Editor).Path
$owner=Get-Content -LiteralPath (Join-Path $mirror '.maestro-build-mirror.json') -Raw | ConvertFrom-Json
if($owner.source -ne $source -or $mirror.StartsWith($repoRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Use a separate receipt-owned mirror, already prepared by Verify-Quest.'}
foreach($running in Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"){if($running.CommandLine -and $running.CommandLine.Contains($mirror)){throw 'Mirror Editor already running.'}}
$runtime=Join-Path $source 'Assets/Maestro/Runtime';$inventory=[ordered]@{}
foreach($file in Get-ChildItem -LiteralPath $runtime -Recurse -File | Sort-Object FullName){
 $relative=[IO.Path]::GetRelativePath($source,$file.FullName);$hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
 if(!(Test-Path -LiteralPath (Join-Path $mirror $relative)) -or (Get-FileHash -LiteralPath (Join-Path $mirror $relative) -Algorithm SHA256).Hash -ne $hash){throw "Stale runtime: $relative. Run Verify-Quest."}
 $inventory[$relative]=$hash
}
$id=[Guid]::NewGuid().ToString('N');$evidence=Join-Path $repoRoot ".quest-evidence/android-storage-build/$id"
New-Item -ItemType Directory -Path $evidence | Out-Null
$inventory | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $evidence 'runtime-sources.json') -Encoding utf8
$templates=@{'QuestAndroidStorageProbe.cs'='Assets/Maestro/Runtime/Persistence/QuestAndroidStorageProbe.cs';'QuestAndroidStorageProbeBuild.cs'='Assets/Maestro/Editor/QuestAndroidStorageProbeBuild.cs'}
$created=@();$backups=@{}
foreach($folder in @('ProjectSettings','Assets/XR/Settings')){
 foreach($file in Get-ChildItem -LiteralPath (Join-Path $mirror $folder) -Recurse -File){
  $relative=[IO.Path]::GetRelativePath($mirror,$file.FullName);$backup=Join-Path $evidence ('mirror-before/'+$relative)
  New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null;Copy-Item -LiteralPath $file.FullName -Destination $backup
  $backups[$file.FullName]=$backup
 }
}
$apk=Join-Path $evidence 'MaestroStorageProbe.apk';$log=Join-Path $evidence 'build.log';$process=$null
try{
 foreach($item in $templates.GetEnumerator()){
  $target=Join-Path $mirror $item.Value;if(Test-Path -LiteralPath $target){throw 'A previous probe source remains; inspect it before retrying.'}
  Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('StorageProbe/'+$item.Key)) -Destination $target;$created+=@($target,($target+'.meta'))
 }
 $scene=Join-Path $mirror 'Assets/Maestro/StorageProbeScene.unity';if(Test-Path -LiteralPath $scene){throw 'A previous probe scene remains.'};$created+=@($scene,($scene+'.meta'))
 $childEnv=@{ADB_SERVER_SOCKET='tcp:localhost:5041';MAESTRO_ANDROID_STORAGE_PROBE='1';MAESTRO_ANDROID_SDK=(Resolve-Path -LiteralPath $AndroidSdk).Path;MAESTRO_ANDROID_NDK=(Resolve-Path -LiteralPath (Join-Path $AndroidSdk 'ndk/27.2.12479018')).Path;MAESTRO_ANDROID_JDK=(Resolve-Path -LiteralPath $AndroidJdk).Path;MAESTRO_QUEST_APK=$apk;MAESTRO_QUEST_RELEASE_PROFILE='';MAESTRO_QUEST_KEYSTORE='';MAESTRO_QUEST_KEY_ALIAS='';MAESTRO_QUEST_STORE_PASSWORD='';MAESTRO_QUEST_KEY_PASSWORD=''}
 Stop-QuestBuildHelper
 $process=Start-Process -FilePath $editorPath -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-force-d3d11','-buildTarget','Android','-projectPath',('"'+$mirror+'"'),'-executeMethod','Maestro.Quest.Editor.QuestAndroidStorageProbeBuild.Build','-logFile',('"'+$log+'"')) -Environment $childEnv
 Write-Output "Storage probe build PID $($process.Id), evidence $evidence"
 $deadline=[DateTime]::UtcNow.AddMinutes(30);$shutdownAt=$null
 while(!$process.WaitForExit(1000)){
  if(!$shutdownAt -and (Test-Path -LiteralPath $log) -and (Select-String -LiteralPath $log -Pattern 'Batchmode quit successfully invoked|Exiting batchmode' -Quiet)){$shutdownAt=[DateTime]::UtcNow}
  if($shutdownAt -and [DateTime]::UtcNow -gt $shutdownAt.AddSeconds(10)){Stop-QuestBuildHelper}
  if([DateTime]::UtcNow -gt $deadline){throw "Probe build deadline exceeded; evidence: $evidence"}
 }
 if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $apk)){throw "Probe build failed; see $log"}
 $report=Get-Content -LiteralPath ($apk+'.build.json') -Raw | ConvertFrom-Json
 if($report.result -ne 'Succeeded' -or $report.package -ne 'com.maestro.quest.storageprobe' -or $report.errors -ne 0){throw 'Invalid diagnostic build report.'}
 foreach($item in $inventory.GetEnumerator()){if((Get-FileHash -LiteralPath (Join-Path $mirror $item.Key) -Algorithm SHA256).Hash -ne $item.Value -or (Get-FileHash -LiteralPath (Join-Path $source $item.Key) -Algorithm SHA256).Hash -ne $item.Value){throw 'Production runtime changed during packaging.'}}
 $templateHashes=[ordered]@{};foreach($item in $templates.GetEnumerator()){$templateHashes[$item.Key]=(Get-FileHash -LiteralPath (Join-Path $mirror $item.Value) -Algorithm SHA256).Hash}
 @{version=1;id=$id;package=$report.package;apk=$apk;sha256=(Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash;runtimeFiles=$inventory.Count;templates=$templateHashes;installed=$false;productionApp=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'build-receipt.json') -Encoding utf8
 Write-Output "Diagnostic APK ready for manifest/signature audit: $apk"
}finally{
 if($process){if(!$process.HasExited){$process.Kill();$process.WaitForExit()};$process.Dispose()};Stop-QuestBuildHelper
 foreach($target in $created){$resolved=[IO.Path]::GetFullPath($target);if(!$resolved.StartsWith($mirror+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected cleanup path.'};if(Test-Path -LiteralPath $resolved){Remove-Item -LiteralPath $resolved}}
 foreach($item in $backups.GetEnumerator()){Copy-Item -LiteralPath $item.Value -Destination $item.Key -Force}
}
