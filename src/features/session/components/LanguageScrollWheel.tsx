
import React, { useRef, useEffect, useCallback } from 'react';
import { LanguageDefinition } from '../../../core/config/languages';

interface LanguageScrollWheelProps {
  languages: LanguageDefinition[];
  selectedValue: LanguageDefinition | null;
  onSelect: (lang: LanguageDefinition) => void;
  title: string;
  disabled?: boolean;
  onInteract?: () => void;
}

const LanguageScrollWheel: React.FC<LanguageScrollWheelProps> = ({ languages, selectedValue, onSelect, title, disabled, onInteract }) => {
    const scrollContainerRef = useRef<HTMLDivElement>(null);
    const itemRefs = useRef<Map<string, HTMLDivElement>>(new Map());
    const scrollTimeoutRef = useRef<number | null>(null);
    const isScrollingProgrammatically = useRef(false);

    useEffect(() => {
        if (selectedValue && scrollContainerRef.current) {
            const selectedElement = itemRefs.current.get(selectedValue.langCode);
            if (selectedElement) {
                isScrollingProgrammatically.current = true;
                selectedElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
                setTimeout(() => { isScrollingProgrammatically.current = false; }, 500);
            }
        } else if (!selectedValue && scrollContainerRef.current) {
            isScrollingProgrammatically.current = true;
            scrollContainerRef.current.scrollTop = 0;
            setTimeout(() => { isScrollingProgrammatically.current = false; }, 300);
        }
    }, [selectedValue]);

  const handleScrollEnd = useCallback(() => {
        if (isScrollingProgrammatically.current) return;
        const container = scrollContainerRef.current;
        if (!container) return;
        onInteract?.();
        
        const scrollTop = container.scrollTop;
        const containerHeight = container.offsetHeight;
        const scrollCenter = scrollTop + (containerHeight / 2);
        
        let closestIndex = -1;
        let minDistance = Infinity;

        languages.forEach((lang, index) => {
            const itemEl = itemRefs.current.get(lang.langCode);
            if (itemEl) {
                const itemTop = itemEl.offsetTop - container.offsetTop;
                const itemHeight = itemEl.offsetHeight;
                const itemCenter = itemTop + itemHeight / 2;
                const distance = Math.abs(scrollCenter - itemCenter);

                if (distance < minDistance) {
                    minDistance = distance;
                    closestIndex = index;
                }
            }
        });

        if (closestIndex > -1) {
            const newSelectedLang = languages[closestIndex];
            if (newSelectedLang.langCode !== selectedValue?.langCode) {
                 onSelect(newSelectedLang);
            }
        }
    }, [languages, onSelect, selectedValue, onInteract]);

  const debouncedScrollHandler = useCallback(() => {
    if (isScrollingProgrammatically.current) return;
    if (scrollTimeoutRef.current) clearTimeout(scrollTimeoutRef.current);
    scrollTimeoutRef.current = window.setTimeout(handleScrollEnd, 150);
  }, [handleScrollEnd]);
    
    useEffect(() => {
        const container = scrollContainerRef.current;
        container?.addEventListener('scroll', debouncedScrollHandler);
        return () => {
            container?.removeEventListener('scroll', debouncedScrollHandler);
            if (scrollTimeoutRef.current) clearTimeout(scrollTimeoutRef.current);
        }
    }, [debouncedScrollHandler]);

    return (
        <div className={`flex-1 text-center relative ${disabled ? 'opacity-50' : ''}`}>
            {title && <p className="text-xs text-slate-400 mb-1 h-4">{title}</p>}
            <div 
                ref={scrollContainerRef}
                className={`h-28 overflow-y-auto relative scrollbar-hide ${disabled ? 'pointer-events-none' : ''}`}
                style={{
                    scrollSnapType: 'y mandatory',
                    WebkitMaskImage: 'linear-gradient(to bottom, transparent, black 25%, black 75%, transparent)',
                    maskImage: 'linear-gradient(to bottom, transparent, black 25%, black 75%, transparent)',
                }}
                onPointerDown={() => onInteract?.()}
            >
                <div className="h-[calc(50%-1.5rem)]"></div>
                {languages.map(lang => {
                    const isSelected = lang.langCode === selectedValue?.langCode;
                    return (
                        <div
                            key={lang.langCode}
                            ref={el => { if (el) itemRefs.current.set(lang.langCode, el) }}
                            className={`flex items-center justify-center h-12 transition-all duration-200 ease-out`}
                            style={{ scrollSnapAlign: 'center' }}
                            onClick={() => { if (!disabled) { onInteract?.(); onSelect(lang); } }}
                        >
                            <span className={`text-sm font-semibold flex items-center gap-2 cursor-pointer transition-all duration-200 ${isSelected ? 'opacity-100 scale-110' : 'opacity-60 scale-90'}`}>
                                {lang.flag} {lang.displayName}
                            </span>
                        </div>
                    );
                })}
                <div className="h-[calc(50%-1.5rem)]"></div>
            </div>
        </div>
    );
};

export default LanguageScrollWheel;
