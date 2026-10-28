# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$Editor,
 [Parameter(Mandatory)][string]$BuildMirror,
 [string]$Prompt,
 [string]$Profile = 'quest-probe',
 [ValidateSet('ContextCreateEdit','LiveVisual','ObserverVisual','EventProgram','AvatarAnimation','CompositeModule','PhysicsLaunch','TaskSteering','WorldPresentation','WorldLighting','WorldTime','WorldWeather','LiquidMedium','WaterTraversal','LiquidContacts','ImportedAudio','ImportedImage','GeneratedImage','ModelGeometry','PassthroughWindow','LearnerConversation')][string]$ProviderScenario,
 [string]$SpeechFixture,
 [switch]$SyntheticRoomScan,
 [switch]$SyntheticSound,
 [switch]$SyntheticLiveSound,
 [switch]$SyntheticImage,
 [switch]$SyntheticModel,
 [switch]$SyntheticGait,
 [switch]$SparseObjectView,
 [switch]$FirstLearner,
 [switch]$LiveContinuity,
 [switch]$NativeRestart,
 [switch]$TransportOnly,
 [switch]$MotionOnly,
 [switch]$SteeringOnly,
 [switch]$IdentityOnly,
 [string]$ResumeIdentityRun,
 [string]$ResumeLearnerRun,
 [ValidateSet('Headless','Book')][string]$Journey = 'Headless'
)
$ErrorActionPreference='Stop'
if($NativeRestart -and (!$LiveContinuity -or !$FirstLearner -or $Journey -ne 'Book')){throw 'NativeRestart requires the explicit offline Journey Book FirstLearner LiveContinuity mode.'}
if($LiveContinuity -and (!$FirstLearner -or $Journey -ne 'Book')){throw 'LiveContinuity requires the explicit offline Journey Book FirstLearner mode.'}
if($FirstLearner -and ($Journey -ne 'Book' -or $ProviderScenario -or ![string]::IsNullOrWhiteSpace($Prompt) -or $SpeechFixture -or $ResumeLearnerRun -or $ResumeIdentityRun -or $TransportOnly -or $MotionOnly -or $SteeringOnly -or $IdentityOnly -or $SparseObjectView -or $SyntheticRoomScan -or $SyntheticSound -or $SyntheticLiveSound -or $SyntheticImage -or $SyntheticModel -or $SyntheticGait)){throw 'FirstLearner requires offline Journey Book without provider, resume, focused or synthetic asset options.'}
# A successful ordinary learner result must never qualify the separate Live journey.
# This pure check is also exercised without starting a browser or native Editor.
. (Join-Path $PSScriptRoot 'Assert-QuestLiveContinuity.ps1')
if($NativeRestart){. (Join-Path $PSScriptRoot 'QuestProbeRestart.ps1')}

