// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  resolveAccessMode: vi.fn(),
  runManaged: vi.fn(),
  runByok: vi.fn(),
  trackMusicGeneration: vi.fn(),
  getAi: vi.fn(async () => ({ direct: true })),
  backend: { managed: true },
  complete: vi.fn(),
  error: vi.fn(),
}));

vi.mock('../../core-sdk/media/musicGeneration', async importOriginal => ({
  ...(await importOriginal<typeof import('../../core-sdk/media/musicGeneration')>()),
  runCoreManagedMusicGeneration: mocks.runManaged,
  runCoreMusicGeneration: mocks.runByok,
}));
vi.mock('../../features/diagnostics', () => ({
  debugLogService: { logRequest: () => ({ complete: mocks.complete, error: mocks.error }) },
}));
vi.mock('../../shared/utils/costTracker', () => ({
  trackMusicGeneration: mocks.trackMusicGeneration,
}));
vi.mock('./client', () => ({ getAi: mocks.getAi }));
vi.mock('../../services/access/maestroAccessService', () => ({
  maestroAccessService: { resolveAccessMode: mocks.resolveAccessMode },
}));
vi.mock('../../services/backend/maestroBackendService', () => ({
  maestroBackendService: mocks.backend,
}));

import { generateMusic } from './music';

const result = {
  operationId: 'music-1',
  dataUrl: 'data:audio/wav;base64,UklGRg==',
  mimeType: 'audio/wav',
  durationSeconds: 8,
  sampleRate: 48_000,
  channels: 2,
  sampleCount: 768_000,
};

describe('Gemini music provider accounting', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.runManaged.mockResolvedValue(result);
    mocks.runByok.mockResolvedValue(result);
  });

  it('does not add local BYOK cost estimates to backend-settled managed music', async () => {
    mocks.resolveAccessMode.mockResolvedValue('managed');

    await generateMusic({ prompt: 'Managed exercise', streamPlayback: false });

    expect(mocks.runManaged).toHaveBeenCalledWith(expect.objectContaining({
      backend: mocks.backend,
      prompt: 'Managed exercise',
    }));
    expect(mocks.runByok).not.toHaveBeenCalled();
    expect(mocks.trackMusicGeneration).not.toHaveBeenCalled();
  });

  it('keeps local provider cost estimation on the direct BYOK path', async () => {
    mocks.resolveAccessMode.mockResolvedValue('byok');

    await generateMusic({ prompt: 'Direct exercise', streamPlayback: false });

    expect(mocks.runByok).toHaveBeenCalledWith(expect.objectContaining({
      aiClient: { direct: true },
      prompt: 'Direct exercise',
    }));
    expect(mocks.runManaged).not.toHaveBeenCalled();
    expect(mocks.trackMusicGeneration).toHaveBeenCalledOnce();
  });
});
