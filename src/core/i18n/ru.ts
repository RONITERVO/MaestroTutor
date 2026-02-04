// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export const ruTranslations: Record<string, string> = {
  // App title
  "app.title": "Maestro",
  // Language selector (used in SttLanguageSelector)
  "sttLang.selectLanguage": "Установить распознавание речи на {language}",

  // Header
  "header.targetLanguageTitle": "Текущий целевой язык: {language}",

  // Start page (used)
  "startPage.clickToStart": "Нажмите на самолёт",
  "startPage.saveChats": "Сохранить все чаты",
  "startPage.loadChats": "Загрузить чаты",
  "startPage.saveThisChat": "Сохранить этот чат",
  "startPage.appendToChat": "Добавить в чат",
  "startPage.trimBeforeBookmark": "Обрезать до закладки",
  "startPage.maestroAvatar": "Аватар Маэстро",
  "startPage.addMaestroAvatar": "Добавить аватар Маэстро",
  "startPage.loadSuccess": "Успешно загружено и заменено {count} сессий чата!",
  "startPage.loadError": "Ошибка загрузки чатов. Файл может быть повреждён или в неправильном формате.",
  "startPage.noChatsToSave": "Нет истории чатов для сохранения.",
  "startPage.saveError": "Ошибка сохранения чатов. Подробности смотрите в консоли.",
  "startPage.noChatSelected": "Пожалуйста, сначала выберите языковую пару.",
  "startPage.noBookmarkSet": "Закладка не установлена. Сначала установите закладку, чтобы удалить сообщения перед ней.",
  "startPage.noMessagesToTrim": "Нет сообщений перед закладкой для удаления.",
  "startPage.trimSuccess": "Удалено {count} сообщений перед закладкой.",
  "startPage.trimError": "Не удалось обрезать сообщения. Пожалуйста, попробуйте снова.",
  "startPage.noMessagesToAppend": "В резервном файле не найдено сообщений для объединения.",
  "startPage.noPairInBackup": "Резервный файл не содержит сообщений для вашей текущей языковой пары. Пожалуйста, выберите файл, соответствующий вашему текущему чату.",
  "startPage.appendSuccess": "Успешно добавлено {count} сообщений к текущему чату.",
  "startPage.combineSuccess": "Чаты объединены: добавлено {added} новых сообщений, всего {total} сообщений.",
  "startPage.combineNoDuplicates": "Все сообщения уже были в вашем чате. Изменения не внесены.",
  "startPage.combineNoChanges": "Нет новых сообщений для добавления. Ваш чат не изменен.",
  "startPage.invalidBackupFormat": "Неверный формат резервного файла. Пожалуйста, выберите корректный файл резервной копии Maestro (.ndjson).",
  "startPage.browserNotSupported": "Ваш браузер не поддерживает сохранение файлов. Пожалуйста, используйте Chrome или Edge.",

  // Session Controls - Action labels and descriptions
  "sessionControls.saveAll.label": "Сохранить всё",
  "sessionControls.saveAll.description": "Экспортировать все чаты в файл резервной копии",
  "sessionControls.loadAll.label": "Загрузить всё",
  "sessionControls.loadAll.description": "Заменить все чаты данными из файла резервной копии",
  "sessionControls.reset.label": "Сброс",
  "sessionControls.reset.description": "Резервное копирование и удаление всех данных",
  "sessionControls.saveThis.label": "Сохранить чат",
  "sessionControls.saveThis.description": "Экспортировать только этот чат",
  "sessionControls.combine.label": "Объединить",
  "sessionControls.combine.description": "Объединить резервную копию с этим чатом",
  "sessionControls.trim.label": "Обрезать",
  "sessionControls.trim.description": "Удалить сообщения перед закладкой",

  // Session Controls - UI elements
  "sessionControls.profile": "Профиль:",
  "sessionControls.profilePlaceholder": "Ваше имя или данные...",
  "sessionControls.editProfile": "Изменить профиль пользователя",
  "sessionControls.allChatsControls": "Управление всеми чатами",
  "sessionControls.thisChatsControls": "Управление этим чатом",
  "sessionControls.back": "Назад",
  "sessionControls.backupAndReset": "Резервная копия и сброс",
  "sessionControls.typeToConfirm": "Введите «{keyword}» для подтверждения",
  "sessionControls.changeAvatar": "Сменить аватар",

  // General
  "general.clear": "Очистить",
  "general.error": "Извините, произошла ошибка.",

  // API key gate
  "apiKeyGate.title": "Подключите ваш ключ API Gemini",
  "apiKeyGate.billingTitle": "Настройте биллинг для увеличения квоты",
  "apiKeyGate.subtitle": "Это приложение полностью работает на вашем устройстве. Ваш ключ никогда не попадает на наши серверы.",
  "apiKeyGate.privacyPolicy": "Политика конфиденциальности",
  "apiKeyGate.stepsTitle": "Два быстрых шага:",
  "apiKeyGate.stepOne": "Откройте Google AI Studio и создайте ключ API.",
  "apiKeyGate.stepTwo": "Вставьте ключ ниже и нажмите «Сохранить».",
  "apiKeyGate.openAiStudio": "Открыть Google AI Studio",
  "apiKeyGate.viewInstructions": "Просмотреть инструкции",
  "apiKeyGate.closeInstructions": "Закрыть инструкции",
  "apiKeyGate.previousInstruction": "Предыдущая инструкция",
  "apiKeyGate.nextInstruction": "Следующая инструкция",
  "apiKeyGate.instructionStep": "Инструкция {step} из {total}",
  "apiKeyGate.keyLabel": "Ключ API Gemini",
  "apiKeyGate.placeholder": "Вставьте ваш ключ API здесь",
  "apiKeyGate.show": "Показать",
  "apiKeyGate.hide": "Скрыть",
  "apiKeyGate.currentKeySaved": "Текущий ключ сохранен {maskedKey}",
  "apiKeyGate.clearSavedKey": "Очистить сохраненный ключ",
  "apiKeyGate.cancel": "Отмена",
  "apiKeyGate.saving": "Сохранение...",
  "apiKeyGate.saveKey": "Сохранить ключ",
  "apiKeyGate.close": "Закрыть",

  // Chat - general
  "chat.thinking": "Думаю...",
  "chat.loadingHistory": "Загрузка истории чата...",
  "chat.loadingSuggestions": "Загрузка предложений...",
  "chat.suggestionsAriaLabel": "Предложения ответов",
  "chat.attachImageFromFile": "Прикрепить файл",
  "chat.removeAttachedImage": "Удалить прикреплённый файл",
  "chat.sendMessage": "Отправить сообщение",
  "chat.messageInputAriaLabel": "Ввод сообщения",
  "chat.retrievedFromWeb": "Получено из интернета:",
  "chat.videoNotSupported": "Ваш браузер не поддерживает тег video.",
  "chat.audioNotSupported": "Ваш браузер не поддерживает тег audio.",
  "chat.fileAttachment": "Прикреплённый файл",
  "chat.imageGenError": "Ошибка генерации изображения",
  "chat.generatingImageLoadingSlow": "Занимает немного больше времени...",
  "chat.stopSpeaking": "Остановить озвучивание",
  "chat.speakThisLine": "Озвучить эту строку",
  "chat.languageSelector.openGlobe": "Изменить языки",
  "chat.maestroTranscriptScrollwheel": "Прокрутка транскрипта Маэстро",

  // Chat - mic/STT
  "chat.mic.listening": "STT активен: Слушаю...",
  "chat.mic.enableStt": "Включить STT",
  "chat.mic.disableStt": "Остановить STT",
  "chat.mic.recordingAudioNote": "Запись аудио...",

  // Chat - placeholders
  "chat.placeholder.normal.listening": "Слушаю на {language}...",
  "chat.placeholder.normal.sttActive": "Говорите на {language} или печатайте...",
  "chat.placeholder.normal.sttInactive": "Печатайте или нажмите на микрофон, чтобы говорить на {language}...",
  "chat.placeholder.suggestion.listening": "Говорите на {language} для перевода...",
  "chat.placeholder.suggestion.sttActive": "Говорите или печатайте на {language} для перевода...",
  "chat.placeholder.suggestion.sttInactive": "Печатайте на {language} для перевода...",

  // Chat - camera
  "chat.camera.turnOn": "Включить предпросмотр камеры",
  "chat.camera.turnOff": "Выключить предпросмотр камеры",
  "chat.camera.imageGenCameraLabel": "Генерация изображения",
  "chat.camera.captureOrRecord": "Нажмите для фото, удерживайте для видео",
  "chat.camera.stopRecording": "Остановить запись",
  "chat.bookIcon.toggleImageGen": "Переключить режим генерации изображений",

  // Chat - image
  "chat.imagePreview.alt": "Предпросмотр",
  "chat.image.dragToEnlarge": "Потяните за угол, чтобы увеличить",
  "chat.image.dragToShrink": "Потяните за угол, чтобы уменьшить",
  "chat.annotateImage": "Аннотировать изображение",
  "chat.annotateVideoFrame": "Аннотировать текущий кадр",

  // Chat - annotate modal
  "chat.annotateModal.editingPreviewAlt": "Изображение для аннотации",
  "chat.annotateModal.cancel": "Отмена",
  "chat.annotateModal.saveAndAttach": "Сохранить и прикрепить",
  "chat.annotateModal.undo": "Отменить",

  // Chat - suggestions
  "chat.suggestion.speak": "Сказать: \"{suggestion}\"",
  "chat.suggestion.ariaLabel": "Озвучить предложение: {suggestion}",
  "chat.suggestion.toggleCreateMode": "Переключить режим создания предложений",
  "chat.suggestion.createAction": "Создать предложение",
  "chat.suggestion.creating": "Создание предложения...",

  // Chat - maestro status
  "chat.maestro.idle": "Маэстро бездействует",
  "chat.maestro.title.idle": "Маэстро в данный момент бездействует.",
  "chat.maestro.resting": "Маэстро отдыхает...",
  "chat.maestro.observing": "Маэстро наблюдает...",
  "chat.maestro.aboutToEngage": "Маэстро собирается включиться...",
  "chat.maestro.title.resting": "Маэстро бездействует, много времени до повторного включения.",
  "chat.maestro.title.observing": "Маэстро наблюдает, немного времени до повторного включения.",
  "chat.maestro.title.aboutToEngage": "Маэстро скоро снова включится.",
  "chat.maestro.typing": "Маэстро печатает...",
  "chat.maestro.title.typing": "Маэстро готовит ответ.",
  "chat.maestro.speaking": "Маэстро говорит",
  "chat.maestro.title.speaking": "Маэстро сейчас говорит.",
  "chat.maestro.listening": "Слушаю...",
  "chat.maestro.title.listening": "Маэстро ждёт вашего ввода или голоса.",
  "chat.maestro.holding": "Маэстро ждёт",
  "chat.maestro.title.holding": "Маэстро ждёт (повторное включение приостановлено)",

  // Chat - bookmark (used)
  "chat.bookmark.hiddenHeaderAria": "Скрытые сообщения выше",
  "chat.bookmark.isHere": "Закладка здесь",
  "chat.bookmark.setHere": "Установить закладку здесь",
  "chat.bookmark.actionsRegionAria": "Действия с закладками",
  "chat.bookmark.actionsToggleTitle": "Параметры закладки",
  "chat.bookmark.decrementAria": "Показать на одно меньше",
  "chat.bookmark.decrementTitle": "Меньше",
  "chat.bookmark.incrementAria": "Показать на одно больше",
  "chat.bookmark.incrementTitle": "Больше",
  "chat.bookmark.hiddenBelowHeaderAria": "Скрытые сообщения ниже",

  // Chat - send preparation
  "chat.sendPrep.optimizingVideo": "Оптимизация видео...",
  "chat.sendPrep.optimizingImage": "Оптимизация изображения...",
  "chat.sendPrep.preparingMedia": "Подготовка медиа...",
  "chat.sendPrep.uploadingMedia": "Загрузка медиа...",
  "chat.sendPrep.finalizing": "Завершение...",

  // Chat - header activity tokens
  "chat.header.annotating": "Аннотирование",
  "chat.header.recordingAudio": "Запись аудио",
  "chat.header.recordingVideo": "Запись видео",
  "chat.header.savePopup": "Сохранение...",
  "chat.header.loadPopup": "Загрузка...",
  "chat.header.maestroAvatar": "Обновление аватара Маэстро",
  "chat.header.watchingVideo": "Просмотр видео",
  "chat.header.viewingAbove": "Просмотр предыдущих сообщений",
  "chat.header.liveSession": "Прямая сессия",

  // Chat - live session
  "chat.liveSession.stop": "Остановить трансляцию",
  "chat.liveSession.retry": "Повторить трансляцию",
  "chat.liveSession.start": "Начать трансляцию",
  "chat.liveSession.liveBadge": "Прямой эфир",
  "chat.liveSession.connecting": "Подключение",
  "chat.liveSession.defaultLastMessage": "Привет! Чем я могу тебе сегодня помочь?",
  "chat.liveSession.defaultSuggestion1": "Привет",
  "chat.liveSession.defaultSuggestion2": "Доброе утро",
  "chat.liveSession.defaultSuggestion3": "Как дела?",

  // Chat - errors
  "chat.error.sttError": "Ошибка STT: {error}. Попробуйте переключить микрофон.",
  "chat.error.autoCaptureCameraError": "Ошибка автозахвата камеры: {error}",
  "chat.error.snapshotUserError": "{error}",
  "chat.error.recordingTimeExceeded": "Запись автоматически остановлена после {maxMinutes} минут.",
  "chat.error.videoMetadataError": "Не удалось прочитать метаданные видео. Файл может быть повреждён или в неподдерживаемом формате.",
  "chat.error.pauseVideoToAnnotate": "Поставьте видео на паузу, чтобы аннотировать текущий кадр",
  "chat.error.imageGenInterrupted": "Генерация изображения была прервана.",
  "chat.error.thinkingInterrupted": "Ответ ИИ был прерван.",

  // Errors - general
  "error.noLanguagePair": "Критическая ошибка: Языковая пара не выбрана.",
  "error.translationFailed": "Перевод не удался. Пожалуйста, попробуйте снова.",
  "error.imageLimitReached": "Достигнут лимит генерации изображений сессии. Пожалуйста, начните новую сессию.",
  "error.tokenLimitReached": "Достигнут лимит токенов сессии. Пожалуйста, начните новую сессию.",
  "error.apiKeyMissing": "Ваш ключ API Gemini отсутствует. Откройте экран ключа API и вставьте свой ключ.",
  "error.apiKeyInvalid": "Ваш ключ API Gemini недействителен. Пожалуйста, проверьте на наличие опечаток и вставьте действующий ключ.",
  "error.apiQuotaExceeded": "Ваша бесплатная квота Gemini API для чата исчерпана. Я открыл экран ключа API с инструкциями по биллингу. Вы все еще можете использовать живое общение в это время.",

  // Errors - camera
  "error.cameraPermissionDenied": "Разрешение на использование камеры отклонено. Пожалуйста, включите доступ к камере в настройках вашего браузера.",
  "error.cameraNotFound": "Выбранная камера не найдена. Пожалуйста, убедитесь, что она подключена, или выберите другую камеру в настройках.",
  "error.cameraAccessNotSupported": "Доступ к камере не поддерживается вашим браузером.",
  "error.cameraUnknown": "Произошла неизвестная ошибка при доступе к камере.",
  "error.cameraStreamNotAvailable": "Поток камеры недоступен для захвата.",
  "error.imageCaptureGeneric": "Неизвестная ошибка при захвате изображения.",

  // Errors - visual context
  "error.visualContextVideoElementNotReady": "Видеоэлемент визуального контекста не готов.",
  "error.snapshotVideoElementNotReady": "Видеоэлемент для снимка не готов.",
  "error.visualContextCameraAccessNotSupported": "Доступ к камере не поддерживается для визуального контекста.",
  "error.snapshotCameraAccessNotSupported": "Доступ к камере не поддерживается для снимка.",
  "error.visualContext2DContext": "Не удалось получить 2D контекст для визуального контекста.",
  "error.snapshot2DContext": "Не удалось получить 2D контекст для снимка.",
  "error.visualContextCaptureFailedPermission": "Ошибка визуального контекста: Разрешение на использование камеры отклонено.",
  "error.snapshotCaptureFailedPermission": "Ошибка создания снимка: Разрешение на использование камеры отклонено.",
  "error.visualContextCaptureFailedNotFound": "Ошибка визуального контекста: Камера не найдена.",
  "error.snapshotCaptureFailedNotFound": "Ошибка создания снимка: Камера не найдена.",
  "error.visualContextCaptureFailedNotReady": "Ошибка визуального контекста: Камера не готова или возникла проблема с потоком. {details}",
  "error.snapshotCaptureFailedNotReady": "Ошибка создания снимка: Камера не готова или возникла проблема с потоком. {details}",
  "error.visualContextCaptureFailedGeneric": "Ошибка визуального контекста: {details}",
  "error.snapshotCaptureFailedGeneric": "Ошибка создания снимка: {details}",
};