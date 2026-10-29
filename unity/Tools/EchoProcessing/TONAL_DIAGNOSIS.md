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
can be allocated for Java audio effects, potentially at higher latency. The raw
isolated `OpenAndroidCapture` requests unprocessed input and allocates no effects
session. The explicit `OpenAndroidPlatformCapture` candidate now allocates an
effects session and guards it with Android AEC ownership; its scripted lifecycle
checks and Android cross-build do not evaluate actual Quest availability or sound.

A platform-owned alternative could reduce custom DSP maintenance, but that is an
architectural possibility, not a Quest capability or quality finding. Before
selection, verify actual session ownership/control/enabled state, Unity and
WebView/all-output echo coverage, barge-in and quiet speech, route/focus recovery,
latency and full original Live/STT parity. Never stack it unknowingly with software
AEC or equate an availability flag with acceptance. Existing headset holds remain;
no device availability query or microphone acquisition was made here.

## Whole-utterance clock-aligned speech investigation — 2026-10-25

The original standalone WAV test repeats its source, starts double-talk at an
already-advanced source position, scores seconds 14–17 and searches a bounded
output lag. English wraps at 15.665 s inside that window. Its passing cases do
not establish preservation of an entire utterance through the aligned worker.

A private diagnostic at source checkpoint `9d3f9423` plays the existing English/
Zira and Spanish/Helena recordings once, starting at source time 6 s. Fixed gains
.5 and .05 apply to both files, without normalization, phoneme amplification or
output-selected lag. Broadband, cold tones, tones after broadband and tones with
a changed path each use nominal and both opposing +/-900 ppm clocks: 24 cases
per recording, 48 total.

Four independent real DuplexProcessor/APM instances receive identical timing:
near speech with silent reference, echo with reference, speech plus echo with
reference, and speech with reference but no acoustic echo. Every frame checks
metadata parity. The whole-source interval includes 20 ms after its end within a
longer simulated continuation; this is not a claim that Close flushes audio.
Export coordinates identify DSP input, not acoustic output time. All 48 cases
retain zero clipping and at most .640 ms modeled delay error.

The unchanged measured32 profile gives these descriptive results:

| Pattern | Classified failures, English / Spanish | Echo reduction across both recordings |
| --- | ---: | ---: |
| Broadband | 0 / 0 | 38.61–50.59 dB |
| Cold tones | 6 / 6 | .089–.135 dB |
| Tones after broadband | 4 / 4 | 13.54–16.80 dB |
| Tones with changed path | 6 / 6 | -2.00–-.94 dB |

Reference-without-echo projected gain stays within .999998–1.000001 of its
processed speech baseline. Four selected sample exports reproduce their matrix
rows exactly. In nominal cold-tone cases for normal English and quiet Spanish,
mixed output is close to baseline speech plus echo-only output (difference RMS
about .00030 and .00291 of baseline RMS). This supports residual echo as the main
contamination in those selected cases. Independent adaptive streams need not
superpose; subtraction is not clean-source recovery or a universal explanation
of the older generated-overlap speech-loss findings.

Four quiet Spanish pause exits have source RMS below one PCM16 step in their
first 80 ms. Ratios against that small denominator cannot establish phoneme loss.
Raw-source attack comparisons also include fixed DSP latency and rising energy.
The analyzer retains sample support, exact zero-pause exits and fixed input-time
windows without moving windows, boosting audio or adding acceptance gates.

### Two supported-setting controls were rejected

Each uses fresh pinned sources and one setting change; neither removes upstream
adaptation guards. The required per-case gate rejects both. Remaining probes are
recorded as diagnostics after rejection, not successful builds.

| Isolated change | Aligned failures / 39 | Existing passes lost | New passes |
| --- | ---: | ---: | ---: |
| Unchanged measured32 | 31 | 0 | 0 |
| Disable coarse-filter output selection | 36 | 5 | 0 |
| Disable stationary residual-echo audibility scaling | 32 | 2 | 1 |

Forcing refined output loses both previously passing drift cases after broadband
and three overlapping-signal cases. It does not disable coarse adaptation or the
shared narrow-band guards. Further speech qualification was unnecessary to reject
this regression; no unexecuted standalone/speech result is claimed for it.

