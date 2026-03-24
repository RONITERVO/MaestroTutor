// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { TranslationReplacements } from '../../../../core/i18n/index';
import { LiveSessionState } from '../../../speech';
import { IconCamera, IconPaperclip, IconPencil, IconXMark, IconVideoCamera } from '../../../../shared/ui/Icons';
import { SmallSpinner } from '../../../../shared/ui/SmallSpinner';
import { useMaestroStore } from '../../../../store';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../../core/config/activityTokens';
import AudioPlayer from '../AudioPlayer';
import PdfViewer from '../PdfViewer';
import TextFileViewer from '../TextFileViewer';
import OfficeFileViewer from '../OfficeFileViewer';
import { isOfficeAttachment, isTextLikeAttachment } from '../../utils/fileAttachments';

interface MediaAttachmentsProps {
  t: (key: string, replacements?: TranslationReplacements) => string;
  isSuggestionMode: boolean;
  attachedImageBase64: string | null;
  attachedImageMimeType: string | null;
  attachedFileName?: string | null;
  showLiveFeed: boolean;
  isTwoUp: boolean;
  liveVideoStream: MediaStream | null;
  liveSessionState: LiveSessionState;
  liveSessionError: string | null;
  onStartLiveSession: () => Promise<void> | void;
  onStopLiveSession: () => void;
  onBeforeStartVideoRecording?: () => Promise<void> | void;
  onRemoveAttachment: () => void;
  onAnnotateImage: () => void;
  onAnnotateVideo: () => void;
  onAnnotatePdf: () => void;
  onSetAttachedImage: (base64: string | null, mimeType: string | null, fileName?: string | null) => void;
  onUserInputActivity: () => void;
  attachedPreviewVideoRef: React.RefObject<HTMLVideoElement | null>;
  isSilentObserverActive?: boolean;
}

