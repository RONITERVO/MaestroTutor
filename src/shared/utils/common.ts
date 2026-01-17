import { ChatMessage } from '../../core/types';

export const uniq = <T,>(arr: T[]) => Array.from(new Set(arr));

export const isRealChatMessage = (m: ChatMessage) => (m.role === 'user' || m.role === 'assistant') && !m.thinking;

export const fetchDefaultAvatarBlob = async (): Promise<Blob | null> => {
  try {
    const man = await fetch('/maestro-avatars/manifest.json', { cache: 'force-cache' });
    if (man.ok) {
      const entries: string[] = await man.json();
      if (Array.isArray(entries)) {
        for (const name of entries) {
          const url = `/maestro-avatars/${name}`;
          try {
            const r = await fetch(url, { cache: 'force-cache' });
            if (r.ok) return await r.blob();
          } catch { /* try next */ }
        }
      }
    }
  } catch { /* ignore */ }
  return null;
};
