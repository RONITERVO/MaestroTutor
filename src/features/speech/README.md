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
signals, not audible-completion signals. Live conversation teardown waits for an
audio-worklet drain acknowledgement plus the device output latency. Triggered TTS
uses the core `SpeechOutput` contract: its browser adapter waits for every scheduled
`AudioBufferSourceNode` to emit `ended`, then allows for the device output tail. Only an explicit
user stop may discard queued model speech. Conversation input also defaults to
`NO_INTERRUPTION` so Android speaker echo cannot barge into the model's response.

`SpeechOutput` accepts copied mono PCM16 and exposes submitted/played sample
positions, a drain fence, reset and disposal. Reset cancels older fences; later
chunks cannot complete an earlier fence prematurely. TTS highlights use played
samples, and output failures stop the request with an unsuccessful result. A
request owns exactly one output; injecting a native adapter must not also start
browser audio. Provider access, transcript and speech caching remain shared.

Unity's `NativeSpeechOutput` implements bounded DSP-scheduled playback at the
current avatar head, including imported-avatar replacement, with generation and
sequence checks and cancellation on focus/pause/audio-device changes. It is not
yet connected to the Android bridge. Live and cached replay still use their
existing output paths. Native routing, Meta HRTF/acoustics and physical echo,
latency and intelligibility acceptance remain open; controlled-clock PCM tests
do not establish audible headset quality.

See [`docs/GEMINI_LIVE_OPEN_POLICY.md`](../../../docs/GEMINI_LIVE_OPEN_POLICY.md)
for the complete allowlist, activity phases, backend audit fields and maintainer
checklist.

## Integration Notes

The speech slice manages observable state. Actual TTS/STT engine 
operations remain in the hooks (useBrowserSpeech, etc.) because they 
involve DOM APIs and event handlers that aren't suitable for pure state.
