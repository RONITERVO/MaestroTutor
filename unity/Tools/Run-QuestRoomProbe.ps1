# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$Editor,
 [Parameter(Mandatory)][string]$BuildMirror,
 [string]$Prompt,
 [string]$Profile = 'quest-probe',
 [ValidateSet('ContextCreateEdit','LiveVisual','ObserverVisual','EventProgram','AvatarAnimation')][string]$ProviderScenario,
 [string]$SpeechFixture,
 [ValidateSet('Headless','Book')][string]$Journey = 'Headless'
)
$ErrorActionPreference='Stop'
if($ProviderScenario -in @('LiveVisual','ObserverVisual')){
 if([string]::IsNullOrWhiteSpace($SpeechFixture) -or !(Test-Path -LiteralPath $SpeechFixture -PathType Leaf)){throw 'Live provider scenarios require an explicit SpeechFixture JSON file.'}
 $SpeechFixture=(Resolve-Path -LiteralPath $SpeechFixture).Path
}
if($ProviderScenario){
 if($Journey -ne 'Headless' -or ![string]::IsNullOrWhiteSpace($Prompt)){throw 'ProviderScenario requires Headless and cannot be combined with Prompt.'}
 $Prompt='Please create my test object now. Use the definition I gave in the previous message.'
}
if($Journey -eq 'Book' -and ![string]::IsNullOrWhiteSpace($Prompt)){throw 'The deterministic book journey does not accept a provider prompt.'}
. (Join-Path $PSScriptRoot 'QuestBuildProcesses.ps1')
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$mirror=(Resolve-Path -LiteralPath $BuildMirror).Path
$editorPath=(Resolve-Path -LiteralPath $Editor).Path
$source=Join-Path $repoRoot 'unity/MaestroQuest'
$runner=Join-Path $repoRoot 'node_modules/.bin/tsx.cmd'
if(!(Test-Path -LiteralPath $runner)){throw 'Install the repository npm dependencies before starting a room probe.'}
$typeChecker=Join-Path $repoRoot 'node_modules/.bin/tsc.cmd'
& $typeChecker -p (Join-Path $repoRoot 'tsconfig.quest-probes.json') --pretty false
if($LASTEXITCODE -ne 0){throw 'Native integration drivers failed type checking; no Editor was started.'}
$owner=Get-Content -LiteralPath (Join-Path $mirror '.maestro-build-mirror.json') -Raw | ConvertFrom-Json
if($owner.source -ne $source){throw 'The mirror is not owned by this checkout. Run Verify-Quest first.'}
# Refuse stale C# in a reused mirror. This tool does not sync, reconfigure or overwrite a checkout.
foreach($file in Get-ChildItem -LiteralPath (Join-Path $source 'Assets/Maestro') -Recurse -File -Filter '*.cs'){
 $relative=[IO.Path]::GetRelativePath($source,$file.FullName);$copy=Join-Path $mirror $relative
 if(!(Test-Path -LiteralPath $copy) -or (Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $copy).Hash){throw "Mirror is stale: $relative. Run Verify-Quest."}
}
foreach($running in Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"){
 if($running.CommandLine -and $running.CommandLine.Contains($mirror)){throw 'The mirror already has a running Editor.'}
}
$id=[Guid]::NewGuid().ToString('N');$directory=Join-Path $repoRoot ".quest-evidence/native-room/$id"
New-Item -ItemType Directory -Path $directory | Out-Null
@{version=1;id=$id} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'owner.json') -Encoding utf8
$log=Join-Path $directory 'unity.log'
Stop-QuestBuildHelper
$process=Start-Process -FilePath $editorPath -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-force-d3d11','-buildTarget','Win64','-projectPath',('"'+$mirror+'"'),'-executeMethod','Maestro.Quest.Editor.QuestRoomProbe.Start','-logFile',('"'+$log+'"')) -Environment @{ADB_SERVER_SOCKET='tcp:localhost:5041';MAESTRO_ROOM_PROBE_DIRECTORY=$directory;MAESTRO_ROOM_PROBE_AVATAR=$(if($ProviderScenario -eq 'AvatarAnimation'){'1'}else{''});MAESTRO_ROOM_PROBE_PHYSICS=$(if([string]::IsNullOrWhiteSpace($Prompt)){'1'}else{''});MAESTRO_QUEST_RELEASE_PROFILE='';MAESTRO_QUEST_KEYSTORE='';MAESTRO_QUEST_KEY_ALIAS='';MAESTRO_QUEST_STORE_PASSWORD='';MAESTRO_QUEST_KEY_PASSWORD=''}
$previousPrompt=$env:MAESTRO_ROOM_PROBE_PROMPT;$previousProfile=$env:MAESTRO_ROOM_PROBE_PROFILE;$previousScenario=$env:MAESTRO_ROOM_PROBE_SCENARIO;$previousSpeech=$env:MAESTRO_ROOM_PROBE_SPEECH
try{
 $env:MAESTRO_ROOM_PROBE_PROMPT=$Prompt;$env:MAESTRO_ROOM_PROBE_PROFILE=$Profile;$env:MAESTRO_ROOM_PROBE_SCENARIO=$ProviderScenario;$env:MAESTRO_ROOM_PROBE_SPEECH=$SpeechFixture
 Push-Location $repoRoot
 try{
  $clientScript=$(if($Journey -eq 'Book'){'scripts/probe-native-book.ts'}else{'scripts/probe-native-room.ts'})
  & $runner $clientScript $directory *> (Join-Path $directory 'client.log');$clientExit=$LASTEXITCODE
 }finally{Pop-Location}
 if($clientExit -ne 0 -and !$process.HasExited){
  $stop=@{version=1;id=$id;operation='stop'} | ConvertTo-Json -Compress
  $pending=Join-Path $directory 'request.json.shutdown'
  [IO.File]::WriteAllText($pending,$stop,[Text.UTF8Encoding]::new($false))
  [IO.File]::Move($pending,(Join-Path $directory 'request.json'),$true)
 }
 $deadline=[DateTime]::UtcNow.AddSeconds(60);$cleanupAt=[DateTime]::UtcNow.AddSeconds(10)
 while(!$process.WaitForExit(1000)){
  if([DateTime]::UtcNow -gt $cleanupAt){Stop-QuestBuildHelper;$cleanupAt=[DateTime]::MaxValue}
  if([DateTime]::UtcNow -gt $deadline){$process.Kill();$process.WaitForExit();throw "Probe Editor did not exit. Evidence: $directory"}
 }
 $terminal=Get-Content -LiteralPath (Join-Path $directory 'terminal.json') -Raw | ConvertFrom-Json
 if($clientExit -ne 0 -or $process.ExitCode -ne 0 -or $terminal.exitCode -ne 0 -or $terminal.id -ne $id){throw "Native room probe failed. Evidence: $directory"}
 @{version=1;id=$id;clientExit=$clientExit;editorExit=$process.ExitCode;directory=$directory;providerUsed=![string]::IsNullOrWhiteSpace($Prompt);journey=$Journey;providerScenario=$ProviderScenario} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'verified.json')
 Write-Output "Native room probe passed: $directory"
}finally{
 $env:MAESTRO_ROOM_PROBE_PROMPT=$previousPrompt;$env:MAESTRO_ROOM_PROBE_PROFILE=$previousProfile;$env:MAESTRO_ROOM_PROBE_SCENARIO=$previousScenario;$env:MAESTRO_ROOM_PROBE_SPEECH=$previousSpeech
 if(!$process.HasExited){$process.Kill();$process.WaitForExit()}
 Stop-QuestBuildHelper
}
