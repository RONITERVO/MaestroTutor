# Sustained-tone echo investigation

The native audio interface remains shared and unselected. **Do not ship the
private DSP modifications or the alternative adapter described here.** They are
diagnostic controls, not production implementations or accepted release profiles.

## Pinned WebRTC APM 2.1 controls

The measured32 profile was rebuilt in a disposable source tree for each control.
Its current three cold-tone fixtures and three broadband fixtures were unchanged.
Each uses actual APM processing, synthetic audio, the same quality thresholds and
nominal/opposite +/-900 ppm clocks. No device or provider was opened.

| Change relative to measured32 | Cold-tone failures / 3 | Broadband failures / 3 |
| --- | ---: | ---: |
| None | 3 | 0 |
| Supported `ep_strength.bounded_erl = true` | 3 | 0 |
| Supported `echo_removal_control.linear_and_stable_echo_path = true` | 3 | 0 |
| Private control: disable poor-excitation freeze only | 3 | 0 |
| Private control: disable narrow-band masking only | 3 | 0 |
| Private control: disable both guards | 0 | 0 |

The last control changes `RenderSignalAnalyzer::PoorSignalExcitation()` and removes
`MaskRegionsAroundNarrowBands(&mu)` calls in both filter update-gain implementations.
Together these changes raise cold-tone reduction from about .1 dB to 35.8–36.9 dB.
The private upstream source files were restored after the experiment; binaries in
that disposable tree are not release artifacts. No production DSP source changed.

This establishes the guards' contribution in these fixtures. It does not establish
that removing them preserves filter stability or speech in general. In particular,
repeated/correlated tones do not identify every possible acoustic path. Do not
replace that uncertainty with a blanket switch or a custom upstream fork based
on six passing cases. Wider qualification below now demonstrates an unacceptable
near-signal regression in that control.

The committed runner now retains additional tones-after-broadband and tonal
path-change cases. The unmodified measured profile fails one and three cases,
respectively. A four-second training period does not make the later tone or moving
reflection case reliable. These are additional open failures, not weaker gates.

## Wider qualification rejects the unguarded control

At source checkpoint `1808026a`, the private control disabling both guards passed
all 127 native lifecycle scenarios, all 40 standalone generated quality cases,
six local SAPI English/Spanish speech cases, and all twelve aligned broadband/
tonal quality cases. This stronger result still did not justify selecting it.

A further 27-case generated voiced-signal matrix overlaps the near-side fundamental
with one render tone, with independent pitch modulation and harmonics. It includes
normal, quiet (one tenth amplitude) and short burst signals, cold/warm/changed-path
tones, and nominal/opposed device clocks. The burst cases include early-window
checks so long-window scores cannot hide attacks being removed. These are generated
stress inputs, not human speech or a claim about word intelligibility.

The unmodified measured32 control fails 24/27 cases. Disabling both guards fails
27/27 despite 30.8–44.5 dB echo reduction: whole-window projected near gain falls
to about .30–.44, below the unchanged .45 floor. Near-only baseline energy gain
remains about .96. The failing .47–.67 correlations and deficient burst onsets
reinforce that an echo-only score cannot establish preservation of desired input.
That first experiment did not isolate adaptation from suppression as the mechanism
of that loss. It rejects the blanket guard removal, not every possible APM solution.
The subsequent stage comparison below narrows the cause.

The committed `--overlapping-speech` mode retains this matrix using explicit signal
parameters and source-time onset windows. `Build.py` requires all 27 outcomes and
72 onset windows, includes failures in its total, and publishes them in CI for
every profile. Existing passing requirements and thresholds remain unchanged.
The private control is not added as a supported profile or shipped upstream patch.
Its commands, source/binary hashes, restored-source checks and outputs are retained
in `.quest-evidence/echo-qualification`. No microphone, headset or provider was used.

## Linear-output attribution

A private build enables the pinned APM's supported linear-output export at both
the AudioProcessing and custom EchoCanceller3 factory boundaries. Separate runs
of identical deterministic inputs select the normal or linear output through the
same clock/resampler path. All 27 normal-output result rows remain identical to
the prior uninstrumented guarded and unguarded controls. This verifies metric
parity for the instrumentation; it is not a bit-for-bit PCM comparison.

Thirty-six impulse cases cover both controls, normal/linear output, three input
rates (16/32/48 kHz) and three block positions. Linear output peaks arrive exactly
64 output samples (4 ms at 16 kHz) before normal output in every case. Each quality
run compares its own double-talk output with its own identically delayed near-only
baseline. Early-window differences between the two modes therefore should not be
read as changes in speech quality without compensating their distinct delays.

The unguarded linear output still fails all 27 cases, with whole-window near gain
.3046–.4446, versus .3045–.4446 after the normal output stage. Its near-only baseline
retains roughly .960–.962 energy gain. The desired-signal loss is already present
before the final suppressor; weakening or bypassing that suppressor is not a fix.
This does not isolate the individual coarse/refined filter or prove a complete
replacement algorithm. No linear-output bypass is added to supported profiles.
Commands, private source and binary hashes, output rows, impulse receipts and
source restoration checks are retained in `.quest-evidence/echo-stages`.