Disabling stationary audibility scaling improves the three original trained-tone
cases to about 24 dB, but loses both passing bursty-overlap drift cases after
broadband (onset gains about .064 and .028). Cold tones and changed paths still
fail. Standalone failures rise from 0 to 6 out of 40: noise-heavy double-talk and
all five onset cases. All six original English/Spanish speech cases still pass.
The new whole-utterance matrix instead worsens from 32 to 36 failures out of 48:
both voices gain the normal nominal trained-tone case but lose all three quiet
broadband cases. Reference-without-echo speech is also attenuated. The old passing
speech excerpts therefore cannot override the broader negative evidence.

Keep the current evaluation configuration and all existing gates unchanged.
These two recordings and synthetic paths do not establish human intelligibility,
device acoustics or app/Quest acceptance. The 32 baseline speech classifications
are separate descriptive evidence, not added to or substituted for the existing
31 open quality failures. No native backend is promoted; device/provider holds
remain in place.

Evidence is retained under `.quest-evidence/echo-speech-20261025`: private probe
and analyzer source, WAV hashes, timing receipts, complete logs, selected
seven-column sample exports, candidate commands/hashes and independent reviews.
`echo-native/sp1w` is the fresh baseline; `sp2w` and `sp3w` are rejected controls.
Private diagnostic sources and recordings are not app assets.

## History attribution and startup-rate rejection — 2026-10-28

The completed private passive trace from October 26 accounts for bypassed input
blocks and delay-buffer reinitialization. All 39 original cases retain identical
PCM, metrics and verdicts; six four-stream controls retain matching outputs.
Warm quiet-voice cases with an identical microphone source and no acoustic echo
already acquire distortion in the coarse/refined filters before final suppression.
Cold quiet and burst controls do not show that same pattern. A projected gain near
one alone therefore cannot establish clean speech: added error can preserve
projection while reducing correlation.

A separate, completed intervention in `attempt4b-history` sets only the near
source before six seconds to zero in two quiet cases. Render, modeled echo,
timing, DSP and the source suffix from six seconds onward stay unchanged.
For nominal trained tones, final no-echo-reference correlation in the three source
windows improves from approximately .702/.631/.663 to 1. For the changed-path
drift case it improves from .745/.710/.727 to 1. This establishes aggregate history
dependence in those controls. The intervention also changes early tonal history,
detector/filter state and the onset at six seconds; it does not isolate the
broadband interval, one coefficient or the cause of every open failure. Changing
the test input is attribution, not an accepted quality fix.

A bounded supported-setting experiment at source `39d20860` changes only
`filter.coarse_initial.rate` from .95 to .70. The steady-state rate, upstream
guards, selection and stationarity settings remain unchanged. A newly linked
unchanged control first reproduces every retained aligned and standalone metric.

| Private control | Aligned failures / 39 | Existing passes lost | Standalone failures / 40 |
| --- | ---: | ---: | ---: |
| Unchanged measured32 | 31 | 0 | 0 |
| Initial coarse rate .70 | 36 | 5 | 0 |

The candidate loses both trained-tone drift passes and all three previously
passing overlapping cases. It is rejected without further speech qualification;
passing standalone checks cannot override these regressions. No app DSP,
configuration, dependency authority or original threshold changes. Commands,
source/binary hashes, complete failed results and unchanged-authority checks are
retained in `.quest-evidence/echo-startup-20261028/`.

The maintained `HistoryProbe.cpp` now makes the four-stream controls reproducible
without private upstream instrumentation or binary sample exports. Six original
cases plus the two prefix interventions report 24 fixed input-source-time windows.
This diagnostic separates reference-induced distortion from residual echo; its
successful execution certifies neither speech nor device quality. The original
39-case quality gate remains independent. Private stage-level attribution and
causal limits remain in `.quest-evidence/echo-passive-trace-20261026/attempt3/` and
`attempt4b-history/`. No private upstream patch or recorded voice is shipped.

## Mobile-AEC screening — 2026-10-28

A private relink first reproduced all 39 aligned and 40 standalone measured32
metrics. The screened replacement enables APM mobile mode and omits the custom
EchoControl factory, which would otherwise override that selection. It uses the
pinned upstream AECM with its default speakerphone route and comfort noise off.

All **39 aligned cases fail**, compared with 31 baseline failures: all eight
existing passes are lost and no new case passes. The candidate is rejected.
Its standalone core runner stops at its first failing echo case; seven remaining
core cases are unrun. Other groups complete with noise 1/1, onset 3/5, path 2/2
and delay 24/24 failures. This is not a complete 40-case candidate result.

