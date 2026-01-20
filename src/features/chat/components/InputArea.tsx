
import React, { useRef, useState, useEffect, useCallback, useMemo } from 'react';
import { TranslationReplacements } from '../../../core/i18n/index';
import { CameraDevice } from '../../../core/types';
import { LanguageDefinition } from '../../../core/config/languages';
import { LOCAL_STORAGE_SETTINGS_KEY, IMAGE_GEN_CAMERA_ID } from '../../../core/config/app';
import { LiveSessionState, SttLanguageSelector } from '../../speech';
import { 
  IconSend, IconPaperclip, IconMicrophone, IconXMark, IconCamera, 
  IconCameraFront, IconBookOpen, IconPencil, IconPlus, IconSparkles, 
  IconUndo, IconCheck, IconSave, IconFolderOpen, IconTrash, IconRobot, IconSpeaker
} from '../../../shared/ui/Icons';
import { SmallSpinner } from '../../../shared/ui/SmallSpinner';
import { LanguageSelectorGlobe, getGlobalProfileDB, setGlobalProfileDB } from '../../session';
import { getMaestroProfileImageDB, setMaestroProfileImageDB, clearMaestroProfileImageDB, MaestroProfileAsset } from '../../../core/db/assets';
import { uploadMediaToFiles, deleteFileByNameOrUri } from '../../../api/gemini';
import { DB_NAME } from '../../../core/db/index';
import { uniq as _uniq } from '../../../shared/utils/common';

interface InputAreaProps {
  t: (key: string, replacements?: TranslationReplacements) => string;
  isSending: boolean;
  isSpeaking: boolean;
  isListening: boolean;
  isSttGloballyEnabled: boolean;
  isSttSupported: boolean;
  transcript: string;
  sttLanguageCode: string;
  targetLanguageDef: LanguageDefinition;
  nativeLanguageDef: LanguageDefinition;
  onSttToggle: () => void;
  onSttLanguageChange: (code: string) => void;
  
  attachedImageBase64: string | null;
  attachedImageMimeType: string | null;
  onSetAttachedImage: (base64: string | null, mimeType: string | null) => void;
  
  onSendMessage: (text: string, imageBase64?: string, imageMimeType?: string) => Promise<boolean>;
  onUserInputActivity: () => void;
  
  liveVideoStream: MediaStream | null;
  liveSessionState: LiveSessionState;
  liveSessionError: string | null;
  onStartLiveSession: () => Promise<void> | void;
  onStopLiveSession: () => void;
  
  isSuggestionMode: boolean;
  onToggleSuggestionMode: (force?: boolean) => void;
  onCreateSuggestion: (text: string) => Promise<void>;
  isCreatingSuggestion: boolean;
  
  sendPrep: { active: boolean; label: string; done?: number; total?: number; etaMs?: number } | null;
  
  availableCameras: CameraDevice[];
  selectedCameraId: string | null;
  currentCameraFacingMode: 'user' | 'environment' | 'unknown';
  isImageGenCameraSelected: boolean;
  onSelectCamera: (deviceId: string) => void;
  onToggleSendWithSnapshot: () => void;
  onToggleUseVisualContextForReengagement: () => void;
  sendWithSnapshotEnabled: boolean;
  useVisualContextForReengagementEnabled: boolean;
  imageGenerationModeEnabled: boolean;
  onToggleImageGenerationMode: () => void;
  
  sttError: string | null;
  autoCaptureError: string | null;
  snapshotUserError: string | null;
  
  onUiTaskStart?: (token?: string) => string | void;
  onUiTaskEnd?: (token?: string) => void;

  // Language Selection
  isLanguageSelectionOpen?: boolean;
  tempNativeLangCode?: string | null;
  tempTargetLangCode?: string | null;
  onTempNativeSelect?: (code: string | null) => void;
  onTempTargetSelect?: (code: string | null) => void;
  onConfirmLanguageSelection?: () => void;
  onSaveAllChats?: (options?: { filename?: string; auto?: boolean }) => Promise<void>;
  onLoadAllChats?: (file: File) => Promise<void>;

  // Settings
  sttProvider: string;
  ttsProvider: string;
  onToggleSttProvider: () => void;
  onToggleTtsProvider: () => void;
  isSpeechRecognitionSupported: boolean;
}

