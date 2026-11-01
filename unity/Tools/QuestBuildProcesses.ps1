# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
function Stop-QuestBuildHelper {
    # Port 5041 belongs exclusively to these build scripts. A child ADB server
    # can inherit Meta's TRiMSchema cache handle and outlive Unity, causing the
    # next editor to crash in native cache cleanup. Leave the user's 5037 server
    # alone; this neither enumerates nor sends commands to connected devices.
    foreach ($candidate in Get-CimInstance Win32_Process -Filter "Name='adb.exe'") {
        if ($candidate.CommandLine -match '(?:^|\s)-L tcp:localhost:5041 fork-server server(?:\s|$)' -and
            [IO.Path]::GetFileName($candidate.ExecutablePath) -eq 'adb.exe') {
            Stop-Process -Id $candidate.ProcessId -ErrorAction SilentlyContinue
        }
    }
}
