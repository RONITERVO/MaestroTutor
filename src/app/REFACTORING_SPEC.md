# Maestro Tutor - App.tsx Refactoring Specification

> **READ THIS ENTIRE DOCUMENT BEFORE MAKING ANY CHANGES**

This document is the **authoritative specification** for refactoring `src/app/App.tsx` into composable hooks in `src/app/hooks/`.

## Purpose

The original monolithic `App.tsx` (~3200 lines) must be split into focused hooks while:
- Preserving **100% of functionality**
- Maintaining the same external behavior
- Keeping `App.tsx` as a thin shell that composes hooks

## Reference Implementation

The original implementation is preserved at:
```
src/app/originalOldAppTsx/originalOldApp.tsx.bak
```

**YOU MUST** read this file in full to understand every state, ref, effect, callback, and behavior that must be preserved. This specification summarizes requirements, but the source file is the source of truth for implementation details.

---

## Refactoring Workflow

### Phase 1: Analysis
1. Read the original `originalOldApp.tsx.bak` completely
2. Identify all state variables, refs, effects, and callbacks
3. Map dependencies between them
4. Plan hook boundaries

### Phase 2: Implementation
1. Create/update hooks in `src/app/hooks/`
2. Each hook should be focused on a single concern
3. Export all necessary state and callbacks
4. Compose hooks in `App.tsx`

### Phase 3: Verification
1. Run `npm run build` - must pass with zero errors
2. Run the app and verify each checklist item below
3. Test edge cases documented in this spec

### Phase 4: Completion
1. Ensure all verification items pass
2. Commit with descriptive message
3. Push to remote main branch

---

## Critical Implementation Notes

### Ref Synchronization Pattern
The original uses refs alongside state for stable callback access. This pattern MUST be preserved:
```
settingsRef, messagesRef, isSendingRef, selectedLanguagePairRef,
isLoadingSuggestionsRef, speechIsSpeakingRef, isUserActiveRef, prevIsListeningRef
```
Each ref must sync with its corresponding state via `useEffect`.

### Timing Constants
- `STT_STABLE_NO_TEXT_MS = 4000` - Auto-send after 4s silence
- `MAX_VISIBLE_MESSAGES_DEFAULT = 50` - Default visible messages
- User activity timeout: 3000ms
- Language selection auto-confirm: 5000ms (check at 4500ms)
- STT restart delay: 100-250ms after stopping

### Size Limits
- `INLINE_CAP_AUDIO` - Max audio data URL size for caching
- `MAX_MEDIA_TO_KEEP` - Max media items in API context

---

## Feature Specifications

### 1. CORE CHAT FUNCTIONALITY

#### 1.1 Message Management
- `addMessage`: Creates message with UUID + timestamp, returns ID
- `updateMessage`: Updates by ID, refreshes timestamp
- `handleDeleteMessage`: Removes by ID
- `thinking` state on assistant messages during generation
- `isGeneratingImage` state during image generation

#### 1.2 Chat History Persistence
- Per-language-pair storage in IndexedDB
- Auto-save on `messages` change (skip if `isLoadingHistory`)
- Load on `selectedLanguagePairId` change
- Check localStorage backup if IndexedDB empty
- Clean interrupted states on load (convert to error messages)

#### 1.3 History Bookmark System
- `historyBookmarkMessageId` in settings
- Auto-set when messages exceed `maxVisibleMessages + 2`
- Must be on assistant (non-thinking) message
- `trimHistoryByBookmark` / `getHistoryRespectingBookmark` for API context
- `resolveBookmarkContextSummary` gets context from `chatSummary`

#### 1.4 Max Visible Messages
- Default: 50, configurable 1-100
- Stored in settings, affects display and API context

#### 1.5 Grounding Chunks
- `latestGroundingChunks` state for Google Search metadata
- Display controlled by `enableGoogleSearch` setting

---

### 2. TEXT-TO-SPEECH (TTS)

#### 2.1 Provider Toggle
- `settings.tts.provider`: 'browser' | 'gemini'
- `toggleTtsProvider` callback

