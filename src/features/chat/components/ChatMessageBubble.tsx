// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useState, useRef, useEffect, useCallback, useMemo } from 'react';
import { ChatMessage, SpeechPart } from '../../../core/types';
import { TranslationReplacements } from '../../../core/i18n/index';
import { IconPaperclip, IconXMark, IconPencil, IconUndo, IconGripCorner, IconCheck, IconChevronLeft, IconChevronRight, IconSpeaker, IconVolumeOff } from '../../../shared/ui/Icons';
import { SmallSpinner } from '../../../shared/ui/SmallSpinner';
import AttachmentTextScrollContainer from './AttachmentTextScrollContainer';
import TextScrollwheel from './TextScrollwheel';
import AudioPlayer from './AudioPlayer';
import PdfViewer from './PdfViewer';
import { usePdfAnnotation } from '../hooks/usePdfAnnotation';
import { useMaestroStore } from '../../../store';
import { selectSettings, selectSelectedLanguagePair, selectTargetLanguageDef, selectNativeLanguageDef } from '../../../store/slices/settingsSlice';
import { selectIsSpeaking, selectIsSending } from '../../../store/slices/uiSlice';
import { getPrimaryCode } from '../../../shared/utils/languageUtils';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE, type TokenSubtype } from '../../../core/config/activityTokens';
import { sketchShapeStyle } from '../../../shared/utils/sketchyShape';
import { generateTapeLayout, tapeStripStyle } from '../../../shared/utils/messageTapes';
import TextFileViewer from './TextFileViewer';
import OfficeFileViewer from './OfficeFileViewer';
import { decodeTextFromDataUrl, isOfficeAttachment, isTextLikeAttachment } from '../utils/fileAttachments';
import { isRunnableMiniGameAttachment } from '../utils/miniGameAttachment';
import { selectPrimaryUploadedAttachmentVariant } from '../utils/uploadedAttachmentVariants';

interface ChatMessageBubbleProps {
  message: ChatMessage;
  messageIndex: number;
  isFocusedMode: boolean;
  speakingUtteranceText: string | null;
  estimatedLoadTime: number; 
  loadingAnimations?: string[] | null;
  t: (key: string, replacements?: TranslationReplacements) => string;
  onToggleSpeakNativeLang: () => void;
  handleSpeakLine: (targetText: string, targetLangCode: string, nativeText?: string, nativeLangCode?: string, sourceMessageId?: string) => void;
  handlePlayUserMessage: (message: ChatMessage) => void;
  speakText: (textOrParts: SpeechPart[], defaultLang: string) => void;
  stopSpeaking: () => void;
  onToggleImageFocusedMode: () => void;
  transitioningImageId: string | null;
  onSetAttachedImage: (base64: string | null, mimeType: string | null, fileName?: string | null) => void;
  onUserInputActivity: () => void;
  onQuotaSetupBilling?: () => void;
  onQuotaStartLive?: () => void;
  onImageGenDisable?: () => void;
  onImageGenViewCost?: () => void;
  registerBubbleEl?: (el: HTMLDivElement | null) => void;
}

