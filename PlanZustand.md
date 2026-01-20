Here is a task for you. All 100% of the intended functionality from the original (~5000 lines added and removed: git diff baseline_repo/main) should work in the new, providing users the same experience while improving maintainability and code collaboration accross several files at the same time and making the future work of the devs easier. These goals are not met as users complain things dont work as before ( Build
 Render Start 
Build
 Render End 
Error
 "Uncaught Error: Minified React error #185; visit https://react.dev/errors/185 for the full message or use the non-minified dev environment for full errors and additional helpful warnings." Minified React error #185
In the minified production build of React, we avoid sending down full error messages in order to reduce the number of bytes sent over the wire.

We highly recommend using the development build locally when debugging your app since it tracks additional debug info and provides helpful warnings about potential problems in your apps, but if you encounter an exception while using the production build, this page will reassemble the original error message.

The full text of the error you just encountered is:

Maximum update depth exceeded. This can happen when a component repeatedly calls setState inside componentWillUpdate or componentDidUpdate. React limits the number of nested updates to prevent infinite loops.) (testing not with tests but in real life by user test group). Implement the full release version and then notify me when done. You can use git to check diff against  baseline_repo/main. The app should have 100% of the intended previous functionality of main when this zustand change is Done.

"C:\Users\ronit\maestrolanguagetutor_gemini_edition\maestrotutor-geminiedition_66_vs\PlanZustand.md"

Plan: Zustand + feature boundaries + orchestration cleanup (full app)
 Goals
- Make the app maintainable for multi-dev work with clear feature ownership.
- Shrink `src/app/App.tsx` into a true composition root.
- Remove ref-threading across hooks by centralizing state/actions in Zustand.
- Establish public APIs for features to prevent cross-feature coupling.
- Keep behavior identical while improving structure.
 Non-goals
- No UI redesign.
- No behavior changes to chat, STT/TTS, live sessions, or reengagement.
- No backend/API changes.
 Decisions (locked)
- Global state is managed with Zustand.
- Each feature owns a slice and exposes public APIs via `index.ts`.
- Non-serializable state (MediaStream) may live in store but is never persisted.
- Persistence stays in existing services; store actions call them.
 Store architecture (new)
- Location: `src/store/`
- Files:
  - `src/store/maestroStore.ts` (root store creation)
  - `src/store/slices/settingsSlice.ts`
  - `src/store/slices/chatSlice.ts`
  - `src/store/slices/speechSlice.ts`
  - `src/store/slices/hardwareSlice.ts`
  - `src/store/slices/reengagementSlice.ts`
  - `src/store/slices/liveSessionSlice.ts`
  - `src/store/slices/uiSlice.ts`
  - `src/store/slices/diagnosticsSlice.ts`
  - `src/store/selectors/*.ts` (optional, for common selectors)
- Middleware:
  - `subscribeWithSelector` for fine-grained subscriptions
  - `devtools` in dev only (no prod overhead)
- Persistence:
  - Settings: persisted via `getAppSettingsDB` and `setAppSettingsDB`
  - Chat history + meta: persisted via `chatHistory` services
  - No persistence for streams, timers, or transient UI
 Slice ownership and responsibility
