// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Real Chrome microphone/worklet ownership with generated audio and local
// detector replies. No provider, credentials, real microphone or speaker output.
import { chromium } from 'playwright-core';
import { createServer } from 'node:http';
import { build } from 'vite';
import { mkdir, writeFile, readFile } from 'node:fs/promises';
import { resolve, extname, sep } from 'node:path';

const output = resolve(process.env.MAESTRO_CAPTURE_PROBE_OUTPUT || '.maestro-debug/pcm-capture');
await mkdir(output, { recursive: true });
const wav = resolve(output, 'generated-input.wav');
const rate = 48000, samples = rate * 12, data = Buffer.alloc(44 + samples * 2);
data.write('RIFF'); data.writeUInt32LE(data.length - 8, 4); data.write('WAVEfmt ', 8);
data.writeUInt32LE(16, 16); data.writeUInt16LE(1, 20); data.writeUInt16LE(1, 22);
data.writeUInt32LE(rate, 24); data.writeUInt32LE(rate * 2, 28);
data.writeUInt16LE(2, 32); data.writeUInt16LE(16, 34); data.write('data', 36);
data.writeUInt32LE(samples * 2, 40);
for (let i = 0; i < samples; i++) data.writeInt16LE(Math.round(8000 * Math.sin(2 * Math.PI * 220 * i / rate)), 44 + i * 2);
await writeFile(wav, data);
const web = resolve(output, 'web');
await build({ configFile: false, logLevel: 'warn', build: {
  outDir: web, emptyOutDir: false,
  rollupOptions: { input: resolve('test-fixtures/browser/pcm-capture.html') },
} });
const server = createServer(async (request, response) => {
  const path = resolve(web, '.' + decodeURIComponent(new URL(request.url, 'http://localhost').pathname));
  if (!path.startsWith(web + sep)) { response.writeHead(403).end(); return; }
  try {
    const data = await readFile(path);
    response.setHeader('Content-Type', extname(path) === '.html' ? 'text/html' : 'text/javascript');
    response.end(data);
  } catch { response.writeHead(404).end(); }
});
let browser, deadline;
const phases = [], requests = [];
const mark = phase => { phases.push({ phase, at: new Date().toISOString() }); console.log(JSON.stringify({ phase })); };
try {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const base = `http://127.0.0.1:${server.address().port}`;
  browser = await chromium.launch({ channel: 'chrome', headless: true, args: [
    '--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream',
    `--use-file-for-fake-audio-capture=${wav}`, '--autoplay-policy=no-user-gesture-required',
  ] });
  const context = await browser.newContext({ permissions: ['microphone'] });
  const forbidden = [], errors = [];
  await context.route('**/*', route => {
    if (new URL(route.request().url()).origin === base) return route.continue();
    forbidden.push(route.request().url()); return route.abort();
  });
  const page = await context.newPage();
  page.on('request', request => requests.push({ event: 'request', url: request.url() }));
  page.on('response', response => requests.push({ event: 'response', status: response.status(), url: response.url() }));
  page.on('requestfailed', request => requests.push({ event: 'failed', url: request.url(), error: request.failure()?.errorText }));
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(`${base}/test-fixtures/browser/pcm-capture.html`);
  await page.exposeFunction('capturePhase', mark);
  const result = await Promise.race([page.evaluate(async () => {
    const { openBrowserPcmCapture, waitForLocalSpeechTrigger } = window.captureProbeModules;
    const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
    const require = (condition, message) => { if (!condition) throw new Error(message); };
    const within = async (check, label) => {
      const deadline = performance.now() + 10000;
      while (!check() && performance.now() < deadline) await delay(20);
      require(check(), `${label} timed out`);
    };
    const tracks = [];
    const get = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
    let acquisitions = 0;
    navigator.mediaDevices.getUserMedia = async constraints => {
      const stream = await get(constraints); acquisitions++; tracks.push(...stream.getTracks()); return stream;
    };
    let direct = null, trigger = null;
    await window.capturePhase('compiled-modules-ready');
    try {
      await window.capturePhase('opening-direct');
      direct = await openBrowserPcmCapture();
      await window.capturePhase('direct-open');
      let packets = 0, sampleCount = 0, nonzero = false;
      direct.start(pcm => { packets++; sampleCount += pcm.length; nonzero ||= pcm.some(sample => sample !== 0); });
      await within(() => nonzero && sampleCount >= 6400, 'Real worklet capture');
      await window.capturePhase('direct-captured');
      await direct.flush();
      await Promise.all([direct.close(), direct.close()]);
      await window.capturePhase('direct-closed');
      const endedPackets = packets;
      await delay(150); require(packets === endedPackets, 'PCM continued after direct close');
      require(tracks.every(track => track.readyState === 'ended'), 'Direct microphone remained active');
      let transcriptions = 0;
      const abort = new AbortController();
      const timeout = setTimeout(() => abort.abort(), 15000);
      try {
        await window.capturePhase('opening-trigger');
        trigger = await waitForLocalSpeechTrigger({ signal: abort.signal,
          detector: { initialize: async () => {}, transcribe: async () => { transcriptions++; return 'hello from generated audio'; } },
        });
      } finally { clearTimeout(timeout); }
      await window.capturePhase('trigger-confirmed');
      require(transcriptions === 1 && trigger.pcm.length > 0, 'Trigger did not confirm actual captured input');
      await delay(250);
      let continued = 0;
      const handoff = trigger.capture.transferTo(pcm => { continued += pcm.length; });
      require(handoff.bufferedSamples > 0, 'Connection interval had no captured continuation');
      const beforeFlush = continued;
      await delay(150); await trigger.capture.flush();
      require(continued > beforeFlush, 'Transferred capture stopped prematurely');
      await trigger.capture.close();
      const endedContinuation = continued;
      await delay(150); require(continued === endedContinuation, 'PCM continued after handoff close');
      require(acquisitions === 2 && tracks.every(track => track.readyState === 'ended'), 'Capture ownership leaked or reopened');
      return { direct: { packets, samples: sampleCount, nonzero }, trigger: {
        confirmedSamples: trigger.pcm.length, transcriptions, handoff, continuedSamples: continued,
      }, acquisitions, allTracksEnded: true, providerUsed: false, generatedAudioOnly: true };
    } finally {
      await direct?.close(); await trigger?.capture.close();
      navigator.mediaDevices.getUserMedia = get;
      for (const track of tracks) track.stop();
    }
  }), new Promise((_, reject) => { deadline = setTimeout(() => reject(new Error('Capture probe exceeded 45 seconds')), 45000); })]);
  clearTimeout(deadline);
  if (errors.length || forbidden.length) throw new Error(`Browser errors: ${errors.length}; forbidden requests: ${forbidden.length}`);
  await writeFile(resolve(output, 'result.json'), JSON.stringify({ ...result, errors, forbiddenRequests: forbidden.length }, null, 2));
  console.log(JSON.stringify({ passed: true, ...result, output }));
} finally {
  clearTimeout(deadline);
  await browser?.close();
  await new Promise(resolve => server.close(resolve));
  await writeFile(resolve(output, 'phases.json'), JSON.stringify(phases, null, 2));
  await writeFile(resolve(output, 'requests.json'), JSON.stringify(requests, null, 2));
}
