// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export const esTranslations: Record<string, string> = {
  // App title
  "app.title": "Maestro",
  // Language selector (used in SttLanguageSelector)
  "sttLang.selectLanguage": "Establecer reconocimiento de voz a {language}",

  // Header
  "header.targetLanguageTitle": "Idioma objetivo actual: {language}",

  // Start page (used)
  "startPage.clickToStart": "Haz clic en el avión",
  "startPage.saveChats": "Guardar todos los chats",
  "startPage.loadChats": "Cargar chats",
  "startPage.saveThisChat": "Guardar este chat",
  "startPage.appendToChat": "Anexar al chat",
  "startPage.trimBeforeBookmark": "Recortar antes del marcador",
  "startPage.maestroAvatar": "Avatar de Maestro",
  "startPage.addMaestroAvatar": "Añadir avatar de Maestro",
  "startPage.loadSuccess": "¡Se cargaron y reemplazaron {count} sesiones de chat con éxito!",
  "startPage.loadError": "Error al cargar chats. El archivo podría estar corrupto o en un formato incorrecto.",
  "startPage.noChatsToSave": "No hay historiales de chat para guardar.",
  "startPage.saveError": "Error al guardar los chats. Consulta la consola para más detalles.",
  "startPage.noChatSelected": "Por favor selecciona un par de idiomas primero.",
  "startPage.noBookmarkSet": "No hay marcador establecido. Establece un marcador primero para recortar los mensajes anteriores a él.",
  "startPage.noMessagesToTrim": "No hay mensajes antes del marcador para eliminar.",
  "startPage.trimSuccess": "Se eliminaron {count} mensajes anteriores al marcador.",
  "startPage.trimError": "Error al recortar mensajes. Por favor inténtalo de nuevo.",
  "startPage.noMessagesToAppend": "No se encontraron mensajes en el archivo de copia de seguridad para combinar.",
  "startPage.noPairInBackup": "El archivo de copia de seguridad no contiene mensajes para tu par de idiomas actual. Por favor selecciona una copia de seguridad que coincida con tu chat actual.",
  "startPage.appendSuccess": "Se anexaron {count} mensajes al chat actual con éxito.",
  "startPage.combineSuccess": "Chats combinados: {added} mensajes nuevos añadidos, {total} mensajes en total.",
  "startPage.combineNoDuplicates": "Todos los mensajes ya estaban en tu chat. No se realizaron cambios.",
  "startPage.combineNoChanges": "No hay mensajes nuevos para añadir. Tu chat no ha cambiado.",
  "startPage.invalidBackupFormat": "Archivo de copia de seguridad no válido. Por favor selecciona un archivo de copia de seguridad de Maestro (.ndjson) válido.",
  "startPage.browserNotSupported": "Tu navegador no soporta el guardado de archivos. Por favor usa Chrome o Edge.",

  // Session Controls - Action labels and descriptions
  "sessionControls.saveAll.label": "Guardar todo",
  "sessionControls.saveAll.description": "Exportar todos los chats a archivo",
  "sessionControls.loadAll.label": "Cargar todo",
  "sessionControls.loadAll.description": "Reemplazar chats con archivo",
  "sessionControls.reset.label": "Restablecer",
  "sessionControls.reset.description": "Respaldar y borrar datos",
  "sessionControls.saveThis.label": "Guardar chat",
  "sessionControls.saveThis.description": "Exportar solo este chat",
  "sessionControls.combine.label": "Combinar",
  "sessionControls.combine.description": "Fusionar archivo en este chat",
  "sessionControls.trim.label": "Recortar",
  "sessionControls.trim.description": "Eliminar msjes. antes marcador",

  // Session Controls - UI elements
  "sessionControls.profile": "Perfil:",
  "sessionControls.profilePlaceholder": "Tu nombre o detalles...",
  "sessionControls.editProfile": "Editar perfil de usuario",
  "sessionControls.allChatsControls": "Controles de todos los chats",
  "sessionControls.thisChatsControls": "Controles de este chat",
  "sessionControls.back": "Atrás",
  "sessionControls.backupAndReset": "Respaldar y Restablecer",
  "sessionControls.typeToConfirm": "Escribe \"{keyword}\" para confirmar",
  "sessionControls.changeAvatar": "Cambiar avatar",

  // General
  "general.clear": "Borrar",
  "general.error": "Lo siento, encontré un error.",

  // API key gate
  "apiKeyGate.title": "Conecta tu clave API de Gemini",
  "apiKeyGate.billingTitle": "Configurar facturación para mayor cuota",
  "apiKeyGate.subtitle": "Esta aplicación se ejecuta totalmente en tu dispositivo. Tu clave nunca toca nuestros servidores.",
  "apiKeyGate.privacyPolicy": "Política de privacidad",
  "apiKeyGate.stepsTitle": "Dos pasos rápidos:",
  "apiKeyGate.stepOne": "Abre Google AI Studio y crea una clave API.",
  "apiKeyGate.stepTwo": "Pega la clave abajo y toca Guardar.",
  "apiKeyGate.openAiStudio": "Abrir Google AI Studio",
  "apiKeyGate.viewInstructions": "Ver instrucciones",
  "apiKeyGate.closeInstructions": "Cerrar instrucciones",
  "apiKeyGate.previousInstruction": "Instrucción anterior",
  "apiKeyGate.nextInstruction": "Siguiente instrucción",
  "apiKeyGate.instructionStep": "Instrucción {step} de {total}",
  "apiKeyGate.keyLabel": "Clave API de Gemini",
  "apiKeyGate.placeholder": "Pega tu clave API aquí",
  "apiKeyGate.show": "Mostrar",
  "apiKeyGate.hide": "Ocultar",
  "apiKeyGate.currentKeySaved": "Clave actual guardada {maskedKey}",
  "apiKeyGate.clearSavedKey": "Borrar clave guardada",
  "apiKeyGate.cancel": "Cancelar",
  "apiKeyGate.saving": "Guardando...",
  "apiKeyGate.saveKey": "Guardar clave",
  "apiKeyGate.close": "Cerrar",

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
  "chat.imageGenError": "Error de generación de imagen",
  "chat.generatingImageLoadingSlow": "Tardando un poco más...",
  "chat.stopSpeaking": "Dejar de hablar",
  "chat.speakThisLine": "Hablar esta línea",
  "chat.languageSelector.openGlobe": "Cambiar idiomas",
  "chat.maestroTranscriptScrollwheel": "Vista de desplazamiento de transcripción de Maestro",

  // Chat - mic/STT
  "chat.mic.listening": "STT activo: Escuchando...",
  "chat.mic.enableStt": "Activar STT",
  "chat.mic.disableStt": "Detener STT",
  "chat.mic.recordingAudioNote": "Grabando audio...",

  // Chat - placeholders
  "chat.placeholder.normal.listening": "Escuchando en {language}...",
  "chat.placeholder.normal.sttActive": "Habla en {language} o escribe...",
  "chat.placeholder.normal.sttInactive": "Escribe o toca el micrófono para hablar en {language}...",
  "chat.placeholder.suggestion.listening": "Habla {language} para traducir...",
  "chat.placeholder.suggestion.sttActive": "Habla o escribe en {language} para traducir...",
  "chat.placeholder.suggestion.sttInactive": "Escribe en {language} para traducir...",

  // Chat - camera
  "chat.camera.turnOn": "Activar vista previa de cámara",
  "chat.camera.turnOff": "Desactivar vista previa de cámara",
  "chat.camera.imageGenCameraLabel": "Generación de imagen",
  "chat.camera.captureOrRecord": "Toca para foto, mantén presionado para video",
  "chat.camera.stopRecording": "Detener grabación",
  "chat.bookIcon.toggleImageGen": "Alternar modo de generación de imagen",

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
  "chat.suggestion.toggleCreateMode": "Alternar modo de creación de sugerencia",
  "chat.suggestion.createAction": "Crear sugerencia",
  "chat.suggestion.creating": "Creando sugerencia...",

  // Chat - maestro status (used via CollapsedMaestroStatus)
  "chat.maestro.idle": "Maestro está inactivo",
  "chat.maestro.title.idle": "Maestro está actualmente inactivo.",
  "chat.maestro.resting": "Maestro está descansando...",
  "chat.maestro.observing": "Maestro está observando...",
  "chat.maestro.aboutToEngage": "Maestro está a punto de interactuar...",
  "chat.maestro.title.resting": "Maestro está inactivo, bastante tiempo antes de la reactivación.",
  "chat.maestro.title.observing": "Maestro está observando, algo de tiempo antes de la reactivación.",
  "chat.maestro.title.aboutToEngage": "Maestro está a punto de reactivarse pronto.",
  "chat.maestro.typing": "Maestro está escribiendo...",
  "chat.maestro.title.typing": "Maestro está preparando una respuesta.",
  "chat.maestro.speaking": "Maestro está hablando",
  "chat.maestro.title.speaking": "Maestro está hablando actualmente.",
  "chat.maestro.listening": "Escuchando...",
  "chat.maestro.title.listening": "Maestro está esperando tu entrada o voz.",
  "chat.maestro.holding": "Maestro está en espera",
  "chat.maestro.title.holding": "Maestro está en espera (reactivación pausada)",

  // Chat - bookmark (used)
  "chat.bookmark.hiddenHeaderAria": "Mensajes ocultos arriba",
  "chat.bookmark.isHere": "El marcador está aquí",
  "chat.bookmark.setHere": "Establecer marcador aquí",
  "chat.bookmark.actionsRegionAria": "Acciones de marcador",
  "chat.bookmark.actionsToggleTitle": "Opciones de marcador",
  "chat.bookmark.decrementAria": "Mostrar uno menos",
  "chat.bookmark.decrementTitle": "Menos",
  "chat.bookmark.incrementAria": "Mostrar uno más",
  "chat.bookmark.incrementTitle": "Más",
  "chat.bookmark.hiddenBelowHeaderAria": "Mensajes ocultos abajo",

  // Chat - send preparation
  "chat.sendPrep.optimizingVideo": "Optimizando video...",
  "chat.sendPrep.optimizingImage": "Optimizando imagen...",
  "chat.sendPrep.preparingMedia": "Preparando medios...",
  "chat.sendPrep.uploadingMedia": "Subiendo medios...",
  "chat.sendPrep.finalizing": "Finalizando...",

  // Chat - header activity tokens (used via activityTokens.ts)
  "chat.header.annotating": "Anotando",
  "chat.header.recordingAudio": "Grabando audio",
  "chat.header.recordingVideo": "Grabando video",
  "chat.header.savePopup": "Guardando...",
  "chat.header.loadPopup": "Cargando...",
  "chat.header.maestroAvatar": "Actualizando avatar de Maestro",
  "chat.header.watchingVideo": "Viendo video",
  "chat.header.viewingAbove": "Viendo mensajes arriba",
  "chat.header.liveSession": "Sesión en vivo",

  // Chat - live session
  "chat.liveSession.stop": "Detener en vivo",
  "chat.liveSession.retry": "Reintentar en vivo",
  "chat.liveSession.start": "Iniciar en vivo",
  "chat.liveSession.liveBadge": "En vivo",
  "chat.liveSession.connecting": "Conectando",
  "chat.liveSession.defaultLastMessage": "¡Hola! ¿Cómo puedo ayudarte hoy?",
  "chat.liveSession.defaultSuggestion1": "Hola",
  "chat.liveSession.defaultSuggestion2": "Buenos días",
  "chat.liveSession.defaultSuggestion3": "¿Cómo estás?",

  // Chat - errors
  "chat.error.sttError": "Error de STT: {error}. Intenta alternar el micrófono.",
  "chat.error.autoCaptureCameraError": "Error de captura automática de cámara: {error}",
  "chat.error.snapshotUserError": "{error}",
  "chat.error.recordingTimeExceeded": "Grabación detenida automáticamente después de {maxMinutes} minutos.",
  "chat.error.videoMetadataError": "No se pudo leer los metadatos del video. El archivo puede estar corrupto o en un formato no soportado.",
  "chat.error.pauseVideoToAnnotate": "Pausa el video para anotar el fotograma actual",
  "chat.error.imageGenInterrupted": "La generación de imagen fue interrumpida.",
  "chat.error.thinkingInterrupted": "La respuesta de la IA fue interrumpida.",

  // Errors - general
  "error.noLanguagePair": "Error crítico: No se seleccionó un par de idiomas.",
  "error.translationFailed": "Traducción fallida. Por favor intenta de nuevo.",
  "error.imageLimitReached": "Límite de generación de imágenes de la sesión alcanzado. Por favor inicia una nueva sesión.",
  "error.tokenLimitReached": "Límite de tokens de la sesión alcanzado. Por favor inicia una nueva sesión.",
  "error.apiKeyMissing": "Falta tu clave API de Gemini. Abre la pantalla de clave API y pega tu clave.",
  "error.apiKeyInvalid": "Tu clave API de Gemini no es válida. Por favor, comprueba si hay errores tipográficos y pega una clave válida.",
  "error.apiQuotaExceeded": "Tu cuota gratuita de la API de Gemini para el chat se ha agotado.",
  "error.quotaSetupBilling": "Configurar facturación",
  "error.quotaStartLive": "Iniciar Live en su lugar",

  // Errors - camera
  "error.cameraPermissionDenied": "Permiso de cámara denegado. Por favor habilita el acceso a la cámara en la configuración de tu navegador.",
  "error.cameraNotFound": "Cámara seleccionada no encontrada. Por favor asegúrate de que está conectada o selecciona una cámara diferente.",
  "error.cameraAccessNotSupported": "El acceso a la cámara no está soportado por tu navegador.",
  "error.cameraUnknown": "Ocurrió un error desconocido al acceder a la cámara.",
  "error.cameraStreamNotAvailable": "Transmisión de cámara no disponible para captura.",
  "error.imageCaptureGeneric": "Error desconocido durante la captura de imagen.",

  // Errors - visual context (dynamically constructed with prefix)
  "error.visualContextVideoElementNotReady": "Elemento de video de contexto visual no está listo.",
  "error.snapshotVideoElementNotReady": "Elemento de video para captura no está listo.",
  "error.visualContextCameraAccessNotSupported": "Acceso a cámara no soportado para contexto visual.",
  "error.snapshotCameraAccessNotSupported": "Acceso a cámara no soportado para captura.",
  "error.visualContext2DContext": "No se pudo obtener contexto 2D para contexto visual.",
  "error.snapshot2DContext": "No se pudo obtener contexto 2D para captura.",
  "error.visualContextCaptureFailedPermission": "Contexto visual falló: Permiso de cámara denegado.",
  "error.snapshotCaptureFailedPermission": "Captura falló: Permiso de cámara denegado.",
  "error.visualContextCaptureFailedNotFound": "Contexto visual falló: Cámara no encontrada.",
  "error.snapshotCaptureFailedNotFound": "Captura falló: Cámara no encontrada.",
  "error.visualContextCaptureFailedNotReady": "Contexto visual falló: Cámara no lista o problema con la transmisión. {details}",
  "error.snapshotCaptureFailedNotReady": "Captura falló: Cámara no lista o problema con la transmisión. {details}",
  "error.visualContextCaptureFailedGeneric": "Contexto visual falló: {details}",
  "error.snapshotCaptureFailedGeneric": "Captura falló: {details}",
};