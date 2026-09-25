# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Editor,
    [Parameter(Mandatory)][string]$BuildMirror,
    [switch]$RenderArt,
    [string]$PageCapture
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sourceProject = Join-Path $repoRoot 'unity/MaestroQuest'
$mirrorRoot = [IO.Path]::GetFullPath($BuildMirror).TrimEnd('\','/')
$editorPath = (Resolve-Path -LiteralPath $Editor).Path
if ($mirrorRoot.Length -gt 80 -or $mirrorRoot.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use a short, dedicated build directory outside the repository.'
}
if ($mirrorRoot -eq [IO.Path]::GetPathRoot($mirrorRoot).TrimEnd('\','/')) { throw 'The drive root cannot be a build mirror.' }
$receiptPath = Join-Path $mirrorRoot '.maestro-build-mirror.json'
if (Test-Path -LiteralPath $mirrorRoot) {
    if (!(Test-Path -LiteralPath $receiptPath)) { throw 'Existing directory has no Maestro build receipt; choose a new empty path.' }
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if ($receipt.source -ne $sourceProject) { throw 'Build mirror belongs to a different source project.' }
} else {
    New-Item -ItemType Directory -Path $mirrorRoot | Out-Null
    @{source=$sourceProject} | ConvertTo-Json | Set-Content -LiteralPath $receiptPath
}
foreach ($folder in @('Assets','Packages','ProjectSettings')) {
    $from = Join-Path $sourceProject $folder
    $to = Join-Path $mirrorRoot $folder
    New-Item -ItemType Directory -Path $to -Force | Out-Null
    # Remove only obsolete source files in this receipt-owned copy. Cache/log
    # directories are outside these three roots and are never touched.
    foreach ($file in Get-ChildItem -LiteralPath $to -Recurse -File) {
        $resolvedFile = [IO.Path]::GetFullPath($file.FullName)
        if (!$resolvedFile.StartsWith($to + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected mirror path.' }
        $relative = [IO.Path]::GetRelativePath($to, $resolvedFile)
        if (!(Test-Path -LiteralPath (Join-Path $from $relative))) { Remove-Item -LiteralPath $resolvedFile }
    }
    Copy-Item -Path "$from/*" -Destination $to -Recurse -Force
}
$logRoot = Join-Path $mirrorRoot 'Logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
function Invoke-QuestEditor([string[]]$Arguments, [string]$LogName) {
    $logPath = Join-Path $logRoot $LogName
    $argumentsWithPaths = @('-batchmode','-projectPath', ('"' + $mirrorRoot + '"'), '-logFile', ('"' + $logPath + '"')) + $Arguments
    $process = Start-Process -FilePath $editorPath -ArgumentList $argumentsWithPaths -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $logPath -Tail 50; throw "Unity exited $($process.ExitCode); see $logPath" }
}
Invoke-QuestEditor @('-quit','-executeMethod','Maestro.Quest.Editor.QuestProjectSetup.Configure') 'configure.log'
$testResult = Join-Path $logRoot 'editmode-results.xml'
Invoke-QuestEditor @('-runTests','-testPlatform','EditMode','-testResults', ('"' + $testResult + '"')) 'editmode.log'
[xml]$testReport = Get-Content -LiteralPath $testResult
if ($testReport.'test-run'.result -ne 'Passed' -or [int]$testReport.'test-run'.total -lt 6) { throw 'Unity test results did not satisfy the current development checks.' }
if ($RenderArt) {
    $env:MAESTRO_ART_EVIDENCE = Join-Path $repoRoot '.quest-evidence/art'
    $env:MAESTRO_BOOK_PREVIEW_TEXTURE = if ($PageCapture) { (Resolve-Path -LiteralPath $PageCapture).Path } else { '' }
    Invoke-QuestEditor @('-quit','-executeMethod','Maestro.Quest.Editor.QuestArtPreview.Render') 'art-preview.log'
}
Write-Output "Unity checks passed: $($testReport.'test-run'.passed) tests. Evidence: $logRoot"
