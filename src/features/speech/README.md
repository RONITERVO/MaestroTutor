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

The automatically started re-engagement observer and Gemini Live STT buffer
microphone PCM locally, run quantized `whisper-tiny.en` in a lazy Web Worker after
the energy pre-gate passes, and send audio to Gemini only after the transcript
filter confirms real words. They share one worker/model so switching between the
observer and STT does not double Android memory. Gemini remains the transcript
authority.

The observer also gates video and closes its input while model audio is playing,
so speaker echo cannot start another turn. STT already stops before app TTS plays.
Both paths send `audioStreamEnd` when speech ends, retain a bounded pre-roll to
avoid clipped syllables, and fall back to energy gating if local Whisper cannot
load. Full user-started Live conversations continue to stream directly because
they need the lowest possible conversational latency.

## Integration Notes

The speech slice manages observable state. Actual TTS/STT engine 
operations remain in the hooks (useBrowserSpeech, etc.) because they 
involve DOM APIs and event handlers that aren't suitable for pure state.
