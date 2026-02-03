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
import { Capacitor } from '@capacitor/core';
import { Filesystem, Directory, Encoding } from '@capacitor/filesystem';
import { Share } from '@capacitor/share';

// --- Types ---
import type { ChatMessage } from '../../../core/types';
import type { TranslationFunction } from '../../../app/hooks/useTranslations';

// --- Services ---
import {
  safeSaveChatHistoryDB,
  saveChatHistoryDB,
  getAllChatMetasDB,
  clearAndSaveAllHistoriesDB,
  getChatHistoryDB,
  iterateChatHistoriesDB,
  hasAnyChatHistoriesDB,
  setChatMetaDB,
} from '../../chat';
import { getGlobalProfileDB, setGlobalProfileDB } from '..';
import { getLoadingGifsDB as getAssetsLoadingGifs, setLoadingGifsDB as setAssetsLoadingGifs, getMaestroProfileImageDB, setMaestroProfileImageDB } from '../../../core/db/assets';
import { DB_VERSION } from '../../../core/db/index';

// --- Config ---
import { ALL_LANGUAGES, DEFAULT_NATIVE_LANG_CODE } from '../../../core/config/languages';

// --- Utils ---
import { uniq } from '../../../shared/utils/common';
import { findLanguageByExactCode, findLanguageByPrimarySubtag } from '../../../shared/utils/languageUtils';
import { useMaestroStore } from '../../../store';

export interface UseDataBackupConfig {
  t: TranslationFunction;
}

export interface UseDataBackupReturn {
  handleSaveAllChats: (options?: { filename?: string; auto?: boolean }) => Promise<void>;
  handleLoadAllChats: (file: File) => Promise<void>;
}

const BACKUP_FORMAT = 'ndjson-v1';
const BACKUP_EXT = 'ndjson';
const BACKUP_MIME = 'application/x-ndjson';
const MAX_CHUNK_CHARS = 1_000_000; // ~1MB per NDJSON line
const MAX_CHUNK_MESSAGES = 200;

type BackupLineWriter = {
  write: (line: string) => Promise<void>;
  close: () => Promise<void>;
};

const normalizeBackupFilename = (rawFilename: string, fallbackBase: string): string => {
  const cleaned = rawFilename.replace(/[\\/:*?"<>|]/g, '-').trim();
  const base = cleaned.length > 0 ? cleaned : fallbackBase;
  const lower = base.toLowerCase();
  if (lower.endsWith('.ndjson') || lower.endsWith('.jsonl')) return base;
  if (lower.endsWith('.json')) return base.slice(0, -5) + `.${BACKUP_EXT}`;
  return `${base}.${BACKUP_EXT}`;
};

const buildHeaderLine = () =>
  JSON.stringify({
    type: 'header',
    format: BACKUP_FORMAT,
    version: DB_VERSION,
    createdAt: new Date().toISOString(),
  }) + '\n';

const buildJsonLine = (obj: any) => JSON.stringify(obj) + '\n';

const buildChatChunkLine = (pairId: string, chunkIndex: number, isLast: boolean, messageJsonParts: string[]) => {
  const pid = JSON.stringify(pairId);
  const lastField = isLast ? ',"isLast":true' : '';
  return `{"type":"chatChunk","pairId":${pid},"chunkIndex":${chunkIndex}${lastField},"messages":[${messageJsonParts.join(',')}]}\n`;
};

const isCancelError = (err: unknown) => {
  const msg = (err instanceof Error ? err.message : String(err)).toLowerCase();
  return msg.includes('canceled') || msg.includes('cancelled') || msg.includes('abort');
};

const createNativeLineWriter = (path: string, directory: Directory) => {
  let hasWritten = false;
  let uri: string | undefined;
  const writer: BackupLineWriter & { getUri: () => string | undefined } = {
    write: async (line: string) => {
      if (!hasWritten) {
        const result = await Filesystem.writeFile({
          path,
          data: line,
          directory,
          encoding: Encoding.UTF8,
        });
        uri = result?.uri;
        hasWritten = true;
        return;
      }
      await Filesystem.appendFile({
        path,
        data: line,
        directory,
        encoding: Encoding.UTF8,
      });
    },
    close: async () => {},
    getUri: () => uri,
  };
  return writer;
};

const createWebLineWriter = async (filename: string): Promise<BackupLineWriter> => {
  const picker = (typeof window !== 'undefined' ? (window as any).showSaveFilePicker : undefined) as undefined | ((options?: any) => Promise<any>);
  if (typeof picker !== 'function') {
    throw new Error('BROWSER_NOT_SUPPORTED');
  }
  const handle = await picker({
    suggestedName: filename,
    types: [{ description: 'Maestro Backup', accept: { [BACKUP_MIME]: ['.ndjson', '.jsonl'] } }],
    excludeAcceptAllOption: false,
  });
  const writable = await handle.createWritable();
  return {
    write: async (line: string) => {
      await writable.write(line);
    },
    close: async () => {
      await writable.close();
    },
  };
};

const readFileAsText = (file: Blob): Promise<string> =>
  new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = (event) => resolve(String(event.target?.result || ''));
    reader.onerror = (e) => reject(e);
    reader.onabort = (e) => reject(e);
    reader.readAsText(file);
  });

