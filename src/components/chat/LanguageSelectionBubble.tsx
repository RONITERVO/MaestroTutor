
import React, { useState, useRef, useEffect, useCallback, useMemo } from 'react';
import { ChatMessage } from '../../../types';
import { TranslationReplacements } from '../../../translations/index';
import { LanguageDefinition, ALL_LANGUAGES, IconPlus, IconXMark, IconSave, IconFolderOpen, LOCAL_STORAGE_SETTINGS_KEY } from '../../../constants';
import { getMaestroProfileImageDB, setMaestroProfileImageDB, clearMaestroProfileImageDB, MaestroProfileAsset } from '../../services/assets';
import { uploadMediaToFiles, deleteFileByNameOrUri } from '../../../services/geminiService';
import { DB_NAME } from '../../storage/db';
import LanguageScrollWheel from './LanguageScrollWheel';

interface LanguageSelectionBubbleProps {
    message: ChatMessage;
    onTempLanguageSelect: (messageId: string, langType: 'native' | 'target', langCode: string | null) => void;
    onConfirmLanguageSelection: (messageId: string, nativeLangCode?: string, targetLangCode?: string) => void;
    onSaveAllChats: (options?: { filename?: string; auto?: boolean }) => Promise<void>;
    onLoadAllChats: (file: File) => Promise<void>;
    loadingGifs?: string[] | null;
    t: (key: string, replacements?: TranslationReplacements) => string;
    onUiTaskStart?: (token?: string) => string | void;
    onUiTaskEnd?: (token?: string) => void;
    isFocusedMode?: boolean;
}

