// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { streamGeminiLiveTts } from '../services/geminiLiveTts';
import { pcmToWav } from '../utils/audioProcessing';
import type { SpeechPart, TtsProvider, SpeechCacheDetails } from '../../../core/types';

export interface UseTtsEngineOptions {
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
  const { onQueueComplete, onQueueStart, pauseSttForPlayback } = options || {};
  const isSpeechSynthesisSupported = typeof window !== 'undefined' && (!!(window.AudioContext || (window as any).webkitAudioContext) || typeof Audio !== 'undefined');
  const [isSpeaking, setIsSpeaking] = useState(false);
  const [speakingUtteranceText, setSpeakingUtteranceText] = useState<string | null>(null);
  
  const queueRef = useRef<SpeechQueueItem[]>([]);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const geminiLiveActiveRef = useRef(false);
  const queueIdRef = useRef(0);
  const liveAbortRef = useRef<AbortController | null>(null);
  
  // Track if context is being created to prevent concurrent creation
  const creatingContextRef = useRef<boolean>(false);
  
  const callbacksRef = useRef({ onQueueComplete, onQueueStart, pauseSttForPlayback });
  useEffect(() => { callbacksRef.current = { onQueueComplete, onQueueStart, pauseSttForPlayback }; }, [onQueueComplete, onQueueStart, pauseSttForPlayback]);

  const handleQueueComplete = useCallback(() => {
    setIsSpeaking(false);
    setSpeakingUtteranceText(null);
    callbacksRef.current.onQueueComplete?.();
  }, []);

  /**
   * Get or create the AudioContext for Gemini Live TTS streaming.
   * Ensures proper cleanup of closed contexts before creating new ones.
   */
  const getAudioContext = useCallback(async (): Promise<AudioContext> => {
    // Clean up closed context before creating a new one
    if (audioContextRef.current && audioContextRef.current.state === 'closed') {
      audioContextRef.current = null;
    }
    
    if (!audioContextRef.current) {
      // Prevent concurrent context creation
      if (creatingContextRef.current) {
        // Wait for the context being created
        await new Promise(resolve => setTimeout(resolve, 50));
        // After waiting, context may have been created by another call
        // Use type assertion since TypeScript doesn't track ref mutations across async boundaries
        const waitedCtx = audioContextRef.current as AudioContext | null;
        if (waitedCtx) {
          if (waitedCtx.state === 'suspended') {
            await waitedCtx.resume();
          }
          return waitedCtx;
        }
      }
      
      creatingContextRef.current = true;
      try {
        const AudioContextCtor = window.AudioContext || (window as any).webkitAudioContext;
        audioContextRef.current = new AudioContextCtor({ sampleRate: 24000 });
      } finally {
        creatingContextRef.current = false;
      }
    }
    
    // At this point, audioContextRef.current is guaranteed to be set
    const ctx = audioContextRef.current as AudioContext;
    if (ctx.state === 'suspended') {
      await ctx.resume();
    }
    return ctx;
  }, []);

  /**
   * Process the queue using Gemini Live TTS streaming.
   * Batches all queued items into a single session for better performance.
   */
  const processGeminiLiveQueue = useCallback(async () => {
    if (geminiLiveActiveRef.current) return; // Already processing
    
    const queue = queueRef.current;
    if (queue.length === 0) {
      handleQueueComplete();
      return;
    }

    // Batch consecutive gemini-live items at the front that are not cached
    const geminiLiveItems: SpeechQueueItem[] = [];
    for (const item of queue) {
      if (item.provider !== 'gemini-live') break;
      if (item.audioDataUrl || item.cachedAudio) break;
      geminiLiveItems.push(item);
    }
    if (geminiLiveItems.length === 0) {
      // No gemini-live items ready
      return;
    }

    geminiLiveActiveRef.current = true;
    setIsSpeaking(true);
    callbacksRef.current.pauseSttForPlayback?.();

    try {
      const audioContext = await getAudioContext();
      
      const lines = geminiLiveItems.map(item => ({
        text: item.text,
        langCode: item.langCode,
        voiceName: item.voiceName,
        cacheKey: item.cacheKey,
        onAudioCached: item.onAudioCached ? (audioDataUrl: string) => {
          if (item.onAudioCached && !item.cacheNotified) {
            item.onAudioCached(audioDataUrl, { 
              cacheKey: item.cacheKey, 
              provider: 'gemini-live', 
              langCode: item.langCode, 
              fromCache: false 
            });
            item.cacheNotified = true;
          }
        } : undefined
      }));

      const abortController = new AbortController();
      liveAbortRef.current = abortController;
      await streamGeminiLiveTts({
        lines,
        audioContext,
        voiceName: geminiLiveItems[0]?.voiceName,
        abortSignal: abortController.signal,
        onLineStart: (_lineIndex, text) => {
          setSpeakingUtteranceText(text);
        },
        onLineComplete: (lineIndex, audioPcm) => {
          // Cache the audio for this line
          const item = geminiLiveItems[lineIndex];
          if (item && audioPcm && audioPcm.length > 0) {
            const audioDataUrl = pcmToWav(audioPcm, 24000);
            if (item.onAudioCached && !item.cacheNotified) {
              item.onAudioCached(audioDataUrl, { 
                cacheKey: item.cacheKey, 
                provider: 'gemini-live', 
                langCode: item.langCode, 
                fromCache: false 
              });
              item.cacheNotified = true;
            }

          }
        },
        onError: (error) => {
          console.error('[GeminiLiveTts] Error:', error);
        }
      });

      // Remove all processed gemini-live items from queue
      queueRef.current = queueRef.current.filter(item => !geminiLiveItems.includes(item));
      
    } catch (e) {
      console.error('[GeminiLiveTts] Exception:', e);
    } finally {
      geminiLiveActiveRef.current = false;
      liveAbortRef.current = null;
      
      // Process any remaining items (non-gemini-live or fallback)
      if (queueRef.current.length > 0) {
        processSpeechQueue();
      } else {
        handleQueueComplete();
      }
    }
  }, [getAudioContext, handleQueueComplete]);

