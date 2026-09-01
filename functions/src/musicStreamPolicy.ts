// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

export const MANAGED_MUSIC_GENERATION_TIMEOUT_MS = 90_000;
export const MANAGED_MUSIC_LEASE_BUFFER_MS = 15_000;

export const getMusicTargetSampleCount = (
  durationSeconds: number,
  sampleRate: number,
  channels: number,
): number => Math.max(0, Math.round(durationSeconds * sampleRate * channels));

export const trimMusicPcmChunk = (params: {
  pcm: Buffer;
  totalSamples: number;
  durationSeconds: number;
  sampleRate: number;
  channels: number;
}): { pcm: Buffer; acceptedSamples: number; complete: boolean } => {
  const targetSamples = getMusicTargetSampleCount(
    params.durationSeconds,
    params.sampleRate,
    params.channels,
  );
  const remainingSamples = Math.max(0, targetSamples - params.totalSamples);
  const acceptedSamples = Math.min(Math.floor(params.pcm.byteLength / 2), remainingSamples);
  return {
    pcm: params.pcm.subarray(0, acceptedSamples * 2),
    acceptedSamples,
    complete: params.totalSamples + acceptedSamples >= targetSamples,
  };
};

export const isCompleteMusicSampleCount = (params: {
  sampleCount: number;
  durationSeconds: number;
  sampleRate: number;
  channels: number;
}): boolean => params.sampleCount >= getMusicTargetSampleCount(
  params.durationSeconds,
  params.sampleRate,
  params.channels,
);

export const getManagedMusicLeaseDurationMs = (
  configuredLifetimeSeconds: number,
): number => Math.max(
  Math.max(0, configuredLifetimeSeconds) * 1_000,
  MANAGED_MUSIC_GENERATION_TIMEOUT_MS + MANAGED_MUSIC_LEASE_BUFFER_MS,
);
