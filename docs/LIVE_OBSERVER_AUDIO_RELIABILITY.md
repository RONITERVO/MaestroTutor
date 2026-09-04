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

A later managed/BYOK parity run exposed a third failure mode. The lossless handoff
initially drained its retained prefix as fast as encoding and WebSocket writes
allowed. A slow managed connection could therefore preserve every sample yet send
several seconds of PCM to Gemini as a burst. Gemini's realtime-input path is
latency-oriented and does not promise deterministic ordering under bursts; two
managed runs each delivered all 127,492 bytes after setup but produced no output.
Both correctly cost zero. The identical direct run, whose connection completed
before the queue grew, succeeded. This explains why byte-count-only tests could be
green while a real turn was silent.

## Long-term fix

The browser now transfers ownership of the original capture graph instead of
replacing it:

1. Local VAD and Whisper capture the confirmed prefix.
2. Samples produced while Whisper inference is pending are appended to that prefix.
3. The same worklet continues buffering while video, playback and Live transport
   setup run.
4. Once the normal PCM router and packetizer are ready, `PcmCaptureHandoff` drains
   the connection buffer in order. The packetizer schedules that retained prefix
   on absolute PCM deadlines instead of flooding the provider, then routes every
   future packet on the same timeline.
5. The same `AudioContext`, worklet and microphone stream remain in use until normal
   session cleanup. Both silent-observer conversation and Live STT use this path.

The managed gateway independently applies the same PCM-cadence limit before its
provider socket. That protects older managed clients and makes billing observation
and provider delivery share one ordered queue. UI/BYOK and gateway package manifests
also exact-pin the same `@google/genai` version; the release-config verifier fails
if those versions diverge.

The Core headless observer/STT path now mirrors the important ownership order:
capture starts, semantic speech is confirmed from an intact elapsed-audio window, Live connects while capture
continues, and the shared `PcmCaptureHandoff` drains into the real packetizer. Paced
input capture and provider delivery use absolute sample deadlines, so work done
between frames or a slow connection does not change the speaker's effective rate.
Model PCM is queued on a 24 kHz real-time playback clock and the command waits for
that clock to drain after provider turn completion.

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
- `realtimePcmPacketizer.test.ts` injects a five-packet burst and requires sends at
  PCM cadence while proving already microphone-paced packets gain no extra delay.
- The gateway state-machine suite injects buffered 100 ms packets and requires
  provider delivery at 0/100/200 ms, independently of client behavior.
- `syntheticLiveJourney.test.ts` starts capture before a deliberately delayed Live
  connection, requires the complete suffix transcript, keeps the socket for a short
  second turn, and waits for every model byte to finish real-time playback even
  when another byte arrives after playback has already begun.
- Every real release fixture supplies the full `expectedTranscript`, with distinctive
  final words. Word recall is computed against the provider input transcript, not
  local Whisper preview text or the assistant response.
- A paced provider run must return all of:
  `transcriptEvidence.passed`, `realtimeEvidence.passed`,
  `realtimeEvidence.providerInputPacingPassed`,
  `timing.uiSpeechHandoff:true`, non-zero connection-handoff samples, non-zero model
  PCM and hashes, the expected completed audio boundaries, and input/playback wall-clock
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

### Same-fixture access parity replay through the server-observed gateway

The identical 108,203-sample human recording was replayed through both managed
and BYOK on 2026-09-03. Both provider paths transcribed all nine expected words,
including the final “I am doing great” clause (`wordRecall:1`), and returned only
after their complete model-audio playback clocks drained:

| Evidence | Managed | BYOK |
| --- | ---: | ---: |
| Input capture for 6,762.7 ms PCM | 6,772 ms | 6,768 ms |
| Provider connection | 1,904 ms | 200 ms |
| PCM retained across UI handoff | 75,840 samples / 237 packets | 48,640 samples / 152 packets |
| PCM sent after gating | 107,200 samples | 107,200 samples |
| Model PCM | 537,135 samples | 298,095 samples |
| Model audio / actual playback | 22,380.6 / 22,947 ms | 12,420.6 / 12,696 ms |
| Provider messages | 81 | 50 |
| Transcript recall | 1.0 / pass | 1.0 / pass |

