// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export const trTranslations: Record<string, string> = {
  // App title
  "app.title": "Maestro",
  // Language selector (used in SttLanguageSelector)
  "sttLang.selectLanguage": "Konuşma tanımayı {language} olarak ayarla",

  // Header
  "header.targetLanguageTitle": "Mevcut Hedef Dil: {language}",

  // Start page (used)
  "startPage.clickToStart": "Uçağa tıklayın",
  "startPage.saveChats": "Tüm Sohbetleri Kaydet",
  "startPage.loadChats": "Sohbetleri Yükle",
  "startPage.saveThisChat": "Bu Sohbeti Kaydet",
  "startPage.appendToChat": "Sohbete Ekle",
  "startPage.trimBeforeBookmark": "Yer İminden Öncekileri Kırp",
  "startPage.maestroAvatar": "Maestro avatarı",
  "startPage.addMaestroAvatar": "Maestro avatarı ekle",
  "startPage.loadSuccess": "{count} sohbet oturumu başarıyla yüklendi ve değiştirildi!",
  "startPage.loadError": "Sohbetler yüklenirken hata oluştu. Dosya bozuk veya yanlış formatta olabilir.",
  "startPage.noChatsToSave": "Kaydedilecek sohbet geçmişi yok.",
  "startPage.saveError": "Sohbetler kaydedilirken hata oluştu. Ayrıntılar için konsola bakın.",
  "startPage.noChatSelected": "Lütfen önce bir dil çifti seçin.",
  "startPage.noBookmarkSet": "Yer imi ayarlanmadı. Önceki mesajları silmek için önce bir yer imi belirleyin.",
  "startPage.noMessagesToTrim": "Yer iminden önce silinecek mesaj yok.",
  "startPage.trimSuccess": "Yer iminden önceki {count} mesaj kaldırıldı.",
  "startPage.trimError": "Mesajlar kırpılamadı. Lütfen tekrar deneyin.",
  "startPage.noMessagesToAppend": "Yedek dosyasında birleştirilecek mesaj bulunamadı.",
  "startPage.noPairInBackup": "Yedek dosyası mevcut dil çiftiniz için mesaj içermiyor. Lütfen mevcut sohbetinizle eşleşen bir yedek seçin.",
  "startPage.appendSuccess": "{count} mesaj mevcut sohbete başarıyla eklendi.",
  "startPage.combineSuccess": "Sohbetler birleştirildi: {added} yeni mesaj eklendi, toplam {total} mesaj.",
  "startPage.combineNoDuplicates": "Tüm mesajlar zaten sohbetinizde mevcuttu. Değişiklik yapılmadı.",
  "startPage.combineNoChanges": "Eklenecek yeni mesaj yok. Sohbetiniz değişmedi.",
  "startPage.invalidBackupFormat": "Geçersiz yedek dosyası. Lütfen geçerli bir Maestro yedekleme (.ndjson) dosyası seçin.",
  "startPage.browserNotSupported": "Tarayıcınız dosya kaydetmeyi desteklemiyor. Lütfen Chrome veya Edge kullanın.",

  // Session Controls - Action labels and descriptions
  "sessionControls.saveAll.label": "Tümünü Kaydet",
  "sessionControls.saveAll.description": "Tüm sohbetleri yedek dosyasına aktar",
  "sessionControls.loadAll.label": "Tümünü Yükle",
  "sessionControls.loadAll.description": "Tüm sohbetleri yedek dosyasıyla değiştir",
  "sessionControls.reset.label": "Sıfırla",
  "sessionControls.reset.description": "Yedekle ve tüm verileri sil",
  "sessionControls.saveThis.label": "Sohbeti Kaydet",
  "sessionControls.saveThis.description": "Sadece bu sohbeti aktar",
  "sessionControls.combine.label": "Birleştir",
  "sessionControls.combine.description": "Yedeği bu sohbete dahil et",
  "sessionControls.trim.label": "Kırp",
  "sessionControls.trim.description": "Yer iminden önceki mesajları kaldır",

  // Session Controls - UI elements
  "sessionControls.profile": "Profil:",
  "sessionControls.profilePlaceholder": "Adınız veya detaylarınız...",
  "sessionControls.editProfile": "Kullanıcı Profilini Düzenle",
  "sessionControls.allChatsControls": "Tüm Sohbet Kontrolleri",
  "sessionControls.thisChatsControls": "Bu Sohbetin Kontrolleri",
  "sessionControls.back": "Geri",
  "sessionControls.backupAndReset": "Yedekle ve Sıfırla",
  "sessionControls.typeToConfirm": "Onaylamak için \"{keyword}\" yazın",
  "sessionControls.changeAvatar": "Avatarı Değiştir",

  // General
  "general.clear": "Temizle",
  "general.error": "Üzgünüm, bir hata oluştu.",

  // API key gate
  "apiKeyGate.title": "Gemini API anahtarınızı bağlayın",
  "apiKeyGate.billingTitle": "Daha yüksek kota için faturalandırmayı ayarlayın",
  "apiKeyGate.subtitle": "Bu uygulama tamamen cihazınızda çalışır. Anahtarınız sunucularımıza asla ulaşmaz.",
  "apiKeyGate.privacyPolicy": "Gizlilik Politikası",
  "apiKeyGate.stepsTitle": "İki hızlı adım:",
  "apiKeyGate.stepOne": "Google AI Studio'yu açın ve bir API anahtarı oluşturun.",
  "apiKeyGate.stepTwo": "Aşağıya anahtarınızı yapıştırın ve Kaydet'e dokunun.",
  "apiKeyGate.openAiStudio": "Google AI Studio'yu aç",
  "apiKeyGate.viewInstructions": "Talimatları görüntüle",
  "apiKeyGate.closeInstructions": "Talimatları kapat",
  "apiKeyGate.previousInstruction": "Önceki talimat",
  "apiKeyGate.nextInstruction": "Sonraki talimat",
  "apiKeyGate.instructionStep": "Talimat {step} / {total}",
  "apiKeyGate.keyLabel": "Gemini API anahtarı",
  "apiKeyGate.placeholder": "API anahtarınızı buraya yapıştırın",
  "apiKeyGate.show": "Göster",
  "apiKeyGate.hide": "Gizle",
  "apiKeyGate.currentKeySaved": "Geçerli anahtar kaydedildi {maskedKey}",
  "apiKeyGate.clearSavedKey": "Kaydedilmiş anahtarı temizle",
  "apiKeyGate.cancel": "İptal",
  "apiKeyGate.saving": "Kaydediliyor...",
  "apiKeyGate.saveKey": "Anahtarı kaydet",
  "apiKeyGate.close": "Kapat",

  // Chat - general
  "chat.thinking": "Düşünüyor...",
  "chat.loadingHistory": "Sohbet geçmişi yükleniyor...",
  "chat.loadingSuggestions": "Öneriler yükleniyor...",
  "chat.suggestionsAriaLabel": "Yanıt önerileri",
  "chat.attachImageFromFile": "Dosya ekle",
  "chat.removeAttachedImage": "Ekli dosyayı kaldır",
  "chat.sendMessage": "Mesaj gönder",
  "chat.messageInputAriaLabel": "Mesaj girişi",
  "chat.retrievedFromWeb": "Web'den alındı:",
  "chat.videoNotSupported": "Tarayıcınız video etiketini desteklemiyor.",
  "chat.audioNotSupported": "Tarayıcınız ses etiketini desteklemiyor.",
  "chat.fileAttachment": "Dosya Eki",
  "chat.imageGenError": "Görsel Oluşturma Hatası",
  "chat.generatingImageLoadingSlow": "Biraz daha uzun sürüyor...",
  "chat.stopSpeaking": "Konuşmayı durdur",
  "chat.speakThisLine": "Bu satırı seslendir",
  "chat.languageSelector.openGlobe": "Dilleri değiştir",
  "chat.maestroTranscriptScrollwheel": "Maestro transkript kaydırma görünümü",

  // Chat - mic/STT
  "chat.mic.listening": "STT Aktif: Dinleniyor...",
  "chat.mic.enableStt": "STT'yi Etkinleştir",
  "chat.mic.disableStt": "STT'yi Durdur",
  "chat.mic.recordingAudioNote": "Ses kaydediliyor...",

  // Chat - placeholders
  "chat.placeholder.normal.listening": "{language} dilinde dinleniyor...",
  "chat.placeholder.normal.sttActive": "{language} dilinde konuşun veya yazın...",
  "chat.placeholder.normal.sttInactive": "Yazın veya {language} dilinde konuşmak için mikrofona dokunun...",
  "chat.placeholder.suggestion.listening": "Çeviri için {language} konuşun...",
  "chat.placeholder.suggestion.sttActive": "Çeviri için {language} dilinde konuşun veya yazın...",
  "chat.placeholder.suggestion.sttInactive": "Çeviri için {language} dilinde yazın...",

  // Chat - camera
  "chat.camera.turnOn": "Kamera önizlemesini etkinleştir",
  "chat.camera.turnOff": "Kamera önizlemesini kapat",
  "chat.camera.imageGenCameraLabel": "Görsel Oluşturma",
  "chat.camera.captureOrRecord": "Fotoğraf için dokunun, video için basılı tutun",
  "chat.camera.stopRecording": "Kaydı Durdur",
  "chat.bookIcon.toggleImageGen": "Görsel Oluşturma Modunu Aç/Kapat",

  // Chat - image
  "chat.imagePreview.alt": "Önizleme",
  "chat.image.dragToEnlarge": "Büyütmek için köşeyi sürükleyin",
  "chat.image.dragToShrink": "Küçültmek için köşeyi sürükleyin",
  "chat.annotateImage": "Görsele Not Ekle",
  "chat.annotateVideoFrame": "Mevcut kareye not ekle",

  // Chat - annotate modal
  "chat.annotateModal.editingPreviewAlt": "Not eklenecek görsel",
  "chat.annotateModal.cancel": "İptal",
  "chat.annotateModal.saveAndAttach": "Kaydet ve Ekle",
  "chat.annotateModal.undo": "Geri Al",

  // Chat - suggestions
  "chat.suggestion.speak": "Söyle: \"{suggestion}\"",
  "chat.suggestion.ariaLabel": "Öneriyi seslendir: {suggestion}",
  "chat.suggestion.toggleCreateMode": "Öneri oluşturma modunu aç/kapat",
  "chat.suggestion.createAction": "Öneri Oluştur",
  "chat.suggestion.creating": "Öneri oluşturuluyor...",

  // Chat - maestro status
  "chat.maestro.idle": "Maestro boşta",
  "chat.maestro.title.idle": "Maestro şu anda boşta.",
  "chat.maestro.resting": "Maestro dinleniyor...",
  "chat.maestro.observing": "Maestro gözlemliyor...",
  "chat.maestro.aboutToEngage": "Maestro katılmak üzere...",
  "chat.maestro.title.resting": "Maestro boşta, yeniden etkileşime geçmek için bolca vakit var.",
  "chat.maestro.title.observing": "Maestro gözlemliyor, etkileşime geçmek için biraz vakit var.",
  "chat.maestro.title.aboutToEngage": "Maestro yakında yeniden etkileşime geçecek.",
  "chat.maestro.typing": "Maestro yazıyor...",
  "chat.maestro.title.typing": "Maestro bir yanıt hazırlıyor.",
  "chat.maestro.speaking": "Maestro konuşuyor",
  "chat.maestro.title.speaking": "Maestro şu anda konuşuyor.",
  "chat.maestro.listening": "Dinleniyor...",
  "chat.maestro.title.listening": "Maestro girişinizi veya konuşmanızı bekliyor.",
  "chat.maestro.holding": "Maestro bekletiyor",
  "chat.maestro.title.holding": "Maestro bekletiyor (yeniden etkileşim duraklatıldı)",

  // Chat - bookmark
  "chat.bookmark.hiddenHeaderAria": "Yukarıdaki mesajlar gizli",
  "chat.bookmark.isHere": "Yer imi burada",
  "chat.bookmark.setHere": "Yer imini buraya ayarla",
  "chat.bookmark.actionsRegionAria": "Yer imi eylemleri",
  "chat.bookmark.actionsToggleTitle": "Yer imi seçenekleri",
  "chat.bookmark.decrementAria": "Bir eksik göster",
  "chat.bookmark.decrementTitle": "Daha az",
  "chat.bookmark.incrementAria": "Bir fazla göster",
  "chat.bookmark.incrementTitle": "Daha fazla",
  "chat.bookmark.hiddenBelowHeaderAria": "Aşağıdaki mesajlar gizli",

  // Chat - send preparation
  "chat.sendPrep.optimizingVideo": "Video optimize ediliyor...",
  "chat.sendPrep.optimizingImage": "Görsel optimize ediliyor...",
  "chat.sendPrep.preparingMedia": "Medya hazırlanıyor...",
  "chat.sendPrep.uploadingMedia": "Medya yükleniyor...",
  "chat.sendPrep.finalizing": "Tamamlanıyor...",

  // Chat - header activity tokens
  "chat.header.annotating": "Not ekleniyor",
  "chat.header.recordingAudio": "Ses kaydediliyor",
  "chat.header.recordingVideo": "Video kaydediliyor",
  "chat.header.savePopup": "Kaydediliyor...",
  "chat.header.loadPopup": "Yükleniyor...",
  "chat.header.maestroAvatar": "Maestro avatarı güncelleniyor",
  "chat.header.watchingVideo": "Video izleniyor",
  "chat.header.viewingAbove": "Önceki mesajlar görüntüleniyor",
  "chat.header.liveSession": "Canlı oturum",

  // Chat - live session
  "chat.liveSession.stop": "Canlıyı Durdur",
  "chat.liveSession.retry": "Canlıyı Tekrar Dene",
  "chat.liveSession.start": "Canlıyı Başlat",
  "chat.liveSession.liveBadge": "Canlı",
  "chat.liveSession.connecting": "Bağlanıyor",
  "chat.liveSession.defaultLastMessage": "Merhaba! Bugün size nasıl yardımcı olabilirim?",
  "chat.liveSession.defaultSuggestion1": "Merhaba",
  "chat.liveSession.defaultSuggestion2": "Günaydın",
  "chat.liveSession.defaultSuggestion3": "Nasılsın?",

  // Chat - errors
  "chat.error.sttError": "STT Hatası: {error}. Mikrofonu kapatıp açmayı deneyin.",
  "chat.error.autoCaptureCameraError": "Otomatik Yakalama Kamera Hatası: {error}",
  "chat.error.snapshotUserError": "{error}",
  "chat.error.recordingTimeExceeded": "Kayıt {maxMinutes} dakika sonra otomatik olarak durduruldu.",
  "chat.error.videoMetadataError": "Video meta verileri okunamadı. Dosya bozuk veya desteklenmeyen bir formatta olabilir.",
  "chat.error.pauseVideoToAnnotate": "Mevcut kareye not eklemek için videoyu duraklatın",
  "chat.error.imageGenInterrupted": "Görsel oluşturma kesintiye uğradı.",
  "chat.error.thinkingInterrupted": "Yapay zekanın yanıtı kesintiye uğradı.",

  // Errors - general
  "error.noLanguagePair": "Kritik hata: Dil çifti seçilmedi.",
  "error.translationFailed": "Çeviri başarısız oldu. Lütfen tekrar deneyin.",
  "error.imageLimitReached": "Oturum görsel oluşturma sınırına ulaşıldı. Lütfen yeni bir oturum başlatın.",
  "error.tokenLimitReached": "Oturum belirteç (token) sınırına ulaşıldı. Lütfen yeni bir oturum başlatın.",
  "error.apiKeyMissing": "Gemini API anahtarınız eksik. API Anahtarı ekranını açın ve anahtarınızı yapıştırın.",
  "error.apiKeyInvalid": "Gemini API anahtarınız geçersiz. Lütfen yazım hatalarını kontrol edin ve geçerli bir anahtar yapıştırın.",
  "error.apiQuotaExceeded": "Sohbet için ücretsiz Gemini API kotanız doldu.",
  "error.quotaSetupBilling": "Faturalandırmayı ayarlayın",
  "error.quotaStartLive": "Bunun yerine Canlı başlatın",

  // Errors - camera
  "error.cameraPermissionDenied": "Kamera izni reddedildi. Lütfen tarayıcı ayarlarından kamera erişimine izin verin.",
  "error.cameraNotFound": "Seçilen kamera bulunamadı. Bağlı olduğundan emin olun veya ayarlardan farklı bir kamera seçin.",
  "error.cameraAccessNotSupported": "Kamera erişimi tarayıcınız tarafından desteklenmiyor.",
  "error.cameraUnknown": "Kameraya erişirken bilinmeyen bir hata oluştu.",
  "error.cameraStreamNotAvailable": "Yakalama için kamera akışı mevcut değil.",
  "error.imageCaptureGeneric": "Görsel yakalama sırasında bilinmeyen hata.",

  // Errors - visual context
  "error.visualContextVideoElementNotReady": "Görsel bağlam video öğesi hazır değil.",
  "error.snapshotVideoElementNotReady": "Anlık görüntü için video öğesi hazır değil.",
  "error.visualContextCameraAccessNotSupported": "Görsel bağlam için kamera erişimi desteklenmiyor.",
  "error.snapshotCameraAccessNotSupported": "Anlık görüntü için kamera erişimi desteklenmiyor.",
  "error.visualContext2DContext": "Görsel bağlam için 2D bağlamı alınamadı.",
  "error.snapshot2DContext": "Anlık görüntü için 2D bağlamı alınamadı.",
  "error.visualContextCaptureFailedPermission": "Görsel Bağlam başarısız: Kamera izni reddedildi.",
  "error.snapshotCaptureFailedPermission": "Anlık Görüntü başarısız: Kamera izni reddedildi.",
  "error.visualContextCaptureFailedNotFound": "Görsel Bağlam başarısız: Kamera bulunamadı.",
  "error.snapshotCaptureFailedNotFound": "Anlık Görüntü başarısız: Kamera bulunamadı.",
  "error.visualContextCaptureFailedNotReady": "Görsel Bağlam başarısız: Kamera hazır değil veya akışta bir sorun var. {details}",
  "error.snapshotCaptureFailedNotReady": "Anlık Görüntü başarısız: Kamera hazır değil veya akışta bir sorun var. {details}",
  "error.visualContextCaptureFailedGeneric": "Görsel Bağlam başarısız: {details}",
  "error.snapshotCaptureFailedGeneric": "Anlık Görüntü başarısız: {details}",
};