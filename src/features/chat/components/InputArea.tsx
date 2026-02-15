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

interface InputAreaProps {
  onSttToggle: () => void;
  onSendMessage: (text: string, imageBase64?: string, imageMimeType?: string) => Promise<boolean>;
  onUserInputActivity: () => void;
  onStartLiveSession: () => Promise<void> | void;
  onStopLiveSession: () => void;
  onToggleSuggestionMode: (force?: boolean) => void;
  onCreateSuggestion: (text: string) => Promise<void>;
  onToggleSendWithSnapshot: () => void;
  onToggleUseVisualContextForReengagement: () => void;
  onToggleImageGenerationMode: () => void;
}

const InputArea: React.FC<InputAreaProps> = ({
  onSttToggle,
  onSendMessage,
  onUserInputActivity,
  onStartLiveSession,
  onStopLiveSession,
  onToggleSuggestionMode,
  onCreateSuggestion,
  onToggleSendWithSnapshot,
  onToggleUseVisualContextForReengagement,
  onToggleImageGenerationMode,
}) => {
  const { t } = useAppTranslations();
  const settings = useMaestroStore(state => state.settings);
  const targetLanguageDef = useMaestroStore(selectTargetLanguageDef) || ALL_LANGUAGES[0];
  const nativeLanguageDef = useMaestroStore(selectNativeLanguageDef) || ALL_LANGUAGES[0];
  const attachedImageBase64 = useMaestroStore(state => state.attachedImageBase64);
  const attachedImageMimeType = useMaestroStore(state => state.attachedImageMimeType);
  const sendPrep = useMaestroStore(state => state.sendPrep);
  const transcript = useMaestroStore(state => state.transcript);
  const sttError = useMaestroStore(state => state.sttError);
  const liveVideoStream = useMaestroStore(state => state.liveVideoStream);
  const liveSessionState = useMaestroStore(state => state.liveSessionState);
  const liveSessionError = useMaestroStore(state => state.liveSessionError);
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

  // Read-only live store-backed ref to avoid stale closures; setter intentionally no-op
  const settingsRef = useMemo<React.MutableRefObject<typeof settings>>(() => ({
    get current() {
      return useMaestroStore.getState().settings;
    },
    set current(_value) {},
  }), []);

  // Read-only live store-backed ref to avoid stale closures; setter intentionally no-op
  const messagesRef = useMemo<React.MutableRefObject<any[]>>(() => ({
    get current() {
      return useMaestroStore.getState().messages;
    },
    set current(_value) {},
  }), []);

  // Read-only live store-backed ref to avoid stale closures; setter intentionally no-op
  const isSendingRef = useMemo<React.MutableRefObject<boolean>>(() => ({
    get current() {
      return selectIsSending(useMaestroStore.getState());
    },
    set current(_value) {},
  }), []);

  const isSuggestionMode = settings.isSuggestionMode;
  const isSttGloballyEnabled = settings.stt.enabled;
  const sttLanguageCode = settings.stt.language;
  const isSttSupported = microphoneApiAvailable;
  const sendWithSnapshotEnabled = settings.sendWithSnapshotEnabled;
  const useVisualContextForReengagementEnabled = settings.smartReengagement.useVisualContext;
  const imageGenerationModeEnabled = settings.imageGenerationModeEnabled;
  const selectedCameraId = settings.selectedCameraId;
  const isImageGenCameraSelected = selectedCameraId === IMAGE_GEN_CAMERA_ID;

  const onSetAttachedImage = useCallback((base64: string | null, mimeType: string | null) => {
    setAttachedImage(base64, mimeType);
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
  const [backgroundHint, setBackgroundHint] = useState('');
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

  useEffect(() => {
    if (transcript === prevTranscriptRef.current) return;
    const shouldApply = isSttGloballyEnabled || isListening;
    if (shouldApply) {
      const raw = transcript || '';
      const bgMatches = raw.match(/\[[^\]]*\]/g) || [];
      const latestBg = bgMatches.length ? bgMatches[bgMatches.length - 1].trim() : '';
      setBackgroundHint(latestBg);
      const cleaned = raw.replace(/\[[^\]]*\]/g, ' ').replace(/\s+/g, ' ').trimStart();
      if (cleaned.length > 0 || isSttGloballyEnabled) {
        setInputText(cleaned);
        if (cleaned.trim().length >= 2) onUserInputActivity();
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

  const handleInputChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const newText = e.target.value;
    setInputText(newText);
    if (newText.replace(/\[[^\]]*\]/g, ' ').trim().length >= 2) {
      onUserInputActivity();
    }
  };

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

  const startComposerAnnotationFromImage = useCallback((dataUrl: string) => {
    if (!composerAnnotateTokenRef.current) {
      composerAnnotateTokenRef.current = createUiToken(TOKEN_SUBTYPE.COMPOSER_ANNOTATE);
    }
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
        ctx.strokeStyle = '#EF4444';
        ctx.lineWidth = Math.max(5, img.naturalWidth * 0.01);
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
      }
    };
    if (img.complete && img.naturalWidth > 0) setup();
    else img.addEventListener('load', setup);
    return () => img.removeEventListener('load', setup);
  }, [isComposerAnnotating, composerAnnotationSourceUrl]);

  const handleImageAttach = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (paperclipOpenTokenRef.current) {
      endUiTask(paperclipOpenTokenRef.current);
      paperclipOpenTokenRef.current = null;
    }
    const file = e.target.files?.[0];
    if (file) {
      if (file.type.startsWith('video/')) {
        const video = document.createElement('video');
        video.preload = 'metadata';
        video.onloadedmetadata = () => {
          window.URL.revokeObjectURL(video.src);
          const reader = new FileReader();
          reader.onloadend = () => { onSetAttachedImage(reader.result as string, file.type); };
          reader.readAsDataURL(file);
        };
        video.onerror = () => { window.URL.revokeObjectURL(video.src); console.error(t('chat.error.videoMetadataError')); if (fileInputRef.current) fileInputRef.current.value = ''; };
        video.src = URL.createObjectURL(file);
      } else {
        const reader = new FileReader();
        reader.onloadend = () => { onSetAttachedImage(reader.result as string, file.type); };
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
      if (isListening) return backgroundHint ? `${t('chat.placeholder.suggestion.listening', { language: sttLangFlag })}  ${backgroundHint}` : t('chat.placeholder.suggestion.listening', { language: sttLangFlag });
      if (isSttGloballyEnabled) return backgroundHint ? `${t('chat.placeholder.suggestion.sttActive', { language: sttLangFlag })}  ${backgroundHint}` : t('chat.placeholder.suggestion.sttActive', { language: sttLangFlag });
      return t('chat.placeholder.suggestion.sttInactive', { language: sttLangFlag });
    }
    if (isListening) return backgroundHint ? `${t('chat.placeholder.normal.listening', { language: sttLangFlag })}  ${backgroundHint}` : t('chat.placeholder.normal.listening', { language: sttLangFlag });
    if (isSttGloballyEnabled) return backgroundHint ? `${t('chat.placeholder.normal.sttActive', { language: sttLangFlag })}  ${backgroundHint}` : t('chat.placeholder.normal.sttActive', { language: sttLangFlag });
    return t('chat.placeholder.normal.sttInactive', { language: sttLangFlag });
  };

  const containerClass = isSuggestionMode
    ? 'bg-card text-foreground shadow-sm sketchy-border focus-within:ring-2 focus-within:ring-pencil-light'
    : 'bg-primary text-primary-foreground shadow-sm sketchy-border focus-within:ring-2 focus-within:ring-pencil-light';

  const sendButtonStyle = isSuggestionMode ? 'bg-primary text-primary-foreground hover:bg-primary/80 focus:ring-ring' : 'bg-card text-primary hover:bg-secondary focus:ring-primary';
  const iconButtonStyle = isSuggestionMode ? 'text-muted-foreground hover:text-foreground hover:bg-secondary' : 'text-primary-foreground/70 hover:text-primary-foreground hover:bg-white/20';

  const handlePaperclipClick = () => {
    if (!paperclipOpenTokenRef.current) {
      paperclipOpenTokenRef.current = createUiToken(TOKEN_SUBTYPE.ATTACH_FILE);
    }
    // Trigger file input since label htmlFor no longer works with button
    fileInputRef.current?.click();
  };

  const outerContainerClass = isSuggestionMode
    ? 'bg-secondary text-foreground'
    : 'bg-accent text-accent-foreground';

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
            className={`transition-colors duration-300 rounded-sketchy p-3 shadow-lg w-full max-w-2xl sketchy-border ${outerContainerClass} relative`}
            style={{
              // @ts-ignore
              containerType: 'inline-size'
            }}
          >

          <div className={`relative w-full flex flex-col overflow-hidden transition-colors ${containerClass}`}>
            {languageSelectionOpen ? (
              <SessionControls />
            ) : (
              <Composer
                t={t}
                inputText={inputText}
                placeholder={getPlaceholderText()}
                isDisabled={isSending || (isListening && isSttGloballyEnabled) || (isSuggestionMode && isCreatingSuggestion)}
                onChange={handleInputChange}
                onKeyDown={handleKeyDown}
                bubbleTextAreaRef={bubbleTextAreaRef}
                prepDisplay={prepDisplay}
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
              />

              <div className="flex items-center space-x-1">
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
                />
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
            </div>
          </div>

          {sttError && <p className={`w-full max-w-2xl p-1 rounded mt-1 ${isSuggestionMode ? 'text-destructive bg-destructive/10' : 'text-red-200 bg-red-900/50'}`} style={{ fontSize: '0.75rem' }} role="alert">{t('chat.error.sttError', {error: sttError})}</p>}
          {autoCaptureError && <p className={`w-full max-w-2xl p-1 rounded mt-1 ${isSuggestionMode ? 'text-destructive bg-destructive/10' : 'text-red-200 bg-red-900/50'}`} style={{ fontSize: '0.75rem' }} role="alert">{t('chat.error.autoCaptureCameraError', {error: autoCaptureError})}</p>}
          {snapshotUserError && <p className={`w-full max-w-2xl p-1 rounded mt-1 ${isSuggestionMode ? 'text-primary bg-primary/10' : 'text-yellow-200 bg-yellow-900/50'}`} style={{ fontSize: '0.75rem' }} role="alert">{t('chat.error.snapshotUserError', {error: snapshotUserError})}</p>}
          </div>{/* end outer accent container */}

          {/* Media attachments and annotation - outside accent container, free-floating */}
          {!languageSelectionOpen && (
            <div className="animate-fade-in-up mt-3 w-full max-w-2xl">
              <MediaAttachments
                t={t}
                isSuggestionMode={isSuggestionMode}
                attachedImageBase64={attachedImageBase64}
                attachedImageMimeType={attachedImageMimeType}
                showLiveFeed={showLiveFeed}
                isTwoUp={isTwoUp}
                liveVideoStream={liveVideoStream}
                liveSessionState={liveSessionState}
                liveSessionError={liveSessionError}
                onStartLiveSession={onStartLiveSession}
                onStopLiveSession={onStopLiveSession}
                onRemoveAttachment={removeAttachedImage}
                onAnnotateImage={handleComposerAnnotateImage}
                onAnnotateVideo={handleComposerAnnotateVideo}
                onAnnotatePdf={handleComposerAnnotatePdf}
                onSetAttachedImage={onSetAttachedImage}
                onUserInputActivity={onUserInputActivity}
                attachedPreviewVideoRef={attachedPreviewVideoRef}
              />
            </div>
          )}

          {isComposerAnnotating && (
            <div className="mt-3 w-full max-w-2xl animate-fade-in-up">
              <div
                ref={composerViewportRef}
                className="relative w-full max-h-[75vh] bg-black rounded-md overflow-hidden transition-all duration-300"
                style={{ aspectRatio: composerImageAspectRatio || undefined, touchAction: 'none' }}
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
                        className="p-1.5 rounded-full bg-black/60 text-white hover:bg-black disabled:opacity-40 disabled:cursor-default focus:outline-none"
                      >
                        <IconChevronLeft className="w-4 h-4" />
                      </button>
                      <span className="text-white text-xs font-medium tabular-nums bg-black/60 rounded px-1.5 py-0.5 select-none">
                        {composerPdfPageNum}/{composerPdfPageCount}
                      </span>
                      <button
                        type="button"
                        onClick={() => handleComposerPdfPageChange(1)}
                        disabled={composerPdfPageNum >= composerPdfPageCount}
                        className="p-1.5 rounded-full bg-black/60 text-white hover:bg-black disabled:opacity-40 disabled:cursor-default focus:outline-none"
                      >
                        <IconChevronRight className="w-4 h-4" />
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
                      className="p-2 rounded-full bg-black/60 text-white hover:bg-black focus:outline-none focus:ring-2 focus:ring-white/40"
                      title={t('chat.annotateModal.cancel')}
                      aria-label={t('chat.annotateModal.cancel')}
                    >
                      <IconXMark className="w-5 h-5" />
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
                      className="p-2 rounded-full bg-black/60 text-white hover:bg-black disabled:opacity-50 disabled:cursor-default focus:outline-none focus:ring-2 focus:ring-white/40"
                      title={t('chat.annotateModal.undo')}
                      aria-label={t('chat.annotateModal.undo')}
                      aria-disabled={composerUndoStack.length === 0}
                    >
                      <IconUndo className="w-5 h-5" />
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
                      className="p-2 rounded-full bg-green-500 text-white hover:bg-green-600 focus:outline-none focus:ring-2 focus:ring-green-300"
                      title={t('chat.annotateModal.saveAndAttach')}
                      aria-label={t('chat.annotateModal.saveAndAttach')}
                    >
                      <IconCheck className="w-5 h-5" />
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
