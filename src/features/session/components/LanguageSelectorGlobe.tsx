
import React, { useRef, useState, useMemo } from 'react';
import { LanguageDefinition, ALL_LANGUAGES } from '../../config/languages';
import { TranslationReplacements } from '../../i18n/index';
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

    const getPosition = (index: number, total: number) => {
        const angle = (index / total) * 2 * Math.PI;
        const adjustedAngle = angle - Math.PI / 2;
        const radius = 45;
        const x = 50 + radius * Math.cos(adjustedAngle);
        const y = 50 + radius * Math.sin(adjustedAngle);
        return { x, y };
    };

    const nativePos = nativeLang ? getPosition(ALL_LANGUAGES.findIndex(l => l.langCode === nativeLang.langCode), ALL_LANGUAGES.length) : null;
    const targetPos = targetLang ? getPosition(ALL_LANGUAGES.findIndex(l => l.langCode === targetLang.langCode), ALL_LANGUAGES.length) : null;

    const pathD = useMemo(() => {
        if (!nativePos || !targetPos) return "";
        const controlX = 50;
        const controlY = 50;
        return `M ${nativePos.x} ${nativePos.y} Q ${controlX} ${controlY} ${targetPos.x} ${targetPos.y}`;
    }, [nativePos, targetPos]);

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
            `}</style>

            <div
                ref={globeRef}
                className="globe-bg relative w-full max-w-[20rem] aspect-square border-2 rounded-full flex items-center justify-center bg-slate-800 text-white overflow-hidden shadow-inner"
                onPointerDown={onInteract}
                onWheel={onInteract}
            >
                {pathD && (
                    <svg key={pathD} viewBox="0 0 100 100" className="absolute w-full h-full top-0 left-0 overflow-visible pointer-events-none">
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
                
                <div
                    className="absolute inset-0 flex items-center justify-center pointer-events-none"
                >
                    <div
                        className="pointer-events-auto w-[85%] max-w-[14rem] bg-slate-900/40 backdrop-blur-sm rounded-lg p-2 transition-opacity duration-200 opacity-30 hover:opacity-100 focus-within:opacity-100 active:opacity-100"
                    >
                        <div className="flex justify-around items-start gap-2">
                            <LanguageScrollWheel
                                languages={ALL_LANGUAGES}
                                selectedValue={nativeLang}
                                onSelect={(l) => onSelectNative(l.langCode)}
                                onInteract={onInteract}
                                title=""
                            />
                            <div className="w-px h-20 bg-white/20 mx-1"></div>
                            <LanguageScrollWheel
                                languages={ALL_LANGUAGES.filter(l => l.langCode !== nativeLang?.langCode)}
                                selectedValue={targetLang}
                                onSelect={(l) => onSelectTarget(l.langCode)}
                                disabled={!nativeLang}
                                onInteract={onInteract}
                                title=""
                            />
                        </div>
                    </div>
                </div>
                
                {ALL_LANGUAGES.map((lang, index) => {
                    const pos = getPosition(index, ALL_LANGUAGES.length);
                    const isNative = nativeLang?.langCode === lang.langCode;
                    const isTarget = targetLang?.langCode === lang.langCode;
                    return (
                        <button
                            key={lang.langCode}
                            ref={el => { if (el) flagRefs.current.set(lang.langCode, el); else flagRefs.current.delete(lang.langCode); }}
                            className="absolute transform -translate-x-1/2 -translate-y-1/2 flex items-center justify-center p-1.5 rounded-full transition-all duration-300 ease-out z-10"
                            style={{ top: `${pos.y}%`, left: `${pos.x}%` }}
                            onClick={() => handleFlagClick(lang)}
                            onMouseEnter={() => setHoveredLang(lang)}
                            onMouseLeave={() => setHoveredLang(null)}
                            title={lang.displayName}
                        >
                            <span className={`text-xl transition-transform duration-200 ${hoveredLang?.langCode === lang.langCode || isNative || isTarget ? 'scale-150' : 'scale-100'}`}>{lang.flag}</span>
                            <div className={`absolute -inset-1 rounded-full border-2 transition-all duration-300 pointer-events-none ${
                                isNative ? 'border-sky-400 shadow-sky-400/50 shadow-lg' :
                                isTarget ? 'border-green-400 shadow-green-400/50 shadow-lg' :
                                'border-transparent'
                            }`}></div>
                        </button>
                    );
                })}
            </div>
        </div>
    );
};

export default LanguageSelectorGlobe;
