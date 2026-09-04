import { loadApiKey } from '../../core/security/apiKeyStorage';
import { loadManagedAccessSession } from '../../core/security/managedAccessSessionStorage';

/** Persist only a digest, never credentials. Token refresh keeps the same owner. */
export const getAvatarAccessScope = async (): Promise<string> => {
  const [key, session] = await Promise.all([loadApiKey(), loadManagedAccessSession()]);
  const identity = JSON.stringify([key || '', session?.user?.id || '']);
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(identity));
  return Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, '0')).join('');
};
