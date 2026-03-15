// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useRef, useState, useEffect, useCallback, useMemo } from 'react';
import { ALL_LANGUAGES } from '../../../core/config/languages';
import { IconXMark, IconUndo, IconCheck, IconSend, IconPlus, IconChevronLeft, IconChevronRight } from '../../../shared/ui/Icons';
import { SmallSpinner } from '../../../shared/ui/SmallSpinner';
import { useMaestroStore } from '../../../store';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { useLanguageSelection } from '../../session';
import { selectTargetLanguageDef, selectNativeLanguageDef } from '../../../store/slices/settingsSlice';
import { selectIsListening, selectIsSending, selectIsSpeaking, selectIsCreatingSuggestion } from '../../../store/slices/uiSlice';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE, type TokenSubtype } from '../../../core/config/activityTokens';
import { IMAGE_GEN_CAMERA_ID } from '../../../core/config/app';
import MediaAttachments from './input/MediaAttachments';
import Composer from './input/Composer';
import AudioControls from './input/AudioControls';
import CameraControls from './input/CameraControls';
import SessionControls from '../../session/components/SessionControls';
import { usePdfAnnotation } from '../hooks/usePdfAnnotation';
import { normalizeAttachmentMimeType } from '../utils/fileAttachments';
import { parseAssistantResponseForAttachment } from '../utils/assistantResponseAttachments';
import { createSmartRef } from '../../../shared/utils/smartRef';

interface InputAreaProps {
  onSttToggle: () => void;
  onSendMessage: (text: string, imageBase64?: string, imageMimeType?: string) => Promise<boolean>;
  onUserInputActivity: () => void;
  onStartLiveSession: () => Promise<void> | void;
  onStopLiveSession: () => void;
  onStopSilentObserver: () => Promise<void> | void;
  onToggleSuggestionMode: (force?: boolean) => void;
  onCreateSuggestion: (text: string) => Promise<void>;
  onToggleSendWithSnapshot: () => void;
  onToggleUseVisualContextForReengagement: () => void;
  onToggleImageGenerationMode: () => void;
  onScrollToBottom?: () => void;
}

type ComposerAttachmentCandidate = {
  cleanedText: string;
  attachment: {
    dataUrl: string;
    mimeType: string;
    fileName: string;
  };
};

const normalizeComposerInputText = (value: string): string =>
  (value || '').replace(/\r\n/g, '\n').replace(/\r/g, '\n').trim();

const toUtf8Base64DataUrl = (mimeType: string, text: string): string => {
  const bytes = new TextEncoder().encode(text);
  let binary = '';
  for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
  return `data:${mimeType};charset=utf-8;base64,${btoa(binary)}`;
};

