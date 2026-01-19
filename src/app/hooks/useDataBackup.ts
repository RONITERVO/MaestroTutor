// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * useDataBackup - Handles saving and loading all chat data as backup files.
 * 
 * This hook extracts the backup/restore orchestration logic from App.tsx,
 * coordinating between multiple services (chats, metas, global profile, assets).
 */
import { useCallback } from 'react';
import type { MutableRefObject } from 'react';

// --- Types ---
import type { ChatMessage, AppSettings } from '../../core/types';
import type { TranslationFunction } from './useTranslations';

// --- Services ---
import { safeSaveChatHistoryDB, getAllChatHistoriesDB, getAllChatMetasDB, clearAndSaveAllHistoriesDB, getChatHistoryDB } from '../../features/chat/services/chatHistory';
import { getGlobalProfileDB } from '../../features/session/services/globalProfile';
import { getLoadingGifsDB as getAssetsLoadingGifs, setLoadingGifsDB as setAssetsLoadingGifs, getMaestroProfileImageDB, setMaestroProfileImageDB } from '../../core/db/assets';

// --- Config ---
import { ALL_LANGUAGES, DEFAULT_NATIVE_LANG_CODE } from '../../core/config/languages';

// --- Utils ---
import { uniq } from '../../shared/utils/common';

export interface UseDataBackupConfig {
  t: TranslationFunction;
  settingsRef: MutableRefObject<AppSettings>;
  messagesRef: MutableRefObject<ChatMessage[]>;
  setMessages: (messages: ChatMessage[]) => void;
  setLoadingGifs: (gifs: string[]) => void;
  setTempNativeLangCode: (code: string | null) => void;
  setTempTargetLangCode: (code: string | null) => void;
  setIsLanguageSelectionOpen: (open: boolean) => void;
}

export interface UseDataBackupReturn {
  handleSaveAllChats: (options?: { filename?: string; auto?: boolean }) => Promise<void>;
  handleLoadAllChats: (file: File) => Promise<void>;
}

