# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Editor,
    [Parameter(Mandatory)][string]$BuildMirror,
    [Parameter(Mandatory)][string]$AndroidSdk,
    [Parameter(Mandatory)][string]$AndroidJdk,
    [switch]$UpdateBehaviourCatalog,
    [switch]$RenderImports,
    [switch]$RenderRecipes,
    [switch]$RenderRules,
    [ValidateSet("Development","Release")][string]$Channel = "Development",
    [string]$ReleaseProfile,
    [switch]$PrepareOnly
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
$release = $Channel -eq 'Release'
$profile = $null
if ($release) {
    if (!$ReleaseProfile) { throw 'Supply a public release profile.' }
    $ReleaseProfile = (Resolve-Path -LiteralPath $ReleaseProfile).Path
    & node (Join-Path $repoRoot 'scripts/quest-release.mjs') check $ReleaseProfile
    if ($LASTEXITCODE -ne 0) { throw 'Quest release profile validation failed.' }
    $profile = Get-Content -LiteralPath $ReleaseProfile -Raw | ConvertFrom-Json
    if (!$PrepareOnly) {
        foreach ($variable in @('MAESTRO_QUEST_KEYSTORE','MAESTRO_QUEST_KEY_ALIAS','MAESTRO_QUEST_STORE_PASSWORD','MAESTRO_QUEST_KEY_PASSWORD')) {
            if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($variable))) { throw "Set $variable locally; do not put signing inputs in the public profile." }
        }
        if (!(Test-Path -LiteralPath $env:MAESTRO_QUEST_KEYSTORE -PathType Leaf) -or $env:MAESTRO_QUEST_KEY_ALIAS -match '[\r\n]' -or $env:MAESTRO_QUEST_KEY_ALIAS -ieq 'androiddebugkey') { throw 'Use the intended release keystore and alias.' }
        $keyReport = & (Join-Path $jdkRoot 'bin/keytool.exe') '-J-Duser.language=en' -list -v -keystore $env:MAESTRO_QUEST_KEYSTORE -alias $env:MAESTRO_QUEST_KEY_ALIAS '-storepass:env' MAESTRO_QUEST_STORE_PASSWORD 2>&1
        if ($LASTEXITCODE -ne 0) { throw 'Release signing certificate could not be read. Check the local keystore inputs.' }
        $keyText = $keyReport -join "`n"
        $certificate = [regex]::Match($keyText,'SHA256:\s*([A-Fa-f0-9:]+)').Groups[1].Value.Replace(':','')
        if ($keyText -notmatch 'PrivateKeyEntry' -or $keyText -match 'CN=Android Debug' -or $certificate -ine $profile.signingCertificateSha256) { throw 'The signing key does not match the public release certificate.' }
        $keyReport = $null; $keyText = $null
    }
}
$classesJar = Join-Path (Split-Path -Parent $editorPath) 'Data/PlaybackEngines/AndroidPlayer/Variations/il2cpp/Release/Classes/classes.jar'
if (!(Test-Path -LiteralPath $classesJar)) { throw 'Install Android build support for the pinned Unity editor.' }