## Alternative backend screening

[SpeexDSP 1.2.1](https://www.speex.org/downloads/) was obtained from Xiph's official
release mirror and verified against its published SHA-256:
`8c777343e4a6399569c72abc38a95b24db56882c83dbdb6c6424a5f4aeb54d3d`.
Its [multi-channel AEC API](https://github.com/xiph/speexdsp/blob/SpeexDSP-1.2.1/include/speex/speex_echo.h)
allows an independent implementation behind the same Maestro C ABI.

A private direct 48 kHz stereo test gave about 34 dB tone reduction. A separate,
provisional 16 kHz adapter with resampling and measured-delay history passed the
existing 27 clock/duplex contracts, eight core cases, three aligned broadband
cases, five onset cases and two changing-path cases. It failed two of three aligned
cold-tone cases and eleven of twenty-four delay/reflection variants. One tone
configuration amplified the residual; upstream also reported automatic resets.
Holding the initial delay instead of following rounded delay updates did not
remove these failures. The direct and adapter fixtures are different experiments;
the direct result is not a parity claim.

Neither this adapter nor SpeexDSP is added to the app or selected as the backend.
No speech-acceptance, Android performance or physical result is claimed for it.
The private source, commands, archive receipt and failed outputs are retained in
`.quest-evidence/echo-tone`; no vendor code or binaries are committed.

### Full-rate follow-up

A fresh private SpeexDSP 1.2.1 screen retains 48 kHz stereo reference/mono capture
through cancellation and downsamples only its output. Archive and extracted source
contents were verified before compiling. A direct 48 kHz screen passes all twelve
normal/quiet/burst near-signal combinations with tonal/broadband render and
128/256 ms filters. This direct screen has no device clocks, resampling or onset
acceptance and cannot establish parity with the shared worker.

The following adapters instead execute the unchanged committed clock-aligned
probe and original processor probes. Numbers are **failing cases**, not passes:

| Private control | Original aligned / 12 | Overlapping / 27 | Onsets / 5 | Path changes / 2 | Delay variants / 24 |
| --- | ---: | ---: | ---: | ---: | ---: |
| Full-rate, 128 ms filter | 0 | 4 | 0 | 2 | 21 |
| Full-rate, 256 ms filter | 0 | 3 | 0 | 2 | 24 |
| 256 ms plus residual suppressor | 0 | 7 | 1 | 2 | 24 |
| 128 ms plus fixed playback-history offset | 0 | 4 | 0 | 2 | 15 |

All four pass the eight core cases. The latter three also pass noise-heavy
double-talk and six local SAPI English/Spanish speech cases. The residual-stage
control uses the upstream preprocessor with noise attenuation, AGC, VAD and
reverberation processing disabled, echo attenuation -40 dB and active-near echo
attenuation 0 dB; its quiet-onset regression remains a failure. Extending the filter
does not solve the three-second recovery requirement. The fixed-history control
anchors a whole-block playback offset once per epoch, retaining at least 16 ms
headroom; it improves some delay variants but does not validate route changes,
arbitrary buffering changes or the complete C ABI delay range.

Upstream automatic-reset warnings also occur and are retained. The 128 ms overlap
run was repeated with stdout/stderr separated after warning interleaving damaged
JSON rows: all 27 outcomes are now individually readable and the same four fail.
The later controls separate those streams from the start. Neither passing direct
screens nor partial improvements qualify a replacement. These Windows private
binaries have no Android/Quest performance, lifecycle or app acceptance. Source,
commands, hashes and failed outputs remain in `.quest-evidence/echo-stages`;
none is a new supported profile or packaged dependency.

### Platform processing remains an unverified option

Android provides a per-session [AcousticEchoCanceler](https://developer.android.com/reference/android/media/audiofx/AcousticEchoCanceler),
with device availability and enabled-state queries; creation can return null.
Its documentation also warns that the platform can enable AEC according to the
input source. An [AAudio session](https://developer.android.com/ndk/reference/group/audio#aaudiostreambuilder_setsessionid)
can be allocated for Java audio effects, potentially at higher latency. The current
isolated `OpenAndroidCapture` requests unprocessed input and allocates no effects
session, so it does not evaluate that option.

A platform-owned alternative could reduce custom DSP maintenance, but that is an
architectural possibility, not a Quest capability or quality finding. Before
selection, verify actual session ownership/control/enabled state, Unity and
WebView/all-output echo coverage, barge-in and quiet speech, route/focus recovery,
latency and full original Live/STT parity. Never stack it unknowingly with software
AEC or equate an availability flag with acceptance. Existing headset holds remain;
no device availability query or microphone acquisition was made here.

## Decision

Keep one shared capture/output ownership and timing contract. Keep both the cold
start and changing-world failures visible. Further backend work must pass the
same echo, double-talk, onset, recovery and timing suite, then physical speech and
Quest acceptance. Do not hide the problem by muting the microphone, removing user
music, injecting calibration noise, or substituting an easier test. Neither the
unmodified APM nor the screened Speex adapter is ready for app promotion.
