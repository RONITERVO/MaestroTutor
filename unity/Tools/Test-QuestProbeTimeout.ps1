# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
param([string]$ReportPath)
$ErrorActionPreference='Stop'
$path=Join-Path $PSScriptRoot 'Run-QuestRoomProbe.ps1'
$tokens=$null;$parseErrors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$parseErrors)
if($parseErrors.Count){throw ($parseErrors | Out-String)}
$parameter=@($ast.ParamBlock.Parameters | Where-Object {$_.Name.VariablePath.UserPath -eq 'FullJourneyTimeoutMinutes'})
if($parameter.Count -ne 1){throw 'Expected one bounded full journey timeout parameter.'}
$statements=@($ast.FindAll({param($node)
 ($node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -in @('$restarting','$fullJourney','$fullJourneyTimeout')) -or
 ($node -is [Management.Automation.Language.IfStatementAst] -and $node.Extent.Text.StartsWith("if(`$PSBoundParameters.ContainsKey('FullJourneyTimeoutMinutes')"))
},$true) | Sort-Object {$_.Extent.StartOffset})
if($statements.Count -ne 4){throw 'Expected explicit timeout scope, dedicated-mode guard and selected budget.'}
# Execute only the actual parameter declaration and pure admission/selection statements.
# No Editor, filesystem preparation, browser, provider, or native journey is launched.
$initial=@(
 '$Journey="Headless";$Prompt="";$ProviderScenario="";$ResumeIdentityRun="";$ResumeLearnerRun=""',
 '$FirstLearner=$false;$LiveContinuity=$false;$NativeRestart=$false;$ActionInterruption=$false',
 '$TransportOnly=$false;$MotionOnly=$false;$SteeringOnly=$false;$IdentityOnly=$false',
 'foreach($key in $InputValues.Keys){Set-Variable -Name $key -Value $InputValues[$key]}'
) -join "`n"
$body='param('+$parameter[0].Extent.Text+',[hashtable]$InputValues)' + "`n" + $initial + "`n" + (($statements | ForEach-Object {$_.Extent.Text}) -join "`n") + "`n" + '[pscustomobject]@{full=$fullJourney;minutes=$fullJourneyTimeout}'
$selection=[scriptblock]::Create($body)
$cases=[Collections.Generic.List[object]]::new()
function Check-Selection([string]$Name,[hashtable]$Values,[bool]$Explicit,[int]$Minutes,[int]$Expected,[bool]$Accepted=$true){
 $arguments=@{InputValues=$Values};if($Explicit){$arguments.FullJourneyTimeoutMinutes=$Minutes}
 $errorText=$null;$actual=$null
 try{$actual=& $selection @arguments}catch{$errorText=$_.Exception.Message}
 if(($null -eq $errorText) -ne $Accepted){throw "Timeout argument contract failed: $Name $errorText"}
 if($Accepted -and ($actual.minutes -ne $Expected -or $actual.full -ne ($Expected -gt 0))){throw "Wrong selected timeout: $Name $($actual | ConvertTo-Json -Compress)"}
 $cases.Add(@{name=$Name;accepted=$Accepted;selectedMinutes=$(if($Accepted){$actual.minutes}else{$null});status='passed'})
}
Check-Selection 'default-headless' @{} $false 0 20
Check-Selection 'default-book' @{Journey='Book'} $false 0 20
Check-Selection 'sparse-headless' @{SparseObjectView=$true} $false 0 20
Check-Selection 'sparse-book' @{Journey='Book';SparseObjectView=$true} $false 0 20
foreach($option in @('SyntheticSound','SyntheticLiveSound','SyntheticImage','SyntheticModel','SyntheticGait')){
 $values=@{Journey='Book'};$values[$option]=$true
 Check-Selection "ordinary-fixture-$option" $values $false 0 20
}
Check-Selection 'explicit-lower-bound' @{} $true 15 15
Check-Selection 'explicit-upper-bound-book' @{Journey='Book'} $true 30 30
Check-Selection 'explicit-default' @{} $true 20 20
Check-Selection 'reject-below-bound' @{} $true 14 0 $false
Check-Selection 'reject-above-bound' @{} $true 31 0 $false
foreach($option in @('Prompt','ProviderScenario','ResumeIdentityRun','ResumeLearnerRun','FirstLearner','LiveContinuity','NativeRestart','ActionInterruption','TransportOnly','MotionOnly','SteeringOnly','IdentityOnly')){
 $values=@{};$values[$option]=$(if($option -in @('Prompt','ProviderScenario','ResumeIdentityRun','ResumeLearnerRun')){'explicit-other-mode'}else{$true})
 Check-Selection "unchanged-default-$option" $values $false 0 0
 Check-Selection "reject-explicit-override-$option" $values $true 20 0 $false
}
Check-Selection 'unchanged-live-restart' @{Journey='Book';FirstLearner=$true;LiveContinuity=$true;NativeRestart=$true} $false 0 0
Check-Selection 'unchanged-interactive-learner' @{ProviderScenario='LearnerConversation';Prompt='learner'} $false 0 0
Check-Selection 'reject-unknown-journey' @{Journey='Other'} $true 20 0 $false
$source=[IO.File]::ReadAllText($path)
if([regex]::Matches($source,'fullJourneyTimeoutMinutes=\$fullJourneyTimeout').Count -ne 2){throw 'Owner and verified evidence must record the selected budget.'}
if(!$source.Contains('$ready.fullJourneyTimeoutMinutes -ne $fullJourneyTimeout') -or !$source.Contains('$ready.sessionTimeoutSeconds -ne 60*$fullJourneyTimeout')){throw 'The full journey must verify the native applied timeout before issuing verified.json.'}
if(!$source.Contains('$clientDeadline=[DateTime]::UtcNow.AddMinutes(15)')){throw 'Restart supervisor deadline must remain unchanged.'}
$nativePath=Join-Path $PSScriptRoot '../MaestroQuest/Assets/Maestro/Editor/QuestRoomProbe.cs'
$native=[IO.File]::ReadAllText($nativePath)
if(!$native.Contains('timeoutSeconds=ResolveTimeoutSeconds(owner,') -or !$native.Contains('["sessionTimeoutSeconds"]=timeoutSeconds') -or !$native.Contains('["fullJourneyTimeoutMinutes"]=(int?)owner["fullJourneyTimeoutMinutes"]??0') -or !$native.Contains('EditorApplication.timeSinceStartup-started>timeoutSeconds')){throw 'Native execution, ready evidence and deadline must use the validated budget.'}
$cases.Add(@{name='launcher-native-timeout-provenance';status='passed'})
$result=@{version=1;status='passed';cases=$cases;boundary='Pure launcher parameter/scope execution and native source linkage; native admission is separately tested in EditMode. No Editor, browser, device or provider started.'}
if($ReportPath){$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportPath -Encoding utf8}
Write-Output ("Quest probe timeout guards passed: {0} cases." -f $cases.Count)
