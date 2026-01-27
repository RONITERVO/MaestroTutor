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
  IconXMark,
} from '../../../shared/ui/Icons';
import { getGlobalProfileDB, setGlobalProfileDB } from '../services/globalProfile';
import { getMaestroProfileImageDB, setMaestroProfileImageDB, clearMaestroProfileImageDB, MaestroProfileAsset } from '../../../core/db/assets';
import { uploadMediaToFiles, deleteFileByNameOrUri } from '../../../api/gemini/files';
import { DB_NAME } from '../../../core/db/index';
import { useMaestroStore } from '../../../store';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../core/config/activityTokens';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { useDataBackup } from '../hooks/useDataBackup';

const SessionControls: React.FC = () => {
  const { t } = useAppTranslations();
  const { handleSaveAllChats, handleLoadAllChats } = useDataBackup({ t });


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
      } catch {}
    })();
    return () => { mounted = false; };
  }, []);

  useEffect(() => {
    const handler = (e: any) => {
      try {
        const d = e?.detail || {};
        if (d && (typeof d.dataUrl === 'string' || typeof d.uri === 'string')) {
          // Use functional update to avoid stale closure issues
          setMaestroAsset(prev => ({
            dataUrl: typeof d.dataUrl === 'string' ? d.dataUrl : prev?.dataUrl,
            mimeType: typeof d.mimeType === 'string' ? d.mimeType : prev?.mimeType,
            uri: typeof d.uri === 'string' ? d.uri : prev?.uri,
            updatedAt: Date.now(),
          }));
        } else {
          getMaestroProfileImageDB().then(a => setMaestroAsset(a)).catch(() => {});
        }
      } catch {}
    };
    window.addEventListener('maestro-avatar-updated', handler as any);
    return () => window.removeEventListener('maestro-avatar-updated', handler as any);
  }, []); // Empty deps - handler uses functional update to avoid stale closure

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
      try { window.dispatchEvent(new CustomEvent('globalProfileUpdated')); } catch {}
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
    } catch {}
  }, []);

  const handleMaestroAvatarClick = () => {
    try {
      if (!maestroAvatarOpenTokenRef.current) {
        maestroAvatarOpenTokenRef.current = createUiToken(TOKEN_SUBTYPE.MAESTRO_AVATAR);
      }
    } catch {}
    maestroFileInputRef.current?.click();
  };

  const handleClearMaestroAvatar = async (e?: React.MouseEvent) => {
    e?.preventDefault?.();
    e?.stopPropagation?.();
    try {
      setIsUploadingMaestro(true);
      if (!maestroUploadTokenRef.current) {
        maestroUploadTokenRef.current = createUiToken(TOKEN_SUBTYPE.MAESTRO_AVATAR);
      }
    } catch {}
    try {
      const prevUri = maestroAsset?.uri;
      if (prevUri) {
        await deleteFileByNameOrUri(prevUri);
      }
    } catch {}
    try { await clearMaestroProfileImageDB(); } catch {}

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
                } catch {}
                const asset: MaestroProfileAsset = { dataUrl, mimeType: uploadedMimeType, uri: uploadedUri, updatedAt: Date.now() };
                try { await setMaestroProfileImageDB(asset); } catch {}
                setMaestroAsset(asset);
                try { window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: asset })); } catch {}
                defaultFound = true;
                break;
              }
            } catch {}
          }
        }
      }
      if (!defaultFound) {
        setMaestroAsset(null);
        try { window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: {} })); } catch {}
      }
    } catch {
      setMaestroAsset(null);
    } finally {
      try { setIsUploadingMaestro(false); } catch {}
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
      } catch {}
      const asset: MaestroProfileAsset = { dataUrl, mimeType: uploadedMimeType, uri: uploadedUri, updatedAt: Date.now() };
      await setMaestroProfileImageDB(asset);
      setMaestroAsset(asset);
      try {
        window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: { uri: uploadedUri, mimeType: uploadedMimeType, dataUrl } }));
      } catch {}
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
      // Backup first
      const safe = `backup-before-reset-${new Date().toISOString().slice(0,10)}`;
      if (handleSaveAllChats) await handleSaveAllChats({ filename: `${safe}.json`, auto: true });

      await new Promise(r => setTimeout(r, 500));
      await wipeLocalMemoryAndDb();
      window.location.reload();
    } catch {
      setResetConfirm('');
    }
  };

  return (
    <div className="w-full py-3 px-4 min-h-[50px] flex items-center justify-between gap-3">
      {resetMode ? (
        <>
          <div className="flex items-center gap-2 flex-1">
            <span className="text-xs font-semibold text-white uppercase whitespace-nowrap">Reset:</span>
            <input
              className="flex-1 min-w-0 bg-white/20 border border-white/30 rounded px-2 py-1 text-sm text-white placeholder-white/50 focus:outline-none focus:border-white"
              placeholder="Type DELETE"
              value={resetConfirm}
              onChange={(e) => setResetConfirm(e.target.value)}
            />
          </div>
          <div className="flex items-center gap-2">
            <button type="button" onClick={handleResetConfirm} disabled={resetConfirm !== 'DELETE'} className="p-1.5 bg-red-500 rounded-full text-white disabled:opacity-50 hover:bg-red-600">
              <IconCheck className="w-4 h-4" />
            </button>
            <button type="button" onClick={() => { setResetMode(false); setResetConfirm(''); }} className="p-1.5 bg-white/20 rounded-full text-white hover:bg-white/30">
              <IconUndo className="w-4 h-4" />
            </button>
          </div>
        </>
      ) : isEditingProfile ? (
        <>
          <div className="flex items-center gap-2 flex-1">
            <span className="text-xs font-semibold text-white uppercase whitespace-nowrap">Profile:</span>
            <input
              className="flex-1 min-w-0 bg-white/20 border border-white/30 rounded px-2 py-1 text-sm text-white placeholder-white/50 focus:outline-none focus:border-white"
              placeholder="User profile details..."
              value={profileText}
              onChange={(e) => setProfileText(e.target.value)}
            />
          </div>
          <div className="flex items-center gap-2">
            <button type="button" onClick={handleProfileSave} className="p-1.5 bg-green-500 rounded-full text-white hover:bg-green-600">
              <IconCheck className="w-4 h-4" />
            </button>
            <button type="button" onClick={() => setIsEditingProfile(false)} className="p-1.5 bg-white/20 rounded-full text-white hover:bg-white/30">
              <IconUndo className="w-4 h-4" />
            </button>
          </div>
        </>
      ) : (
        <>
          <div className="flex items-center gap-3">
            <button type="button" onClick={startProfileEdit} className="p-2 hover:bg-white/20 rounded-full text-white transition-colors" title="Edit Profile">
              <IconPencil className="w-4 h-4" />
            </button>
          </div>

          <div className="flex items-center bg-blue-500/30 rounded-full p-0.5 border border-white/10">
            <button type="button" onClick={handleSave} className="p-2 hover:bg-white/20 rounded-full text-white transition-colors" title={t('startPage.saveChats')}>
              <IconSave className="w-4 h-4" />
            </button>
            <div className="w-px h-4 bg-white/20 mx-0.5"></div>
            <button type="button" onClick={() => loadFileInputRef.current?.click()} className="p-2 hover:bg-white/20 rounded-full text-white transition-colors" title={t('startPage.loadChats')}>
              <IconFolderOpen className="w-4 h-4" />
            </button>
            <div className="w-px h-4 bg-white/20 mx-0.5"></div>
            <button type="button" onClick={() => setResetMode(true)} className="p-2 hover:bg-red-500/50 rounded-full text-white transition-colors" title="Backup & Reset">
              <IconTrash className="w-4 h-4" />
            </button>
          </div>
          <input type="file" ref={loadFileInputRef} onChange={handleLoadFileChange} accept=".json" className="hidden" />

          <div className="relative inline-block">
            <button
              type="button"
              onClick={!isUploadingMaestro ? handleMaestroAvatarClick : undefined}
              disabled={isUploadingMaestro}
              className={`relative w-8 h-8 rounded-full overflow-hidden border-2 ${maestroAsset?.dataUrl ? 'border-white/50' : 'border-white/30 border-dashed'} bg-white/10 flex items-center justify-center hover:bg-white/20 transition cursor-pointer disabled:cursor-wait`}
              aria-label={maestroAsset?.dataUrl ? t('startPage.maestroAvatar') : t('startPage.addMaestroAvatar')}
            >
              {maestroAsset?.dataUrl ? (
                <img src={maestroAsset.dataUrl} alt="Maestro" className="w-full h-full object-cover" />
              ) : (
                <IconPlus className="w-4 h-4 text-white/70" />
              )}
              {isUploadingMaestro && (
                <div className="absolute inset-0 bg-black/40 flex items-center justify-center">
                  <SmallSpinner className="w-4 h-4 text-white" />
                </div>
              )}
            </button>
            <input type="file" ref={maestroFileInputRef} onChange={handleMaestroFileChange} accept="image/*" className="hidden" />
            {maestroAsset?.dataUrl && !isUploadingMaestro && (
              <button
                type="button"
                onClick={handleClearMaestroAvatar}
                className="absolute -top-1 -right-1 bg-red-500 text-white rounded-full p-0.5 shadow-sm hover:bg-red-600"
                title={t('general.clear')}
              >
                <IconXMark className="w-3 h-3" />
              </button>
            )}
          </div>
        </>
      )}
    </div>
  );
};

export default SessionControls;
