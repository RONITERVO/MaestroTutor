# Isolated native echo-processing candidate

This optional evaluation builds the official standalone WebRTC Audio Processing
Module (APM) 2.1 from source and exercises its AEC3 processor. It is outside Unity's
Assets directory: no new library is shipped, no microphone is acquired, and the
original browser/Live capture policy is unchanged. Native capture integration is
**not approved by these results**.

## Reproduce

Use Python 3.12+, MSVC x64 build tools and a Windows SDK. Install Meson 1.9.1 and
Ninja into a task-local directory, or supply existing local installations. The
runner installs nothing and performs no downloads. Keep the output directory name
short on Windows: MSVC can reject a deeply nested source path even when Python
can read it. Obtain these pinned inputs:

| Input | Official download | SHA-256 |
| --- | --- | --- |
| APM 2.1 | [Release archive](https://freedesktop.org/software/pulseaudio/webrtc-audio-processing/webrtc-audio-processing-2.1.tar.xz) | `ae9302824b2038d394f10213cab05312c564a038434269f11dbf68f511f9f9fe` |
| Abseil 20240722.0 | [Abseil release](https://github.com/abseil/abseil-cpp/releases/download/20240722.0/abseil-cpp-20240722.0.tar.gz) | `f50e5ac311a81382da7fa75b97310e4b9006474f9560ac46f54a9967f07d4ae3` |
| Abseil Meson overlay | [Meson WrapDB](https://wrapdb.mesonbuild.com/v2/abseil-cpp_20240722.0-3/get_patch) | `12dd8df1488a314c53e3751abd2750cf233b830651d168b6a9f15e7d0cf71f7b` |

CI uses the official [GStreamer release mirror](https://gstreamer.freedesktop.org/src/mirror/webrtc-audio-processing/webrtc-audio-processing-2.1.tar.gz)
when the primary release host rejects automated access. Its gzip SHA-256 is
`35e86b986d02ea15f3d04741a1a5a735ba399bc0fac0ee089c39480e35fc3253`;
all 663 file contents match the pinned xz release. The runner accepts exactly
these two archive digests and records which one it used.

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
  --ninja .quest-evidence/echo-native/tools/bin/ninja.exe `
  --platform windows --output .quest-evidence/echo-native/my-windows-run
```

The only upstream build-file changes are appending the app-owned probe subdirectory
and its explicit profile option in the disposable source copy. Meson uses no downloads and forces the pinned
Abseil fallback. Both dependencies use C++20: MSVC rejects an upstream designated
initializer under the release's default C++17. Windows SDK `winrt` headers are
needed by Abseil. Build logs retain warnings from upstream sources.

For an Android ARM64 cross-build, use the same command with `--platform android`
and `--ndk C:/path/to/ndk/27.2.12479018`, and a different output directory. The
runner targets API 32, statically links the C++ runtime, and verifies AArch64,
the six C exports, system-only dynamic dependencies and 16 KiB load alignment.
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
synthetic tests supply explicit modeled delays; this is not a Quest calibration.

## Native capture input boundary

`CaptureInput` owns one prepared device on one non-real-time worker. Its Android
implementation explicitly opens AAudio mono/float/48 kHz input, requests the
unprocessed preset and privacy-sensitive capture, checks the returned format and
bounds device buffering to half a second. Acquisition requires the caller's
microphone permission and sole application capture lease; neither is implemented
by this low-level module. Preparation does not start recording. The unprocessed
preset is a request, not proof that device preprocessing is absent.

The owner starts once, assembles short nonblocking reads into 480-sample frames,
and preserves sample positions, epoch/sequence, an AAudio `CLOCK_MONOTONIC`
hardware timestamp and its observation bracket. It never substitutes callback
arrival time for a hardware timestamp. Timestamp availability can lag startup:
the owner waits up to one second rather than returning untimed PCM. Once active,
half-second freshness/progress limits, monotonicity, sample continuity and overrun
checks reject stale or discontinuous data. Missing timestamp support, disconnects,
invalid samples and clock failures retire the device and fault the owner. Closing
is terminal, releases pending input, and never automatically reacquires hardware.

These frames are raw normalized capture **inside the adapter**, not provider
input. They still need alignment with a measured render timeline and AEC processing
before conversion to the existing shared `PcmCapture` output. The original browser
capture owner is unchanged. No permission UI, native app bridge, pause/focus
integration, clock estimator, drift resampler or native selection is added here.

The runner also executes 20 scripted capture scenarios covering ownership,
asynchronous timestamps, startup/freshness deadlines, partial/empty reads, buffer
boundaries, startup failure, rollback/gaps, overruns before and during reading,
invalid samples, disconnects, missing timestamp support, stalled workers and
repeated cleanup. The probe never calls the real Android acquisition factory;
its Android binary cross-links that factory and AAudio timestamp imports for
compile/link verification only. Real Android timestamp quality, route changes,
permission revocation and lifecycle behaviour remain device acceptance work.

API basis: [Android AAudio guide](https://developer.android.com/ndk/guides/audio/aaudio/aaudio)
and [NDK AAudio reference](https://developer.android.com/ndk/reference/group/audio).
The pinned NDK 27.2 headers are authoritative for the compiled API; its format
query is `AAudioStream_getFormat` (the guide's `getDataFormat` example does not
compile against this NDK). AAudio stream access and close are serialized on the
owner's worker, as required by the platform's thread-safety contract.

## Timestamped playback endpoint

`PlaybackOutput` is an optional device endpoint for the caller's final stereo
float/48 kHz mix. It does not decode or spatialize sources. Its Android adapter
marks that mix as already spatialized and disables another platform spatialization
pass (available at the minimum API 32). The application does not select it yet.

One worker owns submission, reference reads, clock queries and shutdown. One
AAudio callback consumes queued PCM and publishes an exact copy of its output,
including known zero samples when the producer underfills a callback. Two fixed
half-second rings provide bounded backpressure. The callback performs no allocation,
locking, clock query, file/network access or stream shutdown. A full reference ring
stops the endpoint instead of silently losing reference audio; a device xrun is a
fatal timing gap, separate from known queue-underfill silence. All faults require
a new owner and epoch. Close fences callbacks before clearing storage.

Variable callback sizes are assembled into 480-frame references with epoch,
sequence and device-frame position. `DeviceAudioStamp` is shared with capture and
retains platform frame/time observations without claiming clock calibration.
Playback completion requires a fresh hardware presentation timestamp reaching the
last submitted source sample, not merely callback consumption. Foreign, superseded,
unavailable and stale observations cannot establish completion. Device disconnect,
clock rollback, impossible presentation positions or missed timestamp deadlines
retire the endpoint. Readiness/progress deadlines match the capture boundary.

The playback probe covers 22 scripted scenario groups, including concurrent
submission/reference transfer of 120,000 ordered source frames and closure with a
callback in flight. Repeated assertions are not additional test cases. Windows
runs these alongside the 20 capture cases and AEC evaluation. Android cross-links
the real callback factory, timestamp and frame-position imports; it does not run
on a device. No microphone or speaker is opened by either scripted probe.

This endpoint is not yet connected to Unity's final mix or `PcmOutputLease`.
Hardware route behaviour, callback shutdown and timestamp accuracy still require
Quest acceptance. Pairing input/output clocks, drift resampling and gap handling
remain unimplemented, as do complete WebView/platform-audio reference coverage
and integration with the original sole `PcmCapture` owner. Unity DSP time and a
monotonic observation alone must not be treated as hardware presentation timing.

## Reproducible profiles and open quality findings

`--profile stock` is the default and keeps the original APM configuration.
Two opt-in profiles use 32-block (128 ms at 16 kHz) filters and stationarity-aware
echo audibility, with the pinned dependency's disabled debug-dump setting:

- `--profile stationary32` uses AEC3's internal delay estimator. It improves
  steady-room near-end preservation but regresses changing-path suppression.
- `--profile measured32` uses the caller's supplied buffer delay, 4 ms headroom
  and a .95 coarse adaptation rate. The rate matches the upstream multichannel
  preset while retaining the longer filter. It passes all current generated
  quality checks, **conditional on the modeled timing supplied by the probe**.

All three remain outside Unity. The measured profile accepts delay values in
[4, 500] ms, rejects values below its headroom and faults the epoch on invalid
input. The other profiles retain [0, 500] ms support. Range validation cannot prove
that the caller measured the delay correctly. No native timing source is connected to the app.

Private linear-output traces of the internal-estimator profile showed an estimate
change several seconds after the acoustic path changed, resetting a filter that
had already recovered. Correlation can also align to a louder later reflection,
leaving a weaker direct path outside the filter. Measured timing avoids those
specific failures in these fixtures. Shorter-filter and larger-headroom candidates
were rejected for other regressions; no upstream DSP source was patched.

The default probe checks eight generated-audio scenarios: mono 16/32 kHz echo,
stereo 48 kHz echo, mixed 48-to-16 kHz rates, near-only voiced input, mono/stereo
double-talk, and recovery after an explicit sequence gap. Fixed thresholds remain
15 dB echo reduction, .65 near-signal correlation, and .45–1.5 near-signal gain.
It also checks malformed frames, nonfinite values, stale epochs, output boundaries,
reference immutability, reset, close and repeated ownership. Assertion counts
include processing each frame and must not be reported as distinct tests.

The source fixture now uses a 161-tap 6 kHz low-pass filter before sampling at
16/32/48 kHz. Previously the microphone simulation point-decimated full-band noise,
creating aliases absent from APM's properly resampled render reference. Earlier
metrics remain historical evidence, not directly comparable measurements of the
corrected fixture. This deliberately band-limited test does not establish quality
throughout the full 48 kHz bandwidth.

The runner and all three CI matrix jobs also execute four diagnostic groups:

- `--noise-stress`: one predominantly noisy near-end double-talk case, with the
  same absolute level/correlation and echo thresholds as the steady-state probe.
- `--onsets`: five voiced/noisy/fricative-like profiles, including quiet voice and
  quiet fricatives. Each checks eight bursts in fixed 80 ms onset and 550 ms body
  windows. Relative near-end gain must remain .6–1.5 and correlation at least .65;
  an independent .45–1.5 baseline RMS gate prevents both compared paths muting
  speech. The fixed 8 ms framing offset never slides past a missing onset.
- `--path-changes`: mono and mildly nonlinear stereo echo paths change abruptly
  at eight seconds without resetting the DSP. Every one-second window from
  seconds 11 through 16 must retain at least 15 dB suppression. Earlier transient
  windows are reported separately; this is not instantaneous path tracking.
- `--delay-variants`: 24 configurations use a separate signal seed, 20/80/140 ms
  hardware delays, weaker direct/louder reflected paths, mono 16 kHz and stereo
  48 kHz, and supplied-delay errors of -4/0/+4 ms. Each abruptly adds 15 ms to the
  acoustic path and uses the same per-window recovery threshold. These finite
  offsets do not establish tolerance to continuous drift, timestamp jitter or
  arbitrary timing errors.

Current standalone processor results (paired-clock checks below are separate):

| Profile | Eight core scenarios | Noise-heavy double-talk | Five onset profiles | Two changing paths | 24 delay variants |
| --- | --- | --- | --- | --- | --- |
| `stock` (default) | pass | **fail** | **all five fail** | pass | pass |
| `stationary32` | pass | pass | pass | **both fail** | **18 fail** |
| `measured32` | pass | pass | pass | pass | pass |

With the corrected fixture, stock noisy near-end gain is about .284 (correlation
.618), and the quiet-fricative onset minimum gain is .022. The stationary profile
retains at least .860 onset gain across the five profiles, but its changed-path
suppression drops to about 2–3 dB even after an earlier recovery. Passing quiet
speech alone would have hidden that regression. Locally synthesized English and
Spanish also exercise near-only and mono/stereo double-talk for each profile;
these six additional steady-state comparisons per profile are not perceptual
pronunciation acceptance. The measured profile passes 40 generated scenarios
(eight core plus 32 diagnostic configurations), with no lowered threshold.
Transient echo after a path change remains visible in the logs even when the
three-second recovery gate passes.

The runner records every quality failure and returns
`bounded-checks-passed-quality-open` only when core contracts and that profile's
established passing quality groups remain intact. Stock path/delay checks,
stationary noise/onset checks, and every measured-profile diagnostic group are
hard regression gates. Missing results, incomplete groups,
invalid-frame failures and crashes fail the run. Known failures remain failures,
with nonzero `openQualityFailures`, explicit CI warnings and retained logs; green
bounded CI is **not** audio-quality acceptance. The measured profile passes those 40 standalone cases, but has additional paired-clock
tonal failures described below. All profiles retain `speechQualityAccepted: false`
and `questAcceptance: false`. No numeric threshold was relaxed.

Before promotion: validate measured timing on the actual capture/output path and
human speech perceptually, connect the existing single capture owner, align
render/capture clocks with drift and gap handling, cover WebView and other audible
sources, verify permission/pause/stop lifecycles, and test actual Quest
playback/capture and the original Live provider path. Production reflections and
microphone gates must not be relaxed on the strength of a cross-build.

## Clock-aligned duplex candidate

`DuplexProcessor` accepts frames from the existing native capture/output owners,
uses their independent hardware timelines, and feeds the existing APM C ABI.
It runs on one worker and acquires no devices. Capture and render epochs, sequence,
positions and playback-owner observations must remain continuous. Half-second
queues reject overflow; missing streams have a startup deadline, and stale data,
clock/provider errors or processing failure retire the whole paired epoch. No raw
microphone fallback or automatic reopen occurs. Close discards pending data.

`HardwareClock` keeps 64 observations with at least 10 ms spacing so frequent
worker reads cannot collapse its fitting window. Nominal 48 kHz timing is used
with an explicit uncertainty allowance during startup. A fit needs eight distinct
observations spanning at least 250 ms. Candidate admission limits are +/-2,000 ppm
rate deviation, 200 microseconds timestamp residual, 250 ms freshness and 500 ms
projection. Prediction bounds use absolute regression weights, an extrapolation
drift allowance and millisecond delay quantization. These bounds **assume** the
hardware timestamp error/rate limits; they are not measured Quest accuracy.

The measured buffer delay follows upstream APM's definition:
`(speaker presentation - reverse analysis) + (capture processing - acquisition)`.
The worker samples the actual APM call times. Delay uncertainty over 2 ms, a reverse
processing interval over 5 ms, or a delay interval outside [4, 500] ms faults the
pair instead of clamping an unknown delay into an accepted range.

The pinned upstream sinc resampler converts microphone samples to the output
clock's cadence. Its ratio slews at at most 10 ppm per processed 10 ms block;
whole/fraction sample positions retain phase without skipping frames. It waits
for real kernel lookahead, never padding missing microphone input. Filter
overshoot is clipped to normalized PCM and counted in the receipt. Three aligned
APM instances compare echo-only, double-talk and near-only baseline paths.
The original and aligned fixtures share the same band-limiting helper.

Thirty-one contract scenarios cover clocks, variable observation cadence,
long uptime, deadlines, identity, queue bounds, lifecycle and missing lookahead.
A 60-frame comparison requires identical PCM and source positions with whole or
fragmented capture blocks, including ring wraparound.
Three 18-second broadband quality configurations add opposite +/-900 ppm device
rates (1,800 ppm relative skew) and +/-20 microsecond timestamp jitter. Current
measured-profile results retain roughly 39–49 dB echo reduction, .964 near-signal
correlation and .95–.96 gain, with delay error below .65 ms and no clipping.
The fixed gates remain 15 dB reduction, .65 correlation, .45–1.5 gain (including
an absolute baseline-energy check), at most 2 ms delay error, and no clipping.
Quality is measured over seconds 8–17; these are finite constant-rate-skew tests,
not arbitrary drift, onset, discontinuity-recovery or perceptual acceptance.

**Sustained tones remain an open release issue.** The eight-tone fixture is
retained as three `--tonal` diagnostics with the same thresholds. The measured
profile reduces its echo by only about .1 dB, so all three fail. A direct APM
control without the new clock/resampler path reproduces that result.

`--warm-tonal` adds four seconds of broadband training before the tones; the same
three clock configurations are evaluated over seconds 8–17. The measured profile
still fails one case. `--warm-tonal-path-change` additionally changes the reflection
at second eight and measures seconds 11–17, preserving the existing three-second
recovery allowance. All three measured-profile cases fail. Training the filter
before music is therefore not an accepted workaround. Capture clipping reports
the final cumulative counters of all three processors, without counting each
historical sample repeatedly.

All twelve aligned quality outcomes enter receipts and `openQualityFailures`.
CI hard-gates the established broadband baseline in **every** profile and records
all tonal/transition failures as warnings and release blockers. No thresholds
have been reduced. See [the diagnostic decision](TONAL_DIAGNOSIS.md) for the
controlled upstream investigation and unselected alternative processor. Green
bounded CI does not approve full-duplex audio.

The source-built Windows runner executes these checks; Android cross-links the
aligned worker and actual APM library with 16 KiB load alignment but does not run
it. No final-mix routing, original app PCM lease bridge, finite-stream flush,
permission/focus integration or complete WebView/platform reference coverage is
provided here. Physical timestamp bounds, changing clock rates, long sessions,
route changes, human speech and Quest/original-Live acceptance remain required.

## Accepted input fragments and flush

`CaptureFrame.samples` identifies the actual valid prefix of the 480-sample
output buffer. Regular `CaptureInput.Read` still assembles complete 10 ms frames.
`FlushPending` hands off a shorter accepted fragment without reading more PCM,
closing the device, inventing acquisition timestamps or changing the owner.
It returns `Empty` when no fragment exists, and cannot replay a previous handoff.
The unused buffer suffix is cleared but is **not** part of the capture stream.
Stale observations, clock rollback, xruns and device loss fail the handoff;
cancellation via `Close` clears the fragment and fences subsequent delivery.

The duplex worker admits 1–480 samples per capture handoff with matching metadata,
contiguous positions and monotonically increasing chunk sequence. Arbitrary chunk
boundaries do not change its fixed 10 ms DSP cadence. It still waits for real sinc
lookahead; a partial handoff does not authorize zero-padding a stalled microphone.
There are 27 capture scenarios and 31 clock/duplex scenarios, including exact
fragment counts, flush/resume, cancellation and a whole/fragmented PCM comparison.

The worker now supplies the downstream **nonterminal** completion fence in
[the drain contract](DRAIN_CONTRACT.md). It snapshots the accepted input, waits
for real alignment context and the backend's fixed processing-delay bound, then
requires ordered sink-delivery receipts. Twenty additional drain scenarios cover
coalescing, timeout, cancellation, resumed PCM parity, 21 backend rate/phase
combinations and 48 final-fragment/skew combinations through the actual APM.
The same sources cross-link for Android; execution here remains off-device.

This is a worker contract, not the original app's integrated `PcmCapture.flush`.
The lease bridge must serialize capture-fragment handoff, sink delivery and
cancellation. Finite EOS, partial output and recursive-filter decay need their
own termination policy; the fixed delay is not a promise that filter history is
empty. App/device/acoustic acceptance remains open. No native backend is selected.

## Dependency notices

No vendor sources or binaries are committed here. The extracted APM release
retains its `COPYING`, `AUTHORS`, `PATENTS` and embedded third-party notices;
Abseil retains its Apache-2.0 license and the Meson overlay its `LICENSE.build`.
Review and package the applicable complete notices before distributing a binary.
The library includes more than Maestro's own Apache-2.0 wrapper.
