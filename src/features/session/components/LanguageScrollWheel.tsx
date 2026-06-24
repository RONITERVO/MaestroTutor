// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useRef, useEffect, useLayoutEffect, useCallback, useState } from 'react';
import { LanguageDefinition } from '../../../core/config/languages';

interface LanguageScrollWheelProps {
  languages: LanguageDefinition[];
  selectedValue: LanguageDefinition | null;
  onSelect: (lang: LanguageDefinition) => void;
  title: string;
  disabled?: boolean;
  onInteract?: () => void;
  variant?: 'native' | 'target';
}

const flagScrollStyle = {
    WebkitMaskImage:
        'linear-gradient(to top, rgba(0,0,0,0.1) 0%, rgba(0,0,0,1) 40%, rgba(0,0,0,1) 60%, rgba(0,0,0,0.7) 65%, rgba(0,0,0,0) 75%, rgba(0,0,0,0) 100%)',
    maskImage:
        'linear-gradient(to top, rgba(0,0,0,0.1) 0%, rgba(0,0,0,1) 40%, rgba(0,0,0,1) 60%, rgba(0,0,0,0.7) 65%, rgba(0,0,0,0) 75%, rgba(0,0,0,0) 100%)',
    clipPath: 'inset(25% 0 0 0)',
    height: '100%',
    overscrollBehavior: 'contain',
    WebkitOverflowScrolling: 'touch',
} as React.CSSProperties;

const getCenteredScrollTop = (container: HTMLElement, item: HTMLElement) => (
    item.offsetTop - (container.clientHeight / 2) + (item.offsetHeight / 2)
);

const getVariantClasses = (variant?: 'native' | 'target') => {
    if (variant === 'native') {
        return {
            text: 'text-attachment-svg-native-text',
        };
    }

    return {
        text: 'text-attachment-svg-target-text',
    };
};