A separate direct-API control compares WebRtcAecm with APM mobile mode using the
same 16 kHz, 40 ms delayed echo-only input and a 10–20 second measurement window.
White-noise reduction is approximately .071/.133 dB; two-tone reduction is
.071/.078 dB respectively. It provides no evidence that changing only the APM
wrapper fixes this candidate. It does not isolate the failure's cause or prove
that all possible AECM integrations are unsuitable.

Commands, hashes, full results and the direct control are retained in
`.quest-evidence/echo-mobile-20261028/`. The previous evidence authority, app DSP,
dependency source, original thresholds and 14 unrelated files are unchanged.
No backend is promoted; the original 31 aligned quality failures remain open.

## Mobile processor timing and input dynamics — 2026-10-29

A separate private control keeps the same pinned direct AECM and APM mobile
implementations and 40 ms synthetic echo path. It varies input rate (8/16 kHz),
delay hint (0/10/20/40/80 ms), direct suppression mode (0–4), and white-noise or
two-tone reference. All 100 rows are retained; APM does not expose the direct
suppression-mode setting, so its 20 unique configurations repeat across that
axis. Measured from 10–20 seconds, direct reduction stays near .07 dB. APM's
best result is about 1.16 dB for 8 kHz noise; the 16 kHz cases remain near
.13 dB noise and .08 dB tone. These settings alone do not rescue these inputs.

An eight-row follow-up holds 16 kHz, the 40 ms hint and direct mode 3 constant.
The steady rows reproduce the earlier direct control exactly. A one-second
silent introduction or periodic half-second silence leaves similar poor results.
Smoothly varying reference amplitude from .05 to 1 over a 1.7-second cycle
instead yields approximately 32.11/32.10 dB direct/APM reduction for noise and
28.19/30.76 dB for the tones. No near-end speech is present in these controls.

This supplies a positive control for both existing integrations; it does not
establish double-talk quality or repair stationary world audio. The pinned
`aecm_core.cc` updates its energy/VAD thresholds from input dynamics, which is a
plausible explanation worth instrumenting, not a measured internal attribution
from these output-only controls. Do not inject calibration audio or replace
the failing stationary acceptance cases with these easier dynamic inputs.

Sources, binaries, commands, hashes, all metrics and the decision are retained
in `.quest-evidence/echo-mobile-timing-20261029/` and
`.quest-evidence/echo-mobile-dynamics-20261029/`. Both runs verify the original
source/archive/probe authority unchanged. The app, supported evaluation profiles
and thresholds remain unchanged; the 31 aligned quality failures remain open.

## Recovery-state and reset-bounds controls — 2026-10-29

A freshly compiled copy of the earlier full-rate, fixed-history Speex adapter
reproduces both retained changing-path metric logs. A passive private trace
preserves those printed metrics and records foreground/background error, learning
state and partition weights every 100 ms. It shows slow adaptation after the
eight-second path change; it does not by itself identify one defective estimator.

Two known-time interventions isolate retained canceller state. Calling the reset
API at eight seconds raises the minimum recovered reduction from 4.40/4.82 dB
to 37.80/20.27 dB in the mono linear/stereo nonlinear cases. Recreating only the
canceller, while keeping external reference and resampler history, yields
39.09/18.71 dB. Both exceed the unchanged 15 dB requirement in these echo-only
cases. These are **oracle controls**, not a usable change detector: no runtime
knows the fixture's change time, and an arbitrary reset during learner speech
has not been qualified.

A supported 240-sample processing frame, called twice per unchanged 10 ms ABI
frame, keeps the same effective 6,240-sample filter span. It improves those
recovery values to 8.53/6.53 dB but still fails both cases. No full quality pass
or supported-profile promotion is claimed for that rejected control.

