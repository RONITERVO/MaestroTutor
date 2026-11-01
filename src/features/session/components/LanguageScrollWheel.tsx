// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useRef, useEffect, useLayoutEffect, useCallback } from 'react';
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
    const contentRef = useRef<HTMLDivElement>(null);
    const itemRefs = useRef<Map<string, HTMLButtonElement>>(new Map());
    const scrollTimeoutRef = useRef<number | null>(null);
    const isScrollingProgrammatically = useRef(false);
    const isTouchingRef = useRef(false);
    const scrollPendingRef = useRef(false);
    const activePointersRef = useRef(new Set<number>());
    const resizePendingRef = useRef(false);
    const resizeFrameRef = useRef<number | null>(null);
    const programmaticResetTimeoutRef = useRef<number | null>(null);
    const currentPropsRef = useRef({ languages, selectedValue, onSelect, onInteract, disabled });
    const variantClasses = getVariantClasses(variant);

    // The globe redraws while animating and supplies fresh arrays/callbacks.
    // Keep pending native interactions alive while using the latest props.
    useLayoutEffect(() => {
        currentPropsRef.current = { languages, selectedValue, onSelect, onInteract, disabled };
    });

    const cancelScrollSettlement = useCallback(() => {
        scrollPendingRef.current = false;
        if (scrollTimeoutRef.current !== null) {
            window.clearTimeout(scrollTimeoutRef.current);
            scrollTimeoutRef.current = null;
        }
    }, []);

    const clearProgrammaticScroll = useCallback(() => {
        isScrollingProgrammatically.current = false;
        if (programmaticResetTimeoutRef.current !== null) {
            window.clearTimeout(programmaticResetTimeoutRef.current);
            programmaticResetTimeoutRef.current = null;
        }
    }, []);

    const scrollToPosition = useCallback((container: HTMLDivElement, scrollTop: number) => {
        resizePendingRef.current = false;
        const nextScrollTop = Math.max(0, scrollTop);
        if (Math.abs(container.scrollTop - nextScrollTop) <= 1) return;
        clearProgrammaticScroll();
        isScrollingProgrammatically.current = true;
        container.scrollTop = nextScrollTop;
        programmaticResetTimeoutRef.current = window.setTimeout(() => {
            isScrollingProgrammatically.current = false;
            programmaticResetTimeoutRef.current = null;
        }, 120);
    }, [clearProgrammaticScroll]);

    const centerElement = useCallback((container: HTMLDivElement, element: HTMLElement) => {
        scrollToPosition(container, getCenteredScrollTop(container, element));
    }, [scrollToPosition]);

    const centerAfterResize = useCallback(() => {
        if (!resizePendingRef.current || scrollPendingRef.current || isTouchingRef.current || activePointersRef.current.size > 0) return;
        resizePendingRef.current = false;
        const container = scrollContainerRef.current;
        const selected = currentPropsRef.current.selectedValue;
        const element = selected ? itemRefs.current.get(selected.langCode) : null;
        if (container && element) centerElement(container, element);
    }, [centerElement]);

    const scheduleResizeCenter = useCallback(() => {
        if (!resizePendingRef.current || resizeFrameRef.current !== null) return;
        // Let pointerup's following click choose its own row before restoring
        // an idle selection. Only one deferred measurement is ever queued.
        resizeFrameRef.current = window.requestAnimationFrame(() => {
            resizeFrameRef.current = null;
            centerAfterResize();
        });
    }, [centerAfterResize]);

    const selectedCode = selectedValue?.langCode ?? null;
    const languageLayout = JSON.stringify(languages.map(language => [language.langCode, language.displayName, language.flag]));
    useLayoutEffect(() => {
        // Only a changed controlled choice or actual row layout owns recentering.
        // An unrelated parent render must not move an option between down/up.
        cancelScrollSettlement();
        clearProgrammaticScroll();
        const container = scrollContainerRef.current;
        if (!container) return;
        const selectedElement = selectedCode ? itemRefs.current.get(selectedCode) : null;
        if (selectedElement) centerElement(container, selectedElement);
        else if (!selectedCode) scrollToPosition(container, 0);
    }, [selectedCode, languageLayout, disabled, cancelScrollSettlement, clearProgrammaticScroll, centerElement, scrollToPosition]);

    useEffect(() => {
        const container = scrollContainerRef.current;
        const content = contentRef.current;
        if (!container || !content || typeof ResizeObserver === 'undefined') return;
        // cqw text and padding can change row positions without changing props.
        // Observe the viewport and its content, not every language row.
        const observer = new ResizeObserver(() => {
            resizePendingRef.current = true;
            centerAfterResize();
        });
        observer.observe(container);
        observer.observe(content);
        return () => observer.disconnect();
    }, [centerAfterResize]);

    const handleScrollEnd = useCallback(() => {
        const props = currentPropsRef.current;
        if (props.disabled || !scrollPendingRef.current || isScrollingProgrammatically.current || isTouchingRef.current) return;
        cancelScrollSettlement();
        const container = scrollContainerRef.current;
        if (!container) return;
        const scrollCenter = container.scrollTop + container.clientHeight / 2;
        let closest: LanguageDefinition | null = null;
        let minDistance = Infinity;
        for (const language of props.languages) {
            const element = itemRefs.current.get(language.langCode);
            if (!element) continue;
            const distance = Math.abs(scrollCenter - (element.offsetTop + element.offsetHeight / 2));
            if (distance < minDistance) {
                minDistance = distance;
                closest = language;
            }
        }
        if (!closest) return;
        const element = itemRefs.current.get(closest.langCode);
        if (element) centerElement(container, element);
        props.onInteract?.();
        if (closest.langCode !== props.selectedValue?.langCode) props.onSelect(closest);
    }, [cancelScrollSettlement, centerElement]);

    const scheduleScrollEnd = useCallback(() => {
        if (!scrollPendingRef.current || isScrollingProgrammatically.current) return;
        if (scrollTimeoutRef.current !== null) window.clearTimeout(scrollTimeoutRef.current);
        // Keep the fallback even when scrollend is advertised but not delivered.
        scrollTimeoutRef.current = window.setTimeout(handleScrollEnd, 250);
    }, [handleScrollEnd]);

    const beginUserInteraction = useCallback(() => {
        if (currentPropsRef.current.disabled) return;
        // A new deliberate gesture supersedes the centering suppression window.
        clearProgrammaticScroll();
        currentPropsRef.current.onInteract?.();
    }, [clearProgrammaticScroll]);

    const selectExplicitly = useCallback((language: LanguageDefinition) => {
        const props = currentPropsRef.current;
        if (props.disabled) return;
        cancelScrollSettlement();
        clearProgrammaticScroll();
        const container = scrollContainerRef.current;
        const element = itemRefs.current.get(language.langCode);
        if (container && element) centerElement(container, element);
        props.onInteract?.();
        props.onSelect(language);
    }, [cancelScrollSettlement, clearProgrammaticScroll, centerElement]);

    useEffect(() => {
        const container = scrollContainerRef.current;
        if (!container) return;
        const supportsScrollEnd = 'onscrollend' in window;
        const onScroll = () => {
            if (currentPropsRef.current.disabled || isScrollingProgrammatically.current) return;
            scrollPendingRef.current = true;
            scheduleScrollEnd();
        };
        const onTouchStart = () => {
            isTouchingRef.current = true;
            cancelScrollSettlement();
            beginUserInteraction();
        };
        const onTouchEnd = () => {
            isTouchingRef.current = false;
            scheduleScrollEnd();
            scheduleResizeCenter();
        };
        const onPointerRelease = (event: PointerEvent) => {
            activePointersRef.current.delete(event.pointerId);
            scheduleResizeCenter();
        };
        const onBlur = () => {
            activePointersRef.current.clear();
            isTouchingRef.current = false;
            cancelScrollSettlement();
            scheduleResizeCenter();
        };
        container.addEventListener('scroll', onScroll, { passive: true });
        if (supportsScrollEnd) container.addEventListener('scrollend', handleScrollEnd);
        container.addEventListener('touchstart', onTouchStart, { passive: true });
        container.addEventListener('touchend', onTouchEnd, { passive: true });
        container.addEventListener('touchcancel', onTouchEnd, { passive: true });
        window.addEventListener('pointerup', onPointerRelease, true);
        window.addEventListener('pointercancel', onPointerRelease, true);
        window.addEventListener('blur', onBlur);
        return () => {
            container.removeEventListener('scroll', onScroll);
            if (supportsScrollEnd) container.removeEventListener('scrollend', handleScrollEnd);
            container.removeEventListener('touchstart', onTouchStart);
            container.removeEventListener('touchend', onTouchEnd);
            container.removeEventListener('touchcancel', onTouchEnd);
            window.removeEventListener('pointerup', onPointerRelease, true);
            window.removeEventListener('pointercancel', onPointerRelease, true);
            window.removeEventListener('blur', onBlur);
            if (resizeFrameRef.current !== null) window.cancelAnimationFrame(resizeFrameRef.current);
            resizeFrameRef.current = null;
            resizePendingRef.current = false;
            activePointersRef.current.clear();
            isTouchingRef.current = false;
            cancelScrollSettlement();
            clearProgrammaticScroll();
        };
    }, [beginUserInteraction, cancelScrollSettlement, clearProgrammaticScroll, handleScrollEnd, scheduleScrollEnd, scheduleResizeCenter]);

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
                onPointerDown={(event) => {
                    if (disabled) return;
                    activePointersRef.current.add(event.pointerId);
                    beginUserInteraction();
                }}
                onKeyDown={(event) => {
                    if (['ArrowUp', 'ArrowDown', 'PageUp', 'PageDown', 'Home', 'End'].includes(event.key)) beginUserInteraction();
                }}
                onWheel={(event) => {
                    event.stopPropagation();
                    if (disabled) return;
                    beginUserInteraction();
                    event.currentTarget.scrollTop += event.deltaY;
                    scrollPendingRef.current = true;
                    scheduleScrollEnd();
                }}
                aria-label={title || (variant === 'native' ? 'Native language' : 'Target language')}
                aria-disabled={disabled || undefined}
                role="listbox"
            >
                <div
                    ref={contentRef}
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
                            onClick={() => selectExplicitly(lang)}
                            disabled={disabled}
                            role="option"
                            aria-selected={isSelected}
                            aria-label={lang.displayName}
                        >
                            <p
                                className={`${variantClasses.text} pointer-events-none whitespace-normal`}
                                style={{ fontSize: '3.55cqw', lineHeight: 1.3 }}
                            >
                                <span className="mr-1.5 drop-shadow-[0_1px_2px_var(--scroll-wheel-flag-shadow-color)]">
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

