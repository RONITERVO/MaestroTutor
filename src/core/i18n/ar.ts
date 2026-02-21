// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export const arTranslations: Record<string, string> = {
  // App title
  "app.title": "مايسترو",
  // Language selector
  "sttLang.selectLanguage": "تعيين التعرف على الكلام إلى {language}",

  // Header
  "header.targetLanguageTitle": "اللغة المستهدفة الحالية: {language}",

  // Start page
  "startPage.clickToStart": "انقر على الطائرة",
  "startPage.saveChats": "حفظ جميع المحادثات",
  "startPage.loadChats": "تحميل المحادثات",
  "startPage.saveThisChat": "حفظ هذه المحادثة",
  "startPage.appendToChat": "إلحاق بالمحادثة",
  "startPage.trimBeforeBookmark": "قص ما قبل الإشارة المرجعية",
  "startPage.maestroAvatar": "صورة مايسترو الرمزية",
  "startPage.addMaestroAvatar": "إضافة صورة مايسترو الرمزية",
  "startPage.loadSuccess": "تم تحميل واستبدال {count} جلسة محادثة بنجاح!",
  "startPage.loadError": "خطأ في تحميل المحادثات. قد يكون الملف تالفًا أو بتنسيق خاطئ.",
  "startPage.noChatsToSave": "لا يوجد سجل محادثات للحفظ.",
  "startPage.saveError": "خطأ في حفظ المحادثات. راجع وحدة التحكم للمزيد من التفاصيل.",
  "startPage.noChatSelected": "يرجى اختيار زوج لغات أولاً.",
  "startPage.noBookmarkSet": "لم يتم تعيين إشارة مرجعية. قم بتعيين واحدة أولاً لقص الرسائل التي تسبقها.",
  "startPage.noMessagesToTrim": "لا توجد رسائل قبل الإشارة المرجعية لإزالتها.",
  "startPage.trimSuccess": "تمت إزالة {count} رسالة قبل الإشارة المرجعية.",
  "startPage.trimError": "فشل قص الرسائل. يرجى المحاولة مرة أخرى.",
  "startPage.noMessagesToAppend": "لم يتم العثور على رسائل في ملف النسخ الاحتياطي لدمجها.",
  "startPage.noPairInBackup": "لا يحتوي ملف النسخ الاحتياطي على رسائل لزوج اللغة الحالي. يرجى اختيار نسخة احتياطية تتطابق مع محادثتك الحالية.",
  "startPage.appendSuccess": "تم إلحاق {count} رسالة بنجاح بالمحادثة الحالية.",
  "startPage.combineSuccess": "تم دمج المحادثات: تمت إضافة {added} رسالة جديدة، إجمالي الرسائل {total}.",
  "startPage.combineNoDuplicates": "جميع الرسائل موجودة بالفعل في محادثتك. لم يتم إجراء أي تغييرات.",
  "startPage.combineNoChanges": "لا توجد رسائل جديدة للإضافة. محادثتك لم تتغير.",
  "startPage.invalidBackupFormat": "ملف نسخ احتياطي غير صالح. يرجى اختيار ملف نسخ احتياطي Maestro (.ndjson) صالح.",
  "startPage.browserNotSupported": "متصفحك لا يدعم حفظ الملفات. يرجى استخدام Chrome أو Edge.",

  // Session Controls
  "sessionControls.saveAll.label": "حفظ الكل",
  "sessionControls.saveAll.description": "تصدير جميع المحادثات لملف نسخ احتياطي",
  "sessionControls.loadAll.label": "تحميل الكل",
  "sessionControls.loadAll.description": "استبدال جميع المحادثات بملف نسخ احتياطي",
  "sessionControls.reset.label": "إعادة تعيين",
  "sessionControls.reset.description": "نسخ احتياطي وحذف جميع البيانات",
  "sessionControls.saveThis.label": "حفظ المحادثة",
  "sessionControls.saveThis.description": "تصدير هذه المحادثة فقط",
  "sessionControls.combine.label": "دمج",
  "sessionControls.combine.description": "دمج النسخة الاحتياطية في هذه المحادثة",
  "sessionControls.trim.label": "قص",
  "sessionControls.trim.description": "إزالة الرسائل قبل الإشارة المرجعية",
  "sessionControls.profile": "الملف الشخصي:",
  "sessionControls.profilePlaceholder": "اسمك أو التفاصيل...",
  "sessionControls.editProfile": "تعديل ملف المستخدم",
  "sessionControls.allChatsControls": "عناصر تحكم جميع المحادثات",
  "sessionControls.thisChatsControls": "عناصر تحكم هذه المحادثة",
  "sessionControls.back": "رجوع",
  "sessionControls.backupAndReset": "نسخ احتياطي وإعادة تعيين",
  "sessionControls.typeToConfirm": "اكتب \"{keyword}\" للتأكيد",
  "sessionControls.changeAvatar": "تغيير الصورة الرمزية",

  // General
  "general.clear": "مسح",
  "general.error": "عذرًا، حدث خطأ.",

  // API key gate
  "apiKeyGate.title": "ربط مفتاح واجهة برمجة تطبيقات Gemini",
  "apiKeyGate.billingTitle": "إعداد الفوترة لزيادة الحصة",
  "apiKeyGate.infoLogin": "تسجيل الدخول الخاص بك: مفتاح API",
  "apiKeyGate.infoVisibility": "من يراه: أنت فقط",
  "apiKeyGate.infoBilling": "الفوترة: شهرية، من Google",
  "apiKeyGate.infoCost": "التكلفة: تظهر في التطبيق — انقر للعرض",
  "apiKeyGate.infoMore": "مزيد من المعلومات:",
  "apiKeyGate.privacyPolicy": "سياسة الخصوصية",
  "apiKeyGate.stepsTitle": "خطوتان سريعتان:",
  "apiKeyGate.stepOne": "افتح Google AI Studio وأنشئ مفتاح API.",
  "apiKeyGate.stepTwo": "الصق المفتاح أدناه واضغط على حفظ.",
  "apiKeyGate.openAiStudio": "فتح Google AI Studio",
  "apiKeyGate.viewInstructions": "عرض التعليمات",
  "apiKeyGate.closeInstructions": "إغلاق التعليمات",
  "apiKeyGate.previousInstruction": "التعليمات السابقة",
  "apiKeyGate.nextInstruction": "التعليمات التالية",
  "apiKeyGate.instructionStep": "التعليمات {step} من {total}",
  "apiKeyGate.keyLabel": "مفتاح API لـ Gemini",
  "apiKeyGate.placeholder": "الصق مفتاح API الخاص بك هنا",
  "apiKeyGate.show": "إظهار",
  "apiKeyGate.hide": "إخفاء",
  "apiKeyGate.currentKeySaved": "تم حفظ المفتاح الحالي {maskedKey}",
  "apiKeyGate.keyInvalid": "المفتاح غير صالح {maskedKey} — الصق مفتاحًا جديدًا أدناه",
  "apiKeyGate.clearSavedKey": "مسح المفتاح المحفوظ",
  "apiKeyGate.cancel": "إلغاء",
  "apiKeyGate.saving": "جارٍ الحفظ...",
  "apiKeyGate.saveKey": "حفظ المفتاح",
  "apiKeyGate.close": "إغلاق",
  "apiKeyGate.costLabel": "تكلفة API المقدرة",
  // API key gate - tester form
  "apiKeyGate.testerFormTitle": "جرب التطبيق",
  "apiKeyGate.testerFormDescription": "أدخل البريد الإلكتروني الذي تستخدمه لمتجر Google Play للحصول على دعوة للإصدار التجريبي من Android!",
  "apiKeyGate.testerFormSubmit": "احصل على وصول مبكر",
  "apiKeyGate.testerFormSubmitting": "جارٍ الإرسال...",
  "apiKeyGate.testerFormError": "حدث خطأ ما. يرجى المحاولة مرة أخرى.",
  "apiKeyGate.testerFormMustBeGmail": "يرجى استخدام عنوان @gmail.com لمتجر Google Play.",
  "apiKeyGate.testerFormCapReached": "تم الوصول إلى الحد الأقصى للطلبات. شكراً لك!",
  "apiKeyGate.developerLogin": "تسجيل دخول المطور",
  "apiKeyGate.submitAnotherEmail": "هل أدخلت بريداً إلكترونياً خاطئاً؟ أعد التعيين وجرب بريداً آخر.",
  "apiKeyGate.testerFormChecking": "جارٍ التحقق من حالة دعوتك...",
  "apiKeyGate.testerFormApprovedTitle": "لقد انضممت! 🎉",
  "apiKeyGate.testerFormApprovedDesc": "تمت الموافقة على بريدك الإلكتروني لبرنامج اختبار Android التجريبي.",
  "apiKeyGate.testerFormDownloadBtn": "تنزيل التطبيق من Google Play",
  "apiKeyGate.testerFormPendingTitle": "أنت على قائمة الانتظار!",
  "apiKeyGate.testerFormPendingDesc": "لقد استلمنا بريدك الإلكتروني. تستغرق معالجة الدعوة يدوياً بعض الوقت. يرجى التحقق مرة أخرى لاحقاً!",
  "apiKeyGate.checkStatusModeBtn": "هل قدمت طلباً بالفعل؟ تحقق من حالتك.",
  "apiKeyGate.submitModeBtn": "هل تحتاج إلى التقديم؟ سجل هنا.",
  "apiKeyGate.checkStatusTitle": "التحقق من حالة الدعوة",
  "apiKeyGate.checkStatusDesc": "أدخل البريد الإلكتروني الذي استخدمته للتقديم في قائمة الانتظار.",
  "apiKeyGate.checkStatusSubmit": "التحقق من الحالة",
  "apiKeyGate.testerFormNotFound": "البريد الإلكتروني غير موجود في قائمة الانتظار. يرجى التسجيل أولاً.",
  // Chat - general
  "chat.thinking": "جارٍ التفكير...",
  "chat.loadingHistory": "جارٍ تحميل سجل المحادثة...",
  "chat.loadingSuggestions": "جارٍ تحميل الاقتراحات...",
  "chat.suggestionsAriaLabel": "اقتراحات الرد",
  "chat.attachImageFromFile": "إرفاق ملف",
  "chat.removeAttachedImage": "إزالة الملف المرفق",
  "chat.sendMessage": "إرسال الرسالة",
  "chat.messageInputAriaLabel": "إدخال الرسالة",
  "chat.retrievedFromWeb": "تم استرجاعه من الويب:",
  "chat.videoNotSupported": "متصفحك لا يدعم وسم الفيديو.",
  "chat.audioNotSupported": "متصفحك لا يدعم وسم الصوت.",
  "chat.fileAttachment": "مرفق ملف",
  "chat.pdf.loading": "جارٍ تحميل PDF...",
  "chat.pdf.error": "تعذّر عرض PDF",
  "chat.pdf.pageIndicator": "الصفحة {current} من {total}",
  "chat.imageGenError": "خطأ في إنشاء الصورة",
  "chat.generatingImageLoadingSlow": "يستغرق وقتًا أطول قليلاً...",
  "chat.stopSpeaking": "إيقاف الكلام",
  "chat.speakThisLine": "نطق هذا السطر",
  "chat.languageSelector.openGlobe": "تغيير اللغات",
  "chat.maestroTranscriptScrollwheel": "عرض التمرير لنص مايسترو",

  // Chat - mic/STT
  "chat.mic.listening": "STT نشط: جارٍ الاستماع...",
  "chat.mic.enableStt": "تفعيل STT",
  "chat.mic.disableStt": "إيقاف STT",
  "chat.mic.recordingAudioNote": "جارٍ تسجيل الصوت...",

  // Chat - placeholders
  "chat.placeholder.normal.listening": "جارٍ الاستماع بـ{language}...",
  "chat.placeholder.normal.sttActive": "تحدث بـ{language} أو اكتب...",
  "chat.placeholder.normal.sttInactive": "اكتب أو انقر على الميكروفون للتحدث بـ{language}...",
  "chat.placeholder.suggestion.listening": "تحدث بـ{language} للترجمة...",
  "chat.placeholder.suggestion.sttActive": "تحدث أو اكتب بـ{language} للترجمة...",
  "chat.placeholder.suggestion.sttInactive": "اكتب بـ{language} للترجمة...",

  // Chat - camera
  "chat.camera.turnOn": "تشغيل معاينة الكاميرا",
  "chat.camera.turnOff": "إيقاف معاينة الكاميرا",
  "chat.camera.selectCamera": "اختر الكاميرا",
  "chat.camera.imageGenCameraLabel": "إنشاء الصورة",
  "chat.camera.captureOrRecord": "انقر للصورة، اضغط مطولاً للفيديو",
  "chat.camera.stopRecording": "إيقاف التسجيل",
  "chat.bookIcon.toggleImageGen": "تبديل وضع إنشاء الصورة",

  // Chat - image
  "chat.imagePreview.alt": "معاينة",
  "chat.image.dragToEnlarge": "اسحب الزاوية للتكبير",
  "chat.image.dragToShrink": "اسحب الزاوية للتصغير",
  "chat.annotateImage": "إضافة تعليق على الصورة",
  "chat.annotateVideoFrame": "إضافة تعليق على الإطار الحالي",

  // Chat - annotate modal
  "chat.annotateModal.editingPreviewAlt": "صورة للتعليق عليها",
  "chat.annotateModal.cancel": "إلغاء",
  "chat.annotateModal.saveAndAttach": "حفظ وإرفاق",
  "chat.annotateModal.undo": "تراجع",

  // Chat - suggestions
  "chat.suggestion.speak": "قل: \"{suggestion}\"",
  "chat.suggestion.ariaLabel": "نطق الاقتراح: {suggestion}",
  "chat.suggestion.toggleCreateMode": "تبديل وضع إنشاء الاقتراح",
  "chat.suggestion.createAction": "إنشاء اقتراح",
  "chat.suggestion.creating": "جارٍ إنشاء الاقتراح...",

  // Chat - maestro status
  "chat.maestro.idle": "مايسترو خامل",
  "chat.maestro.title.idle": "مايسترو خامل حاليًا.",
  "chat.maestro.resting": "مايسترو يرتاح...",
  "chat.maestro.observing": "مايسترو يراقب...",
  "chat.maestro.aboutToEngage": "مايسترو على وشك المشاركة...",
  "chat.maestro.title.resting": "مايسترو خامل، وقت طويل قبل إعادة المشاركة.",
  "chat.maestro.title.observing": "مايسترو يراقب، بعض الوقت قبل إعادة المشاركة.",
  "chat.maestro.title.aboutToEngage": "مايسترو سيعيد المشاركة قريبًا.",
  "chat.maestro.typing": "مايسترو يكتب...",
  "chat.maestro.title.typing": "مايسترو يحضّر ردًا.",
  "chat.maestro.speaking": "مايسترو يتحدث",
  "chat.maestro.title.speaking": "مايسترو يتحدث الآن.",
  "chat.maestro.listening": "جارٍ الاستماع...",
  "chat.maestro.title.listening": "مايسترو ينتظر إدخالك أو صوتك.",
  "chat.maestro.holding": "مايسترو في الانتظار",
  "chat.maestro.title.holding": "مايسترو في الانتظار (إعادة المشاركة متوقفة)",

  // Chat - bookmark
  "chat.bookmark.hiddenHeaderAria": "رسائل مخفية أعلاه",
  "chat.bookmark.isHere": "الإشارة المرجعية هنا",
  "chat.bookmark.setHere": "تعيين إشارة مرجعية هنا",
  "chat.bookmark.actionsRegionAria": "إجراءات الإشارة المرجعية",
  "chat.bookmark.actionsToggleTitle": "خيارات الإشارة المرجعية",
  "chat.bookmark.decrementAria": "عرض واحد أقل",
  "chat.bookmark.decrementTitle": "أقل",
  "chat.bookmark.incrementAria": "عرض واحد إضافي",
  "chat.bookmark.incrementTitle": "أكثر",
  "chat.bookmark.hiddenBelowHeaderAria": "رسائل مخفية أدناه",

  // Chat - send preparation
  "chat.sendPrep.optimizingVideo": "جارٍ تحسين الفيديو...",
  "chat.sendPrep.optimizingImage": "جارٍ تحسين الصورة...",
  "chat.sendPrep.preparingMedia": "جارٍ تحضير الوسائط...",
  "chat.sendPrep.uploadingMedia": "جارٍ رفع الوسائط...",
  "chat.sendPrep.finalizing": "جارٍ الإنهاء...",

  // Chat - header activity tokens
  "chat.header.annotating": "إضافة تعليق",
  "chat.header.recordingAudio": "تسجيل الصوت",
  "chat.header.recordingVideo": "تسجيل الفيديو",
  "chat.header.savePopup": "جارٍ الحفظ...",
  "chat.header.loadPopup": "جارٍ التحميل...",
  "chat.header.maestroAvatar": "تحديث صورة مايسترو الرمزية",
  "chat.header.watchingVideo": "مشاهدة الفيديو",
  "chat.header.viewingAbove": "عرض الرسائل السابقة",
  "chat.header.liveSession": "جلسة مباشرة",

  // Chat - live session
  "chat.liveSession.stop": "إيقاف البث المباشر",
  "chat.liveSession.retry": "إعادة محاولة البث المباشر",
  "chat.liveSession.start": "بدء البث المباشر",
  "chat.liveSession.liveBadge": "مباشر",
  "chat.liveSession.connecting": "جارٍ الاتصال",
  "chat.liveSession.defaultLastMessage": "مرحباً! كيف يمكنني مساعدتك اليوم؟",
  "chat.liveSession.defaultSuggestion1": "مرحباً",
  "chat.liveSession.defaultSuggestion2": "صباح الخير",
  "chat.liveSession.defaultSuggestion3": "كيف حالك؟",

  // Chat - errors
  "chat.error.sttError": "خطأ STT: {error}. حاول تبديل الميكروفون.",
  "chat.error.autoCaptureCameraError": "خطأ الالتقاط التلقائي للكاميرا: {error}",
  "chat.error.snapshotUserError": "{error}",
  "chat.error.recordingTimeExceeded": "توقف التسجيل تلقائيًا بعد {maxMinutes} دقيقة.",
  "chat.error.videoMetadataError": "تعذر قراءة بيانات الفيديو الوصفية. قد يكون الملف تالفًا أو بتنسيق غير مدعوم.",
  "chat.error.pauseVideoToAnnotate": "أوقف الفيديو مؤقتًا لإضافة تعليق على الإطار الحالي",
  "chat.error.imageGenInterrupted": "تم مقاطعة إنشاء الصورة.",
  "chat.error.thinkingInterrupted": "تم مقاطعة رد الذكاء الاصطناعي.",

  // Errors - general
  "error.noLanguagePair": "خطأ حرج: لم يتم اختيار زوج لغات.",
  "error.translationFailed": "فشلت الترجمة. يرجى المحاولة مرة أخرى.",
  "error.imageLimitReached": "تم الوصول إلى حد إنشاء الصور للجلسة. يرجى بدء جلسة جديدة.",
  "error.tokenLimitReached": "تم الوصول إلى حد الرموز للجلسة. يرجى بدء جلسة جديدة.",
  "error.apiKeyMissing": "مفتاح Gemini API الخاص بك مفقود. افتح شاشة مفتاح API والصق مفتاحك.",
  "error.apiKeyInvalid": "مفتاح Gemini API الخاص بك غير صالح. يرجى التحقق من وجود أخطاء إملائية ولصق مفتاح صالح.",
  "error.apiQuotaExceeded": "لقد استنفدت حصتك (المدفوع: دردشة أذكى، فيديو، pdf، وتوليد الصور)",
  "error.quotaSetupBilling": "إعداد الفوترة",
  "error.quotaStartLive": "بدء الإصدار المجاني المباشر (بدون توليد صور)",
  "error.imageGenCostWarning": "تبلغ تكلفة توليد الصور حوالي 0.04 دولار لكل صورة. للرسائل النصية أيضًا تكلفة صغيرة. يتم فوترة الجميع بواسطة Google. يمكنك تتبع التكاليف في شاشة مفتاح API.",
  "error.imageGenDisable": "تعطيل توليد الصور",
  "error.imageGenViewCost": "تتبع التكاليف في شاشة مفتاح API.",
  // Errors - camera
  "error.cameraPermissionDenied": "تم رفض إذن الكاميرا. يرجى تمكين الوصول إلى الكاميرا في إعدادات المتصفح.",
  "error.cameraNotFound": "لم يتم العثور على الكاميرا المحددة. تأكد من توصيلها أو اختر كاميرا مختلفة.",
  "error.cameraAccessNotSupported": "الوصول إلى الكاميرا غير مدعوم من متصفحك.",
  "error.cameraUnknown": "حدث خطأ غير معروف أثناء الوصول إلى الكاميرا.",
  "error.cameraStreamNotAvailable": "بث الكاميرا غير متاح للالتقاط.",
  "error.imageCaptureGeneric": "خطأ غير معروف أثناء التقاط الصورة.",

  // Errors - visual context
  "error.visualContextVideoElementNotReady": "عنصر فيديو السياق المرئي غير جاهز.",
  "error.snapshotVideoElementNotReady": "عنصر فيديو اللقطة غير جاهز.",
  "error.visualContextCameraAccessNotSupported": "الوصول إلى الكاميرا غير مدعوم للسياق المرئي.",
  "error.snapshotCameraAccessNotSupported": "الوصول إلى الكاميرا غير مدعوم للقطة.",
  "error.visualContext2DContext": "تعذر الحصول على سياق 2D للسياق المرئي.",
  "error.snapshot2DContext": "تعذر الحصول على سياق 2D للقطة.",
  "error.visualContextCaptureFailedPermission": "فشل السياق المرئي: تم رفض إذن الكاميرا.",
  "error.snapshotCaptureFailedPermission": "فشلت اللقطة: تم رفض إذن الكاميرا.",
  "error.visualContextCaptureFailedNotFound": "فشل السياق المرئي: لم يتم العثور على الكاميرا.",
  "error.snapshotCaptureFailedNotFound": "فشلت اللقطة: لم يتم العثور على الكاميرا.",
  "error.visualContextCaptureFailedNotReady": "فشل السياق المرئي: الكاميرا غير جاهزة أو مشكلة في البث. {details}",
  "error.snapshotCaptureFailedNotReady": "فشلت اللقطة: الكاميرا غير جاهزة أو مشكلة في البث. {details}",
  "error.visualContextCaptureFailedGeneric": "فشل السياق المرئي: {details}",
  "error.snapshotCaptureFailedGeneric": "فشلت اللقطة: {details}",
};