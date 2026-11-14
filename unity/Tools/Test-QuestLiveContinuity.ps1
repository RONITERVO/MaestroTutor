# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
param([string]$ReportPath)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$path=Join-Path $PSScriptRoot 'Run-QuestRoomProbe.ps1'
$gate=Join-Path $PSScriptRoot 'Assert-QuestLiveContinuity.ps1'
$tokens=$null;$parseErrors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$parseErrors)
if($parseErrors.Count){throw ($parseErrors | Out-String)}
$gateTokens=$null;$gateErrors=$null
$gateAst=[Management.Automation.Language.Parser]::ParseFile($gate,[ref]$gateTokens,[ref]$gateErrors)
if($gateErrors.Count){throw ($gateErrors | Out-String)}
$fn=$gateAst.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Assert-QuestLiveContinuityResult'},$true)
if($fn.Count -ne 1){throw 'Missing unique pure result validator'}
. $gate
$guards=$ast.FindAll({param($node) $node -is [Management.Automation.Language.IfStatementAst] -and ($node.Extent.Text.StartsWith('if($LiveContinuity -and') -or $node.Extent.Text.StartsWith('if($FirstLearner -and'))},$true)
if($guards.Count -ne 2){throw 'Missing explicit admission guards'}
$cases=[Collections.Generic.List[object]]::new()
function Check-Arguments([string]$Name,[hashtable]$Values,[bool]$Accepted){
 $errorText=$null
 try{& {
  param($InputValues,$Statements)
  $FirstLearner=$false;$LiveContinuity=$false;$Journey='Headless'
  foreach($key in $InputValues.Keys){Set-Variable -Name $key -Value $InputValues[$key]}
  foreach($statement in $Statements){. ([scriptblock]::Create($statement.Extent.Text))}
 } $Values $guards}catch{$errorText=$_.Exception.Message}
 if(($null -eq $errorText) -ne $Accepted){throw "Argument contract failed: $Name $errorText"}
 $cases.Add(@{name=$Name;accepted=$Accepted;status='passed'})
}
Check-Arguments 'default-headless' @{} $true
Check-Arguments 'ordinary-first-learner' @{FirstLearner=$true;Journey='Book'} $true
Check-Arguments 'explicit-live-continuity' @{FirstLearner=$true;LiveContinuity=$true;Journey='Book'} $true
Check-Arguments 'live-requires-first-learner' @{LiveContinuity=$true;Journey='Book'} $false
Check-Arguments 'live-requires-book' @{LiveContinuity=$true;FirstLearner=$true;Journey='Headless'} $false
foreach($option in @('ProviderScenario','Prompt','SpeechFixture','ResumeIdentityRun','ResumeLearnerRun','TransportOnly','MotionOnly','SteeringOnly','IdentityOnly','SparseObjectView','SyntheticRoomScan','SyntheticSound','SyntheticLiveSound','SyntheticImage','SyntheticModel','SyntheticGait')){
 $values=@{LiveContinuity=$true;FirstLearner=$true;Journey='Book'};$values[$option]=$(if($option -in @('ProviderScenario','Prompt','SpeechFixture','ResumeIdentityRun','ResumeLearnerRun')){'forbidden'}else{$true})
 Check-Arguments "live-rejects-$option" $values $false
}
$assignment=$ast.FindAll({param($node)$node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -eq '$clientScript'},$true)
if($assignment.Count -ne 1){throw 'Expected one client script selector'}
foreach($selection in @(@{first=$false;live=$false;journey='Headless';script='scripts/probe-native-room.ts'},@{first=$false;live=$false;journey='Book';script='scripts/probe-native-book.ts'},@{first=$true;live=$false;journey='Book';script='scripts/probe-quest-first-learner-native.ts'},@{first=$true;live=$true;journey='Book';script='scripts/probe-quest-first-learner-live.ts'})){
 $FirstLearner=$selection.first;$LiveContinuity=$selection.live;$Journey=$selection.journey;$TransportOnly=$false;$ProviderScenario=$null
 . ([scriptblock]::Create($assignment[0].Extent.Text))
 if($clientScript -cne $selection.script){throw 'Wrong scenario driver selected'}
 $cases.Add(@{name='driver-'+$selection.script;status='passed'})
}
$source=[IO.File]::ReadAllText($path)
if([regex]::Matches($source,'liveContinuity=\[bool\]\$LiveContinuity').Count -ne 2){throw 'Owner and verified receipts must both record explicit mode'}
$probe='1'*32;$tree='2'*32;$apple='3'*32;$hash='a'*64
$valid=@{status='passed';nativeProbeId=$probe;nativeExecution=$true;productionComposition=$true;realProvider=$false;physicalDevice=$false;native=@{
 nativeProbeId=$probe;qualified=$true;realProvider=$false;physicalDevice=$false;treeId=$tree;appleId=$apple;
 receipts=@(@{id='4'*32;capability='object.create';phase='completed';call=@{id='object.create';arguments=@{}};output=@{objectId=$tree}},@{id='5'*32;capability='object.create';phase='completed';call=@{id='object.create';arguments=@{}};output=@{objectId=$apple}},@{id='6'*32;capability='object.delete';phase='completed';call=@{id='object.delete';arguments=@{target=$tree}}});
 live=@{sessions=@(@{starts=1;ends=1;turnComplete=$true;closed=$true;pcmSha256=$hash})};mediaChecks=@(@{pcmSha256=$hash;wavSha256='b'*64;samples=1600;packets=5})}}
