# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
# Probe-only ownership/path admission. No Editor, build, device or provider operations.
function Get-QuestProbeCanonicalPath([string]$Path){
 if([string]::IsNullOrWhiteSpace($Path) -or ![IO.Path]::IsPathFullyQualified($Path)){throw 'Probe evidence paths must be explicit absolute paths.'}
 if($Path.StartsWith('\\') -or $Path.StartsWith('//')){throw 'Use a normal local probe path, not a UNC or device-prefix alias.'}
 $parts=$Path -split '[\\/]'
 if(@($parts | Where-Object {$_ -in @('.','..') -or $_ -match '[. ]$' -or $_ -match '~[0-9]+(?:\.|$)'}).Count){throw 'Probe evidence paths cannot contain dot segments, trailing dots/spaces or short-name aliases.'}
 return [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Path))
}
function Assert-QuestProbeEvidenceParents([string]$Path,[switch]$Leaf){
 $current=$Path;$first=$true
 while($current){
  try{$attributes=[IO.File]::GetAttributes($current)}catch [IO.FileNotFoundException]{$attributes=$null}catch [IO.DirectoryNotFoundException]{$attributes=$null}
  if($null -ne $attributes){
   if($attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Probe evidence paths cannot traverse links or reparse points.'}
   if(!($attributes -band [IO.FileAttributes]::Directory) -and (!$first -or !$Leaf)){throw 'A probe evidence directory is occupied by a file.'}
  }
  $first=$false;$current=[IO.Path]::GetDirectoryName($current)
 }
}
function Read-QuestProbeEvidenceJson([string]$Path,[int]$Maximum=4096){
 Assert-QuestProbeEvidenceParents $Path -Leaf
 $info=Get-Item -LiteralPath $Path -Force -ErrorAction Stop
 if($info.PSIsContainer -or $info.Length -lt 1 -or $info.Length -gt $Maximum){throw 'Probe ownership must be a bounded regular JSON file.'}
 $bytes=[IO.File]::ReadAllBytes($Path)
 if($bytes.Length -lt 1 -or $bytes.Length -gt $Maximum){throw 'Probe ownership byte limit exceeded.'}
 $text=[Text.UTF8Encoding]::new($false,$true).GetString($bytes).TrimStart([char]0xfeff)
 $document=[System.Text.Json.JsonDocument]::Parse($text)
 try{
  if($document.RootElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Object){throw 'Probe ownership must be a JSON object.'}
  $keys=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
  foreach($property in $document.RootElement.EnumerateObject()){if(!$keys.Add($property.Name)){throw 'Duplicate probe ownership field.'}}
 }finally{$document.Dispose()}
 return ConvertFrom-Json -InputObject $text -AsHashtable -Depth 8
}
function Assert-QuestProbeWorkspaceEnvelope([string]$Root,[bool]$Windows=$IsWindows){
 # The longest canonical native write is generation/data/images/<hash>.image.<guid>.part.
 # Reserve its full 191-character suffix, including separators, before any run is created.
 $workspace=Join-Path (Join-Path $Root ('0'*32)) 'workspace'
 $maximum=$workspace.Length+191
 if($Windows -and $maximum -gt 255){throw "Windows Editor probe storage requires workspace path + 191 <= 255 characters (got $maximum). Choose a fresh short owned -EvidenceRoot, for example D:/Projects/Builds/.q. No run or Editor was created."}
 return @{workspaceLength=$workspace.Length;nativeSuffix=191;maximumPathLength=$maximum;windows=[bool]$Windows}
}
function Get-QuestProbeEvidencePlan([string]$EvidenceRoot,[string]$RepoRoot,[string]$BuildMirror,[bool]$Windows=$IsWindows){
 $repo=Get-QuestProbeCanonicalPath $RepoRoot;$mirror=Get-QuestProbeCanonicalPath $BuildMirror
 $explicit=![string]::IsNullOrWhiteSpace($EvidenceRoot)
 $root=Get-QuestProbeCanonicalPath $(if($explicit){$EvidenceRoot}else{Join-Path $repo '.quest-evidence/native-room'})
 if($root -eq [IO.Path]::GetPathRoot($root)){throw 'A probe evidence root must be below the drive root.'}
 Assert-QuestProbeEvidenceParents $repo;Assert-QuestProbeEvidenceParents $mirror;Assert-QuestProbeEvidenceParents $root
 $envelope=Assert-QuestProbeWorkspaceEnvelope $root $Windows
 $marker=Join-Path $root '.maestro-probe-evidence.json'
 if($explicit -and [IO.Directory]::Exists($root)){
  if(![IO.File]::Exists($marker)){throw 'An explicit EvidenceRoot already exists without its exact owner marker; choose a fresh directory. Existing evidence is unchanged.'}
  $owner=Read-QuestProbeEvidenceJson $marker
  if($owner.Count -ne 2 -or !$owner.Contains('source') -or !$owner.Contains('buildMirror') -or $owner.source -isnot [string] -or $owner.buildMirror -isnot [string] -or $owner.source -cne $repo -or $owner.buildMirror -cne $mirror){throw 'The explicit EvidenceRoot belongs to another source or build mirror, or its ownership is malformed.'}
 }
 return @{root=$root;source=$repo;buildMirror=$mirror;explicit=$explicit;marker=$marker;envelope=$envelope;ownership=$(if($explicit){'exact-root-marker'}else{'repository-default'})}
}
function New-QuestProbeEvidenceRun($Plan,[string]$Id){
 if($Id -cnotmatch '^[a-f0-9]{32}$'){throw 'Probe runs require an exact fresh 32-character identity.'}
 # Recheck ownership immediately before mutation. Never adopt an existing unmarked custom root.
 $plan=Get-QuestProbeEvidencePlan $(if($Plan.explicit){$Plan.root}else{''}) $Plan.source $Plan.buildMirror $Plan.envelope.windows
 if(![IO.Directory]::Exists($plan.root)){
  New-Item -ItemType Directory -Path $plan.root -ErrorAction Stop | Out-Null
  Assert-QuestProbeEvidenceParents $plan.root
  if($plan.explicit){
   $bytes=[Text.UTF8Encoding]::new($false).GetBytes((@{source=$plan.source;buildMirror=$plan.buildMirror} | ConvertTo-Json -Compress))
   $stream=[IO.FileStream]::new($plan.marker,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
   try{$stream.Write($bytes,0,$bytes.Length);$stream.Flush($true)}finally{$stream.Dispose()}
  }
 }
 $directory=Join-Path $plan.root $Id
 Assert-QuestProbeEvidenceParents $directory
 if([IO.Directory]::Exists($directory) -or [IO.File]::Exists($directory)){throw 'Probe run directories cannot be reused.'}
 New-Item -ItemType Directory -Path $directory -ErrorAction Stop | Out-Null
 Assert-QuestProbeEvidenceParents $directory
 return $directory
}
function Get-QuestProbeResumeDirectory($Plan,[string]$Id){
 if($Id -cnotmatch '^[a-f0-9]{32}$'){throw 'Resume requires an exact prior probe identity.'}
 $plan=Get-QuestProbeEvidencePlan $(if($Plan.explicit){$Plan.root}else{''}) $Plan.source $Plan.buildMirror $Plan.envelope.windows
 $directory=Join-Path $plan.root $Id
 Assert-QuestProbeEvidenceParents $directory
 if(![IO.Directory]::Exists($directory)){throw 'The resume run does not exist under the selected evidence root. No alternate root is searched.'}
 $owner=Read-QuestProbeEvidenceJson (Join-Path $directory 'owner.json') 16384
 if($owner.version -ne 1 -or $owner.id -isnot [string] -or $owner.id -cne $Id){throw 'The prior run owner does not match the requested resume identity.'}
 return $directory
}
function Assert-QuestProbeResumeTree([string]$Directory){
 Assert-QuestProbeEvidenceParents $Directory
 if(![IO.Directory]::Exists($Directory)){throw 'The prior workspace or profile is missing.'}
 $pending=[Collections.Generic.Stack[string]]::new();$pending.Push($Directory);$count=0
 while($pending.Count){
  foreach($path in [IO.Directory]::EnumerateFileSystemEntries($pending.Pop())){
   if(++$count -gt 100000){throw 'The prior probe tree exceeds the bounded resume inventory.'}
   $attributes=[IO.File]::GetAttributes($path)
   if($attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Resume cannot copy links or reparse points.'}
   if($attributes -band [IO.FileAttributes]::Directory){$pending.Push($path)}
  }
 }
}