#### 2.2 Speak Native Toggle
- `settings.tts.speakNative` boolean
- Heuristic: if no Spanish chars + has English + native is English, might be native-only
- Suggestion clicks should pass suggestion context to TTS cache (messageId + suggestionIndex when available)

#### 2.3 TTS Audio Caching
- Per-message `ttsAudioCache` array
- Cache key: `computeTtsCacheKey(text, lang, provider, voiceName)`
- `upsertMessageTtsCache` for message cache
- `upsertSuggestionTtsCache` for suggestion cache
- `prepareSpeechPartsWithCache` prepares parts with cache lookup

#### 2.4 Auto-Speak
- `speakMessage` called after assistant response
- Respects `speakNative` setting for translation pairs

#### 2.5 Speaking Text Tracking
- `speakingUtteranceText` from useBrowserSpeech for UI highlight

---

### 3. SPEECH-TO-TEXT (STT)

#### 3.1 Provider Toggle
- `settings.stt.provider`: 'browser' | 'gemini'
- `toggleSttProvider` callback

#### 3.2 Language Selection
- `settings.stt.language` code
- `handleSttLanguageChange` - stop, update, restart with delay
- Suggestion mode toggle clears transcript if STT language changes while inactive
- Auto-switch to native in suggestion mode

#### 3.3 Auto-Send on Silence
- Timer starts when transcript >= 2 chars
- 4 second stability check
- `stripBracketedContent` before checking
- In suggestion mode: `handleCreateSuggestion` instead
- Clear timer on: STT disable, user activity, transcript change

#### 3.4 STT Interrupt/Resume
- Stop on send, track with `sttInterruptedBySendRef`
- Resume via `onSpeechQueueCompleted` callback
- Resume on send error
- `restoreSttAfterLiveSession` for live mode

#### 3.5 Recorded Utterance
- `onRecordedUtteranceReady` callback from useBrowserSpeech
- `claimRecordedUtterance` on send
- Attach to user message if within `INLINE_CAP_AUDIO`

#### 3.6 Master Toggle
- `sttMasterToggle` callback
- Clears transcript and auto-send timer

---

### 4. CAMERA / VISUAL CONTEXT

#### 4.1 Camera Device Management
- `fetchAvailableCameras` on mount and devicechange
- `availableCameras` state with deviceId, label, facingMode
- `getFacingModeFromLabel` for facing detection
- `currentCameraFacingMode` state
- Update facing mode when selected camera changes
- Re-fetch when enabling camera features

#### 4.2 Send with Snapshot
- `settings.sendWithSnapshotEnabled` toggle
- Capture on send if no image attached and not IMAGE_GEN_CAMERA

#### 4.3 Visual Context Stream
- `visualContextVideoRef` and `visualContextStreamRef`
- Start when `useVisualContext` or `sendWithSnapshotEnabled`
- Stop when neither enabled
- `setVisualContextCameraError` for user feedback

#### 4.4 Snapshot Capture
- `captureSnapshot(isForReengagement)` returns base64 + mimeType
- 3 second timeout for video ready
- Reuse live stream if available

#### 4.5 Image Attachment
- `attachedImageBase64`, `attachedImageMimeType` state
- `handleSetAttachedImage` callback
- Video files: extract keyframe as separate message first
- Clear after successful send

---

### 5. LANGUAGE PAIR MANAGEMENT

#### 5.1 Language Pair Selection
- `isLanguageSelectionOpen` modal state
- `tempNativeLangCode`, `tempTargetLangCode` temp selection
- Same language cannot be both (clear other on conflict)

#### 5.2 Auto-Confirm
- 5 second timer when both selected
- Check 4500ms idle threshold before confirm
- `lastInteractionRef` tracks last user action

#### 5.3 Language-Specific Prompts
- `currentSystemPromptText` from pair's `baseSystemPrompt`
- `currentReplySuggestionsPromptText` from pair's `baseReplySuggestionsPrompt`
- Update on `selectedLanguagePair` change

#### 5.4 Translation Parsing
- `parseGeminiResponse` extracts `[LANG]` prefixed lines
- Returns array of `{ spanish, english }` (target/native)
- Fallback to full text if no structure