$manifestPath=Join-Path $repoRoot 'test-fixtures/models/whisper-tiny-en.json'
$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$valid.speechAssets=@{modelId=$manifest.modelId;revision=$manifest.revision;manifestSha256=(Get-FileHash -LiteralPath $manifestPath).Hash.ToLowerInvariant();totalBytes=$manifest.totalBytes;loader=$manifest.loader;missingFiles=@();
 files=@($manifest.files | ForEach-Object {@{path=$_.path;bytes=$_.size;sha256=$_.sha256}});
 requests=@($manifest.files | ForEach-Object {@{path=$_.path;method='GET';range=$null;status=200;bytes=$_.size;sha256=$_.sha256}})}
$metadataEntry=$manifest.metadataFiles[0]
$valid.speechAssets.metadataBytes=$manifest.metadataBytes
$valid.speechAssets.metadataFiles=@(@{path=$metadataEntry.path;bytes=$metadataEntry.size;sha256=$metadataEntry.sha256;firstByteSha256=$metadataEntry.firstByteSha256})
$valid.speechAssets.requests+=@{path=$metadataEntry.path;method='GET';range='bytes=0-0';status=206;bytes=1;sha256=$metadataEntry.firstByteSha256}
$valid.speechAssets.fullAttempts=@($manifest.files | ForEach-Object {@{path=$_.path;count=1}})
$valid.native.speechWorkers=@{version=1;overflow=$false;workers=@(@{id=1;url='http://127.0.0.1:12543/assets/observerWhisper.worker-AbCd1234.js';ready=@(@{model=$manifest.modelId;profile='q4'});errors=@();results=@{count=0;totalElapsedMs=0;maxElapsedMs=0}})}
$tempParent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
$tempName='maestro-live-continuity-'+[Guid]::NewGuid().ToString('N');$temporary=Join-Path $tempParent $tempName
$waveFolder=Join-Path $temporary 'first-learner';[void][IO.Directory]::CreateDirectory($waveFolder)
$originalPath=Join-Path $waveFolder 'synthetic-live-microphone.wav';$activePath=Join-Path $waveFolder 'synthetic-live-microphone-input.wav'
$sourceWave=[IO.File]::ReadAllBytes((Join-Path $repoRoot 'test-fixtures/audio/first-learner-apple-request.wav'))
$rate=[BitConverter]::ToUInt32($sourceWave,24);$waveLength=44+30*$rate*2
$original=New-Object byte[] $waveLength;[Array]::Copy($sourceWave,$original,44)
[Array]::Copy([BitConverter]::GetBytes([uint32]($waveLength-8)),0,$original,4,4)
[Array]::Copy([BitConverter]::GetBytes([uint32]($waveLength-44)),0,$original,40,4)
[Array]::Copy($sourceWave,44,$original,44+2*$rate,$sourceWave.Length-44)
$silence=New-Object byte[] $waveLength;[Array]::Copy($original,$silence,44)
function Hash-Wave([byte[]]$Bytes){$algorithm=[Security.Cryptography.SHA256]::Create();try{return [BitConverter]::ToString($algorithm.ComputeHash($Bytes)).Replace('-','').ToLowerInvariant()}finally{$algorithm.Dispose()}}
[IO.File]::WriteAllBytes($originalPath,$original);[IO.File]::WriteAllBytes($activePath,$silence)
$valid.native.live.fixture=@{file=$originalPath;sha256=(Hash-Wave $original);sampleRate=$rate;samples=30*$rate}
$valid.native.live.inputTransition=@{file=$activePath;state='silence';startedAt=21000;completedAt=22000;originalFixtureSha256=(Hash-Wave $original);previousSha256=(Hash-Wave $original);verifiedOriginalSha256=(Hash-Wave $original);silenceSha256=(Hash-Wave $silence);verifiedActiveSha256=(Hash-Wave $silence);sampleRate=$rate;samples=30*$rate;bytes=$waveLength;capturedPcmSha256=$hash}
$valid.native.live.sessions[0].capturedAt=20000;$valid.native.live.sessions[0].completedAt=23000;$valid.native.live.sessions[0].closedAt=24000
$valid.native.releasedCapture=@{tracks=@(@{kind='audio';readyState='ended';enabled=$true});attempts=@(@{audio=$true;video=$false;state='resolved'})}
$valid.native.beforeReloadCapture=$valid.native.releasedCapture
$valid.native.restoredCapture=@{tracks=@();attempts=@()}
$event=@{id=1;stage='first-message';startedAt=9000;method='GET';origin='https://huggingface.co';path='/onnx-community/whisper-tiny.en/resolve/main/onnx/encoder_model_quantized.onnx';type='fetch';state='finished';status=206;endedAt=45000;endedDuringCleanup=$true;error=$null}
$valid.networkEvents=@($event);$valid.networkFailures=@();$valid.networkSettlements=@();$valid.networkReconciliations=@()
$valid.browserCacheVerifications=@(9500,39500 | ForEach-Object {@{cacheName='transformers-cache';modelId=$manifest.modelId;manifestSha256=$valid.speechAssets.manifestSha256;verifiedAt=$_;files=@($manifest.files | ForEach-Object {@{path=$_.path;bytes=$_.size;sha256=$_.sha256}})}})
$names=@('first-message','native-room','reload');$times=@(10000,30000,40000)
for($i=0;$i -lt 3;$i++){
 $pending=@{id=1;stage=$event.stage;startedAt=$event.startedAt;method=$event.method;origin=$event.origin;path=$event.path;type=$event.type;state='pending';status=206;receipt=$valid.speechAssets.requests[7]}
 $workers=$(if($i -lt 2){$valid.native.speechWorkers}else{@{version=1;overflow=$false;workers=@()}})
 $valid.networkSettlements+=@{stage=$names[$i];at=$times[$i];elapsedMs=1000;quietMs=600;networkGeneration=10+$i;manifestSha256=$valid.speechAssets.manifestSha256;fullFiles=7;cacheVerifiedAt=$(if($i -lt 2){9500}else{39500});observedPendingIds=@(1);pendingMetadata=@($pending);workers=$workers}
}
try {
$validJson=$valid | ConvertTo-Json -Depth 12
function Check-Result([string]$Name,[scriptblock]$Mutate,[bool]$Accepted,[bool]$ResetFiles=$false){
 try{
 $result=$validJson | ConvertFrom-Json
 & $Mutate $result
 $errorText=$null;try{Assert-QuestLiveContinuityResult -Result $result -ProbeId $probe -EvidenceDirectory $temporary -SpeechManifestPath $manifestPath}catch{$errorText=$_.Exception.Message}
 if(($null -eq $errorText) -ne $Accepted){throw "Result contract failed: $Name $errorText"}
 $cases.Add(@{name=$Name;accepted=$Accepted;status='passed'})
 }finally{if($ResetFiles){[IO.File]::WriteAllBytes($originalPath,$original);[IO.File]::WriteAllBytes($activePath,$silence)}}
}
Check-Result 'complete-owned-live-result' {} $true
Check-Result 'reject-failed-status' {param($r)$r.status='failed'} $false
Check-Result 'reject-wrong-top-owner' {param($r)$r.nativeProbeId='7'*32} $false
Check-Result 'reject-wrong-native-owner' {param($r)$r.native.nativeProbeId='7'*32} $false
Check-Result 'reject-missing-native-execution' {param($r)$r.nativeExecution=$null} $false
Check-Result 'reject-string-native-execution' {param($r)$r.nativeExecution='true'} $false
Check-Result 'reject-no-production-composition' {param($r)$r.productionComposition=$false} $false
Check-Result 'reject-unqualified' {param($r)$r.native.qualified=$false} $false
Check-Result 'reject-real-provider' {param($r)$r.realProvider=$true} $false
Check-Result 'reject-string-offline-flag' {param($r)$r.realProvider='false'} $false
Check-Result 'reject-physical-device' {param($r)$r.physicalDevice=$true} $false
Check-Result 'reject-native-provider' {param($r)$r.native.realProvider=$true} $false
Check-Result 'reject-invalid-id' {param($r)$r.native.treeId='bad'} $false
Check-Result 'reject-shared-apple-tree-id' {param($r)$r.native.appleId=$r.native.treeId} $false
Check-Result 'reject-two-receipts' {param($r)$r.native.receipts=$r.native.receipts[0..1]} $false
Check-Result 'reject-pending-receipt' {param($r)$r.native.receipts[2].phase='running'} $false
Check-Result 'reject-reused-receipt-id' {param($r)$r.native.receipts[1].id=$r.native.receipts[0].id} $false
Check-Result 'reject-wrong-create-output' {param($r)$r.native.receipts[1].output.objectId=$r.native.treeId} $false
Check-Result 'reject-apple-deletion' {param($r)$r.native.receipts[2].call.arguments.target=$r.native.appleId} $false
Check-Result 'reject-no-live-session' {param($r)$r.native.live.sessions=@()} $false
Check-Result 'reject-replayed-live-session' {param($r)$r.native.live.sessions=@($r.native.live.sessions[0],$r.native.live.sessions[0])} $false
Check-Result 'reject-open-live-session' {param($r)$r.native.live.sessions[0].closed=$false} $false
Check-Result 'reject-string-turn-complete' {param($r)$r.native.live.sessions[0].turnComplete='true'} $false
Check-Result 'reject-no-media-comparison' {param($r)$r.native.mediaChecks=@()} $false
Check-Result 'reject-changed-pcm-hash' {param($r)$r.native.mediaChecks[0].pcmSha256='c'*64} $false
Check-Result 'reject-empty-audio' {param($r)$r.native.mediaChecks[0].samples=0} $false
Check-Result 'reject-no-speech-assets' {param($r)$r.speechAssets=$null} $false
Check-Result 'reject-other-speech-model' {param($r)$r.speechAssets.modelId='other/model'} $false
Check-Result 'reject-unpinned-speech-revision' {param($r)$r.speechAssets.revision='main'} $false
Check-Result 'reject-wrong-pinned-speech-revision' {param($r)$r.speechAssets.revision='7'*40} $false
Check-Result 'reject-wrong-manifest-hash' {param($r)$r.speechAssets.manifestSha256='c'*64} $false
Check-Result 'reject-wrong-total-bytes' {param($r)$r.speechAssets.totalBytes++} $false
Check-Result 'reject-string-total-bytes' {param($r)$r.speechAssets.totalBytes=[string]$r.speechAssets.totalBytes} $false
Check-Result 'reject-non-q4-loader' {param($r)$r.speechAssets.loader.dtype.encoder_model='fp32'} $false
Check-Result 'reject-other-loader-version' {param($r)$r.speechAssets.loader.version='0.0.0'} $false
Check-Result 'reject-other-loader-device' {param($r)$r.speechAssets.loader.device='webgpu'} $false
Check-Result 'reject-incomplete-speech-inventory' {param($r)$r.speechAssets.files=$r.speechAssets.files[0..5]} $false
Check-Result 'reject-duplicate-speech-inventory' {param($r)$r.speechAssets.files[6]=$r.speechAssets.files[0]} $false
Check-Result 'reject-unknown-speech-inventory' {param($r)$r.speechAssets.files[0].path='unknown.json'} $false
Check-Result 'reject-wrong-asset-bytes' {param($r)$r.speechAssets.files[0].bytes++} $false
Check-Result 'reject-string-asset-bytes' {param($r)$r.speechAssets.files[0].bytes=[string]$r.speechAssets.files[0].bytes} $false
Check-Result 'reject-wrong-asset-digest' {param($r)$r.speechAssets.files[0].sha256='c'*64} $false
Check-Result 'reject-missing-file-admission' {param($r)$r.speechAssets.missingFiles=@(@{path='optional.json';status=404})} $false
Check-Result 'reject-null-missing-file-inventory' {param($r)$r.speechAssets.missingFiles=$null} $false
Check-Result 'reject-no-asset-fetches' {param($r)$r.speechAssets.requests=@()} $false
Check-Result 'reject-no-complete-decoder-fetch' {param($r)$r.speechAssets.requests=$r.speechAssets.requests[0..5]+@($r.speechAssets.requests[0],$r.speechAssets.requests[7])} $false
Check-Result 'reject-metadata-only-asset' {param($r)$r.speechAssets.requests[0].range='bytes=0-0';$r.speechAssets.requests[0].status=206;$r.speechAssets.requests[0].bytes=1} $false
Check-Result 'accept-exact-bounded-metadata-get' {param($r)$r.speechAssets.requests+=@{path='config.json';method='GET';range='bytes=0-0';status=206;bytes=1;sha256='c'*64}} $true
Check-Result 'reject-repeated-exact-full-get' {param($r)$r.speechAssets.requests+=@($r.speechAssets.requests[0])} $false
Check-Result 'reject-unknown-asset-fetch' {param($r)$r.speechAssets.requests[0].path='unknown.json'} $false
Check-Result 'reject-asset-post' {param($r)$r.speechAssets.requests[0].method='POST'} $false
Check-Result 'reject-asset-404' {param($r)$r.speechAssets.requests[0].status=404} $false
Check-Result 'reject-wrong-full-get-bytes' {param($r)$r.speechAssets.requests[0].bytes--} $false
Check-Result 'reject-wrong-full-get-digest' {param($r)$r.speechAssets.requests[0].sha256='c'*64} $false
Check-Result 'reject-wrong-metadata-range' {param($r)$r.speechAssets.requests+=@{path='config.json';method='GET';range='bytes=0-1';status=206;bytes=1;sha256='c'*64}} $false
Check-Result 'reject-nonfinite-request-bytes' {param($r)$r.speechAssets.requests[0].bytes=[double]::NaN} $false
Check-Result 'reject-null-worker-evidence' {param($r)$r.native.speechWorkers=$null} $false
Check-Result 'reject-worker-version' {param($r)$r.native.speechWorkers.version=2} $false
Check-Result 'reject-string-worker-version' {param($r)$r.native.speechWorkers.version='1'} $false
Check-Result 'reject-worker-overflow' {param($r)$r.native.speechWorkers.overflow=$true} $false
Check-Result 'reject-string-worker-overflow' {param($r)$r.native.speechWorkers.overflow='false'} $false
Check-Result 'reject-no-worker' {param($r)$r.native.speechWorkers.workers=@()} $false
Check-Result 'reject-unready-worker' {param($r)$r.native.speechWorkers.workers[0].ready=@()} $false
Check-Result 'reject-wrong-ready-model' {param($r)$r.native.speechWorkers.workers[0].ready[0].model='other/model'} $false
Check-Result 'reject-wrong-ready-profile' {param($r)$r.native.speechWorkers.workers[0].ready[0].profile='fp32'} $false
Check-Result 'reject-worker-model-error' {param($r)$r.native.speechWorkers.workers[0].errors=@(@{kind='model-error';message='failed'})} $false
Check-Result 'reject-null-worker-errors' {param($r)$r.native.speechWorkers.workers[0].errors=$null} $false
Check-Result 'reject-duplicate-worker-id' {param($r)$r.native.speechWorkers.workers+=@($r.native.speechWorkers.workers[0])} $false
Check-Result 'reject-invalid-worker-url' {param($r)$r.native.speechWorkers.workers[0].url='http://example.com/assets/observerWhisper.worker-AbCd1234.js'} $false
Check-Result 'reject-other-worker-script' {param($r)$r.native.speechWorkers.workers[0].url='http://127.0.0.1:12543/assets/unrelated.js'} $false
Check-Result 'reject-nonfinite-worker-time' {param($r)$r.native.speechWorkers.workers[0].results.totalElapsedMs=[double]::PositiveInfinity} $false
Check-Result 'reject-nan-worker-time' {param($r)$r.native.speechWorkers.workers[0].results.maxElapsedMs=[double]::NaN} $false
Check-Result 'reject-bool-worker-count' {param($r)$r.native.speechWorkers.workers[0].results.count=$true} $false
Check-Result 'reject-worker-count-overflow' {param($r)$r.native.speechWorkers.workers[0].results.count=1025} $false
Check-Result 'reject-negative-worker-count' {param($r)$r.native.speechWorkers.workers[0].results.count=-1} $false
Check-Result 'reject-max-larger-than-total' {param($r)$r.native.speechWorkers.workers[0].results=@{count=1;totalElapsedMs=1;maxElapsedMs=2}} $false
Check-Result 'reject-time-with-no-results' {param($r)$r.native.speechWorkers.workers[0].results=@{count=0;totalElapsedMs=1;maxElapsedMs=1}} $false
Check-Result 'accept-real-result-metadata-without-transcript' {param($r)$r.native.speechWorkers.workers[0].results=@{count=2;totalElapsedMs=8.5;maxElapsedMs=5.5}} $true
Check-Result 'reject-no-metadata-source-proof' {param($r)$r.speechAssets.metadataFiles=@()} $false
Check-Result 'reject-duplicate-metadata-source-proof' {param($r)$r.speechAssets.metadataFiles+=@($r.speechAssets.metadataFiles[0])} $false
Check-Result 'reject-wrong-metadata-total' {param($r)$r.speechAssets.metadataBytes++} $false
Check-Result 'reject-wrong-metadata-source-path' {param($r)$r.speechAssets.metadataFiles[0].path='onnx/decoder_model_quantized.onnx'} $false
Check-Result 'reject-wrong-metadata-source-size' {param($r)$r.speechAssets.metadataFiles[0].bytes--} $false
Check-Result 'reject-wrong-metadata-source-digest' {param($r)$r.speechAssets.metadataFiles[0].sha256='c'*64} $false
Check-Result 'reject-wrong-metadata-first-byte-digest' {param($r)$r.speechAssets.metadataFiles[0].firstByteSha256='c'*64} $false
Check-Result 'reject-no-metadata-request' {param($r)$r.speechAssets.requests[7]=$r.speechAssets.requests[0]} $false
Check-Result 'reject-full-q8-request' {param($r)$r.speechAssets.requests[7].range=$null;$r.speechAssets.requests[7].status=200;$r.speechAssets.requests[7].bytes=$r.speechAssets.metadataBytes;$r.speechAssets.requests[7].sha256=$r.speechAssets.metadataFiles[0].sha256} $false
Check-Result 'reject-metadata-request-bad-first-byte' {param($r)$r.speechAssets.requests[7].sha256='c'*64} $false
Check-Result 'reject-metadata-request-wrong-range' {param($r)$r.speechAssets.requests[7].range='bytes=0-1';$r.speechAssets.requests[7].bytes=2} $false
Check-Result 'accept-repeated-exact-metadata-only-request' {param($r)$r.speechAssets.requests+=@($r.speechAssets.requests[7])} $true
Check-Result 'reject-no-input-transition' {param($r)$r.native.live.inputTransition=$null} $false
Check-Result 'reject-writing-input-transition' {param($r)$r.native.live.inputTransition.state='writing'} $false
Check-Result 'reject-failed-input-transition' {param($r)$r.native.live.inputTransition.state='failed'} $false
Check-Result 'reject-wrong-captured-pcm-transition' {param($r)$r.native.live.inputTransition.capturedPcmSha256='d'*64} $false
Check-Result 'reject-changed-original-transition' {param($r)$r.native.live.inputTransition.verifiedOriginalSha256='d'*64} $false
Check-Result 'reject-transition-after-turn-completion' {param($r)$r.native.live.inputTransition.completedAt=24001} $false
Check-Result 'reject-transition-before-capture' {param($r)$r.native.live.inputTransition.startedAt=19999} $false
Check-Result 'reject-aliased-active-original-files' {param($r)$r.native.live.inputTransition.file=$r.native.live.fixture.file} $false
Check-Result 'reject-external-original-path' {param($r)$r.native.live.fixture.file=Join-Path $tempParent 'other.wav'} $false
Check-Result 'reject-unbounded-wave-size' {param($r)$r.native.live.fixture.samples++} $false
Check-Result 'reject-missing-retained-wave' {param($r)Remove-Item -LiteralPath $activePath} $false $true
Check-Result 'reject-corrupt-retained-original' {param($r)$changed=[byte[]]$original.Clone();$changed[44]=1;[IO.File]::WriteAllBytes($originalPath,$changed)} $false $true
Check-Result 'reject-nonsilent-active-wave-even-with-matching-report' {param($r)$changed=[byte[]]$silence.Clone();$changed[100]=1;[IO.File]::WriteAllBytes($activePath,$changed);$r.native.live.inputTransition.silenceSha256=Hash-Wave $changed;$r.native.live.inputTransition.verifiedActiveSha256=Hash-Wave $changed} $false $true
Check-Result 'reject-active-track-before-reload' {param($r)$r.native.beforeReloadCapture.tracks[0].readyState='live'} $false
Check-Result 'reject-pending-acquisition-before-reload' {param($r)$r.native.beforeReloadCapture.attempts[0].state='pending'} $false
Check-Result 'reject-reacquired-paused-capture' {param($r)$r.native.beforeReloadCapture.attempts+=@($r.native.beforeReloadCapture.attempts[0])} $false
Check-Result 'reject-restored-pending-acquisition' {param($r)$r.native.restoredCapture.attempts=@(@{audio=$true;video=$false;state='pending'})} $false
Check-Result 'reject-restored-successful-acquisition' {param($r)$r.native.restoredCapture.attempts=@(@{audio=$true;video=$false;state='resolved'})} $false
Check-Result 'reject-restored-track-even-ended' {param($r)$r.native.restoredCapture.tracks=@($r.native.releasedCapture.tracks[0])} $false
Check-Result 'accept-only-rejected-restored-acquisition' {param($r)$r.native.restoredCapture.attempts=@(@{audio=$true;video=$false;state='rejected'})} $true
Check-Result 'reject-missing-settlements' {param($r)$r.networkSettlements=@()} $false
Check-Result 'reject-reordered-settlement-stages' {param($r)$r.networkSettlements[1].stage='reload'} $false
Check-Result 'reject-short-settlement-quiet-period' {param($r)$r.networkSettlements[0].quietMs=499} $false
Check-Result 'reject-wrong-settlement-manifest' {param($r)$r.networkSettlements[0].manifestSha256='d'*64} $false
Check-Result 'reject-missing-pending-request-proof' {param($r)$r.networkSettlements[0].pendingMetadata=@()} $false
Check-Result 'reject-duplicate-pending-request-id' {param($r)$r.networkSettlements[0].observedPendingIds=@(1,1);$r.networkSettlements[0].pendingMetadata+=@($r.networkSettlements[0].pendingMetadata[0])} $false
Check-Result 'reject-missing-original-network-event' {param($r)$r.networkEvents=@()} $false
Check-Result 'reject-changed-original-network-event' {param($r)$r.networkEvents[0].method='POST'} $false
Check-Result 'reject-full-get-as-pending-metadata' {param($r)$r.networkSettlements[0].pendingMetadata[0].receipt.range=$null;$r.networkSettlements[0].pendingMetadata[0].receipt.status=200} $false
Check-Result 'reject-unproven-pending-first-byte' {param($r)$r.networkSettlements[0].pendingMetadata[0].receipt.sha256='d'*64} $false
Check-Result 'reject-nonmetadata-pending-observation' {param($r)$r.networkSettlements[0].observedPendingIds=@(1,2)} $false
Check-Result 'reject-event-finished-before-pending-settlement' {param($r)$r.networkEvents[0].endedAt=9500} $false
Check-Result 'reject-unready-settlement-worker' {param($r)$r.networkSettlements[0].workers.workers[0].ready=@()} $false
Check-Result 'accept-all-metadata-already-finished' {param($r)$r.networkEvents[0].endedAt=9500;foreach($s in $r.networkSettlements){$s.pendingMetadata=@();$s.observedPendingIds=@()}} $true
Check-Result 'accept-exact-late-cleanup-metadata-abort' {param($r)$r.networkEvents[0].state='failed';$r.networkEvents[0].error='net::ERR_ABORTED';$r.networkFailures=@($r.networkEvents[0])} $true
Check-Result 'reject-admitted-failure-before-cleanup' {param($r)$r.networkEvents[0].state='failed';$r.networkEvents[0].error='net::ERR_FAILED';$r.networkEvents[0].endedDuringCleanup=$false;$r.networkFailures=@($r.networkEvents[0])} $false
# These are synthetic retained-evidence fixtures, not network requests. The
# original redirect and failed delivery stay separate and unchanged.
function Add-ReconciledTransfer($r){
 $receipt=$r.speechAssets.requests[0]
 $source=[pscustomobject]@{id=2;stage='first-message';startedAt=8000;method='GET';origin='https://huggingface.co';path=('/'+$manifest.modelId+'/resolve/main/'+$receipt.path);type='fetch';state='finished';status=307;endedAt=8100;endedStage='first-message';endedDuringCleanup=$false;error=$null;redirectedFromId=$null}
 $delivery=[pscustomobject]@{id=3;stage='first-message';startedAt=8050;method='GET';origin='http://127.0.0.1:19000';path=('/11111111-1111-4111-8111-111111111111/'+$receipt.path);type='fetch';state='failed';status=200;endedAt=9300;endedStage='first-message';endedDuringCleanup=$false;error='net::ERR_ABORTED';redirectedFromId=2}
 $r.networkEvents+=@($source,$delivery);$r.networkFailures+=@($delivery)
 $row=$delivery | ConvertTo-Json | ConvertFrom-Json
 $row | Add-Member -NotePropertyName receipt -NotePropertyValue ($receipt | ConvertTo-Json | ConvertFrom-Json)
 $row | Add-Member -NotePropertyName cacheVerifiedAt -NotePropertyValue 9500
 $row | Add-Member -NotePropertyName reason -NotePropertyValue 'verified-browser-cache-after-full-delivery'
 $r.networkReconciliations+=@($row)
}
Check-Result 'accept-exact-full-delivery-cache-reconciliation' {param($r)Add-ReconciledTransfer $r} $true
Check-Result 'accept-cache-read-before-late-chromium-abort' {param($r)Add-ReconciledTransfer $r;$r.networkEvents[2].endedAt=9800;$r.networkReconciliations[0].endedAt=9800} $true
Check-Result 'reject-no-browser-cache-proofs' {param($r)$r.browserCacheVerifications=@()} $false
Check-Result 'reject-only-initial-browser-cache-proof' {param($r)$r.browserCacheVerifications=@($r.browserCacheVerifications[0])} $false
Check-Result 'reject-cache-name' {param($r)$r.browserCacheVerifications[0].cacheName='other-cache'} $false
Check-Result 'reject-cache-other-model' {param($r)$r.browserCacheVerifications[0].modelId='other/model'} $false
Check-Result 'reject-cache-other-manifest' {param($r)$r.browserCacheVerifications[0].manifestSha256='c'*64} $false
Check-Result 'reject-cache-missing-file' {param($r)$r.browserCacheVerifications[0].files=$r.browserCacheVerifications[0].files[0..5]} $false
Check-Result 'reject-cache-duplicate-file' {param($r)$r.browserCacheVerifications[0].files[6]=$r.browserCacheVerifications[0].files[0]} $false
Check-Result 'reject-cache-wrong-bytes' {param($r)$r.browserCacheVerifications[1].files[0].bytes--} $false
Check-Result 'reject-cache-wrong-digest' {param($r)$r.browserCacheVerifications[1].files[0].sha256='c'*64} $false
Check-Result 'reject-cache-proof-before-current-run' {param($r)$r.browserCacheVerifications[0].verifiedAt=8000;$r.networkSettlements[0].cacheVerifiedAt=8000;$r.networkSettlements[1].cacheVerifiedAt=8000} $false
Check-Result 'reject-reused-cache-proof-time' {param($r)$r.browserCacheVerifications[1].verifiedAt=9500;$r.networkSettlements[2].cacheVerifiedAt=9500} $false
Check-Result 'reject-initial-cache-proof-on-reload' {param($r)$r.networkSettlements[2].cacheVerifiedAt=9500} $false
Check-Result 'reject-cache-proof-after-settlement' {param($r)$r.browserCacheVerifications[1].verifiedAt=40001;$r.networkSettlements[2].cacheVerifiedAt=40001} $false
Check-Result 'reject-no-full-attempts' {param($r)$r.speechAssets.fullAttempts=@()} $false
Check-Result 'reject-repeat-full-attempt' {param($r)$r.speechAssets.fullAttempts[0].count=2} $false
Check-Result 'reject-duplicate-full-attempt-path' {param($r)$r.speechAssets.fullAttempts[6].path=$r.speechAssets.fullAttempts[0].path} $false
Check-Result 'reject-string-full-attempt-count' {param($r)$r.speechAssets.fullAttempts[0].count='1'} $false
Check-Result 'reject-no-reconciliation-for-raw-failure' {param($r)Add-ReconciledTransfer $r;$r.networkReconciliations=@()} $false
Check-Result 'reject-duplicate-reconciliation' {param($r)Add-ReconciledTransfer $r;$r.networkReconciliations+=@($r.networkReconciliations[0])} $false
Check-Result 'reject-removed-raw-failure' {param($r)Add-ReconciledTransfer $r;$r.networkFailures=@();$r.networkReconciliations=@()} $false
Check-Result 'reject-relabelled-failed-event' {param($r)Add-ReconciledTransfer $r;$r.networkEvents[2].state='finished'} $false
Check-Result 'reject-reconciliation-changed-raw-error' {param($r)Add-ReconciledTransfer $r;$r.networkReconciliations[0].error='net::ERR_FAILED'} $false
Check-Result 'reject-reconciliation-other-reason' {param($r)Add-ReconciledTransfer $r;$r.networkReconciliations[0].reason='URL-allowed'} $false
Check-Result 'reject-full-delivery-other-error' {param($r)Add-ReconciledTransfer $r;$r.networkEvents[2].error='net::ERR_FAILED';$r.networkReconciliations[0].error='net::ERR_FAILED'} $false
Check-Result 'reject-full-delivery-other-status' {param($r)Add-ReconciledTransfer $r;$r.networkEvents[2].status=204;$r.networkReconciliations[0].status=204} $false
Check-Result 'reject-full-delivery-external-origin' {param($r)Add-ReconciledTransfer $r;$r.networkEvents[2].origin='http://example.com:19000';$r.networkReconciliations[0].origin='http://example.com:19000'} $false
Check-Result 'reject-full-delivery-wrong-ticket-path' {param($r)Add-ReconciledTransfer $r;$r.networkEvents[2].path='/config.json';$r.networkReconciliations[0].path='/config.json'} $false
Check-Result 'reject-missing-source-redirect-id' {param($r)Add-ReconciledTransfer $r;$r.networkEvents[2].redirectedFromId=77;$r.networkReconciliations[0].redirectedFromId=77} $false
Check-Result 'reject-source-not-redirect' {param($r)Add-ReconciledTransfer $r;$r.networkEvents[1].status=200} $false
Check-Result 'reject-source-other-file' {param($r)Add-ReconciledTransfer $r;$r.networkEvents[1].path='/onnx-community/whisper-tiny.en/resolve/main/tokenizer.json'} $false
Check-Result 'reject-source-other-host' {param($r)Add-ReconciledTransfer $r;$r.networkEvents[1].origin='https://other.example'} $false
Check-Result 'reject-reconciliation-receipt-size' {param($r)Add-ReconciledTransfer $r;$r.networkReconciliations[0].receipt.bytes--} $false
Check-Result 'reject-reconciliation-receipt-hash' {param($r)Add-ReconciledTransfer $r;$r.networkReconciliations[0].receipt.sha256='c'*64} $false
Check-Result 'reject-reconciliation-range-receipt' {param($r)Add-ReconciledTransfer $r;$r.networkReconciliations[0].receipt.range='bytes=0-0'} $false
Check-Result 'reject-reconciliation-unknown-cache-proof' {param($r)Add-ReconciledTransfer $r;$r.networkReconciliations[0].cacheVerifiedAt=9400} $false
Check-Result 'reject-omission-from-both-pending-lists' {param($r)$r.networkSettlements[0].observedPendingIds=@();$r.networkSettlements[0].pendingMetadata=@()} $false
Check-Result 'accept-same-millisecond-start-not-proven-pending' {param($r)$r.networkEvents+=@([pscustomobject]@{id=2;startedAt=10000;endedAt=11000;state='finished'})} $true
Check-Result 'accept-same-millisecond-end-not-proven-pending' {param($r)$r.networkEvents+=@([pscustomobject]@{id=2;startedAt=9500;endedAt=10000;state='finished'})} $true
Check-Result 'reject-unreported-normal-request-spanning-settlement' {param($r)$r.networkEvents+=@([pscustomobject]@{id=2;startedAt=9500;endedAt=11000;state='finished'})} $false
$report=@{status='passed';mode='PowerShell AST parse and extracted pure guards/result validator; wrapper not executed';sourceSha256=(Get-FileHash -LiteralPath $gate).Hash;wrapperSha256=(Get-FileHash -LiteralPath $path).Hash;parseErrors=$parseErrors.Count;cases=$cases}
if($ReportPath){$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ReportPath -Encoding utf8}
Write-Output "Passed $($cases.Count) static wrapper contracts; no runtime launched."
} finally {
 if(Test-Path -LiteralPath $temporary){
  $resolved=[IO.Path]::GetFullPath($temporary)
  if($resolved -cne (Join-Path $tempParent $tempName) -or [IO.Path]::GetDirectoryName($resolved) -cne $tempParent -or [IO.Path]::GetFileName($resolved) -cnotmatch '^maestro-live-continuity-[a-f0-9]{32}$'){throw 'Refusing cleanup outside the owned temporary contract folder.'}
  Remove-Item -LiteralPath $resolved -Recurse -Force
 }
}
