// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Muted, synthetic saved speech in the real browser decoder and output graph.
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createServer } from 'vite';
import { chromium } from 'playwright-core';

const evidence = resolve('.quest-evidence/spatial-voice/chrome-cached-speech.json');
const server = await createServer({ configFile: false, cacheDir: '.quest-evidence/spatial-voice/replay-vite-cache',
  optimizeDeps: { noDiscovery: true, include: [] }, server: { host: '127.0.0.1', port: 0, watch: null }, logLevel: 'error' });
let browser, deadline;
try {
  await server.listen();
  const base = `http://127.0.0.1:${server.httpServer.address().port}`;
  browser = await chromium.launch({ channel: 'chrome', headless: true,
    args: ['--mute-audio', '--autoplay-policy=no-user-gesture-required'] });
  deadline = setTimeout(() => { void browser?.close(); }, 60000);
  const context = await browser.newContext(), errors = [];
  await context.route('**/*', route => new URL(route.request().url()).origin === base ? route.continue() : route.abort());
  await context.route(`${base}/_cached-speech`, route => route.fulfill({ contentType: 'text/html', body: '<!doctype html><title>Cached speech test</title>' }));
  const page = await context.newPage(); page.on('pageerror', error => errors.push(error.message));
  await page.goto(`${base}/_cached-speech`);
  const result = await page.evaluate(async () => {
    const { pcmToWav } = await import('/src/core-sdk/media/audioProcessing.ts');
    const { decodeCachedSpeech, playCachedSpeech } = await import('/src/features/speech/utils/cachedSpeechPlayback.ts');
    // Stereo 32 kHz cache deliberately differs from both native mono 24 kHz and
    // the physical/browser output rates. Downmixing must not double loudness.
    const inputRate = 32000, duration = .25, samples = new Int16Array(inputRate * duration * 2);
    for (let frame = 0; frame < samples.length / 2; frame++) {
      samples[frame * 2] = Math.round(Math.sin(frame / inputRate * 440 * Math.PI * 2) * 8192);
      samples[frame * 2 + 1] = 0;
    }
    const dataUrl = pcmToWav(samples, inputRate, 2), decoded = await decodeCachedSpeech(dataUrl);
    const decodedPeak = decoded.reduce((peak, value) => Math.max(peak, Math.abs(value)), 0), outputs = [];
    for (const sampleRate of [24000, 48000]) {
      const context = new AudioContext({ sampleRate }); await context.resume();
      try {
        const analyser = context.createAnalyser(), original = context.createBufferSource.bind(context);
        let sources = 0, watching = true, peak = 0;
        context.createBufferSource = () => { sources++; const node = original(); node.connect(analyser); return node; };
        const measure = (async () => { const data = new Float32Array(analyser.fftSize);
          while (watching) { analyser.getFloatTimeDomainData(data); peak = Math.max(peak, ...data.map(Math.abs)); await new Promise(resolve => setTimeout(resolve, 10)); }
        })();
        let completed, cancelled, fresh, elapsed;
        try {
          const start = performance.now();
          completed = await playCachedSpeech({ audioDataUrl: dataUrl, getAudioContext: async () => context, signal: new AbortController().signal });
          elapsed = performance.now() - start;
          const abort = new AbortController();
          const pending = playCachedSpeech({ audioDataUrl: dataUrl, getAudioContext: async () => context, signal: abort.signal });
          await new Promise(resolve => setTimeout(resolve, 40)); abort.abort(); cancelled = await pending;
          fresh = await playCachedSpeech({ audioDataUrl: dataUrl, getAudioContext: async () => context, signal: new AbortController().signal });
        } finally { watching = false; await measure; }
        outputs.push({ sampleRate: context.sampleRate, completed, cancelled, fresh, elapsed, sources, peak, contextState: context.state });
      } finally { await context.close(); }
    }
    return { decodedLength: decoded.length, decodedPeak, outputs };
  });
  await mkdir(resolve(evidence, '..'), { recursive: true });
  await writeFile(evidence, JSON.stringify({ browser: browser.version(), result, errors,
    boundary: 'Actual browser decoding/resampling, mono conversion, render thread, output tail and cancellation. Muted synthetic caches; no provider, Android transport or physical headset hearing.' }, null, 2));
  assert.deepEqual(errors, []); assert.equal(result.decodedLength, 6000);
  assert(result.decodedPeak > 4000 && result.decodedPeak < 4200);
  for (const output of result.outputs) {
    assert.equal(output.completed, 'drained'); assert.equal(output.cancelled, 'cancelled'); assert.equal(output.fresh, 'drained');
    assert.equal(output.contextState, 'running'); assert.equal(output.sources, 3);
    assert(output.elapsed >= 250, 'Replay finished before its audio duration'); assert(output.peak > .1 && output.peak < .14);
  }
  console.log(`Cached speech browser probe passed at 24/48 kHz. Evidence: ${evidence}`);
} finally { clearTimeout(deadline); await browser?.close(); await server.close(); }
