# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference = 'Stop'
$logPath = Join-Path ([IO.Path]::GetTempPath()) ('maestro-unity-log-' + [Guid]::NewGuid().ToString('N') + '.log')
$gate = Join-Path $PSScriptRoot 'Assert-QuestUnityLog.ps1'
function Assert-Rejected([string]$Expected, [switch]$Facts) {
    $rejected = $false
    try { & $gate -LogPath $logPath -RejectFactReadFailures:$Facts }
    catch { if ($_.Exception.Message -notlike $Expected) { throw }; $rejected = $true }
    if (!$rejected) { throw 'An invalid Unity verification log was accepted.' }
}
try {
    Assert-Rejected 'Unity verification log is missing or empty:*'
    [IO.File]::WriteAllText($logPath, '')
    Assert-Rejected 'Unity verification log is missing or empty:*'
    [IO.File]::WriteAllText($logPath, "Loading project`nScript error (HeadsetBookCamera): Start() can not take parameters.`nBatchmode quit successfully invoked")
    Assert-Rejected 'Unity script diagnostics found at lines 2.*'
    [IO.File]::WriteAllText($logPath, "Passed: 931`r`n  Script error (AnotherComponent): Invalid callback signature.`r`nExiting batchmode successfully")
    Assert-Rejected 'Unity script diagnostics found at lines 2.*'
    [IO.File]::WriteAllLines($logPath, @('Loading project', 'The .meta file Assets/Maestro/New.cs.meta does not have a valid GUID and its corresponding Asset file will be ignored. If this file is not malformed, please add a GUID', 'Exiting batchmode successfully'))
    Assert-Rejected 'Unity ignored-asset metadata diagnostics found at lines 2.*'
    [IO.File]::WriteAllText($logPath, "An expected test exercises the phrase Script error in data.`nTests passed`nExiting batchmode successfully")
    & $gate -LogPath $logPath -RejectFactReadFailures
    [IO.File]::WriteAllLines($logPath, @('Loading project', 'Native fact read failed: image.chat.library v1 (invalid typed value).', 'Exiting batchmode successfully'))
    & $gate -LogPath $logPath # Expected NUnit boundary diagnostics are checked by LogAssert there.
    Assert-Rejected 'Native fact contract failures found at lines 2.*' -Facts
    [IO.File]::WriteAllText($logPath, 'Test data mentions Native fact read failed: without being an emitted diagnostic.')
    & $gate -LogPath $logPath -RejectFactReadFailures
    $wrapper = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Run-QuestRoomProbe.ps1') -Raw
    $calls = @([regex]::Matches($wrapper, "(?m)^.*Assert-QuestUnityLog\.ps1.*-LogPath[^\r\n]+"))
    if ($calls.Count -ne 2 -or @($calls | Where-Object { $_.Value -notmatch '-RejectFactReadFailures' }).Count -ne 0) {
        throw 'Both original and restarted integration logs must reject native fact failures.'
    }
} finally { if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath } }
Write-Output 'Unity log gate: missing/empty logs, engine script diagnostics and ignored-asset metadata rejected; ordinary data accepted; integration fact failures rejected, including restarted runs.'
