# Live observer audio reliability

## Finding

The reported missing sentence ending was a real browser-only capture gap, not a
Gemini transcript-quality problem.

The silent observer first runs local Whisper before it is allowed to open a paid
Live transport. Before this fix, a successful Whisper result closed that capture
`AudioContext` and worklet immediately. The microphone track stayed alive, but no
audio graph consumed it while Maestro created a second `AudioContext`, loaded a
second worklet and connected to Live. Whisper usually recognized an early prefix;
words spoken during the following connection interval were never buffered, sent or
available for transcription. The original headless path opened Live before starting
its synthetic microphone, so it could not reproduce this UI-only blind interval.

There was a second timing drift in the harness. It slept for one frame duration
after every processed frame, so JSON event output and encoding overhead accumulated
on top of the audio duration. A nominal real-time fixture could therefore run much
slower than a real microphone without failing.

## Long-term fix

The browser now transfers ownership of the original capture graph instead of
replacing it:

1. Local VAD and Whisper capture the confirmed prefix.
2. Samples produced while Whisper inference is pending are appended to that prefix.
3. The same worklet continues buffering while video, playback and Live transport
   setup run.
4. Once the normal PCM router and packetizer are ready, `PcmCaptureHandoff` drains
   the connection buffer in order and routes every future packet directly.
5. The same `AudioContext`, worklet and microphone stream remain in use until normal
   session cleanup. Both silent-observer conversation and Live STT use this path.

The Core headless observer/STT path now mirrors the important ownership order:
capture starts, sustained speech is confirmed, Live connects while capture
continues, and the shared `PcmCaptureHandoff` drains into the real packetizer. Paced
input uses absolute sample deadlines, so work done between frames does not change
the speaker's effective rate. Model PCM is queued on a 24 kHz real-time playback
clock and the command waits for that clock to drain after provider turn completion.

This follows Google's Live contract: input is raw PCM16 audio, automatic VAD may be
finalized with `audioStreamEnd`, and model audio output is 24 kHz. Google also
documents prefix padding and silence duration as the controls that prevent onset
clipping and premature pause boundaries. References:

- [Live transcription and hybrid VAD](https://ai.google.dev/gemini-api/docs/live-api/live-transcribe)
- [Live API capabilities and audio formats](https://ai.google.dev/gemini-api/docs/live-api/capabilities)
- [Live WebSocket API reference](https://ai.google.dev/api/live)

## Regression gates

The following checks are intentionally complementary:

- `localSpeechTrigger.test.ts` holds Whisper inference open, continues speaking,
  continues again through a simulated provider-connect interval, and requires every
  marked packet to arrive after transfer. This covers the actual browser capture
  owner.
- `pcmInput.test.ts` requires the shared handoff to preserve order and requires a
  paced synthetic microphone to stay on absolute audio deadlines despite per-frame
  processing time.
- `syntheticLiveJourney.test.ts` starts capture before a deliberately delayed Live
  connection, requires the complete suffix transcript, and waits for model playback
  duration rather than only model bytes or `turnComplete`.
- Every real release fixture supplies the full `expectedTranscript`, with distinctive
  final words. Word recall is computed against the provider input transcript, not
  local Whisper preview text or the assistant response.
- A paced provider run must return all of:
  `transcriptEvidence.passed`, `realtimeEvidence.passed`,
  `timing.uiSpeechHandoff:true`, non-zero connection-handoff samples, non-zero model
  PCM and hashes, one completed audio boundary, and input/playback wall-clock
  durations within their declared tolerances.

Do not weaken these checks to “some transcript arrived”, “some model audio arrived”,
or a mocked provider callback. Those assertions were green while the real UI lost
the suffix.

## Real staging proof: 2026-09-03

The managed `live.observer.turn` command was run against the staging backend with a
recorded 16 kHz PCM user utterance, `pace:true`, and the expected transcript:

> Hello. How are you doing? I am doing great.

The provider input transcript was “Hello, how are you doing? I am doing great.” All
9 expected words matched (`wordRecall:1`, required `0.8`). The final “I am doing
great” clause was present.

Timing and media evidence from operation
`synthetic-live-fd183d58-ddb6-49ae-835f-8a321ac12a7f`:

| Evidence | Result |
| --- | ---: |
| Source PCM | 108,203 samples / 6,762.7 ms |
| Wall-clock input capture | 6,765 ms |
| Provider connection | 1,325 ms |
| PCM retained across the handoff | 66,560 samples / 208 packets |
| PCM sent after gating | 107,200 samples |
| Input stream boundaries | 1 |
| Model PCM | 17,295 samples / 720.6 ms |
| Headless playback elapsed | 731 ms |
| Playback wait after provider completion | 17 ms |
| Input transcript recall | 1.0 / pass |

The command returned only after both the paced user capture and the paced model
playback completed. The input and model PCM SHA-256 values were also non-empty and
stable for the bytes observed by that run.

## Maintainer runbook

Use a real spoken fixture converted to signed PCM16 mono at 16 kHz. Put base64 in a
params file to avoid command-line limits:

```json
{
  "pcmBase64": "<16 kHz PCM16 mono base64>",
  "sampleRate": 16000,
  "pace": true,
  "expectedTranscript": "A phrase whose distinctive final words must survive.",
  "minTranscriptWordRecall": 0.8,
  "runSuggestionAftersteps": false
}
```

Select the same target/native language pair the recording and expected transcript
assume, then run:

```powershell
npm run maestro -- live.observer.turn --profile observer-release --params-file observer-real-audio.json
```

Reject the run if the expected suffix is absent even when aggregate recall happens
to meet a low threshold. For release fixtures, keep the expectation short enough
that losing the final clause necessarily drops recall below the configured minimum.

## Change-review checklist

Any change to local speech recognition, `AudioContext`, worklets, PCM packetization,
Live connection order, VAD hangover, `audioStreamEnd`, or playback teardown must
answer these questions in tests:

1. Who owns capture before, during and after every asynchronous await?
2. Where are samples buffered while Whisper and Live connection are pending?
3. Is transfer ordered, single-use and observable in telemetry?
4. Does a final partial worklet/packetizer chunk reach the provider before the
   stream-end signal?
5. Does the semantic assertion include words recorded after the trigger point?
6. Does a paced check use wall-clock deadlines and wait for audible output drain?

Run the focused audio tests plus the full suite and production build before merging.