const inferPlainCodeAttachmentFileName = (text: string): string => {
  const normalized = normalizeComposerInputText(text);
  if (!normalized) return 'pasted-code.txt';

  if (/^\s*#!/.test(normalized)) {
    if (/python/i.test(normalized)) return 'pasted.py';
    if (/(bash|sh|zsh|fish)/i.test(normalized)) return 'pasted.sh';
    if (/(pwsh|powershell)/i.test(normalized)) return 'pasted.ps1';
  }

  if (/^\s*(select|insert|update|delete|create|alter|drop)\b/im.test(normalized)) {
    return 'pasted.sql';
  }

  if (/^\s*(import|from)\s+.+\s+import\s+/m.test(normalized) || /^\s*(def|class)\s+\w+\s*\(/m.test(normalized)) {
    return 'pasted.py';
  }

  if (/^\s*(import|export)\s+/m.test(normalized) || /^\s*(const|let|var)\s+\w+/m.test(normalized)) {
    return 'pasted.ts';
  }

  if (/^\s*<!doctype html/i.test(normalized) || /<\/?[a-z][\w:-]*[^>]*>/i.test(normalized)) {
    return 'pasted.html';
  }

  if ((normalized.startsWith('{') || normalized.startsWith('['))) {
    try {
      JSON.parse(normalized);
      return 'pasted.json';
    } catch {
      // not JSON
    }
  }

  return 'pasted-code.txt';
};

const looksLikeStandaloneCodeSnippet = (source: string): boolean => {
  const normalized = normalizeComposerInputText(source);
  if (!normalized || normalized.length < 120) return false;

  const lines = normalized.split('\n').filter(line => line.trim().length > 0);
  if (lines.length < 4) return false;

  let score = 0;
  if (/[{}[\];]/.test(normalized)) score += 1;
  if (/=>|::|<\/?[a-z][\w:-]*[^>]*>/i.test(normalized)) score += 1;
  if (/^\s*(import|export|const|let|var|function|class|interface|type|enum|def|return|if|for|while|select|insert|update|delete|create)\b/m.test(normalized)) score += 1;
  if (/^\s*["']?[a-zA-Z0-9_$-]+["']?\s*:\s*.+$/m.test(normalized)) score += 1;
  if (lines.filter(line => /^\s{2,}\S/.test(line)).length >= 2) score += 1;

  return score >= 3;
};

const getComposerAttachmentCandidate = (pastedText: string): ComposerAttachmentCandidate | null => {
  const parsed = parseAssistantResponseForAttachment(pastedText);
  if (parsed.attachment) {
    if (parsed.attachment.kind !== 'code') {
      return {
        cleanedText: parsed.cleanedText,
        attachment: {
          dataUrl: parsed.attachment.dataUrl,
          mimeType: parsed.attachment.mimeType,
          fileName: parsed.attachment.fileName,
        },
      };
    }

    const normalized = normalizeComposerInputText(pastedText);
    const lineCount = normalized ? normalized.split('\n').length : 0;
    if (normalized.length >= 80 || lineCount >= 4) {
      return {
        cleanedText: parsed.cleanedText,
        attachment: {
          dataUrl: parsed.attachment.dataUrl,
          mimeType: parsed.attachment.mimeType,
          fileName: parsed.attachment.fileName,
        },
      };
    }
  }

  const normalized = normalizeComposerInputText(pastedText);
  if (!looksLikeStandaloneCodeSnippet(normalized)) return null;

  return {
    cleanedText: '',
    attachment: {
      dataUrl: toUtf8Base64DataUrl('text/plain', normalized),
      mimeType: 'text/plain',
      fileName: inferPlainCodeAttachmentFileName(normalized),
    },
  };
};

const InputArea: React.FC<InputAreaProps> = ({
  onSttToggle,
  onSendMessage,
  onUserInputActivity,
  onStartLiveSession,
  onStopLiveSession,
  onStopSilentObserver,
  onToggleSuggestionMode,
  onCreateSuggestion,
  onToggleSendWithSnapshot,
  onToggleUseVisualContextForReengagement,
  onToggleImageGenerationMode,
  onScrollToBottom,
}) => {
  const { t } = useAppTranslations();
  const settings = useMaestroStore(state => state.settings);
  const targetLanguageDef = useMaestroStore(selectTargetLanguageDef) || ALL_LANGUAGES[0];
  const nativeLanguageDef = useMaestroStore(selectNativeLanguageDef) || ALL_LANGUAGES[0];
  const attachedImageBase64 = useMaestroStore(state => state.attachedImageBase64);
  const attachedImageMimeType = useMaestroStore(state => state.attachedImageMimeType);
  const attachedFileName = useMaestroStore(state => state.attachedFileName);
  const sendPrep = useMaestroStore(state => state.sendPrep);
  const transcript = useMaestroStore(state => state.transcript);
  const sttError = useMaestroStore(state => state.sttError);
  const liveVideoStream = useMaestroStore(state => state.liveVideoStream);
  const liveSessionState = useMaestroStore(state => state.liveSessionState);
  const liveSessionError = useMaestroStore(state => state.liveSessionError);
  const silentObserverState = useMaestroStore(state => state.silentObserverState);
  const availableCameras = useMaestroStore(state => state.availableCameras);
  const currentCameraFacingMode = useMaestroStore(state => state.currentCameraFacingMode);
  const autoCaptureError = useMaestroStore(state => state.visualContextCameraError);
  const snapshotUserError = useMaestroStore(state => state.snapshotUserError);
  const isSettingsLoaded = useMaestroStore(state => state.isSettingsLoaded);
  const languagePairs = useMaestroStore(state => state.languagePairs);
  const isLanguageSelectionOpen = useMaestroStore(state => state.isLanguageSelectionOpen);
  const tempNativeLangCode = useMaestroStore(state => state.tempNativeLangCode);
  const tempTargetLangCode = useMaestroStore(state => state.tempTargetLangCode);
  const languageSelectorLastInteraction = useMaestroStore(state => state.languageSelectorLastInteraction);
  const setIsLanguageSelectionOpen = useMaestroStore(state => state.setIsLanguageSelectionOpen);
  const setTempNativeLangCode = useMaestroStore(state => state.setTempNativeLangCode);
  const setTempTargetLangCode = useMaestroStore(state => state.setTempTargetLangCode);
  const updateSetting = useMaestroStore(state => state.updateSetting);
  const setAttachedImage = useMaestroStore(state => state.setAttachedImage);
  const microphoneApiAvailable = useMaestroStore(state => state.microphoneApiAvailable);
  const isCreatingSuggestion = useMaestroStore(selectIsCreatingSuggestion);

  const settingsRef = useMemo(
    () => createSmartRef(useMaestroStore.getState, state => state.settings),
    []
  );
  const messagesRef = useMemo(
    () => createSmartRef(useMaestroStore.getState, state => state.messages),
    []
  );
  const isSendingRef = useMemo(
    () => createSmartRef(useMaestroStore.getState, selectIsSending),
    []
  );

  const isSuggestionMode = settings.isSuggestionMode;
  const isSttGloballyEnabled = settings.stt.enabled;
  const sttLanguageCode = settings.stt.language;
  const isSttSupported = microphoneApiAvailable;
  const sendWithSnapshotEnabled = settings.sendWithSnapshotEnabled;
  const useVisualContextForReengagementEnabled = settings.smartReengagement.useVisualContext;
  const imageGenerationModeEnabled = settings.imageGenerationModeEnabled;
  const selectedCameraId = settings.selectedCameraId;
  const isImageGenCameraSelected = selectedCameraId === IMAGE_GEN_CAMERA_ID;

  const onSetAttachedImage = useCallback((base64: string | null, mimeType: string | null, fileName: string | null = null) => {
    setAttachedImage(base64, mimeType, fileName);
  }, [setAttachedImage]);

  const handleSelectCamera = useCallback((deviceId: string) => {
    updateSetting('selectedCameraId', deviceId);
  }, [updateSetting]);

  const { handleShowLanguageSelector } = useLanguageSelection({
    isSettingsLoaded,
    settings,
    settingsRef,
    isSendingRef,
    languagePairs,
    handleSettingsChange: updateSetting,
    messagesRef,
    isLanguageSelectionOpen,
    tempNativeLangCode,
    tempTargetLangCode,
    languageSelectorLastInteraction,
    setIsLanguageSelectionOpen,
    setTempNativeLangCode,
    setTempTargetLangCode,
  });
  const [inputText, setInputText] = useState('');
  const bubbleTextAreaRef = useRef<HTMLTextAreaElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const prevTranscriptRef = useRef('');
  const attachedPreviewVideoRef = useRef<HTMLVideoElement>(null);
  const paperclipOpenTokenRef = useRef<string | null>(null);
  const languageSelectionOpen = Boolean(isLanguageSelectionOpen);

  const isSending = useMaestroStore(selectIsSending);
  const isSpeaking = useMaestroStore(selectIsSpeaking);
  const isListening = useMaestroStore(selectIsListening);
  const addActivityToken = useMaestroStore(state => state.addActivityToken);
  const removeActivityToken = useMaestroStore(state => state.removeActivityToken);

  const createUiToken = useCallback(
    (subtype: TokenSubtype) =>
      addActivityToken(
        TOKEN_CATEGORY.UI,
        `${subtype}:${Date.now()}:${Math.random().toString(36).slice(2, 8)}`
      ),
    [addActivityToken]
  );

  const endUiTask = useCallback((token: string | null) => {
    if (token) removeActivityToken(token);
  }, [removeActivityToken]);

  // --- Composer Annotation State ---
  const [isComposerAnnotating, setIsComposerAnnotating] = useState(false);
  const [composerAnnotationSourceUrl, setComposerAnnotationSourceUrl] = useState<string | null>(null);
  const [composerImageAspectRatio, setComposerImageAspectRatio] = useState<number | null>(null);
  const [composerScale, setComposerScale] = useState(1);
  const [composerPan, setComposerPan] = useState({ x: 0, y: 0 });
  const [composerUndoStack, setComposerUndoStack] = useState<ImageData[]>([]);
  const [composerIsBlankCanvas, setComposerIsBlankCanvas] = useState(false);

  const composerViewportRef = useRef<HTMLDivElement>(null);
  const composerImageRef = useRef<HTMLImageElement | null>(null);
  const composerEditCanvasRef = useRef<HTMLCanvasElement>(null);
  const composerAnnotateTokenRef = useRef<string | null>(null);

  const composerIsDrawingRef = useRef(false);
  const composerLastPosRef = useRef<{x: number, y: number} | null>(null);
  const composerActivePointersRef = useRef<React.PointerEvent[]>([]);
  const composerLastPanPointRef = useRef<{x: number, y: number} | null>(null);
  const composerLastPinchDistanceRef = useRef<number>(0);
  const composerIsNewStrokeRef = useRef(true);

  const showLiveFeed = Boolean(liveVideoStream && (useVisualContextForReengagementEnabled || sendWithSnapshotEnabled) && !isImageGenCameraSelected && !languageSelectionOpen);
  const isTwoUp = Boolean(attachedImageBase64 && showLiveFeed);
  const isLive = liveSessionState === 'active' || liveSessionState === 'connecting';

  // Auto-scroll to bottom when live feed appears
  const prevShowLiveFeedRef = useRef(showLiveFeed);
  useEffect(() => {
    if (showLiveFeed && !prevShowLiveFeedRef.current) {
      onScrollToBottom?.();
    }
    prevShowLiveFeedRef.current = showLiveFeed;
  }, [showLiveFeed, onScrollToBottom]);

  // Auto-scroll to bottom when annotation mode opens
  const prevAnnotatingRef = useRef(isComposerAnnotating);
  useEffect(() => {
    if (isComposerAnnotating && !prevAnnotatingRef.current) {
      onScrollToBottom?.();
    }
    prevAnnotatingRef.current = isComposerAnnotating;
  }, [isComposerAnnotating, onScrollToBottom]);

  useEffect(() => {
    if (transcript === prevTranscriptRef.current) return;
    const shouldApply = isSttGloballyEnabled || isListening;
    if (shouldApply) {
      const raw = transcript || '';
      if (raw.length > 0 || isSttGloballyEnabled) {
        setInputText(raw);
        if (raw.trim().length >= 2) onUserInputActivity();
      }
    }
    prevTranscriptRef.current = transcript;
  }, [transcript, isSttGloballyEnabled, isListening, onUserInputActivity]);

  useEffect(() => {
    if (bubbleTextAreaRef.current) {
      bubbleTextAreaRef.current.style.height = 'auto';
      bubbleTextAreaRef.current.style.height = `${bubbleTextAreaRef.current.scrollHeight}px`;
    }
  }, [inputText]);

  // On mount, the container query (3.6cqw font-size) may not be resolved yet,
  // causing scrollHeight to return an inflated value. Re-measure after layout settles.
  useEffect(() => {
    let cancelled = false;
    requestAnimationFrame(() => {
      if (cancelled) return;
      requestAnimationFrame(() => {
        if (cancelled) return;
        if (bubbleTextAreaRef.current) {
          bubbleTextAreaRef.current.style.height = 'auto';
          bubbleTextAreaRef.current.style.height = `${bubbleTextAreaRef.current.scrollHeight}px`;
        }
      });
    });
    return () => { cancelled = true; };
  }, []);

  const handleInputChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const newText = e.target.value;
    setInputText(newText);
    if (newText.trim().length >= 2) {
      onUserInputActivity();
    }
  };

  const handleComposerPaste = useCallback((e: React.ClipboardEvent<HTMLTextAreaElement>) => {
    if (isSuggestionMode || languageSelectionOpen || !!attachedImageBase64) return;

    const pastedText = e.clipboardData.getData('text/plain');
    if (!pastedText || !pastedText.trim()) return;

    const candidate = getComposerAttachmentCandidate(pastedText);
    if (!candidate) return;

    e.preventDefault();

    const textarea = e.currentTarget;
    const selectionStart = Number.isFinite(textarea.selectionStart) ? textarea.selectionStart : textarea.value.length;
    const selectionEnd = Number.isFinite(textarea.selectionEnd) ? textarea.selectionEnd : selectionStart;
    const cleanedInsertText = candidate.cleanedText;
    const nextCaretPos = selectionStart + cleanedInsertText.length;

    setInputText(prev => {
      const safeStart = Math.max(0, Math.min(selectionStart, prev.length));
      const safeEnd = Math.max(safeStart, Math.min(selectionEnd, prev.length));
      return `${prev.slice(0, safeStart)}${cleanedInsertText}${prev.slice(safeEnd)}`;
    });

    onSetAttachedImage(
      candidate.attachment.dataUrl,
      candidate.attachment.mimeType,
      candidate.attachment.fileName
    );
    onUserInputActivity();

    requestAnimationFrame(() => {
      if (!bubbleTextAreaRef.current) return;
      bubbleTextAreaRef.current.focus();
      bubbleTextAreaRef.current.selectionStart = nextCaretPos;
      bubbleTextAreaRef.current.selectionEnd = nextCaretPos;
    });
  }, [
    attachedImageBase64,
    isSuggestionMode,
    languageSelectionOpen,
    onSetAttachedImage,
    onUserInputActivity,
  ]);

  const handleSend = async () => {
    if (languageSelectionOpen) {
      handleShowLanguageSelector();
      return;
    }
    if (isSuggestionMode) {
      if (!inputText.trim() || isCreatingSuggestion) {
        if (!inputText.trim()) onToggleSuggestionMode();
        return;
      }
      const textToSend = inputText.trim();
      setInputText('');
      await onCreateSuggestion(textToSend);
      return;
    }
    if (isSending || isSpeaking || (!inputText.trim() && !attachedImageBase64)) return;
    const textToSend = inputText.trim();
    setInputText('');
    const success = await onSendMessage(textToSend, attachedImageBase64 || undefined, attachedImageMimeType || undefined);
    if (success) {
      onSetAttachedImage(null, null);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const createDottedPaperDataUrl = useCallback((width: number, height: number) => {
    const safeWidth = Math.max(640, Math.floor(width));
    const safeHeight = Math.max(480, Math.floor(height));
    const paper = document.createElement('canvas');
    paper.width = safeWidth;
    paper.height = safeHeight;
    const ctx = paper.getContext('2d');
    if (!ctx) return null;

    ctx.fillStyle = '#f4efdf';
    ctx.fillRect(0, 0, safeWidth, safeHeight);

    const wash = ctx.createLinearGradient(0, 0, 0, safeHeight);
    wash.addColorStop(0, 'rgba(255, 255, 255, 0.18)');
    wash.addColorStop(1, 'rgba(120, 98, 63, 0.09)');
    ctx.fillStyle = wash;
    ctx.fillRect(0, 0, safeWidth, safeHeight);

    const spacing = Math.max(10, Math.round(safeWidth / 110));
    const radius = Math.max(0.8, safeWidth / 1600);
    ctx.fillStyle = 'rgba(72, 61, 42, 0.2)';
    for (let y = spacing / 2; y < safeHeight; y += spacing) {
      for (let x = spacing / 2; x < safeWidth; x += spacing) {
        ctx.beginPath();
        ctx.arc(x, y, radius, 0, Math.PI * 2);
        ctx.fill();
      }
    }

    ctx.strokeStyle = 'rgba(101, 84, 57, 0.08)';
    ctx.lineWidth = 1;
    for (let y = spacing * 2; y < safeHeight; y += spacing * 6) {
      ctx.beginPath();
      ctx.moveTo(0, y + 0.5);
      ctx.lineTo(safeWidth, y + 0.5);
      ctx.stroke();
    }

    const grainCount = Math.floor((safeWidth * safeHeight) / 1500);
    ctx.fillStyle = 'rgba(71, 56, 34, 0.05)';
    for (let i = 0; i < grainCount; i++) {
      const x = Math.floor(Math.random() * safeWidth);
      const y = Math.floor(Math.random() * safeHeight);
      ctx.fillRect(x, y, 1, 1);
    }

    return paper.toDataURL('image/jpeg', 0.92);
  }, []);

  const startComposerAnnotationFromImage = useCallback((dataUrl: string, options?: { blankCanvas?: boolean }) => {
    if (!composerAnnotateTokenRef.current) {
      composerAnnotateTokenRef.current = createUiToken(TOKEN_SUBTYPE.COMPOSER_ANNOTATE);
    }
    setComposerIsBlankCanvas(Boolean(options?.blankCanvas));
    setComposerAnnotationSourceUrl(dataUrl);

    setTimeout(() => {
      if (composerImageRef.current) {
        const img = composerImageRef.current;
        if (img.naturalWidth > 0) {
          setComposerImageAspectRatio(img.naturalWidth / img.naturalHeight);
          if (composerViewportRef.current) {
            const vw = composerViewportRef.current.clientWidth;
            setComposerScale(vw / img.naturalWidth);
          } else {
            setComposerScale(1);
          }
        }
      }
      setComposerPan({ x: 0, y: 0 });
      setComposerUndoStack([]);
      composerIsNewStrokeRef.current = true;
      setIsComposerAnnotating(true);
    }, 0);
  }, [createUiToken]);

  const handleComposerAnnotateImage = useCallback(() => {
    if (!attachedImageBase64) return;
    startComposerAnnotationFromImage(attachedImageBase64);
  }, [attachedImageBase64, startComposerAnnotationFromImage]);

  const handleComposerAnnotateVideo = useCallback(() => {
    const video = attachedPreviewVideoRef.current;
    if (!video) return;
    if (!video.paused) {
      alert(t('chat.error.pauseVideoToAnnotate'));
      return;
    }
    if (video.videoWidth === 0 || video.videoHeight === 0) return;

    const cv = document.createElement('canvas');
    cv.width = video.videoWidth;
    cv.height = video.videoHeight;
    const ctx = cv.getContext('2d');
    if (!ctx) return;
    ctx.drawImage(video, 0, 0, cv.width, cv.height);
    const frame = cv.toDataURL('image/jpeg', 0.92);
    startComposerAnnotationFromImage(frame);
  }, [t, startComposerAnnotationFromImage]);

  const {
    pdfPageNum: composerPdfPageNum,
    pdfPageCount: composerPdfPageCount,
    startPdfAnnotation: composerStartPdfAnnotation,
    changePdfPage: handleComposerPdfPageChange,
    resetPdfState: composerResetPdfState,
  } = usePdfAnnotation({
    editCanvasRef: composerEditCanvasRef,
    onStartAnnotation: startComposerAnnotationFromImage,
    setAnnotationSourceUrl: setComposerAnnotationSourceUrl,
    setUndoStack: setComposerUndoStack,
    isNewStrokeRef: composerIsNewStrokeRef,
  });

  const handleComposerAnnotatePdf = useCallback(async () => {
    if (!attachedImageBase64 || attachedImageMimeType !== 'application/pdf') return;
    await composerStartPdfAnnotation(attachedImageBase64);
  }, [attachedImageBase64, attachedImageMimeType, composerStartPdfAnnotation]);

  const handleComposerStartBlankCanvas = useCallback(() => {
    const baseWidth = typeof window !== 'undefined'
      ? Math.max(960, Math.min(1800, Math.round(window.innerWidth * 1.4)))
      : 1200;
    const baseHeight = Math.round(baseWidth * 0.75);
    const paperDataUrl = createDottedPaperDataUrl(baseWidth, baseHeight);
    if (!paperDataUrl) return;
    startComposerAnnotationFromImage(paperDataUrl, { blankCanvas: true });
  }, [createDottedPaperDataUrl, startComposerAnnotationFromImage]);

  const composerGetPos = useCallback((e: React.PointerEvent<any>) => {
    const canvas = composerEditCanvasRef.current;
    if (!canvas) return null;
    const rect = canvas.getBoundingClientRect();
    return {
      x: (e.clientX - rect.left) * (canvas.width / rect.width),
      y: (e.clientY - rect.top) * (canvas.height / rect.height),
    };
  }, []);

  const handleComposerPointerDown = useCallback((e: React.PointerEvent<HTMLDivElement>) => {
    e.preventDefault();
    composerActivePointersRef.current.push(e);
    e.currentTarget.setPointerCapture(e.pointerId);
    document.body.style.overscrollBehavior = 'none';

    if (composerActivePointersRef.current.length === 1) {
      composerIsDrawingRef.current = true;
      composerLastPosRef.current = composerGetPos(e);
      composerIsNewStrokeRef.current = true;
    } else if (composerActivePointersRef.current.length === 2) {
      // If a stroke was accidentally started by the first finger, undo it
      if (!composerIsNewStrokeRef.current) {
        const canvas = composerEditCanvasRef.current;
        const ctx = canvas?.getContext('2d');
        if (canvas && ctx) {
          setComposerUndoStack(prev => {
            if (prev.length === 0) return prev;
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            ctx.putImageData(prev[prev.length - 1], 0, 0);
            return prev.slice(0, -1);
          });
        }
      }
      composerIsDrawingRef.current = false;
      composerLastPosRef.current = null;
      const vp = composerViewportRef.current?.getBoundingClientRect();
      if (!vp) return;
      const [p1, p2] = composerActivePointersRef.current;
      composerLastPinchDistanceRef.current = Math.hypot(p1.clientX - p2.clientX, p1.clientY - p2.clientY);
      composerLastPanPointRef.current = {
        x: ((p1.clientX + p2.clientX) / 2) - vp.left,
        y: ((p1.clientY + p2.clientY) / 2) - vp.top,
      };
    }
  }, [composerGetPos]);

  const handleComposerPointerMove = useCallback((e: React.PointerEvent<HTMLDivElement>) => {
    e.preventDefault();
    const idx = composerActivePointersRef.current.findIndex(p => p.pointerId === e.pointerId);
    if (idx === -1) return;
    composerActivePointersRef.current[idx] = e;

    if (composerActivePointersRef.current.length === 1 && composerIsDrawingRef.current && composerLastPosRef.current) {
      const cur = composerGetPos(e);
      const canvas = composerEditCanvasRef.current;
      const ctx = canvas?.getContext('2d');
      if (ctx && cur) {
        if (composerIsNewStrokeRef.current) {
          const snap = ctx.getImageData(0, 0, canvas!.width, canvas!.height);
          setComposerUndoStack(prev => [...prev, snap]);
          composerIsNewStrokeRef.current = false;
        }
        ctx.beginPath();
        ctx.moveTo(composerLastPosRef.current.x, composerLastPosRef.current.y);
        ctx.lineTo(cur.x, cur.y);
        ctx.stroke();
        composerLastPosRef.current = cur;
      }
    } else if (composerActivePointersRef.current.length === 2) {
      const vp = composerViewportRef.current?.getBoundingClientRect();
      if (!vp) return;
      const [p1, p2] = composerActivePointersRef.current;
      const newDist = Math.hypot(p1.clientX - p2.clientX, p1.clientY - p2.clientY);
      const center = { x: ((p1.clientX + p2.clientX) / 2) - vp.left, y: ((p1.clientY + p2.clientY) / 2) - vp.top };
      const panDx = composerLastPanPointRef.current ? center.x - composerLastPanPointRef.current.x : 0;
      const panDy = composerLastPanPointRef.current ? center.y - composerLastPanPointRef.current.y : 0;
      const factor = composerLastPinchDistanceRef.current > 0 ? newDist / composerLastPinchDistanceRef.current : 1;

      setComposerPan(prev => {
        const panned = { x: prev.x + panDx, y: prev.y + panDy };
        const cursorFromCenter = { x: center.x - vp.width / 2, y: center.y - vp.height / 2 };
        return {
          x: cursorFromCenter.x - (cursorFromCenter.x - panned.x) * factor,
          y: cursorFromCenter.y - (cursorFromCenter.y - panned.y) * factor,
        };
      });
      setComposerScale(prev => Math.max(0.2, Math.min(prev * factor, 15)));

      composerLastPinchDistanceRef.current = newDist;
      composerLastPanPointRef.current = center;
    }
  }, [composerGetPos]);

  const handleComposerPointerUp = useCallback((e: React.PointerEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.currentTarget.releasePointerCapture(e.pointerId);
    composerActivePointersRef.current = composerActivePointersRef.current.filter(p => p.pointerId !== e.pointerId);

    if (composerActivePointersRef.current.length < 2) {
      composerLastPinchDistanceRef.current = 0;
      composerLastPanPointRef.current = null;
    }
    if (composerActivePointersRef.current.length < 1) {
      composerIsDrawingRef.current = false;
      composerLastPosRef.current = null;
      document.body.style.overscrollBehavior = 'auto';
    }
  }, []);

  const handleComposerWheel = useCallback((e: React.WheelEvent<HTMLDivElement>) => {
    e.preventDefault();
    const factor = e.deltaY > 0 ? 0.9 : 1.1;
    const vp = e.currentTarget.getBoundingClientRect();
    const cursor = { x: e.clientX - vp.left, y: e.clientY - vp.top };
    setComposerScale(prev => {
      const next = Math.max(0.2, Math.min(prev * factor, 15));
      setComposerPan(prevPan => ({
        x: cursor.x - (cursor.x - prevPan.x) * (next / prev),
        y: cursor.y - (cursor.y - prevPan.y) * (next / prev),
      }));
      return next;
    });
  }, []);

  const handleComposerUndo = useCallback(() => {
    const canvas = composerEditCanvasRef.current;
    const ctx = canvas?.getContext('2d');
    if (!canvas || !ctx) return;

    setComposerUndoStack(prev => {
      if (prev.length === 0) return prev;
      const newStack = prev.slice(0, -1);
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      // Restore the last snapshot (state before the last stroke was drawn)
      ctx.putImageData(prev[prev.length - 1], 0, 0);
      return newStack;
    });
  }, []);

  const handleComposerCancel = useCallback(() => {
    setIsComposerAnnotating(false);
    setComposerAnnotationSourceUrl(null);
    setComposerIsBlankCanvas(false);
    composerIsDrawingRef.current = false;
    composerLastPosRef.current = null;
    setComposerScale(1);
    setComposerPan({ x: 0, y: 0 });
    composerActivePointersRef.current = [];
    setComposerUndoStack([]);
    composerIsNewStrokeRef.current = true;
    composerResetPdfState();
    if (composerAnnotateTokenRef.current) {
      endUiTask(composerAnnotateTokenRef.current);
      composerAnnotateTokenRef.current = null;
    }
  }, [endUiTask, composerResetPdfState]);

  const handleComposerSave = useCallback(() => {
    if (!composerEditCanvasRef.current || !composerImageRef.current || !composerViewportRef.current) return;

    const baseImage = composerImageRef.current;
    const drawingCanvas = composerEditCanvasRef.current;
    const viewport = composerViewportRef.current;
    const rect = viewport.getBoundingClientRect();

    const out = document.createElement('canvas');
    out.width = rect.width;
    out.height = rect.height;
    const ctx = out.getContext('2d')!;
    ctx.save();
    ctx.translate(out.width / 2, out.height / 2);
    ctx.translate(composerPan.x, composerPan.y);
    ctx.scale(composerScale, composerScale);
    ctx.drawImage(baseImage, -baseImage.naturalWidth / 2, -baseImage.naturalHeight / 2);
    ctx.drawImage(drawingCanvas, -baseImage.naturalWidth / 2, -baseImage.naturalHeight / 2);
    ctx.restore();

    const dataUrl = out.toDataURL('image/jpeg', 0.9);

    onSetAttachedImage(dataUrl, 'image/jpeg');
    onUserInputActivity();
    handleComposerCancel();
  }, [composerPan.x, composerPan.y, composerScale, onSetAttachedImage, onUserInputActivity, handleComposerCancel]);

  useEffect(() => {
    if (!isComposerAnnotating || !composerAnnotationSourceUrl) return;
    const canvas = composerEditCanvasRef.current;
    const ctx = canvas?.getContext('2d');
    const img = composerImageRef.current;
    if (!canvas || !ctx || !img) return;

    const setup = () => {
      if (img.naturalWidth > 0) {
        canvas.width = img.naturalWidth;
        canvas.height = img.naturalHeight;
        ctx.strokeStyle = composerIsBlankCanvas ? 'rgba(56, 43, 24, 0.9)' : 'hsl(5, 45%, 55%)';
        ctx.lineWidth = composerIsBlankCanvas
          ? Math.max(4, img.naturalWidth * 0.008)
          : Math.max(5, img.naturalWidth * 0.01);
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
      }
    };
    if (img.complete && img.naturalWidth > 0) setup();
    else img.addEventListener('load', setup);
    return () => img.removeEventListener('load', setup);
  }, [isComposerAnnotating, composerAnnotationSourceUrl, composerIsBlankCanvas]);

  const handleImageAttach = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (paperclipOpenTokenRef.current) {
      endUiTask(paperclipOpenTokenRef.current);
      paperclipOpenTokenRef.current = null;
    }
    const file = e.target.files?.[0];
    if (file) {
      const normalizedMimeType = normalizeAttachmentMimeType(file);

      if (normalizedMimeType.startsWith('video/')) {
        const video = document.createElement('video');
        video.preload = 'metadata';
        video.onloadedmetadata = () => {
          window.URL.revokeObjectURL(video.src);
          const reader = new FileReader();
          reader.onloadend = () => { onSetAttachedImage(reader.result as string, normalizedMimeType, file.name || null); };
          reader.readAsDataURL(file);
        };
        video.onerror = () => { window.URL.revokeObjectURL(video.src); console.error(t('chat.error.videoMetadataError')); if (fileInputRef.current) fileInputRef.current.value = ''; };
        video.src = URL.createObjectURL(file);
      } else {
        const reader = new FileReader();
        reader.onloadend = () => { onSetAttachedImage(reader.result as string, normalizedMimeType, file.name || null); };
        reader.readAsDataURL(file);
      }
    }
  };

  const removeAttachedImage = () => { onSetAttachedImage(null, null); if (fileInputRef.current) fileInputRef.current.value = ''; };

  useEffect(() => {
    const handleWindowFocus = () => {
      if (paperclipOpenTokenRef.current) {
        endUiTask(paperclipOpenTokenRef.current);
        paperclipOpenTokenRef.current = null;
      }
    };
    window.addEventListener('focus', handleWindowFocus);
    return () => window.removeEventListener('focus', handleWindowFocus);
  }, [endUiTask]);

  const prepDisplay = useMemo(() => {
    if (!sendPrep || !sendPrep.active) return null;
    const parts: string[] = [];
    parts.push(sendPrep.label || 'Preparing...');
    if (typeof sendPrep.done === 'number' && typeof sendPrep.total === 'number' && sendPrep.total > 0) parts.push(`${sendPrep.done}/${sendPrep.total}`);
    if (typeof sendPrep.etaMs === 'number') { const sec = Math.ceil(sendPrep.etaMs / 1000); if (isFinite(sec)) parts.push(`~${sec}s`); }
    return parts.join(' · ');
  }, [sendPrep]);

  const sttLangFlag = useMemo(() => {
    if (sttLanguageCode === targetLanguageDef?.langCode) return targetLanguageDef.flag;
    if (sttLanguageCode === nativeLanguageDef?.langCode) return nativeLanguageDef.flag;
    return targetLanguageDef?.flag;
  }, [sttLanguageCode, targetLanguageDef, nativeLanguageDef]);

  const getPlaceholderText = () => {
    if (languageSelectionOpen) return '';
    if (prepDisplay) return prepDisplay;
    if (isSuggestionMode) {
      if (isCreatingSuggestion) return t('chat.suggestion.creating');
      if (isListening) return t('chat.placeholder.suggestion.listening', { language: sttLangFlag });
      if (isSttGloballyEnabled) return t('chat.placeholder.suggestion.sttActive', { language: sttLangFlag });
      return t('chat.placeholder.suggestion.sttInactive', { language: sttLangFlag });
    }
    if (isListening) return t('chat.placeholder.normal.listening', { language: sttLangFlag });
    if (isSttGloballyEnabled) return t('chat.placeholder.normal.sttActive', { language: sttLangFlag });
    return t('chat.placeholder.normal.sttInactive', { language: sttLangFlag });
  };

  const containerClass = isSuggestionMode
    ? 'bg-sugg-input-bg text-sugg-input-text shadow-sm sketchy-border focus-within:ring-2 focus-within:ring-input-focus-ring'
    : 'bg-chat-input-bg text-chat-input-text shadow-sm sketchy-border focus-within:ring-2 focus-within:ring-input-focus-ring';

  const sendButtonStyle = isSuggestionMode ? 'bg-send-sugg-btn-bg text-send-sugg-btn-text hover:bg-send-sugg-btn-bg/80 focus:ring-focus-ring' : 'bg-send-btn-bg text-send-btn-text hover:bg-send-btn-bg/80 focus:ring-focus-ring';
  const iconButtonStyle = isSuggestionMode ? 'text-sugg-input-icon hover:text-sugg-input-text hover:bg-sugg-outer-bg' : 'text-chat-input-icon/70 hover:text-chat-input-icon hover:bg-chat-input-icon-hover-bg';
  const overlayIconShadowStyle: React.CSSProperties = {
    filter: 'drop-shadow(0 1px 2px rgba(0,0,0,0.72))',
  };
  const overlayIconButtonBaseClasses = 'rounded-full opacity-85 transition-all duration-200 hover:opacity-100 focus:outline-none focus:ring-2 active:scale-95 disabled:opacity-40 disabled:cursor-default';
  const overlayNeutralIconButtonClasses = `${overlayIconButtonBaseClasses} text-white/90 hover:text-white focus:ring-white/40`;
  const overlayAccentIconButtonClasses = `${overlayIconButtonBaseClasses} text-annotation-btn-text focus:ring-input-focus-ring`;

  const handlePaperclipClick = () => {
    if (!paperclipOpenTokenRef.current) {
      paperclipOpenTokenRef.current = createUiToken(TOKEN_SUBTYPE.ATTACH_FILE);
    }
    // Trigger file input since label htmlFor no longer works with button
    fileInputRef.current?.click();
  };

  const outerContainerClass = isSuggestionMode
    ? 'bg-sugg-outer-bg text-sugg-input-text'
    : 'bg-chat-outer-bg text-chat-outer-text';

  return (
    <>
      <style>{`
        @keyframes fade-in-up {
          from { opacity: 0; transform: translateY(8px); }
          to { opacity: 1; transform: translateY(0); }
        }
        .animate-fade-in-up {
          animation: fade-in-up 220ms ease-out both;
        }
      `}</style>
        <>
          {/* Core input controls - wrapped in the accent container */}
          <div
            className={`transition-colors duration-300 sketch-shape-3 p-3 shadow-lg w-full max-w-2xl sketchy-border ${outerContainerClass} relative`}
            style={{
              // @ts-ignore
              containerType: 'inline-size'
            }}
          >

          <div className={`relative w-full flex flex-col overflow-hidden transition-colors ${containerClass}`}>
            {isLive && !isSuggestionMode && !languageSelectionOpen ? (
              <div className="w-full py-3 px-4 min-h-[50px] flex items-center" style={{ fontSize: '3.6cqw', lineHeight: 1.35 }}>
                <span className="opacity-60 italic">{t('chat.liveSession.activeIndicator')}</span>
              </div>
            ) : languageSelectionOpen ? (
              <SessionControls />
            ) : (
              <Composer
                t={t}
                inputText={inputText}
                placeholder={getPlaceholderText()}
                isDisabled={isSending || (isListening && isSttGloballyEnabled) || (isSuggestionMode && isCreatingSuggestion)}
                isDrawDisabled={isSending || isSpeaking || isComposerAnnotating || (isSuggestionMode && isCreatingSuggestion)}
                onChange={handleInputChange}
                onKeyDown={handleKeyDown}
                onPaste={handleComposerPaste}
                onOpenDrawCanvas={handleComposerStartBlankCanvas}
                bubbleTextAreaRef={bubbleTextAreaRef}
                prepDisplay={prepDisplay}
                drawCanvasLabel={t('chat.drawMessage')}
                drawButtonClassName={iconButtonStyle}
              />
            )}

            <div className="flex items-center justify-between px-2 pb-2">
              <CameraControls
                t={t}
                isLanguageSelectionOpen={languageSelectionOpen}
                isSuggestionMode={isSuggestionMode}
                fileInputRef={fileInputRef}
                onImageAttach={handleImageAttach}
                onPaperclipClick={handlePaperclipClick}
                availableCameras={availableCameras}
                selectedCameraId={selectedCameraId}
                currentCameraFacingMode={currentCameraFacingMode}
                isImageGenCameraSelected={isImageGenCameraSelected}
                sendWithSnapshotEnabled={sendWithSnapshotEnabled}
                useVisualContextForReengagementEnabled={useVisualContextForReengagementEnabled}
                imageGenerationModeEnabled={imageGenerationModeEnabled}
                onSelectCamera={handleSelectCamera}
                onToggleSendWithSnapshot={onToggleSendWithSnapshot}
                onToggleUseVisualContextForReengagement={onToggleUseVisualContextForReengagement}
                onToggleImageGenerationMode={onToggleImageGenerationMode}
                iconButtonStyle={iconButtonStyle}
                isLive={isLive}
              />

            {/* Show controls if not live, OR if we are in suggestion mode */}
            {(!isLive || isSuggestionMode || languageSelectionOpen) && (
              <div className="flex items-center space-x-1">
                {!isLive && (
                  <AudioControls
                  t={t}
                  isLanguageSelectionOpen={languageSelectionOpen}
                  isSttSupported={isSttSupported}
                  isSttGloballyEnabled={isSttGloballyEnabled}
                  isListening={isListening}
                  isSending={isSending}
                  isSpeaking={isSpeaking}
                  targetLanguageDef={targetLanguageDef}
                  nativeLanguageDef={nativeLanguageDef}
                  isSuggestionMode={isSuggestionMode}
                  onSttToggle={onSttToggle}
                  onSetAttachedImage={onSetAttachedImage}
                  onUserInputActivity={onUserInputActivity}
                  onStopSilentObserver={onStopSilentObserver}
                />
                )}
                <button
                  type="button"
                  onClick={handleSend}
                  className={`p-2 focus:outline-none focus:ring-2 transition-colors disabled:opacity-50 shadow-sm sketchy-border-thin ${sendButtonStyle}`}
                  disabled={isSending || ((!inputText.trim() && !attachedImageBase64) && !languageSelectionOpen) || isSpeaking || (isSuggestionMode && isCreatingSuggestion)}
                  aria-label={
                    languageSelectionOpen
                      ? 'Confirm language selection'
                      : isSuggestionMode
                        ? (isCreatingSuggestion ? t('chat.suggestion.creating') : t('chat.suggestion.createAction'))
                        : (sendPrep && sendPrep.active ? (sendPrep.label || t('chat.sendPrep.finalizing')) : t('chat.sendMessage'))
                  }
                >
                  {languageSelectionOpen
                    ? <IconUndo className="w-5 h-5" />
                    : isSuggestionMode
                      ? (isCreatingSuggestion ? <SmallSpinner className="w-5 h-5" /> : <IconPlus className="w-5 h-5" />)
                      : (sendPrep && sendPrep.active ? <SmallSpinner className="w-5 h-5" /> : <IconSend className="w-5 h-5" />)}
                </button>
              </div>
            )}
            </div>
          </div>

          {!isLive && sttError && <p className={`w-full max-w-2xl p-1 rounded mt-1 ${isSuggestionMode ? 'text-input-error-text bg-input-error-bg/10' : 'text-input-error-text/80 bg-input-error-bg/30'}`} style={{ fontSize: '0.75rem' }} role="alert">{t('chat.error.sttError', {error: sttError})}</p>}
          {autoCaptureError && <p className={`w-full max-w-2xl p-1 rounded mt-1 ${isSuggestionMode ? 'text-input-error-text bg-input-error-bg/10' : 'text-input-error-text/80 bg-input-error-bg/30'}`} style={{ fontSize: '0.75rem' }} role="alert">{t('chat.error.autoCaptureCameraError', {error: autoCaptureError})}</p>}
          {snapshotUserError && <p className={`w-full max-w-2xl p-1 rounded mt-1 ${isSuggestionMode ? 'text-sugg-input-text bg-sugg-input-bg/10' : 'text-input-error-text/80 bg-snapshot-error-bg/30'}`} style={{ fontSize: '0.75rem' }} role="alert">{t('chat.error.snapshotUserError', {error: snapshotUserError})}</p>}
          </div>{/* end outer accent container */}

          {/* Media attachments and annotation - outside accent container, free-floating */}
          {!languageSelectionOpen && (
            <div className="animate-fade-in-up mt-3 w-full max-w-2xl">
              <MediaAttachments
                t={t}
                isSuggestionMode={isSuggestionMode}
                attachedImageBase64={attachedImageBase64}
                attachedImageMimeType={attachedImageMimeType}
                attachedFileName={attachedFileName}
                showLiveFeed={showLiveFeed}
                isTwoUp={isTwoUp}
                liveVideoStream={liveVideoStream}
                liveSessionState={liveSessionState}
                liveSessionError={liveSessionError}
                onStartLiveSession={onStartLiveSession}
                onStopLiveSession={onStopLiveSession}
                onBeforeStartVideoRecording={onStopSilentObserver}
                onRemoveAttachment={removeAttachedImage}
                onAnnotateImage={handleComposerAnnotateImage}
                onAnnotateVideo={handleComposerAnnotateVideo}
                onAnnotatePdf={handleComposerAnnotatePdf}
                onSetAttachedImage={onSetAttachedImage}
                onUserInputActivity={onUserInputActivity}
                attachedPreviewVideoRef={attachedPreviewVideoRef}
                isSilentObserverActive={silentObserverState === 'active' && !isLive}
              />
            </div>
          )}

          {isComposerAnnotating && (
            <div className="mt-3 w-full max-w-2xl animate-fade-in-up">
              <div
                ref={composerViewportRef}
                className="relative w-full max-h-[75vh] bg-black rounded-md overflow-hidden transition-all duration-300"
                style={{
                  aspectRatio: composerImageAspectRatio || undefined,
                  touchAction: 'none',
                  backgroundColor: composerIsBlankCanvas ? '#f4efdf' : '#000000',
                }}
                onPointerDown={handleComposerPointerDown}
                onPointerMove={handleComposerPointerMove}
                onPointerUp={handleComposerPointerUp}
                onPointerCancel={handleComposerPointerUp}
                onWheel={handleComposerWheel}
              >
                <div
                  style={{
                    width: composerImageRef.current?.naturalWidth,
                    height: composerImageRef.current?.naturalHeight,
                    position: 'absolute',
                    top: '50%',
                    left: '50%',
                    transform: `translate(-50%,-50%) translate(${composerPan.x}px, ${composerPan.y}px) scale(${composerScale})`,
                    transition: composerActivePointersRef.current.length > 0 ? 'none' : 'transform 0.1s ease-out',
                  }}
                >
                  <img
                    ref={composerImageRef}
                    src={composerAnnotationSourceUrl!}
                    alt={t('chat.annotateModal.editingPreviewAlt')}
                    className="block w-full h-full object-contain pointer-events-none"
                    style={{ opacity: 1 }}
                    onLoad={(e) => {
                      const img = e.currentTarget;
                      if (img.naturalWidth > 0) {
                        setComposerImageAspectRatio(img.naturalWidth / img.naturalHeight);
                        if (composerViewportRef.current) {
                          const vw = composerViewportRef.current.clientWidth;
                          setComposerScale(vw / img.naturalWidth);
                          setComposerPan({ x: 0, y: 0 });
                        }
                      }
                    }}
                  />
                  <canvas ref={composerEditCanvasRef} className="absolute top-0 left-0 w-full h-full cursor-crosshair" />
                </div>
                <div className="absolute inset-0 pointer-events-none">
                  {composerPdfPageCount > 1 && (
                    <div
                      className="absolute top-2 left-2 pointer-events-auto flex items-center space-x-1"
                      onPointerDown={(e) => { e.stopPropagation(); e.preventDefault(); }}
                      onPointerUp={(e) => { e.stopPropagation(); e.preventDefault(); }}
                      onPointerCancel={(e) => { e.stopPropagation(); e.preventDefault(); }}
                      onClick={(e) => { e.stopPropagation(); e.preventDefault(); }}
                    >
                      <button
                        type="button"
                        onClick={() => handleComposerPdfPageChange(-1)}
                        disabled={composerPdfPageNum <= 1}
                        className={`p-1.5 ${overlayNeutralIconButtonClasses}`}
                      >
                        <span style={overlayIconShadowStyle}>
                          <IconChevronLeft className="w-4 h-4" />
                        </span>
                      </button>
                      <span className="text-white text-xs font-medium tabular-nums bg-black/60 rounded px-1.5 py-0.5 select-none">
                        {composerPdfPageNum}/{composerPdfPageCount}
                      </span>
                      <button
                        type="button"
                        onClick={() => handleComposerPdfPageChange(1)}
                        disabled={composerPdfPageNum >= composerPdfPageCount}
                        className={`p-1.5 ${overlayNeutralIconButtonClasses}`}
                      >
                        <span style={overlayIconShadowStyle}>
                          <IconChevronRight className="w-4 h-4" />
                        </span>
                      </button>
                    </div>
                  )}

                  <div
                    className="absolute top-2 right-2 pointer-events-auto"
                    onPointerDown={(e) => { e.stopPropagation(); e.preventDefault(); }}
                    onPointerUp={(e) => { e.stopPropagation(); e.preventDefault(); }}
                    onPointerCancel={(e) => { e.stopPropagation(); e.preventDefault(); }}
                    onClick={(e) => { e.stopPropagation(); e.preventDefault(); }}
                  >
                    <button
                      type="button"
                      onClick={handleComposerCancel}
                      className={`p-2 ${overlayNeutralIconButtonClasses}`}
                      title={t('chat.annotateModal.cancel')}
                      aria-label={t('chat.annotateModal.cancel')}
                    >
                      <span style={overlayIconShadowStyle}>
                        <IconXMark className="w-5 h-5" />
                      </span>
                    </button>
                  </div>

                  <div
                    className="absolute bottom-2 left-2 pointer-events-auto"
                    onPointerDown={(e) => { e.stopPropagation(); e.preventDefault(); }}
                    onPointerUp={(e) => { e.stopPropagation(); e.preventDefault(); }}
                    onPointerCancel={(e) => { e.stopPropagation(); e.preventDefault(); }}
                    onClick={(e) => { e.stopPropagation(); e.preventDefault(); }}
                  >
                    <button
                      type="button"
                      onClick={handleComposerUndo}
                      disabled={composerUndoStack.length === 0}
                      className={`p-2 ${overlayNeutralIconButtonClasses}`}
                      title={t('chat.annotateModal.undo')}
                      aria-label={t('chat.annotateModal.undo')}
                      aria-disabled={composerUndoStack.length === 0}
                    >
                      <span style={overlayIconShadowStyle}>
                        <IconUndo className="w-5 h-5" />
                      </span>
                    </button>
                  </div>

                  <div
                    className="absolute bottom-2 right-2 pointer-events-auto"
                    onPointerDown={(e) => { e.stopPropagation(); e.preventDefault(); }}
                    onPointerUp={(e) => { e.stopPropagation(); e.preventDefault(); }}
                    onPointerCancel={(e) => { e.stopPropagation(); e.preventDefault(); }}
                    onClick={(e) => { e.stopPropagation(); e.preventDefault(); }}
                  >
                    <button
                      type="button"
                      onClick={handleComposerSave}
                      className={`p-2 ${overlayAccentIconButtonClasses}`}
                      title={t('chat.annotateModal.saveAndAttach')}
                      aria-label={t('chat.annotateModal.saveAndAttach')}
                    >
                      <span style={overlayIconShadowStyle}>
                        <IconCheck className="w-5 h-5" />
                      </span>
                    </button>
                  </div>
                </div>
              </div>
            </div>
          )}


        </>
    </>
  );
};

export default InputArea;
