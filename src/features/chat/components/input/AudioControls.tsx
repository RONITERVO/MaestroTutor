import React, { useCallback, useEffect, useRef, useState } from 'react';
import { TranslationReplacements } from '../../../../core/i18n/index';
import { LanguageDefinition } from '../../../../core/config/languages';
import { IconMicrophone } from '../../../../shared/ui/Icons';
import { SttLanguageSelector } from '../../../speech';
import { useMaestroStore } from '../../../../store';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../../core/config/activityTokens';

interface AudioControlsProps {
  t: (key: string, replacements?: TranslationReplacements) => string;
  isLanguageSelectionOpen: boolean;
  isSttSupported: boolean;
  isSttGloballyEnabled: boolean;
  isListening: boolean;
  isSending: boolean;
  isSpeaking: boolean;
  sttLanguageCode: string;
  targetLanguageDef: LanguageDefinition;
  nativeLanguageDef: LanguageDefinition;
  isSuggestionMode: boolean;
  onSttToggle: () => void;
  onSttLanguageChange: (langCode: string) => void;
  onSetAttachedImage: (base64: string | null, mimeType: string | null) => void;
  onUserInputActivity: () => void;
}

const AudioControls: React.FC<AudioControlsProps> = ({
  t,
  isLanguageSelectionOpen,
  isSttSupported,
  isSttGloballyEnabled,
  isListening,
  isSending,
  isSpeaking,
  sttLanguageCode,
  targetLanguageDef,
  nativeLanguageDef,
  isSuggestionMode,
  onSttToggle,
  onSttLanguageChange,
  onSetAttachedImage,
  onUserInputActivity,
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

  const [isRecordingAudioNote, setIsRecordingAudioNote] = useState(false);
  const audioNoteRecorderRef = useRef<MediaRecorder | null>(null);
  const audioNoteChunksRef = useRef<BlobPart[]>([]);
  const audioNoteStreamRef = useRef<MediaStream | null>(null);
  const audioNoteTokenRef = useRef<string | null>(null);
  const micHoldTimerRef = useRef<number | null>(null);
  const micHoldActiveRef = useRef<boolean>(false);

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
        if (audioNoteTokenRef.current) {
          endUiTask(audioNoteTokenRef.current);
          audioNoteTokenRef.current = null;
        }
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
        if (audioNoteTokenRef.current) {
          endUiTask(audioNoteTokenRef.current);
          audioNoteTokenRef.current = null;
        }
      };
      if (!audioNoteTokenRef.current) {
        audioNoteTokenRef.current = createUiToken(TOKEN_SUBTYPE.AUDIO_NOTE);
      }
      rec.start(250);
      setIsRecordingAudioNote(true);
    } catch (e) {
      console.error('Failed to start audio note recording:', e);
    }
  }, [isRecordingAudioNote, isSttGloballyEnabled, onSetAttachedImage, onUserInputActivity, createUiToken, endUiTask]);

  const stopAudioNoteRecording = useCallback(() => {
    const rec = audioNoteRecorderRef.current;
    if (rec && rec.state === 'recording') { try { rec.requestData(); } catch {} rec.stop(); }
    setIsRecordingAudioNote(false);
    if (audioNoteTokenRef.current) {
      endUiTask(audioNoteTokenRef.current);
      audioNoteTokenRef.current = null;
    }
  }, [endUiTask]);

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

  const getMicButtonTitle = () => {
    if (isRecordingAudioNote) {
      try { const k = t('chat.mic.recordingAudioNote'); if (k !== 'chat.mic.recordingAudioNote') return k; } catch {}
      return 'Recording audio... release to attach';
    }
    return isListening ? t('chat.mic.listening') : (isSttGloballyEnabled ? t('chat.mic.disableStt') : t('chat.mic.enableStt'));
  };

  return (
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
            isRecordingAudioNote ? 'bg-red-500 text-white ring-2 ring-red-300' : isListening ? 'bg-red-500/80 text-white' : (isSuggestionMode ? 'text-gray-500 hover:text-gray-900 hover:bg-gray-100' : 'text-blue-100 hover:text-white hover:bg-white/20')
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
    </div>
  );
};

export default AudioControls;
