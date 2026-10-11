# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference = 'Stop'
$logPath = Join-Path ([IO.Path]::GetTempPath()) ('maestro-unity-log-' + [Guid]::NewGuid().ToString('N') + '.log')
$gate = Join-Path $PSScriptRoot 'Assert-QuestUnityLog.ps1'
function Assert-Rejected([string]$Expected) {
    $rejected = $false
    try { & $gate -LogPath $logPath }
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
    [IO.File]::WriteAllText($logPath, "An expected test exercises the phrase Script error in data.`nTests passed`nExiting batchmode successfully")
    & $gate -LogPath $logPath
} finally { if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath } }
Write-Output 'Unity log gate: missing/empty logs and engine script diagnostics rejected; ordinary data accepted.'
