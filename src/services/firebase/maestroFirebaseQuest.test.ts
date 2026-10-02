// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { beforeEach, expect, it, vi } from 'vitest';
const mocks = vi.hoisted(() => ({ native: true, questAppId: '1:123:web:abcdef', existingApps: [] as { name: string; options: { appId: string; projectId: string } }[], createApp: vi.fn(() => ({ name: 'maestro-quest' })), url: 'https://server.example/questAttestation', suspended: false,
  initialize: vi.fn(() => ({ app: 'quest' })), getToken: vi.fn(async () => ({ token: 'firebase.quest.proof' })),
  acquire: vi.fn(async () => ({ token: 'firebase.quest.proof', expireTimeMillis: 99 })), capacitor: vi.fn(), popup: vi.fn() }));
vi.mock('@capacitor/core', () => ({ Capacitor: { isNativePlatform: () => false, getPlatform: () => 'web' } }));
vi.mock('../../platform/quest/questIntegrityBridge', () => ({ isNativeQuestBook: () => mocks.native }));
vi.mock('../../platform/browser/sessionActivity', () => ({ sessionActivity: { status: () => ({ suspended: mocks.suspended }) } }));
vi.mock('../../core/config/integrations', () => ({ isFirebaseClientConfigured: () => true, MAESTRO_INTEGRATION_CONFIG: {
  firebaseApiKey: 'key', get questFirebaseAppId() { return mocks.questAppId; }, firebaseAppId: '1:123:web:fedcba', firebaseAuthDomain: 'example.test', firebaseProjectId: 'project',
  get questAttestationUrl() { return mocks.url; }, firebaseAppCheckSiteKey: 'recaptcha-site', firebaseAppCheckDebugToken: 'must-not-enable-on-quest',
} }));
vi.mock('firebase/app', () => ({ getApps: () => mocks.existingApps, initializeApp: mocks.createApp }));
vi.mock('firebase/app-check', () => ({ initializeAppCheck: mocks.initialize, getToken: mocks.getToken,
  CustomProvider: class { constructor(public options: unknown) {} }, ReCaptchaEnterpriseProvider: class {} }));
vi.mock('firebase/auth', () => ({ signInWithPopup: mocks.popup }));
vi.mock('@capacitor-firebase/app-check', () => ({ FirebaseAppCheck: { initialize: mocks.capacitor, getToken: mocks.capacitor } }));
vi.mock('./questAppCheck', () => ({ acquireQuestAppCheckToken: mocks.acquire, questAttestationBaseUrl: (value: string) => {
  if (!value) throw new Error('Managed Quest verification is not configured in this build.'); return value;
} }));
beforeEach(() => { vi.resetModules(); vi.clearAllMocks(); mocks.native = true; mocks.questAppId = '1:123:web:abcdef'; mocks.existingApps = []; mocks.suspended = false; mocks.url = 'https://server.example/questAttestation'; });

it('initializes only the Quest CustomProvider, with no debug token or reCAPTCHA fallback', async () => {
  const { maestroFirebaseService: service } = await import('./maestroFirebaseService');
  expect(await service.getAppCheckToken()).toBe('firebase.quest.proof');
  expect(await service.getAppCheckToken(true)).toBe('firebase.quest.proof');
  expect(mocks.initialize).toHaveBeenCalledTimes(1); expect(mocks.capacitor).not.toHaveBeenCalled();
  const options = mocks.initialize.mock.calls[0] as unknown as [unknown, { provider: { options: { getToken: () => Promise<unknown> } }; isTokenAutoRefreshEnabled: boolean }];
  expect(Object.keys(options[1]).sort()).toEqual(['isTokenAutoRefreshEnabled', 'provider']);
  await options[1].provider.options.getToken(); expect(mocks.acquire).toHaveBeenCalledWith(mocks.url);
});
it('disabled Quest configuration stays unavailable even with configured web reCAPTCHA/debug settings', async () => {
  mocks.url = ''; const log = vi.spyOn(console, 'error').mockImplementation(() => {});
  const { maestroFirebaseService: service } = await import('./maestroFirebaseService');
  expect(await service.getAppCheckToken()).toBeNull(); expect(mocks.capacitor).not.toHaveBeenCalled(); expect(mocks.initialize).not.toHaveBeenCalled();
  expect(service.getAppCheckFailureReason()).toContain('not configured'); log.mockRestore();
});
it('suspension denies even an already cached Firebase token', async () => {
  const { maestroFirebaseService: service } = await import('./maestroFirebaseService');
  await service.getAppCheckToken(); mocks.suspended = true;
  expect(await service.getAppCheckToken()).toBeNull(); expect(mocks.getToken).toHaveBeenCalledTimes(1);
});
it('never starts embedded Google OAuth while Quest account linking remains unavailable', async () => {
  const { firebaseAuthBridgeService: auth } = await import('../auth/firebaseAuthBridgeService');
  await expect(auth.beginGoogleSignIn()).rejects.toThrow('Quest account linking is not configured');
  expect(mocks.popup).not.toHaveBeenCalled();
});

it('keeps the same Firebase project while using a distinct named Quest registration', async () => {
  mocks.existingApps = [{ name: '[DEFAULT]', options: { appId: '1:123:web:fedcba', projectId: 'project' } }];
  const { maestroFirebaseService: service } = await import('./maestroFirebaseService');
  await service.getApp();
  expect(mocks.createApp).toHaveBeenCalledWith(expect.objectContaining({ appId: '1:123:web:abcdef', projectId: 'project' }), 'maestro-quest');
});
it.each(['', '1:123:web:fedcba', '1:999:web:abcdef', 'malformed'])('rejects missing, shared or different-project Quest Firebase registration: %s', async value => {
  mocks.questAppId = value;
  const { maestroFirebaseService: service } = await import('./maestroFirebaseService');
  await expect(service.getApp()).rejects.toThrow('separate Quest Firebase app ID');
  expect(mocks.createApp).not.toHaveBeenCalled();
});
it('rejects an existing Quest app initialized against different registration or project', async () => {
  mocks.existingApps = [{ name: 'maestro-quest', options: { appId: 'other', projectId: 'project' } }];
  const { maestroFirebaseService: service } = await import('./maestroFirebaseService');
  await expect(service.getApp()).rejects.toThrow('configuration changed');
});
it('ordinary browser keeps its original Firebase registration', async () => {
  mocks.native = false;
  const { maestroFirebaseService: service } = await import('./maestroFirebaseService');
  await service.getApp();
  expect(mocks.createApp).toHaveBeenCalledWith(expect.objectContaining({ appId: '1:123:web:fedcba' }));
});
