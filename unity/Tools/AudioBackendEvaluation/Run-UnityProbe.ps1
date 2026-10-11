# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$UnityArchive,
    [Parameter(Mandatory)][string]$Editor,
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$archive = (Resolve-Path -LiteralPath $UnityArchive).Path
$editorPath = (Resolve-Path -LiteralPath $Editor).Path
$version = (Get-Item -LiteralPath $editorPath).VersionInfo.ProductVersion
if (!$version.StartsWith('6000.3.24f1')) { throw 'This evaluation is pinned to Unity 6000.3.24f1.' }
$digest = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($digest -ne '9b7e3689561670e6b4b8e0c579a2d951bb4a0589f7d46c23bc9df923d62940f0') {
    throw 'Expected the reviewed official Steam Audio Unity 4.8.1 archive.'
}
if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'") { throw 'Wait for the existing Unity Editor to exit first.' }
$id = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$run = Join-Path $repo ('.quest-evidence/audio-unity/' + $id)
if (Test-Path -LiteralPath $run) { throw 'Do not overwrite existing evaluation evidence.' }
New-Item -ItemType Directory -Path $run | Out-Null
$project = Join-Path $run 'project'
$receipt = Join-Path $run 'result.json'
. (Join-Path $PSScriptRoot '../QuestBuildProcesses.ps1')
$record = [ordered]@{
    version = 1; status = 'running'; sourceCommit = (git -C $repo rev-parse HEAD)
    started = [DateTime]::UtcNow.ToString('o'); unity = $version; sdk = '4.8.1'; sdkSha256 = $digest
    project = $project; platform = 'Windows x64'; appRuntimeChanged = $false
    questAcceptance = $false; providerUsed = $false; physicalAudioPlayback = $false
    sourceHashes = @{}
}
foreach ($relative in @('Run-UnityProbe.ps1', 'PrepareUnityProject.py', 'Unity/CandidateTests.cs')) {
    $record.sourceHashes[$relative] = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $relative) -Algorithm SHA256).Hash
}
$record.sourceHashes['AudioManager.asset'] = (Get-FileHash -LiteralPath (Join-Path $repo 'unity/MaestroQuest/ProjectSettings/AudioManager.asset') -Algorithm SHA256).Hash
function Save-Receipt { $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $receipt }
function Invoke-EvaluationEditor([string]$Phase, [string[]]$Extra) {
    if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'") { throw 'Another Unity Editor started; leave it untouched.' }
    $log = Join-Path $run ($Phase + '.log')
    $arguments = @('-batchmode', '-force-d3d11', '-buildTarget', 'Win64', '-projectPath', ('"' + $project + '"'), '-logFile', ('"' + $log + '"')) + $Extra
    Stop-QuestBuildHelper
    $process = Start-Process -FilePath $editorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Environment @{ ADB_SERVER_SOCKET = 'tcp:localhost:5041' }
    $record.phase = $Phase; $record.editorPid = $process.Id; Save-Receipt
    $deadline = [DateTime]::UtcNow.AddMinutes(10)
    try {
        while (!$process.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -gt $deadline) {
                $process.Kill(); $process.WaitForExit()
                throw 'The owned isolated editor exceeded its deadline; a partial report is not success.'
            }
        }
        $record[$Phase + 'Exit'] = $process.ExitCode
    } finally { $process.Dispose(); Stop-QuestBuildHelper }
}
Save-Receipt
try {
    & $Python (Join-Path $PSScriptRoot 'PrepareUnityProject.py') $archive $project *> (Join-Path $run 'prepare.log')
    if ($LASTEXITCODE -ne 0) { throw 'Project preparation failed; see prepare.log.' }
    Invoke-EvaluationEditor 'configure' @('-quit', '-executeMethod', 'Maestro.AudioEvaluation.Configure.Run')
    if ($record.configureExit -ne 0) { throw 'Candidate configuration failed; see configure.log.' }
    $result = Join-Path $run 'tests.xml'
    Invoke-EvaluationEditor 'playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testFilter', 'Maestro.AudioEvaluation.CandidateTests', '-testResults', ('"' + $result + '"'))
    if (!(Test-Path -LiteralPath $result)) { throw 'The candidate produced no test report.' }
    [xml]$xml = Get-Content -LiteralPath $result
    $record.tests = @($xml.SelectNodes('//test-case') | ForEach-Object {
        @{ name = $_.name; result = $_.result; message = $_.failure.message.InnerText }
    })
    $expected = @('EditableGeometryAndIndependentVoicesRenderThroughUnity', 'ExistingVoiceSurvivesSameConfigurationAudioReset', 'ChangedDeviceFormatUpdatesSimulationConfiguration')
    $actual = @($xml.SelectNodes('//test-case') | ForEach-Object { $_.name })
    if (@(Compare-Object ($expected | Sort-Object) ($actual | Sort-Object)).Count -ne 0 -or
        $record.playmodeExit -ne 0 -or $xml.'test-run'.result -ne 'Passed' -or [int]$xml.'test-run'.passed -ne 3) {
        throw 'Candidate requirements did not pass. Retain the failed cases; this does not alter the app native gate.'
    }
    $record.status = 'passed'
} catch {
    $record.status = 'failed'; $record.error = $_.ToString(); throw
} finally {
    $record.finished = [DateTime]::UtcNow.ToString('o'); Save-Receipt
}
Write-Output "Isolated Unity candidate evaluation passed: $receipt"
