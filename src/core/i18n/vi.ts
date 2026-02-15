// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export const viTranslations: Record<string, string> = {
  // App title
  "app.title": "Maestro",
  // Language selector (used in SttLanguageSelector)
  "sttLang.selectLanguage": "Đặt nhận dạng giọng nói thành {language}",

  // Header
  "header.targetLanguageTitle": "Ngôn ngữ mục tiêu hiện tại: {language}",

  // Start page (used)
  "startPage.clickToStart": "Nhấp vào máy bay",
  "startPage.saveChats": "Lưu tất cả cuộc trò chuyện",
  "startPage.loadChats": "Tải cuộc trò chuyện",
  "startPage.saveThisChat": "Lưu cuộc trò chuyện này",
  "startPage.appendToChat": "Thêm vào cuộc trò chuyện",
  "startPage.trimBeforeBookmark": "Cắt bớt trước dấu trang",
  "startPage.maestroAvatar": "Avatar Maestro",
  "startPage.addMaestroAvatar": "Thêm avatar Maestro",
  "startPage.loadSuccess": "Đã tải và thay thế thành công {count} phiên trò chuyện!",
  "startPage.loadError": "Lỗi khi tải cuộc trò chuyện. Tệp có thể bị hỏng hoặc sai định dạng.",
  "startPage.noChatsToSave": "Không có lịch sử trò chuyện để lưu.",
  "startPage.saveError": "Lỗi khi lưu cuộc trò chuyện. Xem console để biết thêm chi tiết.",
  "startPage.noChatSelected": "Vui lòng chọn một cặp ngôn ngữ trước.",
  "startPage.noBookmarkSet": "Chưa đặt dấu trang. Hãy đặt dấu trang trước để xóa các tin nhắn trước đó.",
  "startPage.noMessagesToTrim": "Không có tin nhắn nào trước dấu trang để xóa.",
  "startPage.trimSuccess": "Đã xóa {count} tin nhắn trước dấu trang.",
  "startPage.trimError": "Không thể cắt bớt tin nhắn. Vui lòng thử lại.",
  "startPage.noMessagesToAppend": "Không tìm thấy tin nhắn nào trong tệp sao lưu để kết hợp.",
  "startPage.noPairInBackup": "Tệp sao lưu không chứa tin nhắn cho cặp ngôn ngữ hiện tại của bạn. Vui lòng chọn tệp sao lưu khớp với cuộc trò chuyện hiện tại.",
  "startPage.appendSuccess": "Đã thêm thành công {count} tin nhắn vào cuộc trò chuyện hiện tại.",
  "startPage.combineSuccess": "Đã kết hợp cuộc trò chuyện: {added} tin nhắn mới được thêm, tổng cộng {total} tin nhắn.",
  "startPage.combineNoDuplicates": "Tất cả tin nhắn đã có trong cuộc trò chuyện của bạn. Không có thay đổi nào được thực hiện.",
  "startPage.combineNoChanges": "Không có tin nhắn mới để thêm. Cuộc trò chuyện của bạn không thay đổi.",
  "startPage.invalidBackupFormat": "Tệp sao lưu không hợp lệ. Vui lòng chọn tệp sao lưu Maestro (.ndjson) hợp lệ.",
  "startPage.browserNotSupported": "Trình duyệt của bạn không hỗ trợ lưu tệp. Vui lòng sử dụng Chrome hoặc Edge.",

  // Session Controls - Action labels and descriptions
  "sessionControls.saveAll.label": "Lưu tất cả",
  "sessionControls.saveAll.description": "Xuất tất cả cuộc trò chuyện ra tệp sao lưu",
  "sessionControls.loadAll.label": "Tải tất cả",
  "sessionControls.loadAll.description": "Thay thế tất cả cuộc trò chuyện bằng tệp sao lưu",
  "sessionControls.reset.label": "Đặt lại",
  "sessionControls.reset.description": "Sao lưu & xóa tất cả dữ liệu",
  "sessionControls.saveThis.label": "Lưu chat",
  "sessionControls.saveThis.description": "Chỉ xuất cuộc trò chuyện này",
  "sessionControls.combine.label": "Kết hợp",
  "sessionControls.combine.description": "Hợp nhất bản sao lưu vào cuộc trò chuyện này",
  "sessionControls.trim.label": "Cắt bớt",
  "sessionControls.trim.description": "Xóa tin nhắn trước dấu trang",

  // Session Controls - UI elements
  "sessionControls.profile": "Hồ sơ:",
  "sessionControls.profilePlaceholder": "Tên hoặc chi tiết của bạn...",
  "sessionControls.editProfile": "Chỉnh sửa hồ sơ người dùng",
  "sessionControls.allChatsControls": "Điều khiển tất cả trò chuyện",
  "sessionControls.thisChatsControls": "Điều khiển trò chuyện này",
  "sessionControls.back": "Quay lại",
  "sessionControls.backupAndReset": "Sao lưu & Đặt lại",
  "sessionControls.typeToConfirm": "Nhập \"{keyword}\" để xác nhận",
  "sessionControls.changeAvatar": "Thay đổi Avatar",

  // General
  "general.clear": "Xóa",
  "general.error": "Xin lỗi, tôi đã gặp lỗi.",

  // API key gate
  "apiKeyGate.title": "Kết nối khóa API Gemini của bạn",
  "apiKeyGate.billingTitle": "Thiết lập thanh toán để có hạn ngạch cao hơn",
  "apiKeyGate.subtitle": "Ứng dụng này chạy hoàn toàn trên thiết bị của bạn. Khóa của bạn không bao giờ được gửi đến máy chủ của chúng tôi.",
  "apiKeyGate.privacyPolicy": "Chính sách bảo mật",
  "apiKeyGate.stepsTitle": "Hai bước nhanh chóng:",
  "apiKeyGate.stepOne": "Mở Google AI Studio và tạo khóa API.",
  "apiKeyGate.stepTwo": "Dán khóa vào bên dưới và nhấn Lưu.",
  "apiKeyGate.openAiStudio": "Mở Google AI Studio",
  "apiKeyGate.viewInstructions": "Xem hướng dẫn",
  "apiKeyGate.closeInstructions": "Đóng hướng dẫn",
  "apiKeyGate.previousInstruction": "Hướng dẫn trước",
  "apiKeyGate.nextInstruction": "Hướng dẫn tiếp theo",
  "apiKeyGate.instructionStep": "Hướng dẫn {step}/{total}",
  "apiKeyGate.keyLabel": "Khóa API Gemini",
  "apiKeyGate.placeholder": "Dán khóa API của bạn vào đây",
  "apiKeyGate.show": "Hiện",
  "apiKeyGate.hide": "Ẩn",
  "apiKeyGate.currentKeySaved": "Khóa hiện tại đã lưu {maskedKey}",
  "apiKeyGate.clearSavedKey": "Xóa khóa đã lưu",
  "apiKeyGate.cancel": "Hủy",
  "apiKeyGate.saving": "Đang lưu...",
  "apiKeyGate.saveKey": "Lưu khóa",
  "apiKeyGate.close": "Đóng",

  // Chat - general
  "chat.thinking": "Đang suy nghĩ...",
  "chat.loadingHistory": "Đang tải lịch sử trò chuyện...",
  "chat.loadingSuggestions": "Đang tải gợi ý...",
  "chat.suggestionsAriaLabel": "Gợi ý trả lời",
  "chat.attachImageFromFile": "Đính kèm tệp",
  "chat.removeAttachedImage": "Xóa tệp đính kèm",
  "chat.sendMessage": "Gửi tin nhắn",
  "chat.messageInputAriaLabel": "Nhập tin nhắn",
  "chat.retrievedFromWeb": "Truy xuất từ web:",
  "chat.videoNotSupported": "Trình duyệt của bạn không hỗ trợ thẻ video.",
  "chat.audioNotSupported": "Trình duyệt của bạn không hỗ trợ thẻ âm thanh.",
  "chat.fileAttachment": "Tệp đính kèm",
  "chat.imageGenError": "Lỗi tạo hình ảnh",
  "chat.generatingImageLoadingSlow": "Mất nhiều thời gian hơn một chút...",
  "chat.stopSpeaking": "Dừng nói",
  "chat.speakThisLine": "Đọc dòng này",
  "chat.languageSelector.openGlobe": "Thay đổi ngôn ngữ",
  "chat.maestroTranscriptScrollwheel": "Chế độ xem cuộn bản ghi Maestro",

  // Chat - mic/STT
  "chat.mic.listening": "STT đang hoạt động: Đang nghe...",
  "chat.mic.enableStt": "Bật STT",
  "chat.mic.disableStt": "Dừng STT",
  "chat.mic.recordingAudioNote": "Đang ghi âm...",

  // Chat - placeholders
  "chat.placeholder.normal.listening": "Đang nghe bằng {language}...",
  "chat.placeholder.normal.sttActive": "Nói bằng {language} hoặc nhập...",
  "chat.placeholder.normal.sttInactive": "Nhập hoặc chạm vào mic để nói bằng {language}...",
  "chat.placeholder.suggestion.listening": "Nói {language} để dịch...",
  "chat.placeholder.suggestion.sttActive": "Nói hoặc nhập bằng {language} để dịch...",
  "chat.placeholder.suggestion.sttInactive": "Nhập bằng {language} để dịch...",

  // Chat - camera
  "chat.camera.turnOn": "Bật xem trước camera",
  "chat.camera.turnOff": "Tắt xem trước camera",
  "chat.camera.imageGenCameraLabel": "Tạo hình ảnh",
  "chat.camera.captureOrRecord": "Chạm để chụp ảnh, giữ để quay video",
  "chat.camera.stopRecording": "Dừng ghi",
  "chat.bookIcon.toggleImageGen": "Chuyển đổi chế độ tạo hình ảnh",

  // Chat - image
  "chat.imagePreview.alt": "Xem trước",
  "chat.image.dragToEnlarge": "Kéo góc để phóng to",
  "chat.image.dragToShrink": "Kéo góc để thu nhỏ",
  "chat.annotateImage": "Chú thích hình ảnh",
  "chat.annotateVideoFrame": "Chú thích khung hình hiện tại",

  // Chat - annotate modal
  "chat.annotateModal.editingPreviewAlt": "Hình ảnh cần chú thích",
  "chat.annotateModal.cancel": "Hủy",
  "chat.annotateModal.saveAndAttach": "Lưu và đính kèm",
  "chat.annotateModal.undo": "Hoàn tác",

  // Chat - suggestions
  "chat.suggestion.speak": "Nói: \"{suggestion}\"",
  "chat.suggestion.ariaLabel": "Đọc gợi ý: {suggestion}",
  "chat.suggestion.toggleCreateMode": "Chuyển đổi chế độ tạo gợi ý",
  "chat.suggestion.createAction": "Tạo gợi ý",
  "chat.suggestion.creating": "Đang tạo gợi ý...",

  // Chat - maestro status (used via CollapsedMaestroStatus)
  "chat.maestro.idle": "Maestro đang rảnh",
  "chat.maestro.title.idle": "Maestro hiện đang rảnh.",
  "chat.maestro.resting": "Maestro đang nghỉ ngơi...",
  "chat.maestro.observing": "Maestro đang quan sát...",
  "chat.maestro.aboutToEngage": "Maestro sắp tham gia...",
  "chat.maestro.title.resting": "Maestro đang rảnh, còn nhiều thời gian trước khi tái tham gia.",
  "chat.maestro.title.observing": "Maestro đang quan sát, còn một chút thời gian trước khi tái tham gia.",
  "chat.maestro.title.aboutToEngage": "Maestro sẽ sớm tái tham gia.",
  "chat.maestro.typing": "Maestro đang nhập...",
  "chat.maestro.title.typing": "Maestro đang chuẩn bị phản hồi.",
  "chat.maestro.speaking": "Maestro đang nói",
  "chat.maestro.title.speaking": "Maestro hiện đang nói.",
  "chat.maestro.listening": "Đang nghe...",
  "chat.maestro.title.listening": "Maestro đang chờ đầu vào hoặc giọng nói của bạn.",
  "chat.maestro.holding": "Maestro đang chờ",
  "chat.maestro.title.holding": "Maestro đang chờ (tái tham gia tạm dừng)",

  // Chat - bookmark (used)
  "chat.bookmark.hiddenHeaderAria": "Tin nhắn ẩn phía trên",
  "chat.bookmark.isHere": "Dấu trang ở đây",
  "chat.bookmark.setHere": "Đặt dấu trang ở đây",
  "chat.bookmark.actionsRegionAria": "Hành động dấu trang",
  "chat.bookmark.actionsToggleTitle": "Tùy chọn dấu trang",
  "chat.bookmark.decrementAria": "Hiển thị ít hơn một",
  "chat.bookmark.decrementTitle": "Ít hơn",
  "chat.bookmark.incrementAria": "Hiển thị thêm một",
  "chat.bookmark.incrementTitle": "Thêm",
  "chat.bookmark.hiddenBelowHeaderAria": "Tin nhắn ẩn phía dưới",

  // Chat - send preparation
  "chat.sendPrep.optimizingVideo": "Đang tối ưu hóa video...",
  "chat.sendPrep.optimizingImage": "Đang tối ưu hóa hình ảnh...",
  "chat.sendPrep.preparingMedia": "Đang chuẩn bị phương tiện...",
  "chat.sendPrep.uploadingMedia": "Đang tải lên phương tiện...",
  "chat.sendPrep.finalizing": "Đang hoàn tất...",

  // Chat - header activity tokens (used via activityTokens.ts)
  "chat.header.annotating": "Đang chú thích",
  "chat.header.recordingAudio": "Đang ghi âm",
  "chat.header.recordingVideo": "Đang quay video",
  "chat.header.savePopup": "Đang lưu...",
  "chat.header.loadPopup": "Đang tải...",
  "chat.header.maestroAvatar": "Đang cập nhật avatar Maestro",
  "chat.header.watchingVideo": "Đang xem video",
  "chat.header.viewingAbove": "Đang xem tin nhắn trước đó",
  "chat.header.liveSession": "Phiên trực tiếp",

  // Chat - live session
  "chat.liveSession.stop": "Dừng trực tiếp",
  "chat.liveSession.retry": "Thử lại trực tiếp",
  "chat.liveSession.start": "Bắt đầu trực tiếp",
  "chat.liveSession.liveBadge": "Trực tiếp",
  "chat.liveSession.connecting": "Đang kết nối",
  "chat.liveSession.defaultLastMessage": "Xin chào! Tôi có thể giúp gì cho bạn hôm nay?",
  "chat.liveSession.defaultSuggestion1": "Xin chào",
  "chat.liveSession.defaultSuggestion2": "Chào buổi sáng",
  "chat.liveSession.defaultSuggestion3": "Bạn khỏe không?",

  // Chat - errors
  "chat.error.sttError": "Lỗi STT: {error}. Thử chuyển đổi mic.",
  "chat.error.autoCaptureCameraError": "Lỗi tự động chụp camera: {error}",
  "chat.error.snapshotUserError": "{error}",
  "chat.error.recordingTimeExceeded": "Ghi tự động dừng sau {maxMinutes} phút.",
  "chat.error.videoMetadataError": "Không thể đọc siêu dữ liệu video. Tệp có thể bị hỏng hoặc định dạng không được hỗ trợ.",
  "chat.error.pauseVideoToAnnotate": "Tạm dừng video để chú thích khung hình hiện tại",
  "chat.error.imageGenInterrupted": "Tạo hình ảnh đã bị gián đoạn.",
  "chat.error.thinkingInterrupted": "Phản hồi AI đã bị gián đoạn.",

  // Errors - general
  "error.noLanguagePair": "Lỗi nghiêm trọng: Không có cặp ngôn ngữ nào được chọn.",
  "error.translationFailed": "Dịch thất bại. Vui lòng thử lại.",
  "error.imageLimitReached": "Đã đạt giới hạn tạo hình ảnh của phiên. Vui lòng bắt đầu phiên mới.",
  "error.tokenLimitReached": "Đã đạt giới hạn token của phiên. Vui lòng bắt đầu phiên mới.",
  "error.apiKeyMissing": "Thiếu khóa Gemini API của bạn. Mở màn hình Khóa API và dán khóa của bạn vào.",
  "error.apiKeyInvalid": "Khóa Gemini API của bạn không hợp lệ. Vui lòng kiểm tra lỗi chính tả và dán khóa hợp lệ.",
  "error.apiQuotaExceeded": "Hạn ngạch miễn phí Gemini API cho trò chuyện của bạn đã hết.",
  "error.quotaSetupBilling": "Thiết lập thanh toán",
  "error.quotaStartLive": "Bắt đầu Trực tiếp thay thế",

  // Errors - camera
  "error.cameraPermissionDenied": "Quyền camera bị từ chối. Vui lòng bật quyền truy cập camera trong cài đặt trình duyệt.",
  "error.cameraNotFound": "Không tìm thấy camera đã chọn. Đảm bảo nó được kết nối hoặc chọn camera khác trong cài đặt.",
  "error.cameraAccessNotSupported": "Truy cập camera không được trình duyệt của bạn hỗ trợ.",
  "error.cameraUnknown": "Đã xảy ra lỗi không xác định khi truy cập camera.",
  "error.cameraStreamNotAvailable": "Luồng camera không khả dụng để chụp.",
  "error.imageCaptureGeneric": "Lỗi không xác định khi chụp hình ảnh.",

  // Errors - visual context (dynamically constructed with prefix)
  "error.visualContextVideoElementNotReady": "Phần tử video ngữ cảnh trực quan chưa sẵn sàng.",
  "error.snapshotVideoElementNotReady": "Phần tử video chụp ảnh chưa sẵn sàng.",
  "error.visualContextCameraAccessNotSupported": "Truy cập camera không được hỗ trợ cho ngữ cảnh trực quan.",
  "error.snapshotCameraAccessNotSupported": "Truy cập camera không được hỗ trợ cho chụp ảnh.",
  "error.visualContext2DContext": "Không thể lấy ngữ cảnh 2D cho ngữ cảnh trực quan.",
  "error.snapshot2DContext": "Không thể lấy ngữ cảnh 2D cho chụp ảnh.",
  "error.visualContextCaptureFailedPermission": "Ngữ cảnh trực quan thất bại: Quyền camera bị từ chối.",
  "error.snapshotCaptureFailedPermission": "Chụp ảnh thất bại: Quyền camera bị từ chối.",
  "error.visualContextCaptureFailedNotFound": "Ngữ cảnh trực quan thất bại: Không tìm thấy camera.",
  "error.snapshotCaptureFailedNotFound": "Chụp ảnh thất bại: Không tìm thấy camera.",
  "error.visualContextCaptureFailedNotReady": "Ngữ cảnh trực quan thất bại: Camera chưa sẵn sàng hoặc có vấn đề với luồng. {details}",
  "error.snapshotCaptureFailedNotReady": "Chụp ảnh thất bại: Camera chưa sẵn sàng hoặc có vấn đề với luồng. {details}",
  "error.visualContextCaptureFailedGeneric": "Ngữ cảnh trực quan thất bại: {details}",
  "error.snapshotCaptureFailedGeneric": "Chụp ảnh thất bại: {details}",
};