if($TransportOnly -and ($IdentityOnly -or $MotionOnly -or $SteeringOnly -or $Journey -ne 'Headless' -or $ProviderScenario -or ![string]::IsNullOrWhiteSpace($Prompt) -or $ResumeLearnerRun -or $ResumeIdentityRun)){throw 'TransportOnly is an offline Headless wire-boundary fixture; other focused, provider, Book and resume modes are excluded.'}
if($IdentityOnly -and ($MotionOnly -or $SteeringOnly -or $Journey -ne 'Headless' -or $ProviderScenario -or ![string]::IsNullOrWhiteSpace($Prompt) -or $ResumeLearnerRun)){throw 'IdentityOnly is an offline Headless fixture; other focused, provider, Book and learner resume modes are excluded.'}
if($ResumeIdentityRun -and (!$IdentityOnly -or $ResumeIdentityRun -notmatch '^[a-f0-9]{32}$')){throw 'ResumeIdentityRun requires IdentityOnly and a completed native probe run ID.'}
if($SteeringOnly -and ($MotionOnly -or $Journey -ne 'Headless' -or $ProviderScenario -or ![string]::IsNullOrWhiteSpace($Prompt) -or $ResumeLearnerRun)){throw 'SteeringOnly is an offline Headless fixture; MotionOnly, provider, Book and resume modes are excluded.'}
if($MotionOnly -and ($Journey -ne 'Headless' -or $ProviderScenario -or ![string]::IsNullOrWhiteSpace($Prompt) -or $ResumeLearnerRun)){throw 'MotionOnly is an offline Headless fixture; provider, Book and resume modes are excluded.'}
if($SparseObjectView -and ($ProviderScenario -or ![string]::IsNullOrWhiteSpace($Prompt))){throw 'SparseObjectView is an explicit offline Headless or Book observation fixture only.'}
if(($SyntheticModel -or $SyntheticGait) -and ($ProviderScenario -or ![string]::IsNullOrWhiteSpace($Prompt))){throw 'SyntheticModel is an explicit offline Headless or Book fixture only.'}
if($SyntheticImage -and ($Journey -ne 'Book' -or $ProviderScenario)){throw 'SyntheticImage is an explicit offline Book fixture only.'}
if($SyntheticLiveSound -and ($ProviderScenario -or ![string]::IsNullOrWhiteSpace($Prompt))){throw 'SyntheticLiveSound is an explicit offline Book or Headless fixture only.'}
if($SyntheticSound -and ($Journey -ne 'Book' -or $ProviderScenario)){throw 'SyntheticSound is an explicit offline Book fixture only.'}
if($SyntheticRoomScan -and $ProviderScenario -ne 'LearnerConversation'){throw 'SyntheticRoomScan is an explicit LearnerConversation fixture only.'}
if($ProviderScenario -in @('LiveVisual','ObserverVisual')){
 if([string]::IsNullOrWhiteSpace($SpeechFixture) -or !(Test-Path -LiteralPath $SpeechFixture -PathType Leaf)){throw 'Live provider scenarios require an explicit SpeechFixture JSON file.'}
 $SpeechFixture=(Resolve-Path -LiteralPath $SpeechFixture).Path
}
if($ProviderScenario -eq 'LearnerConversation' -and $Journey -ne 'Headless'){throw 'LearnerConversation uses the interactive headless driver.'}
if($ProviderScenario){
 if(![string]::IsNullOrWhiteSpace($Prompt)){throw 'ProviderScenario cannot be combined with Prompt.'}
 if($Journey -eq 'Book' -and $ProviderScenario -ne 'ContextCreateEdit'){throw 'The real-provider book supports ContextCreateEdit only.'}
 $Prompt='Please create my test object now. Use the definition I gave in the previous message.'
 if($ProviderScenario -eq 'ModelGeometry'){$Prompt='The imported building is tiny. Please use its original metre scale and original origin, make it fixed with collision that preserves its doorway and interior, and let its floors be walkable. Keep its existing position, rotation and object scale. Leave the other objects, room view and real-room collision policies alone, and keep physics paused.'}
 if($ProviderScenario -eq 'PassthroughWindow'){$Prompt='Please prepare a little freestanding picture frame called RoomWindow, with a rectangular opening that fully reveals my real room when I use my Quest later. Leave its centre open, with no solid picture behind it. I know I am testing without the headset right now; save it ready for later. Keep the overall room view and physics as they are.'}
 if($ProviderScenario -eq 'GeneratedImage'){$Prompt='Could you put the picture you just made in this chat on the outside cover of my book? Keep the pages readable and everything else as it is.'}
 if($ProviderScenario -eq 'ImportedImage'){$Prompt='I imported a picture called Blue tiles.png. Could you put that picture on the outside cover of my book? Keep the pages readable and everything else as it is.'}
 if($ProviderScenario -eq 'ImportedAudio'){$Prompt='I imported a sound called Little bell.wav. Could you give the book that little chime, so it comes from the book itself? Attach the imported sound, but do not play it yet. Keep everything else as it is.'}
 if($ProviderScenario -eq 'LiquidContacts'){$Prompt='Please save an editable program called WaterTouch for the existing ContactPool. Save it only, with no buttons or automatic start. When I start it later, it should wait for the first physical water contact change in that pool and remember who touched it in a text state variable called participant, initially empty. Then wait 20 seconds and finish. Do not touch or change any object, create anything, or start physics.'}
 if($ProviderScenario -eq 'WaterTraversal'){$Prompt='Please let Maestro wade through shallow water up to 15 centimetres deep, but keep WaterRobot out of water. Do not move anything or change collisions or physics.'}
 if($ProviderScenario -eq 'LiquidMedium'){$Prompt='Please fill the small pool named MediumPool with exactly one litre of light oil. Make its density 850 kilograms per cubic metre and let it slow things moving through it more than ordinary water. Leave the empty MediumCup alone, do not move either vessel, and keep physics paused.'}
 if($ProviderScenario -eq 'WorldWeather'){$Prompt='Could you make my virtual world feel like a gentle rainy afternoon right now, with some cloud cover and a little blue-grey fog? Only a light rain please. Keep every object where it is, keep my real room view and collisions as they are, and do not change the world clock.'}
 if($ProviderScenario -eq 'WorldTime'){$Prompt='Could you give my virtual world a repeating day and night lighting cycle? I want dim blue night light at midnight and bright warm sunlight at noon, with a full day taking 24 real minutes. Start at noon but leave the clock paused so I can inspect it. Keep all objects where they are, and keep my real room view and collisions as they are.'}
 if($ProviderScenario -eq 'WorldLighting'){$Prompt='Could you give my virtual objects gentle evening lighting, with dim blue ambient light and a warm sun? Keep everything in its place and leave my real room view and collisions as they are.'}
 if($ProviderScenario -eq 'WorldPresentation'){$Prompt='Could you make only the empty background half virtual and half real, and stop real things from hiding the virtual objects? Keep my book, objects and collision settings as they are.'}
}
if($Journey -eq 'Book' -and !$ProviderScenario -and ![string]::IsNullOrWhiteSpace($Prompt)){throw 'The deterministic book journey does not accept a provider prompt.'}
if($ResumeLearnerRun -and ($ProviderScenario -ne 'LearnerConversation' -or $ResumeLearnerRun -notmatch '^[a-f0-9]{32}$')){throw 'ResumeLearnerRun requires a completed learner run ID.'}
. (Join-Path $PSScriptRoot 'QuestBuildProcesses.ps1')
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$mirror=(Resolve-Path -LiteralPath $BuildMirror).Path
$editorPath=(Resolve-Path -LiteralPath $Editor).Path
$source=Join-Path $repoRoot 'unity/MaestroQuest'
$runner=Join-Path $repoRoot 'node_modules/.bin/tsx.cmd'
if(!(Test-Path -LiteralPath $runner)){throw 'Install the repository npm dependencies before starting a room probe.'}
if($LiveContinuity){
 $speechManifest=Join-Path $repoRoot 'test-fixtures/models/whisper-tiny-en.json'
 $speechRevision=(Get-Content -LiteralPath $speechManifest -Raw | ConvertFrom-Json).revision
 if($speechRevision -notmatch '^[a-f0-9]{40}$'){throw 'Offline speech manifest must pin an immutable revision.'}
 $speechFolder=Join-Path $repoRoot ('.quest-tools/whisper-tiny-en-'+$speechRevision)
 & $runner (Join-Path $repoRoot 'scripts/first-learner-speech-assets.mjs') --verify $speechFolder $speechManifest
 if($LASTEXITCODE -ne 0){throw 'Offline Live speech assets are missing or invalid; no Editor was started. See QUEST_NATIVE_ROOM_PROBE.md.'}
}
$restartNode=$null;$restartTsx=$null
if($NativeRestart){
 $restartNode=(Get-Command node -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
 $restartTsx=Join-Path $repoRoot 'node_modules/tsx/dist/cli.mjs'
 if(!(Test-Path -LiteralPath $restartNode -PathType Leaf) -or !(Test-Path -LiteralPath $restartTsx -PathType Leaf)){throw 'NativeRestart requires the installed Node and tsx CLI before starting an Editor.'}
}
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
@{version=1;id=$id;firstLearner=[bool]$FirstLearner;liveContinuity=[bool]$LiveContinuity;nativeRestart=[bool]$NativeRestart;sparseObjectView=[bool]$SparseObjectView;motionOnly=[bool]$MotionOnly;steeringOnly=[bool]$SteeringOnly;identityOnly=[bool]$IdentityOnly;transportOnly=[bool]$TransportOnly} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'owner.json') -Encoding utf8
if($ResumeIdentityRun){
 $previousRun=Join-Path $repoRoot ".quest-evidence/native-room/$ResumeIdentityRun"
 $previousOwner=Get-Content -LiteralPath (Join-Path $previousRun 'owner.json') -Raw | ConvertFrom-Json
 $previousVerified=Get-Content -LiteralPath (Join-Path $previousRun 'verified.json') -Raw | ConvertFrom-Json
 $identity=Get-Content -LiteralPath (Join-Path $previousRun 'native-object-identity.json') -Raw | ConvertFrom-Json
 if($previousOwner.id -ne $ResumeIdentityRun -or $previousVerified.id -ne $ResumeIdentityRun -or $previousVerified.clientExit -ne 0 -or $previousVerified.editorExit -ne 0 -or $previousVerified.outcome -ne 'passed' -or $previousVerified.providerUsed){throw 'Identity resume requires a cleanly closed, owned offline native run.'}
 if($identity.source -ne 'explicit-native-object-directory' -or $identity.version -ne 1 -or !$identity.ids){throw 'Identity resume requires explicit final saved-object evidence.'}
 Copy-Item -LiteralPath (Join-Path $previousRun 'workspace') -Destination (Join-Path $directory 'workspace') -Recurse
 @{version=1;sourceRun=$ResumeIdentityRun;nativeIdentity=$identity;boundary='Copied closed offline workspace for an identity-only native restart. No provider or old operation is replayed.'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $directory 'learner-resume.json') -Encoding utf8
}
if($ResumeLearnerRun){
 $previousRun=Join-Path $repoRoot ".quest-evidence/native-room/$ResumeLearnerRun"
 $previousOwner=Get-Content -LiteralPath (Join-Path $previousRun 'owner.json') -Raw | ConvertFrom-Json
 $previousVerified=Get-Content -LiteralPath (Join-Path $previousRun 'verified.json') -Raw | ConvertFrom-Json
 $previousSession=Get-Content -LiteralPath (Join-Path $previousRun 'learner-session.json') -Raw | ConvertFrom-Json
 if($previousOwner.id -ne $ResumeLearnerRun -or $previousVerified.id -ne $ResumeLearnerRun -or $previousVerified.clientExit -ne 0 -or $previousVerified.editorExit -ne 0 -or !$previousSession.finished){throw 'Resume requires a cleanly closed, owned learner run.'}
 if($previousSession.finalIdentity.source -ne 'explicit-native-object-directory' -or $previousSession.finalIdentity.version -ne 1 -or !$previousSession.finalIdentity.ids){throw 'Resume requires explicit final saved-object identity evidence; an old periodic object list is insufficient.'}
 if($previousSession.accessMode -ne $env:MAESTRO_HEADLESS_ACCESS_MODE){throw 'Resume must keep the original access mode.'}
 $previousProfile=[IO.Path]::GetFullPath($previousSession.profile)
 $tempPrefix=Join-Path ([IO.Path]::GetTempPath()) 'maestro-headless-'
 $profilePrefix=[IO.Path]::GetFullPath((Join-Path $previousRun 'profiles'))+[IO.Path]::DirectorySeparatorChar
 if(!$previousProfile.StartsWith($tempPrefix,[StringComparison]::OrdinalIgnoreCase) -and !$previousProfile.StartsWith($profilePrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected learner profile location.'}
 $profileCopy=Join-Path $directory 'profiles/learner'
 New-Item -ItemType Directory -Path (Split-Path -Parent $profileCopy) | Out-Null
 Copy-Item -LiteralPath $previousProfile -Destination $profileCopy -Recurse
 Copy-Item -LiteralPath (Join-Path $previousRun 'workspace') -Destination (Join-Path $directory 'workspace') -Recurse
 @{version=1;sourceRun=$ResumeLearnerRun;accessMode=$previousSession.accessMode;nativeIdentity=$previousSession.finalIdentity;profileSha256=(Get-FileHash -LiteralPath (Join-Path $previousProfile 'profile.json')).Hash;boundary='Copied closed diagnostic profile and saved room; new native session. No old operations are replayed.'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $directory 'learner-resume.json') -Encoding utf8
}
$log=Join-Path $directory 'unity.log'
Stop-QuestBuildHelper
$startEditor={param([string]$probeDirectory,[string]$probeLog)
 Start-Process -FilePath $editorPath -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-force-d3d11','-buildTarget','Win64','-projectPath',('"'+$mirror+'"'),'-executeMethod','Maestro.Quest.Editor.QuestRoomProbe.Start','-logFile',('"'+$probeLog+'"')) -Environment @{ADB_SERVER_SOCKET='tcp:localhost:5041';MAESTRO_ROOM_PROBE_DIRECTORY=$probeDirectory;MAESTRO_ROOM_PROBE_SPARSE_OBJECTS=$(if($SparseObjectView){'1'}else{''});MAESTRO_ROOM_PROBE_GAIT=$(if($SyntheticGait){Join-Path $repoRoot 'test-fixtures/models/walking-character.glb'}else{''});MAESTRO_ROOM_PROBE_MODEL=$(if($ProviderScenario -eq 'ModelGeometry' -or $SyntheticModel){Join-Path $repoRoot 'test-fixtures/models/room-building.glb'}else{''});MAESTRO_ROOM_PROBE_IMAGE=$(if($ProviderScenario -eq 'ImportedImage' -or $SyntheticImage){'1'}else{''});MAESTRO_ROOM_PROBE_AUDIO=$(if($ProviderScenario -eq 'ImportedAudio' -or $SyntheticSound){'1'}else{''});MAESTRO_ROOM_PROBE_LIVE_AUDIO=$(if($SyntheticLiveSound){'1'}else{''});MAESTRO_ROOM_PROBE_SCAN=$(if($SyntheticRoomScan){'1'}else{''});MAESTRO_ROOM_PROBE_LEARNER=$(if($ProviderScenario -eq 'LearnerConversation'){'1'}else{''});MAESTRO_ROOM_PROBE_AVATAR=$(if($ProviderScenario -in @('AvatarAnimation','LearnerConversation')){'1'}else{''});MAESTRO_ROOM_PROBE_PHYSICS=$(if([string]::IsNullOrWhiteSpace($Prompt) -or $ProviderScenario -in @('PhysicsLaunch','LearnerConversation')){'1'}else{''});MAESTRO_QUEST_RELEASE_PROFILE='';MAESTRO_QUEST_KEYSTORE='';MAESTRO_QUEST_KEY_ALIAS='';MAESTRO_QUEST_STORE_PASSWORD='';MAESTRO_QUEST_KEY_PASSWORD=''}
}
$process=& $startEditor $directory $log
$activeDirectory=$directory;$activeId=$id;$initialLog=$log;$clientProcess=$null;$restart=$null
$initialIdentity=$null
$previousPrompt=$env:MAESTRO_ROOM_PROBE_PROMPT;$previousProfile=$env:MAESTRO_ROOM_PROBE_PROFILE;$previousScenario=$env:MAESTRO_ROOM_PROBE_SCENARIO;$previousSpeech=$env:MAESTRO_ROOM_PROBE_SPEECH
try{
 if($NativeRestart){$initialIdentity=Get-QuestProbeProcessIdentity $process}
 $env:MAESTRO_ROOM_PROBE_PROMPT=$Prompt;$env:MAESTRO_ROOM_PROBE_PROFILE=$Profile;$env:MAESTRO_ROOM_PROBE_SCENARIO=$ProviderScenario;$env:MAESTRO_ROOM_PROBE_SPEECH=$SpeechFixture
 Push-Location $repoRoot
 try{
  $clientScript=$(if($LiveContinuity){'scripts/probe-quest-first-learner-live.ts'}elseif($FirstLearner){'scripts/probe-quest-first-learner-native.ts'}elseif($TransportOnly){'scripts/probe-room-transport.ts'}elseif($ProviderScenario -eq 'LearnerConversation'){'scripts/probe-learner-room.ts'}elseif($Journey -eq 'Book' -and $ProviderScenario){'scripts/probe-provider-book.ts'}elseif($Journey -eq 'Book'){'scripts/probe-native-book.ts'}else{'scripts/probe-native-room.ts'})
  if($NativeRestart){
   $clientProcess=Start-Process -FilePath $restartNode -WindowStyle Hidden -PassThru -WorkingDirectory $repoRoot -ArgumentList @(('"'+$restartTsx+'"'),('"'+(Join-Path $repoRoot $clientScript)+'"'),('"'+$directory+'"')) -RedirectStandardOutput (Join-Path $directory 'client.log') -RedirectStandardError (Join-Path $directory 'client-errors.log')
   $clientDeadline=[DateTime]::UtcNow.AddMinutes(15)
   while(!$clientProcess.WaitForExit(250)){
    if(!$restart -and (Test-Path -LiteralPath (Join-Path $directory 'restart-request.json'))){
     $restart=Invoke-QuestProbeNativeRestart -Process $process -Identity $initialIdentity -RootDirectory $directory -ProbeId $id -Launch $startEditor
     $process=$restart.process;$activeDirectory=$restart.directory;$activeId=$restart.id;$log=$restart.log
    }
    if([DateTime]::UtcNow -gt $clientDeadline){throw "Native restart client exceeded its bounded duration. Evidence: $directory"}
   }
   $clientExit=$clientProcess.ExitCode
   if(!$restart){throw "The native restart journey did not request and observe an actual interruption. Evidence: $directory"}
  }else{
   & $runner $clientScript $directory *> (Join-Path $directory 'client.log');$clientExit=$LASTEXITCODE
  }
 }finally{Pop-Location}
 if($clientExit -ne 0 -and !$process.HasExited){
  $stop=@{version=1;id=$activeId;operation='stop'} | ConvertTo-Json -Compress
  $pending=Join-Path $activeDirectory 'request.json.shutdown'
  [IO.File]::WriteAllText($pending,$stop,[Text.UTF8Encoding]::new($false))
  [IO.File]::Move($pending,(Join-Path $activeDirectory 'request.json'),$true)
 }
 $deadline=[DateTime]::UtcNow.AddSeconds(60);$shutdownAt=$null;$helperStopped=$false
 while(!$process.WaitForExit(1000)){
  # A client result or exit request is not native Editor shutdown. Match the
  # verifier: do not stop an inherited helper during the Play-to-Edit transition.
  if(!$shutdownAt -and (Test-Path -LiteralPath $log) -and
     (Select-String -LiteralPath $log -Pattern 'Batchmode quit successfully invoked|Killing ADB server|Exiting batchmode' -Quiet)){$shutdownAt=[DateTime]::UtcNow}
  if(!$helperStopped -and $shutdownAt -and [DateTime]::UtcNow -gt $shutdownAt.AddSeconds(10)){Stop-QuestBuildHelper;$helperStopped=$true}
  if([DateTime]::UtcNow -gt $deadline){$process.Kill();$process.WaitForExit();throw "Probe Editor did not exit. Evidence: $directory"}
 }
 $terminal=Get-Content -LiteralPath (Join-Path $activeDirectory 'terminal.json') -Raw | ConvertFrom-Json
 if($clientExit -ne 0 -or $process.ExitCode -ne 0 -or $terminal.exitCode -ne 0 -or $terminal.id -ne $activeId){throw "Native room probe failed. Evidence: $directory"}
 if($FirstLearner){
  $learnerResult=Get-Content -LiteralPath (Join-Path $directory 'first-learner/result.json') -Raw | ConvertFrom-Json
  if($learnerResult.status -ne 'passed' -or $learnerResult.nativeProbeId -ne $id -or $learnerResult.nativeExecution -ne $true -or $learnerResult.productionComposition -ne $true -or $learnerResult.realProvider -ne $false){throw "Production-entry learner did not prove the owned offline native journey. Evidence: $directory"}
  if($LiveContinuity){Assert-QuestLiveContinuityResult -Result $learnerResult -ProbeId $id -EvidenceDirectory $directory}
  if($NativeRestart){
   & $runner (Join-Path $repoRoot 'scripts/assert-quest-native-restart.ts') $directory
   if($LASTEXITCODE -ne 0){throw "Native process restart evidence failed. Evidence: $directory"}
  }
 }
 if($NativeRestart){& (Join-Path $PSScriptRoot 'Assert-QuestUnityLog.ps1') -LogPath $initialLog}
 & (Join-Path $PSScriptRoot 'Assert-QuestUnityLog.ps1') -LogPath $log
 @{version=1;id=$id;clientExit=$clientExit;editorExit=$process.ExitCode;directory=$directory;providerUsed=![string]::IsNullOrWhiteSpace($Prompt);journey=$Journey;firstLearner=[bool]$FirstLearner;liveContinuity=[bool]$LiveContinuity;nativeRestart=[bool]$NativeRestart;sparseObjectView=[bool]$SparseObjectView;motionOnly=[bool]$MotionOnly;steeringOnly=[bool]$SteeringOnly;identityOnly=[bool]$IdentityOnly;transportOnly=[bool]$TransportOnly;providerScenario=$ProviderScenario;outcome=$(if($ProviderScenario -eq 'LearnerConversation'){'collected-requires-semantic-review'}else{'passed'});resumedFrom=$(if($ResumeIdentityRun){$ResumeIdentityRun}else{$ResumeLearnerRun})} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'verified.json')
 if($ProviderScenario -eq 'LearnerConversation'){Write-Output "Learner session collected (manual outcome review required): $directory"}else{Write-Output "Native room probe passed: $directory"}
}finally{
 $env:MAESTRO_ROOM_PROBE_PROMPT=$previousPrompt;$env:MAESTRO_ROOM_PROBE_PROFILE=$previousProfile;$env:MAESTRO_ROOM_PROBE_SCENARIO=$previousScenario;$env:MAESTRO_ROOM_PROBE_SPEECH=$previousSpeech
 if($clientProcess -and !$clientProcess.HasExited){$clientProcess.Kill($true);$clientProcess.WaitForExit()}
 if(!$process.HasExited){$process.Kill();$process.WaitForExit()}
 Stop-QuestBuildHelper
}
