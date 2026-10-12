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
on six passing cases.

The committed runner now retains additional tones-after-broadband and tonal
path-change cases. The unmodified measured profile fails one and three cases,
respectively. A four-second training period does not make the later tone or moving
reflection case reliable. These are additional open failures, not weaker gates.

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

## Decision

Keep one shared capture/output ownership and timing contract. Keep both the cold
start and changing-world failures visible. Further backend work must pass the
same echo, double-talk, onset, recovery and timing suite, then physical speech and
Quest acceptance. Do not hide the problem by muting the microphone, removing user
music, injecting calibration noise, or substituting an easier test. Neither the
unmodified APM nor the screened Speex adapter is ready for app promotion.
