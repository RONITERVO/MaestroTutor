// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** Managed Lyria capture, sample completion, service-fee settlement and lease cleanup. */

import { GoogleGenAI } from '@google/genai';
import { buildMusicPrompt } from '../../../shared/prompts/music';
import type { AppUser } from '../auth';
import { appConfig } from '../config';
import {
  requireAllowedManagedModel
} from '../geminiPolicy';
import { createHttpError } from '../http';
import {
  releaseManagedReservation,
  reserveManagedCredits,
  settleManagedReservation
} from '../managedBilling';
import {
  MANAGED_MUSIC_GENERATION_TIMEOUT_MS,
  getManagedMusicLeaseDurationMs,
  isCompleteMusicSampleCount,
  trimMusicPcmChunk,
} from '../musicStreamPolicy';
import {
  creditsToUsd
} from '../pricing';
import { releaseManagedLiveLease, reserveManagedLiveLease } from './liveLeases';

const MANAGED_MUSIC_MIN_DURATION_SECONDS = 8;

const MANAGED_MUSIC_MAX_DURATION_SECONDS = 20;

const MANAGED_MUSIC_SAMPLE_RATE = 48_000;

const MANAGED_MUSIC_CHANNELS = 2;

const MANAGED_MUSIC_SETUP_TIMEOUT_MS = 12_000;

const readMusicErrorMessage = (value: unknown): string => {
  if (value instanceof Error && value.message) return value.message;
  if (value && typeof value === 'object') {
    const event = value as { message?: unknown; error?: unknown; reason?: unknown };
    if (typeof event.message === 'string' && event.message) return event.message;
    if (event.error instanceof Error && event.error.message) return event.error.message;
    if (typeof event.reason === 'string' && event.reason) return event.reason;
  }
  return 'Music generation failed.';
};

const parseMusicMimeInteger = (mimeType: string | undefined, name: string): number | undefined => {
  const match = (mimeType || '').match(new RegExp(`${name}=([0-9]+)`, 'i'));
  if (!match) return undefined;
  const parsed = Number(match[1]);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : undefined;
};

const generateMusicPcm = async (params: {
  model: string;
  prompt: string;
  durationSeconds: number;
}): Promise<{
  pcmBase64: string;
  durationSeconds: number;
  sampleRate: number;
  channels: number;
  sampleCount: number;
}> => new Promise((resolve, reject) => {
  const client = new GoogleGenAI({
    apiKey: appConfig.geminiApiKey,
    apiVersion: 'v1alpha',
  });
  let session: any = null;
  let settled = false;
  let setupComplete = false;
  let startingPlayback = false;
  let playbackStarted = false;
  let sampleRate = MANAGED_MUSIC_SAMPLE_RATE;
  let channels = MANAGED_MUSIC_CHANNELS;
  let totalSamples = 0;
  let lastWarning = '';
  const chunks: Buffer[] = [];
  let setupTimer: ReturnType<typeof setTimeout> | null = null;
  let generationTimer: ReturnType<typeof setTimeout> | null = null;
  let finalizeTimer: ReturnType<typeof setTimeout> | null = null;

  const cleanup = () => {
    if (setupTimer) clearTimeout(setupTimer);
    if (generationTimer) clearTimeout(generationTimer);
    if (finalizeTimer) clearTimeout(finalizeTimer);
    setupTimer = null;
    generationTimer = null;
    finalizeTimer = null;
  };
  const close = () => {
    try { session?.close(); } catch { }
  };
  const fail = (error: unknown) => {
    if (settled) return;
    settled = true;
    cleanup();
    close();
    reject(createHttpError(502, readMusicErrorMessage(error)));
  };
  const finish = () => {
    if (settled) return;
    const pcm = Buffer.concat(chunks);
    if (!pcm.length) {
      fail(new Error('No music audio was generated.'));
      return;
    }
    const sampleCount = Math.floor(pcm.byteLength / 2);
    if (!isCompleteMusicSampleCount({
      sampleCount,
      durationSeconds: params.durationSeconds,
      sampleRate,
      channels,
    })) {
      fail(new Error('Music generation ended before the requested duration was complete.'));
      return;
    }
    settled = true;
    cleanup();
    close();
    resolve({
      pcmBase64: pcm.toString('base64'),
      durationSeconds: sampleCount / Math.max(1, sampleRate * channels),
      sampleRate,
      channels,
      sampleCount,
    });
  };
  const startPlayback = async () => {
    if (settled || playbackStarted || startingPlayback || !session || !setupComplete) return;
    startingPlayback = true;
    try {
      await session.setWeightedPrompts({
        weightedPrompts: [{
          text: buildMusicPrompt(params.prompt),
          weight: 1,
        }],
      });
      await session.setMusicGenerationConfig({
        musicGenerationConfig: {
          musicGenerationMode: 'QUALITY',
          temperature: 1.1,
          guidance: 4,
        },
      });
      session.play();
      playbackStarted = true;
    } catch (error) {
      fail(error);
    } finally {
      startingPlayback = false;
    }
  };

  setupTimer = setTimeout(
    () => fail(new Error('Music generation setup timed out before setupComplete.')),
    MANAGED_MUSIC_SETUP_TIMEOUT_MS,
  );
  generationTimer = setTimeout(() => {
    if (chunks.length > 0) finish();
    else fail(new Error('Music generation timed out.'));
  }, MANAGED_MUSIC_GENERATION_TIMEOUT_MS);

  void client.live.music.connect({
    model: params.model,
    callbacks: {
      onmessage: (message: any) => {
        if (settled) return;
        if (typeof message?.warning === 'string' && message.warning.trim()) {
          lastWarning = message.warning.trim();
        }
        if (message?.setupComplete) {
          setupComplete = true;
          if (setupTimer) clearTimeout(setupTimer);
          setupTimer = null;
          void startPlayback();
        }
        const filteredReason = message?.filteredPrompt?.filteredReason;
        if (filteredReason && chunks.length === 0) {
          fail(new Error(`Music prompt was filtered: ${filteredReason}`));
          return;
        }
        const audioChunks = Array.isArray(message?.serverContent?.audioChunks)
          ? message.serverContent.audioChunks
          : [];
        for (const chunk of audioChunks) {
          if (settled || typeof chunk?.data !== 'string' || !chunk.data) continue;
          sampleRate = parseMusicMimeInteger(chunk.mimeType, 'rate')
            || parseMusicMimeInteger(chunk.mimeType, 'sampleRate')
            || sampleRate;
          channels = parseMusicMimeInteger(chunk.mimeType, 'channels') || channels;
          const trimmed = trimMusicPcmChunk({
            pcm: Buffer.from(chunk.data, 'base64'),
            totalSamples,
            durationSeconds: params.durationSeconds,
            sampleRate,
            channels,
          });
          if (trimmed.acceptedSamples > 0) {
            chunks.push(trimmed.pcm);
            totalSamples += trimmed.acceptedSamples;
          }
          if (trimmed.complete) {
            try { session?.pause(); } catch { }
            if (finalizeTimer) clearTimeout(finalizeTimer);
            finalizeTimer = setTimeout(finish, 250);
            break;
          }
        }
      },
      onerror: (error: unknown) => fail(error),
      onclose: () => {
        if (settled) return;
        if (chunks.length > 0) finish();
        else fail(new Error(lastWarning || (setupComplete
          ? 'Lyria RealTime stream closed before generating audio.'
          : 'Lyria RealTime closed before setup completed.')));
      },
    },
  }).then((connectedSession) => {
    if (settled) {
      try { connectedSession?.close(); } catch { }
      return;
    }
    session = connectedSession;
    void startPlayback();
  }).catch(fail);
});

