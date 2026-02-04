// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { SmallSpinner } from '../../../shared/ui/SmallSpinner';
import {
  IconPencil,
  IconSave,
  IconFolderOpen,
  IconTrash,
  IconCheck,
  IconUndo,
  IconPlus,
  IconSpeaker,
  IconWind,
  IconMask,
  IconCompass,
  IconShield,
  IconBolt,
  IconSwap,
  IconBookmark,
  IconScissors,
} from '../../../shared/ui/Icons';

import { getGlobalProfileDB, setGlobalProfileDB } from '../services/globalProfile';
import { getMaestroProfileImageDB, setMaestroProfileImageDB, clearMaestroProfileImageDB, MaestroProfileAsset } from '../../../core/db/assets';
import { uploadMediaToFiles, deleteFileByNameOrUri } from '../../../api/gemini/files';
import { DB_NAME } from '../../../core/db/index';
import { useMaestroStore } from '../../../store';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../core/config/activityTokens';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { useDataBackup } from '../hooks/useDataBackup';
import { GEMINI_VOICES } from '../../../core/config/app';
import { selectSettings } from '../../../store/slices/settingsSlice';

const SessionControls: React.FC = () => {
  const { t } = useAppTranslations();
  const { handleSaveAllChats, handleLoadAllChats, handleSaveCurrentChat, handleAppendToCurrentChat, handleTrimBeforeBookmark } = useDataBackup({ t });

  const settings = useMaestroStore(selectSettings);
  const updateSetting = useMaestroStore(state => state.updateSetting);
  const currentVoiceName = settings.tts.voiceName || 'Kore';

  const addActivityToken = useMaestroStore(state => state.addActivityToken);
  const removeActivityToken = useMaestroStore(state => state.removeActivityToken);
  const createUiToken = useCallback(
    (subtype: string) =>
      addActivityToken(
        TOKEN_CATEGORY.UI,
        `${subtype}:${Date.now()}:${Math.random().toString(36).slice(2, 8)}`
      ),
    [addActivityToken]
  );
  const endUiTask = useCallback((token: string | null) => {
    if (token) removeActivityToken(token);
  }, [removeActivityToken]);

  const [maestroAsset, setMaestroAsset] = useState<MaestroProfileAsset | null>(null);
  const [isUploadingMaestro, setIsUploadingMaestro] = useState(false);
  const [controlMode, setControlMode] = useState<'none' | 'all' | 'this'>('none');
  const [isEditingProfile, setIsEditingProfile] = useState(false);
  const [profileText, setProfileText] = useState('');

  
  // Unified pending action confirmation system
  type PendingActionType = 'none' | 'saveAll' | 'loadAll' | 'reset' | 'saveThis' | 'combine' | 'trim';
  const [pendingAction, setPendingAction] = useState<PendingActionType>('none');
  const [confirmInput, setConfirmInput] = useState('');
  const [pendingFile, setPendingFile] = useState<File | null>(null);
  
  // Action configuration: keyword, color scheme, description
  const ACTION_CONFIG: Record<PendingActionType, { keyword: string; label: string; description: string; colorClass: string; bgClass: string; borderClass: string; textClass: string; placeholderClass: string; btnClass: string; shadowClass: string }> = {
    none: { keyword: '', label: '', description: '', colorClass: '', bgClass: '', borderClass: '', textClass: '', placeholderClass: '', btnClass: '', shadowClass: '' },
    saveAll: { keyword: 'SAVE', label: t('sessionControls.saveAll.label') || 'Save All', description: t('sessionControls.saveAll.description') || 'Export all chats to backup file', colorClass: 'text-emerald-300', bgClass: 'bg-emerald-950/30', borderClass: 'border-emerald-500/30', textClass: 'text-emerald-100', placeholderClass: 'placeholder-emerald-400/30', btnClass: 'bg-emerald-500/80 hover:bg-emerald-500', shadowClass: 'shadow-emerald-900/20' },
    loadAll: { keyword: 'LOAD', label: t('sessionControls.loadAll.label') || 'Load All', description: t('sessionControls.loadAll.description') || 'Replace all chats with backup file', colorClass: 'text-blue-300', bgClass: 'bg-blue-950/30', borderClass: 'border-blue-500/30', textClass: 'text-blue-100', placeholderClass: 'placeholder-blue-400/30', btnClass: 'bg-blue-500/80 hover:bg-blue-500', shadowClass: 'shadow-blue-900/20' },
    reset: { keyword: 'DELETE', label: t('sessionControls.reset.label') || 'Reset', description: t('sessionControls.reset.description') || 'Backup & delete all data', colorClass: 'text-red-300', bgClass: 'bg-red-950/30', borderClass: 'border-red-500/30', textClass: 'text-red-100', placeholderClass: 'placeholder-red-400/30', btnClass: 'bg-red-500/80 hover:bg-red-500', shadowClass: 'shadow-red-900/20' },
    saveThis: { keyword: 'SAVE', label: t('sessionControls.saveThis.label') || 'Save Chat', description: t('sessionControls.saveThis.description') || 'Export this chat only', colorClass: 'text-cyan-300', bgClass: 'bg-cyan-950/30', borderClass: 'border-cyan-500/30', textClass: 'text-cyan-100', placeholderClass: 'placeholder-cyan-400/30', btnClass: 'bg-cyan-500/80 hover:bg-cyan-500', shadowClass: 'shadow-cyan-900/20' },
    combine: { keyword: 'COMBINE', label: t('sessionControls.combine.label') || 'Combine', description: t('sessionControls.combine.description') || 'Merge backup into this chat', colorClass: 'text-violet-300', bgClass: 'bg-violet-950/30', borderClass: 'border-violet-500/30', textClass: 'text-violet-100', placeholderClass: 'placeholder-violet-400/30', btnClass: 'bg-violet-500/80 hover:bg-violet-500', shadowClass: 'shadow-violet-900/20' },
    trim: { keyword: 'TRIM', label: t('sessionControls.trim.label') || 'Trim', description: t('sessionControls.trim.description') || 'Remove messages before bookmark', colorClass: 'text-orange-300', bgClass: 'bg-orange-950/30', borderClass: 'border-orange-500/30', textClass: 'text-orange-100', placeholderClass: 'placeholder-orange-400/30', btnClass: 'bg-orange-500/80 hover:bg-orange-500', shadowClass: 'shadow-orange-900/20' },
  };

  // Unified pointer handling for avatar cluster
  const [isAvatarExpanded, setIsAvatarExpanded] = useState(false);
  const [highlightedSide, setHighlightedSide] = useState<'left' | 'right' | null>(null);
  const pointerTypeRef = useRef<'mouse' | 'touch' | 'pen' | null>(null);
  const activePointerIdRef = useRef<number | null>(null);
  const collapseTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const saveTokenRef = useRef<string | null>(null);
  const loadTokenRef = useRef<string | null>(null);
  const saveCurrentTokenRef = useRef<string | null>(null);
  const appendTokenRef = useRef<string | null>(null);
  const trimTokenRef = useRef<string | null>(null);
  const maestroUploadTokenRef = useRef<string | null>(null);
  const maestroAvatarOpenTokenRef = useRef<string | null>(null);
  const loadFileInputRef = useRef<HTMLInputElement>(null);
  const appendFileInputRef = useRef<HTMLInputElement>(null);
  const maestroFileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        const a = await getMaestroProfileImageDB();
        if (mounted) setMaestroAsset(a);
      } catch { }
    })();
    return () => { mounted = false; };
  }, []);

  // Cleanup collapse timer on unmount
  useEffect(() => {
    return () => {
      if (collapseTimeoutRef.current) clearTimeout(collapseTimeoutRef.current);
    };
  }, []);

  useEffect(() => {
    const handler = (e: any) => {
      try {
        const d = e?.detail || {};
        if (d && (typeof d.dataUrl === 'string' || typeof d.uri === 'string')) {
          setMaestroAsset(prev => ({
            dataUrl: typeof d.dataUrl === 'string' ? d.dataUrl : prev?.dataUrl,
            mimeType: typeof d.mimeType === 'string' ? d.mimeType : prev?.mimeType,
            uri: typeof d.uri === 'string' ? d.uri : prev?.uri,
            updatedAt: Date.now(),
          }));
        } else {
          getMaestroProfileImageDB().then(a => setMaestroAsset(a)).catch(() => { });
        }
      } catch { }
    };
    window.addEventListener('maestro-avatar-updated', handler as any);
    return () => window.removeEventListener('maestro-avatar-updated', handler as any);
  }, []);

  const handleVoiceCycle = useCallback(() => {
    const idx = GEMINI_VOICES.findIndex(v => v.id === currentVoiceName);
    const next = GEMINI_VOICES[(idx + 1) % GEMINI_VOICES.length];
    updateSetting('tts', { ...settings.tts, voiceName: next.id });
  }, [currentVoiceName, updateSetting, settings.tts]);

  const startProfileEdit = async () => {
    try {
      const current = (await getGlobalProfileDB())?.text ?? '';
      setProfileText(current);
      setIsEditingProfile(true);
    } catch {
      setProfileText('');
      setIsEditingProfile(true);
    }
  };

  const handleProfileSave = async () => {
    try {
      await setGlobalProfileDB(profileText.trim());
      try { window.dispatchEvent(new CustomEvent('globalProfileUpdated')); } catch { }
    } finally {
      setIsEditingProfile(false);
    }
  };

  const wipeLocalMemoryAndDb = useCallback(async () => {
    try {
      await new Promise<void>((resolve) => {
        let settled = false;
        try {
          const req = indexedDB.deleteDatabase(DB_NAME);
          req.onsuccess = () => { settled = true; resolve(); };
          req.onerror = () => { resolve(); };
          req.onblocked = () => { resolve(); };
        } catch { resolve(); }
        setTimeout(() => { if (!settled) resolve(); }, 1500);
      });
    } catch { }
  }, []);

  const handleSwapAvatarClick = async () => {
    try {
      setIsUploadingMaestro(true);
      if (!maestroUploadTokenRef.current) {
        maestroUploadTokenRef.current = createUiToken(TOKEN_SUBTYPE.MAESTRO_AVATAR);
      }
    } catch { }

    try {
      const prevUri = maestroAsset?.uri;
      if (prevUri) {
        try { await deleteFileByNameOrUri(prevUri); } catch { }
      }
      try { await clearMaestroProfileImageDB(); } catch { }
      setMaestroAsset(null);
      try { window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: {} })); } catch { }
    } catch { }

    // Restore default avatar after clearing
    try {
      const man = await fetch('/maestro-avatars/manifest.json', { cache: 'force-cache' });
      let defaultFound = false;
      if (man.ok) {
        const list: string[] = await man.json();
        if (Array.isArray(list)) {
          for (const name of list) {
            try {
              const r = await fetch(`/maestro-avatars/${name}`, { cache: 'force-cache' });
              if (r.ok) {
                const blob = await r.blob();
                const mime = blob.type || 'image/png';
                const dataUrl: string = await new Promise((resolve, reject) => {
                  const fr = new FileReader();
                  fr.onloadend = () => resolve(fr.result as string);
                  fr.onerror = () => reject(fr.error || new Error('DataURL conversion failed'));
                  fr.readAsDataURL(blob);
                });
                let uploadedUri: string | undefined;
                let uploadedMimeType: string = mime;
                try {
                  const up = await uploadMediaToFiles(dataUrl, mime, 'maestro-avatar');
                  uploadedUri = up.uri;
                  uploadedMimeType = up.mimeType;
                } catch { }
                const asset: MaestroProfileAsset = { dataUrl, mimeType: uploadedMimeType, uri: uploadedUri, updatedAt: Date.now() };
                try { await setMaestroProfileImageDB(asset); } catch { }
                setMaestroAsset(asset);
                try { window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: asset })); } catch { }
                defaultFound = true;
                break;
              }
            } catch { }
          }
        }
      }
      if (!defaultFound) {
        setMaestroAsset(null);
        try { window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: {} })); } catch { }
      }
    } catch {
      setMaestroAsset(null);
      try { window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: {} })); } catch { }
    } finally {
      try { setIsUploadingMaestro(false); } catch { }
      try { if (maestroFileInputRef.current) { maestroFileInputRef.current.value = ''; maestroFileInputRef.current.click(); } } catch { }
      if (maestroUploadTokenRef.current) {
        endUiTask(maestroUploadTokenRef.current);
        maestroUploadTokenRef.current = null;
      }
    }
  };

  const handleMaestroFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    if (maestroAvatarOpenTokenRef.current) {
      endUiTask(maestroAvatarOpenTokenRef.current);
      maestroAvatarOpenTokenRef.current = null;
    }
    const file = event.target.files?.[0];
    if (!file) { event.target.value = ''; return; }
    if (!file.type.startsWith('image/')) { event.target.value = ''; return; }
    try {
      setIsUploadingMaestro(true);
      if (!maestroUploadTokenRef.current) {
        maestroUploadTokenRef.current = createUiToken(TOKEN_SUBTYPE.MAESTRO_AVATAR);
      }
      const dataUrl: string = await new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result));
        reader.onerror = (e) => reject(e);
        reader.readAsDataURL(file);
      });
      let uploadedUri: string | undefined;
      let uploadedMimeType: string = file.type;
      try {
        const up = await uploadMediaToFiles(dataUrl, file.type, 'maestro-avatar');
        uploadedUri = up.uri;
        uploadedMimeType = up.mimeType;
      } catch { }
      const asset: MaestroProfileAsset = { dataUrl, mimeType: uploadedMimeType, uri: uploadedUri, updatedAt: Date.now() };
      await setMaestroProfileImageDB(asset);
      setMaestroAsset(asset);
      try {
        window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: { uri: uploadedUri, mimeType: uploadedMimeType, dataUrl } }));
      } catch { }
    } catch (err) {
      console.error('Failed to save Maestro avatar:', err);
    } finally {
      setIsUploadingMaestro(false);
      event.target.value = '';
      if (maestroUploadTokenRef.current) {
        endUiTask(maestroUploadTokenRef.current);
        maestroUploadTokenRef.current = null;
      }
    }
  };

  const handleLoadFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (file) {
      // Stage file and show confirmation
      setPendingFile(file);
      setPendingAction('loadAll');
      setConfirmInput('');
    }
  };

  const handleAppendFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (file) {
      // Stage file and show confirmation
      setPendingFile(file);
      setPendingAction('combine');
      setConfirmInput('');
    }
  };

  const cancelPendingAction = () => {
    setPendingAction('none');
    setConfirmInput('');
    setPendingFile(null);
  };

  const executePendingAction = async () => {
    const config = ACTION_CONFIG[pendingAction];
    if (confirmInput.toUpperCase() !== config.keyword) return;
    
    try {
      switch (pendingAction) {
        case 'saveAll':
          if (!saveTokenRef.current) {
            saveTokenRef.current = createUiToken(TOKEN_SUBTYPE.SAVE_POPUP);
          }
          try {
            await handleSaveAllChats();
          } finally {
            if (saveTokenRef.current) {
              endUiTask(saveTokenRef.current);
              saveTokenRef.current = null;
            }
          }
          break;
          
        case 'loadAll':
          if (pendingFile && handleLoadAllChats) {
            if (!loadTokenRef.current) {
              loadTokenRef.current = createUiToken(TOKEN_SUBTYPE.LOAD_POPUP);
            }
            try {
              await handleLoadAllChats(pendingFile);
            } finally {
              if (loadTokenRef.current) {
                endUiTask(loadTokenRef.current);
                loadTokenRef.current = null;
              }
            }
          }
          break;
          
        case 'reset': {
          const safe = `backup-before-reset-${new Date().toISOString().slice(0, 10)}`;
          if (handleSaveAllChats) await handleSaveAllChats({ filename: `${safe}.ndjson`, auto: true });
          await new Promise(r => setTimeout(r, 500));
          await wipeLocalMemoryAndDb();
          window.location.reload();
          return; // Don't clear state - page reloads
        }
          
        case 'saveThis':
          if (!saveCurrentTokenRef.current) {
            saveCurrentTokenRef.current = createUiToken(TOKEN_SUBTYPE.SAVE_POPUP);
          }
          try {
            await handleSaveCurrentChat();
          } finally {
            if (saveCurrentTokenRef.current) {
              endUiTask(saveCurrentTokenRef.current);
              saveCurrentTokenRef.current = null;
            }
          }
          break;
          
        case 'combine':
          if (pendingFile && handleAppendToCurrentChat) {
            if (!appendTokenRef.current) {
              appendTokenRef.current = createUiToken(TOKEN_SUBTYPE.LOAD_POPUP);
            }
            try {
              await handleAppendToCurrentChat(pendingFile);
            } finally {
              if (appendTokenRef.current) {
                endUiTask(appendTokenRef.current);
                appendTokenRef.current = null;
              }
            }
          }
          break;
          
        case 'trim':
          if (!trimTokenRef.current) {
            trimTokenRef.current = createUiToken(TOKEN_SUBTYPE.SAVE_POPUP);
          }
          try {
            await handleTrimBeforeBookmark();
          } finally {
            if (trimTokenRef.current) {
              endUiTask(trimTokenRef.current);
              trimTokenRef.current = null;
            }
          }
          break;
      }
    } catch (err) {
      console.error('Action failed:', err);
    }
    
    cancelPendingAction();
  };

  // Data attribute for identifying interactive elements
  const DATA_AVATAR_ACTION = 'data-avatar-action';

  // Find the action target from an element (walks up the DOM tree)
  const getActionFromElement = useCallback((element: Element | null): string | null => {
    let current = element;
    while (current && current !== document.body) {
      const action = current.getAttribute(DATA_AVATAR_ACTION);
      if (action) return action;
      current = current.parentElement;
    }
    return null;
  }, []);

  // Clear collapse timer
  const clearCollapseTimer = useCallback(() => {
    if (collapseTimeoutRef.current) {
      clearTimeout(collapseTimeoutRef.current);
      collapseTimeoutRef.current = null;
    }
  }, []);

  // Start collapse timer
  const startCollapseTimer = useCallback((delay: number = 300) => {
    clearCollapseTimer();
    collapseTimeoutRef.current = setTimeout(() => {
      setIsAvatarExpanded(false);
      setHighlightedSide(null);
    }, delay);
  }, [clearCollapseTimer]);

  // Execute avatar action
  const executeAvatarAction = useCallback((action: string) => {
    if (action === 'swap' && !isUploadingMaestro) {
      handleSwapAvatarClick();
    } else if (action === 'voice') {
      handleVoiceCycle();
    }
    // Collapse after action
    startCollapseTimer(200);
  }, [isUploadingMaestro, handleVoiceCycle, startCollapseTimer]);

  // Unified pointer down handler for avatar cluster
  const handleAvatarPointerDown = useCallback((e: React.PointerEvent) => {
    pointerTypeRef.current = e.pointerType as 'mouse' | 'touch' | 'pen';
    activePointerIdRef.current = e.pointerId;
    clearCollapseTimer();

    if (pointerTypeRef.current === 'touch' || pointerTypeRef.current === 'pen') {
      // Touch/pen: expand and highlight
      setIsAvatarExpanded(true);
      const action = getActionFromElement(e.target as Element);
      setHighlightedSide(action === 'swap' ? 'left' : action === 'voice' ? 'right' : null);
      (e.target as Element).setPointerCapture?.(e.pointerId);
    }
  }, [getActionFromElement, clearCollapseTimer]);

  // Unified pointer move handler for avatar cluster
  const handleAvatarPointerMove = useCallback((e: React.PointerEvent) => {
    if (activePointerIdRef.current !== e.pointerId) return;

    if (pointerTypeRef.current === 'touch' || pointerTypeRef.current === 'pen') {
      // Touch/pen: update highlight based on element under pointer
      const element = document.elementFromPoint(e.clientX, e.clientY);
      const action = getActionFromElement(element);
      setHighlightedSide(action === 'swap' ? 'left' : action === 'voice' ? 'right' : null);
    }
  }, [getActionFromElement]);

  // Unified pointer up handler for avatar cluster
  const handleAvatarPointerUp = useCallback((e: React.PointerEvent) => {
    if (activePointerIdRef.current !== e.pointerId) return;

    (e.target as Element).releasePointerCapture?.(e.pointerId);

    // Get the element under the pointer at release
    const element = document.elementFromPoint(e.clientX, e.clientY);
    const action = getActionFromElement(element);

    if (pointerTypeRef.current === 'touch' || pointerTypeRef.current === 'pen') {
      // Touch/pen: execute action on release if over a valid target
      if (action) {
        executeAvatarAction(action);
      }
      // Always collapse and reset after touch ends (whether action executed or dragged away)
      setHighlightedSide(null);
      setIsAvatarExpanded(false);
    } else {
      // Mouse: execute action on click
      if (action) {
        executeAvatarAction(action);
      }
      setHighlightedSide(null);
    }

    activePointerIdRef.current = null;
  }, [getActionFromElement, executeAvatarAction]);

  // Pointer cancel handler
  const handleAvatarPointerCancel = useCallback((e: React.PointerEvent) => {
    if (activePointerIdRef.current === e.pointerId) {
      setHighlightedSide(null);
      setIsAvatarExpanded(false);
      activePointerIdRef.current = null;
    }
  }, []);

  // Mouse enter/leave for hover expansion (mouse only)
  const handleAvatarMouseEnter = useCallback(() => {
    if (pointerTypeRef.current !== 'touch' && pointerTypeRef.current !== 'pen') {
      clearCollapseTimer();
      setIsAvatarExpanded(true);
    }
  }, [clearCollapseTimer]);

  const handleAvatarMouseLeave = useCallback(() => {
    if (pointerTypeRef.current !== 'touch' && pointerTypeRef.current !== 'pen') {
      startCollapseTimer(300);
    }
  }, [startCollapseTimer]);

  // --- Render Helpers ---

  const getVoiceIcon = (voiceId: string): React.ComponentType<any> => {
    const VOICE_ICON_MAP: Record<string, React.ComponentType<any>> = {
      Zephyr: IconWind,
      Puck: IconMask,
      Charon: IconCompass,
      Kore: IconShield,
      Fenrir: IconBolt,
    };
    return VOICE_ICON_MAP[voiceId] || IconShield;
  };

  const getVoiceColorRing = (voiceId: string) => {
    const map: Record<string, string> = {
      Zephyr: 'ring-cyan-300/50 bg-cyan-900/40',
      Puck: 'ring-amber-300/50 bg-amber-900/40',
      Charon: 'ring-emerald-300/50 bg-emerald-900/40',
      Kore: 'ring-blue-300/50 bg-blue-900/40',
      Fenrir: 'ring-rose-300/50 bg-rose-900/40'
    };
    return map[voiceId] || 'ring-blue-300/50 bg-blue-900/40';
  };

  const VoiceIconComponent = getVoiceIcon(currentVoiceName);
  const voiceBgClass = getVoiceColorRing(currentVoiceName).split(' ')[1] || 'bg-blue-900/40';

  return (
    <div className="w-full py-3 px-4 min-h-[64px] flex items-center justify-between gap-2">

      {/* --- Mode: Pending Action Confirmation --- */}
      {pendingAction !== 'none' ? (
        <div className="flex-1 flex items-start animate-fade-in gap-2 min-w-0">
          <div className="flex flex-col flex-1 min-w-0 gap-1">
            <span className={`text-xs font-bold uppercase tracking-wider ${ACTION_CONFIG[pendingAction].colorClass}`}>
              {ACTION_CONFIG[pendingAction].label}:
            </span>
            <input
              className={`w-full ${ACTION_CONFIG[pendingAction].bgClass} border ${ACTION_CONFIG[pendingAction].borderClass} rounded px-2 py-1 text-sm ${ACTION_CONFIG[pendingAction].textClass} ${ACTION_CONFIG[pendingAction].placeholderClass} focus:outline-none focus:border-opacity-60 transition-colors`}
              placeholder={(t('sessionControls.typeToConfirm') || 'Type "{keyword}" to confirm').replace('{keyword}', ACTION_CONFIG[pendingAction].keyword)}
              value={confirmInput}
              onChange={(e) => setConfirmInput(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && executePendingAction()}
              autoFocus
            />
            <span className="text-xs text-slate-400 break-words">
              {ACTION_CONFIG[pendingAction].description}
            </span>
          </div>
          <div className="flex items-center gap-1 flex-shrink-0">
            <button
              type="button"
              onClick={executePendingAction}
              disabled={confirmInput.toUpperCase() !== ACTION_CONFIG[pendingAction].keyword}
              className={`p-2 ${ACTION_CONFIG[pendingAction].btnClass} rounded-full text-white disabled:opacity-30 disabled:cursor-not-allowed transition-all shadow-lg ${ACTION_CONFIG[pendingAction].shadowClass}`}
            >
              <IconCheck className="w-4 h-4" />
            </button>
            <button
              type="button"
              onClick={cancelPendingAction}
              className="p-2 bg-slate-700/50 hover:bg-slate-600 rounded-full text-slate-300 transition-colors"
            >
              <IconUndo className="w-4 h-4" />
            </button>
          </div>
        </div>

      ) : isEditingProfile ? (
        <div className="flex-1 flex items-start justify-between animate-fade-in gap-2">
          <div className="flex flex-col gap-1 flex-1 min-w-0">
            <span className="text-xs font-bold text-blue-300 uppercase tracking-wider">{t('sessionControls.profile') || 'Profile:'}</span>
            <input
              className="w-full max-w-[520px] bg-blue-950/30 border border-blue-500/30 rounded px-2 py-1.5 text-sm text-blue-100 placeholder-blue-400/30 focus:outline-none focus:border-blue-400 focus:bg-blue-950/50 transition-colors"
              placeholder={t('sessionControls.profilePlaceholder') || 'Your name or details...'}
              value={profileText}
              onChange={(e) => setProfileText(e.target.value)}
              autoFocus
              onKeyDown={(e) => e.key === 'Enter' && handleProfileSave()}
            />
          </div>
          <div className="flex items-center gap-1 flex-shrink-0">
            <button
              type="button"
              onClick={handleProfileSave}
              className="p-2 bg-green-500/80 hover:bg-green-500 rounded-full text-white transition-all shadow-lg shadow-green-900/20"
            >
              <IconCheck className="w-4 h-4" />
            </button>
            <button
              type="button"
              onClick={() => setIsEditingProfile(false)}
              className="p-2 bg-slate-700/50 hover:bg-slate-600 rounded-full text-slate-300 transition-colors"
            >
              <IconUndo className="w-4 h-4" />
            </button>
          </div>
        </div>

      ) : (
        <>
          {/* Left: Edit Profile */}
          <div className="flex items-center">
            <button
              type="button"
              onClick={startProfileEdit}
              className="group p-2.5 rounded-full text-slate-400 hover:text-white hover:bg-white/10 transition-all"
              title={t('sessionControls.editProfile') || 'Edit User Profile'}
            >
              <IconPencil className="w-4 h-4 opacity-70 group-hover:opacity-100" />
            </button>
          </div>

          {/* Center: Action Pill - Grouped Controls */}
          <div className="flex items-center bg-slate-800/60 backdrop-blur-sm rounded-full p-1 border border-white/5 shadow-inner">
            {controlMode === 'none' ? (
              /* Default: Show two group selectors */
              <>
                <button type="button" onClick={() => setControlMode('all')} className="px-3 py-1.5 hover:bg-white/10 rounded-full text-slate-300 hover:text-white transition-colors text-xs font-medium" title={t('sessionControls.allChatsControls') || 'All Chats Controls'}>
                  {'🔧🌐💬'}
                </button>
                <div className="w-px h-4 bg-white/10 mx-0.5"></div>
                <button type="button" onClick={() => setControlMode('this')} className="px-3 py-1.5 hover:bg-white/10 rounded-full text-slate-300 hover:text-white transition-colors text-xs font-medium" title={t('sessionControls.thisChatsControls') || 'This Chat Controls'}>
                  {'🔧🎯💬'}
                </button>
              </>
            ) : controlMode === 'all' ? (
              /* All Chats: Save All, Load All, Reset, Back */
              <>
                <button type="button" onClick={() => setControlMode('none')} className="p-2 hover:bg-white/10 rounded-full text-slate-400 hover:text-white transition-colors" title={t('sessionControls.back') || 'Back'}>
                  <IconUndo className="w-4 h-4" />
                </button>
                <div className="w-px h-4 bg-white/10 mx-0.5"></div>
                <button type="button" onClick={() => { setPendingAction('saveAll'); setConfirmInput(''); }} className="p-2 hover:bg-white/10 rounded-full text-slate-300 hover:text-white transition-colors" title={t('startPage.saveChats') || 'Save All Chats'}>
                  <IconSave className="w-4 h-4" />
                </button>
                <div className="w-px h-4 bg-white/10 mx-0.5"></div>
                <button type="button" onClick={() => loadFileInputRef.current?.click()} className="p-2 hover:bg-white/10 rounded-full text-slate-300 hover:text-white transition-colors" title={t('startPage.loadChats') || 'Load All Chats'}>
                  <IconFolderOpen className="w-4 h-4" />
                </button>
                <div className="w-px h-4 bg-white/10 mx-0.5"></div>
                <button type="button" onClick={() => { setPendingAction('reset'); setConfirmInput(''); }} className="p-2 hover:bg-red-500/20 rounded-full text-slate-300 hover:text-red-200 transition-colors" title={t('sessionControls.backupAndReset') || 'Backup & Reset'}>
                  <IconTrash className="w-4 h-4" />
                </button>
              </>
            ) : (
              /* This Chat: Save This, Combine, Trim, Back */
              <>
                <button type="button" onClick={() => setControlMode('none')} className="p-2 hover:bg-white/10 rounded-full text-slate-400 hover:text-white transition-colors" title={t('sessionControls.back') || 'Back'}>
                  <IconUndo className="w-4 h-4" />
                </button>
                <div className="w-px h-4 bg-white/10 mx-0.5"></div>
                <button type="button" onClick={() => { setPendingAction('saveThis'); setConfirmInput(''); }} className="p-2 hover:bg-white/10 rounded-full text-slate-300 hover:text-white transition-colors" title={t('startPage.saveThisChat') || 'Save This Chat'}>
                  <IconBookmark className="w-4 h-4" />
                </button>
                <div className="w-px h-4 bg-white/10 mx-0.5"></div>
                <button type="button" onClick={() => appendFileInputRef.current?.click()} className="p-2 hover:bg-white/10 rounded-full text-slate-300 hover:text-white transition-colors" title={t('startPage.appendToChat') || 'Combine Chats'}>
                  <IconPlus className="w-4 h-4" />
                </button>
                <div className="w-px h-4 bg-white/10 mx-0.5"></div>
                <button type="button" onClick={() => { setPendingAction('trim'); setConfirmInput(''); }} className="p-2 hover:bg-orange-500/20 rounded-full text-slate-300 hover:text-orange-200 transition-colors" title={t('startPage.trimBeforeBookmark') || 'Trim Before Bookmark'}>
                  <IconScissors className="w-4 h-4" />
                </button>
              </>
            )}
          </div>
          <input type="file" ref={loadFileInputRef} onChange={handleLoadFileChange} accept=".ndjson,.jsonl" className="hidden" />
          <input type="file" ref={appendFileInputRef} onChange={handleAppendFileChange} accept=".ndjson,.jsonl" className="hidden" />

          {/* Right: Maestro Avatar Cluster - unified pointer event handling */}
          <div
            className="relative flex items-center justify-center w-14 h-10 select-none"
            onPointerDown={handleAvatarPointerDown}
            onPointerMove={handleAvatarPointerMove}
            onPointerUp={handleAvatarPointerUp}
            onPointerCancel={handleAvatarPointerCancel}
            onMouseEnter={handleAvatarMouseEnter}
            onMouseLeave={handleAvatarMouseLeave}
            style={{ touchAction: 'none' }}
          >
            {/* Left Wing: Swap/Upload */}
            <button
              type="button"
              {...{ [DATA_AVATAR_ACTION]: 'swap' }}
              className={`absolute left-1/2 -translate-x-[24px] top-1/2 -translate-y-1/2 w-8 h-8 rounded-full bg-slate-800 border border-slate-600 flex items-center justify-center text-slate-400 hover:text-white hover:bg-slate-700 hover:border-slate-500 transition-all duration-200 shadow-lg z-0 ${
                isAvatarExpanded ? '-translate-x-[36px]' : ''
              } ${
                highlightedSide === 'left' ? 'text-white bg-slate-700 border-slate-500 z-20 scale-110 ring-2 ring-white/30' : 'hover:z-20 hover:scale-105'
              }`}
              title={t('general.clear') + " / " + (t('sessionControls.changeAvatar') || 'Change Avatar')}
            >
              <IconSwap className="w-3.5 h-3.5" />

              <IconSwap
                className={`absolute left-0 top-1/2 -translate-y-1/2 -translate-x-2.5 w-3 h-3 text-slate-500 transition-opacity duration-200 ${isAvatarExpanded ? 'opacity-0' : 'opacity-100'}`}
              />
            </button>
 
            {/* Right Wing: Voice Toggle */}
            <button
              type="button"
              {...{ [DATA_AVATAR_ACTION]: 'voice' }}
              className={`absolute right-1/2 translate-x-[24px] top-1/2 -translate-y-1/2 w-8 h-8 rounded-full border flex items-center justify-center transition-all duration-200 shadow-lg z-0 ${getVoiceColorRing(currentVoiceName)} ring-1 border-white/10 ${
                isAvatarExpanded ? 'translate-x-[36px]' : ''
              } ${
                highlightedSide === 'right' ? 'z-20 scale-110 ring-2 ring-white/30' : 'hover:z-20 hover:scale-105'
              }`}
              title={`Voice: ${currentVoiceName}`}
            >
              <div className="absolute inset-0 pointer-events-none">
                <div
                  key={currentVoiceName}
                  className={`absolute inset-0 rounded-full ${voiceBgClass} animate-voice-ripple`}
                />
              </div>

              <VoiceIconComponent
                key={currentVoiceName}
                className="w-3.5 h-3.5 text-white/90 animate-voice-swap"
              />

              <IconSpeaker
                className={`absolute right-0 top-1/2 -translate-y-1/2 translate-x-2.5 w-3 h-3 text-white/70 transition-opacity duration-200 ${isAvatarExpanded ? 'opacity-0' : 'opacity-100'}`}
              />
            </button>

            {/* Center: The Avatar (Visual Anchor) */}
            <div className="relative z-10 w-10 h-10 pointer-events-none">
              <div
                className={`w-full h-full rounded-full overflow-hidden border-2 flex items-center justify-center bg-slate-900 transition-all duration-300
                    ${maestroAsset?.dataUrl
                    ? 'border-blue-400/60 shadow-[0_0_15px_rgba(96,165,250,0.5)]'
                    : 'border-slate-600 border-dashed opacity-80'}`}
              >
                {maestroAsset?.dataUrl ? (
                  <img src={maestroAsset.dataUrl} alt={t('startPage.maestroAvatar') || 'Maestro avatar'} className="w-full h-full object-cover" />
                ) : (
                  <IconPlus className="w-4 h-4 text-slate-500" />
                )}

                {isUploadingMaestro && (
                  <div className="absolute inset-0 bg-black/60 flex items-center justify-center backdrop-blur-[1px]">
                    <SmallSpinner className="w-4 h-4 text-white" />
                  </div>
                )}
              </div>
            </div>

            {/* Hidden Input for upload */}
            <input type="file" ref={maestroFileInputRef} onChange={handleMaestroFileChange} accept="image/*" className="hidden" />
          </div>
        </>
      )}
    </div>
  );
};

export default SessionControls;
