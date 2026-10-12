# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$UnityArchive,
    [Parameter(Mandatory)][string]$Editor,
    [Parameter(Mandatory)][string]$AndroidSdk,
    [Parameter(Mandatory)][string]$AndroidNdk,
    [Parameter(Mandatory)][string]$Java,
    [string]$Python='python'
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$archive=(Resolve-Path -LiteralPath $UnityArchive).Path
$editorPath=(Resolve-Path -LiteralPath $Editor).Path
$version=(Get-Item -LiteralPath $editorPath).VersionInfo.ProductVersion
if (!$version.StartsWith('6000.3.24f1')) { throw 'Pinned Unity 6000.3.24f1 required' }
$digest=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($digest -ne '9b7e3689561670e6b4b8e0c579a2d951bb4a0589f7d46c23bc9df923d62940f0') { throw 'Reviewed Steam Audio Unity 4.8.1 archive required' }
$sdk=(Resolve-Path -LiteralPath $AndroidSdk).Path
$ndk=(Resolve-Path -LiteralPath $AndroidNdk).Path
$jdk=(Resolve-Path -LiteralPath $Java).Path
if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'") { throw 'Wait for the current Unity Editor' }
$id=[Guid]::NewGuid().ToString('N').Substring(0,12)
$run=Join-Path $repo ('.quest-evidence/audio-unity/'+$id)
if (Test-Path -LiteralPath $run) { throw 'Preserve existing evidence' }
New-Item -ItemType Directory -Path $run | Out-Null
$project=Join-Path $run 'project'
$apk=Join-Path $run 'NativeAudioProbe.apk'
$receipt=Join-Path $run 'android-result.json'
$record=[ordered]@{version=1;status='running';sourceCommit=(git -C $repo rev-parse HEAD);started=[DateTime]::UtcNow.ToString('o');
    unity=$version;sdk='4.8.1';sdkSha256=$digest;project=$project;apk=$apk;platform='Android ARM64';
    backend='IL2CPP';managedStripping='High';developmentOnly=$true;releaseKeyUsed=$false;deviceRun=$false;
    providerUsed=$false;appRuntimeChanged=$false;sourceHashes=@{}}