const MediaAttachments: React.FC<MediaAttachmentsProps> = ({
  t,
  isSuggestionMode,
  attachedImageBase64,
  attachedImageMimeType,
  attachedFileName,
  showLiveFeed,
  isTwoUp,
  liveVideoStream,
  liveSessionState,
  liveSessionError,
  onStartLiveSession,
  onStopLiveSession,
  onBeforeStartVideoRecording,
  onRemoveAttachment,
  onAnnotateImage,
  onAnnotateVideo,
  onAnnotatePdf,
  onSetAttachedImage,
  onUserInputActivity,
  attachedPreviewVideoRef,
  isSilentObserverActive = false,
}) => {
  const addActivityToken = useMaestroStore(state => state.addActivityToken);
  const removeActivityToken = useMaestroStore(state => state.removeActivityToken);
  const createUiToken = useCallback(
    (subtype: string) =>
      addActivityToken(
        TOKEN_CATEGORY.UI,
        `${subtype}:${Date.now()}:${Math.random().toString(36).slice(2, 8)}`
      ),
    [addActivityToken]
  );
  const endUiTask = useCallback((token: string | null) => {
    if (token) removeActivityToken(token);
  }, [removeActivityToken]);

  const livePreviewVideoRef = useRef<HTMLVideoElement>(null);
  const [attachedVideoPlaying, setAttachedVideoPlaying] = useState(false);
  const attachedVideoPlayTokenRef = useRef<string | null>(null);

  const [isRecording, setIsRecording] = useState(false);
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const recordedChunksRef = useRef<Blob[]>([]);
  const recordingTimerRef = useRef<number | null>(null);
  const videoRecordTokenRef = useRef<string | null>(null);
  const recordingAudioStreamRef = useRef<MediaStream | null>(null);

  const pickRecorderMimeType = () => {
    const candidates = ['video/webm;codecs=vp9,opus', 'video/webm;codecs=vp8,opus', 'video/webm'];
    for (const m of candidates) if ((window as any).MediaRecorder?.isTypeSupported?.(m)) return m;
    return '';
  };

  const handleStopRecording = useCallback(() => {
    const rec = mediaRecorderRef.current;
    if (rec && rec.state === 'recording') {
      rec.stop();
    }
    if (recordingAudioStreamRef.current) {
      recordingAudioStreamRef.current.getTracks().forEach(track => track.stop());
      recordingAudioStreamRef.current = null;
    }
    if (recordingTimerRef.current) {
      clearTimeout(recordingTimerRef.current);
      recordingTimerRef.current = null;
    }
    setIsRecording(false);
    if (videoRecordTokenRef.current) {
      endUiTask(videoRecordTokenRef.current);
      videoRecordTokenRef.current = null;
    }
  }, [endUiTask]);

  // Cleanup on unmount or stream loss - stop recording and clear timers
  useEffect(() => {
    return () => {
      // Stop any active recording
      const rec = mediaRecorderRef.current;
      if (rec && rec.state === 'recording') {
        try { rec.stop(); } catch {}
      }
      // Stop recording audio stream
      if (recordingAudioStreamRef.current) {
        recordingAudioStreamRef.current.getTracks().forEach(track => track.stop());
        recordingAudioStreamRef.current = null;
      }
      // Clear timers
      if (recordingTimerRef.current) {
        clearTimeout(recordingTimerRef.current);
        recordingTimerRef.current = null;
      }
      // End any pending UI tokens
      if (videoRecordTokenRef.current) {
        endUiTask(videoRecordTokenRef.current);
        videoRecordTokenRef.current = null;
      }
    };
  }, [endUiTask]);

  const handleStartRecording = useCallback(async () => {
    if (isRecording || !liveVideoStream) return;
    try {
      if (onBeforeStartVideoRecording) {
        await Promise.resolve(onBeforeStartVideoRecording());
      }

      if (!videoRecordTokenRef.current) {
        videoRecordTokenRef.current = createUiToken(TOKEN_SUBTYPE.VIDEO_RECORD);
      }

      // Acquire audio lazily to avoid mobile "call mode" from combined audio+video streams
      let recordStream: MediaStream = liveVideoStream;
      try {
        const audioStream = await navigator.mediaDevices.getUserMedia({ audio: true });
        recordingAudioStreamRef.current = audioStream;
        recordStream = new MediaStream([
          ...liveVideoStream.getVideoTracks(),
          ...audioStream.getAudioTracks(),
        ]);
      } catch {
        // Audio unavailable (permission denied, no mic, etc.) -- record video without audio
      }

      const mimeType = pickRecorderMimeType();
      const options: MediaRecorderOptions = mimeType ? { mimeType } : {};
      const rec = new MediaRecorder(recordStream, options);
      mediaRecorderRef.current = rec;
      recordedChunksRef.current = [];
      rec.ondataavailable = (event) => { if (event.data && event.data.size > 0) recordedChunksRef.current.push(event.data); };
      rec.onstop = () => {
        // Strip codec params (e.g. ";codecs=vp9,opus") - the comma in the
        // codec list breaks data-URL parsing (browser splits on first comma).
        const chosenType = (rec.mimeType || mimeType || 'video/webm').split(';')[0];
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
        if (videoRecordTokenRef.current) {
          endUiTask(videoRecordTokenRef.current);
          videoRecordTokenRef.current = null;
        }
      };
      rec.start();
      setIsRecording(true);
      recordingTimerRef.current = window.setTimeout(() => {
        if (mediaRecorderRef.current?.state === 'recording') {
          handleStopRecording();
          alert(t('chat.error.recordingTimeExceeded', { maxMinutes: 15 }));
        }
      }, 15 * 60 * 1000);
    } catch (e) {
      console.error('Failed to start media recorder:', e);
      if (recordingAudioStreamRef.current) {
        recordingAudioStreamRef.current.getTracks().forEach(track => track.stop());
        recordingAudioStreamRef.current = null;
      }
      if (videoRecordTokenRef.current) {
        endUiTask(videoRecordTokenRef.current);
        videoRecordTokenRef.current = null;
      }
    }
  }, [isRecording, liveVideoStream, onBeforeStartVideoRecording, onSetAttachedImage, onUserInputActivity, t, handleStopRecording, createUiToken, endUiTask]);

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

  const handleLiveSessionStart = useCallback(() => {
    Promise.resolve(onStartLiveSession()).catch((err) => {
      console.error('Failed to start live session:', err);
    });
  }, [onStartLiveSession]);

  useEffect(() => {
    if (livePreviewVideoRef.current && liveVideoStream) {
      if (livePreviewVideoRef.current.srcObject !== liveVideoStream) {
        livePreviewVideoRef.current.srcObject = liveVideoStream;
        livePreviewVideoRef.current.play().catch(e => console.error('Error playing live preview:', e));
      } else if (livePreviewVideoRef.current.paused) {
        livePreviewVideoRef.current.play().catch(e => console.error('Error playing live preview:', e));
      }
    } else if (livePreviewVideoRef.current && !liveVideoStream) {
      livePreviewVideoRef.current.srcObject = null;
    }
  }, [liveVideoStream, attachedImageBase64]);

  const isTextAttachment = isTextLikeAttachment(attachedImageMimeType, attachedFileName);
  const isOfficeAttachmentFile = isOfficeAttachment(attachedImageMimeType, attachedFileName);

  if (!attachedImageBase64 && !showLiveFeed) return null;

  const liveSessionActive = liveSessionState === 'active';
  const liveSessionConnecting = liveSessionState === 'connecting';
  const liveSessionErrored = liveSessionState === 'error';
  const panelWidthClass = isTwoUp
    ? 'w-[calc(50%-0.25rem)] max-w-[calc(50%-0.25rem)]'
    : 'w-[min(50%,22rem)] max-w-[50%]';
  const overlayIconShadowStyle: React.CSSProperties = {
    filter: 'drop-shadow(0 1px 2px rgba(0,0,0,0.72))',
  };
  const overlayIconButtonBaseClasses = 'rounded-full opacity-85 transition-all duration-200 hover:opacity-100 focus:outline-none focus:ring-2 active:scale-95 disabled:opacity-40 disabled:cursor-default';
  const neutralOverlayIconButtonClasses = `${overlayIconButtonBaseClasses} text-white/90 hover:text-white focus:ring-white/40`;
  const removeOverlayIconButtonClasses = `${overlayIconButtonBaseClasses} text-remove-attach-icon focus:ring-white/40`;

  return (
    <div className="flex flex-wrap justify-center items-start gap-2 w-full">
      {attachedImageBase64 && (
        <div className={`relative ${panelWidthClass} min-w-0 ${isSuggestionMode ? 'bg-media-sugg-bg' : 'bg-media-chat-bg/80'} p-1 rounded-md`}>
          {attachedImageMimeType?.startsWith('image/') ? (
            <div className="relative">
              <img src={attachedImageBase64} alt={t('chat.imagePreview.alt')} className="h-24 w-full object-cover rounded" />
              <button
                type="button"
                onClick={onAnnotateImage}
                className={`absolute top-1.5 right-1.5 p-1.5 ${neutralOverlayIconButtonClasses}`}
                title={t('chat.annotateImage')}
              >
                <span style={overlayIconShadowStyle}>
                  <IconPencil className="w-4 h-4" />
                </span>
              </button>
            </div>
          ) : attachedImageMimeType?.startsWith('video/') ? (
            <div className="relative">
              <video
                ref={attachedPreviewVideoRef}
                src={attachedImageBase64}
                controls
                className="h-24 w-full object-contain rounded bg-black"
                onPlay={() => {
                  setAttachedVideoPlaying(true);
                  if (!attachedVideoPlayTokenRef.current) {
                    attachedVideoPlayTokenRef.current = createUiToken(TOKEN_SUBTYPE.VIDEO_PLAY);
                  }
                }}
                onPause={() => {
                  setAttachedVideoPlaying(false);
                  if (attachedVideoPlayTokenRef.current) {
                    endUiTask(attachedVideoPlayTokenRef.current);
                    attachedVideoPlayTokenRef.current = null;
                  }
                }}
                onEnded={() => {
                  setAttachedVideoPlaying(false);
                  if (attachedVideoPlayTokenRef.current) {
                    endUiTask(attachedVideoPlayTokenRef.current);
                    attachedVideoPlayTokenRef.current = null;
                  }
                }}
              />
              <button
                type="button"
                onClick={onAnnotateVideo}
                disabled={attachedVideoPlaying}
                className={`absolute top-1.5 right-1.5 p-1.5 ${neutralOverlayIconButtonClasses}`}
                title={attachedVideoPlaying ? t('chat.error.pauseVideoToAnnotate') : t('chat.annotateVideoFrame')}
              >
                <span style={overlayIconShadowStyle}>
                  <IconPencil className="w-4 h-4" />
                </span>
              </button>
            </div>
          ) : attachedImageMimeType?.startsWith('audio/') ? (
            <div className="relative">
              <AudioPlayer src={attachedImageBase64} variant="preview" compact />
            </div>
          ) : attachedImageMimeType === 'application/pdf' ? (
            <div className="relative">
              <PdfViewer src={attachedImageBase64} variant="preview" compact />
              <button
                type="button"
                onClick={onAnnotatePdf}
                className={`absolute top-1.5 right-1.5 p-1.5 ${neutralOverlayIconButtonClasses}`}
                title={t('chat.annotateImage')}
              >
                <span style={overlayIconShadowStyle}>
                  <IconPencil className="w-4 h-4" />
                </span>
              </button>
            </div>
          ) : isOfficeAttachmentFile ? (
            <OfficeFileViewer
              src={attachedImageBase64}
              variant="preview"
              compact
              fileName={attachedFileName}
              mimeType={attachedImageMimeType}
            />
          ) : isTextAttachment ? (
            <TextFileViewer
              src={attachedImageBase64}
              variant="preview"
              compact
              fileName={attachedFileName}
              mimeType={attachedImageMimeType}
            />
          ) : (
            <div className={`h-24 w-full flex flex-col items-center justify-center ${isSuggestionMode ? 'bg-media-sugg-bg' : 'bg-media-empty-bg/60'} rounded`}>
              <IconPaperclip className="w-8 h-8 text-media-empty-text/70" />
              <span className="text-xs mt-1 truncate max-w-full px-1 text-media-empty-text">{attachedImageMimeType}</span>
            </div>
          )}
          <div className="absolute -top-2 -right-2 flex items-center space-x-1">
            <button
              type="button"
              onClick={onRemoveAttachment}
              className={`p-1.5 ${removeOverlayIconButtonClasses}`}
              aria-label={t('chat.removeAttachedImage')}
            >
              <span style={overlayIconShadowStyle}>
                <IconXMark className="w-4 h-4" />
              </span>
            </button>
          </div>
        </div>
      )}

      {showLiveFeed && (
        <div className={`relative ${panelWidthClass} min-w-0 ${isSuggestionMode ? 'bg-media-sugg-bg' : 'bg-media-chat-bg/80'} p-1 rounded-md`}>
          <div className="relative group">
            <video
              ref={livePreviewVideoRef}
              autoPlay
              playsInline
              muted
              className="h-24 w-full object-cover rounded pointer-events-none"
            />
            {isSilentObserverActive && !liveSessionActive && !liveSessionConnecting && (
              <div
                className="absolute top-1 right-1 z-20 h-2.5 w-2.5 rounded-full bg-emerald-400 border border-black/40"
                title={t('camera.observerActive') || 'Observer active'}
                aria-label={t('camera.observerActive') || 'Observer active'}
              />
            )}
            <div className="absolute inset-0 flex items-center justify-center bg-black/20 group-hover:bg-black/40 transition-colors">
              {liveSessionActive ? (
                <div className="flex flex-col items-center gap-2">
                  <div className="flex items-center gap-2 px-3 py-1 rounded-full bg-live-badge-bg text-live-badge-text uppercase text-xs font-semibold tracking-wide">
                    <span className="inline-flex h-2 w-2 rounded-full bg-live-badge-dot animate-pulse" aria-hidden />
                    {t('chat.liveSession.liveBadge') || 'Live'}
                  </div>
                  <button
                    type="button"
                    onClick={onStopLiveSession}
                    className={`p-2 ${overlayIconButtonBaseClasses} focus:ring-white/40`}
                    aria-label={t('chat.camera.stopRecording')}
                  >
                    <div className="w-4 h-4 bg-live-stop-icon rounded-sm" style={overlayIconShadowStyle} />
                  </button>
                </div>
              ) : liveSessionConnecting ? (
                <div className="flex items-center gap-2 px-3 py-1 rounded-full bg-black/60 text-white text-xs">
                  <SmallSpinner className="w-4 h-4 text-white" />
                  <span>{t('chat.liveSession.connecting') || 'Connecting'}</span>
                </div>
              ) : isRecording ? (
                <button
                  type="button"
                  onClick={handleStopRecording}
                  className={`p-2 ${overlayIconButtonBaseClasses} focus:ring-white/40`}
                  aria-label={t('chat.camera.stopRecording')}
                >
                  <div className="w-4 h-4 bg-vid-stop-icon rounded-sm" style={overlayIconShadowStyle} />
                </button>
              ) : (
                <div className="flex items-center gap-3">
                  <button
                    type="button"
                    onClick={handleCaptureImage}
                    className={`p-2 ${neutralOverlayIconButtonClasses}`}
                    aria-label={t('chat.camera.capturePhoto')}
                  >
                    <span style={overlayIconShadowStyle}>
                      <IconCamera className="w-6 h-6" />
                    </span>
                  </button>
                  <button
                    type="button"
                    onClick={() => handleStartRecording()}
                    className={`p-2 ${neutralOverlayIconButtonClasses}`}
                    aria-label={t('chat.camera.startRecording')}
                  >
                    <span style={overlayIconShadowStyle}>
                      <IconVideoCamera className="w-6 h-6" />
                    </span>
                  </button>
                  <button
                    type="button"
                    onClick={handleLiveSessionStart}
                    className={`p-2 rounded-full transition-colors ${
                      liveSessionErrored
                        ? 'bg-overlay-live-error-bg text-overlay-live-error-text hover:bg-overlay-live-error-hover'
                        : 'bg-live-idle-btn-bg text-live-idle-btn-text hover:bg-live-idle-btn-bg/80'
                    }`}
                    aria-label={t('chat.liveSession.liveBadge') || 'LIVE'}
                  >
                    <span className="w-6 h-6 flex items-center justify-center text-[10px] font-bold leading-none">LIVE</span>
                  </button>
                </div>
              )}
            </div>
            {isRecording && !liveSessionActive && !liveSessionConnecting && (
              <div className="absolute top-1 left-1 flex items-center space-x-1 p-1 bg-black/50 rounded-lg z-20">
                <div className="w-2 h-2 bg-rec-dot rounded-full animate-pulse" />
                <span className="text-white text-xs font-mono">REC</span>
              </div>
            )}
          </div>
          {liveSessionError && (
            <div className="mt-1 px-2 py-1 text-xs rounded bg-rec-error-bg/20 text-rec-error-text">
              {liveSessionError}
            </div>
          )}
        </div>
      )}
    </div>
  );
};

export default MediaAttachments;

