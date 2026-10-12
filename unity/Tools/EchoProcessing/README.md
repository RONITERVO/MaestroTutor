# Isolated native echo-processing candidate

This optional evaluation builds the official standalone WebRTC Audio Processing
Module (APM) 2.1 from source and exercises its AEC3 processor. It is outside Unity's
Assets directory: no new library is shipped, no microphone is acquired, and the
original browser/Live capture policy is unchanged. Native capture integration is
**not approved by these results**.

## Reproduce

Use Python 3.12+, MSVC x64 build tools and a Windows SDK. Install Meson 1.9.1 and
Ninja into a task-local directory, or supply existing local installations. The
runner installs nothing and performs no downloads. Obtain these pinned inputs:

| Input | Official download | SHA-256 |
| --- | --- | --- |
| APM 2.1 | [Release archive](https://freedesktop.org/software/pulseaudio/webrtc-audio-processing/webrtc-audio-processing-2.1.tar.xz) | `ae9302824b2038d394f10213cab05312c564a038434269f11dbf68f511f9f9fe` |
| Abseil 20240722.0 | [Abseil release](https://github.com/abseil/abseil-cpp/releases/download/20240722.0/abseil-cpp-20240722.0.tar.gz) | `f50e5ac311a81382da7fa75b97310e4b9006474f9560ac46f54a9967f07d4ae3` |
| Abseil Meson overlay | [Meson WrapDB](https://wrapdb.mesonbuild.com/v2/abseil-cpp_20240722.0-3/get_patch) | `12dd8df1488a314c53e3751abd2750cf233b830651d168b6a9f15e7d0cf71f7b` |

The matched Abseil and overlay hashes come from that release's own wrap file.
All hashes are checked before bounded extraction; links and special tar members
are refused. The runner uses a fresh output directory and never deletes old runs.

```powershell
python -m pip install --target .quest-evidence/echo-native/tools `
  --no-deps --only-binary=:all: meson==1.9.1 ninja==1.13.0
python unity/Tools/EchoProcessing/Build.py `
  --apm C:/downloads/webrtc-audio-processing-2.1.tar.xz `
  --abseil C:/downloads/abseil-cpp-20240722.0.tar.gz `
  --overlay C:/downloads/abseil-cpp_20240722.0-3_patch.zip `
  --meson-root .quest-evidence/echo-native/tools `
  --ninja .quest-evidence/echo-native/tools/ninja/data/bin/ninja.exe `
  --platform windows --output .quest-evidence/echo-native/my-windows-run
```

The only upstream build-file change is appending the app-owned probe subdirectory
in the disposable source copy. Meson uses no downloads and forces the pinned
Abseil fallback. Both dependencies use C++20: MSVC rejects an upstream designated
initializer under the release's default C++17. Windows SDK `winrt` headers are
needed by Abseil. Build logs retain warnings from upstream sources.

For an Android ARM64 cross-build, use the same command with `--platform android`
and `--ndk C:/path/to/ndk/27.2.12479018`, and a different output directory. The
runner targets API 32, statically links the C++ runtime, and verifies AArch64,
the five C exports, system-only dynamic dependencies and 16 KiB load alignment.
The Android probe is **compiled but not executed**. This is not an APK, IL2CPP,
Quest performance, microphone, or Store compatibility test.

To add offline synthesized speech, run the optional fixture helper using Windows
PowerShell 5.1 and installed `en-US` and `es-ES` SAPI voices:

```powershell
powershell.exe -NoProfile -NonInteractive -File unity/Tools/EchoProcessing/Make-SpeechFixtures.ps1 `
  -OutputDirectory "$PWD/.quest-evidence/echo-native/my-speech"
```

It writes WAV files without playing them, records the text/voice/hash, and never
uses a provider. Pass each file with a repeated `--speech-wav` argument to a fresh
Windows build. Do not check generated voice files into source control. The probe
also accepts bounded PCM16/mono/16 kHz WAVs directly; only use audio authorized for
that evaluation.

## What the adapter guarantees

`EchoProcessor.h` defines a small C ABI with one explicitly owned processor.
Render is mono/stereo at 16/32/48 kHz; capture is mono at those rates; output is
160 PCM16 samples per 10 ms. It deinterleaves the original normalized render
reference without altering playback, then runs real APM reverse/capture processing.
Gain controllers and noise suppression remain disabled; AEC's normal high-pass
filter remains enabled.

An owner must serialize all calls off the render callback and fence queued work
before destroying the handle. Feed one aligned render frame followed by one
capture frame. Frame counters and epochs are explicit. Missing, repeated or stale
frames, nonfinite/out-of-range input and DSP failures require a new epoch and DSP
reset. A failed capture clears a correctly sized output buffer; it cannot return
stale PCM. Wrong-sized buffers are not written. Destroy nulls the caller's handle;
stale copied pointers are invalid. Reset/create can allocate and are not real-time
operations. No allocation-free or hard real-time claim is made for upstream DSP.

The adapter does **not** align clocks, estimate device delay or repair gaps. The
caller must provide paired frames and APM's hardware-buffer delay estimate. The
synthetic tests supply an explicit 80 ms model; this is not a Quest calibration.

## Evidence and open quality finding

The default probe checks eight generated-audio scenarios: mono 16/32 kHz echo,
stereo 48 kHz echo, mixed 48-to-16 kHz rates, near-only voiced input, mono/stereo
double-talk, and recovery after an explicit sequence gap. Fixed thresholds are
15 dB echo reduction, .65 near-signal correlation, and .45–1.5 near-signal gain.
It also checks malformed frames, nonfinite values, stale epochs, output boundaries,
reference immutability, reset, close and repeated ownership. Internal assertion
counts include processing each frame and must not be reported as distinct tests.

The separate `--noise-stress` diagnostic **currently fails**: predominantly noisy
near-end input during echo is attenuated too much (gain about .325). It remains a
reproducible failed result, not a passed speech test. The runner records that
failure separately; unexpected failures/crashes still fail the run. Eight passing
bounded tests produce `bounded-checks-passed-quality-open`, never speech-quality
acceptance. CI states the open diagnostic in its summary and retains the logs.

On the local Windows run, ordinary voiced double-talk passes; locally synthesized
English and Spanish also pass three cases each (near-only and mono/stereo echo).
These objective comparisons do not establish pronunciation intelligibility,
quiet/unvoiced speech quality, human double-talk quality, or AEC convergence in
an actual room. The candidate is not ready for microphone integration based on
these results alone.

Before promotion: investigate the failed noise/unvoiced case, validate real speech
and near-end onset, connect the existing single capture owner, align render/capture
clocks with drift and gap handling, cover WebView and other audible sources, verify
permission/pause/stop lifecycles, and test actual Quest playback/capture and the
original Live provider path. Production reflections and microphone gates must not
be relaxed on the strength of a cross-build.

## Dependency notices

No vendor sources or binaries are committed here. The extracted APM release
retains its `COPYING`, `AUTHORS`, `PATENTS` and embedded third-party notices;
Abseil retains its Apache-2.0 license and the Meson overlay its `LICENSE.build`.
Review and package the applicable complete notices before distributing a binary.
The library includes more than Maestro's own Apache-2.0 wrapper.
