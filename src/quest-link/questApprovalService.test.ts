// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { beforeEach, afterEach, expect, it, vi } from 'vitest';
const mocks = vi.hoisted(() => ({ identity: vi.fn(), proof: vi.fn(async () => 'web-proof'), post: vi.fn(async () => ({ approved: true })) }));
vi.mock('../core/config/integrations', () => ({ isFirebaseClientConfigured: () => true, MAESTRO_INTEGRATION_CONFIG: {
  firebaseAppCheckSiteKey: 'site-key', questAccountLinkVerificationUrl: 'https://chatwithmaestro.com/quest-link.html', questAccountLinkUrl: 'https://backend.test/link' } }));
vi.mock('../services/auth/firebaseAuthBridgeService', () => ({ firebaseAuthBridgeService: { getCurrentIdentity: mocks.identity } }));
vi.mock('../services/firebase/maestroFirebaseService', () => ({ maestroFirebaseService: { getAppCheckToken: mocks.proof } }));
vi.mock('../services/auth/questLinkProtocol', async original => ({ ...await original<typeof import('../services/auth/questLinkProtocol')>(), postQuestLink: mocks.post }));
import { questApprovalService } from './questApprovalService';
beforeEach(() => {
  vi.clearAllMocks(); const page = { top: null as unknown, location: { origin: 'https://chatwithmaestro.com', pathname: '/quest-link.html' } }; page.top = page;
  vi.stubGlobal('window', page); mocks.identity.mockResolvedValue({ user: { id: 'expected' }, firebaseIdToken: 'verified-identity' });
});
afterEach(() => vi.unstubAllGlobals());
it('approves only the displayed identity and never sends a client-chosen UID', async () => {
  await questApprovalService.approve('ABCDEFGHJK', 'expected', new AbortController().signal);
  expect(mocks.post).toHaveBeenCalledWith(expect.objectContaining({ body: { code: 'ABCDEFGHJK', confirm: true }, bearer: 'verified-identity', appCheck: 'web-proof' }));
});
it('rejects an account changed in another tab rather than approving the wrong displayed account', async () => {
  mocks.identity.mockResolvedValue({ user: { id: 'different' }, firebaseIdToken: 'other-token' });
  await expect(questApprovalService.approve('ABCDEFGHJK', 'expected', new AbortController().signal)).rejects.toMatchObject({ code: 'account-changed' });
  expect(mocks.post).not.toHaveBeenCalled();
});
it('refuses an embedded approval frame or a different page origin', () => {
  vi.stubGlobal('window', { ...window, top: {} }); expect(questApprovalService.available()).toBe(false);
});