---

### 6. SETTINGS MANAGEMENT

#### 6.1 Persistence
- `getAppSettingsDB` / `setAppSettingsDB` for IndexedDB
- `loadFromLocalStorage` fallback with deep merge
- Fill missing keys from `initialSettings`
- Validate `selectedLanguagePairId` against available pairs

#### 6.2 Settings Sync
- `handleSettingsChange` updates state and persists
- `settingsRef.current` always synced via useEffect

---

### 7. GEMINI LIVE SESSION

#### 7.1 State Machine
- `liveSessionState`: 'idle' | 'connecting' | 'active' | 'error'
- `liveUiTokenRef` for busy state during active
- `liveSessionError` for error display

#### 7.2 Camera for Live
- Reuse `visualContextStreamRef` or `liveVideoStream`
- Create new if needed, track with `liveSessionCaptureRef`
- `releaseLiveSessionCapture` on end

#### 7.3 Turn Processing
- `handleLiveTurnComplete(userText, modelText, userAudioPcm, modelAudioPcm)`
- Capture snapshot for user message
- Save audio as WAV: user 16kHz, model 24kHz
- Split model audio by 400ms silence gaps
- Background image generation
- Fetch suggestions

#### 7.4 System Instruction
- `generateLiveSystemInstruction` includes last 10 messages + global profile
- Live turn should refresh history for suggestions after TTS cache updates

#### 7.5 STT Handling
- Disable STT on start, save state in `liveSessionShouldRestoreSttRef`
- Restore on session end

---

### 8. SMART RE-ENGAGEMENT

#### 8.1 Timer
- `settings.smartReengagement.thresholdSeconds` (default 45)
- Adjustable from suggestion response `reengagementSeconds`

#### 8.2 Phases
- `reengagementPhase`: waiting -> watching -> countdown -> engaging
- Cancel on: user activity, speaking, sending, listening, user active

#### 8.3 Visual Context Re-engagement
- `settings.smartReengagement.useVisualContext` toggle
- Capture image if enabled and stream active
- Fallback to conversational

#### 8.4 Message Types
- `triggerReengagementSequence` handles both types
- `image-reengagement`: with camera image
- `conversational-reengagement`: text only

---

### 9. IMAGE GENERATION

#### 9.1 Mode Toggle
- `settings.imageGenerationModeEnabled`
- Disabling switches from IMAGE_GEN_CAMERA to first physical camera

#### 9.2 Focused Mode
- `settings.imageFocusedModeEnabled`
- `handleToggleImageFocusedMode` uses View Transitions API
- `transitioningImageId` for animation tracking

#### 9.3 User Image Generation
- When `selectedCameraId === IMAGE_GEN_CAMERA_ID`
- Generate with `IMAGE_GEN_USER_PROMPT_TEMPLATE`
- 3 retry attempts

#### 9.4 Assistant Image Generation
- Background after main response
- 3 retries with 1500ms delay
- Uses `maestroAvatarUriRef` for context

#### 9.5 Load Time Estimation
- `imageLoadDurations` array of past durations
- `calculateEstimatedImageLoadTime` returns average or 15s default

#### 9.6 Media Optimization
- `processMediaForUpload` for low-res persistence
- `uploadMediaToFiles` for full-res API access
- Track `llmImageUrl`/`llmFileUri` separately

---

### 10. SUGGESTION MODE

#### 10.1 Mode Toggle
- `settings.isSuggestionMode` boolean
- `handleToggleSuggestionMode(forceState?)` callback
- STT language switches to native when enabled

#### 10.2 Reply Suggestions
- `replySuggestions` state array
- `isLoadingSuggestions` loading state
- `fetchAndSetReplySuggestions` fetches from API
- Triggered after TTS completes (`wasSpeaking` -> `!isSpeaking`)
- Cache in message's `replySuggestions` field
- 2 retries on failure

#### 10.3 Suggestion Creation
- `handleCreateSuggestion(textToTranslate)`
- Detect direction from STT language vs target
- Deduplication before adding
- Exit suggestion mode after creation
- `isCreatingSuggestion` loading state

