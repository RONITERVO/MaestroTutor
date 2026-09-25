# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Editor,
    [Parameter(Mandatory)][string]$BuildMirror,
    [switch]$RenderArt,
    [switch]$RenderRules,
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
function Invoke-QuestEditor([string[]]$Arguments, [string]$LogName, [string]$ResultPath = '') {
    $logPath = Join-Path $logRoot $LogName
    $argumentsWithPaths = @('-batchmode','-projectPath', ('"' + $mirrorRoot + '"'), '-logFile', ('"' + $logPath + '"')) + $Arguments
    if ($ResultPath -and (Test-Path -LiteralPath $ResultPath)) { Remove-Item -LiteralPath $ResultPath }
    if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath }
    # KAT Gateway owns the machine's default ADB server. Unity's shutdown can
    # hang trying to stop it; give build children their own server endpoint.
    $process = Start-Process -FilePath $editorPath -ArgumentList $argumentsWithPaths -WindowStyle Hidden -PassThru -Environment @{ ADB_SERVER_SOCKET = 'tcp:localhost:5041' }
    $deadline = [DateTime]::UtcNow.AddMinutes(20)
    $reportWrittenAt = $null
    while (!$process.WaitForExit(1000)) {
        if (!$reportWrittenAt -and (($ResultPath -and (Test-Path -LiteralPath $ResultPath)) -or
            ((Test-Path -LiteralPath $logPath) -and (Select-String -LiteralPath $logPath -Pattern 'Batchmode quit successfully invoked' -Quiet)))) { $reportWrittenAt = [DateTime]::UtcNow }
        if ([DateTime]::UtcNow -gt $deadline -or ($reportWrittenAt -and [DateTime]::UtcNow -gt $reportWrittenAt.AddSeconds(60))) {
            $process.Kill(); $process.WaitForExit()
            throw "Unity verification did not exit in time; see $logPath. A written test report alone is not a successful build."
        }
    }
    if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $logPath -Tail 50; throw "Unity exited $($process.ExitCode); see $logPath" }
}
Invoke-QuestEditor @('-quit','-executeMethod','Maestro.Quest.Editor.QuestProjectSetup.Configure') 'configure.log'
$testResult = Join-Path $logRoot 'editmode-results.xml'
Invoke-QuestEditor @('-runTests','-testPlatform','EditMode','-testResults', ('"' + $testResult + '"')) 'editmode.log' $testResult
[xml]$testReport = Get-Content -LiteralPath $testResult
if ($testReport.'test-run'.result -ne 'Passed' -or [int]$testReport.'test-run'.total -lt 23) { throw 'Unity test results did not satisfy the current development checks.' }
$playResult = Join-Path $logRoot 'playmode-results.xml'
Invoke-QuestEditor @('-runTests','-testPlatform','PlayMode','-testResults', ('"' + $playResult + '"')) 'playmode.log' $playResult
[xml]$playReport = Get-Content -LiteralPath $playResult
if ($playReport.'test-run'.result -ne 'Passed' -or [int]$playReport.'test-run'.total -lt 10) { throw 'Unity interaction tests did not pass.' }
if ($RenderArt) {
    $env:MAESTRO_ART_EVIDENCE = Join-Path $repoRoot '.quest-evidence/art'
    $env:MAESTRO_BOOK_PREVIEW_TEXTURE = if ($PageCapture) { (Resolve-Path -LiteralPath $PageCapture).Path } else { '' }
    Invoke-QuestEditor @('-quit','-executeMethod','Maestro.Quest.Editor.QuestArtPreview.Render') 'art-preview.log'
}
if ($RenderRules) {
    $env:MAESTRO_ART_EVIDENCE = Join-Path $repoRoot '.quest-evidence/art'
    Invoke-QuestEditor @('-quit','-executeMethod','Maestro.Quest.Editor.QuestArtPreview.RenderRules') 'rule-preview.log'
}
Write-Output "Unity checks passed: $($testReport.'test-run'.passed) EditMode and $($playReport.'test-run'.passed) PlayMode tests. Evidence: $logRoot"