Source inspection also found that upstream 1.2.1's reset loop omits channel
multipliers for the learned weights, foreground coefficients, reference history
and playback buffer. A separate four-layout sentinel probe confirms the mismatch:
with one microphone and two render channels, reset retains half of each tested
allocation (12,480 weights and foreground values, 13,440 reference values and
1,440 playback values). Mono/mono clears those allocations. The same loop bounds
were present in the [upstream master source inspected that day](https://raw.githubusercontent.com/xiph/speexdsp/master/libspeexdsp/mdf.c).
This is not a claim that every internal state field has been audited.

A minimal private patch corrects only those four loop bounds. All four sentinel
layouts then clear their tested allocations, but the complete quality comparison
does **not** qualify a backend:

| Completed group | Unmodified adapter failures | Reset-bounds patch failures |
| --- | ---: | ---: |
| Aligned quality / 39 | 4 | 4 |
| Standalone core + noise + onsets / 14 | 0 | 0 |
| Path recovery / 2 | 2 | 2 |
| Delay/reflection recovery / 24 | 15 | 15 |
| Existing English/Spanish speech excerpts / 6 | 0 | 0 |

The current alignment executable is reused only after checking its retained app
source against current inputs; standalone probes are freshly compiled. Current
onset maxima and exact 39-case identities are validated by `QualityResults.py`.
The private bounds fix changes some metrics after automatic resets but rescues
no failing case. It is a separate dependency defect, not the explanation for
all remaining quiet-speech and recovery failures.

All 148 archive files match the pinned release. Sources, command lines, complete
outputs, passive trace, reset probes and final decision are retained under
`.quest-evidence/echo-speex-recovery-20261029/`. Original source/archive authorities
and the 14 protected files remain unchanged. There is no device/provider work,
new shipped dependency, selected backend change or relaxed acceptance gate.
The supported AEC3 evaluation's 31 failures remain open.

## Partition indexing and retained recovery models — 2026-10-29

A second independent SpeexDSP 1.2.1 defect affects proportional adaptation weights
for multiple render channels. The learned coefficient layout is
`microphone*N*K*M + partition*N*K + speaker*N + bin`. The
`mdf_adjust_prop` scan instead treats all microphone/speaker combinations as
contiguous partition arrays. A sentinel in partition 3 therefore gives the
largest adaptation weight to partition 6 or 7 in stereo. Six of nine channel
positions across mono/stereo input/output layouts fail this identity check.

A private patch passes the render-channel count into that scan and matches the
actual coefficient layout. All nine sentinel checks then pass. It still leaves
four aligned and 17 standalone quality failures: it rescues one burst case but
loses a quiet changed-path case. The mono path-recovery result is unchanged;
stereo improves from 4.82 to 5.49 dB and remains below 15 dB. The two reproduced
indexing defects warrant separate dependency work; neither is a complete audio
solution. The pinned source remains unchanged.

A separate private adapter uses the **unmodified** pinned Speex source. It
retains at most two independent cancellers sharing the same external reference
history and resamplers. All decisions use observed microphone/reference/output
energy, never the known fixture change time or isolated expected sources.
Ten-millisecond energy estimates use a .9/.1 moving average. After 50 frames
with output/input energy below .1, a rise above .2 for five frames can start a
fresh secondary model. Following 20 warm-up frames, five frames below half the
selected model's output energy and .2 of microphone energy begin an 80 ms
crossfade. A one-second cooldown limits retriggering. These are experimental
settings, **not a validated speech detector**.

Discarding the earlier model at replacement solves the standalone recovery
cases but introduces three aligned speech regressions. Keeping both models
learning, and allowing selection back, removes those regressions:

| Private comparison | Aligned failures / 39 | Standalone failures / 40 |
| --- | ---: | ---: |
| Unmodified single model | 4 | 17 |
| Partition-index correction alone | 4 | 17 |
| Energy-driven replacement, discard earlier model | 6 | 0 |
| Energy-driven replacement, retain earlier model | 3 | 0 |

The retained-model control passes all 35 previously passing aligned cases and
rescues the cold burst/negative-capture-drift case. Minimum recovered reduction
is 38.37/18.82 dB in the two changing-path cases and at least 16.97 dB in the
24 delay variants. All 31 clock/duplex contracts and six existing English/Spanish
synthetic speech excerpts pass. The three remaining failures are quiet
overlapping speech: cold tones with -900/+900 ppm capture/render drift, and
trained tones with a path change at nominal or -900/+900 ppm drift. Their
original quality-window correlations are .388486, .567772 and .599183 against
the unchanged .65 requirement. This does not qualify a backend.

The maintained history diagnostic now includes both previously missing exact
quiet cases, giving eight original cases plus two prefix interventions and
**30 source-time windows**. A fresh Windows executable runs against measured32,
baseline Speex and the retained-model control. Each produces all 30 validated
windows plus eight metric contracts. The changed translation unit also compiles
for Android ARM64 with warnings treated as errors; this is not an app build or
Android execution.

The retained-model outputs show why recovery alone is insufficient. In the
11–17-second source window:

| Quiet case | No-echo-reference gain / correlation | Mixed gain / correlation |
| --- | ---: | ---: |
| Cold tone, negative capture drift | .673 / .818 | .588 / .573 |
| Changed path, nominal clock | .692 / .832 | .564 / .568 |
| Changed path, negative capture drift | .690 / .831 | .580 / .599 |

Even the independent stream with no acoustic echo changes learner-like input.
The separate echo-only stream still contains residual energy. These controls
establish both observations without subtracting independent adaptive outputs;
they do not assign every mixed-signal error to one cause. These source-time
windows differ from the quality suite's fixed output-index windows, so their
correlations must not be substituted for the acceptance values above.

Reproduction sources, compiler arguments, binaries, complete logs and hash
receipts are local in `.quest-evidence/echo-speex-partitions-20261029/`. The
`history-expanded` receipt explicitly records the diagnostic-only source changes
after the earlier comparison. Forty-six Python evidence-contract tests pass.
The two-canceller adapter remains private: speech preservation, steady-state
CPU/latency, bounded long-session counters, allocation/failure behavior, complete
delay/route handling and physical Quest acceptance are unqualified. Lower output
energy is not proof of better speech. No app integration, selected dependency,
original quality gate, recorded voice or device/provider action changes.
The supported measured32 evaluation still has its original 31 quality failures.

## Combined fixes and full convolution constraints — 2026-10-29

The retained two-canceller adapter was then rebuilt with each dependency fix and
both together. A second comparison changes only the AUMDF constraint schedule:
all filter partitions are constrained on every frame instead of partition zero
plus one rotating partition. This follows the MDF/AUMDF distinction documented
beside that condition in the pinned source; it is a private algorithm control,
not a supported library setting.

| Retained-model variant | Aligned failures / 39 | Standalone failures / 40 |
| --- | ---: | ---: |
| Reset bounds corrected | 3 | 0 |
| Partition indexing corrected | 4 | 0 |
| Both corrections | 4 | 0 |
| Full MDF constraints, original indexing | 5 | 0 |
| Full MDF constraints, both corrections | 5 | 0 |

Each variant also passes the 31 clock/duplex contracts and six existing synthetic
speech excerpts, and completes all 30 four-stream diagnostic windows. Reset
correction changes some printed metrics but no verdict. The combined correction
still loses the positive-capture-drift quiet changed-path case. Both full-MDF
controls additionally lose the positive-capture-drift quiet cold-tone case.
None rescues a failing case compared with the earlier 36/39 retained prototype.

Full constraints modestly increase no-echo-reference projected gain in the three
late quiet windows, approximately .67–.69 to .72–.73, but correlations remain
about .83 and mixed-signal correlations remain poor. Neither higher gain nor more
frequent constraints establishes better overall speech. No runtime performance
claim follows from these desktop diagnostics; full constraints require more
transforms.

These results reject both combined fixes as a sufficient quality solution and
reject promotion of the full-MDF controls. They do **not** justify retaining
known dependency bugs to preserve a better case count. The next audio decision
needs an explanation and correction for adaptation-induced speech distortion,
not another constraint-rate change. [Valin and Collings' learning-rate study](https://jmvalin.ca/papers/valin_icassp2007.pdf)
describes the competing demands of double-talk and echo-path recovery; it is
research context, not validation of any current Maestro implementation.

Full sources, dependency hashes, command lines, outputs and decision receipts
are retained in `.quest-evidence/echo-speex-combined-20261029/` and
`.quest-evidence/echo-speex-fullmdf-20261029/`. All five runs are terminal;
current source and pinned dependency authorities stayed unchanged. The 14
protected files remain unchanged. No processor was selected, no acceptance
threshold was relaxed and no device/provider work occurred. The native
measured32 evaluation still has 31 open aligned failures.

## Decision

Keep one shared capture/output ownership and timing contract. Keep both the cold
start and changing-world failures visible. Further backend work must pass the
same echo, double-talk, onset, recovery and timing suite, then physical speech and
Quest acceptance. Do not hide the problem by muting the microphone, removing user
music, injecting calibration noise, or substituting an easier test. Neither the
unmodified APM nor the screened Speex adapter is ready for app promotion.