#### 10.4 Suggestion TTS Cache
- Per-suggestion `ttsAudioCache` array
- Sync local state with message storage

#### 10.5 Suggestion Interaction
- `handleSuggestionInteraction(suggestion, 'target' | 'native')`
- Speak if not already speaking
- Trigger user activity

---

### 11. DATA BACKUP/RESTORE

#### 11.1 Export
- `handleSaveAllChats(options?)`
- Flush current chat first
- Version 7 format with: chats, metas, globalProfile, assets

#### 11.2 Import
- `handleLoadAllChats(file)`
- Auto-backup before import
- Handle v7 and legacy formats
- Merge loading GIFs with manifest
- Import maestro profile (clear URI)

#### 11.3 Maestro Avatar
- Load from DB or fetch default blob
- `maestro-avatar-updated` custom event listener for live updates
- `maestroAvatarUriRef`, `maestroAvatarMimeTypeRef`

#### 11.4 Loading GIFs
- `loadingGifs` state
- Merge with `/gifs/manifest.json` on import

---

### 12. UI STATE MANAGEMENT

#### 12.1 UI Busy Tokens
- `uiBusyTokensRef` Set of tokens
- `addUiBusyToken`, `removeUiBusyToken`, `clearUiBusyTokens`
- `recomputeUiBusyState` extracts tags and counts
- Re-engagement tokens excluded from count (prefix check)
- `uiBusyTaskTags` for UI display

#### 12.2 Hold Token
- `handleToggleHold` for manual pause
- `holdUiTokenRef` tracks active hold token

#### 12.3 Maestro Activity Stage
- `maestroActivityStage`: idle | listening | typing | speaking | observing_low/medium/high
- Yields when `externalUiTaskCount > 0`
- Maps re-engagement phases to observation levels

#### 12.4 User Activity Tracking
- `isUserActive` state with 3 second timeout
- `handleUserInputActivity` sets active, clears auto-send timer
- Cancels re-engagement

#### 12.5 Send Preparation Status
- `sendPrep` state: { active, label, done?, total?, etaMs? }
- Labels: preparingMedia, uploadingMedia, finalizing

#### 12.6 Splash Screen
- Fade out on mount with CSS transition
- Remove element on transitionend

---

### 13. API / NETWORK

#### 13.1 Response Generation
- `generateGeminiResponse` with history and system instruction
- `composeMaestroSystemInstruction` adds global profile
- Google Search grounding via `enableGoogleSearch`

#### 13.2 URI Verification
- `ensureUrisForHistoryForSend` verifies/re-uploads media
- `checkFileStatuses` detects deleted files
- Progress callback for UI
- `sanitizeHistoryWithVerifiedUris` for API call
- When uploading current media, reuse existing `llmFileUri` if already present
- Ensure `llmFileUri` corrections are applied before deriving history

#### 13.3 Global Profile
- `getGlobalProfileDB` / `setGlobalProfileDB`
- Merge chat summary via LLM (max 1200 chars merge, 10000 storage)
- Include in system instruction

---

### 14. INTERNATIONALIZATION

#### 14.1 UI Translation
- `useTranslations(nativeLangCode)` hook
- `t(key, replacements?)` function
- Fallback: native lang -> browser lang -> English

#### 14.2 Document Title
- Set `document.title` on language change via `t(APP_TITLE_KEY)`

---

### 15. MISCELLANEOUS

#### 15.1 Microphone Detection
- `microphoneApiAvailable` memo with SSR guard

#### 15.2 Video Keyframe
- `createKeyframeFromVideoDataUrl` extracts first frame
- Creates separate user message with keyframe

#### 15.3 Audio Conversion
- `pcmToWav(pcm, sampleRate)` for audio persistence
- `splitPcmBySilence(pcm, sampleRate, gapMs)` for caching

#### 15.4 Utility Functions
- `stripBracketedContent` removes `[bracketed]` text
- `uniq` for array deduplication
- `isRealChatMessage` filters user/assistant messages
- `getPrimaryCode` / `getShortLangCodeForPrompt` for language codes