- settingsSlice: `AppSettings`, language pairs, STT/TTS config, bookmarks, maxVisibleMessages.
- chatSlice: messages, replySuggestions, loading state, TTS caches, chat meta.
- speechSlice: isListening, transcript, isSpeaking, speech errors, queue state.
- hardwareSlice: camera list, live streams, snapshot errors, camera errors.
- reengagementSlice: phase, timers, schedule/cancel actions.
- liveSessionSlice: live session state, errors, session lifecycle.
- uiSlice: UI-only state that crosses components (e.g. topbar open, debug panel).
- diagnosticsSlice: debug log visibility and any app-wide diagnostics state.
# Inventory and baseline
Initial info to start with (not verified, received from llm):
Inventory (data slices)
- settingsSlice: settings (selectedLanguagePairId, selectedCameraId, sendWithSnapshotEnabled, tts/stt config, smartReengagement, enableGoogleSearch, imageGenerationModeEnabled, imageFocusedModeEnabled, isSuggestionMode, historyBookmarkMessageId, maxVisibleMessages), languagePairs, selectedLanguagePair, isSettingsLoaded; derive currentSystemPromptText/currentReplySuggestionsPromptText from selected pair.
- chatSlice: messages, isLoadingHistory, replySuggestions, isLoadingSuggestions, lastFetchedSuggestionsFor, isSending, sendPrep, isCreatingSuggestion, latestGroundingChunks, imageLoadDurations, attachedImageBase64, attachedImageMimeType.
- speechSlice: isListening, transcript, sttError, isSpeaking, speakingUtteranceText, isSpeechRecognitionSupported, isSpeechSynthesisSupported, recordedUtterancePending, pendingRecordedAudioMessageId, sttInterruptedBySend, queue state.
- hardwareSlice: availableCameras, currentCameraFacingMode, liveVideoStream, visualContextStream, visualContextCameraError, snapshotUserError, microphoneApiAvailable, isCurrentlyPerformingVisualContextCapture.
- liveSessionSlice: liveSessionState, liveSessionError.
- reengagementSlice: reengagementPhase, isUserActive, schedule/cancel actions, timers/tokens.
Inventory (UI/diagnostics)
- uiSlice (language selector): isLanguageSelectionOpen, tempNativeLangCode, tempTargetLangCode, lastInteraction timestamp for auto-confirm.
- uiSlice (shell + activity): isTopbarOpen, uiBusyTaskTags, externalUiTaskCount, maestroActivityStage.
- uiSlice (assets/visual): loadingGifs, transitioningImageId, maestroAvatarUri, maestroAvatarMimeType.
- diagnosticsSlice: showDebugLogs.
Local-only (keep in hooks/components)
- UI/DOM refs: bubbleWrapperRefs, visualContextVideoRef.
- Orchestration timers/refs: autoSendTimerRef, autoSendSnapshotRef, prevIsListeningRef, wasSpeakingRef, userActivityTimerRef.
- Action wiring refs (replace with store getState/actions): handleSendMessageInternalRef, scheduleReengagementRef, cancelReengagementRef, handleToggleSuggestionModeRef, sharedIsSendingRef.
- Hook-internal transient refs: sendWithFileUploadInProgressRef, sendPrepRef, liveSessionCaptureRef, liveSessionShouldRestoreSttRef, beginCountdownRef, reengagementDeadlineRef.
Dependency graph (feature -> feature imports)
- chat -> speech: LiveSessionState in src/features/chat/components/ChatInterface.tsx; LiveSessionState + SttLanguageSelector in src/features/chat/components/InputArea.tsx.
- chat -> session: LanguageSelectorGlobe + globalProfile service in src/features/chat/components/InputArea.tsx.
- core/api -> diagnostics: debugLogService in src/api/gemini.ts (cross-layer; would move behind diagnostics public API).
Note: no src/features/*/index.ts yet (glob found none), so all cross-feature imports currently reach internal paths.
Persistence touchpoints
- settings DB: getAppSettingsDB/setAppSettingsDB in src/app/hooks/useAppSettings.ts, src/app/App.tsx, src/app/hooks/useChatStore.ts, src/app/hooks/useMaestroController.ts, src/app/hooks/useLiveSession.ts.
- chat history/meta DB: getChatHistoryDB, safeSaveChatHistoryDB, getChatMetaDB, setChatMetaDB, getAllChatHistoriesDB, getAllChatMetasDB, clearAndSaveAllHistoriesDB in src/app/hooks/useChatStore.ts, src/app/App.tsx, src/app/hooks/useMaestroController.ts, src/app/hooks/useLiveSession.ts, src/app/hooks/useDataBackup.ts.
- global profile DB: getGlobalProfileDB/setGlobalProfileDB in src/app/hooks/useMaestroController.ts, src/app/hooks/useLiveSession.ts, src/app/hooks/useDataBackup.ts, src/features/chat/components/InputArea.tsx.
- other persistence: assets DB (loadingGifs, maestroProfile) in src/app/App.tsx, src/app/hooks/useDataBackup.ts.

Work items:
- Map all state currently held in `src/app/App.tsx` and `src/app/hooks/*`.
- Identify cross-feature imports (features reading each other’s internals).
- List all persistence touchpoints (`chatHistory`, `settings`, `globalProfile`).
Deliverables:
- Inventory list (state -> owner slice).
- Dependency graph (feature -> feature imports).
Acceptance:
- Every state variable has an assigned slice or confirmed local-only home.
# Zustand foundation
Note: MediaStreams in Store: Noted that non-serializable state is allowed. 
Ensure you mark these fields as transient or exclude them explicitly
 if you add any store persistence middleware (like persist from zustand/middleware) later, 
otherwise, it will crash trying to stringify a MediaStream.
Work items:
- Add Zustand dependency to `package.json`.
- Create store structure in `src/store/`.
- Add base `useMaestroStore` hook and type-safe slices.
Deliverables:
- Store created with empty slices and typed actions.
Acceptance:
- Build passes, store imports compile, no behavior changes.
# Settings + UI state
Scope:
- `src/app/hooks/useAppSettings.ts`
- `src/app/App.tsx` UI state that is app-wide (language selector, debug panel)
Work items:
- Move settings state + persistence into `settingsSlice`.
- Replace `useAppSettings` consumers with selectors/actions.
- Create `uiSlice` for cross-component UI state.
- Keep component-local state local (transitions, minor UI).
Acceptance:
- Settings load/save unchanged.
- `App.tsx` no longer manages settings refs.
# Chat store migration
Scope:
- `src/app/hooks/useChatStore.ts`
- `src/features/chat/services/chatHistory.ts`
- `src/features/chat/components/*`
Work items:
- Move messages, replySuggestions, loading state into `chatSlice`.
- Move `messagesRef` usage to store selectors or `getState()`.
- Keep persistence in `chatHistory` services but trigger from slice actions.
- Ensure `replySuggestions` cache updates remain in sync.
Acceptance:
- Chat history load/save unchanged.
- Reply suggestions and caches behave the same.
- No ref-threading for chat state.
# Speech + hardware slices
Scope:
- `src/app/hooks/useSpeechController.ts`
- `src/app/hooks/useHardware.ts`
- `src/features/speech/*`
- `src/features/vision/*`
Work items:
- Move speech state (isListening, isSpeaking, transcript, errors) to `speechSlice`.
- Move hardware state (camera list, streams, errors) to `hardwareSlice`.
- Ensure non-serializable stream objects are not persisted.
- Replace cross-hook refs with store selectors.
Acceptance:
- STT/TTS behavior unchanged.
- Camera and snapshot flows unchanged.
- No manual ref sharing between hooks.
# Maestro + Live session + Reengagement
Scope:
- `src/app/hooks/useMaestroController.ts`
- `src/app/hooks/useLiveSession.ts`
- `src/features/session/hooks/useSmartReengagement.ts`
Work items:
- Move `isSending`, activity stage, sendPrep to relevant slices.
- Reduce `useMaestroController` config to grouped store access.
- Move reengagement state to `reengagementSlice`.
- Move live session state to `liveSessionSlice`.
- Replace callback refs with store actions and selectors.
Acceptance:
- `useMaestroController` config no longer passes 30+ params.
- Reengagement triggers and scheduling unchanged.
- Live session lifecycle unchanged.
# Feature boundaries + public APIs
Scope:
- `src/features/**`
Work items:
- Create `src/features/<domain>/index.ts` that exports public API.
- Move internals under `internal/` or document as private.
- Update imports to use only feature indexes.
- Add ESLint rule to prevent cross-feature internal imports.
Acceptance:
- No external imports from `src/features/**/components` or `hooks` directly.
- All cross-feature usage goes through index files.
# App shell cleanup
Scope:
- `src/app/App.tsx`
- `src/app/hooks/*`
Work items:
- Move orchestration effects into dedicated hooks:
  - auto-send on silence
  - auto-fetch suggestions on TTS end
  - STT auto-restart in suggestion mode
  - reengagement scheduling on idle
  - asset/avatar bootstrap
- App becomes composition + prop wiring only.
Acceptance:
- `src/app/App.tsx` <= 300 lines.
- No business logic left in the component.

 Definition of Done
- Zustand is the single source of shared state.
- `src/app/App.tsx` is a pure composition root.
- No cross-feature imports of internals.
- All persistence is triggered by store actions.

 Risks and mitigations
- Risk: state migration causes regressions.
  - Mitigation: migrate slice-by-slice and keep behavior‑parity tests.
- Risk: store re-render performance.
  - Mitigation: use selectors + shallow compare; avoid whole-slice reads.
- Risk: non-serializable objects in store.
  - Mitigation: avoid persistence and document in slice.

Before Done; Only finish when Done. Will be submitted to real life testing when Done.
When Done; If something was impossible to figure out you can ask follow up questions and write a summary or write a summary of plan full success if no questions. Will be submitted to real life user testing and release.
