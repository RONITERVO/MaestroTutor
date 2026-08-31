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
- `observerSpeechDetection.ts`: Ariadne-style energy pre-gate, Whisper output filtering, and bounded pre-roll windows

## Silent observer input gate

Only the automatically started re-engagement observer enables `gateInputOnSpeech`.
It buffers microphone PCM locally, runs quantized `whisper-tiny.en` in a lazy Web
Worker after the energy pre-gate passes, and sends audio plus video to Gemini only
after the transcript filter confirms real words. Gemini remains the transcript
authority. Model playback closes and clears the gate so speaker echo cannot start
another turn. If local Whisper cannot load, the observer falls back to the energy,
cooldown, and playback protections rather than becoming unavailable.

## Integration Notes

The speech slice manages observable state. Actual TTS/STT engine 
operations remain in the hooks (useBrowserSpeech, etc.) because they 
involve DOM APIs and event handlers that aren't suitable for pure state.