---

## Verification Checklist

Run through EVERY item. A single failure means the refactoring is incomplete.

### Build Verification
- [ ] `npm run build` succeeds with zero TypeScript errors
- [ ] `npm run dev` starts without runtime errors
- [ ] App renders without crash

### Core Chat
- [ ] Can type and send text messages
- [ ] Can delete messages via UI
- [ ] Messages persist after page refresh
- [ ] Changing language pair loads correct history
- [ ] Thinking indicator shows during AI response
- [ ] Interrupted states show error after reload

### Speech (TTS)
- [ ] Assistant messages auto-speak after generation
- [ ] speakNative toggle affects what is spoken
- [ ] TTS provider toggle works (browser/gemini)
- [ ] Audio caching works (replay uses cache)
- [ ] Speaking text highlights in UI

### Speech (STT)
- [ ] STT toggle enables/disables recognition
- [ ] Transcript appears while speaking
- [ ] Auto-send after 4s silence works
- [ ] STT pauses during send
- [ ] STT resumes after TTS completes
- [ ] Recorded audio attaches to messages
- [ ] STT provider toggle works
- [ ] STT language selection works

### Camera
- [ ] Camera dropdown shows available devices
- [ ] Send with snapshot captures on send
- [ ] Visual context stream starts when enabled
- [ ] Manual image attachment works
- [ ] Video files create keyframe message

### Language
- [ ] Can open language selector
- [ ] Can select native then target
- [ ] Same language blocked for both
- [ ] Auto-confirm after 5s idle
- [ ] System prompt updates on pair change
- [ ] Translation parsing shows target/native

### Re-engagement
- [ ] Triggers after idle threshold
- [ ] Cancels on user activity
- [ ] Visual re-engagement includes image
- [ ] Activity stage reflects phase

### Image Generation
- [ ] Toggle enables/disables feature
- [ ] Assistant messages show generated images
- [ ] IMAGE_GEN_CAMERA generates user images
- [ ] Focused mode toggles size
- [ ] View transition animates toggle

### Suggestions
- [ ] Suggestions appear after TTS completes
- [ ] Tap suggestion speaks it
- [ ] Suggestion mode switches STT to native
- [ ] User can create suggestions via speech
- [ ] Suggestions persist in message

### Live Session
- [ ] Can start live session
- [ ] Turns add messages to chat
- [ ] User snapshots captured
- [ ] Audio saved with messages
- [ ] Can stop live session
- [ ] STT restores after session
- [ ] Image generation works in live

### Data
- [ ] Export downloads JSON with all data
- [ ] Import loads chats correctly
- [ ] Settings persist across refresh
- [ ] Global profile updates from summaries

### UI State
- [ ] Busy tokens block conflicting ops
- [ ] Hold button pauses activity
- [ ] Activity stage animates correctly
- [ ] Send prep shows progress
- [ ] Debug logs toggle works

---

## Hook Architecture Suggestion

The hooks can be organized however makes sense, but here's a suggested structure:

```
src/app/hooks/
  index.ts              - Re-exports
  useAppSettings.ts     - Settings state and persistence
  useChatStore.ts       - Messages, history, bookmarks
  useHardware.ts        - Camera, microphone detection
  useSpeechController.ts - TTS/STT orchestration
  useLiveSession.ts     - Gemini Live session management
  useDataBackup.ts      - Export/import functionality
  useUiBusyState.ts     - Busy tokens, activity stage
  useMaestroController.ts - Main orchestrator hook
  useTranslations.ts    - i18n
```

The final `App.tsx` should:
1. Call `useMaestroController()` which composes all hooks
2. Render only the UI components
3. Pass down props from the controller

---

## Final Notes

1. **No new features** - This is purely reorganization
2. **Preserve all edge cases** - The original has subtle timing behaviors
3. **Test thoroughly** - Especially auto-send, STT resume, re-engagement
4. **Ref sync is critical** - Async callbacks must access current state via refs
5. **When in doubt, check the original** - `originalOldApp.tsx.bak` is the source of truth
