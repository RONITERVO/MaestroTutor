# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SdkArchive,
    [string]$CompilerRoot,
    [string]$WindowsSdkRoot,
    [string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$evidenceRoot=Join-Path $repoRoot '.quest-evidence/audio-backend'
if(!$OutputDirectory){$OutputDirectory=Join-Path $evidenceRoot ([Guid]::NewGuid().ToString('N'))}
$output=[IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\','/')
if(!$output.StartsWith($evidenceRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
    throw 'Use a new output directory inside this checkout .quest-evidence/audio-backend folder.'
}
if(Test-Path -LiteralPath $output){throw 'Output directory already exists; retain earlier evaluation evidence.'}
$archive=(Resolve-Path -LiteralPath $SdkArchive).Path
$expected='4a0aa5ec1176f38f0b0993a37c2259d9e86f27e22d5e24f83ec4c3cb9a1d5449'
$digest=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
if($digest -ne $expected){throw 'Expected the reviewed official Steam Audio SDK 4.8.1 archive; digest differs.'}
if(!$CompilerRoot){
    $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if(!(Test-Path -LiteralPath $vswhere)){throw 'MSVC C++ build tools are required; specify CompilerRoot.'}
    $installation=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if($LASTEXITCODE -ne 0 -or !$installation){throw 'No MSVC C++ installation found.'}
    $versions=Join-Path $installation 'VC/Tools/MSVC'
    $CompilerRoot=(Get-ChildItem -LiteralPath $versions -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1).FullName
}
$compiler=Join-Path $CompilerRoot 'bin/Hostx64/x64/cl.exe'
if(!(Test-Path -LiteralPath $compiler)){throw 'CompilerRoot must contain bin/Hostx64/x64/cl.exe.'}
if(!$WindowsSdkRoot){$WindowsSdkRoot=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'}
$kit=(Get-ChildItem -LiteralPath (Join-Path $WindowsSdkRoot 'Include') -Directory | Where-Object {
    (Test-Path -LiteralPath (Join-Path $_.FullName 'ucrt/stdio.h')) -and
    (Test-Path -LiteralPath (Join-Path $WindowsSdkRoot ('Lib/'+$_.Name+'/um/x64/kernel32.lib')))
} | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1).Name
if(!$kit){throw 'Windows SDK headers and x64 libraries are required.'}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$receipt=Join-Path $output 'result.json'
$previousInclude=$env:INCLUDE;$previousLib=$env:LIB;$previousPath=$env:PATH
$record=[ordered]@{
    version=1;status='running';sourceCommit=(git -C $repoRoot rev-parse HEAD);sdk='4.8.1';sdkSha256=$digest
    probeSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Probe.cpp') -Algorithm SHA256).Hash
    runnerSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    started=[DateTime]::UtcNow.ToString('o');platform='Windows x64';compilerRoot=$CompilerRoot;windowsSdk=$kit
    unityIntegration=$false;questAcceptance=$false;physicalAudioPlayback=$false;providerUsed=$false
}
$record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $receipt
try {
    $zip=[IO.Compression.ZipFile]::OpenRead($archive)
    try {
        # Fixed, checked paths only. No scripts or package installers are extracted.
        foreach($name in @('steamaudio/include/phonon.h','steamaudio/include/phonon_version.h','steamaudio/lib/windows-x64/phonon.dll','steamaudio/lib/windows-x64/phonon.lib')){
            $entry=$zip.GetEntry($name)
            if(!$entry -or $entry.Length -gt 64MB){throw 'Missing or unexpected SDK member.'}
            $target=[IO.Path]::GetFullPath((Join-Path $output $name))
            if(!$target.StartsWith($output+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected extraction path.'}
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target,$false)
        }
    } finally {$zip.Dispose()}
    $env:INCLUDE="$CompilerRoot/include;$WindowsSdkRoot/Include/$kit/ucrt;$WindowsSdkRoot/Include/$kit/shared;$WindowsSdkRoot/Include/$kit/um"
    $env:LIB="$CompilerRoot/lib/x64;$WindowsSdkRoot/Lib/$kit/ucrt/x64;$WindowsSdkRoot/Lib/$kit/um/x64"
    $env:PATH=(Join-Path $CompilerRoot 'bin/Hostx64/x64')+';'+$previousPath
    $binary=Join-Path $output 'steamaudio/lib/windows-x64/maestro-audio-probe.exe'
    $arguments=@('/nologo','/std:c++17','/EHsc','/MT','/O2','/W4','/WX',('/I'+(Join-Path $output 'steamaudio/include')),('/Fo'+(Join-Path $output 'probe.obj')),('/Fe'+$binary),(Join-Path $PSScriptRoot 'Probe.cpp'),(Join-Path $output 'steamaudio/lib/windows-x64/phonon.lib'))
    & $compiler @arguments *> (Join-Path $output 'compile.log')
    if($LASTEXITCODE -ne 0){throw 'Probe compilation failed; see compile.log.'}
    $process=Start-Process -FilePath $binary -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $output 'probe.jsonl') -RedirectStandardError (Join-Path $output 'probe-errors.log')
    try {
        if(!$process.WaitForExit(30000)){
            $process.Kill();$process.WaitForExit()
            throw 'Isolated native probe exceeded 30 seconds; no success is recorded.'
        }
        $record.exitCode=$process.ExitCode
        if($process.ExitCode -ne 0){throw 'Native probe failed; inspect its output and diagnostics.'}
    } finally {$process.Dispose()}
    $results=@(Get-Content -LiteralPath (Join-Path $output 'probe.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
    $summary=$results[-1]
    if($summary.status -ne 'passed' -or $summary.sdk -ne '4.8.1' -or $summary.cycles -ne 12 -or $summary.retiredWorkers -ne 12){throw 'Probe result is incomplete.'}
    $record.summary=$summary;$record.status='passed'
} catch {
    $record.status='failed';$record.error=$_.ToString();throw
} finally {
    $record.finished=[DateTime]::UtcNow.ToString('o')
    $record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $receipt
    $env:INCLUDE=$previousInclude;$env:LIB=$previousLib;$env:PATH=$previousPath
}
Write-Output "Isolated audio candidate evaluation passed: $receipt"