export const useDataBackup = ({
  t,
  settingsRef,
  messagesRef,
  setMessages,
  setLoadingGifs,
  setTempNativeLangCode,
  setTempTargetLangCode,
  setIsLanguageSelectionOpen,
}: UseDataBackupConfig): UseDataBackupReturn => {

  const handleSaveAllChats = useCallback(async (options?: { filename?: string; auto?: boolean }) => {
    const isAuto = options?.auto === true;
    try {
      const selectedPairId = settingsRef.current.selectedLanguagePairId;
      if (selectedPairId) {
        try {
          await safeSaveChatHistoryDB(selectedPairId, messagesRef.current);
        } catch { /* ignore */ }
      }
      const allChats = await getAllChatHistoriesDB();
      const allMetas = await getAllChatMetasDB();
      const gp = await getGlobalProfileDB();
      let assetsLoadingGifs: string[] = [];
      try { assetsLoadingGifs = (await getAssetsLoadingGifs()) || []; } catch {}

      if (Object.keys(allChats).length === 0) {
        if (!isAuto) {
          alert(t('startPage.noChatsToSave'));
        }
        return;
      }
      let maestroProfile: any = null;
      try { maestroProfile = await getMaestroProfileImageDB(); } catch {}
      const backup = { version: 7, chats: allChats, metas: allMetas, globalProfile: gp?.text || null, assets: { loadingGifs: assetsLoadingGifs, maestroProfile } };
      const blob = new Blob([JSON.stringify(backup, null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;

      const timestamp = new Date().toISOString().slice(0, 19).replace('T', '_').replace(/:/g, '-');
      const prefix = isAuto ? 'maestro-backup-' : 'maestro-all-chats-';
      a.download = options?.filename && options.filename.trim().length > 0
        ? options.filename.trim()
        : `${prefix}${timestamp}.json`;

      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch (error) {
      console.error("Failed to save all chats:", error);
      if (!isAuto) {
        alert(t('startPage.saveError'));
      }
    }
  }, [t, settingsRef, messagesRef]);

  const handleLoadAllChats = useCallback(async (file: File) => {
    await handleSaveAllChats({ auto: true });

    const reader = new FileReader();
    reader.onload = async (event) => {
      try {
        const content = event.target?.result as string;
        const parsed = JSON.parse(content);
        if (typeof parsed !== 'object' || parsed === null) throw new Error("Invalid format");
        let chats: Record<string, ChatMessage[]> = {};
        let metas: Record<string, any> | null = null;
        let globalProfileText: string | null = null;
        let importedLoadingGifs: string[] | null = null;
        let importedMaestroProfile: any | null = null;
        
        if ('chats' in parsed) {
          chats = parsed.chats || {};
          metas = parsed.metas || null;
          globalProfileText = typeof parsed.globalProfile === 'string' ? parsed.globalProfile : null;
          if (parsed.assets && Array.isArray(parsed.assets.loadingGifs)) {
            importedLoadingGifs = parsed.assets.loadingGifs as string[];
          }
          if (parsed.assets && parsed.assets.maestroProfile && typeof parsed.assets.maestroProfile === 'object') {
            const mp = parsed.assets.maestroProfile as any;
            if (mp && (typeof mp.dataUrl === 'string' || typeof mp.uri === 'string')) {
              importedMaestroProfile = {
                dataUrl: typeof mp.dataUrl === 'string' ? mp.dataUrl : undefined,
                mimeType: typeof mp.mimeType === 'string' ? mp.mimeType : undefined,
                uri: typeof mp.uri === 'string' ? mp.uri : undefined,
                updatedAt: typeof mp.updatedAt === 'number' ? mp.updatedAt : Date.now(),
              };
            }
          }
        } else {
          chats = parsed as Record<string, ChatMessage[]>;
        }
        await clearAndSaveAllHistoriesDB(chats, metas, null, globalProfileText);
        try {
          const current = (await getAssetsLoadingGifs()) || [];
          let manifest: string[] = [];
          try { const resp = await fetch('/gifs/manifest.json', { cache: 'force-cache' }); if (resp.ok) manifest = await resp.json(); } catch {}
          const merged = uniq([...current, ...(importedLoadingGifs || []), ...manifest]);
          await setAssetsLoadingGifs(merged);
          setLoadingGifs(merged);
        } catch {}
        if (importedMaestroProfile) {
          try {
            let profileToPersist: any = { ...importedMaestroProfile };
            profileToPersist.uri = undefined;
            await setMaestroProfileImageDB(profileToPersist);
            try {
              window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: profileToPersist }));
            } catch { /* ignore */ }
          } catch { /* ignore */ }
        }
        const loadedCount = Object.keys(chats).length;
        alert(t('startPage.loadSuccess', { count: loadedCount }));

        const currentPairId = settingsRef.current.selectedLanguagePairId;
        if (currentPairId) {
          const newHistoryForCurrentPair = await getChatHistoryDB(currentPairId);
          setMessages(newHistoryForCurrentPair);
        } else {
          const browserLangCode = (typeof navigator !== 'undefined' && navigator.language || 'en').substring(0, 2);
          const defaultNative = ALL_LANGUAGES.find(l => l.langCode === browserLangCode) || ALL_LANGUAGES.find(l => l.langCode === DEFAULT_NATIVE_LANG_CODE)!;
          setTempNativeLangCode(defaultNative.langCode);
          setTempTargetLangCode(null);
          setIsLanguageSelectionOpen(true);
        }
      } catch (e) {
        console.error("Failed to load chats:", e);
        alert(t('startPage.loadError'));
      }
    };
    reader.readAsText(file);
  }, [handleSaveAllChats, t, settingsRef, setMessages, setLoadingGifs, setTempNativeLangCode, setTempTargetLangCode, setIsLanguageSelectionOpen]);

  return {
    handleSaveAllChats,
    handleLoadAllChats,
  };
};
