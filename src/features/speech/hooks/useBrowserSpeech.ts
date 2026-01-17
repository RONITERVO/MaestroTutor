
// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useState, useEffect, useCallback, useRef } from 'react';
import type { SpeechPart, TtsProvider, SttProvider, RecordedUtterance } from '../types';
import { useTtsEngine } from './speech/useTtsEngine';
import { useGeminiLiveStt } from './speech/useGeminiLiveStt';
import { pcmToWav } from '../utils/audioProcessing';

interface UseBrowserSpeechProps {
  onEngineCycleEnd?: (errorOccurred: boolean) => void;
  isGlobalSttEnabled?: () => boolean;
  getGlobalSttLanguage?: () => string;
  onSpeechQueueCompleted?: () => void;
  getTtsProvider?: () => TtsProvider;
  getSttProvider?: () => SttProvider;
  onRecordedUtteranceReady?: (utterance: RecordedUtterance) => void;
}

interface UseBrowserSpeechReturn {
  isSpeaking: boolean;
  speak: (textOrParts: string | SpeechPart[], defaultLang: string) => void;
  stopSpeaking: () => void;
  isSpeechSynthesisSupported: boolean;
  isListening: boolean;
  transcript: string;
  startListening: (lang: string) => void;
  stopListening: () => void;
  sttError: string | null;
  isSpeechRecognitionSupported: boolean;
  clearTranscript: () => void;
  speakingUtteranceText: string | null;
  claimRecordedUtterance: () => RecordedUtterance | null;
  hasPendingQueueItems: () => boolean;
}

const useBrowserSpeech = (props?: UseBrowserSpeechProps): UseBrowserSpeechReturn => {
  const { onEngineCycleEnd, onSpeechQueueCompleted, getTtsProvider, getSttProvider } = props || {};
  
  const [isListening, setIsListening] = useState(false);
  const [transcript, setTranscript] = useState('');
  const [sttError, setSttError] = useState<string | null>(null);
  const recognitionRef = useRef<any>(null);
  const isSpeechRecognitionSupported = typeof window !== 'undefined' && (!!window.SpeechRecognition || !!window.webkitSpeechRecognition);
  const sttInterruptedByTTS = useRef(false);
  
  // Use refs for callbacks to avoid re-triggering effects on prop change
  const onEngineCycleEndRef = useRef(onEngineCycleEnd);
  useEffect(() => { onEngineCycleEndRef.current = onEngineCycleEnd; }, [onEngineCycleEnd]);

  // Gemini Live STT Hook
  const geminiStt = useGeminiLiveStt();
  
  // Sync local transcript with Gemini transcript when active
  useEffect(() => {
    const provider = getSttProvider ? getSttProvider() : 'browser';
    if (provider === 'gemini' && geminiStt.isListening) {
       setTranscript(geminiStt.transcript);
    }
  }, [geminiStt.transcript, geminiStt.isListening, getSttProvider]);

  // Sync listening state and error from Gemini
  useEffect(() => {
     const provider = getSttProvider ? getSttProvider() : 'browser';
     if (provider === 'gemini') {
        setIsListening(geminiStt.isListening);
        if (geminiStt.error) setSttError(geminiStt.error);
     }
  }, [geminiStt.isListening, geminiStt.error, getSttProvider]);


  const pauseSttForPlayback = useCallback(() => {
    const provider = getSttProvider ? getSttProvider() : 'browser';
    const shouldBeListening = props?.isGlobalSttEnabled?.() ?? false;

    if (isListening || shouldBeListening) {
        sttInterruptedByTTS.current = true;
        if (provider === 'gemini') {
            geminiStt.stop();
        } else if (recognitionRef.current) {
            try { recognitionRef.current.stop(); } catch {}
        }
    }
  }, [isListening, getSttProvider, geminiStt, props]);

  const { isSpeaking, speak, stopSpeaking, speakingUtteranceText, isSpeechSynthesisSupported, hasPendingQueueItems } = useTtsEngine({
      getTtsProvider,
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

  const errorOccurredRef = useRef(false);

  useEffect(() => {
      if (isSpeechRecognitionSupported) {
          const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
          const rec = new SpeechRecognition();
          rec.continuous = true;
          rec.interimResults = true;
          
          rec.onstart = () => { 
              setIsListening(true); 
              setSttError(null); 
              errorOccurredRef.current = false;
          };
          
          rec.onend = () => { 
              const provider = getSttProvider ? getSttProvider() : 'browser';
              if (provider === 'browser') {
                  setIsListening(false); 
                  if (!sttInterruptedByTTS.current) {
                      onEngineCycleEndRef.current?.(errorOccurredRef.current);
                  }
              }
          };
          
          rec.onerror = (e: any) => { 
              const provider = getSttProvider ? getSttProvider() : 'browser';
              if (provider === 'browser') {
                  if (e.error === 'no-speech') {
                      return;
                  }
                  setSttError(e.error); 
                  errorOccurredRef.current = true;
                  // Let onend handle the state update
              }
          };
          
          rec.onresult = (e: any) => {
              const provider = getSttProvider ? getSttProvider() : 'browser';
              if (provider === 'browser') {
                  let final = '';
                  let interim = '';
                  for (let i = e.resultIndex; i < e.results.length; ++i) {
                      if (e.results[i].isFinal) final += e.results[i][0].transcript;
                      else interim += e.results[i][0].transcript;
                  }
                  setTranscript(final || interim);
              }
          };
          
          recognitionRef.current = rec;
          
          return () => {
              if (rec) rec.abort();
          };
      }
  }, [isSpeechRecognitionSupported, getSttProvider]);

  const startListening = useCallback((lang: string) => {
      const provider = getSttProvider ? getSttProvider() : 'browser';
      
      // If TTS is speaking, we don't start immediately but ensure the interrupt flag is set 
      // so it resumes after TTS finishes.
      if (isSpeaking || hasPendingQueueItems()) {
          sttInterruptedByTTS.current = true;
          return;
      }

      if (provider === 'gemini') {
          geminiStt.start(lang);
          return;
      }

      // Browser STT
      if (!recognitionRef.current) return;
      try {
          // If already started, stop first to reset language if needed
          if (isListening) {
             recognitionRef.current.stop(); 
          }
          recognitionRef.current.lang = lang;
          recognitionRef.current.start();
      } catch (e: any) {
          // Ignore "already started" errors
          if (e?.name !== 'InvalidStateError') {
             console.warn("STT start error", e);
          }
      }
  }, [isSpeaking, hasPendingQueueItems, getSttProvider, geminiStt, isListening]);

  const stopListening = useCallback(() => {
      sttInterruptedByTTS.current = false;
      const provider = getSttProvider ? getSttProvider() : 'browser';
      if (provider === 'gemini') {
          geminiStt.stop();
      } else if (recognitionRef.current) {
          recognitionRef.current.stop();
      }
  }, [getSttProvider, geminiStt]);

  const clearTranscript = useCallback(() => {
      setTranscript('');
  }, []);
  
  const claimRecordedUtterance = useCallback(() => {
      const provider = getSttProvider ? getSttProvider() : 'browser';
      if (provider === 'gemini') {
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
      }
      return null;
  }, [getSttProvider, geminiStt, props, transcript]);

  return {
      isSpeaking, speak, stopSpeaking, isSpeechSynthesisSupported,
      isListening, transcript, startListening, stopListening, sttError,
      isSpeechRecognitionSupported, clearTranscript, speakingUtteranceText, claimRecordedUtterance,
      hasPendingQueueItems
  };
};

export default useBrowserSpeech;