/**
 * Run managed Lyria on the trusted backend. Lyria's music WebSocket currently
 * rejects Gemini ephemeral tokens, so forwarding a token to browser/CLI would
 * bill a session that can never start. This route keeps the durable API key
 * server-side and returns the same PCM boundary to every managed client.
 */
export const generateManagedMusic = async (params: {
  uid: string;
  user: AppUser;
  model: string;
  prompt: string;
  durationSeconds?: number;
}) => {
  if (!appConfig.geminiApiKey) {
    throw createHttpError(500, 'GEMINI_API_KEY is not configured on the backend.');
  }
  const prompt = params.prompt.trim();
  if (!prompt) throw createHttpError(400, 'Music prompt is empty.');
  if (prompt.length > 4_000) throw createHttpError(400, 'Music prompt is too long.');
  const model = requireAllowedManagedModel(
    params.model,
    appConfig.managedAllowedMusicModels,
    'music',
  );
  const durationSeconds = Math.max(
    MANAGED_MUSIC_MIN_DURATION_SECONDS,
    Math.min(
      MANAGED_MUSIC_MAX_DURATION_SECONDS,
      Math.round(Number(params.durationSeconds) || MANAGED_MUSIC_MIN_DURATION_SECONDS),
    ),
  );
  // Lyria RealTime has no published provider tariff. This is the explicit
  // managed-service fee, not an invented provider usage estimate.
  const fixedCredits = appConfig.managedMusicSessionCredits;
  const billedUsd = creditsToUsd(fixedCredits);
  const lease = await reserveManagedLiveLease({
    uid: params.uid,
    purpose: 'music',
    durationMs: getManagedMusicLeaseDurationMs(appConfig.managedLiveTokenLifetimeSeconds),
  });

  let reservation: Awaited<ReturnType<typeof reserveManagedCredits>>;
  try {
    reservation = await reserveManagedCredits({
      uid: params.uid,
      user: params.user,
      operation: 'liveTokenMusic',
      model,
      estimatedCredits: fixedCredits,
      estimatedUsd: billedUsd,
      metadata: { purpose: 'music', chargeBasis: 'managed-service-fee', leaseId: lease.leaseId, requestedDurationSeconds: durationSeconds },
    });
  } catch (error) {
    await releaseManagedLiveLease(params.uid, lease.leaseId).catch(() => undefined);
    throw error;
  }

  try {
    const generated = await generateMusicPcm({ model, prompt, durationSeconds });
    const billingSummary = await settleManagedReservation({
      uid: params.uid,
      reservationId: reservation.reservationId,
      billedCredits: fixedCredits,
      billedUsd,
      operation: 'liveTokenMusic',
      model,
      metadata: {
        purpose: 'music',
        chargeBasis: 'managed-service-fee',
        leaseId: lease.leaseId,
        requestedDurationSeconds: durationSeconds,
        generatedDurationSeconds: generated.durationSeconds,
        sampleRate: generated.sampleRate,
        channels: generated.channels,
      },
    });
    return { ...generated, billingSummary };
  } catch (error) {
    await releaseManagedReservation(params.uid, reservation.reservationId, 'music-generation-failed')
      .catch(() => undefined);
    throw error;
  } finally {
    await releaseManagedLiveLease(params.uid, lease.leaseId).catch(() => undefined);
  }
};
