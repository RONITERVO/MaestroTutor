# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
# Pure retained-evidence validation; no Editor, browser, provider or device use.
function Assert-QuestLiveContinuityResult($Result,[string]$ProbeId,[string]$EvidenceDirectory,[string]$SpeechManifestPath=(Join-Path $PSScriptRoot '../../test-fixtures/models/whisper-tiny-en.json')){
 $native=$Result.native
 # Match the exact committed asset contract, not only a claimed successful load.
 $manifest=Get-Content -LiteralPath $SpeechManifestPath -Raw | ConvertFrom-Json
 $manifestHash=(Get-FileHash -LiteralPath $SpeechManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
 $model='onnx-community/whisper-tiny.en'
 $required=@('config.json','generation_config.json','tokenizer.json','tokenizer_config.json','preprocessor_config.json','onnx/encoder_model_q4.onnx','onnx/decoder_model_merged_q4.onnx')
 $number={param($value) ($value -is [int] -or $value -is [long] -or $value -is [double] -or $value -is [decimal]) -and ![double]::IsNaN([double]$value) -and ![double]::IsInfinity([double]$value) -and $value -ge 0}
 $integer={param($value) (& $number $value) -and [math]::Floor([double]$value) -eq $value -and $value -le 9007199254740991}
 if($manifest.version -ne 1 -or $manifest.modelId -cne $model -or $manifest.revision -cnotmatch '^[a-f0-9]{40}$' -or $manifest.files -isnot [array] -or $manifest.files.Count -ne 7 -or $manifest.missingFiles -isnot [array] -or $manifest.missingFiles.Count -ne 0){throw "Live continuity requires the pinned complete q4 manifest. Evidence: $EvidenceDirectory"}
 $expected=@{};$expectedTotal=0L
 foreach($file in $manifest.files){
  if($file.path -cnotin $required -or $expected.ContainsKey($file.path) -or !(& $integer $file.size) -or $file.size -le 0 -or $file.sha256 -cnotmatch '^[a-f0-9]{64}$'){throw "Live continuity manifest has invalid or duplicate assets. Evidence: $EvidenceDirectory"}
  $expected[$file.path]=$file;$expectedTotal+=$file.size
 }
 $metadataPath='onnx/encoder_model_quantized.onnx'
 if($manifest.metadataFiles -isnot [array] -or $manifest.metadataFiles.Count -ne 1){throw "Live continuity requires its separate pinned metadata source. Evidence: $EvidenceDirectory"}
 $metadataEntry=$manifest.metadataFiles[0]
 if($metadataEntry.path -cne $metadataPath -or !(& $integer $metadataEntry.size) -or $metadataEntry.size -le 0 -or $metadataEntry.sha256 -cnotmatch '^[a-f0-9]{64}$' -or $metadataEntry.firstByteSha256 -cnotmatch '^[a-f0-9]{64}$' -or !(& $integer $manifest.metadataBytes) -or $manifest.metadataBytes -ne $metadataEntry.size){throw "Live continuity metadata source has invalid provenance. Evidence: $EvidenceDirectory"}
 $assets=$Result.speechAssets
 if($assets.modelId -cne $model -or $assets.revision -cne $manifest.revision -or $assets.manifestSha256 -cne $manifestHash -or !(& $integer $assets.totalBytes) -or $assets.totalBytes -ne $expectedTotal -or $manifest.totalBytes -ne $expectedTotal){throw "Live continuity speech assets differ from the committed model, revision, manifest or byte count. Evidence: $EvidenceDirectory"}
 if($assets.metadataFiles -isnot [array] -or $assets.metadataFiles.Count -ne 1 -or !(& $integer $assets.metadataBytes) -or $assets.metadataBytes -ne $manifest.metadataBytes){throw "Live continuity metadata inventory is missing or mismatched. Evidence: $EvidenceDirectory"}
 $metadataProof=$assets.metadataFiles[0]
 if($metadataProof.path -cne $metadataPath -or !(& $integer $metadataProof.bytes) -or $metadataProof.bytes -ne $metadataEntry.size -or $metadataProof.sha256 -cne $metadataEntry.sha256 -or $metadataProof.firstByteSha256 -cne $metadataEntry.firstByteSha256){throw "Live continuity metadata bytes differ from the verified complete source. Evidence: $EvidenceDirectory"}
 if($assets.loader.package -cne '@huggingface/transformers' -or $assets.loader.version -cne $manifest.loader.version -or $assets.loader.device -cne 'wasm' -or $assets.loader.dtype.encoder_model -cne 'q4' -or $assets.loader.dtype.decoder_model_merged -cne 'q4'){throw "Live continuity speech loader is not the pinned q4 WASM path. Evidence: $EvidenceDirectory"}
 if($assets.files -isnot [array] -or $assets.files.Count -ne 7 -or $assets.missingFiles -isnot [array] -or $assets.missingFiles.Count -ne 0){throw "Live continuity speech inventory is incomplete or admits missing files. Evidence: $EvidenceDirectory"}
 $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
 foreach($file in $assets.files){
  if($file.path -cnotin $required -or !$seen.Add($file.path)){throw "Live continuity speech inventory has an unknown or repeated file. Evidence: $EvidenceDirectory"}
  $entry=$expected[$file.path]
  if(!(& $integer $file.bytes) -or $file.bytes -ne $entry.size -or $file.sha256 -cne $entry.sha256){throw "Live continuity speech inventory does not match verified bytes. Evidence: $EvidenceDirectory"}
 }
 if($assets.requests -isnot [array] -or $assets.requests.Count -lt 8 -or $assets.requests.Count -gt 256){throw "Live continuity lacks bounded actual speech file requests. Evidence: $EvidenceDirectory"}
 $fetched=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal);$metadataFetched=$false
 foreach($request in $assets.requests){
  if(($request.path -cnotin $required -and $request.path -cne $metadataPath) -or $request.method -cne 'GET' -or !(& $integer $request.status) -or !(& $integer $request.bytes) -or $request.sha256 -cnotmatch '^[a-f0-9]{64}$'){throw "Live continuity contains an unapproved speech asset request. Evidence: $EvidenceDirectory"}
  if($request.path -ceq $metadataPath){
   if($request.range -cne 'bytes=0-0' -or $request.status -ne 206 -or $request.bytes -ne 1 -or $request.sha256 -cne $metadataEntry.firstByteSha256){throw "Live continuity fetched unapproved metadata bytes or admitted a full q8 download. Evidence: $EvidenceDirectory"}
   $metadataFetched=$true
  }elseif($null -eq $request.range){
   $entry=$expected[$request.path]
   if($request.status -ne 200 -or $request.bytes -ne $entry.size -or $request.sha256 -cne $entry.sha256){throw "Live continuity did not fetch exact complete speech asset bytes. Evidence: $EvidenceDirectory"}
   [void]$fetched.Add($request.path)
  }elseif($request.range -cne 'bytes=0-0' -or $request.status -ne 206 -or $request.bytes -ne 1){throw "Live continuity contains an unapproved speech metadata range. Evidence: $EvidenceDirectory"}
 }
 if(!$metadataFetched){throw "Live continuity never performed its exact metadata-only request. Evidence: $EvidenceDirectory"}
 if($fetched.Count -ne 7){throw "Live continuity did not fetch all seven complete speech assets. Evidence: $EvidenceDirectory"}
 $speechWorkers=$native.speechWorkers
 if(!(& $integer $speechWorkers.version) -or $speechWorkers.version -ne 1 -or $speechWorkers.overflow -isnot [bool] -or $speechWorkers.overflow -ne $false -or $speechWorkers.workers -isnot [array] -or $speechWorkers.workers.Count -lt 1 -or $speechWorkers.workers.Count -gt 4){throw "Live continuity lacks bounded actual speech-worker evidence. Evidence: $EvidenceDirectory"}
 $readyCount=0;$workerIds=[Collections.Generic.HashSet[long]]::new();$workerOrigin=$null
 foreach($worker in $speechWorkers.workers){
  $workerUri=$null
  if(!(& $integer $worker.id) -or $worker.id -lt 1 -or $worker.id -gt 4 -or !$workerIds.Add($worker.id) -or $worker.url -isnot [string] -or ![uri]::TryCreate($worker.url,[UriKind]::Absolute,[ref]$workerUri) -or $workerUri.Scheme -cne 'http' -or !$workerUri.IsLoopback -or $workerUri.UserInfo -or $workerUri.Query -or $workerUri.Fragment -or $workerUri.AbsolutePath -cnotmatch '^/assets/observerWhisper\.worker-[A-Za-z0-9_-]+\.js$'){throw "Live continuity contains an invalid or repeated production speech worker. Evidence: $EvidenceDirectory"}
  if($null -eq $workerOrigin){$workerOrigin=$workerUri.Authority}elseif($workerOrigin -cne $workerUri.Authority){throw "Live continuity speech workers do not share one local application origin. Evidence: $EvidenceDirectory"}
  if($worker.ready -isnot [array] -or $worker.ready.Count -gt 8 -or $worker.errors -isnot [array] -or $worker.errors.Count -ne 0){throw "Live continuity speech worker readiness overflowed or reported an error. Evidence: $EvidenceDirectory"}
  foreach($ready in $worker.ready){
   if($ready.model -cne $model -or $ready.profile -cne 'q4'){throw "Live continuity speech worker loaded an unexpected model or profile. Evidence: $EvidenceDirectory"}
   $readyCount++
  }
  $metrics=$worker.results
  if(!(& $integer $metrics.count) -or $metrics.count -gt 1024 -or !(& $number $metrics.totalElapsedMs) -or !(& $number $metrics.maxElapsedMs) -or $metrics.maxElapsedMs -gt $metrics.totalElapsedMs -or ($metrics.count -eq 0 -and ($metrics.totalElapsedMs -ne 0 -or $metrics.maxElapsedMs -ne 0))){throw "Live continuity speech worker metrics are malformed. Evidence: $EvidenceDirectory"}
 }
 if($readyCount -lt 1){throw "Live continuity never observed the real q4 speech worker become ready. Evidence: $EvidenceDirectory"}
 if($Result.status -cne 'passed' -or $Result.nativeProbeId -cne $ProbeId -or $native.nativeProbeId -cne $ProbeId){throw "Live continuity result does not belong to the successful owned probe. Evidence: $EvidenceDirectory"}
 foreach($value in @($Result.nativeExecution,$Result.productionComposition,$native.qualified)){
  if($value -isnot [bool] -or $value -ne $true){throw "Live continuity lacks qualified production/native execution. Evidence: $EvidenceDirectory"}
 }
 foreach($value in @($Result.realProvider,$Result.physicalDevice,$native.realProvider,$native.physicalDevice)){
  if($value -isnot [bool] -or $value -ne $false){throw "Live continuity must use the offline provider and no physical device. Evidence: $EvidenceDirectory"}
 }
 if($native.treeId -isnot [string] -or $native.appleId -isnot [string] -or $native.treeId -cnotmatch '^[a-f0-9]{32}$' -or $native.appleId -cnotmatch '^[a-f0-9]{32}$' -or $native.treeId -ceq $native.appleId){throw "Live continuity needs distinct exact tree and apple identities. Evidence: $EvidenceDirectory"}
 $receipts=@($native.receipts)
 if($receipts.Count -ne 3){throw "Live continuity must retain the three completed native action receipts. Evidence: $EvidenceDirectory"}
 for($i=0;$i -lt $receipts.Count;$i++){
  $receipt=$receipts[$i];$capability=$(if($i -lt 2){'object.create'}else{'object.delete'})
  if($receipt.phase -cne 'completed' -or $receipt.id -cnotmatch '^[a-f0-9]{32}$' -or $receipt.capability -cne $capability -or $receipt.call.id -cne $capability){throw "Live continuity contains a missing, wrong or unfinished native receipt. Evidence: $EvidenceDirectory"}
 }
 if(@($receipts.id | Select-Object -Unique).Count -ne 3 -or $receipts[0].output.objectId -cne $native.treeId -or $receipts[1].output.objectId -cne $native.appleId -or $receipts[2].call.arguments.target -cne $native.treeId){throw "Live continuity receipts do not prove creation of both identities and deletion of only the tree. Evidence: $EvidenceDirectory"}
 $sessions=@($native.live.sessions);$media=@($native.mediaChecks)
 if($sessions.Count -ne 1 -or $media.Count -lt 1){throw "Live continuity lacks its captured Live turn or delegated media comparisons. Evidence: $EvidenceDirectory"}
 $session=$sessions[0]
 if($session.starts -ne 1 -or $session.ends -ne 1 -or $session.turnComplete -isnot [bool] -or !$session.turnComplete -or $session.closed -isnot [bool] -or !$session.closed -or $session.error -or $session.pcmSha256 -cnotmatch '^[a-f0-9]{64}$'){throw "Live continuity did not finish one clean captured turn. Evidence: $EvidenceDirectory"}
 foreach($comparison in $media){
  if($comparison.pcmSha256 -cne $session.pcmSha256 -or $comparison.samples -le 0 -or $comparison.packets -le 0 -or $comparison.wavSha256 -cnotmatch '^[a-f0-9]{64}$'){throw "Live continuity lacks matching captured and delegated audio. Evidence: $EvidenceDirectory"}
 }
 # One finite synthetic input becomes silence before the completed turn. Read
 # only the two exact retained files, each bounded to a 30-second PCM WAV.
 $fixture=$native.live.fixture;$transition=$native.live.inputTransition
 if($transition.state -cne 'silence' -or !(& $integer $fixture.sampleRate) -or $fixture.sampleRate -notin @(24000,48000) -or !(& $integer $fixture.samples) -or $fixture.samples -ne 30*$fixture.sampleRate -or $fixture.sha256 -cnotmatch '^[a-f0-9]{64}$'){throw 'Live continuity lacks a completed finite microphone transition.'}
 $waveLength=44+2*$fixture.samples
 foreach($field in @('originalFixtureSha256','previousSha256','verifiedOriginalSha256')){if($transition.$field -cne $fixture.sha256){throw 'The original microphone fixture was not preserved.'}}
 if($transition.silenceSha256 -cnotmatch '^[a-f0-9]{64}$' -or $transition.silenceSha256 -ceq $fixture.sha256 -or $transition.verifiedActiveSha256 -cne $transition.silenceSha256 -or $transition.capturedPcmSha256 -cne $session.pcmSha256 -or $transition.sampleRate -ne $fixture.sampleRate -or $transition.samples -ne $fixture.samples -or $transition.bytes -ne $waveLength){throw 'Finite microphone transition identity or format differs from its captured turn.'}
 $previousTime=0
 foreach($time in @($session.capturedAt,$transition.startedAt,$transition.completedAt,$session.completedAt,$session.closedAt)){
  if(!(& $integer $time) -or $time -le 0 -or $time -lt $previousTime){throw 'Microphone silence was not verified between capture and turn completion.'};$previousTime=$time
 }
 $waveFolder=[IO.Path]::GetFullPath((Join-Path $EvidenceDirectory 'first-learner'))
 $originalPath=Join-Path $waveFolder 'synthetic-live-microphone.wav';$activePath=Join-Path $waveFolder 'synthetic-live-microphone-input.wav'
 if($fixture.file -isnot [string] -or $transition.file -isnot [string] -or [IO.Path]::GetFullPath($fixture.file) -cne $originalPath -or [IO.Path]::GetFullPath($transition.file) -cne $activePath){throw 'Microphone evidence does not name its two distinct owned files.'}
 $folderInfo=Get-Item -LiteralPath $waveFolder
 if(!$folderInfo.PSIsContainer -or ($folderInfo.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Microphone evidence folder is not an ordinary owned directory.'}
 $readWave={param([string]$Path,[int]$Length)
  $item=Get-Item -LiteralPath $Path
  if($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $item.Length -ne $Length){throw 'Retained microphone WAV has an invalid bounded size or file type.'}
  $data=New-Object byte[] $Length;$stream=[IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
  try{$offset=0;while($offset -lt $Length){$count=$stream.Read($data,$offset,$Length-$offset);if($count -eq 0){throw 'Retained microphone WAV ended early.'};$offset+=$count};if($stream.ReadByte() -ne -1){throw 'Retained microphone WAV grew beyond its declared size.'}}finally{$stream.Dispose()}
  return ,$data
 }
 $hashWave={param([byte[]]$Bytes)$algorithm=[Security.Cryptography.SHA256]::Create();try{return [BitConverter]::ToString($algorithm.ComputeHash($Bytes)).Replace('-','').ToLowerInvariant()}finally{$algorithm.Dispose()}}
 $original=& $readWave $originalPath $waveLength;$active=& $readWave $activePath $waveLength
 if((& $hashWave $original) -cne $fixture.sha256 -or (& $hashWave $active) -cne $transition.silenceSha256){throw 'Retained microphone bytes do not match their verified transition.'}
 if([Text.Encoding]::ASCII.GetString($original,0,4) -cne 'RIFF' -or [BitConverter]::ToUInt32($original,4) -ne $waveLength-8 -or [Text.Encoding]::ASCII.GetString($original,8,8) -cne 'WAVEfmt ' -or [BitConverter]::ToUInt32($original,16) -ne 16 -or [BitConverter]::ToUInt16($original,20) -ne 1 -or [BitConverter]::ToUInt16($original,22) -ne 1 -or [BitConverter]::ToUInt32($original,24) -ne $fixture.sampleRate -or [BitConverter]::ToUInt32($original,28) -ne 2*$fixture.sampleRate -or [BitConverter]::ToUInt16($original,32) -ne 2 -or [BitConverter]::ToUInt16($original,34) -ne 16 -or [Text.Encoding]::ASCII.GetString($original,36,4) -cne 'data' -or [BitConverter]::ToUInt32($original,40) -ne $waveLength-44){throw 'Retained microphone fixture is not canonical mono PCM16.'}
 $silence=New-Object byte[] $waveLength;[Array]::Copy($original,$silence,44)
 if((& $hashWave $silence) -cne $transition.silenceSha256){throw 'The active microphone WAV still contains signal or changed its header.'}
 # A later permission reset must not erase pending or reacquired microphone use.
 $released=$native.releasedCapture;$beforeReload=$native.beforeReloadCapture;$restored=$native.restoredCapture
 foreach($capture in @($released,$beforeReload)){
  if($capture.tracks -isnot [array] -or $capture.tracks.Count -lt 1 -or $capture.tracks.Count -gt 256 -or $capture.attempts -isnot [array] -or $capture.attempts.Count -gt 256){throw 'Live continuity lacks bounded released capture evidence.'}
  foreach($track in $capture.tracks){if($track.kind -cne 'audio' -or $track.readyState -cne 'ended' -or $track.enabled -isnot [bool]){throw 'Live continuity retains an active or non-audio track.'}}
  foreach($attempt in $capture.attempts){if($attempt.audio -isnot [bool] -or !$attempt.audio -or $attempt.video -isnot [bool] -or $attempt.video -or $attempt.state -cnotin @('resolved','rejected')){throw 'Live continuity has unresolved or unexpected capture acquisition.'}}
 }
 foreach($property in @('tracks','attempts')){
  if($released.$property.Count -ne $beforeReload.$property.Count){throw 'Capture was reacquired after its initial release.'}
  for($i=0;$i -lt $released.$property.Count;$i++){
   $fields=$(if($property -eq 'tracks'){@('kind','readyState','enabled')}else{@('audio','video','state')})
   foreach($field in $fields){if($released.$property[$i].$field -cne $beforeReload.$property[$i].$field){throw 'Capture changed before permission withdrawal.'}}
  }
 }
 if($restored.tracks -isnot [array] -or $restored.tracks.Count -ne 0 -or $restored.attempts -isnot [array] -or $restored.attempts.Count -gt 256){throw 'Reload erased or reacquired microphone tracks.'}
 foreach($attempt in $restored.attempts){if($attempt.audio -isnot [bool] -or !$attempt.audio -or $attempt.video -isnot [bool] -or $attempt.video -or $attempt.state -cne 'rejected'){throw 'Reload has a pending or successful microphone acquisition.'}}
 # Pending metadata is an exact fulfilled request exception, never a URL-wide
 # network-idle exemption. Final events may settle later during browser cleanup.
 $settlements=$Result.networkSettlements;$events=$Result.networkEvents;$failures=$Result.networkFailures
 $cacheProofs=$Result.browserCacheVerifications;$reconciliations=$Result.networkReconciliations
 if($settlements -isnot [array] -or $settlements.Count -ne 3 -or $events -isnot [array] -or $events.Count -gt 5000 -or $failures -isnot [array] -or $failures.Count -gt 5000){throw 'Live continuity lacks bounded network settlement evidence.'}
 # A browser HTTP abort is reconciled only with the real cache bytes and the
 # exact sole-attempt server receipt. Raw events/failures remain unchanged.
 if($cacheProofs -isnot [array] -or $cacheProofs.Count -ne 2 -or $reconciliations -isnot [array] -or $reconciliations.Count -gt 7 -or $assets.fullAttempts -isnot [array] -or $assets.fullAttempts.Count -ne 7){throw 'Live continuity lacks bounded browser-cache or sole-attempt delivery evidence.'}
 $attempts=@{}
 foreach($attempt in $assets.fullAttempts){
  if($attempt.path -cnotin $required -or $attempts.ContainsKey($attempt.path) -or !(& $integer $attempt.count) -or $attempt.count -ne 1){throw 'Full speech delivery was repeated or has unknown attempt provenance.'}
  $attempts[$attempt.path]=$attempt.count
  $delivered=@($assets.requests | Where-Object {$_.path -ceq $attempt.path -and $_.range -eq $null -and $_.status -eq 200})
  if($delivered.Count -ne 1){throw 'Sole-attempt speech delivery does not have exactly one completed receipt.'}
 }
 $cacheByTime=@{};$previousCacheAt=0
 foreach($proof in $cacheProofs){
  if($proof.cacheName -cne 'transformers-cache' -or $proof.modelId -cne $model -or $proof.manifestSha256 -cne $manifestHash -or !(& $integer $proof.verifiedAt) -or $proof.verifiedAt -le $previousCacheAt -or $proof.files -isnot [array] -or $proof.files.Count -ne 7){throw 'Browser-cache proof has the wrong model, manifest, time or inventory.'}
  $cacheFiles=@{}
  foreach($file in $proof.files){
   if($file.path -cnotin $required -or $cacheFiles.ContainsKey($file.path) -or !(& $integer $file.bytes) -or $file.bytes -ne $expected[$file.path].size -or $file.sha256 -cne $expected[$file.path].sha256){throw 'Browser-consumed speech bytes differ from the complete pinned manifest.'}
   $cacheFiles[$file.path]=$file
  }
  $cacheByTime[[string]$proof.verifiedAt]=$cacheFiles;$previousCacheAt=$proof.verifiedAt
 }
 $eventById=@{}
 $firstRequestAt=[double]::PositiveInfinity
 foreach($event in $events){
  if(!(& $integer $event.id) -or $event.id -lt 1 -or $eventById.ContainsKey([string]$event.id) -or !(& $integer $event.startedAt) -or $event.startedAt -le 0 -or $event.state -cnotin @('pending','finished','failed')){throw 'Network event identity or time is missing, invalid or duplicated.'}
  if(($event.state -ceq 'pending' -and $null -ne $event.endedAt) -or ($event.state -cne 'pending' -and (!(& $integer $event.endedAt) -or $event.endedAt -lt $event.startedAt))){throw 'Network event end does not match its recorded state.'}
  $eventById[[string]$event.id]=$event;$firstRequestAt=[math]::Min($firstRequestAt,$event.startedAt)
 }
 if($cacheProofs[0].verifiedAt -lt $firstRequestAt){throw 'Browser-cache evidence predates this fresh network run.'}

 $stageNames=@('first-message','native-room','reload');$lastAt=0;$lastGeneration=0
 for($i=0;$i -lt 3;$i++){
  $settlement=$settlements[$i]
  if($settlement.stage -cne $stageNames[$i] -or !(& $integer $settlement.at) -or $settlement.at -le 0 -or $settlement.at -lt $lastAt -or !(& $integer $settlement.elapsedMs) -or !(& $integer $settlement.quietMs) -or $settlement.quietMs -lt 500 -or $settlement.quietMs -gt $settlement.elapsedMs -or !(& $integer $settlement.networkGeneration) -or $settlement.networkGeneration -lt $lastGeneration -or $settlement.manifestSha256 -cne $manifestHash -or $settlement.fullFiles -ne 7){throw 'Network settlement stage, quiet interval or model provenance is invalid.'}
  $lastAt=$settlement.at;$lastGeneration=$settlement.networkGeneration
  $expectedCacheAt=$cacheProofs[$(if($i -lt 2){0}else{1})].verifiedAt
  if(!(& $integer $settlement.cacheVerifiedAt) -or $settlement.cacheVerifiedAt -ne $expectedCacheAt -or $settlement.cacheVerifiedAt -gt $settlement.at){throw 'Network settlement did not use its current document browser-cache proof.'}
  $observed=$settlement.observedPendingIds;$pendingMetadata=$settlement.pendingMetadata
  if($observed -isnot [array] -or $pendingMetadata -isnot [array] -or $observed.Count -gt 256 -or $pendingMetadata.Count -ne $observed.Count){throw 'Network settlement omits pending requests.'}
  $pendingIds=[Collections.Generic.HashSet[long]]::new()
  foreach($id in $observed){if(!(& $integer $id) -or $id -lt 1 -or !$pendingIds.Add($id)){throw 'Observed pending network identities are invalid or duplicated.'}}
  # Timestamps have millisecond resolution: same-ms starts/ends cannot prove
  # ordering, but a request spanning strictly across the checkpoint is pending.
  foreach($event in $events){
   if($event.startedAt -lt $settlement.at -and ($null -eq $event.endedAt -or $event.endedAt -gt $settlement.at) -and !$pendingIds.Contains($event.id)){throw 'Settlement omitted a definitely pending network request.'}
  }
  foreach($pendingRequest in $pendingMetadata){
   if(!(& $integer $pendingRequest.id) -or !$pendingIds.Remove($pendingRequest.id) -or !$eventById.ContainsKey([string]$pendingRequest.id)){throw 'Metadata settlement refers to a missing or repeated exact request.'}
   $event=$eventById[[string]$pendingRequest.id];$receipt=$pendingRequest.receipt
   foreach($field in @('stage','startedAt','method','origin','path','type')){if($pendingRequest.$field -cne $event.$field){throw 'Metadata settlement changed its originating request.'}}
   if($pendingRequest.state -cne 'pending' -or $pendingRequest.method -cne 'GET' -or $pendingRequest.origin -cne 'https://huggingface.co' -or $pendingRequest.status -ne 206 -or !(& $integer $pendingRequest.startedAt) -or $pendingRequest.startedAt -gt $settlement.at -or $receipt.path -cnotin ($required+@($metadataPath)) -or $pendingRequest.path -cne ('/'+$model+'/resolve/main/'+$receipt.path) -or $receipt.method -cne 'GET' -or $receipt.range -cne 'bytes=0-0' -or $receipt.status -ne 206 -or $receipt.bytes -ne 1 -or $receipt.sha256 -cnotmatch '^[a-f0-9]{64}$'){throw 'Only exact successful first-byte metadata responses may remain pending.'}
   $matches=@($assets.requests | Where-Object {$_.path -ceq $receipt.path -and $_.method -ceq 'GET' -and $_.range -ceq 'bytes=0-0' -and $_.status -eq 206 -and $_.bytes -eq 1 -and $_.sha256 -ceq $receipt.sha256})
   if($matches.Count -lt 1){throw 'Pending metadata has no completed asset-boundary receipt.'}
   if($event.state -cnotin @('pending','finished','failed') -or ($event.state -cne 'pending' -and (!(& $integer $event.endedAt) -or $event.endedAt -lt $settlement.at))){throw 'Metadata request ended before its claimed pending settlement.'}
  }
  if($pendingIds.Count -ne 0){throw 'Non-metadata requests remained pending at settlement.'}
  $workers=$settlement.workers
  if($workers.version -ne 1 -or $workers.overflow -isnot [bool] -or $workers.overflow -or $workers.workers -isnot [array] -or $workers.workers.Count -gt 4 -or ($i -lt 2 -and $workers.workers.Count -lt 1)){throw 'Network settlement lacks actual speech readiness.'}
  foreach($worker in $workers.workers){
   if($worker.errors -isnot [array] -or $worker.errors.Count -ne 0 -or $worker.ready -isnot [array] -or $worker.ready.Count -lt 1 -or $worker.ready.Count -gt 8){throw 'Network settlement has an unready or failed speech worker.'}
   foreach($ready in $worker.ready){if($ready.model -cne $model -or $ready.profile -cne 'q4'){throw 'Network settlement used an unexpected speech model.'}}
  }
 }
 $failureById=@{};$precleanup=@{}
 foreach($failure in $failures){
  if(!(& $integer $failure.id) -or !$eventById.ContainsKey([string]$failure.id) -or $failureById.ContainsKey([string]$failure.id) -or $failure.state -cne 'failed' -or $failure.endedDuringCleanup -isnot [bool] -or !(& $integer $failure.endedAt)){throw 'A raw network failure has missing or duplicate identity.'}
  $event=$eventById[[string]$failure.id]
  foreach($field in @('stage','startedAt','state','status','endedAt','endedStage','endedDuringCleanup','method','origin','path','type','error','redirectedFromId')){if($failure.$field -cne $event.$field){throw 'Raw failure does not match its exact final network event.'}}
  $failureById[[string]$failure.id]=$failure
  if($failure.endedDuringCleanup){if($failure.endedAt -lt $lastAt){throw 'Cleanup failure predates final settlement.'}}
  else{$precleanup[[string]$failure.id]=$failure}
 }
 # Keep the existing precise font/placeholder denials; all other raw failures
 # must be retained even if a receipt/cache reconciliation eventually succeeds.
 foreach($event in $events){
  if($event.state -cne 'failed'){continue}
  $font=$event.origin -ceq 'https://fonts.googleapis.com' -and $event.path -ceq '/css2' -and $event.error -cin @('net::ERR_BLOCKED_BY_CLIENT','net::ERR_BLOCKED_BY_CLIENT.Inspector')
  $placeholder=$event.origin -ceq ('http://'+$workerOrigin) -and $event.error -ceq 'net::ERR_ABORTED' -and ($event.path.StartsWith('/animations/') -or $event.path -ceq '/artifact-loading-scene.svg')
  if(!$font -and !$placeholder -and !$failureById.ContainsKey([string]$event.id)){throw 'An admitted failed event was removed from raw network failures.'}
 }
 if($reconciliations.Count -ne $precleanup.Count){throw 'Every pre-cleanup failure needs exactly one bounded reconciliation.'}
 $reconciled=[Collections.Generic.HashSet[long]]::new();$reconciledPaths=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
 foreach($row in $reconciliations){
  if(!(& $integer $row.id) -or !$precleanup.ContainsKey([string]$row.id) -or !$reconciled.Add($row.id) -or $row.reason -cne 'verified-browser-cache-after-full-delivery'){throw 'Network reconciliation has unknown or repeated identity or reason.'}
  $failure=$precleanup[[string]$row.id]
  foreach($field in @('stage','startedAt','state','status','endedAt','endedStage','endedDuringCleanup','method','origin','path','type','error','redirectedFromId')){if($row.$field -cne $failure.$field){throw 'Reconciliation changed the recorded raw failure.'}}
  $receipt=$row.receipt
  if($row.method -cne 'GET' -or !(& $integer $row.status) -or $row.status -ne 200 -or $row.error -cne 'net::ERR_ABORTED' -or $receipt.path -cnotin $required -or !$reconciledPaths.Add($receipt.path) -or $receipt.method -cne 'GET' -or $null -ne $receipt.range -or !(& $integer $receipt.status) -or $receipt.status -ne 200 -or !(& $integer $receipt.bytes) -or $receipt.bytes -ne $expected[$receipt.path].size -or $receipt.sha256 -cne $expected[$receipt.path].sha256 -or $attempts[$receipt.path] -ne 1){throw 'Failed request does not have exact sole-attempt full-file delivery.'}
  $deliveryUri=$null
  if($row.origin -isnot [string] -or ![uri]::TryCreate($row.origin,[UriKind]::Absolute,[ref]$deliveryUri) -or $deliveryUri.Scheme -cne 'http' -or $deliveryUri.Host -cne '127.0.0.1' -or $deliveryUri.Port -lt 1 -or $deliveryUri.UserInfo -or $deliveryUri.Query -or $deliveryUri.Fragment -or $row.origin -cne ('http://127.0.0.1:'+$deliveryUri.Port) -or $row.path -cnotmatch ('^/[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}/'+[regex]::Escape($receipt.path)+'$')){throw 'Full delivery did not use its exact loopback asset ticket.'}
  if(!(& $integer $row.redirectedFromId) -or !$eventById.ContainsKey([string]$row.redirectedFromId)){throw 'Full delivery has no exact source redirect identity.'}
  $source=$eventById[[string]$row.redirectedFromId]
  if($source.method -cne 'GET' -or $source.origin -cne 'https://huggingface.co' -or $source.path -cne ('/'+$model+'/resolve/main/'+$receipt.path) -or $source.status -ne 307 -or $source.state -cne 'finished' -or $source.startedAt -gt $row.startedAt){throw 'Full delivery source is not its completed exact Hugging Face redirect.'}
  if(!(& $integer $row.cacheVerifiedAt) -or !$cacheByTime.ContainsKey([string]$row.cacheVerifiedAt)){throw 'Full delivery has no current-run browser-cache proof.'}
  $cached=$cacheByTime[[string]$row.cacheVerifiedAt][$receipt.path]
  if($cached.bytes -ne $receipt.bytes -or $cached.sha256 -cne $receipt.sha256){throw 'Full delivery and independently received browser cache disagree.'}
  $matches=@($assets.requests | Where-Object {$_.path -ceq $receipt.path -and $_.method -ceq 'GET' -and $null -eq $_.range -and $_.status -eq 200 -and $_.bytes -eq $receipt.bytes -and $_.sha256 -ceq $receipt.sha256})
  if($matches.Count -ne 1){throw 'Reconciliation does not match its sole completed asset-boundary receipt.'}
 }

}
