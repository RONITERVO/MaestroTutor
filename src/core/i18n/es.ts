
// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export const esTranslations: Record<string, string> = {
  // Language selector (used in SttLanguageSelector)
  "sttLang.selectLanguage": "Establecer reconocimiento de voz a {language}",
  
  // Header
  "header.targetLanguageTitle": "Idioma objetivo actual: {language}",
  
  // Start page (used)
  "startPage.clickToStart": "Haz clic en el avion",
  "startPage.saveChats": "Guardar Todos los Chats",
  "startPage.loadChats": "Cargar Chats",
  "startPage.maestroAvatar": "Avatar de Maestro",
  "startPage.addMaestroAvatar": "Anadir avatar de Maestro",
  "startPage.loadSuccess": "Se cargaron y reemplazaron {count} sesiones de chat con exito!",
  "startPage.loadError": "Error al cargar chats. El archivo podria estar corrupto o en un formato incorrecto.",
  "startPage.noChatsToSave": "No hay historiales de chat para guardar.",
  "startPage.saveError": "Error al guardar los chats. Consulta la consola para mas detalles.",
  
  // General
  "general.clear": "Borrar",
  "general.error": "Lo siento, encontre un error.",
  
  // Chat - general
  "chat.thinking": "Pensando...",
  "chat.loadingHistory": "Cargando historial de chat...",
  "chat.loadingSuggestions": "Cargando sugerencias...",
  "chat.suggestionsAriaLabel": "Sugerencias de respuesta",
  "chat.attachImageFromFile": "Adjuntar archivo",
  "chat.removeAttachedImage": "Eliminar archivo adjunto",
  "chat.sendMessage": "Enviar mensaje",
  "chat.messageInputAriaLabel": "Entrada de mensaje",
  "chat.retrievedFromWeb": "Obtenido de la web:",
  "chat.videoNotSupported": "Tu navegador no soporta la etiqueta de video.",
  "chat.audioNotSupported": "Tu navegador no soporta la etiqueta de audio.",
  "chat.fileAttachment": "Archivo adjunto",
  "chat.imageGenError": "Error de generacion de imagen",
  "chat.generatingImageLoadingSlow": "Tardando un poco mas...",
  "chat.stopSpeaking": "Dejar de hablar",
  "chat.speakThisLine": "Hablar esta linea",
  "chat.languageSelector.openGlobe": "Cambiar idiomas",
  "chat.maestroTranscriptScrollwheel": "Vista de desplazamiento de transcripcion de Maestro",
  
  // Chat - mic/STT
  "chat.mic.listening": "STT Activo: Escuchando...",
  "chat.mic.enableStt": "Activar STT",
  "chat.mic.disableStt": "Detener STT",
  "chat.mic.recordingAudioNote": "Grabando audio...",
  
  // Chat - placeholders
  "chat.placeholder.normal.listening": "Escuchando en {language}...",
  "chat.placeholder.normal.sttActive": "Habla en {language} o escribe...",
  "chat.placeholder.normal.sttInactive": "Escribe o toca el microfono para hablar en {language}...",
  "chat.placeholder.suggestion.listening": "Habla {language} para traducir...",
  "chat.placeholder.suggestion.sttActive": "Habla o escribe en {language} para traducir...",
  "chat.placeholder.suggestion.sttInactive": "Escribe en {language} para traducir...",
  
  // Chat - camera
  "chat.camera.turnOn": "Activar vista previa de camara",
  "chat.camera.turnOff": "Desactivar vista previa de camara",
  "chat.camera.imageGenCameraLabel": "Generacion de imagen",
  "chat.camera.captureOrRecord": "Toca para foto, manten presionado para video",
  "chat.camera.stopRecording": "Detener grabacion",
  "chat.bookIcon.toggleImageGen": "Alternar modo de generacion de imagen",
  
  // Chat - image
  "chat.imagePreview.alt": "Vista previa",
  "chat.image.dragToEnlarge": "Arrastra la esquina para agrandar",
  "chat.image.dragToShrink": "Arrastra la esquina para reducir",
  "chat.annotateImage": "Anotar imagen",
  "chat.annotateVideoFrame": "Anotar fotograma actual",
  
  // Chat - annotate modal
  "chat.annotateModal.editingPreviewAlt": "Imagen a anotar",
  "chat.annotateModal.cancel": "Cancelar",
  "chat.annotateModal.saveAndAttach": "Guardar y adjuntar",
  "chat.annotateModal.undo": "Deshacer",
  
  // Chat - suggestions
  "chat.suggestion.speak": "Hablar: \"{suggestion}\"",
  "chat.suggestion.ariaLabel": "Hablar sugerencia: {suggestion}",
  "chat.suggestion.toggleCreateMode": "Alternar modo de creacion de sugerencia",
  "chat.suggestion.createAction": "Crear sugerencia",
  "chat.suggestion.creating": "Creando sugerencia...",
  
  // Chat - maestro status
  "chat.maestro.idle": "Maestro esta inactivo",
  "chat.maestro.title.idle": "Maestro esta actualmente inactivo.",
  "chat.maestro.resting": "Maestro esta descansando...",
  "chat.maestro.observing": "Maestro esta observando...",
  "chat.maestro.aboutToEngage": "Maestro esta a punto de interactuar...",
  "chat.maestro.title.resting": "Maestro esta inactivo, bastante tiempo antes de la reactivacion.",
  "chat.maestro.title.observing": "Maestro esta observando, algo de tiempo antes de la reactivacion.",
  "chat.maestro.title.aboutToEngage": "Maestro esta a punto de reactivarse pronto.",
  "chat.maestro.typing": "Maestro esta escribiendo...",
  "chat.maestro.title.typing": "Maestro esta preparando una respuesta.",
  "chat.maestro.speaking": "Maestro esta hablando",
  "chat.maestro.title.speaking": "Maestro esta hablando actualmente.",
  "chat.maestro.listening": "Escuchando...",
  "chat.maestro.title.listening": "Maestro esta esperando tu entrada o voz.",
  "chat.maestro.holding": "Maestro esta en espera",
  "chat.maestro.title.holding": "Maestro esta en espera (reactivacion pausada)",
  
  // Chat - bookmark (used)
  "chat.bookmark.hiddenHeaderAria": "Mensajes ocultos arriba",
  "chat.bookmark.isHere": "El marcador esta aqui",
  "chat.bookmark.setHere": "Establecer marcador aqui",
  "chat.bookmark.actionsRegionAria": "Acciones de marcador",
  "chat.bookmark.actionsToggleTitle": "Opciones de marcador",
  "chat.bookmark.decrementAria": "Mostrar uno menos",
  "chat.bookmark.decrementTitle": "Menos",
  "chat.bookmark.incrementAria": "Mostrar uno mas",
  "chat.bookmark.incrementTitle": "Mas",
  "chat.bookmark.hiddenBelowHeaderAria": "Mensajes ocultos abajo",
  
  // Chat - send preparation
  "chat.sendPrep.optimizingVideo": "Optimizando video...",
  "chat.sendPrep.optimizingImage": "Optimizando imagen...",
  "chat.sendPrep.preparingMedia": "Preparando medios...",
  "chat.sendPrep.uploadingMedia": "Subiendo medios...",
  "chat.sendPrep.finalizing": "Finalizando...",
  
  // Chat - header activity tokens
  "chat.header.annotating": "Anotando",
  "chat.header.recordingAudio": "Grabando audio",
  "chat.header.recordingVideo": "Grabando video",
  "chat.header.savePopup": "Guardando...",
  "chat.header.loadPopup": "Cargando...",
  "chat.header.maestroAvatar": "Actualizando avatar de Maestro",
  "chat.header.watchingVideo": "Viendo video",
  "chat.header.viewingAbove": "Viendo mensajes arriba",
  "chat.header.liveSession": "Sesion en vivo",
  
  // Chat - live session
  "chat.liveSession.stop": "Detener en vivo",
  "chat.liveSession.retry": "Reintentar en vivo",
  "chat.liveSession.start": "Iniciar en vivo",
  "chat.liveSession.liveBadge": "En vivo",
  "chat.liveSession.connecting": "Conectando",
  
  // Chat - errors
  "chat.error.sttError": "Error de STT: {error}. Intenta alternar el microfono.",
  "chat.error.autoCaptureCameraError": "Error de captura automatica de camara: {error}",
  "chat.error.snapshotUserError": "{error}",
  "chat.error.recordingTimeExceeded": "Grabacion detenida automaticamente despues de {maxMinutes} minutos.",
  "chat.error.videoMetadataError": "No se pudo leer los metadatos del video. El archivo puede estar corrupto o en un formato no soportado.",
  "chat.error.pauseVideoToAnnotate": "Pausa el video para anotar el fotograma actual",
  "chat.error.imageGenInterrupted": "La generacion de imagen fue interrumpida.",
  "chat.error.thinkingInterrupted": "La respuesta de la IA fue interrumpida.",
  
  // Errors - general
  "error.noLanguagePair": "Error critico: No se selecciono un par de idiomas.",
  "error.translationFailed": "Traduccion fallida. Por favor intenta de nuevo.",
  "error.imageLimitReached": "Limite de generacion de imagenes de la sesion alcanzado. Por favor inicia una nueva sesion.",
  "error.tokenLimitReached": "Limite de tokens de la sesion alcanzado. Por favor inicia una nueva sesion.",
  
  // Errors - camera
  "error.cameraPermissionDenied": "Permiso de camara denegado. Por favor habilita el acceso a la camara en la configuracion de tu navegador.",
  "error.cameraNotFound": "Camara seleccionada no encontrada. Por favor asegurate de que esta conectada o selecciona una camara diferente.",
  "error.cameraAccessNotSupported": "El acceso a la camara no esta soportado por tu navegador.",
  "error.cameraUnknown": "Ocurrio un error desconocido al acceder a la camara.",
  "error.cameraStreamNotAvailable": "Transmision de camara no disponible para captura.",
  "error.imageCaptureGeneric": "Error desconocido durante la captura de imagen.",
  
  // Errors - visual context
  "error.visualContextVideoElementNotReady": "Elemento de video de contexto visual no esta listo.",
  "error.snapshotVideoElementNotReady": "Elemento de video para captura no esta listo.",
  "error.visualContextCameraAccessNotSupported": "Acceso a camara no soportado para contexto visual.",
  "error.snapshotCameraAccessNotSupported": "Acceso a camara no soportado para captura.",
  "error.visualContext2DContext": "No se pudo obtener contexto 2D para contexto visual.",
  "error.snapshot2DContext": "No se pudo obtener contexto 2D para captura.",
  "error.visualContextCaptureFailedPermission": "Contexto visual fallo: Permiso de camara denegado.",
  "error.snapshotCaptureFailedPermission": "Captura fallo: Permiso de camara denegado.",
  "error.visualContextCaptureFailedNotFound": "Contexto visual fallo: Camara no encontrada.",
  "error.snapshotCaptureFailedNotFound": "Captura fallo: Camara no encontrada.",
  "error.visualContextCaptureFailedNotReady": "Contexto visual fallo: Camara no lista o problema con la transmision. {details}",
  "error.snapshotCaptureFailedNotReady": "Captura fallo: Camara no lista o problema con la transmision. {details}",
  "error.visualContextCaptureFailedGeneric": "Contexto visual fallo: {details}",
  "error.snapshotCaptureFailedGeneric": "Captura fallo: {details}",
};
