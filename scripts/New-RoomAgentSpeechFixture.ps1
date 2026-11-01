# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
# Run with Windows PowerShell 5.1; uses an installed offline voice, no provider.
[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$OutputPath,
 [string]$Voice = 'Microsoft David Desktop'
)
$ErrorActionPreference = 'Stop'
$destination = [IO.Path]::GetFullPath($OutputPath)
if(Test-Path -LiteralPath $destination){throw 'Choose a new output file; existing fixtures are not overwritten.'}
Add-Type -AssemblyName System.Speech
$text = 'Please ask the room agent to create my test object now. Make it the same colour as the large circle shown in the camera image.'
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$temporaryWav = [IO.Path]::GetTempFileName()
try {
 $synth.SelectVoice($Voice)
 $synth.Rate = 0
 $format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(16000,[System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen,[System.Speech.AudioFormat.AudioChannel]::Mono)
 # No speaker playback or user recording. The owned temporary WAV is removed.
 $synth.SetOutputToWaveFile($temporaryWav,$format)
 $synth.Speak($text)
 $synth.SetOutputToNull()
 $wav = [IO.File]::ReadAllBytes($temporaryWav)
} finally {$synth.Dispose();Remove-Item -LiteralPath $temporaryWav -ErrorAction SilentlyContinue}
# Validate the WAV chunks before using them as a fidelity fixture.
$position = 12; $data = $null; $sampleRate = 0; $channels = 0; $bits = 0; $encoding = 0
while($position + 8 -le $wav.Length){
 $name = [Text.Encoding]::ASCII.GetString($wav,$position,4)
 $size = [BitConverter]::ToUInt32($wav,$position+4)
 $start = $position+8
 if($start+$size -gt $wav.Length){throw 'Truncated generated WAV.'}
 if($name -eq 'fmt ' -and $size -ge 16){
  $encoding=[BitConverter]::ToUInt16($wav,$start);$channels=[BitConverter]::ToUInt16($wav,$start+2)
  $sampleRate=[BitConverter]::ToUInt32($wav,$start+4);$bits=[BitConverter]::ToUInt16($wav,$start+14)
 }
 if($name -eq 'data'){$data=New-Object byte[] $size;[Array]::Copy($wav,$start,$data,0,$size)}
 $position=$start+$size+($size%2)
}
if($encoding -ne 1 -or $channels -ne 1 -or $bits -ne 16 -or !$data -or $sampleRate -ne 16000){throw "Voice generated $sampleRate Hz/$bits-bit/$channels-channel audio; use a 16 kHz mono PCM voice or recorded fixture."}
$pcm = New-Object byte[] ($data.Length+96000)
[Array]::Copy($data,0,$pcm,32000,$data.Length) # one second lead, two seconds tail
$sha=[Security.Cryptography.SHA256]::Create()
try {$hash=([BitConverter]::ToString($sha.ComputeHash($pcm))).Replace('-','').ToLowerInvariant()} finally {$sha.Dispose()}
$json=@{pcmBase64=[Convert]::ToBase64String($pcm);sampleRate=16000;expectedTranscript=$text;fixture=@{kind='offline-synthetic-speech';voice=$Voice;pcmSha256=$hash;inputDurationSeconds=$pcm.Length/32000}} | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText($destination,$json,(New-Object Text.UTF8Encoding($false)))
Write-Output "Speech fixture created: $destination"