const InputArea: React.FC<InputAreaProps> = ({
  t, isSending, isSpeaking, isListening, isSttGloballyEnabled, isSttSupported,
  transcript, sttLanguageCode, targetLanguageDef, nativeLanguageDef, onSttToggle, onSttLanguageChange,
  attachedImageBase64, attachedImageMimeType, onSetAttachedImage,
  onSendMessage, onUserInputActivity,
  liveVideoStream, liveSessionState, liveSessionError, onStartLiveSession, onStopLiveSession,
  isSuggestionMode, onToggleSuggestionMode, onCreateSuggestion, isCreatingSuggestion,
  sendPrep,
  availableCameras, selectedCameraId, currentCameraFacingMode, isImageGenCameraSelected, onSelectCamera,
  onToggleSendWithSnapshot, onToggleUseVisualContextForReengagement, sendWithSnapshotEnabled, useVisualContextForReengagementEnabled,
  imageGenerationModeEnabled, onToggleImageGenerationMode,
  sttError, autoCaptureError, snapshotUserError,
  onUiTaskStart, onUiTaskEnd,
  isLanguageSelectionOpen, tempNativeLangCode, tempTargetLangCode, onTempNativeSelect, onTempTargetSelect, onConfirmLanguageSelection, onSaveAllChats, onLoadAllChats,
  sttProvider, ttsProvider, onToggleSttProvider, onToggleTtsProvider, isSpeechRecognitionSupported
}) => {
  const [inputText, setInputText] = useState('');
  const [backgroundHint, setBackgroundHint] = useState('');
  const bubbleTextAreaRef = useRef<HTMLTextAreaElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const prevTranscriptRef = useRef('');
  
  const livePreviewVideoRef = useRef<HTMLVideoElement>(null);
  const attachedPreviewVideoRef = useRef<HTMLVideoElement>(null);
  const [attachedVideoPlaying, setAttachedVideoPlaying] = useState(false);
  const attachedVideoPlayTokenRef = useRef<string | null>(null);
  const paperclipOpenTokenRef = useRef<string | null>(null);
  
  // Recording states
  const [isRecording, setIsRecording] = useState(false);
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const recordedChunksRef = useRef<Blob[]>([]);
  const recordingTimerRef = useRef<number | null>(null);
  const videoRecordTokenRef = useRef<string | null>(null);
  const capturePressTimerRef = useRef<number | null>(null);
  
  const [isRecordingAudioNote, setIsRecordingAudioNote] = useState(false);
  const audioNoteRecorderRef = useRef<MediaRecorder | null>(null);
  const audioNoteChunksRef = useRef<BlobPart[]>([]);
  const audioNoteStreamRef = useRef<MediaStream | null>(null);
  const audioNoteTokenRef = useRef<string | null>(null);
  const micHoldTimerRef = useRef<number | null>(null);
  const micHoldActiveRef = useRef<boolean>(false);

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

  // --- Language Selection Actions State ---
  const [maestroAsset, setMaestroAsset] = useState<MaestroProfileAsset | null>(null);
  const [isUploadingMaestro, setIsUploadingMaestro] = useState(false);
  
  // Reset confirmation inside input area
  const [resetMode, setResetMode] = useState(false);
  const [resetConfirm, setResetConfirm] = useState<string>('');
  const [_isResetting, setIsResetting] = useState(false);

  // Profile editing inside input area
  const [isEditingProfile, setIsEditingProfile] = useState(false);
  const [profileText, setProfileText] = useState('');

  const saveTokenRef = useRef<string | null>(null);
  const loadTokenRef = useRef<string | null>(null);
  const maestroUploadTokenRef = useRef<string | null>(null);
  const maestroAvatarOpenTokenRef = useRef<string | null>(null);
  
  const loadFileInputRef = useRef<HTMLInputElement>(null);
  const maestroFileInputRef = useRef<HTMLInputElement>(null);

  const isCameraActive = sendWithSnapshotEnabled || useVisualContextForReengagementEnabled;
  const liveSessionActive = liveSessionState === 'active';
  const liveSessionConnecting = liveSessionState === 'connecting';
  const liveSessionErrored = liveSessionState === 'error';
  const showLiveFeed = liveVideoStream && (useVisualContextForReengagementEnabled || sendWithSnapshotEnabled) && !isImageGenCameraSelected && !isLanguageSelectionOpen;
  const isTwoUp = Boolean(attachedImageBase64 && showLiveFeed);

  const genUiToken = useCallback((tag: string) => `${tag}:${Date.now()}:${Math.random().toString(36).slice(2,8)}`, []);

  // Sync transcript to input
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

  // Auto-resize textarea
  useEffect(() => {
    if (bubbleTextAreaRef.current) {
      bubbleTextAreaRef.current.style.height = 'auto';
      bubbleTextAreaRef.current.style.height = `${bubbleTextAreaRef.current.scrollHeight}px`;
    }
  }, [inputText]);

  // Maestro Avatar Init
  useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        const a = await getMaestroProfileImageDB();
        if (mounted) setMaestroAsset(a);
      } catch {}
    })();
    return () => { mounted = false; };
  }, []);

  useEffect(() => {
    const handler = (e: any) => {
      try {
        const d = e?.detail || {};
        if (d && (typeof d.dataUrl === 'string' || typeof d.uri === 'string')) {
          setMaestroAsset({
            dataUrl: typeof d.dataUrl === 'string' ? d.dataUrl : maestroAsset?.dataUrl,
            mimeType: typeof d.mimeType === 'string' ? d.mimeType : maestroAsset?.mimeType,
            uri: typeof d.uri === 'string' ? d.uri : maestroAsset?.uri,
            updatedAt: Date.now(),
          });
        } else {
          getMaestroProfileImageDB().then(a => setMaestroAsset(a)).catch(() => {});
        }
      } catch { /* ignore */ }
    };
    window.addEventListener('maestro-avatar-updated', handler as any);
    return () => window.removeEventListener('maestro-avatar-updated', handler as any);
  }, [maestroAsset]);

  const handleInputChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const newText = e.target.value;
    setInputText(newText);
    if (newText.replace(/\[[^\]]*\]/g, ' ').trim().length >= 2) {
      onUserInputActivity();
    }
  };

  const handleSend = async () => {
    if (isLanguageSelectionOpen && onConfirmLanguageSelection) {
        onConfirmLanguageSelection();
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

  const startProfileEdit = async () => {
    try {
      const current = (await getGlobalProfileDB())?.text ?? '';
      setProfileText(current);
      setIsEditingProfile(true);
    } catch {
      setProfileText('');
      setIsEditingProfile(true);
    }
  };

  const handleProfileSave = async () => {
    try {
      await setGlobalProfileDB(profileText.trim());
      try { window.dispatchEvent(new CustomEvent('globalProfileUpdated')); } catch {}
    } finally {
      setIsEditingProfile(false);
    }
  };

  // --- Composer Annotation Handlers ---

  const startComposerAnnotationFromImage = useCallback((dataUrl: string) => {
    if (!composerAnnotateTokenRef.current && onUiTaskStart) {
      const tok = genUiToken('composer-annotate');
      const ret = onUiTaskStart(tok);
      composerAnnotateTokenRef.current = typeof ret === 'string' ? ret : tok;
    }
    setComposerAnnotationSourceUrl(dataUrl);
    
    // Defer scale calculation until image renders
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
  }, [genUiToken, onUiTaskStart]);

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
      composerIsDrawingRef.current = false;
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
      if (newStack.length > 0) ctx.putImageData(newStack[newStack.length - 1], 0, 0);
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
    if (composerAnnotateTokenRef.current && onUiTaskEnd) { onUiTaskEnd(composerAnnotateTokenRef.current); composerAnnotateTokenRef.current = null; }
  }, [onUiTaskEnd]);

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

  // Setup canvas for composer
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
        ctx.strokeStyle = '#EF4444'; // red-500
        ctx.lineWidth = Math.max(5, img.naturalWidth * 0.01);
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
      }
    };
    if (img.complete && img.naturalWidth > 0) setup();
    else img.addEventListener('load', setup);
    return () => img.removeEventListener('load', setup);
  }, [isComposerAnnotating, composerAnnotationSourceUrl]);


  // Video recording logic
  const pickRecorderMimeType = () => {
    const candidates = ['video/webm;codecs=vp9,opus', 'video/webm;codecs=vp8,opus', 'video/webm'];
    for (const m of candidates) if ((window as any).MediaRecorder?.isTypeSupported?.(m)) return m;
    return '';
  };

  const handleStopRecording = useCallback(() => {
    const rec = mediaRecorderRef.current;
    if (rec && rec.state === 'recording') {
      try { rec.requestData(); } catch {}
      rec.stop();
    }
    if (recordingTimerRef.current) {
      clearTimeout(recordingTimerRef.current);
      recordingTimerRef.current = null;
    }
    setIsRecording(false);
    if (videoRecordTokenRef.current && onUiTaskEnd) { onUiTaskEnd(videoRecordTokenRef.current); videoRecordTokenRef.current = null; }
  }, [onUiTaskEnd]);

  const handleStartRecording = useCallback(() => {
    if (isRecording || !liveVideoStream) return;
    try {
      if (!videoRecordTokenRef.current && onUiTaskStart) {
        const tok = genUiToken('video-record');
        const ret = onUiTaskStart(tok);
        videoRecordTokenRef.current = typeof ret === 'string' ? ret : tok;
      }
      const mimeType = pickRecorderMimeType();
      const options: MediaRecorderOptions = mimeType ? { mimeType } : {};
      const rec = new MediaRecorder(liveVideoStream, options);
      mediaRecorderRef.current = rec;
      recordedChunksRef.current = [];
      rec.ondataavailable = (event) => { if (event.data && event.data.size > 0) recordedChunksRef.current.push(event.data); };
      rec.onstop = () => {
        const chosenType = rec.mimeType || mimeType || 'video/webm';
        const chunks = recordedChunksRef.current;
        recordedChunksRef.current = [];
        if (!chunks.length) return;
        const videoBlob = new Blob(chunks, { type: chosenType });
        const reader = new FileReader();
        reader.onloadend = () => {
          onSetAttachedImage(reader.result as string, chosenType);
          onUserInputActivity();
        };
        reader.readAsDataURL(videoBlob);
        if (videoRecordTokenRef.current && onUiTaskEnd) { onUiTaskEnd(videoRecordTokenRef.current); videoRecordTokenRef.current = null; }
      };
      rec.start(1000);
      setIsRecording(true);
      recordingTimerRef.current = window.setTimeout(() => {
        if (mediaRecorderRef.current?.state === 'recording') {
          handleStopRecording();
          alert(t('chat.error.recordingTimeExceeded', { maxMinutes: 15 }));
        }
      }, 15 * 60 * 1000);
    } catch (e) {
      console.error('Failed to start media recorder:', e);
      if (videoRecordTokenRef.current && onUiTaskEnd) { onUiTaskEnd(videoRecordTokenRef.current); videoRecordTokenRef.current = null; }
    }
  }, [isRecording, liveVideoStream, onSetAttachedImage, onUserInputActivity, t, handleStopRecording, onUiTaskStart, onUiTaskEnd, genUiToken]);

  const handleCaptureImage = useCallback(() => {
     if (!livePreviewVideoRef.current || !liveVideoStream || !livePreviewVideoRef.current.srcObject) return;
     if (livePreviewVideoRef.current.videoWidth === 0 || livePreviewVideoRef.current.videoHeight === 0) return;
     const videoElement = livePreviewVideoRef.current;
     const canvas = document.createElement('canvas');
     canvas.width = videoElement.videoWidth;
     canvas.height = videoElement.videoHeight;
     const context = canvas.getContext('2d');
     if (context) {
       context.drawImage(videoElement, 0, 0, canvas.width, canvas.height);
       const dataUrl = canvas.toDataURL('image/jpeg', 0.9);
       onSetAttachedImage(dataUrl, 'image/jpeg');
       onUserInputActivity();
     }
   }, [liveVideoStream, onSetAttachedImage, onUserInputActivity]);

   const handleCaptureButtonPointerDown = () => {
       if (capturePressTimerRef.current) clearTimeout(capturePressTimerRef.current);
       capturePressTimerRef.current = window.setTimeout(() => {
           handleStartRecording();
           capturePressTimerRef.current = null;
       }, 500);
   };
   const handleCaptureButtonPointerUp = () => {
       if (capturePressTimerRef.current) {
           clearTimeout(capturePressTimerRef.current);
           capturePressTimerRef.current = null;
           handleCaptureImage();
       }
   };
   const handleCaptureButtonPointerLeave = () => {
       if (capturePressTimerRef.current) {
           clearTimeout(capturePressTimerRef.current);
           capturePressTimerRef.current = null;
       }
   };

  // Audio Note Logic
  const pickAudioMimeType = () => {
    const candidates = ['audio/webm;codecs=opus', 'audio/ogg;codecs=opus', 'audio/mp4', 'audio/webm'];
    for (const m of candidates) if ((window as any).MediaRecorder?.isTypeSupported?.(m)) return m;
    return '';
  };
  const startAudioNoteRecording = useCallback(async () => {
    if (isRecordingAudioNote || isSttGloballyEnabled) return;
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      audioNoteStreamRef.current = stream;
      const mimeType = pickAudioMimeType();
      const options: MediaRecorderOptions = mimeType ? { mimeType } : {};
      const rec = new MediaRecorder(stream, options);
      audioNoteRecorderRef.current = rec;
      audioNoteChunksRef.current = [];
      rec.ondataavailable = (e) => { if (e.data && e.data.size) audioNoteChunksRef.current.push(e.data); };
      rec.onstop = () => {
        const chosenType = rec.mimeType || mimeType || 'audio/webm';
        const chunks = audioNoteChunksRef.current;
        audioNoteChunksRef.current = [];
        if (audioNoteStreamRef.current) { try { audioNoteStreamRef.current.getTracks().forEach(t => t.stop()); } catch {} audioNoteStreamRef.current = null; }
        if (audioNoteTokenRef.current && onUiTaskEnd) { onUiTaskEnd(audioNoteTokenRef.current); audioNoteTokenRef.current = null; }
        if (!chunks.length) return;
        const blob = new Blob(chunks, { type: chosenType });
        const reader = new FileReader();
        reader.onloadend = () => { onSetAttachedImage(reader.result as string, chosenType); onUserInputActivity(); };
        reader.readAsDataURL(blob);
      };
      rec.onerror = () => {
        try { audioNoteStreamRef.current?.getTracks()?.forEach(t => t.stop()); } catch {}
        audioNoteStreamRef.current = null;
        setIsRecordingAudioNote(false);
        if (audioNoteTokenRef.current && onUiTaskEnd) { onUiTaskEnd(audioNoteTokenRef.current); audioNoteTokenRef.current = null; }
      };
      if (!audioNoteTokenRef.current && onUiTaskStart) {
        const tok = genUiToken('audio-note');
        const ret = onUiTaskStart(tok);
        audioNoteTokenRef.current = typeof ret === 'string' ? ret : tok;
      }
      rec.start(250); 
      setIsRecordingAudioNote(true);
    } catch (e) {
      console.error('Failed to start audio note recording:', e);
    }
  }, [isRecordingAudioNote, isSttGloballyEnabled, onSetAttachedImage, onUserInputActivity, onUiTaskStart, onUiTaskEnd, genUiToken]);

  const stopAudioNoteRecording = useCallback(() => {
    const rec = audioNoteRecorderRef.current;
    if (rec && rec.state === 'recording') { try { rec.requestData(); } catch {} rec.stop(); }
    setIsRecordingAudioNote(false);
    if (audioNoteTokenRef.current && onUiTaskEnd) { onUiTaskEnd(audioNoteTokenRef.current); audioNoteTokenRef.current = null; }
  }, [onUiTaskEnd]);

  const handleMicPointerDown = useCallback((e: React.PointerEvent) => {
    if (isSttGloballyEnabled || isSending || isSpeaking) return; 
    e.preventDefault(); e.stopPropagation();
    micHoldActiveRef.current = false;
    if (micHoldTimerRef.current) { clearTimeout(micHoldTimerRef.current); micHoldTimerRef.current = null; }
    micHoldTimerRef.current = window.setTimeout(async () => {
      micHoldTimerRef.current = null;
      try {
        if ('permissions' in navigator && (navigator as any).permissions?.query) {
          const status = await (navigator as any).permissions.query({ name: 'microphone' as PermissionName });
          if ((status as any).state !== 'granted') { micHoldActiveRef.current = false; return; }
        } else { micHoldActiveRef.current = false; return; }
      } catch { micHoldActiveRef.current = false; return; }
      micHoldActiveRef.current = true;
      await startAudioNoteRecording();
    }, 450);
  }, [isSttGloballyEnabled, isSending, isSpeaking, startAudioNoteRecording]);

  const handleMicPointerUp = useCallback((e: React.PointerEvent) => {
    if (micHoldTimerRef.current) { clearTimeout(micHoldTimerRef.current); micHoldTimerRef.current = null; }
    if (!isSttGloballyEnabled && micHoldActiveRef.current) {
      stopAudioNoteRecording();
      e.preventDefault(); e.stopPropagation();
      window.setTimeout(() => { micHoldActiveRef.current = false; }, 200);
    }
  }, [isSttGloballyEnabled, stopAudioNoteRecording]);

  const handleMicPointerCancel = useCallback(() => {
    if (micHoldTimerRef.current) { clearTimeout(micHoldTimerRef.current); micHoldTimerRef.current = null; }
    if (!isSttGloballyEnabled && micHoldActiveRef.current) { stopAudioNoteRecording(); micHoldActiveRef.current = false; }
  }, [isSttGloballyEnabled, stopAudioNoteRecording]);

  const handleMicClick = useCallback((e: React.MouseEvent) => {
    if (micHoldActiveRef.current) { micHoldActiveRef.current = false; e.preventDefault(); e.stopPropagation(); return; }
    if (micHoldTimerRef.current) { clearTimeout(micHoldTimerRef.current); micHoldTimerRef.current = null; }
    onSttToggle();
  }, [onSttToggle]);

  useEffect(() => () => { if (micHoldTimerRef.current) clearTimeout(micHoldTimerRef.current); }, []);

  // Camera Logic
  const allCameraOptions = useMemo(() => {
       const cameraOptions: CameraDevice[] = [...availableCameras];
       if (imageGenerationModeEnabled) {
           cameraOptions.push({ deviceId: IMAGE_GEN_CAMERA_ID, label: t('chat.camera.imageGenCameraLabel'), facingMode: 'unknown' });
       }
       return cameraOptions;
   }, [availableCameras, imageGenerationModeEnabled, t]);

   const handleCameraActivationClick = () => { onToggleSendWithSnapshot(); onToggleUseVisualContextForReengagement(); };
   const handleCameraDeactivationClick = () => { if (sendWithSnapshotEnabled) onToggleSendWithSnapshot(); if (useVisualContextForReengagementEnabled) onToggleUseVisualContextForReengagement(); };

   // File Input
   const handleImageAttach = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (paperclipOpenTokenRef.current && onUiTaskEnd) { onUiTaskEnd(paperclipOpenTokenRef.current); paperclipOpenTokenRef.current = null; }
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
         video.onerror = () => { window.URL.revokeObjectURL(video.src); alert(t('chat.error.videoMetadataError')); if (fileInputRef.current) fileInputRef.current.value = ''; };
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
     const handleWindowFocus = () => { if (paperclipOpenTokenRef.current && onUiTaskEnd) { onUiTaskEnd(paperclipOpenTokenRef.current); paperclipOpenTokenRef.current = null; } };
     window.addEventListener('focus', handleWindowFocus);
     return () => window.removeEventListener('focus', handleWindowFocus);
   }, [onUiTaskEnd]);

   // Live preview ref sync
   useEffect(() => {
     if (livePreviewVideoRef.current && liveVideoStream) {
         if (livePreviewVideoRef.current.srcObject !== liveVideoStream) {
             livePreviewVideoRef.current.srcObject = liveVideoStream;
             livePreviewVideoRef.current.play().catch(e => console.error("Error playing live preview:", e));
         } else if (livePreviewVideoRef.current.paused) {
             livePreviewVideoRef.current.play().catch(e => console.error("Error playing live preview:", e));
         }
     } else if (livePreviewVideoRef.current && !liveVideoStream) {
         livePreviewVideoRef.current.srcObject = null;
     }
   }, [liveVideoStream, attachedImageBase64]);

   const prepDisplay = useMemo(() => {
    if (!sendPrep || !sendPrep.active) return null;
    const parts: string[] = [];
    parts.push(sendPrep.label || 'Preparing…');
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
    if (isLanguageSelectionOpen) return "";
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

  const getMicButtonTitle = () => {
    if (isRecordingAudioNote) { try { const k = t('chat.mic.recordingAudioNote'); if (k !== 'chat.mic.recordingAudioNote') return k; } catch {} return 'Recording audio… release to attach'; }
    return isListening ? t("chat.mic.listening") : (isSttGloballyEnabled ? t("chat.mic.disableStt") : t("chat.mic.enableStt"));
  }

  const liveSessionButtonLabel = liveSessionActive ? t('chat.liveSession.stop') : (liveSessionErrored ? t('chat.liveSession.retry') : t('chat.liveSession.start'));
  const liveSessionButtonClasses = liveSessionActive ? 'bg-red-600/80 hover:bg-red-500 text-white' : (liveSessionErrored ? 'bg-yellow-500/80 hover:bg-yellow-500 text-slate-900' : (isSuggestionMode ? 'bg-gray-700/80 hover:bg-gray-800 text-white' : 'bg-black/60 hover:bg-black/80 text-white'));
  
  const containerClass = isSuggestionMode 
    ? 'bg-white text-gray-800 shadow-sm ring-1 ring-gray-300 focus-within:ring-2 focus-within:ring-gray-400' 
    : 'bg-blue-400 text-white shadow-sm ring-1 ring-blue-300 focus-within:ring-2 focus-within:ring-white/80';

  const sendButtonStyle = isSuggestionMode ? 'bg-gray-700 text-white hover:bg-gray-600 focus:ring-gray-400' : 'bg-white text-blue-600 hover:bg-blue-100 focus:ring-blue-200';
  const iconButtonStyle = isSuggestionMode ? 'text-gray-500 hover:text-gray-900 hover:bg-gray-100' : 'text-blue-100 hover:text-white hover:bg-white/20';

  const handleLiveSessionToggle = () => {
    if (liveSessionActive) onStopLiveSession();
    else { try { onStartLiveSession(); } catch {} }
  };

  // --- Handlers for Language Selection Popups ---
  const wipeLocalMemoryAndDb = useCallback(async () => {
    try {
        await new Promise<void>((resolve) => {
            let settled = false;
            try {
                const req = indexedDB.deleteDatabase(DB_NAME);
                req.onsuccess = () => { settled = true; resolve(); };
                req.onerror = () => { resolve(); };
                req.onblocked = () => { resolve(); };
            } catch { resolve(); }
            setTimeout(() => { if (!settled) resolve(); }, 1500);
        });
    } catch {}
    try {
        const keys: string[] = [];
        for (let i = 0; i < window.localStorage.length; i++) {
            const k = window.localStorage.key(i);
            if (k) keys.push(k);
        }
        keys.forEach(k => {
            if (k.startsWith('chatBackup:') || k === LOCAL_STORAGE_SETTINGS_KEY) {
                try { window.localStorage.removeItem(k); } catch {}
            }
        });
    } catch {}
  }, []);

  const handleMaestroAvatarClick = () => {
    try {
      if (!maestroAvatarOpenTokenRef.current && onUiTaskStart) {
        const tok = genUiToken('maestro-avatar-open');
        const ret = onUiTaskStart(tok);
        maestroAvatarOpenTokenRef.current = typeof ret === 'string' ? ret : tok;
      }
    } catch {}
    maestroFileInputRef.current?.click();
  };

  const handleClearMaestroAvatar = async (e?: React.MouseEvent) => {
    e?.preventDefault?.();
    e?.stopPropagation?.();
    try { 
      setIsUploadingMaestro(true); 
      if (!maestroUploadTokenRef.current && onUiTaskStart) {
        const tok = genUiToken('maestro-avatar');
        const ret = onUiTaskStart(tok);
        maestroUploadTokenRef.current = typeof ret === 'string' ? ret : tok;
      }
    } catch {}
    try {
      const prevUri = maestroAsset?.uri;
      if (prevUri) {
        await deleteFileByNameOrUri(prevUri);
      }
    } catch { }
    try { await clearMaestroProfileImageDB(); } catch { }
    
    // Attempt default avatar
    try {
      const man = await fetch('/maestro-avatars/manifest.json', { cache: 'force-cache' });
      let defaultFound = false;
      if (man.ok) {
        const list: string[] = await man.json();
        if (Array.isArray(list)) {
          for (const name of list) {
            try {
              const r = await fetch(`/maestro-avatars/${name}`, { cache: 'force-cache' });
              if (r.ok) {
                const blob = await r.blob();
                const mime = blob.type || 'image/png';
                const dataUrl: string = await new Promise((resolve, reject) => {
                  const fr = new FileReader();
                  fr.onloadend = () => resolve(fr.result as string);
                  fr.onerror = () => reject(fr.error || new Error('DataURL conversion failed'));
                  fr.readAsDataURL(blob);
                });
                let uploadedUri: string | undefined;
                let uploadedMimeType: string = mime; // Default to detected mime, but use normalized from upload if available
                try {
                  const up = await uploadMediaToFiles(dataUrl, mime, 'maestro-avatar');
                  uploadedUri = up.uri;
                  uploadedMimeType = up.mimeType; // Use the normalized MIME type from the upload
                } catch { }
                const asset: MaestroProfileAsset = { dataUrl, mimeType: uploadedMimeType, uri: uploadedUri, updatedAt: Date.now() };
                try { await setMaestroProfileImageDB(asset); } catch {}
                setMaestroAsset(asset);
                try { window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: asset })); } catch {}
                defaultFound = true;
                break;
              }
            } catch { }
          }
        }
      }
      if (!defaultFound) {
        setMaestroAsset(null);
        try { window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: {} })); } catch {}
      }
    } catch {
      setMaestroAsset(null);
    } finally {
      try { setIsUploadingMaestro(false); } catch {}
      if (maestroUploadTokenRef.current && onUiTaskEnd) { onUiTaskEnd(maestroUploadTokenRef.current); maestroUploadTokenRef.current = null; }
    }
  };

  const handleMaestroFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    if (maestroAvatarOpenTokenRef.current && onUiTaskEnd) { onUiTaskEnd(maestroAvatarOpenTokenRef.current); maestroAvatarOpenTokenRef.current = null; }
    const file = event.target.files?.[0];
    if (!file) { event.target.value = ''; return; }
    if (!file.type.startsWith('image/')) { event.target.value = ''; return; }
    try {
      setIsUploadingMaestro(true);
      if (!maestroUploadTokenRef.current && onUiTaskStart) {
        const tok = genUiToken('maestro-avatar');
        const ret = onUiTaskStart(tok);
        maestroUploadTokenRef.current = typeof ret === 'string' ? ret : tok;
      }
      const dataUrl: string = await new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result));
        reader.onerror = (e) => reject(e);
        reader.readAsDataURL(file);
      });
      let uploadedUri: string | undefined;
      let uploadedMimeType: string = file.type; // Default to file.type, but use normalized from upload if available
      try {
        const up = await uploadMediaToFiles(dataUrl, file.type, 'maestro-avatar');
        uploadedUri = up.uri;
        uploadedMimeType = up.mimeType; // Use the normalized MIME type from the upload
      } catch {}
      const asset: MaestroProfileAsset = { dataUrl, mimeType: uploadedMimeType, uri: uploadedUri, updatedAt: Date.now() };
      await setMaestroProfileImageDB(asset);
      setMaestroAsset(asset);
      try {
        window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: { uri: uploadedUri, mimeType: uploadedMimeType, dataUrl } }));
      } catch {}
    } catch {
    } finally {
      setIsUploadingMaestro(false);
      event.target.value = '';
      if (maestroUploadTokenRef.current && onUiTaskEnd) { onUiTaskEnd(maestroUploadTokenRef.current); maestroUploadTokenRef.current = null; }
    }
  };

  const handleLoadFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file && onLoadAllChats) {
        try {
          if (!loadTokenRef.current && onUiTaskStart) {
             const tok = genUiToken('load-chats');
             const ret = onUiTaskStart(tok);
             loadTokenRef.current = typeof ret === 'string' ? ret : tok;
          }
          await onLoadAllChats(file);
        } finally {
          if (loadTokenRef.current && onUiTaskEnd) { onUiTaskEnd(loadTokenRef.current); loadTokenRef.current = null; }
        }
    }
    event.target.value = '';
  };

  const handleSave = async () => {
    if (onSaveAllChats) {
        if (!saveTokenRef.current && onUiTaskStart) {
            const tok = genUiToken('save-chats');
            const ret = onUiTaskStart(tok);
            saveTokenRef.current = typeof ret === 'string' ? ret : tok;
        }
        try {
            await onSaveAllChats();
        } finally {
            if (saveTokenRef.current && onUiTaskEnd) { onUiTaskEnd(saveTokenRef.current); saveTokenRef.current = null; }
        }
    }
  };

  const handleResetConfirm = async () => {
    if (resetConfirm !== 'DELETE') return;
    try {
        setIsResetting(true);
        // Backup first
        const safe = `backup-before-reset-${new Date().toISOString().slice(0,10)}`;
        if (onSaveAllChats) await onSaveAllChats({ filename: `${safe}.json`, auto: true });
        
        await new Promise(r => setTimeout(r, 500));
        await wipeLocalMemoryAndDb();
        window.location.reload();
    } catch(e) {
        setIsResetting(false);
    }
  };

  return (
    <>
      {isComposerAnnotating ? (
        <div className="w-full">
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
                style={{ opacity: 0.7 }}
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
      ) : (
      <>
      {/* Previews */}
      {isLanguageSelectionOpen && onTempNativeSelect && onTempTargetSelect && onConfirmLanguageSelection ? (
          <LanguageSelectorGlobe 
            nativeLangCode={tempNativeLangCode || null}
            targetLangCode={tempTargetLangCode || null}
            onSelectNative={onTempNativeSelect}
            onSelectTarget={onTempTargetSelect}
            onConfirm={onConfirmLanguageSelection}
            t={t}
            onInteract={onUserInputActivity}
          />
      ) : (
        (attachedImageBase64 || showLiveFeed) && (
         <div className="flex flex-wrap justify-center items-start gap-2 mb-2 order-first w-full">
           {attachedImageBase64 && (
             <div className={`relative ${isTwoUp ? 'w-[calc(50%-0.25rem)] sm:w-48' : 'w-48'} min-w-0 ${isSuggestionMode ? 'bg-gray-300' : 'bg-blue-400'} p-1 rounded-md`}>
               {attachedImageMimeType?.startsWith('image/') ? (
                 <div className="relative">
                   <img src={attachedImageBase64} alt={t('chat.imagePreview.alt')} className="h-24 w-full object-cover rounded" />
                   {/* Annotate image button */}
                   <button
                     onClick={handleComposerAnnotateImage}
                     className="absolute top-1.5 right-1.5 p-1.5 bg-black/60 text-white rounded-full hover:bg-black"
                     title={t('chat.annotateImage')}
                   >
                     <IconPencil className="w-4 h-4" />
                   </button>
                 </div>
               ) : attachedImageMimeType?.startsWith('video/') ? (
                 <div className="relative">
                   <video
                     ref={attachedPreviewVideoRef}
                     src={attachedImageBase64}
                     controls
                     className="h-24 w-full object-contain rounded bg-black"
                    onPlay={() => { setAttachedVideoPlaying(true); if (!attachedVideoPlayTokenRef.current && onUiTaskStart) { const tok = genUiToken('video-play'); const ret = onUiTaskStart(tok); attachedVideoPlayTokenRef.current = typeof ret === 'string' ? ret : tok; } }}
                    onPause={() => { setAttachedVideoPlaying(false); if (attachedVideoPlayTokenRef.current && onUiTaskEnd) { onUiTaskEnd(attachedVideoPlayTokenRef.current); attachedVideoPlayTokenRef.current = null; } }}
                    onEnded={() => { setAttachedVideoPlaying(false); if (attachedVideoPlayTokenRef.current && onUiTaskEnd) { onUiTaskEnd(attachedVideoPlayTokenRef.current); attachedVideoPlayTokenRef.current = null; } }}
                   />
                   {/* Annotate frame button */}
                   <button
                     onClick={handleComposerAnnotateVideo}
                     disabled={attachedVideoPlaying}
                     className="absolute top-1.5 right-1.5 p-1.5 bg-black/60 text-white rounded-full hover:bg-black disabled:opacity-50"
                     title={attachedVideoPlaying ? t('chat.error.pauseVideoToAnnotate') : t('chat.annotateVideoFrame')}
                   >
                     <IconPencil className="w-4 h-4" />
                   </button>
                 </div>
               ) : attachedImageMimeType?.startsWith('audio/') ? (
                 <div className="relative">
                   <audio src={attachedImageBase64} controls className="h-24 w-full object-contain rounded bg-black/5" />
                   <span className={`text-xs mt-1 truncate max-w-full px-1 block ${isSuggestionMode ? 'text-gray-600' : 'text-white'}`}>{attachedImageMimeType}</span>
                 </div>
               ) : (
                 <div className={`h-24 w-full flex flex-col items-center justify-center ${isSuggestionMode ? 'bg-gray-100' : 'bg-blue-300'} rounded`}>
                   <IconPaperclip className={`w-8 h-8 ${isSuggestionMode ? 'text-gray-500' : 'text-blue-100'}`} />
                   <span className={`text-xs mt-1 truncate max-w-full px-1 ${isSuggestionMode ? 'text-gray-600' : 'text-white'}`}>{attachedImageMimeType}</span>
                 </div>
               )}
               <div className="absolute -top-2 -right-2 flex items-center space-x-1">
                 <button onClick={removeAttachedImage} className="p-1 bg-red-500/80 text-white rounded-full hover:bg-red-500" aria-label={t('chat.removeAttachedImage')}>
                   <IconXMark className="w-4 h-4" />
                 </button>
               </div>
             </div>
           )}

          {showLiveFeed && (
            <div className={`relative ${isTwoUp ? 'w-[calc(50%-0.25rem)] sm:w-48' : 'w-48'} min-w-0 ${isSuggestionMode ? 'bg-gray-300' : 'bg-blue-400'} p-1 rounded-md`}>
              <div className="relative group">
                <video
                  ref={livePreviewVideoRef}
                  autoPlay
                  playsInline
                  muted
                  className="h-24 w-full object-cover rounded pointer-events-none"
                />
                <div className="absolute top-1 right-1 flex items-center gap-2 z-30">
                  {liveSessionConnecting && <SmallSpinner className="w-5 h-5 text-white drop-shadow" />}
                  <button
                    type="button"
                    onClick={handleLiveSessionToggle}
                    disabled={liveSessionConnecting}
                    className={`px-2 py-1 text-xs font-semibold rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-white/50 ${liveSessionButtonClasses} ${liveSessionConnecting ? 'opacity-70 cursor-wait' : ''}`}
                  >
                    {liveSessionButtonLabel}
                  </button>
                </div>
                <div className="absolute inset-0 flex items-center justify-center bg-black/20 group-hover:bg-black/40 transition-colors">
                  {liveSessionActive ? (
                    <div className="flex items-center gap-2 px-3 py-1 rounded-full bg-red-600/80 text-white uppercase text-xs font-semibold tracking-wide">
                      <span className="inline-flex h-2 w-2 rounded-full bg-white animate-pulse" aria-hidden />
                      {t('chat.liveSession.liveBadge') || 'Live'}
                    </div>
                  ) : liveSessionConnecting ? (
                    <div className="flex items-center gap-2 px-3 py-1 rounded-full bg-black/60 text-white text-xs">
                      <SmallSpinner className="w-4 h-4 text-white" />
                      <span>{t('chat.liveSession.connecting') || 'Connecting'}</span>
                    </div>
                  ) : isRecording ? (
                    <button
                      onClick={handleStopRecording}
                      className="p-2 rounded-full bg-red-500/80 text-white group-hover:bg-red-500 transition-colors"
                      aria-label={t('chat.camera.stopRecording')}
                    >
                      <div className="w-4 h-4 bg-white rounded-sm" />
                    </button>
                  ) : (
                    <button
                      onPointerDown={handleCaptureButtonPointerDown}
                      onPointerUp={handleCaptureButtonPointerUp}
                      onPointerLeave={handleCaptureButtonPointerLeave}
                      onContextMenu={(e) => e.preventDefault()}
                      className="p-2 rounded-full bg-white/30 text-white group-hover:bg-white/50 transition-colors"
                      aria-label={t('chat.camera.captureOrRecord')}
                    >
                      <IconCamera className="w-6 h-6" />
                    </button>
                  )}
                </div>
                {isRecording && !liveSessionActive && !liveSessionConnecting && (
                  <div className="absolute top-1 left-1 flex items-center space-x-1 p-1 bg-black/50 rounded-lg z-20">
                    <div className="w-2 h-2 bg-red-500 rounded-full animate-pulse" />
                    <span className="text-white text-xs font-mono">REC</span>
                  </div>
                )}
              </div>
              {liveSessionError && (
                <div className={`mt-1 px-2 py-1 text-xs rounded ${isSuggestionMode ? 'bg-red-100 text-red-700' : 'bg-red-600/20 text-red-100'}`}>
                  {liveSessionError}
                </div>
              )}
            </div>
          )}
         </div>
       ))}

      <div className={`relative w-full flex flex-col rounded-3xl overflow-hidden transition-colors ${containerClass}`}>
        {isLanguageSelectionOpen ? (
          <div className="w-full py-3 px-4 min-h-[50px] flex items-center justify-between gap-3">
            {resetMode ? (
               <>
                 <div className="flex items-center gap-2 flex-1">
                   <span className="text-xs font-semibold text-white uppercase whitespace-nowrap">Reset:</span>
                   <input 
                      className="flex-1 min-w-0 bg-white/20 border border-white/30 rounded px-2 py-1 text-sm text-white placeholder-white/50 focus:outline-none focus:border-white"
                      placeholder="Type DELETE"
                      value={resetConfirm}
                      onChange={(e) => setResetConfirm(e.target.value)}
                   />
                 </div>
                 <div className="flex items-center gap-2">
                   <button onClick={handleResetConfirm} disabled={resetConfirm !== 'DELETE'} className="p-1.5 bg-red-500 rounded-full text-white disabled:opacity-50 hover:bg-red-600">
                      <IconCheck className="w-4 h-4" />
                   </button>
                   <button onClick={() => { setResetMode(false); setResetConfirm(''); }} className="p-1.5 bg-white/20 rounded-full text-white hover:bg-white/30">
                      <IconUndo className="w-4 h-4" />
                   </button>
                 </div>
               </>
            ) : isEditingProfile ? (
               <>
                 <div className="flex items-center gap-2 flex-1">
                   <span className="text-xs font-semibold text-white uppercase whitespace-nowrap">Profile:</span>
                   <input 
                      className="flex-1 min-w-0 bg-white/20 border border-white/30 rounded px-2 py-1 text-sm text-white placeholder-white/50 focus:outline-none focus:border-white"
                      placeholder="User profile details..."
                      value={profileText}
                      onChange={(e) => setProfileText(e.target.value)}
                   />
                 </div>
                 <div className="flex items-center gap-2">
                   <button onClick={handleProfileSave} className="p-1.5 bg-green-500 rounded-full text-white hover:bg-green-600">
                      <IconCheck className="w-4 h-4" />
                   </button>
                   <button onClick={() => setIsEditingProfile(false)} className="p-1.5 bg-white/20 rounded-full text-white hover:bg-white/30">
                      <IconUndo className="w-4 h-4" />
                   </button>
                 </div>
               </>
            ) : (
               <>
                 <div className="flex items-center gap-3">
                    <button onClick={startProfileEdit} className="p-2 hover:bg-white/20 rounded-full text-white transition-colors" title="Edit Profile">
                        <IconPencil className="w-4 h-4" />
                    </button>
                    <button 
                        onClick={onToggleTtsProvider} 
                        className="p-2 hover:bg-white/20 rounded-full text-white transition-colors relative" 
                        title={`TTS Provider: ${ttsProvider === 'gemini' ? 'Gemini' : 'Browser'}`}
                    >
                        <IconSpeaker className="w-5 h-5" />
                        <div className="absolute -bottom-1 -right-1 bg-blue-600 rounded-full p-0.5 border border-white">
                            {ttsProvider === 'gemini' ? <IconSparkles className="w-2.5 h-2.5 text-white" /> : <IconRobot className="w-2.5 h-2.5 text-white" />}
                        </div>
                    </button>
                    <button 
                        onClick={onToggleSttProvider} 
                        className="p-2 hover:bg-white/20 rounded-full text-white transition-colors relative" 
                        disabled={!isSpeechRecognitionSupported && sttProvider === 'gemini'}
                        title={`STT Provider: ${sttProvider === 'gemini' ? 'Gemini' : 'Browser'}`}
                    >
                        <IconMicrophone className="w-5 h-5" />
                        <div className="absolute -bottom-1 -right-1 bg-blue-600 rounded-full p-0.5 border border-white">
                            {sttProvider === 'gemini' ? <IconSparkles className="w-2.5 h-2.5 text-white" /> : <IconRobot className="w-2.5 h-2.5 text-white" />}
                        </div>
                    </button>
                 </div>
                 
                 <div className="flex items-center bg-blue-500/30 rounded-full p-0.5 border border-white/10">
                    <button onClick={handleSave} className="p-2 hover:bg-white/20 rounded-full text-white transition-colors" title={t('startPage.saveChats')}>
                        <IconSave className="w-4 h-4" />
                    </button>
                    <div className="w-px h-4 bg-white/20 mx-0.5"></div>
                    <button onClick={() => loadFileInputRef.current?.click()} className="p-2 hover:bg-white/20 rounded-full text-white transition-colors" title={t('startPage.loadChats')}>
                        <IconFolderOpen className="w-4 h-4" />
                    </button>
                    <div className="w-px h-4 bg-white/20 mx-0.5"></div>
                    <button onClick={() => setResetMode(true)} className="p-2 hover:bg-red-500/50 rounded-full text-white transition-colors" title="Backup & Reset">
                        <IconTrash className="w-4 h-4" />
                    </button>
                 </div>
                 <input type="file" ref={loadFileInputRef} onChange={handleLoadFileChange} accept=".json" className="hidden" />
               </>
            )}
          </div>
        ) : (
          <div className="relative w-full">
              <textarea
                  ref={bubbleTextAreaRef}
                  rows={1}
                  className={`w-full py-3 px-4 bg-transparent border-none focus:ring-0 resize-none overflow-hidden placeholder-inherit min-h-[50px]`}
                  style={{ fontSize: '3.6cqw', lineHeight: 1.35 }}
                  placeholder={getPlaceholderText()}
                  value={inputText}
                  onChange={handleInputChange}
                  onKeyDown={handleKeyDown}                            
                  disabled={isSending || (isListening && isSttGloballyEnabled) || (isSuggestionMode && isCreatingSuggestion)}
                  aria-label={t('chat.messageInputAriaLabel')}
              />
              {prepDisplay && <span className="sr-only" role="status" aria-live="polite">{prepDisplay}</span>}
          </div>
        )}

        <div className="flex items-center justify-between px-2 pb-2">
            <div className="flex items-center space-x-1">
              {!isLanguageSelectionOpen && (
                <>
                  <input type="file" accept="image/*,video/*,audio/*,application/pdf,text/plain,text/csv,text/markdown" ref={fileInputRef} onChange={handleImageAttach} className="hidden" id="imageUpload" />
                  <label
                    htmlFor="imageUpload"
                    className={`p-2 cursor-pointer rounded-full transition-colors ${iconButtonStyle}`}
                    title={t('chat.attachImageFromFile')}
                    role="button"
                    tabIndex={0}
                    onClick={() => { if (!paperclipOpenTokenRef.current && onUiTaskStart) { const tok = genUiToken('case-file-open'); const ret = onUiTaskStart(tok); paperclipOpenTokenRef.current = typeof ret === 'string' ? ret : tok; } }}
                  >
                      <IconPaperclip className="w-5 h-5" />
                  </label>
                  {isCameraActive && allCameraOptions.length > 0 ? (
                     <div className={`flex items-center p-0.5 ${isSuggestionMode ? 'bg-gray-300/50' : 'bg-blue-600/50'} rounded-full`}>
                         <button onClick={handleCameraDeactivationClick} className="p-1.5 rounded-full bg-red-500 text-white hover:bg-red-600" title={t('chat.camera.turnOff')}>
                             <IconXMark className="w-4 h-4" />
                         </button>
                         <div className="flex items-center space-x-0.5 ml-1">
                             {allCameraOptions.map(cam => {
                                 const isSelected = cam.deviceId === selectedCameraId;
                                 let Icon;
                                 if (cam.deviceId === IMAGE_GEN_CAMERA_ID) Icon = IconSparkles;
                                 else if (cam.facingMode === 'user') Icon = IconCameraFront;
                                 else Icon = IconCamera;
                                 return (
                                     <button key={cam.deviceId} onClick={() => onSelectCamera(cam.deviceId)} className={`p-1.5 rounded-full transition-colors ${isSelected ? `bg-white ${isSuggestionMode ? 'text-gray-800' : 'text-blue-600'}` : `${isSuggestionMode ? 'text-gray-600 hover:bg-black/10' : 'text-blue-100 hover:bg-blue-400/80'}`}`} title={cam.label}>
                                         <Icon className="w-4 h-4" />
                                     </button>
                                 );
                             })}
                         </div>
                     </div>
                 ) : (
                     <button onClick={handleCameraActivationClick} className={`p-2 cursor-pointer rounded-full transition-colors touch-manipulation ${isSuggestionMode ? 'text-gray-600 hover:text-black hover:bg-black/10' : 'hover:text-white hover:bg-blue-400/80'} ${isImageGenCameraSelected ? (isSuggestionMode ? 'text-purple-600' : 'text-purple-300 hover:text-purple-200') : ''}`} title={t('chat.camera.turnOn')}>
                         {isImageGenCameraSelected ? <IconSparkles className="w-5 h-5" /> : (currentCameraFacingMode === 'user' ? <IconCameraFront className="w-5 h-5" /> : <IconCamera className="w-5 h-5" />)}
                     </button>
                 )}
                  <button onClick={onToggleImageGenerationMode} className={`p-2 cursor-pointer rounded-full transition-colors touch-manipulation ${iconButtonStyle} ${imageGenerationModeEnabled ? (isSuggestionMode ? 'text-purple-600' : 'text-purple-300 hover:text-purple-200') : ''}`} title={t('chat.bookIcon.toggleImageGen')}>
                      <IconBookOpen className="w-5 h-5" />
                  </button>
                </>
              )}
              
              {isLanguageSelectionOpen && (
                  <div className="relative inline-block">
                    <div
                        onClick={!isUploadingMaestro ? handleMaestroAvatarClick : undefined}
                        className={`relative w-8 h-8 rounded-full overflow-hidden border-2 ${maestroAsset?.dataUrl ? 'border-white/50' : 'border-white/30 border-dashed'} bg-white/10 flex items-center justify-center hover:bg-white/20 transition cursor-pointer`}
                        title={maestroAsset?.dataUrl ? t('startPage.maestroAvatar') : t('startPage.addMaestroAvatar')}
                    >
                        {maestroAsset?.dataUrl ? (
                            <img src={maestroAsset.dataUrl} alt="Maestro" className="w-full h-full object-cover" />
                        ) : (
                            <IconPlus className="w-4 h-4 text-white/70" />
                        )}
                        {isUploadingMaestro && (
                            <div className="absolute inset-0 bg-black/40 flex items-center justify-center">
                                <SmallSpinner className="w-4 h-4 text-white" />
                            </div>
                        )}
                    </div>
                    <input type="file" ref={maestroFileInputRef} onChange={handleMaestroFileChange} accept="image/*" className="hidden" />
                    {maestroAsset?.dataUrl && !isUploadingMaestro && (
                        <button
                            onClick={handleClearMaestroAvatar}
                            className="absolute -top-1 -right-1 bg-red-500 text-white rounded-full p-0.5 shadow-sm hover:bg-red-600"
                            title={t('general.clear')}
                        >
                            <IconXMark className="w-3 h-3" />
                        </button>
                    )}
                  </div>
              )}
            </div>
            <div className="flex items-center space-x-1">
                {!isLanguageSelectionOpen && isSttSupported && (
                    <SttLanguageSelector
                        targetLang={targetLanguageDef}
                        nativeLang={nativeLanguageDef}
                        currentSttLangCode={sttLanguageCode}
                        onSelectLang={onSttLanguageChange}
                        t={t}
                        isCollapsed={true}
                        isInSuggestionMode={isSuggestionMode}
                    />
                )}
                {!isLanguageSelectionOpen && isSttSupported && (
                  <button
                    onClick={handleMicClick}
                    onPointerDown={handleMicPointerDown}
                    onPointerUp={handleMicPointerUp}
                    onPointerCancel={handleMicPointerCancel}
                    onPointerLeave={handleMicPointerCancel}
                    onContextMenu={(e) => { e.preventDefault(); e.stopPropagation(); }}
                    style={{ WebkitTouchCallout: 'none' } as React.CSSProperties}
                    className={`relative overflow-visible p-2 rounded-full transition-colors touch-manipulation select-none ${
                      isRecordingAudioNote ? 'bg-red-500 text-white ring-2 ring-red-300' : isListening ? 'bg-red-500/80 text-white' : iconButtonStyle
                    } disabled:opacity-50`}
                    title={getMicButtonTitle()}
                    disabled={isSending || isSpeaking || isLanguageSelectionOpen}
                    aria-pressed={isListening}
                  >
                    {isRecordingAudioNote && (
                      <>
                        <span className="pointer-events-none absolute -inset-4 rounded-full bg-red-400/30 animate-ping" />
                        <span className="pointer-events-none absolute -inset-6 rounded-full bg-red-400/15 animate-ping" style={{ animationDuration: '2s' }} />
                      </>
                    )}
                    <IconMicrophone className={`relative z-10 w-5 h-5 ${isRecordingAudioNote ? 'drop-shadow-[0_0_6px_rgba(239,68,68,0.8)]' : ''}`} />
                  </button>
                )}
                <button
                    onClick={handleSend}
                    className={`p-2 rounded-full focus:outline-none focus:ring-2 transition-colors disabled:opacity-50 shadow-sm ${sendButtonStyle}`}
                    disabled={isSending || ((!inputText.trim() && !attachedImageBase64) && !isLanguageSelectionOpen) || isSpeaking || (isSuggestionMode && isCreatingSuggestion) } 
                    aria-label={
                        isSuggestionMode 
                            ? (isCreatingSuggestion ? t('chat.suggestion.creating') : t('chat.suggestion.createAction')) 
                            : (sendPrep && sendPrep.active ? (sendPrep.label || t('chat.sendPrep.finalizing')) : t('chat.sendMessage'))
                    }
                >
                    {isSuggestionMode ? (isCreatingSuggestion ? <SmallSpinner className="w-5 h-5" /> : <IconPlus className="w-5 h-5" />) : (sendPrep && sendPrep.active ? <SmallSpinner className="w-5 h-5" /> : <IconSend className="w-5 h-5" />)}
                </button>
            </div>
        </div>
      </div>

      {sttError && <p className={`p-1 rounded mt-1 ${isSuggestionMode ? 'text-red-800 bg-red-200/50' : 'text-red-200 bg-red-900/50'}`} style={{ fontSize: '2.8cqw' }} role="alert">{t('chat.error.sttError', {error: sttError})}</p>}
      {autoCaptureError && <p className={`p-1 rounded mt-1 ${isSuggestionMode ? 'text-red-800 bg-red-200/50' : 'text-red-200 bg-red-900/50'}`} style={{ fontSize: '2.8cqw' }} role="alert">{t('chat.error.autoCaptureCameraError', {error: autoCaptureError})}</p>}
      {snapshotUserError && <p className={`p-1 rounded mt-1 ${isSuggestionMode ? 'text-orange-800 bg-orange-200/50' : 'text-orange-200 bg-orange-900/50'}`} style={{ fontSize: '2.8cqw' }} role="alert">{t('chat.error.snapshotUserError', {error: snapshotUserError})}</p>}
      </>
      )}
    </>
  );
};

export default InputArea;
