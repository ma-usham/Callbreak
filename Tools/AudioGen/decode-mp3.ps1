# Decodes a downloaded music file (MP3, M4A, ...) to 16-bit PCM WAV with Windows Media Foundation, for
# ArtSource/Audio/Sources. Unlike Unity's MP3 import it drops the encoder's leading delay (528 samples for
# Gemini's MP3s); otherwise the two decodes agree to -58 dB.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File Tools/AudioGen/decode-mp3.ps1 <in> <out.wav> [sample rate]
param([string]$In, [string]$Out, [int]$Rate = 0)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Media.Transcoding.MediaTranscoder, Windows.Media.Transcoding, ContentType = WindowsRuntime]
$null = [Windows.Media.MediaProperties.MediaEncodingProfile, Windows.Media.MediaProperties, ContentType = WindowsRuntime]

$extensions = [System.WindowsRuntimeSystemExtensions].GetMethods()
$asTask = ($extensions | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
$asTaskWithProgress = ($extensions | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncActionWithProgress`1' })[0]
function Await($operation, [Type]$type) {
    $task = $asTask.MakeGenericMethod($type).Invoke($null, @($operation))
    $task.Wait() | Out-Null
    $task.Result
}

$source = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync((Resolve-Path $In).Path)) ([Windows.Storage.StorageFile])
$folder = Await ([Windows.Storage.StorageFolder]::GetFolderFromPathAsync((Split-Path -Parent ([System.IO.Path]::GetFullPath($Out))))) ([Windows.Storage.StorageFolder])
$target = Await ($folder.CreateFileAsync((Split-Path -Leaf $Out), [Windows.Storage.CreationCollisionOption]::ReplaceExisting)) ([Windows.Storage.StorageFile])

$sourceAudio = (Await ([Windows.Media.MediaProperties.MediaEncodingProfile]::CreateFromFileAsync($source)) ([Windows.Media.MediaProperties.MediaEncodingProfile])).Audio
$profile = [Windows.Media.MediaProperties.MediaEncodingProfile]::CreateWav([Windows.Media.MediaProperties.AudioEncodingQuality]::High)
$profile.Audio.SampleRate = if ($Rate -gt 0) { [uint32]$Rate } else { $sourceAudio.SampleRate }
$profile.Audio.ChannelCount = $sourceAudio.ChannelCount
$profile.Audio.BitsPerSample = 16
$profile.Audio.Bitrate = $profile.Audio.SampleRate * $profile.Audio.ChannelCount * 16

$prepared = Await ((New-Object Windows.Media.Transcoding.MediaTranscoder).PrepareFileTranscodeAsync($source, $target, $profile)) ([Windows.Media.Transcoding.PrepareTranscodeResult])
if (-not $prepared.CanTranscode) { throw "Cannot decode $In ($($prepared.FailureReason))" }
$asTaskWithProgress.MakeGenericMethod([double]).Invoke($null, @($prepared.TranscodeAsync())).Wait() | Out-Null
"$In ($($sourceAudio.Subtype), $($sourceAudio.SampleRate) Hz, $($sourceAudio.ChannelCount) ch) -> $Out ($($profile.Audio.SampleRate) Hz, 16-bit)"
