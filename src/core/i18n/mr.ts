// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export const mrTranslations: Record<string, string> = {
  // App title
  "app.title": "Maestro",
  // Language selector (used in SttLanguageSelector)
  "sttLang.selectLanguage": "भाषण ओळख {language} वर सेट करा",

  // Header
  "header.targetLanguageTitle": "वर्तमान लक्ष्य भाषा: {language}",

  // Start page (used)
  "startPage.clickToStart": "विमानावर क्लिक करा",
  "startPage.saveChats": "सर्व गप्पा जतन करा",
  "startPage.loadChats": "गप्पा लोड करा",
  "startPage.saveThisChat": "ही गप्पा जतन करा",
  "startPage.appendToChat": "गप्पांमध्ये जोडा",
  "startPage.trimBeforeBookmark": "बुकमार्कपूर्वीचे ट्रिम करा",
  "startPage.maestroAvatar": "मायस्ट्रो अवतार",
  "startPage.addMaestroAvatar": "मायस्ट्रो अवतार जोडा",
  "startPage.loadSuccess": "{count} चॅट सत्रे यशस्वीरित्या लोड आणि बदलली आहेत!",
  "startPage.loadError": "गप्पा लोड करण्यात त्रुटी. फाइल खराब किंवा चुकीच्या फॉरमॅटमध्ये असू शकते.",
  "startPage.noChatsToSave": "जतन करण्यासाठी कोणताही गप्पा इतिहास नाही.",
  "startPage.saveError": "गप्पा जतन करण्यात त्रुटी. अधिक तपशीलांसाठी कन्सोल पहा.",
  "startPage.noChatSelected": "कृपया आधी भाषा जोडी निवडा.",
  "startPage.noBookmarkSet": "कोणताही बुकमार्क सेट केलेला नाही. त्यापूर्वीचे संदेश ट्रिम करण्यासाठी आधी एक बुकमार्क सेट करा.",
  "startPage.noMessagesToTrim": "काढण्यासाठी बुकमार्कपूर्वी कोणतेही संदेश नाहीत.",
  "startPage.trimSuccess": "बुकमार्कपूर्वीचे {count} संदेश यशस्वीरित्या काढले.",
  "startPage.trimError": "संदेश ट्रिम करण्यात अक्षम. कृपया पुन्हा प्रयत्न करा.",
  "startPage.noMessagesToAppend": "एकत्र करण्यासाठी बॅकअप फाइलमध्ये कोणतेही संदेश सापडले नाहीत.",
  "startPage.noPairInBackup": "बॅकअप फाइलमध्ये तुमच्या वर्तमान भाषा जोडीसाठी संदेश नाहीत. कृपया तुमच्या वर्तमान चॅटशी जुळणारा बॅकअप निवडा.",
  "startPage.appendSuccess": "सध्याच्या गप्पांमध्ये {count} संदेश यशस्वीरित्या जोडले.",
  "startPage.combineSuccess": "गप्पा एकत्र केल्या: {added} नवीन संदेश जोडले, एकूण {total} संदेश.",
  "startPage.combineNoDuplicates": "सर्व संदेश आधीच तुमच्या चॅटमध्ये होते. कोणताही बदल केला नाही.",
  "startPage.combineNoChanges": "जोडण्यासाठी कोणतेही नवीन संदेश नाहीत. तुमची चॅट बदललेली नाही.",
  "startPage.invalidBackupFormat": "अवैध बॅकअप फाइल. कृपया वैध Maestro बॅकअप (.ndjson) फाइल निवडा.",
  "startPage.browserNotSupported": "तुमचा ब्राउझर फाइल जतन करण्यास समर्थन देत नाही. कृपया Chrome किंवा Edge वापरा.",

  // Session Controls - Action labels and descriptions
  "sessionControls.saveAll.label": "सर्व जतन करा",
  "sessionControls.saveAll.description": "सर्व गप्पा बॅकअप फाइलमध्ये एक्सपोर्ट करा",
  "sessionControls.loadAll.label": "सर्व लोड करा",
  "sessionControls.loadAll.description": "सर्व गप्पा बॅकअप फाइलने बदला",
  "sessionControls.reset.label": "रीसेट करा",
  "sessionControls.reset.description": "सर्व डेटा बॅकअप घ्या आणि हटवा",
  "sessionControls.saveThis.label": "गप्पा जतन करा",
  "sessionControls.saveThis.description": "फक्त हीच गप्पा एक्सपोर्ट करा",
  "sessionControls.combine.label": "एकत्र करा",
  "sessionControls.combine.description": "बॅकअप या गप्पांमध्ये विलीन करा",
  "sessionControls.trim.label": "ट्रिम करा",
  "sessionControls.trim.description": "बुकमार्कपूर्वीचे संदेश काढा",

  // Session Controls - UI elements
  "sessionControls.profile": "प्रोफाइल:",
  "sessionControls.profilePlaceholder": "तुमचे नाव किंवा तपशील...",
  "sessionControls.editProfile": "वापरकर्ता प्रोफाइल संपादित करा",
  "sessionControls.allChatsControls": "सर्व गप्पा नियंत्रणे",
  "sessionControls.thisChatsControls": "या गप्पांची नियंत्रणे",
  "sessionControls.all": "सर्व",
  "sessionControls.this": "हे",
  "sessionControls.back": "मागे",
  "sessionControls.backupAndReset": "बॅकअप आणि रीसेट",
  "sessionControls.typeToConfirm": "पुष्टी करण्यासाठी \"{keyword}\" टाइप करा",
  "sessionControls.changeAvatar": "अवतार बदला",

  // General
  "general.clear": "साफ करा",
  "general.error": "क्षमस्व, एक त्रुटी आली.",

  // API key gate
  "apiKeyGate.title": "तुमची Gemini API की जोडा",
  "apiKeyGate.billingTitle": "उच्च कोट्यासाठी बिलिंग सेट करा",
  "apiKeyGate.subtitle": "हे ॲप पूर्णपणे तुमच्या डिव्हाइसवर चालते. तुमची की आमच्या सर्व्हरला कधीही स्पर्श करत नाही.",
  "apiKeyGate.privacyPolicy": "गोपनीयता धोरण",
  "apiKeyGate.stepsTitle": "दोन जलद पावले:",
  "apiKeyGate.stepOne": "Google AI Studio उघडा आणि API की तयार करा.",
  "apiKeyGate.stepTwo": "खाली की पेस्ट करा आणि 'जतन करा' (Save) वर टॅप करा.",
  "apiKeyGate.openAiStudio": "Google AI Studio उघडा",
  "apiKeyGate.viewInstructions": "सूचना पहा",
  "apiKeyGate.closeInstructions": "सूचना बंद करा",
  "apiKeyGate.previousInstruction": "मागील सूचना",
  "apiKeyGate.nextInstruction": "पुढील सूचना",
  "apiKeyGate.instructionStep": "सूचना {step} पैकी {total}",
  "apiKeyGate.keyLabel": "Gemini API की",
  "apiKeyGate.placeholder": "तुमची API की येथे पेस्ट करा",
  "apiKeyGate.show": "दाखवा",
  "apiKeyGate.hide": "लपवा",
  "apiKeyGate.currentKeySaved": "वर्तमान की जतन केली आहे {maskedKey}",
  "apiKeyGate.clearSavedKey": "जतन केलेली की पुसा",
  "apiKeyGate.cancel": "रद्द करा",
  "apiKeyGate.saving": "जतन करत आहे...",
  "apiKeyGate.saveKey": "की जतन करा",
  "apiKeyGate.close": "बंद करा",

  // Chat - general
  "chat.thinking": "विचार करत आहे...",
  "chat.loadingHistory": "गप्पा इतिहास लोड करत आहे...",
  "chat.loadingSuggestions": "सूचना लोड करत आहे...",
  "chat.suggestionsAriaLabel": "उत्तर सूचना",
  "chat.attachImageFromFile": "फाइल जोडा",
  "chat.removeAttachedImage": "जोडलेली फाइल काढा",
  "chat.sendMessage": "संदेश पाठवा",
  "chat.messageInputAriaLabel": "संदेश इनपुट",
  "chat.retrievedFromWeb": "वेबवरून मिळवले:",
  "chat.videoNotSupported": "तुमचा ब्राउझर व्हिडिओ टॅगला सपोर्ट करत नाही.",
  "chat.audioNotSupported": "तुमचा ब्राउझर ऑडिओ टॅगला सपोर्ट करत नाही.",
  "chat.fileAttachment": "फाइल संलग्नक",
  "chat.imageGenError": "प्रतिमा निर्मिती त्रुटी",
  "chat.generatingImageLoadingSlow": "थोडा जास्त वेळ लागत आहे...",
  "chat.stopSpeaking": "बोलणे थांबवा",
  "chat.speakThisLine": "ही ओळ बोला",
  "chat.languageSelector.openGlobe": "भाषा बदला",
  "chat.maestroTranscriptScrollwheel": "मायस्ट्रो ट्रान्सक्रिप्ट स्क्रोल व्ह्यू",

  // Chat - mic/STT
  "chat.mic.listening": "STT सक्रिय: ऐकत आहे...",
  "chat.mic.enableStt": "STT सक्षम करा",
  "chat.mic.disableStt": "STT थांबवा",
  "chat.mic.recordingAudioNote": "ऑडिओ रेकॉर्ड करत आहे...",

  // Chat - placeholders
  "chat.placeholder.normal.listening": "{language} मध्ये ऐकत आहे...",
  "chat.placeholder.normal.sttActive": "{language} मध्ये बोला किंवा टाइप करा...",
  "chat.placeholder.normal.sttInactive": "टाइप करा किंवा {language} मध्ये बोलण्यासाठी माइकवर टॅप करा...",
  "chat.placeholder.suggestion.listening": "अनुवाद करण्यासाठी {language} बोला...",
  "chat.placeholder.suggestion.sttActive": "अनुवाद करण्यासाठी {language} मध्ये बोला किंवा टाइप करा...",
  "chat.placeholder.suggestion.sttInactive": "अनुवाद करण्यासाठी {language} मध्ये टाइप करा...",

  // Chat - camera
  "chat.camera.turnOn": "कॅमेरा पूर्वावलोकन चालू करा",
  "chat.camera.turnOff": "कॅमेरा पूर्वावलोकन बंद करा",
  "chat.camera.imageGenCameraLabel": "प्रतिमा निर्मिती",
  "chat.camera.captureOrRecord": "फोटोसाठी टॅप करा, व्हिडिओसाठी धरून ठेवा",
  "chat.camera.stopRecording": "रेकॉर्डिंग थांबवा",
  "chat.bookIcon.toggleImageGen": "प्रतिमा निर्मिती मोड टॉगल करा",

  // Chat - image
  "chat.imagePreview.alt": "पूर्वावलोकन",
  "chat.image.dragToEnlarge": "मोठे करण्यासाठी कोपरा खेचा",
  "chat.image.dragToShrink": "लहान करण्यासाठी कोपरा खेचा",
  "chat.annotateImage": "प्रतिमेवर टिपणी करा",
  "chat.annotateVideoFrame": "वर्तमान फ्रेमवर टिपणी करा",

  // Chat - annotate modal
  "chat.annotateModal.editingPreviewAlt": "टिपणी करण्यासाठी प्रतिमा",
  "chat.annotateModal.cancel": "रद्द करा",
  "chat.annotateModal.saveAndAttach": "जतन करा आणि जोडा",
  "chat.annotateModal.undo": "पूर्ववत करा",

  // Chat - suggestions
  "chat.suggestion.speak": "बोला: \"{suggestion}\"",
  "chat.suggestion.ariaLabel": "सूचना बोला: {suggestion}",
  "chat.suggestion.toggleCreateMode": "सूचना निर्मिती मोड टॉगल करा",
  "chat.suggestion.createAction": "सूचना तयार करा",
  "chat.suggestion.creating": "सूचना तयार करत आहे...",

  // Chat - maestro status
  "chat.maestro.idle": "मायस्ट्रो निष्क्रिय आहे",
  "chat.maestro.title.idle": "मायस्ट्रो सध्या निष्क्रिय आहे.",
  "chat.maestro.resting": "मायस्ट्रो विश्रांती घेत आहे...",
  "chat.maestro.observing": "मायस्ट्रो निरीक्षण करत आहे...",
  "chat.maestro.aboutToEngage": "मायस्ट्रो लवकरच सामील होईल...",
  "chat.maestro.title.resting": "मायस्ट्रो निष्क्रिय आहे, पुन्हा सक्रिय होण्यासाठी बराच वेळ आहे.",
  "chat.maestro.title.observing": "मायस्ट्रो निरीक्षण करत आहे, पुन्हा सक्रिय होण्यासाठी थोडा वेळ आहे.",
  "chat.maestro.title.aboutToEngage": "मायस्ट्रो लवकरच पुन्हा सक्रिय होईल.",
  "chat.maestro.typing": "मायस्ट्रो टाइप करत आहे...",
  "chat.maestro.title.typing": "मायस्ट्रो उत्तर तयार करत आहे.",
  "chat.maestro.speaking": "मायस्ट्रो बोलत आहे",
  "chat.maestro.title.speaking": "मायस्ट्रो आता बोलत आहे.",
  "chat.maestro.listening": "ऐकत आहे...",
  "chat.maestro.title.listening": "मायस्ट्रो तुमच्या इनपुट किंवा आवाजाची वाट पाहत आहे.",
  "chat.maestro.holding": "मायस्ट्रो वाट पाहत आहे",
  "chat.maestro.title.holding": "मायस्ट्रो वाट पाहत आहे (पुन्हा सक्रिय होणे थांबवले आहे)",

  // Chat - bookmark (used)
  "chat.bookmark.hiddenHeaderAria": "वर लपलेले संदेश",
  "chat.bookmark.isHere": "बुकमार्क येथे आहे",
  "chat.bookmark.setHere": "येथे बुकमार्क सेट करा",
  "chat.bookmark.actionsRegionAria": "बुकमार्क क्रिया",
  "chat.bookmark.actionsToggleTitle": "बुकमार्क पर्याय",
  "chat.bookmark.decrementAria": "एक कमी दाखवा",
  "chat.bookmark.decrementTitle": "कमी",
  "chat.bookmark.incrementAria": "एक जास्त दाखवा",
  "chat.bookmark.incrementTitle": "जास्त",
  "chat.bookmark.hiddenBelowHeaderAria": "खाली लपलेले संदेश",

  // Chat - send preparation
  "chat.sendPrep.optimizingVideo": "व्हिडिओ ऑप्टिमाइझ करत आहे...",
  "chat.sendPrep.optimizingImage": "प्रतिमा ऑप्टिमाइझ करत आहे...",
  "chat.sendPrep.preparingMedia": "मीडिया तयार करत आहे...",
  "chat.sendPrep.uploadingMedia": "मीडिया अपलोड करत आहे...",
  "chat.sendPrep.finalizing": "अंतिम रूप देत आहे...",

  // Chat - header activity tokens
  "chat.header.annotating": "टिपणी करत आहे",
  "chat.header.recordingAudio": "ऑडिओ रेकॉर्ड करत आहे",
  "chat.header.recordingVideo": "व्हिडिओ रेकॉर्ड करत आहे",
  "chat.header.savePopup": "जतन करत आहे...",
  "chat.header.loadPopup": "लोड करत आहे...",
  "chat.header.maestroAvatar": "मायस्ट्रो अवतार अपडेट करत आहे",
  "chat.header.watchingVideo": "व्हिडिओ पाहत आहे",
  "chat.header.viewingAbove": "मागील संदेश पाहत आहे",
  "chat.header.liveSession": "लाइव्ह सत्र",

  // Chat - live session
  "chat.liveSession.stop": "लाइव्ह थांबवा",
  "chat.liveSession.retry": "लाइव्ह पुन्हा प्रयत्न करा",
  "chat.liveSession.start": "लाइव्ह सुरू करा",
  "chat.liveSession.liveBadge": "लाइव्ह",
  "chat.liveSession.connecting": "कनेक्ट करत आहे",
  "chat.liveSession.defaultLastMessage": "नमस्कार! आज मी तुम्हाला कशी मदत करू शकतो?",
  "chat.liveSession.defaultSuggestion1": "नमस्कार",
  "chat.liveSession.defaultSuggestion2": "शुभ प्रभात",
  "chat.liveSession.defaultSuggestion3": "तुम्ही कसे आहात?",

  // Chat - errors
  "chat.error.sttError": "STT त्रुटी: {error}. माइक बदलून पहा.",
  "chat.error.autoCaptureCameraError": "ऑटो कॅप्चर कॅमेरा त्रुटी: {error}",
  "chat.error.snapshotUserError": "{error}",
  "chat.error.recordingTimeExceeded": "रेकॉर्डिंग {maxMinutes} मिनिटांनंतर आपोआप थांबले.",
  "chat.error.videoMetadataError": "व्हिडिओ मेटाडेटा वाचता आला नाही. फाइल खराब किंवा असमर्थित फॉरमॅटमध्ये असू शकते.",
  "chat.error.pauseVideoToAnnotate": "वर्तमान फ्रेमवर टिपणी करण्यासाठी व्हिडिओ थांबवा",
  "chat.error.imageGenInterrupted": "प्रतिमा निर्मितीमध्ये व्यत्यय आला.",
  "chat.error.thinkingInterrupted": "AI प्रतिसादामध्ये व्यत्यय आला.",

  // Errors - general
  "error.noLanguagePair": "गंभीर त्रुटी: कोणतीही भाषा जोडी निवडलेली नाही.",
  "error.translationFailed": "अनुवाद अयशस्वी झाला. कृपया पुन्हा प्रयत्न करा.",
  "error.imageLimitReached": "सत्र प्रतिमा निर्मिती मर्यादा गाठली. कृपया नवीन सत्र सुरू करा.",
  "error.tokenLimitReached": "सत्र टोकन मर्यादा गाठली. कृपया नवीन सत्र सुरू करा.",
  "error.apiKeyMissing": "तुमची Gemini API की गहाळ आहे. API की स्क्रीन उघडा आणि तुमची की पेस्ट करा.",
  "error.apiKeyInvalid": "तुमची Gemini API की अवैध आहे. कृपया टायपो तपासा आणि वैध की पेस्ट करा.",
  "error.apiQuotaExceeded": "तुमचा चॅटसाठीचा Gemini API मोफत कोटा संपला आहे. मी बिलिंग चरणांसह API की स्क्रीन उघडली आहे. तुम्ही तोपर्यंत लाइव्ह संभाषण वापरू शकता.",

  // Errors - camera
  "error.cameraPermissionDenied": "कॅमेरा परवानगी नाकारली. ब्राउझर सेटिंग्जमध्ये कॅमेरा प्रवेश सक्षम करा.",
  "error.cameraNotFound": "निवडलेला कॅमेरा आढळला नाही. तो जोडलेला असल्याचे सुनिश्चित करा किंवा दुसरा कॅमेरा निवडा.",
  "error.cameraAccessNotSupported": "तुमच्या ब्राउझरद्वारे कॅमेरा प्रवेश समर्थित नाही.",
  "error.cameraUnknown": "कॅमेऱ्यात प्रवेश करताना अज्ञात त्रुटी आली.",
  "error.cameraStreamNotAvailable": "कॅप्चरसाठी कॅमेरा स्ट्रीम उपलब्ध नाही.",
  "error.imageCaptureGeneric": "प्रतिमा कॅप्चर करताना अज्ञात त्रुटी.",

  // Errors - visual context
  "error.visualContextVideoElementNotReady": "दृश्य संदर्भ व्हिडिओ घटक तयार नाही.",
  "error.snapshotVideoElementNotReady": "स्नॅपशॉट व्हिडिओ घटक तयार नाही.",
  "error.visualContextCameraAccessNotSupported": "दृश्य संदर्भासाठी कॅमेरा प्रवेश समर्थित नाही.",
  "error.snapshotCameraAccessNotSupported": "स्नॅपशॉटसाठी कॅमेरा प्रवेश समर्थित नाही.",
  "error.visualContext2DContext": "दृश्य संदर्भासाठी 2D संदर्भ मिळवता आला नाही.",
  "error.snapshot2DContext": "स्नॅपशॉटसाठी 2D संदर्भ मिळवता आला नाही.",
  "error.visualContextCaptureFailedPermission": "दृश्य संदर्भ अयशस्वी: कॅमेरा परवानगी नाकारली.",
  "error.snapshotCaptureFailedPermission": "स्नॅपशॉट अयशस्वी: कॅमेरा परवानगी नाकारली.",
  "error.visualContextCaptureFailedNotFound": "दृश्य संदर्भ अयशस्वी: कॅमेरा आढळला नाही.",
  "error.snapshotCaptureFailedNotFound": "स्नॅपशॉट अयशस्वी: कॅमेरा आढळला नाही.",
  "error.visualContextCaptureFailedNotReady": "दृश्य संदर्भ अयशस्वी: कॅमेरा तयार नाही किंवा फीडमध्ये समस्या. {details}",
  "error.snapshotCaptureFailedNotReady": "स्नॅपशॉट अयशस्वी: कॅमेरा तयार नाही किंवा फीडमध्ये समस्या. {details}",
  "error.visualContextCaptureFailedGeneric": "दृश्य संदर्भ अयशस्वी: {details}",
  "error.snapshotCaptureFailedGeneric": "स्नॅपशॉट अयशस्वी: {details}",
};