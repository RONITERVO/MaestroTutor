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
  const { handleSaveAllChats, handleLoadAllChats } = useDataBackup({ t });

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
  const [resetMode, setResetMode] = useState(false);
  const [resetConfirm, setResetConfirm] = useState('');
  const [isEditingProfile, setIsEditingProfile] = useState(false);
  const [profileText, setProfileText] = useState('');

  // Touch support for avatar cluster
  const [isTouchActive, setIsTouchActive] = useState(false);
  const [touchSide, setTouchSide] = useState<'left' | 'right' | null>(null);
  const avatarClusterRef = useRef<HTMLDivElement>(null);
  const recentTouch = useRef(false);

  const saveTokenRef = useRef<string | null>(null);
  const loadTokenRef = useRef<string | null>(null);
  const maestroUploadTokenRef = useRef<string | null>(null);
  const maestroAvatarOpenTokenRef = useRef<string | null>(null);
  const loadFileInputRef = useRef<HTMLInputElement>(null);
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
    if (file && handleLoadAllChats) {
      try {
        if (!loadTokenRef.current) {
          loadTokenRef.current = createUiToken(TOKEN_SUBTYPE.LOAD_POPUP);
        }
        await handleLoadAllChats(file);
      } finally {
        if (loadTokenRef.current) {
          endUiTask(loadTokenRef.current);
          loadTokenRef.current = null;
        }
      }
    }
    event.target.value = '';
  };

  const handleSave = async () => {
    if (handleSaveAllChats) {
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
    }
  };

  const handleResetConfirm = async () => {
    if (resetConfirm !== 'DELETE') return;
    try {
      const safe = `backup-before-reset-${new Date().toISOString().slice(0, 10)}`;
      if (handleSaveAllChats) await handleSaveAllChats({ filename: `${safe}.json`, auto: true });
      await new Promise(r => setTimeout(r, 500));
      await wipeLocalMemoryAndDb();
      window.location.reload();
    } catch {
      setResetConfirm('');
    }
  };

  // Click handlers that ignore emulated clicks after touch gestures
  const handleLeftButtonClick = useCallback(() => {
    if (recentTouch.current) {
      recentTouch.current = false;
      return;
    }
    handleSwapAvatarClick();
  }, [isUploadingMaestro]); // Note: isUploadingMaestro check is now in the button's conditional onClick

  const handleRightButtonClick = useCallback(() => {
    if (recentTouch.current) {
      recentTouch.current = false;
      return;
    }
    handleVoiceCycle();
  }, [handleVoiceCycle]);

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
    <div className="w-full py-3 px-4 min-h-[64px] flex items-center justify-between gap-4">

      {/* --- Mode: Reset Confirmation --- */}
      {resetMode ? (
        <div className="flex-1 flex items-center justify-between animate-fade-in gap-3">
          <div className="flex items-center gap-2 flex-1">
            <span className="text-xs font-bold text-red-300 uppercase tracking-wider whitespace-nowrap">Reset:</span>
            <input
              className="flex-1 min-w-[100px] bg-red-950/30 border border-red-500/30 rounded px-2 py-1.5 text-sm text-red-100 placeholder-red-400/30 focus:outline-none focus:border-red-400 focus:bg-red-950/50 transition-colors"
              placeholder='Type "DELETE"'
              value={resetConfirm}
              onChange={(e) => setResetConfirm(e.target.value)}
              autoFocus
            />
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={handleResetConfirm}
              disabled={resetConfirm !== 'DELETE'}
              className="p-2 bg-red-500/80 hover:bg-red-500 rounded-full text-white disabled:opacity-30 disabled:cursor-not-allowed transition-all shadow-lg shadow-red-900/20"
            >
              <IconCheck className="w-4 h-4" />
            </button>
            <button
              type="button"
              onClick={() => { setResetMode(false); setResetConfirm(''); }}
              className="p-2 bg-slate-700/50 hover:bg-slate-600 rounded-full text-slate-300 transition-colors"
            >
              <IconUndo className="w-4 h-4" />
            </button>
          </div>
        </div>

      ) : isEditingProfile ? (
        <div className="flex-1 flex items-center justify-between animate-fade-in gap-3">
          <div className="flex items-center gap-2 flex-1">
            <span className="text-xs font-bold text-blue-300 uppercase tracking-wider whitespace-nowrap">Profile:</span>
            <input
              className="flex-1 min-w-[100px] bg-blue-950/30 border border-blue-500/30 rounded px-2 py-1.5 text-sm text-blue-100 placeholder-blue-400/30 focus:outline-none focus:border-blue-400 focus:bg-blue-950/50 transition-colors"
              placeholder="Your name or details..."
              value={profileText}
              onChange={(e) => setProfileText(e.target.value)}
              autoFocus
              onKeyDown={(e) => e.key === 'Enter' && handleProfileSave()}
            />
          </div>
          <div className="flex items-center gap-2">
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
              title="Edit User Profile"
            >
              <IconPencil className="w-4 h-4 opacity-70 group-hover:opacity-100" />
            </button>
          </div>

          {/* Center: Action Pill */}
          <div className="flex items-center bg-slate-800/60 backdrop-blur-sm rounded-full p-1 border border-white/5 shadow-inner">
            <button type="button" onClick={handleSave} className="p-2 hover:bg-white/10 rounded-full text-slate-300 hover:text-white transition-colors" title={t('startPage.saveChats')}>
              <IconSave className="w-4 h-4" />
            </button>
            <div className="w-px h-4 bg-white/10 mx-0.5"></div>
            <button type="button" onClick={() => loadFileInputRef.current?.click()} className="p-2 hover:bg-white/10 rounded-full text-slate-300 hover:text-white transition-colors" title={t('startPage.loadChats')}>
              <IconFolderOpen className="w-4 h-4" />
            </button>
            <div className="w-px h-4 bg-white/10 mx-0.5"></div>
            <button type="button" onClick={() => setResetMode(true)} className="p-2 hover:bg-red-500/20 rounded-full text-slate-300 hover:text-red-200 transition-colors" title="Backup & Reset">
              <IconTrash className="w-4 h-4" />
            </button>
          </div>
          <input type="file" ref={loadFileInputRef} onChange={handleLoadFileChange} accept=".json" className="hidden" />

          {/* Right: Maestro Avatar Cluster */}
          <div
            ref={avatarClusterRef}
            className="relative group flex items-center justify-center w-14 h-10"
            onTouchStart={(e) => {
              recentTouch.current = true;
              if (!avatarClusterRef.current) return;
              const touch = e.touches[0];
              const rect = avatarClusterRef.current.getBoundingClientRect();
              const relX = (touch.clientX - rect.left) / rect.width;
              setTouchSide(relX < 0.4 ? 'left' : relX > 0.6 ? 'right' : null);
              setIsTouchActive(true);
              e.preventDefault();
            }}
            onTouchMove={(e) => {
              if (!avatarClusterRef.current) return;
              const touch = e.touches[0];
              const rect = avatarClusterRef.current.getBoundingClientRect();
              const relX = (touch.clientX - rect.left) / rect.width;
              setTouchSide(relX < 0.4 ? 'left' : relX > 0.6 ? 'right' : null);
              e.preventDefault();
            }}
            onTouchEnd={(e) => {
              if (!avatarClusterRef.current) return;
              const touch = e.changedTouches[0];
              if (touch) {
                const rect = avatarClusterRef.current.getBoundingClientRect();
                const relX = (touch.clientX - rect.left) / rect.width;
                if (relX < 0.4) {
                  if (!isUploadingMaestro) handleSwapAvatarClick();
                } else if (relX > 0.6) {
                  handleVoiceCycle();
                }
              }
              setIsTouchActive(false);
              setTouchSide(null);
              setTimeout(() => { recentTouch.current = false; }, 500);
            }}
          >
            {/* Left Wing: Swap/Upload */}
            <button
              type="button"
              onClick={isUploadingMaestro ? undefined : handleLeftButtonClick}
              className={`absolute left-1/2 -translate-x-[24px] top-1/2 -translate-y-1/2 w-8 h-8 rounded-full bg-slate-800 border border-slate-600 flex items-center justify-center text-slate-400 hover:text-white hover:bg-slate-700 hover:border-slate-500 hover:z-20 hover:scale-110 transition-all duration-300 shadow-lg z-0 group-hover:-translate-x-[36px] ${isTouchActive ? '-translate-x-[36px] text-white bg-slate-700 border-slate-500 z-20 scale-110' : ''} ${touchSide === 'left' ? 'text-white bg-slate-700 border-slate-500 z-20 scale-110' : ''}`}
              title={t('general.clear') + " / Change Avatar"}
            >
              <IconSwap className="w-3.5 h-3.5" />

              <IconSwap
                className={`absolute left-0 top-1/2 -translate-y-1/2 -translate-x-2.5 w-3 h-3 text-slate-500 opacity-100 group-hover:opacity-0 transition-opacity duration-300 ${isTouchActive ? 'opacity-0' : ''}`}
              />
            </button>

            {/* Right Wing: Voice Toggle */}
            <button
              type="button"
              onClick={handleRightButtonClick}
              className={`absolute right-1/2 translate-x-[24px] top-1/2 -translate-y-1/2 w-8 h-8 rounded-full border flex items-center justify-center hover:z-20 hover:scale-110 transition-all duration-300 shadow-lg z-0 group-hover:translate-x-[36px] ${getVoiceColorRing(currentVoiceName)} ring-1 border-white/10 ${isTouchActive ? 'translate-x-[36px]' : ''} ${touchSide === 'right' ? 'z-20 scale-110' : ''}`}
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
                className={`absolute right-0 top-1/2 -translate-y-1/2 translate-x-2.5 w-3 h-3 text-white/70 opacity-100 group-hover:opacity-0 transition-opacity duration-300 ${isTouchActive ? 'opacity-0' : ''}`}
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
                  <img src={maestroAsset.dataUrl} alt="Maestro" className="w-full h-full object-cover" />
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