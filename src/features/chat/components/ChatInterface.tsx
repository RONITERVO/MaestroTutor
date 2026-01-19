
import React, { useState, useRef, useEffect, useCallback, useMemo } from 'react';
import { ChatMessage, GroundingChunk, ReplySuggestion, MaestroActivityStage, SpeechPart, CameraDevice } from '../../../core/types';
import { LiveSessionState } from '../../speech/hooks/useGeminiLiveConversation';
import { TranslationReplacements } from '../../../core/i18n/index';
import { IconEyeOpen, IconBookmark, IconTrash } from '../../../shared/ui/Icons';
import BookmarkActions from './BookmarkActions';
import ChatMessageBubble from './ChatMessageBubble';
import SuggestionsList from './SuggestionsList';
import InputArea from './InputArea';

const BOOKMARK_SHOW_ABOVE_CHUNK_SIZE = 100;
const isRealChatMessage = (m: ChatMessage) => (m.role === 'user' || m.role === 'assistant') && !m.thinking;

interface ChatInterfaceProps {
  messages: ChatMessage[];
  onSendMessage: (text: string, imageBase64?: string, imageMimeType?: string) => Promise<boolean>;
  onDeleteMessage: (messageId: string) => void;
  onBookmarkAt: (messageId: string | null) => void;
  bookmarkedMessageId: string | null;
  updateMessage?: (messageId: string, updates: Partial<ChatMessage>) => void;
  maxVisibleMessages: number;
  onChangeMaxVisibleMessages: (n: number) => void;
  isSending: boolean;

  onUiTaskStart?: (token?: string) => string | void;
  onUiTaskEnd?: (token?: string) => void;

  bubbleWrapperRefs: React.MutableRefObject<Map<string, HTMLDivElement>>;
  
  // New Language Selection Props
  isLanguageSelectionOpen: boolean;
  tempNativeLangCode: string | null;
  tempTargetLangCode: string | null;
  onTempNativeSelect: (code: string | null) => void;
  onTempTargetSelect: (code: string | null) => void;
  onConfirmLanguageSelection: () => void;

  onSaveAllChats: (options?: { filename?: string; auto?: boolean }) => Promise<void>;
  onLoadAllChats: (file: File) => Promise<void>;
  loadingGifs?: string[] | null;

  attachedImageBase64: string | null;
  attachedImageMimeType: string | null;
  onSetAttachedImage: (base64: string | null, mimeType: string | null) => void;

  isSttSupported: boolean;
  isSttGloballyEnabled: boolean;
  isListening: boolean;
  sttError: string | null;
  transcript: string;
  onSttToggle: () => void;
  clearTranscript: () => void;
  sttLanguageCode: string;
  onSttLanguageChange: (langCode: string) => void;
  targetLanguageDef: any;
  nativeLanguageDef: any;

  isTtsSupported: boolean;
  isSpeaking: boolean; 
  speakText: (textOrParts: string | SpeechPart[], defaultLang: string) => void;
  stopSpeaking: () => void; 
  speakingUtteranceText: string | null; 

  speakNativeLang: boolean;
  onToggleSpeakNativeLang: () => void;

  currentTargetLangCode: string;
  currentNativeLangCode: string;
  currentNativeLangForTranslations: string;

  latestGroundingChunks?: GroundingChunk[];
  onUserInputActivity: () => void;
  autoCaptureError?: string | null;
  selectedCameraId: string | null;
  currentCameraFacingMode: 'user' | 'environment' | 'unknown';
  snapshotUserError?: string | null;
  onToggleSendWithSnapshot: () => void;
  sendWithSnapshotEnabled: boolean;
  onToggleUseVisualContextForReengagement: () => void;
  useVisualContextForReengagementEnabled: boolean;
  availableCameras: CameraDevice[];
  onSelectCamera: (deviceId: string) => void;

  replySuggestions: ReplySuggestion[];
  isLoadingSuggestions: boolean;
  onSuggestionClick: (suggestion: ReplySuggestion, langType: 'target' | 'native') => void;

  maestroActivityStage: MaestroActivityStage;
  t: (key: string, replacements?: TranslationReplacements) => string;

  imageGenerationModeEnabled: boolean;
  imageFocusedModeEnabled: boolean;
  onToggleImageGenerationMode: () => void;
  onToggleImageFocusedMode: (messageId: string) => void;
  transitioningImageId: string | null;

  estimatedImageLoadTime: number; 
  isImageGenCameraSelected: boolean;
  liveVideoStream: MediaStream | null;
  liveSessionState: LiveSessionState;
  liveSessionError: string | null;
  onStartLiveSession: () => Promise<void> | void;
  onStopLiveSession: () => void;
  
  isSuggestionMode: boolean;
  onToggleSuggestionMode: (forceState?: boolean) => void;
  onCreateSuggestion: (text: string) => Promise<void>;
  isCreatingSuggestion: boolean;
  sendPrep: { active: boolean; label: string; done?: number; total?: number; etaMs?: number } | null;

  // Settings passed down
  sttProvider: string;
  ttsProvider: string;
  onToggleSttProvider: () => void;
  onToggleTtsProvider: () => void;
  isSpeechRecognitionSupported: boolean;
}

