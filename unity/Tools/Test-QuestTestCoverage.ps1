# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$taskTemp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)
$taskRoot=Join-Path $taskTemp ('MaestroTestCoverage-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskRoot | Out-Null
$checker=Join-Path $PSScriptRoot 'Assert-QuestTestCoverage.ps1'
function Case([string]$Name,[string]$Result='Passed',[string]$Label=''){
 return "<test-case fullname='$Name' name='$($Name.Split('.')[-1])' result='$Result' label='$Label'/>"
}
function Report([string]$Name,[string[]]$Cases){
 $path=Join-Path $taskRoot $Name
 Set-Content -LiteralPath $path -Value ("<test-run result='Passed' total='"+$Cases.Count+"'>"+($Cases -join '')+"</test-run>")
 return $path
}
function Reject([scriptblock]$Run,[string]$Reason){
 try{& $Run | Out-Null}catch{if($_.Exception.Message -notlike "*$Reason*"){throw};return}
 throw "Expected coverage rejection: $Reason"
}
try{
 $inventory=Join-Path $taskRoot 'inventory.json'
 '{"editMode":["Tests.A","Tests.B"],"playMode":["Tests.A","Tests.B"]}' | Set-Content -LiteralPath $inventory
 $a=Report 'a.xml' @((Case 'Tests.A'));$b=Report 'b.xml' @((Case 'Tests.B'))
 $proof=& $checker -Inventory $inventory -Platform PlayMode -Results @($a,$b)
 if($proof.passed -ne 2 -or $proof.batches -ne 2){throw 'Complete disjoint coverage was not accepted.'}
 Reject {& $checker -Inventory $inventory -Platform PlayMode -Results @($a)} 'Missing'
 Reject {& $checker -Inventory $inventory -Platform PlayMode -Results @($a,$a,$b)} 'Duplicate native'
 $extra=Report 'extra.xml' @((Case 'Tests.C'))
 Reject {& $checker -Inventory $inventory -Platform PlayMode -Results @($a,$extra)} 'Unexpected'
 $suiteFailure=Report 'suite-failure.xml' @((Case 'Tests.A'),(Case 'Tests.B'))
 (Get-Content -LiteralPath $suiteFailure -Raw).Replace("<test-run result='Passed'","<test-run result='Failed'")|Set-Content -LiteralPath $suiteFailure
 Reject {& $checker -Inventory $inventory -Platform PlayMode -Results @($suiteFailure)} 'run did not pass'
 $failed=Report 'failed.xml' @((Case 'Tests.B' 'Failed'))
 Reject {& $checker -Inventory $inventory -Platform PlayMode -Results @($a,$failed)} 'did not pass'
 $ignored=Report 'ignored.xml' @((Case 'Tests.B' 'Skipped' 'Ignored'))
 Reject {& $checker -Inventory $inventory -Platform PlayMode -Results @($a,$ignored)} 'did not pass'
 Reject {& $checker -Inventory $inventory -Platform PlayMode -Results @($a,$ignored) -AllowedSkipped B} 'did not pass'
 $proof=& $checker -Inventory $inventory -Platform PlayMode -Results @($a,$ignored) -AllowedSkipped Tests.B
 if($proof.skipped -ne 1 -or $proof.passed -ne 1){throw 'Explicit optional case was not recorded.'}
 $empty=Report 'empty.xml' @()
 Reject {& $checker -Inventory $inventory -Platform PlayMode -Results @($empty)} 'Empty'
 '{"editMode":[],"playMode":[]}' | Set-Content -LiteralPath $inventory
 Reject {& $checker -Inventory $inventory -Platform PlayMode -Results @($a,$b)} 'empty or malformed'
 'Native test coverage checks passed: complete batches, missing, duplicate, unexpected, failed, optional and empty evidence.'
}finally{
 $resolved=[IO.Path]::GetFullPath($taskRoot)
 if(!$resolved.StartsWith($taskTemp+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notlike 'MaestroTestCoverage-*'){throw 'Unexpected test cleanup target.'}
 Remove-Item -LiteralPath $resolved -Recurse -Force
}
