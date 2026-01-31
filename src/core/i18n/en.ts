// When adding new translations, please only update the en.ts file and index.ts if nessesary. Other language files will be updated by language experts of those languages.
// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export const enTranslations: Record<string, string> = {
  // Language selector (used in SttLanguageSelector)
  "sttLang.selectLanguage": "Set speech recognition to {language}",
  
  // Header
  "header.targetLanguageTitle": "Current Target Language: {language}",
  
  // Start page (used)
  "startPage.clickToStart": "Click the plane",
  "startPage.saveChats": "Save All Chats",
  "startPage.loadChats": "Load Chats",
  "startPage.maestroAvatar": "Maestro avatar",
  "startPage.addMaestroAvatar": "Add Maestro avatar",
  "startPage.loadSuccess": "Successfully loaded and replaced {count} chat sessions!",
  "startPage.loadError": "Error loading chats. The file might be corrupted or in the wrong format.",
  "startPage.noChatsToSave": "There are no chat histories to save.",
  "startPage.saveError": "Error saving chats. Check the console for more details.",
  
  // General
  "general.clear": "Clear",
  "general.error": "Sorry, I encountered an error.",

  // API key gate
  "apiKeyGate.title": "Connect your Gemini API key",
  "apiKeyGate.subtitle": "This app runs fully on your device. Your key never touches our servers.",
  "apiKeyGate.privacyPolicy": "Privacy Policy",
  "apiKeyGate.stepsTitle": "Two quick steps:",
  "apiKeyGate.stepOne": "Open Google AI Studio and create an API key.",
  "apiKeyGate.stepTwo": "Paste the key below and tap Save.",
  "apiKeyGate.openAiStudio": "Open Google AI Studio",
  "apiKeyGate.keyLabel": "Gemini API key",
  "apiKeyGate.placeholder": "Paste your API key here",
  "apiKeyGate.show": "Show",
  "apiKeyGate.hide": "Hide",
  "apiKeyGate.currentKeySaved": "Current key saved {maskedKey}",
  "apiKeyGate.clearSavedKey": "Clear saved key",
  "apiKeyGate.cancel": "Cancel",
  "apiKeyGate.saving": "Saving...",
  "apiKeyGate.saveKey": "Save key",
  "apiKeyGate.close": "Close",
  
  // Chat - general
  "chat.thinking": "Thinking...",
  "chat.loadingHistory": "Loading chat history...",
  "chat.loadingSuggestions": "Loading suggestions...",
  "chat.suggestionsAriaLabel": "Reply suggestions",
  "chat.attachImageFromFile": "Attach file",
  "chat.removeAttachedImage": "Remove attached file",
  "chat.sendMessage": "Send message",
  "chat.messageInputAriaLabel": "Message input",
  "chat.retrievedFromWeb": "Retrieved from the web:",
  "chat.videoNotSupported": "Your browser does not support the video tag.",
  "chat.audioNotSupported": "Your browser does not support the audio tag.",
  "chat.fileAttachment": "File Attachment",
  "chat.imageGenError": "Image Gen Error",
  "chat.generatingImageLoadingSlow": "Taking a bit longer...",
  "chat.stopSpeaking": "Stop speaking",
  "chat.speakThisLine": "Speak this line",
  "chat.languageSelector.openGlobe": "Change languages",
  "chat.maestroTranscriptScrollwheel": "Maestro transcript scroll view",
  
  // Chat - mic/STT
  "chat.mic.listening": "STT Active: Listening...",
  "chat.mic.enableStt": "Enable STT",
  "chat.mic.disableStt": "Stop STT",
  "chat.mic.recordingAudioNote": "Recording audio...",
  
  // Chat - placeholders
  "chat.placeholder.normal.listening": "Listening in {language}...",
  "chat.placeholder.normal.sttActive": "Speak in {language} or type...",
  "chat.placeholder.normal.sttInactive": "Type or tap the mic to speak in {language}...",
  "chat.placeholder.suggestion.listening": "Speak {language} to translate...",
  "chat.placeholder.suggestion.sttActive": "Speak or type in {language} to translate...",
  "chat.placeholder.suggestion.sttInactive": "Type in {language} to translate...",
  
  // Chat - camera
  "chat.camera.turnOn": "Activate camera preview",
  "chat.camera.turnOff": "Deactivate camera preview",
  "chat.camera.imageGenCameraLabel": "Image Generation",
  "chat.camera.captureOrRecord": "Tap for photo, long press for video",
  "chat.camera.stopRecording": "Stop Recording",
  "chat.bookIcon.toggleImageGen": "Toggle Image Generation Mode",
  
  // Chat - image
  "chat.imagePreview.alt": "Preview",
  "chat.image.dragToEnlarge": "Drag corner to enlarge",
  "chat.image.dragToShrink": "Drag corner to shrink",
  "chat.annotateImage": "Annotate Image",
  "chat.annotateVideoFrame": "Annotate current frame",
  
  // Chat - annotate modal
  "chat.annotateModal.editingPreviewAlt": "Image to be annotated",
  "chat.annotateModal.cancel": "Cancel",
  "chat.annotateModal.saveAndAttach": "Save & Attach",
  "chat.annotateModal.undo": "Undo",
  
  // Chat - suggestions
  "chat.suggestion.speak": "Speak: \"{suggestion}\"",
  "chat.suggestion.ariaLabel": "Speak suggestion: {suggestion}",
  "chat.suggestion.toggleCreateMode": "Toggle suggestion creation mode",
  "chat.suggestion.createAction": "Create Suggestion",
  "chat.suggestion.creating": "Creating suggestion...",
  
  // Chat - maestro status (used via CollapsedMaestroStatus)
  "chat.maestro.idle": "Maestro is idle",
  "chat.maestro.title.idle": "Maestro is currently idle.",
  "chat.maestro.resting": "Maestro is resting...",
  "chat.maestro.observing": "Maestro is observing...",
  "chat.maestro.aboutToEngage": "Maestro is about to engage...",
  "chat.maestro.title.resting": "Maestro is idle, plenty of time before re-engagement.",
  "chat.maestro.title.observing": "Maestro is observing, some time before re-engagement.",
  "chat.maestro.title.aboutToEngage": "Maestro is about to re-engage soon.",
  "chat.maestro.typing": "Maestro is typing...",
  "chat.maestro.title.typing": "Maestro is preparing a response.",
  "chat.maestro.speaking": "Maestro is speaking",
  "chat.maestro.title.speaking": "Maestro is currently speaking.",
  "chat.maestro.listening": "Listening...",
  "chat.maestro.title.listening": "Maestro is waiting for your input or speech.",
  "chat.maestro.holding": "Maestro is holding",
  "chat.maestro.title.holding": "Maestro is holding (re-engagement paused)",
  
  // Chat - bookmark (used)
  "chat.bookmark.hiddenHeaderAria": "Hidden above messages",
  "chat.bookmark.isHere": "Bookmark is here",
  "chat.bookmark.setHere": "Set bookmark here",
  "chat.bookmark.actionsRegionAria": "Bookmark actions",
  "chat.bookmark.actionsToggleTitle": "Bookmark options",
  "chat.bookmark.decrementAria": "Show one less",
  "chat.bookmark.decrementTitle": "Less",
  "chat.bookmark.incrementAria": "Show one more",
  "chat.bookmark.incrementTitle": "More",
  "chat.bookmark.hiddenBelowHeaderAria": "Hidden messages below",
  
  // Chat - send preparation
  "chat.sendPrep.optimizingVideo": "Optimizing video...",
  "chat.sendPrep.optimizingImage": "Optimizing image...",
  "chat.sendPrep.preparingMedia": "Preparing media...",
  "chat.sendPrep.uploadingMedia": "Uploading media...",
  "chat.sendPrep.finalizing": "Finalizing...",
  
  // Chat - header activity tokens (used via activityTokens.ts)
  "chat.header.annotating": "Annotating",
  "chat.header.recordingAudio": "Recording audio",
  "chat.header.recordingVideo": "Recording video",
  "chat.header.savePopup": "Saving...",
  "chat.header.loadPopup": "Loading...",
  "chat.header.maestroAvatar": "Updating Maestro avatar",
  "chat.header.watchingVideo": "Watching video",
  "chat.header.viewingAbove": "Viewing messages above",
  "chat.header.liveSession": "Live session",
  
  // Chat - live session
  "chat.liveSession.stop": "Stop Live",
  "chat.liveSession.retry": "Retry Live",
  "chat.liveSession.start": "Start Live",
  "chat.liveSession.liveBadge": "Live",
  "chat.liveSession.connecting": "Connecting",
  
  // Chat - errors
  "chat.error.sttError": "STT Error: {error}. Try toggling mic.",
  "chat.error.autoCaptureCameraError": "Auto-Capture Camera Error: {error}",
  "chat.error.snapshotUserError": "{error}",
  "chat.error.recordingTimeExceeded": "Recording stopped automatically after {maxMinutes} minutes.",
  "chat.error.videoMetadataError": "Could not read video metadata. The file may be corrupt or in an unsupported format.",
  "chat.error.pauseVideoToAnnotate": "Pause the video to annotate the current frame",
  "chat.error.imageGenInterrupted": "Image generation was interrupted.",
  "chat.error.thinkingInterrupted": "The AI's response was interrupted.",
  
  // Errors - general
  "error.noLanguagePair": "Critical error: No language pair selected.",
  "error.translationFailed": "Translation failed. Please try again.",
  "error.imageLimitReached": "Session image generation limit reached. Please start a new session.",
  "error.tokenLimitReached": "Session token limit reached. Please start a new session.",
  
  // Errors - camera
  "error.cameraPermissionDenied": "Camera permission denied. Please enable camera access in your browser settings.",
  "error.cameraNotFound": "Selected camera not found. Please ensure it's connected or select a different camera in settings.",
  "error.cameraAccessNotSupported": "Camera access is not supported by your browser.",
  "error.cameraUnknown": "An unknown error occurred while accessing the camera.",
  "error.cameraStreamNotAvailable": "Camera stream not available for capture.",
  "error.imageCaptureGeneric": "Unknown error during image capture.",
  
  // Errors - visual context (dynamically constructed with prefix)
  "error.visualContextVideoElementNotReady": "Visual context video element not ready.",
  "error.snapshotVideoElementNotReady": "Video element for snapshot not ready.",
  "error.visualContextCameraAccessNotSupported": "Camera access not supported for visual context.",
  "error.snapshotCameraAccessNotSupported": "Camera access not supported for snapshot.",
  "error.visualContext2DContext": "Could not get 2D context for visual context.",
  "error.snapshot2DContext": "Could not get 2D context for snapshot.",
  "error.visualContextCaptureFailedPermission": "Visual Context failed: Camera permission denied.",
  "error.snapshotCaptureFailedPermission": "Snapshot failed: Camera permission denied.",
  "error.visualContextCaptureFailedNotFound": "Visual Context failed: Camera not found.",
  "error.snapshotCaptureFailedNotFound": "Snapshot failed: Camera not found.",
  "error.visualContextCaptureFailedNotReady": "Visual Context failed: Camera not ready or problem with the feed. {details}",
  "error.snapshotCaptureFailedNotReady": "Snapshot failed: Camera not ready or problem with the feed. {details}",
  "error.visualContextCaptureFailedGeneric": "Visual Context failed: {details}",
  "error.snapshotCaptureFailedGeneric": "Snapshot failed: {details}",
};
