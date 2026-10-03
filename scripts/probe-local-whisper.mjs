// Real WASM inference through the production worker after dependency updates.
// Uses generated, non-personal fixture audio. No Gemini key or provider session.
import { chromium } from 'playwright-core';
import { preview } from 'vite';
import { readdir, readFile, mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
const out = '.quest-evidence/quest-privacy/whisper';
await mkdir(out, { recursive: true });
const workers = (await readdir('dist/assets')).filter(name => /^observerWhisper\.worker-.*\.js$/.test(name));
assert.equal(workers.length, 1, 'Build the app first');
const audio = [...await readFile('test-fixtures/audio/long-live-generated.wav')];
const server = await preview({ preview: { host: '127.0.0.1', port: 5192, strictPort: true } });
let browser;
try {
  browser = await chromium.launch({ channel: 'chrome', headless: true });
  const context = await browser.newContext();
  const blocked = [];
  await context.route('**/*', route => {
    const request = route.request(), host = new URL(request.url()).hostname;
    if (host === '127.0.0.1' || (request.method() === 'GET' && (host === 'huggingface.co' || host.endsWith('.huggingface.co') || host.endsWith('.hf.co') || host === 'cdn.jsdelivr.net'))) return route.continue();
    blocked.push({ host, method: request.method() }); return route.abort();
  });
  const page = await context.newPage();
  await page.goto('http://127.0.0.1:5192/privacy.html');
  const result = await page.evaluate(async ({ worker, audio }) => {
    const decoder = new AudioContext({ sampleRate: 16000 });
    const decoded = await decoder.decodeAudioData(new Uint8Array(audio).buffer);
    const samples = decoded.getChannelData(0).slice(0, 16000 * 8);
    await decoder.close();
    return await new Promise((resolve, reject) => {
      const messages = [], start = performance.now();
      const instance = new Worker('/assets/' + worker);
      const finish = (error, value) => { clearTimeout(timeout); instance.terminate(); error ? reject(error) : resolve(value); };
      const timeout = setTimeout(() => finish(new Error('Whisper model/inference timeout')), 300000);
      instance.onerror = event => finish(new Error(event.message));
      instance.onmessage = ({ data }) => {
        if (data.kind !== 'loading' || data.progress === undefined) messages.push(data);
        if (data.kind === 'error') finish(new Error(data.message));
        if (data.kind === 'ready') instance.postMessage({ kind: 'transcribe', requestId: 1, audio: samples.buffer }, [samples.buffer]);
        if (data.kind === 'result') finish(null, { ...data, messages, totalMs: Math.round(performance.now() - start), sampleRate: decoded.sampleRate, duration: 8 });
      };
      instance.postMessage({ kind: 'init', model: 'onnx-community/whisper-tiny.en', allowFp32Fallback: false });
    });
  }, { worker: workers[0], audio });
  assert.equal(result.sampleRate, 16000);
  assert.match(result.text.toLowerCase(), /hello/); assert.match(result.text.toLowerCase(), /how are you/);
  assert.ok(result.messages.some(message => message.kind === 'ready' && message.profile === 'q4'));
  assert.equal(blocked.length, 0, JSON.stringify(blocked));
  await writeFile(`${out}/evidence.json`, JSON.stringify({ passed: true, worker: workers[0], result, blocked, realHeadset: false }, null, 2));
  console.log(JSON.stringify(result));
} finally {
  await browser?.close();
  await new Promise(resolve => server.httpServer.close(resolve));
}