# Validate the dedicated copy's ownership and run its current source tests.
& (Join-Path $PSScriptRoot 'Verify-Quest.ps1') -Editor $editorPath -BuildMirror $mirrorRoot -RenderImports:$RenderImports -RenderRules:$RenderRules -RenderRecipes:$RenderRecipes -UpdateBehaviourCatalog:$UpdateBehaviourCatalog
$logRoot = Join-Path $mirrorRoot 'Logs'
$env:JAVA_HOME = $jdkRoot
$env:ANDROID_HOME = $sdkRoot
$env:MAESTRO_UNITY_CLASSES_JAR = $classesJar
Push-Location $repoRoot
try {
    if ($release) { & node (Join-Path $repoRoot 'scripts/quest-release.mjs') build-web $ReleaseProfile *> (Join-Path $logRoot 'web-build.log') }
    else { & npm.cmd run build *> (Join-Path $logRoot 'web-build.log') }
    if ($LASTEXITCODE -ne 0) { throw "Shared web build failed; see $logRoot/web-build.log" }
    & (Join-Path $repoRoot 'android/gradlew.bat') -p (Join-Path $repoRoot 'unity/NativeBrowser') testReleaseUnitTest assembleRelease lintRelease --console=plain *> (Join-Path $logRoot 'native-build.log')
    if ($LASTEXITCODE -ne 0) { throw "Native browser build failed; see $logRoot/native-build.log" }
    $nativeTests = 0
    foreach ($reportFile in Get-ChildItem -LiteralPath (Join-Path $repoRoot 'unity/NativeBrowser/build/test-results/testReleaseUnitTest') -Filter 'TEST-*.xml') {
        [xml]$nativeReport = Get-Content -LiteralPath $reportFile.FullName
        if ([int]$nativeReport.testsuite.failures -gt 0 -or [int]$nativeReport.testsuite.errors -gt 0) { throw 'Native browser tests failed.' }
        $nativeTests += [int]$nativeReport.testsuite.tests
    }
    if ($nativeTests -lt 25) { throw 'Native browser tests did not cover the current request and file checks.' }
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
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$env:MAESTRO_QUEST_APK = Join-Path $mirrorRoot $(if ($release) { "Builds/MaestroQuest-intermediate-$stamp.apk" } else { 'Builds/MaestroQuest-development.apk' })
$method = if ($release) { 'Maestro.Quest.Editor.QuestReleaseBuild.Build' } else { 'Maestro.Quest.Editor.QuestDevelopmentBuild.Build' }
$childEnvironment = @{ ADB_SERVER_SOCKET = 'tcp:localhost:5041'; MAESTRO_QUEST_RELEASE_PROFILE = ''; MAESTRO_QUEST_STORE_PASSWORD = ''; MAESTRO_QUEST_KEY_PASSWORD = ''; MAESTRO_QUEST_KEYSTORE = ''; MAESTRO_QUEST_KEY_ALIAS = '' }
if ($release) {
    $profileTarget = Join-Path $mirrorRoot 'Release/profile.json'
    New-Item -ItemType Directory -Path (Split-Path -Parent $profileTarget) -Force | Out-Null
    Copy-Item -LiteralPath $ReleaseProfile -Destination $profileTarget -Force
    $childEnvironment.MAESTRO_QUEST_RELEASE_PROFILE = $profileTarget
}
$buildLog = Join-Path $logRoot $(if ($release) { 'release-build.log' } else { 'development-build.log' })
if (Test-Path -LiteralPath $buildLog) { Remove-Item -LiteralPath $buildLog }
Stop-QuestBuildHelper
$process = Start-Process -FilePath $editorPath -ArgumentList @('-batchmode','-force-d3d11','-quit','-buildTarget','Android','-projectPath',('"'+$mirrorRoot+'"'),'-executeMethod',$method,'-logFile',('"'+$buildLog+'"')) -WindowStyle Hidden -PassThru -Environment $childEnvironment
$deadline = [DateTime]::UtcNow.AddMinutes(45)
$reportWrittenAt = $null
$shutdownAt = $null
$helperStopped = $false
try { while (!$process.WaitForExit(1000)) {
    if (!$reportWrittenAt -and (Test-Path -LiteralPath $buildLog) -and (Select-String -LiteralPath $buildLog -Pattern 'MAESTRO_DEVELOPMENT_APK|MAESTRO_RELEASE_INTERMEDIATE_APK|Batchmode quit successfully invoked' -Quiet)) {
        $reportWrittenAt = [DateTime]::UtcNow
    }
    # The APK marker precedes shutdown; do not interrupt a still-working editor.
    if (!$shutdownAt -and (Test-Path -LiteralPath $buildLog) -and (Select-String -LiteralPath $buildLog -Pattern 'Batchmode quit successfully invoked|Killing ADB server|Exiting batchmode' -Quiet)) {
        $shutdownAt = [DateTime]::UtcNow
    }
    if (!$helperStopped -and $shutdownAt -and [DateTime]::UtcNow -gt $shutdownAt.AddSeconds(10)) {
        Stop-QuestBuildHelper
        $helperStopped = $true
    }
    if ([DateTime]::UtcNow -gt $deadline -or ($reportWrittenAt -and [DateTime]::UtcNow -gt $reportWrittenAt.AddSeconds(60))) {
        $process.Kill(); $process.WaitForExit()
        throw "Unity Android build timed out; see $buildLog. An APK alone is not a successful build."
    }
} } finally { Stop-QuestBuildHelper }
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $buildLog -Tail 60; throw "Unity Android build failed ($($process.ExitCode))." }
if (!(Test-Path -LiteralPath $env:MAESTRO_QUEST_APK)) { throw 'Unity exited without producing the expected APK.' }
$output = $env:MAESTRO_QUEST_APK
& (Join-Path $PSScriptRoot 'Assert-QuestNoStorageProbe.ps1') -Apk $output
if ($release) {
    $signer = Join-Path $sdkRoot 'build-tools/35.0.1/apksigner.bat'
    $analyzer = Join-Path $sdkRoot 'cmdline-tools/16.0/bin/apkanalyzer.bat'
    if (!$PrepareOnly) {
        $output = Join-Path $mirrorRoot "Builds/MaestroQuest-release-$($profile.versionCode)-$stamp.apk"
        & $signer sign --v2-signing-enabled true --ks $env:MAESTRO_QUEST_KEYSTORE --ks-key-alias $env:MAESTRO_QUEST_KEY_ALIAS --ks-pass env:MAESTRO_QUEST_STORE_PASSWORD --key-pass env:MAESTRO_QUEST_KEY_PASSWORD --out $output $env:MAESTRO_QUEST_APK
        if ($LASTEXITCODE -ne 0) { throw 'Release APK signing failed.' }
    }
    $signature = & $signer verify --verbose --print-certs $output
    if ($LASTEXITCODE -ne 0) { throw 'APK signature verification failed.' }
    if (!$PrepareOnly) {
        $actualCertificate = [regex]::Match(($signature -join "`n"),'Signer #1 certificate SHA-256 digest:\s*([a-fA-F0-9]+)').Groups[1].Value
        if ($actualCertificate -ine $profile.signingCertificateSha256) { throw 'Final APK signing certificate does not match the release profile.' }
    }
    $manifestText = & $analyzer manifest print $output
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the packaged Android manifest.' }
    [xml]$manifest = $manifestText -join "`n"
    $android = 'http://schemas.android.com/apk/res/android'
    if ($manifest.manifest.package -ne $profile.package -or $manifest.manifest.GetAttribute('versionCode',$android) -ne [string]$profile.versionCode -or $manifest.manifest.GetAttribute('versionName',$android) -ne $profile.versionName -or $manifest.manifest.application.GetAttribute('debuggable',$android) -eq 'true') { throw 'The APK identity/version/debug state does not match the release profile.' }
    & (Join-Path $PSScriptRoot 'Assert-QuestReleaseManifest.ps1') -Manifest $manifest
    $signature | Set-Content -LiteralPath ($output + '.signature.txt')
    $manifestText | Set-Content -LiteralPath ($output + '.manifest.xml')
    @{ version=1; releaseSigned=(!$PrepareOnly); profileSha256=(Get-FileHash -LiteralPath $ReleaseProfile -Algorithm SHA256).Hash; apkSha256=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash; providerAcceptanceVerified=$false; storeAvailabilityVerified=$false } | ConvertTo-Json | Set-Content -LiteralPath ($output + '.release.json')
}
$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
Set-Content -LiteralPath ($output + '.sha256') -Value $hash
Write-Output "Packaged APK: $output"
Write-Output "SHA256: $hash"
if ($release -and $PrepareOnly) { Write-Output 'Preparation only: non-debuggable intermediate with a development certificate. Not release-signed or ready for Store upload.' }
elseif (!$release) { Write-Output 'Development signing only.' }
Write-Output 'No upload or device installation. Real-provider and Store acceptance remain separate.'