const LanguageScrollWheel: React.FC<LanguageScrollWheelProps> = ({ languages, selectedValue, onSelect, title, disabled, onInteract, variant }) => {
    const scrollContainerRef = useRef<HTMLDivElement>(null);
    const itemRefs = useRef<Map<string, HTMLButtonElement>>(new Map());
    const scrollTimeoutRef = useRef<number | null>(null);
    const isScrollingProgrammatically = useRef(false);
    const isTouchingRef = useRef(false);
    const [isScrolling, setIsScrolling] = useState(false);
    const scrollingTimeoutRef = useRef<number | null>(null);
    const programmaticResetTimeoutRef = useRef<number | null>(null);
    const variantClasses = getVariantClasses(variant);

    const releaseProgrammaticScrollSoon = useCallback(() => {
        if (programmaticResetTimeoutRef.current !== null) {
            window.clearTimeout(programmaticResetTimeoutRef.current);
        }
        programmaticResetTimeoutRef.current = window.setTimeout(() => {
            isScrollingProgrammatically.current = false;
            programmaticResetTimeoutRef.current = null;
        }, 120);
    }, []);

    const centerElement = useCallback((container: HTMLDivElement, element: HTMLElement) => {
        const nextScrollTop = Math.max(0, getCenteredScrollTop(container, element));
        if (Math.abs(container.scrollTop - nextScrollTop) <= 1) return;

        isScrollingProgrammatically.current = true;
        container.scrollTop = nextScrollTop;
        releaseProgrammaticScrollSoon();
    }, [releaseProgrammaticScrollSoon]);

    useLayoutEffect(() => {
        if (isScrolling || isTouchingRef.current) return;

        const container = scrollContainerRef.current;

        if (selectedValue && container) {
            const selectedElement = itemRefs.current.get(selectedValue.langCode);
            if (selectedElement) {
                centerElement(container, selectedElement);
            }
        } else if (!selectedValue && container) {
            if (container.scrollTop <= 1) return;
            isScrollingProgrammatically.current = true;
            container.scrollTop = 0;
            releaseProgrammaticScrollSoon();
        }
    });

    useEffect(() => {
        return () => {
            if (programmaticResetTimeoutRef.current !== null) {
                window.clearTimeout(programmaticResetTimeoutRef.current);
            }
        };
    }, []);

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
                const itemTop = itemEl.offsetTop;
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
            const newSelectedElement = itemRefs.current.get(newSelectedLang.langCode);
            if (newSelectedElement) {
                centerElement(container, newSelectedElement);
            }
            if (newSelectedLang.langCode !== selectedValue?.langCode) {
                onSelect(newSelectedLang);
            }
        }
    }, [centerElement, languages, onSelect, selectedValue]);

    const handleScrollEnd = useCallback(() => {
        if (isScrollingProgrammatically.current || isTouchingRef.current) return;
        setIsScrolling(false);
        onInteract?.();
        selectClosestItem();
    }, [onInteract, selectClosestItem]);

    const scheduleScrollEnd = useCallback(() => {
        if (isScrollingProgrammatically.current) return;
        if (scrollTimeoutRef.current) clearTimeout(scrollTimeoutRef.current);
        // Keep a timer-based fallback even when scrollend exists because
        // some mobile browsers report support but do not fire it reliably.
        scrollTimeoutRef.current = window.setTimeout(handleScrollEnd, 250);
    }, [handleScrollEnd]);

    const handleTouchStart = useCallback(() => {
        isTouchingRef.current = true;
        if (scrollTimeoutRef.current) clearTimeout(scrollTimeoutRef.current);
        onInteract?.();
    }, [onInteract]);

    const handleTouchEnd = useCallback(() => {
        isTouchingRef.current = false;
        // Schedule selection after touch ends and scroll momentum settles
        scheduleScrollEnd();
    }, [scheduleScrollEnd]);
    
    useEffect(() => {
        const container = scrollContainerRef.current;
        if (!container) return;

        const supportsScrollEnd = 'onscrollend' in window;
        
        const onScroll = () => {
            if (!isScrollingProgrammatically.current) {
                setIsScrolling(true);
                // Clear any existing scrolling timeout
                if (scrollingTimeoutRef.current) clearTimeout(scrollingTimeoutRef.current);
                scrollingTimeoutRef.current = window.setTimeout(() => setIsScrolling(false), 300);
            }
            scheduleScrollEnd();
        };
        
        const onScrollEnd = () => {
            if (scrollTimeoutRef.current) {
                clearTimeout(scrollTimeoutRef.current);
                scrollTimeoutRef.current = null;
            }
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

    return (
        <div
            className={[
                'maestro-language-flag-wheel relative h-full min-h-0 w-full text-center',
                disabled ? 'opacity-35' : '',
            ].filter(Boolean).join(' ')}
            data-variant={variant}
        >
            {title && <p className="sr-only">{title}</p>}
            <div
                ref={scrollContainerRef}
                className={[
                    'relative h-full min-h-0 overflow-y-auto scrollbar-hide pointer-events-auto',
                    disabled ? 'pointer-events-none' : '',
                ].filter(Boolean).join(' ')}
                style={flagScrollStyle}
                onPointerDown={() => onInteract?.()}
                onWheel={(event) => {
                    event.stopPropagation();
                    if (!disabled) {
                        event.currentTarget.scrollTop += event.deltaY;
                    }
                    onInteract?.();
                }}
                aria-label={title || (variant === 'native' ? 'Native language' : 'Target language')}
                aria-disabled={disabled || undefined}
                role="listbox"
            >
                <div
                    className="flex flex-col items-center justify-start"
                    style={{
                        paddingTop: '8cqw',
                        paddingBottom: '8cqw',
                    }}
                >
                <div
                    aria-hidden
                    role="presentation"
                    className="text-center p-1 w-full opacity-0 select-none pointer-events-none"
                >
                    <p
                        className={variantClasses.text}
                        style={{ fontSize: '3.55cqw', lineHeight: 1.3 }}
                    >
                        {'\u00A0'}
                    </p>
                </div>
                {languages.map(lang => {
                    const isSelected = lang.langCode === selectedValue?.langCode;
                    return (
                        <button
                            type="button"
                            key={lang.langCode}
                            ref={el => {
                                if (el) {
                                    itemRefs.current.set(lang.langCode, el);
                                } else {
                                    itemRefs.current.delete(lang.langCode);
                                }
                            }}
                            className={[
                                'text-center p-1 w-full',
                                'transition-all duration-300 transform-gpu outline-none',
                                'cursor-pointer disabled:cursor-default',
                                isSelected ? 'opacity-100 scale-105' : 'opacity-70 scale-100',
                            ].join(' ')}
                            onClick={() => { if (!disabled) { onInteract?.(); onSelect(lang); } }}
                            disabled={disabled}
                            role="option"
                            aria-selected={isSelected}
                            aria-label={lang.displayName}
                        >
                            <p
                                className={`${variantClasses.text} pointer-events-none whitespace-normal`}
                                style={{ fontSize: '3.55cqw', lineHeight: 1.3 }}
                            >
                                <span className="mr-1.5 drop-shadow-[0_1px_2px_rgba(0,0,0,0.22)]">
                                    {lang.flag}
                                </span>
                                <span>{lang.displayName}</span>
                            </p>
                        </button>
                    );
                })}
                <div
                    aria-hidden
                    role="presentation"
                    className="text-center p-1 w-full opacity-0 select-none pointer-events-none"
                >
                    <p
                        className={variantClasses.text}
                        style={{ fontSize: '3.55cqw', lineHeight: 1.3 }}
                    >
                        {'\u00A0'}
                    </p>
                </div>
                </div>
            </div>
        </div>
    );
};

export default LanguageScrollWheel;

