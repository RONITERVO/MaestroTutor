
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { generateSpeech } from '../../../api/gemini';
import type { SpeechPart, TtsProvider, SpeechCacheDetails } from '../../../core/types';

export interface UseTtsEngineOptions {
  getTtsProvider?: () => TtsProvider;
  onQueueComplete?: () => void;
  onQueueStart?: () => void;
  pauseSttForPlayback?: () => void;
}

interface SpeechQueueItem {
  id: string;
  text: string;
  langCode: string;
  voiceName?: string;
  provider: TtsProvider;
  audioDataUrl?: string;
  cachedAudio?: string;
  fetching?: boolean;
  onAudioCached?: (audioDataUrl: string, details: SpeechCacheDetails) => void;
  cacheKey?: string;
  cacheNotified?: boolean;
}

export interface UseTtsEngineReturn {
  isSpeechSynthesisSupported: boolean;
  isSpeaking: boolean;
  speakingUtteranceText: string | null;
  speak: (textOrParts: string | SpeechPart[], defaultLang: string) => void;
  stopSpeaking: () => void;
  hasPendingQueueItems: () => boolean;
}

export const useTtsEngine = (options?: UseTtsEngineOptions): UseTtsEngineReturn => {
  const { getTtsProvider, onQueueComplete, onQueueStart, pauseSttForPlayback } = options || {};
  const isSpeechSynthesisSupported = typeof Audio !== 'undefined' || typeof window.speechSynthesis !== 'undefined';
  const [isSpeaking, setIsSpeaking] = useState(false);
  const [speakingUtteranceText, setSpeakingUtteranceText] = useState<string | null>(null);
  
  const queueRef = useRef<SpeechQueueItem[]>([]);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const queueIdRef = useRef(0);
  
  const callbacksRef = useRef({ onQueueComplete, onQueueStart, pauseSttForPlayback });
  useEffect(() => { callbacksRef.current = { onQueueComplete, onQueueStart, pauseSttForPlayback }; }, [onQueueComplete, onQueueStart, pauseSttForPlayback]);

  const handleQueueComplete = useCallback(() => {
    setIsSpeaking(false);
    setSpeakingUtteranceText(null);
    callbacksRef.current.onQueueComplete?.();
  }, []);

  const processSpeechQueue = useCallback(async () => {
    const queue = queueRef.current;
    if (queue.length === 0) {
      if (!audioRef.current) handleQueueComplete();
      return;
    }

    const current = queue[0];
    const removeCurrent = () => {
      if (queue.length && queue[0].id === current.id) queue.shift();
    };

    // Use cached audio if available
    if (!current.audioDataUrl && current.cachedAudio) {
      current.audioDataUrl = current.cachedAudio;
    }

    // Generate audio if needed (Gemini provider)
    if (current.provider === 'gemini' && !current.audioDataUrl) {
      if (current.fetching) return; // wait for fetch
      current.fetching = true;
      try {
        const { audioBase64, mimeType } = await generateSpeech({ 
          text: current.text, 
          voiceName: current.voiceName 
        });
        if (audioBase64) {
          const dataUrl = `data:${mimeType || 'audio/wav'};base64,${audioBase64}`;
          current.audioDataUrl = dataUrl;
          if (current.onAudioCached && !current.cacheNotified) {
            current.onAudioCached(dataUrl, { cacheKey: current.cacheKey, provider: 'gemini', langCode: current.langCode, fromCache: false });
            current.cacheNotified = true;
          }
        }
      } catch (e) {
        console.error('TTS Generation failed', e);
        // Fallback to browser TTS for this item if generation fails
        current.provider = 'browser';
      } finally {
        current.fetching = false;
        processSpeechQueue(); 
        return;
      }
    }

    // Playback logic
    if (audioRef.current || (window.speechSynthesis.speaking && current.provider === 'browser')) return; // already playing

    setIsSpeaking(true);
    setSpeakingUtteranceText(current.text);
    callbacksRef.current.pauseSttForPlayback?.();

    if (current.provider === 'gemini' && current.audioDataUrl) {
        const audio = new Audio(current.audioDataUrl);
        audioRef.current = audio;
        audio.onended = () => {
            audioRef.current = null;
            removeCurrent();
            processSpeechQueue();
        };
        audio.onerror = () => {
            audioRef.current = null;
            removeCurrent();
            processSpeechQueue();
        };
        try { await audio.play(); } catch { audioRef.current = null; removeCurrent(); processSpeechQueue(); }
    } else {
        // Browser fallback
        const u = new SpeechSynthesisUtterance(current.text);
        u.lang = current.langCode;
        u.onend = () => {
            removeCurrent();
            processSpeechQueue();
        };
        u.onerror = () => {
            removeCurrent();
            processSpeechQueue();
        };
        window.speechSynthesis.speak(u);
    }
  }, [handleQueueComplete]);

  const speak = useCallback((textOrParts: string | SpeechPart[], defaultLang: string) => {
    const parts: SpeechPart[] = typeof textOrParts === 'string' ? [{ text: textOrParts, langCode: defaultLang }] : textOrParts;
    const provider = typeof getTtsProvider === 'function' ? getTtsProvider() : 'gemini';
    
    if (queueRef.current.length === 0) callbacksRef.current.onQueueStart?.();

    parts.forEach(p => {
        if (!p.text.trim()) return;
        queueRef.current.push({
            id: `tts-${Date.now()}-${queueIdRef.current++}`,
            text: p.text,
            langCode: p.langCode || defaultLang,
            voiceName: p.voiceName,
            provider,
            cacheKey: p.cacheKey,
            cachedAudio: p.cachedAudio,
            onAudioCached: p.onAudioCached
        });
    });
    processSpeechQueue();
  }, [getTtsProvider, processSpeechQueue]);

  const stopSpeaking = useCallback(() => {
    if (audioRef.current) {
        audioRef.current.pause();
        audioRef.current = null;
    }
    window.speechSynthesis.cancel();
    queueRef.current = [];
    handleQueueComplete();
  }, [handleQueueComplete]);

  const hasPendingQueueItems = useCallback(() => queueRef.current.length > 0, []);

  return useMemo(() => ({
    isSpeechSynthesisSupported,
    isSpeaking,
    speakingUtteranceText,
    speak,
    stopSpeaking,
    hasPendingQueueItems
  }), [isSpeechSynthesisSupported, isSpeaking, speakingUtteranceText, speak, stopSpeaking, hasPendingQueueItems]);
};
