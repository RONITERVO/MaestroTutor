
import React, { useState, useRef, useEffect, useCallback, useMemo } from 'react';
import { ChatMessage, SpeechPart } from '../../../core/types';
import { TranslationReplacements } from '../../../core/i18n/index';
import { IconPaperclip, IconXMark, IconPencil, IconUndo, IconGripCorner, IconCheck } from '../../../shared/ui/Icons';
import TextScrollwheel from './TextScrollwheel';

interface ChatMessageBubbleProps { 
  message: ChatMessage; 
  isFocusedMode: boolean; 
  speakingUtteranceText: string | null; 
  estimatedLoadTime: number; 
  isSending: boolean;
  loadingGifs?: string[] | null;
  currentTargetLangCode: string;
  currentNativeLangCode: string;
  t: (key: string, replacements?: TranslationReplacements) => string;
  isSpeaking: boolean;
  speakNativeLang: boolean;
  onToggleSpeakNativeLang: () => void;
  handleSpeakWholeMessage: (message: ChatMessage) => void;
  handleSpeakLine: (targetText: string, targetLangCode: string, nativeText?: string, nativeLangCode?: string, sourceMessageId?: string) => void;
  handlePlayUserMessage: (message: ChatMessage) => void;
  speakText: (textOrParts: SpeechPart[], defaultLang: string) => void;
  stopSpeaking: () => void;
  isTtsSupported: boolean;
  onToggleImageFocusedMode: () => void;
  transitioningImageId: string | null;
  onSetAttachedImage: (base64: string | null, mimeType: string | null) => void;
  onUserInputActivity: () => void;
  registerBubbleEl?: (el: HTMLDivElement | null) => void;
  onUiTaskStart?: (token?: string) => string | void;
  onUiTaskEnd?: (token?: string) => void;
}

