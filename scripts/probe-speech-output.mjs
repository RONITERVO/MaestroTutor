// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Real Chromium AudioWorklet rendering; muted synthetic PCM, no provider or account.
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createServer } from 'vite';
import { chromium } from 'playwright-core';

const evidence = resolve('.quest-evidence/spatial-voice/chrome-output.json');
// This probe needs only the output modules. Do not crawl every app fixture or
// share the app's dependency-optimizer cache with an unrelated running server.
const server = await createServer({ configFile: false, cacheDir: '.quest-evidence/spatial-voice/vite-cache',
  optimizeDeps: { noDiscovery: true, include: [] }, server: { host: '127.0.0.1', port: 0, watch: null }, logLevel: 'error' });
let browser;
let deadline;
try {
  await server.listen();
  const base = `http://127.0.0.1:${server.httpServer.address().port}`;
  browser = await chromium.launch({ channel: 'chrome', headless: true,
    args: ['--mute-audio', '--autoplay-policy=no-user-gesture-required'] });
  deadline = setTimeout(() => { void browser?.close(); }, 60000);
  const context = await browser.newContext(); const errors = [];
  await context.route('**/*', route => new URL(route.request().url()).origin === base ? route.continue() : route.abort());
  await context.route(`${base}/_speech-output`, route => route.fulfill({ contentType: 'text/html', body: '<!doctype html><title>Speech output test</title>' }));
  const page = await context.newPage(); page.on('pageerror', error => errors.push(error.message));
  await page.goto(`${base}/_speech-output`);
  console.log('Speech probe page ready; loading isolated output modules.');
  const result = await page.evaluate(async () => {
    const { WorkletSpeechOutput } = await import('/src/features/speech/utils/workletSpeechOutput.ts');
    const { PCM_PLAYBACK_PROCESSOR_URL, PCM_PLAYBACK_PROCESSOR_NAME } = await import('/src/features/speech/worklets/index.ts');
    const results = [];
    for (const sampleRate of [24000, 48000]) {
      const context = new AudioContext({ sampleRate });
      let output, watchdog;
      try {
        results.push(await Promise.race([new Promise((_, reject) => {
          watchdog = setTimeout(() => reject(new Error('Audio output did not settle within 15 seconds')), 15000);
        }), (async () => {
          await context.audioWorklet.addModule(PCM_PLAYBACK_PROCESSOR_URL); await context.resume();
          const node = new AudioWorkletNode(context, PCM_PLAYBACK_PROCESSOR_NAME, { numberOfInputs: 0, numberOfOutputs: 1, outputChannelCount: [1] });
          const analyser = context.createAnalyser(); node.connect(analyser);
          const events = []; output = new WorkletSpeechOutput(context, node, { onEvent: event => events.push(event) });
          const pcm = new Int16Array(4800).fill(1024); const started = performance.now();
          output.write(pcm); pcm.fill(0);
          const first = output.drain(); output.write(new Int16Array(24000).fill(512));
          let peak = 0, watching = true;
          const measure = (async () => { const data = new Float32Array(analyser.fftSize);
            while (watching) { analyser.getFloatTimeDomainData(data); peak = Math.max(peak, ...data.map(Math.abs)); await new Promise(resolve => setTimeout(resolve, 10)); }
          })();
          const firstResult = await first; const firstRead = output.read(); const firstMs = performance.now() - started;
          const secondResult = await output.drain(); watching = false; await measure;
          const full = output.read(); const fullMs = performance.now() - started;
          output.write(new Int16Array(12000)); const cancelled = output.drain(); output.reset();
          const cancelledResult = await cancelled; const afterReset = output.read();
          output.write(new Int16Array(240)); const freshResult = await output.drain(); const fresh = output.read();
          return { requestedRate: sampleRate, deviceRate: context.sampleRate, peak, events,
            firstResult, firstRead, firstMs, secondResult, full, fullMs, cancelledResult, afterReset, freshResult, fresh };
        })()]));
      } finally { clearTimeout(watchdog); output?.dispose(); await context.close(); }
    }
    return results;
  });
  await mkdir(resolve(evidence, '..'), { recursive: true });
  await writeFile(evidence, JSON.stringify({ browser: browser.version(), result, errors,
    boundary: 'Real Chromium render thread and analyser; muted synthetic PCM, no Gemini calls, Unity bridge or physical hearing/AEC acceptance.' }, null, 2));
  assert.deepEqual(errors, []);
  for (const run of result) {
    assert.equal(run.deviceRate, run.requestedRate); assert(run.peak > .01);
    assert.equal(run.firstResult, 'drained'); assert(run.firstRead.playedSamples >= 4800);
    assert(run.firstRead.playedSamples < run.firstRead.submittedSamples, 'The first fence waited for later audio');
    assert.equal(run.secondResult, 'drained'); assert.equal(run.full.playedSamples, 28800);
    assert.equal(run.cancelledResult, 'cancelled'); assert.equal(run.afterReset.playedSamples, 0);
    assert.equal(run.freshResult, 'drained'); assert.equal(run.fresh.playedSamples, 240);
    assert(run.events.includes('started'));
  }
  console.log(`Chrome speech output passed at 24/48 kHz: PCM, fences, output tail, reset and reuse. Evidence: ${evidence}`);
} finally { clearTimeout(deadline); await browser?.close(); await server.close(); }