const ChatMessageBubble: React.FC<ChatMessageBubbleProps> = React.memo(({
  message, messageIndex, isFocusedMode, speakingUtteranceText, estimatedLoadTime, loadingAnimations,
  t,
  onToggleSpeakNativeLang, handleSpeakLine, handlePlayUserMessage, speakText, stopSpeaking,
  onToggleImageFocusedMode, transitioningImageId, onSetAttachedImage, onUserInputActivity,
  onQuotaSetupBilling, onQuotaStartLive,
  onImageGenDisable, onImageGenViewCost,
  registerBubbleEl
}) => {
  const isUser = message.role === 'user';
  const settings = useMaestroStore(selectSettings);
  const selectedLanguagePair = useMaestroStore(selectSelectedLanguagePair);
  const targetLanguageDef = useMaestroStore(selectTargetLanguageDef);
  const nativeLanguageDef = useMaestroStore(selectNativeLanguageDef);
  const isSpeaking = useMaestroStore(selectIsSpeaking);
  const isSending = useMaestroStore(selectIsSending);
  const speakNativeLang = settings.tts.speakNative;
  const currentTargetLangCode = useMemo(
    () => getPrimaryCode(selectedLanguagePair?.targetLanguageCode || targetLanguageDef?.code || 'es'),
    [selectedLanguagePair, targetLanguageDef]
  );
  const currentNativeLangCode = useMemo(
    () => getPrimaryCode(selectedLanguagePair?.nativeLanguageCode || nativeLanguageDef?.code || 'en'),
    [selectedLanguagePair, nativeLanguageDef]
  );

  const [isAnnotating, setIsAnnotating] = useState(false);
  const [annotationSourceUrl, setAnnotationSourceUrl] = useState<string | null>(null);
  const [imageAspectRatio, setImageAspectRatio] = useState<number | null>(null);

  const isAnnotationActive = isAnnotating && isFocusedMode;
  const isAssistant = message.role === 'assistant';
  const isError = message.role === 'error';
  const isStatus = message.role === 'status';
  const isAttachmentLoading = Boolean(message.isGeneratingImage || message.isGeneratingToolAttachment || message.isLoadingArtifact);
  
  const [remainingTimeDisplay, setRemainingTimeDisplay] = useState<string | null>(null);
  const [isVideoPlaying, setIsVideoPlaying] = useState(false);
  const videoPlayTokenRef = useRef<string | null>(null);
  const resizerRef = useRef<HTMLButtonElement>(null);
  const [showSvgCodeView, setShowSvgCodeView] = useState(false);

  const pointerDownPosRef = useRef<{x: number, y: number} | null>(null);
  const [nativeFlashIndex, setNativeFlashIndex] = useState<number | null>(null);
  const [nativeFlashIsOn, setNativeFlashIsOn] = useState<boolean>(false);
  const nativeFlashTimeoutRef = useRef<number | null>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const textOverlayRef = useRef<HTMLDivElement>(null);
  const [textOverlayHeight, setTextOverlayHeight] = useState(0);

  const imageForAnnotationRef = useRef<HTMLImageElement | null>(null);
  const editCanvasRef = useRef<HTMLCanvasElement>(null);
  const annotationViewportRef = useRef<HTMLDivElement>(null);

  const isDrawingRef = useRef(false);
  const lastPosRef = useRef<{x: number, y: number} | null>(null);
  const activePointersRef = useRef<React.PointerEvent[]>([]);
  const lastPanPointRef = useRef<{ x: number, y: number } | null>(null);
  const lastPinchDistanceRef = useRef<number>(0);
  const isNewStrokeRef = useRef(true);

  const [undoStack, setUndoStack] = useState<ImageData[]>([]);
  const [scale, setScale] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const annotationTokenRef = useRef<string | null>(null);

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

  
  const handleResizePointerDown = useCallback((e: React.PointerEvent) => {
    e.stopPropagation();

    const targetElement = e.currentTarget as HTMLElement;
    targetElement.setPointerCapture(e.pointerId);

    const dragStartPos = { x: e.clientX, y: e.clientY };
    const dragThreshold = 40; 
    const clickSlop = 8; 
    let dragTriggered = false;

    const handleMove = (ev: PointerEvent) => {
      const deltaX = ev.clientX - dragStartPos.x;
      const deltaY = ev.clientY - dragStartPos.y;

      if (dragTriggered) return;

      if (!isFocusedMode) {
        if (deltaX > dragThreshold || deltaY > dragThreshold) {
          dragTriggered = true;
          onToggleImageFocusedMode();
          handleUp(ev);
        }
      } else {
        if (deltaX < -dragThreshold || deltaY < -dragThreshold) {
          dragTriggered = true;
          onToggleImageFocusedMode();
          handleUp(ev);
        }
      }
    };

    const handleUp = (ev: PointerEvent) => {
      if (!dragTriggered) {
        const totalDx = Math.abs(ev.clientX - dragStartPos.x);
        const totalDy = Math.abs(ev.clientY - dragStartPos.y);
        if (totalDx <= clickSlop && totalDy <= clickSlop) {
          onToggleImageFocusedMode();
        }
      }

      try {
        if (targetElement.hasPointerCapture(ev.pointerId)) {
          targetElement.releasePointerCapture(ev.pointerId);
        }
      } catch (e) {
      }

      window.removeEventListener('pointermove', handleMove);
      window.removeEventListener('pointerup', handleUp);
      window.removeEventListener('pointercancel', handleUp);
    };

    window.addEventListener('pointermove', handleMove);
    window.addEventListener('pointerup', handleUp);
    window.addEventListener('pointercancel', handleUp);
  }, [isFocusedMode, onToggleImageFocusedMode]);

  const handleStartAnnotation = useCallback((imageUrl: string) => {
    if (!isFocusedMode || !imageUrl) return;
    if (!annotationTokenRef.current) {
      annotationTokenRef.current = createUiToken(TOKEN_SUBTYPE.BUBBLE_ANNOTATE);
    }
  
    let initialScale = 1;
  
    if (imageForAnnotationRef.current && imageForAnnotationRef.current.naturalWidth > 0) {
      const imgEl = imageForAnnotationRef.current;
      const rect = imgEl.getBoundingClientRect();
      initialScale = rect.width / imgEl.naturalWidth;
    } else if (videoRef.current && videoRef.current.videoWidth > 0) {
      const vidEl = videoRef.current;
      const rect = vidEl.getBoundingClientRect();
      initialScale = rect.width / vidEl.videoWidth;
  
      setImageAspectRatio(vidEl.videoWidth / vidEl.videoHeight);
    } else if (annotationViewportRef.current) {
      initialScale = 1;
    }
  
    setScale(initialScale);
    setPan({ x: 0, y: 0 });
  
    setAnnotationSourceUrl(imageUrl);
    setUndoStack([]);
    isNewStrokeRef.current = true;
    onUserInputActivity();
    setIsAnnotating(true);
  }, [isFocusedMode, onUserInputActivity, createUiToken]);

  const {
    pdfPageNum,
    pdfPageCount,
    startPdfAnnotation,
    changePdfPage: handlePdfPageChange,
    resetPdfState,
  } = usePdfAnnotation({
    editCanvasRef,
    onStartAnnotation: handleStartAnnotation,
    setAnnotationSourceUrl,
    setUndoStack,
    isNewStrokeRef,
  });

  const handleAnnotateVideo = () => {
    const video = videoRef.current;
    if (!video) return;
  
    if (!video.paused) {
      alert(t('chat.error.pauseVideoToAnnotate'));
      return;
    }
    if (video.videoWidth === 0 || video.videoHeight === 0) {
      return;
    }
  
    setImageAspectRatio(video.videoWidth / video.videoHeight);
  
    const canvas = document.createElement('canvas');
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    const ctx = canvas.getContext('2d');
    if (ctx) {
      ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
      const frameDataUrl = canvas.toDataURL('image/jpeg', 0.92);
      handleStartAnnotation(frameDataUrl);
    }
  };

  const handleAnnotatePdf = async () => {
    if (!displayUrl || !isPdfSuccessfullyDisplayed) return;
    await startPdfAnnotation(displayUrl);
  };

  const handleLinePointerDown = (e: React.PointerEvent) => {
    pointerDownPosRef.current = { x: e.clientX, y: e.clientY };
  };

  const handleLinePointerUp = (
    e: React.PointerEvent, 
    targetText: string,
    targetLangCode: string,
    nativeText?: string,
    nativeLangCode?: string
  ) => {
    if (pointerDownPosRef.current) {
        const deltaX = Math.abs(e.clientX - pointerDownPosRef.current.x);
        const deltaY = Math.abs(e.clientY - pointerDownPosRef.current.y);
        if (deltaX < 10 && deltaY < 10) {
            e.preventDefault(); 
            if (isSpeaking) {
              stopSpeaking();
            } else if (!isSending) {
              handleSpeakLine(targetText, targetLangCode, nativeText, nativeLangCode, message.id);
            }
        }
    }
    pointerDownPosRef.current = null;
  };

  const handleLinePointerLeave = () => {
    pointerDownPosRef.current = null;
  };

  const handleUserMessagePointerUp = (e: React.PointerEvent) => {
    if (!message.text) {
      pointerDownPosRef.current = null;
      return;
    }
    if (pointerDownPosRef.current) {
      const deltaX = Math.abs(e.clientX - pointerDownPosRef.current.x);
      const deltaY = Math.abs(e.clientY - pointerDownPosRef.current.y);
      if (deltaX < 10 && deltaY < 10) {
        e.preventDefault();
        if (isSpeaking) {
          stopSpeaking();
        } else if (!isSending) {
          handlePlayUserMessage(message);
        }
      }
    }
    pointerDownPosRef.current = null;
  };

  useEffect(() => {
      let intervalId: number | undefined;
      if (message.isGeneratingImage && message.imageGenerationStartTime && estimatedLoadTime > 0) {
          const updateRemainingTime = () => {
              const elapsedMs = Date.now() - message.imageGenerationStartTime!;
              const estimatedTotalMs = estimatedLoadTime * 1000;
              const remainingMs = Math.max(0, estimatedTotalMs - elapsedMs);
              setRemainingTimeDisplay(`Est: ${(remainingMs / 1000).toFixed(0)}s`);
              
              if (remainingMs === 0 && elapsedMs > estimatedTotalMs + 5000) { 
                   setRemainingTimeDisplay(t("chat.generatingImageLoadingSlow"));
              }
          };
          updateRemainingTime(); 
          intervalId = window.setInterval(updateRemainingTime, 1000);
      } else if (message.maestroToolKind === 'music' && message.toolAttachmentPhase === 'pending') {
          setRemainingTimeDisplay(t('chat.music.starting'));
      } else {
          setRemainingTimeDisplay(null);
      }
      return () => {
          if (intervalId) clearInterval(intervalId);
      };
  }, [estimatedLoadTime, message.imageGenerationStartTime, message.isGeneratingImage, message.maestroToolKind, message.toolAttachmentPhase, t]);

  useEffect(() => {
    return () => {
      if (videoPlayTokenRef.current) {
        removeActivityToken(videoPlayTokenRef.current);
        videoPlayTokenRef.current = null;
      }
    };
  }, [removeActivityToken]);

  const handleCancelAnnotation = useCallback(() => {
    setAnnotationSourceUrl(null);
    setIsAnnotating(false);
    isDrawingRef.current = false;
    lastPosRef.current = null;
    setScale(1);
    setPan({ x: 0, y: 0 });
    activePointersRef.current = [];
    setUndoStack([]);
    isNewStrokeRef.current = true;
    resetPdfState();
    if (annotationTokenRef.current) {
      removeActivityToken(annotationTokenRef.current);
      annotationTokenRef.current = null;
    }
  }, [removeActivityToken, resetPdfState]);

  const handleSaveAnnotation = () => {
    if (!editCanvasRef.current || !imageForAnnotationRef.current || !annotationViewportRef.current) return;

    const baseImage = imageForAnnotationRef.current;
    const drawingCanvas = editCanvasRef.current;
    const viewport = annotationViewportRef.current;

    const viewportRect = viewport.getBoundingClientRect();

    const finalCanvas = document.createElement('canvas');
    finalCanvas.width = viewportRect.width;
    finalCanvas.height = viewportRect.height;
    const ctx = finalCanvas.getContext('2d')!;
    
    ctx.save();
    ctx.translate(viewportRect.width / 2, viewportRect.height / 2);
    ctx.translate(pan.x, pan.y);
    ctx.scale(scale, scale);
    ctx.drawImage(baseImage, -baseImage.naturalWidth / 2, -baseImage.naturalHeight / 2, baseImage.naturalWidth, baseImage.naturalHeight);
    ctx.drawImage(drawingCanvas, -baseImage.naturalWidth / 2, -baseImage.naturalHeight / 2, baseImage.naturalWidth, baseImage.naturalHeight);
    ctx.restore();

    const newDataUrl = finalCanvas.toDataURL('image/jpeg', 0.9);

    onSetAttachedImage(newDataUrl, 'image/jpeg');
    onUserInputActivity();
    handleCancelAnnotation();
    if (annotationTokenRef.current) {
      removeActivityToken(annotationTokenRef.current);
      annotationTokenRef.current = null;
    }
  };

  const handleUndo = useCallback(() => {
    const canvas = editCanvasRef.current;
    const ctx = canvas?.getContext('2d');
    if (!canvas || !ctx) return;
  
    setUndoStack(prevStack => {
        if (prevStack.length === 0) {
            return [];
        }
  
        const newStack = prevStack.slice(0, -1);

        ctx.clearRect(0, 0, canvas.width, canvas.height);

        // Restore the last snapshot (state before the last stroke was drawn)
        ctx.putImageData(prevStack[prevStack.length - 1], 0, 0);

        return newStack;
    });
  }, []);

  const getTransformedPos = useCallback((e: React.PointerEvent<any>) => {
    const canvas = editCanvasRef.current;
    if (!canvas) return null;
    const rect = canvas.getBoundingClientRect();
    const x = (e.clientX - rect.left) * (canvas.width / rect.width);
    const y = (e.clientY - rect.top) * (canvas.height / rect.height);
    return { x, y };
  }, []);

  const handleAnnotationAreaPointerDown = useCallback((e: React.PointerEvent<HTMLDivElement>) => {
      e.stopPropagation();
      e.preventDefault();
      activePointersRef.current.push(e);
      document.body.style.overscrollBehavior = 'none';
      e.currentTarget.setPointerCapture(e.pointerId);

      if (activePointersRef.current.length === 1) {
          isDrawingRef.current = true;
          lastPosRef.current = getTransformedPos(e);
          isNewStrokeRef.current = true;
      } else if (activePointersRef.current.length === 2) {
          // If a stroke was accidentally started by the first finger, undo it
          if (!isNewStrokeRef.current) {
              const canvas = editCanvasRef.current;
              const ctx = canvas?.getContext('2d');
              if (canvas && ctx) {
                  setUndoStack(prev => {
                      if (prev.length === 0) return prev;
                      ctx.clearRect(0, 0, canvas.width, canvas.height);
                      ctx.putImageData(prev[prev.length - 1], 0, 0);
                      return prev.slice(0, -1);
                  });
              }
          }
          isDrawingRef.current = false;
          lastPosRef.current = null;
          const viewportRect = annotationViewportRef.current?.getBoundingClientRect();
          if (!viewportRect) return;
          const p1 = activePointersRef.current[0];
          const p2 = activePointersRef.current[1];
          lastPinchDistanceRef.current = Math.hypot(p1.clientX - p2.clientX, p1.clientY - p2.clientY);
          lastPanPointRef.current = {
              x: ((p1.clientX + p2.clientX) / 2) - viewportRect.left,
              y: ((p1.clientY + p2.clientY) / 2) - viewportRect.top
          };
      }
  }, [getTransformedPos]);

  const handleAnnotationAreaPointerMove = useCallback((e: React.PointerEvent<HTMLDivElement>) => {
      e.preventDefault();
      const index = activePointersRef.current.findIndex(p => p.pointerId === e.pointerId);
      if (index === -1) return;
      activePointersRef.current[index] = e;

      if (activePointersRef.current.length === 1 && isDrawingRef.current && lastPosRef.current) {
          const currentPos = getTransformedPos(e);
          const canvas = editCanvasRef.current;
          const ctx = canvas?.getContext('2d');
          if (ctx && currentPos) {
              if (isNewStrokeRef.current && canvas) {
                  const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
                  setUndoStack(prev => [...prev, imageData]);
                  isNewStrokeRef.current = false;
              }
              ctx.beginPath();
              ctx.moveTo(lastPosRef.current.x, lastPosRef.current.y);
              ctx.lineTo(currentPos.x, currentPos.y);
              ctx.stroke();
              lastPosRef.current = currentPos;
          }
      } else if (activePointersRef.current.length === 2) {
          const viewportRect = annotationViewportRef.current?.getBoundingClientRect();
          if (!viewportRect) return;

          const p1 = activePointersRef.current[0];
          const p2 = activePointersRef.current[1];
          const newPinchDistance = Math.hypot(p1.clientX - p2.clientX, p1.clientY - p2.clientY);
          
          const newCenter = {
            x: ((p1.clientX + p2.clientX) / 2) - viewportRect.left,
            y: ((p1.clientY + p2.clientY) / 2) - viewportRect.top
          };
          
          const panDx = lastPanPointRef.current ? newCenter.x - lastPanPointRef.current.x : 0;
          const panDy = lastPanPointRef.current ? newCenter.y - lastPanPointRef.current.y : 0;
          
          const scaleFactor = lastPinchDistanceRef.current > 0 ? newPinchDistance / lastPinchDistanceRef.current : 1;
          
          setPan(prevPan => {
              const panned = { x: prevPan.x + panDx, y: prevPan.y + panDy };
              const cursorFromCenter = {
                  x: newCenter.x - viewportRect.width / 2,
                  y: newCenter.y - viewportRect.height / 2
              };
              const finalPan = {
                  x: cursorFromCenter.x - (cursorFromCenter.x - panned.x) * scaleFactor,
                  y: cursorFromCenter.y - (cursorFromCenter.y - panned.y) * scaleFactor
              };
              return finalPan;
          });

          setScale(prevScale => Math.max(0.2, Math.min(prevScale * scaleFactor, 15)));

          lastPinchDistanceRef.current = newPinchDistance;
          lastPanPointRef.current = newCenter;
      }
  }, [getTransformedPos]); 

  const handleModalPointerUp = useCallback((e: React.PointerEvent<HTMLDivElement>) => {
      e.preventDefault();
      e.currentTarget.releasePointerCapture(e.pointerId);
      activePointersRef.current = activePointersRef.current.filter(p => p.pointerId !== e.pointerId);      
      
      if (activePointersRef.current.length < 2) {
          lastPinchDistanceRef.current = 0;
          lastPanPointRef.current = null;
      }
      if (activePointersRef.current.length < 1) {
          isDrawingRef.current = false;
          lastPosRef.current = null;
          document.body.style.overscrollBehavior = 'auto';
      }
  }, []);

  const handleAnnotationAreaWheel = useCallback((e: React.WheelEvent<HTMLDivElement>) => {
      e.preventDefault();
      const scaleFactor = e.deltaY > 0 ? 0.9 : 1.1;
      const rect = e.currentTarget.getBoundingClientRect();
      const cursor = { x: e.clientX - rect.left, y: e.clientY - rect.top };
      
      setScale(prevScale => {
          const newScale = Math.max(0.2, Math.min(prevScale * scaleFactor, 15));
          setPan(prevPan => ({
              x: cursor.x - (cursor.x - prevPan.x) * (newScale / prevScale),
              y: cursor.y - (cursor.y - prevPan.y) * (newScale / prevScale)
          }));
          return newScale;
      });
  }, []);

  useEffect(() => {
    if (!isAnnotating || !annotationSourceUrl) {
      return;
    }
  
    const canvas = editCanvasRef.current;
    const ctx = canvas?.getContext('2d');
    const imageEl = imageForAnnotationRef.current;
  
    if (!canvas || !ctx || !imageEl) return;
    
    const setupCanvas = () => {
      if (imageEl.naturalWidth > 0 && imageEl.naturalHeight > 0) {
        canvas.width = imageEl.naturalWidth;
        canvas.height = imageEl.naturalHeight;
  
        ctx.strokeStyle = 'hsl(5, 45%, 55%)';
        ctx.lineWidth = Math.max(5, imageEl.naturalWidth * 0.01);
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
      }
    };
  
    if (imageEl.complete && imageEl.naturalWidth > 0) {
      setupCanvas();
    } else {
      imageEl.addEventListener('load', setupCanvas);
    }
  
    return () => {
      if (imageEl) {
        imageEl.removeEventListener('load', setupCanvas);
      }
    };
  }, [isAnnotating, annotationSourceUrl]);

  const displayUrlRaw = message.imageUrl || message.storageOptimizedImageUrl;
  const displayUrl = typeof displayUrlRaw === 'string' ? displayUrlRaw : undefined;
  const primaryUploadedVariant = selectPrimaryUploadedAttachmentVariant(message);
  const displayMimeRaw = message.imageMimeType || message.storageOptimizedImageMimeType || primaryUploadedVariant?.mimeType;
  const displayMime = typeof displayMimeRaw === 'string' ? displayMimeRaw : '';
  const normalizedDisplayMime = (displayMime || '').trim().toLowerCase();
  const isAudioToolAttachment = message.maestroToolKind === 'audio-note' || message.maestroToolKind === 'music';
  const isAttachmentAnImage = !!displayMime?.startsWith('image/');
  const isAttachmentAVideo = !!displayMime?.startsWith('video/');
  const isAttachmentAAudio = !!displayMime?.startsWith('audio/');
  const isAttachmentAPdf = displayMime === 'application/pdf';
  const isAttachmentSvg = normalizedDisplayMime.startsWith('image/svg+xml');
  const isAttachmentAOffice = isOfficeAttachment(displayMime, message.attachmentName);
  const isAttachmentAText =
    !isAttachmentAnImage &&
    !isAttachmentAVideo &&
    !isAttachmentAAudio &&
    !isAttachmentAPdf &&
    isTextLikeAttachment(displayMime, message.attachmentName);
  const hasRemoteAttachment = !!primaryUploadedVariant;
  const hasAttachmentSource = !!displayUrl || hasRemoteAttachment;
  const toolAttachmentStatusText = useMemo(() => {
    if (message.maestroToolKind !== 'music') return null;
    switch (message.toolAttachmentPhase) {
      case 'pending':
        return t('chat.music.starting');
      case 'streaming':
        return t('chat.music.streaming');
      case 'finalizing':
        return t('chat.music.finalizing');
      default:
        return null;
    }
  }, [message.maestroToolKind, message.toolAttachmentPhase, t]);
  const shouldShowAudioAttachmentPlaceholder = !displayUrl
    && !message.imageGenError
    && (isAttachmentAAudio || isAudioToolAttachment)
    && (isAttachmentLoading || message.toolAttachmentPhase === 'streaming' || message.toolAttachmentPhase === 'finalizing');
  const audioAttachmentPlaceholderStatusText = useMemo(() => {
    if (!shouldShowAudioAttachmentPlaceholder) return null;
    if (message.maestroToolKind === 'music') {
      return toolAttachmentStatusText;
    }
    if (message.toolAttachmentPhase === 'finalizing') {
      return t('chat.sendPrep.finalizing');
    }
    return null;
  }, [message.maestroToolKind, message.toolAttachmentPhase, shouldShowAudioAttachmentPlaceholder, t, toolAttachmentStatusText]);
  const audioAttachmentPlaceholderProgress = useMemo(() => {
    if (!shouldShowAudioAttachmentPlaceholder) return 0;
    if (message.toolAttachmentPhase === 'streaming') return 0.58;
    if (message.toolAttachmentPhase === 'finalizing') return 0.84;
    return 0.24;
  }, [message.toolAttachmentPhase, shouldShowAudioAttachmentPlaceholder]);

  const isImageSuccessfullyDisplayed = isAttachmentAnImage && displayUrl && !isAttachmentLoading && !message.imageGenError;
  const isVideoSuccessfullyDisplayed = isAttachmentAVideo && displayUrl && !isAttachmentLoading;
  const isAudioSuccessfullyDisplayed = isAttachmentAAudio && displayUrl && !isAttachmentLoading && !message.imageGenError;
  const usesAudioAttachmentShell = isAudioSuccessfullyDisplayed || shouldShowAudioAttachmentPlaceholder;
  const isPdfSuccessfullyDisplayed = isAttachmentAPdf && displayUrl && !isAttachmentLoading && !message.imageGenError;
  const isOfficeFileSuccessfullyDisplayed = isAttachmentAOffice && hasAttachmentSource && !isAttachmentLoading && !message.imageGenError;
  const isTextFileSuccessfullyDisplayed = isAttachmentAText && !!displayUrl && !isAttachmentLoading && !message.imageGenError;
  const isTextFileRemoteOnly = isAttachmentAText && !displayUrl && hasRemoteAttachment && !isAttachmentLoading && !message.imageGenError;
  const isFileSuccessfullyDisplayed = !isAttachmentAnImage && !isAttachmentAVideo && !isAttachmentAAudio && !isAttachmentAPdf && !isAttachmentAOffice && !isAttachmentAText && hasAttachmentSource && !isAttachmentLoading && !message.imageGenError;
  const isScrollableFileAttachment = isTextFileSuccessfullyDisplayed || isOfficeFileSuccessfullyDisplayed || isTextFileRemoteOnly;
  const svgSourceCode = useMemo(() => {
    if (!isAttachmentSvg || !displayUrl) return null;
    return decodeTextFromDataUrl(displayUrl);
  }, [isAttachmentSvg, displayUrl]);
  const textAttachmentSourceCode = useMemo(() => {
    if (!isAttachmentAText || !displayUrl) return null;
    return decodeTextFromDataUrl(displayUrl);
  }, [isAttachmentAText, displayUrl]);
  const isMiniGameAttachment = useMemo(() => {
    if (!textAttachmentSourceCode) return false;
    return isRunnableMiniGameAttachment({
      sourceCode: textAttachmentSourceCode,
      fileName: message.attachmentName,
      mimeType: displayMime,
    });
  }, [displayMime, message.attachmentName, textAttachmentSourceCode]);

  const selectedLoadingAnimation = useMemo(() => {
    const source = (loadingAnimations && loadingAnimations.length > 0) ? loadingAnimations : [];
    if (!isAttachmentLoading || source.length === 0) return null;
    const seedStr = `${message.id || ''}|${message.imageGenerationStartTime || message.toolAttachmentStartTime || message.artifactLoadStartTime || 0}`;
    let h = 0;
    for (let i = 0; i < seedStr.length; i++) {
      h = ((h << 5) - h) + seedStr.charCodeAt(i);
      h |= 0;
    }
    const idx = Math.abs(h) % source.length;
    return source[idx];
  }, [isAttachmentLoading, loadingAnimations, message.artifactLoadStartTime, message.id, message.imageGenerationStartTime, message.toolAttachmentStartTime]);

  const [loadingAnimationError, setLoadingAnimationError] = useState(false);

  useEffect(() => {
    setLoadingAnimationError(false);
  }, [isAttachmentLoading, message.id, selectedLoadingAnimation]);

  useEffect(() => {
    setShowSvgCodeView(false);
  }, [message.id, displayUrl, isAttachmentSvg]);

  const bubbleShapeStyle = useMemo(() => sketchShapeStyle(messageIndex), [messageIndex]);
  const tapeLayout = useMemo(() => generateTapeLayout(messageIndex), [messageIndex]);
  const hasTextContent = message.text || (message.translations && message.translations.some(tr => tr.target || tr.native)) || message.rawAssistantResponse;

  const applyFocusedImageStyles = isFocusedMode && (isImageSuccessfullyDisplayed || isAttachmentLoading || isFileSuccessfullyDisplayed || isOfficeFileSuccessfullyDisplayed || isTextFileSuccessfullyDisplayed || isTextFileRemoteOnly || isVideoSuccessfullyDisplayed || usesAudioAttachmentShell || isPdfSuccessfullyDisplayed);
  const hasVisibleAttachment = shouldShowAudioAttachmentPlaceholder || isAttachmentLoading || isImageSuccessfullyDisplayed || isFileSuccessfullyDisplayed || isOfficeFileSuccessfullyDisplayed || isTextFileSuccessfullyDisplayed || isTextFileRemoteOnly || isVideoSuccessfullyDisplayed || isAudioSuccessfullyDisplayed || isPdfSuccessfullyDisplayed;
  const shouldOverlayTextOnAttachment = applyFocusedImageStyles && !usesAudioAttachmentShell && !isVideoSuccessfullyDisplayed;
  const shouldUseScrollableTextOverlay = shouldOverlayTextOnAttachment && hasVisibleAttachment && !!hasTextContent;
  const overlayTranscriptBottomInset = shouldUseScrollableTextOverlay ? Math.max(0, textOverlayHeight + 8) : 0;
  const usesDetachedTranscriptShell = shouldUseScrollableTextOverlay && (isMiniGameAttachment || isAttachmentSvg);
  const detachedTranscriptShellHeight = usesDetachedTranscriptShell && overlayTranscriptBottomInset > 0
    ? Math.max(92, Math.min(Math.round(overlayTranscriptBottomInset * 0.45) + 32, 122))
    : 0;
  const detachedTranscriptBottomPadding = detachedTranscriptShellHeight > 0
    ? Math.max(72, detachedTranscriptShellHeight - 10)
    : 0;
  const useOverlayTextColors = shouldOverlayTextOnAttachment && !usesDetachedTranscriptShell;
  const shouldInsetScrollableAttachmentForOverlay = overlayTranscriptBottomInset > 0 && (isTextFileSuccessfullyDisplayed || isPdfSuccessfullyDisplayed);
  const scrollableAttachmentBottomInset = shouldInsetScrollableAttachmentForOverlay ? overlayTranscriptBottomInset : 0;
  const textOverlayScrollStyle: React.CSSProperties | undefined = shouldUseScrollableTextOverlay
    ? {
        maxHeight: '40vh',
        overflowY: 'auto',
        overscrollBehavior: 'contain',
        touchAction: 'pan-y',
        WebkitOverflowScrolling: 'touch' as any,
        scrollbarGutter: 'stable',
        ...(usesDetachedTranscriptShell ? { textShadow: 'none' } : null),
      }
    : undefined;
  const textOverlayClasses = shouldOverlayTextOnAttachment
    ? (usesDetachedTranscriptShell
        ? 'absolute inset-x-0 bottom-0 p-3 bg-transparent rounded-b-lg z-10'
        : 'absolute inset-x-0 bottom-0 p-3 bg-gradient-to-t from-black/80 via-black/60 to-transparent text-white rounded-b-lg z-10')
    : 'relative z-10 mt-1';
  const userAttachmentTextMode: 'plain' | 'inline' | 'audio' | 'overlay' | 'svg' | 'game' = hasVisibleAttachment
    ? (shouldOverlayTextOnAttachment
        ? (isMiniGameAttachment ? 'game' : isAttachmentSvg ? 'svg' : 'overlay')
        : usesAudioAttachmentShell
          ? 'audio'
          : 'inline')
    : 'plain';
  const userAttachmentTextClass = userAttachmentTextMode === 'overlay'
    ? 'text-user-attachment-overlay-text'
    : userAttachmentTextMode === 'svg'
      ? 'text-user-attachment-svg-text'
    : userAttachmentTextMode === 'game'
      ? 'text-user-attachment-game-text'
    : userAttachmentTextMode === 'audio'
      ? 'text-user-attachment-audio-text'
    : userAttachmentTextMode === 'inline'
      ? 'text-user-attachment-inline-text'
      : 'text-user-msg-text';
  const userAttachmentSubtleTextClass = userAttachmentTextMode === 'overlay'
    ? 'text-user-attachment-overlay-text/70'
    : userAttachmentTextMode === 'svg'
      ? 'text-user-attachment-svg-text/70'
    : userAttachmentTextMode === 'game'
      ? 'text-user-attachment-game-text/70'
    : userAttachmentTextMode === 'audio'
      ? 'text-user-attachment-audio-text/70'
    : userAttachmentTextMode === 'inline'
      ? 'text-user-attachment-inline-text/70'
      : 'text-user-msg-text/70';
  const userAttachmentHoverSurfaceClass = userAttachmentTextMode === 'overlay'
    ? 'hover:bg-user-attachment-overlay-text/10'
    : userAttachmentTextMode === 'svg'
      ? 'hover:bg-user-attachment-svg-text/10'
    : userAttachmentTextMode === 'game'
      ? 'hover:bg-user-attachment-game-text/10'
    : userAttachmentTextMode === 'audio'
      ? 'hover:bg-user-attachment-audio-text/10'
    : userAttachmentTextMode === 'inline'
      ? 'hover:bg-user-attachment-inline-text/5'
      : 'hover:bg-user-msg-text/5';
  const userAttachmentSpeakingSurfaceClass = userAttachmentTextMode === 'overlay'
    ? 'bg-user-attachment-overlay-text/20'
    : userAttachmentTextMode === 'svg'
      ? 'bg-user-attachment-svg-text/20'
    : userAttachmentTextMode === 'game'
      ? 'bg-user-attachment-game-text/20'
    : userAttachmentTextMode === 'audio'
      ? 'bg-user-attachment-audio-text/20'
    : userAttachmentTextMode === 'inline'
      ? 'bg-user-attachment-inline-text/10'
      : 'bg-user-msg-text/10';
  const assistantAttachmentTextMode: 'plain' | 'inline' | 'overlay' | 'svg' | 'game' = hasVisibleAttachment
    ? (shouldOverlayTextOnAttachment ? (isMiniGameAttachment ? 'game' : isAttachmentSvg ? 'svg' : 'overlay') : 'inline')
    : 'plain';
  const assistantTargetTextClass = assistantAttachmentTextMode === 'overlay'
    ? 'text-attachment-overlay-target-text'
    : assistantAttachmentTextMode === 'svg'
      ? 'text-attachment-svg-target-text'
    : assistantAttachmentTextMode === 'game'
      ? 'text-attachment-game-target-text'
      : assistantAttachmentTextMode === 'inline'
        ? 'text-attachment-inline-target-text'
        : 'text-ai-msg-text';
  const assistantTargetHoverTextClass = assistantAttachmentTextMode === 'overlay'
    ? 'hover:text-attachment-overlay-target-text'
    : assistantAttachmentTextMode === 'svg'
      ? 'hover:text-attachment-svg-target-text'
    : assistantAttachmentTextMode === 'game'
      ? 'hover:text-attachment-game-target-text'
      : assistantAttachmentTextMode === 'inline'
        ? 'hover:text-attachment-inline-target-text'
        : 'hover:text-ai-msg-text';
  const assistantNativeTextClass = assistantAttachmentTextMode === 'overlay'
    ? 'text-attachment-overlay-native-text/70 border-attachment-overlay-native-text/30'
    : assistantAttachmentTextMode === 'svg'
      ? 'text-attachment-svg-native-text border-attachment-svg-native-text/25'
    : assistantAttachmentTextMode === 'game'
      ? 'text-attachment-game-native-text border-attachment-game-native-text/25'
      : assistantAttachmentTextMode === 'inline'
        ? 'text-attachment-inline-native-text border-attachment-inline-native-text/25'
        : 'text-ai-file-text border-line-border';
  const handleAttachmentImageLoad = useCallback((img: HTMLImageElement) => {
    if (img.naturalWidth > 0 && img.naturalHeight > 0) {
      setImageAspectRatio(img.naturalWidth / img.naturalHeight);

      if (isAnnotationActive && annotationViewportRef.current) {
        const vw = annotationViewportRef.current.clientWidth;
        setScale(vw / img.naturalWidth);
        setPan({ x: 0, y: 0 });
      }
    }
  }, [isAnnotationActive]);
  
  const bubbleAlignClass = isUser ? 'justify-end' : 'justify-start';
  const sanitizedUserText = message.text ? message.text.replace(/\*/g, '') : '';
  const isUserLineSpeaking = isUser && sanitizedUserText && speakingUtteranceText === sanitizedUserText;
  const shouldUseScrollableUserTextShell = isUser && !!message.text && applyFocusedImageStyles && shouldUseScrollableTextOverlay;
  const userMessageTextNode = isUser && message.text ? (
    <p
      className={`${shouldUseScrollableUserTextShell ? '' : 'mb-1 '}whitespace-pre-wrap rounded-sm px-1 -mx-1 cursor-pointer transition-colors pointer-events-auto ${userAttachmentTextClass} ${isUserLineSpeaking ? userAttachmentSpeakingSurfaceClass : userAttachmentHoverSurfaceClass}`.trim()}
      style={{ fontSize: '3.8cqw', lineHeight: 1.35 }}
      onPointerDown={handleLinePointerDown}
      onPointerUp={handleUserMessagePointerUp}
      onPointerLeave={handleLinePointerLeave}
      role="button"
      tabIndex={isSending && !isSpeaking ? -1 : 0}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          if (isSpeaking) {
            stopSpeaking();
          } else if (!isSending) {
            handlePlayUserMessage(message);
          }
        }
      }}
      aria-label={isUserLineSpeaking ? (t('chat.stopSpeaking') || 'Stop playback') : (t('chat.speakThisLine') ? `${t('chat.speakThisLine')}: ${sanitizedUserText}` : 'Play message audio')}
      aria-disabled={isSending && !isSpeaking}
    >
      {message.text}
    </p>
  ) : null;
  // --- Thinking bubble: content line for the "native" position ---
  // Concatenate all trace lines into a single ticker string for scrolling effect.
  // When thinkingDraftText (streamed response) arrives, prioritize that instead.
  const thinkingTickerText = useMemo(() => {
    const traceLines = Array.isArray(message.thinkingTrace)
      ? message.thinkingTrace.filter(line => typeof line === 'string' && line.trim().length > 0)
      : [];
    // Join all trace lines with a separator to create a continuous stream effect
    return traceLines.join('  \u00B7  '); // middot separator between entries
  }, [message.thinkingTrace]);

  const thinkingDraftCondensed = useMemo(() => {
    const draftText = typeof message.thinkingDraftText === 'string' ? message.thinkingDraftText : '';
    if (!draftText) return '';
    return draftText.replace(/\s+/g, ' ').trim();
  }, [message.thinkingDraftText]);

  const thinkingStatusCondensed = useMemo(() => {
    const statusText = typeof message.thinkingStatusLine === 'string' ? message.thinkingStatusLine : '';
    if (!statusText) return '';
    return statusText.replace(/\s+/g, ' ').trim();
  }, [message.thinkingStatusLine]);

  // Ticker offset for the scrolling "train window" effect on thought signatures
  const tickerOffsetRef = useRef(0);
  const tickerRafRef = useRef<number | null>(null);
  const tickerDisplayRef = useRef<HTMLSpanElement | null>(null);
  const lastTickerTextRef = useRef('');

  // When new trace lines arrive, they seamlessly extend the ticker stream
  useEffect(() => {
    if (!message.thinking || isAttachmentLoading) return; // only for thinking state
    // If we have streamed response text, stop the ticker animation - the draft text
    // is displayed via its own mechanism
    if (thinkingDraftCondensed) {
      if (tickerRafRef.current !== null) {
        cancelAnimationFrame(tickerRafRef.current);
        tickerRafRef.current = null;
      }
      return;
    }
    if (!thinkingTickerText) return;

    // If the ticker text grew (new thought appended), keep the offset continuous
    // so there's no jump - only reset if the text shrank (shouldn't happen normally).
    if (thinkingTickerText.length < lastTickerTextRef.current.length) {
      tickerOffsetRef.current = 0;
    }
    lastTickerTextRef.current = thinkingTickerText;

    const CHARS_PER_SECOND = 18; // scroll speed
    const VISIBLE_CHARS = 40; // how many chars the "window" shows

    let lastTime = performance.now();
    let subCharProgress = 0;

    const tick = (now: number) => {
      const dt = (now - lastTime) / 1000;
      lastTime = now;
      subCharProgress += dt * CHARS_PER_SECOND;
      const charsToAdvance = Math.floor(subCharProgress);
      if (charsToAdvance > 0) {
        subCharProgress -= charsToAdvance;
        tickerOffsetRef.current += charsToAdvance;
      }

      // Build the visible slice by looping through the text
      const text = lastTickerTextRef.current;
      if (text.length > 0 && tickerDisplayRef.current) {
        const start = tickerOffsetRef.current % text.length;
        let visible = '';
        for (let i = 0; i < VISIBLE_CHARS; i++) {
          visible += text[(start + i) % text.length];
        }
        tickerDisplayRef.current.textContent = visible;
      }

      tickerRafRef.current = requestAnimationFrame(tick);
    };

    tickerRafRef.current = requestAnimationFrame(tick);
    return () => {
      if (tickerRafRef.current !== null) {
        cancelAnimationFrame(tickerRafRef.current);
        tickerRafRef.current = null;
      }
    };
  }, [isAttachmentLoading, message.thinking, thinkingDraftCondensed, thinkingTickerText]);

  // Phase label for the upper line
  const thinkingPhaseLabel = message.thinkingPhase || t('chat.thinking');

  // The content for the lower line (native position) of the thinking bubble
  const thinkingContentLine = useMemo(() => {
    if (thinkingDraftCondensed) {
      // Streamed response text - show tail like a train window
      return thinkingDraftCondensed.length > 42
        ? `\u2026${thinkingDraftCondensed.slice(-42)}`
        : thinkingDraftCondensed;
    }
    if (thinkingTickerText) {
      // For thought signatures we use the ticker ref, so return null to signal ref usage
      return null;
    }
    return thinkingStatusCondensed || thinkingPhaseLabel;
  }, [thinkingDraftCondensed, thinkingPhaseLabel, thinkingStatusCondensed, thinkingTickerText]);

  useEffect(() => {
    if (!shouldUseScrollableTextOverlay) {
      setTextOverlayHeight(0);
      return;
    }

    const overlayEl = textOverlayRef.current;
    if (!overlayEl) return;

    const updateHeight = () => {
      setTextOverlayHeight(overlayEl.clientHeight);
    };
    updateHeight();

    let resizeObserver: ResizeObserver | null = null;
    if (typeof ResizeObserver !== 'undefined') {
      resizeObserver = new ResizeObserver(updateHeight);
      resizeObserver.observe(overlayEl);
    }

    return () => {
      resizeObserver?.disconnect();
    };
  }, [shouldUseScrollableTextOverlay, message.text, message.rawAssistantResponse, message.translations]);

  if (message.thinking && !isAttachmentLoading) {
    return (
      <div className="flex justify-start mb-4">
        <div className="relative max-w-[90%] sm:max-w-[80%] md:max-w-[70%] lg:max-w-[65%]" style={{ width: '100%' }}>
          <div
            className="relative p-3 overflow-visible msg-depth bg-ai-msg-bg bg-opacity-90 text-ai-msg-text sketchy-border-thin animate-pulse"
            style={{
              containerType: 'inline-size',
              width: '100%',
              ...bubbleShapeStyle,
            }}
          >
            {/* Upper line (target position): phase label */}
            <p
              className="font-semibold whitespace-nowrap truncate text-ai-msg-text"
              style={{ fontSize: '4cqw', lineHeight: 1.3 }}
            >
              {thinkingPhaseLabel}
            </p>
            {/* Lower line (native position): thought ticker or streamed text */}
            <p
              className="italic mt-0.5 truncate pl-2 border-l-2 text-ai-file-text border-line-border"
              style={{ fontSize: '3.55cqw', lineHeight: 1.3 }}
            >
              {thinkingContentLine !== null ? (
                thinkingContentLine
              ) : (
                <span ref={tickerDisplayRef} className="inline-block font-mono">{'\u00A0'}</span>
              )}
            </p>
          </div>
        </div>
      </div>
    );
  }

  let bubbleWrapperClasses = "relative transition-all duration-300 ease-in-out";
  let tapeWrapperMaxWidth = '';
   if (applyFocusedImageStyles) {
      bubbleWrapperClasses += " w-full overflow-hidden";
      if (usesAudioAttachmentShell) {
           bubbleWrapperClasses += " p-3";
           if (isUser) bubbleWrapperClasses += " msg-depth-user bg-user-msg-bg bg-opacity-90 text-user-msg-text";
           else if (isError) bubbleWrapperClasses += " msg-depth bg-error-msg-bg/10 bg-opacity-90 text-error-msg-text";
           else if (isStatus) bubbleWrapperClasses += " msg-depth bg-status-msg-bg bg-opacity-90 text-status-msg-text";
           else bubbleWrapperClasses += " msg-depth bg-ai-msg-bg bg-opacity-90 text-ai-msg-text";
      } else if (!isImageSuccessfullyDisplayed && !isAttachmentLoading && !isFileSuccessfullyDisplayed && !isOfficeFileSuccessfullyDisplayed && !isTextFileSuccessfullyDisplayed && !isTextFileRemoteOnly && !isVideoSuccessfullyDisplayed) {
           bubbleWrapperClasses += " p-3";
           if (isUser) bubbleWrapperClasses += " msg-depth-user bg-user-msg-bg bg-opacity-90 text-user-msg-text";
           else if (isError) bubbleWrapperClasses += " msg-depth bg-error-msg-bg/10 bg-opacity-90 text-error-msg-text";
           else if (isStatus) bubbleWrapperClasses += " msg-depth bg-status-msg-bg bg-opacity-90 text-status-msg-text";
           else bubbleWrapperClasses += " msg-depth bg-ai-msg-bg bg-opacity-90 text-ai-msg-text";
      }
  } else {
      tapeWrapperMaxWidth = "max-w-[90%] sm:max-w-[80%] md:max-w-[70%] lg:max-w-[65%]";
      bubbleWrapperClasses += " p-3 overflow-visible";
      if (isUser) bubbleWrapperClasses += " msg-depth-user bg-user-msg-bg bg-opacity-90 text-user-msg-text";
      else if (isError) bubbleWrapperClasses += " msg-depth bg-error-msg-bg/10 bg-opacity-90 text-error-msg-text";
      else if (isStatus) bubbleWrapperClasses += " msg-depth bg-status-msg-bg bg-opacity-90 text-status-msg-text";
      else bubbleWrapperClasses += " msg-depth bg-ai-msg-bg bg-opacity-90 text-ai-msg-text sketchy-border-thin";
  }

  const imageContainerBaseClasses = "relative rounded-lg group transition-all duration-300 ease-in-out";
  let imageContainerSizeClasses = "";
  let imageContainerAspectClasses = "";
  let imageContainerFlexCenteringClasses = "flex items-center justify-center";

  if (applyFocusedImageStyles) {
      imageContainerSizeClasses = "w-full max-h-[75vh]";
      if (usesAudioAttachmentShell) {
          imageContainerSizeClasses = "w-full";
          imageContainerAspectClasses = "";
          imageContainerFlexCenteringClasses = "";
      } else if (isAttachmentLoading || isFileSuccessfullyDisplayed) {
          imageContainerAspectClasses = "aspect-square";
      } else if (isImageSuccessfullyDisplayed || isVideoSuccessfullyDisplayed) {
          if (isAnnotationActive) {
              imageContainerAspectClasses = "bg-user-msg-bg";
              imageContainerFlexCenteringClasses = "";
          } else {
              imageContainerAspectClasses = "";
          }
      }
  } else {
      if (usesAudioAttachmentShell) {
        imageContainerSizeClasses = "w-full my-2";
        imageContainerAspectClasses = "";
        imageContainerFlexCenteringClasses = "";
      } else if (isScrollableFileAttachment) {
        imageContainerSizeClasses = "w-full max-w-[320px] mx-auto my-2";
        imageContainerAspectClasses = "";
        imageContainerFlexCenteringClasses = "";
      } else {
        imageContainerSizeClasses = "w-full max-w-[250px] mx-auto my-2";
        imageContainerAspectClasses = "aspect-square";
      }
  }
  
  const imageContainerDynamicBg = isAttachmentLoading && !shouldShowAudioAttachmentPlaceholder ? 
      (applyFocusedImageStyles ? (isUser ? 'bg-user-msg-bg/40' : 'bg-ai-msg-placeholder/50') : (isUser ? 'bg-user-msg-bg/30' : 'bg-status-msg-bg/50')) 
      : '';

  const imageContainerStyle: React.CSSProperties = {};
  if ((isImageSuccessfullyDisplayed || isVideoSuccessfullyDisplayed) && message.id === transitioningImageId) {
      imageContainerStyle.viewTransitionName = `image-transition-${message.id}`;
      imageContainerStyle.contain = 'layout';
  }
  
  if ((isAnnotationActive || (isFocusedMode && isImageSuccessfullyDisplayed)) && imageAspectRatio) {
    imageContainerStyle.aspectRatio = `${imageAspectRatio}`;
  }
  const bubbleWrapperStyle: React.CSSProperties = {
    touchAction: 'pan-y',
    // @ts-ignore
    containerType: 'inline-size',
    width: '100%',
    ...(isAttachmentSvg && detachedTranscriptBottomPadding > 0 && !isAnnotationActive
      ? { paddingBottom: `${detachedTranscriptBottomPadding}px` }
      : null),
    ...bubbleShapeStyle,
  };

  // Build corner-lift class list for un-taped corners
  const cornerLiftClasses = tapeLayout.liftedCorners.length > 0
    ? `msg-corner-lift msg-corner-lift-${tapeLayout.liftedCorners[0]}`
    : '';
  const overlayIconShadowStyle: React.CSSProperties = {
    filter: 'drop-shadow(0 1px 2px rgba(0,0,0,0.72))',
  };
  const overlayIconButtonBaseClasses = 'rounded-full opacity-85 transition-all duration-200 hover:opacity-100 focus:outline-none focus:ring-2 active:scale-95 disabled:opacity-40 disabled:cursor-default';
  const overlayNeutralIconButtonClasses = `${overlayIconButtonBaseClasses} text-white/90 hover:text-white focus:ring-white/40`;
  const overlayAccentIconButtonClasses = `${overlayIconButtonBaseClasses} text-annotation-btn-text focus:ring-annotation-btn-focus`;
  const focusToggleLabel = isFocusedMode ? t('chat.focusedMode.exit') : t('chat.focusedMode.enter');
  const shouldShowFocusedModeToggle = hasVisibleAttachment && !isAnnotationActive;
  const focusTogglePlacementClasses = isPdfSuccessfullyDisplayed ? 'bottom-2 left-2' : 'bottom-2 right-2';
  const focusToggleButtonClasses = `absolute ${focusTogglePlacementClasses} z-30 flex h-8 w-8 items-center justify-center ${overlayNeutralIconButtonClasses} touch-none`;

  return (
    <div className={`flex mb-4 ${bubbleAlignClass}`}>
      <div className={`relative ${tapeWrapperMaxWidth} ${tapeLayout.liftedCorners.length > 0 ? 'msg-lifted-shadow' : ''}`} style={{ width: '100%' }}>
        {/* Tape strips */}
        {tapeLayout.tapes.map((tape, i) => (
          <div
            key={i}
            className={`msg-tape${tape.wrinkled ? ' msg-tape-wrinkled' : ''}${tape.lifted ? ' msg-tape-lifted' : ''}`}
            style={tapeStripStyle(tape) as React.CSSProperties}
          />
        ))}
      <div
        className={`${bubbleWrapperClasses} ${cornerLiftClasses}`}
        style={bubbleWrapperStyle}
        ref={registerBubbleEl}
      >
          {hasVisibleAttachment && (
               <div 
                  ref={annotationViewportRef} 
                  className={`${imageContainerBaseClasses} ${imageContainerSizeClasses} ${imageContainerAspectClasses} ${imageContainerDynamicBg} ${imageContainerFlexCenteringClasses}`}
                  style={{ ...imageContainerStyle, overflow: 'hidden', touchAction: isAnnotationActive ? 'none' : 'auto', position: 'relative' }}
                  onPointerDown={isAnnotationActive ? handleAnnotationAreaPointerDown : undefined}
                  onPointerMove={isAnnotationActive ? handleAnnotationAreaPointerMove : undefined}
                  onPointerUp={isAnnotationActive ? handleModalPointerUp : undefined}
                  onPointerCancel={isAnnotationActive ? handleModalPointerUp : undefined}
                  onWheel={isAnnotationActive ? handleAnnotationAreaWheel : undefined}
                >
                  {isAttachmentLoading && !shouldShowAudioAttachmentPlaceholder && (
                      <div className="absolute top-2 right-2 flex flex-col items-end z-20">
                        <div className="w-8 h-8 rounded-full overflow-hidden bg-black/30 drop-shadow-md flex items-center justify-center">
                          {selectedLoadingAnimation && !loadingAnimationError ? (
                            <video
                              src={selectedLoadingAnimation}
                              autoPlay
                              loop
                              muted
                              playsInline
                              className="w-full h-full object-cover opacity-90"
                              onError={() => setLoadingAnimationError(true)}
                            />
                          ) : (
                            <SmallSpinner className={`w-full h-full ${userAttachmentTextClass}`} />
                          )}
                        </div>
                        {remainingTimeDisplay && (
                          <p className={`mt-1 text-right text-xs px-1.5 py-0.5 rounded ${applyFocusedImageStyles ? `${userAttachmentSubtleTextClass} bg-user-msg-bg/60` : 'text-thinking-bubble-text bg-status-msg-bg/70'}`}>
                            {remainingTimeDisplay}
                          </p>
                        )}
                      </div>
                  )}

                  {(isImageSuccessfullyDisplayed || (isAnnotationActive && annotationSourceUrl)) && (
                    <>
                        {!isAnnotationActive && isAttachmentSvg && showSvgCodeView ? (
                          <div
                            className={`relative w-full h-full overflow-auto rounded-lg ${isUser ? 'bg-user-msg-bg/20' : 'bg-ai-file-bg'}`}
                            style={{
                              overscrollBehavior: 'contain',
                              touchAction: 'pan-x pan-y',
                              WebkitOverflowScrolling: 'touch' as any,
                            }}
                          >
                            <pre className={`p-3 text-[11px] leading-5 font-mono whitespace-pre w-max min-w-full ${isUser ? 'text-user-attachment-inline-text' : 'text-ai-file-text'}`}>
                              {svgSourceCode || 'SVG source unavailable for this attachment.'}
                            </pre>
                          </div>
                        ) : (
                          <div 
                              style={isAnnotationActive ? {
                                  width: imageForAnnotationRef.current?.naturalWidth,
                                  height: imageForAnnotationRef.current?.naturalHeight,
                                  position: 'absolute',
                                  top: '50%',
                                  left: '50%',
                                  transform: `translate(-50%, -50%) translate(${pan.x}px, ${pan.y}px) scale(${scale})`,
                                  transition: activePointersRef.current.length > 0 ? 'none' : 'transform 0.1s ease-out',
                              } : {
                                  position: 'relative',
                                  width: '100%',
                                  height: '100%',
                              }}
                          >
                            <img
                              ref={imageForAnnotationRef}
                              src={isAnnotationActive ? annotationSourceUrl! : displayUrl!}
                              alt={isAnnotationActive ? t('chat.annotateModal.editingPreviewAlt') : (t('chat.imagePreview.alt'))}
                              className={`block w-full h-full pointer-events-none ${!isAnnotationActive ? 'object-contain' : ''}`}
                              style={{ opacity: 1 }}
                              onLoad={(e) => handleAttachmentImageLoad(e.currentTarget)}
                            />
                            {isAnnotationActive && ( <canvas ref={editCanvasRef} className="absolute top-0 left-0 w-full h-full cursor-crosshair" /> )}
                          </div>
                        )}
      {!isAnnotationActive && isImageSuccessfullyDisplayed && isAttachmentSvg && (
                            <button
        onClick={() => setShowSvgCodeView((prev) => !prev)}
                                className={`absolute top-2 left-2 z-20 p-2 ${overlayNeutralIconButtonClasses}`}
                                title={showSvgCodeView ? 'Show SVG preview' : 'Show SVG code'}
                                aria-label={showSvgCodeView ? 'Show SVG preview' : 'Show SVG code'}
                            >
                                <span style={overlayIconShadowStyle}>
                                  {showSvgCodeView ? <IconChevronLeft className="w-5 h-5" /> : <IconChevronRight className="w-5 h-5" />}
                                </span>
                            </button>
                        )}
      {!isAnnotationActive && isFocusedMode && isImageSuccessfullyDisplayed && !(isAttachmentSvg && showSvgCodeView) && (
                            <button
        onClick={() => handleStartAnnotation(displayUrl!)}
                                className={`absolute top-2 right-2 z-20 p-2 ${overlayNeutralIconButtonClasses}`}
                                title={t('chat.annotateImage')}
                                aria-label={t('chat.annotateImage')}
                            >
                                <span style={overlayIconShadowStyle}>
                                  <IconPencil className="w-5 h-5" />
                                </span>
                            </button>
                        )}
                        {isAnnotationActive && (
                          <>
                            {pdfPageCount > 1 && (
                              <div
                                className="absolute top-2 left-2 z-30 pointer-events-auto flex items-center space-x-1"
                                onPointerDown={(e) => { e.stopPropagation(); e.preventDefault(); }}
                                onPointerUp={(e) => { e.stopPropagation(); e.preventDefault(); }}
                                onPointerCancel={(e) => { e.stopPropagation(); e.preventDefault(); }}
                                onClick={(e) => { e.stopPropagation(); e.preventDefault(); }}
                              >
                                <button
                                  onClick={() => handlePdfPageChange(-1)}
                                  disabled={pdfPageNum <= 1}
                                  className={`p-1.5 ${overlayNeutralIconButtonClasses}`}
                                >
                                  <span style={overlayIconShadowStyle}>
                                    <IconChevronLeft className="w-4 h-4" />
                                  </span>
                                </button>
                                <span className="text-white text-xs font-medium tabular-nums bg-black/60 rounded px-1.5 py-0.5 select-none">
                                  {pdfPageNum}/{pdfPageCount}
                                </span>
                                <button
                                  onClick={() => handlePdfPageChange(1)}
                                  disabled={pdfPageNum >= pdfPageCount}
                                  className={`p-1.5 ${overlayNeutralIconButtonClasses}`}
                                >
                                  <span style={overlayIconShadowStyle}>
                                    <IconChevronRight className="w-4 h-4" />
                                  </span>
                                </button>
                              </div>
                            )}
                            <div
                              className="absolute top-2 right-2 z-30 pointer-events-auto"
                              onPointerDown={(e) => { e.stopPropagation(); e.preventDefault(); }}
                              onPointerUp={(e) => { e.stopPropagation(); e.preventDefault(); }}
                              onPointerCancel={(e) => { e.stopPropagation(); e.preventDefault(); }}
                              onClick={(e) => { e.stopPropagation(); e.preventDefault(); }}
                            >
                              <button
                                onClick={handleCancelAnnotation}
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
                              className="absolute bottom-2 left-2 z-30 pointer-events-auto"
                              onPointerDown={(e) => { e.stopPropagation(); e.preventDefault(); }}
                              onPointerUp={(e) => { e.stopPropagation(); e.preventDefault(); }}
                              onPointerCancel={(e) => { e.stopPropagation(); e.preventDefault(); }}
                              onClick={(e) => { e.stopPropagation(); e.preventDefault(); }}
                            >
                              <button
                                onClick={handleUndo}
                                disabled={undoStack.length === 0}
                                className={`p-2 ${overlayNeutralIconButtonClasses} disabled:cursor-not-allowed`}
                                title={t('chat.annotateModal.undo')}
                                aria-label={t('chat.annotateModal.undo')}
                                aria-disabled={undoStack.length === 0}
                              >
                                <span style={overlayIconShadowStyle}>
                                  <IconUndo className="w-5 h-5" />
                                </span>
                              </button>
                            </div>
                            <div
                              className="absolute bottom-2 right-2 z-30 pointer-events-auto"
                              onPointerDown={(e) => { e.stopPropagation(); e.preventDefault(); }}
                              onPointerUp={(e) => { e.stopPropagation(); e.preventDefault(); }}
                              onPointerCancel={(e) => { e.stopPropagation(); e.preventDefault(); }}
                              onClick={(e) => { e.stopPropagation(); e.preventDefault(); }}
                            >
                              <button
                                onClick={handleSaveAnnotation}
                                className={`p-2 ${overlayAccentIconButtonClasses}`}
                                title={t('chat.annotateModal.saveAndAttach')}
                                aria-label={t('chat.annotateModal.saveAndAttach')}
                              >
                                <span style={overlayIconShadowStyle}>
                                  <IconCheck className="w-5 h-5" />
                                </span>
                              </button>
                            </div>
                          </>
                        )}
                    </>
                  )}

                  {isVideoSuccessfullyDisplayed && !isAnnotationActive && (
                      <div className="relative">
              <video
                              ref={videoRef}
                src={displayUrl!}
                              controls
                              onPlay={() => {
                                setIsVideoPlaying(true);
                                if (!videoPlayTokenRef.current) {
                                  videoPlayTokenRef.current = createUiToken(TOKEN_SUBTYPE.VIDEO_PLAY);
                                }
                              }}
                              onPause={() => {
                                setIsVideoPlaying(false);
                                if (videoPlayTokenRef.current) {
                                  removeActivityToken(videoPlayTokenRef.current);
                                  videoPlayTokenRef.current = null;
                                }
                              }}
                              onEnded={() => {
                                setIsVideoPlaying(false);
                                if (videoPlayTokenRef.current) {
                                  removeActivityToken(videoPlayTokenRef.current);
                                  videoPlayTokenRef.current = null;
                                }
                              }}
                              className={`block w-full h-full object-contain rounded-lg bg-black`}
                          >
                              {t('chat.videoNotSupported')}
                          </video>
                          <button
                            onClick={handleAnnotateVideo}
                            disabled={isVideoPlaying}
                            className={`absolute top-2 right-2 z-20 p-1.5 ${overlayNeutralIconButtonClasses} disabled:cursor-not-allowed`}
                            title={isVideoPlaying ? t('chat.error.pauseVideoToAnnotate') : t('chat.annotateVideoFrame')}
                            aria-label={isVideoPlaying ? t('chat.error.pauseVideoToAnnotate') : t('chat.annotateVideoFrame')}
                          >
                              <span style={overlayIconShadowStyle}>
                                <IconPencil className="w-4 h-4" />
                              </span>
                          </button>
                      </div>
                  )}

                  {shouldShowFocusedModeToggle && (
                    <button
                      ref={resizerRef}
                      type="button"
                      onPointerDown={handleResizePointerDown}
                      onClick={(e) => {
                        if ((e.detail ?? 1) !== 0) return;
                        e.preventDefault();
                        e.stopPropagation();
                        onToggleImageFocusedMode();
                      }}
                      className={focusToggleButtonClasses}
                      title={focusToggleLabel}
                      aria-label={focusToggleLabel}
                      aria-pressed={isFocusedMode}
                    >
                      <div style={overlayIconShadowStyle}>
                        <IconGripCorner className="w-4 h-4" />
                      </div>
                    </button>
                  )}
                  {isTextFileSuccessfullyDisplayed && (
                    <TextFileViewer
                      src={displayUrl!}
                      variant={isUser ? 'user' : 'assistant'}
                      fileName={message.attachmentName}
                      mimeType={displayMime}
                      bottomInset={scrollableAttachmentBottomInset}
                    />
                  )}
                  {isOfficeFileSuccessfullyDisplayed && (
                      <OfficeFileViewer
                      src={displayUrl}
                      variant={isUser ? 'user' : 'assistant'}
                      fileName={message.attachmentName}
                      mimeType={displayMime}
                      hasRemoteUri={hasRemoteAttachment}
                    />
                  )}
                  {isTextFileRemoteOnly && (
                    <div className={`p-4 flex flex-col items-center justify-center text-center rounded-lg ${isUser ? 'bg-user-msg-bg/80' : 'bg-ai-file-bg'}`}>
                      <IconPaperclip className={`w-10 h-10 ${isUser ? 'text-user-attachment-inline-text/70' : 'text-ai-file-text'}`} />
                      <p className={`mt-2 text-xs font-mono break-all ${isUser ? 'text-user-attachment-inline-text' : 'text-ai-file-text'}`}>
                        {message.attachmentName || displayMime || 'text file'}
                      </p>
                      <p className={`mt-1 text-xs ${isUser ? 'text-user-attachment-inline-text/70' : 'text-ai-file-text'}`}>
                        Local text preview unavailable.
                      </p>
                    </div>
                  )}
                  {isFileSuccessfullyDisplayed && (
                      <div className={`p-4 flex flex-col items-center justify-center text-center rounded-lg h-full ${isUser ? 'bg-user-msg-bg/80' : 'bg-ai-file-bg'}`}>
                          <IconPaperclip className={`w-10 h-10 ${isUser ? 'text-user-attachment-inline-text/70' : 'text-ai-file-text'}`} />
                          <p className={`mt-2 text-xs font-mono break-all ${isUser ? 'text-user-attachment-inline-text' : 'text-ai-file-text'}`}>{message.attachmentName || displayMime}</p>
                          <p className={`mt-1 text-xs ${isUser ? 'text-user-attachment-inline-text/70' : 'text-ai-file-text'}`}>{t('chat.fileAttachment')}</p>
                      </div>
                  )}
                  {isPdfSuccessfullyDisplayed && (
                    <div className={`relative w-full ${isAnnotationActive ? 'hidden' : ''}`}>
                      <PdfViewer
                        src={displayUrl!}
                        variant={isUser ? 'user' : 'assistant'}
                        bottomInset={scrollableAttachmentBottomInset}
                      />
                      {isFocusedMode && !isAnnotationActive && (
                        <button
                          onClick={handleAnnotatePdf}
                          className={`absolute top-2 right-2 z-20 p-1.5 ${overlayNeutralIconButtonClasses}`}
                          title={t('chat.annotateImage')}
                          aria-label={t('chat.annotateImage')}
                        >
                          <span style={overlayIconShadowStyle}>
                            <IconPencil className="w-4 h-4" />
                          </span>
                        </button>
                      )}
                    </div>
                  )}
          {usesAudioAttachmentShell && !isAnnotationActive && (
            <div className="relative w-full">
              <AudioPlayer
                src={displayUrl}
                variant={isUser ? 'user' : 'assistant'}
                statusText={audioAttachmentPlaceholderStatusText}
                waveformSeed={message.id || message.attachmentName || message.maestroToolKind || 'audio'}
                placeholderProgress={audioAttachmentPlaceholderProgress}
              />
            </div>
          )}
              </div>
          )}
          
          {message.imageGenError && !isAttachmentLoading && (
               <div className={`flex flex-col items-center justify-center p-2 rounded-lg 
                  ${applyFocusedImageStyles ? 'absolute inset-0 bg-black/60 z-20' : `my-2 ${isUser ? 'bg-user-msg-bg/60' : 'bg-status-msg-bg/60'}`}
               `}>
                  <IconXMark className="w-8 h-8 text-img-error-text mb-1"/>
                  <p className={`text-xs text-center ${applyFocusedImageStyles ? 'text-input-error-text/80' : 'text-img-error-text'}`}>
                      {t('chat.imageGenError')}: {message.imageGenError}
                  </p>
              </div>
          )}

        {isAssistant && toolAttachmentStatusText && !isAttachmentLoading && !hasAttachmentSource && !shouldShowAudioAttachmentPlaceholder && (
          <div className="mb-2">
            <p className="inline-flex items-center rounded-full bg-status-msg-bg/70 px-2.5 py-1 text-xs text-thinking-bubble-text">
              {toolAttachmentStatusText}
            </p>
          </div>
        )}

        {hasTextContent && (
           <div className={`transition-opacity duration-300
                ${textOverlayClasses}
                ${shouldUseScrollableTextOverlay ? 'pointer-events-none' : ''}
                ${isAnnotationActive ? 'opacity-0 pointer-events-none' : 'opacity-100'}
            `}
            style={textOverlayScrollStyle}
            onWheel={shouldUseScrollableTextOverlay ? (e) => e.stopPropagation() : undefined}
            onTouchMove={shouldUseScrollableTextOverlay ? (e) => e.stopPropagation() : undefined}
            ref={textOverlayRef}
           >
        <style>{`
        @keyframes pop-fade-speak { 0% { transform: scale(0.85); opacity: 0; } 20% { transform: scale(1.15); opacity: 1; } 80% { transform: scale(1.0); opacity: 1; } 100% { transform: scale(0.95); opacity: 0; } }
        .animate-speak-flash { animation: pop-fade-speak 900ms ease-out both; }
        `}</style>
               {isAssistant && applyFocusedImageStyles && message.translations && message.translations.length > 0 ? (
                   <>
             <TextScrollwheel
                           translations={message.translations}
                           speakingUtteranceText={speakingUtteranceText}
                           currentTargetLangCode={currentTargetLangCode}
                           currentNativeLangCode={currentNativeLangCode}
                           t={t}
                             isSpeaking={isSpeaking}
                           isSending={isSending}
                           speakText={speakText}
                             stopSpeaking={stopSpeaking}
               speakNativeLang={speakNativeLang}
               onToggleSpeakNativeLang={onToggleSpeakNativeLang}
               messageId={message.id}
               colorMode={isMiniGameAttachment ? 'game' : isAttachmentSvg ? 'svg' : usesAudioAttachmentShell ? 'audio' : 'overlay'}
                       />
                   </>
               ) : (
                 <>
                  {userMessageTextNode && (
                    shouldUseScrollableUserTextShell ? (
                      <AttachmentTextScrollContainer
                        spacerClassName={userAttachmentSubtleTextClass}
                        onWheel={(e) => e.stopPropagation()}
                        onTouchMove={(e) => e.stopPropagation()}
                      >
                        <div className="text-center p-1 w-full transition-all duration-300 transform-gpu opacity-100 scale-105">
                          {userMessageTextNode}
                        </div>
                      </AttachmentTextScrollContainer>
                    ) : userMessageTextNode
                  )}
 
                   {isAssistant && message.translations && message.translations.length > 0 && message.translations.map((pair, index) => {
                     const cleanedUtterance = speakingUtteranceText?.replace(/\*/g, '') ?? null;
                     const isCurrentlySpeakingTarget = pair.target && cleanedUtterance === pair.target.replace(/\*/g, '');
                     const isCurrentlySpeakingNative = pair.native && cleanedUtterance === pair.native.replace(/\*/g, '');
                     const isCurrentLineSpeaking = isCurrentlySpeakingTarget || isCurrentlySpeakingNative;
 
                     return (
                     <div key={index} className={index > 0 && !applyFocusedImageStyles ? "mt-2" : ""}>
                       {pair.target && (
                         <p
                             className={`font-semibold whitespace-pre-wrap cursor-pointer transition-colors rounded-sm px-1 -mx-1 ${
                                 isCurrentLineSpeaking
                                   ? 'bg-marker-target-bg text-marker-target-text'
                                   : `${assistantTargetHoverTextClass} ${assistantTargetTextClass}`
                             }`}
                             style={{ fontSize: '4cqw', lineHeight: 1.3 }}
                             onPointerDown={handleLinePointerDown}
                             onPointerUp={(e) => {
                               if (pointerDownPosRef.current) {
                                 const dx = Math.abs(e.clientX - pointerDownPosRef.current.x);
                                 const dy = Math.abs(e.clientY - pointerDownPosRef.current.y);
                                 if (dx < 10 && dy < 10) {
                                   e.preventDefault();
                                   if (isSpeaking) { stopSpeaking(); pointerDownPosRef.current = null; return; }
                                   if (isSending) { pointerDownPosRef.current = null; return; }
                                   const startIdx = index;
                                   const parts: SpeechPart[] = [];
                                   const msgContext = { source: 'message' as const, messageId: message.id };
                                   for (let i = startIdx; i < (message.translations?.length || 0); i++) {
                                     const p = message.translations![i];
                                     const t = p.target?.trim();
                                     const n = p.native?.trim();
                                     if (t) parts.push({ text: t, langCode: currentTargetLangCode, context: msgContext });
                                     if (speakNativeLang && n) parts.push({ text: n, langCode: currentNativeLangCode, context: msgContext });
                                   }
                                   if (parts.length) speakText(parts, parts[0].langCode);
                                 }
                               }
                               pointerDownPosRef.current = null;
                             }}
                             onPointerLeave={handleLinePointerLeave}
                             onKeyDown={(e) => {
                               if (e.key === 'Enter' || e.key === ' ') {
                                 e.preventDefault();
                                 if (isSpeaking) {
                                   stopSpeaking();
                                 } else if (!isSending) {
                                   handleSpeakLine(pair.target, currentTargetLangCode, pair.native, currentNativeLangCode, message.id);
                                 }
                               }
                             }}
                             role="button"
                             tabIndex={isSending && !isSpeaking ? -1 : 0} 
                             aria-label={`${isSpeaking ? t('chat.stopSpeaking') : t('chat.speakThisLine')}: ${pair.target.replace(/\*/g, '')}`}
                             aria-disabled={isSending && !isSpeaking}
                         >
                             {pair.target}
                         </p>
                       )}
                       {pair.native && (
                        <p className={`italic mt-0.5 whitespace-pre-wrap pl-2 border-l-2 rounded-sm px-1 -mx-1 ${
                             isCurrentLineSpeaking
                               ? 'bg-marker-native-bg text-marker-native-text border-marker-native-text/40'
                               : assistantNativeTextClass
                         }`} style={{ fontSize: '3.55cqw', lineHeight: 1.3 }}
                         onPointerDown={handleLinePointerDown}
                         onPointerUp={(e) => {
                           if (pointerDownPosRef.current) {
                             const dx = Math.abs(e.clientX - pointerDownPosRef.current.x);
                             const dy = Math.abs(e.clientY - pointerDownPosRef.current.y);
                             if (dx < 10 && dy < 10) {
                               e.preventDefault();
                               const next = !speakNativeLang;
                               setNativeFlashIndex(index);
                               setNativeFlashIsOn(next);
                               if (nativeFlashTimeoutRef.current) clearTimeout(nativeFlashTimeoutRef.current);
                               nativeFlashTimeoutRef.current = window.setTimeout(() => { setNativeFlashIndex(null); }, 900);
                               onToggleSpeakNativeLang();
                             }
                           }
                           pointerDownPosRef.current = null;
                         }}
                         onPointerLeave={handleLinePointerLeave}
                       >
                          {pair.native}
                          {nativeFlashIndex === index && (
                            <span className="ml-1 inline-block align-middle animate-speak-flash">
                              {nativeFlashIsOn ? <IconSpeaker className="w-3 h-3 inline" /> : <IconVolumeOff className="w-3 h-3 inline" />}
                            </span>
                          )}
                         </p>
                       )}
                     </div>
                   )})}
                   {isAssistant && (!message.translations || message.translations.length === 0) && message.rawAssistantResponse && (
                     (() => {
                       const isCurrentlySpeakingRaw = message.rawAssistantResponse && (speakingUtteranceText?.replace(/\*/g, '') ?? null) === message.rawAssistantResponse.replace(/\*/g, '');
                       return (
                         <p
                             className={`whitespace-pre-wrap cursor-pointer transition-colors rounded-sm px-1 -mx-1 ${
                                 isCurrentlySpeakingRaw
                                   ? 'bg-marker-target-bg text-marker-target-text'
                                   : `${assistantTargetHoverTextClass} ${assistantTargetTextClass}`
                             }`} style={{ fontSize: '4cqw', lineHeight: 1.3 }}
                             onPointerDown={handleLinePointerDown}
                             onPointerUp={(e) => handleLinePointerUp(e, message.rawAssistantResponse!, currentTargetLangCode)}
                             onPointerLeave={handleLinePointerLeave}
                             onKeyDown={(e) => {
                               if (e.key === 'Enter' || e.key === ' ') {
                                 e.preventDefault();
                                 if (isSpeaking) {
                                   stopSpeaking();
                                 } else if (!isSending) {
                                   handleSpeakLine(message.rawAssistantResponse!, currentTargetLangCode, undefined, undefined, message.id);
                                 }
                               }
                             }}
                             role="button"
                             tabIndex={isSending && !isSpeaking ? -1 : 0} 
                             aria-label={`${isSpeaking ? t('chat.stopSpeaking') : t('chat.speakThisLine')}: ${message.rawAssistantResponse!.replace(/\*/g, '')}`}
                             aria-disabled={isSending && !isSpeaking}
                         >
                             {message.rawAssistantResponse}
                         </p>
                       );
                     })()
                   )}
                   {(isError || isStatus) && message.text && (
                     <p className={`${ useOverlayTextColors ? (isError ? 'text-img-error-text font-semibold' : 'text-status-msg-text font-semibold') : ''}`}
                        style={{ fontSize: '3.2cqw', lineHeight: 1.25 }}>
                         {message.text}
                     </p>
                   )}
                   {isError && message.errorAction === 'quota' && (
                     <div className="flex flex-wrap gap-2 mt-2">
                       {onQuotaSetupBilling && (
                         <button
                           type="button"
                           onClick={onQuotaSetupBilling}
                           className="inline-flex items-center gap-1.5 px-3 py-1.5 text-cta-btn-text bg-cta-btn-bg hover:bg-cta-btn-bg/80 sketchy-border-thin"
                           style={{ fontSize: '2.8cqw', lineHeight: 1.25 }}
                         >
                           {t('error.quotaSetupBilling')}
                         </button>
                       )}
                       {onQuotaStartLive && (
                         <button
                           type="button"
                           onClick={onQuotaStartLive}
                           className="inline-flex items-center gap-1.5 px-3 py-1.5 text-cta-btn-text bg-cta-btn-bg hover:bg-cta-btn-bg/80 sketchy-border-thin"
                           style={{ fontSize: '2.8cqw', lineHeight: 1.25 }}
                         >
                           {t('error.quotaStartLive')}
                         </button>
                       )}
                     </div>
                   )}
                   {isError && message.errorAction === 'imageGenCost' && (
                     <div className="flex flex-wrap gap-2 mt-2">
                       {onImageGenDisable && (
                         <button
                           type="button"
                           onClick={onImageGenDisable}
                           className="inline-flex items-center gap-1.5 px-3 py-1.5 text-cta-btn-text bg-cta-btn-bg hover:bg-cta-btn-bg/80 sketchy-border-thin"
                           style={{ fontSize: '2.8cqw', lineHeight: 1.25 }}
                         >
                           {t('error.imageGenDisable')}
                         </button>
                       )}
                       {onImageGenViewCost && (
                         <button
                           type="button"
                           onClick={onImageGenViewCost}
                           className="inline-flex items-center gap-1.5 px-3 py-1.5 text-cta-btn-text bg-cta-btn-bg hover:bg-cta-btn-bg/80 sketchy-border-thin"
                           style={{ fontSize: '2.8cqw', lineHeight: 1.25 }}
                         >
                           {t('error.imageGenViewCost')}
                         </button>
                       )}
                     </div>
                  )}
                  {isAssistant && !message.translations?.length && !message.rawAssistantResponse && !displayUrl && !isAttachmentLoading && message.text && (
                     <p className={`whitespace-pre-wrap ${assistantTargetTextClass}`} style={{ fontSize: '3.6cqw', lineHeight: 1.35 }}>{message.text}</p>
                   )}
               </>
             )}
           </div>
         )}
       </div>
        {/* Corner lift overlays for un-taped corners beyond the first */}
        {tapeLayout.liftedCorners.slice(1).map(corner => (
          <div key={corner} className={`absolute pointer-events-none msg-corner-lift msg-corner-lift-${corner}`} style={{ inset: 0, zIndex: 2 }}>
            <div style={{ position: 'absolute', ...(corner.includes('t') ? { top: -1 } : { bottom: -1 }), ...(corner.includes('l') ? { left: -1 } : { right: -1 }), width: 28, height: 28, borderRadius: corner === 'tl' ? '0 0 8px 0' : corner === 'tr' ? '0 0 0 8px' : corner === 'bl' ? '0 8px 0 0' : '8px 0 0 0', background: corner === 'tl' ? 'linear-gradient(135deg, hsl(var(--page-bg)) 25%, hsl(var(--page-bg) / 0.5) 45%, transparent 70%)' : corner === 'tr' ? 'linear-gradient(225deg, hsl(var(--page-bg)) 25%, hsl(var(--page-bg) / 0.5) 45%, transparent 70%)' : corner === 'bl' ? 'linear-gradient(45deg, hsl(var(--page-bg)) 25%, hsl(var(--page-bg) / 0.5) 45%, transparent 70%)' : 'linear-gradient(315deg, hsl(var(--page-bg)) 25%, hsl(var(--page-bg) / 0.5) 45%, transparent 70%)' }} />
          </div>
        ))}
       </div>
     </div>
   );
 });
 ChatMessageBubble.displayName = "ChatMessageBubble";

 export default ChatMessageBubble;




