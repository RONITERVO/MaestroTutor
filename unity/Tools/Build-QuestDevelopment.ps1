# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Editor,
    [Parameter(Mandatory)][string]$BuildMirror,
    [Parameter(Mandatory)][string]$AndroidSdk,
    [Parameter(Mandatory)][string]$AndroidJdk
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'QuestBuildProcesses.ps1')
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$mirrorRoot = [IO.Path]::GetFullPath($BuildMirror).TrimEnd('\','/')
$sdkRoot = (Resolve-Path -LiteralPath $AndroidSdk).Path
$jdkRoot = (Resolve-Path -LiteralPath $AndroidJdk).Path
$editorPath = (Resolve-Path -LiteralPath $Editor).Path
$ndkRoot = Join-Path $sdkRoot 'ndk/27.2.12479018'
if (!(Test-Path -LiteralPath (Join-Path $ndkRoot 'source.properties'))) { throw 'Install the pinned Android NDK 27.2.12479018.' }
$classesJar = Join-Path (Split-Path -Parent $editorPath) 'Data/PlaybackEngines/AndroidPlayer/Variations/il2cpp/Release/Classes/classes.jar'
if (!(Test-Path -LiteralPath $classesJar)) { throw 'Install Android build support for the pinned Unity editor.' }

# Validate the dedicated copy's ownership and run its current source tests.
& (Join-Path $PSScriptRoot 'Verify-Quest.ps1') -Editor $editorPath -BuildMirror $mirrorRoot
$logRoot = Join-Path $mirrorRoot 'Logs'
$env:JAVA_HOME = $jdkRoot
$env:ANDROID_HOME = $sdkRoot
$env:MAESTRO_UNITY_CLASSES_JAR = $classesJar
Push-Location $repoRoot
try {
    & npm.cmd run build *> (Join-Path $logRoot 'web-build.log')
    if ($LASTEXITCODE -ne 0) { throw "Shared web build failed; see $logRoot/web-build.log" }
    & (Join-Path $repoRoot 'android/gradlew.bat') -p (Join-Path $repoRoot 'unity/NativeBrowser') testReleaseUnitTest assembleRelease lintRelease --console=plain *> (Join-Path $logRoot 'native-build.log')
    if ($LASTEXITCODE -ne 0) { throw "Native browser build failed; see $logRoot/native-build.log" }
    $nativeTests = 0
    foreach ($reportFile in Get-ChildItem -LiteralPath (Join-Path $repoRoot 'unity/NativeBrowser/build/test-results/testReleaseUnitTest') -Filter 'TEST-*.xml') {
        [xml]$nativeReport = Get-Content -LiteralPath $reportFile.FullName
        if ([int]$nativeReport.testsuite.failures -gt 0 -or [int]$nativeReport.testsuite.errors -gt 0) { throw 'Native browser tests failed.' }
        $nativeTests += [int]$nativeReport.testsuite.tests
    }
    if ($nativeTests -lt 15) { throw 'Native browser tests did not cover the current request and file checks.' }
} finally { Pop-Location }
$webTarget = Join-Path $mirrorRoot 'Assets/StreamingAssets/maestro-web'
$pluginTarget = Join-Path $mirrorRoot 'Assets/Plugins/Android'
New-Item -ItemType Directory -Path $webTarget,$pluginTarget -Force | Out-Null
# Verify-Quest has already synchronized this owned mirror, removing stale files.
Copy-Item -Path (Join-Path $repoRoot 'dist/*') -Destination $webTarget -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'unity/NativeBrowser/build/outputs/aar/MaestroBookBrowser-release.aar') -Destination (Join-Path $pluginTarget 'MaestroBookBrowser.aar')
$env:MAESTRO_ANDROID_SDK = $sdkRoot
$env:MAESTRO_ANDROID_NDK = $ndkRoot
$env:MAESTRO_ANDROID_JDK = $jdkRoot
$env:MAESTRO_QUEST_APK = Join-Path $mirrorRoot 'Builds/MaestroQuest-development.apk'
$buildLog = Join-Path $logRoot 'development-build.log'
if (Test-Path -LiteralPath $buildLog) { Remove-Item -LiteralPath $buildLog }
Stop-QuestBuildHelper
$process = Start-Process -FilePath $editorPath -ArgumentList @('-batchmode','-force-d3d11','-quit','-buildTarget','Android','-projectPath',('"'+$mirrorRoot+'"'),'-executeMethod','Maestro.Quest.Editor.QuestDevelopmentBuild.Build','-logFile',('"'+$buildLog+'"')) -WindowStyle Hidden -PassThru -Environment @{ ADB_SERVER_SOCKET = 'tcp:localhost:5041' }
$deadline = [DateTime]::UtcNow.AddMinutes(45)
$shutdownAt = $null
try { while (!$process.WaitForExit(1000)) {
    if (!$shutdownAt -and (Test-Path -LiteralPath $buildLog) -and (Select-String -LiteralPath $buildLog -Pattern 'MAESTRO_DEVELOPMENT_APK|Batchmode quit successfully invoked' -Quiet)) {
        $shutdownAt = [DateTime]::UtcNow
        Stop-QuestBuildHelper
    }
    if ([DateTime]::UtcNow -gt $deadline -or ($shutdownAt -and [DateTime]::UtcNow -gt $shutdownAt.AddSeconds(60))) {
        $process.Kill(); $process.WaitForExit()
        throw "Unity Android build timed out; see $buildLog. An APK alone is not a successful build."
    }
} } finally { Stop-QuestBuildHelper }
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $buildLog -Tail 60; throw "Unity Android build failed ($($process.ExitCode))." }
if (!(Test-Path -LiteralPath $env:MAESTRO_QUEST_APK)) { throw 'Unity exited without producing the expected APK.' }
$hash = (Get-FileHash -LiteralPath $env:MAESTRO_QUEST_APK -Algorithm SHA256).Hash
Set-Content -LiteralPath ($env:MAESTRO_QUEST_APK + '.sha256') -Value $hash
Write-Output "Development APK: $env:MAESTRO_QUEST_APK"
Write-Output "SHA256: $hash"
Write-Output 'Development signing only. Headset behavior and production/store release gates remain separate.'