const streamNdjsonLines = async (file: File, onLine: (line: string) => Promise<void> | void) => {
  if (typeof (file as any).stream !== 'function') {
    const text = await readFileAsText(file);
    const lines = text.split(/\r?\n/);
    for (const line of lines) {
      const cleaned = line.trim();
      if (!cleaned) continue;
      await onLine(cleaned);
    }
    return;
  }
  const reader = (file as any).stream().getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  while (true) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });
    let idx = buffer.indexOf('\n');
    while (idx >= 0) {
      const line = buffer.slice(0, idx);
      buffer = buffer.slice(idx + 1);
      const cleaned = line.trim();
      if (cleaned) {
        await onLine(cleaned);
      }
      idx = buffer.indexOf('\n');
    }
  }
  buffer += decoder.decode();
  const trailing = buffer.trim();
  if (trailing) await onLine(trailing);
};

export const useDataBackup = ({ t }: UseDataBackupConfig): UseDataBackupReturn => {
  const setMessages = useMaestroStore(state => state.setMessages);
  const setLoadingGifs = useMaestroStore(state => state.setLoadingGifs);
  const setTempNativeLangCode = useMaestroStore(state => state.setTempNativeLangCode);
  const setTempTargetLangCode = useMaestroStore(state => state.setTempTargetLangCode);
  const setIsLanguageSelectionOpen = useMaestroStore(state => state.setIsLanguageSelectionOpen);

  const handleSaveAllChats = useCallback(async (options?: { filename?: string; auto?: boolean }) => {
    const isAuto = options?.auto === true;
    try {
      const selectedPairId = useMaestroStore.getState().settings.selectedLanguagePairId;
      if (selectedPairId) {
        try {
          await safeSaveChatHistoryDB(selectedPairId, useMaestroStore.getState().messages);
        } catch { /* ignore */ }
      }
      const hasAnyChats = await hasAnyChatHistoriesDB();
      if (!hasAnyChats) {
        if (!isAuto) {
          alert(t('startPage.noChatsToSave'));
        }
        return;
      }

      const allMetas = await getAllChatMetasDB();
      const gp = await getGlobalProfileDB();
      let assetsLoadingGifs: string[] = [];
      try { assetsLoadingGifs = (await getAssetsLoadingGifs()) || []; } catch {}
      let maestroProfile: any = null;
      try { maestroProfile = await getMaestroProfileImageDB(); } catch {}

      const timestamp = new Date().toISOString().slice(0, 19).replace('T', '_').replace(/:/g, '-');
      const prefix = isAuto ? 'maestro-backup-' : 'maestro-all-chats-';
      const rawFilename = options?.filename && options.filename.trim().length > 0
        ? options.filename.trim()
        : `${prefix}${timestamp}.${BACKUP_EXT}`;
      const safeFilename = normalizeBackupFilename(rawFilename, `${prefix}${timestamp}`);

      const writeBackupLines = async (writer: BackupLineWriter) => {
        await writer.write(buildHeaderLine());
        await writer.write(buildJsonLine({ type: 'globalProfile', text: gp?.text || null }));
        await writer.write(buildJsonLine({ type: 'assets', loadingGifs: assetsLoadingGifs, maestroProfile }));

        await iterateChatHistoriesDB(async (pairId, messages) => {
          if (!pairId) return;
          if (Array.isArray(messages) && messages.length > 0) {
            let chunkIndex = 0;
            let parts: string[] = [];
            let size = 0;
            const flush = async (isLast: boolean) => {
              if (!parts.length) return;
              await writer.write(buildChatChunkLine(pairId, chunkIndex, isLast, parts));
              chunkIndex += 1;
              parts = [];
              size = 0;
            };

            for (let i = 0; i < messages.length; i++) {
              const msgJson = JSON.stringify(messages[i]);
              const extra = parts.length ? 1 + msgJson.length : msgJson.length;
              if (parts.length && (size + extra > MAX_CHUNK_CHARS || parts.length >= MAX_CHUNK_MESSAGES)) {
                await flush(false);
              }
              parts.push(msgJson);
              size += extra;
              if (i === messages.length - 1) {
                await flush(true);
              }
            }
          }

          const meta = (allMetas && (allMetas as any)[pairId]) || null;
          if (meta) {
            await writer.write(buildJsonLine({ type: 'meta', pairId, meta }));
          }
        });

        await writer.close();
      };

      if (Capacitor.isNativePlatform()) {
        const attemptWrite = async (directory: Directory) => {
          const writer = createNativeLineWriter(safeFilename, directory);
          await writeBackupLines(writer);
          const uri = writer.getUri();
          if (!uri) throw new Error('Missing file URI for backup');
          return uri;
        };

        try {
          let uri: string | undefined;
          try {
            uri = await attemptWrite(Directory.Documents);
          } catch {
            uri = await attemptWrite(Directory.Cache);
          }

          await Share.share({
            title: t('startPage.saveChats'),
            url: uri,
            dialogTitle: t('startPage.saveChats'),
          });
        } catch (err) {
          if (isCancelError(err)) {
            return;
          }
          const errorMsg = err instanceof Error ? err.message : String(err);
          console.error('Failed to save/share backup:', err);
          if (!isAuto) {
            alert(`${t('startPage.saveError')}\n${errorMsg}`);
          }
        }
        return;
      }

      try {
        const writer = await createWebLineWriter(safeFilename);
        await writeBackupLines(writer);
      } catch (err) {
        if (isCancelError(err)) {
          return;
        }
        const errMsg = err instanceof Error ? err.message : String(err);
        if (errMsg === 'BROWSER_NOT_SUPPORTED') {
          if (!isAuto) {
            alert(t('startPage.browserNotSupported') || 'Your browser does not support file saving. Please use Chrome or Edge.');
          }
          return;
        }
        throw err;
      }
    } catch (error) {
      if (isCancelError(error)) {
        return;
      }
      const errorMsg = error instanceof Error ? error.message : String(error);
      if (errorMsg === 'BROWSER_NOT_SUPPORTED') {
        if (!isAuto) {
          alert(t('startPage.browserNotSupported') || 'Your browser does not support file saving. Please use Chrome or Edge.');
        }
        return;
      }
      console.error("Failed to save all chats:", error);
      if (!isAuto) {
        alert(`${t('startPage.saveError')}\n${errorMsg}`);
      }
    }
  }, [t]);

  const applyImportedAssets = useCallback(async (importedLoadingGifs: string[] | null, importedMaestroProfile: any | null) => {
    try {
      const current = (await getAssetsLoadingGifs()) || [];
      let manifest: string[] = [];
      try {
        const resp = await fetch('/gifs/manifest.json', { cache: 'force-cache' });
        if (resp.ok) manifest = await resp.json();
      } catch {}
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
  }, [setLoadingGifs]);

  const finalizeLoad = useCallback(async (loadedCount: number) => {
    alert(t('startPage.loadSuccess', { count: loadedCount }));

    const currentPairId = useMaestroStore.getState().settings.selectedLanguagePairId;
    if (currentPairId) {
      const newHistoryForCurrentPair = await getChatHistoryDB(currentPairId);
      setMessages(newHistoryForCurrentPair);
    } else {
      const browserLangCode = (typeof navigator !== 'undefined' && navigator.language) || DEFAULT_NATIVE_LANG_CODE;
      const defaultNative = findLanguageByExactCode(browserLangCode)
        || findLanguageByPrimarySubtag(browserLangCode)
        || ALL_LANGUAGES.find(l => l.langCode === DEFAULT_NATIVE_LANG_CODE)!;
      setTempNativeLangCode(defaultNative.langCode);
      setTempTargetLangCode(null);
      setIsLanguageSelectionOpen(true);
    }
  }, [t, setMessages, setTempNativeLangCode, setTempTargetLangCode, setIsLanguageSelectionOpen]);

  const loadNdjsonBackup = useCallback(async (file: File) => {
    // --- Validation pass: ensure file has valid header before clearing DB ---
    let hasValidHeader = false;
    let validLineCount = 0;
    await streamNdjsonLines(file, async (line) => {
      let parsed: any;
      try {
        parsed = JSON.parse(line);
      } catch {
        return; // Skip malformed lines in validation
      }
      if (parsed && typeof parsed === 'object') {
        validLineCount += 1;
        if (parsed.type === 'header' && parsed.format === BACKUP_FORMAT) {
          hasValidHeader = true;
        }
      }
    });
    if (!hasValidHeader || validLineCount < 2) {
      throw new Error('INVALID_BACKUP_FORMAT');
    }

    // --- Actual import pass ---
    let globalProfileText: string | null = null;
    let importedLoadingGifs: string[] | null = null;
    let importedMaestroProfile: any | null = null;
    const metas: Record<string, any> = {};
    let currentPairId: string | null = null;
    let currentMessages: ChatMessage[] = [];
    let loadedCount = 0;

    await clearAndSaveAllHistoriesDB({}, null, null, null);

    const flushPair = async (pairId: string) => {
      if (!pairId) return;
      await saveChatHistoryDB(pairId, currentMessages);
      loadedCount += 1;
      currentPairId = null;
      currentMessages = [];
    };

    await streamNdjsonLines(file, async (line) => {
      let parsed: any;
      try {
        parsed = JSON.parse(line);
      } catch (parseErr) {
        console.warn('Skipping malformed line in backup:', parseErr);
        return;
      }
      if (!parsed || typeof parsed !== 'object') return;
      const type = parsed.type;

      if (type === 'header') {
        return;
      }

      if (type === 'globalProfile') {
        globalProfileText = typeof parsed.text === 'string' ? parsed.text : null;
        return;
      }

      if (type === 'assets') {
        if (Array.isArray(parsed.loadingGifs)) {
          importedLoadingGifs = parsed.loadingGifs as string[];
        }
        if (parsed.maestroProfile && typeof parsed.maestroProfile === 'object') {
          const mp = parsed.maestroProfile as any;
          if (mp && (typeof mp.dataUrl === 'string' || typeof mp.uri === 'string')) {
            importedMaestroProfile = {
              dataUrl: typeof mp.dataUrl === 'string' ? mp.dataUrl : undefined,
              mimeType: typeof mp.mimeType === 'string' ? mp.mimeType : undefined,
              uri: typeof mp.uri === 'string' ? mp.uri : undefined,
              updatedAt: typeof mp.updatedAt === 'number' ? mp.updatedAt : Date.now(),
            };
          }
        }
        return;
      }

      if (type === 'meta') {
        const pid = typeof parsed.pairId === 'string' ? parsed.pairId : '';
        if (pid) {
          metas[pid] = parsed.meta || null;
        }
        return;
      }

      if (type === 'chatChunk') {
        const pid = typeof parsed.pairId === 'string' ? parsed.pairId : '';
        if (!pid) return;
        if (currentPairId && currentPairId !== pid) {
          await flushPair(currentPairId);
        }
        currentPairId = pid;
        if (Array.isArray(parsed.messages) && parsed.messages.length) {
          currentMessages.push(...(parsed.messages as ChatMessage[]));
        }
        if (parsed.isLast === true) {
          await flushPair(pid);
        }
        return;
      }
    });

    if (currentPairId) {
      await flushPair(currentPairId);
    }

    const metaEntries = Object.entries(metas);
    for (const [pairId, meta] of metaEntries) {
      if (meta) {
        try { await setChatMetaDB(pairId, meta); } catch { /* ignore */ }
      }
    }

    const trimmed = (globalProfileText || '').trim();
    if (trimmed) {
      try { await setGlobalProfileDB(trimmed); } catch { /* ignore */ }
    }

    await applyImportedAssets(importedLoadingGifs, importedMaestroProfile);
    await finalizeLoad(loadedCount);
  }, [applyImportedAssets, finalizeLoad]);

  const handleLoadAllChats = useCallback(async (file: File) => {
    await handleSaveAllChats({ auto: true });
    try {
      const name = (file.name || '').toLowerCase();
      if (!name.endsWith('.ndjson') && !name.endsWith('.jsonl')) {
        throw new Error('UNSUPPORTED_FORMAT');
      }
      await loadNdjsonBackup(file);
    } catch (e) {
      const errMsg = e instanceof Error ? e.message : String(e);
      console.error("Failed to load chats:", e);
      if (errMsg === 'INVALID_BACKUP_FORMAT' || errMsg === 'UNSUPPORTED_FORMAT') {
        alert(t('startPage.invalidBackupFormat') || 'Invalid backup file. Please select a valid Maestro backup (.ndjson) file.');
      } else {
        alert(t('startPage.loadError'));
      }
    }
  }, [handleSaveAllChats, t, loadNdjsonBackup]);

  return {
    handleSaveAllChats,
    handleLoadAllChats,
  };
};
