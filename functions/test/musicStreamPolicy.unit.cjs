// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

const assert = require('node:assert/strict');
const test = require('node:test');
const {
  MANAGED_MUSIC_GENERATION_TIMEOUT_MS,
  getManagedMusicLeaseDurationMs,
  isCompleteMusicSampleCount,
  trimMusicPcmChunk,
} = require('../lib/functions/src/musicStreamPolicy.js');

test('managed music trims the final PCM chunk to the exact requested duration', () => {
  const input = Buffer.alloc(12);
  const result = trimMusicPcmChunk({
    pcm: input,
    totalSamples: 6,
    durationSeconds: 8,
    sampleRate: 1,
    channels: 1,
  });

  assert.equal(result.acceptedSamples, 2);
  assert.equal(result.pcm.byteLength, 4);
  assert.equal(result.complete, true);
  assert.equal(isCompleteMusicSampleCount({
    sampleCount: 8,
    durationSeconds: 8,
    sampleRate: 1,
    channels: 1,
  }), true);
  assert.equal(isCompleteMusicSampleCount({
    sampleCount: 7,
    durationSeconds: 8,
    sampleRate: 1,
    channels: 1,
  }), false);
});

test('managed music lease always outlives the generation timeout', () => {
  assert.ok(getManagedMusicLeaseDurationMs(30) > MANAGED_MUSIC_GENERATION_TIMEOUT_MS);
  assert.equal(getManagedMusicLeaseDurationMs(180), 180_000);
});
