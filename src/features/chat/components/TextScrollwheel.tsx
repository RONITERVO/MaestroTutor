
import React, { useRef, useEffect, useMemo, useState } from 'react';
import { TranslationReplacements } from '../../i18n/index';
import { SpeechPart } from '../../types';

interface TextScrollwheelProps {
  translations: Array<{ spanish: string; english: string; }>;
  speakingUtteranceText: string | null;
  currentTargetLangCode: string;
  currentNativeLangCode: string;
  t: (key: string, replacements?: TranslationReplacements) => string;
  isSpeakDisabled: boolean;
  speakText: (textOrParts: SpeechPart[], defaultLang: string) => void;
  stopSpeaking: () => void;
  speakNativeLang: boolean;
  onToggleSpeakNativeLang: () => void;
  messageId?: string;
}

const TextScrollwheel: React.FC<TextScrollwheelProps> = React.memo(({ translations, speakingUtteranceText, currentTargetLangCode, currentNativeLangCode, t, isSpeakDisabled, speakText, stopSpeaking, speakNativeLang, onToggleSpeakNativeLang, messageId }) => {
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
      target: { type: 'target' as const, text: pair.spanish, lang: currentTargetLangCode },
      native: { type: 'native' as const, text: pair.english, lang: currentNativeLangCode },
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
            if (isSpeakDisabled) {
              stopSpeaking();
              return;
            }
            const startPairIdx = pairIndexByFlatIndex[flatIndex] ?? 0;
            const parts: SpeechPart[] = [];
            const baseContext = messageId ? { source: 'message' as const, messageId } : { source: 'adHoc' as const };
            for (let i = startPairIdx; i < translations.length; i++) {
              const pair = translations[i];
              const t = pair.spanish?.trim();
              const n = pair.english?.trim();
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


  return (
      <div
        ref={scrollContainerRef}
        onScroll={handleScroll}
        className="overflow-y-auto relative scrollbar-hide"
        style={{
          WebkitMaskImage:
            'linear-gradient(to top, rgba(0,0,0,0.1) 0%, rgba(0,0,0,1) 40%, rgba(0,0,0,1) 60%, rgba(0,0,0,0.7) 65%, rgba(0,0,0,0) 75%, rgba(0,0,0,0) 100%)',
          maskImage:
            'linear-gradient(to top, rgba(0,0,0,0.1) 0%, rgba(0,0,0,1) 40%, rgba(0,0,0,1) 60%, rgba(0,0,0,0.7) 65%, rgba(0,0,0,0) 75%, rgba(0,0,0,0) 100%)',
          height: '33cqw',
          // @ts-ignore
          containerType: 'inline-size'
        }}
        aria-label={t('chat.maestroTranscriptScrollwheel')}
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
          <div
            className="flex flex-col items-center justify-start"
            style={{
              paddingTop: '8cqw',
              paddingBottom: '8cqw'
            }}
          > 
              <div
                aria-hidden
                role="presentation"
                className="text-center p-1 w-full opacity-0 select-none pointer-events-none"
              >
                <p
                  className="italic text-gray-300"
                  style={{ fontSize: '3.55cqw', lineHeight: 1.3 }}
                >
                  \u00A0
                </p>
              </div>
              {allLinePairs.map((line, index) => ( 
                <div 
                  key={index} 
                  ref={el => { itemRefs.current[index] = el; }} 
                  className={`text-center p-1 w-full transition-all duration-300 transform-gpu cursor-pointer ${ index === activeIndex ? 'opacity-100 scale-105' : 'opacity-70 scale-100'}`}
                  onPointerDown={handleLinePointerDown}
                  onPointerUp={(e) => handleLinePointerUp(e, line, index)}
                  onPointerLeave={handlePointerLeave}
                  onContextMenu={(e) => e.preventDefault()}
                > 
                  <p 
                    className={`${line.type === 'target' ? 'font-semibold text-white' : 'italic text-gray-300'} pointer-events-none`}
                    style={{
                      fontSize: line.type === 'target' ? '4cqw' : '3.55cqw',
                      lineHeight: 1.3
                    }}
                  > 
                    {line.text}
                    {line.type === 'native' && index === flashIndex && (
                      <span className="ml-2 inline-block align-middle animate-speak-flash">
                        {flashIsOn ? '🔊' : '🔇'}
                      </span>
                    )}
                  </p> 
                </div> 
              ))}
              <div
                aria-hidden
                role="presentation"
                className="text-center p-1 w-full opacity-0 select-none pointer-events-none"
              >
                <p
                  className="italic text-gray-300"
                  style={{ fontSize: '3.55cqw', lineHeight: 1.3 }}
                >
                  \u00A0
                </p>
              </div>
          </div>
      </div>
  );
});
TextScrollwheel.displayName = 'TextScrollwheel';

export default TextScrollwheel;
