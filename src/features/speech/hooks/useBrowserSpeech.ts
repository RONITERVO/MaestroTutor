
// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useState, useEffect, useCallback, useRef } from 'react';
import type { SpeechPart, RecordedUtterance } from '../../../core/types';
import { useTtsEngine } from './useTtsEngine';
import { useGeminiLiveStt } from './useGeminiLiveStt';
import { pcmToWav } from '../utils/audioProcessing';

interface UseBrowserSpeechProps {
  onEngineCycleEnd?: (errorOccurred: boolean) => void;
  isGlobalSttEnabled?: () => boolean;
  getGlobalSttLanguage?: () => string;
  onSpeechQueueCompleted?: () => void;
  onRecordedUtteranceReady?: (utterance: RecordedUtterance) => void;
}

interface UseBrowserSpeechReturn {
  isSpeaking: boolean;
  speak: (textOrParts: string | SpeechPart[], defaultLang: string) => void;
  stopSpeaking: () => void;
  isSpeechSynthesisSupported: boolean;
  isListening: boolean;
  transcript: string;
    startListening: (languageOrOptions?: string | { language?: string; lastAssistantMessage?: string; replySuggestions?: string[] }) => void;
  stopListening: () => void;
  sttError: string | null;
  isSpeechRecognitionSupported: boolean;
  clearTranscript: () => void;
  speakingUtteranceText: string | null;
  claimRecordedUtterance: () => RecordedUtterance | null;
  hasPendingQueueItems: () => boolean;
}

const useBrowserSpeech = (props?: UseBrowserSpeechProps): UseBrowserSpeechReturn => {
    const { onEngineCycleEnd, onSpeechQueueCompleted } = props || {};
  
  const [isListening, setIsListening] = useState(false);
  const [transcript, setTranscript] = useState('');
  const [sttError, setSttError] = useState<string | null>(null);
    const isSpeechRecognitionSupported = typeof navigator !== 'undefined' && !!navigator.mediaDevices?.getUserMedia;
  const sttInterruptedByTTS = useRef(false);
  
  // Use refs for callbacks to avoid re-triggering effects on prop change
  const onEngineCycleEndRef = useRef(onEngineCycleEnd);
  useEffect(() => { onEngineCycleEndRef.current = onEngineCycleEnd; }, [onEngineCycleEnd]);

  // Gemini Live STT Hook
  const geminiStt = useGeminiLiveStt();
  
  // Sync local transcript with Gemini transcript when active
    useEffect(() => {
        if (geminiStt.isListening) {
            setTranscript(geminiStt.transcript);
        }
    }, [geminiStt.transcript, geminiStt.isListening]);

  // Sync listening state and error from Gemini
  useEffect(() => {
      setIsListening(geminiStt.isListening);
      if (geminiStt.error) setSttError(geminiStt.error);
  }, [geminiStt.isListening, geminiStt.error]);


  const pauseSttForPlayback = useCallback(() => {
    const shouldBeListening = props?.isGlobalSttEnabled?.() ?? false;

    if (isListening || shouldBeListening) {
        sttInterruptedByTTS.current = true;
        geminiStt.stop();
    }
  }, [isListening, geminiStt, props]);

    const { isSpeaking, speak, stopSpeaking, speakingUtteranceText, isSpeechSynthesisSupported, hasPendingQueueItems } = useTtsEngine({
        pauseSttForPlayback,
        onQueueComplete: () => {
            onSpeechQueueCompleted?.();
            // Auto-resume logic
            if (sttInterruptedByTTS.current && props?.isGlobalSttEnabled?.()) {
                sttInterruptedByTTS.current = false;
                if (props.getGlobalSttLanguage) {
                    // Small delay to ensure audio context is clean
                    setTimeout(() => {
                        if (props.isGlobalSttEnabled?.()) {
                            startListening(props.getGlobalSttLanguage!());
                        }
                    }, 100);
                }
            } else {
                // If not globally enabled anymore, clear interruption flag
                sttInterruptedByTTS.current = false;
            }
        }
    });

    const wasListeningRef = useRef(false);
    useEffect(() => {
        if (wasListeningRef.current && !geminiStt.isListening) {
            if (!sttInterruptedByTTS.current) {
                onEngineCycleEndRef.current?.(!!geminiStt.error);
            }
        }
        wasListeningRef.current = geminiStt.isListening;
    }, [geminiStt.isListening, geminiStt.error]);

  const startListening = useCallback((languageOrOptions?: string | { language?: string; lastAssistantMessage?: string; replySuggestions?: string[] }) => {
      // If TTS is speaking, we don't start immediately but ensure the interrupt flag is set 
      // so it resumes after TTS finishes.
      if (isSpeaking || hasPendingQueueItems()) {
          sttInterruptedByTTS.current = true;
          return;
      }
      geminiStt.start(languageOrOptions);
  }, [isSpeaking, hasPendingQueueItems, geminiStt]);

  const stopListening = useCallback(() => {
      sttInterruptedByTTS.current = false;
      geminiStt.stop();
  }, [geminiStt]);

  const clearTranscript = useCallback(() => {
      setTranscript('');
  }, []);
  
  const claimRecordedUtterance = useCallback(() => {
      const pcm = geminiStt.getRecordedAudio();
      if (pcm && pcm.length > 0) {
          const wavBase64 = pcmToWav(pcm, 16000);
          return {
              dataUrl: wavBase64,
              provider: 'gemini',
              langCode: props?.getGlobalSttLanguage ? props.getGlobalSttLanguage() : 'en',
              transcript: transcript,
          } as RecordedUtterance;
      }
      return null;
  }, [geminiStt, props, transcript]);
  
  // Store geminiStt.stop in a ref so unmount effect doesn't depend on geminiStt identity
  const geminiSttStopRef = useRef(geminiStt.stop);
  geminiSttStopRef.current = geminiStt.stop;
  
  // Cleanup on unmount - ensure STT is stopped
  useEffect(() => {
    return () => {
      geminiSttStopRef.current();
    };
  }, []); // Empty deps - only runs on unmount

  return {
      isSpeaking, speak, stopSpeaking, isSpeechSynthesisSupported,
      isListening, transcript, startListening, stopListening, sttError,
      isSpeechRecognitionSupported, clearTranscript, speakingUtteranceText, claimRecordedUtterance,
      hasPendingQueueItems
  };
};

export default useBrowserSpeech;
