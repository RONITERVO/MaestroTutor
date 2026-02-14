// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { TranslationReplacements } from '../../../../core/i18n/index';
import { LanguageDefinition, hasSharedFlag } from '../../../../core/config/languages';
import { IconMicrophone } from '../../../../shared/ui/Icons';
import { useMaestroStore } from '../../../../store';
import { selectIsSending } from '../../../../store/slices/uiSlice';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../../core/config/activityTokens';
import { parseLanguagePairId } from '../../../../shared/utils/languageUtils';

interface AudioControlsProps {
  t: (key: string, replacements?: TranslationReplacements) => string;
  isLanguageSelectionOpen: boolean;
  isSttSupported: boolean;
  isSttGloballyEnabled: boolean;
  isListening: boolean;
  isSending: boolean;
  isSpeaking: boolean;
  targetLanguageDef: LanguageDefinition;
  nativeLanguageDef: LanguageDefinition;
  isSuggestionMode: boolean;
  onSttToggle: () => void;
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
  targetLanguageDef,
  nativeLanguageDef,
  isSuggestionMode,
  onSttToggle,
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
      // EXPLICIT PERMISSION REQUEST:
      // We request the stream and await it. If the user sees a prompt,
      // this await will pause execution until they Allow or Deny.
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });

      // If we got here, permission is granted!
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
      // User denied permission or system error
      setIsRecordingAudioNote(false);
      micHoldActiveRef.current = false;
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

    // Wait for the "hold" duration before starting recording logic
    micHoldTimerRef.current = window.setTimeout(async () => {
      micHoldTimerRef.current = null;
      micHoldActiveRef.current = true;

      // We removed the flaky 'navigator.permissions.query' check here.
      // Instead, we just call startAudioNoteRecording(), which now safely waits for getUserMedia.
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

  // Cleanup on unmount: stop recording, release streams, clear timers, end tokens
  useEffect(() => {
    return () => {
      // Clear mic hold timer
      if (micHoldTimerRef.current) {
        clearTimeout(micHoldTimerRef.current);
        micHoldTimerRef.current = null;
      }
      // Stop MediaRecorder if active
      const rec = audioNoteRecorderRef.current;
      if (rec && rec.state === 'recording') {
        try { rec.stop(); } catch {}
      }
      audioNoteRecorderRef.current = null;
      // Stop all stream tracks
      if (audioNoteStreamRef.current) {
        try { audioNoteStreamRef.current.getTracks().forEach(t => t.stop()); } catch {}
        audioNoteStreamRef.current = null;
      }
      // End UI token if still active
      if (audioNoteTokenRef.current) {
        endUiTask(audioNoteTokenRef.current);
        audioNoteTokenRef.current = null;
      }
    };
  }, [endUiTask]);

  const getMicButtonTitle = () => {
    if (isRecordingAudioNote) {
      try { const k = t('chat.mic.recordingAudioNote'); if (k !== 'chat.mic.recordingAudioNote') return k; } catch {}
      return 'Recording audio... release to attach';
    }
    return isListening ? t('chat.mic.listening') : (isSttGloballyEnabled ? t('chat.mic.disableStt') : t('chat.mic.enableStt'));
  };

  const setIsLanguageSelectionOpen = useMaestroStore(state => state.setIsLanguageSelectionOpen);
  const setTempNativeLangCode = useMaestroStore(state => state.setTempNativeLangCode);
  const setTempTargetLangCode = useMaestroStore(state => state.setTempTargetLangCode);
  const selectedLanguagePairId = useMaestroStore(state => state.settings.selectedLanguagePairId);

  const handleOpenLanguageSelector = useCallback(() => {
    const state = useMaestroStore.getState();
    if (selectIsSending(state)) return;
    setIsLanguageSelectionOpen(true);
    const currentPairId = selectedLanguagePairId;
    if (currentPairId && typeof currentPairId === 'string') {
      const parsed = parseLanguagePairId(currentPairId);
      if (parsed) {
        setTempTargetLangCode(parsed.targetCode);
        setTempNativeLangCode(parsed.nativeCode);
      } else {
        setTempNativeLangCode(null);
        setTempTargetLangCode(null);
      }
    } else {
      setTempNativeLangCode(null);
      setTempTargetLangCode(null);
    }
  }, [setIsLanguageSelectionOpen, setTempNativeLangCode, setTempTargetLangCode, selectedLanguagePairId]);

  return (
    <div className="flex items-center space-x-1">
      {!isLanguageSelectionOpen && (
        <button
          type="button"
          onClick={handleOpenLanguageSelector}
          className={`flex flex-col items-center justify-center p-1 rounded-full transition-colors ${
            isSuggestionMode
              ? 'hover:bg-secondary'
              : 'hover:bg-white/20'
          }`}
          title={t('chat.languageSelector.openGlobe')}
          disabled={isSending || isSpeaking}
        >
          <span className="flex items-center gap-0.5 leading-none" style={{ fontSize: '10px' }}>
            {nativeLanguageDef.flag}
            {hasSharedFlag(nativeLanguageDef) && <span className={`text-[7px] font-bold ${isSuggestionMode ? 'text-muted-foreground' : 'text-accent-foreground/70'}`}>{nativeLanguageDef.shortCode}</span>}
          </span>
          <span className="flex items-center gap-0.5 leading-none" style={{ fontSize: '14px' }}>
            {targetLanguageDef.flag}
            {hasSharedFlag(targetLanguageDef) && <span className={`text-[9px] font-bold ${isSuggestionMode ? 'text-muted-foreground' : 'text-accent-foreground/70'}`}>{targetLanguageDef.shortCode}</span>}
          </span>
        </button>
      )}
      {!isLanguageSelectionOpen && isSttSupported && (
        <button
          type="button"
          onClick={handleMicClick}
          onPointerDown={handleMicPointerDown}
          onPointerUp={handleMicPointerUp}
          onPointerCancel={handleMicPointerCancel}
          onPointerLeave={handleMicPointerCancel}
          onContextMenu={(e) => { e.preventDefault(); e.stopPropagation(); }}
          style={{ WebkitTouchCallout: 'none' } as React.CSSProperties}
          className={`relative overflow-visible p-2 rounded-full transition-colors touch-manipulation select-none ${
            isRecordingAudioNote ? 'bg-red-500 text-white ring-2 ring-red-300' : isListening ? 'bg-red-500/80 text-white' : (isSuggestionMode ? 'text-muted-foreground hover:text-foreground hover:bg-secondary' : 'text-accent-foreground/70 hover:text-accent-foreground hover:bg-white/20')
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
