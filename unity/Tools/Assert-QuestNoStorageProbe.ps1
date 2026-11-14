# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Apk)
$ErrorActionPreference='Stop'
$archive=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Apk).Path)
try{
 if($archive.Entries | Where-Object {$_.FullName -match '(^|/)(wrap[.]sh|libmaestro_storage_fault[.]so)$'}){
  throw 'Storage fault injection or a native startup wrapper is present in a normal Maestro package.'
 }
}finally{$archive.Dispose()}
