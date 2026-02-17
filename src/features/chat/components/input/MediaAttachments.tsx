// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { TranslationReplacements } from '../../../../core/i18n/index';
import { LiveSessionState } from '../../../speech';
import { IconCamera, IconPaperclip, IconPencil, IconXMark } from '../../../../shared/ui/Icons';
import { SmallSpinner } from '../../../../shared/ui/SmallSpinner';
import { useMaestroStore } from '../../../../store';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../../core/config/activityTokens';
import LiveSessionControls from './LiveSessionControls';
import AudioPlayer from '../AudioPlayer';
import PdfViewer from '../PdfViewer';

interface MediaAttachmentsProps {
  t: (key: string, replacements?: TranslationReplacements) => string;
  isSuggestionMode: boolean;
  attachedImageBase64: string | null;
  attachedImageMimeType: string | null;
  showLiveFeed: boolean;
  isTwoUp: boolean;
  liveVideoStream: MediaStream | null;
  liveSessionState: LiveSessionState;
  liveSessionError: string | null;
  onStartLiveSession: () => Promise<void> | void;
  onStopLiveSession: () => void;
  onRemoveAttachment: () => void;
  onAnnotateImage: () => void;
  onAnnotateVideo: () => void;
  onAnnotatePdf: () => void;
  onSetAttachedImage: (base64: string | null, mimeType: string | null) => void;
  onUserInputActivity: () => void;
  attachedPreviewVideoRef: React.RefObject<HTMLVideoElement | null>;
}

const MediaAttachments: React.FC<MediaAttachmentsProps> = ({
  t,
  isSuggestionMode,
  attachedImageBase64,
  attachedImageMimeType,
  showLiveFeed,
  isTwoUp,
  liveVideoStream,
  liveSessionState,
  liveSessionError,
  onStartLiveSession,
  onStopLiveSession,
  onRemoveAttachment,
  onAnnotateImage,
  onAnnotateVideo,
  onAnnotatePdf,
  onSetAttachedImage,
  onUserInputActivity,
  attachedPreviewVideoRef,
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
  const capturePressTimerRef = useRef<number | null>(null);
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
      if (capturePressTimerRef.current) {
        clearTimeout(capturePressTimerRef.current);
        capturePressTimerRef.current = null;
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
        // Strip codec params (e.g. ";codecs=vp9,opus") — the comma in the
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
  }, [isRecording, liveVideoStream, onSetAttachedImage, onUserInputActivity, t, handleStopRecording, createUiToken, endUiTask]);

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

  if (!attachedImageBase64 && !showLiveFeed) return null;

  const liveSessionActive = liveSessionState === 'active';
  const liveSessionConnecting = liveSessionState === 'connecting';

  return (
    <div className="flex flex-wrap justify-center items-start gap-2 w-full">
      {attachedImageBase64 && (
        <div className={`relative ${isTwoUp ? 'w-[calc(50%-0.25rem)] sm:w-48' : 'w-48'} min-w-0 ${isSuggestionMode ? 'bg-secondary' : 'bg-accent/80'} p-1 rounded-md`}>
          {attachedImageMimeType?.startsWith('image/') ? (
            <div className="relative">
              <img src={attachedImageBase64} alt={t('chat.imagePreview.alt')} className="h-24 w-full object-cover rounded" />
              <button
                type="button"
                onClick={onAnnotateImage}
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
                className="absolute top-1.5 right-1.5 p-1.5 bg-black/60 text-white rounded-full hover:bg-black disabled:opacity-50"
                title={attachedVideoPlaying ? t('chat.error.pauseVideoToAnnotate') : t('chat.annotateVideoFrame')}
              >
                <IconPencil className="w-4 h-4" />
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
                className="absolute top-1.5 right-1.5 p-1.5 bg-black/60 text-white rounded-full hover:bg-black"
                title={t('chat.annotateImage')}
              >
                <IconPencil className="w-4 h-4" />
              </button>
            </div>
          ) : (
            <div className={`h-24 w-full flex flex-col items-center justify-center ${isSuggestionMode ? 'bg-muted' : 'bg-accent/60'} rounded`}>
              <IconPaperclip className={`w-8 h-8 ${isSuggestionMode ? 'text-muted-foreground' : 'text-accent-foreground/70'}`} />
              <span className={`text-xs mt-1 truncate max-w-full px-1 ${isSuggestionMode ? 'text-muted-foreground' : 'text-accent-foreground'}`}>{attachedImageMimeType}</span>
            </div>
          )}
          <div className="absolute -top-2 -right-2 flex items-center space-x-1">
            <button type="button" onClick={onRemoveAttachment} className="p-1 bg-red-500/80 text-white rounded-full hover:bg-red-500" aria-label={t('chat.removeAttachedImage')}>
              <IconXMark className="w-4 h-4" />
            </button>
          </div>
        </div>
      )}

      {showLiveFeed && (
        <div className={`relative ${isTwoUp ? 'w-[calc(50%-0.25rem)] sm:w-48' : 'w-48'} min-w-0 ${isSuggestionMode ? 'bg-secondary' : 'bg-accent/80'} p-1 rounded-md`}>
          <div className="relative group">
            <video
              ref={livePreviewVideoRef}
              autoPlay
              playsInline
              muted
              className="h-24 w-full object-cover rounded pointer-events-none"
            />
            <LiveSessionControls
              t={t}
              liveSessionState={liveSessionState}
              isSuggestionMode={isSuggestionMode}
              onStartLiveSession={onStartLiveSession}
              onStopLiveSession={onStopLiveSession}
            />
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
                  type="button"
                  onClick={handleStopRecording}
                  className="p-2 rounded-full bg-red-500/80 text-white group-hover:bg-red-500 transition-colors"
                  aria-label={t('chat.camera.stopRecording')}
                >
                  <div className="w-4 h-4 bg-white rounded-sm" />
                </button>
              ) : (
                <button
                  type="button"
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
            <div className={`mt-1 px-2 py-1 text-xs rounded ${isSuggestionMode ? 'bg-destructive/10 text-destructive' : 'bg-red-600/20 text-red-100'}`}>
              {liveSessionError}
            </div>
          )}
        </div>
      )}
    </div>
  );
};

export default MediaAttachments;
