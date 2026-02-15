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
    onCancel?: () => void;
    t: (key: string, replacements?: TranslationReplacements) => string;
    onInteract: () => void;
}

// ── Fibonacci sphere distribution ──
// Returns evenly-spaced points on a unit sphere
const GOLDEN_ANGLE = Math.PI * (3 - Math.sqrt(5));

function fibonacciSphere(count: number): { lat: number; lng: number }[] {
    const points: { lat: number; lng: number }[] = [];
    for (let i = 0; i < count; i++) {
        const lat = Math.asin(1 - (2 * i) / (count - 1)); // +π/2 → -π/2
        const lng = GOLDEN_ANGLE * i;
        points.push({ lat, lng });
    }
    return points;
}

const LanguageSelectorGlobe: React.FC<LanguageSelectorGlobeProps> = ({
    nativeLangCode,
    targetLangCode,
    onSelectNative,
    onSelectTarget,
    onConfirm: _onConfirm,
    onCancel,
    t: _t,
    onInteract
}) => {
    const nativeLang = ALL_LANGUAGES.find(l => l.langCode === nativeLangCode) || null;
    const targetLang = ALL_LANGUAGES.find(l => l.langCode === targetLangCode) || null;
    const [hoveredLang, setHoveredLang] = useState<LanguageDefinition | null>(null);
    const globeRef = useRef<HTMLDivElement>(null);
    const globeContainerRef = useRef<HTMLDivElement>(null);

    // Time-driven realism: day/night terminator + sun glint
    const [timeAngleDeg, setTimeAngleDeg] = useState(0);
    const [glintPos, setGlintPos] = useState<{ x: number; y: number }>({ x: 45, y: 35 });

    // ── Sphere rotation state (two axes) ──
    const [rotLng, setRotLng] = useState(0); // horizontal rotation (radians)
    const [rotLat, setRotLat] = useState(0); // vertical rotation (radians)

    const isDraggingRef = useRef(false);
    const lastPointerRef = useRef<{ x: number; y: number }>({ x: 0, y: 0 });
    const velocityRef = useRef<{ lng: number; lat: number }>({ lng: 0, lat: 0 });
    const animationFrameRef = useRef<number | null>(null);

    // Pre-compute flag positions on the unit sphere (stable unless language list changes)
    const spherePositions = useMemo(() => fibonacciSphere(ALL_LANGUAGES.length), []);

    // Update day/night and sun glint by local time (subtle, realistic)
    useEffect(() => {
        const update = () => {
            const now = new Date();
            const fraction = (now.getHours() + now.getMinutes() / 60 + now.getSeconds() / 3600) / 24;
            const angle = fraction * 360;
            setTimeAngleDeg(angle);
            const theta = (angle - 90) * (Math.PI / 180);
            const radius = 18;
            setGlintPos({ x: 50 + radius * Math.cos(theta), y: 50 + radius * Math.sin(theta) });
        };
        update();
        const id = setInterval(update, 30000);
        return () => clearInterval(id);
    }, []);

    // ── Project all flags through rotation → 2D ──
    const projectedFlags = useMemo(() => {
        const cosLng = Math.cos(rotLng);
        const sinLng = Math.sin(rotLng);
        const cosLat = Math.cos(rotLat);
        const sinLat = Math.sin(rotLat);

        return ALL_LANGUAGES.map((lang, i) => {
            const { lat, lng } = spherePositions[i];

            // Cartesian on unit sphere
            const px = Math.cos(lat) * Math.sin(lng);
            const py = Math.sin(lat);
            const pz = Math.cos(lat) * Math.cos(lng);

            // Y-axis rotation (horizontal drag → longitude)
            const x1 = px * cosLng + pz * sinLng;
            const y1 = py;
            const z1 = -px * sinLng + pz * cosLng;

            // X-axis rotation (vertical drag → latitude)
            const x2 = x1;
            const y2 = y1 * cosLat - z1 * sinLat;
            const z2 = y1 * sinLat + z1 * cosLat;

            // depth = z2 (positive = facing viewer)
            const depth = z2;
            // Screen position: center at (50,50), radius mapped to 42%
            const screenX = 50 + x2 * 42;
            const screenY = 50 - y2 * 42; // invert Y for screen coords

            return { lang, screenX, screenY, depth };
        });
    }, [rotLng, rotLat, spherePositions]);

    // ── Interaction: pointer drag (two-axis rotation) ──
    const handlePointerDown = useCallback((e: React.PointerEvent) => {
        isDraggingRef.current = true;
        lastPointerRef.current = { x: e.clientX, y: e.clientY };
        velocityRef.current = { lng: 0, lat: 0 };
        if (animationFrameRef.current) {
            cancelAnimationFrame(animationFrameRef.current);
            animationFrameRef.current = null;
        }
        onInteract();
        (e.target as HTMLElement).setPointerCapture?.(e.pointerId);
    }, [onInteract]);

    const handlePointerMove = useCallback((e: React.PointerEvent) => {
        if (!isDraggingRef.current) return;
        const dx = e.clientX - lastPointerRef.current.x;
        const dy = e.clientY - lastPointerRef.current.y;
        lastPointerRef.current = { x: e.clientX, y: e.clientY };

        const sensitivity = 0.006;
        const vLng = -dx * sensitivity;
        const vLat = dy * sensitivity;
        velocityRef.current = { lng: vLng, lat: vLat };

        setRotLng(prev => prev + vLng);
        setRotLat(prev => {
            const next = prev + vLat;
            return Math.max(-Math.PI / 2.2, Math.min(Math.PI / 2.2, next));
        });
    }, []);

    const handlePointerUp = useCallback((e: React.PointerEvent) => {
        isDraggingRef.current = false;
        (e.target as HTMLElement).releasePointerCapture?.(e.pointerId);

        // Momentum / inertia
        const applyMomentum = () => {
            const v = velocityRef.current;
            if (Math.abs(v.lng) < 0.0003 && Math.abs(v.lat) < 0.0003) return;

            velocityRef.current = { lng: v.lng * 0.94, lat: v.lat * 0.94 };
            setRotLng(prev => prev + velocityRef.current.lng);
            setRotLat(prev => {
                const next = prev + velocityRef.current.lat;
                return Math.max(-Math.PI / 2.2, Math.min(Math.PI / 2.2, next));
            });
            animationFrameRef.current = requestAnimationFrame(applyMomentum);
        };

        if (Math.abs(velocityRef.current.lng) > 0.0005 || Math.abs(velocityRef.current.lat) > 0.0005) {
            animationFrameRef.current = requestAnimationFrame(applyMomentum);
        }
    }, []);

    // Wheel → horizontal rotation
    const handleWheel = useCallback((e: React.WheelEvent) => {
        e.preventDefault();
        onInteract();
        const delta = e.deltaY > 0 ? 0.08 : -0.08;
        setRotLng(prev => prev + delta);
    }, [onInteract]);

    // Cleanup animation frame on unmount
    useEffect(() => {
        return () => {
            if (animationFrameRef.current) cancelAnimationFrame(animationFrameRef.current);
        };
    }, []);

    // ── Flag click ──
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

    // When scroll wheel selects a language, rotate globe to show it
    const handleScrollWheelSelect = useCallback((lang: LanguageDefinition, isNative: boolean) => {
        const idx = ALL_LANGUAGES.findIndex(l => l.langCode === lang.langCode);
        if (idx !== -1) {
            const { lat, lng } = spherePositions[idx];
            setRotLng(-lng);
            setRotLat(-lat);
        }
        if (isNative) {
            onSelectNative(lang.langCode);
        } else {
            onSelectTarget(lang.langCode);
        }
    }, [onSelectNative, onSelectTarget, spherePositions]);

    // ── Flight-path arc between selected flags ──
    const getScreenPos = (langCode: string) => {
        const p = projectedFlags.find(f => f.lang.langCode === langCode);
        return p ? { x: p.screenX, y: p.screenY } : null;
    };

    const nativePos = nativeLang ? getScreenPos(nativeLang.langCode) : null;
    const targetPos = targetLang ? getScreenPos(targetLang.langCode) : null;
    const pathD = useMemo(() => {
        if (!nativePos || !targetPos) return '';
        return `M ${nativePos.x} ${nativePos.y} Q 50 50 ${targetPos.x} ${targetPos.y}`;
    }, [nativePos, targetPos]);

    // Sort by depth so front flags render on top
    const sortedFlags = useMemo(
        () => [...projectedFlags].sort((a, b) => a.depth - b.depth),
        [projectedFlags],
    );

    // Backdrop click handler via useEffect on document
    useEffect(() => {
        if (!onCancel) return;
        const handler = (e: MouseEvent) => {
            // If click is outside the globe container, cancel
            if (globeContainerRef.current && !globeContainerRef.current.contains(e.target as Node)) {
                onCancel();
            }
        };
        // Use capture phase + slight delay so the opening click doesn't immediately cancel
        const timer = setTimeout(() => document.addEventListener('pointerdown', handler, true), 100);
        return () => {
            clearTimeout(timer);
            document.removeEventListener('pointerdown', handler, true);
        };
    }, [onCancel]);

    return (
        <div ref={globeContainerRef} className="w-full flex justify-center py-2">
            <style>{`
                @keyframes fly-in-bubble {
                    from { offset-distance: 0%; }
                    to { offset-distance: 100%; }
                }
                .animate-fly-in-bubble {
                    animation: fly-in-bubble 2.5s ease-in-out forwards;
                    offset-path: path(var(--flight-path));
                }
                .sphere-flag {
                    transition: opacity 0.12s ease-out;
                }
            `}</style>

            <div className="glass-cube relative w-full max-w-[22rem] aspect-square">
                {/* Front pane: square window that clips the globe */}
                <div className="cube-front relative inset-0 w-full h-full overflow-hidden rounded-lg border border-white/10 z-10">
                    <div
                        ref={globeRef}
                        className="globe-bg absolute inset-0 border-2 rounded-full flex items-center justify-center text-white overflow-hidden shadow-inner touch-none isolate"
                        style={{ backgroundColor: '#0b3d66' }}
                        onPointerDown={handlePointerDown}
                        onPointerMove={handlePointerMove}
                        onPointerUp={handlePointerUp}
                        onPointerCancel={handlePointerUp}
                        onPointerLeave={() => { if (isDraggingRef.current) handlePointerUp({} as React.PointerEvent); }}
                        onWheel={handleWheel}
                    >
                        {/* Atmosphere & visual layers */}
                        <div className="atmosphere-haze"></div>
                        <div className="style-harmonizer"></div>
                        <div className="rim-warmth"></div>
                        <div className="day-night-shade" style={{ ['--terminator-angle' as any]: `${timeAngleDeg}deg` }}></div>
                        <div className="sun-glint" style={{ ['--glint-x' as any]: `${glintPos.x}%`, ['--glint-y' as any]: `${glintPos.y}%` }}></div>
                        <div className="cloud-layer cloud-layer-far"></div>
                        <div className="cloud-layer cloud-layer-near"></div>
                        <div className="cloud-layer cloud-layer-highlights"></div>

                        {/* ── Scroll wheel overlay (always on top, always blocks input) ── */}
                        <div className="absolute inset-0 flex items-center justify-center" style={{ zIndex: 100, pointerEvents: 'none' }}>
                            <div
                                className="w-[70%] max-w-[10rem] backdrop-blur-md px-3 py-2 transition-opacity duration-200 opacity-50 hover:opacity-100 focus-within:opacity-100 active:opacity-100 shadow-lg border border-white/10 sketchy-border-thin"
                                style={{ background: 'rgba(12, 30, 48, 0.38)', pointerEvents: 'auto', position: 'relative', zIndex: 100 }}
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

                        {/* ── Projected 3D flags ── */}
                        {sortedFlags.map(({ lang, screenX, screenY, depth }) => {
                            // Cull back-hemisphere flags
                            if (depth < 0.05) return null;

                            const isNative = nativeLang?.langCode === lang.langCode;
                            const isTarget = targetLang?.langCode === lang.langCode;
                            const showShortCode = hasSharedFlag(lang);

                            // Depth-based visual properties
                            const scale = 0.45 + depth * 0.75;     // 0.45 → 1.2
                            const scaleX = 0.3 + depth * 0.7;      // horizontal compression at edges
                            const opacity = Math.pow(depth, 0.6);   // smooth fade
                            const zIndex = Math.floor(depth * 20) + 10;

                            return (
                                <button
                                    key={lang.langCode}
                                    className="sphere-flag absolute flex flex-col items-center justify-center p-1 sketchy-border-thin"
                                    style={{
                                        top: `${screenY}%`,
                                        left: `${screenX}%`,
                                        transform: `translate(-50%, -50%) scale(${scale}) scaleX(${scaleX / scale})`,
                                        opacity,
                                        zIndex,
                                    }}
                                    onClick={() => handleFlagClick(lang)}
                                    onMouseEnter={() => setHoveredLang(lang)}
                                    onMouseLeave={() => setHoveredLang(null)}
                                    title={lang.displayName}
                                >
                                    <span className={`text-lg leading-none transition-transform duration-200 ${hoveredLang?.langCode === lang.langCode || isNative || isTarget ? 'scale-125' : 'scale-100'}`}>
                                        {lang.flag}
                                    </span>
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
                    </div>

                    {/* Glass cube inner overlays */}
                    <div className="cube-inner-shadow pointer-events-none"></div>
                    <div className="cube-ice-scratches pointer-events-none"></div>

                    {/* Flight path and plane */}
                    {pathD && (
                        <svg key={pathD} viewBox="0 0 100 100" className="absolute inset-0 w-full h-full overflow-visible pointer-events-none z-50">
                            <path d={pathD} stroke="rgba(255,255,255,0.3)" strokeWidth="2" fill="none" strokeDasharray="5,5" />
                            <g
                                style={{ '--flight-path': `"${pathD}"` } as React.CSSProperties}
                                className="animate-fly-in-bubble"
                            >
                                <text
                                    className={nativeLang && targetLang ? 'animate-pulse' : ''}
                                    fontSize="10"
                                    dominantBaseline="middle"
                                    textAnchor="middle"
                                >
                                    ✈️
                                </text>
                            </g>
                        </svg>
                    )}
                </div>

                {/* Facet edges for 3D glass cube effect */}
                <div className="cube-edge cube-edge-top pointer-events-none"></div>
                <div className="cube-edge cube-edge-bottom pointer-events-none"></div>
                <div className="cube-edge cube-edge-left pointer-events-none"></div>
                <div className="cube-edge cube-edge-right pointer-events-none"></div>
            </div>

            <style>{`
                /* Theme-safe ocean base */
                .globe-bg { background-color: #0b3d66; }

                /* Glass cube container */
                .glass-cube {
                    position: relative;
                    filter: drop-shadow(0 14px 22px rgba(0,0,0,0.35));
                    perspective: 900px;
                }
                .cube-front {
                    background:
                        radial-gradient(circle at 50% 35%, rgba(255,255,255,0.08) 0%, rgba(255,255,255,0.04) 40%, transparent 70%),
                        linear-gradient(180deg, rgba(20,40,60,0.32) 0%, rgba(12,24,36,0.36) 100%);
                    backdrop-filter: saturate(1.1) contrast(1.02);
                    box-shadow:
                        inset 0 1px 0 rgba(255,255,255,0.18),
                        inset 0 -10px 30px rgba(12,24,36,0.35);
                }
                .cube-inner-shadow {
                    position: absolute; inset: 0; border-radius: 0.75rem;
                    background: radial-gradient(closest-side, transparent 68%, rgba(0,0,0,0.25) 100%);
                    mix-blend-mode: multiply; opacity: 0.75;
                }
                .cube-ice-scratches {
                    position: absolute; inset: 0; border-radius: 0.75rem; opacity: 0.10;
                    background-image: repeating-linear-gradient(135deg, rgba(255,255,255,0.14) 0px, rgba(255,255,255,0.14) 1px, transparent 3px, transparent 7px);
                    mask-image: radial-gradient(circle at 50% 50%, black 45%, transparent 100%);
                }
                .cube-edge { position: absolute; }
                .cube-edge-top { top:-6px;left:10px;right:10px;height:14px;border-radius:10px;background:linear-gradient(180deg,rgba(255,255,255,0.35)0%,rgba(255,255,255,0.08)100%);transform:rotateX(25deg); }
                .cube-edge-bottom { bottom:-10px;left:10px;right:10px;height:18px;border-radius:12px;background:linear-gradient(0deg,rgba(12,24,36,0.45)0%,rgba(12,24,36,0.18)100%);transform:rotateX(-25deg); }
                .cube-edge-left { left:-8px;top:12px;bottom:12px;width:16px;border-radius:12px;background:linear-gradient(90deg,rgba(255,255,255,0.30)0%,rgba(255,255,255,0.08)100%);transform:rotateY(-25deg); }
                .cube-edge-right { right:-8px;top:12px;bottom:12px;width:16px;border-radius:12px;background:linear-gradient(270deg,rgba(255,255,255,0.30)0%,rgba(255,255,255,0.08)100%);transform:rotateY(25deg); }

                /* Atmosphere layers */
                @keyframes sun-glint-drift {
                    0% { transform:translate(-6%,-4%);opacity:0.35; }
                    50% { transform:translate(4%,2%);opacity:0.5; }
                    100% { transform:translate(-6%,-4%);opacity:0.35; }
                }
                @keyframes cloud-drift-far { 0%{transform:translateX(-10px)}100%{transform:translateX(10px)} }
                @keyframes cloud-drift-near { 0%{transform:translateX(12px)}100%{transform:translateX(-12px)} }

                .atmosphere-haze {
                    position:absolute;inset:-2%;border-radius:50%;pointer-events:none;
                    background:
                        radial-gradient(circle at 50% 45%,rgba(255,255,255,0.08)0%,rgba(255,255,255,0.03)45%,transparent 70%),
                        radial-gradient(circle at 50% 50%,transparent 60%,rgba(125,211,252,0.12)100%);
                    box-shadow:inset 0 0 30px rgba(255,255,255,0.08),0 0 18px rgba(125,211,252,0.18);
                }
                .style-harmonizer {
                    position:absolute;inset:0;border-radius:50%;pointer-events:none;
                    mix-blend-mode:soft-light;opacity:0.18;
                    background:
                        radial-gradient(circle at 50% 55%,rgba(14,65,110,0.20)0%,rgba(14,65,110,0.08)55%,transparent 70%),
                        linear-gradient(180deg,rgba(200,95,48,0.12)0%,rgba(0,0,0,0)40%);
                }
                .rim-warmth {
                    position:absolute;inset:-1%;border-radius:50%;pointer-events:none;
                    mix-blend-mode:soft-light;opacity:0.28;
                    background:radial-gradient(closest-side,transparent 70%,rgba(200,95,48,0.22)100%);
                }
                .day-night-shade {
                    position:absolute;inset:0;border-radius:50%;pointer-events:none;
                    background:linear-gradient(var(--terminator-angle,120deg),rgba(255,255,255,0.10)0%,rgba(255,255,255,0.03)30%,rgba(0,0,0,0.10)64%,rgba(0,0,0,0.18)100%);
                }
                .sun-glint {
                    position:absolute;inset:0;border-radius:50%;pointer-events:none;
                    background:radial-gradient(circle at var(--glint-x,28%) var(--glint-y,22%),rgba(255,255,255,0.26)0%,rgba(255,255,255,0.08)22%,transparent 45%);
                    animation:sun-glint-drift 48s ease-in-out infinite;
                }
                .cloud-layer {
                    position:absolute;inset:0;border-radius:50%;pointer-events:none;opacity:0.20;filter:blur(0.6px);
                }
                .cloud-layer-far {
                    background:
                        radial-gradient(ellipse 18% 9% at 24% 28%,rgba(255,255,255,0.54)0%,transparent 100%),
                        radial-gradient(ellipse 14% 7% at 66% 34%,rgba(255,255,255,0.46)0%,transparent 100%),
                        radial-gradient(ellipse 16% 8% at 52% 72%,rgba(255,255,255,0.40)0%,transparent 100%);
                    animation:cloud-drift-far 160s linear infinite alternate;
                }
                .cloud-layer-near {
                    background:
                        radial-gradient(ellipse 22% 10% at 36% 48%,rgba(255,255,255,0.46)0%,transparent 100%),
                        radial-gradient(ellipse 15% 8% at 74% 58%,rgba(255,255,255,0.40)0%,transparent 100%);
                    animation:cloud-drift-near 115s linear infinite alternate;
                }
                .cloud-layer-highlights {
                    opacity:0.14;mix-blend-mode:screen;
                    background:
                        radial-gradient(ellipse 16% 7% at 30% 40%,rgba(255,255,255,0.35)0%,transparent 100%),
                        radial-gradient(ellipse 13% 6% at 70% 55%,rgba(255,255,255,0.28)0%,transparent 100%),
                        radial-gradient(ellipse 18% 8% at 54% 72%,rgba(255,255,255,0.24)0%,transparent 100%);
                    animation:cloud-drift-far 145s linear infinite alternate;
                }

                @media (prefers-reduced-motion:reduce) {
                    .sun-glint,.cloud-layer-far,.cloud-layer-near,.cloud-layer-highlights { animation:none; }
                }
            `}</style>
        </div>
    );
};

export default LanguageSelectorGlobe;