Both runs observed setup, server-content, session-resumption and usage metadata.
The managed operation was
`synthetic-live-a894f8b8-8e84-4698-9f5c-b4777e98fcf1`; BYOK was
`synthetic-live-e5dbc922-e37a-4d34-bfe4-6668f1dc1ffc`.
The managed ledger recorded 4,510 provider tokens, billed 13 credits / USD
0.012719, released the rest of the 87-credit maximum reservation and finished at
zero reserved credits. BYOK attributed provider billing to the supplied key owner.
Response length is intentionally not compared because model wording and audio are
nondeterministic. This is the same recording and semantic assertion at the same
real-time boundaries, not a comparison between synthetic transcripts or
differently paced fixtures.

The fixed-window flaw was also retested directly after the gateway deployment. A
second managed ticket was consumed, the provider connected, and the client closed
without sending input or receiving useful output. The result was `released`, zero
credits/USD, `usefulOutput:false`, no usage row, no charge row, unchanged available
and lifetime-spent balances, and zero reserved credits. A separate unused issued
ticket reserved 87 credits and was then recovered by the deployed one-minute
scheduler without client cooperation or spend. The long-term accounting and deployment gates are in
[`HEADLESS_COVERAGE.md`](./HEADLESS_COVERAGE.md).

### Burst-pacing reproduction and final full-journey proof

Before provider-send pacing, two isolated managed first-lesson runs failed the STT
stage after setup. Each attempt contained all 127,492 input bytes, four provider
messages, no turn completion/output, and an auditable zero-credit release. After
client and gateway pacing were enabled, the same short `Play. Play. Play.` fixture
completed on every managed Live surface.

Against exact-pinned staging gateway revision `maestrotutor-live-gateway-00005-t4v`,
managed operation `first-lesson-75ff94aa-64a7-4933-a3c7-cd20ae2f5c13`
completed 14 user turns with all 18 coverage flags true. STT, Live audio, Live
audio/video, observer audio and observer audio/video all had full input-transcript
recall; each visual mode sent a frame and each response's complete PCM playback was
awaited. Its 48 usage rows matched 48 charge rows and 433 credits / USD 0.413022,
with zero reservation before and after. This was the exact fixture that had exposed
the burst failure, not a relaxed replacement.

### Single-turn and long-audio release gate

Each managed and BYOK socket now handles one turn, with an optional low-resolution
visual frame. Continuing chat uses a new connection with rebuilt context.
The managed proof correlates the socket request ID with exactly one usage row and
one charge row, checks the provider usage snapshots are included, recalculates
the charge from the checked-in price registry, and requires zero shortfall or
stranded reservation. This prevents later turns from being accidentally free or
charged as a fixed maximum.

The long fixture is based on the 37.55-second English question recorded with a
Finnish accent in `Tallenne (14).m4a`. Local Whisper `large-v3-turbo` with English
forced recovered the whole question, ending with “what is the main reason for
this”. The checked-in transcript evidence includes segment and word timestamps;
the personal recording itself is not committed. CI instead reuses one checked-in,
non-personal generated WAV with the same wording and a 400 ms mid-sentence
hesitation. Its exact bytes previously reached 47/48-word recall in both access
modes, avoiding per-run TTS truncation and needless fixture-generation cost. Each
run must transcribe the complete question and finish playing five English/Finnish
response pairs. The observer continues to exercise its real local semantic gate.
For the finite long conversation check, CI disables provider VAD and explicitly
marks the recording's start and end. This isolates long-audio delivery and output
playback from provider endpoint timing; `journey.firstLesson` separately keeps the
normal product conversation on automatic VAD.

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
7. Is retained PCM paced at the provider-send boundary, including after a slow
   connection, rather than merely captured at real time?
8. Does the same connected session pass a short second utterance after the first
   reply has audibly drained?

Run the focused audio tests plus the full suite and production build before merging.