const ChatInterface: React.FC<ChatInterfaceProps> = (props) => {
  const {
    messages,
    onDeleteMessage,
    isSending,

    bubbleWrapperRefs,
    
    isLanguageSelectionOpen,
    tempNativeLangCode,
    tempTargetLangCode,
    onTempNativeSelect,
    onTempTargetSelect,
    onConfirmLanguageSelection,

    onSaveAllChats,
    onLoadAllChats,
    loadingGifs,

    attachedImageBase64,
    attachedImageMimeType,
    onSetAttachedImage,
    isSttSupported,
    isSttGloballyEnabled,
    isListening,
    sttError,
    transcript,
    onSttToggle,
    sttLanguageCode,
    onSttLanguageChange,
    targetLanguageDef,
    nativeLanguageDef,
    isTtsSupported,
    isSpeaking, 
    speakText,
    stopSpeaking,
    speakingUtteranceText,
    speakNativeLang,
    onToggleSpeakNativeLang,
    currentTargetLangCode,
    currentNativeLangCode,
    latestGroundingChunks,
    onUserInputActivity,
    autoCaptureError,
    selectedCameraId,
    currentCameraFacingMode,
    snapshotUserError,
    onToggleSendWithSnapshot,
    sendWithSnapshotEnabled,
    onToggleUseVisualContextForReengagement,
    useVisualContextForReengagementEnabled,
    availableCameras,
    onSelectCamera,
    replySuggestions,
    isLoadingSuggestions,
    onSuggestionClick,
    t,
    imageGenerationModeEnabled,
    imageFocusedModeEnabled,
    onToggleImageGenerationMode,
    onToggleImageFocusedMode,
    transitioningImageId,
    estimatedImageLoadTime,
    isImageGenCameraSelected,
    liveVideoStream,
    liveSessionState,
    liveSessionError,
    onStartLiveSession,
    onStopLiveSession,
    isSuggestionMode,
    onToggleSuggestionMode,
    onCreateSuggestion,
    isCreatingSuggestion,
    onBookmarkAt,
    bookmarkedMessageId,
    sendPrep,
    onUiTaskStart,
    onUiTaskEnd,
    onSendMessage,
    sttProvider,
    ttsProvider,
    onToggleSttProvider,
    onToggleTtsProvider,
    isSpeechRecognitionSupported
  } = props;

  const maxVisibleBookmarkBudget = useMemo(() => {
    const base = Math.max(1, props.maxVisibleMessages);
    return base + 2;
  }, [props.maxVisibleMessages]);

  const bookmarkEligibleAssistantIds = useMemo(() => {
    const eligible = new Set<string>();
    let runningCount = 0;
    for (let i = messages.length - 1; i >= 0; i--) {
      const m = messages[i];
      if (isRealChatMessage(m)) {
        runningCount++;
        if (m.role === 'assistant' && !m.thinking && runningCount <= maxVisibleBookmarkBudget) {
          eligible.add(m.id);
        }
      }
    }
    return eligible;
  }, [messages, maxVisibleBookmarkBudget]);

  const scrollContainerRef = useRef<HTMLDivElement>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const [bookmarkViewMode, setBookmarkViewMode] = useState<'below' | 'above'>('below');
  const [bookmarkAboveChunkIndex, setBookmarkAboveChunkIndex] = useState(0);
  useEffect(() => {
    setBookmarkViewMode('below');
    setBookmarkAboveChunkIndex(0);
    setOpenBookmarkControlsForId(null);
  }, [bookmarkedMessageId]);

  const viewingAboveTokenRef = useRef<string | null>(null);
  useEffect(() => {
    if (bookmarkViewMode === 'above') {
      if (!viewingAboveTokenRef.current) {
        const tok = `viewing-above:${Date.now()}:${Math.random().toString(36).slice(2,8)}`;
        const ret = onUiTaskStart?.(tok);
        viewingAboveTokenRef.current = typeof ret === 'string' ? ret : tok;
      }
    } else {
      if (viewingAboveTokenRef.current) {
        onUiTaskEnd?.(viewingAboveTokenRef.current);
        viewingAboveTokenRef.current = null;
      }
    }
    return () => {
      if (viewingAboveTokenRef.current) {
        onUiTaskEnd?.(viewingAboveTokenRef.current);
        viewingAboveTokenRef.current = null;
      }
    };
  }, [bookmarkViewMode, onUiTaskStart, onUiTaskEnd]);

  const swipeRef = useRef<{
    messageId: string | null;
    startX: number;
    startY: number;
    isUser: boolean;
    isSwiping: boolean;
    trayWidth: number;
  }>({ messageId: null, startX: 0, startY: 0, isUser: false, isSwiping: false, trayWidth: 0 });

  const [openTrayForId, setOpenTrayForId] = useState<string | null>(null);
  const [openBookmarkControlsForId, setOpenBookmarkControlsForId] = useState<string | null>(null);

  const translateOrFallback = useCallback(
    (key: string, fallback: string, replacements?: TranslationReplacements) => {
      const result = t(key, replacements);
      return result === key ? fallback : result;
    },
    [t]
  );

  const handleSwipePointerDown = (e: React.PointerEvent<HTMLDivElement>, messageId: string, isUser: boolean) => {
    if (swipeRef.current.messageId || !e.isPrimary) return;
    if (openTrayForId && openTrayForId !== messageId) {
      const prev = bubbleWrapperRefs.current.get(openTrayForId);
      if (prev) {
        prev.style.transition = 'transform 0.2s ease-out';
        prev.style.transform = 'translateX(0px)';
      }
      setOpenTrayForId(null);
    }
  
    swipeRef.current = {
      messageId,
      startX: e.clientX,
      startY: e.clientY,
      isUser,
      isSwiping: false,
      trayWidth: 56, 
    };
  };

  const handleSwipePointerMove = (e: React.PointerEvent<HTMLDivElement>) => {
    if (!swipeRef.current.messageId || !e.isPrimary) return;
  
    const { startX, startY, isUser, messageId } = swipeRef.current;
    const bubble = bubbleWrapperRefs.current.get(messageId);
    if (!bubble) return;
  
    const deltaX = e.clientX - startX;
    const deltaY = e.clientY - startY;
  
    if (!swipeRef.current.isSwiping) {
      if (Math.abs(deltaX) > 10 || Math.abs(deltaY) > 10) {
        if (Math.abs(deltaX) > Math.abs(deltaY)) {
          swipeRef.current.isSwiping = true;
          bubble.style.touchAction = 'none';
          bubble.style.cursor = 'grabbing';
        } else {
          swipeRef.current = { messageId: null, startX: 0, startY: 0, isUser: false, isSwiping: false, trayWidth: 0 };
          return;
        }
      }
    }
    
    if (swipeRef.current.isSwiping) {
      const max = swipeRef.current.trayWidth + 12; 
      let constrained: number;
      if (isUser) {
        constrained = Math.min(0, deltaX);
        constrained = Math.max(constrained, -max);
      } else {
        constrained = Math.max(0, deltaX);
        constrained = Math.min(constrained, max);
      }
      bubble.style.transition = 'none';
      bubble.style.transform = `translateX(${constrained}px)`;
    }
  };

  const handleSwipePointerUp = (e: React.PointerEvent<HTMLDivElement>) => {
    if (!swipeRef.current.messageId || !e.isPrimary) return;
    
    const { messageId, startX, isUser, isSwiping, trayWidth } = swipeRef.current;
    const bubble = bubbleWrapperRefs.current.get(messageId);
    
    if (bubble) {
      bubble.style.touchAction = 'pan-y';
      bubble.style.cursor = 'auto';
    }
  
    if (!isSwiping || !bubble) {
      swipeRef.current = { messageId: null, startX: 0, startY: 0, isUser: false, isSwiping: false, trayWidth: 0 } as any;
      setOpenTrayForId(null);
      return;
    }
    
    const deltaX = e.clientX - startX;
    const shouldOpenTray = isUser ? deltaX < -trayWidth / 2 : deltaX > trayWidth / 2;
    bubble.style.transition = 'transform 0.2s ease-out';
    bubble.style.transform = shouldOpenTray ? `translateX(${isUser ? -trayWidth : trayWidth}px)` : 'translateX(0px)';
    setOpenTrayForId(shouldOpenTray ? messageId : null);
    
    swipeRef.current = { messageId: null, startX: 0, startY: 0, isUser: false, isSwiping: false, trayWidth: 0 };
  };

  const handleSwipePointerCancel = (e: React.PointerEvent<HTMLDivElement>) => {
    if (!swipeRef.current.messageId || !e.isPrimary) return;
    const { messageId } = swipeRef.current;
    const bubble = bubbleWrapperRefs.current.get(messageId);
    if (bubble) {
      bubble.style.transition = 'transform 0.3s ease-out';
      bubble.style.transform = 'translateX(0px)';
      bubble.style.touchAction = 'pan-y';
      bubble.style.cursor = 'auto';
    }
    setOpenTrayForId(null);
    swipeRef.current = { messageId: null, startX: 0, startY: 0, isUser: false, isSwiping: false, trayWidth: 0 };
  }

  const bookmarkInfo = useMemo(() => {
    if (!bookmarkedMessageId) {
      return {
        hasBookmark: false,
        bookmarkIdx: -1,
        pre: [] as ChatMessage[],
        post: messages,
        hiddenCount: 0,
      };
    }
    const idx = messages.findIndex(m => m.id === bookmarkedMessageId);
    if (idx === -1) {
      return {
        hasBookmark: false,
        bookmarkIdx: -1,
        pre: [] as ChatMessage[],
        post: messages,
        hiddenCount: 0,
      };
    }
    const pre = messages.slice(0, idx);
    const post = messages.slice(idx);
    return {
      hasBookmark: true,
      bookmarkIdx: idx,
      pre,
      post,
      hiddenCount: pre.length,
    };
  }, [messages, bookmarkedMessageId]);

  useEffect(() => {
    if (bookmarkViewMode !== 'above') return;
    const chunkCount = Math.ceil(bookmarkInfo.hiddenCount / BOOKMARK_SHOW_ABOVE_CHUNK_SIZE);
    if (chunkCount <= 0) {
      setBookmarkViewMode('below');
      if (bookmarkAboveChunkIndex !== 0) setBookmarkAboveChunkIndex(0);
      return;
    }
    const maxIndex = chunkCount - 1;
    if (bookmarkAboveChunkIndex > maxIndex) {
      setBookmarkAboveChunkIndex(maxIndex);
    }
  }, [bookmarkViewMode, bookmarkAboveChunkIndex, bookmarkInfo.hiddenCount]);

  let messagesToRender = messages;
  let hiddenCount = bookmarkInfo.hiddenCount;
  let bookmarkChunkMeta: {
    chunkCount: number;
    chunkIndex: number;
    chunkStart: number;
    chunkEnd: number;
    displayStart: number;
    displayEnd: number;
  } | null = null;

  if (bookmarkInfo.hasBookmark && bookmarkInfo.bookmarkIdx >= 0) {
    if (bookmarkViewMode === 'above' && hiddenCount > 0) {
      const chunkCount = Math.ceil(hiddenCount / BOOKMARK_SHOW_ABOVE_CHUNK_SIZE);
      if (chunkCount > 0) {
        const maxIndex = chunkCount - 1;
        const clampedIndex = Math.min(Math.max(bookmarkAboveChunkIndex, 0), maxIndex);
        const chunkEnd = hiddenCount - clampedIndex * BOOKMARK_SHOW_ABOVE_CHUNK_SIZE;
        const chunkStart = Math.max(0, chunkEnd - BOOKMARK_SHOW_ABOVE_CHUNK_SIZE);
        messagesToRender = bookmarkInfo.pre.slice(chunkStart, chunkEnd);
        bookmarkChunkMeta = {
          chunkCount,
          chunkIndex: clampedIndex,
          chunkStart,
          chunkEnd,
          displayStart: chunkStart + 1,
          displayEnd: chunkEnd,
        };
      } else {
        messagesToRender = bookmarkInfo.post;
      }
    } else {
      messagesToRender = bookmarkInfo.post;
    }
  }

  const handleSpeakWholeMessage = useCallback((message: ChatMessage) => {
     if (isSpeaking) {
       stopSpeaking();
       return;
     }
 
     if (message.role === 'assistant') {
       const partsForTTS: SpeechPart[] = [];
       let defaultLangForSpeakText = currentTargetLangCode || 'es';
 
       if (message.translations && message.translations.length > 0) {
         if (speakNativeLang) { 
           message.translations.forEach(pair => {
             if (pair.spanish && pair.spanish.trim()) {
              partsForTTS.push({ text: pair.spanish, langCode: currentTargetLangCode, context: { source: 'message', messageId: message.id } });
             }
             if (pair.english && pair.english.trim()) {
              partsForTTS.push({ text: pair.english, langCode: currentNativeLangCode, context: { source: 'message', messageId: message.id } });
             }
           });
         } else { 
           message.translations.forEach(pair => {
             if (pair.spanish && pair.spanish.trim()) {
              partsForTTS.push({ text: pair.spanish, langCode: currentTargetLangCode, context: { source: 'message', messageId: message.id } });
             }
           });
         }
       } else if (message.rawAssistantResponse) { 
         let textToSay = message.rawAssistantResponse;
         let langToUse = currentTargetLangCode;
         const mightBeNative = !textToSay.match(/[¡¿ñáéíóú]/i) && currentNativeLangCode.startsWith('en') && textToSay.match(/[a-zA-Z]/);
         if (mightBeNative && speakNativeLang) {
           langToUse = currentNativeLangCode;
         }
         if (textToSay.trim()) {
          partsForTTS.push({ text: textToSay.trim(), langCode: langToUse, context: { source: 'message', messageId: message.id } });
           defaultLangForSpeakText = langToUse;
         }
       } else if (message.text) { 
           let textToSay = message.text;
           let langToUse = currentTargetLangCode;
           const mightBeNative = !textToSay.match(/[¡¿ñáéíóú]/i) && currentNativeLangCode.startsWith('en') && textToSay.match(/[a-zA-Z]/);
           if (mightBeNative && speakNativeLang) {
             langToUse = currentNativeLangCode;
           }
           if (textToSay.trim()) {
           partsForTTS.push({ text: textToSay.trim(), langCode: langToUse, context: { source: 'message', messageId: message.id } });
             defaultLangForSpeakText = langToUse;
           }
       }
 
       if (partsForTTS.length > 0) {
           speakText(partsForTTS, defaultLangForSpeakText); 
           return;
       }
 
     } else if (message.text && (message.role === 'error' || message.role === 'status')) {
       const textToSay = message.text;
       const langToUse = currentNativeLangCode || 'en';
       if (textToSay.trim()) {
        speakText([{ text: textToSay.trim(), langCode: langToUse, context: { source: 'adHoc' } }], langToUse); 
       }
       return;
     }
  }, [isSpeaking, stopSpeaking, speakNativeLang, currentTargetLangCode, currentNativeLangCode, speakText]);
 
  const handleSpeakLine = useCallback((
    targetText: string,
    targetLangCode: string,
    nativeText?: string,
    nativeLangCode?: string,
    sourceMessageId?: string
  ) => {
     if (isSpeaking) {
       stopSpeaking();
       return;
     }
     if (!targetText || !targetLangCode) return;
 
    const messageContext = sourceMessageId
      ? { source: 'message' as const, messageId: sourceMessageId }
      : { source: 'adHoc' as const };

     const partsToSpeak: SpeechPart[] = [];
    partsToSpeak.push({ text: targetText, langCode: targetLangCode, context: messageContext });
 
     if (speakNativeLang && nativeText && nativeLangCode && nativeText.trim()) {
      partsToSpeak.push({ text: nativeText, langCode: nativeLangCode, context: messageContext });
     }
     
     if (partsToSpeak.length > 0) {
       speakText(partsToSpeak, partsToSpeak[0].langCode);
     }
  }, [isSpeaking, stopSpeaking, speakNativeLang, speakText]);
 
  const handlePlayUserMessage = useCallback((targetMessage: ChatMessage) => {
      if (!targetMessage?.text) return;
      if (isSpeaking) {
        stopSpeaking();
        return;
      }

      const recorded = targetMessage.recordedUtterance;
      if (recorded && typeof recorded.dataUrl === 'string' && recorded.dataUrl.length > 0) {
        const lang = recorded.langCode || currentTargetLangCode || 'es';
        const parts: SpeechPart[] = [{
          text: targetMessage.text,
          langCode: lang,
          cachedAudio: recorded.dataUrl,
          context: { source: 'message', messageId: targetMessage.id },
        }];
        speakText(parts, lang);
        return;
      }

      let langToUse = currentTargetLangCode || 'es';
      if (targetMessage.text && currentNativeLangCode && currentNativeLangCode.startsWith('en')) {
        const mightBeNative = !targetMessage.text.match(/[¡¿ñáéíóú]/i) && /[a-zA-Z]/.test(targetMessage.text);
        if (mightBeNative) {
          langToUse = currentNativeLangCode;
        }
      }

      speakText([
        { text: targetMessage.text, langCode: langToUse, context: { source: 'message', messageId: targetMessage.id } },
      ], langToUse);
    }, [isSpeaking, stopSpeaking, currentTargetLangCode, currentNativeLangCode, speakText]);

  return (
    <div className="flex flex-col h-full bg-slate-100">
      <div 
        ref={scrollContainerRef}
        className="flex-grow overflow-y-auto p-4 space-y-2"
        onPointerMove={handleSwipePointerMove}
        onPointerUp={handleSwipePointerUp}
        onPointerCancel={handleSwipePointerCancel}
      >
       {bookmarkInfo.hasBookmark && hiddenCount > 0 && (
         <div
           className="my-1 px-2 py-1 bg-slate-200 border border-slate-300 rounded flex items-center gap-2"
           role="region"
           aria-label={t('chat.bookmark.hiddenHeaderAria') || 'Hidden messages above'}
           style={{
             // @ts-ignore
             containerType: 'inline-size'
           }}
         >
           <IconEyeOpen className="w-4 h-4 text-slate-600" />
           <span className="text-slate-700" style={{ fontSize: '2.8cqw' }}>
             {bookmarkViewMode === 'above' && bookmarkChunkMeta
               ? translateOrFallback(
                   'chat.bookmark.showingAboveRange',
                   `Showing messages above ${bookmarkChunkMeta.displayStart}-${bookmarkChunkMeta.displayEnd} of ${hiddenCount}`,
                   { start: bookmarkChunkMeta.displayStart, end: bookmarkChunkMeta.displayEnd, total: hiddenCount }
                 )
               : translateOrFallback(
                   'chat.bookmark.hiddenCount',
                   `${hiddenCount} above messages hidden`,
                   { count: hiddenCount }
                 )}
           </span>
           <div className="ml-auto flex items-center gap-2" style={{ fontSize: '2.8cqw' }}>
             {bookmarkViewMode === 'above' && bookmarkChunkMeta ? (
               <button
                 className="px-2 py-1 rounded bg-white hover:bg-gray-100 text-slate-800 border border-slate-300 disabled:opacity-50"
                 onClick={() => {
                   if (bookmarkChunkMeta && bookmarkChunkMeta.chunkIndex < bookmarkChunkMeta.chunkCount - 1) {
                     setBookmarkAboveChunkIndex(prev => Math.min(prev + 1, bookmarkChunkMeta.chunkCount - 1));
                   }
                 }}
                 disabled={bookmarkChunkMeta.chunkIndex >= bookmarkChunkMeta.chunkCount - 1}
                 title={translateOrFallback(
                   'chat.bookmark.showNextChunk',
                   `Show ${BOOKMARK_SHOW_ABOVE_CHUNK_SIZE} above`,
                   { count: BOOKMARK_SHOW_ABOVE_CHUNK_SIZE }
                 )}
               >
                 {translateOrFallback(
                   'chat.bookmark.showNextChunk',
                   `Show ${BOOKMARK_SHOW_ABOVE_CHUNK_SIZE} above`,
                   { count: BOOKMARK_SHOW_ABOVE_CHUNK_SIZE }
                 )}
               </button>
             ) : (
               <button
                 className="px-2 py-1 rounded bg-white hover:bg-gray-100 text-slate-800 border border-slate-300"
                 onClick={() => {
                   if (hiddenCount > 0) {
                     setBookmarkViewMode('above');
                     setBookmarkAboveChunkIndex(0);
                   }
                 }}
                 title={translateOrFallback(
                   'chat.bookmark.showAboveChunk',
                   hiddenCount > BOOKMARK_SHOW_ABOVE_CHUNK_SIZE
                     ? `Show ${BOOKMARK_SHOW_ABOVE_CHUNK_SIZE} above`
                     : 'Show messages above',
                   { count: Math.min(hiddenCount, BOOKMARK_SHOW_ABOVE_CHUNK_SIZE) }
                 )}
               >
                 {translateOrFallback(
                   'chat.bookmark.showAboveChunk',
                   hiddenCount > BOOKMARK_SHOW_ABOVE_CHUNK_SIZE
                     ? `Show ${BOOKMARK_SHOW_ABOVE_CHUNK_SIZE} above`
                     : 'Show messages above',
                   { count: Math.min(hiddenCount, BOOKMARK_SHOW_ABOVE_CHUNK_SIZE) }
                 )}
               </button>
             )}
           </div>
         </div>
       )}
         {messagesToRender.map((msg, _idx) => {
          if (msg.role === 'system_selection') {
            return null;
          }
          const isStatus = msg.role === 'status';
          const isUser = msg.role === 'user';
          const isAssistant = msg.role === 'assistant';
          const isError = msg.role === 'error';
          const canBeDeleted = (isUser || isAssistant || isError || isStatus) && !msg.thinking && !msg.isGeneratingImage;

          return (
             <div
              key={msg.id}
              style={{ touchAction: 'pan-y', position: 'relative' }}
              onPointerDown={openTrayForId === msg.id ? undefined : (canBeDeleted ? (e) => handleSwipePointerDown(e, msg.id, isUser) : undefined)}
            >
             {bookmarkedMessageId === msg.id && (
               <div
                 className={`absolute top-1 ${isUser ? 'right-1' : 'left-1'} z-40 flex items-center gap-2`}
                 onPointerDown={(e) => { e.stopPropagation(); }}
               >
                 {isUser && openBookmarkControlsForId === msg.id && (
                   <div className="mr-1">
                     <BookmarkActions
                       t={t}
                       message={msg}
                       maxVisibleMessages={props.maxVisibleMessages}
                       onChangeMaxVisibleMessages={props.onChangeMaxVisibleMessages}
                       updateMessage={props.updateMessage}
                     />
                   </div>
                 )}
                 <button
                   className={`p-1 rounded-full bg-amber-500/80 text-white hover:bg-amber-500 shadow-sm border border-amber-600`}
                   onClick={(e) => { e.stopPropagation(); setOpenBookmarkControlsForId(prev => prev === msg.id ? null : msg.id); }}
                   title={t('chat.bookmark.actionsToggleTitle') || 'Bookmark options'}
                   aria-expanded={openBookmarkControlsForId === msg.id}
                  >
                   <IconBookmark className="w-4 h-4" />
                 </button>
                 {!isUser && openBookmarkControlsForId === msg.id && (
                   <div className="ml-1">
                     <BookmarkActions
                       t={t}
                       message={msg}
                       maxVisibleMessages={props.maxVisibleMessages}
                       onChangeMaxVisibleMessages={props.onChangeMaxVisibleMessages}
                       updateMessage={props.updateMessage}
                     />
                   </div>
                 )}
               </div>
             )}
             {canBeDeleted && (
               <div
                 className={`absolute ${isUser ? 'right-0' : 'left-0'} top-1/2 -translate-y-1/2 flex flex-col items-center gap-2`}
                 style={{
                   width: 56,
                   zIndex: 50,
                   pointerEvents: openTrayForId === msg.id ? 'auto' : 'none',
                   opacity: openTrayForId === msg.id ? 1 : 0,
                   transform: `translateY(-50%) ${openTrayForId === msg.id ? 'translateX(0)' : `translateX(${isUser ? '8px' : '-8px'})`}`,
                   transition: 'opacity 120ms ease, transform 120ms ease',
                   touchAction: 'none',
                 }}
                 onPointerDown={(e) => { e.stopPropagation(); }}
                 aria-hidden={openTrayForId === msg.id ? undefined : true}
               >
                 {isAssistant && (msg.id === bookmarkedMessageId || bookmarkEligibleAssistantIds.has(msg.id)) && (
                   <button
                     className={`p-2 rounded-full ${isSuggestionMode ? 'bg-gray-300 text-gray-800' : 'bg-amber-400 text-white'} shadow`}
                     onPointerDown={(e) => { e.stopPropagation(); }}
                         onClick={(e) => { e.stopPropagation(); if (msg.id !== bookmarkedMessageId) { onBookmarkAt(msg.id); } }}
                         title={msg.id === bookmarkedMessageId ? (t('chat.bookmark.isHere') || 'Bookmark is here') : (t('chat.bookmark.setHere') || 'Set bookmark here')}
                     aria-pressed={msg.id === bookmarkedMessageId}
                   >
                     <IconBookmark className={`w-5 h-5 ${msg.id === bookmarkedMessageId ? 'opacity-100' : 'opacity-90'}`} />
                   </button>
                 )}
                 <button
                   className={`p-2 rounded-full ${isSuggestionMode ? 'bg-gray-300 text-gray-800' : 'bg-red-500 text-white'} shadow`}
                   onPointerDown={(e) => { e.stopPropagation(); }}
                   onClick={(e) => { e.stopPropagation(); onDeleteMessage(msg.id); }}
                   title="Delete message"
                 >
                   <IconTrash className="w-5 h-5" />
                 </button>
               </div>
             )}

              <ChatMessageBubble 
                key={msg.id} 
                message={msg} 
                isFocusedMode={imageFocusedModeEnabled} 
                speakingUtteranceText={speakingUtteranceText} 
                estimatedLoadTime={estimatedImageLoadTime} 
                isSending={isSending} 
                loadingGifs={loadingGifs}
                currentTargetLangCode={currentTargetLangCode} 
                currentNativeLangCode={currentNativeLangCode} 
                t={t}
                isSpeaking={isSpeaking}
                speakNativeLang={speakNativeLang}
                onToggleSpeakNativeLang={onToggleSpeakNativeLang}
                handleSpeakWholeMessage={handleSpeakWholeMessage}
                handleSpeakLine={handleSpeakLine}
                handlePlayUserMessage={handlePlayUserMessage}
                speakText={speakText}
                stopSpeaking={stopSpeaking}
                isTtsSupported={isTtsSupported}
                onToggleImageFocusedMode={() => onToggleImageFocusedMode(msg.id)}
                transitioningImageId={transitioningImageId}
                onSetAttachedImage={onSetAttachedImage}
                onUserInputActivity={onUserInputActivity}
                onUiTaskStart={onUiTaskStart}
                onUiTaskEnd={onUiTaskEnd}
                registerBubbleEl={(el) => {
                  if (el) bubbleWrapperRefs.current.set(msg.id, el);
                  else bubbleWrapperRefs.current.delete(msg.id);
                }}
              />
            </div>
          );
        })}
        {latestGroundingChunks && latestGroundingChunks.length > 0 && (
          <div
            className="mt-4 p-3 bg-gray-100 rounded-lg shadow"
            style={{
              // @ts-ignore
              containerType: 'inline-size'
            }}
          >
            <h4 className="font-semibold text-gray-600 mb-1" style={{ fontSize: '2.9cqw' }}>{t('chat.retrievedFromWeb')}</h4>
            <ul className="space-y-1">
              {latestGroundingChunks.map((chunk, index) => (
                (chunk.web || chunk.retrievedContext) && (
                  <li key={index}>
                    <a
                      href={(chunk.web?.uri || chunk.retrievedContext?.uri)!}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-blue-500 hover:underline truncate block"
                      style={{ fontSize: '2.8cqw' }}
                      title={(chunk.web?.title || chunk.retrievedContext?.title || chunk.web?.uri || chunk.retrievedContext?.uri)!}
                    >
                      {(chunk.web?.title || chunk.retrievedContext?.title) ? (chunk.web?.title || chunk.retrievedContext?.title) : (chunk.web?.uri || chunk.retrievedContext?.uri)}
                    </a>
                  </li>
                )
              ))}
            </ul>
          </div>
        )}


       {bookmarkInfo.hasBookmark && bookmarkViewMode === 'above' && (
         <div
           className="my-1 px-2 py-1 bg-slate-200 border border-slate-300 rounded flex items-center gap-2"
           role="region"
           aria-label={t('chat.bookmark.hiddenBelowHeaderAria') || 'Hidden messages below'}
           style={{
             // @ts-ignore
             containerType: 'inline-size'
           }}
         >
           <IconEyeOpen className="w-4 h-4 text-slate-600" />
           <span className="text-slate-700" style={{ fontSize: '2.8cqw' }}>
             {bookmarkChunkMeta
               ? translateOrFallback(
                   'chat.bookmark.showingAboveRange',
                   `Showing messages above ${bookmarkChunkMeta.displayStart}-${bookmarkChunkMeta.displayEnd} of ${hiddenCount}`,
                   { start: bookmarkChunkMeta.displayStart, end: bookmarkChunkMeta.displayEnd, total: hiddenCount }
                 )
               : translateOrFallback(
                   'chat.bookmark.hiddenBelow',
                   `Messages are hidden below`
                 )}
           </span>
           <div className="ml-auto flex items-center gap-2" style={{ fontSize: '2.8cqw' }}>
             {bookmarkChunkMeta && bookmarkChunkMeta.chunkIndex > 0 && (
               <button
                 className="px-2 py-1 rounded bg-white hover:bg-gray-100 text-slate-800 border border-slate-300"
                 onClick={() => setBookmarkAboveChunkIndex(prev => Math.max(prev - 1, 0))}
                 title={translateOrFallback(
                   'chat.bookmark.showPreviousChunk',
                   `Show ${BOOKMARK_SHOW_ABOVE_CHUNK_SIZE} below`,
                   { count: BOOKMARK_SHOW_ABOVE_CHUNK_SIZE }
                 )}
               >
                 {translateOrFallback(
                   'chat.bookmark.showPreviousChunk',
                   `Show ${BOOKMARK_SHOW_ABOVE_CHUNK_SIZE} below`,
                   { count: BOOKMARK_SHOW_ABOVE_CHUNK_SIZE }
                 )}
               </button>
             )}
             <button
               className="px-2 py-1 rounded bg-white hover:bg-gray-100 text-slate-800 border border-slate-300"
               onClick={() => setBookmarkViewMode('below')}
               title={translateOrFallback('chat.bookmark.returnToRecent', 'Back to messages below')}
             >
               {translateOrFallback('chat.bookmark.returnToRecent', 'Back to messages below')}
             </button>
           </div>
         </div>
       )}

        {/* Note: Input area is always rendered now, but modes switch inside */}
        <div className="flex flex-col items-end mt-2">
            <div
                className={`transition-colors duration-300 rounded-xl p-3 shadow-lg w-full max-w-2xl ${isSuggestionMode ? 'bg-gray-200 text-gray-700' : 'bg-blue-500 text-white'} relative`}
                style={{
                // @ts-ignore
                containerType: 'inline-size'
                }}
            >
                <InputArea
                    t={t}
                    isSending={isSending}
                    isSpeaking={isSpeaking}
                    isListening={isListening}
                    isSttGloballyEnabled={isSttGloballyEnabled}
                    isSttSupported={isSttSupported}
                    transcript={transcript}
                    sttLanguageCode={sttLanguageCode}
                    targetLanguageDef={targetLanguageDef}
                    nativeLanguageDef={nativeLanguageDef}
                    onSttToggle={onSttToggle}
                    onSttLanguageChange={onSttLanguageChange}
                    attachedImageBase64={attachedImageBase64}
                    attachedImageMimeType={attachedImageMimeType}
                    onSetAttachedImage={onSetAttachedImage}
                    onSendMessage={onSendMessage}
                    onUserInputActivity={onUserInputActivity}
                    liveVideoStream={liveVideoStream}
                    liveSessionState={liveSessionState}
                    liveSessionError={liveSessionError}
                    onStartLiveSession={onStartLiveSession}
                    onStopLiveSession={onStopLiveSession}
                    isSuggestionMode={isSuggestionMode}
                    onToggleSuggestionMode={onToggleSuggestionMode}
                    onCreateSuggestion={onCreateSuggestion}
                    isCreatingSuggestion={isCreatingSuggestion}
                    sendPrep={sendPrep}
                    availableCameras={availableCameras}
                    selectedCameraId={selectedCameraId}
                    currentCameraFacingMode={currentCameraFacingMode}
                    isImageGenCameraSelected={isImageGenCameraSelected}
                    onSelectCamera={onSelectCamera}
                    onToggleSendWithSnapshot={onToggleSendWithSnapshot}
                    onToggleUseVisualContextForReengagement={onToggleUseVisualContextForReengagement}
                    sendWithSnapshotEnabled={sendWithSnapshotEnabled}
                    useVisualContextForReengagementEnabled={useVisualContextForReengagementEnabled}
                    imageGenerationModeEnabled={imageGenerationModeEnabled}
                    onToggleImageGenerationMode={onToggleImageGenerationMode}
                    sttError={sttError}
                     autoCaptureError={autoCaptureError ?? null}
                     snapshotUserError={snapshotUserError ?? null}
                    onUiTaskStart={onUiTaskStart}
                    onUiTaskEnd={onUiTaskEnd}
                    
                    // Language Selection Props
                    isLanguageSelectionOpen={isLanguageSelectionOpen}
                    tempNativeLangCode={tempNativeLangCode}
                    tempTargetLangCode={tempTargetLangCode}
                    onTempNativeSelect={onTempNativeSelect}
                    onTempTargetSelect={onTempTargetSelect}
                    onConfirmLanguageSelection={onConfirmLanguageSelection}
                    onSaveAllChats={onSaveAllChats}
                    onLoadAllChats={onLoadAllChats}

                    // Settings
                    sttProvider={sttProvider}
                    ttsProvider={ttsProvider}
                    onToggleSttProvider={onToggleSttProvider}
                    onToggleTtsProvider={onToggleTtsProvider}
                    isSpeechRecognitionSupported={isSpeechRecognitionSupported}
                />
            </div>
            {(isLoadingSuggestions || replySuggestions.length > 0) && (
                <SuggestionsList
                    isLoadingSuggestions={isLoadingSuggestions}
                    replySuggestions={replySuggestions}
                    t={t}
                    isSttSupported={isSttSupported}
                    isSuggestionMode={isSuggestionMode}
                    onToggleSuggestionMode={() => onToggleSuggestionMode()}
                    isCreatingSuggestion={isCreatingSuggestion}
                    onSuggestionClick={onSuggestionClick}
                    isSpeaking={isSpeaking}
                    stopSpeaking={stopSpeaking}
                />
            )}
        </div>
        <div ref={messagesEndRef} />
      </div>

    </div>
  );
};

export default React.memo(ChatInterface);
