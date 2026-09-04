import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  scope: 'account-a:key-a',
  asset: { dataUrl: 'data:image/png;base64,YQ==', mimeType: 'image/png', uri: 'old-owner' } as any,
  upload: vi.fn(),
  save: vi.fn(),
}));
vi.mock('./avatarAccessScope', () => ({ getAvatarAccessScope: async () => mocks.scope }));
vi.mock('../../core/db/assets', () => ({
  getMaestroProfileImageDB: async () => mocks.asset,
  setMaestroProfileImageDB: async (asset: any) => { mocks.asset = asset; mocks.save(asset); },
}));
vi.mock('./files', () => ({
  checkFileStatuses: async (uris: string[]) => Object.fromEntries(uris.map(uri => [uri, { active: true }])),
  uploadMediaToFiles: (...args: any[]) => mocks.upload(...args),
}));
vi.mock('../../features/vision', () => ({
  createAvatarWithOverlay: async () => ({ dataUrl: 'overlay', mimeType: 'image/png' }),
}));
import { ensureMaestroAvatarUris, invalidateMaestroAvatarCache } from './maestroAvatarEnsure';

describe('avatar access ownership', () => {
  beforeEach(() => {
    invalidateMaestroAvatarCache();
    mocks.scope = 'account-a:key-a';
    mocks.asset = { dataUrl: 'data:image/png;base64,YQ==', mimeType: 'image/png', uri: 'old-owner' };
    mocks.save.mockClear();
    mocks.upload.mockReset().mockImplementation(async (_data, _mime, name) => ({ uri: `${mocks.scope}/${name}`, mimeType: 'image/png' }));
  });

  it('refreshes raw and derived files for account and key changes even if old files report active', async () => {
    const first = await ensureMaestroAvatarUris();
    expect(first.rawUri).toBe('account-a:key-a/maestro-avatar');
    await ensureMaestroAvatarUris();
    expect(mocks.upload).toHaveBeenCalledTimes(2);
    for (const scope of ['account-b:key-a', 'account-b:key-b', 'account-b:managed']) {
      mocks.scope = scope;
      const result = await ensureMaestroAvatarUris();
      expect(result.rawUri).toBe(`${scope}/maestro-avatar`);
      expect(result.overlayUri).toBe(`${scope}/maestro-avatar-overlay`);
      expect(mocks.asset.accessScope).toBe(scope);
    }
    expect(mocks.upload).toHaveBeenCalledTimes(8);
  });

  it('does not persist an upload that completes after access changes', async () => {
    mocks.upload.mockImplementationOnce(async () => {
      mocks.scope = 'account-b:key-b';
      return { uri: 'stale-upload' };
    });
    await expect(ensureMaestroAvatarUris()).rejects.toThrow('Avatar access changed');
    expect(mocks.save).not.toHaveBeenCalled();
    const result = await ensureMaestroAvatarUris();
    expect(result.rawUri).toBe('account-b:key-b/maestro-avatar');
  });

  it('shares uploads between simultaneous app hydration and chat preparation', async () => {
    const results = await Promise.all([ensureMaestroAvatarUris(), ensureMaestroAvatarUris()]);
    expect(results[0]).toEqual(results[1]);
    expect(mocks.upload).toHaveBeenCalledTimes(2);
  });
});
