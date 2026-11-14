# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
# Explicit offline probe restart orchestration. No device/provider operations.
function Assert-QuestProbeRestartKeys($Value,[string[]]$Names){
 if($null -eq $Value -or ($Value -isnot [Collections.IDictionary] -and $Value -isnot [pscustomobject])){throw 'Expected a restart record.'}
 $keys=@(if($Value -is [Collections.IDictionary]){$Value.Keys}else{$Value.PSObject.Properties.Name})
 if($keys.Count -ne $Names.Count -or @($keys | Where-Object {$_ -cnotin $Names}).Count){throw 'Unexpected restart record fields.'}
}
function Test-QuestProbeRestartInteger($Value){
 return ($Value -is [int] -or $Value -is [long]) -and $Value -ge 0 -and $Value -le 9007199254740991
}
function Assert-QuestProbeRestartRequest($Request,[string]$ProbeId){
 $action=$Request.version -ceq 2
 Assert-QuestProbeRestartKeys $Request $(if($action){@('version','id','operation','clientId','nativeSession','runId','taskId','checkpoint')}else{@('version','id','operation','clientId','nativeSession','appleId','treeId','checkpoint')})
 if(!(Test-QuestProbeRestartInteger $Request.version) -or $Request.version -notin @(1,2) -or $ProbeId -cnotmatch '^[a-f0-9]{32}$' -or $Request.id -isnot [string] -or $Request.id -cne $ProbeId -or $Request.operation -isnot [string] -or $Request.operation -cne 'restart'){throw 'Restart request has the wrong owner or operation.'}
 foreach($name in $(if($action){@('clientId','nativeSession','runId')}else{@('clientId','nativeSession','appleId','treeId')})){if($Request.$name -isnot [string] -or $Request.$name -cnotmatch '^[a-f0-9]{32}$'){throw "Invalid restart identity: $name"}}
 if($action){if($Request.taskId -isnot [string] -or $Request.taskId -cnotmatch '^room-task:[a-zA-Z0-9:_-]{1,200}$'){throw 'Invalid interrupted task identity.'}}
 elseif($Request.appleId -ceq $Request.treeId){throw 'Restart must preserve distinct tree and apple identities.'}
 Assert-QuestProbeRestartKeys $Request.checkpoint @('room','behaviours')
 foreach($name in @('room','behaviours')){
  $store=$Request.checkpoint.$name;Assert-QuestProbeRestartKeys $store @('phase','pending','dirty','dispatchHeld','error')
  if($store.phase -isnot [string] -or $store.phase -cne 'idle' -or $store.error -isnot [string] -or $store.error -cne ''){throw 'Restart requires a confirmed persistence checkpoint.'}
  foreach($flag in @('pending','dirty','dispatchHeld')){if($store.$flag -isnot [bool] -or $store.$flag){throw 'Restart requires idle, clean, released persistence.'}}
 }
}
function Assert-QuestProbeRestartJsonUnique([System.Text.Json.JsonElement]$Value){
 if($Value.ValueKind -eq [System.Text.Json.JsonValueKind]::Object){
  $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
  foreach($entry in $Value.EnumerateObject()){if(!$seen.Add($entry.Name)){throw 'Duplicate restart JSON field.'};Assert-QuestProbeRestartJsonUnique $entry.Value}
 }elseif($Value.ValueKind -eq [System.Text.Json.JsonValueKind]::Array){foreach($entry in $Value.EnumerateArray()){Assert-QuestProbeRestartJsonUnique $entry}}
}
function Read-QuestProbeRestartRequest([string]$Path,[string]$ProbeId){
 $info=Get-Item -LiteralPath $Path
 if($info.PSIsContainer -or ($info.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $info.Length -gt 8192){throw 'Restart request must be a bounded regular file.'}
 $bytes=[IO.File]::ReadAllBytes($info.FullName);if($bytes.Length -eq 0 -or $bytes.Length -gt 8192){throw 'Restart request byte limit exceeded.'}
 $text=[Text.UTF8Encoding]::new($false,$true).GetString($bytes)
 $document=[System.Text.Json.JsonDocument]::Parse($text)
 try{Assert-QuestProbeRestartJsonUnique $document.RootElement}finally{$document.Dispose()}
 $request=ConvertFrom-Json -InputObject $text -AsHashtable -Depth 8
 Assert-QuestProbeRestartRequest $request $ProbeId
 $digest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
 return @{request=$request;sha256=$digest}
}
function Get-QuestProbeRestartPaths([string]$RootDirectory){
 if(![IO.Path]::IsPathFullyQualified($RootDirectory)){throw 'Restart root must be absolute.'}
 $root=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($RootDirectory))
 return @{root=$root;child=(Join-Path $root 'restart');workspace=(Join-Path $root 'workspace');request=(Join-Path $root 'restart-request.json');receipt=(Join-Path $root 'restart.json');terminal=(Join-Path $root 'terminal.json')}
}
function Assert-QuestProbeRestartDirectory([string]$Path){
 $current=[IO.DirectoryInfo]::new([IO.Path]::GetFullPath($Path))
 while($null -ne $current){if($current.Exists -and ($current.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Restart paths cannot traverse a reparse directory.'};$current=$current.Parent}
}
function Get-QuestProbeProcessIdentity([Diagnostics.Process]$Process){
 $Process.Refresh()
 return @{processId=$Process.Id;startedAt=[DateTimeOffset]::new($Process.StartTime.ToUniversalTime()).ToUnixTimeMilliseconds();hasExited=$Process.HasExited}
}
function Assert-QuestProbeProcessIdentity($Expected,$Actual,[bool]$TerminalExists){
 foreach($value in @($Expected,$Actual)){
  Assert-QuestProbeRestartKeys $value @('processId','startedAt','hasExited')
  if(!(Test-QuestProbeRestartInteger $value.processId) -or $value.processId -lt 1 -or $value.processId -gt [int]::MaxValue -or !(Test-QuestProbeRestartInteger $value.startedAt) -or $value.startedAt -lt 1 -or $value.hasExited -isnot [bool] -or $value.hasExited){throw 'Restart requires a still-live owned process.'}
 }
 if($Expected.processId -ne $Actual.processId -or $Expected.startedAt -ne $Actual.startedAt -or $TerminalExists){throw 'The owned restart process identity or terminal state changed.'}
}
function Assert-QuestProbeRestartReceipt($Receipt,[string]$RootDirectory,[string]$ProbeId,[string]$RequestSha256,[switch]$RequireResumedProcess){
 $paths=Get-QuestProbeRestartPaths $RootDirectory
 Assert-QuestProbeRestartKeys $Receipt @('version','id','method','workspace','requestSha256','source','resumed')
 if(!(Test-QuestProbeRestartInteger $Receipt.version) -or $Receipt.version -ne 1 -or $ProbeId -cnotmatch '^[a-f0-9]{32}$' -or $Receipt.id -isnot [string] -or $Receipt.id -cne $ProbeId -or $Receipt.method -isnot [string] -or $Receipt.method -cne 'terminate-after-checkpoint' -or $RequestSha256 -cnotmatch '^[a-f0-9]{64}$' -or $Receipt.requestSha256 -isnot [string] -or $Receipt.requestSha256 -cne $RequestSha256){throw 'Restart receipt ownership is invalid.'}
 if($Receipt.workspace -isnot [string] -or ![IO.Path]::IsPathFullyQualified($Receipt.workspace) -or ![string]::Equals($Receipt.workspace,$paths.workspace,[StringComparison]::OrdinalIgnoreCase)){throw 'Restart must use the exact original workspace directory.'}
 $source=$Receipt.source;Assert-QuestProbeRestartKeys $source @('id','processId','startedAt','exitedAt','exitCode','terminalExisted')
 if($source.id -isnot [string] -or $source.id -cne $ProbeId -or !(Test-QuestProbeRestartInteger $source.processId) -or $source.processId -lt 1 -or $source.processId -gt [int]::MaxValue -or !(Test-QuestProbeRestartInteger $source.startedAt) -or $source.startedAt -lt 1 -or !(Test-QuestProbeRestartInteger $source.exitedAt) -or $source.exitedAt -lt $source.startedAt -or ($source.exitCode -isnot [int] -and $source.exitCode -isnot [long]) -or $source.exitCode -lt [int]::MinValue -or $source.exitCode -gt [int]::MaxValue -or $source.terminalExisted -isnot [bool] -or $source.terminalExisted){throw 'Restart lacks an observed owned interruption.'}
 $resumed=$Receipt.resumed
 Assert-QuestProbeRestartKeys $resumed $(if($RequireResumedProcess){@('id','directory','processId','startedAt')}else{@('id','directory')})
 if($resumed.id -isnot [string] -or $resumed.id -cnotmatch '^[a-f0-9]{32}$' -or $resumed.id -ceq $ProbeId -or $resumed.directory -isnot [string] -or ![string]::Equals($resumed.directory,$paths.child,[StringComparison]::OrdinalIgnoreCase)){throw 'Restart needs a fresh fixed child wire directory.'}
 if($RequireResumedProcess){
  if(!(Test-QuestProbeRestartInteger $resumed.processId) -or $resumed.processId -lt 1 -or $resumed.processId -gt [int]::MaxValue -or $resumed.processId -eq $source.processId -or !(Test-QuestProbeRestartInteger $resumed.startedAt) -or $resumed.startedAt -lt $source.exitedAt){throw 'The resumed process must start after its distinct predecessor exits.'}
 }
}
function Write-QuestProbeRestartJson([string]$Path,$Value){
 $bytes=[Text.UTF8Encoding]::new($false).GetBytes(($Value | ConvertTo-Json -Depth 12 -Compress))
 if($bytes.Length -gt 16384){throw 'Restart receipt exceeds its byte limit.'}
 $pending=$Path+'.pending';[IO.File]::WriteAllBytes($pending,$bytes);[IO.File]::Move($pending,$Path,$true)
}
function Invoke-QuestProbeNativeRestart([Diagnostics.Process]$Process,$Identity,[string]$RootDirectory,[string]$ProbeId,[scriptblock]$Launch,[switch]$ActionInterruption){
 $paths=Get-QuestProbeRestartPaths $RootDirectory
 Assert-QuestProbeRestartDirectory $paths.root;Assert-QuestProbeRestartDirectory $paths.workspace
 if(!(Test-Path -LiteralPath $paths.workspace -PathType Container) -or (Test-Path -LiteralPath $paths.child) -or (Test-Path -LiteralPath $paths.receipt)){throw 'Restart requires one existing workspace and a fresh child/receipt.'}
 $request=Read-QuestProbeRestartRequest $paths.request $ProbeId
 if(($request.request.version -eq 2) -ne [bool]$ActionInterruption){throw 'Restart request does not match the owned journey mode.'}
 $actual=Get-QuestProbeProcessIdentity $Process
 Assert-QuestProbeProcessIdentity $Identity $actual (Test-Path -LiteralPath $paths.terminal)
 # Kill only this exact still-live process handle. No normal stop/terminal is fabricated.
 $Process.Kill();if(!$Process.WaitForExit(30000)){throw 'The owned first Editor did not terminate.'}
 if(Test-Path -LiteralPath $paths.terminal){throw 'The first Editor unexpectedly produced a normal terminal during interruption.'}
 $exitAt=[DateTimeOffset]::new($Process.ExitTime.ToUniversalTime()).ToUnixTimeMilliseconds()
 $childId=[Guid]::NewGuid().ToString('N');[void][IO.Directory]::CreateDirectory($paths.child)
 Write-QuestProbeRestartJson (Join-Path $paths.child 'owner.json') @{version=1;id=$childId;firstLearner=(!$ActionInterruption);liveContinuity=(!$ActionInterruption);nativeRestart=$true;actionInterruption=[bool]$ActionInterruption;restartOf=$ProbeId}
 $receipt=@{version=1;id=$ProbeId;method='terminate-after-checkpoint';workspace=$paths.workspace;requestSha256=$request.sha256;source=@{id=$ProbeId;processId=$Identity.processId;startedAt=$Identity.startedAt;exitedAt=$exitAt;exitCode=$Process.ExitCode;terminalExisted=$false};resumed=@{id=$childId;directory=$paths.child}}
 Assert-QuestProbeRestartReceipt $receipt $paths.root $ProbeId $request.sha256
 Write-QuestProbeRestartJson $paths.receipt $receipt
 Stop-QuestBuildHelper
 $child=$null
 try{
  $child=& $Launch $paths.child (Join-Path $paths.child 'unity.log')
  if($child -isnot [Diagnostics.Process]){throw 'Restart launcher did not return its owned process handle.'}
  $childIdentity=Get-QuestProbeProcessIdentity $child
  if($childIdentity.hasExited){throw 'The resumed Editor exited before its launch receipt.'}
  $receipt.resumed.processId=$childIdentity.processId;$receipt.resumed.startedAt=$childIdentity.startedAt
  Assert-QuestProbeRestartReceipt $receipt $paths.root $ProbeId $request.sha256 -RequireResumedProcess
  Write-QuestProbeRestartJson $paths.receipt $receipt
  return @{process=$child;id=$childId;directory=$paths.child;log=(Join-Path $paths.child 'unity.log');receipt=$receipt}
 }catch{if($null -ne $child -and !$child.HasExited){$child.Kill();$child.WaitForExit()};throw}
}