  const processSpeechQueue = useCallback(async () => {
    const queue = queueRef.current;
    if (queue.length === 0) {
      if (!audioRef.current && !geminiLiveActiveRef.current) handleQueueComplete();
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

    // Route to Gemini Live TTS if no cached audio
    if (current.provider !== 'gemini-live') {
      current.provider = 'gemini-live';
    }
    if (!current.audioDataUrl) {
      processGeminiLiveQueue();
      return;
    }

    // Playback logic
    if (audioRef.current) return; // already playing

    setIsSpeaking(true);
    setSpeakingUtteranceText(current.text);
    callbacksRef.current.pauseSttForPlayback?.();

    if (current.audioDataUrl) {
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
      }
  }, [handleQueueComplete, processGeminiLiveQueue]);

  const speak = useCallback((textOrParts: string | SpeechPart[], defaultLang: string) => {
    const parts: SpeechPart[] = typeof textOrParts === 'string' ? [{ text: textOrParts, langCode: defaultLang }] : textOrParts;
    const provider: TtsProvider = 'gemini-live';
    
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
  }, [processSpeechQueue]);

  const stopSpeaking = useCallback(() => {
    // Stop HTML Audio element
    if (audioRef.current) {
        audioRef.current.pause();
        audioRef.current = null;
    }
    if (geminiLiveActiveRef.current) {
      try { liveAbortRef.current?.abort(); } catch {}
      geminiLiveActiveRef.current = false;
      liveAbortRef.current = null;
    }
    // Stop Gemini Live TTS (suspend AudioContext to stop playback)
    if (audioContextRef.current && audioContextRef.current.state === 'running') {
      try { audioContextRef.current.suspend(); } catch {}
    }
    if (audioContextRef.current) {
      try { audioContextRef.current.close(); } catch {}
      audioContextRef.current = null;
    }
    geminiLiveActiveRef.current = false;
    // Clear queue
    queueRef.current = [];
    handleQueueComplete();
  }, [handleQueueComplete]);
  
  // Cleanup on unmount
  useEffect(() => {
    return () => {
      // Abort any active live session
      if (liveAbortRef.current) {
        try { liveAbortRef.current.abort(); } catch {}
        liveAbortRef.current = null;
      }
      // Stop audio element
      if (audioRef.current) {
        try { audioRef.current.pause(); } catch {}
        audioRef.current = null;
      }
      // Close audio context
      if (audioContextRef.current) {
        try { audioContextRef.current.close(); } catch {}
        audioContextRef.current = null;
      }
      // Clear queue
      queueRef.current = [];
    };
  }, []);

  const hasPendingQueueItems = useCallback(() => queueRef.current.length > 0 || geminiLiveActiveRef.current, []);

  return useMemo(() => ({
    isSpeechSynthesisSupported,
    isSpeaking,
    speakingUtteranceText,
    speak,
    stopSpeaking,
    hasPendingQueueItems
  }), [isSpeechSynthesisSupported, isSpeaking, speakingUtteranceText, speak, stopSpeaking, hasPendingQueueItems]);
};
