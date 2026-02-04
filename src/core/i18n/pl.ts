// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export const plTranslations: Record<string, string> = {
  // App title
  "app.title": "Maestro",
  // Language selector (used in SttLanguageSelector)
  "sttLang.selectLanguage": "Ustaw rozpoznawanie mowy na {language}",

  // Header
  "header.targetLanguageTitle": "Aktualny język docelowy: {language}",

  // Start page (used)
  "startPage.clickToStart": "Kliknij samolot",
  "startPage.saveChats": "Zapisz wszystkie czaty",
  "startPage.loadChats": "Wczytaj czaty",
  "startPage.saveThisChat": "Zapisz ten czat",
  "startPage.appendToChat": "Dołącz do czatu",
  "startPage.trimBeforeBookmark": "Przytnij przed zakładką",
  "startPage.maestroAvatar": "Awatar Maestro",
  "startPage.addMaestroAvatar": "Dodaj awatar Maestro",
  "startPage.loadSuccess": "Pomyślnie wczytano i zastąpiono {count} sesji czatu!",
  "startPage.loadError": "Błąd wczytywania czatów. Plik może być uszkodzony lub w nieprawidłowym formacie.",
  "startPage.noChatsToSave": "Brak historii czatów do zapisania.",
  "startPage.saveError": "Błąd zapisywania czatów. Sprawdź konsolę, aby uzyskać więcej szczegółów.",
  "startPage.noChatSelected": "Proszę najpierw wybrać parę językową.",
  "startPage.noBookmarkSet": "Nie ustawiono zakładki. Ustaw najpierw zakładkę, aby usunąć wiadomości przed nią.",
  "startPage.noMessagesToTrim": "Brak wiadomości przed zakładką do usunięcia.",
  "startPage.trimSuccess": "Usunięto {count} wiadomości przed zakładką.",
  "startPage.trimError": "Nie udało się przyciąć wiadomości. Spróbuj ponownie.",
  "startPage.noMessagesToAppend": "W pliku kopii zapasowej nie znaleziono żadnych wiadomości do połączenia.",
  "startPage.noPairInBackup": "Plik kopii zapasowej nie zawiera wiadomości dla aktualnej pary językowej. Wybierz kopię zapasową pasującą do bieżącego czatu.",
  "startPage.appendSuccess": "Pomyślnie dołączono {count} wiadomości do bieżącego czatu.",
  "startPage.combineSuccess": "Połączono czaty: dodano {added} nowych wiadomości, łącznie {total} wiadomości.",
  "startPage.combineNoDuplicates": "Wszystkie wiadomości znajdowały się już w Twoim czacie. Nie wprowadzono żadnych zmian.",
  "startPage.combineNoChanges": "Brak nowych wiadomości do dodania. Twój czat pozostał bez zmian.",
  "startPage.invalidBackupFormat": "Nieprawidłowy plik kopii zapasowej. Proszę wybrać prawidłowy plik kopii zapasowej Maestro (.ndjson).",
  "startPage.browserNotSupported": "Twoja przeglądarka nie obsługuje zapisywania plików. Użyj Chrome lub Edge.",

  // Session Controls - Action labels and descriptions
  "sessionControls.saveAll.label": "Zapisz wszystko",
  "sessionControls.saveAll.description": "Eksportuj wszystkie czaty do pliku kopii zapasowej",
  "sessionControls.loadAll.label": "Wczytaj wszystko",
  "sessionControls.loadAll.description": "Zastąp wszystkie czaty plikiem kopii zapasowej",
  "sessionControls.reset.label": "Resetuj",
  "sessionControls.reset.description": "Utwórz kopię i usuń wszystkie dane",
  "sessionControls.saveThis.label": "Zapisz czat",
  "sessionControls.saveThis.description": "Eksportuj tylko ten czat",
  "sessionControls.combine.label": "Połącz",
  "sessionControls.combine.description": "Scal kopię zapasową z tym czatem",
  "sessionControls.trim.label": "Przytnij",
  "sessionControls.trim.description": "Usuń wiadomości przed zakładką",

  // Session Controls - UI elements
  "sessionControls.profile": "Profil:",
  "sessionControls.profilePlaceholder": "Twoje imię lub dane...",
  "sessionControls.editProfile": "Edytuj profil użytkownika",
  "sessionControls.allChatsControls": "Sterowanie wszystkimi czatami",
  "sessionControls.thisChatsControls": "Sterowanie tym czatem",
  "sessionControls.back": "Wstecz",
  "sessionControls.backupAndReset": "Kopia zapasowa i reset",
  "sessionControls.typeToConfirm": "Wpisz „{keyword}”, aby potwierdzić",
  "sessionControls.changeAvatar": "Zmień awatar",

  // General
  "general.clear": "Wyczyść",
  "general.error": "Przepraszam, wystąpił błąd.",

  // API key gate
  "apiKeyGate.title": "Podłącz klucz API Gemini",
  "apiKeyGate.billingTitle": "Skonfiguruj rozliczenia dla wyższego limitu",
  "apiKeyGate.subtitle": "Ta aplikacja działa w całości na Twoim urządzeniu. Twój klucz nigdy nie trafia na nasze serwery.",
  "apiKeyGate.privacyPolicy": "Polityka prywatności",
  "apiKeyGate.stepsTitle": "Dwa szybkie kroki:",
  "apiKeyGate.stepOne": "Otwórz Google AI Studio i utwórz klucz API.",
  "apiKeyGate.stepTwo": "Wklej klucz poniżej i stuknij Zapisz.",
  "apiKeyGate.openAiStudio": "Otwórz Google AI Studio",
  "apiKeyGate.viewInstructions": "Wyświetl instrukcje",
  "apiKeyGate.closeInstructions": "Zamknij instrukcje",
  "apiKeyGate.previousInstruction": "Poprzednia instrukcja",
  "apiKeyGate.nextInstruction": "Następna instrukcja",
  "apiKeyGate.instructionStep": "Instrukcja {step} z {total}",
  "apiKeyGate.keyLabel": "Klucz API Gemini",
  "apiKeyGate.placeholder": "Wklej tutaj swój klucz API",
  "apiKeyGate.show": "Pokaż",
  "apiKeyGate.hide": "Ukryj",
  "apiKeyGate.currentKeySaved": "Bieżący klucz zapisany {maskedKey}",
  "apiKeyGate.clearSavedKey": "Wyczyść zapisany klucz",
  "apiKeyGate.cancel": "Anuluj",
  "apiKeyGate.saving": "Zapisywanie...",
  "apiKeyGate.saveKey": "Zapisz klucz",
  "apiKeyGate.close": "Zamknij",

  // Chat - general
  "chat.thinking": "Myślę...",
  "chat.loadingHistory": "Wczytywanie historii czatu...",
  "chat.loadingSuggestions": "Wczytywanie sugestii...",
  "chat.suggestionsAriaLabel": "Sugestie odpowiedzi",
  "chat.attachImageFromFile": "Załącz plik",
  "chat.removeAttachedImage": "Usuń załączony plik",
  "chat.sendMessage": "Wyślij wiadomość",
  "chat.messageInputAriaLabel": "Pole wiadomości",
  "chat.retrievedFromWeb": "Pobrane z sieci:",
  "chat.videoNotSupported": "Twoja przeglądarka nie obsługuje tagu wideo.",
  "chat.audioNotSupported": "Twoja przeglądarka nie obsługuje tagu audio.",
  "chat.fileAttachment": "Załącznik",
  "chat.imageGenError": "Błąd generowania obrazu",
  "chat.generatingImageLoadingSlow": "To trwa trochę dłużej...",
  "chat.stopSpeaking": "Przestań mówić",
  "chat.speakThisLine": "Wypowiedz tę linię",
  "chat.languageSelector.openGlobe": "Zmień języki",
  "chat.maestroTranscriptScrollwheel": "Widok przewijania transkrypcji Maestro",

  // Chat - mic/STT
  "chat.mic.listening": "STT aktywne: Słucham...",
  "chat.mic.enableStt": "Włącz STT",
  "chat.mic.disableStt": "Zatrzymaj STT",
  "chat.mic.recordingAudioNote": "Nagrywanie dźwięku...",

  // Chat - placeholders
  "chat.placeholder.normal.listening": "Słucham w {language}...",
  "chat.placeholder.normal.sttActive": "Mów po {language} lub pisz...",
  "chat.placeholder.normal.sttInactive": "Pisz lub dotknij mikrofonu, aby mówić po {language}...",
  "chat.placeholder.suggestion.listening": "Mów po {language}, aby przetłumaczyć...",
  "chat.placeholder.suggestion.sttActive": "Mów lub pisz po {language}, aby przetłumaczyć...",
  "chat.placeholder.suggestion.sttInactive": "Pisz po {language}, aby przetłumaczyć...",

  // Chat - camera
  "chat.camera.turnOn": "Włącz podgląd kamery",
  "chat.camera.turnOff": "Wyłącz podgląd kamery",
  "chat.camera.imageGenCameraLabel": "Generowanie obrazu",
  "chat.camera.captureOrRecord": "Dotknij, aby zrobić zdjęcie, przytrzymaj, aby nagrać wideo",
  "chat.camera.stopRecording": "Zatrzymaj nagrywanie",
  "chat.bookIcon.toggleImageGen": "Przełącz tryb generowania obrazu",

  // Chat - image
  "chat.imagePreview.alt": "Podgląd",
  "chat.image.dragToEnlarge": "Przeciągnij róg, aby powiększyć",
  "chat.image.dragToShrink": "Przeciągnij róg, aby zmniejszyć",
  "chat.annotateImage": "Dodaj adnotację do obrazu",
  "chat.annotateVideoFrame": "Dodaj adnotację do bieżącej klatki",

  // Chat - annotate modal
  "chat.annotateModal.editingPreviewAlt": "Obraz do adnotacji",
  "chat.annotateModal.cancel": "Anuluj",
  "chat.annotateModal.saveAndAttach": "Zapisz i załącz",
  "chat.annotateModal.undo": "Cofnij",

  // Chat - suggestions
  "chat.suggestion.speak": "Powiedz: „{suggestion}”",
  "chat.suggestion.ariaLabel": "Wypowiedz sugestię: {suggestion}",
  "chat.suggestion.toggleCreateMode": "Przełącz tryb tworzenia sugestii",
  "chat.suggestion.createAction": "Utwórz sugestię",
  "chat.suggestion.creating": "Tworzenie sugestii...",

  // Chat - maestro status (used via CollapsedMaestroStatus)
  "chat.maestro.idle": "Maestro jest bezczynny",
  "chat.maestro.title.idle": "Maestro jest obecnie bezczynny.",
  "chat.maestro.resting": "Maestro odpoczywa...",
  "chat.maestro.observing": "Maestro obserwuje...",
  "chat.maestro.aboutToEngage": "Maestro za chwilę się włączy...",
  "chat.maestro.title.resting": "Maestro jest bezczynny, dużo czasu do ponownego włączenia.",
  "chat.maestro.title.observing": "Maestro obserwuje, trochę czasu do ponownego włączenia.",
  "chat.maestro.title.aboutToEngage": "Maestro wkrótce się ponownie włączy.",
  "chat.maestro.typing": "Maestro pisze...",
  "chat.maestro.title.typing": "Maestro przygotowuje odpowiedź.",
  "chat.maestro.speaking": "Maestro mówi",
  "chat.maestro.title.speaking": "Maestro obecnie mówi.",
  "chat.maestro.listening": "Słucham...",
  "chat.maestro.title.listening": "Maestro czeka na Twój wpis lub głos.",
  "chat.maestro.holding": "Maestro czeka",
  "chat.maestro.title.holding": "Maestro czeka (ponowne włączenie wstrzymane)",

  // Chat - bookmark (used)
  "chat.bookmark.hiddenHeaderAria": "Ukryte wiadomości powyżej",
  "chat.bookmark.isHere": "Zakładka jest tutaj",
  "chat.bookmark.setHere": "Ustaw zakładkę tutaj",
  "chat.bookmark.actionsRegionAria": "Akcje zakładki",
  "chat.bookmark.actionsToggleTitle": "Opcje zakładki",
  "chat.bookmark.decrementAria": "Pokaż o jeden mniej",
  "chat.bookmark.decrementTitle": "Mniej",
  "chat.bookmark.incrementAria": "Pokaż o jeden więcej",
  "chat.bookmark.incrementTitle": "Więcej",
  "chat.bookmark.hiddenBelowHeaderAria": "Ukryte wiadomości poniżej",

  // Chat - send preparation
  "chat.sendPrep.optimizingVideo": "Optymalizacja wideo...",
  "chat.sendPrep.optimizingImage": "Optymalizacja obrazu...",
  "chat.sendPrep.preparingMedia": "Przygotowywanie mediów...",
  "chat.sendPrep.uploadingMedia": "Przesyłanie mediów...",
  "chat.sendPrep.finalizing": "Finalizowanie...",

  // Chat - header activity tokens (used via activityTokens.ts)
  "chat.header.annotating": "Dodawanie adnotacji",
  "chat.header.recordingAudio": "Nagrywanie dźwięku",
  "chat.header.recordingVideo": "Nagrywanie wideo",
  "chat.header.savePopup": "Zapisywanie...",
  "chat.header.loadPopup": "Wczytywanie...",
  "chat.header.maestroAvatar": "Aktualizowanie awatara Maestro",
  "chat.header.watchingVideo": "Oglądanie wideo",
  "chat.header.viewingAbove": "Przeglądanie wiadomości powyżej",
  "chat.header.liveSession": "Sesja na żywo",

  // Chat - live session
  "chat.liveSession.stop": "Zatrzymaj na żywo",
  "chat.liveSession.retry": "Ponów na żywo",
  "chat.liveSession.start": "Rozpocznij na żywo",
  "chat.liveSession.liveBadge": "Na żywo",
  "chat.liveSession.connecting": "Łączenie",
  "chat.liveSession.defaultLastMessage": "Cześć! Jak mogę Ci dzisiaj pomóc?",
  "chat.liveSession.defaultSuggestion1": "Cześć",
  "chat.liveSession.defaultSuggestion2": "Dzień dobry",
  "chat.liveSession.defaultSuggestion3": "Jak się masz?",

  // Chat - errors
  "chat.error.sttError": "Błąd STT: {error}. Spróbuj przełączyć mikrofon.",
  "chat.error.autoCaptureCameraError": "Błąd automatycznego przechwytywania kamery: {error}",
  "chat.error.snapshotUserError": "{error}",
  "chat.error.recordingTimeExceeded": "Nagrywanie zatrzymane automatycznie po {maxMinutes} minutach.",
  "chat.error.videoMetadataError": "Nie można odczytać metadanych wideo. Plik może być uszkodzony lub w nieobsługiwanym formacie.",
  "chat.error.pauseVideoToAnnotate": "Wstrzymaj wideo, aby dodać adnotację do bieżącej klatki",
  "chat.error.imageGenInterrupted": "Generowanie obrazu zostało przerwane.",
  "chat.error.thinkingInterrupted": "Odpowiedź AI została przerwana.",

  // Errors - general
  "error.noLanguagePair": "Błąd krytyczny: Nie wybrano pary języków.",
  "error.translationFailed": "Tłumaczenie nie powiodło się. Spróbuj ponownie.",
  "error.imageLimitReached": "Osiągnięto limit generowania obrazów sesji. Rozpocznij nową sesję.",
  "error.tokenLimitReached": "Osiągnięto limit tokenów sesji. Rozpocznij nową sesję.",
  "error.apiKeyMissing": "Brak klucza API Gemini. Otwórz ekran klucza API i wklej swój klucz.",
  "error.apiKeyInvalid": "Klucz API Gemini jest nieprawidłowy. Sprawdź, czy nie ma literówek i wklej prawidłowy klucz.",
  "error.apiQuotaExceeded": "Twój bezpłatny limit Gemini API na czat został wyczerpany. Otworzyłem ekran klucza API z krokami rozliczeniowymi. W międzyczasie nadal możesz korzystać z rozmowy na żywo.",

  // Errors - camera
  "error.cameraPermissionDenied": "Odmówiono dostępu do kamery. Włącz dostęp do kamery w ustawieniach przeglądarki.",
  "error.cameraNotFound": "Nie znaleziono wybranej kamery. Upewnij się, że jest podłączona lub wybierz inną kamerę w ustawieniach.",
  "error.cameraAccessNotSupported": "Dostęp do kamery nie jest obsługiwany przez Twoją przeglądarkę.",
  "error.cameraUnknown": "Wystąpił nieznany błąd podczas dostępu do kamery.",
  "error.cameraStreamNotAvailable": "Strumień kamery niedostępny do przechwycenia.",
  "error.imageCaptureGeneric": "Nieznany błąd podczas przechwytywania obrazu.",

  // Errors - visual context (dynamically constructed with prefix)
  "error.visualContextVideoElementNotReady": "Element wideo kontekstu wizualnego nie jest gotowy.",
  "error.snapshotVideoElementNotReady": "Element wideo dla zrzutu ekranu nie jest gotowy.",
  "error.visualContextCameraAccessNotSupported": "Dostęp do kamery nie jest obsługiwany dla kontekstu wizualnego.",
  "error.snapshotCameraAccessNotSupported": "Dostęp do kamery nie jest obsługiwany dla zrzutu ekranu.",
  "error.visualContext2DContext": "Nie można uzyskać kontekstu 2D dla kontekstu wizualnego.",
  "error.snapshot2DContext": "Nie można uzyskać kontekstu 2D dla zrzutu ekranu.",
  "error.visualContextCaptureFailedPermission": "Kontekst wizualny nie powiódł się: Odmówiono dostępu do kamery.",
  "error.snapshotCaptureFailedPermission": "Zrzut ekranu nie powiódł się: Odmówiono dostępu do kamery.",
  "error.visualContextCaptureFailedNotFound": "Kontekst wizualny nie powiódł się: Nie znaleziono kamery.",
  "error.snapshotCaptureFailedNotFound": "Zrzut ekranu nie powiódł się: Nie znaleziono kamery.",
  "error.visualContextCaptureFailedNotReady": "Kontekst wizualny nie powiódł się: Kamera nie jest gotowa lub problem ze strumieniem. {details}",
  "error.snapshotCaptureFailedNotReady": "Zrzut ekranu nie powiódł się: Kamera nie jest gotowa lub problem ze strumieniem. {details}",
  "error.visualContextCaptureFailedGeneric": "Kontekst wizualny nie powiódł się: {details}",
  "error.snapshotCaptureFailedGeneric": "Zrzut ekranu nie powiódł się: {details}",
};