const LanguageSelectionBubble: React.FC<LanguageSelectionBubbleProps> = ({
    message,
    onTempLanguageSelect,
    onConfirmLanguageSelection,
    onSaveAllChats,
    onLoadAllChats,
    loadingGifs,
    t,
    onUiTaskStart,
    onUiTaskEnd,
    isFocusedMode
}) => {
    const nativeLang = ALL_LANGUAGES.find(l => l.langCode === message.tempSelectedNativeLangCode) || null;
    const targetLang = ALL_LANGUAGES.find(l => l.langCode === message.tempSelectedTargetLangCode) || null;
    const [hoveredLang, setHoveredLang] = useState<LanguageDefinition | null>(null);

    const globeRef = useRef<HTMLDivElement>(null);
    const fileInputRef = useRef<HTMLInputElement>(null);
    const maestroFileInputRef = useRef<HTMLInputElement>(null);
    const flagRefs = useRef<Map<string, HTMLButtonElement>>(new Map());

    const [maestroAsset, setMaestroAsset] = useState<MaestroProfileAsset | null>(null);
    const [isUploadingMaestro, setIsUploadingMaestro] = useState(false);
    
    const [isSavePopupOpen, setIsSavePopupOpen] = useState(false);
    const [isLoadPopupOpen, setIsLoadPopupOpen] = useState(false);
    const [isSavingInProgress, setIsSavingInProgress] = useState(false);
    const [isLoadingInProgress, setIsLoadingInProgress] = useState(false);
    
    const [isResetPopupOpen, setIsResetPopupOpen] = useState(false);
    const [resetName, setResetName] = useState<string>(() => {
        const ts = new Date().toISOString().slice(0, 19).replace('T', '_').replace(/:/g, '-');
        return `maestro-reset-${ts}`;
    });
    const [resetConfirm, setResetConfirm] = useState<string>('');
    const [isResetting, setIsResetting] = useState(false);

    const savePopupTokenRef = useRef<string | null>(null);
    const loadPopupTokenRef = useRef<string | null>(null);
    const maestroUploadTokenRef = useRef<string | null>(null);
    const maestroAvatarOpenTokenRef = useRef<string | null>(null);
    const resetPopupTokenRef = useRef<string | null>(null);

    const wipeLocalMemoryAndDb = useCallback(async () => {
        try {
            await new Promise<void>((resolve) => {
                let settled = false;
                try {
                    const req = indexedDB.deleteDatabase(DB_NAME);
                    req.onsuccess = () => { settled = true; resolve(); };
                    req.onerror = () => { resolve(); };
                    req.onblocked = () => { resolve(); };
                } catch {
                    resolve();
                }
                setTimeout(() => { if (!settled) resolve(); }, 1500);
            });
        } catch {}
        try {
            const keys: string[] = [];
            for (let i = 0; i < window.localStorage.length; i++) {
                const k = window.localStorage.key(i);
                if (k) keys.push(k);
            }
            keys.forEach(k => {
                if (k.startsWith('chatBackup:') || k === LOCAL_STORAGE_SETTINGS_KEY) {
                    try { window.localStorage.removeItem(k); } catch {}
                }
            });
        } catch {}
    }, []);

    const genToken = useCallback((tag: string) => `${tag}:${Date.now()}:${Math.random().toString(36).slice(2,8)}`,[ ]);
    const startUiTask = useCallback((tag: string) => {
      const tok = genToken(tag);
      const ret = onUiTaskStart?.(tok);
      return typeof ret === 'string' ? ret : tok;
    }, [genToken, onUiTaskStart]);
    const endUiTask = useCallback((token: string | null) => {
      if (token) onUiTaskEnd?.(token);
    }, [onUiTaskEnd]);

    useEffect(() => {
      let mounted = true;
      (async () => {
        try {
          const a = await getMaestroProfileImageDB();
          if (mounted) setMaestroAsset(a);
        } catch {}
      })();
      return () => { mounted = false; };
    }, []);

    useEffect(() => {
      const handler = (e: any) => {
        try {
          const d = e?.detail || {};
          if (d && (typeof d.dataUrl === 'string' || typeof d.uri === 'string')) {
            setMaestroAsset({
              dataUrl: typeof d.dataUrl === 'string' ? d.dataUrl : maestroAsset?.dataUrl,
              mimeType: typeof d.mimeType === 'string' ? d.mimeType : maestroAsset?.mimeType,
              uri: typeof d.uri === 'string' ? d.uri : maestroAsset?.uri,
              updatedAt: Date.now(),
            });
          } else {
            getMaestroProfileImageDB().then(a => setMaestroAsset(a)).catch(() => {});
          }
        } catch { /* ignore */ }
      };
      window.addEventListener('maestro-avatar-updated', handler as any);
      return () => window.removeEventListener('maestro-avatar-updated', handler as any);
    }, [maestroAsset]);

  const lastInteractionRef = useRef<number>(Date.now());
  const autoConfirmTimeoutRef = useRef<number | null>(null);
  const lastSelectionKeyRef = useRef<string | null>(null);

  const clearAutoConfirm = useCallback(() => {
    if (autoConfirmTimeoutRef.current) {
      clearTimeout(autoConfirmTimeoutRef.current);
      autoConfirmTimeoutRef.current = null;
    }
  }, []);

  const clearTempSelections = useCallback(() => {
    clearAutoConfirm();
    lastSelectionKeyRef.current = null;
    if (message?.id) {
      onTempLanguageSelect(message.id, 'target', null);
    }
  }, [clearAutoConfirm, message?.id, onTempLanguageSelect]);

  const scheduleAutoConfirm = useCallback(() => {
    clearAutoConfirm();
    if (!nativeLang || !targetLang) return;
    const key = `${nativeLang.langCode}|${targetLang.langCode}`;
    lastSelectionKeyRef.current = key;
    autoConfirmTimeoutRef.current = window.setTimeout(() => {
      const unchanged = lastSelectionKeyRef.current === key;
      const inactiveForMs = Date.now() - lastInteractionRef.current;
      if (unchanged && inactiveForMs >= 5000) {
        onConfirmLanguageSelection(message.id, nativeLang.langCode, targetLang.langCode);
      }
    }, 5000);
  }, [clearAutoConfirm, nativeLang, targetLang, onConfirmLanguageSelection, message.id]);

  const markInteraction = useCallback(() => {
    lastInteractionRef.current = Date.now();
    if (nativeLang && targetLang) scheduleAutoConfirm();
  }, [nativeLang, targetLang, scheduleAutoConfirm]);

    const handleNativeSelect = useCallback((lang: LanguageDefinition) => {
    markInteraction();
        onTempLanguageSelect(message.id, 'native', lang.langCode);
    }, [message.id, onTempLanguageSelect]);

    const handleTargetSelect = useCallback((lang: LanguageDefinition) => {
        if (!nativeLang || lang.langCode === nativeLang.langCode) return;
    markInteraction();
        onTempLanguageSelect(message.id, 'target', lang.langCode);
    }, [message.id, onTempLanguageSelect, nativeLang]);

  const handleFlagClick = (lang: LanguageDefinition) => {
    markInteraction();
        if (!nativeLang) {
            handleNativeSelect(lang);
        } else if (!targetLang && lang.langCode !== nativeLang.langCode) {
            handleTargetSelect(lang);
        } else if (lang.langCode === nativeLang.langCode) {
            onTempLanguageSelect(message.id, 'native', null);
            onTempLanguageSelect(message.id, 'target', null);
        } else if (lang.langCode === targetLang?.langCode) {
            onTempLanguageSelect(message.id, 'target', null);
        } else {
            handleTargetSelect(lang);
        }
    };

  const handleLoadClick = () => { fileInputRef.current?.click(); };

    const handleFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
        const file = event.target.files?.[0];
        if (file) {
            try {
              if (!loadPopupTokenRef.current) loadPopupTokenRef.current = startUiTask('load-popup');
              setIsLoadingInProgress(true);
              await onLoadAllChats(file);
              setIsLoadPopupOpen(false);
            } finally {
              setIsLoadingInProgress(false);
              endUiTask(loadPopupTokenRef.current);
              loadPopupTokenRef.current = null;
            }
        }
        event.target.value = '';
    };

    const handleMaestroAvatarClick = () => {
      clearTempSelections();
      try {
        if (!maestroAvatarOpenTokenRef.current) {
          const tok = startUiTask('maestro-avatar-open');
          maestroAvatarOpenTokenRef.current = typeof tok === 'string' ? tok : String(tok);
        }
      } catch {}
      maestroFileInputRef.current?.click();
    };

    const fetchDefaultAvatarBlob = async (): Promise<Blob | null> => {
      try {
        const man = await fetch('/maestro-avatars/manifest.json', { cache: 'force-cache' });
        if (man.ok) {
          const list: string[] = await man.json();
          if (Array.isArray(list)) {
            for (const name of list) {
              try {
                const r = await fetch(`/maestro-avatars/${name}`, { cache: 'force-cache' });
                if (r.ok) return await r.blob();
              } catch { }
            }
          }
        }
      } catch { }
      return null;
    };

    const handleClearMaestroAvatar = async (e?: React.MouseEvent) => {
      e?.preventDefault?.();
      e?.stopPropagation?.();
      try { 
        setIsUploadingMaestro(true); 
        if (!maestroUploadTokenRef.current) maestroUploadTokenRef.current = startUiTask('maestro-avatar');
      } catch {}
      try {
        const prevUri = maestroAsset?.uri;
        if (prevUri) {
          await deleteFileByNameOrUri(prevUri);
        }
      } catch { }
      try { await clearMaestroProfileImageDB(); } catch { }
      try {
        const blob = await fetchDefaultAvatarBlob();
        if (blob) {
          const mime = blob.type || 'image/png';
          const dataUrl: string = await new Promise((resolve, reject) => {
            const fr = new FileReader();
            fr.onloadend = () => resolve(fr.result as string);
            fr.onerror = () => reject(fr.error || new Error('DataURL conversion failed'));
            fr.readAsDataURL(blob);
          });
          let uploadedUri: string | undefined;
          let uploadedMime: string | undefined;
          try {
            const up = await uploadMediaToFiles(dataUrl, mime, 'maestro-avatar');
            uploadedUri = up.uri; uploadedMime = up.mimeType;
          } catch { }
          const asset: MaestroProfileAsset = { dataUrl, mimeType: uploadedMime || mime, uri: uploadedUri, updatedAt: Date.now() };
          try { await setMaestroProfileImageDB(asset); } catch {}
          setMaestroAsset(asset);
          try { window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: asset })); } catch {}
        } else {
          setMaestroAsset(null);
          try { window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: {} })); } catch {}
        }
      } catch {
        setMaestroAsset(null);
      } finally {
        try { setIsUploadingMaestro(false); } catch {}
        endUiTask(maestroUploadTokenRef.current);
        maestroUploadTokenRef.current = null;
      }
    };

    const handleMaestroFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
      try { if (maestroAvatarOpenTokenRef.current) { endUiTask(maestroAvatarOpenTokenRef.current); maestroAvatarOpenTokenRef.current = null; } } catch {}
      const file = event.target.files?.[0];
      if (!file) { event.target.value = ''; return; }
      if (!file.type.startsWith('image/')) { event.target.value = ''; return; }
      try {
        setIsUploadingMaestro(true);
        if (!maestroUploadTokenRef.current) maestroUploadTokenRef.current = startUiTask('maestro-avatar');
        const dataUrl: string = await new Promise((resolve, reject) => {
          const reader = new FileReader();
          reader.onload = () => resolve(String(reader.result));
          reader.onerror = (e) => reject(e);
          reader.readAsDataURL(file);
        });
        let uploadedUri: string | undefined;
        let uploadedMime: string | undefined;
        try {
          const up = await uploadMediaToFiles(dataUrl, file.type, 'maestro-avatar');
          uploadedUri = up.uri; uploadedMime = up.mimeType;
        } catch {}
        const asset: MaestroProfileAsset = { dataUrl, mimeType: file.type, uri: uploadedUri, updatedAt: Date.now() };
        await setMaestroProfileImageDB(asset);
        setMaestroAsset(asset);
        try {
          window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: { uri: uploadedUri, mimeType: uploadedMime, dataUrl } }));
        } catch {}
      } catch {
      } finally {
        setIsUploadingMaestro(false);
        event.target.value = '';
        endUiTask(maestroUploadTokenRef.current);
        maestroUploadTokenRef.current = null;
      }
    };

    useEffect(() => {
      const onFocus = () => {
        try { if (maestroAvatarOpenTokenRef.current) { endUiTask(maestroAvatarOpenTokenRef.current); maestroAvatarOpenTokenRef.current = null; } } catch {}
      };
      window.addEventListener('focus', onFocus);
      return () => window.removeEventListener('focus', onFocus);
    }, [endUiTask]);

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

  useEffect(() => {
    if (nativeLang && targetLang) {
      scheduleAutoConfirm();
    } else {
      clearAutoConfirm();
    }
    return () => { };
  }, [nativeLang?.langCode, targetLang?.langCode, scheduleAutoConfirm, clearAutoConfirm]);

  useEffect(() => () => clearAutoConfirm(), [clearAutoConfirm]);

  const wrapperBase = 'bg-slate-800 text-white rounded-xl p-4 flex flex-col items-center overflow-hidden antialiased my-4 shadow transition-all duration-300 ease-in-out';
  const widthClasses = isFocusedMode
    ? 'w-full'
    : 'w-full max-w-[90%] sm:max-w-[80%] md:max-w-[70%] lg:max-w-[65%]';

  return (
    <div id="language-selector-bubble" className={`${wrapperBase} ${widthClasses}`}>
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
        className="globe-bg relative w-full max-w-[20rem] sm:max-w-[24rem] aspect-square border-2 rounded-full flex items-center justify-center"
        onPointerDown={markInteraction}
        onWheel={markInteraction}
      >
                 {pathD && (
                    <svg key={pathD} viewBox="0 0 100 100" className="absolute w-full h-full top-0 left-0 overflow-visible">
                        <path d={pathD} stroke="rgba(255,255,255,0.2)" strokeWidth="2" fill="none" strokeDasharray="5,5" />
                        <g 
                            style={{'--flight-path': `"${pathD}"`} as React.CSSProperties} 
              className="animate-fly-in-bubble cursor-pointer"
                            onClick={() => onConfirmLanguageSelection(message.id, nativeLang?.langCode, targetLang?.langCode)}
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
                  aria-hidden={false}
                >
                  <div
                    className="pointer-events-auto w-[78%] sm:w-[80%] max-w-[22rem] bg-slate-900/30 backdrop-blur-sm rounded-lg p-2 transition-opacity duration-200 opacity-20 hover:opacity-100 focus-within:opacity-100 active:opacity-100"
                  >
                    <div className="flex justify-around items-start gap-3">
                      <LanguageScrollWheel
                        languages={ALL_LANGUAGES}
                        selectedValue={nativeLang}
                        onSelect={handleNativeSelect}
                        onInteract={markInteraction}
                        title={t('nativeLang.label')}
                      />
                      <LanguageScrollWheel
                        languages={ALL_LANGUAGES.filter(l => l.langCode !== nativeLang?.langCode)}
                        selectedValue={targetLang}
                        onSelect={handleTargetSelect}
                        disabled={!nativeLang}
                        onInteract={markInteraction}
                        title={t('targetLang.label')}
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
                            className="absolute transform -translate-x-1/2 -translate-y-1/2 flex items-center justify-center p-1.5 rounded-full transition-all duration-300 ease-out"
                            style={{ top: `${pos.y}%`, left: `${pos.x}%` }}
                            onClick={() => handleFlagClick(lang)}
                            onMouseEnter={() => setHoveredLang(lang)}
                            onMouseLeave={() => setHoveredLang(null)}
                            title={lang.displayName}
                        >
                            <span className={`text-2xl transition-transform duration-200 ${hoveredLang?.langCode === lang.langCode || isNative || isTarget ? 'scale-150' : 'scale-100'}`}>{lang.flag}</span>
                            <div className={`absolute -inset-1 rounded-full border-2 transition-all duration-300 pointer-events-none ${
                                isNative ? 'border-sky-400 shadow-sky-400/50 shadow-lg' :
                                isTarget ? 'border-green-400 shadow-green-400/50 shadow-lg' :
                                'border-transparent'
                            }`}></div>
                        </button>
                    );
                })}
            </div>

            <div className="flex items-center space-x-2 mt-4 pt-3 border-t border-slate-600 w-full max-w-lg justify-center">
                <div className="relative inline-block">
                  <div
                    onClick={!isUploadingMaestro ? handleMaestroAvatarClick : undefined}
                    onKeyDown={(e) => {
                      if (isUploadingMaestro) return;
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        handleMaestroAvatarClick();
                      }
                    }}
                    role="button"
                    tabIndex={isUploadingMaestro ? -1 : 0}
                    aria-disabled={isUploadingMaestro}
                    className={`relative w-10 h-10 rounded-full overflow-hidden border-2 ${maestroAsset?.dataUrl ? 'border-slate-500' : 'border-slate-400 border-dashed'} bg-slate-700 flex items-center justify-center hover:ring-2 hover:ring-slate-400 transition ${isUploadingMaestro ? 'opacity-70' : 'cursor-pointer'}`}
                    title={maestroAsset?.dataUrl ? t('startPage.maestroAvatar') || 'Maestro avatar' : t('startPage.addMaestroAvatar') || 'Add Maestro avatar'}
                    aria-label={maestroAsset?.dataUrl ? (t('startPage.maestroAvatar') || 'Maestro avatar') : (t('startPage.addMaestroAvatar') || 'Add Maestro avatar')}
                  >
                    {maestroAsset?.dataUrl ? (
                      <img src={maestroAsset.dataUrl} alt="Maestro avatar" className="w-full h-full object-cover" />
                    ) : (
                      <IconPlus className="w-5 h-5 text-slate-200" />
                    )}
                    {isUploadingMaestro && (
                      <div className="absolute inset-0 bg-black/40 flex items-center justify-center">
                        <svg className="animate-spin h-4 w-4 text-white" viewBox="0 0 24 24">
                          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
                        </svg>
                      </div>
                    )}
                  </div>
                  {maestroAsset?.dataUrl && !isUploadingMaestro && (
                    <button
                      onClick={(e) => { e.stopPropagation(); handleClearMaestroAvatar(); }}
                      className="absolute -top-1 -right-1 z-10 bg-slate-900/80 hover:bg-red-600 text-white rounded-full p-0.5 shadow-lg"
                      title={t('general.clear') || 'Clear'}
                      aria-label={t('general.clear') || 'Clear'}
                    >
                      <IconXMark className="w-3.5 h-3.5" />
                    </button>
                  )}
                </div>
                <input type="file" ref={maestroFileInputRef} onChange={handleMaestroFileChange} accept="image/*" className="hidden" />
                <div className="relative">
                  <button
                    onClick={() => { 
                      clearTempSelections();
                      if (!savePopupTokenRef.current) savePopupTokenRef.current = startUiTask('save-popup'); 
                      setIsSavePopupOpen(true); 
                    }}
                    className="flex items-center justify-center p-2 bg-slate-600 hover:bg-slate-500 rounded-full shadow-lg transition-colors"
                    title={t('startPage.saveChats')}
                  >
                    <IconSave className="w-5 h-5" />
                  </button>
                </div>
                <input type="file" ref={fileInputRef} onChange={handleFileChange} accept=".json" className="hidden" />
                <div className="relative">
                  <button
                    onClick={() => { 
                      clearTempSelections();
                      if (!loadPopupTokenRef.current) loadPopupTokenRef.current = startUiTask('load-popup'); 
                      setIsLoadPopupOpen(true); 
                    }}
                    className="flex items-center justify-center p-2 bg-slate-600 hover:bg-slate-500 rounded-full shadow-lg transition-colors"
                    title={t('startPage.loadChats')}
                  >
                    <IconFolderOpen className="w-5 h-5" />
                  </button>
                </div>
            </div>
            {isSavePopupOpen && (
              <div className="fixed inset-0 z-50 flex items-center justify-center">
                <div
                  className="absolute inset-0 bg-black/50"
                  onClick={() => { if (!isSavingInProgress) { setIsSavePopupOpen(false); endUiTask(savePopupTokenRef.current); savePopupTokenRef.current = null; } }}
                />
                <div className="relative bg-white text-slate-900 rounded-lg shadow-xl w-[90%] max-w-sm p-4">
                  <h3 className="text-lg font-semibold mb-3">{t('startPage.saveChats')}</h3>
                  <div className="space-y-2">
                    <button
                      className="w-full text-left px-3 py-2 rounded bg-slate-100 hover:bg-slate-200 disabled:opacity-60"
                      disabled={isSavingInProgress}
                      onClick={async () => {
                        try {
                          clearTempSelections();
                          setIsSavingInProgress(true);
                          if (!savePopupTokenRef.current) savePopupTokenRef.current = startUiTask('save-popup');
                          await onSaveAllChats();
                          setIsSavePopupOpen(false);
                        } finally {
                          setIsSavingInProgress(false);
                          endUiTask(savePopupTokenRef.current);
                          savePopupTokenRef.current = null;
                        }
                      }}
                    >
                      {t('startPage.saveToDevice')}
                    </button>
                    <div className="pt-1 border-t border-slate-200" />
                    <button
                      className="w-full text-left px-3 py-2 rounded bg-red-100 hover:bg-red-200 text-red-800 disabled:opacity-60"
                      disabled={isSavingInProgress}
                      onClick={() => {
                        clearTempSelections();
                        if (!resetPopupTokenRef.current) resetPopupTokenRef.current = startUiTask('reset-popup');
                        setIsResetPopupOpen(true);
                      }}
                      title="Backup to device and wipe local data"
                    >
                      Backup & Reset (wipe local data)
                    </button>
                  </div>
                  <div className="mt-4 text-right">
                    <button
                      className="px-3 py-1 rounded bg-slate-200 hover:bg-slate-300 disabled:opacity-60"
                      disabled={isSavingInProgress}
                      onClick={() => { setIsSavePopupOpen(false); endUiTask(savePopupTokenRef.current); savePopupTokenRef.current = null; }}
                    >
                      {t('chat.imageGenModal.close')}
                    </button>
                  </div>
                  {isSavingInProgress && (
                    <div className="absolute inset-0 bg-white/70 rounded-lg flex items-center justify-center" aria-live="polite" aria-busy="true">
                      <div className="flex items-center gap-2 text-slate-700">
                        <svg className="animate-spin h-5 w-5 text-slate-700" viewBox="0 0 24 24">
                          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
                        </svg>
                        <span>Processing…</span>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            )}
            {isResetPopupOpen && (
              <div className="fixed inset-0 z-[60] flex items-center justify-center">
                <div
                  className="absolute inset-0 bg-black/50"
                  onClick={() => { if (!isResetting) { setIsResetPopupOpen(false); endUiTask(resetPopupTokenRef.current); resetPopupTokenRef.current = null; } }}
                />
                <div className="relative bg-white text-slate-900 rounded-lg shadow-xl w-[92%] max-w-md p-4">
                  <h3 className="text-lg font-semibold mb-2">Backup & Reset</h3>
                  <p className="text-sm text-slate-700 mb-3">This will create a local backup file only, then wipe all local data (chats, settings, assets). You can restore later by loading the backup file.</p>
                  <label className="block text-xs font-medium text-slate-600 mb-1">Backup file name</label>
                  <input
                    type="text"
                    className="w-full px-2 py-1 border rounded text-sm mb-3"
                    value={resetName}
                    onChange={(e) => setResetName(e.target.value)}
                    placeholder="e.g., my-clean-start"
                    disabled={isResetting}
                  />
                  <div className="p-2 mb-3 rounded border border-red-200 bg-red-50 text-red-800 text-xs">
                    Type DELETE to confirm you understand this will erase all local data after saving the backup.
                  </div>
                  <input
                    type="text"
                    className="w-full px-2 py-1 border rounded text-sm mb-3"
                    value={resetConfirm}
                    onChange={(e) => setResetConfirm(e.target.value)}
                    placeholder="Type DELETE to confirm"
                    disabled={isResetting}
                    aria-label="Confirm by typing DELETE"
                  />
                  <div className="flex justify-end gap-2 mt-2">
                    <button
                      className="px-3 py-1 rounded bg-slate-200 hover:bg-slate-300 disabled:opacity-60"
                      onClick={() => { setIsResetPopupOpen(false); endUiTask(resetPopupTokenRef.current); resetPopupTokenRef.current = null; }}
                      disabled={isResetting}
                    >
                      Cancel
                    </button>
                    <button
                      className="px-3 py-1 rounded bg-red-600 text-white hover:bg-red-500 disabled:opacity-60"
                      disabled={isResetting || resetConfirm.trim() !== 'DELETE' || !resetName.trim()}
                      onClick={async () => {
                        if (resetConfirm.trim() !== 'DELETE' || !resetName.trim()) return;
                        const safe = resetName.trim().replace(/[^\w\- ]+/g, '').replace(/\s+/g, '_');
                        const filename = safe.length > 0 ? `${safe}.json` : undefined;
                        try {
                          setIsResetting(true);
                          await onSaveAllChats({ filename });
                          await new Promise(r => setTimeout(r, 3000));
                          await wipeLocalMemoryAndDb();
                          setIsResetPopupOpen(false);
                          setIsSavePopupOpen(false);
                          try { endUiTask(resetPopupTokenRef.current); resetPopupTokenRef.current = null; } catch {}
                          window.location.reload();
                        } catch (e) {
                          console.error('Backup & Reset failed', e);
                          alert('Backup & Reset failed. Please try again.');
                        } finally {
                          setIsResetting(false);
                        }
                      }}
                    >
                      Backup and Delete
                    </button>
                  </div>
                  {isResetting && (
                    <div className="absolute inset-0 bg-white/70 rounded-lg flex items-center justify-center" aria-live="polite" aria-busy="true">
                      <div className="flex items-center gap-2 text-slate-700">
                        <svg className="animate-spin h-5 w-5 text-slate-700" viewBox="0 0 24 24">
                          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
                        </svg>
                        <span>Backing up and wiping…</span>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            )}
            {isLoadPopupOpen && (
              <div className="fixed inset-0 z-50 flex items-center justify-center">
                <div
                  className="absolute inset-0 bg-black/50"
                  onClick={() => { if (!isLoadingInProgress) { setIsLoadPopupOpen(false); endUiTask(loadPopupTokenRef.current); loadPopupTokenRef.current = null; } }}
                />
                <div className="relative bg-white text-slate-900 rounded-lg shadow-xl w-[90%] max-w-sm p-4">
                  <h3 className="text-lg font-semibold mb-3">{t('startPage.loadChats')}</h3>
                  <div className="space-y-2">
                    <button
                      className="w-full text-left px-3 py-2 rounded bg-slate-100 hover:bg-slate-200 disabled:opacity-60"
                      disabled={isLoadingInProgress}
                      onClick={() => { 
                        clearTempSelections();
                        if (!loadPopupTokenRef.current) loadPopupTokenRef.current = startUiTask('load-popup'); 
                        handleLoadClick(); 
                      }}
                    >
                      {t('startPage.loadFromDevice')}
                    </button>
                  </div>
                  <div className="mt-4 text-right">
                    <button
                      className="px-3 py-1 rounded bg-slate-200 hover:bg-slate-300 disabled:opacity-60"
                      disabled={isLoadingInProgress}
                      onClick={() => { setIsLoadPopupOpen(false); endUiTask(loadPopupTokenRef.current); loadPopupTokenRef.current = null; }}
                    >
                      {t('chat.imageGenModal.close')}
                    </button>
                  </div>
                  {isLoadingInProgress && (
                    <div className="absolute inset-0 bg-white/70 rounded-lg flex items-center justify-center" aria-live="polite" aria-busy="true">
                      <div className="flex items-center gap-2 text-slate-700">
                        <svg className="animate-spin h-5 w-5 text-slate-700" viewBox="0 0 24 24">
                          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
                        </svg>
                        <span>Processing…</span>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            )}
        </div>
    );
};

export default LanguageSelectionBubble;