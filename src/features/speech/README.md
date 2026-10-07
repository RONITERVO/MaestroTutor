# Speech Feature

The speech feature handles Text-to-Speech (TTS) and Speech-to-Text (STT) functionality.

## Responsibilities

- Gemini Live TTS playback (single TTS engine)
- Gemini Live STT integration
- Audio recording and playback
- Speech queue management
- Cost-gated silent observer input

## Owned Store Slice

`speechSlice` - see `src/store/slices/speechSlice.ts`

### State
**STT:**
- `isListening`: Whether STT is active
- `transcript`: Current recognized text
- `sttError`: Any STT error message
- `isSpeechRecognitionSupported`: Microphone API availability
- `recordedUtterancePending`: Pending audio recording
- `sttInterruptedBySend`: Whether STT was interrupted by send

**TTS:**
- `isSpeaking`: Whether TTS is active
- `speakingUtteranceText`: Text currently being spoken
- `isSpeechSynthesisSupported`: Live TTS capability

### Key Actions
- `setIsListening()`: Update listening state
- `setTranscript()`: Update transcript
- `clearTranscript()`: Clear transcript
- `setIsSpeaking()`: Update speaking state
- `claimRecordedUtterance()`: Get pending recording

## Public API

Import from `src/features/speech/index.ts`:

```typescript
import { 
  SttLanguageSelector,
  useBrowserSpeech,
  useGeminiLiveConversation,
  pcmToWav,
} from '../features/speech';
```

## Components

- `SttLanguageSelector`: Language picker for STT

## Hooks

- `useBrowserSpeech`: Gemini STT wrapper (legacy name)
- `useTtsEngine`: TTS engine abstraction
- `useGeminiLiveConversation`: Gemini Live API
- `useGeminiLiveStt`: Gemini-based STT

## Utils

- `audioProcessing.ts`: PCM to WAV conversion, silence detection
- `audioUtils.ts`: Audio playback utilities
- `observerSpeechDetection.ts`: Ariadne-style detector implementation and backward-compatible observer names
- `liveSpeechDetection.ts`: shared detector names used by observer and Live STT
- `localWhisperClient.ts`: one reference-counted Whisper worker shared by both Live paths

## Local Live input gate

The automatically armed re-engagement observer and Gemini Live STT buffer
microphone PCM locally and run quantized `whisper-tiny.en` in a lazy Web Worker
after the energy pre-gate passes. They do not open Gemini Live until the transcript
filter confirms real words. They share one worker/model so switching between the
observer and STT does not double Android memory. The local transcript appears as
a pending preview; Gemini remains the final transcript authority.

The observer also gates video and closes its input while model audio is playing,
so speaker echo cannot start another turn. STT already stops before app TTS plays.
Both paths send `audioStreamEnd` when speech ends and retain a bounded pre-roll to
avoid clipped syllables. Before connection they fail closed if local Whisper is
unavailable. An energy fallback can preserve later turns only after an authorized
transport is already active. Full user-started camera Live conversations continue
to stream directly because their click is an explicit open reason.

## Live output completion contract

Gemini can deliver an entire transcript and PCM response much faster than the
speaker can play it. `turnComplete`, `goAway`, and socket close are transport
signals, not audible-completion signals. Live and triggered TTS use the same core
`SpeechOutput` contract. Live's browser adapter owns worklet acknowledgements plus
device output latency; triggered TTS's browser adapter waits for every scheduled
`AudioBufferSourceNode` to emit `ended`, then allows for the device output tail.
Stop, host suspension or renderer failure can discard queued speech; provider
completion cannot. Conversation input also defaults to
`NO_INTERRUPTION` so Android speaker echo cannot barge into the model's response.

`SpeechOutput` accepts copied mono PCM16 and exposes submitted/played sample
positions, a drain fence, reset and disposal. Reset cancels older fences; later
chunks cannot complete an earlier fence prematurely. TTS highlights use played
samples, and output failures stop the request with an unsuccessful result. A
request owns exactly one output; injecting a native adapter must not also start
browser audio. Provider access, transcript and speech caching remain shared.

Live's renderer is injected by its runtime port. The browser adapter keeps
worklet resampling/startup buffering, copies PCM before transfer and tags messages
with a reset generation. Drains fence submitted samples so later speech cannot
delay a previous fence indefinitely. Worker results commit in provider order.
Render/decode failures close Live with an error rather than complete a turn with
missing speech. Stop resets output immediately, including while capture cleanup
is pending; the speech-gated input path stays closed until actual output drains.

Unity's `NativeSpeechOutput` implements bounded DSP-scheduled playback at the
current avatar head, including imported-avatar replacement, with generation and
sequence checks and cancellation on focus/pause/audio-device changes. The native
book selects it for both Live and triggered TTS through `SpeechBookClient` and
Android's top-document polling. No JavaScript-to-JNI object is exposed to frames.
Each transient exchange carries a document, browser session, output revision and
ordered chunks; Unity acknowledges accepted and actually played sample counts.
Lost receipts may resend the same bytes but cannot play them twice. Stop/reload,
missing heartbeats, a stalled output or avatar-owner replacement closes the voice;
an old browser revision cannot resume it after focus returns, even if JavaScript
missed the suspension notification. Native failure never falls back to a second
browser copy. Provider, account and transcript ownership remain unchanged.

The web queue is capped at 120 seconds; native credit admits at most two seconds,
in batches of at most eight 4,800-sample chunks. Small incoming packets coalesce
until offered, then their sequence/bytes remain immutable. Native polling slows
while idle. PCM never enters saved room state or action receipts. Cached Maestro
speech selects the same output, decoding inline audio to mono 24 kHz before
playback. Decoding is inaudible; Quest never starts an HTML Audio copy. Browser
replay uses the scheduled output and its actual device-tail fence. Encoded input
is capped at 16 MiB and decoded speech at 120 seconds. Stop fences outstanding
decode/context promises, and a renderer failure clears the remaining replay
queue. A learner's original recording carries explicit speaker identity and
retains HTML Audio playback; artifact music/audio remain independent. Meta
HRTF/acoustics and physical echo, latency and intelligibility acceptance remain
open; controlled-clock PCM tests do not establish audible headset quality.

Run `node scripts/probe-speech-output.mjs` for isolated, muted Chromium rendering
at 24/48 kHz with the real worklet and analyser. It checks PCM, early drain fences,
the output tail and reset/reuse without a provider or account. Vitest additionally
checks the full Live lifecycle with native-style renderer injection. These checks
do not replace managed/BYOK headset output, microphone echo or acoustics testing.

`node scripts/probe-cached-speech.mjs` additionally decodes a synthetic stereo
32 kHz WAV in real Chromium, checks mono 24 kHz samples, renders at 24/48 kHz and
verifies drain, cancellation and fresh replay without closing the borrowed
context. The probe is muted and blocks non-local network requests.

See [`docs/GEMINI_LIVE_OPEN_POLICY.md`](../../../docs/GEMINI_LIVE_OPEN_POLICY.md)
for the complete allowlist, activity phases, backend audit fields and maintainer
checklist.

## Integration Notes

The speech slice manages observable state. Actual TTS/STT engine 
operations remain in the hooks (useBrowserSpeech, etc.) because they 
involve DOM APIs and event handlers that aren't suitable for pure state.