foreach ($file in @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Unity') -Filter '*.cs') +
    @('PrepareUnityProject.py','Run-AndroidProbe.ps1','VerifyAndroidProbe.py' | ForEach-Object {Get-Item -LiteralPath (Join-Path $PSScriptRoot $_)})) {
    $relative=[IO.Path]::GetRelativePath($PSScriptRoot,$file.FullName).Replace('\','/')
    $record.sourceHashes[$relative]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
}
foreach ($relative in @('ProjectSettings/AudioManager.asset','Assets/Maestro/Runtime/Book/SpeechPcmStream.cs','Assets/Maestro/Runtime/Book/PcmOutputLease.cs','Assets/Maestro/Runtime/Book/PcmOutputBackend.cs','Assets/Maestro/Runtime/Book/AcousticGeometry.cs','Assets/Maestro/Runtime/Book/AcousticGeometryCatalog.cs','Assets/Maestro/Runtime/Creation/RoomPreparationBudget.cs')) {
    $record.sourceHashes['app/'+$relative]=(Get-FileHash -LiteralPath (Join-Path $repo ('unity/MaestroQuest/'+$relative)) -Algorithm SHA256).Hash
}
function Save-Receipt { $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $receipt }
. (Join-Path $PSScriptRoot '../QuestBuildProcesses.ps1')
Save-Receipt
try {
    & $Python (Join-Path $PSScriptRoot 'PrepareUnityProject.py') $archive $project android *> (Join-Path $run 'prepare.log')
    if ($LASTEXITCODE -ne 0) { throw 'Candidate preparation failed' }
    $record.nativeInputSha256=(Get-FileHash -LiteralPath (Join-Path $project 'Assets/Plugins/SteamAudio/Binaries/Android/arm64/libphonon.so') -Algorithm SHA256).Hash.ToLowerInvariant()
    Save-Receipt
    if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'") { throw 'Another Unity Editor started; leave it untouched' }
    Stop-QuestBuildHelper
    $args=@('-batchmode','-nographics','-buildTarget','Android','-projectPath',('"'+$project+'"'),'-logFile',('"'+(Join-Path $run 'android-build.log')+'"'),'-executeMethod','Maestro.AudioEvaluation.Editor.AndroidCandidateBuild.Build')
    $process=Start-Process -FilePath $editorPath -ArgumentList $args -WindowStyle Hidden -PassThru -Environment @{
        ADB_SERVER_SOCKET='tcp:localhost:5041';MAESTRO_ANDROID_SDK=$sdk;MAESTRO_ANDROID_NDK=$ndk;MAESTRO_ANDROID_JDK=$jdk;MAESTRO_AUDIO_PROBE_APK=$apk}
    $record.editorPid=$process.Id;$record.phase='compile';Save-Receipt
    $deadline=[DateTime]::UtcNow.AddMinutes(25)
    try {
        while (!$process.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -gt $deadline) { $process.Kill();$process.WaitForExit();throw 'Owned Android build exceeded its deadline' }
        }
        $record.editorExit=$process.ExitCode
    } finally { $process.Dispose();Stop-QuestBuildHelper }
    if ($record.editorExit -ne 0 -or !(Test-Path -LiteralPath $apk)) { throw 'Android compilation failed; preserve android-build.log' }
    $record.build=Get-Content -LiteralPath ([IO.Path]::ChangeExtension($apk,'.build.json')) -Raw | ConvertFrom-Json
    if ($record.build.result -ne 'Succeeded' -or $record.build.errors -ne 0) { throw 'Android build report is not success' }
    foreach ($entry in $record.sourceHashes.GetEnumerator()) {
        $inputPath=if ($entry.Key.StartsWith('app/')) {Join-Path $repo ('unity/MaestroQuest/'+$entry.Key.Substring(4))} else {Join-Path $PSScriptRoot $entry.Key}
        if ((Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash -ne $entry.Value) { throw ('Input changed during build: '+$entry.Key) }
    }
    foreach ($copied in Get-ChildItem -LiteralPath (Join-Path $project 'Assets/Evaluation') -Recurse -Filter '*.cs') {
        $key=if ($copied.Name -in @('SpeechPcmStream.cs','AcousticGeometry.cs','AcousticGeometryCatalog.cs','PcmOutputLease.cs','PcmOutputBackend.cs')) {'app/Assets/Maestro/Runtime/Book/'+$copied.Name} elseif ($copied.Name -eq 'RoomPreparationBudget.cs') {'app/Assets/Maestro/Runtime/Creation/RoomPreparationBudget.cs'} else {'Unity/'+$copied.Name}
        if (!$record.sourceHashes.ContainsKey($key) -or (Get-FileHash -LiteralPath $copied.FullName -Algorithm SHA256).Hash -ne $record.sourceHashes[$key]) { throw ('Prepared source differs: '+$copied.Name) }
    }
    $record.sourceHashesVerified=$record.sourceHashes.Count
    $record.phase='verify';Save-Receipt
    & $Python (Join-Path $PSScriptRoot 'VerifyAndroidProbe.py') $project $apk (Join-Path $run 'android-payload.json') (Join-Path $ndk 'toolchains/llvm/prebuilt/windows-x86_64/bin/llvm-strip.exe') $record.nativeInputSha256 *> (Join-Path $run 'verify.log')
    if ($LASTEXITCODE -ne 0) { throw 'Android payload verification failed; preserve verify.log' }
    $record.payload=Get-Content -LiteralPath (Join-Path $run 'android-payload.json') -Raw | ConvertFrom-Json
    $record.status='passed'
} catch { $record.status='failed';$record.error=$_.ToString();throw }
finally { $record.finished=[DateTime]::UtcNow.ToString('o');Save-Receipt }
Write-Output "Android candidate build-only verification passed: $receipt"
