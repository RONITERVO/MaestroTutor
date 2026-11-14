# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
param([string]$ReportPath)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'QuestProbeEvidence.ps1')
$cases=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[scriptblock]$Action){& $Action;$cases.Add(@{name=$Name;status='passed'})}
function Reject([scriptblock]$Action,[string]$Pattern){$message=$null;try{& $Action | Out-Null}catch{$message=$_.Exception.Message};if(!$message -or $message -notmatch $Pattern){throw "Expected rejection /$Pattern/, got '$message'."}}
function Need([bool]$Condition,[string]$Message){if(!$Condition){throw $Message}}
$tempBase=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot=Join-Path $tempBase ('maestro-probe-evidence-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
try{
 $repo=Join-Path $testRoot 'repo';$mirror=Join-Path $testRoot 'mirror'
 New-Item -ItemType Directory -Path $repo,$mirror | Out-Null
 $chosen=Join-Path $testRoot 'explicit';$id='1'*32
 Check 'windows-boundary-255' {
  $short=$(if($IsWindows){'D:\'+('q'*18)}else{'/'+('q'*20)})
  $budget=Assert-QuestProbeWorkspaceEnvelope $short $true
  Need ($budget.workspaceLength -eq 64 -and $budget.maximumPathLength -eq 255) 'Wrong canonical Windows path envelope.'
 }
 Check 'windows-over-boundary-before-creation' {
  Reject {Assert-QuestProbeWorkspaceEnvelope $(if($IsWindows){'D:\'+('q'*19)}else{'/'+('q'*21)}) $true} 'EvidenceRoot'
  $absent=Join-Path $testRoot 'must-not-create'
  Reject {Get-QuestProbeEvidencePlan $absent $repo $mirror $true} 'EvidenceRoot'
  Need (![IO.Directory]::Exists($absent)) 'Admission created an over-budget directory.'
 }
 Check 'non-windows-keeps-normal-paths' {Need ((Assert-QuestProbeWorkspaceEnvelope $chosen $false).maximumPathLength -gt 255) 'Expected an intentionally long temp fixture path.'}
 Check 'explicit-preflight-is-read-only' {$plan=Get-QuestProbeEvidencePlan $chosen $repo $mirror $false;Need (!$plan.root.EndsWith([IO.Path]::DirectorySeparatorChar) -and ![IO.Directory]::Exists($chosen)) 'Preflight mutated or aliased the root.'}
 $plan=Get-QuestProbeEvidencePlan $chosen $repo $mirror $false
 Check 'fresh-explicit-root-and-run' {
  $script:run=New-QuestProbeEvidenceRun $plan $id
  Need ($script:run -ceq (Join-Path $chosen $id)) 'Run identity/path changed.'
  $owner=Read-QuestProbeEvidenceJson $plan.marker
  Need ($owner.Count -eq 2 -and $owner.source -ceq $repo -and $owner.buildMirror -ceq $mirror) 'Root owner mismatch.'
 }
 Check 'same-owner-second-run-preserves-first' {
  [IO.File]::WriteAllText((Join-Path $script:run 'kept.txt'),'original')
  $next=New-QuestProbeEvidenceRun $plan ('2'*32)
  Need ([IO.Directory]::Exists($next) -and [IO.File]::ReadAllText((Join-Path $script:run 'kept.txt')) -ceq 'original') 'A new run changed old evidence.'
 }
 Check 'run-reuse-refused' {Reject {New-QuestProbeEvidenceRun $plan $id} 'cannot be reused'}
 Check 'invalid-id-refused' {Reject {New-QuestProbeEvidenceRun $plan '../elsewhere'} 'identity'}
 Check 'foreign-source-refused' {Reject {Get-QuestProbeEvidencePlan $chosen (Join-Path $testRoot 'other-repo') $mirror $false} 'another source'}
 Check 'foreign-mirror-refused' {Reject {Get-QuestProbeEvidencePlan $chosen $repo (Join-Path $testRoot 'other-mirror') $false} 'another source'}
 Check 'existing-unmarked-custom-root-not-adopted' {
  $unowned=Join-Path $testRoot 'unowned';New-Item -ItemType Directory -Path $unowned | Out-Null
  [IO.File]::WriteAllText((Join-Path $unowned 'old.txt'),'preserve')
  Reject {Get-QuestProbeEvidencePlan $unowned $repo $mirror $false} 'without its exact owner marker'
  Need (@(Get-ChildItem -LiteralPath $unowned -Force).Count -eq 1) 'An unowned directory was claimed.'
 }
 Check 'malformed-and-extra-owner-fields-refused' {
  $original=[IO.File]::ReadAllBytes($plan.marker)
  try{
   foreach($json in @('null','[]','{}','{"source":"a","source":"b","buildMirror":"c"}',(@{source=$repo;buildMirror=$mirror;extra=$true}|ConvertTo-Json -Compress))){
    [IO.File]::WriteAllText($plan.marker,$json);Reject {Get-QuestProbeEvidencePlan $chosen $repo $mirror $false} 'ownership|owner|another source|JSON'
   }
  }finally{[IO.File]::WriteAllBytes($plan.marker,$original)}
 }
 Check 'late-unowned-directory-not-adopted' {
  $late=Join-Path $testRoot 'late';$latePlan=Get-QuestProbeEvidencePlan $late $repo $mirror $false
  New-Item -ItemType Directory -Path $late | Out-Null
  Reject {New-QuestProbeEvidenceRun $latePlan ('3'*32)} 'without its exact owner marker'
 }
 Check 'relative-dot-device-and-short-alias-paths-refused' {
  foreach($path in @('relative', (Join-Path $testRoot '../alias'), '\\?\D:\probe', '\\.\D:\probe', '//server/share', (Join-Path $testRoot 'ALIAS~1'), (Join-Path $testRoot 'trailing.'))){Reject {Get-QuestProbeCanonicalPath $path} 'path|alias|absolute'}
 }
 Check 'file-in-directory-position-refused' {
  $file=Join-Path $testRoot 'occupied';[IO.File]::WriteAllText($file,'keep')
  Reject {Get-QuestProbeEvidencePlan $file $repo $mirror $false} 'occupied by a file'
  Reject {Get-QuestProbeEvidencePlan (Join-Path $file 'child') $repo $mirror $false} 'occupied by a file'
 }
 Check 'legacy-default-root-and-resume-preserved' {
  $default=Join-Path $repo '.quest-evidence/native-room';$legacy=Join-Path $default ('a'*32)
  New-Item -ItemType Directory -Path $legacy | Out-Null
  [IO.File]::WriteAllText((Join-Path $legacy 'owner.json'),'{"version":1,"id":"'+('a'*32)+'"}')
  $legacyPlan=Get-QuestProbeEvidencePlan '' $repo $mirror $false
  Need ($legacyPlan.ownership -ceq 'repository-default' -and (Get-QuestProbeResumeDirectory $legacyPlan ('a'*32)) -ceq $legacy) 'Legacy namespace/resume lost.'
  [void](New-QuestProbeEvidenceRun $legacyPlan ('b'*32))
  Need (![IO.File]::Exists($legacyPlan.marker)) 'Legacy default root was re-claimed.'
 }
 Check 'resume-only-selected-root-and-exact-owner' {
  [IO.File]::WriteAllText((Join-Path $script:run 'owner.json'),'{"version":1,"id":"'+$id+'"}')
  Need ((Get-QuestProbeResumeDirectory $plan $id) -ceq $script:run) 'Wrong resume location.'
  Reject {Get-QuestProbeResumeDirectory $plan ('a'*32)} 'selected evidence root'
  [IO.File]::WriteAllText((Join-Path $script:run 'owner.json'),'{"version":1,"id":"'+('f'*32)+'"}')
  Reject {Get-QuestProbeResumeDirectory $plan $id} 'owner does not match'
 }
 Check 'links-rejected-without-traversal' {
  $target=Join-Path $testRoot 'link-target';New-Item -ItemType Directory -Path $target | Out-Null
  $link=Join-Path $testRoot 'linked'
  New-Item -ItemType $(if($IsWindows){'Junction'}else{'SymbolicLink'}) -Path $link -Target $target | Out-Null
  try{
   Reject {Get-QuestProbeEvidencePlan $link $repo $mirror $false} 'links or reparse'
   Reject {Get-QuestProbeEvidencePlan (Join-Path $link 'child') $repo $mirror $false} 'links or reparse'
   Reject {Assert-QuestProbeResumeTree $testRoot} 'links or reparse'
  }finally{Remove-Item -LiteralPath $link -Force}
  Need ([IO.Directory]::Exists($target)) 'Link refusal/cleanup touched its target.'
 }
 Check 'packaging-entry-chain-forwards-without-execution' {
  foreach($name in @('Build-QuestDevelopment.ps1','Build-QuestRelease.ps1','Build-QuestPackage.ps1')){
   $path=Join-Path $PSScriptRoot $name;$tokens=$null;$errors=$null
   $ast=[Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$errors)
   Need ($errors.Count -eq 0) "Packaging helper no longer parses: $name"
   $parameter=@($ast.ParamBlock.Parameters|Where-Object {$_.Name.VariablePath.UserPath -eq 'EvidenceRoot'})
   Need ($parameter.Count -eq 1 -and !$parameter[0].Extent.Text.Contains('Mandatory')) "Packaging helper must accept optional EvidenceRoot: $name"
   $target=$(if($name -eq 'Build-QuestPackage.ps1'){'Verify-Quest.ps1'}else{'Build-QuestPackage.ps1'})
   $calls=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.Extent.Text.StartsWith("& (Join-Path `$PSScriptRoot '$target')")},$true))
   Need ($calls.Count -eq 1) "Expected one packaging-chain call: $name"
   if($name -eq 'Build-QuestPackage.ps1'){Need ($calls[0].Extent.Text.Contains('-EvidenceRoot $EvidenceRoot')) 'Package helper omitted the selected verification root.'}
   else{Need (@($calls[0].CommandElements|Where-Object {$_ -is [Management.Automation.Language.VariableExpressionAst] -and $_.Splatted -and $_.VariablePath.UserPath -eq 'PSBoundParameters'}).Count -eq 1) "Public packaging entry must forward its bound options: $name"}
  }
 }
 Check 'top-level-verification-preflights-and-forwards' {
  $path=Join-Path $PSScriptRoot 'Verify-Quest.ps1';$tokens=$null;$errors=$null
  $ast=[Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$errors)
  Need ($errors.Count -eq 0) 'Top-level verification no longer parses.'
  Need (@($ast.ParamBlock.Parameters|Where-Object {$_.Name.VariablePath.UserPath -eq 'EvidenceRoot'}).Count -eq 1) 'Top-level verification lost EvidenceRoot.'
  $calls=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.Extent.Text.StartsWith("& (Join-Path `$PSScriptRoot 'Run-QuestRoomProbe.ps1')")},$true))
  Need ($calls.Count -eq 3) 'Expected three independent complete verification journeys.'
  foreach($call in $calls){Need ($call.Extent.Text.Contains('-EvidenceRoot $EvidenceRoot')) 'A verification journey omitted the selected root.'}
  $source=[IO.File]::ReadAllText($path)
  $admission=$source.IndexOf('[void](Get-QuestProbeEvidencePlan $EvidenceRoot $repoRoot $mirrorRoot)');$mutation=$source.IndexOf('New-Item -ItemType Directory')
  Need ($admission -ge 0 -and $mutation -ge 0) 'Verification admission/order anchor is missing.'
  Need ($admission -lt $mutation) 'Verification must reject an unsupported envelope before mirror mutation.'
 }
 Check 'launcher-preflights-before-typecheck-and-Editor' {
  $path=Join-Path $PSScriptRoot 'Run-QuestRoomProbe.ps1';$tokens=$null;$errors=$null
  $ast=[Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$errors)
  Need ($errors.Count -eq 0) 'Launcher no longer parses.'
  Need (@($ast.ParamBlock.Parameters|Where-Object {$_.Name.VariablePath.UserPath -eq 'EvidenceRoot'}).Count -eq 1) 'Missing explicit EvidenceRoot option.'
  $source=[IO.File]::ReadAllText($path)
  $admission=$source.IndexOf('$evidencePlan=Get-QuestProbeEvidencePlan');$typeCheck=$source.IndexOf('& $typeChecker')
  $creation=$source.IndexOf('$directory=New-QuestProbeEvidenceRun');$launch=$source.IndexOf('$process=& $startEditor')
  Need ($admission -ge 0 -and $typeCheck -ge 0 -and $creation -ge 0 -and $launch -ge 0) 'Launcher admission/order anchor is missing.'
  Need ($admission -lt $typeCheck -and $admission -lt $creation -and $creation -lt $launch) 'Launcher admission order changed.'
  Need ([regex]::Matches($source,'evidenceRoot=\$evidencePlan.root').Count -eq 2) 'Owner and verified receipts must record the selected root.'
  Need ([regex]::Matches($source,'Get-QuestProbeResumeDirectory \$evidencePlan').Count -eq 2) 'Both resume modes must use the selected root.'
 }
}finally{
 $resolved=[IO.Path]::GetFullPath($testRoot)
 if(!$resolved.StartsWith($tempBase,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^maestro-probe-evidence-test-[a-f0-9]{32}$'){throw 'Refusing unexpected test cleanup target.'}
 Assert-QuestProbeEvidenceParents $resolved
 Remove-Item -LiteralPath $resolved -Recurse -Force
}
$result=@{version=1;status='passed';cases=$cases;boundary='Offline PowerShell path/ownership and launcher-admission checks. Temporary test fixtures only; no real EvidenceRoot, Editor, browser, device or provider was opened.'}
if($ReportPath){$result|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $ReportPath -Encoding utf8}
Write-Output ("Quest probe evidence guards passed: {0} cases." -f $cases.Count)
