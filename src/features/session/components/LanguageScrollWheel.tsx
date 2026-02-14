// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useRef, useEffect, useCallback, useState } from 'react';
import { LanguageDefinition, hasSharedFlag } from '../../../core/config/languages';

interface LanguageScrollWheelProps {
  languages: LanguageDefinition[];
  selectedValue: LanguageDefinition | null;
  onSelect: (lang: LanguageDefinition) => void;
  title: string;
  disabled?: boolean;
  onInteract?: () => void;
  variant?: 'native' | 'target';
}

const LanguageScrollWheel: React.FC<LanguageScrollWheelProps> = ({ languages, selectedValue, onSelect, title, disabled, onInteract, variant }) => {
    const scrollContainerRef = useRef<HTMLDivElement>(null);
    const itemRefs = useRef<Map<string, HTMLDivElement>>(new Map());
    const scrollTimeoutRef = useRef<number | null>(null);
    const isScrollingProgrammatically = useRef(false);
    const isTouchingRef = useRef(false);
    const [isScrolling, setIsScrolling] = useState(false);
    const scrollingTimeoutRef = useRef<number | null>(null);

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

    const selectClosestItem = useCallback(() => {
        if (isScrollingProgrammatically.current) return;
        const container = scrollContainerRef.current;
        if (!container) return;
        
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
    }, [languages, onSelect, selectedValue]);

    const handleScrollEnd = useCallback(() => {
        if (isScrollingProgrammatically.current || isTouchingRef.current) return;
        setIsScrolling(false);
        onInteract?.();
        selectClosestItem();
    }, [onInteract, selectClosestItem]);

    const scheduleScrollEnd = useCallback(() => {
        if (isScrollingProgrammatically.current) return;
        if (scrollTimeoutRef.current) clearTimeout(scrollTimeoutRef.current);
        // Use longer timeout for better mobile scroll momentum handling
        scrollTimeoutRef.current = window.setTimeout(handleScrollEnd, 250);
    }, [handleScrollEnd]);

    const handleTouchStart = useCallback(() => {
        isTouchingRef.current = true;
        if (scrollTimeoutRef.current) clearTimeout(scrollTimeoutRef.current);
    }, []);

    const handleTouchEnd = useCallback(() => {
        isTouchingRef.current = false;
        // Schedule selection after touch ends and scroll momentum settles
        scheduleScrollEnd();
    }, [scheduleScrollEnd]);
    
    useEffect(() => {
        const container = scrollContainerRef.current;
        if (!container) return;

        // Use scrollend event if supported (modern browsers)
        const supportsScrollEnd = 'onscrollend' in window;
        
        const onScroll = () => {
            if (!isScrollingProgrammatically.current) {
                setIsScrolling(true);
                // Clear any existing scrolling timeout
                if (scrollingTimeoutRef.current) clearTimeout(scrollingTimeoutRef.current);
                scrollingTimeoutRef.current = window.setTimeout(() => setIsScrolling(false), 300);
            }
            if (!supportsScrollEnd) {
                scheduleScrollEnd();
            }
        };
        
        const onScrollEnd = () => {
            if (!isTouchingRef.current) {
                handleScrollEnd();
            }
        };

        container.addEventListener('scroll', onScroll, { passive: true });
        if (supportsScrollEnd) {
            container.addEventListener('scrollend', onScrollEnd);
        }
        container.addEventListener('touchstart', handleTouchStart, { passive: true });
        container.addEventListener('touchend', handleTouchEnd, { passive: true });
        container.addEventListener('touchcancel', handleTouchEnd, { passive: true });

        return () => {
            container.removeEventListener('scroll', onScroll);
            if (supportsScrollEnd) {
                container.removeEventListener('scrollend', onScrollEnd);
            }
            container.removeEventListener('touchstart', handleTouchStart);
            container.removeEventListener('touchend', handleTouchEnd);
            container.removeEventListener('touchcancel', handleTouchEnd);
            if (scrollTimeoutRef.current) clearTimeout(scrollTimeoutRef.current);
            if (scrollingTimeoutRef.current) clearTimeout(scrollingTimeoutRef.current);
        };
    }, [scheduleScrollEnd, handleScrollEnd, handleTouchStart, handleTouchEnd]);

    const scrollingBorderClass = isScrolling && variant
        ? variant === 'native'
            ? 'ring-2 ring-watercolor shadow-watercolor/30 shadow-md'
            : 'ring-2 ring-green-500 shadow-green-500/30 shadow-md'
        : '';

    return (
        <div className={`flex-1 text-center relative min-w-[3rem] ${disabled ? 'opacity-50' : ''}`}>
            {title && <p className="text-xs text-muted-foreground mb-1 h-4">{title}</p>}
            <div className={`transition-all duration-150 sketchy-border-thin ${scrollingBorderClass}`}>
                <div 
                    ref={scrollContainerRef}
                    className={`h-32 overflow-y-auto relative scrollbar-hide ${disabled ? 'pointer-events-none' : ''}`}
                    style={{
                        scrollSnapType: 'y mandatory',
                        WebkitMaskImage: 'linear-gradient(to bottom, transparent, black 20%, black 80%, transparent)',
                        maskImage: 'linear-gradient(to bottom, transparent, black 20%, black 80%, transparent)',
                    }}
                    onPointerDown={() => onInteract?.()}
                >
                <div className="h-[calc(50%-1.25rem)]"></div>
                {languages.map(lang => {
                    const isSelected = lang.langCode === selectedValue?.langCode;
                    const showShortCode = hasSharedFlag(lang);
                    return (
                        <div
                            key={lang.langCode}
                            ref={el => { if (el) itemRefs.current.set(lang.langCode, el) }}
                            className={`flex items-center justify-center h-10 transition-all duration-200 ease-out`}
                            style={{ scrollSnapAlign: 'center' }}
                            onClick={() => { if (!disabled) { onInteract?.(); onSelect(lang); } }}
                        >
                            <span className={`text-2xl font-semibold flex items-center gap-0.5 cursor-pointer transition-all duration-200 ${isSelected ? 'opacity-100 scale-125' : 'opacity-50 scale-100'}`}>
                                {lang.flag}
                                {showShortCode && <span className="text-[9px] text-primary-foreground/50 font-bold ml-0.5">{lang.shortCode}</span>}
                            </span>
                        </div>
                    );
                })}
                <div className="h-[calc(50%-1.25rem)]"></div>
                </div>
            </div>
        </div>
    );
};

export default LanguageScrollWheel;
