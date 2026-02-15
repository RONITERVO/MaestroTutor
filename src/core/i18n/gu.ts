// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export const guTranslations: Record<string, string> = {
  // App title
  "app.title": "માએસ્ટ્રો",
  // Language selector (used in SttLanguageSelector)
  "sttLang.selectLanguage": "સ્પીચ રેકગ્નિશન {language} પર સેટ કરો",

  // Header
  "header.targetLanguageTitle": "વર્તમાન લક્ષ્ય ભાષા: {language}",

  // Start page (used)
  "startPage.clickToStart": "વિમાન પર ક્લિક કરો",
  "startPage.saveChats": "બધી ચેટ સાચવો",
  "startPage.loadChats": "ચેટ લોડ કરો",
  "startPage.saveThisChat": "આ ચેટ સાચવો",
  "startPage.appendToChat": "ચેટમાં જોડો",
  "startPage.trimBeforeBookmark": "બુકમાર્ક પહેલાનું કાપો",
  "startPage.maestroAvatar": "માએસ્ટ્રો અવતાર",
  "startPage.addMaestroAvatar": "માએસ્ટ્રો અવતાર ઉમેરો",
  "startPage.loadSuccess": "{count} ચેટ સત્રો સફળતાપૂર્વક લોડ થયા અને બદલાયા!",
  "startPage.loadError": "ચેટ લોડ કરવામાં ભૂલ. ફાઇલ દૂષિત અથવા ખોટા ફોર્મેટમાં હોઈ શકે છે.",
  "startPage.noChatsToSave": "સાચવવા માટે કોઈ ચેટ ઇતિહાસ નથી.",
  "startPage.saveError": "ચેટ સાચવવામાં ભૂલ. વધુ વિગતો માટે કન્સોલ જુઓ.",
  "startPage.noChatSelected": "કૃપા કરીને પહેલા ભાષાની જોડી પસંદ કરો.",
  "startPage.noBookmarkSet": "કોઈ બુકમાર્ક સેટ નથી. તેના પહેલાના સંદેશાઓ કાપવા માટે પહેલા બુકમાર્ક સેટ કરો.",
  "startPage.noMessagesToTrim": "બુકમાર્ક પહેલા દૂર કરવા માટે કોઈ સંદેશા નથી.",
  "startPage.trimSuccess": "બુકમાર્ક પહેલાના {count} સંદેશાઓ દૂર કર્યા.",
  "startPage.trimError": "સંદેશાઓ કાપવામાં નિષ્ફળ. કૃપા કરીને ફરી પ્રયાસ કરો.",
  "startPage.noMessagesToAppend": "બેકઅપ ફાઇલમાં જોડવા માટે કોઈ સંદેશા મળ્યા નથી.",
  "startPage.noPairInBackup": "બેકઅપ ફાઇલમાં તમારી વર્તમાન ભાષાની જોડી માટે સંદેશાઓ નથી. કૃપા કરીને તમારી વર્તમાન ચેટ સાથે મેળ ખાતું બેકઅપ પસંદ કરો.",
  "startPage.appendSuccess": "વર્તમાન ચેટમાં સફળતાપૂર્વક {count} સંદેશાઓ જોડ્યા.",
  "startPage.combineSuccess": "ચેટ્સ સંયોજિત કરી: {added} નવા સંદેશાઓ ઉમેર્યા, કુલ {total} સંદેશાઓ.",
  "startPage.combineNoDuplicates": "બધા સંદેશાઓ પહેલાથી જ તમારી ચેટમાં હતા. કોઈ ફેરફાર થયો નથી.",
  "startPage.combineNoChanges": "ઉમેરવા માટે કોઈ નવા સંદેશા નથી. તમારી ચેટ યથાવત છે.",
  "startPage.invalidBackupFormat": "અમાન્ય બેકઅપ ફાઇલ. કૃપા કરીને માન્ય Maestro બેકઅપ (.ndjson) ફાઇલ પસંદ કરો.",
  "startPage.browserNotSupported": "તમારું બ્રાઉઝર ફાઇલ સેવિંગને સપોર્ટ કરતું નથી. કૃપા કરીને Chrome અથવા Edge નો ઉપયોગ કરો.",

  // Session Controls - Action labels and descriptions
  "sessionControls.saveAll.label": "બધું સાચવો",
  "sessionControls.saveAll.description": "બેકઅપ ફાઇલમાં બધી ચેટ્સ નિકાસ કરો",
  "sessionControls.loadAll.label": "બધું લોડ કરો",
  "sessionControls.loadAll.description": "બધી ચેટ્સને બેકઅપ ફાઇલ સાથે બદલો",
  "sessionControls.reset.label": "રીસેટ",
  "sessionControls.reset.description": "બેકઅપ લો અને ડેટા કાઢી નાખો",
  "sessionControls.saveThis.label": "ચેટ સાચવો",
  "sessionControls.saveThis.description": "ફક્ત આ ચેટ નિકાસ કરો",
  "sessionControls.combine.label": "જોડો",
  "sessionControls.combine.description": "આ ચેટમાં બેકઅપ મર્જ કરો",
  "sessionControls.trim.label": "કાપો",
  "sessionControls.trim.description": "બુકમાર્ક પહેલાના સંદેશાઓ દૂર કરો",

  // Session Controls - UI elements
  "sessionControls.profile": "પ્રોફાઇલ:",
  "sessionControls.profilePlaceholder": "તમારું નામ અથવા વિગતો...",
  "sessionControls.editProfile": "વપરાશકર્તા પ્રોફાઇલ સંપાદિત કરો",
  "sessionControls.allChatsControls": "બધી ચેટ્સ નિયંત્રણો",
  "sessionControls.thisChatsControls": "આ ચેટ નિયંત્રણો",
  "sessionControls.back": "પાછા",
  "sessionControls.backupAndReset": "બેકઅપ અને રીસેટ",
  "sessionControls.typeToConfirm": "પુષ્ટિ કરવા માટે \"{keyword}\" ટાઇપ કરો",
  "sessionControls.changeAvatar": "અવતાર બદલો",

  // General
  "general.clear": "સાફ કરો",
  "general.error": "ક્ષમા કરશો, એક ભૂલ આવી.",

  // API key gate
  "apiKeyGate.title": "તમારી Gemini API કી જોડો",
  "apiKeyGate.billingTitle": "ઉચ્ચ ક્વોટા માટે બિલિંગ સેટ કરો",
  "apiKeyGate.subtitle": "આ એપ્લિકેશન સંપૂર્ણપણે તમારા ઉપકરણ પર ચાલે છે. તમારી કી ક્યારેય અમારા સર્વરને સ્પર્શતી નથી.",
  "apiKeyGate.privacyPolicy": "ગોપનીયતા નીતિ",
  "apiKeyGate.stepsTitle": "બે ઝડપી પગલાં:",
  "apiKeyGate.stepOne": "Google AI Studio ખોલો અને API કી બનાવો.",
  "apiKeyGate.stepTwo": "નીચે કી પેસ્ટ કરો અને સાચવો પર ટેપ કરો.",
  "apiKeyGate.openAiStudio": "Google AI Studio ખોલો",
  "apiKeyGate.viewInstructions": "સૂચનાઓ જુઓ",
  "apiKeyGate.closeInstructions": "સૂચનાઓ બંધ કરો",
  "apiKeyGate.previousInstruction": "અગાઉની સૂચના",
  "apiKeyGate.nextInstruction": "આગામી સૂચના",
  "apiKeyGate.instructionStep": "સૂચના {step} / {total}",
  "apiKeyGate.keyLabel": "Gemini API કી",
  "apiKeyGate.placeholder": "તમારી API કી અહીં પેસ્ટ કરો",
  "apiKeyGate.show": "બતાવો",
  "apiKeyGate.hide": "છુપાવો",
  "apiKeyGate.currentKeySaved": "વર્તમાન કી સાચવેલી છે {maskedKey}",
  "apiKeyGate.clearSavedKey": "સાચવેલી કી ભૂંસી નાખો",
  "apiKeyGate.cancel": "રદ કરો",
  "apiKeyGate.saving": "સાચવી રહ્યું છે...",
  "apiKeyGate.saveKey": "કી સાચવો",
  "apiKeyGate.close": "બંધ કરો",

  // Chat - general
  "chat.thinking": "વિચારું છું...",
  "chat.loadingHistory": "ચેટ ઇતિહાસ લોડ કરી રહ્યું છે...",
  "chat.loadingSuggestions": "સૂચનો લોડ કરી રહ્યું છે...",
  "chat.suggestionsAriaLabel": "જવાબ સૂચનો",
  "chat.attachImageFromFile": "ફાઇલ જોડો",
  "chat.removeAttachedImage": "જોડેલી ફાઇલ દૂર કરો",
  "chat.sendMessage": "સંદેશ મોકલો",
  "chat.messageInputAriaLabel": "સંદેશ ઇનપુટ",
  "chat.retrievedFromWeb": "વેબ પરથી મેળવેલ:",
  "chat.videoNotSupported": "તમારું બ્રાઉઝર વિડિઓ ટેગને સપોર્ટ કરતું નથી.",
  "chat.audioNotSupported": "તમારું બ્રાઉઝર ઓડિયો ટેગને સપોર્ટ કરતું નથી.",
  "chat.fileAttachment": "ફાઇલ જોડાણ",
  "chat.imageGenError": "છબી બનાવટમાં ભૂલ",
  "chat.generatingImageLoadingSlow": "થોડો વધુ સમય લાગી રહ્યો છે...",
  "chat.stopSpeaking": "બોલવાનું બંધ કરો",
  "chat.speakThisLine": "આ લાઇન બોલો",
  "chat.languageSelector.openGlobe": "ભાષાઓ બદલો",
  "chat.maestroTranscriptScrollwheel": "માએસ્ટ્રો ટ્રાન્સક્રિપ્ટ સ્ક્રોલ વ્યૂ",

  // Chat - mic/STT
  "chat.mic.listening": "STT સક્રિય: સાંભળી રહ્યું છે...",
  "chat.mic.enableStt": "STT સક્ષમ કરો",
  "chat.mic.disableStt": "STT રોકો",
  "chat.mic.recordingAudioNote": "ઓડિયો રેકોર્ડ કરી રહ્યું છે...",

  // Chat - placeholders
  "chat.placeholder.normal.listening": "{language} માં સાંભળી રહ્યું છે...",
  "chat.placeholder.normal.sttActive": "{language} માં બોલો અથવા ટાઈપ કરો...",
  "chat.placeholder.normal.sttInactive": "ટાઈપ કરો અથવા {language} માં બોલવા માટે માઇક પર ટૅપ કરો...",
  "chat.placeholder.suggestion.listening": "અનુવાદ કરવા માટે {language} બોલો...",
  "chat.placeholder.suggestion.sttActive": "અનુવાદ કરવા માટે {language} માં બોલો અથવા ટાઈપ કરો...",
  "chat.placeholder.suggestion.sttInactive": "અનુવાદ કરવા માટે {language} માં ટાઈપ કરો...",

  // Chat - camera
  "chat.camera.turnOn": "કેમેરા પૂર્વાવલોકન ચાલુ કરો",
  "chat.camera.turnOff": "કેમેરા પૂર્વાવલોકન બંધ કરો",
  "chat.camera.imageGenCameraLabel": "છબી બનાવટ",
  "chat.camera.captureOrRecord": "ફોટા માટે ટૅપ કરો, વિડિઓ માટે હોલ્ડ કરો",
  "chat.camera.stopRecording": "રેકોર્ડિંગ રોકો",
  "chat.bookIcon.toggleImageGen": "છબી બનાવટ મોડ ટૉગલ કરો",

  // Chat - image
  "chat.imagePreview.alt": "પૂર્વાવલોકન",
  "chat.image.dragToEnlarge": "મોટું કરવા માટે ખૂણેથી ખેંચો",
  "chat.image.dragToShrink": "નાનું કરવા માટે ખૂણેથી ખેંચો",
  "chat.annotateImage": "છબી પર ટિપ્પણી કરો",
  "chat.annotateVideoFrame": "વર્તમાન ફ્રેમ પર ટિપ્પણી કરો",

  // Chat - annotate modal
  "chat.annotateModal.editingPreviewAlt": "ટિપ્પણી કરવા માટેની છબી",
  "chat.annotateModal.cancel": "રદ કરો",
  "chat.annotateModal.saveAndAttach": "સાચવો અને જોડો",
  "chat.annotateModal.undo": "પૂર્વવત્ કરો",

  // Chat - suggestions
  "chat.suggestion.speak": "બોલો: \"{suggestion}\"",
  "chat.suggestion.ariaLabel": "સૂચન બોલો: {suggestion}",
  "chat.suggestion.toggleCreateMode": "સૂચન બનાવટ મોડ ટૉગલ કરો",
  "chat.suggestion.createAction": "સૂચન બનાવો",
  "chat.suggestion.creating": "સૂચન બનાવી રહ્યું છે...",

  // Chat - maestro status
  "chat.maestro.idle": "માએસ્ટ્રો નિષ્ક્રિય છે",
  "chat.maestro.title.idle": "માએસ્ટ્રો હાલમાં નિષ્ક્રિય છે.",
  "chat.maestro.resting": "માએસ્ટ્રો આરામ કરી રહ્યું છે...",
  "chat.maestro.observing": "માએસ્ટ્રો નિરીક્ષણ કરી રહ્યું છે...",
  "chat.maestro.aboutToEngage": "માએસ્ટ્રો જલ્દી જોડાશે...",
  "chat.maestro.title.resting": "માએસ્ટ્રો નિષ્ક્રિય છે, ફરી સક્રિય થવામાં ઘણો સમય છે.",
  "chat.maestro.title.observing": "માએસ્ટ્રો નિરીક્ષણ કરી રહ્યું છે, ફરી સક્રિય થવામાં થોડો સમય છે.",
  "chat.maestro.title.aboutToEngage": "માએસ્ટ્રો ટૂંક સમયમાં ફરી સક્રિય થશે.",
  "chat.maestro.typing": "માએસ્ટ્રો ટાઈપ કરી રહ્યું છે...",
  "chat.maestro.title.typing": "માએસ્ટ્રો જવાબ તૈયાર કરી રહ્યું છે.",
  "chat.maestro.speaking": "માએસ્ટ્રો બોલી રહ્યું છે",
  "chat.maestro.title.speaking": "માએસ્ટ્રો અત્યારે બોલી રહ્યું છે.",
  "chat.maestro.listening": "સાંભળી રહ્યું છે...",
  "chat.maestro.title.listening": "માએસ્ટ્રો તમારા ઇનપુટ અથવા અવાજની રાહ જોઈ રહ્યું છે.",
  "chat.maestro.holding": "માએસ્ટ્રો રાહ જોઈ રહ્યું છે",
  "chat.maestro.title.holding": "માએસ્ટ્રો રાહ જોઈ રહ્યું છે (પુનઃસક્રિયકરણ થોભાવ્યું છે)",

  // Chat - bookmark
  "chat.bookmark.hiddenHeaderAria": "ઉપર છુપાયેલા સંદેશાઓ",
  "chat.bookmark.isHere": "બુકમાર્ક અહીં છે",
  "chat.bookmark.setHere": "અહીં બુકમાર્ક સેટ કરો",
  "chat.bookmark.actionsRegionAria": "બુકમાર્ક ક્રિયાઓ",
  "chat.bookmark.actionsToggleTitle": "બુકમાર્ક વિકલ્પો",
  "chat.bookmark.decrementAria": "એક ઓછું બતાવો",
  "chat.bookmark.decrementTitle": "એક ઓછું",
  "chat.bookmark.incrementAria": "એક વધુ બતાવો",
  "chat.bookmark.incrementTitle": "એક વધુ",
  "chat.bookmark.hiddenBelowHeaderAria": "નીચે છુપાયેલા સંદેશાઓ",

  // Chat - send preparation
  "chat.sendPrep.optimizingVideo": "વિડિઓ ઑપ્ટિમાઇઝ કરી રહ્યું છે...",
  "chat.sendPrep.optimizingImage": "છબી ઑપ્ટિમાઇઝ કરી રહ્યું છે...",
  "chat.sendPrep.preparingMedia": "મીડિયા તૈયાર કરી રહ્યું છે...",
  "chat.sendPrep.uploadingMedia": "મીડિયા અપલોડ કરી રહ્યું છે...",
  "chat.sendPrep.finalizing": "આખરી ઓપ આપી રહ્યું છે...",

  // Chat - header activity tokens
  "chat.header.annotating": "ટિપ્પણી કરી રહ્યું છે",
  "chat.header.recordingAudio": "ઓડિયો રેકોર્ડ કરી રહ્યું છે",
  "chat.header.recordingVideo": "વિડિઓ રેકોર્ડ કરી રહ્યું છે",
  "chat.header.savePopup": "સાચવી રહ્યું છે...",
  "chat.header.loadPopup": "લોડ કરી રહ્યું છે...",
  "chat.header.maestroAvatar": "માએસ્ટ્રો અવતાર અપડેટ કરી રહ્યું છે",
  "chat.header.watchingVideo": "વિડિઓ જોઈ રહ્યું છે",
  "chat.header.viewingAbove": "અગાઉના સંદેશાઓ જોઈ રહ્યું છે",
  "chat.header.liveSession": "લાઇવ સત્ર",

  // Chat - live session
  "chat.liveSession.stop": "લાઇવ રોકો",
  "chat.liveSession.retry": "લાઇવ ફરી પ્રયાસ કરો",
  "chat.liveSession.start": "લાઇવ શરૂ કરો",
  "chat.liveSession.liveBadge": "લાઇવ",
  "chat.liveSession.connecting": "કનેક્ટ કરી રહ્યું છે",
  "chat.liveSession.defaultLastMessage": "નમસ્તે! આજે હું તમને કેવી રીતે મદદ કરી શકું?",
  "chat.liveSession.defaultSuggestion1": "નમસ્તે",
  "chat.liveSession.defaultSuggestion2": "શુભ સવાર",
  "chat.liveSession.defaultSuggestion3": "તમે કેમ છો?",

  // Chat - errors
  "chat.error.sttError": "STT ભૂલ: {error}. માઇક બદલવાનો પ્રયાસ કરો.",
  "chat.error.autoCaptureCameraError": "ઓટો કેપ્ચર કેમેરા ભૂલ: {error}",
  "chat.error.snapshotUserError": "{error}",
  "chat.error.recordingTimeExceeded": "{maxMinutes} મિનિટ પછી રેકોર્ડિંગ આપમેળે બંધ થયું.",
  "chat.error.videoMetadataError": "વિડિઓ મેટાડેટા વાંચી શકાયું નથી. ફાઇલ દૂષિત અથવા અસમર્થિત ફોર્મેટમાં હોઈ શકે છે.",
  "chat.error.pauseVideoToAnnotate": "વર્તમાન ફ્રેમ પર ટિપ્પણી કરવા માટે વિડિઓ થોભાવો",
  "chat.error.imageGenInterrupted": "છબી બનાવટમાં ખલેલ પડી.",
  "chat.error.thinkingInterrupted": "AI પ્રતિસાદમાં ખલેલ પડી.",

  // Errors - general
  "error.noLanguagePair": "ગંભીર ભૂલ: કોઈ ભાષા જોડી પસંદ કરેલ નથી.",
  "error.translationFailed": "અનુવાદ નિષ્ફળ. ફરી પ્રયાસ કરો.",
  "error.imageLimitReached": "સત્ર છબી મર્યાદા પૂરી થઈ. કૃપા કરીને નવું સત્ર શરૂ કરો.",
  "error.tokenLimitReached": "સત્ર ટોકન મર્યાદા પૂરી થઈ. કૃપા કરીને નવું સત્ર શરૂ કરો.",
  "error.apiKeyMissing": "તમારી Gemini API કી ખૂટે છે. API કી સ્ક્રીન ખોલો અને તમારી કી પેસ્ટ કરો.",
  "error.apiKeyInvalid": "તમારી Gemini API કી અમાન્ય છે. કૃપા કરીને ટાઈપો માટે તપાસો અને માન્ય કી પેસ્ટ કરો.",
  "error.apiQuotaExceeded": "ચેટ માટે તમારો Gemini API ફ્રી ક્વોટા સમાપ્ત થઈ ગયો છે.",
  "error.quotaSetupBilling": "બિલિંગ સેટ કરો",
  "error.quotaStartLive": "તેના બદલે લાઈવ શરૂ કરો",

  // Errors - camera
  "error.cameraPermissionDenied": "કેમેરા પરવાનગી નકારી. બ્રાઉઝર સેટિંગ્સમાં કેમેરા ઍક્સેસ સક્ષમ કરો.",
  "error.cameraNotFound": "પસંદ કરેલ કેમેરા મળ્યો નથી. ખાતરી કરો કે તે કનેક્ટ થયેલ છે અથવા અન્ય કેમેરા પસંદ કરો.",
  "error.cameraAccessNotSupported": "તમારા બ્રાઉઝર દ્વારા કેમેરા ઍક્સેસ સમર્થિત નથી.",
  "error.cameraUnknown": "કેમેરા ઍક્સેસ કરતી વખતે અજ્ઞાત ભૂલ આવી.",
  "error.cameraStreamNotAvailable": "કેપ્ચર માટે કેમેરા સ્ટ્રીમ ઉપલબ્ધ નથી.",
  "error.imageCaptureGeneric": "છબી કેપ્ચર કરતી વખતે અજ્ઞાત ભૂલ.",

  // Errors - visual context
  "error.visualContextVideoElementNotReady": "વિઝ્યુઅલ સંદર્ભ વિડિઓ તત્વ તૈયાર નથી.",
  "error.snapshotVideoElementNotReady": "સ્નેપશોટ વિડિઓ તત્વ તૈયાર નથી.",
  "error.visualContextCameraAccessNotSupported": "વિઝ્યુઅલ સંદર્ભ માટે કેમેરા ઍક્સેસ સમર્થિત નથી.",
  "error.snapshotCameraAccessNotSupported": "સ્નેપશોટ માટે કેમેરા ઍક્સેસ સમર્થિત નથી.",
  "error.visualContext2DContext": "વિઝ્યુઅલ સંદર્ભ માટે 2D સંદર્ભ મેળવી શકાયું નથી.",
  "error.snapshot2DContext": "સ્નેપશોટ માટે 2D સંદર્ભ મેળવી શકાયું નથી.",
  "error.visualContextCaptureFailedPermission": "વિઝ્યુઅલ સંદર્ભ નિષ્ફળ: કેમેરા પરવાનગી નકારી.",
  "error.snapshotCaptureFailedPermission": "સ્નેપશોટ નિષ્ફળ: કેમેરા પરવાનગી નકારી.",
  "error.visualContextCaptureFailedNotFound": "વિઝ્યુઅલ સંદર્ભ નિષ્ફળ: કેમેરા મળ્યો નથી.",
  "error.snapshotCaptureFailedNotFound": "સ્નેપશોટ નિષ્ફળ: કેમેરા મળ્યો નથી.",
  "error.visualContextCaptureFailedNotReady": "વિઝ્યુઅલ સંદર્ભ નિષ્ફળ: કેમેરા તૈયાર નથી અથવા ફીડમાં સમસ્યા. {details}",
  "error.snapshotCaptureFailedNotReady": "સ્નેપશોટ નિષ્ફળ: કેમેરા તૈયાર નથી અથવા ફીડમાં સમસ્યા. {details}",
  "error.visualContextCaptureFailedGeneric": "વિઝ્યુઅલ સંદર્ભ નિષ્ફળ: {details}",
  "error.snapshotCaptureFailedGeneric": "સ્નેપશોટ નિષ્ફળ: {details}",
};