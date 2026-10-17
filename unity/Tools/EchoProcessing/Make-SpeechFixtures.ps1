# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
# Run with Windows PowerShell 5.1; uses installed offline SAPI voices only.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$allowed=Join-Path $repo '.quest-evidence/echo-native'
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(!$output.StartsWith($allowed+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $output)){
    throw 'Use a fresh child folder under .quest-evidence/echo-native.'
}
Add-Type -AssemblyName System.Speech
$synth=New-Object System.Speech.Synthesis.SpeechSynthesizer
$receipts=@()
try {
    $format=New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(16000,[System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen,[System.Speech.AudioFormat.AudioChannel]::Mono)
    $fixtures=@(
        @{culture='en-US';name='english';text='Hello Maestro. I am learning Spanish for the first time. Please help me practise. I would like an apple and a cup of coffee. Can we play a game together? Please remove the tree, but keep the apple.'},
        @{culture='es-ES';name='spanish';text='Hola Maestro. Estoy aprendiendo a hablar contigo. Quiero una manzana y una taza de cafe, por favor. Hace buen tiempo hoy. Podemos jugar juntos en el parque. Quita el arbol, pero deja la manzana. Gracias por ayudarme.'}
    )
    New-Item -ItemType Directory -Path $output | Out-Null
    foreach($fixture in $fixtures){
        $voice=$synth.GetInstalledVoices() | Where-Object {$_.Enabled -and $_.VoiceInfo.Culture.Name -eq $fixture.culture} | Select-Object -First 1
        if(!$voice){throw ('No installed offline voice for '+$fixture.culture)}
        $synth.SelectVoice($voice.VoiceInfo.Name)
        $wave=Join-Path $output ($fixture.name+'.wav')
        $synth.SetOutputToWaveFile($wave,$format)
        $synth.Speak($fixture.text)
        $synth.SetOutputToNull()
        $receipts+=@{culture=$fixture.culture;voice=$voice.VoiceInfo.Name;text=$fixture.text;file=$wave;sha256=(Get-FileHash -LiteralPath $wave -Algorithm SHA256).Hash}
    }
    @{source='Windows installed offline speech synthesis';physicalAudio=$false;providerUsed=$false;fixtures=$receipts} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'fixtures.json')
} finally { $synth.Dispose() }
Write-Output (Join-Path $output 'fixtures.json')
