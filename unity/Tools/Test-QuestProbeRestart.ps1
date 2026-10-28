# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
param([string]$ReportPath)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'QuestProbeRestart.ps1')
$cases=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[scriptblock]$Action,[bool]$Accepted=$true){
 $failure=$null;try{& $Action | Out-Null}catch{$failure=$_.Exception.Message}
 if(($null -eq $failure) -ne $Accepted){throw "Restart contract failed: $Name ($failure)"}
 $cases.Add(@{name=$Name;accepted=$Accepted;status='passed'})
}
$probe='1'*32;$child='2'*32;$hash='a'*64
function Request {return @{version=1;id=$probe;operation='restart';clientId='3'*32;nativeSession='4'*32;appleId='5'*32;treeId='6'*32;checkpoint=@{room=@{phase='idle';pending=$false;dirty=$false;dispatchHeld=$false;error=''};behaviours=@{phase='idle';pending=$false;dirty=$false;dispatchHeld=$false;error=''}}}}
function Check-Request([string]$Name,[scriptblock]$Mutate,[bool]$Accepted=$false){Check $Name { $r=Request;& $Mutate $r;Assert-QuestProbeRestartRequest $r $probe } $Accepted}
Check-Request 'confirmed-idle-checkpoint' {} $true
Check-Request 'wrong-owner' {param($r)$r.id='7'*32}
Check-Request 'string-version' {param($r)$r.version='1'}
Check-Request 'wrong-operation' {param($r)$r.operation='stop'}
Check-Request 'empty-array-owner' {param($r)$r.id=@()}
Check-Request 'empty-array-operation' {param($r)$r.operation=@()}
Check-Request 'extra-request-field' {param($r)$r.extra=$true}
Check-Request 'missing-request-field' {param($r)$r.Remove('clientId')}
Check-Request 'malformed-client' {param($r)$r.clientId='../wire'}
Check-Request 'uppercase-session' {param($r)$r.nativeSession='A'*32}
Check-Request 'same-object-identities' {param($r)$r.appleId=$r.treeId}
Check-Request 'missing-checkpoint-store' {param($r)$r.checkpoint.Remove('behaviours')}
Check-Request 'extra-checkpoint-store' {param($r)$r.checkpoint.fake=@{}}
foreach($store in @('room','behaviours')){
 foreach($field in @('pending','dirty','dispatchHeld')){Check-Request "$store-$field-refused" {param($r)$r.checkpoint.$store.$field=$true};Check-Request "$store-$field-string-refused" {param($r)$r.checkpoint.$store.$field='false'}}
 Check-Request "$store-error-refused" {param($r)$r.checkpoint.$store.error='write failed'}
 Check-Request "$store-phase-refused" {param($r)$r.checkpoint.$store.phase='saving'}
 Check-Request "$store-array-phase-refused" {param($r)$r.checkpoint.$store.phase=@()}
 Check-Request "$store-extra-field-refused" {param($r)$r.checkpoint.$store.revision=10}
}
Check 'request-PSCustomObject-supported' {Assert-QuestProbeRestartRequest ((Request | ConvertTo-Json -Depth 8) | ConvertFrom-Json) $probe}
$expected=@{processId=123;startedAt=1000L;hasExited=$false}
function Check-Identity([string]$Name,[scriptblock]$Mutate,[bool]$Accepted=$false){Check $Name { $actual=$expected.Clone();& $Mutate $actual;Assert-QuestProbeProcessIdentity $expected $actual $false } $Accepted}
Check-Identity 'same-live-handle-identity' {} $true
Check-Identity 'PID-reuse' {param($r)$r.startedAt=1001L}
Check-Identity 'different-PID' {param($r)$r.processId=124}
Check-Identity 'already-exited' {param($r)$r.hasExited=$true}
Check-Identity 'string-exited-state' {param($r)$r.hasExited='false'}
Check-Identity 'invalid-PID' {param($r)$r.processId=0}
Check-Identity 'fractional-start' {param($r)$r.startedAt=1000.5}
Check 'existing-native-terminal-refused' {Assert-QuestProbeProcessIdentity $expected $expected $true} $false
Check 'already-exited-owned-snapshot-refused' {$old=$expected.Clone();$old.hasExited=$true;Assert-QuestProbeProcessIdentity $old $expected $false} $false
$tempParent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
$tempName='maestro-probe-restart-'+[Guid]::NewGuid().ToString('N');$temporary=Join-Path $tempParent $tempName
[void][IO.Directory]::CreateDirectory($temporary)
try{
 $paths=Get-QuestProbeRestartPaths $temporary
 Check 'fixed-restart-and-shared-workspace-paths' {if($paths.child -cne (Join-Path $temporary 'restart') -or $paths.workspace -cne (Join-Path $temporary 'workspace') -or $paths.request -cne (Join-Path $temporary 'restart-request.json')){throw 'Wrong fixed paths'}}
 Check 'relative-root-refused' {Get-QuestProbeRestartPaths 'relative/root'} $false
 Check 'ordinary-owned-directory-accepted' {Assert-QuestProbeRestartDirectory $temporary}
 $requestText=Request | ConvertTo-Json -Depth 8
 [IO.File]::WriteAllText($paths.request,$requestText,[Text.UTF8Encoding]::new($false))
 Check 'hash-original-request-bytes' {$read=Read-QuestProbeRestartRequest $paths.request $probe;if($read.sha256 -cne (Get-FileHash -LiteralPath $paths.request -Algorithm SHA256).Hash.ToLowerInvariant()){throw 'Request hash mismatch'}}
 Check 'duplicate-json-key-refused' {[IO.File]::WriteAllText($paths.request,$requestText.Replace('"version": 1','"version": 1, "version": 1'));Read-QuestProbeRestartRequest $paths.request $probe} $false
 Check 'oversized-request-refused' {[IO.File]::WriteAllText($paths.request,(' '*8193));Read-QuestProbeRestartRequest $paths.request $probe} $false
 Check 'empty-request-refused' {[IO.File]::WriteAllBytes($paths.request,[byte[]]@());Read-QuestProbeRestartRequest $paths.request $probe} $false
 Check 'invalid-UTF8-refused' {[IO.File]::WriteAllBytes($paths.request,[byte[]]@(0xff,0xfe));Read-QuestProbeRestartRequest $paths.request $probe} $false
 function Receipt {return @{version=1;id=$probe;method='terminate-after-checkpoint';workspace=$paths.workspace;requestSha256=$hash;source=@{id=$probe;processId=123;startedAt=1000L;exitedAt=2000L;exitCode=-1;terminalExisted=$false};resumed=@{id=$child;directory=$paths.child}}}
 function Check-Receipt([string]$Name,[scriptblock]$Mutate,[bool]$Accepted=$false,[switch]$Resumed){Check $Name {$r=Receipt;if($Resumed){$r.resumed.processId=124;$r.resumed.startedAt=2001L};& $Mutate $r;Assert-QuestProbeRestartReceipt $r $temporary $probe $hash -RequireResumedProcess:$Resumed} $Accepted}
 Check-Receipt 'prelaunch-observed-interruption' {} $true
 Check-Receipt 'postlaunch-distinct-process' {} $true -Resumed
 Check-Receipt 'wrong-receipt-owner' {param($r)$r.id=$child}
 Check-Receipt 'array-receipt-method' {param($r)$r.method=@()}
 Check-Receipt 'wrong-request-hash' {param($r)$r.requestSha256='b'*64}
 Check-Receipt 'copied-workspace-refused' {param($r)$r.workspace=Join-Path $paths.child 'workspace'}
 Check-Receipt 'workspace-traversal-spelling-refused' {param($r)$r.workspace=Join-Path $temporary 'restart/../workspace'}
 Check-Receipt 'sibling-wire-refused' {param($r)$r.resumed.directory=Join-Path $temporary 'sibling'}
 Check-Receipt 'reused-wire-id-refused' {param($r)$r.resumed.id=$probe}
 Check-Receipt 'source-normal-terminal-refused' {param($r)$r.source.terminalExisted=$true}
 Check-Receipt 'source-exit-before-start-refused' {param($r)$r.source.exitedAt=999L}
 Check-Receipt 'source-string-exitcode-refused' {param($r)$r.source.exitCode='-1'}
 Check-Receipt 'source-exitcode-outside-int32-refused' {param($r)$r.source.exitCode=2147483648L}
 Check-Receipt 'source-missing-process-start-refused' {param($r)$r.source.Remove('startedAt')}
 Check-Receipt 'resumed-reused-PID-refused' {param($r)$r.resumed.processId=123} $false -Resumed
 Check-Receipt 'resumed-before-source-exit-refused' {param($r)$r.resumed.startedAt=1999L} $false -Resumed
 Check-Receipt 'postlaunch-missing-start-refused' {param($r)$r.resumed.Remove('startedAt')} $false -Resumed
 Check-Receipt 'prelaunch-fabricated-child-PID-refused' {param($r)$r.resumed.processId=124}
 Check 'JSON-receipt-roundtrip' {$r=(Receipt | ConvertTo-Json -Depth 8) | ConvertFrom-Json -AsHashtable;Assert-QuestProbeRestartReceipt $r $temporary $probe $hash}
 Check 'atomic-receipt-write-preserves-original-request-hash' {$r=Receipt;Write-QuestProbeRestartJson $paths.receipt $r;$read=Get-Content -LiteralPath $paths.receipt -Raw | ConvertFrom-Json -AsHashtable;Assert-QuestProbeRestartReceipt $read $temporary $probe $hash;if(Test-Path -LiteralPath ($paths.receipt+'.pending')){throw 'Uncommitted receipt remained'}}
 $wrapper=Join-Path $PSScriptRoot 'Run-QuestRoomProbe.ps1';$tokens=$null;$errors=$null
 $ast=[Management.Automation.Language.Parser]::ParseFile($wrapper,[ref]$tokens,[ref]$errors)
 if($errors.Count){throw ($errors | Out-String)}
 $guards=$ast.FindAll({param($node)$node -is [Management.Automation.Language.IfStatementAst] -and ($node.Extent.Text.StartsWith('if($NativeRestart -and') -or $node.Extent.Text.StartsWith('if($LiveContinuity -and') -or $node.Extent.Text.StartsWith('if($FirstLearner -and'))},$true)
 if($guards.Count -ne 3){throw 'Missing exact early offline restart guards'}
 function Check-Admission([string]$Name,[hashtable]$Values,[bool]$Accepted){Check $Name {& {param($Values,$Guards)$NativeRestart=$false;$FirstLearner=$false;$LiveContinuity=$false;$Journey='Headless';foreach($key in $Values.Keys){Set-Variable -Name $key -Value $Values[$key]};foreach($guard in $Guards){. ([scriptblock]::Create($guard.Extent.Text))}} $Values $guards} $Accepted}
 Check-Admission 'ordinary-headless-unmodified' @{} $true
 Check-Admission 'ordinary-book-unmodified' @{Journey='Book'} $true
 Check-Admission 'ordinary-live-unmodified' @{Journey='Book';FirstLearner=$true;LiveContinuity=$true} $true
 Check-Admission 'explicit-restart-admitted' @{Journey='Book';FirstLearner=$true;LiveContinuity=$true;NativeRestart=$true} $true
 foreach($missing in @('FirstLearner','LiveContinuity')){$values=@{Journey='Book';FirstLearner=$true;LiveContinuity=$true;NativeRestart=$true};$values[$missing]=$false;Check-Admission "restart-requires-$missing" $values $false}
 Check-Admission 'restart-requires-book' @{Journey='Headless';FirstLearner=$true;LiveContinuity=$true;NativeRestart=$true} $false
 foreach($option in @('ProviderScenario','Prompt','SpeechFixture','ResumeIdentityRun','ResumeLearnerRun','TransportOnly','MotionOnly','SteeringOnly','IdentityOnly','SparseObjectView','SyntheticRoomScan','SyntheticSound','SyntheticLiveSound','SyntheticImage','SyntheticModel','SyntheticGait')){$values=@{Journey='Book';FirstLearner=$true;LiveContinuity=$true;NativeRestart=$true};$values[$option]=$(if($option -in @('ProviderScenario','Prompt','SpeechFixture','ResumeIdentityRun','ResumeLearnerRun')){'forbidden'}else{$true});Check-Admission "restart-rejects-$option" $values $false}
 $source=[IO.File]::ReadAllText($wrapper)
 Check 'owner-and-final-receipt-record-mode' {if([regex]::Matches($source,'nativeRestart=\[bool\]\$NativeRestart').Count -ne 2){throw 'Missing mode receipt'}}
 $report=@{status='passed';mode='Pure restart validation and extracted wrapper admission only; no native/browser/process lifecycle launched';cases=$cases;helperSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'QuestProbeRestart.ps1')).Hash;wrapperSha256=(Get-FileHash -LiteralPath $wrapper).Hash}
 if($ReportPath){$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ReportPath -Encoding utf8}
 Write-Output "Passed $($cases.Count) restart contracts; no runtime launched."
}finally{
 if(Test-Path -LiteralPath $temporary){$resolved=[IO.Path]::GetFullPath($temporary);if($resolved -cne (Join-Path $tempParent $tempName) -or [IO.Path]::GetDirectoryName($resolved) -cne $tempParent -or [IO.Path]::GetFileName($resolved) -cnotmatch '^maestro-probe-restart-[a-f0-9]{32}$'){throw 'Refusing cleanup outside owned restart contract folder'};Remove-Item -LiteralPath $resolved -Recurse -Force}
}
