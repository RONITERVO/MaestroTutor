// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export const frTranslations: Record<string, string> = {
  // Language selector (used in SttLanguageSelector)
  "sttLang.selectLanguage": "Définir la reconnaissance vocale sur {language}",
  
  // Header
  "header.targetLanguageTitle": "Langue cible actuelle : {language}",
  
  // Start page (used)
  "startPage.clickToStart": "Cliquez sur l'avion",
  "startPage.saveChats": "Sauvegarder tous les chats",
  "startPage.loadChats": "Charger les chats",
  "startPage.maestroAvatar": "Avatar de Maestro",
  "startPage.addMaestroAvatar": "Ajouter un avatar Maestro",
  "startPage.loadSuccess": "{count} sessions de chat chargées et remplacées avec succès !",
  "startPage.loadError": "Erreur lors du chargement des chats. Le fichier peut être corrompu ou dans un mauvais format.",
  "startPage.noChatsToSave": "Aucun historique de chat à sauvegarder.",
  "startPage.saveError": "Erreur lors de la sauvegarde des chats. Consultez la console pour plus de détails.",
  
  // General
  "general.clear": "Effacer",
  "general.error": "Désolé, j'ai rencontré une erreur.",

  // API key gate
  "apiKeyGate.title": "Connectez votre clé API Gemini",
  "apiKeyGate.subtitle": "Cette application fonctionne entièrement sur votre appareil. Votre clé ne touche jamais nos serveurs.",
  "apiKeyGate.privacyPolicy": "Politique de confidentialité",
  "apiKeyGate.stepsTitle": "Deux étapes rapides :",
  "apiKeyGate.stepOne": "Ouvrez Google AI Studio et créez une clé API.",
  "apiKeyGate.stepTwo": "Collez la clé ci-dessous et appuyez sur Enregistrer.",
  "apiKeyGate.openAiStudio": "Ouvrir Google AI Studio",
  "apiKeyGate.keyLabel": "Clé API Gemini",
  "apiKeyGate.placeholder": "Collez votre clé API ici",
  "apiKeyGate.show": "Afficher",
  "apiKeyGate.hide": "Masquer",
  "apiKeyGate.currentKeySaved": "Clé actuelle enregistrée {maskedKey}",
  "apiKeyGate.clearSavedKey": "Effacer la clé enregistrée",
  "apiKeyGate.cancel": "Annuler",
  "apiKeyGate.saving": "Enregistrement...",
  "apiKeyGate.saveKey": "Enregistrer la clé",
  "apiKeyGate.close": "Fermer",
  
  // Chat - general
  "chat.thinking": "Réflexion en cours...",
  "chat.loadingHistory": "Chargement de l'historique...",
  "chat.loadingSuggestions": "Chargement des suggestions...",
  "chat.suggestionsAriaLabel": "Suggestions de réponse",
  "chat.attachImageFromFile": "Joindre un fichier",
  "chat.removeAttachedImage": "Supprimer le fichier joint",
  "chat.sendMessage": "Envoyer le message",
  "chat.messageInputAriaLabel": "Saisie du message",
  "chat.retrievedFromWeb": "Récupéré du web :",
  "chat.videoNotSupported": "Votre navigateur ne prend pas en charge la balise vidéo.",
  "chat.audioNotSupported": "Votre navigateur ne prend pas en charge la balise audio.",
  "chat.fileAttachment": "Pièce jointe",
  "chat.imageGenError": "Erreur de génération d'image",
  "chat.generatingImageLoadingSlow": "Cela prend un peu plus de temps...",
  "chat.stopSpeaking": "Arrêter de parler",
  "chat.speakThisLine": "Prononcer cette ligne",
  "chat.languageSelector.openGlobe": "Changer de langues",
  "chat.maestroTranscriptScrollwheel": "Vue de défilement de la transcription Maestro",
  
  // Chat - mic/STT
  "chat.mic.listening": "STT actif : Écoute en cours...",
  "chat.mic.enableStt": "Activer STT",
  "chat.mic.disableStt": "Arrêter STT",
  "chat.mic.recordingAudioNote": "Enregistrement audio...",
  
  // Chat - placeholders
  "chat.placeholder.normal.listening": "Écoute en {language}...",
  "chat.placeholder.normal.sttActive": "Parlez en {language} ou tapez...",
  "chat.placeholder.normal.sttInactive": "Tapez ou appuyez sur le micro pour parler en {language}...",
  "chat.placeholder.suggestion.listening": "Parlez {language} pour traduire...",
  "chat.placeholder.suggestion.sttActive": "Parlez ou tapez en {language} pour traduire...",
  "chat.placeholder.suggestion.sttInactive": "Tapez en {language} pour traduire...",
  
  // Chat - camera
  "chat.camera.turnOn": "Activer l'aperçu caméra",
  "chat.camera.turnOff": "Désactiver l'aperçu caméra",
  "chat.camera.imageGenCameraLabel": "Génération d'image",
  "chat.camera.captureOrRecord": "Appuyez pour photo, maintenez pour vidéo",
  "chat.camera.stopRecording": "Arrêter l'enregistrement",
  "chat.bookIcon.toggleImageGen": "Activer/désactiver le mode génération d'image",
  
  // Chat - image
  "chat.imagePreview.alt": "Aperçu",
  "chat.image.dragToEnlarge": "Faites glisser le coin pour agrandir",
  "chat.image.dragToShrink": "Faites glisser le coin pour réduire",
  "chat.annotateImage": "Annoter l'image",
  "chat.annotateVideoFrame": "Annoter l'image actuelle",
  
  // Chat - annotate modal
  "chat.annotateModal.editingPreviewAlt": "Image à annoter",
  "chat.annotateModal.cancel": "Annuler",
  "chat.annotateModal.saveAndAttach": "Enregistrer et joindre",
  "chat.annotateModal.undo": "Annuler",
  
  // Chat - suggestions
  "chat.suggestion.speak": "Dire : \"{suggestion}\"",
  "chat.suggestion.ariaLabel": "Dire la suggestion : {suggestion}",
  "chat.suggestion.toggleCreateMode": "Activer/désactiver le mode création de suggestion",
  "chat.suggestion.createAction": "Créer une suggestion",
  "chat.suggestion.creating": "Création de la suggestion...",
  
  // Chat - maestro status
  "chat.maestro.idle": "Maestro est inactif",
  "chat.maestro.title.idle": "Maestro est actuellement inactif.",
  "chat.maestro.resting": "Maestro se repose...",
  "chat.maestro.observing": "Maestro observe...",
  "chat.maestro.aboutToEngage": "Maestro est sur le point d'intervenir...",
  "chat.maestro.title.resting": "Maestro est inactif, beaucoup de temps avant la réactivation.",
  "chat.maestro.title.observing": "Maestro observe, un peu de temps avant la réactivation.",
  "chat.maestro.title.aboutToEngage": "Maestro est sur le point de se réactiver bientôt.",
  "chat.maestro.typing": "Maestro écrit...",
  "chat.maestro.title.typing": "Maestro prépare une réponse.",
  "chat.maestro.speaking": "Maestro parle",
  "chat.maestro.title.speaking": "Maestro parle actuellement.",
  "chat.maestro.listening": "Écoute en cours...",
  "chat.maestro.title.listening": "Maestro attend votre saisie ou votre voix.",
  "chat.maestro.holding": "Maestro est en attente",
  "chat.maestro.title.holding": "Maestro est en attente (réactivation en pause)",
  
  // Chat - bookmark
  "chat.bookmark.hiddenHeaderAria": "Messages cachés au-dessus",
  "chat.bookmark.isHere": "Le marque-page est ici",
  "chat.bookmark.setHere": "Définir le marque-page ici",
  "chat.bookmark.actionsRegionAria": "Actions du marque-page",
  "chat.bookmark.actionsToggleTitle": "Options du marque-page",
  "chat.bookmark.decrementAria": "Afficher un de moins",
  "chat.bookmark.decrementTitle": "Moins",
  "chat.bookmark.incrementAria": "Afficher un de plus",
  "chat.bookmark.incrementTitle": "Plus",
  "chat.bookmark.hiddenBelowHeaderAria": "Messages cachés en dessous",
  
  // Chat - send preparation
  "chat.sendPrep.optimizingVideo": "Optimisation de la vidéo...",
  "chat.sendPrep.optimizingImage": "Optimisation de l'image...",
  "chat.sendPrep.preparingMedia": "Préparation du média...",
  "chat.sendPrep.uploadingMedia": "Téléchargement du média...",
  "chat.sendPrep.finalizing": "Finalisation...",
  
  // Chat - header activity tokens
  "chat.header.annotating": "Annotation",
  "chat.header.recordingAudio": "Enregistrement audio",
  "chat.header.recordingVideo": "Enregistrement vidéo",
  "chat.header.savePopup": "Sauvegarde...",
  "chat.header.loadPopup": "Chargement...",
  "chat.header.maestroAvatar": "Mise à jour de l'avatar Maestro",
  "chat.header.watchingVideo": "Visionnage vidéo",
  "chat.header.viewingAbove": "Affichage des messages précédents",
  "chat.header.liveSession": "Session en direct",
  
  // Chat - live session
  "chat.liveSession.stop": "Arrêter le direct",
  "chat.liveSession.retry": "Réessayer le direct",
  "chat.liveSession.start": "Démarrer le direct",
  "chat.liveSession.liveBadge": "Direct",
  "chat.liveSession.connecting": "Connexion",
  
  // Chat - errors
  "chat.error.sttError": "Erreur STT : {error}. Essayez de basculer le micro.",
  "chat.error.autoCaptureCameraError": "Erreur de capture automatique caméra : {error}",
  "chat.error.snapshotUserError": "{error}",
  "chat.error.recordingTimeExceeded": "Enregistrement arrêté automatiquement après {maxMinutes} minutes.",
  "chat.error.videoMetadataError": "Impossible de lire les métadonnées vidéo. Le fichier peut être corrompu ou dans un format non pris en charge.",
  "chat.error.pauseVideoToAnnotate": "Mettez la vidéo en pause pour annoter l'image actuelle",
  "chat.error.imageGenInterrupted": "La génération d'image a été interrompue.",
  "chat.error.thinkingInterrupted": "La réponse de l'IA a été interrompue.",
  
  // Errors - general
  "error.noLanguagePair": "Erreur critique : Aucune paire de langues sélectionnée.",
  "error.translationFailed": "Échec de la traduction. Veuillez réessayer.",
  "error.imageLimitReached": "Limite de génération d'images de la session atteinte. Veuillez démarrer une nouvelle session.",
  "error.tokenLimitReached": "Limite de tokens de la session atteinte. Veuillez démarrer une nouvelle session.",
  
  // Errors - camera
  "error.cameraPermissionDenied": "Permission caméra refusée. Veuillez activer l'accès caméra dans les paramètres de votre navigateur.",
  "error.cameraNotFound": "Caméra sélectionnée introuvable. Assurez-vous qu'elle est connectée ou sélectionnez une autre caméra.",
  "error.cameraAccessNotSupported": "L'accès à la caméra n'est pas pris en charge par votre navigateur.",
  "error.cameraUnknown": "Une erreur inconnue s'est produite lors de l'accès à la caméra.",
  "error.cameraStreamNotAvailable": "Flux caméra non disponible pour la capture.",
  "error.imageCaptureGeneric": "Erreur inconnue lors de la capture d'image.",
  
  // Errors - visual context
  "error.visualContextVideoElementNotReady": "Élément vidéo du contexte visuel non prêt.",
  "error.snapshotVideoElementNotReady": "Élément vidéo pour la capture non prêt.",
  "error.visualContextCameraAccessNotSupported": "Accès caméra non pris en charge pour le contexte visuel.",
  "error.snapshotCameraAccessNotSupported": "Accès caméra non pris en charge pour la capture.",
  "error.visualContext2DContext": "Impossible d'obtenir le contexte 2D pour le contexte visuel.",
  "error.snapshot2DContext": "Impossible d'obtenir le contexte 2D pour la capture.",
  "error.visualContextCaptureFailedPermission": "Échec du contexte visuel : Permission caméra refusée.",
  "error.snapshotCaptureFailedPermission": "Échec de la capture : Permission caméra refusée.",
  "error.visualContextCaptureFailedNotFound": "Échec du contexte visuel : Caméra introuvable.",
  "error.snapshotCaptureFailedNotFound": "Échec de la capture : Caméra introuvable.",
  "error.visualContextCaptureFailedNotReady": "Échec du contexte visuel : Caméra non prête ou problème avec le flux. {details}",
  "error.snapshotCaptureFailedNotReady": "Échec de la capture : Caméra non prête ou problème avec le flux. {details}",
  "error.visualContextCaptureFailedGeneric": "Échec du contexte visuel : {details}",
  "error.snapshotCaptureFailedGeneric": "Échec de la capture : {details}",
};