const ChatMessageBubble: React.FC<ChatMessageBubbleProps> = React.memo(({ 
  message, isFocusedMode, speakingUtteranceText, estimatedLoadTime, isSending, loadingGifs,
  currentTargetLangCode, currentNativeLangCode, t,
  isSpeaking, speakNativeLang, onToggleSpeakNativeLang, handleSpeakWholeMessage: _handleSpeakWholeMessage, handleSpeakLine, handlePlayUserMessage, speakText, stopSpeaking, isTtsSupported: _isTtsSupported,
  onToggleImageFocusedMode, transitioningImageId, onSetAttachedImage, onUserInputActivity,
  registerBubbleEl,
  onUiTaskStart,
  onUiTaskEnd
}) => {
  const isUser = message.role === 'user';

  const [isAnnotating, setIsAnnotating] = useState(false);
  const [annotationSourceUrl, setAnnotationSourceUrl] = useState<string | null>(null);
  const [imageAspectRatio, setImageAspectRatio] = useState<number | null>(null);

  const isAnnotationActive = isAnnotating && isFocusedMode;
  const isAssistant = message.role === 'assistant';
  const isError = message.role === 'error';
  const isStatus = message.role === 'status';
  
  const [remainingTimeDisplay, setRemainingTimeDisplay] = useState<string | null>(null);
  const [isVideoPlaying, setIsVideoPlaying] = useState(false);
  const videoPlayTokenRef = useRef<string | null>(null);
  const isSpeakDisabled = isSending || isSpeaking;
  const resizerRef = useRef<HTMLDivElement>(null);

  const pointerDownPosRef = useRef<{x: number, y: number} | null>(null);
  const [nativeFlashIndex, setNativeFlashIndex] = useState<number | null>(null);
  const [nativeFlashIsOn, setNativeFlashIsOn] = useState<boolean>(false);
  const nativeFlashTimeoutRef = useRef<number | null>(null);
  const videoRef = useRef<HTMLVideoElement>(null);

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
  const genToken = useCallback((tag: string) => `${tag}:${Date.now()}:${Math.random().toString(36).slice(2,8)}`,[ ]);

  
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
      const tok = genToken('bubble-annotate');
      const ret = onUiTaskStart?.(tok);
      annotationTokenRef.current = typeof ret === 'string' ? ret : tok;
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
  }, [isFocusedMode, onUserInputActivity, genToken, onUiTaskStart]);

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
            handleSpeakLine(targetText, targetLangCode, nativeText, nativeLangCode, message.id);
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
        if (isSpeakDisabled) {
          stopSpeaking();
          pointerDownPosRef.current = null;
          return;
        }
        handlePlayUserMessage(message);
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
      } else {
          setRemainingTimeDisplay(null);
      }
      return () => {
          if (intervalId) clearInterval(intervalId);
      };
  }, [message.isGeneratingImage, message.imageGenerationStartTime, estimatedLoadTime, t]);

  useEffect(() => {
    return () => {
      if (videoPlayTokenRef.current && onUiTaskEnd) {
        onUiTaskEnd(videoPlayTokenRef.current);
        videoPlayTokenRef.current = null;
      }
    };
  }, [onUiTaskEnd]);

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
    if (annotationTokenRef.current && onUiTaskEnd) { onUiTaskEnd(annotationTokenRef.current); annotationTokenRef.current = null; }
  }, [onUiTaskEnd]);

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
    if (annotationTokenRef.current && onUiTaskEnd) { onUiTaskEnd(annotationTokenRef.current); annotationTokenRef.current = null; }
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
        
        if (newStack.length > 0) {
            ctx.putImageData(newStack[newStack.length - 1], 0, 0);
        }
        
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
          isDrawingRef.current = false;
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
  
        ctx.strokeStyle = '#EF4444'; 
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

  const displayUrl = (message.imageUrl || message.storageOptimizedImageUrl);
  const displayMime = (message.imageMimeType || message.storageOptimizedImageMimeType);
  const isAttachmentAnImage = !!displayMime?.startsWith('image/');
  const isAttachmentAVideo = !!displayMime?.startsWith('video/');
  const isAttachmentAAudio = !!displayMime?.startsWith('audio/');

  const isImageSuccessfullyDisplayed = isAttachmentAnImage && displayUrl && !message.isGeneratingImage && !message.imageGenError;
  const isVideoSuccessfullyDisplayed = isAttachmentAVideo && displayUrl;
  const isAudioSuccessfullyDisplayed = isAttachmentAAudio && displayUrl && !message.isGeneratingImage && !message.imageGenError;
  const isFileSuccessfullyDisplayed = !isAttachmentAnImage && !isAttachmentAVideo && !isAttachmentAAudio && displayUrl && !message.isGeneratingImage && !message.imageGenError;

  const selectedLoadingGif = useMemo(() => {
    const userLoadingGifs = loadingGifs as string[] | undefined;
    const source = (userLoadingGifs && userLoadingGifs.length > 0) ? userLoadingGifs : [];
    if (!message.isGeneratingImage || source.length === 0) return null;
    const seedStr = `${message.id || ''}|${message.imageGenerationStartTime || 0}`;
    let h = 0;
    for (let i = 0; i < seedStr.length; i++) {
      h = ((h << 5) - h) + seedStr.charCodeAt(i);
      h |= 0;
    }
    const idx = Math.abs(h) % source.length;
    return source[idx];
  }, [message.id, message.imageGenerationStartTime, message.isGeneratingImage, loadingGifs]);

  const applyFocusedImageStyles = isFocusedMode && (isImageSuccessfullyDisplayed || message.isGeneratingImage || isFileSuccessfullyDisplayed || isVideoSuccessfullyDisplayed || isAudioSuccessfullyDisplayed);
  
  if (message.thinking && !message.isGeneratingImage) {
    return (
      <div className="flex justify-start mb-3 animate-pulse">
        <div className="bg-gray-200 rounded-lg p-3 max-w-xl">
          <p className="text-sm text-gray-700">{t('chat.thinking')}</p>
        </div>
      </div>
    );
  }

  const bubbleAlignClass = isUser ? 'justify-end' : 'justify-start';
  const hasTextContent = message.text || (message.translations && message.translations.some(tr => tr.spanish || tr.english)) || message.rawAssistantResponse;
  const sanitizedUserText = message.text ? message.text.replace(/\*/g, '') : '';
  const isUserLineSpeaking = isUser && sanitizedUserText && speakingUtteranceText === sanitizedUserText;

  let bubbleWrapperClasses = "shadow relative overflow-hidden rounded-lg transition-all duration-300 ease-in-out";
   if (applyFocusedImageStyles) {
      bubbleWrapperClasses += " w-full"; 
      if (!isImageSuccessfullyDisplayed && !message.isGeneratingImage && !isFileSuccessfullyDisplayed && !isVideoSuccessfullyDisplayed) { 
           bubbleWrapperClasses += " p-3"; 
           if (isUser) bubbleWrapperClasses += " bg-blue-500 bg-opacity-90 text-white";
           else if (isError) bubbleWrapperClasses += " bg-red-100 bg-opacity-90 text-red-700";
           else if (isStatus) bubbleWrapperClasses += " bg-yellow-100 bg-opacity-90 text-yellow-700";
           else bubbleWrapperClasses += " bg-white bg-opacity-90 text-gray-800";
      }
  } else { 
      bubbleWrapperClasses += " p-3 max-w-[90%] sm:max-w-[80%] md:max-w-[70%] lg:max-w-[65%]";
      if (isUser) bubbleWrapperClasses += " bg-blue-500 bg-opacity-90 text-white";
      else if (isError) bubbleWrapperClasses += " bg-red-100 bg-opacity-90 text-red-700";
      else if (isStatus) bubbleWrapperClasses += " bg-yellow-100 bg-opacity-90 text-yellow-700";
      else bubbleWrapperClasses += " bg-white bg-opacity-90 text-gray-800";
  }

  const imageContainerBaseClasses = "relative rounded-lg group transition-all duration-300 ease-in-out";
  let imageContainerSizeClasses = "";
  let imageContainerAspectClasses = "";
  let imageContainerFlexCenteringClasses = "flex items-center justify-center";

  if (applyFocusedImageStyles) {
      imageContainerSizeClasses = "w-full max-h-[75vh]"; 
      if (message.isGeneratingImage || isFileSuccessfullyDisplayed) {
          imageContainerAspectClasses = "aspect-square"; 
      } else if (isImageSuccessfullyDisplayed || isVideoSuccessfullyDisplayed) {
          if (isAnnotationActive) {
              imageContainerAspectClasses = "bg-gray-800"; 
              imageContainerFlexCenteringClasses = ""; 
          } else {
              imageContainerAspectClasses = "";
          }
      }
  } else { 
      imageContainerSizeClasses = "w-full max-w-[250px] mx-auto my-2";
      imageContainerAspectClasses = "aspect-square"; 
  }
  
  const imageContainerDynamicBg = message.isGeneratingImage ? 
      (applyFocusedImageStyles ? (isUser ? 'bg-blue-600/40' : 'bg-slate-700/50') : (isUser ? 'bg-blue-400/30' : 'bg-gray-200/50')) 
      : '';

  const imageContainerStyle: React.CSSProperties = {};
  if ((isImageSuccessfullyDisplayed || isVideoSuccessfullyDisplayed) && message.id === transitioningImageId) {
      imageContainerStyle.viewTransitionName = `image-transition-${message.id}`;
      imageContainerStyle.contain = 'layout';
  }
  
  if ((isAnnotationActive || (isFocusedMode && isImageSuccessfullyDisplayed)) && imageAspectRatio) {
    imageContainerStyle.aspectRatio = `${imageAspectRatio}`;
  }
  
  return (
    <div className={`flex mb-4 ${bubbleAlignClass}`}>
      <div 
        className={bubbleWrapperClasses} 
        style={{
          touchAction: 'pan-y', 
          // @ts-ignore
          containerType: 'inline-size',
          width: '100%'
        }}
        ref={registerBubbleEl}
      >
          {(message.isGeneratingImage || isImageSuccessfullyDisplayed || isFileSuccessfullyDisplayed || isVideoSuccessfullyDisplayed || isAudioSuccessfullyDisplayed) && (
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
                  {message.isGeneratingImage && (
                      <div className="absolute top-2 right-2 flex flex-col items-end z-20">
                        {selectedLoadingGif ? (
                          <div className="w-36 h-36 rounded-full overflow-hidden bg-black/30 drop-shadow-md flex items-center justify-center">
                            <img
                              src={selectedLoadingGif}
                              alt={t('chat.imagePreview.alt')}
                              className="w-full h-full object-cover opacity-90"
                            />
                          </div>
                        ) : (
                          <svg className="animate-spin h-8 w-8 text-slate-100" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                          </svg>
                        )}
                        {remainingTimeDisplay && (
                          <p className={`mt-1 text-right text-xs px-1.5 py-0.5 rounded ${applyFocusedImageStyles ? 'text-slate-200 bg-slate-800/60' : 'text-gray-700 bg-gray-100/70'}`}>
                            {remainingTimeDisplay}
                          </p>
                        )}
                      </div>
                  )}

                  {(isImageSuccessfullyDisplayed || (isAnnotationActive && annotationSourceUrl)) && (
                    <>
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
                                style={{ opacity: isAnnotationActive ? 0.7 : 1 }}
                                onLoad={(e) => {
                                    const img = e.currentTarget;
                                    if (img.naturalWidth > 0 && img.naturalHeight > 0) {
                                      setImageAspectRatio(img.naturalWidth / img.naturalHeight);
                                  
                                      if (isAnnotationActive && annotationViewportRef.current) {
                                        const vw = annotationViewportRef.current.clientWidth;
                                        setScale(vw / img.naturalWidth);
                                        setPan({ x: 0, y: 0 });
                                      }
                                    }
                                  }}
                            />
                            {isAnnotationActive && ( <canvas ref={editCanvasRef} className="absolute top-0 left-0 w-full h-full cursor-crosshair" /> )}
                        </div>
      {!isAnnotationActive && isFocusedMode && isImageSuccessfullyDisplayed && (
                            <button
        onClick={() => handleStartAnnotation(displayUrl!)}
                                className="absolute top-2 right-2 z-20 p-2 bg-black/50 text-white rounded-full hover:bg-black/75 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-black/50 focus:ring-white transition-colors"
                                title={t('chat.annotateImage')}
                                aria-label={t('chat.annotateImage')}
                            >
                                <IconPencil className="w-5 h-5" />
                            </button>
                        )}
                        {isAnnotationActive && (
                          <>
                            <div
                              className="absolute top-2 right-2 z-30 pointer-events-auto"
                              onPointerDown={(e) => { e.stopPropagation(); e.preventDefault(); }}
                              onPointerUp={(e) => { e.stopPropagation(); e.preventDefault(); }}
                              onPointerCancel={(e) => { e.stopPropagation(); e.preventDefault(); }}
                              onClick={(e) => { e.stopPropagation(); e.preventDefault(); }}
                            >
                              <button
                                onClick={handleCancelAnnotation}
                                className="p-2 bg-black/60 text-white rounded-full hover:bg-black/80 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-black/50 focus:ring-white transition-colors"
                                title={t('chat.annotateModal.cancel')}
                                aria-label={t('chat.annotateModal.cancel')}
                              >
                                <IconXMark className="w-5 h-5" />
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
                                className="p-2 bg-black/60 text-white rounded-full hover:bg-black/80 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-black/50 focus:ring-white transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                                title={t('chat.annotateModal.undo')}
                                aria-label={t('chat.annotateModal.undo')}
                                aria-disabled={undoStack.length === 0}
                              >
                                <IconUndo className="w-5 h-5" />
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
                                className="p-2 bg-white text-black rounded-full hover:bg-gray-100 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-black/50 focus:ring-white transition-colors"
                                title={t('chat.annotateModal.saveAndAttach')}
                                aria-label={t('chat.annotateModal.saveAndAttach')}
                              >
                                <IconCheck className="w-5 h-5" />
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
                                if (!videoPlayTokenRef.current && onUiTaskStart) {
                                  const tok = genToken('video-play');
                                  const ret = onUiTaskStart(tok);
                                  videoPlayTokenRef.current = typeof ret === 'string' ? ret : tok;
                                }
                              }}
                              onPause={() => {
                                setIsVideoPlaying(false);
                                if (videoPlayTokenRef.current && onUiTaskEnd) { onUiTaskEnd(videoPlayTokenRef.current); videoPlayTokenRef.current = null; }
                              }}
                              onEnded={() => {
                                setIsVideoPlaying(false);
                                if (videoPlayTokenRef.current && onUiTaskEnd) { onUiTaskEnd(videoPlayTokenRef.current); videoPlayTokenRef.current = null; }
                              }}
                              className={`block w-full h-full object-contain rounded-lg bg-black`}
                          >
                              {t('chat.videoNotSupported')}
                          </video>
                          <button
                            onClick={handleAnnotateVideo}
                            disabled={isVideoPlaying}
                            className="absolute top-2 right-2 z-20 p-1.5 bg-black/50 text-white rounded-full hover:bg-black/75 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-black/50 focus:ring-white transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                            title={isVideoPlaying ? t('chat.error.pauseVideoToAnnotate') : t('chat.annotateVideoFrame')}
                            aria-label={isVideoPlaying ? t('chat.error.pauseVideoToAnnotate') : t('chat.annotateVideoFrame')}
                          >
                              <IconPencil className="w-4 h-4" />
                          </button>
                      </div>
                  )}

                  {(isImageSuccessfullyDisplayed || isVideoSuccessfullyDisplayed) && !isAnnotationActive && (
                      <div
                          ref={resizerRef}
                          onPointerDown={handleResizePointerDown}
                          className={`absolute bottom-0 right-0 cursor-se-resize p-2 touch-none z-30 opacity-100`}
                          title={isFocusedMode ? t('chat.image.dragToShrink') : t('chat.image.dragToEnlarge')}
                      >
                          <div style={{ filter: 'drop-shadow(0 1px 1px rgba(0,0,0,0.7))' }}>
                            <IconGripCorner className="w-4 h-4 text-white" />
                          </div>
                      </div>
                  )}
                  {isFileSuccessfullyDisplayed && (
                      <div className={`p-4 flex flex-col items-center justify-center text-center rounded-lg h-full ${isUser ? 'bg-blue-400' : 'bg-gray-200'}`}>
                          <IconPaperclip className={`w-10 h-10 ${isUser ? 'text-blue-100' : 'text-gray-500'}`} />
                          <p className={`mt-2 text-xs font-mono break-all ${isUser ? 'text-white' : 'text-gray-700'}`}>{message.imageMimeType}</p>
                          <p className={`mt-1 text-xs ${isUser ? 'text-blue-200' : 'text-gray-500'}`}>{t('chat.fileAttachment')}</p>
                      </div>
                  )}
          {isAudioSuccessfullyDisplayed && !isAnnotationActive && (
            <div className="relative w-full">
              <audio
                src={displayUrl!}
                controls
                className="w-full"
              >
                {t('chat.audioNotSupported')}
              </audio>
              <p className={`mt-2 text-xs font-mono break-all ${isUser ? 'text-white/80' : 'text-gray-700'}`}>{displayMime}</p>
            </div>
          )}
              </div>
          )}
          
          {message.imageGenError && !message.isGeneratingImage && (
               <div className={`flex flex-col items-center justify-center p-2 rounded-lg 
                  ${applyFocusedImageStyles ? 'absolute inset-0 bg-black/60 z-20' : `my-2 ${isUser ? 'bg-blue-400/60' : 'bg-gray-300/60'}`}
               `}>
                  <IconXMark className="w-8 h-8 text-red-300 mb-1"/>
                  <p className={`text-xs text-center ${applyFocusedImageStyles ? 'text-red-200' : 'text-red-700'}`}>
                      {t('chat.imageGenError')}: {message.imageGenError}
                  </p>
              </div>
          )}

        {hasTextContent && (
           <div className={`transition-opacity duration-300
                ${applyFocusedImageStyles 
                    ? `absolute inset-x-0 bottom-0 p-3 bg-gradient-to-t from-black/80 via-black/60 to-transparent text-white rounded-b-lg z-10`
                    : 'relative z-10 mt-1'
                }
                ${isAnnotationActive ? 'opacity-0 pointer-events-none' : 'opacity-100'}
            `}
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
                           isSpeakDisabled={isSpeaking || isSpeakDisabled}
                           speakText={speakText}
                           stopSpeaking={stopSpeaking}
               speakNativeLang={speakNativeLang}
               onToggleSpeakNativeLang={onToggleSpeakNativeLang}
               messageId={message.id}
                       />
                   </>
               ) : (
                 <>
                  {isUser && message.text && (
                    <p
                      className={`mb-1 whitespace-pre-wrap rounded-sm px-1 -mx-1 cursor-pointer transition-colors ${applyFocusedImageStyles ? 'text-white' : 'text-white'} ${isUserLineSpeaking ? 'bg-white/20 text-white' : 'hover:bg-white/10'}`}
                      style={{ fontSize: '3.8cqw', lineHeight: 1.35 }}
                      onPointerDown={handleLinePointerDown}
                      onPointerUp={handleUserMessagePointerUp}
                      onPointerLeave={handleLinePointerLeave}
                      role="button"
                      tabIndex={isSpeakDisabled ? -1 : 0}
                      onKeyDown={(e) => {
                        if ((e.key === 'Enter' || e.key === ' ') && !isSpeakDisabled) {
                          e.preventDefault();
                          handlePlayUserMessage(message);
                        }
                      }}
                      aria-label={isUserLineSpeaking ? (t('chat.stopSpeaking') || 'Stop playback') : (t('chat.speakThisLine') ? `${t('chat.speakThisLine')}: ${sanitizedUserText}` : 'Play message audio')}
                      aria-disabled={isSpeakDisabled}
                    >
                      {message.text}
                    </p>
                  )}
 
                   {isAssistant && message.translations && message.translations.length > 0 && message.translations.map((pair, index) => {
                     const isCurrentlySpeakingSpanish = pair.spanish && speakingUtteranceText === pair.spanish.replace(/\*/g, '');
                     const isCurrentlySpeakingEnglish = pair.english && speakingUtteranceText === pair.english.replace(/\*/g, '');
                     const isCurrentLineSpeaking = isCurrentlySpeakingSpanish || isCurrentlySpeakingEnglish;
 
                     return (
                     <div key={index} className={index > 0 && !applyFocusedImageStyles ? "mt-2" : ""}>
                       {pair.spanish && (
                         <p
                             className={`font-semibold whitespace-pre-wrap cursor-pointer transition-colors rounded-sm px-1 -mx-1 ${
                                 applyFocusedImageStyles
                                 ? (isCurrentLineSpeaking ? 'bg-sky-400 text-sky-900' : 'hover:text-sky-200 text-white')
                                 : (isCurrentLineSpeaking ? 'bg-sky-100 text-sky-800' : 'hover:text-blue-600 text-gray-800')
                             }`}
                             style={{ fontSize: '4cqw', lineHeight: 1.3 }}
                             onPointerDown={handleLinePointerDown}
                             onPointerUp={(e) => {
                               if (pointerDownPosRef.current) {
                                 const dx = Math.abs(e.clientX - pointerDownPosRef.current.x);
                                 const dy = Math.abs(e.clientY - pointerDownPosRef.current.y);
                                 if (dx < 10 && dy < 10) {
                                   e.preventDefault();
                                   if (isSpeakDisabled) { stopSpeaking(); pointerDownPosRef.current = null; return; }
                                   const startIdx = index;
                                   const parts: SpeechPart[] = [];
                                   const msgContext = { source: 'message' as const, messageId: message.id };
                                   for (let i = startIdx; i < (message.translations?.length || 0); i++) {
                                     const p = message.translations![i];
                                     const t = p.spanish?.trim();
                                     const n = p.english?.trim();
                                     if (t) parts.push({ text: t, langCode: currentTargetLangCode, context: msgContext });
                                     if (speakNativeLang && n) parts.push({ text: n, langCode: currentNativeLangCode, context: msgContext });
                                   }
                                   if (parts.length) speakText(parts, parts[0].langCode);
                                 }
                               }
                               pointerDownPosRef.current = null;
                             }}
                             onPointerLeave={handleLinePointerLeave}
                             onKeyDown={(e) => { if ((e.key === 'Enter' || e.key === ' ') && !isSpeakDisabled) { e.preventDefault(); handleSpeakLine(pair.spanish, currentTargetLangCode, pair.english, currentNativeLangCode, message.id); } }}
                             role="button"
                             tabIndex={isSpeakDisabled ? -1 : 0} 
                             aria-label={`${isSpeaking ? t('chat.stopSpeaking') : t('chat.speakThisLine')}: ${pair.spanish.replace(/\*/g, '')}`}
                             aria-disabled={isSpeakDisabled}
                         >
                             {pair.spanish}
                         </p>
                       )}
                       {pair.english && (
                        <p className={`italic mt-0.5 whitespace-pre-wrap pl-2 border-l-2 rounded-sm px-1 -mx-1 ${
                             applyFocusedImageStyles
                             ? (isCurrentLineSpeaking ? 'bg-slate-500 text-slate-100' : 'text-gray-200 border-gray-400')
                             : (isCurrentLineSpeaking ? 'bg-slate-200 text-slate-800' : 'text-gray-500 border-gray-300')
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
                          {pair.english}
                          {nativeFlashIndex === index && (
                            <span className="ml-1 inline-block align-middle animate-speak-flash">
                              {nativeFlashIsOn ? '🔊' : '🔇'}
                            </span>
                          )}
                         </p>
                       )}
                     </div>
                   )})}
                   {isAssistant && (!message.translations || message.translations.length === 0) && message.rawAssistantResponse && (
                     (() => {
                       const isCurrentlySpeakingRaw = message.rawAssistantResponse && speakingUtteranceText === message.rawAssistantResponse.replace(/\*/g, '');
                       return (
                         <p
                             className={`whitespace-pre-wrap cursor-pointer transition-colors rounded-sm px-1 -mx-1 ${
                                 applyFocusedImageStyles
                                 ? (isCurrentlySpeakingRaw ? 'bg-sky-400 text-sky-900' : 'hover:text-sky-200 text-white')
                                 : (isCurrentlySpeakingRaw ? 'bg-sky-100 text-sky-800' : 'hover:text-blue-600 text-gray-800')
                             }`} style={{ fontSize: '4cqw', lineHeight: 1.3 }}
                             onPointerDown={handleLinePointerDown}
                             onPointerUp={(e) => handleLinePointerUp(e, message.rawAssistantResponse!, currentTargetLangCode)}
                             onPointerLeave={handleLinePointerLeave}
                             onKeyDown={(e) => { if ((e.key === 'Enter' || e.key === ' ') && !isSpeakDisabled) { e.preventDefault(); handleSpeakLine(message.rawAssistantResponse!, currentTargetLangCode, undefined, undefined, message.id); } }}
                             role="button"
                             tabIndex={isSpeakDisabled ? -1 : 0} 
                             aria-label={`${isSpeaking ? t('chat.stopSpeaking') : t('chat.speakThisLine')}: ${message.rawAssistantResponse!.replace(/\*/g, '')}`}
                             aria-disabled={isSpeakDisabled}
                         >
                             {message.rawAssistantResponse}
                         </p>
                       );
                     })()
                   )}
                   {(isError || isStatus) && message.text && (
                     <p className={`${ applyFocusedImageStyles ? (isError ? 'text-red-200 font-semibold' : 'text-yellow-200 font-semibold') : ''}`}
                        style={{ fontSize: '3.2cqw', lineHeight: 1.25 }}>
                         {message.text}
                     </p>
                   )}
                   {isAssistant && !message.translations?.length && !message.rawAssistantResponse && !message.imageUrl && !message.isGeneratingImage && message.text && (
                     <p className={`whitespace-pre-wrap ${applyFocusedImageStyles ? 'text-white' : 'text-gray-800'}`} style={{ fontSize: '3.6cqw', lineHeight: 1.35 }}>{message.text}</p>
                   )}
               </>
             )}
           </div>
         )}
       </div>
     </div>
   );
 });
 ChatMessageBubble.displayName = "ChatMessageBubble";

 export default ChatMessageBubble;
