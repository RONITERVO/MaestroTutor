// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useRef, useState, useMemo, useCallback, useEffect } from 'react';
import { LanguageDefinition, ALL_LANGUAGES, hasSharedFlag } from '../../../core/config/languages';
import { TranslationReplacements } from '../../../core/i18n/index';
import LanguageScrollWheel from './LanguageScrollWheel';

interface LanguageSelectorGlobeProps {
    nativeLangCode: string | null;
    targetLangCode: string | null;
    onSelectNative: (code: string | null) => void;
    onSelectTarget: (code: string | null) => void;
    onConfirm: () => void;
    t: (key: string, replacements?: TranslationReplacements) => string;
    onInteract: () => void;
}

// Number of flags visible at once on the spiral ring
const VISIBLE_FLAGS = 12;

const LanguageSelectorGlobe: React.FC<LanguageSelectorGlobeProps> = ({
    nativeLangCode,
    targetLangCode,
    onSelectNative,
    onSelectTarget,
    onConfirm,
    t,
    onInteract
}) => {
    const nativeLang = ALL_LANGUAGES.find(l => l.langCode === nativeLangCode) || null;
    const targetLang = ALL_LANGUAGES.find(l => l.langCode === targetLangCode) || null;
    const [hoveredLang, setHoveredLang] = useState<LanguageDefinition | null>(null);
    const globeRef = useRef<HTMLDivElement>(null);
    const flagRefs = useRef<Map<string, HTMLButtonElement>>(new Map());
    
    // Spiral rotation offset (0 to 1 represents one full cycle through all languages)
    const [spiralOffset, setSpiralOffset] = useState(0);
    const isDraggingRef = useRef(false);
    const lastPointerYRef = useRef(0);
    const velocityRef = useRef(0);
    const animationFrameRef = useRef<number | null>(null);

    // Calculate which flags are currently visible in the spiral window
    const visibleFlags = useMemo(() => {
        const total = ALL_LANGUAGES.length;
        const centerIndex = Math.floor(spiralOffset * total) % total;
        const halfVisible = Math.floor(VISIBLE_FLAGS / 2);
        
        const flags: { lang: LanguageDefinition; index: number; position: number }[] = [];
        
        for (let i = -halfVisible; i <= halfVisible; i++) {
            let idx = (centerIndex + i + total) % total;
            flags.push({
                lang: ALL_LANGUAGES[idx],
                index: idx,
                position: i // -6 to +6 for 12 visible flags
            });
        }
        
        return flags;
    }, [spiralOffset]);

    // Get position on the circular edge based on relative position in the visible window
    const getPosition = useCallback((position: number) => {
        // Map position (-halfVisible to +halfVisible) to angle on circle
        const normalizedPos = position / (VISIBLE_FLAGS / 2); // -1 to 1
        const angle = normalizedPos * Math.PI; // -PI to PI (half circle, top portion)
        const adjustedAngle = angle - Math.PI / 2; // Start from top
        const radius = 45;
        const x = 50 + radius * Math.cos(adjustedAngle);
        const y = 50 + radius * Math.sin(adjustedAngle);
        
        // Calculate depth for 3D spiral effect (flags at edges are "further away")
        const depth = Math.cos(normalizedPos * Math.PI * 0.5); // 1 at center, 0 at edges
        
        return { x, y, depth };
    }, []);

    // Get the actual position of a language (for drawing connection lines)
    const getLanguagePosition = useCallback((langCode: string) => {
        const visibleFlag = visibleFlags.find(f => f.lang.langCode === langCode);
        if (visibleFlag) {
            return getPosition(visibleFlag.position);
        }
        // If not visible, calculate where it would be if we scrolled to it
        const idx = ALL_LANGUAGES.findIndex(l => l.langCode === langCode);
        const total = ALL_LANGUAGES.length;
        const centerIndex = Math.floor(spiralOffset * total) % total;
        const distance = ((idx - centerIndex + total) % total);
        const adjustedDistance = distance > total / 2 ? distance - total : distance;
        
        // Return position outside visible area
        const angle = (adjustedDistance / (VISIBLE_FLAGS / 2)) * Math.PI - Math.PI / 2;
        const radius = 45;
        return {
            x: 50 + radius * Math.cos(angle),
            y: 50 + radius * Math.sin(angle),
            depth: 0
        };
    }, [visibleFlags, spiralOffset, getPosition]);

    const nativePos = nativeLang ? getLanguagePosition(nativeLang.langCode) : null;
    const targetPos = targetLang ? getLanguagePosition(targetLang.langCode) : null;

    const pathD = useMemo(() => {
        if (!nativePos || !targetPos) return "";
        const controlX = 50;
        const controlY = 50;
        return `M ${nativePos.x} ${nativePos.y} Q ${controlX} ${controlY} ${targetPos.x} ${targetPos.y}`;
    }, [nativePos, targetPos]);

    // Sync spiral offset when scroll wheel selection changes
    useEffect(() => {
        const selectedLang = targetLang || nativeLang;
        if (selectedLang) {
            const idx = ALL_LANGUAGES.findIndex(l => l.langCode === selectedLang.langCode);
            if (idx !== -1) {
                const targetOffset = idx / ALL_LANGUAGES.length;
                setSpiralOffset(targetOffset);
            }
        }
    }, [nativeLang, targetLang]);

    // Handle wheel scroll on the globe to rotate spiral
    const handleWheel = useCallback((e: React.WheelEvent) => {
        e.preventDefault();
        onInteract();
        const delta = e.deltaY > 0 ? 0.02 : -0.02;
        setSpiralOffset(prev => {
            let next = prev + delta;
            // Wrap around
            if (next < 0) next += 1;
            if (next >= 1) next -= 1;
            return next;
        });
    }, [onInteract]);

    // Handle drag to rotate spiral
    const handlePointerDown = useCallback((e: React.PointerEvent) => {
        isDraggingRef.current = true;
        lastPointerYRef.current = e.clientY;
        velocityRef.current = 0;
        if (animationFrameRef.current) {
            cancelAnimationFrame(animationFrameRef.current);
            animationFrameRef.current = null;
        }
        onInteract();
        (e.target as HTMLElement).setPointerCapture?.(e.pointerId);
    }, [onInteract]);

    const handlePointerMove = useCallback((e: React.PointerEvent) => {
        if (!isDraggingRef.current) return;
        const deltaY = e.clientY - lastPointerYRef.current;
        lastPointerYRef.current = e.clientY;
        
        // Convert pixel movement to spiral rotation
        const sensitivity = 0.002;
        velocityRef.current = deltaY * sensitivity;
        
        setSpiralOffset(prev => {
            let next = prev + velocityRef.current;
            if (next < 0) next += 1;
            if (next >= 1) next -= 1;
            return next;
        });
    }, []);

    const handlePointerUp = useCallback((e: React.PointerEvent) => {
        isDraggingRef.current = false;
        (e.target as HTMLElement).releasePointerCapture?.(e.pointerId);
        
        // Apply momentum/inertia
        const applyMomentum = () => {
            if (Math.abs(velocityRef.current) < 0.0005) {
                velocityRef.current = 0;
                return;
            }
            
            velocityRef.current *= 0.95; // Friction
            setSpiralOffset(prev => {
                let next = prev + velocityRef.current;
                if (next < 0) next += 1;
                if (next >= 1) next -= 1;
                return next;
            });
            
            animationFrameRef.current = requestAnimationFrame(applyMomentum);
        };
        
        if (Math.abs(velocityRef.current) > 0.001) {
            animationFrameRef.current = requestAnimationFrame(applyMomentum);
        }
    }, []);

    // Cleanup animation frame on unmount
    useEffect(() => {
        return () => {
            if (animationFrameRef.current) {
                cancelAnimationFrame(animationFrameRef.current);
            }
        };
    }, []);

    const handleFlagClick = (lang: LanguageDefinition) => {
        onInteract();
        if (!nativeLang) {
            onSelectNative(lang.langCode);
        } else if (!targetLang && lang.langCode !== nativeLang.langCode) {
            onSelectTarget(lang.langCode);
        } else if (lang.langCode === nativeLang.langCode) {
            onSelectNative(null);
        } else if (lang.langCode === targetLang?.langCode) {
            onSelectTarget(null);
        } else {
            onSelectTarget(lang.langCode);
        }
    };

    // When scroll wheel selects a language, scroll spiral to show it
    const handleScrollWheelSelect = useCallback((lang: LanguageDefinition, isNative: boolean) => {
        const idx = ALL_LANGUAGES.findIndex(l => l.langCode === lang.langCode);
        if (idx !== -1) {
            setSpiralOffset(idx / ALL_LANGUAGES.length);
        }
        if (isNative) {
            onSelectNative(lang.langCode);
        } else {
            onSelectTarget(lang.langCode);
        }
    }, [onSelectNative, onSelectTarget]);

    return (
        <div className="w-full flex justify-center py-2">
            <style>{`
                @keyframes fly-in-bubble {
                    from { offset-distance: 0%; }
                    to { offset-distance: 100%; }
                }
                .animate-fly-in-bubble {
                    animation: fly-in-bubble 2.5s ease-in-out forwards;
                    offset-path: path(var(--flight-path));
                }
                .spiral-flag {
                    transition: transform 0.15s ease-out, opacity 0.15s ease-out;
                }
            `}</style>

            <div className="relative w-full max-w-[20rem] aspect-square">
                <div
                    ref={globeRef}
                    className="globe-bg absolute inset-0 border-2 flex items-center justify-center bg-primary text-white overflow-hidden shadow-inner touch-none sketchy-border"
                    onPointerDown={handlePointerDown}
                    onPointerMove={handlePointerMove}
                    onPointerUp={handlePointerUp}
                    onPointerCancel={handlePointerUp}
                    onPointerLeave={() => { if (isDraggingRef.current) handlePointerUp({} as React.PointerEvent); }}
                    onWheel={handleWheel}
                >
                {/* Spiral indicator - shows current position in the full language list */}
                <div className="absolute top-2 left-1/2 -translate-x-1/2 text-xs text-muted-foreground opacity-60 pointer-events-none">
                    {Math.floor(spiralOffset * ALL_LANGUAGES.length) + 1} / {ALL_LANGUAGES.length}
                </div>

                <div
                    className="absolute inset-0 flex items-center justify-center pointer-events-none"
                >
                    <div
                        className="pointer-events-auto w-[70%] max-w-[10rem] bg-primary/60 backdrop-blur-md px-3 py-2 transition-opacity duration-200 opacity-40 hover:opacity-100 focus-within:opacity-100 active:opacity-100 shadow-lg border border-white/10 sketchy-border-thin"
                    >
                        <div className="flex justify-center items-start gap-3">
                            <LanguageScrollWheel
                                languages={ALL_LANGUAGES}
                                selectedValue={nativeLang}
                                onSelect={(l) => handleScrollWheelSelect(l, true)}
                                onInteract={onInteract}
                                title=""
                                variant="native"
                            />
                            <div className="w-px h-24 bg-white/30 self-center"></div>
                            <LanguageScrollWheel
                                languages={ALL_LANGUAGES.filter(l => l.langCode !== nativeLang?.langCode)}
                                selectedValue={targetLang}
                                onSelect={(l) => handleScrollWheelSelect(l, false)}
                                disabled={!nativeLang}
                                onInteract={onInteract}
                                title=""
                                variant="target"
                            />
                        </div>
                    </div>
                </div>
                
                {/* Spiral flags - only render visible ones */}
                {visibleFlags.map(({ lang, position }) => {
                    const pos = getPosition(position);
                    const isNative = nativeLang?.langCode === lang.langCode;
                    const isTarget = targetLang?.langCode === lang.langCode;
                    const showShortCode = hasSharedFlag(lang);
                    
                    // Scale and opacity based on depth (center flags are larger/more visible)
                    const scale = 0.6 + pos.depth * 0.6; // 0.6 to 1.2
                    const opacity = 0.4 + pos.depth * 0.6; // 0.4 to 1.0
                    
                    return (
                        <button
                            key={lang.langCode}
                            ref={el => { if (el) flagRefs.current.set(lang.langCode, el); else flagRefs.current.delete(lang.langCode); }}
                            className="spiral-flag absolute transform -translate-x-1/2 -translate-y-1/2 flex flex-col items-center justify-center p-1 z-10 sketchy-border-thin"
                            style={{ 
                                top: `${pos.y}%`, 
                                left: `${pos.x}%`,
                                transform: `translate(-50%, -50%) scale(${scale})`,
                                opacity: opacity,
                                zIndex: Math.floor(pos.depth * 10) + 10
                            }}
                            onClick={() => handleFlagClick(lang)}
                            onMouseEnter={() => setHoveredLang(lang)}
                            onMouseLeave={() => setHoveredLang(null)}
                            title={lang.displayName}
                        >
                            <span className={`text-lg leading-none transition-transform duration-200 ${hoveredLang?.langCode === lang.langCode || isNative || isTarget ? 'scale-125' : 'scale-100'}`}>{lang.flag}</span>
                            {showShortCode && (
                                <span className="text-[8px] font-bold text-white/90 leading-none mt-0.5 drop-shadow-sm">{lang.shortCode}</span>
                            )}
                            <div className={`absolute -inset-1 border-2 transition-all duration-300 pointer-events-none sketchy-border-thin ${
                                isNative ? 'border-watercolor shadow-watercolor/50 shadow-lg' :
                                isTarget ? 'border-green-400 shadow-green-400/50 shadow-lg' :
                                'border-transparent'
                            }`}></div>
                        </button>
                    );
                })}

                {/* Scroll hint arrows */}
                <div className="absolute left-2 top-1/2 -translate-y-1/2 text-white/30 pointer-events-none text-lg">◀</div>
                <div className="absolute right-2 top-1/2 -translate-y-1/2 text-white/30 pointer-events-none text-lg">▶</div>
                </div>

                {/* Flight path and plane - rendered outside globe to always be on top */}
                {pathD && (
                    <svg key={pathD} viewBox="0 0 100 100" className="absolute inset-0 w-full h-full overflow-visible pointer-events-none z-50">
                        <path d={pathD} stroke="rgba(255,255,255,0.3)" strokeWidth="2" fill="none" strokeDasharray="5,5" />
                        <g 
                            style={{'--flight-path': `"${pathD}"`} as React.CSSProperties} 
                            className="animate-fly-in-bubble cursor-pointer pointer-events-auto"
                            onClick={() => onConfirm()}
                        >
                            <title>{t('startPage.clickToStart')}</title>
                            <text
                                className={nativeLang && targetLang ? 'animate-pulse' : ''}
                                fontSize="24"
                                dominantBaseline="middle"
                                textAnchor="middle"
                            >
                                ✈️
                            </text>
                        </g>
                    </svg>
                )}
            </div>
        </div>
    );
};

export default LanguageSelectorGlobe;
