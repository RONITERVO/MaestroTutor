// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useRef, useEffect, useMemo, useState } from 'react';
import { TranslationReplacements } from '../../../core/i18n/index';
import { SpeechPart } from '../../../core/types';
import { IconSpeaker, IconVolumeOff } from '../../../shared/ui/Icons';
import AttachmentTextScrollContainer from './AttachmentTextScrollContainer';

interface TextScrollwheelProps {
  translations: Array<{ target: string; native: string; }>;
  speakingUtteranceText: string | null;
  currentTargetLangCode: string;
  currentNativeLangCode: string;
  t: (key: string, replacements?: TranslationReplacements) => string;
  isSpeaking: boolean;
  isSending: boolean;
  speakText: (textOrParts: SpeechPart[], defaultLang: string) => void;
  stopSpeaking: () => void;
  speakNativeLang: boolean;
  onToggleSpeakNativeLang: () => void;
  messageId?: string;
  /** `overlay` is for image/PDF overlays, `svg` is for focused SVG shells, `game` is for mini-game overlays, `audio` is for focused audio bubbles. */
  colorMode?: 'overlay' | 'svg' | 'game' | 'audio';
}

const TextScrollwheel: React.FC<TextScrollwheelProps> = React.memo(({ translations, speakingUtteranceText, currentTargetLangCode, currentNativeLangCode, t, isSpeaking, isSending, speakText, stopSpeaking, speakNativeLang, onToggleSpeakNativeLang, messageId, colorMode = 'overlay' }) => {
  const scrollContainerRef = useRef<HTMLDivElement>(null);
  const itemRefs = useRef<(HTMLDivElement | null)[]>([]);
  const isUserScrollingRef = useRef(false);
  const scrollTimeoutRef = useRef<number | null>(null);
  const longPressTimerRef = useRef<number | null>(null);
  const pointerDownPosRef = useRef<{x: number; y: number} | null>(null);
  const [flashIndex, setFlashIndex] = useState<number | null>(null);
  const [flashIsOn, setFlashIsOn] = useState<boolean>(false);
  const flashTimeoutRef = useRef<number | null>(null);

  const { allLinePairs, pairIndexByFlatIndex } = useMemo(() => {
    const pairs = translations.map(pair => ({
      target: { type: 'target' as const, text: pair.target, lang: currentTargetLangCode },
      native: { type: 'native' as const, text: pair.native, lang: currentNativeLangCode },
    }));
    const flat: Array<{ type: 'target'|'native'; text: string; lang: string; counterpart: { text: string; lang: string } | null }> = [];
    const pairIdxByFlat: number[] = [];
    pairs.forEach((p, idx) => {
      const hasTarget = p.target.text && p.target.text.trim();
      const hasNative = p.native.text && p.native.text.trim();
      if (hasTarget) {
        flat.push({ type: 'target', text: p.target.text, lang: p.target.lang, counterpart: hasNative ? { text: p.native.text, lang: p.native.lang } : null });
        pairIdxByFlat.push(idx);
      }
      if (hasNative) {
        flat.push({ type: 'native', text: p.native.text, lang: p.native.lang, counterpart: hasTarget ? { text: p.target.text, lang: p.target.lang } : null });
        pairIdxByFlat.push(idx);
      }
    });
    return { allLinePairs: flat, pairIndexByFlatIndex: pairIdxByFlat };
  }, [translations, currentTargetLangCode, currentNativeLangCode]);

  const activeIndex = useMemo(() => {
      if (!speakingUtteranceText) return -1;
      const cleanedUtterance = speakingUtteranceText.replace(/\*/g, '');
      const index = allLinePairs.findIndex(line => line.text.replace(/\*/g, '') === cleanedUtterance);
      if (index !== -1) isUserScrollingRef.current = false;
      return index;
  }, [speakingUtteranceText, allLinePairs]);

  useEffect(() => {
    if (activeIndex !== -1 && !isUserScrollingRef.current) {
        itemRefs.current[activeIndex]?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  }, [activeIndex]);

  const handleScroll = () => {
    isUserScrollingRef.current = true;
    if (scrollTimeoutRef.current) clearTimeout(scrollTimeoutRef.current);
    scrollTimeoutRef.current = window.setTimeout(() => { isUserScrollingRef.current = false; }, 2000);
  };
  
  useEffect(() => () => { if (scrollTimeoutRef.current) clearTimeout(scrollTimeoutRef.current) }, []);
  
  const handleLinePointerDown = (e: React.PointerEvent) => {
    if (longPressTimerRef.current) clearTimeout(longPressTimerRef.current);
    pointerDownPosRef.current = { x: e.clientX, y: e.clientY };
  };

  const handleLinePointerUp = (e: React.PointerEvent, line: (typeof allLinePairs)[0], flatIndex: number) => {
    if (longPressTimerRef.current) clearTimeout(longPressTimerRef.current);

    if (pointerDownPosRef.current) {
        const deltaX = Math.abs(e.clientX - pointerDownPosRef.current.x);
        const deltaY = Math.abs(e.clientY - pointerDownPosRef.current.y);
        if (deltaX < 10 && deltaY < 10) {
            e.preventDefault();
            if (line.type === 'native') {
              const next = !speakNativeLang;
              setFlashIndex(flatIndex);
              setFlashIsOn(next);
              if (flashTimeoutRef.current) clearTimeout(flashTimeoutRef.current);
              flashTimeoutRef.current = window.setTimeout(() => {
                setFlashIndex(null);
              }, 900);
              onToggleSpeakNativeLang();
              return;
            }
            if (isSpeaking) {
              stopSpeaking();
              return;
            }
            if (isSending) {
              return;
            }
            const startPairIdx = pairIndexByFlatIndex[flatIndex] ?? 0;
            const parts: SpeechPart[] = [];
            const baseContext = messageId ? { source: 'message' as const, messageId } : { source: 'adHoc' as const };
            for (let i = startPairIdx; i < translations.length; i++) {
              const pair = translations[i];
              const t = pair.target?.trim();
              const n = pair.native?.trim();
              if (t) parts.push({ text: t, langCode: currentTargetLangCode, context: baseContext });
              if (speakNativeLang && n) parts.push({ text: n, langCode: currentNativeLangCode, context: baseContext });
            }
            if (parts.length > 0) {
              speakText(parts, parts[0].langCode);
            }
        }
    }
    pointerDownPosRef.current = null;
  };

  const handlePointerLeave = () => {
    if (longPressTimerRef.current) {
      clearTimeout(longPressTimerRef.current);
      longPressTimerRef.current = null;
    }
    pointerDownPosRef.current = null;
  };

  const targetTextClass = colorMode === 'game'
    ? 'text-attachment-game-target-text'
    : colorMode === 'svg'
      ? 'text-attachment-svg-target-text'
    : colorMode === 'audio'
      ? 'text-attachment-audio-target-text'
      : 'text-attachment-overlay-target-text';
  const nativeTextClass = colorMode === 'game'
    ? 'text-attachment-game-native-text'
    : colorMode === 'svg'
      ? 'text-attachment-svg-native-text'
    : colorMode === 'audio'
      ? 'text-attachment-audio-native-text'
      : 'text-attachment-overlay-native-text/70';
  const spacerTextClass = colorMode === 'game'
    ? 'text-attachment-game-native-text/40'
    : colorMode === 'svg'
      ? 'text-attachment-svg-native-text/40'
    : colorMode === 'audio'
      ? 'text-attachment-audio-native-text/40'
      : 'text-attachment-overlay-native-text/40';

  return (
      <AttachmentTextScrollContainer
        ref={scrollContainerRef}
        onScroll={handleScroll}
        onWheel={(e) => e.stopPropagation()}
        onTouchMove={(e) => e.stopPropagation()}
        ariaLabel={t('chat.maestroTranscriptScrollwheel')}
        spacerClassName={spacerTextClass}
      >
          <style>{`
            @keyframes pop-fade-speak {
              0% { transform: scale(0.85); opacity: 0; }
              20% { transform: scale(1.15); opacity: 1; }
              80% { transform: scale(1.0); opacity: 1; }
              100% { transform: scale(0.95); opacity: 0; }
            }
            .animate-speak-flash { animation: pop-fade-speak 900ms ease-out both; }
          `}</style>
              {allLinePairs.map((line, index) => ( 
                <div 
                  key={index} 
                  ref={el => { itemRefs.current[index] = el; }} 
                  className={`text-center p-1 w-full transition-all duration-300 transform-gpu cursor-pointer pointer-events-auto ${ index === activeIndex ? 'opacity-100 scale-105' : 'opacity-70 scale-100'}`}
                  onPointerDown={handleLinePointerDown}
                  onPointerUp={(e) => handleLinePointerUp(e, line, index)}
                  onPointerLeave={handlePointerLeave}
                  onContextMenu={(e) => e.preventDefault()}
                  style={{ touchAction: 'pan-y' }}
                > 
                  <p 
                    className={`${line.type === 'target' ? `font-semibold ${targetTextClass}` : `italic ${nativeTextClass}`} pointer-events-none`}
                    style={{
                      fontSize: line.type === 'target' ? '4cqw' : '3.55cqw',
                      lineHeight: 1.3
                    }}
                  > 
                    {line.text}
                    {line.type === 'native' && index === flashIndex && (
                      <span className="ml-2 inline-block align-middle animate-speak-flash">
                        {flashIsOn ? <IconSpeaker className="w-3 h-3 inline" /> : <IconVolumeOff className="w-3 h-3 inline" />}
                      </span>
                    )}
                  </p> 
                </div> 
              ))}
      </AttachmentTextScrollContainer>
  );
});
TextScrollwheel.displayName = 'TextScrollwheel';

export default TextScrollwheel;
