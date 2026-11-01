# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
# Dot-source QuestProbeRestart.ps1 first. Only exact owned process handles are killed.
function Assert-QuestActionInterruptionBoundary($Request,$Source,$Envelope,$Identity,[string]$ProbeId){
 Assert-QuestProbeRestartRequest $Request $ProbeId
 if($Request.version -ne 2 -or $Source.version -ne 1 -or $Source.phase -cne 'awaiting-process-termination' -or $Source.nodeProcessId -ne $Identity.processId){throw 'Wrong source client process at the interruption boundary.'}
 if($Source.request.id -cne $ProbeId -or $Source.request.runId -cne $Request.runId -or $Source.request.taskId -cne $Request.taskId){throw 'The source did not attest this requested action.'}
 if($Envelope.version -ne 1 -or $Envelope.id -cne $ProbeId -or $Envelope.clientId -cne $Request.clientId -or $Envelope.state.session -cne $Request.nativeSession){throw 'The native action observation has the wrong scope.'}
 $selected=$Envelope.state.execution.selected
 if($selected.id -cne $Request.runId -or $selected.phase -cne 'running' -or $selected.capability -cne 'animation.play' -or @($Envelope.state.execution.running | Where-Object {$_.id -ceq $Request.runId -and $_.phase -ceq 'running'}).Count -ne 1){throw 'The selected native action is no longer running.'}
}
function Read-QuestActionInterruptionJson([string]$Path){
 $info=Get-Item -LiteralPath $Path
 if($info.PSIsContainer -or ($info.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $info.Length -lt 1 -or $info.Length -gt 4194304){throw 'Invalid bounded interruption evidence.'}
 $bytes=[IO.File]::ReadAllBytes($info.FullName);if($bytes.Length -gt 4194304){throw 'Interruption evidence exceeds its byte limit.'}
 return [Text.UTF8Encoding]::new($false,$true).GetString($bytes) | ConvertFrom-Json -AsHashtable -Depth 100
}
function Stop-QuestActionInterruptionClient([Diagnostics.Process]$Process,$Identity,[string]$RootDirectory,[string]$ProbeId){
 Assert-QuestProbeRestartDirectory $RootDirectory
 $request=(Read-QuestProbeRestartRequest (Join-Path $RootDirectory 'restart-request.json') $ProbeId).request
 $source=Read-QuestActionInterruptionJson (Join-Path $RootDirectory 'action-interruption-source.json')
 $envelope=Read-QuestActionInterruptionJson (Join-Path $RootDirectory 'state.json')
 Assert-QuestProbeProcessIdentity $Identity (Get-QuestProbeProcessIdentity $Process) (Test-Path -LiteralPath (Join-Path $RootDirectory 'action-interruption-error.json'))
 Assert-QuestActionInterruptionBoundary $request $source $envelope $Identity $ProbeId
 $Process.Kill();if(!$Process.WaitForExit(30000)){throw 'The owned source chat client did not terminate.'}
 if(Test-Path -LiteralPath (Join-Path $RootDirectory 'action-interruption-error.json')){throw 'The source performed normal error cleanup instead of abrupt termination.'}
 # Freeze an actual native observation AFTER the client is dead and BEFORE killing Unity.
 $envelope=Read-QuestActionInterruptionJson (Join-Path $RootDirectory 'state.json')
 Assert-QuestActionInterruptionBoundary $request $source $envelope $Identity $ProbeId
 $path=Join-Path $RootDirectory 'action-interruption-native.json'
 [IO.File]::WriteAllText($path,($envelope | ConvertTo-Json -Depth 100 -Compress),[Text.UTF8Encoding]::new($false))
 return @{version=1;id=$ProbeId;profileDirectory=(Join-Path $RootDirectory 'profiles/action-interruption');source=@{processId=$Identity.processId;startedAt=$Identity.startedAt;exitedAt=[DateTimeOffset]::new($Process.ExitTime.ToUniversalTime()).ToUnixTimeMilliseconds();exitCode=$Process.ExitCode;normalExit=$false};resumed=@{}}
}
