# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$Inventory,
 [Parameter(Mandatory)][ValidateSet('EditMode','PlayMode')][string]$Platform,
 [Parameter(Mandatory)][string[]]$Results,
 [string[]]$AllowedSkipped=@()
)
$ErrorActionPreference='Stop'
$catalog=Get-Content -LiteralPath $Inventory -Raw | ConvertFrom-Json
$key=if($Platform -eq 'EditMode'){'editMode'}else{'playMode'}
$expected=@($catalog.$key)
if(!$expected.Count -or @($expected|Where-Object {$_ -isnot [string] -or !$_}).Count){throw 'Native test inventory is empty or malformed.'}
$names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach($name in $expected){if(!$names.Add($name)){throw "Duplicate inventory case: $name"}}
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$passed=0;$skipped=0
foreach($path in $Results){
 [xml]$report=Get-Content -LiteralPath $path -Raw
 $cases=@($report.SelectNodes('//test-case'))
 if(!$report.'test-run' -or !$cases.Count){throw "Empty or incomplete native result: $path"}
 if([int]$report.'test-run'.total -ne $cases.Count){throw "Native result count does not match its cases: $path"}
 if($report.'test-run'.result -notin @('Passed','Skipped:Ignored') -or [int]$report.'test-run'.failed -gt 0 -or [int]$report.'test-run'.inconclusive -gt 0){throw "Native run did not pass: $path"}
 foreach($case in $cases){
  $name=[string]$case.fullname
  if(!$names.Contains($name)){throw "Unexpected native case: $name"}
  if(!$seen.Add($name)){throw "Duplicate native case across batches: $name"}
  if($case.result -eq 'Passed'){$passed++}
  elseif($case.result -eq 'Skipped' -and $case.label -eq 'Ignored' -and $AllowedSkipped -ccontains $name){$skipped++}
  else{throw "Native case did not pass: $name ($($case.result))"}
 }
}
$missing=@($expected|Where-Object {!$seen.Contains($_)})
if($missing.Count){throw "Missing $($missing.Count) discovered native cases; first: $($missing[0])"}
[pscustomobject]@{platform=$Platform;discovered=$expected.Count;completed=$seen.Count;passed=$passed;skipped=$skipped;batches=$Results.Count}
