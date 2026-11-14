# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$Editor,
 [Parameter(Mandatory)][string]$BuildMirror
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'QuestBuildProcesses.ps1')
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=Join-Path $repoRoot 'unity/MaestroQuest'
$mirror=(Resolve-Path -LiteralPath $BuildMirror).Path
$editorPath=(Resolve-Path -LiteralPath $Editor).Path
$owner=Get-Content -LiteralPath (Join-Path $mirror '.maestro-build-mirror.json') -Raw | ConvertFrom-Json
if($owner.source -ne $source -or $mirror.StartsWith($repoRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Use a separate receipt-owned build mirror. Run Verify-Quest first.'}
foreach($running in Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"){
 if($running.CommandLine -and $running.CommandLine.Contains($mirror)){throw 'The mirror already has a running Editor.'}
}
function Source-Inventory {
 $inventory=[ordered]@{}
 $sourceFiles=@(Get-ChildItem -LiteralPath (Join-Path $source 'Assets/Maestro') -Recurse -File | Where-Object {$_.Name.EndsWith('.cs') -or $_.Name.EndsWith('.cs.meta') -or $_.Name.EndsWith('.asmdef')})
 $mirrorFiles=@(Get-ChildItem -LiteralPath (Join-Path $mirror 'Assets/Maestro') -Recurse -File | Where-Object {$_.Name.EndsWith('.cs') -or $_.Name.EndsWith('.cs.meta') -or $_.Name.EndsWith('.asmdef')})
 if($sourceFiles.Count -ne $mirrorFiles.Count){throw 'The mirror source inventory is stale. Run Verify-Quest.'}
 foreach($file in $sourceFiles | Sort-Object FullName){
  $relative=[IO.Path]::GetRelativePath($source,$file.FullName);$copy=Join-Path $mirror $relative;$hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
  if(!(Test-Path -LiteralPath $copy) -or (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $hash){throw "Mirror is stale: $relative. Run Verify-Quest."}
  $inventory[$relative]=$hash
 }
 return $inventory
}
$inventory=Source-Inventory
$runId=[Guid]::NewGuid().ToString('N');$runRoot=Join-Path $repoRoot ".quest-evidence/storage-crash/$runId"
New-Item -ItemType Directory -Path $runRoot | Out-Null
$inventory | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runRoot 'source-inventory.json') -Encoding utf8
$cases=@()
foreach($stage in @('before-journal','prepared','room','memory','committed','room-backup','memory-backup')){$cases+=@{stage=$stage;firstSave=$false;recoveryStop=$null}}
foreach($stage in @('prepared','room','memory','committed')){$cases+=@{stage=$stage;firstSave=$true;recoveryStop=$null}}
foreach($stage in @('room','committed')){$cases+=@{stage=$stage;firstSave=$false;recoveryStop='recovered-room'}}
$verified=@()
function Assert-Intermediate([string]$directory,$ready,[string]$mode){
 $expected=Get-Content -LiteralPath (Join-Path $directory 'expected.json') -Raw | ConvertFrom-Json
 if($mode -eq 'write'){
  $room=$(if($ready.stage -in @('before-journal','prepared')){$expected.before.room}else{$expected.after.room})
  $memory=$(if($ready.stage -in @('before-journal','prepared','room')){$expected.before.memory}else{$expected.after.memory})
 }else{$room=$expected.expected.room;$memory=$expected.expected.memory}
 if($ready.primaryRoom -cne $room -or $ready.primaryMemory -cne $memory){throw 'The writer did not reach the expected intermediate pair.'}
 if($ready.stage -eq 'before-journal'){
  if($null -ne $ready.journal){throw 'Unexpected journal before preparation.'}
 }else{
  $journal=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($ready.journal)) | ConvertFrom-Json
  $phase=$(if($expected.committed){'committed'}else{'prepared'})
  if($journal.phase -cne $phase){throw 'Unexpected durable journal phase.'}
 }
}
function Run-Child([string]$directory,[string]$id,[string]$mode,[string]$marker,[string]$point){
 Stop-QuestBuildHelper
 $log=Join-Path $directory ($mode+'.log')
 $process=Start-Process -FilePath $editorPath -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-force-d3d11','-buildTarget','Win64','-projectPath',('"'+$mirror+'"'),'-executeMethod','Maestro.Quest.Editor.QuestStorageCrashProbe.Run','-logFile',('"'+$log+'"')) -Environment @{ADB_SERVER_SOCKET='tcp:localhost:5041';MAESTRO_STORAGE_PROBE_DIRECTORY=$directory;MAESTRO_STORAGE_PROBE_MODE=$mode;MAESTRO_QUEST_RELEASE_PROFILE='';MAESTRO_QUEST_KEYSTORE='';MAESTRO_QUEST_KEY_ALIAS='';MAESTRO_QUEST_STORE_PASSWORD='';MAESTRO_QUEST_KEY_PASSWORD=''}
 try {
  $deadline=[DateTime]::UtcNow.AddMinutes(4);$readyPath=Join-Path $directory $marker
  while(!$process.WaitForExit(200)){
   if(Test-Path -LiteralPath $readyPath){break}
   if([DateTime]::UtcNow -gt $deadline){throw "Crash probe did not reach $mode. Evidence: $directory"}
  }
  if(!(Test-Path -LiteralPath $readyPath)){throw "Crash probe exited before $marker. Evidence: $directory"}
  $ready=Get-Content -LiteralPath $readyPath -Raw | ConvertFrom-Json
  if($ready.id -ne $id -or $ready.version -ne 1){throw 'Wrong process evidence identity.'}
  if($mode -eq 'verify'){
   # Verification must also exit normally; a written receipt alone is insufficient.
   $deadline=[DateTime]::UtcNow.AddSeconds(60);$cleanupAt=[DateTime]::UtcNow.AddSeconds(10)
   while(!$process.WaitForExit(500)){
    if([DateTime]::UtcNow -gt $cleanupAt){Stop-QuestBuildHelper;$cleanupAt=[DateTime]::MaxValue}
    if([DateTime]::UtcNow -gt $deadline){throw "Recovery verifier did not exit. Evidence: $directory"}
   }
   if($process.ExitCode -ne 0){throw "Recovery verifier failed. Evidence: $directory"}
  } else {
   if($process.HasExited -or $ready.pid -ne $process.Id -or $ready.startedUtcTicks -ne $process.StartTime.ToUniversalTime().Ticks -or $ready.stage -ne $point){throw 'Refusing to kill an unverified process.'}
   Assert-Intermediate $directory $ready $mode
   $process.Kill();$process.WaitForExit()
   $name=$(if($mode -eq 'write'){'writer-stopped.json'}else{'recovery-stopped.json'})
   @{version=1;id=$id;pid=$process.Id;stage=$point;forced=$true;exitCode=$process.ExitCode;at=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory $name) -Encoding utf8
  }
 }finally{
  # Only this owned child can be terminated, including cleanup after a failed test.
  if(!$process.HasExited){$process.Kill();$process.WaitForExit()}
  $process.Dispose();Stop-QuestBuildHelper
 }
}
foreach($case in $cases){
 $id=[Guid]::NewGuid().ToString('N');$directory=Join-Path $runRoot $id
 New-Item -ItemType Directory -Path $directory | Out-Null
 @{version=1;id=$id;mirror=$mirror;stage=$case.stage;firstSave=$case.firstSave;recoveryStop=$case.recoveryStop} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'owner.json') -Encoding utf8
 Write-Output "Crash probe: $($case.stage), first save=$($case.firstSave), recovery stop=$($case.recoveryStop)"
 Run-Child $directory $id 'write' 'writer-ready.json' $case.stage
 if($case.recoveryStop){Run-Child $directory $id 'recoverStop' 'recovery-ready.json' $case.recoveryStop}
 Run-Child $directory $id 'verify' 'verified.json' ''
 $result=Get-Content -LiteralPath (Join-Path $directory 'verified.json') -Raw | ConvertFrom-Json
 if(!$result.backupsMatched -or !$result.idempotent -or $result.headset -or $result.powerLoss){throw 'Invalid verification boundary.'}
 $verified+=@{id=$id;directory=$directory;result=$result}
 Write-Output "Crash recovery passed: $($case.stage), first save=$($case.firstSave), recovery stop=$($case.recoveryStop)"
}
$after=Source-Inventory
if(($inventory | ConvertTo-Json -Compress) -cne ($after | ConvertTo-Json -Compress)){throw 'Sources changed during the crash probe.'}
@{version=1;id=$runId;headset=$false;powerLoss=$false;cases=$verified;sourceFiles=$inventory.Count;directory=$runRoot} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $runRoot 'verified.json') -Encoding utf8
Write-Output "All $($verified.Count) process-termination cases passed. Evidence: $runRoot"
