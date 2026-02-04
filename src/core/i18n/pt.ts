// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export const ptTranslations: Record<string, string> = {
  // App title
  "app.title": "Maestro",
  // Language selector (used in SttLanguageSelector)
  "sttLang.selectLanguage": "Definir reconhecimento de voz para {language}",

  // Header
  "header.targetLanguageTitle": "Idioma de destino atual: {language}",

  // Start page (used)
  "startPage.clickToStart": "Clique no avião",
  "startPage.saveChats": "Salvar todos os chats",
  "startPage.loadChats": "Carregar chats",
  "startPage.saveThisChat": "Salvar este chat",
  "startPage.appendToChat": "Anexar ao chat",
  "startPage.trimBeforeBookmark": "Recortar antes do marcador",
  "startPage.maestroAvatar": "Avatar do Maestro",
  "startPage.addMaestroAvatar": "Adicionar avatar do Maestro",
  "startPage.loadSuccess": "{count} sessões de chat carregadas e substituídas com sucesso!",
  "startPage.loadError": "Erro ao carregar chats. O arquivo pode estar corrompido ou em formato errado.",
  "startPage.noChatsToSave": "Não há histórico de chat para salvar.",
  "startPage.saveError": "Erro ao salvar chats. Verifique o console para mais detalhes.",
  "startPage.noChatSelected": "Por favor, selecione um par de idiomas primeiro.",
  "startPage.noBookmarkSet": "Nenhum marcador definido. Defina um marcador primeiro para remover as mensagens anteriores a ele.",
  "startPage.noMessagesToTrim": "Não há mensagens antes do marcador para remover.",
  "startPage.trimSuccess": "{count} mensagens removidas antes do marcador.",
  "startPage.trimError": "Falha ao recortar mensagens. Por favor, tente novamente.",
  "startPage.noMessagesToAppend": "Nenhuma mensagem encontrada no arquivo de backup para combinar.",
  "startPage.noPairInBackup": "O arquivo de backup não contém mensagens para o seu par de idiomas atual. Por favor, selecione um backup que corresponda ao seu chat atual.",
  "startPage.appendSuccess": "{count} mensagens anexadas ao chat atual com sucesso.",
  "startPage.combineSuccess": "Chats combinados: {added} novas mensagens adicionadas, {total} mensagens no total.",
  "startPage.combineNoDuplicates": "Todas as mensagens já estavam no seu chat. Nenhuma alteração feita.",
  "startPage.combineNoChanges": "Nenhuma nova mensagem para adicionar. Seu chat permanece inalterado.",
  "startPage.invalidBackupFormat": "Arquivo de backup inválido. Por favor, selecione um arquivo de backup do Maestro (.ndjson) válido.",
  "startPage.browserNotSupported": "Seu navegador não suporta o salvamento de arquivos. Por favor, use Chrome ou Edge.",

  // Session Controls - Action labels and descriptions
  "sessionControls.saveAll.label": "Salvar tudo",
  "sessionControls.saveAll.description": "Exportar todos os chats para um arquivo de backup",
  "sessionControls.loadAll.label": "Carregar tudo",
  "sessionControls.loadAll.description": "Substituir todos os chats pelo arquivo de backup",
  "sessionControls.reset.label": "Redefinir",
  "sessionControls.reset.description": "Fazer backup e apagar todos os dados",
  "sessionControls.saveThis.label": "Salvar chat",
  "sessionControls.saveThis.description": "Exportar apenas este chat",
  "sessionControls.combine.label": "Combinar",
  "sessionControls.combine.description": "Mesclar backup neste chat",
  "sessionControls.trim.label": "Recortar",
  "sessionControls.trim.description": "Remover mensagens antes do marcador",

  // Session Controls - UI elements
  "sessionControls.profile": "Perfil:",
  "sessionControls.profilePlaceholder": "Seu nome ou detalhes...",
  "sessionControls.editProfile": "Editar perfil de usuário",
  "sessionControls.allChatsControls": "Controles de todos os chats",
  "sessionControls.thisChatsControls": "Controles deste chat",
  "sessionControls.back": "Voltar",
  "sessionControls.backupAndReset": "Backup e Redefinição",
  "sessionControls.typeToConfirm": "Digite \"{keyword}\" para confirmar",
  "sessionControls.changeAvatar": "Alterar avatar",

  // General
  "general.clear": "Limpar",
  "general.error": "Desculpe, encontrei um erro.",

  // API key gate
  "apiKeyGate.title": "Conecte sua chave API do Gemini",
  "apiKeyGate.billingTitle": "Configurar faturamento para cota maior",
  "apiKeyGate.subtitle": "Este app roda totalmente no seu dispositivo. Sua chave nunca passa pelos nossos servidores.",
  "apiKeyGate.privacyPolicy": "Política de Privacidade",
  "apiKeyGate.stepsTitle": "Dois passos rápidos:",
  "apiKeyGate.stepOne": "Abra o Google AI Studio e crie uma chave API.",
  "apiKeyGate.stepTwo": "Cole a chave abaixo e toque em Salvar.",
  "apiKeyGate.openAiStudio": "Abrir Google AI Studio",
  "apiKeyGate.viewInstructions": "Ver instruções",
  "apiKeyGate.closeInstructions": "Fechar instruções",
  "apiKeyGate.previousInstruction": "Instrução anterior",
  "apiKeyGate.nextInstruction": "Próxima instrução",
  "apiKeyGate.instructionStep": "Instrução {step} de {total}",
  "apiKeyGate.keyLabel": "Chave API do Gemini",
  "apiKeyGate.placeholder": "Cole sua chave API aqui",
  "apiKeyGate.show": "Mostrar",
  "apiKeyGate.hide": "Ocultar",
  "apiKeyGate.currentKeySaved": "Chave atual salva {maskedKey}",
  "apiKeyGate.clearSavedKey": "Limpar chave salva",
  "apiKeyGate.cancel": "Cancelar",
  "apiKeyGate.saving": "Salvando...",
  "apiKeyGate.saveKey": "Salvar chave",
  "apiKeyGate.close": "Fechar",

  // Chat - general
  "chat.thinking": "Pensando...",
  "chat.loadingHistory": "Carregando histórico do chat...",
  "chat.loadingSuggestions": "Carregando sugestões...",
  "chat.suggestionsAriaLabel": "Sugestões de resposta",
  "chat.attachImageFromFile": "Anexar arquivo",
  "chat.removeAttachedImage": "Remover arquivo anexado",
  "chat.sendMessage": "Enviar mensagem",
  "chat.messageInputAriaLabel": "Entrada de mensagem",
  "chat.retrievedFromWeb": "Recuperado da web:",
  "chat.videoNotSupported": "Seu navegador não suporta a tag de vídeo.",
  "chat.audioNotSupported": "Seu navegador não suporta a tag de áudio.",
  "chat.fileAttachment": "Anexo de arquivo",
  "chat.imageGenError": "Erro na geração de imagem",
  "chat.generatingImageLoadingSlow": "Demorando um pouco mais...",
  "chat.stopSpeaking": "Parar de falar",
  "chat.speakThisLine": "Falar esta linha",
  "chat.languageSelector.openGlobe": "Alterar idiomas",
  "chat.maestroTranscriptScrollwheel": "Vista de rolagem da transcrição do Maestro",

  // Chat - mic/STT
  "chat.mic.listening": "STT ativo: Ouvindo...",
  "chat.mic.enableStt": "Ativar STT",
  "chat.mic.disableStt": "Parar STT",
  "chat.mic.recordingAudioNote": "Gravando áudio...",

  // Chat - placeholders
  "chat.placeholder.normal.listening": "Ouvindo em {language}...",
  "chat.placeholder.normal.sttActive": "Fale em {language} ou digite...",
  "chat.placeholder.normal.sttInactive": "Digite ou toque no microfone para falar em {language}...",
  "chat.placeholder.suggestion.listening": "Fale {language} para traduzir...",
  "chat.placeholder.suggestion.sttActive": "Fale ou digite em {language} para traduzir...",
  "chat.placeholder.suggestion.sttInactive": "Digite em {language} para traduzir...",

  // Chat - camera
  "chat.camera.turnOn": "Ativar visualização da câmera",
  "chat.camera.turnOff": "Desativar visualização da câmera",
  "chat.camera.imageGenCameraLabel": "Geração de imagem",
  "chat.camera.captureOrRecord": "Toque para foto, segure para vídeo",
  "chat.camera.stopRecording": "Parar gravação",
  "chat.bookIcon.toggleImageGen": "Alternar modo de geração de imagem",

  // Chat - image
  "chat.imagePreview.alt": "Visualização",
  "chat.image.dragToEnlarge": "Arraste o canto para ampliar",
  "chat.image.dragToShrink": "Arraste o canto para reduzir",
  "chat.annotateImage": "Anotar imagem",
  "chat.annotateVideoFrame": "Anotar quadro atual",

  // Chat - annotate modal
  "chat.annotateModal.editingPreviewAlt": "Imagem a ser anotada",
  "chat.annotateModal.cancel": "Cancelar",
  "chat.annotateModal.saveAndAttach": "Salvar e anexar",
  "chat.annotateModal.undo": "Desfazer",

  // Chat - suggestions
  "chat.suggestion.speak": "Falar: \"{suggestion}\"",
  "chat.suggestion.ariaLabel": "Falar sugestão: {suggestion}",
  "chat.suggestion.toggleCreateMode": "Alternar modo de criação de sugestão",
  "chat.suggestion.createAction": "Criar sugestão",
  "chat.suggestion.creating": "Criando sugestão...",

  // Chat - maestro status (used via CollapsedMaestroStatus)
  "chat.maestro.idle": "Maestro está inativo",
  "chat.maestro.title.idle": "Maestro está atualmente inativo.",
  "chat.maestro.resting": "Maestro está descansando...",
  "chat.maestro.observing": "Maestro está observando...",
  "chat.maestro.aboutToEngage": "Maestro está prestes a interagir...",
  "chat.maestro.title.resting": "Maestro está inativo, muito tempo antes da reativação.",
  "chat.maestro.title.observing": "Maestro está observando, algum tempo antes da reativação.",
  "chat.maestro.title.aboutToEngage": "Maestro está prestes a se reativar em breve.",
  "chat.maestro.typing": "Maestro está digitando...",
  "chat.maestro.title.typing": "Maestro está preparando uma resposta.",
  "chat.maestro.speaking": "Maestro está falando",
  "chat.maestro.title.speaking": "Maestro está falando agora.",
  "chat.maestro.listening": "Ouvindo...",
  "chat.maestro.title.listening": "Maestro está aguardando sua entrada ou voz.",
  "chat.maestro.holding": "Maestro está aguardando",
  "chat.maestro.title.holding": "Maestro está aguardando (reativação pausada)",

  // Chat - bookmark (used)
  "chat.bookmark.hiddenHeaderAria": "Oculto acima das mensagens",
  "chat.bookmark.isHere": "O marcador está aqui",
  "chat.bookmark.setHere": "Definir marcador aqui",
  "chat.bookmark.actionsRegionAria": "Ações do marcador",
  "chat.bookmark.actionsToggleTitle": "Opções do marcador",
  "chat.bookmark.decrementAria": "Mostrar um a menos",
  "chat.bookmark.decrementTitle": "Menos",
  "chat.bookmark.incrementAria": "Mostrar um a mais",
  "chat.bookmark.incrementTitle": "Mais",
  "chat.bookmark.hiddenBelowHeaderAria": "Mensagens ocultas abaixo",

  // Chat - send preparation
  "chat.sendPrep.optimizingVideo": "Otimizando vídeo...",
  "chat.sendPrep.optimizingImage": "Otimizando imagem...",
  "chat.sendPrep.preparingMedia": "Preparando mídia...",
  "chat.sendPrep.uploadingMedia": "Enviando mídia...",
  "chat.sendPrep.finalizing": "Finalizando...",

  // Chat - header activity tokens (used via activityTokens.ts)
  "chat.header.annotating": "Anotando",
  "chat.header.recordingAudio": "Gravando áudio",
  "chat.header.recordingVideo": "Gravando vídeo",
  "chat.header.savePopup": "Salvando...",
  "chat.header.loadPopup": "Carregando...",
  "chat.header.maestroAvatar": "Atualizando avatar do Maestro",
  "chat.header.watchingVideo": "Assistindo vídeo",
  "chat.header.viewingAbove": "Visualizando mensagens anteriores",
  "chat.header.liveSession": "Sessão ao vivo",

  // Chat - live session
  "chat.liveSession.stop": "Parar ao vivo",
  "chat.liveSession.retry": "Tentar novamente ao vivo",
  "chat.liveSession.start": "Iniciar ao vivo",
  "chat.liveSession.liveBadge": "Ao vivo",
  "chat.liveSession.connecting": "Conectando",
  "chat.liveSession.defaultLastMessage": "Olá! Como posso ajudar você hoje?",
  "chat.liveSession.defaultSuggestion1": "Olá",
  "chat.liveSession.defaultSuggestion2": "Bom dia",
  "chat.liveSession.defaultSuggestion3": "Como você está?",

  // Chat - errors
  "chat.error.sttError": "Erro STT: {error}. Tente alternar o microfone.",
  "chat.error.autoCaptureCameraError": "Erro de captura automática da câmera: {error}",
  "chat.error.snapshotUserError": "{error}",
  "chat.error.recordingTimeExceeded": "Gravação parada automaticamente após {maxMinutes} minutos.",
  "chat.error.videoMetadataError": "Não foi possível ler os metadados do vídeo. O arquivo pode estar corrompido ou em formato não suportado.",
  "chat.error.pauseVideoToAnnotate": "Pause o vídeo para anotar o quadro atual",
  "chat.error.imageGenInterrupted": "A geração de imagem foi interrompida.",
  "chat.error.thinkingInterrupted": "A resposta da IA foi interrompida.",

  // Errors - general
  "error.noLanguagePair": "Erro crítico: Nenhum par de idiomas selecionado.",
  "error.translationFailed": "Tradução falhou. Por favor, tente novamente.",
  "error.imageLimitReached": "Limite de geração de imagens da sessão atingido. Por favor, inicie uma nova sessão.",
  "error.tokenLimitReached": "Limite de tokens da sessão atingido. Por favor, inicie uma nova sessão.",
  "error.apiKeyMissing": "Sua chave API do Gemini está faltando. Abra a tela de chave API e cole sua chave.",
  "error.apiKeyInvalid": "Sua chave API do Gemini é inválida. Verifique se há erros de digitação e cole uma chave válida.",
  "error.apiQuotaExceeded": "Sua cota gratuita da API Gemini para chat esgotou. Abri a tela de chave API com passos de faturamento. Você ainda pode usar a conversa ao vivo enquanto isso.",

  // Errors - camera
  "error.cameraPermissionDenied": "Permissão da câmera negada. Por favor, habilite o acesso à câmera nas configurações do seu navegador.",
  "error.cameraNotFound": "Câmera selecionada não encontrada. Certifique-se de que está conectada ou selecione uma câmera diferente nas configurações.",
  "error.cameraAccessNotSupported": "O acesso à câmera não é suportado pelo seu navegador.",
  "error.cameraUnknown": "Ocorreu um erro desconhecido ao acessar a câmera.",
  "error.cameraStreamNotAvailable": "Stream da câmera não disponível para captura.",
  "error.imageCaptureGeneric": "Erro desconhecido durante a captura de imagem.",

  // Errors - visual context (dynamically constructed with prefix)
  "error.visualContextVideoElementNotReady": "Elemento de vídeo do contexto visual não está pronto.",
  "error.snapshotVideoElementNotReady": "Elemento de vídeo para captura não está pronto.",
  "error.visualContextCameraAccessNotSupported": "Acesso à câmera não suportado para contexto visual.",
  "error.snapshotCameraAccessNotSupported": "Acesso à câmera não suportado para captura.",
  "error.visualContext2DContext": "Não foi possível obter contexto 2D para contexto visual.",
  "error.snapshot2DContext": "Não foi possível obter contexto 2D para captura.",
  "error.visualContextCaptureFailedPermission": "Contexto visual falhou: Permissão da câmera negada.",
  "error.snapshotCaptureFailedPermission": "Captura falhou: Permissão da câmera negada.",
  "error.visualContextCaptureFailedNotFound": "Contexto visual falhou: Câmera não encontrada.",
  "error.snapshotCaptureFailedNotFound": "Captura falhou: Câmera não encontrada.",
  "error.visualContextCaptureFailedNotReady": "Contexto visual falhou: Câmera não pronta ou problema com o sinal. {details}",
  "error.snapshotCaptureFailedNotReady": "Captura falhou: Câmera não pronta ou problema com o sinal. {details}",
  "error.visualContextCaptureFailedGeneric": "Contexto visual falhou: {details}",
  "error.snapshotCaptureFailedGeneric": "Captura falhou: {details}",
};